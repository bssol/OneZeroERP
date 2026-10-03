namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed class ReconciliationItem : AuditableEntity
{
    private ReconciliationItem()
    {
    }

    private ReconciliationItem(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, ReconciliationItemType type, string description, decimal adjustmentAmount)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        Type = type;
        Description = description;
        AdjustmentAmount = adjustmentAmount;
    }

    public Guid Id { get; private set; }
    public ReconciliationItemType Type { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal AdjustmentAmount { get; private set; }
    public bool IsCleared { get; private set; }

    internal static ReconciliationItem Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, ReconciliationItemType type, string description, decimal adjustmentAmount, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainRuleException("A reconciliation item description is required.");
        if (adjustmentAmount == 0m) throw new DomainRuleException("A reconciliation item adjustment cannot be zero.");
        return new ReconciliationItem(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, type, description.Trim(), adjustmentAmount);
    }

    public void MarkCleared(Guid actorUserName, DateTimeOffset updatedOn)
    {
        IsCleared = true;
        MarkUpdated(actorUserName, updatedOn);
    }
}

public sealed class GL_BankReconciliation : AuditableEntity
{
    private readonly List<ReconciliationItem> _items = [];

    private GL_BankReconciliation()
    {
    }

    private GL_BankReconciliation(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid bankAccountId, DateOnly statementDate, decimal statementEndingBalance, decimal bookEndingBalance)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        BankAccountId = bankAccountId;
        StatementDate = statementDate;
        StatementEndingBalance = statementEndingBalance;
        BookEndingBalance = bookEndingBalance;
    }

    public Guid Id { get; private set; }
    public Guid BankAccountId { get; private set; }
    public DateOnly StatementDate { get; private set; }
    public decimal StatementEndingBalance { get; private set; }
    public decimal BookEndingBalance { get; private set; }
    public ReconciliationStatus Status { get; private set; } = ReconciliationStatus.Draft;
    public IReadOnlyList<ReconciliationItem> Items => _items.AsReadOnly();
    public decimal AdjustedStatementBalance => StatementEndingBalance + _items.Where(x => !x.IsCleared).Sum(x => x.AdjustmentAmount);

    public static GL_BankReconciliation Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid bankAccountId, DateOnly statementDate, decimal statementEndingBalance, decimal bookEndingBalance, Guid? id = null)
    {
        if (bankAccountId == Guid.Empty) throw new DomainRuleException("A reconciliation must reference a bank account.");
        return new GL_BankReconciliation(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, bankAccountId, statementDate, statementEndingBalance, bookEndingBalance);
    }

    public Guid AddItem(Guid actorUserName, DateTimeOffset updatedOn, ReconciliationItemType type, string description, decimal adjustmentAmount)
    {
        EnsureDraft();
        var item = ReconciliationItem.Create(CompanyId, actorUserName, updatedOn, type, description, adjustmentAmount);
        _items.Add(item);
        MarkUpdated(actorUserName, updatedOn);
        return item.Id;
    }

    public void MarkItemCleared(Guid actorUserName, DateTimeOffset updatedOn, Guid itemId)
    {
        EnsureDraft();
        var item = _items.SingleOrDefault(x => x.Id == itemId) ?? throw new DomainRuleException("The reconciliation item was not found.");
        item.MarkCleared(actorUserName, updatedOn);
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Complete(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureDraft();
        if (AdjustedStatementBalance != BookEndingBalance) throw new DomainRuleException("The adjusted bank balance must equal the book balance before reconciliation can be completed.");
        Status = ReconciliationStatus.Completed;
        MarkUpdated(actorUserName, updatedOn);
    }

    private void EnsureDraft()
    {
        if (IsDeleted) throw new DomainRuleException("A deleted bank reconciliation cannot be changed.");
        if (Status != ReconciliationStatus.Draft) throw new DomainRuleException("A completed bank reconciliation cannot be edited.");
    }
}