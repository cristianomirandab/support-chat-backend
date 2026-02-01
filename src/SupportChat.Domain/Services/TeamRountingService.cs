using SupportChat.Domain.Models;

namespace SupportChat.Domain.Services;

public sealed class TeamRoutingService
{
    private readonly OfficeHoursService _office;

    public TeamRoutingService(OfficeHoursService office) => _office = office;

    public Teams SelectMainTeam(DateTimeOffset now, bool forceOfficeHours)
    {
        if (forceOfficeHours) return Teams.TeamA;
        return _office.IsWithinOfficeHours(now) ? Teams.TeamA : Teams.TeamC;
    }

}
