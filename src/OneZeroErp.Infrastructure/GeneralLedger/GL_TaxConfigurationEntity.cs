using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_TaxConfigurationEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GL_TaxType TaxType { get; set; }
    public decimal RatePercent { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
