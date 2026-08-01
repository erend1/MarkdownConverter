using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public interface IPreviewPresenter
{
    PreviewViewModel ViewModel { get; }
    void Attach(IPreviewView view);
    Task RenderPreviewAsync(string rawMarkdown);
}
