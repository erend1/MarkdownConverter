using MarkdownConverter.WebApp.Core.Models;
using MarkdownConverter.WebApp.Core.Presenters;
using MarkdownConverter.WebApp.Core.Services;
using MarkdownConverter.WebApp.Core.Views;
using MarkdownConverter.Core.Enums;
using MarkdownConverter.Core.Interfaces;
using MarkdownConverter.Core.Models;
using Moq;

namespace MarkdownConverter.WebApp.Core.Tests;

public class ExportPresenterTests
{
    private readonly Mock<IConversionService> _conversionMock;
    private readonly Mock<IFileDownloadService> _downloadMock;
    private readonly Mock<IFileSystem> _fsMock;
    private readonly Mock<IToastService> _toastMock;
    private readonly Mock<IExportView> _viewMock;
    private readonly ExportPresenter _sut;

    public ExportPresenterTests()
    {
        _conversionMock = new Mock<IConversionService>();
        _downloadMock = new Mock<IFileDownloadService>();
        _fsMock = new Mock<IFileSystem>();
        _toastMock = new Mock<IToastService>();
        _viewMock = new Mock<IExportView>();

        // Default: Save dialog confirmed by user.
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.Saved());

        _sut = new ExportPresenter(
            _conversionMock.Object, _downloadMock.Object, _fsMock.Object, _toastMock.Object);
        _sut.Attach(_viewMock.Object);
    }

    [Fact]
    public void AvailableFormats_ContainsWordAndLatex()
    {
        Assert.Equal(2, _sut.ViewModel.AvailableFormats.Count);
        Assert.Contains(_sut.ViewModel.AvailableFormats, f => f.Format == ExportFormat.Word);
        Assert.Contains(_sut.ViewModel.AvailableFormats, f => f.Format == ExportFormat.Latex);
    }

    [Fact]
    public void SelectFormat_UpdatesSelectedFormat()
    {
        _sut.SelectFormat(ExportOption.Latex);

        Assert.Equal(ExportOption.Latex, _sut.ViewModel.SelectedFormat);
    }

    [Fact]
    public async Task ExportAsync_SuccessfulExport_DownloadsFile()
    {
        _conversionMock
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), ExportFormat.Word, "test.docx", It.IsAny<IDictionary<string, string>?>(), default))
            .ReturnsAsync(ConversionResult.Ok("test.docx", 5000));
        _fsMock
            .Setup(f => f.ReadAllBytesAsync("test.docx", default))
            .ReturnsAsync(new byte[5000]);

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# Hello", "test.md");

        _downloadMock.Verify(d => d.DownloadAsync("test.docx", It.IsAny<byte[]>(), ExportOption.Word.MimeType), Times.Once);
        Assert.True(_sut.ViewModel.LastExportSuccess);
    }

    [Fact]
    public async Task ExportAsync_FailedConversion_ShowsError()
    {
        _conversionMock
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<ExportFormat>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>?>(), default))
            .ReturnsAsync(ConversionResult.Fail("test.docx", "Something went wrong"));

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# Hello", "test.md");

        Assert.False(_sut.ViewModel.LastExportSuccess);
        Assert.Contains("Something went wrong", _sut.ViewModel.LastExportMessage);
    }

    [Fact]
    public async Task ExportAsync_SetsIsExportingDuringExport()
    {
        var tcs = new TaskCompletionSource<ConversionResult>();
        _conversionMock
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<ExportFormat>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>?>(), default))
            .Returns(tcs.Task);

        _sut.SelectFormat(ExportOption.Word);
        var exportTask = _sut.ExportAsync("test", "test.md");

        // During export, IsExporting should be true
        Assert.True(_sut.ViewModel.IsExporting);

        tcs.SetResult(ConversionResult.Fail("test.docx", "error"));
        await exportTask;

        Assert.False(_sut.ViewModel.IsExporting);
    }

    [Fact]
    public async Task ExportAsync_WithBibPath_PassesInOptions()
    {
        IDictionary<string, string>? capturedOptions = null;
        _conversionMock
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<ExportFormat>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>?>(), default))
            .Callback<string, ExportFormat, string, IDictionary<string, string>?, CancellationToken>((_, _, _, opts, _) => capturedOptions = opts)
            .ReturnsAsync(ConversionResult.Ok("test.tex", 1000));
        _fsMock.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), default)).ReturnsAsync(new byte[100]);

        _sut.SelectFormat(ExportOption.Latex);
        await _sut.ExportAsync("test", "test.md", "bibliography.bib");

        Assert.NotNull(capturedOptions);
        Assert.Equal("bibliography.bib", capturedOptions!["bibliography"]);
    }

    // -------- Save-dialog regression tests --------

    private void SetupSuccessfulConversion(string outputFile = "test.docx", int bytes = 1000)
    {
        _conversionMock
            .Setup(c => c.ConvertAsync(It.IsAny<string>(), It.IsAny<ExportFormat>(), outputFile, It.IsAny<IDictionary<string, string>?>(), default))
            .ReturnsAsync(ConversionResult.Ok(outputFile, bytes));
        _fsMock
            .Setup(f => f.ReadAllBytesAsync(outputFile, default))
            .ReturnsAsync(new byte[bytes]);
    }

    [Fact]
    public async Task ExportAsync_SaveDialogConfirmed_ShowsSuccessToast()
    {
        SetupSuccessfulConversion();
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.Saved());

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# hi", "test.md");

        Assert.True(_sut.ViewModel.LastExportSuccess);
        _toastMock.Verify(t => t.ShowSuccess(It.Is<string>(s => s.Contains("Exported"))), Times.Once);
        _toastMock.Verify(t => t.ShowInfo(It.IsAny<string>()), Times.Never);
        _toastMock.Verify(t => t.ShowError(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ExportAsync_SaveDialogCancelled_NoToastNoSuccess()
    {
        // Regression: if the user dismisses the OS save dialog we must NOT
        // claim "Exported successfully" — that was the old auto-download behaviour
        // dressed up with a toast.
        SetupSuccessfulConversion();
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.Cancelled());

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# hi", "test.md");

        Assert.False(_sut.ViewModel.LastExportSuccess);
        Assert.Equal("Export cancelled", _sut.ViewModel.LastExportMessage);
        _toastMock.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
        _toastMock.Verify(t => t.ShowError(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _toastMock.Verify(t => t.ShowInfo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExportAsync_FallbackDownload_ShowsInfoToastExplainingWhy()
    {
        // Browsers without showSaveFilePicker (Firefox, Safari) — we still
        // wrote the file but the user didn't get a chooser. Tell them why.
        SetupSuccessfulConversion();
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.FellBackToDownload());

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# hi", "test.md");

        Assert.True(_sut.ViewModel.LastExportSuccess);
        _toastMock.Verify(
            t => t.ShowInfo(It.Is<string>(s => s.Contains("Downloads"))),
            Times.Once);
        _toastMock.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExportAsync_DownloadError_ShowsErrorToast()
    {
        SetupSuccessfulConversion();
        _downloadMock
            .Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync(DownloadResult.Error("disk full"));

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# hi", "test.md");

        Assert.False(_sut.ViewModel.LastExportSuccess);
        _toastMock.Verify(
            t => t.ShowError(It.Is<string>(s => s.Contains("disk full")), It.IsAny<string?>()),
            Times.Once);
        _toastMock.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExportAsync_PassesSuggestedFileNameToDownloadService()
    {
        // The save dialog needs the original filename so the user sees a
        // sensible default — and so re-saves overwrite cleanly.
        SetupSuccessfulConversion(outputFile: "report.docx");

        _sut.SelectFormat(ExportOption.Word);
        await _sut.ExportAsync("# hi", "report.md");

        _downloadMock.Verify(
            d => d.DownloadAsync("report.docx", It.IsAny<byte[]>(), ExportOption.Word.MimeType),
            Times.Once);
    }
}
