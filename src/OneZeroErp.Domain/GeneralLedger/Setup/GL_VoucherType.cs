namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed class GL_VoucherType : AuditableEntity
{
    private GL_VoucherType()
    {
    }

    private GL_VoucherType(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, string code, string description, bool requiresBankAccount, bool requiresCashAccount, bool isActive)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        Code = NormalizeCode(code);
        Description = Normalize(description, "Voucher type description", 200);
        RequiresBankAccount = requiresBankAccount;
        RequiresCashAccount = requiresCashAccount;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool RequiresBankAccount { get; private set; }
    public bool RequiresCashAccount { get; private set; }
    public bool IsActive { get; private set; }

    public static GL_VoucherType Create(
        Guid companyId,
        Guid createdBy,
        DateTimeOffset createdOn,
        string code,
        string description,
        bool requiresBankAccount = false,
        bool requiresCashAccount = false,
        bool isActive = true,
        Guid? id = null)
    {
        if (requiresBankAccount && requiresCashAccount) throw new DomainRuleException("A voucher type cannot require both a bank and cash account.");
        return new GL_VoucherType(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, code, description, requiresBankAccount, requiresCashAccount, isActive);
    }

    public void ChangeDescription(Guid actorUserName, DateTimeOffset updatedOn, string description)
    {
        Description = Normalize(description, "Voucher type description", 200);
        MarkUpdated(actorUserName, updatedOn);
    }

    public void SetActive(Guid actorUserName, DateTimeOffset updatedOn, bool isActive)
    {
        IsActive = isActive;
        MarkUpdated(actorUserName, updatedOn);
    }

    public static IReadOnlyList<GL_VoucherType> StandardTypes(Guid companyId, Guid createdBy, DateTimeOffset createdOn) =>
    [
        Create(companyId, createdBy, createdOn, "OPV", "Opening Voucher"),
        Create(companyId, createdBy, createdOn, "JVV", "Journal Voucher"),
        Create(companyId, createdBy, createdOn, "CPV", "Cash Payment Voucher", requiresCashAccount: true),
        Create(companyId, createdBy, createdOn, "CRV", "Cash Receipt Voucher", requiresCashAccount: true),
        Create(companyId, createdBy, createdOn, "BPV", "Bank Payment Voucher", requiresBankAccount: true),
        Create(companyId, createdBy, createdOn, "BRV", "Bank Receipt Voucher", requiresBankAccount: true)
    ];

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("Voucher type code is required.");
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 20) throw new DomainRuleException("Voucher type code cannot exceed 20 characters.");
        return normalized;
    }

    private static string Normalize(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException($"{fieldName} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new DomainRuleException($"{fieldName} cannot exceed {maxLength} characters.");
        return normalized;
    }
}