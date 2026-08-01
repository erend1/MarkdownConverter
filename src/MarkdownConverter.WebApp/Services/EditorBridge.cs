using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Services;

/// <summary>
/// Blazor implementation of <see cref="IEditorBridge"/> — forwards each
/// call to the matching <c>window.domBridge</c> primitive. Every method
/// is a one-line passthrough; this class deliberately has zero branching
/// logic so presenter tests can mock <see cref="IEditorBridge"/> without
/// re-implementing JS interop semantics.
/// </summary>
public sealed class EditorBridge : IEditorBridge
{
    private readonly IJSRuntime _js;

    public EditorBridge(IJSRuntime js) => _js = js;

    public ValueTask<TextRange> GetSelectionAsync(string selector) =>
        _js.InvokeAsync<TextRange>("domBridge.getSelection", selector);

    public ValueTask SetSelectionAsync(string selector, int start, int end) =>
        _js.InvokeVoidAsync("domBridge.setSelection", selector, start, end);

    public ValueTask<string> GetValueAsync(string selector) =>
        _js.InvokeAsync<string>("domBridge.getValue", selector);

    public ValueTask ScrollSelectionIntoViewAsync(string selector) =>
        _js.InvokeVoidAsync("domBridge.scrollSelectionIntoView", selector);

    public ValueTask<double> GetScrollRatioAsync(string selector) =>
        _js.InvokeAsync<double>("domBridge.getScrollRatio", selector);

    public ValueTask SetScrollRatioAsync(string selector, double ratio) =>
        _js.InvokeVoidAsync("domBridge.setScrollRatio", selector, ratio);

    public ValueTask InsertTextAtCursorAsync(string selector, string text) =>
        _js.InvokeVoidAsync("domBridge.insertTextAtCursor", selector, text);

    public ValueTask FocusAsync(string selector) =>
        _js.InvokeVoidAsync("domBridge.focus", selector);
}
