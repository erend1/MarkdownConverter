using MarkdownConverter.WebApp.Core.Models;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.ViewModels;
using MarkdownConverter.WebApp.Core.Views;
using MarkdownConverter.Core.Interfaces;

namespace MarkdownConverter.WebApp.Core.Presenters;

public sealed class ExportPresenter : IExportPresenter
{
    private readonly IConversionService _conversionService;
    private readonly IFileDownloadService _downloadService;
    private readonly IFileSystem _fileSystem;
    private readonly IToastService? _toastService;
    private IExportView? _view;

    public ExportViewModel ViewModel { get; } = new()
    {
        AvailableFormats = ExportOption.All,
        SelectedFormat = ExportOption.Word
    };

    public ExportPresenter(
        IConversionService conversionService,
        IFileDownloadService downloadService,
        IFileSystem fileSystem,
        IToastService? toastService = null)
    {
        _conversionService = conversionService;
        _downloadService = downloadService;
        _fileSystem = fileSystem;
        _toastService = toastService;
    }

    public void Attach(IExportView view) => _view = view;

    public void SelectFormat(ExportOption format)
    {
        ViewModel.SelectedFormat = format;
        _view?.RequestRender();
    }

    public async Task ExportAsync(string markdown, string baseFileName, string? bibVirtualPath = null)
    {
        if (ViewModel.SelectedFormat is null) return;

        var format = ViewModel.SelectedFormat;
        ViewModel.IsExporting = true;
        ViewModel.LastExportMessage = null;
        _view?.RequestRender();

        try
        {
            var outputFileName = Path.GetFileNameWithoutExtension(baseFileName) + format.FileExtension;

            var options = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(bibVirtualPath))
                options["bibliography"] = bibVirtualPath;

            var result = await _conversionService.ConvertAsync(
                markdown, format.Format, outputFileName, options);

            if (result.Success)
            {
                var bytes = await _fileSystem.ReadAllBytesAsync(outputFileName);
                var download = await _downloadService.DownloadAsync(
                    outputFileName, bytes, format.MimeType);

                switch (download.Outcome)
                {
                    case DownloadOutcome.Saved:
                        ViewModel.LastExportSuccess = true;
                        ViewModel.LastExportMessage = $"Exported {outputFileName}";
                        _toastService?.ShowSuccess(
                            $"Exported {outputFileName} ({result.BytesWritten:N0} bytes)");
                        break;

                    case DownloadOutcome.Cancelled:
                        ViewModel.LastExportSuccess = false;
                        ViewModel.LastExportMessage = "Export cancelled";
                        // No toast — cancellation is a normal user choice, not a failure.
                        break;

                    case DownloadOutcome.FellBackToDownload:
                        ViewModel.LastExportSuccess = true;
                        ViewModel.LastExportMessage = $"Exported {outputFileName}";
                        _toastService?.ShowInfo(
                            $"Saved {outputFileName} to Downloads (your browser doesn't support custom save locations).");
                        break;

                    case DownloadOutcome.Error:
                        ViewModel.LastExportSuccess = false;
                        ViewModel.LastExportMessage = $"Save failed: {download.ErrorMessage}";
                        _toastService?.ShowError($"Save failed: {download.ErrorMessage}");
                        break;
                }
            }
            else
            {
                ViewModel.LastExportSuccess = false;
                ViewModel.LastExportMessage = $"Failed: {result.ErrorMessage}";
                _toastService?.ShowError($"Export failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            ViewModel.LastExportSuccess = false;
            ViewModel.LastExportMessage = $"Error: {ex.Message}";
            _toastService?.ShowError($"Export error: {ex.Message}");
        }
        finally
        {
            ViewModel.IsExporting = false;
            _view?.RequestRender();
        }
    }
}
