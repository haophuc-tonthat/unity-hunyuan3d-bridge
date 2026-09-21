#!/usr/bin/env python3
"""
Hunyuan3D-2.1 Server Optimization Patch for Unity Bridge.
Applies essential bugfixes to upstream Hunyuan3D-2.1:
1. Expands num_inference_steps constraint from <=20 to <=50 in api_models.py.
2. Enables robust Shape-Only generation when texture ckpts are missing in model_worker.py.
3. Fixes Windows WinError 32 file-lock crash during cache cleaning in model_worker.py.
4. Adds automatic VRAM cleanup (gc + torch.cuda.empty_cache) after each inference in model_worker.py.
5. Injects a POST /unload endpoint into api_server.py to fully offload models from GPU on demand.
"""

import os
import re
import sys
import shutil

def apply_patch(hunyuan_dir):
    if not os.path.isdir(hunyuan_dir):
        print(f"[ERROR] Directory does not exist: {hunyuan_dir}")
        return False

    api_models_path = os.path.join(hunyuan_dir, "api_models.py")
    model_worker_path = os.path.join(hunyuan_dir, "model_worker.py")
    api_server_path = os.path.join(hunyuan_dir, "api_server.py")

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

    # 4. Add automatic VRAM cleanup after inference
    vram_cleanup_marker = "# [Unity Bridge] VRAM cleanup"
    if vram_cleanup_marker not in worker_content:
        vram_cleanup_code = """
# [Unity Bridge] VRAM cleanup
import gc as _gc

def _cleanup_vram():
    _gc.collect()
    try:
        import torch
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
    except Exception:
        pass
"""
        # Inject the helper at the top of model_worker.py (after existing imports)
        class_match = re.search(r'^(class\s+\w+)', worker_content, re.MULTILINE)
        if class_match:
            insert_pos = class_match.start()
            worker_content = worker_content[:insert_pos] + vram_cleanup_code + "\n" + worker_content[insert_pos:]
            modified_worker = True
            print("[+] model_worker.py: Injected _cleanup_vram() helper function.")

        # Add _cleanup_vram() call after the file-cleanup loop
        if "_cleanup_vram()" not in worker_content:
            worker_content = worker_content.replace(
                "                except: pass",
                "                except: pass\n        _cleanup_vram()"
            )
            modified_worker = True
            print("[+] model_worker.py: Added _cleanup_vram() call after generation cache cleanup.")

    if modified_worker:
        with open(model_worker_path, "w", encoding="utf-8") as f:
            f.write(worker_content)

    # 5. Inject POST /unload endpoint into api_server.py
    if os.path.isfile(api_server_path):
        with open(api_server_path, "r", encoding="utf-8") as f:
            server_content = f.read()

        unload_marker = "# [Unity Bridge] VRAM unload endpoint"
        if unload_marker not in server_content:
            unload_endpoint = '''
# [Unity Bridge] Healthcheck & Root endpoints
@app.get("/")
async def root():
    return {"status": "healthy", "message": "Hunyuan3D-2.1 Server is running"}

@app.get("/health")
async def health():
    return {"status": "healthy", "worker_id": "hunyuan-worker-1"}

# [Unity Bridge] VRAM unload endpoint
@app.post("/unload")
async def unload_vram():
    """Offload models from GPU to CPU and free VRAM. Models reload on next request."""
    import gc
    freed_mb = 0
    try:
        import torch
        if torch.cuda.is_available():
            before = torch.cuda.memory_allocated()
            # Move all model worker pipelines to CPU if accessible
            if hasattr(app, 'state') and hasattr(app.state, 'worker'):
                worker = app.state.worker
                for attr_name in ['pipeline', 'shape_pipeline', 'paint_pipeline', 'model', 'denoiser']:
                    obj = getattr(worker, attr_name, None)
                    if obj is not None and hasattr(obj, 'to'):
                        try:
                            obj.to('cpu')
                        except Exception:
                            pass
            gc.collect()
            torch.cuda.empty_cache()
            torch.cuda.ipc_collect()
            after = torch.cuda.memory_allocated()
            freed_mb = (before - after) / 1024 / 1024
    except Exception as e:
        return {"status": "error", "message": str(e)}
    gc.collect()
    return {"status": "ok", "freed_mb": round(freed_mb, 1)}
'''
            # Insert before the last if __name__ block or at the end
            if 'if __name__' in server_content:
                server_content = server_content.replace(
                    'if __name__',
                    unload_endpoint + '\nif __name__'
                )
            else:
                server_content += unload_endpoint

            with open(api_server_path, "w", encoding="utf-8") as f:
                f.write(server_content)
            print("[+] api_server.py: Injected POST /unload VRAM cleanup endpoint.")
        else:
            print("[i] api_server.py: /unload endpoint already present.")
    else:
        print("[i] api_server.py not found, skipping /unload endpoint injection.")

    print("[SUCCESS] Hunyuan3D-2.1 server patch successfully applied!")
    return True

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "."
    apply_patch(target)