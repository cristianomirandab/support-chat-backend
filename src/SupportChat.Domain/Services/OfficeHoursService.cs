namespace SupportChat.Domain.Services;

public sealed class OfficeHoursService
{
    private readonly TimeOnly _start;
    private readonly TimeOnly _end;

    public OfficeHoursService(TimeOnly start, TimeOnly end)
    {
        _start = start;
        _end = end;
    }

    public bool IsWithinOfficeHours(DateTimeOffset now)
    {
        var t = TimeOnly.FromDateTime(now.LocalDateTime);
        return t >= _start && t < _end;
    }
}
