@echo off
@rem [Tracker] double-click to run tracker.py (uses venv if present)
cd /d "%~dp0"
if exist venv\Scripts\python.exe (
    venv\Scripts\python.exe tracker.py %*
) else (
    echo [!] No venv yet - run setup_venv.bat first. Using system python.
    python tracker.py %*
)
pause
