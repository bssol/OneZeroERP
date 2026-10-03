using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class VoucherTypeService(IUnitOfWork unitOfWork, IClock clock) : IVoucherTypeService
{
    public async Task<PagedResult<VoucherTypeListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.VoucherTypes.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderBy(x => x.Code), cancellationToken);
        return new(page.Items.Select(Map).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<VoucherTypeListItem?> GetAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default) =>
        MapOrNull(await unitOfWork.VoucherTypes.SingleOrDefaultNonTrackingAsync(x => x.CompanyId == companyId && x.Id == id, cancellationToken));

    public async Task<VoucherTypeOperationResult> SaveAsync(VoucherTypeEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var code = model.Code.Trim().ToUpperInvariant();
        var description = model.Description.Trim();
        if (model.CompanyId == Guid.Empty) return new(false, "Company is required.");
        if (code.Length is 0 or > 20) return new(false, "Voucher type code must be between 1 and 20 characters.");
        if (description.Length is 0 or > 200) return new(false, "Voucher type description must be between 1 and 200 characters.");
        if (model.RequiresBankAccount && model.RequiresCashAccount) return new(false, "A voucher type cannot require both a bank and cash account.");
        if (await unitOfWork.VoucherTypes.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == code && x.Id != model.Id, cancellationToken))
            return new(false, "A voucher type with this code already exists for the company.");

        var now = clock.UtcNow;
        GL_VoucherTypeEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.VoucherTypes.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The voucher type no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.VoucherTypes.Add(entity);
        }

        entity.Code = code;
        entity.Description = description;
        entity.RequiresBankAccount = model.RequiresBankAccount;
        entity.RequiresCashAccount = model.RequiresCashAccount;
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "VoucherType", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity));
    }

    public async Task<VoucherTypeOperationResult> SetActiveAsync(Guid companyId, Guid id, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.VoucherTypes.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Id == id, cancellationToken);
        if (entity is null) return new(false, "The voucher type no longer exists.");
        var now = clock.UtcNow;
        var beforeSummary = Summary(entity);
        entity.IsActive = isActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = isActive ? "Activated" : "Deactivated", EntityType = "VoucherType", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity));
    }

    private static VoucherTypeListItem Map(GL_VoucherTypeEntity x) => new(x.Id, x.CompanyId, x.Code, x.Description, x.RequiresBankAccount, x.RequiresCashAccount, x.IsActive, x.CreatedAtUtc);
    private static VoucherTypeListItem? MapOrNull(GL_VoucherTypeEntity? x) => x is null ? null : Map(x);
    private static string Summary(GL_VoucherTypeEntity x) => $"CompanyId={x.CompanyId};Code={x.Code};Description={x.Description};Bank={x.RequiresBankAccount};Cash={x.RequiresCashAccount};IsActive={x.IsActive}";
}
