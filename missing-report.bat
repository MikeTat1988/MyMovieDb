@echo off
setlocal
cd /d "%~dp0"
echo ==================================
echo MyMovieDB - missing report
echo ==================================
where dotnet >nul 2>nul
if errorlevel 1 (
  echo.
  echo ERROR: dotnet SDK not found.
  pause
  exit /b 1
)

dotnet run --project tools\MissingReporter\MissingReporter.csproj
set ERR=%ERRORLEVEL%
echo.
if not "%ERR%"=="0" (
  echo ERROR CODE: %ERR%
) else (
  echo Done.
)
pause
exit /b %ERR%
