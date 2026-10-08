using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.GeneralLedger;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.IntegrationTests;

public sealed class BootstrapPersistenceTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Manufacturing_chart_seed_is_audited_idempotent_and_only_populates_an_empty_chart()
    {
        var seededFixture = new SqlFixture();
        try
        {
            await seededFixture.InitializeAsync();
            var configuration = new ConfigurationBuilder().AddConfiguration(seededFixture.Configuration)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DevelopmentSeed:ChartOfAccountsProfile"] = ManufacturingChartOfAccountsSeed.ProfileName
                }).Build();

            async Task SeedAsync()
            {
                await using var db = seededFixture.CreateDbContext();
                await new DatabaseInitializer(db, configuration, seededFixture.Clock).InitializeAsync();
            }

            await SeedAsync();
            await SeedAsync();

            await using var verify = seededFixture.CreateDbContext();
            Assert.Equal(ManufacturingChartOfAccountsSeed.Accounts.Count, await verify.ChartOfAccounts.CountAsync());
            Assert.Equal(ManufacturingChartOfAccountsSeed.Accounts.Count,
                await verify.AuditEvents.CountAsync(x => x.EntityType == "ChartOfAccount" && x.Action == "Seeded"));
            Assert.Equal(ManufacturingChartOfAccountsSeed.Accounts.Count,
                await verify.OutboxMessages.CountAsync(x => verify.AuditEvents
                    .Where(audit => audit.EntityType == "ChartOfAccount" && audit.Action == "Seeded")
                    .Select(audit => audit.Id)
                    .Contains(x.AuditEventId)));

            var workInProcess = await verify.ChartOfAccounts.SingleAsync(x => x.AccountNo == "113200000");
            Assert.Equal("Inventories", workInProcess.ParentAccountTitle);
            Assert.Equal(3, workInProcess.AccountLevel);
            Assert.True(workInProcess.IsPostingAccount);

            var totalDebits = await verify.JournalLines.SumAsync(x => (decimal?)x.Debit) ?? 0m;
            var totalCredits = await verify.JournalLines.SumAsync(x => (decimal?)x.Credit) ?? 0m;
            Assert.Equal(0m, totalDebits);
            Assert.Equal(totalDebits, totalCredits);
        }
        finally
        {
            await seededFixture.DisposeAsync();
        }

        var existingFixture = new SqlFixture();
        try
        {
            await existingFixture.InitializeAsync();
            await using (var db = existingFixture.CreateDbContext())
            {
                db.ChartOfAccounts.Add(new GL_ChartOfAccountEntity
                {
                    Id = Guid.NewGuid(),
                    AccountNo = "CUSTOM-1000",
                    AccountLevel = 0,
                    AccountType = OneZeroErp.Domain.GeneralLedger.Setup.AccountType.Asset,
                    Title = "Existing Custom Chart",
                    IsPostingAccount = false,
                    IsActive = true,
                    CreatedAtUtc = existingFixture.Clock.UtcNow,
                    UpdatedAtUtc = existingFixture.Clock.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var configuration = new ConfigurationBuilder().AddConfiguration(existingFixture.Configuration)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DevelopmentSeed:ChartOfAccountsProfile"] = ManufacturingChartOfAccountsSeed.ProfileName
                }).Build();
            await using (var db = existingFixture.CreateDbContext())
                await new DatabaseInitializer(db, configuration, existingFixture.Clock).InitializeAsync();

            await using var verify = existingFixture.CreateDbContext();
            Assert.Equal("CUSTOM-1000", (await verify.ChartOfAccounts.SingleAsync()).AccountNo);
            Assert.False(await verify.AuditEvents.AnyAsync(x => x.EntityType == "ChartOfAccount" && x.Action == "Seeded"));
        }
        finally
        {
            await existingFixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Concurrent_development_initializers_seed_once_with_atomic_audit_and_keep_existing_password()
    {
        var configuration = new ConfigurationBuilder().AddConfiguration(fixture.Configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentSeed:AdminPassword"] = fixture.Password }).Build();
        async Task SeedAsync()
        {
            await using var db = fixture.CreateDbContext();
            await new DatabaseInitializer(db, configuration, fixture.Clock).InitializeAsync();
        }
        await Task.WhenAll(SeedAsync(), SeedAsync());
        configuration["DevelopmentSeed:AdminPassword"] = "different-" + Guid.NewGuid();
        await SeedAsync();
        await using var verify = fixture.CreateDbContext();
        var admin = await verify.AppUsers.SingleAsync(x => x.UserName == "admin");
        Assert.True(PasswordHasher.Verify(fixture.Password, admin.PasswordHash));
        Assert.Equal(fixture.CompanyId, admin.CompanyId);
        var audit = await verify.AuditEvents.SingleAsync(x => x.EntityId == admin.Id && x.Action == "DevelopmentUserCreated");
        Assert.True(await verify.OutboxMessages.AnyAsync(x => x.AuditEventId == audit.Id));
    }

    [Fact]
    public async Task Disposable_database_backup_can_be_restored_after_a_failed_release()
    {
        var recovery = new SqlFixture();
        var backupPath = Path.Combine(Path.GetTempPath(), recovery.DatabaseName + ".bak");
        try
        {
            await recovery.InitializeAsync();
            var user = await recovery.AddUserAsync();
            // The fixture creates this database; never restore over the application database.
            Assert.StartsWith("OneZeroErp_Test_", recovery.DatabaseName);
            Assert.True(Guid.TryParseExact(recovery.DatabaseName[16..], "N", out _));
            var connection = new SqlConnectionStringBuilder(recovery.ConnectionString) { InitialCatalog = "master", Pooling = false };
            await using var master = new SqlConnection(connection.ConnectionString);
            await master.OpenAsync();
            await using (var backup = master.CreateCommand())
            {
                backup.CommandText = $"BACKUP DATABASE [{recovery.DatabaseName}] TO DISK = @path WITH COPY_ONLY, INIT";
                backup.Parameters.AddWithValue("@path", backupPath);
                await backup.ExecuteNonQueryAsync();
            }
            await using (var db = recovery.CreateDbContext())
            {
                var stored = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
                stored.DisplayName = "Changed after backup";
                await db.SaveChangesAsync();
            }
            // Clear only this fixture's pooled connections before the restore.
            using (var pooled = new SqlConnection(recovery.ConnectionString)) SqlConnection.ClearPool(pooled);
            await using (var restore = master.CreateCommand())
            {
                restore.CommandText = $"RESTORE DATABASE [{recovery.DatabaseName}] FROM DISK = @path WITH REPLACE";
                restore.Parameters.AddWithValue("@path", backupPath);
                await restore.ExecuteNonQueryAsync();
            }
            await using var verify = recovery.CreateDbContext();
            Assert.Equal(user.DisplayName, (await verify.AppUsers.SingleAsync(x => x.Id == user.Id)).DisplayName);
            Assert.Empty(await verify.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await recovery.DisposeAsync();
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    [Fact]
    public async Task Clean_migration_has_no_pending_changes_and_upgrade_preserves_existing_data()
    {
        await using var clean = fixture.CreateDbContext();
        Assert.Empty(await clean.Database.GetPendingMigrationsAsync());
        Assert.False(clean.Database.HasPendingModelChanges());
        var upgrade = new SqlFixture();
        try
        {
            await using var db = upgrade.CreateDbContext();
            await db.GetService<IMigrator>().MigrateAsync("20261003140000_AddTaxConfigurations");
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].[Identity_AppUsers] ([Id],[UserName],[DisplayName],[PasswordHash],[Role],[IsActive],[CreatedAtUtc],[UpdatedAtUtc])
                VALUES ({id}, {"upgrade-test"}, {"Synthetic upgrade test"}, {"not-a-valid-password-hash"}, {"Test"}, {false}, {fixture.Clock.UtcNow}, {fixture.Clock.UtcNow})
                """);
            await db.Database.MigrateAsync();
            Assert.Equal("upgrade-test", (await db.AppUsers.SingleAsync(x => x.Id == id)).UserName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally { await upgrade.DisposeAsync(); }
    }

    [Fact]
    public async Task Business_change_audit_and_outbox_commit_and_rollback_together()
    {
        var user = await fixture.AddUserAsync();
        var auditId = Guid.NewGuid();
        await using (var db = fixture.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var stored = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
            stored.DisplayName = "Rolled back";
            db.AuditEvents.Add(new() { Id = auditId, ActorUserId = user.Id, EntityId = user.Id, EntityType = "BootstrapTest", Action = "Updated", OccurredAtUtc = fixture.Clock.UtcNow });
            await db.SaveChangesAsync();
            Assert.True(await db.OutboxMessages.AnyAsync(x => x.AuditEventId == auditId));
            await transaction.RollbackAsync();
        }
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.Equal(user.DisplayName, (await verify.AppUsers.SingleAsync(x => x.Id == user.Id)).DisplayName);
            Assert.False(await verify.AuditEvents.AnyAsync(x => x.Id == auditId));
            Assert.False(await verify.OutboxMessages.AnyAsync(x => x.AuditEventId == auditId));
        }
        var login = await fixture.Authentication.AuthenticateAsync(new(user.UserName, fixture.Password));
        Assert.True(login.Succeeded);
        await using var committed = fixture.CreateDbContext();
        var audit = await committed.AuditEvents.SingleAsync(x => x.EntityId == user.Id && x.Action == "LoggedIn");
        Assert.True(await committed.OutboxMessages.AnyAsync(x => x.AuditEventId == audit.Id));
        Assert.True(await committed.UserSessions.AnyAsync(x => x.AppUserId == user.Id));
        audit.Action = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => committed.SaveChangesAsync());
    }

    [Fact]
    public async Task Outbox_replay_produces_one_delivery_receipt()
    {
        var user = await fixture.AddUserAsync();
        await fixture.Authentication.AuthenticateAsync(new(user.UserName, fixture.Password));
        var dispatcher = new OutboxDispatcher(fixture, fixture.Clock);
        await dispatcher.DispatchBatchAsync();
        await using (var db = fixture.CreateDbContext())
        {
            var auditId = await db.AuditEvents.Where(x => x.EntityId == user.Id && x.Action == "LoggedIn").Select(x => x.Id).SingleAsync();
            var message = await db.OutboxMessages.SingleAsync(x => x.AuditEventId == auditId);
            message.DispatchedAtUtc = null; // Simulate replay after a lost acknowledgement.
            await db.SaveChangesAsync();
            await dispatcher.DispatchBatchAsync();
            Assert.Equal(1, await db.AuditDeliveries.CountAsync(x => x.AuditEventId == auditId));
        }
    }
}
