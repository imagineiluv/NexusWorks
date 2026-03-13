[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64",

    [string]$ArtifactsDir,

    [string]$DotnetExe = $env:DOTNET_EXE,

    [switch]$AllowUnsupportedDotnetSdk,

    [switch]$SkipPrereqCheck
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "This script must be run on Windows."
}

$RepoRoot = Split-Path -Parent $PSScriptRoot

function Get-GitMetadataValue {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Commit", "Branch", "Dirty")]
        [string]$Kind
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return "unknown"
    }

    try {
        $null = & git -C $RepoRoot rev-parse --is-inside-work-tree 2>$null
    }
    catch {
        return "unknown"
    }

    switch ($Kind) {
        "Commit" { return (& git -C $RepoRoot rev-parse HEAD).Trim() }
        "Branch" { return (& git -C $RepoRoot rev-parse --abbrev-ref HEAD).Trim() }
        "Dirty" {
            $statusOutput = (& git -C $RepoRoot status --porcelain --untracked-files=no | Out-String).Trim()
            if ([string]::IsNullOrWhiteSpace($statusOutput)) {
                return "false"
            }

            return "true"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $ArtifactsDir = Join-Path $RepoRoot "artifacts\windows\$Timestamp"
}

if ([string]::IsNullOrWhiteSpace($DotnetExe)) {
    $DotnetExe = "dotnet"
}

if (-not $SkipPrereqCheck.IsPresent) {
    & (Join-Path $RepoRoot "scripts\check-windows-dotnet8-prereqs.ps1") -DotnetExe $DotnetExe
}

& node --test (Join-Path $RepoRoot "scripts\tests\release-scripts.test.mjs")

& (Join-Path $RepoRoot "scripts\publish-windows.ps1") `
    -Configuration $Configuration `
    -Architecture $Architecture `
    -ArtifactsDir $ArtifactsDir `
    -DotnetExe $DotnetExe `
    -AllowUnsupportedDotnetSdk:$AllowUnsupportedDotnetSdk.IsPresent

$DotnetVersion = (& $DotnetExe --version).Trim()

node (Join-Path $RepoRoot "scripts\write-release-manifest.mjs") `
    --artifacts-dir $ArtifactsDir `
    --platform windows `
    --metadata "configuration=$Configuration" `
    --metadata "architecture=$Architecture" `
    --metadata "runtimeIdentifier=win10-$Architecture" `
    --metadata "dotnetVersion=$DotnetVersion" `
    --metadata "gitCommit=$(Get-GitMetadataValue -Kind Commit)" `
    --metadata "gitBranch=$(Get-GitMetadataValue -Kind Branch)" `
    --metadata "gitDirty=$(Get-GitMetadataValue -Kind Dirty)" `
    --metadata "workflow=release-windows"

node (Join-Path $RepoRoot "scripts\write-release-summary.mjs") `
    --artifacts-dir $ArtifactsDir
