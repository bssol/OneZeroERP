using OneZeroErp.Application;

namespace OneZeroErp.Infrastructure;

public sealed class ModuleRegistry : IModuleRegistry
{
    private static readonly IReadOnlyList<ModuleNavigationItem> Modules =
    [
        new("gl", "General Ledger", "Accounts, journals, budgets, and financial statements.", "/modules/gl", "GL", true, "Foundation shell"),
        new("ap", "Accounts Payable", "Supplier invoices and settlement workflows.", "/modules/ap", "AP", false),
        new("ar", "Accounts Receivable", "Customer billing and collections.", "/modules/ar", "AR", false),
        new("inventory", "Inventory", "Stock, valuation, and movements.", "/modules/inventory", "IN", false),
        new("sales", "Sales", "Orders, invoicing, and customer operations.", "/modules/sales", "SA", false),
        new("purchasing", "Purchasing", "Requisitions, purchase orders, and receiving.", "/modules/purchasing", "PU", false),
        new("hr", "Human Resources", "People, attendance, and payroll extensions.", "/modules/hr", "HR", false),
        new("assets", "Fixed Assets", "Asset registers and depreciation.", "/modules/assets", "FA", false),
        new("reports", "Reports", "Cross-module operational reporting.", "/modules/reports", "RP", false),
        new("admin", "Administration", "Organization, security, and configuration.", "/modules/admin", "AD", false)
    ];

    public IReadOnlyList<ModuleNavigationItem> GetModules() => Modules;
}

public sealed class DashboardService(IModuleRegistry moduleRegistry) : IDashboardService
{
    public ApplicationDashboardModel GetApplicationDashboard() => new(
        [
            new("Open tasks", "12", "4 due today", "up", "blue"),
            new("Active modules", "1", "9 planned", "steady", "indigo"),
            new("System health", "99.9%", "Last 30 days", "steady", "emerald"),
            new("Pending approvals", "6", "2 high priority", "up", "amber")
        ],
        [
            new("Review access requests", "Two users are waiting for role assignment.", "/modules/admin", "amber"),
            new("Explore General Ledger", "Open the initial GL workspace shell.", "/modules/gl", "blue")
        ],
        [
            new("GL workspace prepared", "The module shell is ready for fiscal-year work.", "Today", "blue"),
            new("Platform foundation created", "Authentication, themes, and module registry are active.", "Today", "emerald")
        ],
        moduleRegistry.GetModules());

    public ModuleDashboardModel GetModuleDashboard(string moduleKey)
    {
        var module = moduleRegistry.GetModules().FirstOrDefault(x => x.Key == moduleKey)
            ?? throw new KeyNotFoundException($"Module '{moduleKey}' was not found.");

        if (moduleKey != "gl")
        {
            return new(module.Name, module.Description, [], [], [], [], [module]);
        }

        return new(
            "General Ledger",
            "A safe workspace shell for future accounting capabilities. No accounting calculations are implemented yet.",
            [
                new("Fiscal years", "—", "Not implemented", "steady", "slate"),
                new("Draft journals", "—", "Placeholder", "steady", "slate"),
                new("Bank reconciliation", "—", "Placeholder", "steady", "slate")
            ],
            [
                new("Manage fiscal years", "Define the accounting calendar before adding periods and journals.", "/gl/fiscal-years", "blue"),
                new("Manage chart of accounts", "Build the account hierarchy used by future journals and reports.", "/gl/chart-of-accounts", "blue"),
                new("Manage voucher types", "Configure the transaction categories used by journal workflows.", "/gl/voucher-types", "blue"),
                new("Manage day locks", "Lock accounting dates before controlled journal processing begins.", "/gl/day-locks", "amber"),
                new("Manage bank accounts", "Link bank accounts to posting Asset accounts.", "/gl/bank-accounts", "blue"),
                new("Manage cash accounts", "Define physical cash locations and their GL links.", "/gl/cash-accounts", "blue"),
                new("Manage currencies", "Define base and foreign currencies for the company.", "/gl/currencies", "indigo"),
                new("Manage exchange rates", "Maintain effective-dated conversion rates.", "/gl/exchange-rates", "indigo"),
                new("Manage tax configuration", "Define reusable tax rates and effective dates.", "/gl/tax-configurations", "amber"),
                new("Review roadmap", "See the planned GL delivery sequence.", "/modules/gl", "indigo")
            ],
            [new("GL shell initialized", "Placeholder data only; no journals or reports exist yet.", "Today", "slate")],
            ["Posting volume trend — planned", "Trial balance — planned"],
            [module]);
    }
}
