namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Pure-C# replacement for the <c>editor-interop.js</c>
/// <c>_wrapSelection</c> / <c>_insertText</c> helpers. Given a chord
/// and the user's currently selected text, returns the replacement
/// string plus the inner-range offsets the caller should re-select
/// after applying the edit.
/// </summary>
public static class EditorShortcutHandler
{
    /// <summary>
    /// Returns the action for <paramref name="chord"/>, or <c>null</c>
    /// when the chord has no shortcut defined.
    /// </summary>
    public static ShortcutAction? Handle(string selectedText, EditorChord chord)
        => chord switch
        {
            EditorChord.Tab           => InsertOnly("    "),
            EditorChord.CtrlB         => Wrap(selectedText, "**"),
            EditorChord.CtrlI         => Wrap(selectedText, "*"),
            EditorChord.CtrlBacktick  => Wrap(selectedText, "`"),
            EditorChord.CtrlK         => Link(selectedText),
            _ => null
        };

    private static ShortcutAction InsertOnly(string text)
        => new(text, null, null);

    private static ShortcutAction Wrap(string selected, string marker)
    {
        var inner = string.IsNullOrEmpty(selected) ? "text" : selected;
        var replacement = marker + inner + marker;
        // Re-select the inner text — same UX as the old _wrapSelection JS
        // helper. Offsets are relative to the start of the replacement
        // string; the caller adds the original selection start to map
        // them back into textarea coordinates.
        var innerStart = marker.Length;
        var innerEnd = innerStart + inner.Length;
        return new ShortcutAction(replacement, innerStart, innerEnd);
    }

    private static ShortcutAction Link(string selected)
    {
        var inner = string.IsNullOrEmpty(selected) ? "text" : selected;
        // The old JS Ctrl+K did NOT re-select after insertion, leaving the
        // cursor at the end of "[…](url)". Preserve that behaviour.
        return new ShortcutAction($"[{inner}](url)", null, null);
    }
}

/// <summary>
/// Result of <see cref="EditorShortcutHandler.Handle"/>:
/// the text to insert at the current selection, and (optionally) the
/// inner-range offsets to re-select after the insert lands.
/// </summary>
public sealed record ShortcutAction(
    string Replacement,
    int? InnerStartOffset,
    int? InnerEndOffset);
