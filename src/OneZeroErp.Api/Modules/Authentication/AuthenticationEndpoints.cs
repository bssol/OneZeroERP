using OneZeroErp.Application;

namespace OneZeroErp.Api.Modules.Authentication;

public static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");
        group.MapPost("/login", LoginAsync);
        return group;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        var result = await authentication.AuthenticateAsync(request, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new { accessToken = result.AccessToken, expiresIn = 900 })
            : Results.Unauthorized();
    }
}
