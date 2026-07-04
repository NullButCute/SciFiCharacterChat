using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using SciFiCharacterChat.Data;
using SciFiCharacterChat.Models;

namespace SciFiCharacterChat.Services;

public class ChatServiceException(string message, Exception inner) : Exception(message, inner);

public class CharacterChatService(
    IChatClient chatClient,
    IDbContextFactory<ChatDbContext> dbFactory,
    VisitorContext visitor)
{
    public async Task<List<DisplayMessage>> GetHistoryAsync(string characterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Messages
            .Where(m => m.VisitorId == visitor.VisitorId && m.CharacterId == characterId)
            .OrderBy(m => m.Id)
            .ToListAsync();

        if (existing.Count == 0)
        {
            var character = CharacterCatalog.Find(characterId);
            if (character is not null)
            {
                var opener = new StoredMessage
                {
                    VisitorId = visitor.VisitorId,
                    CharacterId = characterId,
                    Role = "assistant",
                    Content = character.OpeningLine
                };
                db.Messages.Add(opener);
                await db.SaveChangesAsync();
                existing = [opener];
            }
        }

        return existing
            .Select(m => new DisplayMessage(m.Role == "user", m.Content, m.CreatedAt))
            .ToList();
    }

    public async Task ClearHistoryAsync(string characterId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = db.Messages.Where(m => m.VisitorId == visitor.VisitorId && m.CharacterId == characterId);
        db.Messages.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        string characterId, string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var character = CharacterCatalog.Find(characterId)
            ?? throw new ArgumentException($"Unknown character: {characterId}");

        var priorHistory = await GetHistoryAsync(characterId);
        var promptMessages = new List<ChatMessage> { new(ChatRole.System, character.SystemPrompt) };
        promptMessages.AddRange(priorHistory.Select(m =>
            new ChatMessage(m.IsUser ? ChatRole.User : ChatRole.Assistant, m.Text)));
        promptMessages.Add(new ChatMessage(ChatRole.User, userMessage));

        await using var db = await dbFactory.CreateDbContextAsync();
        db.Messages.Add(new StoredMessage
        {
            VisitorId = visitor.VisitorId,
            CharacterId = characterId,
            Role = "user",
            Content = userMessage
        });
        await db.SaveChangesAsync(CancellationToken.None); // keep what the user sent, even on an immediate Stop

        var reply = new StringBuilder();
        ChatServiceException? failure = null;

        var enumerator = chatClient.GetStreamingResponseAsync(promptMessages, cancellationToken: ct)
            .GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync()) break;
                    update = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    break; // Stop button pressed — keep whatever streamed so far
                }
                catch (Exception ex)
                {
                    failure = new ChatServiceException(
                        "Signal lost mid-transmission. The channel may be rate-limited — try again shortly.", ex);
                    break;
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    reply.Append(update.Text);
                    yield return update.Text;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (reply.Length > 0)
        {
            db.Messages.Add(new StoredMessage
            {
                VisitorId = visitor.VisitorId,
                CharacterId = characterId,
                Role = "assistant",
                Content = reply.ToString()
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        if (failure is not null) throw failure;
    }
}