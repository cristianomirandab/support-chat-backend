using SupportChat.Domain.Models;

namespace SupportChat.Domain.Services;

public sealed class CapacityCalculator
{
    public int MaxConcurrentForAgent(Seniority s)
    {        
        var multiplier = s switch
        {
            Seniority.Junior => 0.4m,
            Seniority.Mid => 0.6m,
            Seniority.Senior => 0.8m,
            Seniority.TeamLead => 0.5m,
            _ => 0m
        };

        return (int)Math.Floor(10m * multiplier);
    }

    public int TeamCapacity(IEnumerable<Agent> agents)
        => agents.Where(a => a.AcceptingNewChats)
                 .Sum(a => MaxConcurrentForAgent(a.Seniority));

    public int MaxQueueSize(int teamCapacity)
        => (int)Math.Floor(teamCapacity * 1.5m);
}
