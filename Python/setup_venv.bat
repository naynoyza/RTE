@echo off
@rem [Setup] create venv + install packages (run once after clone)
cd /d "%~dp0"
if not exist venv (
    echo Creating venv...
    py -3.11 -m venv venv 2>nul || python -m venv venv
)
call venv\Scripts\activate.bat
python -m pip install --upgrade pip
@rem PyTorch with CUDA (NVIDIA GPU). No NVIDIA card? it still runs on CPU, just slower.
python -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
python -m pip install -r requirements.txt
echo.
echo Done. Run run_tracker.bat  (first run downloads models\yolo11n-pose.pt and models\hand_landmarker.task)
pause
