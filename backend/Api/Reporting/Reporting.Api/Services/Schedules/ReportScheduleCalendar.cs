using Reporting.Entity.Enums;

namespace Reporting.Api.Services.Schedules;

/// <summary>
/// When a schedule next falls due (TK-114). Pure. The hour is the branch's
/// local hour, so "8 a.m. every Monday" stays 8 a.m. across a daylight-saving
/// change where the zone has one; India has none, which is why a missing zone
/// database falls back to a fixed +05:30 for <c>Asia/Kolkata</c>.
/// </summary>
public static class ReportScheduleCalendar
{
    public const string DefaultTimeZone = "Asia/Kolkata";

    /// <summary>The first slot strictly after <paramref name="after"/>.</summary>
    public static DateTimeOffset NextRun(
        ReportScheduleFrequency frequency,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        int hour,
        string? timeZoneId,
        DateTimeOffset after)
    {
        TimeZoneInfo zone = Zone(timeZoneId);
        DateOnly start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);

        // Two months of days always holds the next slot of any frequency.
        for (int offset = 0; offset <= 62; offset++)
        {
            DateOnly day = start.AddDays(offset);

            bool matches = frequency switch
            {
                ReportScheduleFrequency.Daily => true,
                ReportScheduleFrequency.Weekly => day.DayOfWeek == (dayOfWeek ?? DayOfWeek.Monday),
                ReportScheduleFrequency.Monthly => day.Day == Math.Clamp(dayOfMonth ?? 1, 1, 28),
                _ => false,
            };

            if (!matches)
            {
                continue;
            }

            DateTime local = day.ToDateTime(new TimeOnly(Math.Clamp(hour, 0, 23), 0), DateTimeKind.Unspecified);
            DateTimeOffset slot = new(local, zone.GetUtcOffset(local));

            if (slot > after)
            {
                return slot.ToUniversalTime();
            }
        }

        throw new InvalidOperationException($"No {frequency} slot found after {after:O}.");
    }

    /// <summary>The zone, or +05:30 for India and UTC otherwise when the host has no zone database.</summary>
    public static TimeZoneInfo Zone(string? timeZoneId)
    {
        string id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZone : timeZoneId;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return id == DefaultTimeZone
                ? TimeZoneInfo.CreateCustomTimeZone(DefaultTimeZone, TimeSpan.FromMinutes(330), "India Standard Time", "India Standard Time")
                : TimeZoneInfo.Utc;
        }
    }

    /// <summary>Whether <paramref name="timeZoneId"/> names a zone this host knows.</summary>
    public static bool IsKnown(string timeZoneId) =>
        timeZoneId == DefaultTimeZone || TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}
