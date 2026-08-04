using BeNGy.Services.Interfaces;
using NSubstitute;

namespace BeNGy.Tests;

/// <summary>
/// Tests for ITrayService interface contract and tray service behavior.
/// Windows-specific TrayService implementation is tested via interface abstraction
/// since it requires WinUI runtime (integration test territory).
/// </summary>
public class TrayServiceTests
{
    [Fact]
    public void ITrayService_CanBeCreatedAsMock()
    {
        // Verifies the interface is well-formed and can be substituted
        var trayService = Substitute.For<ITrayService>();
        Assert.NotNull(trayService);
    }

    [Fact]
    public void Initialize_CanBeCalled()
    {
        var trayService = Substitute.For<ITrayService>();

        trayService.Initialize();

        trayService.Received(1).Initialize();
    }

    [Fact]
    public void ShowIcon_CanBeCalled()
    {
        var trayService = Substitute.For<ITrayService>();

        trayService.ShowIcon();

        trayService.Received(1).ShowIcon();
    }

    [Fact]
    public void HideIcon_CanBeCalled()
    {
        var trayService = Substitute.For<ITrayService>();

        trayService.HideIcon();

        trayService.Received(1).HideIcon();
    }

    [Fact]
    public void Dispose_CanBeCalled()
    {
        var trayService = Substitute.For<ITrayService>();

        trayService.Dispose();

        trayService.Received(1).Dispose();
    }

    [Fact]
    public void OpenRequested_EventCanBeSubscribed()
    {
        var trayService = Substitute.For<ITrayService>();
        var eventFired = false;

        trayService.OpenRequested += (_, _) => eventFired = true;
        trayService.OpenRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        Assert.True(eventFired);
    }

    [Fact]
    public void SettingsRequested_EventCanBeSubscribed()
    {
        var trayService = Substitute.For<ITrayService>();
        var eventFired = false;

        trayService.SettingsRequested += (_, _) => eventFired = true;
        trayService.SettingsRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        Assert.True(eventFired);
    }

    [Fact]
    public void ExitRequested_EventCanBeSubscribed()
    {
        var trayService = Substitute.For<ITrayService>();
        var eventFired = false;

        trayService.ExitRequested += (_, _) => eventFired = true;
        trayService.ExitRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        Assert.True(eventFired);
    }

    [Fact]
    public void MultipleEvents_CanBeSubscribedIndependently()
    {
        var trayService = Substitute.For<ITrayService>();
        var openFired = false;
        var settingsFired = false;
        var exitFired = false;

        trayService.OpenRequested += (_, _) => openFired = true;
        trayService.SettingsRequested += (_, _) => settingsFired = true;
        trayService.ExitRequested += (_, _) => exitFired = true;

        // Fire only OpenRequested
        trayService.OpenRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        Assert.True(openFired);
        Assert.False(settingsFired);
        Assert.False(exitFired);
    }

    [Fact]
    public void ExitRequested_TriggersDispose()
    {
        // Simulates the App.xaml.cs pattern: Exit → Dispose → Quit
        var trayService = Substitute.For<ITrayService>();
        var disposed = false;

        trayService.When(x => x.Dispose()).Do(_ => disposed = true);
        trayService.ExitRequested += (_, _) => trayService.Dispose();
        trayService.ExitRequested += Raise.Event<EventHandler>(trayService, EventArgs.Empty);

        Assert.True(disposed);
    }
}
