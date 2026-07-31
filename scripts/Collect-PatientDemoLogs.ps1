[CmdletBinding()]
param(
    [string]$SourceDirectory = $env:PATIENT_DEMO_LOG_DIRECTORY,
    [Parameter(Mandatory)]
    [string]$DestinationDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceDirectory) -or -not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
    Write-Host 'No configured Patient Demo log directory was available.'
    return
}

New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null
Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse -Include '*.log', '*.txt' |
    Where-Object LastWriteTimeUtc -ge [DateTime]::UtcNow.AddDays(-7) |
    Copy-Item -Destination $DestinationDirectory -Force
