using OneZeroErp.Application;

namespace OneZeroErp.SharedUi;

public sealed class PermissionPagedPageContext<TItem>
{
    private readonly Func<PermissionAction, bool> can;
    private readonly Action closeEditor;
    private readonly Action<string?> setError;
    private readonly Func<Task> refresh;
    private readonly Func<Guid> currentUserId;
    private readonly Func<Func<Task<PageActionResult>>, Task<PageActionResult>> execute;

    internal PermissionPagedPageContext(TItem? editingItem, Func<PermissionAction, bool> can, Action closeEditor, Action<string?> setError, Func<Task> refresh, Func<Guid> currentUserId, Func<Func<Task<PageActionResult>>, Task<PageActionResult>> execute)
    {
        EditingItem = editingItem;
        this.can = can;
        this.closeEditor = closeEditor;
        this.setError = setError;
        this.refresh = refresh;
        this.currentUserId = currentUserId;
        this.execute = execute;
    }

    public TItem? EditingItem { get; }
    public Guid CurrentUserId => currentUserId();
    public bool Can(PermissionAction action) => can(action);
    public void CloseEditor() => closeEditor();
    public void SetError(string? message) => setError(message);
    public Task RefreshAsync() => refresh();
    public Task<PageActionResult> SubmitAsync(Func<Task<PageActionResult>> action) => execute(action);
}
