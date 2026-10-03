using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class BankCashAccountService(IUnitOfWork unitOfWork, IClock clock) : IBankAccountService, ICashAccountService
{
    public async Task<PagedResult<BankAccountListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        var accounts = await GetPostingAccountsAsync(cancellationToken);
        var page = await unitOfWork.BankAccounts.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderBy(x => x.Code), cancellationToken);
        return new(page.Items.Select(x => Map(x, accounts)).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<GlPostingAccountOption>> GetGlAccountOptionsAsync(CancellationToken cancellationToken = default) =>
        (await GetPostingAccountsAsync(cancellationToken)).Values
            .OrderBy(x => x.AccountNo)
            .Select(x => new GlPostingAccountOption(x.Id, x.AccountNo, x.Title))
            .ToList();

    public async Task<BankAccountOperationResult> SaveAsync(BankAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var error = Validate(model.CompanyId, model.GlAccountId, model.Code, model.Name, model.BankName, model.AccountNumber);
        if (error is not null) return new(false, error);
        var accounts = await GetPostingAccountsAsync(cancellationToken);
        if (!accounts.ContainsKey(model.GlAccountId)) return new(false, "Select an active posting Asset account from the chart of accounts.");
        var code = model.Code.Trim().ToUpperInvariant();
        if (await unitOfWork.BankAccounts.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == code && x.Id != model.Id, cancellationToken))
            return new(false, "A bank account with this code already exists for the company.");
        if (await unitOfWork.BankAccounts.AnyAsync(x => x.CompanyId == model.CompanyId && x.GlAccountId == model.GlAccountId && x.Id != model.Id, cancellationToken))
            return new(false, "The selected GL account is already linked to a bank account.");

        var now = clock.UtcNow;
        GL_BankAccountEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.BankAccounts.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The bank account no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.BankAccounts.Add(entity);
        }

        entity.GlAccountId = model.GlAccountId;
        entity.Code = code;
        entity.Name = model.Name.Trim();
        entity.BankName = model.BankName.Trim();
        entity.AccountNumber = model.AccountNumber.Trim();
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "BankAccount", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity, accounts));
    }

    async Task<PagedResult<CashAccountListItem>> ICashAccountService.GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken)
    {
        var accounts = await GetPostingAccountsAsync(cancellationToken);
        var page = await unitOfWork.CashAccounts.GetPageAsync(query, source => source.Where(x => x.CompanyId == companyId).OrderBy(x => x.Code), cancellationToken);
        return new(page.Items.Select(x => Map(x, accounts)).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
    }

    public async Task<CashAccountOperationResult> SaveAsync(CashAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.CompanyId == Guid.Empty) return new(false, "Company is required.");
        if (model.GlAccountId == Guid.Empty) return new(false, "A general ledger account is required.");
        if (string.IsNullOrWhiteSpace(model.Code) || model.Code.Trim().Length > 30) return new(false, "Cash account code is required and cannot exceed 30 characters.");
        if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Trim().Length > 160) return new(false, "Cash account name is required and cannot exceed 160 characters.");
        if (string.IsNullOrWhiteSpace(model.Location) || model.Location.Trim().Length > 160) return new(false, "Cash account location is required and cannot exceed 160 characters.");
        var accounts = await GetPostingAccountsAsync(cancellationToken);
        if (!accounts.ContainsKey(model.GlAccountId)) return new(false, "Select an active posting Asset account from the chart of accounts.");
        var code = model.Code.Trim().ToUpperInvariant();
        if (await unitOfWork.CashAccounts.AnyAsync(x => x.CompanyId == model.CompanyId && x.Code == code && x.Id != model.Id, cancellationToken))
            return new(false, "A cash account with this code already exists for the company.");
        if (await unitOfWork.CashAccounts.AnyAsync(x => x.CompanyId == model.CompanyId && x.GlAccountId == model.GlAccountId && x.Id != model.Id, cancellationToken))
            return new(false, "The selected GL account is already linked to a cash account.");

        var now = clock.UtcNow;
        GL_CashAccountEntity entity;
        string? beforeSummary = null;
        if (model.Id.HasValue)
        {
            entity = await unitOfWork.CashAccounts.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.Id == model.Id.Value, cancellationToken)
                ?? throw new InvalidOperationException("The cash account no longer exists.");
            beforeSummary = Summary(entity);
        }
        else
        {
            entity = new() { Id = Guid.NewGuid(), CompanyId = model.CompanyId, CreatedAtUtc = now };
            unitOfWork.CashAccounts.Add(entity);
        }

        entity.GlAccountId = model.GlAccountId;
        entity.Code = code;
        entity.Name = model.Name.Trim();
        entity.Location = model.Location.Trim();
        entity.IsActive = model.IsActive;
        entity.UpdatedAtUtc = now;
        unitOfWork.AuditEvents.Add(new AuditEventEntity { Id = Guid.NewGuid(), ActorUserId = actorUserId, Action = model.Id.HasValue ? "Updated" : "Created", EntityType = "CashAccount", EntityId = entity.Id, OccurredAtUtc = now, BeforeSummary = beforeSummary, AfterSummary = Summary(entity) });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new(true, Item: Map(entity, accounts));
    }

    async Task<IReadOnlyList<GlPostingAccountOption>> ICashAccountService.GetGlAccountOptionsAsync(CancellationToken cancellationToken) =>
        await GetGlAccountOptionsAsync(cancellationToken);

    private async Task<Dictionary<Guid, GL_ChartOfAccountEntity>> GetPostingAccountsAsync(CancellationToken cancellationToken) =>
        (await unitOfWork.ChartOfAccounts.ListAsync(x => x.AccountType == AccountType.Asset && x.IsPostingAccount && x.IsActive, cancellationToken))
            .ToDictionary(x => x.Id);

    private static string? Validate(Guid companyId, Guid glAccountId, string code, string name, string bankName, string accountNumber)
    {
        if (companyId == Guid.Empty) return "Company is required.";
        if (glAccountId == Guid.Empty) return "A general ledger account is required.";
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 30) return "Bank account code is required and cannot exceed 30 characters.";
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) return "Bank account name is required and cannot exceed 160 characters.";
        if (string.IsNullOrWhiteSpace(bankName) || bankName.Trim().Length > 160) return "Bank name is required and cannot exceed 160 characters.";
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length > 80) return "Bank account number is required and cannot exceed 80 characters.";
        return null;
    }

    private static BankAccountListItem Map(GL_BankAccountEntity entity, IReadOnlyDictionary<Guid, GL_ChartOfAccountEntity> accounts)
    {
        accounts.TryGetValue(entity.GlAccountId, out var account);
        return new(entity.Id, entity.CompanyId, entity.GlAccountId, account?.AccountNo ?? "—", account?.Title ?? "Account unavailable", entity.Code, entity.Name, entity.BankName, entity.AccountNumber, entity.IsActive, entity.CreatedAtUtc);
    }

    private static CashAccountListItem Map(GL_CashAccountEntity entity, IReadOnlyDictionary<Guid, GL_ChartOfAccountEntity> accounts)
    {
        accounts.TryGetValue(entity.GlAccountId, out var account);
        return new(entity.Id, entity.CompanyId, entity.GlAccountId, account?.AccountNo ?? "—", account?.Title ?? "Account unavailable", entity.Code, entity.Name, entity.Location, entity.IsActive, entity.CreatedAtUtc);
    }

    private static string Summary(GL_BankAccountEntity entity) => $"CompanyId={entity.CompanyId};GlAccountId={entity.GlAccountId};Code={entity.Code};Name={entity.Name};BankName={entity.BankName};AccountNumber={entity.AccountNumber};IsActive={entity.IsActive}";
    private static string Summary(GL_CashAccountEntity entity) => $"CompanyId={entity.CompanyId};GlAccountId={entity.GlAccountId};Code={entity.Code};Name={entity.Name};Location={entity.Location};IsActive={entity.IsActive}";
}
