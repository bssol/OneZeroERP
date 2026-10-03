namespace OneZeroErp.Domain.ErpSetups;

public static class Erp_FiscalYearRules
{
    public static string? Validate(string code, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Fiscal year code is required.";
        if (code.Trim().Length > 30) return "Fiscal year code cannot exceed 30 characters.";
        if (startDate > endDate) return "The start date must be on or before the end date.";
        return null;
    }

    public static bool Overlaps(DateOnly startDate, DateOnly endDate, DateOnly otherStartDate, DateOnly otherEndDate) =>
        startDate <= otherEndDate && otherStartDate <= endDate;
}
