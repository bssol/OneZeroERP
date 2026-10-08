using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using OneZeroErp.Application;
using OneZeroErp.Infrastructure;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;
using OneZeroErp.Infrastructure.Runtime;
using OneZeroErp.Web;
using OneZeroErp.Web.Components;
using OneZeroErp.Web.Endpoints.Authentication;
using OneZeroErp.Web.Endpoints.Identity;

var recoveryRequested = args is ["reset-admin-password"];
var developmentPasswordsRequested = args is ["sync-development-passwords"];
var builder = WebApplication.CreateBuilder(recoveryRequested || developmentPasswordsRequested ? [] : args);
builder.AddOneZeroRuntime();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")))
        .SetApplicationName("OneZeroErp");
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, SessionRevalidatingAuthenticationStateProvider>();
builder.Services.AddOneZeroPlatform(builder.Configuration);
builder.AddOneZeroSecurity(browser: true);
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database");
builder.Services.AddScoped<IThemeService, ThemeService>();
var app = builder.Build();
if (developmentPasswordsRequested)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Development password synchronization is available only in Development.");
    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
        throw new InvalidOperationException("Set ConnectionStrings:Default explicitly for local password synchronization.");
    var target = new SqlConnectionStringBuilder(app.Configuration.GetConnectionString("Default"));
    if (string.IsNullOrWhiteSpace(target.DataSource) || string.IsNullOrWhiteSpace(target.InitialCatalog))
        throw new InvalidOperationException("Choose an explicit SQL Server and database for local password synchronization.");
    if (!Guid.TryParse(app.Configuration["Company:DefaultCompanyId"], out var companyId) || companyId == Guid.Empty)
        throw new InvalidOperationException("Set Company:DefaultCompanyId to the intended company.");
    Console.WriteLine($"Synchronizing development passwords in {target.DataSource}/{target.InitialCatalog}, company {companyId}.");
    await using var scope = app.Services.CreateAsyncScope();
    var userNames = await scope.ServiceProvider.GetRequiredService<DevelopmentCredentialSynchronizer>().ApplyAsync(companyId);
    Console.WriteLine($"Updated {userNames.Count} existing users: {string.Join(", ", userNames)}. Prior sessions were revoked.");
    return;
}
if (recoveryRequested)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Local admin recovery is available only in Development.");
    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
        throw new InvalidOperationException("Set ConnectionStrings:Default explicitly for local admin recovery.");
    var recoveryTarget = new SqlConnectionStringBuilder(app.Configuration.GetConnectionString("Default"));
    if (string.IsNullOrWhiteSpace(recoveryTarget.DataSource) || string.IsNullOrWhiteSpace(recoveryTarget.InitialCatalog))
        throw new InvalidOperationException("Choose an explicit SQL Server and database for local admin recovery.");
    if (!Guid.TryParse(app.Configuration["Company:DefaultCompanyId"], out var recoveryCompanyId) || recoveryCompanyId == Guid.Empty)
        throw new InvalidOperationException("Set Company:DefaultCompanyId to the admin account's company.");
    if (Console.IsInputRedirected) throw new InvalidOperationException("Run this command in an interactive terminal.");
    Console.WriteLine($"Resetting admin in {recoveryTarget.DataSource}/{recoveryTarget.InitialCatalog}, company {recoveryCompanyId}.");
    var recoveryPassword = ReadPassword("New password: ");
    var confirmation = ReadPassword("Confirm password: ");
    if (recoveryPassword != confirmation) throw new InvalidOperationException("The passwords do not match.");
    await using var recoveryScope = app.Services.CreateAsyncScope();
    await recoveryScope.ServiceProvider.GetRequiredService<AdminPasswordRecovery>().ResetAsync(recoveryCompanyId, recoveryPassword);
    Console.WriteLine("Admin password reset. Existing sessions have been revoked.");
    return;
}
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}
app.UseOneZeroRuntime();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    if (!app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapAuthenticationEndpoints();
app.MapIdentityEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var characters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(characters.ToArray()); }
        if (key.Key == ConsoleKey.Backspace) { if (characters.Count > 0) characters.RemoveAt(characters.Count - 1); continue; }
        if (!char.IsControl(key.KeyChar)) characters.Add(key.KeyChar);
    }
}
