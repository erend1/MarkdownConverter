using MarkdownConverter.WebApp.Core.Models;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public interface IExportPresenter
{
    ExportViewModel ViewModel { get; }
    void Attach(IExportView view);
    void SelectFormat(ExportOption format);
    Task ExportAsync(string markdown, string baseFileName, string? bibVirtualPath = null);
}
