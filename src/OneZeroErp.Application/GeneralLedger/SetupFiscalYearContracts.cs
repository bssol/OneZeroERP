using OneZeroErp.Domain.GeneralLedger.Setup;
using System.ComponentModel.DataAnnotations;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record SetupFiscalYearListItem(
    Guid Id,
    Guid CompanyId,
    string Code,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearStatus Status,
    DateTimeOffset CreatedAtUtc);

public sealed class SetupFiscalYearEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }

    [Required, StringLength(30, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).AddYears(1).AddDays(-1);

    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;
}

public sealed record SetupFiscalYearOperationResult(bool Succeeded, string? ErrorMessage = null, SetupFiscalYearListItem? Item = null);

public sealed record DayLockListItem(
    Guid Id,
    Guid CompanyId,
    Guid FiscalYearId,
    string FiscalYearCode,
    DateOnly Date,
    bool IsLocked,
    Guid? LockedByUserId,
    DateTimeOffset? LockedAtUtc);

public sealed class DayLockEditModel
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string FiscalYearCode { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public bool IsLocked { get; set; }
}

public sealed record DayLockOperationResult(bool Succeeded, string? ErrorMessage = null, DayLockListItem? Item = null);

public interface ISetupFiscalYearService
{
    Task<PagedResult<SetupFiscalYearListItem>> GetPageAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<SetupFiscalYearListItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SetupFiscalYearOperationResult> SaveAsync(SetupFiscalYearEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<SetupFiscalYearOperationResult> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IDayLockService
{
    Task<PagedResult<DayLockListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<DayLockOperationResult> SaveAsync(DayLockEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}
