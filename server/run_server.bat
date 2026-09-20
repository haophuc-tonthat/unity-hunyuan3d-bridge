@echo off
echo ===================================================
echo   Hunyuan3D-2.1 Server Launcher (Unity Bridge)
echo ===================================================

set CONDA_ENV=hunyuan3d21
set HOST=127.0.0.1
set PORT=8081

echo [*] Activating Conda environment: %CONDA_ENV%...
call conda activate %CONDA_ENV% 2>nul
if errorlevel 1 (
    echo [!] Conda environment '%CONDA_ENV%' could not be activated automatically.
    echo [*] Attempting to launch with default Python in PATH...
)

echo [*] Starting Hunyuan3D-2.1 FastAPI server on http://%HOST%:%PORT%...
python api_server.py --host %HOST% --port %PORT%

pause
