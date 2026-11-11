using BeNGy.Services.Interfaces;
using H.NotifyIcon.Core;

namespace BeNGy.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="ITrayService"/> using H.NotifyIcon.Core's
/// Win32 popup menu rather than the WinUI XAML flyout. The flyout variant hosts
/// its menu on a hidden XAML window, which is unreliable on unpackaged WinAppSDK
/// apps (menu items not responding); TrackPopupMenu has no such dependency.
/// </summary>
public class TrayService : ITrayService
{
    private TrayIconWithContextMenu? _trayIcon;
    private System.Drawing.Icon? _icon;   // keeps the HICON alive for the tray's lifetime
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon != null) return;

        // The icon's GUID is hashed from this name, and Windows ties a tray GUID
        // to the executable's path (NOTIFYICONDATA guidItem). A Store (MSIX)
        // install moves to a new versioned folder on every update, so the path
        // is part of the seed or the icon could fail to appear after an update.
        _trayIcon = new TrayIconWithContextMenu($"BeNGy|{Environment.ProcessPath}")
        {
            ContextMenu = new PopupMenu
            {
                Items =
                {
                    // Bold "default action" styling is not available on a Win32
                    // menu; Open stays first, which is the convention that matters.
                    new PopupMenuItem("Open", (_, _) => Raise(OpenRequested)),
                    new PopupMenuItem("Settings", (_, _) => Raise(SettingsRequested)),
                    new PopupMenuSeparator(),
                    new PopupMenuItem("Exit", (_, _) => Raise(ExitRequested)),
                },
            },
        };

        _trayIcon.UpdateToolTip("BeNGy");

        // Load tray icon from embedded resources
        var iconStream = GetTrayIconStream();
        if (iconStream != null)
        {
            _icon = new System.Drawing.Icon(iconStream);
            _trayIcon.UpdateIcon(_icon.Handle);
        }

        // Double-click tray icon to show window (AC #3 of Story 1.4)
        _trayIcon.MessageWindow.MouseEventReceived += (_, e) =>
        {
            if (e.MouseEvent is MouseEvent.IconDoubleClick or MouseEvent.IconLeftDoubleClick)
                Raise(OpenRequested);
        };

        _trayIcon.Create();
    }

    /// <summary>
    /// Menu callbacks arrive on the tray's message-loop thread; every subscriber
    /// touches AppWindow/XAML, so marshal to the UI thread before raising.
    /// </summary>
    private void Raise(EventHandler? handler)
    {
        MainThread.BeginInvokeOnMainThread(() => handler?.Invoke(this, EventArgs.Empty));
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

    public void ShowIcon() => _trayIcon?.Show();

    public void HideIcon() => _trayIcon?.Hide();

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
            _trayIcon?.TryRemove();
            _trayIcon?.Dispose();
            _trayIcon = null;
            _icon?.Dispose();
            _icon = null;
        }
        _disposed = true;
    }

    public void ShowBalloon(string title, string message)
    {
        _trayIcon?.ShowNotification(title, message, NotificationIcon.Info);
    }
}
