[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    [string]$ArtifactsDir
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "This script must be run on Windows."
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$UiDir = Join-Path $RepoRoot "src\NexusWorks.Guardian.UI"
$ProjectFile = Join-Path $UiDir "NexusWorks.Guardian.UI.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet is required."
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required."
}

if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $ArtifactsDir = Join-Path $RepoRoot "artifacts\windows\$Timestamp"
}

$PublishDir = Join-Path $ArtifactsDir "publish"
$ZipPath = Join-Path $ArtifactsDir ("NexusWorks.Guardian-win-" + $Architecture + ".zip")
$RuntimeIdentifier = "win10-$Architecture"

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Push-Location $UiDir

try {
    if (Test-Path (Join-Path $UiDir "package-lock.json")) {
        npm ci
    }
    else {
        npm install
    }

    npm run tailwind:build

    dotnet publish $ProjectFile `
        -f net8.0-windows10.0.19041.0 `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained `
        -p:RuntimeIdentifierOverride=$RuntimeIdentifier `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:GenerateAppxPackageOnBuild=false `
        -o $PublishDir
}
finally {
    Pop-Location
}

Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal

Write-Host "Windows publish complete."
Write-Host "Artifacts: $ArtifactsDir"
Get-ChildItem -Path $ArtifactsDir -File | Select-Object FullName, Length
