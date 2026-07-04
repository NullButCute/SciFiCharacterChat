namespace SciFiCharacterChat.Services;

public class VisitorContext(IHttpContextAccessor httpContextAccessor)
{
    public string VisitorId { get; } = httpContextAccessor.HttpContext?.Items["VisitorId"] as string
            ?? Guid.NewGuid().ToString("N");
}