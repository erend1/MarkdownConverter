using Microsoft.AspNetCore.Components;

namespace MarkdownConverter.WebApp.Components.Primitives;

public partial class AppSelect<TItem>
{
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];
    [Parameter] public TItem? Selected { get; set; }
    [Parameter] public Func<TItem, string> ValueBinder { get; set; } = default!;
    [Parameter] public Func<TItem, string> DisplayBinder { get; set; } = default!;
    [Parameter] public EventCallback<TItem> SelectionChanged { get; set; }
    [Parameter] public string CssClass { get; set; } = "export-select";
    [Parameter] public string? AriaLabel { get; set; }

    private async Task OnChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString();
        foreach (var item in Items)
        {
            if (ValueBinder(item) == val)
            {
                await SelectionChanged.InvokeAsync(item);
                return;
            }
        }
    }
}
