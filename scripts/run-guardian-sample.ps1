[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$ReportTitle = "Guardian Sample Dataset",
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$RootDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RootDir "sample/guardian/output"
}

Push-Location $RootDir
try {
    dotnet run `
        --project "src/NexusWorks.Guardian.Cli/NexusWorks.Guardian.Cli.csproj" `
        -c $Configuration `
        -- `
        --sample `
        --output-root $OutputRoot `
        --report-title $ReportTitle
}
finally {
    Pop-Location
}
