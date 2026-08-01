namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Keyboard chords that the JS keydown shim intercepts on the textarea
/// and forwards to <see cref="EditorShortcutHandler"/> for processing.
/// Anything not in this enum is left to the browser's default behaviour.
/// </summary>
public enum EditorChord
{
    Tab,
    CtrlB,
    CtrlI,
    CtrlK,
    CtrlBacktick
}
