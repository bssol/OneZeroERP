using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OneZeroErp.Application;
using OneZeroErp.Application.Extensions;

namespace OneZeroErp.SharedUi;

public abstract class PermissionPagedPageBase<TEntity> : ComponentBase
{
    [Inject] protected AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] protected IPermissionService PermissionService { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    protected PermissionSet Permissions { get; private set; } = PermissionSet.Empty;
    protected ClaimsPrincipal CurrentUser { get; private set; } = new();
    protected Guid CurrentUserId => Guid.TryParse(CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;
    protected PagedResult<TEntity>? PageResult { get; private set; }
    protected IReadOnlyList<TEntity> Items => PageResult?.Items ?? Array.Empty<TEntity>();
    protected bool IsLoading { get; set; }
    protected string? ErrorMessage { get; private set; }
    protected string? SuccessMessage { get; private set; }
    protected bool IsUnauthorized => !Permissions.Allows(PermissionAction.CanView);
    protected bool IsDeleteConfirmationOpen { get; private set; }
    protected TEntity? PendingDelete { get; private set; }
    protected bool IsEntityDialogOpen { get; private set; }
    protected TEntity? EditingEntity { get; private set; }

    protected abstract string PermissionResource { get; }
    protected virtual int PageSize => 25;
    protected abstract Task<PagedResult<TEntity>> LoadEntitiesAsync(PagedQuery query, CancellationToken cancellationToken = default);
    protected virtual Task<PageActionResult> DeleteEntityAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        Task.FromResult(PageActionResult.Failure("This entity page has not enabled delete operations."));
    protected virtual string DeleteConfirmationMessage => "Are you sure you want to delete this item? This action cannot be undone.";

    protected override async Task OnInitializedAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        CurrentUser = authenticationState.User;
        Permissions = await PermissionService.GetPermissionsAsync(authenticationState.User, PermissionResource);
        if (Permissions.Allows(PermissionAction.CanView)) await RefreshAsync();
    }

    protected bool Can(PermissionAction action) => Permissions.Allows(action);

    protected async Task RefreshAsync(int pageNumber = 1)
    {
        if (!Can(PermissionAction.CanView)) return;
        IsLoading = true;
        ClearMessages();
        try { PageResult = await LoadEntitiesAsync(new PagedQuery(pageNumber, PageSize)); }
        catch (Exception exception) { ErrorMessage = exception.GetAllMessages(); }
        finally { IsLoading = false; }
    }

    protected Task GoToPageAsync(int pageNumber) => PageResult is null || pageNumber < 1 || pageNumber > PageResult.TotalPages ? Task.CompletedTask : RefreshAsync(pageNumber);
    protected void ClearError() => ErrorMessage = null;
    protected void ClearMessages() { ErrorMessage = null; SuccessMessage = null; }
    protected void SetError(string? message) { ErrorMessage = message; SuccessMessage = null; }
    protected void SetActionResult(PageActionResult result) { if (result.Succeeded) { ErrorMessage = null; SuccessMessage = result.SuccessMessage; } else SetError(result.ErrorMessage); }

    protected async Task<PageActionResult> ExecutePageActionAsync(Func<Task<PageActionResult>> action, bool closeDialogOnSuccess = false)
    {
        IsLoading = true;
        ClearMessages();
        try
        {
            var result = await action();
            SetActionResult(result);
            if (result.Succeeded && closeDialogOnSuccess)
            {
                CloseEntityDialog();
                await RefreshAsync(PageResult?.PageNumber ?? 1);
            }
            return result;
        }
        catch (Exception exception)
        {
            var result = PageActionResult.Failure(exception.GetAllMessages());
            SetActionResult(result);
            return result;
        }
        finally { IsLoading = false; }
    }

    protected void OpenEntityDialog(TEntity? entity = default) { EditingEntity = entity; IsEntityDialogOpen = true; }
    protected void CloseEntityDialog() { EditingEntity = default; IsEntityDialogOpen = false; }
    protected void OpenEntityPage(string route) => NavigationManager.NavigateTo(route);

    protected void RequestDelete(TEntity entity)
    {
        if (Can(PermissionAction.CanDelete))
        {
            PendingDelete = entity;
            IsDeleteConfirmationOpen = true;
        }
    }

    protected void CancelDelete() { PendingDelete = default; IsDeleteConfirmationOpen = false; }

    protected async Task ConfirmDeleteAsync()
    {
        if (PendingDelete is null || !Can(PermissionAction.CanDelete)) { CancelDelete(); return; }
        IsLoading = true;
        ClearMessages();
        try
        {
            var result = await DeleteEntityAsync(PendingDelete);
            SetActionResult(result);
            if (result.Succeeded)
            {
                CancelDelete();
                await RefreshAsync(PageResult?.PageNumber ?? 1);
            }
            else IsDeleteConfirmationOpen = false;
        }
        catch (Exception exception)
        {
            SetError(exception.GetAllMessages());
            IsDeleteConfirmationOpen = false;
        }
        finally { IsLoading = false; }
    }
}