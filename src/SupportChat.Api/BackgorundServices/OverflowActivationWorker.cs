using SupportChat.Domain.Models;
using SupportChat.Domain.Services;
using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

namespace SupportChat.Api.BackgroundServices;

public sealed class OverflowActivationWorker : BackgroundService
{
    private readonly InMemoryChatStore _chats;
    private readonly InMemoryAgentStore _agents;
    private readonly CapacityCalculator _capacity;
    private readonly OfficeHoursService _office;
    private readonly TeamRoutingService _routing;
    private readonly IClock _clock;
    private readonly IConfiguration _cfg;

    public OverflowActivationWorker(
        InMemoryChatStore chats,
        InMemoryAgentStore agents,
        CapacityCalculator capacity,
        OfficeHoursService office,
        TeamRoutingService routing,
        IClock clock,
        IConfiguration cfg)
    {
        _chats = chats;
        _agents = agents;
        _capacity = capacity;
        _office = office;
        _routing = routing;
        _clock = clock;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.UtcNow;

            // TESTING: optionally force office hours to make local tests deterministic.
            var forceOffice = _cfg.GetValue("Testing:ForceOfficeHours", false);
            var inOffice = forceOffice || _office.IsWithinOfficeHours(now);

            var mainTeam = _routing.SelectMainTeam(now, forceOffice);

            var agentsSnapshot = _agents.All().ToList();
            var chatsSnapshot = _chats.All().ToList();

            // Calculate real team capacity based on agent seniority.
            var mainAgents = agentsSnapshot.Where(a => a.Team == mainTeam);
            var mainCapacity = _capacity.TeamCapacity(mainAgents);

            // Max queue size according to the PDF rule: floor(capacity * 1.5).
            var maxMainQueue = _capacity.MaxQueueSize(mainCapacity);

            // TESTING: override max queue size to trigger overflow with fewer requests.
            var overrideMainMax = _cfg.GetValue<int?>("Testing:MainMaxQueueOverride", null);
            if (overrideMainMax is > 0)
                maxMainQueue = overrideMainMax.Value;

            // Backlog represents all chats that still consume team capacity
            // in this model (Queued + Assigned + Active).
            var mainBacklog = chatsSnapshot.Count(c =>
                c.AssignedTeam == mainTeam &&
                (c.Status == ChatStatus.Queued ||
                 c.Status == ChatStatus.Assigned ||
                 c.Status == ChatStatus.Active));

            // SIMULATION FLAG (kept explicit for clarity).
            // In this implementation, backlog is always used as the trigger,
            // since the dispatcher assigns chats very quickly.
            var usePressure = _cfg.GetValue("Testing:UsePressureForOverflowTrigger", false);

            // According to the PDF rule:
            // Overflow is enabled only when the main team reaches its maximum
            // AND this happens during office hours.
            var enableOverflow = inOffice && mainBacklog >= maxMainQueue;

            // Enable or disable overflow agents accordingly.
            foreach (var agent in agentsSnapshot.Where(a => a.Team == Teams.Overflow))
            {
                agent.AcceptingNewChats = enableOverflow;
                _agents.Upsert(agent);
            }

            // Worker runs periodically (once per second).
            await Task.Delay(1000, stoppingToken);
        }
    }
}
