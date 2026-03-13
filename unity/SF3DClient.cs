using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP client for the SF3D FastAPI backend.
///
/// Flow:
///   1. Unity clicks image
///   2. POST image to  http://{serverIP}:{uploadPort}/generate   → receives job_id
///   3. GET  model from http://{serverIP}:{downloadPort}/download/{job_id} → receives .glb bytes
///   4. SF3DManager loads .glb into scene
/// </summary>
public class SF3DClient : MonoBehaviour
{
    private ServerConfig _config;

    void Awake()
    {
        _config = GetComponent<ServerConfig>();
        if (_config == null)
            _config = FindObjectOfType<ServerConfig>();

        if (_config == null)
            Debug.LogError("[SF3DClient] No ServerConfig found in scene! Add ServerConfig to this GameObject.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Upload PNG bytes → download GLB bytes via callback.</summary>
    public void GenerateMesh(byte[] pngBytes, Action<byte[]> onSuccess, Action<string> onError)
    {
        StartCoroutine(GenerateCoroutine(pngBytes, onSuccess, onError));
    }

    /// <summary>Ping /health on the upload port to check if server is alive.</summary>
    public void CheckHealth(Action<bool, string> callback)
    {
        StartCoroutine(HealthCoroutine(callback));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator GenerateCoroutine(byte[] pngBytes, Action<byte[]> onSuccess, Action<string> onError)
    {
        // ── Step 1: Upload image to port 8080, receive job_id ─────────────────
        string uploadUrl = $"{_config.UploadUrl}/generate";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", pngBytes, "capture.png", "image/png");

        using (UnityWebRequest uploadReq = UnityWebRequest.Post(uploadUrl, form))
        {
            uploadReq.timeout         = 120;
            uploadReq.downloadHandler = new DownloadHandlerBuffer();

            Debug.Log($"[SF3DClient] UPLOAD → {uploadUrl}  ({pngBytes.Length:N0} bytes)");
            float t0 = Time.realtimeSinceStartup;

            yield return uploadReq.SendWebRequest();

            float elapsed = Time.realtimeSinceStartup - t0;

            if (uploadReq.result != UnityWebRequest.Result.Success)
            {
                string err = $"Upload failed [{uploadReq.responseCode}]: {uploadReq.error}";
                Debug.LogError($"[SF3DClient] {err}");
                onError?.Invoke(err);
                yield break;
            }

            // Parse job_id from JSON response: {"job_id": "xxxx"}
            string json   = uploadReq.downloadHandler.text;
            string job_id = ParseJobId(json);

            if (string.IsNullOrEmpty(job_id))
            {
                string err = $"No job_id in server response: {json}";
                Debug.LogError($"[SF3DClient] {err}");
                onError?.Invoke(err);
                yield break;
            }

            Debug.Log($"[SF3DClient] Job {job_id} queued in {elapsed:F2}s — downloading model...");

            // ── Step 2: Download .glb from port 8081 ─────────────────────────
            string downloadUrl = $"{_config.DownloadUrl}/download/{job_id}";

            using (UnityWebRequest dlReq = UnityWebRequest.Get(downloadUrl))
            {
                dlReq.timeout         = 120;
                dlReq.downloadHandler = new DownloadHandlerBuffer();

                Debug.Log($"[SF3DClient] DOWNLOAD ← {downloadUrl}");
                float t1 = Time.realtimeSinceStartup;

                yield return dlReq.SendWebRequest();

                float dlElapsed = Time.realtimeSinceStartup - t1;

                if (dlReq.result != UnityWebRequest.Result.Success)
                {
                    string err = $"Download failed [{dlReq.responseCode}]: {dlReq.error}";
                    Debug.LogError($"[SF3DClient] {err}");
                    onError?.Invoke(err);
                    yield break;
                }

                byte[] glbBytes = dlReq.downloadHandler.data;
                Debug.Log($"[SF3DClient] Received {glbBytes.Length:N0} bytes in {dlElapsed:F2}s");
                onSuccess?.Invoke(glbBytes);
            }
        }
    }

    private IEnumerator HealthCoroutine(Action<bool, string> callback)
    {
        string url = $"{_config.UploadUrl}/health";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();
            bool   ok   = req.result == UnityWebRequest.Result.Success;
            string body = ok ? req.downloadHandler.text : req.error;
            callback?.Invoke(ok, body);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string ParseJobId(string json)
    {
        // Minimal JSON parse for {"job_id": "some-uuid"}
        int keyIdx = json.IndexOf("job_id");
        if (keyIdx < 0) return null;
        int colon  = json.IndexOf(':', keyIdx);
        int open   = json.IndexOf('"', colon);
        int close  = json.IndexOf('"', open + 1);
        if (open < 0 || close < 0) return null;
        return json.Substring(open + 1, close - open - 1);
    }
}
