@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo    Hunyuan3D-2.1 Local Environment Setup for Unity
echo ==========================================================
echo.

:: 1. Check Git
where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Git is not installed or not in PATH!
    echo Please install Git from https://git-scm.com/ and rerun this script.
    pause
    exit /b 1
)

:: 2. Check Conda
where conda >nul 2>&1
if errorlevel 1 (
    echo [WARNING] Conda command not found in standard PATH.
    echo Checking standard Miniconda / Anaconda installation locations...
    if exist "%USERPROFILE%\miniconda3\Scripts\conda.exe" (
        set "CONDA_EXE=%USERPROFILE%\miniconda3\Scripts\conda.exe"
    ) else if exist "%USERPROFILE%\anaconda3\Scripts\conda.exe" (
        set "CONDA_EXE=%USERPROFILE%\anaconda3\Scripts\conda.exe"
    ) else (
        echo [ERROR] Miniconda or Anaconda is required to manage CUDA/PyTorch dependencies.
        echo Please install Miniconda from https://docs.conda.io/en/latest/miniconda.html
        pause
        exit /b 1
    )
) else (
    set "CONDA_EXE=conda"
)

:: 3. Clone Repository if needed
set "HUNYUAN_DIR=Hunyuan3D-2.1"
if not exist "%HUNYUAN_DIR%" (
    echo [*] Cloning official Tencent Hunyuan3D-2.1 repository...
    git clone https://github.com/Tencent/Hunyuan3D-2.git %HUNYUAN_DIR%
    if errorlevel 1 (
        echo [ERROR] Failed to clone Hunyuan3D repository.
        pause
        exit /b 1
    )
) else (
    echo [*] Found existing Hunyuan3D directory: %HUNYUAN_DIR%
)

:: 4. Create and configure Conda Environment
set "ENV_NAME=hunyuan3d21"
echo [*] Creating or updating Conda environment '%ENV_NAME%' (Python 3.10)...
call %CONDA_EXE% create -y -n %ENV_NAME% python=3.10
call %CONDA_EXE% activate %ENV_NAME%

:: 5. Install PyTorch with CUDA 12.4
echo [*] Installing PyTorch with CUDA 12.4 support...
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124

:: 6. Install Hunyuan dependencies
echo [*] Installing Hunyuan3D-2.1 core dependencies...
pushd %HUNYUAN_DIR%
pip install -r requirements-windows-shape.txt 2>nul || pip install -r requirements.txt
pip install fastapi uvicorn pydantic trimesh rembg
popd

:: 7. Apply Unity Bridge Patch
echo [*] Applying Unity Bridge step-limit and shape-only optimization patch...
python patch_hunyuan_server.py "%HUNYUAN_DIR%"

echo.
echo ==========================================================
echo [SUCCESS] Hunyuan3D-2.1 environment is ready!
echo.
echo To start the server:
echo   1. Run 'run_server.bat'
echo   2. Open Unity -> Tools -> Hunyuan3D -> Generator
echo   3. Click 'Check Server' (should show [ONLINE])
echo ==========================================================
pause
