using System.Data;
using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.Identity;

public sealed class AdminPasswordRecovery(IDbContextFactory<ErpDbContext> factory, IClock clock)
{
    public async Task ResetAsync(Guid companyId, string password, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Select a company before resetting its admin password.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 256)
            throw new ArgumentException("Use a password between 12 and 256 characters.", nameof(password));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM [identity].[Identity_AppUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [UserName] = {"admin"}")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null || user.CompanyId != companyId || !user.IsActive || user.Role != "Administrator")
            throw new InvalidOperationException("An active Administrator named admin was not found in the selected company.");

        var now = clock.UtcNow;
        user.PasswordHash = PasswordHasher.Hash(password);
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        user.UpdatedAtUtc = now;
        foreach (var session in await db.UserSessions.Where(x => x.AppUserId == user.Id && x.RevokedAtUtc == null).ToListAsync(cancellationToken))
            session.RevokedAtUtc = now;
        foreach (var grant in await db.PasswordResets.Where(x => x.AppUserId == user.Id && x.ConsumedAtUtc == null).ToListAsync(cancellationToken))
            grant.ConsumedAtUtc = now;
        db.AuditEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ActorUserId = Guid.Empty,
            EntityId = user.Id,
            EntityType = "AppUser",
            Action = "AdminPasswordRecoveredLocally",
            OccurredAtUtc = now,
            AfterSummary = "Local operator reset the admin password; sessions and reset grants revoked."
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
