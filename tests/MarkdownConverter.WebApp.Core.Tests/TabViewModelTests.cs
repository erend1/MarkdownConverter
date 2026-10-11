using MarkdownConverter.WebApp.Core.ViewModels;

namespace MarkdownConverter.WebApp.Core.Tests;

public class TabViewModelTests
{
    // The statistics used to be recomputed with string.Split on every read.
    // These are the expressions they replaced; the cached single-pass counts
    // must agree with them for every input.
    private static int ReferenceLineCount(string text) =>
        string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;

    private static int ReferenceWordCount(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    // Builds a sample from text parts and explicit code points so the source
    // file carries no invisible characters.
    private static string WithCodePoints(params object[] parts) =>
        string.Concat(parts.Select(part => part is int codePoint
            ? ((char)codePoint).ToString()
            : (string)part));

    public static TheoryData<string> StatisticsSamples => new()
    {
        "",
        " ",
        "\n",
        "\n\n\n",
        "one",
        "one two three",
        "  leading and trailing  ",
        "a\nb\nc",
        "trailing newline\n",
        "\nleading newline",
        "blank\n\n\nlines between",
        "windows\r\nline\r\nendings\r\n",
        "tabs\tand\tspaces  and\nnewlines",
        // No-break space, em space, ideographic space, line separator,
        // paragraph separator and next line all separate words.
        WithCodePoints(
            "non", 0x00A0, "breaking", 0x2003, "em", 0x3000, "ideographic",
            0x2028, "line", 0x2029, "paragraph", 0x0085, "next"),
        // A zero-width space is not white space and does not split a word.
        WithCodePoints("zero", 0x200B, "width is one word"),
        "çğıöşü ÇĞİÖŞÜ 日本語 Привет",
        "emoji 😀 🇹🇷 count as words",
        "# Heading\n\n- item *em* **strong** `code`\n\n| a | b |\n|---|---|\n| 1 | 2 |\n",
        "   \t \n \r\n  ",
        "single-word-without-any-white-space"
    };

    [Theory]
    [MemberData(nameof(StatisticsSamples))]
    public void Statistics_MatchTheSplitBasedDefinition(string text)
    {
        var sut = new TabViewModel { MarkdownText = text };

        Assert.Equal(ReferenceLineCount(text), sut.LineCount);
        Assert.Equal(ReferenceWordCount(text), sut.WordCount);
        Assert.Equal(text.Length, sut.CharCount);
    }

    [Fact]
    public void Statistics_UnicodeWhiteSpace_SeparatesWords()
    {
        var separated = new TabViewModel
        {
            MarkdownText = WithCodePoints("a", 0x00A0, "b", 0x3000, "c", 0x2028, "d")
        };
        var joined = new TabViewModel { MarkdownText = WithCodePoints("a", 0x200B, "b") };

        Assert.Equal(4, separated.WordCount);
        Assert.Equal(1, separated.LineCount);
        Assert.Equal(1, joined.WordCount);
    }

    [Fact]
    public void Statistics_NewTab_ReportsOneEmptyLine()
    {
        var sut = new TabViewModel();

        Assert.Equal(1, sut.LineCount);
        Assert.Equal(0, sut.WordCount);
        Assert.Equal(0, sut.CharCount);
    }

    [Theory]
    [InlineData(100 * 1024, 1_000, 1584, 16271, 102403)]
    [InlineData(1024 * 1024, 10_000, 16158, 166051, 1048581)]
    public void Statistics_LargeDocument_MatchTheSplitBasedDefinition(
        int targetChars, int matchCount, int expectedLines, int expectedWords, int expectedChars)
    {
        var text = FindReplaceFixture.Build(targetChars, matchCount);
        var sut = new TabViewModel { MarkdownText = text };

        Assert.Equal(expectedLines, sut.LineCount);
        Assert.Equal(expectedWords, sut.WordCount);
        Assert.Equal(expectedChars, sut.CharCount);
        Assert.Equal(ReferenceLineCount(text), sut.LineCount);
        Assert.Equal(ReferenceWordCount(text), sut.WordCount);
    }

    [Fact]
    public void Statistics_FollowEveryTextChange()
    {
        var sut = new TabViewModel { MarkdownText = "one two\nthree" };
        Assert.Equal(2, sut.LineCount);
        Assert.Equal(3, sut.WordCount);

        sut.MarkdownText = "one\ntwo\nthree\nfour five";
        Assert.Equal(4, sut.LineCount);
        Assert.Equal(5, sut.WordCount);
        Assert.Equal(23, sut.CharCount);

        sut.MarkdownText = "";
        Assert.Equal(1, sut.LineCount);
        Assert.Equal(0, sut.WordCount);
        Assert.Equal(0, sut.CharCount);

        // A different string of the same length must not reuse stale counts.
        sut.MarkdownText = "ab cd";
        Assert.Equal(2, sut.WordCount);
        sut.MarkdownText = "abcde";
        Assert.Equal(1, sut.WordCount);
        sut.MarkdownText = "a\nb\nc";
        Assert.Equal(3, sut.LineCount);
        Assert.Equal(3, sut.WordCount);
    }

    [Fact]
    public void Statistics_ReadingOnlyOneOfThem_IsStillCorrect()
    {
        var lines = new TabViewModel { MarkdownText = "a b\nc" };
        var words = new TabViewModel { MarkdownText = "a b\nc" };

        Assert.Equal(2, lines.LineCount);
        Assert.Equal(3, words.WordCount);
    }

    [Fact]
    public void Statistics_RepeatedReads_DoNotRescanOrAllocate()
    {
        // Every editor render reads the statistics several times, and the
        // editor re-renders on find navigation and highlight repaint without
        // any text change. Re-splitting a document of this size on each read
        // allocated megabytes per render; a cached count allocates nothing.
        // Allocation is asserted instead of elapsed time so the test stays
        // deterministic.
        var sut = new TabViewModel { MarkdownText = FindReplaceFixture.Build(100 * 1024, 1_000) };
        var firstLines = sut.LineCount;
        var firstWords = sut.WordCount;

        var before = GC.GetAllocatedBytesForCurrentThread();
        var checksum = 0L;
        for (var i = 0; i < 200; i++)
        {
            checksum += sut.LineCount;
            checksum += sut.WordCount;
            checksum += sut.CharCount;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(200L * (firstLines + firstWords + sut.CharCount), checksum);
        Assert.InRange(allocated, 0, 4096);
    }
}
