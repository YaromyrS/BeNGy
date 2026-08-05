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
    as a host run (48/65 as of 2026-08-05), so it is a faithful environment.

.NOTES
    Requires the Windows Sandbox feature (Windows 10/11 Pro or Enterprise) and
    prebuilt Debug binaries: dotnet build BeNGy.sln
    Only one Windows Sandbox instance can run at a time.
#>
[CmdletBinding()]
param(
    # Maximum minutes to wait for the run before giving up. The suite itself is
    # capped at 15 minutes by e2e.runsettings; boot adds about a minute.
    [int]$TimeoutMinutes = 25
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
if (Get-Process WindowsSandbox* -ErrorAction SilentlyContinue) {
    throw 'A Windows Sandbox instance is already running; only one is allowed at a time. Close it and retry.'
}

# ── Writable work dir (guest writes results here through the mapping) ────────
$work = Join-Path $env:TEMP 'bengy-sandbox-e2e'
$workLeaf = Split-Path $work -Leaf
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

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

"BENGY_EXE_PATH exists: $(Test-Path $env:BENGY_EXE_PATH)"
Set-Location $work

& "$desk\dotnet\dotnet.exe" test "$desk\{REPO}\{TESTDLL}" `
    --logger 'trx;LogFileName=sandbox.trx' `
    --results-directory "$work\TestResults" `
    --settings "$desk\{REPO}\tests\BeNGy.E2ETests\e2e.runsettings" `
    *>&1 | Tee-Object -FilePath "$work\test-output.txt"

"exit=$LASTEXITCODE" | Set-Content "$work\DONE.txt"
Stop-Transcript
'@
$guestScript = $guestScript.
    Replace('{DESK}', $guestDesk).
    Replace('{WORK}', $workLeaf).
    Replace('{REPO}', $repoLeaf).
    Replace('{APPEXE}', $appExeRel).
    Replace('{TESTDLL}', $testDllRel)
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
    exit 2
}

# ── Report ───────────────────────────────────────────────────────────────────
$trx = Join-Path $work 'TestResults\sandbox.trx'
if (Test-Path $trx) {
    $dest = Join-Path $testDir 'TestResults'
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item $trx (Join-Path $dest 'sandbox.trx') -Force
    [xml]$x = Get-Content $trx
    $c = $x.TestRun.ResultSummary.Counters
    Write-Host ("Result: {0} total, {1} passed, {2} failed  (TRX copied to {3})" -f $c.total, $c.passed, $c.failed, (Join-Path $dest 'sandbox.trx'))
} else {
    Write-Warning "Run finished but no TRX was produced. See $work\test-output.txt"
}

# The sandbox stays open for inspection; close its window to discard the VM.
$guestExit = ((Get-Content $doneFile) -replace 'exit=', '')
exit ([int]$guestExit)
