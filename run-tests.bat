@echo off
setlocal
echo.
echo ==============================================================================
echo Smart Dam Control System - STQA Automated Test Runner
echo ==============================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-tests.ps1"

endlocal
