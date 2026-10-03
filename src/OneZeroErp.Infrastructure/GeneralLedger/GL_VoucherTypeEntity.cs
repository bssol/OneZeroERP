namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_VoucherTypeEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresBankAccount { get; set; }
    public bool RequiresCashAccount { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
