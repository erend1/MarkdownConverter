using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public interface IEditorPresenter
{
    EditorViewModel ViewModel { get; }
    void Attach(IEditorView view);
    void OnTextChanged(string newText);
    Task OnFileOpenAsync(string fileName, string content);
    void OnNewFile();
    string GetDownloadContent();
}
