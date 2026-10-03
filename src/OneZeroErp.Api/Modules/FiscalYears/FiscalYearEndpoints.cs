using OneZeroErp.Api.Common;
using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;

namespace OneZeroErp.Api.Modules.FiscalYears;

public static class FiscalYearEndpoints
{
    public static RouteGroupBuilder MapFiscalYearEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/fiscal-years").RequireAuthorization();
        group.MapGet("", GetPageAsync).RequirePermission("gl.fiscal-years:CanView");
        group.MapGet("/{id:guid}", GetAsync).RequirePermission("gl.fiscal-years:CanView");
        group.MapPost("", CreateAsync).RequirePermission("gl.fiscal-years:CanAdd");
        group.MapPut("/{id:guid}", UpdateAsync).RequirePermission("gl.fiscal-years:CanEdit");
        group.MapDelete("/{id:guid}", DeleteAsync).RequirePermission("gl.fiscal-years:CanDelete");
        return group;
    }

    private static async Task<IResult> GetPageAsync(int? pageNumber, int? pageSize, ISetupFiscalYearService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetPageAsync(new PagedQuery(pageNumber ?? 1, pageSize ?? 25), cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, ISetupFiscalYearService service, CancellationToken cancellationToken)
    {
        var item = await service.GetAsync(id, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> CreateAsync(SetupFiscalYearEditModel model, HttpContext context, ISetupFiscalYearService service, CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(model, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.Created($"/api/v1/fiscal-years/{result.Item!.Id}", result.Item) : Results.BadRequest(new { error = result.ErrorMessage });
    }

    private static async Task<IResult> UpdateAsync(Guid id, SetupFiscalYearEditModel model, HttpContext context, ISetupFiscalYearService service, CancellationToken cancellationToken)
    {
        model.Id = id;
        var result = await service.SaveAsync(model, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.Ok(result.Item) : Results.BadRequest(new { error = result.ErrorMessage });
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context, ISetupFiscalYearService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, context.ActorId(), cancellationToken);
        return result.Succeeded ? Results.NoContent() : Results.NotFound(new { error = result.ErrorMessage });
    }
}
