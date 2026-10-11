namespace MarkdownConverter.WebApp.Core.ViewModels;

public sealed class TabViewModel
{
    private string _markdownText = string.Empty;
    private bool _statisticsCurrent;
    private int _lineCount;
    private int _wordCount;

    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; set; } = "Untitled.md";

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            _markdownText = value;
            _statisticsCurrent = false;
        }
    }

    public bool IsDirty { get; set; }
    public double ScrollRatio { get; set; }

    // The statistics are read several times by every editor render, and the
    // editor re-renders for reasons unrelated to the text (find navigation,
    // highlight repaint). They are therefore counted once per text change, in
    // a single pass that allocates nothing, rather than on every read.
    public int LineCount
    {
        get
        {
            EnsureStatistics();
            return _lineCount;
        }
    }

    public int WordCount
    {
        get
        {
            EnsureStatistics();
            return _wordCount;
        }
    }

    public int CharCount => MarkdownText.Length;

    public string DisplayName => IsDirty ? $"{FileName} *" : FileName;

    private void EnsureStatistics()
    {
        if (_statisticsCurrent) return;

        // Same results as Split('\n').Length and
        // Split(null, RemoveEmptyEntries).Length: one more line than there
        // are line feeds, and one word per run of non-white-space characters.
        var lines = 1;
        var words = 0;
        var inWord = false;
        foreach (var c in _markdownText ?? string.Empty)
        {
            if (c == '\n') lines++;

            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }

        _lineCount = lines;
        _wordCount = words;
        _statisticsCurrent = true;
    }
}
