using System.ComponentModel.DataAnnotations;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record GlPostingAccountOption(Guid Id, string AccountNo, string Title);

public sealed record BankAccountListItem(
    Guid Id,
    Guid CompanyId,
    Guid GlAccountId,
    string GlAccountNo,
    string GlAccountTitle,
    string Code,
    string Name,
    string BankName,
    string AccountNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed class BankAccountEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GlAccountId { get; set; }

    [Required, StringLength(30, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 1)] public string BankName { get; set; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 1)] public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record BankAccountOperationResult(bool Succeeded, string? ErrorMessage = null, BankAccountListItem? Item = null);

public interface IBankAccountService
{
    Task<PagedResult<BankAccountListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPostingAccountOption>> GetGlAccountOptionsAsync(CancellationToken cancellationToken = default);
    Task<BankAccountOperationResult> SaveAsync(BankAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}

public sealed record CashAccountListItem(
    Guid Id,
    Guid CompanyId,
    Guid GlAccountId,
    string GlAccountNo,
    string GlAccountTitle,
    string Code,
    string Name,
    string Location,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed class CashAccountEditModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid GlAccountId { get; set; }

    [Required, StringLength(30, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 1)] public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record CashAccountOperationResult(bool Succeeded, string? ErrorMessage = null, CashAccountListItem? Item = null);

public interface ICashAccountService
{
    Task<PagedResult<CashAccountListItem>> GetPageAsync(Guid companyId, PagedQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlPostingAccountOption>> GetGlAccountOptionsAsync(CancellationToken cancellationToken = default);
    Task<CashAccountOperationResult> SaveAsync(CashAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
}
