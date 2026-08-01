using Microsoft.AspNetCore.Components;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;
using MarkdownConverter.WebApp.Interop;

namespace MarkdownConverter.WebApp.Components;

public partial class HtmlPreview : IPreviewView
{
    [Inject] private IPreviewPresenter Presenter { get; set; } = default!;
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IEditorBridge EditorBridge { get; set; } = default!;
    [Inject] private KaTeXInterop KaTeX { get; set; } = default!;

    private string _lastHtml = string.Empty;
    private string? _lastRestoredTabId;

    protected override void OnInitialized()
    {
        Presenter.Attach(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Presenter.ViewModel.HtmlContent != _lastHtml)
        {
            _lastHtml = Presenter.ViewModel.HtmlContent;
            await KaTeX.RenderMathAsync("preview-content");
        }

        var activeTab = TabPresenter.ActiveTab;
        if (firstRender || _lastRestoredTabId != activeTab.Id)
        {
            _lastRestoredTabId = activeTab.Id;
            await EditorBridge.SetScrollRatioAsync(".preview-content", activeTab.ScrollRatio);
        }
    }

    public void RequestRender() => InvokeAsync(StateHasChanged);

    public async Task RenderMathAsync()
    {
        await KaTeX.RenderMathAsync("preview-content");
    }
}
