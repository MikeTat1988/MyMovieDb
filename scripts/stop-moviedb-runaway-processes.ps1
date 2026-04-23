param(
    [int]$LookbackHours = 6
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$cutoff = (Get-Date).AddHours(-1 * $LookbackHours)
$patterns = @(
    "LocalMovieVault.Web.Tests.exe",
    "LocalMovieVault.Web.Tests.csproj",
    "run-smoke-tests.ps1",
    "invoke-build-release.ps1",
    "build-release.bat",
    "LocalMovieVault.Web\\bin\\",
    "C:\\Dev\\MovieDb\\build\\LocalMovieVault.Web"
)

$processes = Get-CimInstance Win32_Process | Where-Object {
    $process = $_
    $matchesPattern = $false
    foreach ($pattern in $patterns) {
        if (($process.CommandLine -and $process.CommandLine -like "*$pattern*") -or
            ($process.ExecutablePath -and $process.ExecutablePath -like "*$pattern*")) {
            $matchesPattern = $true
            break
        }
    }

    $process.CreationDate -ge $cutoff -and (
        $process.Name -eq "LocalMovieVault.Web.Tests.exe" -or
        $process.Name -eq "LocalMovieVault.Web.exe" -or
        $matchesPattern
    )
}

$stopped = New-Object System.Collections.Generic.List[object]
foreach ($process in $processes) {
    try {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        $stopped.Add([PSCustomObject]@{
            ProcessId = $process.ProcessId
            Name = $process.Name
            ExecutablePath = $process.ExecutablePath
        }) | Out-Null
    }
    catch {
    }
}

[PSCustomObject]@{
    Count = $stopped.Count
    Stopped = $stopped
} | ConvertTo-Json -Depth 5
