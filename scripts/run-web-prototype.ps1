param(
    [int]$Port = 8123
)

$root = Join-Path $PSScriptRoot "..\prototype\engine\web"

Write-Output "Serving web prototype from $root"
Write-Output "Open http://localhost:$Port after the server starts."

Push-Location $root
try {
    python -m http.server $Port
}
finally {
    Pop-Location
}
