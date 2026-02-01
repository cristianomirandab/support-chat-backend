using SupportChat.Domain.Models;

namespace SupportChat.Domain.Services;

public sealed class RoundRobinAssigner
{
    private readonly CapacityCalculator _capacity;
    private readonly object _lock = new();
    private readonly Dictionary<(Teams Team, Seniority Seniority), int> _rrIndex = new();

    // Junior -> Mid -> Senior -> TeamLead
    private static readonly Seniority[] Preference = new[]
    {
        Seniority.Junior, Seniority.Mid, Seniority.Senior, Seniority.TeamLead
    };

    public RoundRobinAssigner(CapacityCalculator capacity)
    {
        _capacity = capacity;
    }

    public Agent? TryPickNextAgent(IEnumerable<Agent> agents, Teams team)
    {
        var teamAgents = agents.Where(a => a.Team == team && a.AcceptingNewChats).ToList();
        if (teamAgents.Count == 0) return null;

        foreach (var seniority in Preference)
        {
            var candidates = teamAgents.Where(a => a.Seniority == seniority).ToList();
            if (candidates.Count == 0) continue;
                        
            var available = candidates
                .Where(a => a.ActiveChatsCount < _capacity.MaxConcurrentForAgent(a.Seniority))
                .ToList();

            if (available.Count == 0) continue;

            lock (_lock)
            {
                var key = (team, seniority);
                _rrIndex.TryGetValue(key, out var idx);
                                
                for (int i = 0; i < available.Count; i++)
                {
                    var pick = available[(idx + i) % available.Count];
                    _rrIndex[key] = (idx + i + 1) % available.Count;
                    return pick;
                }
            }
        }

        return null;
    }
}
