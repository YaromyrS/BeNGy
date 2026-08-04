using BeNGy.Helpers;

namespace BeNGy.Tests;

/// <summary>
/// Tests for SingleInstanceManager — mutex-based single instance enforcement
/// and named-pipe activation signaling.
/// </summary>
public class SingleInstanceManagerTests : IDisposable
{
    // Use unique names per test run to avoid cross-test interference
    private readonly string _mutexName = $"BeNGy_Test_Mutex_{Guid.NewGuid():N}";
    private readonly string _pipeName = $"BeNGy_Test_Pipe_{Guid.NewGuid():N}";
    private readonly List<SingleInstanceManager> _managers = new();

    private SingleInstanceManager CreateManager(Action<Action>? dispatcher = null)
    {
        var mgr = new SingleInstanceManager(_mutexName, _pipeName, dispatcher ?? (a => a()));
        _managers.Add(mgr);
        return mgr;
    }

    public void Dispose()
    {
        foreach (var m in _managers)
            m.Dispose();
    }

    [Fact]
    public void TryAcquire_FirstInstance_ReturnsTrue()
    {
        var mgr = CreateManager();

        var result = mgr.TryAcquire();

        Assert.True(result);
        Assert.True(mgr.IsFirstInstance);
    }

    [Fact]
    public void TryAcquire_SecondInstance_ReturnsFalse()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        var second = CreateManager();
        var result = second.TryAcquire();

        Assert.False(result);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void TryAcquire_AfterFirstDisposed_NewInstanceSucceeds()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());
        first.Dispose();

        // Use same mutex/pipe names but new manager
        var second = new SingleInstanceManager(_mutexName, _pipeName, a => a());
        _managers.Add(second);

        Assert.True(second.TryAcquire());
        Assert.True(second.IsFirstInstance);
    }

    [Fact]
    public void IsFirstInstance_BeforeAcquire_IsFalse()
    {
        var mgr = CreateManager();

        Assert.False(mgr.IsFirstInstance);
    }

    [Fact]
    public async Task ActivationRequested_FiredWhenSecondInstanceConnects()
    {
        var activationFired = new TaskCompletionSource<bool>();
        var first = CreateManager();
        first.TryAcquire();
        first.ActivationRequested += (_, _) => activationFired.TrySetResult(true);

        // Allow listener to start
        await Task.Delay(200);

        // Simulate second instance
        var second = CreateManager();
        second.TryAcquire(); // This signals the first instance

        var fired = await Task.WhenAny(activationFired.Task, Task.Delay(3000)) == activationFired.Task;
        Assert.True(fired, "ActivationRequested should fire when second instance connects");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var mgr = CreateManager();
        mgr.TryAcquire();

        mgr.Dispose();
        mgr.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_ReleaseMutex_AllowsNewFirstInstance()
    {
        var first = CreateManager();
        Assert.True(first.TryAcquire());

        first.Dispose();

        var second = new SingleInstanceManager(_mutexName, _pipeName, a => a());
        _managers.Add(second);
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public void Dispose_WithoutAcquire_DoesNotThrow()
    {
        var mgr = CreateManager();
        mgr.Dispose(); // Should not throw even without TryAcquire
    }

    [Fact]
    public void TryAcquire_SecondInstance_IsFirstInstanceIsFalse()
    {
        var first = CreateManager();
        first.TryAcquire();

        var second = CreateManager();
        second.TryAcquire();

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public async Task Dispatcher_IsUsedWhenFiringActivation()
    {
        var dispatcherCalled = false;
        var first = CreateManager(dispatcher: action =>
        {
            dispatcherCalled = true;
            action();
        });
        first.TryAcquire();

        var activationFired = new TaskCompletionSource<bool>();
        first.ActivationRequested += (_, _) => activationFired.TrySetResult(true);

        await Task.Delay(200);

        var second = CreateManager();
        second.TryAcquire();

        await Task.WhenAny(activationFired.Task, Task.Delay(3000));

        Assert.True(dispatcherCalled, "Custom dispatcher should be invoked");
    }
}
