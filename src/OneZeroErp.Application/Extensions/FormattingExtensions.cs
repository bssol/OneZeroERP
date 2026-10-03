using System.Globalization;
using OneZeroErp.Application.Time;

namespace OneZeroErp.Application.Extensions;

public static class FormattingExtensions
{
    public static DateOnly CurrentDate(this IClock clock) => clock.CurrentDate;
    public static DateTimeOffset CurrentDateTime(this IClock clock) => clock.CurrentDateTime;
    public static TimeOnly CurrentTime(this IClock clock) => clock.CurrentTime;

    public static string ToDateFormatPK(this DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string ToDateFormatPK(this DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string ToDateFormatPK(this DateTimeOffset value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string ToFormated(this decimal value, int decimalPlaces = 2)
    {
        var places = Math.Clamp(decimalPlaces, 0, 28);
        return value.ToString($"N{places}", CultureInfo.InvariantCulture);
    }

    public static string ToFormatted(this decimal value, int decimalPlaces = 2) => value.ToFormated(decimalPlaces);

    public static string GetAllMessages(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var messages = new List<string>();
        AddMessages(exception, messages);
        return string.Join(" | ", messages.Distinct(StringComparer.Ordinal));
    }

    private static void AddMessages(Exception exception, ICollection<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(exception.Message)) messages.Add(exception.Message);
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions) AddMessages(inner, messages);
            return;
        }
        if (exception.InnerException is not null) AddMessages(exception.InnerException, messages);
    }
}
