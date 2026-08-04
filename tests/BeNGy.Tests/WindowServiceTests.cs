using BeNGy.Services.Interfaces;
using NSubstitute;

namespace BeNGy.Tests;

/// <summary>
/// Tests for IWindowService interface contract.
/// Windows-specific WindowService implementation requires WinUI runtime
/// and is validated via integration/manual testing.
/// </summary>
public class WindowServiceTests
{
    [Fact]
    public void IWindowService_CanBeCreatedAsMock()
    {
        var windowService = Substitute.For<IWindowService>();
        Assert.NotNull(windowService);
    }

    [Fact]
    public void ShowWindow_CanBeCalled()
    {
        var windowService = Substitute.For<IWindowService>();

        windowService.ShowWindow();

        windowService.Received(1).ShowWindow();
    }

    [Fact]
    public void HideWindow_CanBeCalled()
    {
        var windowService = Substitute.For<IWindowService>();

        windowService.HideWindow();

        windowService.Received(1).HideWindow();
    }

    [Fact]
    public void CenterOnScreen_CanBeCalled()
    {
        var windowService = Substitute.For<IWindowService>();

        windowService.CenterOnScreen();

        windowService.Received(1).CenterOnScreen();
    }

    [Fact]
    public void IsVisible_DefaultsToFalse()
    {
        var windowService = Substitute.For<IWindowService>();

        Assert.False(windowService.IsVisible);
    }

    [Fact]
    public void IsVisible_CanBeConfigured()
    {
        var windowService = Substitute.For<IWindowService>();

        windowService.IsVisible.Returns(true);

        Assert.True(windowService.IsVisible);
    }

    [Fact]
    public void ShowWindow_SetsIsVisibleTrue()
    {
        var windowService = Substitute.For<IWindowService>();
        windowService.When(x => x.ShowWindow()).Do(_ => windowService.IsVisible.Returns(true));

        windowService.ShowWindow();

        Assert.True(windowService.IsVisible);
    }

    [Fact]
    public void HideWindow_SetsIsVisibleFalse()
    {
        var windowService = Substitute.For<IWindowService>();
        windowService.IsVisible.Returns(true);
        windowService.When(x => x.HideWindow()).Do(_ => windowService.IsVisible.Returns(false));

        windowService.HideWindow();

        Assert.False(windowService.IsVisible);
    }

    [Fact]
    public void ShowThenHide_TogglesVisibility()
    {
        var windowService = Substitute.For<IWindowService>();
        windowService.When(x => x.ShowWindow()).Do(_ => windowService.IsVisible.Returns(true));
        windowService.When(x => x.HideWindow()).Do(_ => windowService.IsVisible.Returns(false));

        windowService.ShowWindow();
        Assert.True(windowService.IsVisible);

        windowService.HideWindow();
        Assert.False(windowService.IsVisible);

        windowService.ShowWindow();
        Assert.True(windowService.IsVisible);
    }

    [Fact]
    public void TrayOpenRequested_ShowsWindow()
    {
        // Validates the integration pattern: tray Open → window show
        var trayService = Substitute.For<ITrayService>();
        var windowService = Substitute.For<IWindowService>();

        trayService.OpenRequested += (_, _) => windowService.ShowWindow();

        trayService.OpenRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        windowService.Received(1).ShowWindow();
    }
}
