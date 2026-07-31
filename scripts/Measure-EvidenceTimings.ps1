[CmdletBinding()]
param(
    [string]$EvidenceDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'artifacts'),
    [string]$OutputDirectory = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'artifacts\timing-summary')
)

$ErrorActionPreference = 'Stop'

function Get-Percentile([long[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return $null }
    $ordered = @($Values | Sort-Object)
    $index = [Math]::Ceiling($Percentile * $ordered.Count) - 1
    return $ordered[[Math]::Max(0, [Math]::Min($index, $ordered.Count - 1))]
}

function Get-Measurement([string]$Name, [long[]]$Values) {
    return [ordered]@{
        name = $Name
        sampleCount = $Values.Count
        p50Milliseconds = Get-Percentile $Values 0.50
        p95Milliseconds = Get-Percentile $Values 0.95
        maximumMilliseconds = if ($Values.Count -eq 0) { $null } else { ($Values | Measure-Object -Maximum).Maximum }
    }
}

$startup = [System.Collections.Generic.List[long]]::new()
$scenario = [System.Collections.Generic.List[long]]::new()
$lookup = [System.Collections.Generic.List[long]]::new()
$action = [System.Collections.Generic.List[long]]::new()

if (Test-Path -LiteralPath $EvidenceDirectory -PathType Container) {
    Get-ChildItem -LiteralPath $EvidenceDirectory -Filter 'run-metadata.json' -File -Recurse | ForEach-Object {
        $metadata = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
        if ($null -ne $metadata.startupDurationMilliseconds) { $startup.Add([long]$metadata.startupDurationMilliseconds) }
        if ($null -ne $metadata.scenarioDurationMilliseconds) { $scenario.Add([long]$metadata.scenarioDurationMilliseconds) }
    }

    Get-ChildItem -LiteralPath $EvidenceDirectory -Filter 'events.jsonl' -File -Recurse | ForEach-Object {
        Get-Content -LiteralPath $_.FullName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
            $line = $_.Trim()
            if (-not $line.StartsWith('{', [System.StringComparison]::Ordinal)) {
                return
            }

            try {
                $event = $line | ConvertFrom-Json
            }
            catch {
                Write-Verbose 'Ignoring a non-JSONL evidence entry from a pre-Phase-6 artifact.'
                return
            }

            if ($event.result -eq 'passed') {
                switch ($event.action) {
                    'waitFor' { $lookup.Add([long]$event.durationMilliseconds) }
                    { $_ -in @('setText', 'invoke', 'selectItem') } { $action.Add([long]$event.durationMilliseconds) }
                }
            }
        }
    }
}

$measurements = @(
    (Get-Measurement 'startup' $startup.ToArray()),
    (Get-Measurement 'lookup' $lookup.ToArray()),
    (Get-Measurement 'action' $action.ToArray()),
    (Get-Measurement 'scenario' $scenario.ToArray())
)

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$summary = [ordered]@{ generatedAtUtc = [DateTimeOffset]::UtcNow; measurements = $measurements }
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'timing-summary.json') -Encoding utf8

$markdown = @(
    '# Evidence timing summary',
    '',
    '| Measurement | Samples | P50 (ms) | P95 (ms) | Max (ms) |',
    '| --- | ---: | ---: | ---: | ---: |'
)
foreach ($measurement in $measurements) {
    $markdown += "| $($measurement.name) | $($measurement.sampleCount) | $($measurement.p50Milliseconds) | $($measurement.p95Milliseconds) | $($measurement.maximumMilliseconds) |"
}
$markdown | Set-Content -LiteralPath (Join-Path $OutputDirectory 'timing-summary.md') -Encoding utf8
Write-Host "Timing summary written to $OutputDirectory."
