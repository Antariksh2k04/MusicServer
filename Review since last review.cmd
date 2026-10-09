@echo off
powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -File "%~dp0scripts\copy-review-prompt.ps1"
exit /b %errorlevel%
