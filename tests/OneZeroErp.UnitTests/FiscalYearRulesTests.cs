using OneZeroErp.Domain.ErpSetups;

namespace OneZeroErp.UnitTests;

public sealed class Erp_FiscalYearRulesTests
{
    [Fact]
    public void Fiscal_year_requires_a_valid_date_range()
    {
        var error = Erp_FiscalYearRules.Validate("FY2026", new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1));

        Assert.Equal("The start date must be on or before the end date.", error);
    }

    [Fact]
    public void Fiscal_year_ranges_overlap_when_their_dates_intersect()
    {
        Assert.True(Erp_FiscalYearRules.Overlaps(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31)));
        Assert.False(Erp_FiscalYearRules.Overlaps(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)));
    }
    [Fact]
    public void Code_cannot_exceed_the_persisted_limit()
    {
        var error = Erp_FiscalYearRules.Validate(new string('X', 31), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal("Fiscal year code cannot exceed 30 characters.", error);
    }
}