using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public interface IBibliographyPresenter
{
    BibliographyViewModel ViewModel { get; }
    void Attach(IBibliographyView view);
    Task OnBibFileUploadedAsync(string fileName, string content);
    void ClearBibliography();
}
