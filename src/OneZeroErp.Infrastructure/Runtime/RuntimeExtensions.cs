using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OneZeroErp.Infrastructure.Persistence;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OneZeroErp.Infrastructure.Runtime;

public static class RuntimeExtensions
{
    public static void AddOneZeroRuntime(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());
    }

    public static void UseOneZeroRuntime(this WebApplication app)
    {
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            await Results.Problem("An unexpected error occurred.", statusCode: 500,
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }));
        app.Use(async (context, next) =>
        {
            // Do not trust caller-supplied correlation strings or log credentials/bodies.
            context.TraceIdentifier = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            if (!context.Request.Path.StartsWithSegments("/_framework") && !Path.HasExtension(context.Request.Path))
                context.Response.Headers.CacheControl = "no-store";
            using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier });
            var started = Stopwatch.GetTimestamp();
            await next(context);
            app.Logger.LogInformation("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        });
    }
}

public sealed class DatabaseReadinessCheck(IDbContextFactory<ErpDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            if (!await db.Database.CanConnectAsync(cancellationToken)) return HealthCheckResult.Unhealthy("Database unavailable.");
            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return HealthCheckResult.Unhealthy("Migrations pending.");
            return HealthCheckResult.Healthy();
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("Database unavailable."); }
    }
}
