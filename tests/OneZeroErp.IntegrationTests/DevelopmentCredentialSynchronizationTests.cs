using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.IntegrationTests;

public sealed class DevelopmentCredentialSynchronizationTests
{
    private static readonly (string UserName, string Role, string Setting)[] Users =
    [
        ("admin", "Administrator", "DevelopmentSeed:AdminPassword"),
        ("fiscal.manager", "Fiscal Manager", "DevelopmentSeed:FiscalManagerPassword"),
        ("gl.accountant", "Accountant", "DevelopmentSeed:AccountantPassword"),
        ("gl.viewer", "Viewer", "DevelopmentSeed:ViewerPassword")
    ];

    [Fact]
    public async Task Synchronization_hashes_all_four_passwords_and_allows_subsequent_startup_seeding()
    {
        var fixture = new SqlFixture();
        try
        {
            await fixture.InitializeAsync();
            var configuration = Configuration();
            await SeedUsersAsync(fixture);
            await using (var db = fixture.CreateDbContext())
            {
                var adminId = await db.AppUsers.Where(x => x.UserName == "admin").Select(x => x.Id).SingleAsync();
                db.UserSessions.Add(new()
                {
                    Id = Guid.NewGuid(),
                    AppUserId = adminId,
                    CompanyId = fixture.CompanyId,
                    CreatedAtUtc = fixture.Clock.UtcNow,
                    ExpiresAtUtc = fixture.Clock.UtcNow.AddHours(1)
                });
                db.PasswordResets.Add(new()
                {
                    Id = Guid.NewGuid(),
                    AppUserId = adminId,
                    TokenHash = Guid.NewGuid().ToString("N"),
                    ExpiresAtUtc = fixture.Clock.UtcNow.AddMinutes(15)
                });
                await db.SaveChangesAsync();
            }
            var changed = await new DevelopmentCredentialSynchronizer(fixture, configuration, fixture.Clock).ApplyAsync(fixture.CompanyId);
            Assert.Equal(4, changed.Count);
            await using (var startup = fixture.CreateDbContext())
                await new DatabaseInitializer(startup, fixture.Configuration, fixture.Clock).InitializeAsync();
            await using var verify = fixture.CreateDbContext();
            foreach (var target in Users)
            {
                var stored = await verify.AppUsers.SingleAsync(x => x.UserName == target.UserName);
                Assert.Equal(fixture.CompanyId, stored.CompanyId);
                Assert.True(PasswordHasher.Verify(configuration[target.Setting]!, stored.PasswordHash));
                Assert.Equal(0, stored.FailedLoginCount);
                var audit = await verify.AuditEvents.SingleAsync(x => x.EntityId == stored.Id && x.Action == "DevelopmentPasswordSynchronized");
                Assert.True(await verify.OutboxMessages.AnyAsync(x => x.AuditEventId == audit.Id));
            }
            Assert.All(await verify.UserSessions.ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
            Assert.All(await verify.PasswordResets.ToListAsync(), x => Assert.NotNull(x.ConsumedAtUtc));
            Assert.Equal(4, await verify.AppUserPermissions.CountAsync(x => x.Permission == "gl.vouchers:CanView"));
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Role_mismatch_aborts_every_password_change()
    {
        var fixture = new SqlFixture();
        try
        {
            await fixture.InitializeAsync();
            await SeedUsersAsync(fixture, wrongViewerRole: true);
            var configuration = Configuration();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DevelopmentCredentialSynchronizer(fixture, configuration, fixture.Clock).ApplyAsync(fixture.CompanyId));
            await using var verify = fixture.CreateDbContext();
            Assert.All(await verify.AppUsers.ToListAsync(), user =>
            {
                Assert.Equal(Guid.Empty, user.CompanyId);
                Assert.True(PasswordHasher.Verify("Original-password-123", user.PasswordHash));
            });
            Assert.False(await verify.AuditEvents.AnyAsync(x => x.Action == "DevelopmentPasswordSynchronized"));
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(Users.ToDictionary(x => x.Setting, x => (string?)("Synthetic-new-password-" + x.UserName)))
        .Build();

    private static async Task SeedUsersAsync(SqlFixture fixture, bool wrongViewerRole = false)
    {
        await using var db = fixture.CreateDbContext();
        foreach (var target in Users)
            db.AppUsers.Add(new AppUserEntity
            {
                Id = Guid.NewGuid(),
                UserName = target.UserName,
                DisplayName = target.UserName,
                Role = wrongViewerRole && target.UserName == "gl.viewer" ? "Unexpected" : target.Role,
                PasswordHash = PasswordHasher.Hash("Original-password-123"),
                IsActive = true,
                CreatedAtUtc = fixture.Clock.UtcNow,
                UpdatedAtUtc = fixture.Clock.UtcNow
            });
        await db.SaveChangesAsync();
    }
}
