# Changelog

All notable changes to the Hunyuan3D-2.1 Unity Bridge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
