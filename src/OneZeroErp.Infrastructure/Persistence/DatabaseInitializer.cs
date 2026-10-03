using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;

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
		await RepairChartOfAccountPostingColumnAsync(cancellationToken);
		await SeedDevelopmentUserAsync(cancellationToken);
	}

	private Task<int> RepairChartOfAccountPostingColumnAsync(CancellationToken cancellationToken) =>
		db.Database.ExecuteSqlRawAsync("""
			IF OBJECT_ID(N'[gl].[Gl_Setup_ChartOfAccounts]', N'U') IS NOT NULL
			   AND COL_LENGTH(N'[gl].[Gl_Setup_ChartOfAccounts]', N'IsPostingAccount') IS NULL
			BEGIN
				ALTER TABLE [gl].[Gl_Setup_ChartOfAccounts]
					ADD [IsPostingAccount] bit NOT NULL CONSTRAINT [DF_Gl_Setup_ChartOfAccounts_IsPostingAccount] DEFAULT (1);
			END
			""", cancellationToken);

	private async Task SeedDevelopmentUserAsync(CancellationToken cancellationToken)
	{
		var hasNewUsers = false;
		foreach (var seedUser in DevelopmentUsers)
		{
			var password = GetConfiguredPassword(seedUser.PasswordConfigurationKey);
			if (string.IsNullOrWhiteSpace(password))
				continue;

			var existingUser = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == seedUser.UserName, cancellationToken);
			if (existingUser is not null)
			{
				foreach (var permission in seedUser.Permissions)
				{
					await db.Database.ExecuteSqlInterpolatedAsync($"""
						IF NOT EXISTS
						(
							SELECT 1
							FROM [identity].[Identity_AppUserPermissions]
							WHERE [AppUserId] = {existingUser.Id} AND [Permission] = {permission}
						)
						INSERT INTO [identity].[Identity_AppUserPermissions] ([Id], [AppUserId], [Permission])
						VALUES (NEWID(), {existingUser.Id}, {permission});
					""", cancellationToken);
				}
				continue;
			}

			hasNewUsers = true;
			var now = clock.UtcNow;
			db.AppUsers.Add(new AppUserEntity
			{
				Id = Guid.NewGuid(),
				UserName = seedUser.UserName,
				DisplayName = seedUser.DisplayName,
				PasswordHash = PasswordHasher.Hash(password),
				Role = seedUser.Role,
				IsActive = true,
				CreatedAtUtc = now,
				UpdatedAtUtc = now,
				Permissions = seedUser.Permissions.Select(permission => new AppUserPermissionEntity
				{
					Id = Guid.NewGuid(),
					Permission = permission
				}).ToList()
			});
		}

		if (hasNewUsers)
			await db.SaveChangesAsync(cancellationToken);
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
