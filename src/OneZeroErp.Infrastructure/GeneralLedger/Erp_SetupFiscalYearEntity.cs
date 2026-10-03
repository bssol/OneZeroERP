using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class Erp_SetupFiscalYearEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public FiscalYearStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
