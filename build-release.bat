@echo off
setlocal EnableExtensions
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\invoke-build-release.ps1"
set "EXITCODE=%ERRORLEVEL%"

if not "%EXITCODE%"=="0" (
    echo.
    echo ERROR: build-release failed.
    echo Open this log:
    echo "%~dp0logs\build-release.log"
    exit /b %EXITCODE%
)

echo.
echo Done.
echo Log:
echo "%~dp0logs\build-release.log"
exit /b 0
