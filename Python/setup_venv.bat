@echo off
@rem [Setup] create venv + install packages (run once after clone)
cd /d "%~dp0"
if not exist venv (
    echo Creating venv...
    py -3.11 -m venv venv 2>nul || python -m venv venv
)
call venv\Scripts\activate.bat
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
echo.
echo Done. Run run_tracker.bat
pause
