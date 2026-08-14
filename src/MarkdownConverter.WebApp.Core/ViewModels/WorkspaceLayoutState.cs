namespace MarkdownConverter.WebApp.Core.ViewModels;

/// <summary>
/// Identifies the content surface selected when the responsive workspace can
/// expose only one pane. Wide layouts may render both panes without changing
/// this retained compact-layout preference.
/// </summary>
public enum WorkspacePane
{
    Source,
    Preview
}

/// <summary>
/// Plain presentation state for responsive workspace decisions. Browser width
/// classification remains declarative CSS; this model owns only user choices
/// that must survive re-renders, breakpoint changes, and document switches.
/// </summary>
public sealed class WorkspaceLayoutState
{
    public const double MinimumSplitPercentage = 20.0;
    public const double MaximumSplitPercentage = 80.0;
    public const double EqualSplitPercentage = 50.0;

    public WorkspacePane SelectedPane { get; private set; } = WorkspacePane.Source;

    public double SourcePanePercentage { get; private set; } = EqualSplitPercentage;

    public double PreviewPanePercentage => 100.0 - SourcePanePercentage;

    public void SelectPane(WorkspacePane pane)
    {
        if (!Enum.IsDefined(pane))
            throw new ArgumentOutOfRangeException(nameof(pane));

        SelectedPane = pane;
    }

    public void SetSourcePanePercentage(double percentage)
    {
        if (!double.IsFinite(percentage))
            throw new ArgumentOutOfRangeException(nameof(percentage));

        SourcePanePercentage = Math.Clamp(
            percentage,
            MinimumSplitPercentage,
            MaximumSplitPercentage);
    }

    public void ResetSplit() => SourcePanePercentage = EqualSplitPercentage;
}
