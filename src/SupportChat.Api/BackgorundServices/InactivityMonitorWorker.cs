using SupportChat.Domain.Models;
using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

namespace SupportChat.Api.BackgroundServices;

public sealed class InactivityMonitorWorker : BackgroundService
{
    private readonly InMemoryChatStore _chats;
    private readonly InMemoryAgentStore _agents;
    private readonly IClock _clock;
    private readonly TimeSpan _inactiveAfter;

    public InactivityMonitorWorker(
        InMemoryChatStore chats,
        InMemoryAgentStore agents,
        IClock clock,
        IConfiguration configuration)
    {
        _chats = chats;
        _agents = agents;
        _clock = clock;

        var seconds = int.Parse(configuration["Polling:InactiveAfterSeconds"] ?? "3");
        _inactiveAfter = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MarkInactiveChats();
            await Task.Delay(1000, stoppingToken);
        }
    }

    private void MarkInactiveChats()
    {
        var now = _clock.UtcNow;

        foreach (var chat in _chats.All())
        {
            if (chat.Status is ChatStatus.Closed or ChatStatus.Rejected or ChatStatus.Inactive)
                continue;

            var lastSeen = chat.LastPollAt ?? chat.CreatedAt;

            if (now - lastSeen <= _inactiveAfter)
                continue;
                        
            if (chat.Status is ChatStatus.Assigned or ChatStatus.Active)
            {
                if (chat.AssignedAgentId is Guid agentId)
                {
                    var agent = _agents.Get(agentId);
                    if (agent is not null && agent.ActiveChatsCount > 0)
                    {
                        agent.ActiveChatsCount--;
                        _agents.Upsert(agent);
                    }
                }
            }

            chat.Status = ChatStatus.Inactive;
            _chats.Update(chat);
        }
    }
}
