[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfigurationPath,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$LeaseDirectory
)

$ErrorActionPreference = 'Stop'

function Get-CanonicalPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

$configuration = Get-Content -Raw -LiteralPath $ConfigurationPath | ConvertFrom-Json -AsHashtable
$application = $configuration.applications.'patient-demo'
if ($null -eq $application) {
    throw 'The cleanup configuration does not contain the patient-demo allowlist entry.'
}

$configuredPath = Get-CanonicalPath $application.executablePath
$configuredProcessName = [string]$application.processName
if ([string]::IsNullOrWhiteSpace($configuredProcessName) -or $configuredProcessName -match '[\\/:]') {
    throw 'The configured Patient Demo process name is invalid.'
}

if (-not (Test-Path -LiteralPath $LeaseDirectory -PathType Container)) {
    Write-Host 'No workspace-owned Patient Demo process leases were found.'
    return
}

Get-ChildItem -LiteralPath $LeaseDirectory -Filter '*.json' -File | ForEach-Object {
    $leasePath = $_.FullName
    try {
        $lease = Get-Content -Raw -LiteralPath $leasePath | ConvertFrom-Json -AsHashtable
        if ([string]$lease.runId -notmatch '^ci-[A-Za-z0-9_-]+$' -or [int]$lease.processId -lt 1) {
            Write-Warning "Ignoring invalid process lease '$leasePath'."
            return
        }

        if ([string]$lease.processName -cne $configuredProcessName -or (Get-CanonicalPath ([string]$lease.executablePath)) -cne $configuredPath) {
            Write-Warning "Ignoring process lease '$leasePath' because it does not match the configured Patient Demo."
            return
        }

        try {
            $process = Get-Process -Id ([int]$lease.processId) -ErrorAction Stop
        }
        catch [System.ArgumentException], [Microsoft.PowerShell.Commands.ProcessCommandException] {
            Remove-Item -LiteralPath $leasePath -Force
            return
        }

        $processPath = Get-CanonicalPath ([string]$process.Path)
        if ($process.ProcessName -cne $configuredProcessName -or $processPath -cne $configuredPath) {
            Write-Warning "Ignoring process $($process.Id): it no longer matches its workspace-owned lease."
            return
        }

        if ($PSCmdlet.ShouldProcess("Patient Demo process $($process.Id)", 'Stop')) {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            $process.WaitForExit(10000)
            Remove-Item -LiteralPath $leasePath -Force
            Write-Host "Stopped workspace-owned Patient Demo process $($process.Id)."
        }
    }
    catch {
        Write-Warning "Could not process lease '$leasePath': $($_.Exception.Message)"
    }
}
