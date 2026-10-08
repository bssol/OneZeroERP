using System.Security.Claims;
using OneZeroErp.Api.Modules.Authentication;
using OneZeroErp.Api.Modules.ChartOfAccounts;
using OneZeroErp.Api.Modules.FiscalYears;
using OneZeroErp.IdentityAccess;

namespace OneZeroErp.Api.Common;

public static class ApiEndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");
        api.MapFiscalYearEndpoints();
        api.MapChartOfAccountEndpoints();
        return endpoints;
    }

    public static Guid ActorId(this HttpContext context) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var id)
            ? id
            : throw new InvalidOperationException("The authenticated identity has no valid user identifier.");
}
