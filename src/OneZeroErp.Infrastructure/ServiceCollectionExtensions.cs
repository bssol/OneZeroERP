using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Identity;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.GeneralLedger;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;
using OneZeroErp.Infrastructure.Time;

namespace OneZeroErp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOneZeroPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(ConfiguredBusinessTimeZone.Resolve(configuration));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDashboardService, DashboardService>();
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=OneZeroErp;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
        services.AddDbContextFactory<ErpDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IPermissionService, ClaimPermissionService>();
        services.AddScoped<ISessionAuthenticationService, SqlSessionAuthenticationService>();
        services.AddScoped<AdminPasswordRecovery>();
        services.AddScoped<DevelopmentCredentialSynchronizer>();
        services.AddScoped<IAuthenticationService>(provider => provider.GetRequiredService<ISessionAuthenticationService>());
        services.AddScoped<OutboxDispatcher>();
        if (configuration.GetValue("Outbox:Enabled", true)) services.AddHostedService<OutboxWorker>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISetupFiscalYearService, SetupFiscalYearService>();
        services.AddScoped<IDayLockService, DayLockService>();
        services.AddScoped<IChartOfAccountService, ChartOfAccountService>();
        services.AddScoped<IVoucherTypeService, VoucherTypeService>();
        services.AddScoped<IVoucherDraftService, VoucherDraftService>();
        services.AddScoped<IDemoDataService, DemoDataService>();
        services.AddScoped<IBankAccountService, BankCashAccountService>();
        services.AddScoped<ICashAccountService, BankCashAccountService>();
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IExchangeRateService, CurrencyService>();
        services.AddScoped<ITaxConfigurationService, TaxConfigurationService>();
        services.AddScoped<IAppUserStore, SqlAppUserStore>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
