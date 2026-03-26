using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using ScriptableObjects.Gameplay.Tiles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class LandmassEditorMode : EditorMode
    {
        private int selectedTileIndex = 0;
        private VisualElement tileGrid;
        private TextField searchField;
        private VisualElement selectedTilePreview;
        private Label selectedTileNameLabel;
        private IntegerField mapWidthField;
        private IntegerField mapHeightField;
        private Button applyMapSizeBtn;
        private Label mapSizeCurrentLabel;
        private IntegerField bottomLeftXField;
        private IntegerField bottomLeftYField;
        private IntegerField topRightXField;
        private IntegerField topRightYField;
        private Button applyMissionCornersBtn;

        public LandmassEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter) : base(tab,
            editor, _highlighter)
        {
            InitializeUI();
            PopulateTileGrid();
        }

        private void InitializeUI()
        {
            // Get UI elements
            tileGrid = _tab.Q<VisualElement>("tile-grid");
            searchField = _tab.Q<TextField>("search-field");
            selectedTilePreview = _tab.Q<VisualElement>("selected-tile-preview");
            selectedTileNameLabel = _tab.Q<Label>("selected-tile-name");
            mapWidthField = _tab.Q<IntegerField>("map-width-field");
            mapHeightField = _tab.Q<IntegerField>("map-height-field");
            applyMapSizeBtn = _tab.Q<Button>("map-size-apply-btn");
            mapSizeCurrentLabel = _tab.Q<Label>("map-size-current-label");
            bottomLeftXField = _tab.Q<IntegerField>("map-bottomleft-x-field");
            bottomLeftYField = _tab.Q<IntegerField>("map-bottomleft-y-field");
            topRightXField = _tab.Q<IntegerField>("map-topright-x-field");
            topRightYField = _tab.Q<IntegerField>("map-topright-y-field");
            applyMissionCornersBtn = _tab.Q<Button>("map-corners-apply-btn");
            // Search field callback
            searchField.RegisterValueChangedCallback(evt => FilterTiles(evt.newValue));
            if (applyMapSizeBtn != null)
            {
                applyMapSizeBtn.clicked += ApplyMapSize;
            }
            if (applyMissionCornersBtn != null)
            {
                applyMissionCornersBtn.clicked += ApplyMissionCorners;
            }

            RefreshMapSizeUI();

            // Set initial selection
            if (availableTiles.Count > 0)
            {
                SelectTile(0);
            }
        }

        void PopulateTileGrid()
        {
            tileGrid.Clear();

            for (int i = 0; i < availableTiles.Count; i++)
            {
                int index = i; // Capture for closure
                Tile tileData = availableTiles[i];

                // Create tile item container
                VisualElement tileItem = new VisualElement();
                tileItem.AddToClassList("tile-item");
                tileItem.name = $"tile-{index}";

                // Create preview image
                VisualElement preview = new VisualElement();
                preview.AddToClassList("tile-preview");

                /*  if (tileData.preview != null)
                  {
                      preview.style.backgroundImage = new StyleBackground(tileData.preview);
                  }*/

                // Create tile name label
                Label nameLabel = new Label(tileData.name);
                nameLabel.AddToClassList("tile-name");

                // Add elements
                tileItem.Add(preview);
                tileItem.Add(nameLabel);

                // Add click handler
                tileItem.RegisterCallback<ClickEvent>(evt => SelectTile(index));

                tileGrid.Add(tileItem);
            }
        }

        void FilterTiles(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all tiles
                foreach (var child in tileGrid.Children())
                {
                    child.style.display = DisplayStyle.Flex;
                }

                return;
            }

            searchText = searchText.ToLower();

            for (int i = 0; i < availableTiles.Count; i++)
            {
                var tileItem = tileGrid.Q<VisualElement>($"tile-{i}");
                if (tileItem != null)
                {
                    bool matches = availableTiles[i].name.ToLower().Contains(searchText);
                    tileItem.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        void SelectTile(int index)
        {
            if (index < 0 || index >= availableTiles.Count) return;

            selectedTileIndex = index;
            Tile selectedTile = availableTiles[index];

            // Update visual selection in grid
            foreach (var child in tileGrid.Children())
            {
                child.RemoveFromClassList("selected");
            }

            var selectedItem = tileGrid.Q<VisualElement>($"tile-{index}");
            if (selectedItem != null)
            {
                selectedItem.AddToClassList("selected");
            }

            selectedTilePreview.style.backgroundImage = null;
            selectedTileNameLabel.text = selectedTile.name;

            Debug.Log($"Selected: {selectedTile.name}");
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            bool success = base.PaintTile(cellPos, lastPaintedCell);
            if (!success)
                return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;
            if (availableTiles.Count == 0 || selectedTileIndex >= availableTiles.Count)
                return false;

            HZPLTile selectedTile = availableTiles[selectedTileIndex];
            var tileData = Editor.editingCampaign.tileData[cellPos];
            if (tileData.terrainID == Guid.Empty)
            {
                tileData.terrainID = Editor.tilemapManager.terrainTypes.First().ID;
            }
            tileData.landmassTileID = selectedTile.ID;
            Editor.tilemapManager.UpdateTile(cellPos);

            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return;
            Editor.editingCampaign.tileData[cellPos].landmassTileID = Guid.Empty;
            Editor.tilemapManager.UpdateTile(cellPos);
        }

        public override void SetCampaign()
        {
            RefreshMapSizeUI();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshMapSizeUI();
        }

        private void ApplyMapSize()
        {
            int width = mapWidthField != null ? mapWidthField.value : 100;
            int height = mapHeightField != null ? mapHeightField.value : 100;

            Editor.ResizeCampaignTileArea(width, height);
            RefreshMapSizeUI();
        }

        private void ApplyMissionCorners()
        {
            var bottomLeft = new Vector2Int(
                bottomLeftXField != null ? bottomLeftXField.value : -50,
                bottomLeftYField != null ? bottomLeftYField.value : -50
            );
            var topRight = new Vector2Int(
                topRightXField != null ? topRightXField.value : 49,
                topRightYField != null ? topRightYField.value : 49
            );

            Editor.SetCampaignMissionCorners(bottomLeft, topRight);
            RefreshMapSizeUI();
        }

        private void RefreshMapSizeUI()
        {
            var size = Editor.GetCampaignSizeInTiles();
            Editor.GetCampaignMissionCorners(out var missionBottomLeft, out var missionTopRight);
            if (mapWidthField != null) mapWidthField.SetValueWithoutNotify(size.x);
            if (mapHeightField != null) mapHeightField.SetValueWithoutNotify(size.y);
            if (mapSizeCurrentLabel != null) mapSizeCurrentLabel.text = $"Current: {size.x} x {size.y}";
            if (bottomLeftXField != null) bottomLeftXField.SetValueWithoutNotify(missionBottomLeft.x);
            if (bottomLeftYField != null) bottomLeftYField.SetValueWithoutNotify(missionBottomLeft.y);
            if (topRightXField != null) topRightXField.SetValueWithoutNotify(missionTopRight.x);
            if (topRightYField != null) topRightYField.SetValueWithoutNotify(missionTopRight.y);
        }
    }
}
