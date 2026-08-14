using Microsoft.AspNetCore.Components;

namespace MarkdownConverter.WebApp.Components.Primitives;

public enum ButtonVariant { Default, Export, Pdf, CompilePdf, Theme }
public enum ButtonSize { Default, Sm }

public partial class AppButton
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public string? Icon { get; set; }
    [Parameter] public ButtonVariant Variant { get; set; } = ButtonVariant.Default;
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Default;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool AsLabel { get; set; }

    // Extra class string appended after the variant/size classes — used for
    // one-off modifiers like "find-close" that aren't worth a dedicated variant.
    [Parameter] public string? CssClass { get; set; }

    private string ComposedCssClass
    {
        get
        {
            var variantClass = Variant switch
            {
                ButtonVariant.Export => " btn-export",
                ButtonVariant.Pdf => " btn-pdf",
                ButtonVariant.CompilePdf => " btn-compile-pdf",
                ButtonVariant.Theme => " btn-theme",
                _ => string.Empty,
            };
            var sizeClass = Size == ButtonSize.Sm ? " btn-sm" : string.Empty;
            var extra = string.IsNullOrWhiteSpace(CssClass) ? string.Empty : " " + CssClass;
            return "btn" + variantClass + sizeClass + extra;
        }
    }
}
