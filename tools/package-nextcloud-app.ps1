param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appId = "nextcloudshot"
$appDir = Join-Path $repoRoot "NextCloudShot.NextcloudApp"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingRoot = Join-Path $artifactsDir "nextcloud-app"
$stagingAppDir = Join-Path $stagingRoot $appId
$tarPath = Join-Path $artifactsDir "$appId.tar.gz"

if (-not (Test-Path $appDir)) {
    throw "Nextcloud app directory was not found: $appDir"
}

$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDir)
$resolvedStaging = [System.IO.Path]::GetFullPath($stagingRoot)
if (-not $resolvedStaging.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to package outside artifacts directory: $resolvedStaging"
}

if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingAppDir | Out-Null

$releaseItems = @(
    "appinfo",
    "css",
    "img",
    "js",
    "lib",
    "templates",
    "LICENSE",
    "README.md",
    "composer.json"
)

foreach ($item in $releaseItems) {
    $source = Join-Path $appDir $item
    if (-not (Test-Path $source)) {
        throw "Release item is missing: $source"
    }
    Copy-Item -LiteralPath $source -Destination $stagingAppDir -Recurse -Force
}

if (Test-Path $tarPath) {
    Remove-Item -LiteralPath $tarPath -Force
}

tar -czf $tarPath -C $stagingRoot $appId
Write-Host "Created $tarPath"
