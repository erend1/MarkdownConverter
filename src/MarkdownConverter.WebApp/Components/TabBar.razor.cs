using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Components;

public partial class TabBar : ITabView
{
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IEditorBridge EditorBridge { get; set; } = default!;

    private int _renamingIndex = -1;
    private string _renameText = string.Empty;
    private int _dragSourceIndex = -1;
    private int _dragOverIndex = -1;

    protected override void OnInitialized()
    {
        TabPresenter.Attach(this);
    }

    public void RequestRender() => InvokeAsync(StateHasChanged);

    public void FocusEditor()
    {
        _ = EditorBridge.FocusAsync(".editor-textarea");
    }

    private async Task CaptureActiveScrollAsync()
    {
        var ratio = await EditorBridge.GetScrollRatioAsync(".preview-content");
        TabPresenter.SetActiveTabScrollRatio(ratio);
    }

    private async Task NewTabAsync()
    {
        await CaptureActiveScrollAsync();
        TabPresenter.NewTab();
    }

    private async Task SwitchToAsync(int index)
    {
        await CaptureActiveScrollAsync();
        TabPresenter.SwitchTo(index);
    }

    private async Task CloseTabAsync(int index)
    {
        await CaptureActiveScrollAsync();
        TabPresenter.CloseTab(index);
    }

    private void StartRename(int index)
    {
        _renamingIndex = index;
        // Strip .md extension for editing
        var name = TabPresenter.Tabs[index].FileName;
        _renameText = name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? name[..^3] : name;
    }

    private void FinishRename(int index)
    {
        if (_renamingIndex < 0) return;
        TabPresenter.RenameTab(index, _renameText);
        _renamingIndex = -1;
    }

    private void OnRenameKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key == "Enter")
            FinishRename(index);
        else if (e.Key == "Escape")
            _renamingIndex = -1;
    }

    private void OnDragStart(int index) => _dragSourceIndex = index;

    private void OnDragOver(int index) => _dragOverIndex = index;

    private void OnDragLeave() => _dragOverIndex = -1;

    private void OnDrop(int targetIndex)
    {
        if (_dragSourceIndex >= 0 && _dragSourceIndex != targetIndex)
            TabPresenter.MoveTab(_dragSourceIndex, targetIndex);
        _dragSourceIndex = -1;
        _dragOverIndex = -1;
    }

    private void OnDragEnd()
    {
        _dragSourceIndex = -1;
        _dragOverIndex = -1;
    }
}
