#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hunyuan3DBridge.Editor
{
    public enum ServerStatus
    {
        Unknown,
        Online,
        Offline,
        Generating,
        Completed,
        Error
    }

    /// <summary>
    /// Open-source Unity Editor Bridge Window for Tencent Hunyuan3D-2.1.
    /// Internal editor asset pipeline tooling. Enables single-click 3D mesh generation.
    /// </summary>
    public sealed class HunyuanBridgeWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private string serverUrl = HunyuanBridgeSettings.DefaultServerUrl;
        private ServerStatus serverStatus = ServerStatus.Unknown;
        private string serverWorkerId = "";
        private string serverMessage = "";

        // Local Server
        private bool showLocalServer = false;
        private string pythonPath;
        private string repoPath;
        private static Process localServerProcess;

        // Paths & Staging
        private string stagingDirectory = HunyuanBridgePaths.DefaultStagingDirectory;

        // Input Image
        private string inputImagePath = "";
        private Texture2D previewTexture;
        private ImageValidationResult imageInfo;

        // Settings
        private HunyuanPreset activePreset = HunyuanPreset.StandardQuality;
        private HunyuanGenerationRequest request = new HunyuanGenerationRequest();

        // Execution state
        private bool isGenerating = false;
        private string currentTaskUid = "";
        private float generationElapsedSeconds = 0f;
        private double generationStartTime = 0;
        private string currentStatusText = "";
        private CancellationTokenSource pollCancellation;

        // Output & Audit
        private string lastOutputGlbPath = "";
        private string lastOutputReportPath = "";
        private GlbValidationResult lastGlbValidation;

        [MenuItem("Tools/Hunyuan3D/Generator")]
        [MenuItem("Window/AI/Hunyuan3D Generator")]
        public static void Open()
        {
            var window = GetWindow<HunyuanBridgeWindow>("Hunyuan3D Generator");
            window.minSize = new Vector2(480, 720);
            window.Show();
        }

        private void OnEnable()
        {
            serverUrl = HunyuanBridgeSettings.ServerUrl;
            pythonPath = HunyuanBridgeSettings.PythonPath;
            repoPath = HunyuanBridgeSettings.RepoPath;
            stagingDirectory = HunyuanBridgeSettings.StagingDirectory;

            HunyuanBridgeSettings.ApplyPreset(activePreset, request);
            EditorApplication.update += OnEditorUpdate;

            CheckServerHealth();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CancelGeneration();
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }

        private void OnEditorUpdate()
        {
            if (isGenerating)
            {
                generationElapsedSeconds = (float)(EditorApplication.timeSinceStartup - generationStartTime);
                Repaint();
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            EditorGUILayout.Space(6);

            DrawServerSection();
            EditorGUILayout.Space(6);

            DrawLocalServerSection();
            EditorGUILayout.Space(6);

            DrawInputImageSection();
            EditorGUILayout.Space(6);

            DrawSettingsSection();
            EditorGUILayout.Space(6);

            DrawStagingSection();
            EditorGUILayout.Space(6);

            DrawGenerateSection();
            EditorGUILayout.Space(6);

            DrawOutputSection();

            EditorGUILayout.EndScrollView();
        }

        #region UI Sections

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = new Color(0.15f, 0.85f, 1f) } };
            EditorGUILayout.LabelField("HUNYUAN3D-2.1 UNITY BRIDGE", titleStyle);
            EditorGUILayout.LabelField("Single-Image 3D Mesh Reconstruction · glTF 2.0 Binary Export", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawServerSection()
        {
            EditorGUILayout.LabelField("1. HUNYUAN SERVER", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            serverUrl = EditorGUILayout.TextField("Server URL", serverUrl);
            if (EditorGUI.EndChangeCheck()) HunyuanBridgeSettings.ServerUrl = serverUrl;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("CHECK SERVER", GUILayout.Height(24)))
            {
                CheckServerHealth();
            }
            if (GUILayout.Button("OPEN API DOCS", GUILayout.Height(24)))
            {
                Application.OpenURL(serverUrl.TrimEnd('/') + "/docs");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Server Status");
            var statusColor = serverStatus switch
            {
                ServerStatus.Online => new Color(0.2f, 0.85f, 0.3f),
                ServerStatus.Generating => new Color(0.15f, 0.85f, 1f),
                ServerStatus.Completed => new Color(0.2f, 0.85f, 0.3f),
                ServerStatus.Offline => new Color(0.9f, 0.25f, 0.2f),
                ServerStatus.Error => new Color(1f, 0.6f, 0.1f),
                _ => Color.gray
            };
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = statusColor;
            GUILayout.Box($"[{serverStatus.ToString().ToUpperInvariant()}]", GUILayout.Height(20));
            GUI.backgroundColor = prevBg;

            if (!string.IsNullOrEmpty(serverWorkerId))
            {
                EditorGUILayout.LabelField($"Worker: {serverWorkerId}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(serverMessage))
            {
                EditorGUILayout.HelpBox(serverMessage, serverStatus == ServerStatus.Online ? MessageType.Info : MessageType.Warning);
            }

            if (serverStatus == ServerStatus.Offline || serverStatus == ServerStatus.Unknown)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Server is offline or not installed. Choose how you want to run Hunyuan3D:", MessageType.None);
                EditorGUILayout.BeginHorizontal();
                var prevBtn = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.75f, 0.20f);
                if (GUILayout.Button("🌐 RUN ON FREE GOOGLE COLAB", GUILayout.Height(26)))
                {
                    Application.OpenURL("https://colab.research.google.com/github/haophuc-tonthat/unity-hunyuan3d-bridge/blob/main/server/Hunyuan3D_Cloud_Server.ipynb");
                }
                GUI.backgroundColor = prevBtn;
                if (GUILayout.Button("📖 LOCAL INSTALL GUIDE", GUILayout.Height(26)))
                {
                    Application.OpenURL("https://github.com/haophuc-tonthat/unity-hunyuan3d-bridge/blob/main/server/README.md");
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLocalServerSection()
        {
            showLocalServer = EditorGUILayout.Foldout(showLocalServer, "LOCAL SERVER LAUNCHER (Optional)", true);
            if (!showLocalServer) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            pythonPath = EditorGUILayout.TextField("Python Path", pythonPath);
            if (EditorGUI.EndChangeCheck()) HunyuanBridgeSettings.PythonPath = pythonPath;

            EditorGUI.BeginChangeCheck();
            repoPath = EditorGUILayout.TextField("Hunyuan Repo Path", repoPath);
            if (EditorGUI.EndChangeCheck()) HunyuanBridgeSettings.RepoPath = repoPath;

            EditorGUILayout.BeginHorizontal();
            var isProcessRunning = localServerProcess != null && !localServerProcess.HasExited;
            GUI.enabled = !isProcessRunning;
            if (GUILayout.Button("START LOCAL SERVER", GUILayout.Height(26)))
            {
                StartLocalServer();
            }
            GUI.enabled = isProcessRunning;
            if (GUILayout.Button("STOP LOCAL SERVER", GUILayout.Height(26)))
            {
                StopLocalServer();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Launches: python api_server.py --host 127.0.0.1 --port 8081. Logs are written to Logs/Hunyuan/server.log.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawInputImageSection()
        {
            EditorGUILayout.LabelField("2. INPUT IMAGE", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            inputImagePath = EditorGUILayout.TextField("Image Path", inputImagePath);
            if (GUILayout.Button("BROWSE...", GUILayout.Width(80)))
            {
                var inputDir = HunyuanBridgePaths.GetInputDirectory(stagingDirectory);
                var picked = EditorUtility.OpenFilePanel("Select Concept Image", inputDir, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(picked))
                {
                    SetInputImage(picked);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Drag and drop zone
            var evt = Event.current;
            var dropArea = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag and Drop PNG/JPG here", EditorStyles.helpBox);
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.paths)
                    {
                        if (dragged.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            dragged.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            SetInputImage(dragged);
                            break;
                        }
                    }
                    evt.Use();
                }
            }

            // Preview & metadata
            if (previewTexture != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box(previewTexture, GUILayout.Width(96), GUILayout.Height(128));
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"File: {Path.GetFileName(inputImagePath)}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Resolution: {imageInfo.Width} x {imageInfo.Height}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Size: {imageInfo.FileSize / 1024f:F1} KB", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Alpha: {(imageInfo.HasAlpha ? "Detected" : "Opaque")}", EditorStyles.miniLabel);
                if (imageInfo.IsValid)
                {
                    EditorGUILayout.LabelField("Status: READY FOR RECONSTRUCTION", EditorStyles.miniBoldLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(imageInfo.Error, MessageType.Error);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("3. GENERATION SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            activePreset = (HunyuanPreset)EditorGUILayout.EnumPopup("Preset", activePreset);
            if (EditorGUI.EndChangeCheck())
            {
                HunyuanBridgeSettings.ApplyPreset(activePreset, request);
            }

            GUI.enabled = (activePreset == HunyuanPreset.Custom);
            request.octree_resolution = EditorGUILayout.IntSlider("Octree Resolution", request.octree_resolution, 64, 512);
            request.num_inference_steps = EditorGUILayout.IntSlider("Inference Steps", request.num_inference_steps, 5, 50);
            request.guidance_scale = EditorGUILayout.Slider("Guidance Scale", request.guidance_scale, 1f, 15f);
            request.num_chunks = EditorGUILayout.IntSlider("Chunks", request.num_chunks, 1000, 20000);
            request.seed = EditorGUILayout.IntField("Seed", request.seed);
            request.remove_background = EditorGUILayout.Toggle("Remove Background", request.remove_background);

            EditorGUI.BeginChangeCheck();
            request.texture = EditorGUILayout.Toggle("Generate Texture", request.texture);
            if (EditorGUI.EndChangeCheck() && request.texture)
            {
                Debug.LogWarning("[Hunyuan3DBridge] Texturing enabled. VRAM usage will increase significantly.");
            }
            GUI.enabled = true;

            if (request.texture)
            {
                EditorGUILayout.HelpBox("⚠ VRAM WARNING: Texturing requires high GPU memory (>= 16GB VRAM recommended). On 10-12GB GPUs, shape-only mode is recommended.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Shape-Only Mode Active: Fast, robust DiT shape generation directly to untextured glTF 2.0 binary (.glb).", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStagingSection()
        {
            EditorGUILayout.LabelField("4. STAGING PATHS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            stagingDirectory = EditorGUILayout.TextField("Staging Directory", stagingDirectory);
            if (EditorGUI.EndChangeCheck()) HunyuanBridgeSettings.StagingDirectory = stagingDirectory;

            EditorGUILayout.LabelField($"Output: {HunyuanBridgePaths.GetOutputDirectory(stagingDirectory)}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateSection()
        {
            EditorGUILayout.LabelField("5. GENERATION", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUI.enabled = !isGenerating && imageInfo.IsValid && serverStatus == ServerStatus.Online;
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.85f, 1.0f);
            if (GUILayout.Button(isGenerating ? "GENERATING 3D MESH..." : "GENERATE 3D", GUILayout.Height(36)))
            {
                StartGeneration();
            }
            GUI.backgroundColor = prevBg;
            GUI.enabled = true;

            if (isGenerating)
            {
                EditorGUILayout.Space(6);

                // Compute expected duration & dynamic visual progress percentage
                float expectedDuration = (request.octree_resolution <= 256) ? 35f : 95f;
                float progressFraction;
                string phaseName;

                if (generationElapsedSeconds < 3.0f)
                {
                    progressFraction = Mathf.Lerp(0.05f, 0.15f, generationElapsedSeconds / 3.0f);
                    phaseName = "[Phase 1/4] Uploading & Preprocessing Image...";
                }
                else if (generationElapsedSeconds < expectedDuration * 0.65f)
                {
                    float t = (generationElapsedSeconds - 3.0f) / (expectedDuration * 0.65f - 3.0f);
                    progressFraction = Mathf.Lerp(0.15f, 0.65f, t);
                    phaseName = "[Phase 2/4] DiT Shape Flow Sampling (GPU Denoising)...";
                }
                else if (generationElapsedSeconds < expectedDuration)
                {
                    float t = (generationElapsedSeconds - expectedDuration * 0.65f) / (expectedDuration * 0.35f);
                    progressFraction = Mathf.Lerp(0.65f, 0.90f, t);
                    phaseName = "[Phase 3/4] High-Res Octree Decoding & Marching Cubes...";
                }
                else
                {
                    float overTime = generationElapsedSeconds - expectedDuration;
                    progressFraction = 0.90f + 0.06f * (1.0f - Mathf.Exp(-overTime / 40.0f));
                    phaseName = "[Phase 3/4] High-Res Octree Finishing (Heavy VRAM)...";
                }

                // Visual Animated Progress Bar
                Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(26), GUILayout.ExpandWidth(true));
                string barLabel = $"{progressFraction * 100f:F0}% — {phaseName}";
                EditorGUI.ProgressBar(progressRect, progressFraction, barLabel);

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Task: {currentTaskUid}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Elapsed: {generationElapsedSeconds:F1}s / ~{expectedDuration:F0}s", EditorStyles.miniBoldLabel, GUILayout.Width(130));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Status: {currentStatusText}", EditorStyles.boldLabel);

                if (GUILayout.Button("CANCEL POLLING", GUILayout.Height(22)))
                {
                    CancelGeneration();
                }
            }
            else if (!string.IsNullOrEmpty(currentStatusText))
            {
                EditorGUILayout.Space(4);
                if (serverStatus == ServerStatus.Completed)
                {
                    Rect doneRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(24), GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(doneRect, 1.0f, "100% — [Phase 4/4] Completed! glTF 2.0 Binary Validated.");
                    EditorGUILayout.Space(2);
                }
                var msgType = serverStatus == ServerStatus.Error ? MessageType.Error :
                              serverStatus == ServerStatus.Completed ? MessageType.Info : MessageType.None;
                EditorGUILayout.HelpBox(currentStatusText, msgType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawOutputSection()
        {
            if (string.IsNullOrEmpty(lastOutputGlbPath)) return;

            EditorGUILayout.LabelField("6. GENERATION RESULT & AUDIT", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Output GLB: {lastOutputGlbPath}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"File Size: {lastGlbValidation.ActualLength / 1024f:F1} KB", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"SHA256: {lastGlbValidation.Sha256}", EditorStyles.wordWrappedMiniLabel);

            if (lastGlbValidation.IsValid)
            {
                EditorGUILayout.HelpBox($"✓ GLB VALIDATED: glTF version {lastGlbValidation.Version}, magic 0x{HunyuanBridgeValidation.GlbMagic:X8} correct.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"✗ GLB VALIDATION FAILED: {lastGlbValidation.Error}", MessageType.Error);
            }

            if (!string.IsNullOrEmpty(lastOutputReportPath))
            {
                EditorGUILayout.LabelField($"Report Sidecar: {Path.GetFileName(lastOutputReportPath)}", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("REVEAL IN EXPLORER", GUILayout.Height(26)))
            {
                EditorUtility.RevealInFinder(lastOutputGlbPath);
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Operations

        private async void CheckServerHealth()
        {
            serverStatus = ServerStatus.Unknown;
            serverMessage = "Pinging Hunyuan server...";
            Repaint();

            var health = await HunyuanApiClient.CheckHealthAsync(serverUrl);
            if (health.IsOnline)
            {
                serverStatus = ServerStatus.Online;
                serverWorkerId = health.WorkerId;
                serverMessage = $"Server Online · Worker {health.WorkerId} ready.";
                if (serverStatus != ServerStatus.Generating) currentStatusText = "";
            }
            else
            {
                serverStatus = ServerStatus.Offline;
                serverWorkerId = "";
                serverMessage = $"Server Offline: {health.Error}";
            }
            Repaint();
        }

        private void SetInputImage(string path)
        {
            inputImagePath = path;
            imageInfo = HunyuanBridgeValidation.ValidateImageFile(path);

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            if (imageInfo.IsValid && File.Exists(path))
            {
                previewTexture = new Texture2D(2, 2);
                previewTexture.LoadImage(File.ReadAllBytes(path));
            }
            Repaint();
        }

        private async void StartGeneration()
        {
            if (!imageInfo.IsValid || !File.Exists(inputImagePath))
            {
                EditorUtility.DisplayDialog("Hunyuan Bridge", "Invalid input image selected.", "OK");
                return;
            }

            isGenerating = true;
            serverStatus = ServerStatus.Generating;
            generationStartTime = EditorApplication.timeSinceStartup;
            currentStatusText = "Submitting task to Hunyuan server...";
            pollCancellation = new CancellationTokenSource();

            var outputDir = HunyuanBridgePaths.GetOutputDirectory(stagingDirectory);
            var reportsDir = HunyuanBridgePaths.GetReportsDirectory(stagingDirectory);

            var stopwatch = Stopwatch.StartNew();
            var report = new HunyuanGenerationReport
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                serverUrl = serverUrl,
                sourceImagePath = inputImagePath,
                sourceImageSha256 = HunyuanBridgeValidation.ComputeFileSha256(inputImagePath),
                sourceImageResolution = $"{imageInfo.Width}x{imageInfo.Height}",
                seed = request.seed,
                removeBackground = request.remove_background,
                texture = request.texture,
                octreeResolution = request.octree_resolution,
                numInferenceSteps = request.num_inference_steps,
                guidanceScale = request.guidance_scale,
                numChunks = request.num_chunks,
                outputType = request.type
            };

            try
            {
                var imgBytes = File.ReadAllBytes(inputImagePath);
                request.image = Convert.ToBase64String(imgBytes);

                var sendResult = await HunyuanApiClient.SendTaskAsync(serverUrl, request);
                if (!sendResult.Success)
                {
                    throw new Exception(sendResult.Error);
                }

                currentTaskUid = sendResult.Uid;
                report.uid = currentTaskUid;
                currentStatusText = $"Task {currentTaskUid} queued. Processing on GPU...";
                Repaint();

                byte[] glbBytes = null;
                while (!pollCancellation.IsCancellationRequested)
                {
                    await Task.Delay(1500, pollCancellation.Token);
                    if (pollCancellation.IsCancellationRequested) break;

                    var poll = await HunyuanApiClient.PollStatusAsync(serverUrl, currentTaskUid);
                    currentStatusText = $"Status: {poll.Status.ToUpperInvariant()} (Elapsed: {stopwatch.Elapsed.TotalSeconds:F1}s)";
                    Repaint();

                    if (poll.Status == "completed")
                    {
                        glbBytes = poll.ModelBytes;
                        break;
                    }

                    if (poll.Status == "error")
                    {
                        throw new Exception($"Hunyuan generation error: {poll.Message ?? poll.Error}");
                    }
                }

                if (glbBytes == null || glbBytes.Length == 0)
                {
                    throw new Exception("Received empty GLB payload on completion.");
                }

                currentStatusText = "Validating GLB binary...";
                lastGlbValidation = HunyuanBridgeValidation.ValidateGlbBytes(glbBytes);
                if (!lastGlbValidation.IsValid)
                {
                    throw new Exception($"GLB binary validation failed: {lastGlbValidation.Error}");
                }

                var basePrefix = Path.GetFileNameWithoutExtension(inputImagePath);
                var filename = HunyuanBridgeValidation.GenerateOutputFilename(basePrefix, request.seed, request.octree_resolution);
                lastOutputGlbPath = Path.Combine(outputDir, filename);
                File.WriteAllBytes(lastOutputGlbPath, glbBytes);

                stopwatch.Stop();
                report.elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                report.outputGlbPath = lastOutputGlbPath;
                report.outputGlbSha256 = lastGlbValidation.Sha256;
                report.outputByteSize = lastGlbValidation.ActualLength;
                report.success = true;

                var reportFilename = Path.ChangeExtension(filename, ".json");
                lastOutputReportPath = Path.Combine(reportsDir, reportFilename);
                File.WriteAllText(lastOutputReportPath, report.ToJson(true));

                serverStatus = ServerStatus.Completed;
                currentStatusText = $"Success! GLB saved in {report.elapsedSeconds:F1}s.";
                Debug.Log($"[Hunyuan3DBridge] Generated GLB saved: {lastOutputGlbPath} ({lastGlbValidation.ActualLength / 1024f:F1} KB, SHA256: {lastGlbValidation.Sha256})");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                report.elapsedSeconds = (float)stopwatch.Elapsed.TotalSeconds;
                report.success = false;
                report.error = ex.Message;

                serverStatus = ServerStatus.Error;
                currentStatusText = $"Failed: {ex.Message}";
                Debug.LogError($"[Hunyuan3DBridge] Generation failed: {ex.Message}");

                var failName = $"failure_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                lastOutputReportPath = Path.Combine(reportsDir, failName);
                File.WriteAllText(lastOutputReportPath, report.ToJson(true));
            }
            finally
            {
                isGenerating = false;
                Repaint();
            }
        }

        private void CancelGeneration()
        {
            if (pollCancellation != null && !pollCancellation.IsCancellationRequested)
            {
                pollCancellation.Cancel();
                currentStatusText = "Polling cancelled by user.";
                isGenerating = false;
                serverStatus = ServerStatus.Online;
            }
        }

        private void StartLocalServer()
        {
            try
            {
                if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                {
                    EditorUtility.DisplayDialog("Local Server", $"Python executable not found at: {pythonPath}", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
                {
                    EditorUtility.DisplayDialog("Local Server", $"Hunyuan repository directory not found at: {repoPath}", "OK");
                    return;
                }

                var logsDir = HunyuanBridgePaths.GetLogsDirectory();
                var logFile = Path.Combine(logsDir, "server.log");

                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "api_server.py --host 127.0.0.1 --port 8081",
                    WorkingDirectory = repoPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                localServerProcess = new Process { StartInfo = psi };
                localServerProcess.OutputDataReceived += (s, e) => { if (e.Data != null) File.AppendAllText(logFile, e.Data + "\n"); };
                localServerProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) File.AppendAllText(logFile, e.Data + "\n"); };

                localServerProcess.Start();
                localServerProcess.BeginOutputReadLine();
                localServerProcess.BeginErrorReadLine();

                Debug.Log($"[Hunyuan3DBridge] Started local Hunyuan server (PID {localServerProcess.Id}). Logging to {logFile}");
                serverMessage = $"Started local server process (PID {localServerProcess.Id}). Initializing...";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Hunyuan3DBridge] Failed to start local server: {ex.Message}");
                EditorUtility.DisplayDialog("Local Server Error", ex.Message, "OK");
            }
        }

        private void StopLocalServer()
        {
            if (localServerProcess != null && !localServerProcess.HasExited)
            {
                try
                {
                    localServerProcess.Kill();
                    Debug.Log("[Hunyuan3DBridge] Stopped local Hunyuan server process.");
                    serverMessage = "Local server process stopped.";
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Hunyuan3DBridge] Error stopping process: {ex.Message}");
                }
                finally
                {
                    localServerProcess = null;
                }
            }
        }

        #endregion
    }
}
#endif
