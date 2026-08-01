using Microsoft.AspNetCore.Components;
using MarkdownConverter.WebApp.Core.Services;

namespace MarkdownConverter.WebApp.Components;

public partial class ToastContainer : IDisposable
{
    [Inject] private IToastService ToastService { get; set; } = default!;
    [Inject] private IErrorDetailsService ErrorDetailsService { get; set; } = default!;

    private const int FadeOutMs = 500;

    private readonly List<ToastItem> _toasts = new();

    protected override void OnInitialized()
    {
        ToastService.OnShow += OnToastShow;
    }

    private async void OnToastShow(ToastMessage message)
    {
        var item = new ToastItem
        {
            Message = message,
            Cts = new CancellationTokenSource()
        };
        _toasts.Add(item);
        await InvokeAsync(StateHasChanged);

        var lifetime = ToastDurations.AutoDismissFor(message.Type);

        try
        {
            await Task.Delay(lifetime, item.Cts.Token);
        }
        catch (TaskCanceledException)
        {
            // User dismissed (× or Show details) before the timer ran out.
            return;
        }

        await FadeOutAndRemoveAsync(item);
    }

    private async Task FadeOutAndRemoveAsync(ToastItem item)
    {
        if (item.Fading || item.Removed) return;
        item.Fading = true;
        await InvokeAsync(StateHasChanged);

        await Task.Delay(FadeOutMs);
        item.Removed = true;
        _toasts.Remove(item);
        await InvokeAsync(StateHasChanged);
    }

    private void DismissNow(ToastItem item)
    {
        item.Cts.Cancel();
        _ = FadeOutAndRemoveAsync(item);
    }

    private void OnShowDetails(ToastItem item)
    {
        if (string.IsNullOrEmpty(item.Message.Details)) return;
        ErrorDetailsService.Show(item.Message.Text, item.Message.Details);
        // The modal owns the message now — dismiss the toast.
        DismissNow(item);
    }

    public void Dispose()
    {
        ToastService.OnShow -= OnToastShow;
        foreach (var t in _toasts) t.Cts.Cancel();
    }

    private sealed class ToastItem
    {
        public required ToastMessage Message { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public bool Fading { get; set; }
        public bool Removed { get; set; }
    }
}
