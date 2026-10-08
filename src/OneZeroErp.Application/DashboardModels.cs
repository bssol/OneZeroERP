namespace OneZeroErp.Application;

public sealed record ModuleNavigationItem(
    string Key,
    string Name,
    string Description,
    string Route,
    string Icon,
    bool IsImplemented,
    string Status = "Planned");

public sealed record DashboardMetric(string Label, string Value, string Comparison, string Trend, string Tone);

public sealed record DashboardAction(string Title, string Description, string Route, string Tone);

public sealed record DashboardActivity(string Title, string Detail, string Timestamp, string Tone);

public sealed record ModuleDashboardModel(
    string Title,
    string Description,
    IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<DashboardAction> QuickActions,
    IReadOnlyList<DashboardActivity> RecentActivity,
    IReadOnlyList<string> ChartPlaceholders,
    IReadOnlyList<ModuleNavigationItem> Navigation);

public sealed record ApplicationDashboardModel(
    IReadOnlyList<DashboardMetric> Metrics,
    IReadOnlyList<DashboardAction> PendingActions,
    IReadOnlyList<DashboardActivity> RecentActivity,
    IReadOnlyList<ModuleNavigationItem> Modules);

public sealed record LoginRequest(string UserName, string Password);

public sealed record AuthenticationResult(bool Succeeded, string? AccessToken, System.Security.Claims.ClaimsPrincipal? Principal, string? Error,
    string? RefreshToken = null, DateTimeOffset? SessionExpiresAtUtc = null)
{
    public static AuthenticationResult Failure(string error) => new(false, null, null, error);
}
