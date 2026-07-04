using Microsoft.EntityFrameworkCore;

namespace SciFiCharacterChat.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<StoredMessage> Messages => Set<StoredMessage>();
}

public class StoredMessage
{
    public int Id { get; set; }
    public required string VisitorId { get; set; }
    public required string CharacterId { get; set; }
    public required string Role { get; set; }        // "user" | "assistant"
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}