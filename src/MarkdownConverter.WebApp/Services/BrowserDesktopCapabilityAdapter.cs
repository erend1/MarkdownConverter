using MarkdownConverter.WebApp.Core.Services;
using Microsoft.JSInterop;

namespace MarkdownConverter.WebApp.Services;

public sealed class BrowserDesktopCapabilityAdapter
{
    private readonly IJSRuntime _js;

    public BrowserDesktopCapabilityAdapter(IJSRuntime js) => _js = js;

    public ValueTask<DesktopCapabilities?> ReadAsync() =>
        _js.InvokeAsync<DesktopCapabilities?>("desktopCapabilities.read");
}
