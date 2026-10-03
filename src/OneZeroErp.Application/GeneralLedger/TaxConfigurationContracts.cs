using System.ComponentModel.DataAnnotations;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record TaxConfigurationListItem(Guid Id, Guid CompanyId, string Code, string Name, GL_TaxType TaxType, decimal RatePercent, DateOnly EffectiveFrom, bool IsActive, DateTimeOffset CreatedAtUtc);

public sealed class TaxConfigurationEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    [Required, StringLength(30, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    public GL_TaxType TaxType { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal RatePercent { get; set; }
    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool IsActive { get; set; } = true;
}

public sealed record TaxConfigurationOperationResult(bool Succeeded, string? ErrorMessage = null, TaxConfigurationListItem? Item = null);

public interface ITaxConfigurationService
{
    Task<PagedResult<TaxConfigurationListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<TaxConfigurationOperationResult> SaveAsync(TaxConfigurationEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}
