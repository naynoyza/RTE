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
@rem download the MediaPipe lite pose model now (needs internet once) so the tracker also works offline
python -c "import mediapipe as mp; mp.solutions.pose.Pose(model_complexity=0).close(); print('MediaPipe pose model ready')"
echo.
echo Done. Run run_tracker.bat
pause
