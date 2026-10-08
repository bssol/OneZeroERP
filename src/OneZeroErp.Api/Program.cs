using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OneZeroErp.Api.Common;
using OneZeroErp.Infrastructure;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;
using OneZeroErp.Infrastructure.Runtime;

var builder = WebApplication.CreateBuilder(args);
builder.AddOneZeroRuntime();
builder.Services.AddOneZeroPlatform(builder.Configuration);
builder.AddOneZeroSecurity(browser: false);
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "OneZero ERP API", Version = "v1", Description = "Application API for OneZero ERP." });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT access token."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddOpenApi();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    app.MapOpenApi().AllowAnonymous();
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseOneZeroRuntime();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var requiredPermission = context.GetEndpoint()?.Metadata.GetMetadata<PermissionMetadata>()?.Permission;
    if (requiredPermission is not null && !context.User.HasPermission(requiredPermission))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapGet("/api/v1/platform/health", () => Results.Ok(new { status = "ready" })).RequireAuthorization("PlatformDiagnostics");
app.MapSessionEndpoints();
app.MapApiEndpoints();
app.Run();

public partial class Program;
