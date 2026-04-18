Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

$targets = @(
    (Join-Path $root "package-v2"),
    (Join-Path $root "package-v2-clean"),
    (Join-Path $root "package-v4"),
    (Join-Path $root "package-v4-clean"),
    (Join-Path $root "package-v5-clean"),
    (Join-Path $root "tmp-stdout.log"),
    (Join-Path $root "tmp-stderr.log"),
    (Join-Path $root "build\screenshot-review"),
    (Join-Path $root "build\screenshot-review-2"),
    (Join-Path $root "build\shot-review"),
    (Join-Path $root "build\MyMovieDB-9.0.0-masterapp.7.zip"),
    (Join-Path $root "logs\build-release.log"),
    (Join-Path $root "logs\hotfix-settings-err.log"),
    (Join-Path $root "logs\hotfix-settings-out.log"),
    (Join-Path $root "logs\local-run.err.log"),
    (Join-Path $root "logs\local-run.out.log"),
    (Join-Path $root "logs\settings-cookies.txt"),
    (Join-Path $root "logs\settings-page.html"),
    (Join-Path $root "tools\dbcheck.cs"),
    (Join-Path $root "tools\dbsample.cs")
)

foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target)) {
        continue
    }

    $item = Get-Item -LiteralPath $target
    if ($item.PSIsContainer) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    else {
        Remove-Item -LiteralPath $target -Force
    }
}

Write-Host "Workspace cleanup complete."
