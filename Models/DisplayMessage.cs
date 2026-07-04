namespace SciFiCharacterChat.Models;

public record DisplayMessage(bool IsUser, string Text, DateTimeOffset Timestamp);