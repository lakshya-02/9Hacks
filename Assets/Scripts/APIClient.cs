using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

public class APIClient : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ServerConfig config;
    [SerializeField] private ModelLoader modelLoader;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Settings")]
    [SerializeField] private int timeoutSeconds = 60;

    private bool _isSending;

    public void SendImage(Texture2D texture)
    {
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

        byte[] jpgBytes = texture.EncodeToJPG(85);
        Destroy(texture);

        // --- Step 1: Upload image on the upload port ---
        SetStatus("Sending image to SF3D server...");

        var form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "snapshot.jpg", "image/jpeg");

        string uploadUrl = config.UploadURL + "/generate";
        string modelId = null;

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

            modelId = uploadRequest.downloadHandler.text;
            Debug.Log($"[APIClient] Upload success. Server response: {modelId}");
        }

        // --- Step 2: Download .glb model on the download port ---
        SetStatus("Downloading 3D model...");

        string downloadUrl = config.DownloadURL + "/download/" + UnityWebRequest.EscapeURL(modelId?.Trim());

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
            }
            else
            {
                string errorMsg = downloadRequest.result == UnityWebRequest.Result.ConnectionError
                    ? $"Network error: {downloadRequest.error}"
                    : $"Download error ({downloadRequest.responseCode}): {downloadRequest.error}";

                SetStatus(errorMsg);
                Debug.LogError($"[APIClient] Download failed — {errorMsg}");
            }
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
}
