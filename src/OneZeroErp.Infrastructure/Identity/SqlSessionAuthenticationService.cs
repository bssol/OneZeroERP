using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application;
using OneZeroErp.Application.Identity;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.Identity;

public sealed class SqlSessionAuthenticationService(IDbContextFactory<ErpDbContext> factory, JwtTokenService tokens,
    IPermissionService permissions, IClock clock) : ISessionAuthenticationService
{
    private static readonly string DummyHash = PasswordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 100 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
            return AuthenticationResult.Failure("Invalid credentials.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var name = request.UserName.Trim();
        // Update locks serialize lockout and credential changes before verifying a password.
        var user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM [identity].[Identity_AppUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [UserName] = {name}")
            .Include(x => x.Permissions).SingleOrDefaultAsync(cancellationToken);
        var now = clock.UtcNow;
        var matches = PasswordHasher.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !user.IsActive || user.CompanyId == Guid.Empty || user.LockedUntilUtc > now || !matches)
        {
            if (user is not null && user.LockedUntilUtc <= now) { user.LockedUntilUtc = null; user.FailedLoginCount = 0; }
            if (user is { IsActive: true } && user.LockedUntilUtc is null && !matches)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5) user.LockedUntilUtc = now.AddMinutes(15);
            }
            Audit(db, user, "LoginFailed", user?.Id ?? Guid.Empty);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failure("Invalid credentials.");
        }
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        var session = new UserSessionEntity { Id = Guid.NewGuid(), AppUserId = user.Id, CompanyId = user.CompanyId, CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(8) };
        db.UserSessions.Add(session);
        var result = IssueTokens(db, user, session);
        Audit(db, user, "LoggedIn", user.Id);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!IsTokenShapeValid(refreshToken)) return AuthenticationResult.Failure("Invalid refresh token.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var hash = HashToken(refreshToken);
        var reference = await (from token in db.RefreshTokens.AsNoTracking()
                               join session in db.UserSessions on token.SessionId equals session.Id
                               where token.TokenHash == hash
                               select new { session.AppUserId, session.Id }).SingleOrDefaultAsync(cancellationToken);
        if (reference is null) return AuthenticationResult.Failure("Invalid refresh token.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await LockUserAsync(db, reference.AppUserId, cancellationToken);
        var sessionEntity = await db.UserSessions.SingleAsync(x => x.Id == reference.Id, cancellationToken);
        var tokenEntity = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash, cancellationToken);
        if (tokenEntity.ConsumedAtUtc.HasValue)
        {
            sessionEntity.RevokedAtUtc ??= clock.UtcNow;
            Audit(db, user, "RefreshTokenReuseDetected", reference.AppUserId);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failure("Invalid refresh token.");
        }
        if (user is null || !user.IsActive || user.CompanyId == Guid.Empty || sessionEntity.CompanyId != user.CompanyId || sessionEntity.RevokedAtUtc.HasValue || sessionEntity.ExpiresAtUtc <= clock.UtcNow)
            return AuthenticationResult.Failure("Invalid refresh token.");
        tokenEntity.ConsumedAtUtc = clock.UtcNow;
        var result = IssueTokens(db, user, sessionEntity);
        Audit(db, user, "RefreshTokenRotated", user.Id);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ClaimsPrincipal?> ValidateSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirstValue(IdentityClaims.SessionId), out var sessionId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var userId)) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var now = clock.UtcNow;
        var session = await db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && x.AppUserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, cancellationToken);
        if (session is null) return null;
        var user = await db.AppUsers.AsNoTracking().Include(x => x.Permissions).SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null || user.CompanyId == Guid.Empty || session.CompanyId != user.CompanyId || principal.FindFirstValue(IdentityClaims.CompanyId) != user.CompanyId.ToString()) return null;
        return Principal(user, sessionId, principal.Identity.AuthenticationType ?? "OneZeroErpCookie");
    }

    public async Task RevokeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(principal.FindFirstValue(IdentityClaims.SessionId), out var sessionId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await LockUserAsync(db, userId, cancellationToken);
        var session = await db.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.AppUserId == userId, cancellationToken);
        if (session is null || session.RevokedAtUtc.HasValue) return;
        session.RevokedAtUtc = clock.UtcNow;
        Audit(db, user, "SessionRevoked", userId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ResetGrant?> IssuePasswordResetAsync(ClaimsPrincipal actor, Guid userId, CancellationToken cancellationToken = default)
    {
        var current = await ValidateSessionAsync(actor, cancellationToken);
        if (current is null || !(await permissions.GetPermissionsAsync(current, "identity.users", cancellationToken)).Allows(PermissionAction.CanEdit))
            throw new UnauthorizedAccessException("Password reset permission is required.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await IsCurrentSessionAsync(db, current, cancellationToken)) throw new UnauthorizedAccessException("Sign in again.");
        var actorId = Guid.Parse(current.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await db.AppUserPermissions.AnyAsync(x => x.AppUserId == actorId && (x.Permission == "identity.users:CanEdit" || x.Permission == "*:CanEdit"), cancellationToken))
            throw new UnauthorizedAccessException("Password reset permission is required.");
        var user = await LockUserAsync(db, userId, cancellationToken);
        if (user is null || !user.IsActive || user.CompanyId.ToString() != current.FindFirstValue(IdentityClaims.CompanyId)) return null;
        foreach (var pending in await db.PasswordResets.Where(x => x.AppUserId == userId && x.ConsumedAtUtc == null).ToListAsync(cancellationToken)) pending.ConsumedAtUtc = clock.UtcNow;
        var raw = NewToken();
        var expires = clock.UtcNow.AddMinutes(15);
        db.PasswordResets.Add(new() { Id = Guid.NewGuid(), AppUserId = userId, TokenHash = HashToken(raw), ExpiresAtUtc = expires });
        Audit(db, user, "PasswordResetIssued", Guid.Parse(current.FindFirstValue(ClaimTypes.NameIdentifier)!));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(raw, expires);
    }

    public async Task<SecurityOperationResult> ResetPasswordAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidPassword(request.NewPassword)) return new(false, "Use a password between 12 and 256 characters.");
        if (!IsTokenShapeValid(request.Token)) return new(false, "Invalid or expired reset grant.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var hash = HashToken(request.Token);
        var reference = await db.PasswordResets.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (reference is null) return new(false, "Invalid or expired reset grant.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await LockUserAsync(db, reference.AppUserId, cancellationToken);
        var grant = await db.PasswordResets.SingleAsync(x => x.Id == reference.Id, cancellationToken);
        if (user is null || !user.IsActive || grant.ConsumedAtUtc.HasValue || grant.ExpiresAtUtc <= clock.UtcNow) return new(false, "Invalid or expired reset grant.");
        await ReplacePasswordAsync(db, user, request.NewPassword, "PasswordResetCompleted", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    public async Task<SecurityOperationResult> ChangePasswordAsync(ClaimsPrincipal actor, PasswordChangeRequest request, CancellationToken cancellationToken = default)
    {
        var current = await ValidateSessionAsync(actor, cancellationToken);
        if (current is null) return new(false, "Sign in again.");
        if (!ValidPassword(request.NewPassword)) return new(false, "Use a password between 12 and 256 characters.");
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 256) return new(false, "Invalid credentials.");
        var userId = Guid.Parse(current.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await LockUserAsync(db, userId, cancellationToken);
        if (!await IsCurrentSessionAsync(db, current, cancellationToken)) return new(false, "Sign in again.");
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            Audit(db, user, "PasswordChangeRejected", userId);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, "Invalid credentials.");
        }
        await ReplacePasswordAsync(db, user, request.NewPassword, "PasswordChanged", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true);
    }

    private async Task ReplacePasswordAsync(ErpDbContext db, AppUserEntity user, string password, string action, CancellationToken cancellationToken)
    {
        user.PasswordHash = PasswordHasher.Hash(password);
        user.UpdatedAtUtc = clock.UtcNow;
        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        foreach (var session in await db.UserSessions.Where(x => x.AppUserId == user.Id && x.RevokedAtUtc == null).ToListAsync(cancellationToken)) session.RevokedAtUtc = clock.UtcNow;
        foreach (var grant in await db.PasswordResets.Where(x => x.AppUserId == user.Id && x.ConsumedAtUtc == null).ToListAsync(cancellationToken)) grant.ConsumedAtUtc = clock.UtcNow;
        Audit(db, user, action, user.Id);
        await db.SaveChangesAsync(cancellationToken);
    }

    private AuthenticationResult IssueTokens(ErpDbContext db, AppUserEntity user, UserSessionEntity session)
    {
        var raw = NewToken();
        db.RefreshTokens.Add(new() { Id = Guid.NewGuid(), SessionId = session.Id, TokenHash = HashToken(raw) });
        var appUser = new AppUser(user.Id, user.UserName, user.DisplayName, user.PasswordHash, user.Role, user.IsActive, user.Permissions.Select(x => x.Permission).ToArray());
        return new(true, tokens.CreateToken(appUser, session.Id, user.CompanyId), Principal(user, session.Id), null, raw, session.ExpiresAtUtc);
    }

    private static ClaimsPrincipal Principal(AppUserEntity user, Guid sessionId, string scheme = "OneZeroErpCookie")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role), new("unique_name", user.UserName),
            new(IdentityClaims.SessionId, sessionId.ToString()), new(IdentityClaims.CompanyId, user.CompanyId.ToString())
        };
        claims.AddRange(user.Permissions.Select(x => new Claim(ClaimPermissionService.PermissionClaimType, x.Permission)));
        return new(new ClaimsIdentity(claims, scheme));
    }

    private void Audit(ErpDbContext db, AppUserEntity? user, string action, Guid actorId) => db.AuditEvents.Add(new()
    {
        Id = Guid.NewGuid(),
        ActorUserId = actorId,
        CompanyId = user?.CompanyId,
        EntityId = user?.Id ?? Guid.Empty,
        EntityType = "AppUser",
        Action = action,
        OccurredAtUtc = clock.UtcNow,
        AfterSummary = user is null ? null : $"Active={user.IsActive};FailedLoginCount={user.FailedLoginCount};LockedUntilUtc={user.LockedUntilUtc:O}"
    });

    private static Task<AppUserEntity?> LockUserAsync(ErpDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.AppUsers.FromSqlInterpolated($"SELECT * FROM [identity].[Identity_AppUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
            .Include(x => x.Permissions).SingleOrDefaultAsync(cancellationToken);
    private Task<bool> IsCurrentSessionAsync(ErpDbContext db, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(actor.FindFirstValue(IdentityClaims.SessionId)!);
        var actorId = Guid.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var companyId = Guid.Parse(actor.FindFirstValue(IdentityClaims.CompanyId)!);
        var now = clock.UtcNow;
        return (from session in db.UserSessions
                join user in db.AppUsers on session.AppUserId equals user.Id
                where session.Id == sessionId && session.AppUserId == actorId && session.CompanyId == companyId
                    && user.CompanyId == companyId && user.IsActive && session.RevokedAtUtc == null && session.ExpiresAtUtc > now
                select session.Id).AnyAsync(cancellationToken);
    }
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool IsTokenShapeValid(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);
    private static bool ValidPassword(string? password) => !string.IsNullOrWhiteSpace(password) && password.Length is >= 12 and <= 256;
}
