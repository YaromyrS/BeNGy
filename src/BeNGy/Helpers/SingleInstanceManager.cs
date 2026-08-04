using System.IO.Pipes;

namespace BeNGy.Helpers;

/// <summary>
/// Ensures only one instance of BeNGy runs at a time.
/// Uses a named Mutex for ownership detection and a named pipe for activation signaling.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string DefaultMutexName = "BeNGy_SingleInstance_20c5be2d";
    private const string DefaultPipeName = "BeNGy_Activation_20c5be2d";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly Action<Action>? _dispatcher;

    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;
    private bool _disposed;

    /// <summary>
    /// Fired on the first instance when a second instance attempts to launch.
    /// </summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// True if this process successfully acquired single-instance ownership.
    /// </summary>
    public bool IsFirstInstance { get; private set; }

    /// <summary>
    /// Creates a SingleInstanceManager with default names and MAUI MainThread dispatcher.
    /// </summary>
    public SingleInstanceManager()
        : this(DefaultMutexName, DefaultPipeName, null)
    {
    }

    /// <summary>
    /// Creates a SingleInstanceManager with custom names and dispatcher (for testing).
    /// </summary>
    /// <param name="mutexName">Named mutex identifier.</param>
    /// <param name="pipeName">Named pipe identifier.</param>
    /// <param name="dispatcher">Action dispatcher for raising events. If null, invokes directly.</param>
    public SingleInstanceManager(string mutexName, string pipeName, Action<Action>? dispatcher)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Attempts to acquire single-instance ownership.
    /// Returns true if this is the first instance (caller should continue startup).
    /// Returns false if another instance owns the mutex (caller should exit).
    /// </summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: _mutexName, createdNew: out var createdNew);
        IsFirstInstance = createdNew;

        if (IsFirstInstance)
        {
            StartListening();
            return true;
        }

        // Another instance is running — signal it and return false
        SignalExistingInstance();
        return false;
    }

    /// <summary>
    /// Starts a background named-pipe listener that waits for activation signals
    /// from subsequent launch attempts.
    /// </summary>
    private void StartListening()
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    // Connection received — raise activation event
                    var action = () => ActivationRequested?.Invoke(this, EventArgs.Empty);
                    if (_dispatcher != null)
                        _dispatcher(action);
                    else
                        action();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Pipe error — brief pause then retry
                    try { await Task.Delay(500, token); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }, token);
    }

    /// <summary>
    /// Sends an activation signal to the existing (first) instance via named pipe.
    /// </summary>
    private void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            // Connecting is sufficient to trigger the server-side ActivationRequested event.
        }
        catch
        {
            // Existing instance may not be listening yet — second instance exits regardless.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;

        if (IsFirstInstance && _mutex != null)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* Mutex not owned — already released */ }
        }

        _mutex?.Dispose();
        _mutex = null;
    }
}
