param(
    [int]$BuildTimeoutSeconds = 45,
    [int]$RunTimeoutSeconds = 30,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $root "tests\LocalMovieVault.Web.Tests\LocalMovieVault.Web.Tests.csproj"
$testExe = Join-Path $root "tests\LocalMovieVault.Web.Tests\bin\Debug\net8.0\LocalMovieVault.Web.Tests.exe"
$logDir = Join-Path $root "logs"
$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$buildLog = Join-Path $logDir ("smoke-build-" + $runStamp + ".log")
$stdoutLog = Join-Path $logDir ("smoke-run-" + $runStamp + ".out.log")
$stderrLog = Join-Path $logDir ("smoke-run-" + $runStamp + ".err.log")

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Stop-StaleSmokeProcesses {
    Get-Process -Name "LocalMovieVault.Web.Tests" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $_.Kill($true)
            $_.WaitForExit()
        }
        catch {
            try {
                Stop-Process -Id $_.Id -Force -ErrorAction Stop
            }
            catch {
            }
        }
    }
}

function Start-TrackedProcess {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [int]$TimeoutSeconds,
        [string]$StdOutPath,
        [string]$StdErrPath
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

    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $null = $process.Start()

    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $finished = $process.WaitForExit($TimeoutSeconds * 1000)
    if (-not $finished) {
        try {
            $process.Kill($true)
        }
        catch {
        }

        try {
            $process.WaitForExit()
        }
        catch {
        }
    }

    [System.Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask), 5000) | Out-Null
    [System.IO.File]::WriteAllText($StdOutPath, $stdoutTask.Result)
    [System.IO.File]::WriteAllText($StdErrPath, $stderrTask.Result)

    return [PSCustomObject]@{
        Finished   = $finished
        ExitCode   = if ($finished) { $process.ExitCode } else { $null }
        StdOutPath = $StdOutPath
        StdErrPath = $StdErrPath
    }
}

Stop-StaleSmokeProcesses

try {
    if (-not $SkipBuild) {
        $buildResult = Start-TrackedProcess `
            -FileName "dotnet" `
            -Arguments @("build", $testProject) `
            -WorkingDirectory $root `
            -TimeoutSeconds $BuildTimeoutSeconds `
            -StdOutPath $buildLog `
            -StdErrPath ($buildLog + ".err")

        if (-not $buildResult.Finished) {
            throw "Smoke test build timed out after $BuildTimeoutSeconds seconds. See $buildLog"
        }

        if ($buildResult.ExitCode -ne 0) {
            throw "Smoke test build failed with exit code $($buildResult.ExitCode). See $buildLog"
        }
    }

    Stop-StaleSmokeProcesses

    $runResult = Start-TrackedProcess `
        -FileName $testExe `
        -Arguments @() `
        -WorkingDirectory $root `
        -TimeoutSeconds $RunTimeoutSeconds `
        -StdOutPath $stdoutLog `
        -StdErrPath $stderrLog

    if (-not $runResult.Finished) {
        throw "Smoke test run timed out after $RunTimeoutSeconds seconds. See $stdoutLog and $stderrLog"
    }

    if ($runResult.ExitCode -ne 0) {
        throw "Smoke test run failed with exit code $($runResult.ExitCode). See $stdoutLog and $stderrLog"
    }

    Write-Host "Smoke tests passed."
    Write-Host "StdOut: $stdoutLog"
    Write-Host "StdErr: $stderrLog"
}
finally {
    Stop-StaleSmokeProcesses
}
