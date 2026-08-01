using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Components;

public partial class Toolbar
{
    [Inject] private ITabPresenter TabPresenter { get; set; } = default!;
    [Inject] private IFileDownloadService DownloadService { get; set; } = default!;
    [Inject] private IFileUploadService FileUploadService { get; set; } = default!;
    [Inject] private IToastService ToastService { get; set; } = default!;

    private void OnNew() => TabPresenter.NewTab();

    private async Task OnFileOpen(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null) return;

        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        var (name, content) = await FileUploadService.ReadTextFileAsync(stream, file.Name);
        await TabPresenter.OpenFileInNewTabAsync(name, content);
        ToastService.ShowInfo($"Opened {name}");
    }

    private async Task OnSave()
    {
        var result = await TabPresenter.SaveStateAsync();
        if (result.Success)
            ToastService.ShowSuccess("Saved to session");
        else
            ToastService.ShowError($"Save failed: {result.ErrorMessage}");
    }

    private async Task OnSaveAs()
    {
        var fileName = TabPresenter.ActiveTab.FileName;
        var content = TabPresenter.GetActiveTabContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var result = await DownloadService.DownloadAsync(fileName, bytes, "text/markdown");

        switch (result.Outcome)
        {
            case DownloadOutcome.Saved:
                ToastService.ShowSuccess($"Saved {fileName}");
                break;
            case DownloadOutcome.Cancelled:
                // No toast — cancellation is a normal user choice.
                break;
            case DownloadOutcome.FellBackToDownload:
                ToastService.ShowInfo(
                    $"Saved {fileName} to Downloads (your browser doesn't support custom save locations).");
                break;
            case DownloadOutcome.Error:
                ToastService.ShowError($"Save failed: {result.ErrorMessage}");
                break;
        }
    }
}
