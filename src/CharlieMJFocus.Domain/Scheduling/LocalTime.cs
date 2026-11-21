namespace CharlieMJFocus.Domain.Scheduling;

/// <summary>
/// Conversions between the local wall-clock times the user sees and the UTC instants we store.
/// Centralised here so daylight-saving edge cases are handled in exactly one place.
/// </summary>
public static class LocalTime
{
    /// <summary>Converts a local date + time to a UTC-based instant. Times skipped by a DST jump are moved forward.</summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        ToInstant(date.ToDateTime(time, DateTimeKind.Unspecified), zone);

    /// <summary>Converts an unspecified-kind local date-time to an instant.</summary>
    public static DateTimeOffset ToInstant(DateTime localDateTime, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        // A wall-clock time inside the "spring forward" gap does not exist; step past it.
        if (zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    /// <summary>Converts an instant to the local date-time in the given zone.</summary>
    public static DateTimeOffset ToLocal(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone);

    /// <summary>The local calendar date of an instant.</summary>
    public static DateOnly DateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(ToLocal(instant, zone).DateTime);

    /// <summary>Start (local midnight) and exclusive end (next local midnight) of a day, as instants.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateOnly date, TimeZoneInfo zone) =>
        (ToInstant(date, TimeOnly.MinValue, zone), ToInstant(date.AddDays(1), TimeOnly.MinValue, zone));

    /// <summary>The last minute of a local day, used for "due on this date" deadlines.</summary>
    public static DateTimeOffset EndOfDay(DateOnly date, TimeZoneInfo zone) =>
        ToInstant(date, new TimeOnly(23, 59), zone);
}
