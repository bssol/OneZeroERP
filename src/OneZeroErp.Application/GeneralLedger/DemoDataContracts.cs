namespace OneZeroErp.Application.GeneralLedger;

public sealed record DemoDataStatus(
    bool IsReady,
    int FiscalYears,
    int Accounts,
    int VoucherTypes,
    int Currencies,
    int BankAccounts,
    int CashAccounts,
    int TaxConfigurations,
    int VoucherDrafts);

public sealed record DemoDataSeedResult(bool Succeeded, string Message, DemoDataStatus Status);

public interface IDemoDataService
{
    Task<DemoDataStatus> GetStatusAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<DemoDataSeedResult> SeedAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);
}
