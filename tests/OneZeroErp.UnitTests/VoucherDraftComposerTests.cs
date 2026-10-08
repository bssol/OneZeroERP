using OneZeroErp.Application.GeneralLedger;

namespace OneZeroErp.UnitTests;

public sealed class VoucherDraftComposerTests
{
    [Fact]
    public void Fast_receipt_debits_source_and_credits_counterparts_exactly()
    {
        var source = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lines = VoucherDraftComposer.Compose(new VoucherDraftEditModel
        {
            Mode = VoucherEntryMode.FastReceipt,
            SourceAccountId = source,
            Lines = [new() { AccountId = first, Debit = 10.25m }, new() { AccountId = second, Debit = 0.75m }]
        });
        Assert.Equal(source, lines[0].AccountId);
        Assert.Equal(11m, lines[0].Debit);
        Assert.Equal(0m, lines[0].Credit);
        Assert.Equal(11m, lines.Sum(x => x.Credit));
        Assert.All(lines.Skip(1), x => Assert.Equal(0m, x.Debit));
    }

    [Fact]
    public void Fast_entry_rejects_source_as_counterpart()
    {
        var source = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => VoucherDraftComposer.Compose(new VoucherDraftEditModel
        {
            Mode = VoucherEntryMode.FastPayment,
            SourceAccountId = source,
            Lines = [new() { AccountId = source, Debit = 1m }]
        }));
    }
}
