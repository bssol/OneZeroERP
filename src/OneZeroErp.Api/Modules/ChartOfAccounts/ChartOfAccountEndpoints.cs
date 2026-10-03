using OneZeroErp.Api.Common;
using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;

namespace OneZeroErp.Api.Modules.ChartOfAccounts;

public static class ChartOfAccountEndpoints
{
    public static RouteGroupBuilder MapChartOfAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/chart-of-accounts").RequireAuthorization();
        group.MapGet("", GetPageAsync).RequirePermission("gl.chart-of-accounts:CanView");
        group.MapGet("/{id:guid}", GetAsync).RequirePermission("gl.chart-of-accounts:CanView");
        group.MapPost("", CreateAsync).RequirePermission("gl.chart-of-accounts:CanAdd");
        group.MapPut("/{id:guid}", UpdateAsync).RequirePermission("gl.chart-of-accounts:CanEdit");
        group.MapPatch("/{id:guid}/status", SetActiveAsync).RequirePermission("gl.chart-of-accounts:CanEdit");
        return group;
    }

    private static async Task<IResult> GetPageAsync(int? pageNumber, int? pageSize, IChartOfAccountService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetPageAsync(new PagedQuery(pageNumber ?? 1, pageSize ?? 25), cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, IChartOfAccountService service, CancellationToken cancellationToken)
    {
        var item = await service.GetAsync(id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> CreateAsync(ChartOfAccountEditModel model, HttpContext context, IChartOfAccountService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(model, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.Created($"/api/v1/chart-of-accounts/{result.Item!.Id}", result.Item) : Results.BadRequest(new { error = result.ErrorMessage });
    }

    private static async Task<IResult> UpdateAsync(Guid id, ChartOfAccountEditModel model, HttpContext context, IChartOfAccountService service, CancellationToken cancellationToken)
    {
        model.Id = id;
        var result = await service.SaveAsync(model, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Item) : Results.BadRequest(new { error = result.ErrorMessage });
    }

    private static async Task<IResult> SetActiveAsync(Guid id, SetActiveRequest request, HttpContext context, IChartOfAccountService service, CancellationToken cancellationToken)
    {
        var result = await service.SetActiveAsync(id, request.IsActive, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Item) : Results.NotFound(new { error = result.ErrorMessage });
    }
}

public sealed record SetActiveRequest(bool IsActive);
