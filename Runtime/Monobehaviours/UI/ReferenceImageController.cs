
using System.IO;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ReferenceImageController : MonoBehaviour
{
    [Header("Render Setup")]
    [SerializeField] private MeshRenderer quadRenderer;
    [SerializeField] private MeshFilter quadFilter;

    [Header("Sorting")]
    [SerializeField] private int behindAllOrder = -1000;
    [SerializeField] private int aheadAllOrder = 1000;

    private Material _runtimeMaterial;
    private Texture2D _texture;
    private string _sourcePath;
    private bool _visible = true;
    private bool _ahead = false;

    public bool Visible => _visible;
    public bool IsAhead => _ahead;
    public Texture2D CurrentTexture => _texture;
    public string SourcePath => _sourcePath;

    private void Awake()
    {
        EnsureQuad();
        EnsureMaterial();
        ApplySorting();
    }

    private void EnsureQuad()
    {
        if (quadRenderer != null && quadFilter != null) return;

        // Create a simple quad if not provided
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ReferenceImageQuad";
        quad.transform.SetParent(transform, false);
        quad.transform.localRotation = Quaternion.Euler(0, 0f, 0f); // lay flat on XZ plane

        // Remove collider
        var col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        quadRenderer = quad.GetComponent<MeshRenderer>();
        quadFilter = quad.GetComponent<MeshFilter>();
        quadRenderer.gameObject.SetActive(false);
    }

    private void EnsureMaterial()
    {
        if (quadRenderer == null) return;

        // Use an unlit transparent shader for reference images
        _runtimeMaterial = new Material(Shader.Find("Unlit/Transparent"));
        quadRenderer.sharedMaterial = _runtimeMaterial;
    }

    private void ApplySorting()
    {
        if (quadRenderer == null) return;
        quadRenderer.sortingOrder = _ahead ? aheadAllOrder : behindAllOrder;
        quadRenderer.sortingLayerName = "Default";
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (quadRenderer != null) quadRenderer.enabled = _visible;
    }

    public void ToggleVisible()
    {
        SetVisible(!_visible);
    }

    public void SetAheadOfTilemaps(bool ahead)
    {
        _ahead = ahead;
        ApplySorting();
    }

    public void SetPositionXZ(float x, float y)
    {
        var p = transform.position;
        transform.position = new Vector3(x, y, p.z);
    }

    public void SetScale(float scale)
    {
        if (scale <= 0f) scale = 0.01f;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    public bool TryGetWorldSize(out float width, out float height)
    {
        width = 0f;
        height = 0f;

        if (quadRenderer == null || _texture == null || !quadRenderer.gameObject.activeSelf)
            return false;

        var size = quadRenderer.bounds.size;
        width = Mathf.Abs(size.x);
        height = Mathf.Abs(size.y);
        return width > 0f && height > 0f;
    }

    public void ScaleToFitWorldSize(float targetWidth, float targetHeight)
    {
        targetWidth = Mathf.Max(0.01f, targetWidth);
        targetHeight = Mathf.Max(0.01f, targetHeight);

        if (!TryGetWorldSize(out float currentWidth, out float currentHeight))
            return;

        float widthFactor = targetWidth / currentWidth;
        float heightFactor = targetHeight / currentHeight;
        float factor = Mathf.Min(widthFactor, heightFactor);
        SetScale(transform.localScale.x * factor);
    }

    public void StretchToWorldSize(float targetWidth, float targetHeight)
    {
        targetWidth = Mathf.Max(0.01f, targetWidth);
        targetHeight = Mathf.Max(0.01f, targetHeight);

        if (!TryGetWorldSize(out float currentWidth, out float currentHeight))
            return;

        float factorX = targetWidth / currentWidth;
        float factorY = targetHeight / currentHeight;

        var s = transform.localScale;
        transform.localScale = new Vector3(s.x * factorX, s.y * factorY, s.z);
    }

    public void SetTexture(Texture2D tex)
    {
        _texture = tex;
        if (_runtimeMaterial == null) EnsureMaterial();
        if (_runtimeMaterial != null) _runtimeMaterial.mainTexture = _texture;
        ApplyTextureAspect();
    }

    public void LoadImageFromPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"ReferenceImageController: file not found {path}");
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (tex.LoadImage(bytes))
        {
            tex.name = Path.GetFileName(path);
            _sourcePath = path;
            quadRenderer.gameObject.SetActive(true);
            SetTexture(tex);
        }
        else
        {
            Debug.LogWarning("ReferenceImageController: failed to load image bytes.");
        }
    }

    public ReferenceImageSaveData CaptureSaveData()
    {
        if (_texture == null)
            return null;

        byte[] bytes = _texture.EncodeToPNG();
        if (bytes == null || bytes.Length == 0)
            return null;

        return new ReferenceImageSaveData
        {
            SourcePath = _sourcePath,
            ImageFileName = _texture.name,
            ImageBase64 = System.Convert.ToBase64String(bytes),
            Position = transform.position,
            Scale = transform.localScale,
            Visible = _visible,
            AheadOfTilemaps = _ahead
        };
    }

    public void ApplySaveData(ReferenceImageSaveData data)
    {
        if (data == null)
        {
            SetVisible(false);
            return;
        }

        Texture2D tex = null;

        if (!string.IsNullOrEmpty(data.ImageBase64))
        {
            try
            {
                var bytes = System.Convert.FromBase64String(data.ImageBase64);
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                    tex = null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ReferenceImageController: failed to decode saved image bytes. {e.Message}");
            }
        }

        if (tex == null && !string.IsNullOrEmpty(data.SourcePath) && File.Exists(data.SourcePath))
        {
            var bytes = File.ReadAllBytes(data.SourcePath);
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
                tex = null;
        }

        if (tex == null)
        {
            Debug.LogWarning("ReferenceImageController: no valid saved reference image data to restore.");
            SetVisible(false);
            return;
        }

        tex.name = string.IsNullOrEmpty(data.ImageFileName) ? "ReferenceImage" : data.ImageFileName;
        _sourcePath = data.SourcePath;
        quadRenderer.gameObject.SetActive(true);
        SetTexture(tex);

        transform.position = data.Position;
        transform.localScale = data.Scale == Vector3.zero ? Vector3.one : data.Scale;

        SetAheadOfTilemaps(data.AheadOfTilemaps);
        SetVisible(data.Visible);
    }

    private void ApplyTextureAspect()
    {
        if (quadRenderer == null || _texture == null) return;

        var quadTransform = quadRenderer.transform;
        if (_texture.width <= 0 || _texture.height <= 0)
        {
            quadTransform.localScale = Vector3.one;
            return;
        }

        // Keep UV mapping unchanged and scale geometry so world-space proportions match image proportions.
        var aspect = (float)_texture.width / _texture.height;
        quadTransform.localScale = new Vector3(aspect, 1f, 1f);
    }
}
