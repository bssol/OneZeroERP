using OneZeroErp.Application.Time;

namespace OneZeroErp.UnitTests;

public sealed class ClockTests
{
    [Fact]
    public void System_clock_exposes_utc_and_business_timezone_values_from_one_source()
    {
        var utcNow = new DateTimeOffset(2026, 1, 1, 22, 30, 0, TimeSpan.Zero);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("Test UTC+5", TimeSpan.FromHours(5), "Test UTC+5", "Test UTC+5");
        var clock = new SystemClock(new FixedTimeProvider(utcNow), timeZone);

        Assert.Equal(utcNow, clock.UtcNow);
        Assert.Equal(new DateOnly(2026, 1, 2), clock.CurrentDate);
        Assert.Equal(new TimeOnly(3, 30), clock.CurrentTime);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 30, 0, TimeSpan.FromHours(5)), clock.CurrentDateTime);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}