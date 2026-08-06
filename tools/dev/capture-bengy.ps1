param(
    [string]$Exe = (Join-Path (git rev-parse --show-toplevel) "src\BeNGy\bin\Debug\net8.0-windows10.0.19041.0\win-x64\BeNGy.exe"),
    [Parameter(Mandatory = $true)][string]$Out
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class K {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int c);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    public const byte VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_B = 0x42;
    public const uint KEYUP = 0x0002;
}
"@
[void][K]::SetProcessDPIAware()
Get-Process BeNGy -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(3000) }
Start-Sleep -Milliseconds 1500
Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe)
Start-Sleep -Seconds 5
function Get-Bengy { Get-Process BeNGy -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1 }
$p = Get-Bengy
if (-not $p) {
    # Start-minimized default: summon via the global hotkey.
    [K]::keybd_event([K]::VK_CONTROL,0,0,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_SHIFT,0,0,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_B,0,0,[UIntPtr]::Zero)
    Start-Sleep -Milliseconds 120
    [K]::keybd_event([K]::VK_B,0,[K]::KEYUP,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_SHIFT,0,[K]::KEYUP,[UIntPtr]::Zero); [K]::keybd_event([K]::VK_CONTROL,0,[K]::KEYUP,[UIntPtr]::Zero)
    Start-Sleep -Seconds 3
    $p = Get-Bengy
}
if (-not $p) { Write-Error "No visible BeNGy window"; exit 1 }
[void][K]::SetForegroundWindow($p.MainWindowHandle)
Start-Sleep -Milliseconds 900
$r = New-Object K+RECT
if ([K]::DwmGetWindowAttribute($p.MainWindowHandle, 9, [ref]$r, [System.Runtime.InteropServices.Marshal]::SizeOf($r)) -ne 0) { [void][K]::GetWindowRect($p.MainWindowHandle, [ref]$r) }
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$g.Dispose(); $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
"captured ${w}x${h} -> $Out"
