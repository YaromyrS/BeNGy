using BeNGy.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using MuxControls = Microsoft.UI.Xaml.Controls;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="ITrayService"/> using H.NotifyIcon.WinUI.
/// Manages the system tray icon, tooltip, and context menu.
/// </summary>
public class TrayService : ITrayService
{
    private TaskbarIcon? _trayIcon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon != null) return;

        _trayIcon = new TaskbarIcon();
        _trayIcon.ToolTipText = "BeNGy";
        _trayIcon.ContextFlyout = CreateContextMenu();

        // Double-click tray icon to show window (AC #3 of Story 1.4)
        _trayIcon.DoubleClickCommand = new RelayCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty));

        // Load tray icon from embedded resources
        var iconStream = GetTrayIconStream();
        if (iconStream != null)
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconStream);
        }

        _trayIcon.ForceCreate();
    }

    private static Stream? GetTrayIconStream()
    {
        try
        {
            // Try to load from MAUI raw assets
            var task = FileSystem.OpenAppPackageFileAsync("trayicon.ico");
            task.Wait();
            return task.Result;
        }
        catch
        {
            return null;
        }
    }

    public void ShowIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
    }

    public void HideIcon()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        }
        _disposed = true;
    }

    public void ShowBalloon(string title, string message)
    {
        _trayIcon?.ShowNotification(title, message, NotificationIcon.Info);
    }

    private MuxControls.MenuFlyout CreateContextMenu()
    {
        var menu = new MuxControls.MenuFlyout();

        var openItem = new MuxControls.MenuFlyoutItem
        {
            Text = "Open",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold  // Primary/default action (Story 5.3)
        };
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new MuxControls.MenuFlyoutItem { Text = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new MuxControls.MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new MuxControls.MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }
}
