using System.Text;
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

	public async Task<ChartOfAccountImportResult> ImportCsvAsync(Stream csvStream, Guid actorUserId, CancellationToken cancellationToken = default)
	{
		var parsed = await ParseImportRowsAsync(csvStream, cancellationToken);
		if (parsed.Errors.Count > 0) return new(false, 0, parsed.Errors);

		var existingAccounts = await unitOfWork.ChartOfAccounts.ListAsync(null, cancellationToken);
		var knownAccounts = existingAccounts.ToDictionary(x => x.AccountNo, StringComparer.OrdinalIgnoreCase);
		var candidates = existingAccounts
			.Select(x => new ChartOfAccountNumberCandidate(x.AccountNo, x.IsPostingAccount))
			.ToList();
		var planned = new List<GL_ChartOfAccountEntity>();
		var errors = new List<string>();

		foreach (var row in parsed.Rows)
		{
			var parentAccountNo = string.IsNullOrWhiteSpace(row.ParentAccountNo) ? null : row.ParentAccountNo.Trim().ToUpperInvariant();
			GL_ChartOfAccountEntity? parent = null;
			if (parentAccountNo is not null)
			{
				if (!knownAccounts.TryGetValue(parentAccountNo, out parent))
				{
					errors.Add($"Row {row.LineNumber}: parent account '{parentAccountNo}' was not found. Import parent rows before child rows.");
					continue;
				}
				if (parent.AccountType != row.AccountType)
				{
					errors.Add($"Row {row.LineNumber}: parent account '{parentAccountNo}' must have account type '{ChartOfAccountNumbering.AccountTypeTitle(row.AccountType)}'.");
					continue;
				}
				if (parent.IsPostingAccount || !parent.IsActive)
				{
					errors.Add($"Row {row.LineNumber}: parent account '{parentAccountNo}' must be active and non-posting.");
					continue;
				}
			}
			else if (knownAccounts.Values.Any(x => x.AccountType == row.AccountType && x.ParentAccountNo is null))
			{
				errors.Add($"Row {row.LineNumber}: a root account already exists for account type '{ChartOfAccountNumbering.AccountTypeTitle(row.AccountType)}'.");
				continue;
			}

			var accountNo = parent is null
				? ChartOfAccountNumbering.RootAccountNo[row.AccountType]
				: ChartOfAccountNumbering.NextChildAccountNumber(parent.AccountNo, row.IsPostingAccount, candidates);
			if (accountNo is null || knownAccounts.ContainsKey(accountNo))
			{
				errors.Add($"Row {row.LineNumber}: the next account number could not be generated.");
				continue;
			}

			var entity = new GL_ChartOfAccountEntity
			{
				Id = Guid.NewGuid(),
				AccountNo = accountNo,
				ParentAccountNo = parentAccountNo,
				ParentAccountTitle = parent?.Title,
				AccountLevel = parent is null ? 0 : parent.AccountLevel + 1,
				AccountType = row.AccountType,
				Title = row.Title,
				Description = row.Description,
				IsPostingAccount = row.IsPostingAccount,
				IsActive = row.IsActive
			};
			knownAccounts.Add(accountNo, entity);
			candidates.Add(new(accountNo, row.IsPostingAccount));
			planned.Add(entity);
		}

		if (errors.Count > 0) return new(false, 0, errors);

		var now = clock.UtcNow;
		foreach (var entity in planned)
		{
			entity.CreatedAtUtc = now;
			entity.UpdatedAtUtc = now;
			unitOfWork.ChartOfAccounts.Add(entity);
			unitOfWork.AuditEvents.Add(new AuditEventEntity
			{
				Id = Guid.NewGuid(),
				ActorUserId = actorUserId,
				Action = "Imported",
				EntityType = "ChartOfAccount",
				EntityId = entity.Id,
				OccurredAtUtc = now,
				AfterSummary = Summary(entity)
			});
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
		return new(true, planned.Count, Array.Empty<string>());
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

	private static async Task<ImportParseResult> ParseImportRowsAsync(Stream csvStream, CancellationToken cancellationToken)
	{
		using var reader = new StreamReader(csvStream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
		var headerLine = await reader.ReadLineAsync(cancellationToken);
		var expectedHeaders = new[] { "AccountType", "ParentAccountNo", "Title", "Description", "IsPostingAccount", "IsActive" };
		if (headerLine is null)
			return new([], ["The CSV file is empty."]);

		var headers = ParseCsvLine(headerLine);
		if (!headers.SequenceEqual(expectedHeaders, StringComparer.OrdinalIgnoreCase))
			return new([], [$"The CSV header must be: {string.Join(',', expectedHeaders)}"]);

		var rows = new List<ImportRow>();
		var errors = new List<string>();
		var lineNumber = 1;
		while (await reader.ReadLineAsync(cancellationToken) is { } line)
		{
			lineNumber++;
			if (string.IsNullOrWhiteSpace(line)) continue;
			var fields = ParseCsvLine(line);
			if (fields.Count != expectedHeaders.Length)
			{
				errors.Add($"Row {lineNumber}: expected {expectedHeaders.Length} columns but found {fields.Count}.");
				continue;
			}

			if (!TryParseAccountType(fields[0], out var accountType))
				errors.Add($"Row {lineNumber}: account type '{fields[0]}' is invalid.");
			if (!TryParseBoolean(fields[4], out var isPostingAccount))
				errors.Add($"Row {lineNumber}: IsPostingAccount must be true/false, yes/no, or 1/0.");
			if (!TryParseBoolean(fields[5], out var isActive))
				errors.Add($"Row {lineNumber}: IsActive must be true/false, yes/no, or 1/0.");
			if (string.IsNullOrWhiteSpace(fields[2]))
				errors.Add($"Row {lineNumber}: Title is required.");

			if (errors.Count == 0 || errors.All(error => !error.StartsWith($"Row {lineNumber}:", StringComparison.Ordinal)))
				rows.Add(new(lineNumber, accountType, fields[1], fields[2].Trim(), NullIfEmpty(fields[3]), isPostingAccount, isActive));
		}

		return new(rows, errors);
	}

	private static bool TryParseAccountType(string value, out AccountType accountType)
	{
		var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
		accountType = normalized switch
		{
			"1" or "asset" or "assets" => AccountType.Asset,
			"2" or "liability" or "liabilities" => AccountType.Liability,
			"3" or "equity" => AccountType.Equity,
			"4" or "revenue" or "revenues" => AccountType.Revenue,
			"5" or "expense" or "expenses" => AccountType.Expense,
			_ => default
		};
		return normalized is "1" or "asset" or "assets" or "2" or "liability" or "liabilities" or "3" or "equity" or "4" or "revenue" or "revenues" or "5" or "expense" or "expenses";
	}

	private static bool TryParseBoolean(string value, out bool result)
	{
		result = value.Trim().ToLowerInvariant() switch
		{
			"true" or "yes" or "1" => true,
			"false" or "no" or "0" => false,
			_ => false
		};
		return value.Trim().ToLowerInvariant() is "true" or "yes" or "1" or "false" or "no" or "0";
	}

	private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private static List<string> ParseCsvLine(string line)
	{
		var fields = new List<string>();
		var field = new StringBuilder();
		var quoted = false;
		for (var index = 0; index < line.Length; index++)
		{
			var character = line[index];
			if (character == '"')
			{
				if (quoted && index + 1 < line.Length && line[index + 1] == '"')
				{
					field.Append('"');
					index++;
				}
				else quoted = !quoted;
			}
			else if (character == ',' && !quoted)
			{
				fields.Add(field.ToString().Trim());
				field.Clear();
			}
			else field.Append(character);
		}
		fields.Add(field.ToString().Trim());
		return fields;
	}

	private sealed record ImportRow(int LineNumber, AccountType AccountType, string ParentAccountNo, string Title, string? Description, bool IsPostingAccount, bool IsActive);
	private sealed record ImportParseResult(IReadOnlyList<ImportRow> Rows, IReadOnlyList<string> Errors);
}
