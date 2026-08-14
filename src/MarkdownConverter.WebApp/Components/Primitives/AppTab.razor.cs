using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MarkdownConverter.WebApp.Core.ViewModels;

namespace MarkdownConverter.WebApp.Components.Primitives;

public partial class AppTab
{
    [Parameter, EditorRequired] public TabViewModel Tab { get; set; } = default!;
    [Parameter] public bool IsActive { get; set; }
    [Parameter] public bool IsDragOver { get; set; }
    [Parameter] public bool IsRenaming { get; set; }
    [Parameter] public string RenameText { get; set; } = string.Empty;

    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public EventCallback OnDragStart { get; set; }
    [Parameter] public EventCallback OnDragOver { get; set; }
    [Parameter] public EventCallback OnDragLeave { get; set; }
    [Parameter] public EventCallback OnDragEnd { get; set; }
    [Parameter] public EventCallback OnDrop { get; set; }
    [Parameter] public EventCallback OnStartRename { get; set; }
    [Parameter] public EventCallback OnRenameBlur { get; set; }
    [Parameter] public EventCallback<KeyboardEventArgs> OnRenameKeyDown { get; set; }
    [Parameter] public EventCallback<string> RenameTextChanged { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private Task OnRenameTextChanged(string value) => RenameTextChanged.InvokeAsync(value);

    private static string TruncateName(string name)
        => name.Length > 20 ? name[..17] + "..." : name;

    private string GetTabAriaLabel() => Tab.IsDirty
        ? $"{Tab.FileName}, unsaved changes"
        : Tab.FileName;
}
