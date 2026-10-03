using System.Security.Claims;
using OneZeroErp.IdentityAccess;

namespace OneZeroErp.Api.Common;

public static class PermissionEndpointConvention
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new PermissionMetadata(permission)));
        return builder;
    }

    public static bool HasPermission(this ClaimsPrincipal principal, string requiredPermission) =>
        principal.FindAll(ClaimPermissionService.PermissionClaimType)
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(permission => string.Equals(permission, requiredPermission, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "*:" + requiredPermission[(requiredPermission.IndexOf(':') + 1)..], StringComparison.OrdinalIgnoreCase));
}

public sealed record PermissionMetadata(string Permission);
