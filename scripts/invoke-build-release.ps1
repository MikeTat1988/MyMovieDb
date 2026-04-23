Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root "logs"
$logFile = Join-Path $logDir "build-release.log"
$webProject = Join-Path $root "src\LocalMovieVault.Web\LocalMovieVault.Web.csproj"
$canonicalOutDir = Join-Path $root "build\LocalMovieVault.Web"
$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stagingRoot = Join-Path $root ("build\release-staging\" + $runStamp)
$stagingOutDir = Join-Path $stagingRoot "LocalMovieVault.Web"
$packageDir = Join-Path $root "build\packages"
$incomingDir = "G:\My Drive\MasterApp\Incoming"
$appName = "MyMovieDB"
$manifestPath = Join-Path $root "app.manifest.json"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-Log {
    param([string]$Message)
    $line = $Message
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

function Remove-IfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Run-External {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    $escapedArguments = $Arguments | ForEach-Object {
        if ($_ -match '\s') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.Arguments = if ($escapedArguments) { [string]::Join(' ', $escapedArguments) } else { '' }

    $psi.WorkingDirectory = $root
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $null = $process.Start()

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        Add-Content -Path $logFile -Value $stdout.TrimEnd()
    }

    if (-not [string]::IsNullOrWhiteSpace($stderr)) {
        Add-Content -Path $logFile -Value $stderr.TrimEnd()
    }

    if ($process.ExitCode -ne 0) {
        throw "$FileName exited with code $($process.ExitCode)."
    }
}

Set-Content -Path $logFile -Value @(
    "==================================================",
    "MyMovieDB build-release started $(Get-Date)",
    "Working dir: $root",
    "Incoming dir: $incomingDir",
    "=================================================="
)

if (-not (Test-Path -LiteralPath $incomingDir)) {
    throw "Incoming folder was not found: $incomingDir"
}

$packageVersion = (Get-Content -Path $manifestPath -Raw | ConvertFrom-Json).version
$zipName = "$appName-$packageVersion.zip"
$canonicalZipPath = Join-Path $packageDir $zipName
$incomingPath = Join-Path $incomingDir $zipName
$stagingZipPath = Join-Path $packageDir ($zipName -replace '\.zip$', ('-' + $runStamp + '.staging.zip'))
$deliveredIncomingPath = $incomingPath

Write-Log "Package version: $packageVersion"
Write-Log "Canonical out dir: $canonicalOutDir"
Write-Log "Staging out dir: $stagingOutDir"
Write-Log "Canonical zip: $canonicalZipPath"
Write-Log "Incoming zip: $incomingPath"

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

Run-External -FileName "dotnet" -Arguments @("restore", $webProject)
Run-External -FileName "dotnet" -Arguments @("publish", $webProject, "-c", "Release", "-o", $stagingOutDir)
Run-External -FileName "powershell" -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    (Join-Path $PSScriptRoot "write-package-manifest.ps1"),
    "-SourceManifestPath",
    $manifestPath,
    "-DestinationManifestPath",
    (Join-Path $stagingOutDir "app.manifest.json")
)

Compress-Archive -Path (Join-Path $stagingOutDir "*") -DestinationPath $stagingZipPath -Force
try {
    Copy-Item -LiteralPath $stagingZipPath -Destination $incomingPath -Force
    $deliveredIncomingPath = $incomingPath
}
catch {
    $deliveredIncomingPath = Join-Path $incomingDir ($zipName -replace '\.zip$', ('-' + $runStamp + '.zip'))
    Copy-Item -LiteralPath $stagingZipPath -Destination $deliveredIncomingPath -Force
    Write-Log "WARNING: canonical Incoming zip was locked. Delivered timestamped fallback instead."
}

Write-Log "Incoming zip updated: $deliveredIncomingPath"

try {
    Copy-Item -LiteralPath $stagingZipPath -Destination $canonicalZipPath -Force
    Write-Log "Canonical zip updated: $canonicalZipPath"
}
catch {
    Write-Log "WARNING: canonical local zip could not be updated: $($_.Exception.Message)"
}

try {
    if (-not (Test-Path -LiteralPath $canonicalOutDir)) {
        New-Item -ItemType Directory -Force -Path $canonicalOutDir | Out-Null
    }

    $null = robocopy $stagingOutDir $canonicalOutDir /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }

    Write-Log "Canonical publish folder synchronized: $canonicalOutDir"
}
catch {
    Write-Log "WARNING: canonical publish folder could not be fully synchronized: $($_.Exception.Message)"
}

Write-Log "Done."
