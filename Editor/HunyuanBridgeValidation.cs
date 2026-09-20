#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Hunyuan3DBridge.Editor
{
    public struct ImageValidationResult
    {
        public bool IsValid;
        public string Error;
        public int Width;
        public int Height;
        public long FileSize;
        public bool HasAlpha;
    }

    public struct GlbValidationResult
    {
        public bool IsValid;
        public string Error;
        public uint Version;
        public uint HeaderLength;
        public long ActualLength;
        public string Sha256;
    }

    /// <summary>
    /// Strict input image and output binary validation.
    /// Checks 12-byte glTF 2.0 container headers, ensures non-empty payloads,
    /// catches HTML/JSON error bodies returned by web servers, and calculates SHA256.
    /// </summary>
    public static class HunyuanBridgeValidation
    {
        public const uint GlbMagic = 0x46546C67; // 'glTF' in little-endian ASCII (0x67, 0x6C, 0x54, 0x46)
        public const uint ExpectedVersion = 2;

        public static ImageValidationResult ValidateImageFile(string path)
        {
            var result = new ImageValidationResult();

            if (string.IsNullOrEmpty(path))
            {
                result.Error = "Image path is empty.";
                return result;
            }

            if (!File.Exists(path))
            {
                result.Error = $"Image file does not exist: {path}";
                return result;
            }

            var info = new FileInfo(path);
            result.FileSize = info.Length;
            if (result.FileSize == 0)
            {
                result.Error = "Image file is 0 bytes.";
                return result;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    result.Error = "Failed to load image texture bytes.";
                    return result;
                }

                result.Width = tex.width;
                result.Height = tex.height;
                result.HasAlpha = (tex.format == TextureFormat.RGBA32 || tex.format == TextureFormat.ARGB32 || tex.format == TextureFormat.DXT5);
                UnityEngine.Object.DestroyImmediate(tex);

                if (result.Width < 64 || result.Height < 64)
                {
                    result.Error = $"Image resolution too small ({result.Width}x{result.Height}). Minimum 64x64 required.";
                    return result;
                }

                result.IsValid = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Exception inspecting image: {ex.Message}";
                return result;
            }
        }

        public static GlbValidationResult ValidateGlbBytes(byte[] bytes)
        {
            var result = new GlbValidationResult();

            if (bytes == null || bytes.Length == 0)
            {
                result.Error = "GLB payload is null or 0 bytes.";
                return result;
            }

            result.ActualLength = bytes.Length;
            result.Sha256 = ComputeSha256(bytes);

            if (bytes.Length < 12)
            {
                result.Error = $"GLB payload is too small for 12-byte container header ({bytes.Length} bytes).";
                return result;
            }

            // Check if payload is an ASCII error body (HTML or JSON error response)
            var startAscii = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 64)).TrimStart();
            if (startAscii.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                startAscii.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ||
                startAscii.StartsWith("{\"detail\"", StringComparison.OrdinalIgnoreCase) ||
                startAscii.StartsWith("{\"error\"", StringComparison.OrdinalIgnoreCase))
            {
                result.Error = $"Received text/error body instead of binary GLB: {startAscii.Substring(0, Math.Min(startAscii.Length, 48))}...";
                return result;
            }

            var magic = BitConverter.ToUInt32(bytes, 0);
            result.Version = BitConverter.ToUInt32(bytes, 4);
            result.HeaderLength = BitConverter.ToUInt32(bytes, 8);

            if (magic != GlbMagic)
            {
                result.Error = $"Invalid GLB magic header: 0x{magic:X8} (expected 0x{GlbMagic:X8} 'glTF').";
                return result;
            }

            if (result.Version != ExpectedVersion)
            {
                result.Error = $"Unsupported GLB version: {result.Version} (expected {ExpectedVersion}).";
                return result;
            }

            if (result.HeaderLength > (uint)bytes.Length)
            {
                result.Error = $"Truncated GLB payload: Header declares {result.HeaderLength} bytes, but received {bytes.Length} bytes.";
                return result;
            }

            result.IsValid = true;
            return result;
        }

        public static string ComputeSha256(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string ComputeFileSha256(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            var bytes = File.ReadAllBytes(path);
            return ComputeSha256(bytes);
        }

        public static string GenerateOutputFilename(string prefix, int seed, int octreeResolution, int version = 1)
        {
            var cleanPrefix = string.IsNullOrEmpty(prefix) ? "model" : prefix.Trim();
            return $"{cleanPrefix}_seed{seed}_o{octreeResolution}_v{version:D3}.glb";
        }
    }
}
#endif
