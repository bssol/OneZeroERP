using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.GeneralLedger;

namespace OneZeroErp.UnitTests;

public sealed class ManufacturingChartOfAccountsSeedTests
{
    [Fact]
    public void Manufacturing_chart_has_valid_unique_hierarchy_and_posting_leaves()
    {
        var accounts = ManufacturingChartOfAccountsSeed.Accounts;
        var byNumber = accounts.ToDictionary(x => x.AccountNo, StringComparer.Ordinal);
        var positionByNumber = accounts.Select((account, index) => (account.AccountNo, index))
            .ToDictionary(x => x.AccountNo, x => x.index, StringComparer.Ordinal);

        Assert.Equal(accounts.Count, byNumber.Count);
        Assert.Equal(Enum.GetValues<AccountType>().Length, accounts.Count(x => x.ParentAccountNo is null));

        foreach (var account in accounts)
        {
            Assert.Matches("^[1-5][0-9]{8}$", account.AccountNo);
            Assert.False(string.IsNullOrWhiteSpace(account.Title));
            Assert.False(string.IsNullOrWhiteSpace(account.Description));

            if (account.ParentAccountNo is null)
            {
                Assert.False(account.IsPostingAccount);
                Assert.Equal(ChartOfAccountNumbering.RootAccountNo[account.AccountType], account.AccountNo);
                continue;
            }

            Assert.True(byNumber.TryGetValue(account.ParentAccountNo, out var parent));
            Assert.Equal(account.AccountType, parent!.AccountType);
            Assert.False(parent.IsPostingAccount);
            Assert.True(positionByNumber[parent.AccountNo] < positionByNumber[account.AccountNo]);
        }

        foreach (var group in accounts.Where(x => !x.IsPostingAccount))
            Assert.Contains(accounts, x => x.ParentAccountNo == group.AccountNo);
    }
}
