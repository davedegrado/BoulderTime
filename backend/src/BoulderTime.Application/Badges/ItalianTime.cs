namespace BoulderTime.Application.Badges;

/// <summary>
/// The calendar day of an instant in Italy, where BoulderTime's climbers are: a session at 23:30 counts on its own
/// day, not on tomorrow's as in UTC. Falls back to the European summer-time rule if the server has no time-zone data.
/// </summary>
public static class ItalianTime
{
    private static readonly TimeZoneInfo? Rome = Find();

    private static TimeZoneInfo? Find()
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }

    public static DateOnly Day(DateTimeOffset instant)
    {
        if (Rome is not null) return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Rome).DateTime);
        var utc = instant.UtcDateTime;
        // Summer time: from 01:00 UTC of the last Sunday of March to 01:00 UTC of the last Sunday of October.
        var start = LastSunday(utc.Year, 3).AddHours(1);
        var end = LastSunday(utc.Year, 10).AddHours(1);
        return DateOnly.FromDateTime(utc.AddHours(utc >= start && utc < end ? 2 : 1));
    }

    /// <summary>An instant inside the day, for dates shown as "earned on".</summary>
    public static DateTimeOffset Noon(DateOnly day) => new(day.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero);

    private static DateTime LastSunday(int year, int month)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return last.AddDays(-(int)last.DayOfWeek);
    }
}
