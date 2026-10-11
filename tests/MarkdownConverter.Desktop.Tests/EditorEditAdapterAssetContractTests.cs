using System.Text.RegularExpressions;

namespace MarkdownConverter.Desktop.Tests;

/// <summary>
/// Structural guards for the native-undo edit adapter in <c>dom-bridge.js</c>.
/// They pin the invariants a future edit must not silently drop; the
/// behaviour itself (exact text, one undo step, one input notification) is a
/// browser property and is verified in real browsers.
/// </summary>
public class EditorEditAdapterAssetContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void InsertTextAtCursor_EditsOnlyThroughNativeUndoableCommands()
    {
        var adapter = ReadInsertTextAtCursor();

        Assert.Contains("document.execCommand('insertText', false, text)", adapter);
        // Assigning the value or using setRangeText would wipe or bypass the
        // browser's undo history.
        Assert.DoesNotMatch(new Regex(@"\.value\s*=(?!=)"), adapter);
        Assert.DoesNotContain("setRangeText", adapter);
    }

    [Fact]
    public void InsertTextAtCursor_RaisesExactlyOneInputNotificationPerEdit()
    {
        var adapter = ReadInsertTextAtCursor();

        // Events raised by the edit itself are stopped for its duration only.
        Assert.Contains("window.addEventListener('input', coalesce, true)", adapter);
        Assert.Contains("window.removeEventListener('input', coalesce, true)", adapter);
        Assert.Contains("if (e.target === el) e.stopImmediatePropagation();", adapter);
        Assert.Equal(1, Count(adapter, "dispatchEvent(new Event('input', { bubbles: true }))"));
        // The listener is removed and the notification raised even if the edit throws.
        Assert.Matches(
            new Regex(@"finally\s*\{\s*window\.removeEventListener\('input', coalesce, true\);[\s\S]*?dispatchEvent\("),
            adapter);
    }

    [Fact]
    public void InsertTextAtCursor_VerifiesThePasteStyleEditAndKeepsThePlainFallback()
    {
        var adapter = ReadInsertTextAtCursor();

        Assert.Contains("accepted = document.execCommand('insertHTML', false, escapeHtml(text));", adapter);
        // An engine or content policy may refuse the command by throwing;
        // that must lead to the fallback, not to a failed edit.
        Assert.Matches(
            new Regex(@"try \{\s*accepted = document\.execCommand\('insertHTML'[^;]*;\s*\} catch \(rejected\) \{"),
            adapter);
        // The command must report success AND the result must equal the
        // expected text exactly; otherwise the edit is reverted and plain
        // insertText is used. The value alone is not enough: replacing text
        // with identical text leaves it unchanged whether or not the engine
        // performed the edit.
        Assert.Contains("var expected = before.substring(0, start) + text + before.substring(end);", adapter);
        Assert.Contains("if (accepted && el.value === expected)", adapter);
        // The revert must be conditional on this call having changed the
        // text, or it would undo an unrelated earlier edit.
        Assert.Matches(
            new Regex(@"if \(el\.value !== before\) \{\s*document\.execCommand\('undo'\);\s*if \(el\.value !== before\) \{\s*throw new Error\("),
            adapter);
        Assert.True(
            adapter.IndexOf("document.execCommand('insertHTML'", StringComparison.Ordinal)
                < adapter.LastIndexOf("document.execCommand('insertText', false, text)", StringComparison.Ordinal),
            "Plain insertText must remain the fallback after the paste-style attempt.");
    }

    [Fact]
    public void InsertTextAtCursor_LimitsThePasteStyleEditToVerifiedInputs()
    {
        var adapter = ReadInsertTextAtCursor();

        // Single-line text keeps the plain path.
        Assert.Contains(@"if (text.indexOf('\n') < 0) return false;", adapter);
        // Text the HTML parser would alter keeps the plain path.
        Assert.Contains(@"if (text.indexOf('\0') >= 0 || text.indexOf('\r') >= 0) return false;", adapter);
        // So does an edit that deletes the document's final line break.
        Assert.Contains("end === before.length", adapter);
        Assert.Contains("before.charCodeAt(end - 1) === 10", adapter);
        // Markup characters are escaped before the text reaches the parser.
        Assert.Contains(
            "s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')",
            adapter);
    }

    private static string ReadInsertTextAtCursor()
    {
        var bridge = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "MarkdownConverter.WebApp", "wwwroot", "js", "dom-bridge.js"));
        var start = bridge.IndexOf("insertTextAtCursor: function", StringComparison.Ordinal);
        var end = bridge.IndexOf("focus: function", StringComparison.Ordinal);

        Assert.True(start >= 0, "insertTextAtCursor was not found in dom-bridge.js.");
        Assert.True(end > start, "The member following insertTextAtCursor was not found.");
        return bridge[start..end];
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        var start = 0;
        while ((start = value.IndexOf(search, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += search.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MarkdownConverter.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the MarkdownConverter repository root.");
    }
}
