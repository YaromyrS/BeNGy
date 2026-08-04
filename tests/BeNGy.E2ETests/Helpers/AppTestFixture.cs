using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace BeNGy.E2ETests.Helpers;

public class AppTestFixture : IDisposable
{
    private const string AppRelativePath =
        @"src\BeNGy\bin\Debug\net8.0-windows10.0.19041.0\win-x64\BeNGy.exe";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private Application? _app;
    private UIA3Automation? _automation;

    public UIA3Automation Automation => _automation ??= new UIA3Automation();

    public string ExePath { get; }

    public AppTestFixture()
    {
        var envPath = Environment.GetEnvironmentVariable("BENGY_EXE_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            ExePath = envPath;
        }
        else
        {
            // Walk up from assembly location to find the repo root (contains BeNGy.sln)
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(AppTestFixture).Assembly.Location)!);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BeNGy.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
                throw new InvalidOperationException(
                    "Could not find repo root (BeNGy.sln). Set BENGY_EXE_PATH environment variable.");

            ExePath = Path.Combine(dir.FullName, AppRelativePath);
        }

        if (!File.Exists(ExePath))
            throw new FileNotFoundException(
                $"BeNGy.exe not found at '{ExePath}'. Build the app first or set BENGY_EXE_PATH.", ExePath);
    }

    /// <summary>
    /// Ensures the app is running. If it is already running, does nothing.
    /// If it is not running (first call or after the process exited), launches it.
    /// </summary>
    public Application EnsureRunning()
    {
        if (_app is not null && !_app.HasExited)
            return _app;

        return LaunchApp();
    }

    /// <summary>
    /// Kills any already-running BeNGy instances, then starts a fresh one.
    /// For MAUI single-file apps, the initial process may exit and spawn a child,
    /// so we attach to whatever BeNGy process is running after launch.
    /// </summary>
    public Application LaunchApp()
    {
        KillExistingInstances();

        // Wait for the mutex to be fully released after killing
        Thread.Sleep(2000);

        // Launch the process directly via Process.Start for more control
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = ExePath,
            WorkingDirectory = Path.GetDirectoryName(ExePath)!,
            UseShellExecute = false
        });

        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {ExePath}");

        // Wait for a BeNGy process with a main window handle to appear.
        // The initial process may die (single-file extraction) and a child takes over.
        var attachResult = Retry.WhileNull(
            () =>
            {
                var procs = Process.GetProcessesByName("BeNGy");
                var candidate = procs.FirstOrDefault(p =>
                {
                    try { return !p.HasExited && p.MainWindowHandle != IntPtr.Zero; }
                    catch { return false; }
                });
                foreach (var p in procs.Where(p => p != candidate)) p.Dispose();
                return candidate;
            },
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(500));

        if (attachResult.Result is null)
            throw new TimeoutException("No BeNGy process with a main window appeared within 15s.");

        _app = Application.Attach(attachResult.Result);
        return _app;
    }

    /// <summary>
    /// Closes / kills the application process.
    /// </summary>
    public void CloseApp()
    {
        if (_app is null) return;

        try
        {
            if (!_app.HasExited)
            {
                _app.Kill();
            }
        }
        catch
        {
            // Best-effort cleanup
        }

        _app = null;
    }

    /// <summary>
    /// Waits for the main window whose title contains "BeNGy".
    /// </summary>
    public Window GetMainWindow(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var window = Retry.WhileNull(
            () =>
            {
                if (_app is null || _app.HasExited) return null;
                var win = _app.GetMainWindow(Automation);
                return win?.Title?.Contains("BeNGy", StringComparison.OrdinalIgnoreCase) == true
                    ? win
                    : null;
            },
            effectiveTimeout,
            TimeSpan.FromMilliseconds(250));

        return window.Result
               ?? throw new TimeoutException(
                   $"Main window with title containing 'BeNGy' did not appear within {effectiveTimeout.TotalSeconds}s.");
    }

    /// <summary>
    /// Finds a descendant element by its Name property (e.g. ToolbarItems that lack AutomationId).
    /// </summary>
    public AutomationElement? FindByName(AutomationElement parent, string name)
    {
        var cf = Automation.ConditionFactory;
        return parent.FindFirstDescendant(cf.ByName(name));
    }

    /// <summary>
    /// Waits until an element with the given Name appears under <paramref name="parent"/>.
    /// </summary>
    public AutomationElement WaitForElementByName(AutomationElement parent, string name, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var result = Retry.WhileNull(
            () => FindByName(parent, name),
            effectiveTimeout,
            TimeSpan.FromMilliseconds(250));

        return result.Result
               ?? throw new TimeoutException(
                   $"Element with name '{name}' did not appear within {effectiveTimeout.TotalSeconds}s.");
    }

    /// <summary>
    /// Finds a descendant element by its AutomationId.
    /// </summary>
    public AutomationElement? FindByAutomationId(AutomationElement parent, string automationId)
    {
        var cf = Automation.ConditionFactory;
        return parent.FindFirstDescendant(cf.ByAutomationId(automationId));
    }

    /// <summary>
    /// Waits until an element with the given AutomationId appears under <paramref name="parent"/>.
    /// </summary>
    public AutomationElement WaitForElement(AutomationElement parent, string automationId, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var result = Retry.WhileNull(
            () => FindByAutomationId(parent, automationId),
            effectiveTimeout,
            TimeSpan.FromMilliseconds(250));

        return result.Result
               ?? throw new TimeoutException(
                   $"Element '{automationId}' did not appear within {effectiveTimeout.TotalSeconds}s.");
    }

    /// <summary>
    /// Clears the input field and sets text. Uses ValuePattern if available,
    /// falls back to keyboard simulation.
    /// </summary>
    public void TypeText(AutomationElement element, string text)
    {
        element.Focus();
        Thread.Sleep(100);

        // Try ValuePattern first (most reliable for WinUI/MAUI controls)
        var valuePattern = element.Patterns.Value.PatternOrDefault;
        if (valuePattern != null)
        {
            valuePattern.SetValue(text);
            return;
        }

        // Fallback: keyboard simulation
        Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
                                    FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        Keyboard.Type(text);
    }

    /// <summary>
    /// Reads the current clipboard text content using Win32 APIs.
    /// </summary>
    public string GetClipboardText()
    {
        string text = string.Empty;
        var thread = new Thread(() =>
        {
            if (!OpenClipboard(IntPtr.Zero)) return;
            try
            {
                var handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero) return;
                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero) return;
                try
                {
                    text = System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return text;
    }

    private const uint CF_UNICODETEXT = 13;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    /// <summary>
    /// Sends keyboard input via FlaUI.
    /// </summary>
    public void SendKeys(string keys)
    {
        Keyboard.Type(keys);
    }

    /// <summary>
    /// Waits for the main window to close or become hidden.
    /// </summary>
    public bool WaitForWindowClose(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var result = Retry.WhileTrue(
            () =>
            {
                try
                {
                    if (_app is null || _app.HasExited) return false;
                    var win = _app.GetMainWindow(Automation, TimeSpan.FromMilliseconds(500));
                    return win is not null && !win.IsOffscreen;
                }
                catch
                {
                    return false;
                }
            },
            effectiveTimeout,
            TimeSpan.FromMilliseconds(250));

        return result.Result;
    }

    public void Dispose()
    {
        CloseApp();
        _automation?.Dispose();
        _automation = null;
        GC.SuppressFinalize(this);
    }

    private static void KillExistingInstances()
    {
        foreach (var process in Process.GetProcessesByName("BeNGy"))
        {
            try
            {
                process.Kill();
                process.WaitForExit(3000);
            }
            catch
            {
                // Best-effort
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
