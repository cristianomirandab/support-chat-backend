namespace SupportChat.Domain.Models;

public sealed class Agent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Teams Team { get; init; }
    public Seniority Seniority { get; init; }

    public DateTimeOffset ShiftStart { get; set; }
    public DateTimeOffset ShiftEnd { get; set; }

    public bool AcceptingNewChats { get; set; } = true;
    
    public int ActiveChatsCount { get; set; }
}
