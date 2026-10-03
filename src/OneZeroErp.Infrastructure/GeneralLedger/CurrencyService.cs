using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class CurrencyService(IUnitOfWork unitOfWork, IClock clock) : ICurrencyService, IExchangeRateService
{
    public async Task<PagedResult<CurrencyListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.Currencies.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderBy(x => x.Code), cancellationToken);
        return new(page.Items.Select(Map).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<CurrencyOperationResult> SaveAsync(CurrencyEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.CompanyId == Guid.Empty) return new(false, "Company is required.");
        var code = model.Code.Trim().ToUpperInvariant();
        var name = model.Name.Trim();
        var symbol = model.Symbol.Trim();
        if (code.Length is < 3 or > 10) return new(false, "Currency code must be between 3 and 10 characters.");
        if (name.Length is 0 or > 120) return new(false, "Currency name is required and cannot exceed 120 characters.");
        if (symbol.Length is 0 or > 8) return new(false, "Currency symbol is required and cannot exceed 8 characters.");
        if (model.DecimalPlaces is < 0 or > 6) return new(false, "Decimal places must be between 0 and 6.");
        if (!model.IsActive && model.IsBaseCurrency) return new(false, "The base currency must remain active.");
        if (await unitOfWork.Currencies.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == code && x.Id != model.Id, cancellationToken))
            return new(false, "A currency with this code already exists for the company.");
        if (model.IsBaseCurrency && await unitOfWork.Currencies.AnyAsync(x => x.CompanyId == model.CompanyId && x.IsBaseCurrency && x.Id != model.Id, cancellationToken))
            return new(false, "The company already has a base currency. Clear it before selecting another base currency.");

        var now = clock.UtcNow;
        GL_CurrencyEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.Currencies.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The currency no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.Currencies.Add(entity);
        }

        entity.Code = code;
        entity.Name = name;
        entity.Symbol = symbol;
        entity.DecimalPlaces = model.DecimalPlaces;
        entity.IsBaseCurrency = model.IsBaseCurrency;
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "Currency", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity));
    }

    async Task<PagedResult<ExchangeRateListItem>> IExchangeRateService.GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken)
    {
        var currencies = (await unitOfWork.Currencies.ListAsync(x => x.CompanyId == companyId, cancellationToken)).ToDictionary(x => x.Id);
        var page = await unitOfWork.ExchangeRates.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderByDescending(x => x.EffectiveDate), cancellationToken);
        return new(page.Items.Select(x => Map(x, currencies)).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<CurrencyListItem>> GetCurrencyOptionsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        (await unitOfWork.Currencies.ListAsync(x => x.CompanyId == companyId && x.IsActive, cancellationToken)).OrderBy(x => x.Code).Select(Map).ToList();

    public async Task<ExchangeRateOperationResult> SaveAsync(ExchangeRateEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.CompanyId == Guid.Empty) return new(false, "Company is required.");
        if (model.CurrencyId == Guid.Empty) return new(false, "Currency is required.");
        if (model.EffectiveDate == default) return new(false, "Effective date is required.");
        if (model.RateToBase <= 0m) return new(false, "Exchange rate must be greater than zero.");
        var currency = await unitOfWork.Currencies.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.CurrencyId, cancellationToken);
        if (currency is null) return new(false, "The selected currency does not exist for the company.");
        if (!currency.IsActive) return new(false, "An inactive currency cannot receive an exchange rate.");
        if (currency.IsBaseCurrency && model.RateToBase != 1m) return new(false, "The base currency exchange rate must be 1.");
        if (await unitOfWork.ExchangeRates.AnyAsync(x => x.CompanyId == model.CompanyId && x.CurrencyId == model.CurrencyId && x.EffectiveDate == model.EffectiveDate && x.Id != model.Id, cancellationToken))
            return new(false, "An exchange rate already exists for this currency and effective date.");

        var now = clock.UtcNow;
        GL_ExchangeRateEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.ExchangeRates.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The exchange rate no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.ExchangeRates.Add(entity);
        }

        entity.CurrencyId = model.CurrencyId;
        entity.EffectiveDate = model.EffectiveDate;
        entity.RateToBase = model.RateToBase;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "ExchangeRate", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity, new Dictionary<Guid, GL_CurrencyEntity> { [currency.Id] = currency }));
    }

    private static CurrencyListItem Map(GL_CurrencyEntity x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.Symbol, x.DecimalPlaces, x.IsBaseCurrency, x.IsActive, x.CreatedAtUtc);

    private static ExchangeRateListItem Map(GL_ExchangeRateEntity x, IReadOnlyDictionary<Guid, GL_CurrencyEntity> currencies)
    {
        currencies.TryGetValue(x.CurrencyId, out var currency);
        return new(x.Id, x.CompanyId, x.CurrencyId, currency?.Code ?? "—", currency?.Name ?? "Currency unavailable", x.EffectiveDate, x.RateToBase, x.CreatedAtUtc);
    }

    private static string Summary(GL_CurrencyEntity x) => $"CompanyId={x.CompanyId};Code={x.Code};Name={x.Name};Symbol={x.Symbol};DecimalPlaces={x.DecimalPlaces};IsBaseCurrency={x.IsBaseCurrency};IsActive={x.IsActive}";
    private static string Summary(GL_ExchangeRateEntity x) => $"CompanyId={x.CompanyId};CurrencyId={x.CurrencyId};EffectiveDate={x.EffectiveDate:O};RateToBase={x.RateToBase}";
}
