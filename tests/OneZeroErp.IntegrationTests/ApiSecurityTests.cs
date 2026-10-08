using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OneZeroErp.Application;

namespace OneZeroErp.IntegrationTests;

public sealed class ApiSecurityTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private WebApplicationFactory<Program> CreateHost() => new ApiFactory(fixture.Configuration);

    private sealed class ApiFactory(IConfiguration configuration) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddConfiguration(configuration));
            return base.CreateHost(builder);
        }
    }

    [Fact]
    public async Task Protected_endpoint_denies_anonymous_and_unprivileged_users_and_accepts_authorized_user()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
        var viewer = await fixture.AddUserAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, viewer.UserName));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
        var admin = await fixture.AddUserAsync("platform.diagnostics:CanView");
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, admin.UserName));
        var response = await client.GetAsync("/api/v1/platform/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
    }

    [Fact]
    public async Task Permission_removal_and_account_deactivation_take_effect_for_existing_jwt()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        var user = await fixture.AddUserAsync("platform.diagnostics:CanView");
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, user.UserName));
        await using var db = fixture.CreateDbContext();
        db.AppUserPermissions.RemoveRange(await db.AppUserPermissions.Where(x => x.AppUserId == user.Id).ToListAsync());
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
        var stored = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        stored.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
    }

    [Fact]
    public async Task Liveness_and_readiness_are_public_and_refresh_response_is_not_cacheable()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        var user = await fixture.AddUserAsync();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.UserName, fixture.Password));
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = body.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.True(refresh.Headers.CacheControl?.NoStore);
    }

    private async Task<string> LoginAsync(HttpClient client, string userName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(userName, fixture.Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    [Theory]
    [InlineData("UnexpectedIssuer", "OneZeroErp.Api")]
    [InlineData("OneZeroErp", "UnexpectedAudience")]
    public async Task Wrong_issuer_or_audience_is_rejected_even_with_a_valid_signature(string issuer, string audience)
    {
        using var host = CreateHost();
        using var client = host.CreateClient();
        var user = await fixture.AddUserAsync("platform.diagnostics:CanView");
        var login = await fixture.Authentication.AuthenticateAsync(new(user.UserName, fixture.Password));
        var original = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        var token = new JwtSecurityToken(issuer, audience, original.Claims.Where(x => x.Type is not ("iss" or "aud" or "exp" or "nbf" or "iat")), DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(fixture.Configuration["Authentication:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/health")).StatusCode);
    }
}
