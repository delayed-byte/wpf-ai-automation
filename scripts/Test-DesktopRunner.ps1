[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfigurationPath
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'The Patient Demo smoke suite requires Windows.'
}

if (-not [Environment]::UserInteractive) {
    throw 'The runner is not attached to an interactive desktop session.'
}

$runnerSessionId = (Get-Process -Id $PID).SessionId
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $runnerSessionId)) {
    throw 'No Explorer process is available in the runner session. Unlock the dedicated desktop before retrying.'
}

$dpi = Get-ItemPropertyValue -Path 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction Stop
if ([int]$dpi -ne 96) {
    throw "The runner display scaling must be fixed at 100% (AppliedDPI 96); found $dpi."
}

$resolvedConfigurationPath = (Resolve-Path -LiteralPath $ConfigurationPath).Path
$configuration = Get-Content -Raw -LiteralPath $resolvedConfigurationPath | ConvertFrom-Json -AsHashtable
$application = $configuration.applications.'patient-demo'
if ($null -eq $application) {
    throw 'The runner configuration does not contain the patient-demo allowlist entry.'
}

if ([string]::IsNullOrWhiteSpace($application.processName) -or $application.processName -match '[\\/:]') {
    throw 'The configured Patient Demo process name is invalid.'
}

if (-not (Test-Path -LiteralPath $application.executablePath -PathType Leaf)) {
    throw 'The configured Patient Demo executable is unavailable on this runner.'
}

Write-Host "Desktop preflight passed for process '$($application.processName)' in session $runnerSessionId."
