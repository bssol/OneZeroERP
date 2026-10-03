using OneZeroErp.Domain;
using OneZeroErp.Domain.ErpSetups;
using OneZeroErp.Domain.GeneralLedger;
using OneZeroErp.Domain.GeneralLedger.Configurations;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.UnitTests;

public sealed class GlDomainModelTests
{
	private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
	private static readonly DateTimeOffset CreatedOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset UpdatedOn = CreatedOn.AddMinutes(1);

	[Fact]
	public void Journal_can_post_only_when_balanced_and_inside_an_open_unlocked_fiscal_year()
	{
		var fiscalYear = Erp_SetupFiscalYear.Create(CompanyId, ActorId, CreatedOn, "fy2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
		var voucherType = GL_VoucherType.Create(CompanyId, ActorId, CreatedOn, "JVV", "Journal Voucher");
		var journal = Gl_Journal.Create(CompanyId, ActorId, CreatedOn, voucherType.Id, "JVV-0001", new DateOnly(2026, 9, 25), fiscalYear.Id);
		journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Debit", 100m, 0m);
		journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Credit", 0m, 100m);

		journal.Post(ActorId, UpdatedOn, fiscalYear);

		Assert.Equal(JournalStatus.Posted, journal.Status);
		Assert.Throws<DomainRuleException>(() => journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Late edit", 1m, 0m));
		Assert.Equal(CompanyId, journal.CompanyId);
		Assert.Equal(ActorId, journal.CreatedBy);
	}

	[Fact]
	public void Journal_rejects_unbalanced_or_locked_posting()
	{
		var fiscalYear = Erp_SetupFiscalYear.Create(CompanyId, ActorId, CreatedOn, "FY2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
		var voucherType = GL_VoucherType.Create(CompanyId, ActorId, CreatedOn, "CPV", "Cash Payment Voucher", requiresCashAccount: true);
		var journal = Gl_Journal.Create(CompanyId, ActorId, CreatedOn, voucherType.Id, "CPV-0001", new DateOnly(2026, 9, 25), fiscalYear.Id);
		journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Debit", 100m, 0m);
		journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Credit", 0m, 90m);

		Assert.Throws<DomainRuleException>(() => journal.Post(ActorId, UpdatedOn, fiscalYear));

		journal.AddLine(ActorId, UpdatedOn, Guid.NewGuid(), "Balancing credit", 0m, 10m);
		var dayLock = Erp_DayLock.Create(CompanyId, ActorId, CreatedOn, journal.TransactionDate);
		dayLock.Lock(ActorId, UpdatedOn);

		Assert.Throws<DomainRuleException>(() => journal.Post(ActorId, UpdatedOn, fiscalYear, dayLock));
	}

	[Fact]
	public void Chart_of_accounts_builds_a_hierarchical_tree_from_parent_account_numbers()
	{
		var chart = GL_ChartOfAccounts.Create(CompanyId, ActorId, CreatedOn);
		var root = GL_ChartOfAccount.Create(CompanyId, ActorId, CreatedOn, "1000", "Assets", AccountType.Asset);
		var child = GL_ChartOfAccount.Create(CompanyId, ActorId, CreatedOn, "1100", "Cash", AccountType.Asset, "1000", "Assets", 1);

		chart.Add(root, ActorId, UpdatedOn);
		chart.Add(child, ActorId, UpdatedOn);

		var tree = chart.GetTree();

		Assert.Single(tree);
		Assert.Equal("1000", tree[0].Account.AccountNo);
		Assert.Single(tree[0].Children);
		Assert.Equal("1100", tree[0].Children[0].Account.AccountNo);
		Assert.Equal(CompanyId, child.CompanyId);
	}

	[Fact]
	public void Chart_of_account_cannot_be_its_own_parent()
	{
		Assert.Throws<DomainRuleException>(() => GL_ChartOfAccount.Create(
				CompanyId,
				ActorId,
				CreatedOn,
				"1000",
				"Assets",
				AccountType.Asset,
				"1000",
				"Assets",
				1));
	}

	[Fact]
	public void Chart_of_account_numbering_generates_sequential_and_left_incremented_children()
	{
		var existing = new[]
		{
						new ChartOfAccountNumberCandidate("100000001", false),
						new ChartOfAccountNumberCandidate("110000000", true)
				};

		Assert.Equal("100000002", ChartOfAccountNumbering.NextChildAccountNumber("100000000", false, existing));
		Assert.Equal("120000000", ChartOfAccountNumbering.NextChildAccountNumber("100000000", true, existing));
	}

	[Fact]
	public void Chart_of_account_root_number_and_title_use_the_account_type_defaults()
	{
		Assert.Equal(1, (int)AccountType.Asset);
		Assert.Equal("100000000", ChartOfAccountNumbering.RootAccountNo[AccountType.Asset]);
		Assert.Equal("Assets", ChartOfAccountNumbering.AccountTypeTitle(AccountType.Asset));
	}

	[Fact]
	public void Approved_budget_cannot_be_changed()
	{
		var budget = GL_Budget.Create(CompanyId, ActorId, CreatedOn, Guid.NewGuid(), "BUD-2026", "Operating Budget");
		var accountId = Guid.NewGuid();
		budget.AddOrUpdateLine(ActorId, UpdatedOn, accountId, 1, 500m);
		budget.Approve(ActorId, UpdatedOn);

		Assert.Equal(BudgetStatus.Approved, budget.Status);
		Assert.Throws<DomainRuleException>(() => budget.AddOrUpdateLine(ActorId, UpdatedOn, accountId, 1, 700m));
	}

	[Fact]
	public void Reconciliation_completes_only_when_adjusted_statement_matches_books()
	{
		var reconciliation = GL_BankReconciliation.Create(CompanyId, ActorId, CreatedOn, Guid.NewGuid(), new DateOnly(2026, 9, 30), 950m, 1000m);
		reconciliation.AddItem(ActorId, UpdatedOn, ReconciliationItemType.OutstandingDeposit, "Deposit in transit", 50m);

		reconciliation.Complete(ActorId, UpdatedOn);

		Assert.Equal(ReconciliationStatus.Completed, reconciliation.Status);
		Assert.Throws<DomainRuleException>(() => reconciliation.AddItem(ActorId, UpdatedOn, ReconciliationItemType.BankCharge, "Late charge", -5m));
	}

	[Fact]
	public void Financial_report_uses_configurable_mappings_before_activation()
	{
		var report = GL_FinancialReportDefinition.Create(CompanyId, ActorId, CreatedOn, FinancialReportType.ProfitAndLoss, "PNL", "Profit and Loss");
		var line = report.AddLine(ActorId, UpdatedOn, "Revenue", "Revenue", 1);
		report.AddMapping(ActorId, UpdatedOn, line.Id, GL_ReportAccountMapping.ForAccountType(AccountType.Revenue));

		report.Activate(ActorId, UpdatedOn);

		Assert.True(report.IsActive);
		Assert.Single(line.Mappings);
		Assert.Equal(ReportMappingType.AccountType, line.Mappings[0].MappingType);
		Assert.Throws<DomainRuleException>(() => report.AddLine(ActorId, UpdatedOn, "Expense", "Expense", 2));
	}

	[Fact]
	public void Standard_voucher_types_cover_the_initial_journal_set()
	{
		var types = GL_VoucherType.StandardTypes(CompanyId, ActorId, CreatedOn);

		Assert.Equal(6, types.Count);
		Assert.Contains(types, type => type.Code == "CPV" && type.RequiresCashAccount);
		Assert.Contains(types, type => type.Code == "BRV" && type.RequiresBankAccount);
		Assert.All(types, type => Assert.Equal(CompanyId, type.CompanyId));
	}
}
