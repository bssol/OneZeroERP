namespace OneZeroErp.Application.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset CurrentDateTime { get; }
    DateOnly CurrentDate { get; }
    TimeOnly CurrentTime { get; }
}

public sealed class SystemClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTimeOffset CurrentDateTime => TimeZoneInfo.ConvertTime(UtcNow, businessTimeZone);

    public DateOnly CurrentDate => DateOnly.FromDateTime(CurrentDateTime.DateTime);

    public TimeOnly CurrentTime => TimeOnly.FromDateTime(CurrentDateTime.DateTime);
}

public static class BusinessTimeZone
{
    public const string DefaultId = "Pakistan Standard Time";

    public static TimeZoneInfo Resolve(string? configuredId)
    {
        var requestedId = string.IsNullOrWhiteSpace(configuredId) ? DefaultId : configuredId.Trim();
        var candidates = new[] { requestedId, "Pakistan Standard Time", "Asia/Karachi", "UTC" }.Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}