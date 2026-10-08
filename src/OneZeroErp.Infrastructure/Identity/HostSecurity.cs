using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OneZeroErp.Application;
using OneZeroErp.Application.Identity;

namespace OneZeroErp.Infrastructure.Identity;

public static class HostSecurity
{
    public static void AddOneZeroSecurity(this WebApplicationBuilder builder, bool browser)
    {
        var key = builder.Configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) && builder.Environment.IsDevelopment())
        {
            key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            builder.Configuration["Authentication:SigningKey"] = key;
        }
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Supply Authentication:SigningKey with at least 32 bytes through secret configuration.");
        var authentication = builder.Services.AddAuthentication(browser ? CookieAuthenticationDefaults.AuthenticationScheme : JwtBearerDefaults.AuthenticationScheme);
        authentication.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Authentication:Issuer"] ?? "OneZeroErp",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Authentication:Audience"] ?? "OneZeroErp.Api",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var current = await context.HttpContext.RequestServices.GetRequiredService<ISessionAuthenticationService>()
                        .ValidateSessionAsync(context.Principal!, context.HttpContext.RequestAborted);
                    if (current is null) context.Fail("The session is no longer valid.");
                    else context.Principal = current;
                }
            };
        });
        if (browser) authentication.AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/unauthorized";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnValidatePrincipal = async context =>
            {
                var current = await context.HttpContext.RequestServices.GetRequiredService<ISessionAuthenticationService>()
                    .ValidateSessionAsync(context.Principal!, context.HttpContext.RequestAborted);
                if (current is null) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(); }
                else context.ReplacePrincipal(current);
            };
        });
        builder.Services.AddAuthorization(options =>
        {
            // Blazor has explicit route authorization and public framework endpoints.
            if (!browser) options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("PlatformDiagnostics", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.Claims.Any(x => x.Type == "onezero:permission" && (x.Value == "platform.diagnostics:CanView" || x.Value == "*:CanView"))));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").RequireRateLimiting("authentication");
        group.MapPost("/login", async (LoginRequest request, ISessionAuthenticationService service, CancellationToken ct) =>
            TokenResponse(await service.AuthenticateAsync(request, ct))).AllowAnonymous();
        group.MapPost("/refresh", async (RefreshRequest request, ISessionAuthenticationService service, CancellationToken ct) =>
            TokenResponse(await service.RefreshAsync(request.RefreshToken, ct))).AllowAnonymous();
        group.MapPost("/logout", async (HttpContext context, ISessionAuthenticationService service, CancellationToken ct) =>
        { await service.RevokeAsync(context.User, ct); return Results.NoContent(); }).RequireAuthorization();
        group.MapPost("/password-reset/{userId:guid}", async (Guid userId, HttpContext context, ISessionAuthenticationService service, CancellationToken ct) =>
        {
            try
            {
                var grant = await service.IssuePasswordResetAsync(context.User, userId, ct);
                return grant is null ? Results.NotFound() : Results.Ok(grant);
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        }).RequireAuthorization();
        group.MapPost("/password-reset/complete", async (PasswordResetRequest request, ISessionAuthenticationService service, CancellationToken ct) =>
        {
            var result = await service.ResetPasswordAsync(request, ct);
            return result.Succeeded ? Results.NoContent() : Results.Problem(result.Error, statusCode: 400);
        }).AllowAnonymous();
        group.MapPost("/password/change", async (PasswordChangeRequest request, HttpContext context, ISessionAuthenticationService service, CancellationToken ct) =>
        {
            var result = await service.ChangePasswordAsync(context.User, request, ct);
            return result.Succeeded ? Results.NoContent() : Results.Problem(result.Error, statusCode: 400);
        }).RequireAuthorization();
    }

    private static IResult TokenResponse(AuthenticationResult result) => result.Succeeded
        ? Results.Ok(new { accessToken = result.AccessToken, refreshToken = result.RefreshToken, expiresIn = 900, sessionExpiresAtUtc = result.SessionExpiresAtUtc })
        : Results.Unauthorized();
}
