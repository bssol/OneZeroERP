namespace OneZeroErp.Domain.GeneralLedger.Setup;

public sealed record ChartOfAccountNumberCandidate(string AccountNo, bool IsPostingAccount);

public static class ChartOfAccountNumbering
{
	public static Dictionary<AccountType, string> RootAccountNo = new Dictionary<AccountType, string>
			{
					{AccountType.Asset, $"{(int)AccountType.Asset}00000000" },
					{AccountType.Liability, $"{(int)AccountType.Liability}00000000" },
					{AccountType.Equity, $"{(int)AccountType.Equity}00000000" },
					{AccountType.Expense, $"{(int)AccountType.Expense}00000000" },
					{AccountType.Revenue, $"{(int)AccountType.Revenue}00000000" },
			};




	public static string? NextChildAccountNumber(
			string parentAccountNo,
			bool isPostingAccount,
			IReadOnlyCollection<ChartOfAccountNumberCandidate> existingAccounts)
	{
		if (!long.TryParse(parentAccountNo, out var parentNumber) || parentNumber < 0)
			return null;

		var existingNumbers = existingAccounts
				.Select(x => x.AccountNo.Trim())
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

		return isPostingAccount
				? NextPostingAccountNumber(parentAccountNo.Trim(), existingNumbers)
				: NextSequentialAccountNumber(parentNumber, existingNumbers);
	}

	public static string AccountTypeTitle(AccountType accountType) => accountType switch
	{
		AccountType.Asset => "Assets",
		AccountType.Liability => "Liabilities",
		AccountType.Equity => "Equity",
		AccountType.Revenue => "Revenue",
		AccountType.Expense => "Expenses",
		_ => accountType.ToString()
	};

	private static string? NextSequentialAccountNumber(long parentNumber, HashSet<string> existingNumbers)
	{
		for (var candidate = parentNumber + 1; candidate <= 999_999_999; candidate++)
		{
			var accountNo = candidate.ToString("D9");
			if (!existingNumbers.Contains(accountNo)) return accountNo;
		}

		return null;
	}

	private static string? NextPostingAccountNumber(string parentAccountNo, HashSet<string> existingNumbers)
	{
		if (parentAccountNo.Length != 9 || parentAccountNo.Any(character => character is < '0' or > '9'))
			return null;

		var incrementPosition = -1;
		for (var index = 1; index < parentAccountNo.Length; index++)
		{
			if (parentAccountNo[index] < '9')
			{
				incrementPosition = index;
				break;
			}
		}

		if (incrementPosition < 0) return null;

		var candidate = parentAccountNo.ToCharArray();
		for (var digit = candidate[incrementPosition] - '0' + 1; digit <= 9; digit++)
		{
			candidate[incrementPosition] = (char)('0' + digit);
			var accountNo = new string(candidate);
			if (!existingNumbers.Contains(accountNo)) return accountNo;
		}

		return null;
	}
}
