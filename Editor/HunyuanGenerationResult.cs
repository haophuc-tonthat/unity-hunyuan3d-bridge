#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Hunyuan3DBridge.Editor
{
    [Serializable]
    public sealed class HunyuanSendResponse
    {
        public string uid;
    }

    [Serializable]
    public sealed class HunyuanStatusResponse
    {
        public string status;
        public string model_base64;
        public string message;
    }

    [Serializable]
    public sealed class HunyuanHealthResponse
    {
        public string status;
        public string worker_id;
    }

    [Serializable]
    public sealed class HunyuanUnloadResponse
    {
        public string status;
        public float freed_mb;
        public string message;
    }

    /// <summary>
    /// Deterministic sidecar audit report for reproduction and QA tracking.
    /// Saved alongside generated GLB files in Reports/.
    /// </summary>
    [Serializable]
    public sealed class HunyuanGenerationReport
    {
        public string timestamp;
        public string serverUrl;
        public string sourceImagePath;
        public string sourceImageSha256;
        public string sourceImageResolution;
        public int seed;
        public bool removeBackground;
        public bool texture;
        public int octreeResolution;
        public int numInferenceSteps;
        public float guidanceScale;
        public int numChunks;
        public string outputType;
        public string uid;
        public float elapsedSeconds;
        public string outputGlbPath;
        public string outputGlbSha256;
        public long outputByteSize;
        public bool success;
        public string error;

        public string ToJson(bool pretty = true)
        {
            return JsonUtility.ToJson(this, pretty);
        }

        public static HunyuanGenerationReport FromJson(string json)
        {
            return JsonUtility.FromJson<HunyuanGenerationReport>(json);
        }
    }
}
#endif
