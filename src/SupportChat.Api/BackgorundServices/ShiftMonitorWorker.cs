using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

namespace SupportChat.Api.BackgroundServices;

public sealed class ShiftMonitorWorker : BackgroundService
{
    private readonly InMemoryAgentStore _agents;
    private readonly IClock _clock;

    public ShiftMonitorWorker(InMemoryAgentStore agents, IClock clock)
    {
        _agents = agents;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.UtcNow;

            foreach (var agent in _agents.All())
            {
                if (agent.AcceptingNewChats && now >= agent.ShiftEnd)
                {
                    agent.AcceptingNewChats = false;
                    _agents.Upsert(agent);
                }
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
