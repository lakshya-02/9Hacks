using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System;

public class APIClient : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ServerConfig config;
    [SerializeField] private ModelLoader modelLoader;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Settings")]
    [SerializeField] private int timeoutSeconds = 120;
    [SerializeField] private int maxDownloadAttempts = 30;
    [SerializeField] private float downloadRetryDelaySeconds = 1.0f;

    private bool _isSending;

    [Serializable]
    private class GenerateResponse
    {
        public string job_id;
        public string status;
        public string filename;
        public long size_bytes;
        public string download_url;
    }

    public void SendImage(Texture2D texture)
    {
        if (config == null || modelLoader == null)
        {
            Debug.LogError("[APIClient] Missing references: assign ServerConfig and ModelLoader in Inspector.");
            return;
        }

        if (_isSending)
        {
            Debug.LogWarning("[APIClient] Already sending a request. Ignoring.");
            return;
        }

        StartCoroutine(SendImageCoroutine(texture));
    }

    private IEnumerator SendImageCoroutine(Texture2D texture)
    {
        _isSending = true;
        SetStatus("Capturing...");
        SetLoading(true);

        byte[] pngBytes = texture.EncodeToPNG();
        Destroy(texture);

        // --- Step 1: Upload image on the upload port ---
        SetStatus("Sending image to SF3D server...");

        var form = new WWWForm();
        form.AddBinaryData("file", pngBytes, "snapshot.png", "image/png");

        string uploadUrl = config.UploadURL + "/generate";
        string modelId = null;
        string downloadUrl = null;

        using (var uploadRequest = UnityWebRequest.Post(uploadUrl, form))
        {
            uploadRequest.timeout = timeoutSeconds;

            yield return uploadRequest.SendWebRequest();

            if (uploadRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = uploadRequest.result == UnityWebRequest.Result.ConnectionError
                    ? $"Network error: {uploadRequest.error}"
                    : $"Server error ({uploadRequest.responseCode}): {uploadRequest.error}";

                SetStatus(errorMsg);
                Debug.LogError($"[APIClient] Upload failed — {errorMsg}");
                SetLoading(false);
                _isSending = false;
                yield break;
            }

            // Server returns JSON with job_id and optional download_url.
            string responseBody = uploadRequest.downloadHandler.text;
            GenerateResponse generateResponse = ParseGenerateResponse(responseBody);
            modelId = generateResponse != null ? generateResponse.job_id : null;
            downloadUrl = generateResponse != null ? generateResponse.download_url : null;

            if (string.IsNullOrEmpty(modelId))
            {
                string errorMsg = $"No job_id in server response: {responseBody}";
                SetStatus(errorMsg);
                Debug.LogError($"[APIClient] {errorMsg}");
                SetLoading(false);
                _isSending = false;
                yield break;
            }

            Debug.Log($"[APIClient] Upload success. Job ID: {modelId}");
        }

        // --- Step 2: Download .glb model on the download port ---
        SetStatus("Downloading 3D model...");

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = config.DownloadURL + "/download/" + UnityWebRequest.EscapeURL(modelId.Trim());
        }

        bool downloaded = false;
        for (int attempt = 1; attempt <= maxDownloadAttempts; attempt++)
        {
            using (var downloadRequest = UnityWebRequest.Get(downloadUrl))
            {
                downloadRequest.timeout = timeoutSeconds;

                yield return downloadRequest.SendWebRequest();

                if (downloadRequest.result == UnityWebRequest.Result.Success)
                {
                    SetStatus("Model received! Loading...");
                    byte[] glbBytes = downloadRequest.downloadHandler.data;
                    modelLoader.LoadModel(glbBytes);
                    SetStatus("Model loaded!");
                    downloaded = true;
                    break;
                }

                bool retryable = downloadRequest.responseCode == 404 || downloadRequest.responseCode == 425;
                if (retryable && attempt < maxDownloadAttempts)
                {
                    SetStatus($"Model not ready yet ({attempt}/{maxDownloadAttempts}). Retrying...");
                    yield return new WaitForSeconds(downloadRetryDelaySeconds);
                    continue;
                }

                string errorMsg = downloadRequest.result == UnityWebRequest.Result.ConnectionError
                    ? $"Network error: {downloadRequest.error}"
                    : $"Download error ({downloadRequest.responseCode}): {downloadRequest.error}";

                SetStatus(errorMsg);
                Debug.LogError($"[APIClient] Download failed - {errorMsg}");
                break;
            }
        }

        if (!downloaded)
        {
            SetStatus("Model generation failed or timed out.");
        }

        SetLoading(false);
        _isSending = false;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"[APIClient] {message}");
    }

    private void SetLoading(bool active)
    {
        if (loadingIndicator != null)
            loadingIndicator.SetActive(active);
    }

    private GenerateResponse ParseGenerateResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonUtility.FromJson<GenerateResponse>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[APIClient] Failed to parse response JSON: {ex.Message}");
            return null;
        }
    }
}
