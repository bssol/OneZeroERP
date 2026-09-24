using Microsoft.Extensions.Configuration;
using OneZeroErp.Application;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure;

namespace OneZeroErp.UnitTests;

public sealed class AuthenticationAndDashboardTests
{
    [Fact]
    public void Password_hashes_are_not_plaintext_and_verify_correct_password()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        Assert.NotEqual("correct horse battery staple", hash);
        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
        Assert.False(PasswordHasher.Verify("wrong password", hash));
    }

    [Fact]
    public async Task Authentication_returns_principal_and_jwt_for_valid_user()
    {
        var user = new AppUser(Guid.NewGuid(), "admin", "Administrator", PasswordHasher.Hash("secret"), "Administrator");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "development-only-signing-key-that-is-long-enough"
        }).Build();
        var service = new AppUserAuthenticationService(new StubUserStore(user), new JwtTokenService(configuration));

        var result = await service.AuthenticateAsync(new LoginRequest("admin", "secret"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public void Module_registry_contains_gl_and_marks_future_modules_as_planned()
    {
        var modules = new ModuleRegistry().GetModules();
        Assert.Contains(modules, x => x.Key == "gl" && x.IsImplemented);
        Assert.Contains(modules, x => x.Key == "ap" && !x.IsImplemented);
        Assert.Equal(10, modules.Count);
    }

    [Fact]
    public void Gl_dashboard_is_placeholder_only()
    {
        var dashboard = new DashboardService(new ModuleRegistry()).GetModuleDashboard("gl");
        Assert.Contains(dashboard.Metrics, x => x.Label == "Fiscal years" && x.Value == "—");
        Assert.Contains(dashboard.ChartPlaceholders, x => x.Contains("planned", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubUserStore(AppUser user) : IAppUserStore
    {
        public AppUser? FindByUserName(string userName) => userName == user.UserName ? user : null;
    }
}
