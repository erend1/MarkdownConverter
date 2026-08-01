using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class EditorPresenter : IEditorPresenter
{
    private readonly ITabPresenter _tabPresenter;

    public EditorViewModel ViewModel { get; } = new();

    public EditorPresenter(ITabPresenter tabPresenter)
    {
        _tabPresenter = tabPresenter;
    }

    public void Attach(IEditorView view) { /* Rendering handled by TabView */ }

    public void OnTextChanged(string newText)
    {
        _tabPresenter.OnActiveTabTextChanged(newText);
        SyncViewModel();
    }

    public Task OnFileOpenAsync(string fileName, string content)
    {
        var task = _tabPresenter.OpenFileInNewTabAsync(fileName, content);
        SyncViewModel();
        return task;
    }

    public void OnNewFile()
    {
        _tabPresenter.NewTab();
        SyncViewModel();
    }

    public string GetDownloadContent() => _tabPresenter.GetActiveTabContent();

    /// <summary>
    /// Syncs the EditorViewModel with the active tab state for backwards compatibility.
    /// </summary>
    private void SyncViewModel()
    {
        var tab = _tabPresenter.ActiveTab;
        ViewModel.MarkdownText = tab.MarkdownText;
        ViewModel.FileName = tab.FileName;
        ViewModel.IsDirty = tab.IsDirty;
    }
}
