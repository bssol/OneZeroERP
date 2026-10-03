using OneZeroErp.Domain.ErpSetups;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Domain.GeneralLedger;

public sealed class GL_JournalLine : AuditableEntity
{
    private GL_JournalLine()
    {
    }

    private GL_JournalLine(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, int lineNumber, Guid accountId, string description, decimal debitAmount, decimal creditAmount)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        LineNumber = lineNumber;
        AccountId = accountId;
        Description = description;
        DebitAmount = debitAmount;
        CreditAmount = creditAmount;
    }

    public Guid Id { get; private set; }
    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal DebitAmount { get; private set; }
    public decimal CreditAmount { get; private set; }

    internal static GL_JournalLine Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, int lineNumber, Guid accountId, string description, decimal debitAmount, decimal creditAmount, Guid? id = null)
    {
        if (lineNumber <= 0) throw new DomainRuleException("A journal line number must be positive.");
        if (accountId == Guid.Empty) throw new DomainRuleException("A journal line must reference an account.");
        if (debitAmount < 0 || creditAmount < 0) throw new DomainRuleException("Debit and credit amounts cannot be negative.");
        if (debitAmount == 0m && creditAmount == 0m) throw new DomainRuleException("A journal line must have a debit or credit amount.");
        if (debitAmount > 0m && creditAmount > 0m) throw new DomainRuleException("A journal line cannot have both debit and credit amounts.");
        if (description?.Trim().Length > 500) throw new DomainRuleException("Journal line description cannot exceed 500 characters.");
        return new GL_JournalLine(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, lineNumber, accountId, description?.Trim() ?? string.Empty, debitAmount, creditAmount);
    }

    internal void MarkDeletedForAggregate(Guid actorUserName, DateTimeOffset deletedOn) => MarkDeleted(actorUserName, deletedOn);
}

public sealed class Gl_Journal : AuditableEntity
{
    private readonly List<GL_JournalLine> _lines = [];

    private Gl_Journal()
    {
    }

    private Gl_Journal(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid voucherTypeId, string number, DateOnly transactionDate, Guid fiscalYearId, string description)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        VoucherTypeId = voucherTypeId;
        Number = NormalizeRequired(number, "Journal number", 40);
        TransactionDate = transactionDate;
        FiscalYearId = fiscalYearId;
        Description = description?.Trim() ?? string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid VoucherTypeId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public DateOnly TransactionDate { get; private set; }
    public Guid FiscalYearId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public JournalStatus Status { get; private set; } = JournalStatus.Draft;
    public DateTimeOffset? PostedAtUtc { get; private set; }
    public IReadOnlyList<GL_JournalLine> Lines => _lines.AsReadOnly();
    public decimal TotalDebit => _lines.Where(x => !x.IsDeleted).Sum(x => x.DebitAmount);
    public decimal TotalCredit => _lines.Where(x => !x.IsDeleted).Sum(x => x.CreditAmount);

    public static Gl_Journal Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, Guid voucherTypeId, string number, DateOnly transactionDate, Guid fiscalYearId, string? description = null, Guid? id = null)
    {
        if (voucherTypeId == Guid.Empty) throw new DomainRuleException("A journal must reference a voucher type.");
        if (fiscalYearId == Guid.Empty) throw new DomainRuleException("A journal must reference a fiscal year.");
        return new Gl_Journal(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, voucherTypeId, number, transactionDate, fiscalYearId, description ?? string.Empty);
    }

    public Guid AddLine(Guid actorUserName, DateTimeOffset updatedOn, Guid accountId, string? description, decimal debitAmount, decimal creditAmount)
    {
        EnsureDraft();
        var lineNumber = _lines.Count == 0 ? 1 : _lines.Max(x => x.LineNumber) + 1;
        var line = GL_JournalLine.Create(CompanyId, actorUserName, updatedOn, lineNumber, accountId, description ?? string.Empty, debitAmount, creditAmount);
        _lines.Add(line);
        MarkUpdated(actorUserName, updatedOn);
        return line.Id;
    }

    public void RemoveLine(Guid actorUserName, DateTimeOffset updatedOn, Guid lineId)
    {
        EnsureDraft();
        var line = _lines.SingleOrDefault(x => x.Id == lineId) ?? throw new DomainRuleException("The journal line was not found.");
        line.MarkDeletedForAggregate(actorUserName, updatedOn);
        _lines.Remove(line);
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Post(Guid actorUserName, DateTimeOffset postedAtUtc, Erp_SetupFiscalYear fiscalYear, Erp_DayLock? dayLock = null)
    {
        EnsureDraft();
        if (fiscalYear.CompanyId != CompanyId) throw new DomainRuleException("The journal and fiscal year belong to different companies.");
        if (fiscalYear.Id != FiscalYearId) throw new DomainRuleException("The journal fiscal year does not match the selected fiscal year.");
        if (fiscalYear.Status != FiscalYearStatus.Open) throw new DomainRuleException("A journal cannot be posted into a closed fiscal year.");
        if (!fiscalYear.Contains(TransactionDate)) throw new DomainRuleException("The journal date is outside the fiscal year.");
        if (dayLock is not null && dayLock.Date == TransactionDate && dayLock.IsLocked) throw new DomainRuleException("The journal date is locked.");
        if (_lines.Count(x => !x.IsDeleted) < 2) throw new DomainRuleException("A journal must contain at least two lines before posting.");
        if (TotalDebit <= 0m || TotalCredit <= 0m || TotalDebit != TotalCredit) throw new DomainRuleException("Debit and credit totals must balance before posting.");

        Status = JournalStatus.Posted;
        PostedAtUtc = postedAtUtc.ToUniversalTime();
        MarkUpdated(actorUserName, postedAtUtc);
    }

    public void Cancel(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureDraft();
        Status = JournalStatus.Cancelled;
        MarkUpdated(actorUserName, updatedOn);
    }

    private void EnsureDraft()
    {
        if (IsDeleted) throw new DomainRuleException("A deleted journal cannot be changed.");
        if (Status != JournalStatus.Draft) throw new DomainRuleException("Posted or cancelled journals cannot be edited.");
    }

    private static string NormalizeRequired(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException($"{fieldName} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new DomainRuleException($"{fieldName} cannot exceed {maxLength} characters.");
        return normalized;
    }
}