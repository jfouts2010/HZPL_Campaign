using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Paints land tiles with a controlling alliance for the scenario.
    /// All land tiles should be assigned to some alliance for the scenario to be playable.
    /// </summary>
    public class CountryControlEditorMode : EditorMode
    {
        private ListView alliancesListView;
        private Label selectedAllianceLabel;
        private Label validationLabel;

        private Alliance _selectedAlliance;

        public CountryControlEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter tileHighlighter) : base(tab, editor, tileHighlighter)
        {
            SetupUI();
        }

        private void SetupUI()
        {
            // New (alliance-based) element names
            alliancesListView = _tab.Q<ListView>("control-alliances-list");
            selectedAllianceLabel = _tab.Q<Label>("control-selected-alliance-label");

            // Backwards compatibility with older UXML (country-based naming)
            alliancesListView ??= _tab.Q<ListView>("control-countries-list");
            selectedAllianceLabel ??= _tab.Q<Label>("control-selected-country-label");

            validationLabel = _tab.Q<Label>("control-validation-label");

            if (alliancesListView == null)
            {
                Debug.LogError("CountryControlEditorMode: Missing ListView for alliance control. Expected 'control-alliances-list' (or legacy 'control-countries-list').");
                return;
            }

            alliancesListView.makeItem = () =>
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

                var swatch = new VisualElement { name = "swatch" };
                swatch.style.width = 12;
                swatch.style.height = 12;
                swatch.style.marginRight = 8;
                swatch.style.borderTopLeftRadius = 3;
                swatch.style.borderTopRightRadius = 3;
                swatch.style.borderBottomLeftRadius = 3;
                swatch.style.borderBottomRightRadius = 3;

                var name = new Label { name = "name" };
                name.style.flexGrow = 1;

                row.Add(swatch);
                row.Add(name);
                return row;
            };

            alliancesListView.bindItem = (e, i) =>
            {
                var alliances = GetAlliances();
                if (i < 0 || i >= alliances.Count) return;

                var a = alliances[i];
                var swatch = e.Q<VisualElement>("swatch");
                var name = e.Q<Label>("name");
                name.text = GetAllianceDisplayName(a);
                swatch.style.backgroundColor = GetAllianceColor(a);
            };

            alliancesListView.selectionType = SelectionType.Single;
            alliancesListView.selectionChanged += OnAllianceSelected;
            alliancesListView.fixedItemHeight = 28;

            // Default to Neutral so right away painting is safe.
            _selectedAlliance = Alliance.Neutral;
            UpdateSelectedAllianceLabel();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            Editor.tilemapManager.controlTilemap.gameObject.GetComponent<TilemapRenderer>().enabled = true;
            RefreshUI();
            ShowValidation();
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();
            Editor.tilemapManager.controlTilemap.gameObject.GetComponent<TilemapRenderer>().enabled = false;
            if (validationLabel != null) validationLabel.text = "";
        }

        public override void SetCampaign()
        {
            RefreshUI();
            ShowValidation();
        }

        private void RefreshUI()
        {
            if (alliancesListView == null) return;

            alliancesListView.itemsSource = GetAlliances();
            alliancesListView.Rebuild();
            UpdateSelectedAllianceLabel();
        }

        private void OnAllianceSelected(IEnumerable<object> selection)
        {
            var picked = selection?.FirstOrDefault();
            if (picked is Alliance a)
            {
                _selectedAlliance = a;
                UpdateSelectedAllianceLabel();
            }
            ShowValidation();
        }

        private void UpdateSelectedAllianceLabel()
        {
            if (selectedAllianceLabel == null) return;
            selectedAllianceLabel.text = GetAllianceDisplayName(_selectedAlliance);
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            bool success = base.PaintTile(cellPos, lastPaintedCell);
            if (!success) return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;

            if (Editor.editingCampaign == null)
                return false;

            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
                Editor.editingCampaign.tileData[cellPos] = new HZPLTileData();

            var tile = Editor.editingCampaign.tileData[cellPos];

            // Only allow painting land tiles
            if (!tile.LandTile)
                return false;

            tile.controllingAlliance = _selectedAlliance;
            Editor.tilemapManager.UpdateTile(cellPos);
            ShowValidation();
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (Editor.editingCampaign == null) return;

            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
                return;

            var tile = Editor.editingCampaign.tileData[cellPos];
            if (!tile.LandTile)
                return;

            tile.controllingAlliance = Alliance.Neutral;
            Editor.tilemapManager.UpdateTile(cellPos);
            ShowValidation();
        }

        private void ShowValidation()
        {
            if (validationLabel == null) return;
            if (Editor.editingCampaign == null)
            {
                validationLabel.text = "";
                return;
            }

            int totalLand = 0;
            int unassigned = 0;

            foreach (var kvp in Editor.editingCampaign.tileData)
            {
                var td = kvp.Value;
                if (!td.LandTile) continue;
                totalLand++;
                if (td.controllingAlliance == Alliance.Neutral) unassigned++;
            }

            if (totalLand == 0)
            {
                validationLabel.text = "No land tiles found. Paint land tiles first.";
                validationLabel.style.color = new StyleColor(Color.yellow);
            }
            else if (unassigned == 0)
            {
                validationLabel.text = "✅ All land tiles assigned to an alliance.";
                validationLabel.style.color = new StyleColor(Color.green);
            }
            else
            {
                validationLabel.text = $"⚠ {unassigned} / {totalLand} land tiles unassigned.";
                validationLabel.style.color = new StyleColor(Color.red);
            }
        }

        private static List<Alliance> GetAlliances()
        {
            // Explicit ordering to match gameplay meaning.
            return new List<Alliance> { Alliance.Neutral, Alliance.BlueFor, Alliance.RedFor };
        }

        private static string GetAllianceDisplayName(Alliance alliance)
        {
            return alliance switch
            {
                Alliance.BlueFor => "BlueFor",
                Alliance.RedFor => "RedFor",
                _ => "Neutral"
            };
        }

        private static Color GetAllianceColor(Alliance alliance)
        {
            return alliance switch
            {
                Alliance.BlueFor => Color.blue,
                Alliance.RedFor => Color.red,
                _ => new Color(0.75f, 0.75f, 0.75f, 1f)
            };
        }
    }
}
