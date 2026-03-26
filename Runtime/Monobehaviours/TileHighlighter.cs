using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Manages visual highlighting of selected tiles in the editor
/// </summary>
public class TileHighlighter : MonoBehaviour
{
    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0f, 0.5f); // Yellow with transparency
    [SerializeField] private float highlightHeight = 0.1f; // Slightly above the tile
    [SerializeField] private bool useOutline = true;
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.1f;
    [SerializeField] private float tileRadius = 1.28f;
    [Header("References")]
    [SerializeField] private Grid grid;
    
    // Visual components
    private GameObject highlightObject;
    private SpriteRenderer highlightRenderer;
    private LineRenderer outlineRenderer;
    
    // Current state
    private Vector3Int? currentHighlightedTile = null;
    private bool isHighlightActive = false;

    private void Awake()
    {
        CreateHighlightVisuals();
    }

    private void CreateHighlightVisuals()
    {
        // Create highlight object
        highlightObject = new GameObject("TileHighlight");
        highlightObject.transform.SetParent(transform);
        
        // Add sprite renderer for the fill
        highlightRenderer = highlightObject.AddComponent<SpriteRenderer>();
        highlightRenderer.sprite = CreateHexagonSprite();
        highlightRenderer.color = highlightColor;
        highlightRenderer.sortingOrder = 1000; // Render on top
        
        // Add line renderer for the outline
        if (useOutline)
        {
            GameObject outlineObject = new GameObject("TileOutline");
            outlineObject.transform.SetParent(highlightObject.transform);
            
            outlineRenderer = outlineObject.AddComponent<LineRenderer>();
            outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            outlineRenderer.startWidth = outlineWidth;
            outlineRenderer.endWidth = outlineWidth;
            outlineRenderer.sortingOrder = 1001; // Render on top of fill
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            
            // Set hexagon points
            SetHexagonOutline();
        }
        
        // Start hidden
        highlightObject.SetActive(false);
    }

    /// <summary>
    /// Highlight a specific tile
    /// </summary>
    public void HighlightTile(Vector3Int cellPosition)
    {
        if (!isHighlightActive)
        {
            highlightObject.SetActive(true);
            isHighlightActive = true;
        }
        
        currentHighlightedTile = cellPosition;
        
        // Convert cell position to world position
        Vector3 worldPos = grid.CellToWorld(cellPosition);
        worldPos.z = highlightHeight; // Lift slightly above the tile
        
        highlightObject.transform.position = worldPos;
    }

    /// <summary>
    /// Clear the highlight
    /// </summary>
    public void ClearHighlight()
    {
        if (isHighlightActive)
        {
            highlightObject.SetActive(false);
            isHighlightActive = false;
            currentHighlightedTile = null;
        }
    }

    /// <summary>
    /// Check if a tile is currently highlighted
    /// </summary>
    public bool IsTileHighlighted(Vector3Int cellPosition)
    {
        return currentHighlightedTile.HasValue && currentHighlightedTile.Value == cellPosition;
    }

    /// <summary>
    /// Update highlight color
    /// </summary>
    public void SetHighlightColor(Color color)
    {
        highlightColor = color;
        if (highlightRenderer != null)
        {
            highlightRenderer.color = color;
        }
    }

    /// <summary>
    /// Update outline color
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        outlineColor = color;
        if (outlineRenderer != null)
        {
            outlineRenderer.startColor = color;
            outlineRenderer.endColor = color;
        }
    }

    private Sprite CreateHexagonSprite()
    {
        // Create a simple hexagon texture
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        
        // Clear to transparent
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Draw hexagon
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2.5f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                if (IsInsideHexagon(point - center, radius))
                {
                    pixels[y * size + x] = Color.white;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private bool IsInsideHexagon(Vector2 point, float radius)
    {
        // Simple hexagon point-in-polygon test
        float angle = 30f * Mathf.Deg2Rad;
        
        for (int i = 0; i < 6; i++)
        {
            float currentAngle = angle + i * 60f * Mathf.Deg2Rad;
            float nextAngle = angle + (i + 1) * 60f * Mathf.Deg2Rad;
            
            Vector2 v1 = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * radius;
            Vector2 v2 = new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)) * radius;
            
            // Check if point is on the correct side of the edge
            if (CrossProduct2D(v2 - v1, point - v1) < 0)
            {
                return false;
            }
        }
        
        return true;
    }

    private float CrossProduct2D(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void SetHexagonOutline()
    {
        if (outlineRenderer == null) return;
        
        // Hexagon vertices (pointy-top orientation)
        float angle = 30f * Mathf.Deg2Rad;
        
        Vector3[] positions = new Vector3[7]; // 6 vertices + 1 to close the loop
        
        for (int i = 0; i < 6; i++)
        {
            float currentAngle = angle + i * 60f * Mathf.Deg2Rad;
            positions[i] = new Vector3(
                Mathf.Cos(currentAngle) * tileRadius,
                Mathf.Sin(currentAngle) * tileRadius,
                0f
            );
        }
        
        positions[6] = positions[0]; // Close the loop
        
        outlineRenderer.positionCount = 7;
        outlineRenderer.SetPositions(positions);
    }

    /// <summary>
    /// Alternative: Use a quad for square tiles
    /// </summary>
    private Sprite CreateSquareSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Alternative: Set square outline
    /// </summary>
    private void SetSquareOutline()
    {
        if (outlineRenderer == null) return;
        
        float halfSize = 0.5f;
        
        Vector3[] positions = new Vector3[5]
        {
            new Vector3(-halfSize, -halfSize, 0),
            new Vector3(halfSize, -halfSize, 0),
            new Vector3(halfSize, halfSize, 0),
            new Vector3(-halfSize, halfSize, 0),
            new Vector3(-halfSize, -halfSize, 0)
        };
        
        outlineRenderer.positionCount = 5;
        outlineRenderer.SetPositions(positions);
    }

    private void OnDestroy()
    {
        if (highlightObject != null)
        {
            Destroy(highlightObject);
        }
    }
}