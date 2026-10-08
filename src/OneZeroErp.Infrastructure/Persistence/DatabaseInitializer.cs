using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.GeneralLedger;

namespace OneZeroErp.Infrastructure.Persistence;

public sealed class DatabaseInitializer(ErpDbContext db, IConfiguration configuration, IClock clock)
{
    private static readonly DevelopmentSeedUser[] DevelopmentUsers =
    [
            new(
                        "admin",
                        "Development Administrator",
                        "Administrator",
                        "DevelopmentSeed:AdminPassword",
                        [
                                "gl.fiscal-years:CanView",
                                "gl.fiscal-years:CanAdd",
                                "gl.fiscal-years:CanEdit",
                                "gl.fiscal-years:CanDelete",
                                "gl.chart-of-accounts:CanView",
                                "gl.chart-of-accounts:CanAdd",
                                "gl.chart-of-accounts:CanEdit",
                                "gl.voucher-types:CanView",
                                "gl.voucher-types:CanAdd",
                                "gl.voucher-types:CanEdit",
                                "gl.vouchers:CanView",
                                "gl.vouchers:CanAdd",
                                "gl.vouchers:CanEdit",
                                "gl.day-locks:CanView",
                                "gl.day-locks:CanEdit",
                                "gl.bank-accounts:CanView",
                                "gl.bank-accounts:CanAdd",
                                "gl.bank-accounts:CanEdit",
                                "gl.cash-accounts:CanView",
                                "gl.cash-accounts:CanAdd",
                                "gl.cash-accounts:CanEdit",
                                "gl.currencies:CanView",
                                "gl.currencies:CanAdd",
                                "gl.currencies:CanEdit",
                                "gl.exchange-rates:CanView",
                                "gl.exchange-rates:CanAdd",
                                "gl.exchange-rates:CanEdit",
                                "gl.tax-configuration:CanView",
                                "gl.tax-configuration:CanAdd",
                                "gl.tax-configuration:CanEdit"
                        ]),
                new(
                        "fiscal.manager",
                        "Fiscal Year Manager",
                        "Fiscal Manager",
                        "DevelopmentSeed:FiscalManagerPassword",
                        [
                                "gl.fiscal-years:CanView",
                                "gl.fiscal-years:CanAdd",
                                "gl.fiscal-years:CanEdit",
                                "gl.fiscal-years:CanDelete",
                                "gl.chart-of-accounts:CanView",
                                "gl.voucher-types:CanView",
                                "gl.vouchers:CanView",
                                "gl.day-locks:CanView",
                                "gl.day-locks:CanEdit",
                                "gl.bank-accounts:CanView",
                                "gl.cash-accounts:CanView",
                                "gl.currencies:CanView",
                                "gl.exchange-rates:CanView",
                                "gl.tax-configuration:CanView"
                        ]),
                new(
                        "gl.accountant",
                        "General Ledger Accountant",
                        "Accountant",
                        "DevelopmentSeed:AccountantPassword",
                        [
                                "gl.fiscal-years:CanView",
                                "gl.chart-of-accounts:CanView",
                                "gl.chart-of-accounts:CanAdd",
                                "gl.chart-of-accounts:CanEdit",
                                "gl.voucher-types:CanView",
                                "gl.voucher-types:CanAdd",
                                "gl.voucher-types:CanEdit",
                                "gl.vouchers:CanView",
                                "gl.vouchers:CanAdd",
                                "gl.vouchers:CanEdit",
                                "gl.day-locks:CanView",
                                "gl.bank-accounts:CanView",
                                "gl.bank-accounts:CanAdd",
                                "gl.bank-accounts:CanEdit",
                                "gl.cash-accounts:CanView",
                                "gl.cash-accounts:CanAdd",
                                "gl.cash-accounts:CanEdit",
                                "gl.currencies:CanView",
                                "gl.currencies:CanAdd",
                                "gl.currencies:CanEdit",
                                "gl.exchange-rates:CanView",
                                "gl.exchange-rates:CanAdd",
                                "gl.exchange-rates:CanEdit",
                                "gl.tax-configuration:CanView",
                                "gl.tax-configuration:CanAdd",
                                "gl.tax-configuration:CanEdit"
                        ]),
                new(
                        "gl.viewer",
                        "GL Read Only User",
                        "Viewer",
                        "DevelopmentSeed:ViewerPassword",
                        [
                                "gl.fiscal-years:CanView",
                                "gl.chart-of-accounts:CanView",
                                "gl.voucher-types:CanView",
                                "gl.vouchers:CanView",
                                "gl.day-locks:CanView",
                                "gl.bank-accounts:CanView",
                                "gl.cash-accounts:CanView",
                                "gl.currencies:CanView",
                                "gl.exchange-rates:CanView",
                                "gl.tax-configuration:CanView"
                        ])
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        await SeedDevelopmentUserAsync(cancellationToken);
        await SeedChartOfAccountsAsync(cancellationToken);
    }

    private async Task SeedChartOfAccountsAsync(CancellationToken cancellationToken)
    {
        var profile = configuration["DevelopmentSeed:ChartOfAccountsProfile"]?.Trim();
        if (string.IsNullOrEmpty(profile)) return;
        if (!profile.Equals(ManufacturingChartOfAccountsSeed.ProfileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown DevelopmentSeed:ChartOfAccountsProfile '{profile}'.");
        if (!Guid.TryParse(configuration["Company:DefaultCompanyId"], out var companyId) || companyId == Guid.Empty)
            throw new InvalidOperationException("Company:DefaultCompanyId must be a nonempty GUID.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.ChartOfAccounts.AnyAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = clock.UtcNow;
        foreach (var seed in ManufacturingChartOfAccountsSeed.Accounts)
        {
            var account = new GL_ChartOfAccountEntity
            {
                Id = CreateDeterministicId(seed.AccountNo),
                AccountNo = seed.AccountNo,
                ParentAccountNo = seed.ParentAccountNo,
                ParentAccountTitle = seed.ParentAccountNo is null
                    ? null
                    : ManufacturingChartOfAccountsSeed.Accounts.Single(x => x.AccountNo == seed.ParentAccountNo).Title,
                AccountLevel = GetAccountLevel(seed),
                AccountType = seed.AccountType,
                Title = seed.Title,
                Description = seed.Description,
                IsPostingAccount = seed.IsPostingAccount,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.ChartOfAccounts.Add(account);
            db.AuditEvents.Add(new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ActorUserId = Guid.Empty,
                Action = "Seeded",
                EntityType = "ChartOfAccount",
                EntityId = account.Id,
                OccurredAtUtc = now,
                AfterSummary = $"Profile={ManufacturingChartOfAccountsSeed.ProfileName};AccountNo={account.AccountNo};ParentAccountNo={account.ParentAccountNo};Level={account.AccountLevel};Type={account.AccountType};Title={account.Title};IsPostingAccount={account.IsPostingAccount};IsActive={account.IsActive}"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static int GetAccountLevel(ManufacturingChartOfAccountSeedItem account)
    {
        var level = 0;
        var current = account;
        while (current.ParentAccountNo is not null)
        {
            level++;
            current = ManufacturingChartOfAccountsSeed.Accounts.Single(x => x.AccountNo == current.ParentAccountNo);
        }
        return level;
    }

    private static Guid CreateDeterministicId(string accountNo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"OneZeroErp:{ManufacturingChartOfAccountsSeed.ProfileName}:{accountNo}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private async Task SeedDevelopmentUserAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(configuration["Company:DefaultCompanyId"], out var companyId) || companyId == Guid.Empty)
            throw new InvalidOperationException("Company:DefaultCompanyId must be a nonempty GUID.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        foreach (var seedUser in DevelopmentUsers)
        {
            var password = GetConfiguredPassword(seedUser.PasswordConfigurationKey);
            if (!string.IsNullOrWhiteSpace(password) && password.Length is < 12 or > 256)
                throw new InvalidOperationException("Development seed passwords must have 12 to 256 characters.");
            var now = clock.UtcNow;
            var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM [identity].[Identity_AppUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [UserName] = {seedUser.UserName}")
                .Include(x => x.Permissions).SingleOrDefaultAsync(cancellationToken);
            if (user is null && string.IsNullOrWhiteSpace(password)) continue;
            if (user is not null && user.Role != seedUser.Role) continue;
            if (user is not null && user.CompanyId != Guid.Empty && user.CompanyId != companyId)
                throw new InvalidOperationException("The existing development user belongs to another company. Review the company configuration before seeding.");
            var created = user is null;
            if (user is null)
            {
                user = new AppUserEntity
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    UserName = seedUser.UserName,
                    DisplayName = seedUser.DisplayName,
                    PasswordHash = PasswordHasher.Hash(password!),
                    Role = seedUser.Role,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.AppUsers.Add(user);
            }
            var membershipChanged = user.CompanyId == Guid.Empty;
            if (membershipChanged) user.CompanyId = companyId;
            var changed = created || membershipChanged;
            var desired = seedUser.Permissions.Concat(seedUser.UserName == "admin" ? new[] { "platform.diagnostics:CanView", "identity.users:CanEdit" } : []);
            foreach (var permission in desired)
            {
                if (user.Permissions.Any(x => x.Permission == permission)) continue;
                var newPermission = new AppUserPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    AppUserId = user.Id,
                    Permission = permission
                };
                db.AppUserPermissions.Add(newPermission);
                user.Permissions.Add(newPermission);
                changed = true;
            }
            if (changed)
                db.AuditEvents.Add(new()
                {
                    Id = Guid.NewGuid(),
                    ActorUserId = Guid.Empty,
                    CompanyId = companyId,
                    EntityId = user.Id,
                    EntityType = "AppUser",
                    Action = created ? "DevelopmentUserCreated" : "DevelopmentUserPermissionsUpdated",
                    OccurredAtUtc = now
                });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private string? GetConfiguredPassword(string configurationKey)
    {
        var password = configuration[configurationKey];
        if (!string.IsNullOrWhiteSpace(password))
            return password;

        if (configurationKey != "DevelopmentSeed:AdminPassword")
            return null;

        return configuration["DevelopmentSeed:Password"]
                ?? configuration["DevelopmentSeed__Password"]
                ?? configuration["DevelopmentSeed_Password"];
    }

    private sealed record DevelopmentSeedUser(
            string UserName,
            string DisplayName,
            string Role,
            string PasswordConfigurationKey,
            IReadOnlyList<string> Permissions);
}
