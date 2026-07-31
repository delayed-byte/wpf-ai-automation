[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$centralPackages = Join-Path $root 'Directory.Packages.props'
if (-not (Test-Path -LiteralPath $centralPackages -PathType Leaf)) {
    throw 'Directory.Packages.props is required for central package management.'
}

[xml]$central = Get-Content -Raw -LiteralPath $centralPackages
$centralEnabled = @($central.Project.PropertyGroup.ManagePackageVersionsCentrally) -contains 'true'
if (-not $centralEnabled) {
    throw 'ManagePackageVersionsCentrally must be true.'
}

$projects = Get-ChildItem -LiteralPath $root -Filter '*.csproj' -File -Recurse
foreach ($project in $projects) {
    [xml]$projectXml = Get-Content -Raw -LiteralPath $project.FullName
    $versionedReference = @($projectXml.SelectNodes('//PackageReference[@Version]')) | Select-Object -First 1
    if ($null -ne $versionedReference) {
        throw "$($project.FullName) pins package '$($versionedReference.Include)' outside Directory.Packages.props."
    }

    $lockFile = Join-Path $project.DirectoryName 'packages.lock.json'
    if (-not (Test-Path -LiteralPath $lockFile -PathType Leaf)) {
        throw "$($project.FullName) is missing packages.lock.json. Run dotnet restore --use-lock-file and commit the result."
    }
}

Write-Host "Central package management and $($projects.Count) committed lock files verified."
