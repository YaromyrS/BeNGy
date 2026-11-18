<#
.SYNOPSIS
    Installs, runs and uninstalls the release artifacts in a disposable
    Windows Sandbox: Setup.exe, the MSI, and the portable zip.

.DESCRIPTION
    The sandbox is a clean Windows without .NET 8 or the Windows App SDK, so
    this also proves the builds are self-contained. Networking is off. For
    each artifact it checks: silent install exit code, install location,
    Start menu shortcut, Apps & features entry, the app starting (main window
    created, still alive 5 s later), single instance (a second launch hands
    over and exits), and silent uninstall removing files and shortcut.
    A screenshot of the summoned palette is saved per artifact.

    Results: artifacts\sandbox-test-<version>\results.txt (+ logs, PNGs).
    Run tools\packaging\build-release.ps1 first.

.EXAMPLE
    powershell -File tools\packaging\test-installers-sandbox.ps1
#>
param(
    [string]$Version = '',
    [int]$TimeoutMinutes = 20,
    [switch]$KeepOpen
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Version) {
    [xml]$csproj = Get-Content -Raw (Join-Path $repo 'src\BeNGy\BeNGy.csproj')
    $Version = @($csproj.Project.PropertyGroup | ForEach-Object { $_.ApplicationDisplayVersion } | Where-Object { $_ })[0]
}
$artifacts = Join-Path $repo "artifacts\$Version"
foreach ($f in 'BeNGy-Setup-x64.exe', 'BeNGy-x64.msi', 'BeNGy-x64-portable.zip') {
    if (-not (Test-Path (Join-Path $artifacts $f))) { throw "Missing $f in $artifacts. Run build-release.ps1 first." }
}
if (-not (Test-Path (Join-Path $env:windir 'System32\WindowsSandbox.exe'))) { throw 'Windows Sandbox is not available.' }
if ((Get-Command wsb.exe -ErrorAction SilentlyContinue) -and ((wsb list) | Where-Object { "$_".Trim() })) {
    throw 'A Windows Sandbox session is already running; only one is allowed at a time.'
}

# Beside the artifacts, not inside: that folder is mapped read-only.
$work = Join-Path $repo "artifacts\sandbox-test-$Version"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null
# A MappedFolder without SandboxFolder appears on the guest desktop under the
# host folder's own name.
$guestDesk = 'C:\Users\WDAGUtilityAccount\Desktop'
$guestArt = "$guestDesk\$(Split-Path $artifacts -Leaf)"
$guestWork = "$guestDesk\$(Split-Path $work -Leaf)"

$guestScript = @'
$ErrorActionPreference = 'Continue'
$art = '{ART}'
$work = '{WORK}'
Start-Transcript -Path "$work\transcript.txt" -Force
$script:failed = 0
$results = New-Object System.Collections.Generic.List[string]
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $line = '{0}  {1}  {2}' -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $name, $detail
    $results.Add($line); Write-Host $line
    if (-not $ok) { $script:failed++ }
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class G {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    public static IntPtr FindTop(int pid) { IntPtr f = IntPtr.Zero;
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p != (uint)pid) return true;
            var sb = new StringBuilder(128); GetClassName(h, sb, 128);
            if (sb.ToString() == "WinUIDesktopWin32WindowClass") { f = h; return false; } return true; }, IntPtr.Zero);
        return f; }
}
"@
[void][G]::SetProcessDpiAwarenessContext([IntPtr]-4)

function Save-Screen([string]$path) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size); $g.Dispose()
    $bmp.Save($path); $bmp.Dispose()
}
Add-Type -AssemblyName System.Windows.Forms

function Test-Launch([string]$exe, [string]$label) {
    $p = Start-Process $exe -PassThru
    $sw = [Diagnostics.Stopwatch]::StartNew(); $hwnd = [IntPtr]::Zero
    while ($sw.Elapsed.TotalSeconds -lt 60 -and $hwnd -eq [IntPtr]::Zero -and -not $p.HasExited) {
        Start-Sleep -Milliseconds 500; $hwnd = [G]::FindTop($p.Id)
    }
    Check "$label - app starts (main window created)" ($hwnd -ne [IntPtr]::Zero) ('{0:N1} s' -f $sw.Elapsed.TotalSeconds)
    Start-Sleep -Seconds 5
    Check "$label - still running 5 s later" (-not $p.HasExited) $(if ($p.HasExited) { "exit code $($p.ExitCode)" } else { '' })
    # Starts minimized to the tray by default; a second launch summons the
    # running instance and must exit itself.
    $second = Start-Process $exe -PassThru
    $exited = $second.WaitForExit(15000)
    Check "$label - second launch hands over and exits" $exited $(if ($exited) { "exit code $($second.ExitCode)" } else { 'still running' })
    Start-Sleep -Seconds 3
    Save-Screen "$work\$($label -replace '[^A-Za-z0-9]', '-').png"
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    if (-not $second.HasExited) { Stop-Process -Id $second.Id -Force }
    Start-Sleep -Seconds 2
}

$installDir = "$env:LOCALAPPDATA\Programs\BeNGy"
$installed = "$installDir\BeNGy.exe"
$lnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\BeNGy.lnk"
# Where Apps & features gets its entry: Setup.exe (Burn) writes its own
# Uninstall key and hides the MSI; a per-user MSI on its own is registered
# with Windows Installer under HKCU\Software\Microsoft\Installer\Products.
function Get-Registration([string]$kind) {
    if ($kind -eq 'bundle') {
        Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
            Get-ItemProperty | Where-Object { $_.DisplayName -eq 'BeNGy' -and -not $_.SystemComponent } |
            ForEach-Object { "$($_.DisplayName) $($_.DisplayVersion)" }
    } else {
        Get-ChildItem 'HKCU:\Software\Microsoft\Installer\Products' -ErrorAction SilentlyContinue |
            Get-ItemProperty | Where-Object { $_.ProductName -eq 'BeNGy' } | ForEach-Object { $_.ProductName }
    }
}
function Check-Installed([string]$label, [string]$kind) {
    $found = Test-Path $installed
    $where = if ($found) { '' } else { (Get-ChildItem $env:LOCALAPPDATA, $env:ProgramFiles -Recurse -Filter BeNGy.exe -ErrorAction SilentlyContinue | Select-Object -First 1).FullName }
    Check "$label - installed to %LocalAppData%\Programs\BeNGy" $found $(if ($where) { "found at $where" } else { '' })
    Check "$label - Start menu shortcut" (Test-Path $lnk)
    $reg = @(Get-Registration $kind)
    Check "$label - Apps & features entry (per user)" ($reg.Count -ge 1) ($reg -join '; ')
}
function Check-Removed([string]$label, [string]$kind) {
    Check "$label - install folder removed entirely" (-not (Test-Path $installDir)) $(if (Test-Path $installDir) { "$(@(Get-ChildItem $installDir -Recurse).Count) items left" } else { '' })
    Check "$label - shortcut removed" (-not (Test-Path $lnk))
    Check "$label - Apps & features entry removed" (@(Get-Registration $kind).Count -eq 0)
}

# -- Setup.exe (Burn bundle) --------------------------------------------------
$setup = "$art\BeNGy-Setup-x64.exe"
$p = Start-Process $setup -ArgumentList '/quiet', '/norestart', '/log', "$work\setup-install.log" -Wait -PassThru
Check 'Setup.exe - silent install' ($p.ExitCode -eq 0) "exit code $($p.ExitCode)"
Check-Installed 'Setup.exe' 'bundle'
if (Test-Path $installed) { Test-Launch $installed 'Setup.exe' }
$p = Start-Process $setup -ArgumentList '/uninstall', '/quiet', '/norestart', '/log', "$work\setup-uninstall.log" -Wait -PassThru
Check 'Setup.exe - silent uninstall' ($p.ExitCode -eq 0) "exit code $($p.ExitCode)"
Check-Removed 'Setup.exe' 'bundle'

# -- MSI ----------------------------------------------------------------------
$msi = "$art\BeNGy-x64.msi"
$p = Start-Process msiexec.exe -ArgumentList '/i', "`"$msi`"", '/qn', '/norestart', '/l*v', "`"$work\msi-install.log`"" -Wait -PassThru
Check 'MSI - silent install' ($p.ExitCode -eq 0) "exit code $($p.ExitCode)"
Check-Installed 'MSI' 'msi'
if (Test-Path $installed) { Test-Launch $installed 'MSI' }
$p = Start-Process msiexec.exe -ArgumentList '/x', "`"$msi`"", '/qn', '/norestart', '/l*v', "`"$work\msi-uninstall.log`"" -Wait -PassThru
Check 'MSI - silent uninstall' ($p.ExitCode -eq 0) "exit code $($p.ExitCode)"
Check-Removed 'MSI' 'msi'

# -- Portable zip ---------------------------------------------------------------
$portable = "$env:TEMP\bengy-portable"
Expand-Archive "$art\BeNGy-x64-portable.zip" $portable -Force
Check 'Portable - zip extracts BeNGy.exe' (Test-Path "$portable\BeNGy.exe")
if (Test-Path "$portable\BeNGy.exe") { Test-Launch "$portable\BeNGy.exe" 'Portable' }

$results | Set-Content "$work\results.txt"
"failed=$script:failed" | Set-Content "$work\DONE.txt"
Stop-Transcript
'@
$guestScript = $guestScript.Replace('{ART}', $guestArt).Replace('{WORK}', $guestWork)
Set-Content -Path (Join-Path $work 'run-tests.ps1') -Value $guestScript -Encoding ascii

$wsb = @"
<Configuration>
  <vGPU>Enable</vGPU>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MemoryInMB>4096</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$artifacts</HostFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$work</HostFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -ExecutionPolicy Bypass -File "$guestWork\run-tests.ps1"</Command>
  </LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $work 'installers.wsb'
Set-Content -Path $wsbPath -Value $wsb -Encoding utf8

Write-Host "Launching Windows Sandbox for $Version (minimize it and keep working)..."
Start-Process $wsbPath
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$done = Join-Path $work 'DONE.txt'
while ((Get-Date) -lt $deadline -and -not (Test-Path $done)) { Start-Sleep -Seconds 10 }

function Close-Sandbox {
    if (Get-Command wsb.exe -ErrorAction SilentlyContinue) {
        foreach ($id in (wsb list)) { $id = "$id".Trim(); if ($id) { wsb stop --id $id 2>$null | Out-Null } }
    }
}
if (-not (Test-Path $done)) {
    Write-Warning "No result after $TimeoutMinutes minutes. See $work\transcript.txt"
    if (-not $KeepOpen) { Close-Sandbox }
    exit 2
}
Get-Content (Join-Path $work 'results.txt')
if (-not $KeepOpen) { Close-Sandbox }
$failed = [int]((Get-Content $done) -replace 'failed=', '')
Write-Host "Failed checks: $failed  (details: $work)"
exit $failed
