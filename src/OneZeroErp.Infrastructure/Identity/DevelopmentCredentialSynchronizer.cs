using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.Identity;

public sealed class DevelopmentCredentialSynchronizer(
    IDbContextFactory<ErpDbContext> factory, IConfiguration configuration, IClock clock)
{
    private static readonly (string UserName, string Role, string Setting)[] Targets =
    [
        ("admin", "Administrator", "DevelopmentSeed:AdminPassword"),
        ("fiscal.manager", "Fiscal Manager", "DevelopmentSeed:FiscalManagerPassword"),
        ("gl.accountant", "Accountant", "DevelopmentSeed:AccountantPassword"),
        ("gl.viewer", "Viewer", "DevelopmentSeed:ViewerPassword")
    ];

    public async Task<IReadOnlyList<string>> ApplyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new InvalidOperationException("Set Company:DefaultCompanyId to the intended company.");

        var passwords = Targets.Select(target => (target.UserName, target.Role, Password: configuration[target.Setting])).ToArray();
        if (passwords.Any(x => string.IsNullOrWhiteSpace(x.Password) || x.Password.Length is < 12 or > 256))
            throw new InvalidOperationException("Configure all four development passwords with 12 to 256 characters before synchronization.");

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var users = new List<(AppUserEntity User, string Password)>();
        foreach (var target in passwords)
        {
            var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM [identity].[Identity_AppUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [UserName] = {target.UserName}")
                .SingleOrDefaultAsync(cancellationToken);
            if (user is null || !user.IsActive || user.Role != target.Role ||
                user.CompanyId != Guid.Empty && user.CompanyId != companyId)
                throw new InvalidOperationException($"The expected active development user '{target.UserName}' was not found in the selected company and role. No passwords were changed.");
            users.Add((user, target.Password!));
        }

        var now = clock.UtcNow;
        var ids = users.Select(x => x.User.Id).ToArray();
        foreach (var session in await db.UserSessions.Where(x => ids.Contains(x.AppUserId) && x.RevokedAtUtc == null).ToListAsync(cancellationToken))
            session.RevokedAtUtc = now;
        foreach (var grant in await db.PasswordResets.Where(x => ids.Contains(x.AppUserId) && x.ConsumedAtUtc == null).ToListAsync(cancellationToken))
            grant.ConsumedAtUtc = now;

        foreach (var (user, password) in users)
        {
            var membershipWasEmpty = user.CompanyId == Guid.Empty;
            if (membershipWasEmpty) user.CompanyId = companyId;
            user.PasswordHash = PasswordHasher.Hash(password);
            user.FailedLoginCount = 0;
            user.LockedUntilUtc = null;
            user.UpdatedAtUtc = now;
            db.AuditEvents.Add(new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ActorUserId = Guid.Empty,
                EntityId = user.Id,
                EntityType = "AppUser",
                Action = "DevelopmentPasswordSynchronized",
                OccurredAtUtc = now,
                AfterSummary = $"Local development password reset; prior sessions and grants revoked; company membership assigned={membershipWasEmpty}."
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return users.Select(x => x.User.UserName).ToArray();
    }
}
