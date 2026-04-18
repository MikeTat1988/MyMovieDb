param(
    [Parameter(Mandatory = $true)]
    [string]$SourceManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$DestinationManifestPath
)

$ErrorActionPreference = 'Stop'

$sourceManifest = Get-Content -Path $SourceManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

$packageManifest = [ordered]@{
    schemaVersion = $sourceManifest.schemaVersion
    id = $sourceManifest.id
    name = $sourceManifest.name
    version = $sourceManifest.version
    appType = 'portable'
    entry = 'index.html'
    launch = [ordered]@{
        kind = $sourceManifest.launch.kind
        executablePath = 'LocalMovieVault.Web.exe'
        workingDirectory = '.'
        arguments = @()
        environmentVariables = [ordered]@{
            AppHost__AutoLaunchBrowser = 'false'
        }
        port = $sourceManifest.launch.port
        urlTemplate = $sourceManifest.launch.urlTemplate
        healthPath = $sourceManifest.launch.healthPath
        startupTimeoutSeconds = $sourceManifest.launch.startupTimeoutSeconds
    }
    display = [ordered]@{
        shortName = $sourceManifest.display.shortName
        storeVisible = $sourceManifest.display.storeVisible
        showInLibrary = $sourceManifest.display.showInLibrary
    }
}

$packageManifest | ConvertTo-Json -Depth 10 | Set-Content -Path $DestinationManifestPath -Encoding UTF8
