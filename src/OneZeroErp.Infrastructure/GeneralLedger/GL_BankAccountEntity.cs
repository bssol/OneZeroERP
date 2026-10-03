namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_BankAccountEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GlAccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
