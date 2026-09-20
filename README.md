# Hunyuan3D-2.1 Unity Editor Bridge

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B%20%7C%202022.3%20%7C%20Unity%206-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![glTF Support](<https://img.shields.io/badge/Output-glTF%202.0%20Binary%20(.glb)-orange.svg>)](https://www.khronos.org/gltf/)
[![Hunyuan3D](https://img.shields.io/badge/AI%20Model-Tencent%20Hunyuan3D--2.1-red.svg)](https://github.com/Tencent/Hunyuan3D-2)
[![Open In Colab](https://colab.research.google.com/assets/colab-badge.svg)](https://colab.research.google.com/github/haophuc-tonthat/unity-hunyuan3d-bridge/blob/main/server/Hunyuan3D_Cloud_Server.ipynb)

A production-grade, non-blocking **Unity Editor Bridge** for [Tencent Hunyuan3D-2.1](https://github.com/Tencent/Hunyuan3D-2). Reconstruct high-fidelity 3D meshes (`.glb`) from single 2D concept images directly inside Unity Editor using your local GPU or a remote FastAPI inference server.

---

## 🌟 Key Features

- **⚡ Zero Editor Freezes:** Dispatches asynchronous HTTP tasks and polls server status smoothly without blocking the Unity main thread.
- **🎨 Drag-and-Drop Workflow:** Simply drag a PNG/JPG concept art directly into the Editor window to inspect resolution, size, and alpha channel before generating.
- **🛡️ Strict Binary Validation:** Automatically verifies the 12-byte glTF 2.0 container header (`glTF` magic `0x46546C67`, version 2), catches HTML/JSON server errors, and computes cryptographic SHA256 checksums.
- **📊 Sidecar Audit Reports:** Every generation creates a reproducible JSON report capturing seed, inference steps, octree resolution, GPU timings, and file hashes.
- **🖥️ Local Server Management:** Start and stop the local Python FastAPI server directly from the Unity Editor, with automatic output logging to `Logs/Hunyuan/`.
- **📦 Zero Runtime Overhead:** 100% Editor-only tooling. Introducing this package introduces **ZERO** runtime game dependencies, model weights, or Python requirements to your player builds.

---

## 🏗️ Architecture

```text
┌────────────────────────────────────────────────────────┐
│                   Unity Editor Window                  │
│               [Tools -> Hunyuan3D -> Generator]        │
│                           │                            │
│                           ▼                            │
│            [HunyuanApiClient] (Async WebRequest)       │
└───────────────────────────┬────────────────────────────┘
                            │
                            │ 1. POST /send (Base64 Image + JSON)
                            ▼
┌────────────────────────────────────────────────────────┐
│           Hunyuan3D-2.1 Local Server (FastAPI)         │
│                   http://127.0.0.1:8081                │
│                           │                            │
│                           ▼                            │
│            Hunyuan3D DiT Shape Flow (CUDA)             │
│                 [NVIDIA GPU / CUDA VRAM]               │
│                           │                            │
│                           ▼                            │
│            glTF 2.0 Binary Mesh (.glb) Export          │
└───────────────────────────┬────────────────────────────┘
                            │
                            │ 2. GET /status/{uid} -> Decode Base64
                            ▼
┌────────────────────────────────────────────────────────┐
│                   Project Staging Area                 │
│               Assets/Hunyuan3D/Generated/              │
│               ├── Input/   (Source Concept Art)        │
│               ├── Output/  (Validated 3D .glb Meshes)  │
│               └── Reports/ (JSON Audit Sidecars)       │
└────────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start in 2 Minutes

### 1. Install via Unity Package Manager (UPM)

In Unity Editor:

1. Go to **Window** ➔ **Package Manager**.
2. Click the **`+`** button in the top-left corner.
3. Select **Add package from git URL...**
4. Enter:
   ```text
   https://github.com/haophuc-tonthat/unity-hunyuan3d-bridge.git
   ```
   _(Or clone into your project's `Packages/` or `Assets/` folder)._

### 2. Start the Hunyuan3D Server (Cloud or Local)

#### 🌐 Option A: Free Cloud GPU (No Local NVIDIA GPU Required)

Ideal for **MacBooks**, office laptops, or GPUs with < 8GB VRAM:

1. Open the included **[Google Colab Notebook](https://colab.research.google.com/github/haophuc-tonthat/unity-hunyuan3d-bridge/blob/main/server/Hunyuan3D_Cloud_Server.ipynb)**.
2. Select **Runtime ➔ Run all** (uses free Google T4 GPU).
3. Copy the generated public tunnel URL (e.g. `https://xxxx.trycloudflare.com`) and paste it into Unity's **Server URL** field.

#### 💻 Option B: Local NVIDIA GPU (CUDA Enabled, ≥ 8 GB VRAM)

1. Double-click `server/setup_local.bat` for automated 1-click environment setup.
2. Launch the server via `server/run_server.bat` or from the Unity Editor's **Local Server Launcher** foldout.

### 3. Open the Generator in Unity

1. In the Unity menu, go to **Tools** ➔ **Hunyuan3D** ➔ **Generator**.
2. Click **CHECK SERVER** (should display **`[ONLINE]`**).
3. Drag and drop any character or prop PNG/JPG into the window.
4. Select a preset (e.g. **Production Base**).
5. Click **GENERATE 3D**!
6. In ~30–45 seconds, click **REVEAL IN EXPLORER** to inspect your new `.glb` model!

---

## ⚙️ Tested Presets & Hardware Tuning

| Preset              | Octree Res | Steps  | Guidance | VRAM Recommended | Generation Time |
| :------------------ | :--------: | :----: | :------: | :--------------: | :-------------: |
| **Fast Test**       |    256     |   20   |   5.0    |     >= 8 GB      |    ~15 – 20s    |
| **Production Base** |  **384**   | **30** | **5.0**  |   **>= 10 GB**   |  **~30 – 45s**  |
| **Custom**          |  64 – 512  | 5 – 50 |  1 – 15  |     >= 12 GB     |    Variable     |

> **💡 GPU Hardware Recommendation (≤ 12 GB VRAM):**  
> We strongly recommend keeping **Generate Texture = OFF** for shape generation. The shape pipeline generates clean watertight geometry in 30 seconds while utilizing ~7.2 GB VRAM. Texturing requires high multi-view inpainting memory and is intended for 16GB+ GPUs.

---

## 📝 Sidecar Audit Report Schema

Every generated model produces an accompanying JSON sidecar:

```json
{
  "timestamp": "2026-09-21T01:34:49Z",
  "serverUrl": "http://127.0.0.1:8081",
  "sourceImagePath": "Assets/Hunyuan3D/Generated/Input/character_a_pose.png",
  "sourceImageSha256": "4260b9a45c39fc4045bae81d27f8eb17127cdb201df614193077268d996ce436",
  "sourceImageResolution": "1024x1792",
  "seed": 1234,
  "removeBackground": true,
  "texture": false,
  "octreeResolution": 384,
  "numInferenceSteps": 30,
  "guidanceScale": 5.0,
  "numChunks": 8000,
  "outputType": "glb",
  "uid": "14a27852-72a6-44f9-b671-597e4d8a7af7",
  "elapsedSeconds": 32.11,
  "outputGlbPath": "Assets/Hunyuan3D/Generated/Output/character_seed1234_o384_v001.glb",
  "outputGlbSha256": "90e95777255e6895e1048f863163184fae818dd2208c9a29cd031e7907a9ae7f",
  "outputByteSize": 5449984,
  "success": true,
  "error": ""
}
```

---

## 🎯 Best Practices for Input Images

For optimal single-image 3D reconstruction results:

1. **Pose:** Symmetrical A-pose with arms 15–20° away from torso and feet slightly apart.
2. **Background:** Clean neutral gray background (or enable **Remove Background**).
3. **Framing:** Full body centered, occupying ~85% of image height.
4. **Lighting:** Soft, balanced studio lighting without harsh specular glares.
5. **Simplicity:** Avoid floating props, weapons held across the torso, or complex turnarounds on a single sheet.

---

## 🧪 Unit Tests

This package includes a comprehensive NUnit EditMode test suite covering:

- Request serialization against official FastAPI schema.
- Status and health response parsing.
- glTF 2.0 12-byte binary container validation.
- Corrupt / truncated / HTML error payload rejection.
- Cryptographic SHA256 calculation.
- Filename generation and preset application.

Run tests via Unity's **Test Runner** window (**Window** ➔ **General** ➔ **Test Runner** ➔ **EditMode**).

---

## 📄 License & Disclaimer

- **Package License:** [MIT License](LICENSE)
- **Hunyuan3D Upstream:** Subject to the [Tencent Hunyuan Non-Commercial License Agreement](https://github.com/Tencent/Hunyuan3D-2/blob/main/LICENSE).
- **Disclaimer:** This is an independent, community-developed open-source integration. It is not affiliated with, sponsored by, or endorsed by Tencent Holdings Limited.
