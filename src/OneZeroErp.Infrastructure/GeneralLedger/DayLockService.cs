using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class DayLockService(IUnitOfWork unitOfWork, IClock clock) : IDayLockService
{
    public async Task<PagedResult<DayLockListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        var fiscalYears = await unitOfWork.FiscalYears.ListAsync(x => x.CompanyId == companyId, cancellationToken);
        var fiscalYearCodes = fiscalYears.ToDictionary(x => x.Id, x => x.Code);
        var page = await unitOfWork.LockDates.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderByDescending(x => x.Date), cancellationToken);
        var items = page.Items
            .Where(x => fiscalYearCodes.ContainsKey(x.FiscalYearId))
            .Select(x => Map(x, fiscalYearCodes[x.FiscalYearId]))
            .ToList();
        return new(items, page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<DayLockOperationResult> SaveAsync(DayLockEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.LockDates.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id, cancellationToken);
        if (entity is null) return new(false, "The accounting day no longer exists.");

        var fiscalYear = await unitOfWork.FiscalYears.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == entity.FiscalYearId, cancellationToken);
        if (fiscalYear is null) return new(false, "The fiscal year for this accounting day no longer exists.");
        if (entity.Date < fiscalYear.StartDate || entity.Date > fiscalYear.EndDate)
            return new(false, "The accounting day is outside its fiscal year.");

        var now = clock.UtcNow;
        var beforeSummary = Summary(entity, fiscalYear.Code);
        entity.IsLocked = model.IsLocked;
        entity.LockedByUserId = model.IsLocked ? actorUserId : null;
        entity.LockedAtUtc = model.IsLocked ? now : null;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = model.IsLocked ? "Locked" : "Unlocked",
            EntityType = "AccountingDayLock",
            EntityId = entity.Id,
            OccurredAtUtc = now,
            BeforeSummary = beforeSummary,
            AfterSummary = Summary(entity, fiscalYear.Code)
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity, fiscalYear.Code));
    }

    private static DayLockListItem Map(Erp_LockDateEntity entity, string fiscalYearCode) => new(
        entity.Id,
        entity.CompanyId,
        entity.FiscalYearId,
        fiscalYearCode,
        entity.Date,
        entity.IsLocked,
        entity.LockedByUserId,
        entity.LockedAtUtc);

    private static string Summary(Erp_LockDateEntity entity, string fiscalYearCode) =>
        $"FiscalYear={fiscalYearCode};Date={entity.Date:O};IsLocked={entity.IsLocked};LockedByUserId={entity.LockedByUserId};LockedAtUtc={entity.LockedAtUtc:O}";
}
