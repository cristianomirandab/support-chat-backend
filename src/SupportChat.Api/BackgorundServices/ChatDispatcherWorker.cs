using SupportChat.Domain.Models;
using SupportChat.Domain.Services;
using SupportChat.Infrastructure.Stores;

namespace SupportChat.Api.BackgroundServices;

public sealed class ChatDispatcherWorker : BackgroundService
{
    private readonly InMemoryChatStore _chats;
    private readonly InMemoryAgentStore _agents;
    private readonly RoundRobinAssigner _assigner;
    private readonly int _tickMs;

    public ChatDispatcherWorker(
        InMemoryChatStore chats,
        InMemoryAgentStore agents,
        RoundRobinAssigner assigner,
        IConfiguration cfg)
    {
        _chats = chats;
        _agents = agents;
        _assigner = assigner;
        _tickMs = int.Parse(cfg["Dispatcher:TickMs"] ?? "250");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DispatchTeam(Teams.TeamA);
            DispatchTeam(Teams.TeamB);
            DispatchTeam(Teams.TeamC);
            DispatchTeam(Teams.Overflow);

            await Task.Delay(_tickMs, stoppingToken);
        }
    }

    private void DispatchTeam(Teams team)
    {
        var agentsSnapshot = _agents.All().ToList();

        var queued = _chats.All()
            .Where(c => c.Status == ChatStatus.Queued && c.AssignedTeam == team)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        foreach (var chat in queued)
        {
            var agent = _assigner.TryPickNextAgent(agentsSnapshot, team);
            if (agent is null)
                continue;

            agent.ActiveChatsCount++;
            _agents.Upsert(agent);

            chat.AssignedAgentId = agent.Id;
            chat.Status = ChatStatus.Assigned;
            _chats.Update(chat);
        }
    }
}
