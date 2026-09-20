# Hunyuan3D Server Setup & Deployment Guide

This directory contains automation scripts and deployment guides for running the Hunyuan3D-2.1 inference backend for Unity Editor.

---

## 🚀 Option 1: Run on Free Cloud GPU (Google Colab) — Recommended for Machines with < 8 GB VRAM or Non-NVIDIA Systems

If your local computer does not have an NVIDIA GPU with at least 8GB–10GB VRAM (e.g., MacBook, office laptop, AMD GPU, or GTX 1650/1660):

[![Open In Colab](https://colab.research.google.com/assets/colab-badge.svg)](https://colab.research.google.com/github/haophuc-tonthat/unity-hunyuan3d-bridge/blob/main/server/Hunyuan3D_Cloud_Server.ipynb)

1. Open the [Hunyuan3D_Cloud_Server.ipynb](Hunyuan3D_Cloud_Server.ipynb) notebook in Google Colab.
2. Select **Runtime** ➔ **Change runtime type** ➔ Choose **T4 GPU** (free tier).
3. Select **Runtime** ➔ **Run all**.
4. Once loaded, the notebook automatically starts a free HTTPS Cloudflare Tunnel and prints:
   ```text
   🎉 HUNYUAN3D CLOUD SERVER IS READY FOR UNITY!
   👉 Copy this Server URL into Unity: https://xxxx-xxxx.trycloudflare.com
   ```
5. Paste that URL into the **Server URL** field in Unity (**Tools ➔ Hunyuan3D ➔ Generator**).
6. Click **Check Server** ➔ You can now generate 3D models from any machine without installing Python or CUDA locally!

---

## 💻 Option 2: Local GPU Setup (NVIDIA CUDA)

For machines with a CUDA-enabled NVIDIA GPU (≥ 8 GB VRAM recommended):
_(Compatible with any CUDA-capable NVIDIA card: GeForce RTX series, GTX series, Quadro/RTX Workstation, or Tesla/Data Center GPUs with Compute Capability ≥ 7.5)_

### Automated 1-Click Setup:

Double-click `setup_local.bat`. The script will automatically:

1. Detect Git and Conda/Python.
2. Clone the official [Tencent/Hunyuan3D-2](https://github.com/Tencent/Hunyuan3D-2) repository.
3. Create the `hunyuan3d21` Conda environment with Python 3.10 and PyTorch CUDA 12.4.
4. Install all required dependencies.
5. Apply the Unity Bridge optimization patch (`patch_hunyuan_server.py`).

### Manual Setup:

```bash
# 1. Clone Hunyuan3D repository
git clone https://github.com/Tencent/Hunyuan3D-2.git Hunyuan3D-2.1
cd Hunyuan3D-2.1

# 2. Create and activate conda environment
conda create -y -n hunyuan3d21 python=3.10
conda activate hunyuan3d21

# 3. Install PyTorch with CUDA 12.4
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124

# 4. Install dependencies
pip install -r requirements-windows-shape.txt
pip install fastapi uvicorn pydantic trimesh rembg

# 5. Apply the Unity Bridge patch
python ../patch_hunyuan_server.py .
```

### Starting the Local Server:

- **Option A:** Double-click `run_server.bat`.
- **Option B:** From the Unity Editor window, expand **LOCAL SERVER LAUNCHER** and click **START LOCAL SERVER**.
- **Option C:** From terminal:
  ```bash
  python api_server.py --host 127.0.0.1 --port 8081
  ```

---

## 🔧 Why is the Patch Necessary?

The included `patch_hunyuan_server.py` resolves two critical upstream constraints:

1. **Inference Steps Limit:** Upstream `api_models.py` restricts `num_inference_steps <= 20`. The patch expands this to `<= 50` to allow production-quality detail.
2. **Shape-Only VRAM Protection:** Upstream unconditionally loads multi-view inpainting texture checkpoints that crash machines lacking `RealESRGAN_x4plus.pth` or on <= 10GB GPUs. The patch wraps texturing in graceful fallbacks, ensuring rock-solid shape generation.
3. **Windows File Lock:** Fixes a Windows `[WinError 32]` crash when cleaning cache while active logs are open.
