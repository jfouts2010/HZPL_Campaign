using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class AirportEditorMode : EditorMode
    {
        private readonly TilemapEditor _editor;

        private ListView airportsListView;
        private Button createBtn;
        private Button editBtn;
        private Button deleteBtn;
        private Label selectedAirportLabel;
        private Label selectedAirportSummaryLabel;
        private Label placementHintLabel;

        private VisualElement popupOverlay;
        private VisualElement editorPopup;
        private VisualElement deletePopup;
        private Label editorTitle;
        private TextField airportNameField;
        private DropdownField ownerAllianceDropdown;
        private SliderInt airportLevelSlider;
        private Label airportLevelLabel;
        private Label tileLabel;
        private Button saveBtn;
        private Button cancelBtn;

        private Label deleteMessage;
        private Button deleteConfirmBtn;
        private Button deleteCancelBtn;

        private readonly List<AirportDefinition> visibleAirports = new List<AirportDefinition>();
        private AirportDefinition selectedAirport;
        private bool isEditingExisting;

        private Campaign campaign => _editor.editingCampaign;

        public AirportEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter)
            : base(tab, editor, highlighter)
        {
            _editor = editor;
            WireUi();
        }

        private void WireUi()
        {
            airportsListView = _tab.Q<ListView>("airport-listview");
            createBtn = _tab.Q<Button>("airport-create-btn");
            editBtn = _tab.Q<Button>("airport-edit-btn");
            deleteBtn = _tab.Q<Button>("airport-delete-btn");
            selectedAirportLabel = _tab.Q<Label>("airport-selected-label");
            selectedAirportSummaryLabel = _tab.Q<Label>("airport-selected-summary");
            placementHintLabel = _tab.Q<Label>("airport-placement-hint");

            var root = _tab.panel.visualTree;
            popupOverlay = root.Q<VisualElement>("popup-overlay");
            editorPopup = root.Q<VisualElement>("airport-editor-popup");
            deletePopup = root.Q<VisualElement>("airport-delete-popup");
            editorTitle = root.Q<Label>("airport-editor-title");
            airportNameField = root.Q<TextField>("airport-name-field");
            ownerAllianceDropdown = root.Q<DropdownField>("airport-owner-dropdown");
            airportLevelSlider = root.Q<SliderInt>("airport-level-slider");
            airportLevelLabel = root.Q<Label>("airport-level-label");
            tileLabel = root.Q<Label>("airport-tile-label");
            saveBtn = root.Q<Button>("airport-save-btn");
            cancelBtn = root.Q<Button>("airport-cancel-btn");

            deleteMessage = root.Q<Label>("airport-delete-message");
            deleteConfirmBtn = root.Q<Button>("airport-delete-confirm-btn");
            deleteCancelBtn = root.Q<Button>("airport-delete-cancel-btn");

            ownerAllianceDropdown.choices = Enum.GetNames(typeof(Alliance)).ToList();

            SetupAirportListView();

            createBtn.clicked += OnCreateClicked;
            editBtn.clicked += OnEditClicked;
            deleteBtn.clicked += OnDeleteClicked;
            saveBtn.clicked += OnSaveClicked;
            cancelBtn.clicked += CloseEditorPopup;
            deleteConfirmBtn.clicked += OnDeleteConfirmClicked;
            deleteCancelBtn.clicked += CloseDeletePopup;

            airportNameField.RegisterValueChangedCallback(_ => UpdateEditorPreview());
            ownerAllianceDropdown.RegisterValueChangedCallback(_ => UpdateEditorPreview());
            airportLevelSlider.RegisterValueChangedCallback(evt => UpdateLevelLabel(evt.newValue));
            airportLevelSlider.RegisterValueChangedCallback(_ => UpdateEditorPreview());

            RefreshSelectedAirportDisplay();
            UpdateButtonsEnabled();
        }

        public override void SetCampaign()
        {
            campaign?.EnsureAirDataInitialized();
            RefreshAirportsList();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            campaign?.EnsureAirDataInitialized();
            RefreshAirportsList();
            RefreshSelectedAirportDisplay();
            HighlightSelectedAirport();
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();
            highlighter?.ClearHighlight();
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!base.PaintTile(cellPos, lastPaintedCell))
                return false;

            if (campaign == null)
                return false;

            campaign.EnsureAirDataInitialized();

            var clickedAirport = FindAirportAtCell(cellPos);
            if (clickedAirport != null && (selectedAirport == null || clickedAirport.Id != selectedAirport.Id))
            {
                selectedAirport = clickedAirport;
                RefreshAirportsList();
                return true;
            }

            if (selectedAirport == null)
                return clickedAirport != null;

            if (lastPaintedCell.HasValue && lastPaintedCell.Value == cellPos)
                return false;

            if (!IsValidTile(cellPos) || IsOccupiedByOtherAirport(cellPos, selectedAirport.Id))
                return false;

            selectedAirport.Tile = cellPos;
            _editor.RefreshCampaignView();
            RefreshAirportsList();
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (selectedAirport == null || selectedAirport.Tile != cellPos)
                return;

            selectedAirport = null;
            RefreshAirportsList();
        }

        private void SetupAirportListView()
        {
            airportsListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.paddingLeft = 10;
                container.style.paddingRight = 10;
                container.style.paddingTop = 6;
                container.style.paddingBottom = 6;

                var nameLabel = new Label();
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var infoLabel = new Label();
                infoLabel.style.fontSize = 11;
                infoLabel.style.color = new Color(0.75f, 0.75f, 0.75f);

                container.Add(nameLabel);
                container.Add(infoLabel);
                return container;
            };

            airportsListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= visibleAirports.Count)
                    return;

                var airport = visibleAirports[index];
                var nameLabel = element.ElementAt(0) as Label;
                var infoLabel = element.ElementAt(1) as Label;

                nameLabel.text = airport.Name;
                infoLabel.text = $"{airport.OwnerAlliance} @ {FormatTile(airport.Tile)} | Level {airport.Level}";
            };

            airportsListView.selectionChanged += OnAirportSelectionChanged;
            airportsListView.fixedItemHeight = 42;
        }

        private void OnAirportSelectionChanged(IEnumerable<object> selection)
        {
            selectedAirport = selection?.FirstOrDefault() as AirportDefinition;
            RefreshSelectedAirportDisplay();
            HighlightSelectedAirport();
            UpdateButtonsEnabled();
        }

        private void RefreshAirportsList()
        {
            visibleAirports.Clear();

            if (campaign != null)
            {
                campaign.EnsureAirDataInitialized();
                visibleAirports.AddRange((campaign.Airports ?? new List<AirportDefinition>())
                    .Where(airport => airport != null)
                    .OrderBy(airport => airport.OwnerAlliance)
                    .ThenBy(airport => airport.Name));
            }

            airportsListView.itemsSource = visibleAirports;
            airportsListView.Rebuild();

            if (selectedAirport != null)
            {
                var selectedIndex = visibleAirports.FindIndex(airport => airport.Id == selectedAirport.Id);
                if (selectedIndex >= 0)
                {
                    airportsListView.SetSelection(selectedIndex);
                    selectedAirport = visibleAirports[selectedIndex];
                }
                else
                {
                    selectedAirport = null;
                    airportsListView.ClearSelection();
                }
            }
            else
            {
                airportsListView.ClearSelection();
            }

            RefreshSelectedAirportDisplay();
            HighlightSelectedAirport();
            UpdateButtonsEnabled();
        }

        private void RefreshSelectedAirportDisplay()
        {
            if (selectedAirport == null)
            {
                selectedAirportLabel.text = "No airport selected.";
                selectedAirportSummaryLabel.text = string.Empty;
                placementHintLabel.text =
                    "Select an airport, then click on the map to move it. Clicking a tile that already contains an airport selects that airport.";
                return;
            }

            selectedAirportLabel.text = $"Selected: {selectedAirport.Name} ({selectedAirport.OwnerAlliance})";
            selectedAirportSummaryLabel.text =
                $"Tile: {FormatTile(selectedAirport.Tile)} | Airport Level: {selectedAirport.Level}";
            placementHintLabel.text =
                "Click an empty tile to move the selected airport. Right click the selected tile to clear the selection.";
        }

        private void HighlightSelectedAirport()
        {
            if (selectedAirport == null)
            {
                highlighter?.ClearHighlight();
                return;
            }

            highlighter?.HighlightTile(selectedAirport.Tile);
        }

        private void UpdateButtonsEnabled()
        {
            var hasCampaign = campaign != null;
            createBtn.SetEnabled(hasCampaign);
            editBtn.SetEnabled(hasCampaign && selectedAirport != null);
            deleteBtn.SetEnabled(hasCampaign && selectedAirport != null);
        }

        private void OnCreateClicked()
        {
            if (campaign == null)
                return;

            campaign.EnsureAirDataInitialized();
            isEditingExisting = false;

            var initialTile = GetAvailablePlacementCell(Guid.NewGuid(), _editor.lastPaintedCell);
            var airport = new AirportDefinition
            {
                Tile = initialTile,
                OwnerAlliance = GetAllianceForCell(initialTile),
                Name = "New Airport",
                Level = 1
            };

            OpenEditorPopup(CloneAirport(airport));
        }

        private void OnEditClicked()
        {
            if (selectedAirport == null)
                return;

            isEditingExisting = true;
            OpenEditorPopup(CloneAirport(selectedAirport));
        }

        private void OnDeleteClicked()
        {
            if (selectedAirport == null)
                return;

            deleteMessage.text = $"Delete airport '{selectedAirport.Name}'? Wings assigned to it will lose their home base.";
            popupOverlay.style.display = DisplayStyle.Flex;
            deletePopup.style.display = DisplayStyle.Flex;
        }

        private void OnDeleteConfirmClicked()
        {
            if (campaign == null || selectedAirport == null)
                return;

            campaign.EnsureAirDataInitialized();
            ClearWingAssignments(selectedAirport.Id);
            campaign.Airports.RemoveAll(airport => airport.Id == selectedAirport.Id);
            selectedAirport = null;

            CloseDeletePopup();
            _editor.RefreshCampaignView();
            RefreshAirportsList();
        }

        private void CloseDeletePopup()
        {
            deletePopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
        }

        private void OpenEditorPopup(AirportDefinition airport)
        {
            popupOverlay.style.display = DisplayStyle.Flex;
            editorPopup.style.display = DisplayStyle.Flex;
            editorPopup.userData = airport;

            editorTitle.text = isEditingExisting ? "Edit Airport" : "Create Airport";
            airportNameField.value = airport.Name ?? string.Empty;
            ownerAllianceDropdown.value = airport.OwnerAlliance.ToString();
            airportLevelSlider.value = Mathf.Clamp(airport.Level, 1, 10);
            tileLabel.text = FormatTile(airport.Tile);

            UpdateLevelLabel(airportLevelSlider.value);
            UpdateEditorPreview();
        }

        private void CloseEditorPopup()
        {
            editorPopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
            editorPopup.userData = null;
        }

        private void UpdateLevelLabel(int level)
        {
            airportLevelLabel.text = GetAirportLevelDescription(level);
        }

        private void UpdateEditorPreview()
        {
            if (editorPopup.userData is not AirportDefinition airport)
                return;

            tileLabel.text = FormatTile(airport.Tile);
            UpdateLevelLabel(Mathf.Clamp(airportLevelSlider.value, 1, 10));
        }

        private void OnSaveClicked()
        {
            if (campaign == null || editorPopup.userData is not AirportDefinition editedAirport)
                return;

            campaign.EnsureAirDataInitialized();

            editedAirport.Name = string.IsNullOrWhiteSpace(airportNameField.value)
                ? "Unnamed Airport"
                : airportNameField.value.Trim();
            editedAirport.OwnerAlliance = ParseAlliance(ownerAllianceDropdown.value);
            editedAirport.Level = Mathf.Clamp(airportLevelSlider.value, 1, 10);
            editedAirport.Tile = GetAvailablePlacementCell(editedAirport.Id, editedAirport.Tile);

            if (isEditingExisting)
            {
                var index = campaign.Airports.FindIndex(airport => airport.Id == editedAirport.Id);
                if (index >= 0)
                    campaign.Airports[index] = editedAirport;
            }
            else
            {
                campaign.Airports.Add(editedAirport);
            }

            selectedAirport = campaign.Airports.FirstOrDefault(airport => airport.Id == editedAirport.Id);
            CloseEditorPopup();
            _editor.RefreshCampaignView();
            RefreshAirportsList();
        }

        private void ClearWingAssignments(Guid airportId)
        {
            if (campaign?.Wings == null)
                return;

            foreach (var wing in campaign.Wings.Where(wing => wing != null && wing.HomeAirportId == airportId))
            {
                wing.HomeAirportId = Guid.Empty;
                wing.HomeAirfieldCell = new Vector3Int(int.MinValue, int.MinValue, 0);
            }
        }

        private AirportDefinition CloneAirport(AirportDefinition airport)
        {
            return new AirportDefinition
            {
                Id = airport.Id,
                Name = airport.Name,
                Tile = airport.Tile,
                OwnerAlliance = airport.OwnerAlliance,
                Level = airport.Level
            };
        }

        private AirportDefinition FindAirportAtCell(Vector3Int cellPos)
        {
            return campaign?.Airports?.FirstOrDefault(airport => airport != null && airport.Tile == cellPos);
        }

        private bool IsValidTile(Vector3Int cellPos)
        {
            return campaign?.tileData != null && campaign.tileData.ContainsKey(cellPos);
        }

        private bool IsOccupiedByOtherAirport(Vector3Int cellPos, Guid airportId)
        {
            return campaign?.Airports?.Any(airport =>
                       airport != null && airport.Id != airportId && airport.Tile == cellPos) == true;
        }

        private Vector3Int GetAvailablePlacementCell(Guid airportId, Vector3Int preferredCell)
        {
            if (IsValidTile(preferredCell) && !IsOccupiedByOtherAirport(preferredCell, airportId))
                return preferredCell;

            if (IsValidTile(_editor.lastPaintedCell) && !IsOccupiedByOtherAirport(_editor.lastPaintedCell, airportId))
                return _editor.lastPaintedCell;

            if (campaign?.tileData != null)
            {
                foreach (var tile in campaign.tileData.Keys.OrderBy(tile => tile.x).ThenBy(tile => tile.y))
                {
                    if (!IsOccupiedByOtherAirport(tile, airportId))
                        return tile;
                }
            }

            return preferredCell;
        }

        private Alliance GetAllianceForCell(Vector3Int cellPos)
        {
            if (campaign?.tileData != null && campaign.tileData.TryGetValue(cellPos, out var tileData) && tileData != null)
                return tileData.controllingAlliance;

            return Alliance.Neutral;
        }

        private static Alliance ParseAlliance(string value)
        {
            return Enum.TryParse(value, out Alliance alliance) ? alliance : Alliance.Neutral;
        }

        private static string GetAirportLevelDescription(int level)
        {
            if (level <= 2) return "Rough strip / austere field";
            if (level <= 4) return "Small regional airfield";
            if (level <= 6) return "Standard military airport";
            if (level <= 8) return "Large operational air base";
            return "Major strategic air hub";
        }

        private static string FormatTile(Vector3Int tile)
        {
            return $"({tile.x}, {tile.y}, {tile.z})";
        }
    }
}
