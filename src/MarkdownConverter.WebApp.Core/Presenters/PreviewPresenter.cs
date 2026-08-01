using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class PreviewPresenter : IPreviewPresenter
{
    private readonly IHtmlPreviewRenderer _renderer;
    private IPreviewView? _view;

    public PreviewViewModel ViewModel { get; } = new();

    public PreviewPresenter(IHtmlPreviewRenderer renderer)
    {
        _renderer = renderer;
    }

    public void Attach(IPreviewView view) => _view = view;

    public async Task RenderPreviewAsync(string rawMarkdown)
    {
        ViewModel.IsRendering = true;
        _view?.RequestRender();

        ViewModel.HtmlContent = _renderer.RenderToHtml(rawMarkdown);
        ViewModel.IsRendering = false;
        _view?.RequestRender();

        if (_view != null)
            await _view.RenderMathAsync();
    }
}
