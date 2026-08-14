namespace MarkdownConverter.WebApp.Core.Services;

/// <summary>
/// Thin C# wrapper over <c>wwwroot/js/dom-bridge.js</c>. Presenters and
/// components depend on this interface rather than calling
/// <c>IJSRuntime</c> directly, so business logic stays in C# and the JS
/// surface stays minimal and audit-able.
/// </summary>
public interface IEditorBridge
{
    /// <summary>
    /// Reads the current selection range of the element matched by
    /// <paramref name="selector"/>. Returns <c>(0, 0)</c> if the element
    /// cannot be located.
    /// </summary>
    ValueTask<TextRange> GetSelectionAsync(string selector);

    /// <summary>
    /// Assigns the selection range on the matched element. No-op if no
    /// element is found.
    /// </summary>
    ValueTask SetSelectionAsync(string selector, int start, int end);

    /// <summary>
    /// Returns the current <c>.value</c> of the matched element, bypassing
    /// any Blazor render-cycle staleness.
    /// </summary>
    ValueTask<string> GetValueAsync(string selector);

    /// <summary>
    /// Selects the explicit half-open range and scrolls the matched editor so
    /// that range is centred vertically. The range is supplied by the C# find
    /// owner rather than inferred from asynchronously rendered highlights.
    /// </summary>
    ValueTask RevealSelectionAsync(string selector, int start, int end);

    /// <summary>
    /// Reads the vertical scroll position as a proportional value in the
    /// <c>[0, 1]</c> range. Returns <c>0</c> if no element is found.
    /// </summary>
    ValueTask<double> GetScrollRatioAsync(string selector);

    /// <summary>
    /// Sets the matched element's vertical scroll position from a
    /// proportional <c>[0, 1]</c> value. No-op if no element is found.
    /// </summary>
    ValueTask SetScrollRatioAsync(string selector, double ratio);

    /// <summary>
    /// Inserts text at the current cursor position of the matched element,
    /// using <c>document.execCommand('insertText', …)</c> so the browser's
    /// native undo stack is preserved. The previously focused element is
    /// restored after the edit.
    /// </summary>
    ValueTask InsertTextAtCursorAsync(string selector, string text);

    /// <summary>
    /// Moves keyboard focus to the matched element.
    /// </summary>
    ValueTask FocusAsync(string selector);
}

/// <summary>
/// Half-open <c>[Start, End)</c> text range produced by
/// <see cref="IEditorBridge.GetSelectionAsync"/>.
/// </summary>
public readonly record struct TextRange(int Start, int End);
