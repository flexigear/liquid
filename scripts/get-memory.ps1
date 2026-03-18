param(
    [Parameter(Mandatory = $false)]
    [string]$Task = "",

    [Parameter(Mandatory = $false)]
    [int]$Top = 6
)

$manifestPath = Join-Path $PSScriptRoot "..\\memory\\00-system\\memory-manifest.json"

if (-not (Test-Path $manifestPath)) {
    Write-Error "Memory manifest not found: $manifestPath"
    exit 1
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($Task)) {
    $taskWords = @()
} else {
    $taskWords = $Task.ToLowerInvariant().Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
}

$results = foreach ($file in $manifest.files) {
    $score = [int]$file.priority

    foreach ($tag in $file.tags) {
        foreach ($word in $taskWords) {
            if ($tag -like "*$word*" -or $word -like "*$tag*") {
                $score += 25
            }
        }
    }

    if ($taskWords.Count -eq 0 -and $file.layer -in @("foundation", "active")) {
        $score += 20
    }

    [pscustomobject]@{
        Score = $score
        Layer = $file.layer
        Path = $file.path
        Summary = $file.summary
    }
}

$selected = $results |
    Sort-Object @{ Expression = 'Score'; Descending = $true }, @{ Expression = 'Layer'; Descending = $false }, @{ Expression = 'Path'; Descending = $false } |
    Select-Object -First $Top

Write-Output "Task: $Task"
Write-Output "Recommended memory files:"

foreach ($item in $selected) {
    Write-Output ""
    Write-Output ("[{0}] {1}" -f $item.Layer, $item.Path)
    Write-Output ("  Score: {0}" -f $item.Score)
    Write-Output ("  {0}" -f $item.Summary)
}
