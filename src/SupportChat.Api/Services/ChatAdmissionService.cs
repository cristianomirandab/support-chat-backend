using SupportChat.Domain.Models;
using SupportChat.Domain.Services;
using SupportChat.Infrastructure.Queues;
using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

namespace SupportChat.Api.Services
{
    public sealed class ChatAdmissionService
    {
        private readonly InMemoryChatStore _chats;
        private readonly InMemoryAgentStore _agents;
        private readonly CapacityCalculator _capacity;
        private readonly OfficeHoursService _officeHours;
        private readonly TeamRoutingService _routing;
        private readonly IClock _clock;
        private readonly IMainChatQueue _mainQueue;
        private readonly IOverflowChatQueue _overflowQueue;
        private readonly IConfiguration _cfg;

        public ChatAdmissionService(
            InMemoryChatStore chats,
            InMemoryAgentStore agents,
            CapacityCalculator capacity,
            OfficeHoursService officeHours,
            TeamRoutingService routing,
            IClock clock,
            IMainChatQueue mainQueue,
            IOverflowChatQueue overflowQueue,
            IConfiguration cfg)
        {
            _chats = chats;
            _agents = agents;
            _capacity = capacity;
            _officeHours = officeHours;
            _routing = routing;
            _clock = clock;
            _mainQueue = mainQueue;
            _overflowQueue = overflowQueue;
            _cfg = cfg;
        }

        public CreateChatResult CreateChat()
        {
            var now = _clock.UtcNow;

            // TESTING: optionally force office hours to make scenarios deterministic.
            var forceOffice = _cfg.GetValue("Testing:ForceOfficeHours", false);
            var inOffice = forceOffice || _officeHours.IsWithinOfficeHours(now);

            var mainTeam = _routing.SelectMainTeam(now, forceOffice);

            var agentsSnapshot = _agents.All().ToList();
            var chatsSnapshot = _chats.All().ToList();

            // Compute main team capacity and max queue size (PDF rule: floor(capacity * 1.5)).
            var mainAgents = agentsSnapshot.Where(a => a.Team == mainTeam);
            var mainCapacity = _capacity.TeamCapacity(mainAgents);

            var maxMainQueue = _capacity.MaxQueueSize(mainCapacity);

            // TESTING: override max queue to trigger overflow with fewer requests.
            var overrideMainMax = _cfg.GetValue<int?>("Testing:MainMaxQueueOverride", null);
            if (overrideMainMax is > 0)
                maxMainQueue = overrideMainMax.Value;

            // Backlog = "queue" in this model (Queued + Assigned + Active).
            var mainBacklog = chatsSnapshot.Count(c =>
                c.AssignedTeam == mainTeam &&
                (c.Status == ChatStatus.Queued ||
                 c.Status == ChatStatus.Assigned ||
                 c.Status == ChatStatus.Active));

            var mainHasRoom = mainBacklog < maxMainQueue;

            // Overflow is enabled only if the overflow worker set AcceptingNewChats=true
            // AND we are in office hours (or forced office hours).
            var overflowEnabled = inOffice && agentsSnapshot.Any(a => a.Team == Teams.Overflow && a.AcceptingNewChats);

            var overflowAgents = agentsSnapshot.Where(a => a.Team == Teams.Overflow && a.AcceptingNewChats);
            var overflowCapacity = _capacity.TeamCapacity(overflowAgents);
            var maxOverflowQueue = _capacity.MaxQueueSize(overflowCapacity);

            var overflowBacklog = chatsSnapshot.Count(c =>
                c.AssignedTeam == Teams.Overflow &&
                (c.Status == ChatStatus.Queued ||
                 c.Status == ChatStatus.Assigned ||
                 c.Status == ChatStatus.Active));

            var overflowHasRoom = overflowEnabled && overflowBacklog < maxOverflowQueue;

            Teams targetTeam;
            if (mainHasRoom)
            {
                targetTeam = mainTeam;
            }
            else if (overflowHasRoom)
            {
                targetTeam = Teams.Overflow;
            }
            else
            {
                var rejected = _chats.Add(new ChatSession { Status = ChatStatus.Rejected });
                return new CreateChatResult(false, rejected.Id, "Rejected", null);
            }

            var chat = _chats.Add(new ChatSession
            {
                Status = ChatStatus.Queued,
                AssignedTeam = targetTeam
            });

            var enqueued = targetTeam == Teams.Overflow
                ? _overflowQueue.TryEnqueue(chat.Id)
                : _mainQueue.TryEnqueue(chat.Id);

            if (!enqueued)
            {
                chat.Status = ChatStatus.Rejected;
                _chats.Update(chat);
                return new CreateChatResult(false, chat.Id, "Rejected", null);
            }

            return new CreateChatResult(true, chat.Id, "OK", targetTeam);
        }
    }
}
