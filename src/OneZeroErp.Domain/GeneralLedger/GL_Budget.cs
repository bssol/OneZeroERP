using OneZeroErp.Domain;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Domain.GeneralLedger;

public sealed class GL_BudgetLine : AuditableEntity
{
    private GL_BudgetLine()
    {
    }

    private GL_BudgetLine(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid accountId, int periodNumber, decimal amount)
        : base(companyId, createdBy, createdOn)
    {
        AccountId = accountId;
        PeriodNumber = periodNumber;
        Amount = amount;
    }

    public Guid AccountId { get; private set; }
    public int PeriodNumber { get; private set; }
    public decimal Amount { get; private set; }

    internal static GL_BudgetLine Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid accountId, int periodNumber, decimal amount)
    {
        if (accountId == Guid.Empty) throw new DomainRuleException("A budget line must reference an account.");
        if (periodNumber is < 1 or > 12) throw new DomainRuleException("Budget period must be between 1 and 12.");
        if (amount < 0m) throw new DomainRuleException("Budget amount cannot be negative.");
        return new GL_BudgetLine(companyId, createdBy, createdOn, accountId, periodNumber, amount);
    }

    internal void ChangeAmount(Guid actorUserName, DateTimeOffset updatedOn, decimal amount)
    {
        if (amount < 0m) throw new DomainRuleException("Budget amount cannot be negative.");
        Amount = amount;
        MarkUpdated(actorUserName, updatedOn);
    }
}

public sealed class GL_Budget : AuditableEntity
{
    private readonly List<GL_BudgetLine> _lines = [];

    private GL_Budget()
    {
    }

    private GL_Budget(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid fiscalYearId, string code, string name)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        FiscalYearId = fiscalYearId;
        Code = Normalize(code, "Budget code", 30);
        Name = Normalize(name, "Budget name", 160);
    }

    public Guid Id { get; private set; }
    public Guid FiscalYearId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public BudgetStatus Status { get; private set; } = BudgetStatus.Draft;
    public IReadOnlyList<GL_BudgetLine> Lines => _lines.AsReadOnly();

    public static GL_Budget Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid fiscalYearId, string code, string name, Guid? id = null)
    {
        if (fiscalYearId == Guid.Empty) throw new DomainRuleException("A budget must reference a fiscal year.");
        return new GL_Budget(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, fiscalYearId, code, name);
    }

    public void AddOrUpdateLine(Guid actorUserName, DateTimeOffset updatedOn, Guid accountId, int periodNumber, decimal amount)
    {
        EnsureDraft();
        var existing = _lines.SingleOrDefault(x => x.AccountId == accountId && x.PeriodNumber == periodNumber);
        if (existing is null) _lines.Add(GL_BudgetLine.Create(CompanyId, actorUserName, updatedOn, accountId, periodNumber, amount));
        else existing.ChangeAmount(actorUserName, updatedOn, amount);
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Approve(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new DomainRuleException("A budget must contain at least one line before approval.");
        Status = BudgetStatus.Approved;
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Close(Guid actorUserName, DateTimeOffset updatedOn)
    {
        if (Status != BudgetStatus.Approved) throw new DomainRuleException("Only an approved budget can be closed.");
        Status = BudgetStatus.Closed;
        MarkUpdated(actorUserName, updatedOn);
    }

    private void EnsureDraft()
    {
        if (IsDeleted) throw new DomainRuleException("A deleted budget cannot be changed.");
        if (Status != BudgetStatus.Draft) throw new DomainRuleException("Only draft budgets can be edited.");
    }

    private static string Normalize(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException($"{fieldName} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new DomainRuleException($"{fieldName} cannot exceed {maxLength} characters.");
        return normalized;
    }
}