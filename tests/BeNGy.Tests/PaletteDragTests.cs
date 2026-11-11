using BeNGy.Helpers;

namespace BeNGy.Tests;

public class PaletteDragTests
{
    private const double Width = 560;

    [Theory]
    [InlineData(10, 20)]      // left end of the header, beside the title
    [InlineData(459, 55.9)]   // last DIP before the main page's button column
    public void IsInZone_HeaderStrip_MainPage(double x, double y) =>
        Assert.True(PaletteDrag.IsInZone(x, y, Width, leftExclusion: 0, rightExclusion: 100));

    [Theory]
    [InlineData(460, 20)]     // gear button column
    [InlineData(540, 20)]     // close button
    [InlineData(200, 56)]     // just below the strip: inputs start here
    [InlineData(200, -1)]
    public void IsInZone_ButtonsAndBody_AreNotDraggable(double x, double y) =>
        Assert.False(PaletteDrag.IsInZone(x, y, Width, leftExclusion: 0, rightExclusion: 100));

    [Fact]
    public void IsInZone_SettingsPage_ExcludesBackButtonColumn()
    {
        Assert.False(PaletteDrag.IsInZone(30, 20, Width, leftExclusion: 60, rightExclusion: 60));
        Assert.True(PaletteDrag.IsInZone(60, 20, Width, leftExclusion: 60, rightExclusion: 60));
    }

    [Fact]
    public void Move_FollowsTheCursorDelta_FromTheStartOrigin()
    {
        var drag = new PaletteDrag();
        drag.Begin(cursorX: 1000, cursorY: 300, windowX: 610, windowY: 255);

        Assert.Equal((730, 405), drag.Move(1120, 450));
        Assert.Equal((600, 245), drag.Move(990, 290));   // absolute from the start, not cumulative
    }

    [Fact]
    public void Move_WithoutAnActiveDrag_ReturnsNull()
    {
        var drag = new PaletteDrag();
        Assert.Null(drag.Move(10, 10));

        drag.Begin(0, 0, 0, 0);
        drag.End();

        Assert.False(drag.IsActive);
        Assert.Null(drag.Move(10, 10));
    }
}
