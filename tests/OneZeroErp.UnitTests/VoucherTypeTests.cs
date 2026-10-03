using OneZeroErp.Domain;
using OneZeroErp.Domain.GeneralLedger.Setup;

namespace OneZeroErp.UnitTests;

public sealed class VoucherTypeTests
{
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset CreatedOn = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StandardTypesContainExpectedOperationalRequirements()
    {
        var types = GL_VoucherType.StandardTypes(CompanyId, ActorId, CreatedOn);

        Assert.Equal(["OPV", "JVV", "CPV", "CRV", "BPV", "BRV"], types.Select(x => x.Code));
        Assert.All(types, type => Assert.False(type.RequiresBankAccount && type.RequiresCashAccount));
        Assert.True(types.Single(x => x.Code == "CPV").RequiresCashAccount);
        Assert.True(types.Single(x => x.Code == "BPV").RequiresBankAccount);
    }

    [Fact]
    public void BankAndCashRequirementsCannotBothBeEnabled()
    {
        var exception = Assert.Throws<DomainRuleException>(() => GL_VoucherType.Create(
            CompanyId, ActorId, CreatedOn, "BAD", "Invalid", requiresBankAccount: true, requiresCashAccount: true));

        Assert.Equal("A voucher type cannot require both a bank and cash account.", exception.Message);
    }
}
