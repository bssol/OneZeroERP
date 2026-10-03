namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed class GL_ChartOfAccount : AuditableEntity
{
	private GL_ChartOfAccount()
	{
	}

	private GL_ChartOfAccount(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, string accountNo, string? parentAccountNo, string? parentAccountTitle, int accountLevel, AccountType accountType, string title, string? description, bool isPostingAccount, bool isActive)
			: base(companyId, createdBy, createdOn)
	{
		Id = id;
		AccountNo = Normalize(accountNo, "Account number", 30).ToUpperInvariant();
		ParentAccountNo = NormalizeOptional(parentAccountNo, 30)?.ToUpperInvariant();
		ParentAccountTitle = NormalizeOptional(parentAccountTitle, 160);
		AccountLevel = accountLevel;
		AccountType = accountType;
		
		Title = Normalize(title, "Account title", 160);
		Description = NormalizeOptional(description, 1000);
		IsPostingAccount = isPostingAccount;
		IsActive = isActive;
	}

	public Guid Id { get; private set; }
	public string AccountNo { get; private set; } = string.Empty;
	public string? ParentAccountNo { get; private set; }
	public string? ParentAccountTitle { get; private set; }
	public int AccountLevel { get; private set; }
	public AccountType AccountType { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Description { get; private set; }
	public bool IsPostingAccount { get; private set; }
	public bool IsActive { get; private set; }

	public static GL_ChartOfAccount Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, string accountNo, string title, AccountType accountType, string? parentAccountNo = null, string? parentAccountTitle = null, int accountLevel = 0, string? description = null, bool isPostingAccount = true, bool isActive = true, Guid? id = null)
	{
		if (accountLevel < 0) throw new DomainRuleException("Account level cannot be negative.");
		if (accountLevel == 0 && parentAccountNo is not null) throw new DomainRuleException("A root account cannot have a parent account.");
		if (accountLevel > 0 && string.IsNullOrWhiteSpace(parentAccountNo)) throw new DomainRuleException("A child account must have a parent account.");
		if (string.Equals(accountNo.Trim(), parentAccountNo?.Trim(), StringComparison.OrdinalIgnoreCase)) throw new DomainRuleException("An account cannot be its own parent.");
		return new GL_ChartOfAccount(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, accountNo, parentAccountNo, parentAccountTitle, accountLevel, accountType, title, description, isPostingAccount, isActive);
	}

	public void Rename(Guid actorUserName, DateTimeOffset updatedOn, string title)
	{
		Title = Normalize(title, "Account title", 160);
		MarkUpdated(actorUserName, updatedOn);
	}

	public void ChangeDescription(Guid actorUserName, DateTimeOffset updatedOn, string? description)
	{
		Description = NormalizeOptional(description, 1000);
		MarkUpdated(actorUserName, updatedOn);
	}

	public void ChangeHierarchy(Guid actorUserName, DateTimeOffset updatedOn, string? parentAccountNo, string? parentAccountTitle, int accountLevel)
	{
		if (accountLevel < 0) throw new DomainRuleException("Account level cannot be negative.");
		if (accountLevel == 0 && parentAccountNo is not null) throw new DomainRuleException("A root account cannot have a parent account.");
		if (accountLevel > 0 && string.IsNullOrWhiteSpace(parentAccountNo)) throw new DomainRuleException("A child account must have a parent account.");
		if (string.Equals(AccountNo, parentAccountNo?.Trim(), StringComparison.OrdinalIgnoreCase)) throw new DomainRuleException("An account cannot be its own parent.");

		ParentAccountNo = NormalizeOptional(parentAccountNo, 30)?.ToUpperInvariant();
		ParentAccountTitle = NormalizeOptional(parentAccountTitle, 160);
		AccountLevel = accountLevel;
		MarkUpdated(actorUserName, updatedOn);
	}

	public void SetActive(Guid actorUserName, DateTimeOffset updatedOn, bool isActive)
	{
		IsActive = isActive;
		MarkUpdated(actorUserName, updatedOn);
	}

	private static string Normalize(string value, string fieldName, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleException($"{fieldName} is required.");
		var normalized = value.Trim();
		if (normalized.Length > maxLength) throw new DomainRuleException($"{fieldName} cannot exceed {maxLength} characters.");
		return normalized;
	}

	private static string? NormalizeOptional(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value)) return null;
		var normalized = value.Trim();
		if (normalized.Length > maxLength) throw new DomainRuleException($"A field cannot exceed {maxLength} characters.");
		return normalized;
	}
}

public sealed class GL_ChartOfAccounts : AuditableEntity
{
	private readonly List<GL_ChartOfAccount> _accounts = [];

	private GL_ChartOfAccounts()
	{
	}

	private GL_ChartOfAccounts(Guid companyId, Guid createdBy, DateTimeOffset createdOn)
			: base(companyId, createdBy, createdOn)
	{
	}

	public IReadOnlyList<GL_ChartOfAccount> Accounts => _accounts.AsReadOnly();

	public static GL_ChartOfAccounts Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn) => new(companyId, createdBy, createdOn);

	public void Add(GL_ChartOfAccount account, Guid actorUserName, DateTimeOffset updatedOn)
	{
		if (account.CompanyId != CompanyId) throw new DomainRuleException("The account belongs to a different company.");
		if (_accounts.Any(x => x.AccountNo.Equals(account.AccountNo, StringComparison.OrdinalIgnoreCase))) throw new DomainRuleException("An account with this account number already exists.");

		if (account.ParentAccountNo is not null)
		{
			var parent = _accounts.SingleOrDefault(x => x.AccountNo.Equals(account.ParentAccountNo, StringComparison.OrdinalIgnoreCase));
			if (parent is null) throw new DomainRuleException("The selected parent account does not exist.");
			if (account.AccountLevel != parent.AccountLevel + 1) throw new DomainRuleException("A child account level must be exactly one level below its parent.");
		}
		else if (account.AccountLevel != 0)
		{
			throw new DomainRuleException("A root account must have account level zero.");
		}

		_accounts.Add(account);
		MarkUpdated(actorUserName, updatedOn);
	}

	public IReadOnlyList<ChartOfAccountsTreeNode> GetTree() =>
			_accounts
					.Where(x => x.ParentAccountNo is null)
					.OrderBy(x => x.AccountNo)
					.Select(BuildNode)
					.ToList();

	private ChartOfAccountsTreeNode BuildNode(GL_ChartOfAccount account) =>
			new(
					account,
					_accounts
							.Where(x => x.ParentAccountNo is not null && x.ParentAccountNo.Equals(account.AccountNo, StringComparison.OrdinalIgnoreCase))
							.OrderBy(x => x.AccountNo)
							.Select(BuildNode)
							.ToList());
}

public sealed class ChartOfAccountsTreeNode
{
	internal ChartOfAccountsTreeNode(GL_ChartOfAccount account, IReadOnlyList<ChartOfAccountsTreeNode> children)
	{
		Account = account;
		Children = children;
	}

	public GL_ChartOfAccount Account { get; }
	public IReadOnlyList<ChartOfAccountsTreeNode> Children { get; }
}
