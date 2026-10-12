using System.Globalization;
using System.Text.Json;
using BoulderTime.Application.Common;

namespace BoulderTime.Application.Gyms;

/// <summary>One stretch of time the gym is open, as "HH:mm" in the gym's local time. "24:00" closes at midnight.</summary>
public sealed record OpeningIntervalDto(string Opens, string Closes);

/// <summary>A day of the week (1 Monday … 7 Sunday) and when the gym is open on it. No intervals: closed that day.</summary>
public sealed record OpeningDayDto(int Day, IReadOnlyList<OpeningIntervalDto> Intervals);

/// <summary>Days the gym is closed outside its usual week (holidays, works), both ends included.</summary>
public sealed record ClosureDto(DateOnly From, DateOnly To, string? Note = null);

/// <summary>
/// The gym's opening hours: a usual week and its exceptions, deliberately nothing more (no seasons, no per-sector
/// times). Kept as one JSON document on the gym. Times are the gym's own local time.
/// </summary>
public sealed record OpeningHoursDto(IReadOnlyList<OpeningDayDto> Week, IReadOnlyList<ClosureDto> Closures)
{
    public const int MaxIntervalsPerDay = 3;
    public const int MaxClosures = 30;
    public const int NoteMaxLength = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static OpeningHoursDto? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<OpeningHoursDto>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Minutes since midnight for "HH:mm" (00:00–24:00), or null if it isn't one.</summary>
    public static int? Minutes(string? time)
    {
        if (time is not { Length: 5 } || time[2] != ':') return null;
        if (!int.TryParse(time.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(time.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)) return null;
        if (m > 59 || h > 24 || (h == 24 && m != 0)) return null;
        return h * 60 + m;
    }

    /// <summary>
    /// Checks what an admin sent and returns it tidied: days in order, intervals in order, closures that are already over
    /// dropped. Null when there is nothing left to show (no open day and no closure), which clears the hours.
    /// </summary>
    public static OpeningHoursDto? Normalize(OpeningHoursDto? input, DateOnly today)
    {
        if (input is null) return null;
        var week = input.Week ?? [];
        var closures = input.Closures ?? [];
        var v = new Validator()
            .Check(week.All(d => d.Day is >= 1 and <= 7), "week", "Days go from 1 (Monday) to 7 (Sunday).")
            .Check(week.Select(d => d.Day).Distinct().Count() == week.Count, "week", "Give each day once.")
            .Check(week.All(d => (d.Intervals ?? []).Count <= MaxIntervalsPerDay), "week", $"Use at most {MaxIntervalsPerDay} opening times a day.")
            .Check(closures.Count <= MaxClosures, "closures", $"Add at most {MaxClosures} closures.")
            .Check(closures.All(c => c.From <= c.To), "closures", "A closure can't end before it starts.")
            .Check(closures.All(c => Input.MaxLength(c.Note, NoteMaxLength)), "closures", $"Keep each note to {NoteMaxLength} characters or fewer.")
            .Check(closures.All(c => !Community.ContentFilter.IsObjectionable(c.Note)), "closures", Community.ContentFilter.Message);

        var days = new List<OpeningDayDto>();
        foreach (var day in week.OrderBy(d => d.Day))
        {
            var intervals = (day.Intervals ?? []).Select(i => (i, Opens: Minutes(i.Opens), Closes: Minutes(i.Closes))).ToList();
            if (intervals.Any(i => i.Opens is null || i.Closes is null))
            {
                v.Check(false, "week", "Write times as HH:mm, for example 09:30.");
                continue;
            }
            if (intervals.Any(i => i.Opens >= i.Closes)) v.Check(false, "week", "Each opening time has to close after it opens.");
            var ordered = intervals.OrderBy(i => i.Opens).ToList();
            for (var k = 1; k < ordered.Count; k++)
                if (ordered[k].Opens < ordered[k - 1].Closes) v.Check(false, "week", "Opening times on the same day can't overlap.");
            if (ordered.Count > 0) days.Add(new OpeningDayDto(day.Day, ordered.Select(i => new OpeningIntervalDto(i.i.Opens, i.i.Closes)).ToList()));
        }
        v.ThrowIfInvalid();

        var upcoming = closures.Where(c => c.To >= today).OrderBy(c => c.From)
            .Select(c => new ClosureDto(c.From, c.To, string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim())).ToList();
        return days.Count == 0 && upcoming.Count == 0 ? null : new OpeningHoursDto(days, upcoming);
    }
}
