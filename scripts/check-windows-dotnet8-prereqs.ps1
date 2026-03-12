[CmdletBinding()]
param(
    [string]$DotnetExe = $env:DOTNET_EXE
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "This script must be run on Windows."
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ExpectedSdkVersion = "8.0.416"

if ([string]::IsNullOrWhiteSpace($DotnetExe)) {
    $DotnetExe = "dotnet"
}

if ((Test-Path $DotnetExe) -or (Get-Command $DotnetExe -ErrorAction SilentlyContinue)) {
    $DotnetCommand = $DotnetExe
}
else {
    throw "dotnet is required. Set -DotnetExe or DOTNET_EXE if you want to use a non-default SDK path."
}

Push-Location $RepoRoot

try {
    $CurrentSdkVersion = (& $DotnetCommand --version).Trim()
    $WorkloadsOutput = (& $DotnetCommand workload list | Out-String).TrimEnd()

    Write-Host "Repository: $RepoRoot"
    Write-Host "global.json: $(Join-Path $RepoRoot 'global.json')"
    Write-Host "dotnet: $DotnetCommand"
    Write-Host "Selected SDK: $CurrentSdkVersion"
    Write-Host ""
    Write-Host "Installed workloads for the selected SDK:"
    Write-Host $WorkloadsOutput
    Write-Host ""

    if (-not $CurrentSdkVersion.Equals($ExpectedSdkVersion, [System.StringComparison]::Ordinal)) {
        Write-Host "status: sdk-mismatch"
        Write-Host "expected: $ExpectedSdkVersion"
        Write-Host "next:"
        Write-Host "  1. Run this script from the repository root so global.json is applied."
        Write-Host "  2. If the selected SDK is still different, install .NET SDK $ExpectedSdkVersion."
        Write-Host "  3. Or point -DotnetExe / DOTNET_EXE to a local .NET 8 SDK binary."
        exit 1
    }

    if ($WorkloadsOutput -notmatch '(^|\r?\n)\s*maui\s+') {
        Write-Host "status: missing-maui-workload"
        Write-Host "next:"
        Write-Host "  $DotnetCommand workload install maui --skip-manifest-update"
        exit 1
    }

    Write-Host "status: ready"
    Write-Host "next:"
    Write-Host "  pwsh -File .\scripts\publish-windows.ps1 -DotnetExe `"$DotnetCommand`""
}
finally {
    Pop-Location
}
