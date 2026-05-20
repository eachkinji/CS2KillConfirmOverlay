@echo off
setlocal
cd /d "%~dp0"
echo [1/3] Killing existing processes...
powershell -Command "taskkill /F /IM cskillconfirm.exe /T; taskkill /F /IM TestXboxGameBar.exe /T; exit 0"

echo [2/3] Building the updated transfer package (Release)...
powershell -ExecutionPolicy Bypass -File "Build-TransferPackage.ps1" -Configuration Release
if errorlevel 1 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

echo [3/3] Running installer with loopback and CS2 GSI fixes...
for /f %%V in ('powershell -NoProfile -Command "[xml]$m=Get-Content ''Package\Package.appxmanifest''; $m.Package.Identity.Version"') do set VERSION=%%V
set INSTALL_SCRIPT=..\KillConfirmGameBar_Transfer_%VERSION%\Install-KillConfirm.ps1
if exist "%INSTALL_SCRIPT%" (
    echo [SUCCESS] Found version %VERSION%.
    powershell -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%"
    if errorlevel 1 (
        echo [ERROR] Install failed. Check %%TEMP%%\KillConfirmGameBar_Install.log
        pause
        exit /b 1
    )
    echo [SUCCESS] Installed and applied loopback/GSI fixes.
) else (
    echo [ERROR] Could not find the installer at %INSTALL_SCRIPT%.
    echo Please check the build output above.
    pause
    exit /b 1
)
pause
