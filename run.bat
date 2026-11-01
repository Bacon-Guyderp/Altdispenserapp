@echo off
REM Run N64 Asset Studio from source (installs dependencies on first run).
cd /d "%~dp0"
python -c "import numpy, PIL" 2>nul || python -m pip install -r requirements.txt
start "" pythonw main.py %*
