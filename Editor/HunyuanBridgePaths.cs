#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace Hunyuan3DBridge.Editor
{
    /// <summary>
    /// Path resolver for Hunyuan3D generation staging, input, output, and reports.
    /// Configurable per project, defaults to Assets/Hunyuan3D/Generated/ or project root.
    /// </summary>
    public static class HunyuanBridgePaths
    {
        public const string DefaultStagingDirectory = "Assets/Hunyuan3D/Generated";
        public const string DefaultLogsDirectory = "Logs/Hunyuan";

        public static string ProjectRoot
        {
            get
            {
                var dir = Directory.GetParent(Application.dataPath);
                return dir == null ? Application.dataPath : dir.FullName;
            }
        }

        public static string GetAbsolutePath(string relativeOrAbsolutePath)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolutePath)) return ProjectRoot;
            if (Path.IsPathRooted(relativeOrAbsolutePath)) return Path.GetFullPath(relativeOrAbsolutePath);
            return Path.GetFullPath(Path.Combine(ProjectRoot, relativeOrAbsolutePath));
        }

        public static string GetInputDirectory(string baseDir = DefaultStagingDirectory)
        {
            var p = Path.Combine(GetAbsolutePath(baseDir), "Input");
            Directory.CreateDirectory(p);
            return p;
        }

        public static string GetOutputDirectory(string baseDir = DefaultStagingDirectory)
        {
            var p = Path.Combine(GetAbsolutePath(baseDir), "Output");
            Directory.CreateDirectory(p);
            return p;
        }

        public static string GetReportsDirectory(string baseDir = DefaultStagingDirectory)
        {
            var p = Path.Combine(GetAbsolutePath(baseDir), "Reports");
            Directory.CreateDirectory(p);
            return p;
        }

        public static string GetLogsDirectory()
        {
            var p = GetAbsolutePath(DefaultLogsDirectory);
            Directory.CreateDirectory(p);
            return p;
        }
    }
}
#endif
