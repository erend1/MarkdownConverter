using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Components;

public partial class ErrorDetailsDialog : IDisposable
{
    [Inject] private IErrorDetailsService ErrorDetailsService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private ErrorDetails? _current;
    private string _copyStatus = string.Empty;

    protected override void OnInitialized()
    {
        ErrorDetailsService.OnShow += OnShow;
    }

    private void OnShow(ErrorDetails details)
    {
        _current = details;
        _copyStatus = string.Empty;
        InvokeAsync(StateHasChanged);
    }

    private void Close()
    {
        _current = null;
        _copyStatus = string.Empty;
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") Close();
    }

    private async Task Copy()
    {
        if (_current is null) return;
        try
        {
            var ok = await JS.InvokeAsync<bool>("fileInterop.copyToClipboard", _current.Details);
            _copyStatus = ok ? "Copied to clipboard" : "Copy failed";
        }
        catch (Exception ex)
        {
            _copyStatus = $"Copy failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        ErrorDetailsService.OnShow -= OnShow;
    }
}
