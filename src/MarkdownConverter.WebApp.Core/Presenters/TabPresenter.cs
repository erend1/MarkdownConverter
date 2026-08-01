using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class TabPresenter : ITabPresenter
{
    private readonly IPreviewPresenter _previewPresenter;
    private readonly IDebouncer _debouncer;
    private readonly ILocalStorageService? _storage;
    private readonly List<TabViewModel> _tabs = new();
    private ITabView? _view;
    private IEditorView? _editorView;
    private int _tabCounter;

    public IReadOnlyList<TabViewModel> Tabs => _tabs;
    public TabViewModel ActiveTab => _tabs[ActiveIndex];
    public int ActiveIndex { get; private set; }
    public int MaxTabs => 10;
    public bool CanAddTab => _tabs.Count < MaxTabs;

    public event Action<string>? AutoSaveFailed;

    public TabPresenter(IPreviewPresenter previewPresenter, IDebouncer debouncer,
        ILocalStorageService? storage = null)
    {
        _previewPresenter = previewPresenter;
        _debouncer = debouncer;
        _storage = storage;
        // Start with one empty tab
        _tabs.Add(CreateUntitledTab());
    }

    private TabViewModel CreateUntitledTab()
    {
        _tabCounter++;
        return new TabViewModel { FileName = $"Untitled {_tabCounter}.md" };
    }

    public void Attach(ITabView view) => _view = view;
    public void AttachEditor(IEditorView editorView) => _editorView = editorView;

    public void NewTab()
    {
        if (!CanAddTab) return;
        _tabs.Add(CreateUntitledTab());
        ActiveIndex = _tabs.Count - 1;
        _ = _previewPresenter.RenderPreviewAsync(ActiveTab.MarkdownText);
        NotifyViews();
        _view?.FocusEditor();
        _ = AutoSaveAsync();
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == ActiveIndex) return;
        ActiveIndex = index;
        _ = _previewPresenter.RenderPreviewAsync(ActiveTab.MarkdownText);
        NotifyViews();
        _view?.FocusEditor();
    }

    public void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (_tabs.Count == 1)
        {
            _tabs[0] = CreateUntitledTab();
            ActiveIndex = 0;
            _ = _previewPresenter.RenderPreviewAsync(string.Empty);
            NotifyViews();
            _ = AutoSaveAsync();
            return;
        }

        _tabs.RemoveAt(index);
        if (ActiveIndex >= _tabs.Count)
            ActiveIndex = _tabs.Count - 1;
        else if (index < ActiveIndex)
            ActiveIndex--;

        _ = _previewPresenter.RenderPreviewAsync(ActiveTab.MarkdownText);
        NotifyViews();
        _ = AutoSaveAsync();
    }

    public async Task OpenFileInNewTabAsync(string fileName, string content)
    {
        if (CanAddTab)
        {
            _tabs.Add(new TabViewModel { FileName = fileName, MarkdownText = content });
            ActiveIndex = _tabs.Count - 1;
        }
        else
        {
            ActiveTab.FileName = fileName;
            ActiveTab.MarkdownText = content;
            ActiveTab.IsDirty = false;
        }

        await _previewPresenter.RenderPreviewAsync(content);
        NotifyViews();
        _view?.FocusEditor();
        await AutoSaveAsync();
    }

    public void OnActiveTabTextChanged(string newText)
    {
        ActiveTab.MarkdownText = newText;
        ActiveTab.IsDirty = true;
        _debouncer.Debounce(300, async () =>
        {
            await _previewPresenter.RenderPreviewAsync(newText);
            await AutoSaveAsync();
        });
        NotifyViews();
    }

    public void SetActiveTabScrollRatio(double ratio)
    {
        ActiveTab.ScrollRatio = Math.Clamp(ratio, 0, 1);
    }

    public string GetActiveTabContent() => ActiveTab.MarkdownText;

    public void RenameTab(int index, string newName)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var trimmed = newName.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;
        // Ensure .md extension
        if (!trimmed.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            trimmed += ".md";
        _tabs[index].FileName = trimmed;
        NotifyViews();
        _ = AutoSaveAsync();
    }

    public void MoveTab(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _tabs.Count) return;
        if (toIndex < 0 || toIndex >= _tabs.Count) return;
        if (fromIndex == toIndex) return;

        var tab = _tabs[fromIndex];
        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, tab);

        // Adjust active index to follow the active tab
        if (ActiveIndex == fromIndex)
            ActiveIndex = toIndex;
        else if (fromIndex < ActiveIndex && toIndex >= ActiveIndex)
            ActiveIndex--;
        else if (fromIndex > ActiveIndex && toIndex <= ActiveIndex)
            ActiveIndex++;

        NotifyViews();
        _ = AutoSaveAsync();
    }

    public async Task<SaveResult> SaveStateAsync()
    {
        if (_storage == null) return SaveResult.Ok();

        var states = SnapshotTabStates();
        var result = await _storage.SaveTabsAsync(states, ActiveIndex);

        if (!result.Success) return result;

        // Only clear dirty flags after the write actually succeeded.
        foreach (var tab in _tabs)
            tab.IsDirty = false;
        NotifyViews();
        return result;
    }

    public async Task<LoadStatus> RestoreStateAsync()
    {
        if (_storage == null) return LoadStatus.Empty;

        var result = await _storage.LoadTabsAsync();
        if (result.Status != LoadStatus.Loaded) return result.Status;

        _tabs.Clear();
        _tabCounter = 0;

        foreach (var state in result.Tabs)
        {
            _tabCounter++;
            _tabs.Add(new TabViewModel
            {
                FileName = state.FileName,
                MarkdownText = state.Content,
                IsDirty = false
            });
        }

        if (_tabs.Count == 0)
            _tabs.Add(CreateUntitledTab());

        ActiveIndex = Math.Clamp(result.ActiveIndex, 0, _tabs.Count - 1);
        await _previewPresenter.RenderPreviewAsync(ActiveTab.MarkdownText);
        NotifyViews();
        return LoadStatus.Loaded;
    }

    private async Task AutoSaveAsync()
    {
        if (_storage == null) return;

        try
        {
            var states = SnapshotTabStates();
            var result = await _storage.SaveTabsAsync(states, ActiveIndex);
            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                AutoSaveFailed?.Invoke(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            AutoSaveFailed?.Invoke(ex.Message);
        }
    }

    private List<TabState> SnapshotTabStates() =>
        _tabs.Select(t => new TabState
        {
            FileName = t.FileName,
            Content = t.MarkdownText
        }).ToList();

    private void NotifyViews()
    {
        _view?.RequestRender();
        _editorView?.RequestRender();
    }
}
