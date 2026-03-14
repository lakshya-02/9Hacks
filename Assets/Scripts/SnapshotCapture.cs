using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using System.IO;
using Meta.XR;
using Unity.Collections;

public class SnapshotCapture : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private APIClient apiClient;
    [SerializeField] private PassthroughCameraAccess passthroughCamera;

    [Header("Preview")]
    [SerializeField] private RawImage previewImage;

    [Header("Editor Testing")]
    [SerializeField] private Texture2D testImage;

    [Header("Settings")]
    [SerializeField] private bool saveLocally = true;
    [SerializeField] private bool sendToServer = true;

    private bool _capturing;
    private string _saveFolder;
    private bool _aButtonPrev;
    private Texture2D _lastCapturedTexture;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InstallEditorLogFilter()
    {
        Debug.unityLogger.logHandler = new AndroidErrorFilter(Debug.unityLogger.logHandler);
    }

    private class AndroidErrorFilter : ILogHandler
    {
        private readonly ILogHandler _inner;
        public AndroidErrorFilter(ILogHandler inner) { _inner = inner; }

        public void LogException(System.Exception exception, Object context)
        {
            if (context != null && context.GetType().FullName == "Meta.XR.PassthroughCameraAccess")
                return;
            _inner.LogException(exception, context);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (logType == LogType.Error)
            {
                string msg = (args != null && args.Length > 0) ? string.Format(format, args) : format;
                if (msg.Contains("only supported on Android") || msg.Contains("Failure_NotInitialized"))
                    return;
            }
            _inner.LogFormat(logType, context, format, args);
        }
    }

    private void Start()
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb != null && mb.GetType().FullName == "Meta.XR.PassthroughCameraAccess")
            {
                mb.enabled = false;
                Destroy(mb);
                Debug.Log("[SnapshotCapture] Removed PassthroughCameraAccess in Editor.");
            }
        }
    }
#endif

    private void Awake()
    {
#if UNITY_EDITOR
        _saveFolder = Path.Combine(Application.dataPath, "Images");
#else
        _saveFolder = Path.Combine(Application.persistentDataPath, "Images");
#endif

        if (!Directory.Exists(_saveFolder))
            Directory.CreateDirectory(_saveFolder);

        if (previewImage != null)
            previewImage.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_capturing) return;

        if (Keyboard.current == null) return;

        // --- Press 1, 2, or 3 = Load image + send to server + get model ---
        if (Keyboard.current.digit1Key.wasPressedThisFrame) { RunFullPipeline("1"); return; }
        if (Keyboard.current.digit2Key.wasPressedThisFrame) { RunFullPipeline("2"); return; }
        if (Keyboard.current.digit3Key.wasPressedThisFrame) { RunFullPipeline("3"); return; }

        // --- Space = just capture + preview (no server) ---
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CaptureSnapshot();
            return;
        }

#if !UNITY_EDITOR
        bool aButtonNow = false;
        try { aButtonNow = OVRInput.Get(OVRInput.Button.One); } catch { }
        if (!aButtonNow)
        {
            var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid)
                rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out aButtonNow);
        }
        bool justPressed = aButtonNow && !_aButtonPrev;
        _aButtonPrev = aButtonNow;
        if (justPressed) CaptureSnapshot();
#endif
    }

    private void RunFullPipeline(string imageNum)
    {
        Debug.Log($"[SnapshotCapture] >>> KEY {imageNum} PRESSED — Full pipeline <<<");

        string imageFolder = Path.Combine(Application.dataPath, "Image");
        string filePath = Path.Combine(imageFolder, imageNum + ".jpeg");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"[SnapshotCapture] File not found: {filePath}");
            return;
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(fileBytes);
        Debug.Log($"[SnapshotCapture] Loaded {imageNum}.jpeg ({tex.width}x{tex.height})");

        // Show preview
        if (previewImage != null)
        {
            previewImage.texture = tex;
            previewImage.gameObject.SetActive(true);
        }

        // Send to server immediately
        if (apiClient != null)
        {
            var copy = new Texture2D(tex.width, tex.height, tex.format, false);
            copy.SetPixels32(tex.GetPixels32());
            copy.Apply();
            apiClient.SendImage(copy);
        }
        else
        {
            Debug.LogError("[SnapshotCapture] apiClient is NULL!");
        }
    }

    private void CaptureSnapshot()
    {
        _capturing = true;

        try
        {
#if UNITY_EDITOR
            Texture2D texture = GetEditorTexture();
#else
            Texture2D texture = GetQuestTexture();
#endif

            if (texture == null)
            {
                Debug.LogError("[SnapshotCapture] Texture is NULL - capture failed!");
                _capturing = false;
                return;
            }

            Debug.Log($"[SnapshotCapture] Captured texture: {texture.width}x{texture.height}");

            // Store for later use by R key
            _lastCapturedTexture = texture;

            // Show preview
            if (previewImage != null)
            {
                previewImage.texture = texture;
                previewImage.gameObject.SetActive(true);
                Debug.Log("[SnapshotCapture] Preview updated and visible.");
            }
            else
            {
                Debug.LogError("[SnapshotCapture] previewImage is NULL - assign RawImage in Inspector!");
            }

            if (saveLocally)
            {
                SaveImageLocally(texture);
            }

            Debug.Log("[SnapshotCapture] Image captured! Press R to send to server.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SnapshotCapture] Capture failed: {e.Message}\n{e.StackTrace}");
        }

        _capturing = false;
    }

#if UNITY_EDITOR
    private Texture2D GetEditorTexture()
    {
        if (testImage == null)
        {
            // Search in both Assets/Images/ and Assets/Image/
            string[] searchFolders = new[] {
                _saveFolder,
                Path.Combine(Application.dataPath, "Image")
            };

            string filePath = null;
            foreach (string folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;
                string[] jpgs = Directory.GetFiles(folder, "*.jpg");
                string[] pngs = Directory.GetFiles(folder, "*.png");
                if (jpgs.Length > 0) { filePath = jpgs[0]; break; }
                if (pngs.Length > 0) { filePath = pngs[0]; break; }
            }

            if (filePath == null)
            {
                Debug.LogWarning("[SnapshotCapture] No test image. Drag one into 'Test Image' field or drop a .jpg/.png into Assets/Images/ or Assets/Image/.");
                return null;
            }

            Debug.Log($"[SnapshotCapture] Using test image: {filePath}");
            byte[] fileBytes = File.ReadAllBytes(filePath);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(fileBytes);
            return tex;
        }

        var rt = RenderTexture.GetTemporary(testImage.width, testImage.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(testImage, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }
#else
    private Texture2D GetQuestTexture()
    {
        if (passthroughCamera == null)
        {
            Debug.LogError("[SnapshotCapture] PassthroughCameraAccess is NOT assigned! Drag it in Inspector.");
            return null;
        }

        Debug.Log($"[SnapshotCapture] IsPlaying={passthroughCamera.IsPlaying}, IsUpdated={passthroughCamera.IsUpdatedThisFrame}");

        if (!passthroughCamera.IsPlaying)
        {
            Debug.LogWarning("[SnapshotCapture] Camera not playing yet. Wait a moment and try again.");
            return null;
        }

        NativeArray<Color32> colors = passthroughCamera.GetColors();

        if (!colors.IsCreated || colors.Length == 0)
        {
            Debug.LogError("[SnapshotCapture] GetColors() returned empty.");
            return null;
        }

        Vector2Int res = passthroughCamera.CurrentResolution;
        Debug.Log($"[SnapshotCapture] Got {colors.Length} pixels at {res.x}x{res.y}");

        var texture = new Texture2D(res.x, res.y, TextureFormat.RGBA32, false);
        texture.SetPixels32(colors.ToArray());
        texture.Apply();

        Debug.Log("[SnapshotCapture] Texture created!");
        return texture;
    }
#endif

    private void SaveImageLocally(Texture2D texture)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"snapshot_{timestamp}.png";
        string filePath = Path.Combine(_saveFolder, fileName);

        byte[] pngBytes = texture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngBytes);
        Debug.Log($"[SnapshotCapture] Image saved: {filePath}");
    }
}
