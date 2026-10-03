using System.Security.Claims;

namespace OneZeroErp.Web.Endpoints.Identity;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/me", GetCurrentUser).RequireAuthorization();
        return endpoints;
    }

    private static IResult GetCurrentUser(HttpContext httpContext) => Results.Ok(new
    {
        Name = httpContext.User.Identity?.Name,
        Role = httpContext.User.FindFirstValue(ClaimTypes.Role)
    });
}
