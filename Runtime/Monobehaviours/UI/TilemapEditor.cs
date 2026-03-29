using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Models.CampaignEditor;
using Models.Gameplay.Campaign;
using Models.Gameplay;
using Models.Module;
using ScriptableObjects.Gameplay.Tiles;
using Services;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TilemapEditor : MonoBehaviour
{
    public Campaign editingCampaign { get; set; }

    public BaseTilemapManager tilemapManager;
    public TileHighlighter highlighter;

    [Header("Tile Palettes")] public List<LandmassTiles> availableLandTiles;
    public List<HZPLTile> availableOtherTiles;

    [Header("Reference Image")] public ReferenceImageController referenceImageController;
    [Header("Work Area Outline")] [SerializeField] private MapWorkAreaOutlineController workAreaOutline;

    [Header("UI Document")] public UIDocument uiDocument;

    [Header("Campaign Load Settings")] public string campaignFolderRelative = "Campaigns";

    [Header("Editor Modes")] private EditorMode selectedEditorMode;
    private List<EditorMode> editorModes;
    private LandmassEditorMode landmassEditorMode;
    private CampaignLoadEditorMode campaignLoadEditorMode;
    private CountryEditorMode countryEditorMode;
    private TerrainEditorMode terrainEditorMode;
    private DivisionEditorMode divisionEditorMode;
    private UnitSpawnEditorMode unitSpawnEditorMode;
    private InfrastructureEditorMode infrastructureEditorMode;
    private RiverEditorMode riverEditorMode;
    private AreaEditorMode areaEditorMode;
    private ReferenceImageEditorMode referenceImageEditorMode;
    private CountryControlEditorMode countryControlEditorMode;
    private AirWingEditorMode airWingEditorMode;
    private StaticAirDefenseSiteEditorMode staticAirDefenseSiteEditorMode;
    private AllianceEditorMode allianceEditorMode;
    private TestEditorMode testEditorMode;

    private VisualElement root;

    [Header("Tabs")] private VisualElement tabHolder;
    private VisualElement landmassTab;
    private VisualElement campaignLoadTab;
    private VisualElement terrainTab;
    private VisualElement countryTab;
    private VisualElement divisionTab;
    private VisualElement unitSpawnTab;
    private VisualElement infrastructureTab;
    private VisualElement riverTab;
    private VisualElement areaTab;
    private VisualElement referenceImageTab;
    private VisualElement controlTab;
    private VisualElement airWingTab;
    private VisualElement airDefenseTab;
    private VisualElement allianceTab;
    private VisualElement testTab;


    // UI Button references
    private Button landBtn;
    private Button campaignBtn;
    private Button countryBtn;
    private Button terrainBtn;
    private Button divisionBtn;
    private Button unitSpawnBtn;
    private Button infrastructureBtn;
    private Button riverBtn;
    private Button areaBtn;
    private Button referenceImageBtn;
    private Button controlBtn;
    private Button airWingBtn;
    private Button airDefenseBtn;
    private Button allianceBtn;
    private Button testBtn;

    // Mouse state tracking
    private bool isLeftMouseDown = false;
    private bool isRightMouseDown = false;
    public Vector3Int lastPaintedCell { get; private set; } = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

    public EditorDivisionManager divisionManager = new EditorDivisionManager();
    private Vector3Int cellPos
    {
        get
        {
            var v3 = tilemapManager.grid.WorldToCell(GetMouseWorldPosition(0));
            return new Vector3Int(v3.x, v3.y, 0);
        }
    }


    private void Start()
    {
        if (tilemapManager.grid != null && tilemapManager.grid.cellLayout != GridLayout.CellLayout.Hexagon)
        {
            Debug.LogWarning("Grid should be set to Hexagon layout!");
        }
        availableLandTiles = tilemapManager.availableLandTiles;
        availableOtherTiles = tilemapManager.availableOtherTiles;
    
        editingCampaign = new Campaign();
        divisionManager.Initialize(this);

        SubscribeToEvents();
        InitializeUI();
        EnsureWorkAreaOutline();
        SetCampaign();
    }

    private void OnDestroy()
    {
        UnubscribeToEvents();
    }

    private void Update()
    {
        if (selectedEditorMode is RiverEditorMode riverMode)
        {
            riverMode.Update(cellPos);
        }

        // Continuously paint/erase while mouse is held down and moving
        if (isLeftMouseDown)
        {
            Vector3Int currentCell = cellPos;
            selectedEditorMode?.PaintTile(currentCell, lastPaintedCell);
            lastPaintedCell = currentCell;
        }
        else if (isRightMouseDown)
        {
            Vector3Int currentCell = cellPos;
            if (currentCell != lastPaintedCell)
            {
                selectedEditorMode?.EraseTile(currentCell, lastPaintedCell);
                lastPaintedCell = currentCell;
            }
        }
    }

    private void InitializeUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("UI Document not assigned!");
            return;
        }

        root = uiDocument.rootVisualElement;

        // tabs
        tabHolder = root.Q<VisualElement>("tabs");
        landmassTab = root.Q<VisualElement>("landmass-tab");
        campaignLoadTab = root.Q<VisualElement>("campaign-load-tab");
        countryTab = root.Q<VisualElement>("country-tab");
        controlTab = root.Q<VisualElement>("control-tab");
        terrainTab = root.Q<VisualElement>("terrain-tab");
        divisionTab = root.Q<VisualElement>("division-tab");
        unitSpawnTab = root.Q<VisualElement>("unit-spawn-tab");
        infrastructureTab = root.Q<VisualElement>("infrastructure-tab");
        riverTab = root.Q<VisualElement>("river-tab");
        areaTab = root.Q<VisualElement>("area-tab");
        referenceImageTab = root.Q<VisualElement>("reference-image-tab");
        airWingTab = root.Q<VisualElement>("airwing-tab");
        airDefenseTab = root.Q<VisualElement>("air-defense-tab");
        allianceTab = root.Q<VisualElement>("alliance-tab");
        testTab = root.Q<VisualElement>("test-tab");

        // set editor modes
        editorModes = new List<EditorMode>()
        {
            (landmassEditorMode = new LandmassEditorMode(landmassTab, this, highlighter)),
            (campaignLoadEditorMode =
                new CampaignLoadEditorMode(campaignLoadTab, this, highlighter, campaignFolderRelative)),
            (countryEditorMode = new CountryEditorMode(countryTab, this, highlighter)),
            (countryControlEditorMode = new CountryControlEditorMode(controlTab, this, highlighter)),
            (terrainEditorMode = new TerrainEditorMode(terrainTab, this, highlighter)),
            (divisionEditorMode = new DivisionEditorMode(divisionTab, this, highlighter)),
            (unitSpawnEditorMode = new UnitSpawnEditorMode(unitSpawnTab, this, highlighter)),
            (infrastructureEditorMode = new InfrastructureEditorMode(infrastructureTab, this, highlighter)),
            (areaEditorMode = new AreaEditorMode(areaTab, this, highlighter)),
            (riverEditorMode = new RiverEditorMode(riverTab, this, highlighter)),
            (referenceImageEditorMode =
                new ReferenceImageEditorMode(referenceImageTab, this, highlighter, referenceImageController)),
            (airWingEditorMode = new AirWingEditorMode(airWingTab, this, highlighter)),
            (staticAirDefenseSiteEditorMode = new StaticAirDefenseSiteEditorMode(airDefenseTab, this, highlighter)),
            (allianceEditorMode = new AllianceEditorMode(allianceTab, this, highlighter)),
            (testEditorMode = new TestEditorMode(testTab, this, highlighter))
        };

        // Get buttons
        landBtn = root.Q<Button>("land-btn");
        campaignBtn = root.Q<Button>("campaign-btn");
        countryBtn = root.Q<Button>("country-btn");
        controlBtn = root.Q<Button>("control-btn");
        terrainBtn = root.Q<Button>("terrain-btn");
        divisionBtn = root.Q<Button>("division-btn");
        unitSpawnBtn = root.Q<Button>("unit-spawn-btn");
        infrastructureBtn = root.Q<Button>("infrastructure-btn");
        riverBtn = root.Q<Button>("river-btn");
        areaBtn = root.Q<Button>("area-btn");
        referenceImageBtn = root.Q<Button>("reference-image-btn");
        airWingBtn = root.Q<Button>("airwing-btn");
        airDefenseBtn = root.Q<Button>("air-defense-btn");
        allianceBtn = root.Q<Button>("alliance-btn");
        testBtn = root.Q<Button>("test-btn");

        // Setup button callbacks
        landBtn.clicked += () => SetEditorMode(landmassEditorMode);
        campaignBtn.clicked += () => SetEditorMode(campaignLoadEditorMode);
        countryBtn.clicked += () => SetEditorMode(countryEditorMode);
        controlBtn.clicked += () => SetEditorMode(countryControlEditorMode);
        terrainBtn.clicked += () => SetEditorMode(terrainEditorMode);
        divisionBtn.clicked += () => SetEditorMode(divisionEditorMode);
        unitSpawnBtn.clicked += () => SetEditorMode(unitSpawnEditorMode);
        infrastructureBtn.clicked += () => SetEditorMode(infrastructureEditorMode);
        riverBtn.clicked += () => SetEditorMode(riverEditorMode);
        areaBtn.clicked += () => SetEditorMode(areaEditorMode);
        referenceImageBtn.clicked += () => SetEditorMode(referenceImageEditorMode);
        airWingBtn.clicked += () => SetEditorMode(airWingEditorMode);
        airDefenseBtn.clicked += () => SetEditorMode(staticAirDefenseSiteEditorMode);
        allianceBtn.clicked += () => SetEditorMode(allianceEditorMode);
        testBtn.clicked += () => SetEditorMode(testEditorMode);

        SetEditorMode(landmassEditorMode);
    }

    private void SubscribeToEvents()
    {
        if (InputSingleton.Instance == null) return;
        InputSingleton.Instance.ActionAsset.UI.LeftClick.started += LeftClickStarted;
        InputSingleton.Instance.ActionAsset.UI.LeftClick.canceled += LeftClickCanceled;
        InputSingleton.Instance.ActionAsset.UI.RightClick.started += RightClickStarted;
        InputSingleton.Instance.ActionAsset.UI.RightClick.canceled += RightClickCanceled;
    }

    private void UnubscribeToEvents()
    {
        if (InputSingleton.Instance == null) return;
        InputSingleton.Instance.ActionAsset.UI.LeftClick.started -= LeftClickStarted;
        InputSingleton.Instance.ActionAsset.UI.LeftClick.canceled -= LeftClickCanceled;
        InputSingleton.Instance.ActionAsset.UI.RightClick.started -= RightClickStarted;
        InputSingleton.Instance.ActionAsset.UI.RightClick.canceled -= RightClickCanceled;
    }

    private void LeftClickStarted(InputAction.CallbackContext obj)
    {
        isLeftMouseDown = true;
        lastPaintedCell = cellPos;
        selectedEditorMode?.PaintTile(lastPaintedCell, null);
    }

    private void LeftClickCanceled(InputAction.CallbackContext obj)
    {
        isLeftMouseDown = false;
        lastPaintedCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    }

    private void RightClickStarted(InputAction.CallbackContext obj)
    {
        isRightMouseDown = true;
        lastPaintedCell = cellPos;
        selectedEditorMode?.EraseTile(lastPaintedCell, null);
    }

    private void RightClickCanceled(InputAction.CallbackContext obj)
    {
        isRightMouseDown = false;
        lastPaintedCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    }

    private Vector3 GetMouseWorldPosition(float depth)
    {
        Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, depth);
        return Camera.main.ScreenToWorldPoint(screenPosition);
    }

    private void SetEditorMode(EditorMode mode)
    {
        selectedEditorMode = mode;

        foreach (var editorMode in editorModes)
        {
            editorMode.DisableEditorMode();
        }

        mode.SetEditorMode();
    }

    // Called by CampaignLoadEditorMode when a file is selected.
    public void LoadCampaignFromJson(string fullPath)
    {
        Debug.Log($"Loading campaign from: {fullPath}");

        Campaign loaded;
        try
        {
            loaded = CampaignSavingService.LoadCampaign(fullPath); // <-- implement/adjust in your service
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load campaign: {e}");
            return;
        }

        if (loaded == null)
        {
            Debug.LogWarning("Load returned null campaign.");
            return;
        }

        editingCampaign = loaded;
        Debug.Log("Campaign loaded successfully.");

        SetCampaign();
        ApplyReferenceImageFromCampaign();
    }

    public void CaptureReferenceImageIntoCampaign()
    {
        if (editingCampaign == null)
            return;

        editingCampaign.ReferenceImage = referenceImageController != null
            ? referenceImageController.CaptureSaveData()
            : null;
    }

    public void ApplyReferenceImageFromCampaign()
    {
        if (editingCampaign == null || referenceImageController == null)
            return;

        referenceImageController.ApplySaveData(editingCampaign.ReferenceImage);
    }

    public Vector2Int GetCampaignSizeInTiles()
    {
        if (editingCampaign == null || editingCampaign.tileData == null || editingCampaign.tileData.Count == 0)
            return new Vector2Int(0, 0);

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (var cell in editingCampaign.tileData.Keys)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }

    public void GetCampaignMissionCorners(out Vector2Int bottomLeft, out Vector2Int topRight)
    {
        if (editingCampaign == null)
        {
            bottomLeft = Vector2Int.zero;
            topRight = Vector2Int.zero;
            return;
        }

        bottomLeft = editingCampaign.BottomLeftCorner;
        topRight = editingCampaign.TopRightCorner;
    }

    public void SetCampaignMissionCorners(Vector2Int bottomLeft, Vector2Int topRight)
    {
        if (editingCampaign == null)
            return;

        editingCampaign.BottomLeftCorner = bottomLeft;
        editingCampaign.TopRightCorner = topRight;
    }

    public void ResizeCampaignTileArea(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (editingCampaign == null)
            return;

        var oldTileData = editingCampaign.tileData ?? new Dictionary<Vector3Int, HZPLTileData>();
        var resized = new Dictionary<Vector3Int, HZPLTileData>(width * height);

        int minX = -(width / 2);
        int minY = -(height / 2);

        for (int x = minX; x < minX + width; x++)
        {
            for (int y = minY; y < minY + height; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!oldTileData.TryGetValue(cell, out var tile))
                    tile = new HZPLTileData();

                resized[cell] = tile;
            }
        }

        editingCampaign.tileData = resized;
        SetCampaign();
    }

    public bool FitMapSizeToReferenceImage()
    {
        if (referenceImageController == null || tilemapManager == null || tilemapManager.grid == null)
            return false;

        if (!referenceImageController.TryGetWorldSize(out float imageWidth, out float imageHeight))
            return false;

        var grid = tilemapManager.grid;
        Vector3 center00 = grid.GetCellCenterWorld(new Vector3Int(0, 0, 0));
        Vector3 center10 = grid.GetCellCenterWorld(new Vector3Int(1, 0, 0));
        Vector3 center01 = grid.GetCellCenterWorld(new Vector3Int(0, 1, 0));

        float stepX = Mathf.Abs(center10.x - center00.x);
        float stepY = Mathf.Abs(center01.y - center00.y);
        if (stepX <= 0.0001f || stepY <= 0.0001f)
            return false;

        int targetWidthTiles = Mathf.Max(1, Mathf.RoundToInt(imageWidth / stepX) + 1);
        int targetHeightTiles = Mathf.Max(1, Mathf.RoundToInt(imageHeight / stepY) + 1);

        ResizeCampaignTileArea(targetWidthTiles, targetHeightTiles);

        if (!TryGetCampaignCornerCenters(out var bottomLeft, out var bottomRight, out var topLeft, out var topRight))
            return false;

        float left = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
        float right = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
        float bottom = Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);
        float top = Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);

        Vector3 mapCenter = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
        float mapWidth = right - left;
        float mapHeight = top - bottom;

        referenceImageController.SetPositionXZ(mapCenter.x, mapCenter.y);
        referenceImageController.StretchToWorldSize(mapWidth, mapHeight);
        return true;
    }

    private void SetCampaign()
    {
        tilemapManager.SetCampaign(editingCampaign.tileData, editingCampaign.areas);
        divisionManager?.Rebuild(editingCampaign);
        foreach (var EM in editorModes)
        {
            EM.SetCampaign();
        }

        workAreaOutline?.Rebuild(editingCampaign.tileData.Keys);
    }

    private void EnsureWorkAreaOutline()
    {
        if (workAreaOutline == null)
            workAreaOutline = GetComponentInChildren<MapWorkAreaOutlineController>();

        if (workAreaOutline == null)
        {
            var go = new GameObject("MapWorkAreaOutline");
            go.transform.SetParent(transform, false);
            workAreaOutline = go.AddComponent<MapWorkAreaOutlineController>();
        }

        workAreaOutline.Initialize(tilemapManager.grid);
    }

    private bool TryGetCampaignCornerCenters(out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topLeft, out Vector3 topRight)
    {
        bottomLeft = Vector3.zero;
        bottomRight = Vector3.zero;
        topLeft = Vector3.zero;
        topRight = Vector3.zero;

        if (editingCampaign == null || editingCampaign.tileData == null || editingCampaign.tileData.Count == 0)
            return false;

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (var cell in editingCampaign.tileData.Keys)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        var grid = tilemapManager.grid;
        bottomLeft = grid.GetCellCenterWorld(new Vector3Int(minX, minY, 0));
        bottomRight = grid.GetCellCenterWorld(new Vector3Int(maxX, minY, 0));
        topLeft = grid.GetCellCenterWorld(new Vector3Int(minX, maxY, 0));
        topRight = grid.GetCellCenterWorld(new Vector3Int(maxX, maxY, 0));
        return true;
    }

    private Vector3Int[] GetHexNeighbors(Vector3Int cell)
    {
        bool isEvenRow = cell.y % 2 == 0;

        if (isEvenRow)
        {
            return new Vector3Int[]
            {
                cell + new Vector3Int(1, 0, 0),
                cell + new Vector3Int(-1, 0, 0),
                cell + new Vector3Int(0, 1, 0),
                cell + new Vector3Int(-1, 1, 0),
                cell + new Vector3Int(0, -1, 0),
                cell + new Vector3Int(-1, -1, 0)
            };
        }

        return new Vector3Int[]
        {
            cell + new Vector3Int(1, 0, 0),
            cell + new Vector3Int(-1, 0, 0),
            cell + new Vector3Int(1, 1, 0),
            cell + new Vector3Int(0, 1, 0),
            cell + new Vector3Int(1, -1, 0),
            cell + new Vector3Int(0, -1, 0)
        };
    }
}
