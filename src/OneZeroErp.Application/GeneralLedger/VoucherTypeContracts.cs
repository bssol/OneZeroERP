using System.ComponentModel.DataAnnotations;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record VoucherTypeListItem(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Description,
    bool RequiresBankAccount,
    bool RequiresCashAccount,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed class VoucherTypeEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }

    [Required, StringLength(20, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public bool RequiresBankAccount { get; set; }
    public bool RequiresCashAccount { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record VoucherTypeOperationResult(bool Succeeded, string? ErrorMessage = null, VoucherTypeListItem? Item = null);

public interface IVoucherTypeService
{
    Task<PagedResult<VoucherTypeListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<VoucherTypeListItem?> GetAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<VoucherTypeOperationResult> SaveAsync(VoucherTypeEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<VoucherTypeOperationResult> SetActiveAsync(Guid companyId, Guid id, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default);
}
