#!/usr/bin/env python3
"""
Hunyuan3D-2.1 Server Optimization Patch for Unity Bridge.
Applies essential bugfixes to upstream Hunyuan3D-2.1:
1. Expands num_inference_steps constraint from <=20 to <=50 in api_models.py.
2. Enables robust Shape-Only generation when texture ckpts are missing in model_worker.py.
3. Fixes Windows WinError 32 file-lock crash during cache cleaning in model_worker.py.
"""

import os
import sys
import shutil

def apply_patch(hunyuan_dir):
    if not os.path.isdir(hunyuan_dir):
        print(f"[ERROR] Directory does not exist: {hunyuan_dir}")
        return False

    api_models_path = os.path.join(hunyuan_dir, "api_models.py")
    model_worker_path = os.path.join(hunyuan_dir, "model_worker.py")

    if not os.path.isfile(api_models_path) or not os.path.isfile(model_worker_path):
        print(f"[ERROR] Required Hunyuan3D files (api_models.py, model_worker.py) not found in {hunyuan_dir}")
        return False

    print(f"[*] Patching Hunyuan3D-2.1 in: {hunyuan_dir}")

    # 1. Patch api_models.py
    with open(api_models_path, "r", encoding="utf-8") as f:
        api_models_content = f.read()

    if "le=20" in api_models_content and "num_inference_steps" in api_models_content:
        api_models_content = api_models_content.replace("le=20", "le=50")
        with open(api_models_path, "w", encoding="utf-8") as f:
            f.write(api_models_content)
        print("[+] api_models.py: Expanded num_inference_steps maximum to 50.")
    else:
        print("[i] api_models.py: Already patched or constraint not present.")

    # 2. Patch model_worker.py
    with open(model_worker_path, "r", encoding="utf-8") as f:
        worker_content = f.read()

    modified_worker = False

    # Fix Windows cache log file lock
    old_clean = "for file in os.listdir(self.save_dir):\n            os.remove(os.path.join(self.save_dir, file))"
    new_clean = "for file in os.listdir(self.save_dir):\n            if not file.endswith('.log'):\n                try:\n                    os.remove(os.path.join(self.save_dir, file))\n                except: pass"
    if old_clean in worker_content:
        worker_content = worker_content.replace(old_clean, new_clean)
        modified_worker = True
        print("[+] model_worker.py: Fixed Windows file lock issue on active log files.")

    # Fix shape-only fallback on paint pipeline
    old_paint = "self.paint_pipeline = Hunyuan3DPaintPipeline(conf)"
    new_paint = """try:
            self.paint_pipeline = Hunyuan3DPaintPipeline(conf)
        except Exception as e:
            logger.warning(f"Texture generation pipeline not initialized (Shape-only mode active): {e}")
            self.paint_pipeline = None"""
    if old_paint in worker_content and "except Exception as e:" not in worker_content:
        worker_content = worker_content.replace(old_paint, new_paint)
        modified_worker = True
        print("[+] model_worker.py: Wrapped texture pipeline in graceful shape-only fallback.")

    if modified_worker:
        with open(model_worker_path, "w", encoding="utf-8") as f:
            f.write(worker_content)

    print("[SUCCESS] Hunyuan3D-2.1 server patch successfully applied!")
    return True

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "."
    apply_patch(target)
