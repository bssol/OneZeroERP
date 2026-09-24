using System.Security.Claims;
using OneZeroErp.Application;

namespace OneZeroErp.IdentityAccess;

public sealed class ClaimPermissionService : IPermissionService
{
    public const string PermissionClaimType = "onezero:permission";

    public Task<PermissionSet> GetPermissionsAsync(ClaimsPrincipal principal, string resource, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        var permissions = principal.FindAll(PermissionClaimType)
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParsePermission)
            .Where(permission => permission is not null &&
                (string.Equals(permission.Value.Resource, resource, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(permission.Value.Resource, "*", StringComparison.OrdinalIgnoreCase)))
            .Select(permission => permission!.Value.Action)
            .ToHashSet();
        return Task.FromResult(new PermissionSet(permissions));
    }

    private static (string Resource, PermissionAction Action)? ParsePermission(string value)
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1 || !Enum.TryParse<PermissionAction>(value[(separator + 1)..], true, out var action))
            return null;
        return (value[..separator], action);
    }
}
