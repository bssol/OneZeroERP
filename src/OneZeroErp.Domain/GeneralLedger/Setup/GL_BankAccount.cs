namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed class GL_BankAccount : AuditableEntity
{
    private GL_BankAccount()
    {
    }

    private GL_BankAccount(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid glAccountId, string code, string name, string bankName, string accountNumber, bool isActive)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        GlAccountId = glAccountId;
        Code = Normalize(code, "Bank account code", 30);
        Name = Normalize(name, "Bank account name", 160);
        BankName = Normalize(bankName, "Bank name", 160);
        AccountNumber = Normalize(accountNumber, "Bank account number", 80);
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public Guid GlAccountId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public string AccountNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public static GL_BankAccount Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid glAccountId, string code, string name, string bankName, string accountNumber, bool isActive = true, Guid? id = null)
    {
        if (glAccountId == Guid.Empty) throw new DomainRuleException("A bank account must reference a general ledger account.");
        return new GL_BankAccount(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, glAccountId, code, name, bankName, accountNumber, isActive);
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