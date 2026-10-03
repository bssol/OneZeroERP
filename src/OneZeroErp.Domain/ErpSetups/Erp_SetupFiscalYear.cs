using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Domain.ErpSetups;

public sealed class Erp_SetupFiscalYear : AuditableEntity
{
    private Erp_SetupFiscalYear()
    {
    }

    private Erp_SetupFiscalYear(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, string code, DateOnly startDate, DateOnly endDate, FiscalYearStatus status)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        Code = NormalizeCode(code);
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public FiscalYearStatus Status { get; private set; }

    public static Erp_SetupFiscalYear Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, string code, DateOnly startDate, DateOnly endDate, FiscalYearStatus status = FiscalYearStatus.Open, Guid? id = null)
    {
        Validate(code, startDate, endDate);
        return new Erp_SetupFiscalYear(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, code, startDate, endDate, status);
    }

    public void Update(Guid actorUserName, DateTimeOffset updatedOn, string code, DateOnly startDate, DateOnly endDate, FiscalYearStatus status)
    {
        EnsureOpenForConfiguration();
        Validate(code, startDate, endDate);
        Code = NormalizeCode(code);
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Close(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureNotDeleted();
        Status = FiscalYearStatus.Closed;
        MarkUpdated(actorUserName, updatedOn);
    }

    public void Reopen(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureNotDeleted();
        Status = FiscalYearStatus.Open;
        MarkUpdated(actorUserName, updatedOn);
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public static bool Overlaps(Erp_SetupFiscalYear first, Erp_SetupFiscalYear second) =>
        first.StartDate <= second.EndDate && second.StartDate <= first.EndDate;

    private void EnsureOpenForConfiguration()
    {
        EnsureNotDeleted();
        if (Status == FiscalYearStatus.Closed) throw new DomainRuleException("A closed fiscal year cannot be edited.");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted) throw new DomainRuleException("A deleted fiscal year cannot be changed.");
    }

    private static void Validate(string code, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("Fiscal year code is required.");
        if (code.Trim().Length > 30) throw new DomainRuleException("Fiscal year code cannot exceed 30 characters.");
        if (startDate > endDate) throw new DomainRuleException("The start date must be on or before the end date.");
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}