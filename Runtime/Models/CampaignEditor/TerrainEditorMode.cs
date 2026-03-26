using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using ScriptableObjects.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class TerrainEditorMode : EditorMode
    {
        private DropdownField terrainDropdown;
        private Label selectedTerrainLabel;
        private VisualElement terrainColorPreview;
        private HZPLTerrain selectedTerrain;
        public List<HZPLTerrain> terrainTypes = new List<HZPLTerrain>();
        Color32[] _pixels;

        public TerrainEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter) : base(tab, editor, _highlighter)
        {
            terrainTypes = editor.tilemapManager.terrainTypes;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Get UI elements
            terrainDropdown = _tab.Q<DropdownField>("terrain-type-dropdown");
            selectedTerrainLabel = _tab.Q<Label>("selected-terrain-label");
            terrainColorPreview = _tab.Q<VisualElement>("terrain-color-preview");

            // Populate dropdown with terrain types
            var terrainNames = terrainTypes.Select(t => t.name).ToList();
            terrainDropdown.choices = terrainNames;
            terrainDropdown.value = terrainNames.First();

            // Register value change callback
            terrainDropdown.RegisterValueChangedCallback(evt =>
            {
                var newTerrain = terrainTypes.FirstOrDefault(p=>p.name == evt.newValue);
                SelectTerrain(newTerrain);
            });

            // Set initial selection
            SelectTerrain(terrainTypes.First());
        }

        private void SelectTerrain(HZPLTerrain terrain)
        {
            selectedTerrain = terrain;
            selectedTerrainLabel.text = $"Painting: {terrain}";

            Debug.Log($"Selected terrain: {terrain}");
        }
        
        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            bool success = base.PaintTile(cellPos, lastPaintedCell);
            if (!success)
                return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;
            // Set the terrain type for this tile
            Editor.editingCampaign.tileData[cellPos].terrainID = selectedTerrain.ID;
            Editor.tilemapManager.UpdateTile(cellPos);
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return;
            if (Editor.editingCampaign.tileData.ContainsKey(cellPos))
            {
                Editor.editingCampaign.tileData[cellPos].terrainID = Guid.Empty;
                Editor.tilemapManager.UpdateTile(cellPos);
            }
        }

        public override void SetCampaign()
        {
           
        }
    }
}