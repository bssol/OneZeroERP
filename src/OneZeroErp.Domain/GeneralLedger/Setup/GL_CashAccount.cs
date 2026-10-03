namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed class GL_CashAccount : AuditableEntity
{
    private GL_CashAccount()
    {
    }

    private GL_CashAccount(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid glAccountId, string code, string name, string location, bool isActive)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        GlAccountId = glAccountId;
        Code = Normalize(code, "Cash account code", 30);
        Name = Normalize(name, "Cash account name", 160);
        Location = Normalize(location, "Cash account location", 160);
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public Guid GlAccountId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static GL_CashAccount Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid glAccountId, string code, string name, string location, bool isActive = true, Guid? id = null)
    {
        if (glAccountId == Guid.Empty) throw new DomainRuleException("A cash account must reference a general ledger account.");
        return new GL_CashAccount(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, glAccountId, code, name, location, isActive);
    }

    public void SetActive(Guid actorUserName, DateTimeOffset updatedOn, bool isActive)
    {
        IsActive = isActive;
        MarkUpdated(actorUserName, updatedOn);
    }

    private static string Normalize(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException($"{fieldName} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new DomainRuleException($"{fieldName} cannot exceed {maxLength} characters.");
        return normalized;
    }
}