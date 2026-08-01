using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public interface ITabPresenter
{
    IReadOnlyList<TabViewModel> Tabs { get; }
    TabViewModel ActiveTab { get; }
    int ActiveIndex { get; }
    int MaxTabs { get; }
    bool CanAddTab { get; }

    /// <summary>
    /// Raised when a background autosave fails. UI layers can subscribe
    /// to surface this as a toast so silent data loss stops happening.
    /// </summary>
    event Action<string>? AutoSaveFailed;

    void Attach(ITabView view);
    void AttachEditor(IEditorView editorView);
    void NewTab();
    void SwitchTo(int index);
    void CloseTab(int index);
    Task OpenFileInNewTabAsync(string fileName, string content);
    void OnActiveTabTextChanged(string newText);
    void SetActiveTabScrollRatio(double ratio);
    string GetActiveTabContent();
    void RenameTab(int index, string newName);
    void MoveTab(int fromIndex, int toIndex);
    Task<SaveResult> SaveStateAsync();
    Task<LoadStatus> RestoreStateAsync();
}
