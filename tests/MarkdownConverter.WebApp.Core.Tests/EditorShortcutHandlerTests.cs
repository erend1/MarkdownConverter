using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Core.Tests;

public class EditorShortcutHandlerTests
{
    // ---------- Tab ----------

    [Fact]
    public void Tab_InsertsFourSpaces_NoReselect()
    {
        var action = EditorShortcutHandler.Handle("", EditorChord.Tab);

        Assert.NotNull(action);
        Assert.Equal("    ", action!.Replacement);
        Assert.Null(action.InnerStartOffset);
        Assert.Null(action.InnerEndOffset);
    }

    [Fact]
    public void Tab_WithSelection_StillInsertsFourSpacesReplacingIt()
    {
        // The textarea's execCommand insertText replaces the active
        // selection with the action's Replacement string, so the action
        // itself is independent of the selection for Tab.
        var action = EditorShortcutHandler.Handle("anything", EditorChord.Tab);

        Assert.Equal("    ", action!.Replacement);
    }

    // ---------- Ctrl+B (bold) ----------

    [Fact]
    public void CtrlB_EmptySelection_InsertsPlaceholder()
    {
        var action = EditorShortcutHandler.Handle("", EditorChord.CtrlB);

        Assert.Equal("**text**", action!.Replacement);
        // Inner should be the placeholder "text"
        Assert.Equal(2, action.InnerStartOffset);
        Assert.Equal(6, action.InnerEndOffset);
    }

    [Fact]
    public void CtrlB_WithSelection_WrapsSelection()
    {
        var action = EditorShortcutHandler.Handle("hello", EditorChord.CtrlB);

        Assert.Equal("**hello**", action!.Replacement);
        Assert.Equal(2, action.InnerStartOffset);
        Assert.Equal(7, action.InnerEndOffset);
    }

    // ---------- Ctrl+I (italic) ----------

    [Fact]
    public void CtrlI_WithSelection_WrapsInSingleAsterisk()
    {
        var action = EditorShortcutHandler.Handle("note", EditorChord.CtrlI);

        Assert.Equal("*note*", action!.Replacement);
        Assert.Equal(1, action.InnerStartOffset);
        Assert.Equal(5, action.InnerEndOffset);
    }

    [Fact]
    public void CtrlI_EmptySelection_InsertsPlaceholder()
    {
        var action = EditorShortcutHandler.Handle("", EditorChord.CtrlI);

        Assert.Equal("*text*", action!.Replacement);
    }

    // ---------- Ctrl+` (inline code) ----------

    [Fact]
    public void CtrlBacktick_WithSelection_WrapsInBackticks()
    {
        var action = EditorShortcutHandler.Handle("foo", EditorChord.CtrlBacktick);

        Assert.Equal("`foo`", action!.Replacement);
        Assert.Equal(1, action.InnerStartOffset);
        Assert.Equal(4, action.InnerEndOffset);
    }

    [Fact]
    public void CtrlBacktick_EmptySelection_InsertsPlaceholder()
    {
        var action = EditorShortcutHandler.Handle("", EditorChord.CtrlBacktick);

        Assert.Equal("`text`", action!.Replacement);
    }

    // ---------- Ctrl+K (link) ----------

    [Fact]
    public void CtrlK_WithSelection_InsertsMarkdownLink()
    {
        var action = EditorShortcutHandler.Handle("anthropic", EditorChord.CtrlK);

        Assert.Equal("[anthropic](url)", action!.Replacement);
        // Original JS did NOT re-select after Ctrl+K — preserve that.
        Assert.Null(action.InnerStartOffset);
        Assert.Null(action.InnerEndOffset);
    }

    [Fact]
    public void CtrlK_EmptySelection_InsertsPlaceholderLink()
    {
        var action = EditorShortcutHandler.Handle("", EditorChord.CtrlK);

        Assert.Equal("[text](url)", action!.Replacement);
    }

    // ---------- Unknown chord ----------

    [Fact]
    public void UnknownChord_ReturnsNull()
    {
        // Cast an out-of-range value to make sure the handler fails closed
        // rather than throwing if a future chord is forwarded but not yet
        // wired up.
        var action = EditorShortcutHandler.Handle("foo", (EditorChord)999);

        Assert.Null(action);
    }
}
