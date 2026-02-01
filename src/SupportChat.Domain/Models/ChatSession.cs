namespace SupportChat.Domain.Models;

public sealed class ChatSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public ChatStatus Status { get; set; } = ChatStatus.Queued;

    public Guid? AssignedAgentId { get; set; }
    public Teams? AssignedTeam { get; set; }

    public DateTimeOffset? LastPollAt { get; set; }
}
