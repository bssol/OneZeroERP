using OneZeroErp.Application;
using Microsoft.Extensions.DependencyInjection;

namespace OneZeroErp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOneZeroPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();
        services.AddSingleton<IDashboardService, DashboardService>();
        return services;
    }
}
