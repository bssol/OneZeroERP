using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OneZeroErp.Application;

namespace OneZeroErp.SharedUi;

public abstract class PermissionPagedPageBase<TEntity> : ComponentBase
{
    [Inject] protected AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] protected IPermissionService PermissionService { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    protected PermissionSet Permissions { get; private set; } = PermissionSet.Empty;
    protected PagedResult<TEntity>? PageResult { get; private set; }
    protected IReadOnlyList<TEntity> Items => PageResult?.Items ?? Array.Empty<TEntity>();
    protected bool IsLoading { get; private set; }
    protected string? ErrorMessage { get; private set; }
    protected bool IsUnauthorized => !Permissions.Allows(PermissionAction.CanView);
    protected bool IsDeleteConfirmationOpen { get; private set; }
    protected TEntity? PendingDelete { get; private set; }
    protected bool IsEntityDialogOpen { get; private set; }
    protected TEntity? EditingEntity { get; private set; }
    protected abstract string PermissionResource { get; }
    protected virtual int PageSize => 25;
    protected abstract Task<PagedResult<TEntity>> LoadEntitiesAsync(PagedQuery query, CancellationToken cancellationToken = default);
    protected virtual Task DeleteEntityAsync(TEntity entity, CancellationToken cancellationToken = default) => throw new NotSupportedException("This entity page has not enabled delete operations.");
    protected virtual string DeleteConfirmationMessage => "Are you sure you want to delete this item? This action cannot be undone.";

    protected override async Task OnInitializedAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        Permissions = await PermissionService.GetPermissionsAsync(authenticationState.User, PermissionResource);
        if (Permissions.Allows(PermissionAction.CanView)) await RefreshAsync();
    }

    protected bool Can(PermissionAction action) => Permissions.Allows(action);
    protected async Task RefreshAsync(int pageNumber = 1)
    {
        if (!Can(PermissionAction.CanView)) return;
        IsLoading = true; ErrorMessage = null;
        try { PageResult = await LoadEntitiesAsync(new PagedQuery(pageNumber, PageSize)); }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsLoading = false; }
    }

    protected Task GoToPageAsync(int pageNumber) => PageResult is null || pageNumber < 1 || pageNumber > PageResult.TotalPages ? Task.CompletedTask : RefreshAsync(pageNumber);
    protected void ClearError() => ErrorMessage = null;
    protected void OpenEntityDialog(TEntity? entity = default) { EditingEntity = entity; IsEntityDialogOpen = true; }
    protected void CloseEntityDialog() { EditingEntity = default; IsEntityDialogOpen = false; }
    protected void OpenEntityPage(string route) => NavigationManager.NavigateTo(route);
    protected void RequestDelete(TEntity entity) { if (Can(PermissionAction.CanDelete)) { PendingDelete = entity; IsDeleteConfirmationOpen = true; } }
    protected void CancelDelete() { PendingDelete = default; IsDeleteConfirmationOpen = false; }
    protected async Task ConfirmDeleteAsync()
    {
        if (PendingDelete is null || !Can(PermissionAction.CanDelete)) { CancelDelete(); return; }
        IsLoading = true; ErrorMessage = null;
        try { await DeleteEntityAsync(PendingDelete); CancelDelete(); await RefreshAsync(PageResult?.PageNumber ?? 1); }
        catch (Exception exception) { ErrorMessage = exception.Message; IsDeleteConfirmationOpen = false; }
        finally { IsLoading = false; }
    }
}
