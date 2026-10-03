using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain.ErpSetups;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class SetupFiscalYearService(IUnitOfWork unitOfWork,IClock clock) : ISetupFiscalYearService
{
    public async Task<PagedResult<SetupFiscalYearListItem>> GetPageAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.FiscalYears.GetPageAsync(query, source => source.OrderByDescending(x => x.StartDate), cancellationToken);
        return new(page.Items.Select(Map).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<SetupFiscalYearListItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        MapOrNull(await unitOfWork.FiscalYears.SingleOrDefaultNonTrackingAsync(x => x.Id == id, cancellationToken));

    public async Task<SetupFiscalYearOperationResult> SaveAsync(SetupFiscalYearEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var ruleError = Erp_FiscalYearRules.Validate(model.Code, model.StartDate, model.EndDate);
        if (ruleError is not null) return new(false, ruleError);
        if (!Enum.IsDefined(model.Status)) return new(false, "The fiscal year status is invalid.");
        var normalizedCode = model.Code.Trim().ToUpperInvariant();
        var duplicateCode = await unitOfWork.FiscalYears.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == normalizedCode && x.Id != model.Id, cancellationToken);
        if (duplicateCode) return new(false, "A fiscal year with this code already exists.");

        var overlaps = await unitOfWork.FiscalYears.AnyAsync(x => x.CompanyId == model.CompanyId && x.Id != model.Id &&
            model.StartDate <= x.EndDate && x.StartDate <= model.EndDate, cancellationToken);
        if (overlaps) return new(false, "The fiscal year dates overlap an existing fiscal year.");

        var now = clock.UtcNow;
        string? beforeSummary = null;
        Erp_SetupFiscalYearEntity entity;
        if (model.Id.HasValue)
        {
            var existing = await unitOfWork.FiscalYears.SingleOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken);
            if (existing is null) return new(false, "The fiscal year no longer exists.");
            entity = existing;
            if (entity.Status == FiscalYearStatus.Closed &&
                (entity.Code != normalizedCode || entity.StartDate != model.StartDate || entity.EndDate != model.EndDate))
                return new(false, "A closed fiscal year cannot be edited. Reopen it before changing its dates or code.");
            beforeSummary = $"Code={entity.Code};StartDate={entity.StartDate:O};EndDate={entity.EndDate:O};Status={entity.Status}";
            entity.Code = normalizedCode;
            entity.StartDate = model.StartDate;
            entity.EndDate = model.EndDate;
            entity.Status = model.Status;
            entity.UpdatedAtUtc = now;
        }
        else
        {
            entity = new Erp_SetupFiscalYearEntity { Id = Guid.NewGuid(), CompanyId = model.CompanyId, Code = normalizedCode, StartDate = model.StartDate, EndDate = model.EndDate, Status = model.Status, CreatedAtUtc = now, UpdatedAtUtc = now };
            unitOfWork.FiscalYears.Add(entity);
        }

        if (model.Id.HasValue && model.CompanyId == Guid.Empty)
            model.CompanyId = entity.CompanyId;
        if (model.Id.HasValue && entity.CompanyId != model.CompanyId)
            return new(false, "A fiscal year cannot be moved to another company.");

        await SynchronizeLockDatesAsync(entity, now, cancellationToken);

        unitOfWork.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "FiscalYear", EntityId = entity.Id, OccurredAtUtc = now,
            BeforeSummary = beforeSummary, AfterSummary = $"Code={entity.Code};StartDate={entity.StartDate:O};EndDate={entity.EndDate:O};Status={entity.Status}"
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity));
    }

    public async Task<SetupFiscalYearOperationResult> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.FiscalYears.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return new(false, "The fiscal year no longer exists.");
        unitOfWork.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = "Deleted", EntityType = "FiscalYear", EntityId = entity.Id, OccurredAtUtc = clock.UtcNow,
            BeforeSummary = $"Code={entity.Code};StartDate={entity.StartDate:O};EndDate={entity.EndDate:O};Status={entity.Status}"
        });
        unitOfWork.FiscalYears.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true);
    }

    private async Task SynchronizeLockDatesAsync(Erp_SetupFiscalYearEntity fiscalYear, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.LockDates.ListAsync(
            x => x.CompanyId == fiscalYear.CompanyId && x.FiscalYearId == fiscalYear.Id,
            cancellationToken);
        var dates = new HashSet<DateOnly>();
        for (var date = fiscalYear.StartDate; date <= fiscalYear.EndDate; date = date.AddDays(1))
            dates.Add(date);

        foreach (var lockDate in existing.Where(x => !dates.Contains(x.Date)))
            unitOfWork.LockDates.Remove(lockDate);

        var existingDates = existing.Select(x => x.Date).ToHashSet();
        foreach (var date in dates.Where(date => !existingDates.Contains(date)))
        {
            unitOfWork.LockDates.Add(new Erp_LockDateEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = fiscalYear.CompanyId,
                FiscalYearId = fiscalYear.Id,
                Date = date,
                IsLocked = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    private static SetupFiscalYearListItem Map(Erp_SetupFiscalYearEntity entity) => new(entity.Id, entity.CompanyId, entity.Code, entity.StartDate, entity.EndDate, entity.Status, entity.CreatedAtUtc);
    private static SetupFiscalYearListItem? MapOrNull(Erp_SetupFiscalYearEntity? entity) => entity is null ? null : Map(entity);
}
