$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$documentsPath = [Environment]::GetFolderPath('MyDocuments')
if ([string]::IsNullOrWhiteSpace($documentsPath)) {
    throw "Could not resolve Documents folder."
}

$dataHome = Join-Path $documentsPath 'MyMovieDB'
$dbPath = Join-Path $dataHome 'localmovievault.db'
$settingsPath = Join-Path $dataHome 'mymoviedb.settings.json'
$seedPath = Join-Path $dataHome 'seed_movies.json'
$titleOverridesPath = Join-Path $dataHome 'title-overrides.json'

New-Item -ItemType Directory -Path $dataHome -Force | Out-Null

function Write-Status([string]$text) {
    Write-Host $text
}

function Load-JsonObject([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    $raw = Get-Content -Path $path -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return $raw | ConvertFrom-Json
}

$settings = Load-JsonObject $settingsPath
$settingsChanged = $false
if ($null -eq $settings) {
    $settings = [pscustomobject]@{
        AppHost = [pscustomobject]@{
            Url = 'http://localhost:5057'
            AutoLaunchBrowser = $true
        }
        MetadataProviders = [pscustomobject]@{
            OmDb = [pscustomobject]@{
                ApiKey = ''
                BaseUrl = 'https://www.omdbapi.com/'
            }
        }
    }
    $settingsChanged = $true
}

$localSettingsCandidates = @(
    (Join-Path $projectRoot 'src\LocalMovieVault.Web\appsettings.json'),
    (Join-Path $projectRoot 'appsettings.json')
) | Select-Object -Unique

foreach ($candidate in $localSettingsCandidates) {
    if (-not (Test-Path $candidate)) { continue }
    $local = Load-JsonObject $candidate
    if ($null -eq $local) { continue }

    $candidateKey = $local.MetadataProviders.OmDb.ApiKey
    if ([string]::IsNullOrWhiteSpace($settings.MetadataProviders.OmDb.ApiKey) -and -not [string]::IsNullOrWhiteSpace($candidateKey)) {
        $settings.MetadataProviders.OmDb.ApiKey = $candidateKey.Trim()
        $settingsChanged = $true
        Write-Status "Migrated OMDb API key into $settingsPath"
    }

    $candidateBaseUrl = $local.MetadataProviders.OmDb.BaseUrl
    if ([string]::IsNullOrWhiteSpace($settings.MetadataProviders.OmDb.BaseUrl) -and -not [string]::IsNullOrWhiteSpace($candidateBaseUrl)) {
        $settings.MetadataProviders.OmDb.BaseUrl = $candidateBaseUrl.Trim()
        $settingsChanged = $true
    }

    $candidateUrl = $local.AppHost.Url
    if ([string]::IsNullOrWhiteSpace($settings.AppHost.Url) -and -not [string]::IsNullOrWhiteSpace($candidateUrl)) {
        $settings.AppHost.Url = $candidateUrl.Trim()
        $settingsChanged = $true
    }
}

if ($settingsChanged -or -not (Test-Path $settingsPath)) {
    $settings | ConvertTo-Json -Depth 8 | Set-Content -Path $settingsPath -Encoding UTF8
}
Write-Status "Settings file: $settingsPath"

$legacyDbCandidates = @(
    (Join-Path $projectRoot 'src\LocalMovieVault.Web\App_Data\localmovievault.db'),
    (Join-Path $projectRoot 'App_Data\localmovievault.db')
) | Select-Object -Unique

if (-not (Test-Path $dbPath)) {
    foreach ($candidate in $legacyDbCandidates) {
        if (-not (Test-Path $candidate)) { continue }

        Move-Item -Path $candidate -Destination $dbPath -Force
        if (Test-Path ($candidate + '-wal')) { Move-Item -Path ($candidate + '-wal') -Destination ($dbPath + '-wal') -Force }
        if (Test-Path ($candidate + '-shm')) { Move-Item -Path ($candidate + '-shm') -Destination ($dbPath + '-shm') -Force }
        Write-Status "Migrated database into $dbPath"
        break
    }
}

if (-not (Test-Path $seedPath)) {
    $seedCandidates = @(
        (Join-Path $projectRoot 'src\LocalMovieVault.Web\App_Data\Seed\seed_movies.json')
    )
    foreach ($candidate in $seedCandidates) {
        if (-not (Test-Path $candidate)) { continue }
        Copy-Item -Path $candidate -Destination $seedPath -Force
        Write-Status "Copied seed file into $seedPath"
        break
    }
}



$defaultOverridesPath = Join-Path $projectRoot 'title-overrides.json'
$existingOverrides = @{}
$existingOverridesRaw = ''
if (Test-Path $titleOverridesPath) {
    try {
        $existingOverridesRaw = Get-Content -Path $titleOverridesPath -Raw -Encoding UTF8
        $existingRaw = $existingOverridesRaw | ConvertFrom-Json
        if ($existingRaw) { foreach ($property in $existingRaw.PSObject.Properties) { $existingOverrides[$property.Name] = [string]$property.Value } }
    } catch {
        $existingOverrides = @{}
        $existingOverridesRaw = ''
    }
}

if (Test-Path $defaultOverridesPath) {
    try {
        $defaultRaw = Get-Content -Path $defaultOverridesPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($defaultRaw) { foreach ($property in $defaultRaw.PSObject.Properties) { $existingOverrides[$property.Name] = [string]$property.Value } }
    } catch {
        # ignore malformed bundled overrides
    }
}

if (-not (Test-Path $titleOverridesPath)) {
    if ($existingOverrides.Count -eq 0) {
        '{}' | Set-Content -Path $titleOverridesPath -Encoding UTF8
        Write-Status "Created empty overrides file in $titleOverridesPath"
    } else {
        ($existingOverrides | ConvertTo-Json -Depth 8) | Set-Content -Path $titleOverridesPath -Encoding UTF8
        Write-Status "Created overrides file in $titleOverridesPath"
    }
} elseif ($existingOverrides.Count -gt 0 -and [string]::IsNullOrWhiteSpace($existingOverridesRaw)) {
    ($existingOverrides | ConvertTo-Json -Depth 8) | Set-Content -Path $titleOverridesPath -Encoding UTF8
    Write-Status "Restored overrides file in $titleOverridesPath"
}

Write-Status "Data home: $dataHome"
Write-Status "Database : $dbPath"
Write-Status "Config   : $settingsPath"
Write-Status "Seed     : $seedPath"
Write-Status "Overrides: $titleOverridesPath"
