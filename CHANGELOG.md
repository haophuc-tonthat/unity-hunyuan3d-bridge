# Changelog

All notable changes to the Hunyuan3D-2.1 Unity Bridge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-09-21

### Added
- Automatic VRAM cleanup after each inference (`gc.collect()` + `torch.cuda.empty_cache()`).
- `POST /unload` server endpoint to offload models from GPU to CPU and release VRAM on demand.
- **FREE VRAM** button in the Unity Editor Generator window (Server section).
- `UnloadVramAsync` method in `HunyuanApiClient` for programmatic VRAM release.
- `HunyuanUnloadResponse` and `UnloadVramResult` data types.

### Changed
- `patch_hunyuan_server.py` now applies 5 patches (was 3): added VRAM cleanup helper injection and `/unload` endpoint injection.
- Colab notebook Step 4 updated to include VRAM cleanup and `/unload` endpoint patches.

---
## [1.0.0] - 2026-09-21

### Added
- Initial public release of the Hunyuan3D-2.1 Unity Editor Bridge.
- Full Unity Editor GUI window under `Tools -> Hunyuan3D -> Generator`.
- Real-time server health checking and one-click Swagger API docs trigger.
- Asynchronous non-blocking generation pipeline using UnityWebRequest and TaskCompletionSource.
- glTF 2.0 binary container (.glb) header and payload integrity validation.
- Cryptographic SHA256 checksum calculation for source images and generated 3D meshes.
- Deterministic sidecar JSON generation reports for art pipeline audit trails.
- Tested presets: Fast Test (Octree 256 / 20 steps) and Production Base (Octree 384 / 30 steps).
- Local server management foldout with process start/stop and logging.
- Helper scripts: Windows one-click `run_server.bat` and `patch_hunyuan_server.py`.
- 12 comprehensive NUnit EditMode unit tests.
