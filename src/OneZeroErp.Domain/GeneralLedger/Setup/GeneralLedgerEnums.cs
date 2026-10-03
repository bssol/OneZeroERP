namespace OneZeroErp.Domain.GeneralLedger.Setup;

public enum FiscalYearStatus
{
	Open,
	Closed
}

public enum AccountType
{
	Asset = 1,
	Liability = 2,
	Equity = 3,
	Revenue = 4,
	Expense = 5
}




public enum JournalStatus
{
	Draft,
	Posted,
	Reversed,
	Cancelled
}

public enum BudgetStatus
{
	Draft,
	Approved,
	Closed
}

public enum ReconciliationStatus
{
	Draft,
	Completed
}

public enum ReconciliationItemType
{
	OutstandingDeposit,
	OutstandingPayment,
	BankCharge,
	BankInterest,
	Other
}

public enum FinancialReportType
{
	NotesToAccounts,
	ProfitAndLoss,
	BalanceSheet
}

public enum ReportMappingType
{
	Account,
	AccountType,
	AccountCodePrefix
}
