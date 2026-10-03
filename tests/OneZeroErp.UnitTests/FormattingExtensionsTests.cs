using OneZeroErp.Application.Extensions;

namespace OneZeroErp.UnitTests;

public sealed class FormattingExtensionsTests
{
    [Fact]
    public void Decimal_formatting_uses_grouping_and_requested_precision()
    {
        Assert.Equal("1,234,567.89", 1234567.89m.ToFormated());
        Assert.Equal("1,234,568", 1234567.89m.ToFormated(0));
        Assert.Equal("1,234,567.890", 1234567.89m.ToFormatted(3));
    }

    [Fact]
    public void Pakistan_date_format_is_day_month_year()
    {
        var date = new DateOnly(2026, 9, 24);

        Assert.Equal("24/09/2026", date.ToDateFormatPK());
    }

    [Fact]
    public void Exception_messages_are_combined_without_duplicates()
    {
        var exception = new InvalidOperationException(
            "outer",
            new ArgumentException("inner", new Exception("outer")));

        Assert.Equal("outer | inner", exception.GetAllMessages());
    }
}