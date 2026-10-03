using System.ComponentModel.DataAnnotations;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Application.GeneralLedger;

public sealed record ChartOfAccountListItem(
		Guid Id,
		string AccountNo,
		string? ParentAccountNo,
		string? ParentAccountTitle,
		int AccountLevel,
		AccountType AccountType,
		string Title,
		string? Description,
		bool IsPostingAccount,
		bool IsActive,
		DateTimeOffset CreatedAtUtc);

public sealed record ChartOfAccountParentOption(Guid Id, string AccountNo, string Title, int AccountLevel);

public sealed record ChartOfAccountTreeItem(
		Guid Id,
		string AccountNo,
		string Title,
		AccountType AccountType,
		int AccountLevel,
		IReadOnlyList<ChartOfAccountTreeItem> Children);

public sealed record ChartOfAccountNumberSuggestion(string? AccountNo, string? SuggestedTitle, bool RequiresParent);

public sealed record ChartOfAccountImportResult(bool Succeeded, int ImportedCount, IReadOnlyList<string> Errors);

public sealed class ChartOfAccountEditModel
{
	public Guid? Id { get; set; }

	[Required, StringLength(30, MinimumLength = 1)]
	public string AccountNo { get; set; } = string.Empty;

	[StringLength(30)]
	public string? ParentAccountNo { get; set; }

	[StringLength(160)]
	public string? ParentAccountTitle { get; set; }

	[Range(0, 99)]
	public int AccountLevel { get; set; }

	public AccountType AccountType { get; set; }
	

	[Required, StringLength(160, MinimumLength = 1)]
	public string Title { get; set; } = string.Empty;

	[StringLength(1000)]
	public string? Description { get; set; }

	public bool IsPostingAccount { get; set; } = true;
	public bool IsActive { get; set; } = true;
}

public sealed record ChartOfAccountOperationResult(bool Succeeded, string? ErrorMessage = null, ChartOfAccountListItem? Item = null);

public interface IChartOfAccountService
{
	Task<PagedResult<ChartOfAccountListItem>> GetPageAsync(PagedQuery query, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<ChartOfAccountParentOption>> GetParentOptionsAsync(AccountType accountType, Guid? excludeId = null, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<ChartOfAccountTreeItem>> GetParentTreeAsync(CancellationToken cancellationToken = default);
	Task<ChartOfAccountNumberSuggestion> GetAccountNumberSuggestionAsync(AccountType accountType, string? parentAccountNo, bool isPostingAccount, Guid? excludeId = null, CancellationToken cancellationToken = default);
	Task<ChartOfAccountImportResult> ImportCsvAsync(Stream csvStream, Guid actorUserId, CancellationToken cancellationToken = default);
	Task<ChartOfAccountListItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
	Task<ChartOfAccountOperationResult> SaveAsync(ChartOfAccountEditModel model, Guid actorUserId, CancellationToken cancellationToken = default);
	Task<ChartOfAccountOperationResult> SetActiveAsync(Guid id, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default);
}
