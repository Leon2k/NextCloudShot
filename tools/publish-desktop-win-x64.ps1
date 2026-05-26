param(
    [string]$Configuration = "Release",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "NextCloudShot.Desktop\NextCloudShot.Desktop.csproj"
$publishDir = Join-Path $repoRoot "artifacts\desktop-win-x64"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

if ($FrameworkDependent) {
    Write-Warning "Publishing framework-dependent output. Install .NET Desktop Runtime 8 on target machines."
}

dotnet publish $project `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $selfContained `
    --output $publishDir `
    -p:PublishSingleFile=false

Write-Host "Published NextCloudShot Desktop to $publishDir"
