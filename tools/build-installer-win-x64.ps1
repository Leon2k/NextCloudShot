param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$issPath = Join-Path $repoRoot "installer\windows\NextCloudShot.iss"

& (Join-Path $PSScriptRoot "publish-desktop-win-x64.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "publish-desktop-win-x64.ps1 failed with exit code $LASTEXITCODE"
}

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$iscc = if ($isccCommand) { $isccCommand.Source } else { $null }
if (-not $iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $iscc) {
    throw "ISCC.exe was not found. Install Inno Setup 6 or run: winget install --id JRSoftware.InnoSetup"
}

& $iscc $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE"
}
