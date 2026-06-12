using System;
using System.Collections.Generic;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Allows editing of tile infrastructure: cities, roads, supply, buildings, and resources
    /// </summary>
    public class InfrastructureEditorMode : EditorMode
    {
        // City controls
        // Tile name control
        private TextField tileNameField;
        private RadioButtonGroup cityTypeRadioGroup;
        
        // Infrastructure level
        private SliderInt infrastructureLevelSlider;
        private Label infrastructureLevelDescLabel;
        
        // Supply controls
        private Toggle supplyHubToggle;
        private SliderInt supplyLineLevelSlider;
        private Label supplyLineLevelDescLabel;
        
        // Building controls
        private SliderInt fortificationSlider;
        private SliderInt portSlider;
        
        // Resource controls
        private SliderInt oilSlider;
        private SliderInt electricitySlider;
        private SliderInt steelSlider;
        private SliderInt factorySlider;
        
        // Info display
        private Label selectedTileLabel;
        private Label tileInfoLabel;
        private Button clearAllBtn;
        
        private Vector3Int? currentTile = null;
        private TileInfrastructure currentInfrastructure = null;

        public InfrastructureEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter) : base(tab, editor, _highlighter)
        {
            InitializeUI();
            UpdateDisplay();
        }

        private void InitializeUI()
        {
            // City controls
            cityTypeRadioGroup = _tab.Q<RadioButtonGroup>("city-type-radio");
            
            // Infrastructure level
            infrastructureLevelSlider = _tab.Q<SliderInt>("infrastructure-level-slider");
            infrastructureLevelDescLabel = _tab.Q<Label>("infrastructure-level-desc");
            
            // Supply controls
            supplyHubToggle = _tab.Q<Toggle>("supply-hub-toggle");
            supplyLineLevelSlider = _tab.Q<SliderInt>("supply-line-level-slider");
            supplyLineLevelDescLabel = _tab.Q<Label>("supply-line-level-desc");
            
            // Building controls
            fortificationSlider = _tab.Q<SliderInt>("fortification-slider");
            portSlider = _tab.Q<SliderInt>("port-slider");
            
            // Resource controls
            oilSlider = _tab.Q<SliderInt>("oil-slider");
            electricitySlider = _tab.Q<SliderInt>("electricity-slider");
            steelSlider = _tab.Q<SliderInt>("steel-slider");
            factorySlider = _tab.Q<SliderInt>("factory-slider");
            
            // Info display
            selectedTileLabel = _tab.Q<Label>("selected-tile-label");
            tileNameField = _tab.Q<TextField>("tile-name-field");
            tileInfoLabel = _tab.Q<Label>("tile-info-label");
            clearAllBtn = _tab.Q<Button>("clear-all-btn");
            
            // Setup radio group choices
            var cityChoices = new List<string> { "None", "Suburb", "Metropolitan" };
            cityTypeRadioGroup.choices = cityChoices;
            cityTypeRadioGroup.value = 0; // None
            
            // Register callbacks
            cityTypeRadioGroup.RegisterValueChangedCallback(evt => OnCityTypeChanged(evt.newValue));
            tileNameField?.RegisterValueChangedCallback(evt => OnTileNameChanged(evt.newValue));
            
            infrastructureLevelSlider.RegisterValueChangedCallback(evt => OnInfrastructureLevelChanged(evt.newValue));
            
            supplyHubToggle.RegisterValueChangedCallback(evt => OnSupplyHubChanged(evt.newValue));
            supplyLineLevelSlider.RegisterValueChangedCallback(evt => OnSupplyLineLevelChanged(evt.newValue));
            
            fortificationSlider.RegisterValueChangedCallback(evt => currentInfrastructure.fortification.buildLevel = evt.newValue);
            portSlider.RegisterValueChangedCallback(evt => currentInfrastructure.port.buildLevel = evt.newValue);
            
            oilSlider.RegisterValueChangedCallback(evt => currentInfrastructure.oil.buildLevel = evt.newValue);
            electricitySlider.RegisterValueChangedCallback(evt => currentInfrastructure.electricity.buildLevel = evt.newValue);
            steelSlider.RegisterValueChangedCallback(evt => currentInfrastructure.steel.buildLevel = evt.newValue);
            factorySlider.RegisterValueChangedCallback(evt => currentInfrastructure.factory.buildLevel = evt.newValue);
            
            clearAllBtn.clicked += OnClearAllClicked;
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!base.PaintTile(cellPos, lastPaintedCell)) return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;
            // Load this tile's infrastructure for editing
            LoadTileInfrastructure(cellPos);
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!Editor.editingCampaign.HasTile(cellPos)) return;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return;
            // Clear infrastructure on this tile
            var templateTile = Editor.editingCampaign.EnsureTemplateTile(cellPos);
            var startingTile = Editor.editingCampaign.EnsureStartingTile(cellPos);

            // Tile name
            if (tileNameField != null)
                tileNameField.SetValueWithoutNotify(templateTile.tileName ?? string.Empty);
            if (startingTile.infrastructure != null)
            {
                startingTile.infrastructure.Clear();
                Debug.Log($"Cleared all infrastructure at {cellPos}");
                
                // If this is the current tile, update display
                if (currentTile == cellPos)
                {
                    LoadTileInfrastructure(cellPos);
                }
                Editor.tilemapManager.UpdateTile(cellPos);
            }
        }

        private void LoadTileInfrastructure(Vector3Int cellPos)
        {
            currentTile = cellPos;
            
            if (!Editor.editingCampaign.HasTile(cellPos))
            {
                currentInfrastructure = null;
                UpdateDisplay();
                return;
            }
            
            var startingTile = Editor.editingCampaign.EnsureStartingTile(cellPos);
            
            // Ensure infrastructure exists
            if (startingTile.infrastructure == null)
            {
                startingTile.infrastructure = new TileInfrastructure();
            }
            
            currentInfrastructure = startingTile.infrastructure;
            
            // Update UI to reflect current values (without triggering callbacks)
            UpdateUIFromInfrastructure();
            UpdateDisplay();
        }

        private void UpdateUIFromInfrastructure()
        {
            if (currentInfrastructure == null)
            {
                cityTypeRadioGroup.value = 0;
                infrastructureLevelSlider.value = 0;
                supplyHubToggle.value = false;
                supplyLineLevelSlider.value = 0;
                fortificationSlider.value = 0;
                portSlider.value = 0;
                oilSlider.value = 0;
                electricitySlider.value = 0;
                steelSlider.value = 0;
                factorySlider.value = 0;
                UpdateInfrastructureLevelDescription(0);
                UpdateSupplyLineLevelDescription(0);
                return;
            }
            
            // City
            cityTypeRadioGroup.value = (int)currentInfrastructure.cityType;
            
            // Infrastructure level
            infrastructureLevelSlider.SetValueWithoutNotify(currentInfrastructure.roads.buildLevel);
            UpdateInfrastructureLevelDescription(currentInfrastructure.roads.buildLevel);
            
            // Supply
            supplyHubToggle.SetValueWithoutNotify(currentInfrastructure.isSupplyHub);
            supplyLineLevelSlider.SetValueWithoutNotify(currentInfrastructure.supplyLine.buildLevel);
            UpdateSupplyLineLevelDescription(currentInfrastructure.supplyLine.buildLevel);
            
            // Buildings
            fortificationSlider.SetValueWithoutNotify(currentInfrastructure.fortification.buildLevel);
            portSlider.SetValueWithoutNotify(currentInfrastructure.port.buildLevel);
            
            // Resources
            oilSlider.SetValueWithoutNotify(currentInfrastructure.oil.buildLevel);
            electricitySlider.SetValueWithoutNotify(currentInfrastructure.electricity.buildLevel);
            steelSlider.SetValueWithoutNotify(currentInfrastructure.steel.buildLevel);
            factorySlider.SetValueWithoutNotify(currentInfrastructure.factory.buildLevel);

            if (currentTile.HasValue)
            {
                var tileData = Editor.editingCampaign.EnsureTemplateTile(currentTile.Value);
                tileNameField.SetValueWithoutNotify(tileData.tileName ?? string.Empty);
            }
            else
            {
                tileNameField.SetValueWithoutNotify(string.Empty);  
            }
        }

        private void UpdateInfrastructureLevelDescription(int level)
        {
            if (infrastructureLevelDescLabel != null)
            {
                string description = level == 0 ? "No roads or infrastructure" :
                                   level <= 2 ? "Dirt Roads - Basic paths" :
                                   level <= 4 ? "Basic Roads - Unpaved routes" :
                                   level <= 6 ? "Paved Roads - Standard infrastructure" :
                                   level <= 8 ? "Modern Roads - Well-developed network" :
                                   "Highway Network - Advanced transportation";
                
                infrastructureLevelDescLabel.text = description;
            }
        }

        private void UpdateSupplyLineLevelDescription(int level)
        {
            if (supplyLineLevelDescLabel != null)
            {
                string description = level == 0 ? "No supply line" :
                                   level <= 3 ? "Weak Supply - Limited capacity" :
                                   level <= 6 ? "Moderate Supply - Standard capacity" :
                                   level <= 8 ? "Strong Supply - High capacity" :
                                   "Maximum Supply - Full logistics support";
                
                supplyLineLevelDescLabel.text = description;
            }
        }

        private void UpdateDisplay()
        {
            if (currentTile == null || currentInfrastructure == null)
            {
                selectedTileLabel.text = "No tile selected";
                tileInfoLabel.text = "Click a tile to edit its infrastructure";
                clearAllBtn.SetEnabled(false);
                return;
            }
            
            selectedTileLabel.text = $"Editing Tile: {currentTile.Value}";
            clearAllBtn.SetEnabled(currentInfrastructure.HasAnyBuiltInfrastructure());
            
            // Build info summary
            var info = new List<string>();
            
            if (currentInfrastructure.cityType != CityType.None)
            {
                info.Add($"<b>City:</b> {currentInfrastructure.cityType}");
            }
            
            if (currentInfrastructure.roads.buildLevel > 0)
            {
                info.Add($"<b>Roads:</b> Level {currentInfrastructure.roads.buildLevel}");
            }
            
            // Supply info
            var supplyInfo = new List<string>();
            if (currentInfrastructure.isSupplyHub)
            {
                supplyInfo.Add("Supply Hub");
            }
            if (currentInfrastructure.supplyLine.buildLevel > 0)
            {
                supplyInfo.Add($"Supply Line Lvl {currentInfrastructure.supplyLine.buildLevel}");
            }
            if (supplyInfo.Count > 0)
            {
                info.Add($"<b>Supply:</b> {string.Join(", ", supplyInfo)}");
            }
            
            var buildings = new List<string>();
            if (currentInfrastructure.fortification.buildLevel > 0)
                buildings.Add($"Fortification Lvl {currentInfrastructure.fortification.buildLevel}");
            if (currentInfrastructure.port.buildLevel > 0)
                buildings.Add($"Port Lvl {currentInfrastructure.port.buildLevel}");
            
            if (buildings.Count > 0)
            {
                info.Add($"<b>Buildings:</b> {string.Join(", ", buildings)}");
            }
            
            var resources = new List<string>();
            if (currentInfrastructure.oil.buildLevel > 0)
                resources.Add($"Oil Lvl {currentInfrastructure.oil.buildLevel}");
            if (currentInfrastructure.electricity.buildLevel > 0)
                resources.Add($"Electricity Lvl {currentInfrastructure.electricity.buildLevel}");
            if (currentInfrastructure.steel.buildLevel > 0)
                resources.Add($"Steel Lvl {currentInfrastructure.steel.buildLevel}");
            if (currentInfrastructure.factory.buildLevel > 0)
                resources.Add($"Factory Lvl {currentInfrastructure.factory.buildLevel}");
            
            if (resources.Count > 0)
            {
                info.Add($"<b>Resources:</b> {string.Join(", ", resources)}");
            }
            
            tileInfoLabel.text = info.Count > 0 
                ? string.Join("\n", info) 
                : "No infrastructure on this tile";
        }

        private void OnDataChanged()
        {
            if (currentTile.HasValue)
            {
                Editor.tilemapManager.UpdateTile(currentTile.Value);
            }
        }
        
        private void OnTileNameChanged(string newName)
        {
            if (currentTile == null) return;
            if (!Editor.editingCampaign.HasTile(currentTile.Value)) return;
            
            var tileData = Editor.editingCampaign.EnsureTemplateTile(currentTile.Value);
            tileData.tileName = string.IsNullOrWhiteSpace(newName) ? string.Empty : newName.Trim();
            
            OnDataChanged();
        }

private void OnCityTypeChanged(int value)
        {
            if (currentInfrastructure == null) return;
            
            currentInfrastructure.cityType = (CityType)value;
            UpdateDisplay();
            OnDataChanged();
            Debug.Log($"Set city type to {currentInfrastructure.cityType} at {currentTile}");
        }

        private void OnInfrastructureLevelChanged(int level)
        {
            if (currentInfrastructure == null) return;
            
            var roads = currentInfrastructure.roads;
            roads.SetBuildLevel(level);
            currentInfrastructure.roads = roads;
            UpdateInfrastructureLevelDescription(level);
            UpdateDisplay();
            OnDataChanged();
            Debug.Log($"Set infrastructure level to {level} at {currentTile}");
        }

        private void OnSupplyHubChanged(bool isHub)
        {
            if (currentInfrastructure == null) return;
            
            currentInfrastructure.isSupplyHub = isHub;
            UpdateDisplay();
            OnDataChanged();
            Debug.Log($"Set supply hub to {isHub} at {currentTile}");
        }

        private void OnSupplyLineLevelChanged(int level)
        {
            if (currentInfrastructure == null) return;
            
            var supplyLine = currentInfrastructure.supplyLine;
            supplyLine.SetBuildLevel(level);
            currentInfrastructure.supplyLine = supplyLine;
            UpdateSupplyLineLevelDescription(level);
            UpdateDisplay();
            OnDataChanged();
            Debug.Log($"Set supply line level to {level} at {currentTile}");
        }
        private void OnClearAllClicked()
        {
            if (currentInfrastructure == null || currentTile == null) return;
            
            currentInfrastructure.Clear();
            UpdateUIFromInfrastructure();
            UpdateDisplay();
            OnDataChanged();
            Debug.Log($"Cleared all infrastructure at {currentTile}");
        }

        public override void SetCampaign()
        {
            currentTile = null;
            currentInfrastructure = null;
            UpdateDisplay();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            UpdateDisplay();
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();
            currentTile = null;
            currentInfrastructure = null;
        }
    }
}
