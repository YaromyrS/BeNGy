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

    static AppTestFixture()
    {
        // Match the app's DPI awareness so GetWindowRect and message
        // coordinates are physical pixels on scaled displays (the sandbox
        // guest may not run at 100%). Without this, coordinate feedback in
        // the drag tests is DPI-virtualized and the drag path skews. May
        // fail if the test host already committed an awareness -- fine,
        // the assertions tolerate residual scaling.
        _ = SetProcessDpiAwarenessContext(new IntPtr(-4));   // PER_MONITOR_AWARE_V2
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

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
    /// Ensures the app is running AND its window is on screen, ready to interact with.
    ///
    /// BeNGy hides its window instead of closing it (auto-close after copy, Escape,
    /// close-to-tray), so "the process is alive" is not the same as "a test can drive
    /// the UI". Window lookups key off the main window's class and visibility (see
    /// <see cref="GetMainWindow"/> for why not Process.MainWindowHandle).
    /// </summary>
    public Application EnsureRunning()
    {
        if (_app is null || _app.HasExited)
            return LaunchApp();

        EnsureWindowVisible();
        return _app;
    }

    /// <summary>
    /// True when the app's main window is currently shown (hidden to the tray is the
    /// state this whole class has to cope with).
    /// </summary>
    public bool HasVisibleMainWindow()
    {
        var handle = FindTopLevelWindowHandle();
        return handle != IntPtr.Zero && IsWindowVisible(handle);
    }

    /// <summary>
    /// Brings the window back if it is hidden in the tray, the same way a user would --
    /// via the global hotkey. Falls back to a relaunch so a swallowed hotkey degrades
    /// into a slower test rather than an unrecoverable one.
    /// </summary>
    public void EnsureWindowVisible(TimeSpan? timeout = null)
    {
        if (HasVisibleMainWindow()) return;

        Keyboard.TypeSimultaneously(
            FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL,
            FlaUI.Core.WindowsAPI.VirtualKeyShort.SHIFT,
            FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_B);

        var appeared = Retry.WhileFalse(
            HasVisibleMainWindow,
            timeout ?? DefaultTimeout,
            TimeSpan.FromMilliseconds(250));

        if (!appeared.Result)
            LaunchApp();
    }

    /// <summary>
    /// Hides the main window to the tray, reproducing the state the app is left in
    /// after auto-close / Escape / close-to-tray. Used by the harness regression tests.
    /// </summary>
    public void HideMainWindow()
    {
        var handle = FindTopLevelWindowHandle();
        if (handle == IntPtr.Zero) return;

        ShowWindow(handle, SW_HIDE);
        Retry.WhileTrue(HasVisibleMainWindow, DefaultTimeout, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Finds the app's top-level window by walking the desktop's windows, which -- unlike
    /// Process.MainWindowHandle -- also finds it while it is hidden.
    /// </summary>
    private IntPtr FindTopLevelWindowHandle()
    {
        if (_app is null || _app.HasExited) return IntPtr.Zero;

        var targetPid = (uint)_app.ProcessId;
        var found = IntPtr.Zero;

        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var pid);
            if (pid != targetPid) return true;

            var className = new System.Text.StringBuilder(256);
            GetClassName(handle, className, className.Capacity);
            if (className.ToString() != MainWindowClassName) return true;

            found = handle;
            return false;  // Stop enumerating.
        }, IntPtr.Zero);

        return found;
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
    /// Waits for the app's main window to be shown and returns it.
    ///
    /// Located by window class, NOT Process.MainWindowHandle / FlaUI's
    /// Application.GetMainWindow: WinAppSDK shows dropdowns and tooltips in their
    /// own top-level windows (Microsoft.UI.Content.PopupWindowSiteBridge), and
    /// MainWindowHandle happily returns one of those. Lookups then searched a
    /// popup's tree instead of the app. That was the "Settings never opens"
    /// failure (the page HAD opened; a "Back" tooltip had taken over the handle)
    /// and the long-standing cascades after a test left a dropdown open (FlaUI's
    /// ComboBox.Items expands and never collapses).
    /// </summary>
    public Window GetMainWindow(TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var window = Retry.WhileNull(
            () =>
            {
                var handle = FindTopLevelWindowHandle();
                return handle != IntPtr.Zero && IsWindowVisible(handle)
                    ? Automation.FromHandle(handle).AsWindow()
                    : null;
            },
            effectiveTimeout,
            TimeSpan.FromMilliseconds(250));

        return window.Result
               ?? throw new TimeoutException(
                   $"The BeNGy main window was not shown within {effectiveTimeout.TotalSeconds}s.");
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

    // ── Per-test reset ──────────────────────────────────────────────
    // The suite shares ONE app instance, so without a reset every test inherits
    // whatever the previous one left behind. That, not randomness, was behind
    // the long-standing "known failures": a test that ended on the Settings page
    // made the next one time out looking for the form; a ticket left invalid
    // ("PROJ 123") kept the warning up; a leftover prefix broke "feature/..."
    // assertions; a dropdown left open hid the window's elements.

    /// <summary>The prefix every test starts from (the app's out-of-the-box default).</summary>
    public const string BaselinePrefix = "feature";

    /// <summary>
    /// Puts the app into one known state: running, window shown, no dropdown
    /// open, main page, empty form, <see cref="BaselinePrefix"/> selected. Called
    /// from every test class constructor (xUnit builds a new instance per test).
    /// </summary>
    public void ResetForTest()
    {
        EnsureRunning();
        var window = GetMainWindow();
        CollapseOpenDropdowns(window);

        if (!ReturnToMainPage(window))
        {
            // Somewhere we can't navigate out of: a relaunch is the reliable reset.
            LaunchApp();
            WaitForElement(GetMainWindow(TimeSpan.FromSeconds(10)), AutomationIds.TicketEntry, TimeSpan.FromSeconds(15));
        }

        window = GetMainWindow();
        ClearMainForm(window);
        SelectComboItem(WaitForElement(window, AutomationIds.PrefixPicker), BaselinePrefix);
    }

    /// <summary>
    /// Empties the ticket and title fields through the value pattern, without
    /// moving keyboard focus (focus is itself under test in some tests).
    /// </summary>
    public void ClearMainForm(Window window)
    {
        foreach (var id in new[] { AutomationIds.TicketEntry, AutomationIds.TitleEntry })
        {
            var field = WaitForElement(window, id);
            if (!string.IsNullOrEmpty(field.AsTextBox().Text))
                field.Patterns.Value.Pattern.SetValue(string.Empty);
        }
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(200));
    }

    /// <summary>
    /// Selects a combo box item by text and guarantees the dropdown is closed
    /// afterwards. A WinUI dropdown left open made the next lookups in the window
    /// fail, which is how one test used to take down every test after it.
    /// </summary>
    public void SelectComboItem(AutomationElement comboElement, string itemText)
    {
        var combo = comboElement.AsComboBox();
        if (string.Equals(combo.SelectedItem?.Text, itemText, StringComparison.OrdinalIgnoreCase))
            return;

        combo.Expand();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
        var item = Retry.WhileNull(
                () => combo.Items.FirstOrDefault(i => string.Equals(i.Text, itemText, StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(200)).Result
            ?? throw new InvalidOperationException($"No item '{itemText}' in combo '{comboElement.AutomationId}'.");
        item.Select();

        if (combo.ExpandCollapseState == FlaUI.Core.Definitions.ExpandCollapseState.Expanded)
            combo.Collapse();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
    }

    private void CollapseOpenDropdowns(Window window)
    {
        foreach (var element in window.FindAllDescendants(
                     Automation.ConditionFactory.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)))
        {
            var expandCollapse = element.Patterns.ExpandCollapse.PatternOrDefault;
            if (expandCollapse?.ExpandCollapseState.ValueOrDefault == FlaUI.Core.Definitions.ExpandCollapseState.Expanded)
                expandCollapse.Collapse();
        }
    }

    /// <summary>
    /// True when the main page's form is showing, navigating back from Settings
    /// first if needed. Navigation is polled rather than slept on: the first
    /// page transition in a fresh Debug build can take seconds (JIT).
    /// </summary>
    private bool ReturnToMainPage(Window window)
    {
        if (FindByAutomationId(window, AutomationIds.TicketEntry) is not null)
            return true;

        var done = FindByAutomationId(window, AutomationIds.DoneButton);
        if (done is null)
            return false;

        done.AsButton().Invoke();
        return Retry.WhileNull(
            () => FindByAutomationId(GetMainWindow(), AutomationIds.TicketEntry),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMilliseconds(250)).Result is not null;
    }

    // ── Failure diagnostics ─────────────────────────────────────────

    /// <summary>
    /// Saves what the app looks like right now, a window screenshot plus an outline
    /// of its top-level windows and UI Automation elements, into BENGY_DIAG_DIR (the
    /// sandbox launcher points it at the mapped, writable results folder; the test
    /// host's working directory is the read-only mapped bin folder there), falling
    /// back to %TEMP%. For failures that only reproduce inside the VM, where nobody
    /// can look. Never throws: it runs on failure paths and must not mask the real
    /// error.
    /// </summary>
    public void CaptureDiagnostics(string label)
    {
        var dir = Environment.GetEnvironmentVariable("BENGY_DIAG_DIR")
                  ?? Path.Combine(Path.GetTempPath(), "bengy-e2e-diag");
        var stem = Path.Combine(dir, $"{DateTime.Now:HHmmss}-{label}");
        var lines = new List<string>();
        try
        {
            Directory.CreateDirectory(dir);
            if (_app is null || _app.HasExited)
            {
                lines.Add("app not running");
                return;
            }

            var pid = (uint)_app.ProcessId;
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out var owner);
                if (owner != pid) return true;
                var className = new System.Text.StringBuilder(256);
                GetClassName(handle, className, className.Capacity);
                GetWindowRect(handle, out var rect);
                lines.Add($"window 0x{handle.ToInt64():X} class={className} visible={IsWindowVisible(handle)} " +
                          $"rect=({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom})");
                return true;
            }, IntPtr.Zero);

            // Hover vs keyboard focus: which one opened a stray popup (tooltip)?
            GetCursorPos(out var cursor);
            lines.Add($"cursor=({cursor.X},{cursor.Y})");
            try
            {
                var focused = Automation.FocusedElement();
                lines.Add($"focused: {focused?.ControlType} id={focused?.Properties.AutomationId.ValueOrDefault} " +
                          $"name={focused?.Properties.Name.ValueOrDefault} class={focused?.Properties.ClassName.ValueOrDefault}");
            }
            catch (Exception ex)
            {
                lines.Add("focused: unavailable (" + ex.Message + ")");
            }

            // The real main window by class (MainWindowHandle may be a popup; the
            // window list above shows any popups separately).
            var handle = FindTopLevelWindowHandle();
            if (handle == IntPtr.Zero)
            {
                lines.Add("no main window");
                return;
            }
            var window = Automation.FromHandle(handle).AsWindow();

            foreach (var e in window.FindAllDescendants())
            {
                var expand = e.Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault;
                lines.Add($"{e.ControlType} id={e.Properties.AutomationId.ValueOrDefault} " +
                          $"name={e.Properties.Name.ValueOrDefault} offscreen={e.Properties.IsOffscreen.ValueOrDefault}" +
                          (expand is null ? string.Empty : $" expand={expand}"));
            }
            FlaUI.Core.Capturing.Capture.Element(window).ToFile(stem + ".png");
        }
        catch (Exception ex)
        {
            lines.Add("diagnostics incomplete: " + ex.Message);
        }
        finally
        {
            // Any exception type: an unwritable folder surfaces as
            // UnauthorizedAccessException, not IOException, and once replaced
            // the real failure this method exists to explain.
            try { File.WriteAllLines(stem + ".txt", lines); }
            catch (Exception) { /* nowhere left to report to */ }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    // ── Tray icon ───────────────────────────────────────────────────

    /// <summary>
    /// Finds BeNGy's notification-area icon. Windows 11 puts new icons in the
    /// "Show Hidden Icons" overflow flyout by default, which is why a plain
    /// desktop search never found it; this opens the flyout when needed.
    /// Taskbar identifiers (verified on this Windows build): the chevron is a
    /// Button with AutomationId "SystemTrayIcon" named "Show Hidden Icons";
    /// every app icon is AutomationId "NotifyItemIcon" named after its tooltip,
    /// sometimes with a leading space.
    /// </summary>
    public AutomationElement? FindTrayIcon(TimeSpan? timeout = null)
    {
        var desktop = Automation.GetDesktop();
        var cf = Automation.ConditionFactory;

        return Retry.WhileNull(
            () =>
            {
                var icon = FindBengyNotifyIcon(desktop);
                if (icon is not null) return icon;

                var chevron = desktop
                    .FindFirstChild(cf.ByClassName("Shell_TrayWnd"))
                    ?.FindFirstDescendant(cf.ByAutomationId("SystemTrayIcon").And(cf.ByName("Show Hidden Icons")));
                if (chevron is null) return null;

                chevron.AsButton().Invoke();
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(700));
                return FindBengyNotifyIcon(desktop);
            },
            timeout ?? TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(500)).Result;
    }

    private AutomationElement? FindBengyNotifyIcon(AutomationElement desktop) =>
        desktop.FindAllDescendants(Automation.ConditionFactory.ByAutomationId("NotifyItemIcon"))
            .FirstOrDefault(e => (e.Properties.Name.ValueOrDefault ?? string.Empty).Trim()
                .StartsWith("BeNGy", StringComparison.Ordinal));

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

    /// <summary>WinUI 3 desktop window class -- what MAUI's Windows head creates.</summary>
    private const string MainWindowClassName = "WinUIDesktopWin32WindowClass";

    private const int SW_HIDE = 0;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder className, int maxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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

        Retry.WhileTrue(HasVisibleMainWindow, effectiveTimeout, TimeSpan.FromMilliseconds(250));
        return !HasVisibleMainWindow();
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
