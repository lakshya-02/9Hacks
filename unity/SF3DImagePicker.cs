using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to any UI Image that the user can click to generate a 3D model.
///
/// Setup in Unity:
///   1. Create a UI Canvas with a Panel
///   2. Add UI Image components for each photo you want to be clickable
///   3. Attach this script to each clickable Image
///   4. Assign manager (the GameObject holding SF3DManager + SF3DClient)
///   5. Press Play → click any image → 3D model appears at spawnPoint
/// </summary>
[RequireComponent(typeof(Image))]
public class SF3DImagePicker : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("The SF3DManager GameObject (must also have SF3DClient attached)")]
    public SF3DManager manager;

    [Tooltip("Visual feedback: image border highlight on hover")]
    public Color highlightColor = new Color(0.3f, 0.8f, 1f, 1f);

    private Image _image;
    private Color _originalColor;

    void Awake()
    {
        _image = GetComponent<Image>();
        _originalColor = _image.color;

        if (manager == null)
            manager = FindObjectOfType<SF3DManager>();
    }

    // ── Click handler — fires when user clicks the UI image ───────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null)
        {
            Debug.LogError("[SF3DImagePicker] No SF3DManager assigned!");
            return;
        }

        Texture2D tex = GetTexture2D();
        if (tex == null)
        {
            Debug.LogError("[SF3DImagePicker] Could not extract Texture2D from image.");
            return;
        }

        Debug.Log($"[SF3DImagePicker] Clicked image: {gameObject.name}  ({tex.width}x{tex.height})");
        manager.GenerateFromTexture(tex);
    }

    // ── Hover highlight ───────────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData _) => _image.color = highlightColor;
    public void OnPointerExit(PointerEventData _) => _image.color = _originalColor;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Texture2D GetTexture2D()
    {
        if (_image.sprite == null) return null;

        Texture2D src = _image.sprite.texture;

        // If the texture isn't Read/Write enabled, blit it via RenderTexture
        if (!src.isReadable)
        {
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        return src;
    }
}
