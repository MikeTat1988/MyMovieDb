@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Canonical delivery script for this project.
rem After any user-facing update, publish the app, create a zip, and copy it to:
rem G:\My Drive\MasterApp\Incoming

set "LOGDIR=%~dp0logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LOGFILE=%LOGDIR%\build-release.log"
set "WEB_PROJECT=src\LocalMovieVault.Web\LocalMovieVault.Web.csproj"
set "WEB_OUTDIR=%~dp0build\LocalMovieVault.Web"
set "PACKAGE_DIR=%~dp0build\packages"
set "INCOMING_DIR=G:\My Drive\MasterApp\Incoming"
set "APP_NAME=MyMovieDB"
set "PACKAGE_VERSION=dev"

for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Get-Content '%~dp0app.manifest.json' | ConvertFrom-Json).version"`) do set "PACKAGE_VERSION=%%V"
set "ZIP_NAME=%APP_NAME%-%PACKAGE_VERSION%.zip"
set "ZIP_PATH=%PACKAGE_DIR%\%ZIP_NAME%"
set "INCOMING_PATH=%INCOMING_DIR%\%ZIP_NAME%"

echo ================================================== > "%LOGFILE%"
echo MyMovieDB build-release started %date% %time% >> "%LOGFILE%"
echo Working dir: %cd% >> "%LOGFILE%"
echo Data home: %USERPROFILE%\Documents\MyMovieDB >> "%LOGFILE%"
echo Incoming dir: %INCOMING_DIR% >> "%LOGFILE%"
echo ================================================== >> "%LOGFILE%"

where dotnet >> "%LOGFILE%" 2>&1
if errorlevel 1 goto dotnet_missing

dotnet --info >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup-documents-home.ps1" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

dotnet restore "%WEB_PROJECT%" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

if not exist "%PACKAGE_DIR%" mkdir "%PACKAGE_DIR%"
if not exist "%INCOMING_DIR%" goto incoming_missing

if exist "%WEB_OUTDIR%" rmdir /s /q "%WEB_OUTDIR%" >> "%LOGFILE%" 2>&1
if exist "%ZIP_PATH%" del /f /q "%ZIP_PATH%" >> "%LOGFILE%" 2>&1

dotnet publish "%WEB_PROJECT%" -c Release -o "%WEB_OUTDIR%" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\write-package-manifest.ps1" -SourceManifestPath "%~dp0app.manifest.json" -DestinationManifestPath "%WEB_OUTDIR%\app.manifest.json" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

powershell -NoProfile -Command "Compress-Archive -Path '%WEB_OUTDIR%\*' -DestinationPath '%ZIP_PATH%' -Force" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

copy /y "%ZIP_PATH%" "%INCOMING_PATH%" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

echo.
echo Done.
echo Build: %WEB_OUTDIR%
echo Zip:   %ZIP_PATH%
echo Copied to: %INCOMING_PATH%
echo.
echo Full log:
echo "%LOGFILE%"
pause
exit /b 0

:incoming_missing
echo.
echo ERROR: Incoming folder was not found.
echo Expected:
echo "%INCOMING_DIR%"
echo.
echo Log: "%LOGFILE%"
pause
goto end

:dotnet_missing
echo.
echo ERROR: dotnet was not found in PATH.
echo Install .NET 8 SDK and try again.
echo Log: "%LOGFILE%"
pause
goto end

:fail
echo.
echo ERROR: build-release failed.
echo Open this log:
echo "%LOGFILE%"
echo.
pause

:end
endlocal
