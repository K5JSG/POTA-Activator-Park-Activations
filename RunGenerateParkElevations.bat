@echo off
REM Runs GenerateParkElevations.ps1 from whatever folder this .bat file is in,
REM regardless of where you double-click it from.

cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\GenerateParkElevations.ps1"

echo.
echo Done. Press any key to close this window.
pause >nul
