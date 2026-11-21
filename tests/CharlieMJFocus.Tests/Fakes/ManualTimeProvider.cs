namespace CharlieMJFocus.Tests.Fakes;

/// <summary>A clock the test controls. Time only moves when <see cref="Advance"/> is called.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    private readonly TimeZoneInfo _zone;

    public ManualTimeProvider(DateTimeOffset? start = null, TimeZoneInfo? zone = null)
    {
        _now = start ?? new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        _zone = zone ?? TimeZoneInfo.Utc;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => _zone;

    public void Advance(TimeSpan by) => _now += by;
}
