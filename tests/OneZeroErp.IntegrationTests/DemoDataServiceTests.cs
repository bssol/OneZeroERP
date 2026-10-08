using Microsoft.EntityFrameworkCore;
using OneZeroErp.Infrastructure.GeneralLedger;

namespace OneZeroErp.IntegrationTests;

public sealed class DemoDataServiceTests
{
    [Fact]
    public async Task Seed_is_idempotent_audited_and_creates_only_balanced_draft_journals()
    {
        var fixture = new SqlFixture();
        try
        {
            fixture.Clock.UtcNow = new DateTimeOffset(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
            await fixture.InitializeAsync();
            var administrator = await fixture.AddUserAsync("identity.users:CanEdit");
            var service = new DemoDataService(fixture, fixture.Clock);

            var first = await service.SeedAsync(fixture.CompanyId, administrator.Id);
            var second = await service.SeedAsync(fixture.CompanyId, administrator.Id);

            Assert.True(first.Succeeded);
            Assert.True(first.Status.IsReady);
            Assert.True(second.Succeeded);
            Assert.Equal("The walkthrough dataset is already complete.", second.Message);

            await using var verify = fixture.CreateDbContext();
            Assert.Equal(12, await verify.ChartOfAccounts.CountAsync(x => x.AccountNo.StartsWith("DEMO-")));
            Assert.Equal(3, await verify.Journals.CountAsync(x => x.Description.StartsWith("[DEMO]")));
            Assert.False(await verify.Journals.AnyAsync(x => x.Description.StartsWith("[DEMO]") && x.Status != "Draft"));
            var journals = await verify.Journals.Include(x => x.Lines)
                .Where(x => x.Description.StartsWith("[DEMO]")).ToListAsync();
            Assert.All(journals, journal => Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit)));
            Assert.Equal(4, await verify.AuditEvents.CountAsync(x => x.Action == "DemoDraftCreated" ||
                x.EntityType == "DemoData" && x.Action == "Seeded"));
            Assert.Equal(4, await verify.OutboxMessages.CountAsync(x =>
                verify.AuditEvents.Any(a => a.Id == x.AuditEventId && (a.Action == "DemoDraftCreated" || a.EntityType == "DemoData" && a.Action == "Seeded"))));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Seed_requires_company_administrator_permission()
    {
        var fixture = new SqlFixture();
        try
        {
            await fixture.InitializeAsync();
            var viewer = await fixture.AddUserAsync("gl.vouchers:CanView");
            var service = new DemoDataService(fixture, fixture.Clock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.SeedAsync(fixture.CompanyId, viewer.Id));
            await using var verify = fixture.CreateDbContext();
            Assert.False(await verify.ChartOfAccounts.AnyAsync(x => x.AccountNo.StartsWith("DEMO-")));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
