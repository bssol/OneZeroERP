namespace OneZeroErp.Infrastructure.Persistence;

public sealed class Erp_LockDateEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsLocked { get; set; }
    public Guid? LockedByUserId { get; set; }
    public DateTimeOffset? LockedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
