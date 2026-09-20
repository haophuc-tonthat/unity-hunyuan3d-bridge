#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Hunyuan3DBridge.Editor
{
    /// <summary>
    /// Request payload schema corresponding to the official Hunyuan3D-2.1 POST /send endpoint.
    /// Uses snake_case to serialize directly to FastAPI Pydantic schema.
    /// </summary>
    [Serializable]
    public sealed class HunyuanGenerationRequest
    {
        public string image;
        public bool remove_background = true;
        public bool texture = false;
        public int seed = 1234;
        public int octree_resolution = 384;
        public int num_inference_steps = 30;
        public float guidance_scale = 5.0f;
        public int num_chunks = 8000;
        public int face_count = 40000;
        public string type = "glb";

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static HunyuanGenerationRequest FromJson(string json)
        {
            return JsonUtility.FromJson<HunyuanGenerationRequest>(json);
        }
    }
}
#endif
