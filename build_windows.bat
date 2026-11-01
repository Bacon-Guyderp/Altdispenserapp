@echo off
REM Builds dist\N64AssetStudio.exe (single file, no console window).
REM Requires Python 3.10+ from python.org (includes Tkinter).
setlocal
cd /d "%~dp0"
python -m pip install --upgrade pip || goto :error
python -m pip install -r requirements.txt pyinstaller || goto :error
python -m PyInstaller --noconfirm --clean --onefile --windowed ^
  --name N64AssetStudio ^
  --icon assets\icon.ico ^
  --add-data "assets\icon.ico;assets" ^
  --add-data "assets\icon.png;assets" ^
  main.py || goto :error
echo.
echo Built dist\N64AssetStudio.exe
exit /b 0
:error
echo Build failed.
exit /b 1
