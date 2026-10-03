using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain.GeneralLedger;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class ChartOfAccountService(IUnitOfWork unitOfWork, IClock clock) : IChartOfAccountService
{
	public async Task<PagedResult<ChartOfAccountListItem>> GetPageAsync(PagedQuery query, CancellationToken cancellationToken = default)
	{
		var page = await unitOfWork.ChartOfAccounts.GetPageAsync(query, source => source.OrderBy(x => x.AccountNo), cancellationToken);
		return new(page.Items.Select(Map).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
	}

	public async Task<ChartOfAccountListItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
			MapOrNull(await unitOfWork.ChartOfAccounts.SingleOrDefaultNonTrackingAsync(x => x.Id == id, cancellationToken));

	public async Task<IReadOnlyList<ChartOfAccountParentOption>> GetParentOptionsAsync(AccountType accountType, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
			(await unitOfWork.ChartOfAccounts.ListAsync(x => x.AccountType == accountType && !x.IsPostingAccount && x.IsActive && x.Id != excludeId, cancellationToken))
					.OrderBy(x => x.AccountNo)
					.Select(x => new ChartOfAccountParentOption(x.Id, x.AccountNo, x.Title, x.AccountLevel))
					.ToList();

	public async Task<IReadOnlyList<ChartOfAccountTreeItem>> GetParentTreeAsync(CancellationToken cancellationToken = default)
	{
		var accounts = await unitOfWork.ChartOfAccounts.ListAsync(x => !x.IsPostingAccount && x.IsActive, cancellationToken);
		return BuildTree(accounts, null);
	}

	private static IReadOnlyList<ChartOfAccountTreeItem> BuildTree(IReadOnlyList<GL_ChartOfAccountEntity> accounts, string? parentAccountNo) =>
		accounts
			.Where(x => string.Equals(x.ParentAccountNo, parentAccountNo, StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.AccountNo)
			.Select(x => new ChartOfAccountTreeItem(
				x.Id,
				x.AccountNo,
				x.Title,
				x.AccountType,
				x.AccountLevel,
				BuildTree(accounts, x.AccountNo)))
			.ToList();

	public async Task<ChartOfAccountNumberSuggestion> GetAccountNumberSuggestionAsync(AccountType accountType, string? parentAccountNo, bool isPostingAccount, Guid? excludeId = null, CancellationToken cancellationToken = default)
	{
		var accounts = await unitOfWork.ChartOfAccounts.ListAsync(x => x.Id != excludeId, cancellationToken);
		var parentOptions = accounts.Where(x => x.AccountType == accountType && !x.IsPostingAccount && x.IsActive).ToList();

		if (string.IsNullOrWhiteSpace(parentAccountNo))
		{
			if (parentOptions.Count > 0) return new(null, null, true);

			var rootExists = accounts.Any(x => x.AccountType == accountType && x.ParentAccountNo is null);
			return rootExists
					? new(null, null, true)
					: new(ChartOfAccountNumbering.RootAccountNo[accountType], ChartOfAccountNumbering.AccountTypeTitle(accountType), false);
		}

		var parent = accounts.SingleOrDefault(x => x.AccountNo.Equals(parentAccountNo.Trim(), StringComparison.OrdinalIgnoreCase));
		if (parent is null || parent.AccountType != accountType || parent.IsPostingAccount || !parent.IsActive)
			return new(null, null, true);

		var candidates = accounts
				.Select(x => new ChartOfAccountNumberCandidate(x.AccountNo, x.IsPostingAccount))
				.ToList();
		var accountNo = ChartOfAccountNumbering.NextChildAccountNumber(parent.AccountNo, isPostingAccount, candidates);
		return new(accountNo, null, false);
	}

	public async Task<ChartOfAccountOperationResult> SaveAsync(ChartOfAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
	{
		var parentAccountNo = string.IsNullOrWhiteSpace(model.ParentAccountNo) ? null : model.ParentAccountNo.Trim().ToUpperInvariant();
		var parentAccountTitle = string.IsNullOrWhiteSpace(model.ParentAccountTitle) ? null : model.ParentAccountTitle.Trim();

		GL_ChartOfAccountEntity? parent = null;
		if (parentAccountNo is not null)
		{
			parent = await unitOfWork.ChartOfAccounts.SingleOrDefaultAsync(x => x.AccountNo == parentAccountNo, cancellationToken);
			if (parent is null) return new(false, "The selected parent account does not exist.");
			if (parent.AccountType != model.AccountType) return new(false, "The selected parent account must have the same account type.");
			if (parent.IsPostingAccount) return new(false, "A posting account cannot be used as a parent account.");
			if (parent.Id == model.Id) return new(false, "An account cannot be its own parent.");
			parentAccountTitle = parent.Title;
		}

		if (!model.Id.HasValue)
		{
			var suggestion = await GetAccountNumberSuggestionAsync(model.AccountType, parentAccountNo, model.IsPostingAccount, cancellationToken: cancellationToken);
			if (suggestion.RequiresParent && parent is null)
				return new(false, "Select a parent account for this account type.");
			if (suggestion.AccountNo is null)
				return new(false, "The next account number could not be generated.");

			model.AccountNo = suggestion.AccountNo;
			if (parent is null) model.Title = suggestion.SuggestedTitle!;
		}

		model.AccountLevel = parent is null ? 0 : parent.AccountLevel + 1;
		var validationError = Validate(model);
		if (validationError is not null) return new(false, validationError);

		var now = clock.UtcNow;
		GL_ChartOfAccountEntity entity;
		string? beforeSummary = null;
		if (model.Id.HasValue)
		{
			var existing = await unitOfWork.ChartOfAccounts.SingleOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken);
			if (existing is null) return new(false, "The chart of account no longer exists.");
			entity = existing;
			beforeSummary = Summary(entity);
			model.AccountNo = entity.AccountNo;
		}
		else
		{
			entity = new GL_ChartOfAccountEntity { Id = Guid.NewGuid(), CreatedAtUtc = now, UpdatedAtUtc = now };
			unitOfWork.ChartOfAccounts.Add(entity);
		}

		var accountNo = model.AccountNo.Trim().ToUpperInvariant();
		if (await unitOfWork.ChartOfAccounts.AnyAsync(x => x.AccountNo == accountNo && x.Id != model.Id, cancellationToken))
			return new(false, "An account with this number already exists.");

		entity.AccountNo = accountNo;
		entity.ParentAccountNo = parentAccountNo;
		entity.ParentAccountTitle = parentAccountTitle;
		entity.AccountLevel = model.AccountLevel;
		entity.AccountType = model.AccountType;
		entity.Title = model.Title.Trim();
		entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
		entity.IsPostingAccount = model.IsPostingAccount;
		entity.IsActive = model.IsActive;
		entity.UpdatedAtUtc = now;

		unitOfWork.AuditEvents.Add(new AuditEventEntity
		{
			Id = Guid.NewGuid(),
			ActorUserId = actorUserId,
			Action = model.Id.HasValue ? "Updated" : "Created",
			EntityType = "ChartOfAccount",
			EntityId = entity.Id,
			OccurredAtUtc = now,
			BeforeSummary = beforeSummary,
			AfterSummary = Summary(entity)
		});

		await unitOfWork.SaveChangesAsync(cancellationToken);
		return new(true, Item: Map(entity));
	}

	public async Task<ChartOfAccountOperationResult> SetActiveAsync(Guid id, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default)
	{
		var entity = await unitOfWork.ChartOfAccounts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (entity is null) return new(false, "The chart of account no longer exists.");

		var now = clock.UtcNow;
		var beforeSummary = Summary(entity);
		entity.IsActive = isActive;
		entity.UpdatedAtUtc = now;
		unitOfWork.AuditEvents.Add(new AuditEventEntity
		{
			Id = Guid.NewGuid(),
			ActorUserId = actorUserId,
			Action = isActive ? "Activated" : "Deactivated",
			EntityType = "ChartOfAccount",
			EntityId = entity.Id,
			OccurredAtUtc = now,
			BeforeSummary = beforeSummary,
			AfterSummary = Summary(entity)
		});

		await unitOfWork.SaveChangesAsync(cancellationToken);
		return new(true, Item: Map(entity));
	}

	private static string? Validate(ChartOfAccountEditModel model)
	{
		if (model.Id.HasValue && string.IsNullOrWhiteSpace(model.AccountNo)) return "Account number is required.";
		if (model.AccountNo.Trim().Length > 30) return "Account number cannot exceed 30 characters.";
		if (string.IsNullOrWhiteSpace(model.Title)) return "Account title is required.";
		if (model.Title.Trim().Length > 160) return "Account title cannot exceed 160 characters.";
		if (model.Description?.Trim().Length > 1000) return "Account description cannot exceed 1000 characters.";
		if (model.ParentAccountNo?.Trim().Equals(model.AccountNo.Trim(), StringComparison.OrdinalIgnoreCase) == true)
			return "An account cannot be its own parent.";
		if (model.AccountLevel < 0) return "Account level cannot be negative.";
		if (!Enum.IsDefined(model.AccountType)) return "The account type is invalid.";

		if (model.AccountLevel == 0 && !string.IsNullOrWhiteSpace(model.ParentAccountNo))
			return "A root account cannot have a parent account.";
		if (model.AccountLevel > 0 && string.IsNullOrWhiteSpace(model.ParentAccountNo))
			return "A child account must have a parent account.";
		return null;
	}

	private static ChartOfAccountListItem Map(GL_ChartOfAccountEntity entity) => new(
			entity.Id,
			entity.AccountNo,
			entity.ParentAccountNo,
			entity.ParentAccountTitle,
			entity.AccountLevel,
			entity.AccountType,
			entity.Title,
			entity.Description,
			entity.IsPostingAccount,
			entity.IsActive,
			entity.CreatedAtUtc);

	private static ChartOfAccountListItem? MapOrNull(GL_ChartOfAccountEntity? entity) => entity is null ? null : Map(entity);

	private static string Summary(GL_ChartOfAccountEntity entity) =>
			$"AccountNo={entity.AccountNo};ParentAccountNo={entity.ParentAccountNo};Level={entity.AccountLevel};Type={entity.AccountType};Title={entity.Title};IsPostingAccount={entity.IsPostingAccount};IsActive={entity.IsActive}";
}
