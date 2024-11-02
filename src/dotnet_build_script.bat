@echo off

:: Set PowerShell script path
set SCRIPT_PATH="%~dp0dotnet_build_script.ps1"

:: Check if PowerShell script exists
if not exist %SCRIPT_PATH% (
    echo PowerShell script not found: %SCRIPT_PATH%
    exit /b 1
)

:: Execute PowerShell script
PowerShell -NoProfile -ExecutionPolicy Bypass -File %SCRIPT_PATH%

:: Pause to keep the window open
echo.
pause