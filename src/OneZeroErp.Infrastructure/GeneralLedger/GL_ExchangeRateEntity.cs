namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_ExchangeRateEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal RateToBase { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
