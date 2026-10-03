using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class TaxConfigurationService(IUnitOfWork unitOfWork, IClock clock) : ITaxConfigurationService
{
    public async Task<PagedResult<TaxConfigurationListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.TaxConfigurations.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderBy(x => x.Code), cancellationToken);
        return new(page.Items.Select(Map).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<TaxConfigurationOperationResult> SaveAsync(TaxConfigurationEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.CompanyId == Guid.Empty) return new(false, "Company is required.");
        var code = model.Code.Trim().ToUpperInvariant();
        var name = model.Name.Trim();
        if (code.Length is 0 or > 30) return new(false, "Tax code is required and cannot exceed 30 characters.");
        if (name.Length is 0 or > 160) return new(false, "Tax name is required and cannot exceed 160 characters.");
        if (!Enum.IsDefined(model.TaxType)) return new(false, "The tax type is invalid.");
        if (model.RatePercent is < 0m or > 100m) return new(false, "Tax rate must be between 0 and 100 percent.");
        if (model.EffectiveFrom == default) return new(false, "Effective date is required.");
        if (await unitOfWork.TaxConfigurations.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == code && x.Id != model.Id, cancellationToken))
            return new(false, "A tax configuration with this code already exists for the company.");

        var now = clock.UtcNow;
        GL_TaxConfigurationEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.TaxConfigurations.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The tax configuration no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.TaxConfigurations.Add(entity);
        }

        entity.Code = code;
        entity.Name = name;
        entity.TaxType = model.TaxType;
        entity.RatePercent = model.RatePercent;
        entity.EffectiveFrom = model.EffectiveFrom;
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "TaxConfiguration", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity));
    }

    private static TaxConfigurationListItem Map(GL_TaxConfigurationEntity x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.TaxType, x.RatePercent, x.EffectiveFrom, x.IsActive, x.CreatedAtUtc);
    private static string Summary(GL_TaxConfigurationEntity x) => $"CompanyId={x.CompanyId};Code={x.Code};Name={x.Name};TaxType={x.TaxType};RatePercent={x.RatePercent};EffectiveFrom={x.EffectiveFrom:O};IsActive={x.IsActive}";
}
