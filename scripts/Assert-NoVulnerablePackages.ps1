[CmdletBinding()]
param(
    [string]$SolutionPath = 'WpfAiAutomation.slnx'
)

$ErrorActionPreference = 'Stop'

$report = dotnet list $SolutionPath package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    throw 'The .NET dependency vulnerability scan could not complete.'
}

$scan = $report | ConvertFrom-Json
$vulnerablePackages = foreach ($project in @($scan.projects)) {
    foreach ($framework in @($project.frameworks)) {
        if ($null -eq $framework) {
            continue
        }

        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($null -eq $package) {
                continue
            }

            $vulnerabilities = @($package.vulnerabilities | Where-Object { $null -ne $_ })
            if ($vulnerabilities.Count -gt 0) {
                [pscustomobject]@{
                    Project = $project.path
                    Framework = $framework.framework
                    Package = $package.id
                    Version = $package.resolvedVersion
                    Advisories = ($vulnerabilities | ForEach-Object advisoryurl) -join ', '
                }
            }
        }
    }
}

if (@($vulnerablePackages).Count -gt 0) {
    $vulnerablePackages | Format-Table -AutoSize | Out-String | Write-Error
    throw 'Known vulnerable packages were found. Update the centrally managed version and regenerate lock files.'
}

Write-Host 'No vulnerable direct or transitive packages were reported.'
