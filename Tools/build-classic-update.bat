@echo off
setlocal

REM One-click updater package builder for:
REM   http://classic-gunbound.servegame.com/update
REM
REM Usage (optional args):
REM   build-classic-update.bat "C:\Path\To\Client" "C:\Path\To\Staging\update"

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%build-patch.ps1"

if not exist "%PS_SCRIPT%" (
    echo [ERROR] Missing script: %PS_SCRIPT%
    pause
    exit /b 1
)

set "REPO_ROOT=%SCRIPT_DIR%.."
for %%I in ("%REPO_ROOT%") do set "REPO_ROOT=%%~fI"

REM Edit these defaults once for your machine.
set "DEFAULT_CLIENT_ROOT=C:\GBTH-Client"
set "DEFAULT_STAGING_UPDATE_ROOT=%REPO_ROOT%\release\update"

set "CLIENT_ROOT=%~1"
set "STAGING_UPDATE_ROOT=%~2"

if "%CLIENT_ROOT%"=="" set "CLIENT_ROOT=%DEFAULT_CLIENT_ROOT%"
if "%STAGING_UPDATE_ROOT%"=="" set "STAGING_UPDATE_ROOT=%DEFAULT_STAGING_UPDATE_ROOT%"

echo.
echo ======== Classic GunBound Update Builder ========
echo ClientRoot      : %CLIENT_ROOT%
echo Staging /update : %STAGING_UPDATE_ROOT%
echo Target URL      : http://classic-gunbound.servegame.com/update
echo.

if not exist "%CLIENT_ROOT%" (
    set /p CLIENT_ROOT=ClientRoot not found. Enter full client path: 
)

if not exist "%CLIENT_ROOT%" (
    echo.
    echo [ERROR] ClientRoot does not exist:
    echo         %CLIENT_ROOT%
    pause
    exit /b 1
)

echo Building manifest + patch files...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -ClientRoot "%CLIENT_ROOT%" -PublishRoot "%STAGING_UPDATE_ROOT%" -PruneDeleted
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo.
echo Build completed.
echo.
echo Next:
echo 1) Upload all contents of this local folder:
echo    %STAGING_UPDATE_ROOT%
echo 2) To web folder:
echo    /update
echo 3) Verify URLs:
echo    http://classic-gunbound.servegame.com/update/manifest.txt
echo    http://classic-gunbound.servegame.com/update/gamefiles/
echo.

if exist "%STAGING_UPDATE_ROOT%" start "" "%STAGING_UPDATE_ROOT%"

pause
exit /b 0
