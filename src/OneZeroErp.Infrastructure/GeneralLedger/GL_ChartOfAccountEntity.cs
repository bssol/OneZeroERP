using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_ChartOfAccountEntity
{
	public Guid Id { get; set; }
	public string AccountNo { get; set; } = string.Empty;
	public string? ParentAccountNo { get; set; }
	public string? ParentAccountTitle { get; set; }
	public int AccountLevel { get; set; }
	public AccountType AccountType { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public bool IsPostingAccount { get; set; }
	public bool IsActive { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset UpdatedAtUtc { get; set; }
}
