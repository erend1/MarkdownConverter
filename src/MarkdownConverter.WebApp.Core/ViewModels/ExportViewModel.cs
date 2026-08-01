using MarkdownConverter.WebApp.Core.Models;

namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class ExportViewModel
{
    public IReadOnlyList<ExportOption> AvailableFormats { get; set; } = [];
    public ExportOption? SelectedFormat { get; set; }
    public bool IsExporting { get; set; }
    public string? LastExportMessage { get; set; }
    public bool LastExportSuccess { get; set; }
}
