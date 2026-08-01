using MarkdownConverter.Core.Enums;

namespace MarkdownConverter.WebApp.Core.Models;

public sealed record ExportOption(
    ExportFormat Format,
    string DisplayName,
    string FileExtension,
    string MimeType)
{
    public static readonly ExportOption Word = new(
        ExportFormat.Word, "Word (.docx)", ".docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

    public static readonly ExportOption Latex = new(
        ExportFormat.Latex, "LaTeX (.tex)", ".tex",
        "application/x-tex");

    public static readonly IReadOnlyList<ExportOption> All = [Word, Latex];
}
