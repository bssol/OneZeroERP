using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using OneZeroErp.Application.Identity;

namespace OneZeroErp.Web;

public sealed class SessionRevalidatingAuthenticationStateProvider(ILoggerFactory loggerFactory, IServiceScopeFactory scopes)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var principal = await scope.ServiceProvider.GetRequiredService<ISessionAuthenticationService>()
            .ValidateSessionAsync(authenticationState.User, cancellationToken);
        if (principal is null) return false;
        // Permission/role changes end stale circuits; a reload obtains fresh claims.
        return authenticationState.User.Claims.Select(x => (x.Type, x.Value)).ToHashSet()
            .SetEquals(principal.Claims.Select(x => (x.Type, x.Value)));
    }
}
