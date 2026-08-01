namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class BibliographyViewModel
{
    public string? BibFileName { get; set; }
    public int EntryCount { get; set; }
    public bool IsLoaded { get; set; }
    public string? VirtualPath { get; set; }
}
