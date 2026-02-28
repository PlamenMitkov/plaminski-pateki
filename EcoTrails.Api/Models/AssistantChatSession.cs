namespace EcoTrails.Api.Models;

public class AssistantChatSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? AppUserId { get; set; }
    public string Title { get; set; } = "Нова сесия";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public ICollection<AssistantChatEntry> Messages { get; set; } = new List<AssistantChatEntry>();
}

public class AssistantChatEntry
{
    public int Id { get; set; }
    public int SessionInternalId { get; set; }
    public AssistantChatSession Session { get; set; } = null!;
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
