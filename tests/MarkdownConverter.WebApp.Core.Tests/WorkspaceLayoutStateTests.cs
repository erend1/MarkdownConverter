using MarkdownConverter.WebApp.Core.ViewModels;

namespace MarkdownConverter.WebApp.Core.Tests;

public class WorkspaceLayoutStateTests
{
    [Fact]
    public void Constructor_DefaultsToSourceAndEqualSplit()
    {
        var sut = new WorkspaceLayoutState();

        Assert.Equal(WorkspacePane.Source, sut.SelectedPane);
        Assert.Equal(50.0, sut.SourcePanePercentage);
        Assert.Equal(50.0, sut.PreviewPanePercentage);
    }

    [Fact]
    public void SelectPane_RepeatedSelectionsRemainStable()
    {
        var sut = new WorkspaceLayoutState();

        sut.SelectPane(WorkspacePane.Preview);
        sut.SelectPane(WorkspacePane.Preview);

        Assert.Equal(WorkspacePane.Preview, sut.SelectedPane);

        sut.SelectPane(WorkspacePane.Source);

        Assert.Equal(WorkspacePane.Source, sut.SelectedPane);
    }

    [Theory]
    [InlineData(5.0, 20.0)]
    [InlineData(20.0, 20.0)]
    [InlineData(37.5, 37.5)]
    [InlineData(80.0, 80.0)]
    [InlineData(95.0, 80.0)]
    public void SetSourcePanePercentage_ClampsToSupportedRange(
        double requested,
        double expected)
    {
        var sut = new WorkspaceLayoutState();

        sut.SetSourcePanePercentage(requested);

        Assert.Equal(expected, sut.SourcePanePercentage);
        Assert.Equal(100.0 - expected, sut.PreviewPanePercentage);
    }

    [Fact]
    public void ResetSplit_PreservesSelectedPane()
    {
        var sut = new WorkspaceLayoutState();
        sut.SelectPane(WorkspacePane.Preview);
        sut.SetSourcePanePercentage(72.0);

        sut.ResetSplit();

        Assert.Equal(WorkspacePane.Preview, sut.SelectedPane);
        Assert.Equal(50.0, sut.SourcePanePercentage);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SetSourcePanePercentage_NonFiniteValue_Throws(double value)
    {
        var sut = new WorkspaceLayoutState();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sut.SetSourcePanePercentage(value));
    }
}
