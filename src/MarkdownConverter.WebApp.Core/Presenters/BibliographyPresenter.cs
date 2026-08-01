using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;
using MarkdownConverter.Core.Interfaces;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class BibliographyPresenter : IBibliographyPresenter
{
    private readonly IFileSystem _fileSystem;
    private IBibliographyView? _view;

    private const string VirtualBibPath = "bibliography.bib";

    public BibliographyViewModel ViewModel { get; } = new();

    public BibliographyPresenter(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void Attach(IBibliographyView view) => _view = view;

    public async Task OnBibFileUploadedAsync(string fileName, string content)
    {
        // Store in virtual file system for converter access
        await _fileSystem.WriteAllTextAsync(VirtualBibPath, content);

        // Count entries (simple heuristic: count lines starting with @)
        var entryCount = content
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith('@')
                && !line.TrimStart().StartsWith("@comment", StringComparison.OrdinalIgnoreCase)
                && !line.TrimStart().StartsWith("@preamble", StringComparison.OrdinalIgnoreCase));

        ViewModel.BibFileName = fileName;
        ViewModel.EntryCount = entryCount;
        ViewModel.IsLoaded = true;
        ViewModel.VirtualPath = VirtualBibPath;

        _view?.RequestRender();
    }

    public void ClearBibliography()
    {
        ViewModel.BibFileName = null;
        ViewModel.EntryCount = 0;
        ViewModel.IsLoaded = false;
        ViewModel.VirtualPath = null;

        _view?.RequestRender();
    }
}
