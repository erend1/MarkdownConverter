using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Models;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;

namespace MarkdownConverter.WebApp.Components;

public partial class ExportPanel : IExportView
{
    [Inject] private IExportPresenter ExportPresenter { get; set; } = default!;
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IBibliographyPresenter BibPresenter { get; set; } = default!;
    [Inject] private IToastService ToastService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _isDesktop;
    private bool _isCompiling;

    protected override void OnInitialized()
    {
        ExportPresenter.Attach(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _isDesktop = await JS.InvokeAsync<bool>("fileInterop.isDesktopMode");
                StateHasChanged();
            }
            catch { /* Not in desktop mode */ }
        }
    }

    private void OnFormatSelected(ExportOption format)
        => ExportPresenter.SelectFormat(format);

    private async Task OnExport()
    {
        await ExportPresenter.ExportAsync(
            TabPresenter.ActiveTab.MarkdownText,
            TabPresenter.ActiveTab.FileName,
            BibPresenter.ViewModel.VirtualPath);
    }

    private async Task OnPrintPdf()
    {
        await JS.InvokeVoidAsync("fileInterop.printPreviewAsPdf", "preview-content");
    }

    private async Task OnCompilePdf()
    {
        _isCompiling = true;
        StateHasChanged();

        try
        {
            var result = await JS.InvokeAsync<PdfCompileResult>(
                "fileInterop.compilePdf", TabPresenter.ActiveTab.MarkdownText);

            if (result.Success)
            {
                ToastService.ShowSuccess("PDF compiled and opened");
            }
            else
            {
                // Long pdflatex logs go into Details so the user can read
                // them in the modal instead of fighting the toast timer.
                ToastService.ShowError("PDF compilation failed", result.Error);
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError("PDF compilation error", ex.ToString());
        }
        finally
        {
            _isCompiling = false;
            StateHasChanged();
        }
    }

    public void RequestRender() => InvokeAsync(StateHasChanged);
}
