using System.Collections.Concurrent;
using SupportChat.Domain.Models;

namespace SupportChat.Infrastructure.Stores;

public sealed class InMemoryAgentStore
{
    private readonly ConcurrentDictionary<Guid, Agent> _agents = new();

    public void Upsert(Agent agent) => _agents[agent.Id] = agent;

    public IEnumerable<Agent> All() => _agents.Values;

    public Agent? Get(Guid id) => _agents.TryGetValue(id, out var a) ? a : null;
}
