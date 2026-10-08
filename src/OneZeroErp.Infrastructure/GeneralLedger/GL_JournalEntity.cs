namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class GL_JournalEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VoucherTypeId { get; set; }
    public Guid FiscalYearId { get; set; }
    public Guid CurrencyId { get; set; }
    public int SequenceNumber { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public ICollection<GL_JournalLineEntity> Lines { get; set; } = new List<GL_JournalLineEntity>();
}

public sealed class GL_JournalLineEntity
{
    public Guid Id { get; set; }
    public Guid JournalId { get; set; }
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string Narration { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public GL_JournalEntity Journal { get; set; } = null!;
}
