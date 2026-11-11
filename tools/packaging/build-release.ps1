<#
.SYNOPSIS
    Builds the BeNGy release artifacts into artifacts\<version>\.

.DESCRIPTION
    Website downloads (attach to the GitHub release; the site links to
    releases/latest/download/<name>, so these names must not change):
      BeNGy-Setup-x64.exe       WiX bundle that installs the MSI (recommended)
      BeNGy-x64.msi             per-user MSI, %LocalAppData%\Programs\BeNGy
      BeNGy-x64-portable.zip    the app folder; run BeNGy.exe from anywhere
      SHA256SUMS.txt
    With -Msix, also the Microsoft Store upload:
      BeNGy_<version>.0_x64.msix   unsigned; Partner Center re-signs it.
      Needs the Partner Center identity in Package.appxmanifest first
      (docs/store-release.md).

    The version comes from ApplicationDisplayVersion in src\BeNGy\BeNGy.csproj.
    Installers are not code-signed: SmartScreen warns on first run.

.EXAMPLE
    powershell -File tools\packaging\build-release.ps1 -Msix
#>
param([switch]$Msix)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repo 'src\BeNGy\BeNGy.csproj'
$tfm = 'net8.0-windows10.0.19041.0'

[xml]$csproj = Get-Content -Raw $project
$version = @($csproj.Project.PropertyGroup | ForEach-Object { $_.ApplicationDisplayVersion } | Where-Object { $_ })[0]
if (-not $version) { throw "ApplicationDisplayVersion not found in $project" }

$out = Join-Path $repo "artifacts\$version"
$publish = Join-Path $out 'publish\win-x64\'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

function Invoke-Step([string]$name, [scriptblock]$step) {
    Write-Host "== $name"
    & $step
    if ($LASTEXITCODE) { throw "$name failed (exit $LASTEXITCODE)" }
}

Invoke-Step "Publish app $version (win-x64, self-contained, ReadyToRun)" {
    dotnet publish $project -c Release -f $tfm -r win-x64 --self-contained `
        -p:PublishReadyToRun=true -o $publish -v minimal
}

Write-Host "== Portable zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $publish.TrimEnd('\'), (Join-Path $out 'BeNGy-x64-portable.zip'),
    [IO.Compression.CompressionLevel]::Optimal, $false)

Invoke-Step 'MSI' {
    dotnet build (Join-Path $repo 'installer\BeNGy.Msi.wixproj') -c Release `
        "-p:PublishDir=$publish" "-p:Version=$version" "-p:OutputPath=$out\msi\" -v minimal
}
Copy-Item (Join-Path $out 'msi\BeNGy-x64.msi') $out

# The bundle needs a real .ico; MAUI generated this one from appicon.svg
# during the Release publish above.
$icon = Join-Path $repo "src\BeNGy\obj\Release\$tfm\win-x64\resizetizer\r\appicon.ico"
if (-not (Test-Path $icon)) { throw "App icon not generated: $icon" }

Invoke-Step 'Setup.exe' {
    dotnet build (Join-Path $repo 'installer\BeNGy.Setup.wixproj') -c Release `
        "-p:MsiPath=$out\BeNGy-x64.msi" "-p:IconFile=$icon" "-p:Version=$version" `
        "-p:OutputPath=$out\setup\" -v minimal
}
Copy-Item (Join-Path $out 'setup\BeNGy-Setup-x64.exe') $out

if ($Msix) {
    Invoke-Step 'MSIX (Store upload, unsigned)' {
        dotnet publish $project -c Release -f $tfm -p:RuntimeIdentifierOverride=win-x64 `
            -p:WindowsPackageType=MSIX -p:AppxPackageSigningEnabled=false `
            "-p:AppxPackageDir=$out\msix\" -v minimal
    }
    Get-ChildItem (Join-Path $out 'msix') -Recurse -Filter 'BeNGy_*_x64.msix' |
        Select-Object -First 1 | Copy-Item -Destination $out
}

Write-Host '== Checksums'
$files = Get-ChildItem $out -File | Where-Object { $_.Extension -in '.exe', '.msi', '.zip', '.msix' }
$files | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } |
    Set-Content -Encoding ascii (Join-Path $out 'SHA256SUMS.txt')

Write-Host "== Done: $out"
$files | ForEach-Object { "{0,8:N1} MB  {1}" -f ($_.Length / 1MB), $_.Name }
