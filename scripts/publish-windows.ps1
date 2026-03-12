[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    [string]$ArtifactsDir,

    [string]$DotnetExe = $env:DOTNET_EXE,

    [switch]$AllowUnsupportedDotnetSdk
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "This script must be run on Windows."
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$UiDir = Join-Path $RepoRoot "src\NexusWorks.Guardian.UI"
$ProjectFile = Join-Path $UiDir "NexusWorks.Guardian.UI.csproj"

if ([string]::IsNullOrWhiteSpace($DotnetExe)) {
    $DotnetExe = "dotnet"
}

if ((Test-Path $DotnetExe) -or (Get-Command $DotnetExe -ErrorAction SilentlyContinue)) {
    $DotnetCommand = $DotnetExe
}
else {
    throw "dotnet is required. Set -DotnetExe or DOTNET_EXE if you want to use a non-default SDK path."
}

$DotnetSdkVersion = (& $DotnetCommand --version).Trim()

if (-not $DotnetSdkVersion.StartsWith("8.")) {
    if (-not $AllowUnsupportedDotnetSdk.IsPresent) {
        throw ".NET 8 SDK is required for Windows publish. Current SDK: $DotnetSdkVersion. Select .NET 8 with global.json or PATH, set -DotnetExe or DOTNET_EXE to a local .NET 8 SDK, or pass -AllowUnsupportedDotnetSdk to bypass this guard."
    }

    Write-Warning "Using unsupported SDK $DotnetSdkVersion for a net8.0 MAUI Windows publish."
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

    npm run test:hotkeys
    npm run tailwind:build

    & $DotnetCommand publish $ProjectFile `
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
