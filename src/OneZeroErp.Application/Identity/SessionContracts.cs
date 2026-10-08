using System.Security.Claims;

namespace OneZeroErp.Application.Identity;

public static class IdentityClaims
{
    public const string SessionId = "sid";
    public const string CompanyId = "onezero:company";
}

public sealed record RefreshRequest(string RefreshToken);
public sealed record PasswordResetRequest(string Token, string NewPassword);
public sealed record PasswordChangeRequest(string CurrentPassword, string NewPassword);
public sealed record ResetGrant(string Token, DateTimeOffset ExpiresAtUtc);
public sealed record SecurityOperationResult(bool Succeeded, string? Error = null);

public interface ISessionAuthenticationService : IAuthenticationService
{
    Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<ClaimsPrincipal?> ValidateSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task RevokeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<ResetGrant?> IssuePasswordResetAsync(ClaimsPrincipal actor, Guid userId, CancellationToken cancellationToken = default);
    Task<SecurityOperationResult> ResetPasswordAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
    Task<SecurityOperationResult> ChangePasswordAsync(ClaimsPrincipal actor, PasswordChangeRequest request, CancellationToken cancellationToken = default);
}
