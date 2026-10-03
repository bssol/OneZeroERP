using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.Domain.GeneralLedger.Configurations;

public sealed record GL_ReportAccountMapping
{
	private GL_ReportAccountMapping(ReportMappingType mappingType, Guid? accountId, AccountType? accountType, string? accountCodePrefix)
	{
		MappingType = mappingType;
		AccountId = accountId;
		AccountType = accountType;
		AccountCodePrefix = accountCodePrefix;
	}

	public ReportMappingType MappingType { get; }
	public Guid? AccountId { get; }
	public AccountType? AccountType { get; }
	public string? AccountCodePrefix { get; }

	public static GL_ReportAccountMapping ForAccount(Guid accountId)
	{
		if (accountId == Guid.Empty) throw new DomainRuleException("A report account mapping must reference an account.");
		return new GL_ReportAccountMapping(ReportMappingType.Account, accountId, null, null);
	}

	public static GL_ReportAccountMapping ForAccountType(AccountType accountType) => new(ReportMappingType.AccountType, null, accountType, null);

	public static GL_ReportAccountMapping ForAccountCodePrefix(string accountCodePrefix)
	{
		if (string.IsNullOrWhiteSpace(accountCodePrefix)) throw new DomainRuleException("An account code prefix is required for a report mapping.");
		return new GL_ReportAccountMapping(ReportMappingType.AccountCodePrefix, null, null, accountCodePrefix.Trim().ToUpperInvariant());
	}
}

public sealed class GL_FinancialReportLine : AuditableEntity
{
	private readonly List<GL_ReportAccountMapping> _mappings = [];

	private GL_FinancialReportLine()
	{
	}

	private GL_FinancialReportLine(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, string code, string label, int sortOrder, int indentationLevel)
			: base(companyId, createdBy, createdOn)
	{
		Id = id;
		Code = code;
		Label = label;
		SortOrder = sortOrder;
		IndentationLevel = indentationLevel;
	}

	public Guid Id { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Label { get; private set; } = string.Empty;
	public int SortOrder { get; private set; }
	public int IndentationLevel { get; private set; }
	public IReadOnlyList<GL_ReportAccountMapping> Mappings => _mappings.AsReadOnly();

	internal static GL_FinancialReportLine Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, string code, string label, int sortOrder, int indentationLevel, Guid? id = null)
	{
		if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("Report line code is required.");
		if (string.IsNullOrWhiteSpace(label)) throw new DomainRuleException("Report line label is required.");
		if (sortOrder < 0) throw new DomainRuleException("Report line sort order cannot be negative.");
		if (indentationLevel < 0) throw new DomainRuleException("Report line indentation cannot be negative.");
		return new GL_FinancialReportLine(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, code.Trim().ToUpperInvariant(), label.Trim(), sortOrder, indentationLevel);
	}

	internal void AddMapping(Guid actorUserName, DateTimeOffset updatedOn, GL_ReportAccountMapping mapping)
	{
		_mappings.Add(mapping);
		MarkUpdated(actorUserName, updatedOn);
	}
}

public sealed class GL_FinancialReportDefinition : AuditableEntity
{
	private readonly List<GL_FinancialReportLine> _lines = [];

	private GL_FinancialReportDefinition()
	{
	}

	private GL_FinancialReportDefinition(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, FinancialReportType reportType, string code, string name)
			: base(companyId, createdBy, createdOn)
	{
		Id = id;
		ReportType = reportType;
		Code = code;
		Name = name;
	}

	public Guid Id { get; private set; }
	public FinancialReportType ReportType { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public bool IsActive { get; private set; }
	public IReadOnlyList<GL_FinancialReportLine> Lines => _lines.AsReadOnly();

	public static GL_FinancialReportDefinition Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, FinancialReportType reportType, string code, string name, Guid? id = null)
	{
		if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("Financial report code is required.");
		if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("Financial report name is required.");
		return new GL_FinancialReportDefinition(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, reportType, code.Trim().ToUpperInvariant(), name.Trim());
	}

	public GL_FinancialReportLine AddLine(Guid actorUserName, DateTimeOffset updatedOn, string code, string label, int sortOrder, int indentationLevel = 0)
	{
		EnsureInactiveForConfiguration();
		if (_lines.Any(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))) throw new DomainRuleException("A report line with this code already exists.");
		var line = GL_FinancialReportLine.Create(CompanyId, actorUserName, updatedOn, code, label, sortOrder, indentationLevel);
		_lines.Add(line);
		MarkUpdated(actorUserName, updatedOn);
		return line;
	}

	public void AddMapping(Guid actorUserName, DateTimeOffset updatedOn, Guid lineId, GL_ReportAccountMapping mapping)
	{
		EnsureInactiveForConfiguration();
		var line = _lines.SingleOrDefault(x => x.Id == lineId) ?? throw new DomainRuleException("The financial report line was not found.");
		line.AddMapping(actorUserName, updatedOn, mapping);
		MarkUpdated(actorUserName, updatedOn);
	}

	public void Activate(Guid actorUserName, DateTimeOffset updatedOn)
	{
		if (_lines.Count == 0) throw new DomainRuleException("A financial report must contain at least one line before activation.");
		if (_lines.Any(x => x.Mappings.Count == 0)) throw new DomainRuleException("Every financial report line must have at least one account mapping before activation.");
		IsActive = true;
		MarkUpdated(actorUserName, updatedOn);
	}

	public void Deactivate(Guid actorUserName, DateTimeOffset updatedOn)
	{
		IsActive = false;
		MarkUpdated(actorUserName, updatedOn);
	}

	private void EnsureInactiveForConfiguration()
	{
		if (IsActive) throw new DomainRuleException("An active financial report cannot be edited.");
	}
}