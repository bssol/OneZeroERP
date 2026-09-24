namespace OneZeroErp.Application;

public interface IModuleRegistry
{
    IReadOnlyList<ModuleNavigationItem> GetModules();
}

public interface IDashboardService
{
    ApplicationDashboardModel GetApplicationDashboard();
    ModuleDashboardModel GetModuleDashboard(string moduleKey);
}

public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public interface IThemeService
{
    IReadOnlyList<ThemeDefinition> Themes { get; }
    string CurrentTheme { get; }
    Task SetThemeAsync(string themeKey);
}

public sealed record ThemeDefinition(string Key, string Name, string Description);
