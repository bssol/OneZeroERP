using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Time;
using OneZeroErp.Infrastructure.GeneralLedger;
using OneZeroErp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ISetupFiscalYearService, SetupFiscalYearService>();
        services.AddScoped<IDayLockService, DayLockService>();
        services.AddScoped<IChartOfAccountService, ChartOfAccountService>();
        services.AddScoped<IVoucherTypeService, VoucherTypeService>();
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
