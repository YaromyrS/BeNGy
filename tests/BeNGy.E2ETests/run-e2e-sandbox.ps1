<#
.SYNOPSIS
    Runs the BeNGy E2E suite inside Windows Sandbox so it cannot steal focus,
    keystrokes, or the clipboard from your desktop session.

.DESCRIPTION
    The UI tests drive a real desktop: they synthesize keystrokes (including the
    global Ctrl+Shift+B hotkey), steal foreground focus, and overwrite the system
    clipboard. Run on your own session, that makes the machine unusable for the
    duration. This script runs them in a disposable Windows Sandbox VM instead:

      - the repo and the host's .NET install are mapped in read-only
        (nothing to download, works with networking disabled),
      - clipboard redirection is disabled, so the host clipboard is untouched,
      - the app's preferences live in the sandbox's throwaway profile, so the
        suite's known settings mutations never reach your real settings,
      - results are written to a writable mount and copied to
        TestResults\sandbox.trx when the run finishes.

    Verified baseline: the sandbox produces bit-for-bit the same pass/fail set
    as a host run (checked 2026-08-05), so it is a faithful environment. Since
    2026-09-27 the expected result is ALL tests passing (65/65); any failure is
    a regression. A busy host (CPU pegged, or a Moonlight stream) poisons runs
    with app-launch timeouts: rerun on a quiet machine before believing one.

.NOTES
    Requires the Windows Sandbox feature (Windows 10/11 Pro or Enterprise) and
    prebuilt Debug binaries: dotnet build BeNGy.sln
    Only one Windows Sandbox instance can run at a time.
#>
[CmdletBinding()]
param(
    # Maximum minutes to wait for the run before giving up. The suite itself is
    # capped at 15 minutes by e2e.runsettings; boot adds about a minute.
    [int]$TimeoutMinutes = 25,

    # Keep the sandbox VM open after the run for manual inspection. Default is
    # to close it: a forgotten open sandbox makes the next invocation throw
    # "already running", and it is easy to then misread the previous run's TRX
    # as the new result.
    [switch]$KeepOpen,

    # Optional dotnet test --filter expression for a targeted run, e.g.
    # "FullyQualifiedName~LayoutFitTests".
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'

# ── Resolve paths ────────────────────────────────────────────────────────────
$testDir = $PSScriptRoot
$repo = (Resolve-Path (Join-Path $testDir '..\..')).Path
$repoLeaf = Split-Path $repo -Leaf

$appExeRel = 'src\BeNGy\bin\Debug\net8.0-windows10.0.19041.0\win-x64\BeNGy.exe'
$testDllRel = 'tests\BeNGy.E2ETests\bin\Debug\net8.0-windows10.0.19041.0\win-x64\BeNGy.E2ETests.dll'
$dotnetDir = 'C:\Program Files\dotnet'

# ── Preconditions ────────────────────────────────────────────────────────────
$sandboxExe = Join-Path $env:windir 'System32\WindowsSandbox.exe'
if (-not (Test-Path $sandboxExe)) {
    throw 'Windows Sandbox is not available. Enable the "Windows Sandbox" optional feature (requires Pro/Enterprise and a reboot).'
}
if (-not (Test-Path (Join-Path $repo $appExeRel))) {
    throw "App binary not found. Build first: dotnet build `"$repo\BeNGy.sln`""
}
if (-not (Test-Path (Join-Path $repo $testDllRel))) {
    throw "Test binary not found. Build first: dotnet build `"$repo\BeNGy.sln`""
}
# Only one sandbox SESSION can run at a time. On Win11 24H2+ the authoritative
# check is `wsb list`: WindowsSandboxServer.exe keeps running with no session
# active, so a bare process-name check reports false "already running". Older
# builds have no wsb.exe; there the client process check is the best available.
function Test-SandboxSessionActive {
    if (Get-Command wsb.exe -ErrorAction SilentlyContinue) {
        return [bool]((wsb list) | Where-Object { "$_".Trim() })
    }
    return [bool](Get-Process WindowsSandbox* -ErrorAction SilentlyContinue)
}

function Close-SandboxSession {
    if (Get-Command wsb.exe -ErrorAction SilentlyContinue) {
        foreach ($id in (wsb list)) {
            $id = "$id".Trim()
            if ($id) { wsb stop --id $id 2>$null | Out-Null }
        }
    }
    # Client/session processes only -- WindowsSandboxServer is a standing
    # service process on 24H2+ and is not ours to kill.
    Get-Process WindowsSandboxClient, WindowsSandboxRemoteSession, WindowsSandbox -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_.CloseMainWindow() | Out-Null; if (-not $_.WaitForExit(5000)) { $_.Kill() } } catch { }
    }
}

if (Test-SandboxSessionActive) {
    throw 'A Windows Sandbox session is already running; only one is allowed at a time. Close it (wsb stop) and retry.'
}

# ── Writable work dir (guest writes results here through the mapping) ────────
# Best-effort cleanup of this run's default dir and leftovers from earlier
# runs. A zombie VM from a timed-out run can keep files in its mapped work dir
# locked long after the session is gone; leave such a dir behind and use a
# fresh suffixed one rather than dying on Remove-Item.
Get-ChildItem $env:TEMP -Directory -Filter 'bengy-sandbox-e2e*' -ErrorAction SilentlyContinue | ForEach-Object {
    try { Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop } catch { }
}
$work = Join-Path $env:TEMP 'bengy-sandbox-e2e'
if (Test-Path $work) {
    $work = Join-Path $env:TEMP ('bengy-sandbox-e2e-' + (Get-Date -Format 'HHmmss'))
}
New-Item -ItemType Directory -Path $work | Out-Null
$workLeaf = Split-Path $work -Leaf
Write-Host "Work dir: $work"

$guestDesk = 'C:\Users\WDAGUtilityAccount\Desktop'

# ── Guest bootstrap script ───────────────────────────────────────────────────
# Single-quoted here-string: placeholders are substituted explicitly below, and
# everything else (including guest-side $env references) is left literal.
$guestScript = @'
$ErrorActionPreference = 'Continue'
$desk = '{DESK}'
$work = "$desk\{WORK}"
Start-Transcript -Path "$work\transcript.txt" -Force

$env:DOTNET_ROOT = "$desk\dotnet"
$env:PATH = "$desk\dotnet;" + $env:PATH
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:BENGY_EXE_PATH = "$desk\{REPO}\{APPEXE}"
# Guest-LOCAL path on purpose: the app appends to this file from inside a
# window procedure, and a stall on the mapped-folder share would freeze the
# app's UI thread. Copied to the mapped work dir after the run.
$env:BENGY_DRAG_LOG = "$env:TEMP\bengy-drag-log.txt"
# Failure screenshots / UIA outlines from AppTestFixture.CaptureDiagnostics. On
# the mapped work dir so they survive the VM; written only on failure paths.
$env:BENGY_DIAG_DIR = "$work\diag"

"BENGY_EXE_PATH exists: $(Test-Path $env:BENGY_EXE_PATH)"
Set-Location $work

# Record the display scale the suite ran at. The sandbox's remote-session
# display is locked to the host-derived scale: it offers no alternative
# scales (DISPLAYCONFIG get-DPI reports min=cur=max=0), refuses changes
# (ACCESS_DENIED), and silently ignores the legacy SPI_SETLOGICALDPIOVERRIDE.
# Layout bugs that only exist on scaled displays cannot be reproduced here;
# check those on a real scaled display.
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class GuestDpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public static uint PrimaryDpi() { uint x, y; GetDpiForMonitor(MonitorFromPoint(new POINT(), 1), 0, out x, out y); return x; }
}
"@
[GuestDpi]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null
"$([int]([GuestDpi]::PrimaryDpi() * 100 / 96))%" | Set-Content "$work\scale.txt"

$filterArgs = @()
if ('{FILTER}') { $filterArgs = @('--filter', '{FILTER}') }

& "$desk\dotnet\dotnet.exe" test "$desk\{REPO}\{TESTDLL}" `
    --logger 'trx;LogFileName=sandbox.trx' `
    --results-directory "$work\TestResults" `
    --settings "$desk\{REPO}\tests\BeNGy.E2ETests\e2e.runsettings" `
    @filterArgs `
    *>&1 | Tee-Object -FilePath "$work\test-output.txt"

if (Test-Path $env:BENGY_DRAG_LOG) { Copy-Item $env:BENGY_DRAG_LOG "$work\drag-log.txt" -Force }
"exit=$LASTEXITCODE" | Set-Content "$work\DONE.txt"
Stop-Transcript
'@
$guestScript = $guestScript.
    Replace('{DESK}', $guestDesk).
    Replace('{WORK}', $workLeaf).
    Replace('{REPO}', $repoLeaf).
    Replace('{APPEXE}', $appExeRel).
    Replace('{TESTDLL}', $testDllRel).
    Replace('{FILTER}', $Filter.Replace("'", "''"))
Set-Content -Path (Join-Path $work 'run-e2e.ps1') -Value $guestScript -Encoding utf8

# ── Sandbox configuration ────────────────────────────────────────────────────
$wsb = @"
<Configuration>
  <vGPU>Enable</vGPU>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MemoryInMB>6144</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$repo</HostFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$dotnetDir</HostFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$work</HostFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -ExecutionPolicy Bypass -File "$guestDesk\$workLeaf\run-e2e.ps1"</Command>
  </LogonCommand>
</Configuration>
"@
$wsbPath = Join-Path $work 'bengy-e2e.wsb'
Set-Content -Path $wsbPath -Value $wsb -Encoding utf8

# ── Launch and wait ──────────────────────────────────────────────────────────
Write-Host "Launching Windows Sandbox (minimize its window and keep working)..."
Start-Process $wsbPath

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$doneFile = Join-Path $work 'DONE.txt'
while ((Get-Date) -lt $deadline -and -not (Test-Path $doneFile)) {
    Start-Sleep -Seconds 15
}

if (-not (Test-Path $doneFile)) {
    Write-Warning "No result after $TimeoutMinutes minutes. Transcript: $work\transcript.txt"
    # Close the session even on timeout: a leaked VM blocks every later run
    # with "already running" while its stale TRX invites misreading.
    if (-not $KeepOpen) { Close-SandboxSession }
    exit 2
}

$scaleFile = Join-Path $work 'scale.txt'
if (Test-Path $scaleFile) { Write-Host "Guest display scale: $((Get-Content $scaleFile).Trim())" }

# ── Report ───────────────────────────────────────────────────────────────────
$trx = Join-Path $work 'TestResults\sandbox.trx'
if (Test-Path $trx) {
    $dest = Join-Path $testDir 'TestResults'
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item $trx (Join-Path $dest 'sandbox.trx') -Force
    $dragLog = Join-Path $work 'drag-log.txt'
    if (Test-Path $dragLog) { Copy-Item $dragLog (Join-Path $dest 'drag-log.txt') -Force }
    [xml]$x = Get-Content $trx
    $c = $x.TestRun.ResultSummary.Counters
    Write-Host ("Result: {0} total, {1} passed, {2} failed  (TRX copied to {3})" -f $c.total, $c.passed, $c.failed, (Join-Path $dest 'sandbox.trx'))
} else {
    Write-Warning "Run finished but no TRX was produced. See $work\test-output.txt"
}

if (-not $KeepOpen) {
    Close-SandboxSession
}

$guestExit = ((Get-Content $doneFile) -replace 'exit=', '')
exit ([int]$guestExit)
