using System.ComponentModel.DataAnnotations;

namespace OneZeroErp.Application.GeneralLedger;

public enum VoucherEntryMode { Standard, FastPayment, FastReceipt }

public sealed class VoucherDraftLineModel
{
    public Guid AccountId { get; set; }
    [StringLength(500)] public string Narration { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class VoucherDraftEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VoucherTypeId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateOnly TransactionDate { get; set; }
    [StringLength(500)] public string Description { get; set; } = string.Empty;
    public VoucherEntryMode Mode { get; set; }
    public Guid SourceAccountId { get; set; }
    public List<VoucherDraftLineModel> Lines { get; set; } = [new(), new()];
}

public sealed record VoucherDraftListItem(Guid Id, string Number, DateOnly TransactionDate, string VoucherType,
    string Description, string Currency, decimal TotalDebit, decimal TotalCredit, int LineCount, DateTimeOffset UpdatedAtUtc);
public sealed record VoucherDraftOption(Guid Id, string Label, Guid? GlAccountId = null, int DecimalPlaces = 2,
    VoucherEntryMode FastMode = VoucherEntryMode.Standard, bool RequiresBank = false, bool RequiresCash = false);
public sealed record VoucherDraftOptions(IReadOnlyList<VoucherDraftOption> VoucherTypes,
    IReadOnlyList<VoucherDraftOption> FiscalYears, IReadOnlyList<VoucherDraftOption> Currencies,
    IReadOnlyList<GlPostingAccountOption> Accounts, IReadOnlyList<VoucherDraftOption> BankAccounts,
    IReadOnlyList<VoucherDraftOption> CashAccounts);
public sealed record VoucherDraftOperationResult(bool Succeeded, string? ErrorMessage = null, Guid? Id = null, string? Number = null);

public interface IVoucherDraftService
{
    Task<VoucherDraftOptions> GetOptionsAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<VoucherDraftListItem>> GetPageAsync(Guid companyId, Guid actorUserId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<VoucherDraftEditModel?> GetAsync(Guid companyId, Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
    Task<VoucherDraftOperationResult> SaveAsync(VoucherDraftEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}

public static class VoucherDraftComposer
{
    public static IReadOnlyList<VoucherDraftLineModel> Compose(VoucherDraftEditModel model)
    {
        if (model.Mode == VoucherEntryMode.Standard) return model.Lines;
        if (model.Mode is not (VoucherEntryMode.FastPayment or VoucherEntryMode.FastReceipt))
            throw new ArgumentException("Invalid voucher entry mode.");
        if (model.SourceAccountId == Guid.Empty) throw new ArgumentException("Select the bank or cash account.");
        if (model.Lines.Count == 0) throw new ArgumentException("Add at least one counterpart line.");
        var total = 0m;
        foreach (var line in model.Lines)
        {
            if (line.AccountId == Guid.Empty || line.AccountId == model.SourceAccountId || line.Debit <= 0m || line.Credit != 0m)
                throw new ArgumentException("Each fast-entry line needs a different account and a positive amount.");
            total = checked(total + line.Debit);
        }
        var payment = model.Mode == VoucherEntryMode.FastPayment;
        var source = new VoucherDraftLineModel
        {
            AccountId = model.SourceAccountId,
            Narration = model.Description,
            Debit = payment ? 0m : total,
            Credit = payment ? total : 0m
        };
        return [source, .. model.Lines.Select(x => new VoucherDraftLineModel { AccountId = x.AccountId,
            Narration = x.Narration, Debit = payment ? x.Debit : 0m, Credit = payment ? 0m : x.Debit })];
    }
}
