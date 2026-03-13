using UnityEngine;
using UnityEngine.UI;
using GLTFast;

/// <summary>
/// Main manager.  Attach to a GameObject alongside SF3DClient.
///
/// Setup in Unity:
///   1. Create GameObject "SF3DManager"
///   2. Attach:  SF3DClient + SF3DManager
///   3. Assign spawnPoint (where the 3D model will appear)
///   4. Assign statusText (optional UI Text to show progress)
///   5. Call  GenerateFromTexture(tex)  or  GenerateFromPNG(bytes)
///      from a button / image-click event.
/// </summary>
public class SF3DManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("World-space transform where the generated model will be placed")]
    public Transform spawnPoint;

    [Tooltip("Optional UI Text to display status messages")]
    public Text statusText;

    private SF3DClient _client;
    private GameObject _currentModel;
    private bool _isGenerating;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        _client = GetComponent<SF3DClient>();
        if (_client == null)
        {
            Debug.LogError("[SF3DManager] SF3DClient component missing on this GameObject!");
            return;
        }

        if (spawnPoint == null)
            spawnPoint = transform;

        SetStatus("Checking server...");
        _client.CheckHealth((ok, body) =>
        {
            if (ok) SetStatus($"Server ready  {body}");
            else SetStatus($"WARNING: server not reachable — {body}");
        });
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the user clicks a UI image.
    /// Pass the Texture2D that was displayed in the UI Image component.
    /// </summary>
    public void GenerateFromTexture(Texture2D texture)
    {
        if (!CanGenerate()) return;
        byte[] png = texture.EncodeToPNG();
        StartGeneration(png);
    }

    /// <summary>
    /// Call this when you already have raw PNG bytes (e.g. from file picker).
    /// </summary>
    public void GenerateFromPNG(byte[] pngBytes)
    {
        if (!CanGenerate()) return;
        StartGeneration(pngBytes);
    }

    /// <summary>
    /// Capture the current view of a camera and generate from that.
    /// </summary>
    public void GenerateFromCamera(Camera cam, int width = 512, int height = 512)
    {
        if (!CanGenerate()) return;

        RenderTexture rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        StartGeneration(png);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private bool CanGenerate()
    {
        if (_isGenerating)
        {
            Debug.LogWarning("[SF3DManager] Already generating, wait for it to finish.");
            SetStatus("Busy — please wait...");
            return false;
        }
        return true;
    }

    private void StartGeneration(byte[] pngBytes)
    {
        _isGenerating = true;
        SetStatus("Sending image to SF3D server...");
        _client.GenerateMesh(pngBytes, OnMeshReceived, OnError);
    }

    private async void OnMeshReceived(byte[] glbBytes)
    {
        SetStatus($"Loading model ({glbBytes.Length:N0} bytes)...");

        // Remove the previously generated model
        if (_currentModel != null)
            Destroy(_currentModel);

        // Load GLB using GLTFast  (Package Manager: com.unity.cloud.gltfast)
        var gltf = new GltfImport();
        bool success = await gltf.LoadGltfBinary(glbBytes);

        if (success)
        {
            _currentModel = new GameObject("SF3D_Model");
            _currentModel.transform.position = spawnPoint.position;
            _currentModel.transform.rotation = spawnPoint.rotation;

            await gltf.InstantiateMainSceneAsync(_currentModel.transform);
            SetStatus("Model ready!");
            Debug.Log("[SF3DManager] 3D model instantiated.");
        }
        else
        {
            SetStatus("ERROR: Failed to load GLB.");
            Debug.LogError("[SF3DManager] GLTFast failed to parse the GLB.");
        }

        _isGenerating = false;
    }

    private void OnError(string error)
    {
        SetStatus($"Error: {error}");
        Debug.LogError($"[SF3DManager] {error}");
        _isGenerating = false;
    }

    private void SetStatus(string msg)
    {
        Debug.Log($"[SF3DManager] {msg}");
        if (statusText != null)
            statusText.text = msg;
    }
}
