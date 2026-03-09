@echo off
setlocal

REM Double-click patch builder wrapper for Tools\build-patch.ps1
REM Optional args:
REM   build-patch.bat "C:\Path\To\Client" "C:\Path\To\PublishRoot"

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%build-patch.ps1"

if not exist "%PS_SCRIPT%" (
    echo [ERROR] Missing PowerShell script:
    echo         %PS_SCRIPT%
    pause
    exit /b 1
)

REM Default paths (edit these once and keep using double-click)
set "DEFAULT_CLIENT_ROOT=C:\GB\Client"
set "DEFAULT_PUBLISH_ROOT=C:\GB\PatchPublish"

set "CLIENT_ROOT=%~1"
set "PUBLISH_ROOT=%~2"

if "%CLIENT_ROOT%"=="" set "CLIENT_ROOT=%DEFAULT_CLIENT_ROOT%"
if "%PUBLISH_ROOT%"=="" set "PUBLISH_ROOT=%DEFAULT_PUBLISH_ROOT%"

echo.
echo ========= GBTH Patch Builder =========
echo ClientRoot : %CLIENT_ROOT%
echo PublishRoot: %PUBLISH_ROOT%
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

set "PRUNE_ARG="
set /p PRUNE_CHOICE=Remove files from publish folder that no longer exist in client? (Y/N, default Y): 
if /I "%PRUNE_CHOICE%"=="" set "PRUNE_CHOICE=Y"
if /I "%PRUNE_CHOICE%"=="Y" set "PRUNE_ARG=-PruneDeleted"

echo.
echo Running patch build...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -ClientRoot "%CLIENT_ROOT%" -PublishRoot "%PUBLISH_ROOT%" %PRUNE_ARG%
if errorlevel 1 (
    echo.
    echo [ERROR] Patch build failed.
    pause
    exit /b 1
)

echo.
echo Patch build completed successfully.
if exist "%PUBLISH_ROOT%" (
    start "" "%PUBLISH_ROOT%"
)

pause
exit /b 0
