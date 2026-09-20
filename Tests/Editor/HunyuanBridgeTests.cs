#if UNITY_EDITOR
using System;
using System.Text;
using NUnit.Framework;
using Hunyuan3DBridge.Editor;
using UnityEngine;

namespace Hunyuan3DBridge.Tests
{
    public sealed class HunyuanBridgeTests
    {
        [Test]
        public void RequestSerializationMatchesOfficialSchema()
        {
            var req = new HunyuanGenerationRequest
            {
                image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
                remove_background = true,
                texture = false,
                seed = 1234,
                octree_resolution = 384,
                num_inference_steps = 30,
                guidance_scale = 5.0f,
                num_chunks = 8000,
                face_count = 40000,
                type = "glb"
            };

            var json = req.ToJson();
            Assert.That(json, Does.Contain("remove_background"));
            Assert.That(json, Does.Contain("texture"));
            Assert.That(json, Does.Contain("1234"));
            Assert.That(json, Does.Contain("384"));
            Assert.That(json, Does.Contain("30"));
            Assert.That(json, Does.Contain("5.0"));
            Assert.That(json, Does.Contain("8000"));
            Assert.That(json, Does.Contain("glb"));
        }

        [Test]
        public void StatusJsonParsingHandlesAllStates()
        {
            var jsonProcessing = "{\"status\":\"processing\"}";
            var parsedProcessing = JsonUtility.FromJson<HunyuanStatusResponse>(jsonProcessing);
            Assert.That(parsedProcessing.status, Is.EqualTo("processing"));
            Assert.That(string.IsNullOrEmpty(parsedProcessing.model_base64), Is.True);

            var jsonCompleted = "{\"status\":\"completed\",\"model_base64\":\"Z2xURgI==\"}";
            var parsedCompleted = JsonUtility.FromJson<HunyuanStatusResponse>(jsonCompleted);
            Assert.That(parsedCompleted.status, Is.EqualTo("completed"));
            Assert.That(parsedCompleted.model_base64, Is.EqualTo("Z2xURgI=="));

            var jsonError = "{\"status\":\"error\",\"message\":\"CUDA out of memory\"}";
            var parsedError = JsonUtility.FromJson<HunyuanStatusResponse>(jsonError);
            Assert.That(parsedError.status, Is.EqualTo("error"));
            Assert.That(parsedError.message, Is.EqualTo("CUDA out of memory"));
        }

        [Test]
        public void HealthResponseJsonParsing()
        {
            var json = "{\"status\":\"healthy\",\"worker_id\":\"worker-test-42\"}";
            var health = JsonUtility.FromJson<HunyuanHealthResponse>(json);
            Assert.That(health.status, Is.EqualTo("healthy"));
            Assert.That(health.worker_id, Is.EqualTo("worker-test-42"));
        }

        [Test]
        public void Base64ImageAndGlbRoundtrip()
        {
            var testPayload = Encoding.UTF8.GetBytes("HUNYUAN_GLB_TEST_BINARY_PAYLOAD");
            var b64 = Convert.ToBase64String(testPayload);
            var decoded = Convert.FromBase64String(b64);

            Assert.That(decoded, Is.EqualTo(testPayload));
        }

        [Test]
        public void GlbMagicHeaderValidationAcceptsValidGltfBinary()
        {
            var bytes = new byte[12];
            Array.Copy(BitConverter.GetBytes(0x46546C67), 0, bytes, 0, 4);
            Array.Copy(BitConverter.GetBytes((uint)2), 0, bytes, 4, 4);
            Array.Copy(BitConverter.GetBytes((uint)12), 0, bytes, 8, 4);

            var validation = HunyuanBridgeValidation.ValidateGlbBytes(bytes);
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Version, Is.EqualTo(2));
            Assert.That(validation.HeaderLength, Is.EqualTo(12));
        }

        [Test]
        public void GlbValidationRejectsTruncatedOrCorruptHeaders()
        {
            var tooSmall = new byte[8];
            var valTooSmall = HunyuanBridgeValidation.ValidateGlbBytes(tooSmall);
            Assert.That(valTooSmall.IsValid, Is.False);
            Assert.That(valTooSmall.Error, Does.Contain("too small"));

            var badMagic = new byte[12];
            Array.Copy(BitConverter.GetBytes(0xDEADBEEF), 0, badMagic, 0, 4);
            Array.Copy(BitConverter.GetBytes((uint)2), 0, badMagic, 4, 4);
            Array.Copy(BitConverter.GetBytes((uint)12), 0, badMagic, 8, 4);

            var valBadMagic = HunyuanBridgeValidation.ValidateGlbBytes(badMagic);
            Assert.That(valBadMagic.IsValid, Is.False);
            Assert.That(valBadMagic.Error, Does.Contain("Invalid GLB magic header"));
        }

        [Test]
        public void GlbValidationRejectsHtmlAndJsonErrorBodies()
        {
            var htmlBytes = Encoding.UTF8.GetBytes("<!doctype html><html><body>502 Bad Gateway</body></html>");
            var valHtml = HunyuanBridgeValidation.ValidateGlbBytes(htmlBytes);
            Assert.That(valHtml.IsValid, Is.False);
            Assert.That(valHtml.Error, Does.Contain("text/error body"));

            var jsonBytes = Encoding.UTF8.GetBytes("{\"error\":\"CUDA error: out of memory\"}");
            var valJson = HunyuanBridgeValidation.ValidateGlbBytes(jsonBytes);
            Assert.That(valJson.IsValid, Is.False);
            Assert.That(valJson.Error, Does.Contain("text/error body"));
        }

        [Test]
        public void Sha256CalculationMatchesKnownDigest()
        {
            var input = Encoding.UTF8.GetBytes("NULL//SQUAD");
            var sha = HunyuanBridgeValidation.ComputeSha256(input);
            Assert.That(sha.Length, Is.EqualTo(64));
            Assert.That(sha, Is.EqualTo("de03517452d2f2b3e8e19b52a3ee59c417666b60e90c42ecaa3c10815779ec36"));
        }

        [Test]
        public void OutputFilenameFormatGeneratesDeterministicNames()
        {
            var filename = HunyuanBridgeValidation.GenerateOutputFilename("hero_mesh", 1234, 384, 1);
            Assert.That(filename, Is.EqualTo("hero_mesh_seed1234_o384_v001.glb"));

            var custom = HunyuanBridgeValidation.GenerateOutputFilename("weapon_sword", 42, 256, 5);
            Assert.That(custom, Is.EqualTo("weapon_sword_seed42_o256_v005.glb"));
        }

        [Test]
        public void SettingsPresetsApplyCorrectTuning()
        {
            var req = new HunyuanGenerationRequest();

            HunyuanBridgeSettings.ApplyPreset(HunyuanPreset.FastPreview, req);
            Assert.That(req.octree_resolution, Is.EqualTo(256));
            Assert.That(req.num_inference_steps, Is.EqualTo(20));
            Assert.That(req.texture, Is.False);

            HunyuanBridgeSettings.ApplyPreset(HunyuanPreset.StandardQuality, req);
            Assert.That(req.octree_resolution, Is.EqualTo(384));
            Assert.That(req.num_inference_steps, Is.EqualTo(30));
            Assert.That(req.texture, Is.False);
            Assert.That(req.seed, Is.EqualTo(1234));
        }

        [Test]
        public void InvalidImageHandlingRejectsMissingOrEmptyFiles()
        {
            var missing = HunyuanBridgeValidation.ValidateImageFile("C:/non_existent_image_xyz.png");
            Assert.That(missing.IsValid, Is.False);
            Assert.That(missing.Error, Does.Contain("does not exist"));

            var emptyPath = HunyuanBridgeValidation.ValidateImageFile("");
            Assert.That(emptyPath.IsValid, Is.False);
        }

        [Test, Explicit("Manual live integration test requiring active Hunyuan server on port 8081")]
        public void LocalServerHealthLiveCheck()
        {
            var task = HunyuanApiClient.CheckHealthAsync("http://127.0.0.1:8081", 3);
            task.Wait(4000);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result.IsOnline, Is.True);
            Assert.That(task.Result.Status, Is.EqualTo("healthy"));
        }
    }
}
#endif
