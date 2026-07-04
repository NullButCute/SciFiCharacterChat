namespace SciFiCharacterChat.Models;

public record SciFiCharacter(
    string Id, string Name, string Tagline, string Icon,
    string AccentColor, string SystemPrompt, string OpeningLine);