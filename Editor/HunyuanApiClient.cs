#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Hunyuan3DBridge.Editor
{
    public struct HealthCheckResult
    {
        public bool IsOnline;
        public string WorkerId;
        public string Status;
        public string Error;
    }

    public struct SendTaskResult
    {
        public bool Success;
        public string Uid;
        public string Error;
    }

    public struct PollStatusResult
    {
        public string Status; // "processing", "texturing", "completed", "error"
        public byte[] ModelBytes;
        public string Message;
        public string Error;
    }

    public struct UnloadVramResult
    {
        public bool Success;
        public float FreedMb;
        public string Error;
    }

    /// <summary>
    /// Asynchronous UnityWebRequest HTTP client for the official Hunyuan3D-2.1 FastAPI server.
    /// Safely dispatched via EditorApplication.update to avoid blocking the main Unity thread.
    /// </summary>
    public static class HunyuanApiClient
    {
        public static Task<HealthCheckResult> CheckHealthAsync(string baseUrl, int timeoutSeconds = 5)
        {
            var cleanUrl = NormalizeUrl(baseUrl) + "/health";
            var tcs = new TaskCompletionSource<HealthCheckResult>();

            var req = UnityWebRequest.Get(cleanUrl);
            req.timeout = timeoutSeconds;

            SendEditorRequest(req, () =>
            {
                var res = new HealthCheckResult();
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        res.IsOnline = false;
                        res.Error = $"HTTP error ({req.responseCode}): {req.error}";
                    }
                    else
                    {
                        var json = req.downloadHandler.text;
                        var parsed = JsonUtility.FromJson<HunyuanHealthResponse>(json);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.status))
                        {
                            res.IsOnline = true;
                            res.Status = parsed.status;
                            res.WorkerId = parsed.worker_id;
                        }
                        else
                        {
                            res.IsOnline = false;
                            res.Error = "Unexpected health response payload.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.IsOnline = false;
                    res.Error = ex.Message;
                }
                finally
                {
                    req.Dispose();
                    tcs.TrySetResult(res);
                }
            });

            return tcs.Task;
        }

        public static Task<SendTaskResult> SendTaskAsync(string baseUrl, HunyuanGenerationRequest request, int timeoutSeconds = 30)
        {
            var cleanUrl = NormalizeUrl(baseUrl) + "/send";
            var tcs = new TaskCompletionSource<SendTaskResult>();

            var jsonBody = request.ToJson();
            var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            var req = new UnityWebRequest(cleanUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            req.SetRequestHeader("Content-Type", "application/json");

            SendEditorRequest(req, () =>
            {
                var res = new SendTaskResult();
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        var errorBody = req.downloadHandler?.text;
                        res.Success = false;
                        res.Error = $"POST /send failed ({req.responseCode}): {(string.IsNullOrEmpty(errorBody) ? req.error : errorBody)}";
                    }
                    else
                    {
                        var json = req.downloadHandler.text;
                        var parsed = JsonUtility.FromJson<HunyuanSendResponse>(json);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.uid))
                        {
                            res.Success = true;
                            res.Uid = parsed.uid;
                        }
                        else
                        {
                            res.Success = false;
                            res.Error = $"Failed to parse task UID from response: {json}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.Success = false;
                    res.Error = ex.Message;
                }
                finally
                {
                    req.Dispose();
                    tcs.TrySetResult(res);
                }
            });

            return tcs.Task;
        }

        public static Task<PollStatusResult> PollStatusAsync(string baseUrl, string uid, int timeoutSeconds = 15)
        {
            var cleanUrl = $"{NormalizeUrl(baseUrl)}/status/{uid}";
            var tcs = new TaskCompletionSource<PollStatusResult>();

            var req = UnityWebRequest.Get(cleanUrl);
            req.timeout = timeoutSeconds;

            SendEditorRequest(req, () =>
            {
                var res = new PollStatusResult();
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        res.Status = "error";
                        res.Error = $"GET /status/{uid} failed ({req.responseCode}): {req.error}";
                    }
                    else
                    {
                        var json = req.downloadHandler.text;
                        var parsed = JsonUtility.FromJson<HunyuanStatusResponse>(json);
                        if (parsed != null)
                        {
                            res.Status = string.IsNullOrEmpty(parsed.status) ? "unknown" : parsed.status.ToLowerInvariant();
                            res.Message = parsed.message;

                            if (res.Status == "completed" && !string.IsNullOrEmpty(parsed.model_base64))
                            {
                                res.ModelBytes = Convert.FromBase64String(parsed.model_base64);
                            }
                        }
                        else
                        {
                            res.Status = "error";
                            res.Error = $"Failed to parse status response: {json}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.Status = "error";
                    res.Error = ex.Message;
                }
                finally
                {
                    req.Dispose();
                    tcs.TrySetResult(res);
                }
            });

            return tcs.Task;
        }

        private static void SendEditorRequest(UnityWebRequest req, Action onComplete)
        {
            var op = req.SendWebRequest();

            void UpdateHook()
            {
                if (!op.isDone) return;
                EditorApplication.update -= UpdateHook;
                onComplete?.Invoke();
            }

            EditorApplication.update += UpdateHook;
        }


        public static Task<UnloadVramResult> UnloadVramAsync(string baseUrl, int timeoutSeconds = 30)
        {
            var cleanUrl = NormalizeUrl(baseUrl) + "/unload";
            var tcs = new TaskCompletionSource<UnloadVramResult>();

            var req = new UnityWebRequest(cleanUrl, "POST")
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            req.SetRequestHeader("Content-Type", "application/json");

            SendEditorRequest(req, () =>
            {
                var res = new UnloadVramResult();
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        res.Success = false;
                        res.Error = $"POST /unload failed ({req.responseCode}): {req.error}";
                    }
                    else
                    {
                        var json = req.downloadHandler.text;
                        var parsed = JsonUtility.FromJson<HunyuanUnloadResponse>(json);
                        if (parsed != null && parsed.status == "ok")
                        {
                            res.Success = true;
                            res.FreedMb = parsed.freed_mb;
                        }
                        else
                        {
                            res.Success = false;
                            res.Error = parsed?.message ?? $"Unexpected unload response: {json}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.Success = false;
                    res.Error = ex.Message;
                }
                finally
                {
                    req.Dispose();
                    tcs.TrySetResult(res);
                }
            });

            return tcs.Task;
        }
        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "http://127.0.0.1:8081";
            var trimmed = url.Trim();
            if (trimmed.EndsWith("/")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            return trimmed;
        }
    }
}
#endif
