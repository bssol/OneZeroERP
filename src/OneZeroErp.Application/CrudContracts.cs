using System.Security.Claims;

namespace OneZeroErp.Application;

public enum PermissionAction
{
    CanView,
    CanAdd,
    CanEdit,
    CanDelete,
    CanPrint,
    CanExport
}

public sealed record PermissionSet(IReadOnlySet<PermissionAction> AllowedActions)
{
    public static PermissionSet Empty { get; } = new(new HashSet<PermissionAction>());
    public bool Allows(PermissionAction action) => AllowedActions.Contains(action);
}

public interface IPermissionService
{
    Task<PermissionSet> GetPermissionsAsync(ClaimsPrincipal principal, string resource, CancellationToken cancellationToken = default);
}

public sealed record PagedQuery(int PageNumber = 1, int PageSize = 25)
{
    public int SafePageNumber => Math.Max(1, PageNumber);
    public int SafePageSize => Math.Clamp(PageSize, 1, 200);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public sealed record PageActionResult(bool Succeeded, string? ErrorMessage = null, string? SuccessMessage = null)
{
    public static PageActionResult Success(string? message = null) => new(true, SuccessMessage: message);
    public static PageActionResult Failure(string message) => new(false, message);
}