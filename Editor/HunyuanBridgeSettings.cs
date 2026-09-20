#if UNITY_EDITOR
using UnityEditor;

namespace Hunyuan3DBridge.Editor
{
    public enum HunyuanPreset
    {
        FastPreview,
        StandardQuality,
        Custom
    }

    /// <summary>
    /// Configuration and project presets for the Hunyuan3D-2.1 Editor Bridge.
    /// Manages server preferences via EditorPrefs and provides tested parameter presets.
    /// </summary>
    public static class HunyuanBridgeSettings
    {
        private const string PrefKeyServerUrl = "Hunyuan3D_Bridge_ServerUrl";
        private const string PrefKeyPythonPath = "Hunyuan3D_Bridge_PythonPath";
        private const string PrefKeyRepoPath = "Hunyuan3D_Bridge_RepoPath";
        private const string PrefKeyStagingDir = "Hunyuan3D_Bridge_StagingDir";

        public const string DefaultServerUrl = "http://127.0.0.1:8081";

        public static string ServerUrl
        {
            get => EditorPrefs.GetString(PrefKeyServerUrl, DefaultServerUrl);
            set => EditorPrefs.SetString(PrefKeyServerUrl, value);
        }

        public static string PythonPath
        {
            get => EditorPrefs.GetString(PrefKeyPythonPath, "");
            set => EditorPrefs.SetString(PrefKeyPythonPath, value);
        }

        public static string RepoPath
        {
            get => EditorPrefs.GetString(PrefKeyRepoPath, "");
            set => EditorPrefs.SetString(PrefKeyRepoPath, value);
        }

        public static string StagingDirectory
        {
            get => EditorPrefs.GetString(PrefKeyStagingDir, HunyuanBridgePaths.DefaultStagingDirectory);
            set => EditorPrefs.SetString(PrefKeyStagingDir, value);
        }

        public static void ApplyPreset(HunyuanPreset preset, HunyuanGenerationRequest request)
        {
            if (request == null) return;

            switch (preset)
            {
                case HunyuanPreset.FastPreview:
                    request.octree_resolution = 256;
                    request.num_inference_steps = 20;
                    request.guidance_scale = 5.0f;
                    request.num_chunks = 8000;
                    request.remove_background = true;
                    request.texture = false;
                    request.seed = 1234;
                    request.type = "glb";
                    break;

                case HunyuanPreset.StandardQuality:
                    request.octree_resolution = 384;
                    request.num_inference_steps = 30;
                    request.guidance_scale = 5.0f;
                    request.num_chunks = 8000;
                    request.remove_background = true;
                    request.texture = false;
                    request.seed = 1234;
                    request.type = "glb";
                    break;

                case HunyuanPreset.Custom:
                    break;
            }
        }
    }
}
#endif
