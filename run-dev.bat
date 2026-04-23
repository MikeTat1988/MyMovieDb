@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "LOGDIR=%~dp0logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LOGFILE=%LOGDIR%\run-dev.log"
set "TRAY_LOG=%LOGDIR%\tray.log"
set "WEB_PROJECT=src\LocalMovieVault.Web\LocalMovieVault.Web.csproj"
set "TRAY_PROJECT=tools\MyMovieDB.Tray\MyMovieDB.Tray.csproj"
set "WEB_DLL=%~dp0src\LocalMovieVault.Web\bin\Debug\net8.0\LocalMovieVault.Web.dll"
set "TRAY_DLL=%~dp0tools\MyMovieDB.Tray\bin\Debug\net8.0-windows\MyMovieDB.Tray.dll"
set "TRAY_EXE=%~dp0tools\MyMovieDB.Tray\bin\Debug\net8.0-windows\MyMovieDB.Tray.exe"
set "LISTEN_URL=http://+:5057"
set "LOCAL_URL=http://127.0.0.1:5057"
set "AppHost__AutoLaunchBrowser=true"

echo ================================================== > "%LOGFILE%"
echo MyMovieDB run-dev started %date% %time% >> "%LOGFILE%"
echo Working dir: %cd% >> "%LOGFILE%"
echo Data home: %USERPROFILE%\Documents\MyMovieDB >> "%LOGFILE%"
echo ================================================== >> "%LOGFILE%"

echo [1/4] Checking dotnet...
where dotnet >> "%LOGFILE%" 2>&1
if errorlevel 1 goto dotnet_missing

dotnet --info >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup-documents-home.ps1" >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

echo [2/4] Building web host...
dotnet build "%WEB_PROJECT%" -c Debug >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

echo [3/4] Building tray host...
dotnet build "%TRAY_PROJECT%" -c Debug >> "%LOGFILE%" 2>&1
if errorlevel 1 goto fail

echo [3.5/4] Opening firewall for port 5057 if possible...
netsh advfirewall firewall show rule name="MyMovieDB 5057" >> "%LOGFILE%" 2>&1
if errorlevel 1 netsh advfirewall firewall add rule name="MyMovieDB 5057" dir=in action=allow protocol=TCP localport=5057 >> "%LOGFILE%" 2>&1

echo [4/4] Starting tray...
if not exist "%WEB_DLL%" goto fail

echo ================================================== > "%TRAY_LOG%"
echo MyMovieDB tray started %date% %time% >> "%TRAY_LOG%"
echo Working dir: %cd% >> "%TRAY_LOG%"
echo WEB DLL: %WEB_DLL% >> "%TRAY_LOG%"
echo TRAY EXE: %TRAY_EXE% >> "%TRAY_LOG%"
echo TRAY DLL: %TRAY_DLL% >> "%TRAY_LOG%"
echo ================================================== >> "%TRAY_LOG%"

if exist "%TRAY_EXE%" (
    start "MyMovieDB Tray" "%TRAY_EXE%" --web-dll "%WEB_DLL%" --listen-url "%LISTEN_URL%" --local-url "%LOCAL_URL%" --log-file "%TRAY_LOG%"
) else (
    if not exist "%TRAY_DLL%" goto fail
    start "MyMovieDB Tray" dotnet "%TRAY_DLL%" --web-dll "%WEB_DLL%" --listen-url "%LISTEN_URL%" --local-url "%LOCAL_URL%" --log-file "%TRAY_LOG%"
)

timeout /t 3 /nobreak >nul

echo.
echo MyMovieDB startup finished.
echo Run log:
echo "%LOGFILE%"
echo Tray log:
echo "%TRAY_LOG%"
exit /b 0

:dotnet_missing
echo.
echo ERROR: dotnet was not found in PATH.
echo Install .NET 8 SDK and try again.
echo Log: "%LOGFILE%"
echo. >> "%LOGFILE%"
echo ERROR: dotnet was not found in PATH. >> "%LOGFILE%"
pause
goto end

:fail
echo.
echo ERROR: run-dev failed.
echo Open this log:
echo "%LOGFILE%"
echo.
pause

:end
endlocal
