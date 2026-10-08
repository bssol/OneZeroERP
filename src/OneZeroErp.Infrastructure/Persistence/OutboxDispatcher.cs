using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneZeroErp.Application.Time;

namespace OneZeroErp.Infrastructure.Persistence;

// The first consumer is a durable local audit feed. No external delivery occurs.
// Receipt and acknowledgement share a transaction; replay uses the message ID.
public sealed class OutboxDispatcher(IDbContextFactory<ErpDbContext> factory, IClock clock)
{
    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var delivered = 0;
        for (var i = 0; i < 50; i++)
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var message = await db.OutboxMessages.FromSqlRaw("""
                SELECT TOP (1) * FROM [platform].[Platform_Outbox] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [DispatchedAtUtc] IS NULL ORDER BY [CreatedAtUtc], [Id]
                """).SingleOrDefaultAsync(cancellationToken);
            if (message is null) break;
            if (!await db.AuditDeliveries.AnyAsync(x => x.Id == message.Id, cancellationToken))
                db.AuditDeliveries.Add(new() { Id = message.Id, AuditEventId = message.AuditEventId, DeliveredAtUtc = clock.UtcNow });
            message.Attempts++;
            message.DispatchedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            delivered++;
        }
        return delivered;
    }
}

internal sealed class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Log type only: SQL exception text can contain user data.
                logger.LogError("Outbox dispatch failed ({ErrorType}); pending messages will be retried.", exception.GetType().Name);
            }
        }
    }
}
