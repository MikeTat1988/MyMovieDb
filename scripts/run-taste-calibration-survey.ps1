param(
    [int]$Port = 5127,
    [int]$Minutes = 120
)

$ErrorActionPreference = 'Stop'
$serverPath = Join-Path $PSScriptRoot 'taste_calibration_survey_server.py'

python $serverPath --port $Port --minutes $Minutes
