using SupportChat.Api.BackgroundServices;
using SupportChat.Api.Services;
using SupportChat.Domain.Models;
using SupportChat.Domain.Services;
using SupportChat.Infrastructure.Queues;
using SupportChat.Infrastructure.Stores;
using SupportChat.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();


builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<InMemoryChatStore>();
builder.Services.AddSingleton<InMemoryAgentStore>();

builder.Services.AddSingleton<CapacityCalculator>();

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var start = TimeOnly.Parse(cfg["OfficeHours:Start"] ?? "09:00");
    var end = TimeOnly.Parse(cfg["OfficeHours:End"] ?? "18:00");
    return new OfficeHoursService(start, end);
});

builder.Services.AddSingleton<TeamRoutingService>();
builder.Services.AddSingleton<RoundRobinAssigner>();
builder.Services.AddSingleton<ChatAdmissionService>();


builder.Services.AddSingleton<IMainChatQueue>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var cap = int.Parse(cfg["Queues:MainCapacity"] ?? "1000");
    return new ChatQueue(cap);
});

builder.Services.AddSingleton<IOverflowChatQueue>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var cap = int.Parse(cfg["Queues:OverflowCapacity"] ?? "500");
    return new ChatQueue(cap);
});

builder.Services.AddHostedService<ChatDispatcherWorker>();
builder.Services.AddHostedService<InactivityMonitorWorker>();
builder.Services.AddHostedService<ShiftMonitorWorker>();
builder.Services.AddHostedService<OverflowActivationWorker>();

var app = builder.Build();


SeedAgents(app.Services.GetRequiredService<InMemoryAgentStore>());

app.MapOpenApi();
app.MapControllers();

app.Run();

static void SeedAgents(InMemoryAgentStore store)
{
    var now = DateTimeOffset.UtcNow;

    // Team A: 1 lead, 2 mid, 1 junior
    store.Upsert(new Agent { Team = Teams.TeamA, Seniority = Seniority.TeamLead, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamA, Seniority = Seniority.Mid, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamA, Seniority = Seniority.Mid, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamA, Seniority = Seniority.Junior, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });

    // Team B: 1 senior, 1 mid, 2 junior
    store.Upsert(new Agent { Team = Teams.TeamB, Seniority = Seniority.Senior, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamB, Seniority = Seniority.Mid, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamB, Seniority = Seniority.Junior, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamB, Seniority = Seniority.Junior, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });

    // Team C: 2 mid (night shift team)
    store.Upsert(new Agent { Team = Teams.TeamC, Seniority = Seniority.Mid, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });
    store.Upsert(new Agent { Team = Teams.TeamC, Seniority = Seniority.Mid, ShiftStart = now.AddHours(-1), ShiftEnd = now.AddHours(7), AcceptingNewChats = true });

    // Overflow team: 6 juniors
    for (int i = 0; i < 6; i++)
    {
        store.Upsert(new Agent
        {
            Team = Teams.Overflow,
            Seniority = Seniority.Junior,
            ShiftStart = now.AddHours(-1),
            ShiftEnd = now.AddHours(7),
            AcceptingNewChats = false
        });
    }
}
