using System.ComponentModel.DataAnnotations;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record CurrencyListItem(Guid Id, Guid CompanyId, string Code, string Name, string Symbol, int DecimalPlaces, bool IsBaseCurrency, bool IsActive, DateTimeOffset CreatedAtUtc);

public sealed class CurrencyEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    [Required, StringLength(10, MinimumLength = 3)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(8, MinimumLength = 1)] public string Symbol { get; set; } = string.Empty;
    [Range(0, 6)] public int DecimalPlaces { get; set; } = 2;
    public bool IsBaseCurrency { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record CurrencyOperationResult(bool Succeeded, string? ErrorMessage = null, CurrencyListItem? Item = null);

public interface ICurrencyService
{
    Task<PagedResult<CurrencyListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<CurrencyOperationResult> SaveAsync(CurrencyEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}

public sealed record ExchangeRateListItem(Guid Id, Guid CompanyId, Guid CurrencyId, string CurrencyCode, string CurrencyName, DateOnly EffectiveDate, decimal RateToBase, DateTimeOffset CreatedAtUtc);

public sealed class ExchangeRateEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Range(typeof(decimal), "0.00000001", "999999999999.99999999")] public decimal RateToBase { get; set; } = 1m;
}

public sealed record ExchangeRateOperationResult(bool Succeeded, string? ErrorMessage = null, ExchangeRateListItem? Item = null);

public interface IExchangeRateService
{
    Task<PagedResult<ExchangeRateListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyListItem>> GetCurrencyOptionsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<ExchangeRateOperationResult> SaveAsync(ExchangeRateEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}
