using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using Monobehaviours.Singletons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Air wing placement and editing.
    ///
    /// Refactor note:
    /// Campaigns no longer store CampaignCountry wrappers. Campaigns store a list of Country IDs and
    /// other per-country campaign state (alliances, spawns, etc.) keyed by Guid.
    /// </summary>
    public class AirWingEditorMode : EditorMode
    {
        private const int DefaultWingAircraftCount = 50;
        private const int EditableSquadronCount = 2;

        private readonly TilemapEditor _editor;

        private DropdownField countryDropdown;
        private ListView wingsListView;
        private Button createBtn;
        private Button editBtn;
        private Button deleteBtn;
        private Label selectedWingLabel;
        private Label placementHintLabel;

        // Popup
        private VisualElement popupOverlay;
        private VisualElement editorPopup;
        private Label editorTitle;
        private TextField wingNameField;
        private DropdownField wingTypeDropdown;
        private IntegerField aircraftCountField;
        private DropdownField aircraftTypeDropdown;

        private TextField wingPatchPathField;
        private Button wingPatchBrowseBtn;
        private VisualElement wingPatchPreview;

        private TextField sq1NameField;
        private TextField sq1PatchPathField;
        private Button sq1PatchBrowseBtn;
        private VisualElement sq1PatchPreview;

        private TextField sq2NameField;
        private TextField sq2PatchPathField;
        private Button sq2PatchBrowseBtn;
        private VisualElement sq2PatchPreview;

        private Button saveBtn;
        private Button cancelBtn;

        // Delete confirm popup
        private VisualElement deletePopup;
        private Label deleteMessage;
        private Button deleteConfirmBtn;
        private Button deleteCancelBtn;

        private Campaign campaign => _editor.editingCampaign;

        // Countries available in the current campaign (resolved from module pool).
        private List<CountryData> CampaignCountries => Editor.editingCampaign.CampaignCountries;

        private CountryData _selectedCountry;
        private AirWing selectedWing;

        private bool isEditingExisting;
        private Vector3Int pendingPlacementCell;

        public AirWingEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter) : base(tab, editor, highlighter)
        {
            _tab = tab;
            Editor = editor;
            _editor = editor;
            this.highlighter = highlighter;

            WireUI();
        }

        private void WireUI()
        {
            countryDropdown = _tab.Q<DropdownField>("airwing-country-dropdown");
            wingsListView = _tab.Q<ListView>("airwing-wings-listview");
            createBtn = _tab.Q<Button>("airwing-create-btn");
            editBtn = _tab.Q<Button>("airwing-edit-btn");
            deleteBtn = _tab.Q<Button>("airwing-delete-btn");
            selectedWingLabel = _tab.Q<Label>("airwing-selected-label");
            placementHintLabel = _tab.Q<Label>("airwing-placement-hint");

            var root = _tab.panel.visualTree;
            popupOverlay = root.Q<VisualElement>("popup-overlay");

            editorPopup = root.Q<VisualElement>("airwing-editor-popup");
            editorTitle = root.Q<Label>("airwing-editor-title");
            wingNameField = root.Q<TextField>("airwing-name-field");
            wingTypeDropdown = root.Q<DropdownField>("airwing-type-dropdown");
            aircraftCountField = root.Q<IntegerField>("airwing-aircraftcount-field");
            aircraftTypeDropdown = root.Q<DropdownField>("airwing-aircrafttype-dropdown");

            wingPatchPathField = root.Q<TextField>("airwing-patch-path-field");
            wingPatchBrowseBtn = root.Q<Button>("airwing-patch-browse-btn");
            wingPatchPreview = root.Q<VisualElement>("airwing-patch-preview");

            sq1NameField = root.Q<TextField>("airwing-sq1-name-field");
            sq1PatchPathField = root.Q<TextField>("airwing-sq1-patch-path-field");
            sq1PatchBrowseBtn = root.Q<Button>("airwing-sq1-patch-browse-btn");
            sq1PatchPreview = root.Q<VisualElement>("airwing-sq1-patch-preview");

            sq2NameField = root.Q<TextField>("airwing-sq2-name-field");
            sq2PatchPathField = root.Q<TextField>("airwing-sq2-patch-path-field");
            sq2PatchBrowseBtn = root.Q<Button>("airwing-sq2-patch-browse-btn");
            sq2PatchPreview = root.Q<VisualElement>("airwing-sq2-patch-preview");

            saveBtn = root.Q<Button>("airwing-save-btn");
            cancelBtn = root.Q<Button>("airwing-cancel-btn");

            deletePopup = root.Q<VisualElement>("airwing-delete-popup");
            deleteMessage = root.Q<Label>("airwing-delete-message");
            deleteConfirmBtn = root.Q<Button>("airwing-delete-confirm-btn");
            deleteCancelBtn = root.Q<Button>("airwing-delete-cancel-btn");

            // Dropdown setup
            wingTypeDropdown.choices = Enum.GetNames(typeof(AirWingType)).ToList();

            // ListView setup
            SetupWingsListView();

            // Wire callbacks
            countryDropdown.RegisterValueChangedCallback(evt => OnCountryChanged(evt.newValue));
            createBtn.clicked += OnCreateClicked;
            editBtn.clicked += OnEditClicked;
            deleteBtn.clicked += OnDeleteClicked;
            saveBtn.clicked += OnSaveClicked;
            cancelBtn.clicked += CloseEditorPopup;

            wingPatchBrowseBtn.clicked += () => BrowseSpriteIntoField(wingPatchPathField, wingPatchPreview);
            sq1PatchBrowseBtn.clicked += () => BrowseSpriteIntoField(sq1PatchPathField, sq1PatchPreview);
            sq2PatchBrowseBtn.clicked += () => BrowseSpriteIntoField(sq2PatchPathField, sq2PatchPreview);

            deleteConfirmBtn.clicked += OnDeleteConfirm;
            deleteCancelBtn.clicked += CloseDeletePopup;

            RefreshSelectedWingDisplay();
            UpdateButtonsEnabled();
        }

        public override void SetCampaign()
        {
            RefreshCountryDropdown();
            RefreshWingsList();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshCountryDropdown();
            RefreshWingsList();
            RefreshSelectedWingDisplay();
            HighlightSelectedWingHome();
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();
            // optional: clear highlight when leaving mode
            highlighter?.ClearHighlight();
        }

        // Paint here is to select their home base
        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            base.PaintTile(cellPos, lastPaintedCell);
            pendingPlacementCell = cellPos;

            if (selectedWing == null || campaign == null)
                return false;

            // Avoid repeated same-cell operations while dragging
            if (lastPaintedCell.HasValue && lastPaintedCell.Value == cellPos)
                return false;

            if (!IsValidAirfieldTile(cellPos))
                return false;

            if (!TryGetSelectedCountryAlliance(out var selectedAlliance))
                return false;

            if (_editor.editingCampaign.tileData[cellPos].controllingAlliance != selectedAlliance)
                return false;

            selectedWing.HomeAirfieldCell = cellPos;
            RefreshSelectedWingDisplay();
            HighlightSelectedWingHome();

            // If list needs to show updated info quickly
            wingsListView?.RefreshItems();
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
            // We do NOT erase home location on right-click
            // (could be added later if you want unassign behavior)
            pendingPlacementCell = cellPos;
        }

        private bool IsValidAirfieldTile(Vector3Int cell)
        {
            if (campaign == null) return false;

            if (campaign.tileData.TryGetValue(cell, out var td))
            {
                if (td?.infrastructure != null && td.infrastructure.airfieldLevel > 0)
                    return true;
            }

            return false;
        }

        private bool TryGetSelectedCountryAlliance(out Alliance alliance)
        {
            alliance = Alliance.Neutral;
            if (campaign == null || _selectedCountry == null) return false;

            // If not explicitly set yet, treat as Neutral.
            if (campaign.CountryAlliance != null && campaign.CountryAlliance.TryGetValue(_selectedCountry.ID, out var a))
                alliance = a;

            return true;
        }

        private void HighlightSelectedWingHome()
        {
            if (selectedWing == null)
            {
                highlighter?.ClearHighlight();
                return;
            }

            highlighter?.HighlightTile(selectedWing.HomeAirfieldCell);
        }

        private void RefreshCountryDropdown()
        {
            if (countryDropdown == null) return;

            var campaignCountries = CampaignCountries;
            if (campaignCountries.Count == 0)
            {
                countryDropdown.choices = new List<string>();
                _selectedCountry = null;
                countryDropdown.value = null;
                return;
            }

            var countryNames = campaignCountries.Select(c => c.CountryName).ToList();
            countryDropdown.choices = countryNames;

            if (countryNames.Count == 0)
            {
                countryDropdown.value = string.Empty;
                _selectedCountry = null;
                return;
            }

            if (_selectedCountry == null || !countryNames.Contains(_selectedCountry.CountryName))
            {
                _selectedCountry = campaignCountries[0];
                countryDropdown.value = _selectedCountry.CountryName;
            }
            else
            {
                // Ensure UI value matches selected object.
                countryDropdown.value = _selectedCountry.CountryName;
            }
        }

        private void OnCountryChanged(string newValue)
        {
            _selectedCountry = CampaignCountries.FirstOrDefault(c => c.CountryName == newValue);
            selectedWing = null;
            RefreshWingsList();
            RefreshSelectedWingDisplay();
            HighlightSelectedWingHome();
            UpdateButtonsEnabled();
        }

        private void SetupWingsListView()
        {
            wingsListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.paddingLeft = 10;
                container.style.paddingRight = 10;
                container.style.paddingTop = 6;
                container.style.paddingBottom = 6;

                var nameLabel = new Label();
                nameLabel.style.flexGrow = 1;

                var infoLabel = new Label();
                infoLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                infoLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f));
                infoLabel.style.minWidth = 120;

                container.Add(nameLabel);
                container.Add(infoLabel);
                return container;
            };

            wingsListView.bindItem = (element, index) =>
            {
                var list = GetFilteredWings();
                if (index < 0 || index >= list.Count) return;

                var wing = list[index];
                var nameLabel = element.ElementAt(0) as Label;
                var infoLabel = element.ElementAt(1) as Label;

                nameLabel.text = wing.Name;
                infoLabel.text = $"{wing.WingType} • {GetWingAircraftCount(wing)}/{DefaultWingAircraftCount}";
            };

            wingsListView.selectionChanged += OnWingSelectionChanged;
            wingsListView.fixedItemHeight = 32;
        }

        private void OnWingSelectionChanged(IEnumerable<object> selection)
        {
            selectedWing = selection?.FirstOrDefault() as AirWing;
            RefreshSelectedWingDisplay();
            HighlightSelectedWingHome();
            UpdateButtonsEnabled();
        }

        private void RefreshWingsList()
        {
            if (wingsListView == null) return;
            wingsListView.itemsSource = GetFilteredWings();
            wingsListView.Rebuild();

            if (selectedWing != null && !GetFilteredWings().Contains(selectedWing))
            {
                selectedWing = null;
            }

            RefreshSelectedWingDisplay();
            HighlightSelectedWingHome();
            UpdateButtonsEnabled();
        }

        private List<AirWing> GetFilteredWings()
        {
            if (campaign == null) return new List<AirWing>();
            if (_selectedCountry == null) return new List<AirWing>();
            return Editor.editingCampaign.airWingSpawns.Where(w => w.CountryId == _selectedCountry.ID).ToList();
        }

        private void RefreshSelectedWingDisplay()
        {
            if (selectedWingLabel == null) return;

            if (selectedWing == null)
            {
                selectedWingLabel.text = "No wing selected.";
                placementHintLabel.text = "Select a wing, then click/drag on an airfield tile (Airfield Level > 0) to set its home base.";
            }
            else
            {
                selectedWingLabel.text = $"Selected: {selectedWing.Name} ({selectedWing.WingType})";
                placementHintLabel.text =
                    $"Home Base Tile: {selectedWing.HomeAirfieldCell}  |  Paint to change (Airfield Level > 0 required).";
            }
        }

        private void UpdateButtonsEnabled()
        {
            bool hasCountry = _selectedCountry != null;
            createBtn.SetEnabled(hasCountry);
            editBtn.SetEnabled(hasCountry && selectedWing != null);
            deleteBtn.SetEnabled(hasCountry && selectedWing != null);
        }

        private void OnCreateClicked()
        {
            if (_selectedCountry == null || campaign == null) return;

            isEditingExisting = false;
            selectedWing = null;
            pendingPlacementCell = Editor.lastPaintedCell;

            OpenEditorPopupForWing(new AirWing("New Wing", AirWingType.Fighter, _selectedCountry.ID,
                pendingPlacementCell));
        }

        private void OnEditClicked()
        {
            if (selectedWing == null) return;

            isEditingExisting = true;
            pendingPlacementCell = Editor.lastPaintedCell;

            var clone = CloneWing(selectedWing);
            OpenEditorPopupForWing(clone);
        }

        private void OnDeleteClicked()
        {
            if (selectedWing == null || campaign == null) return;

            popupOverlay.style.display = DisplayStyle.Flex;
            deletePopup.style.display = DisplayStyle.Flex;
            deleteMessage.text = $"Delete wing '{selectedWing.Name}'? This cannot be undone.";
        }

        private void OnDeleteConfirm()
        {
            if (selectedWing == null || campaign == null) return;

            Editor.editingCampaign.airWingSpawns.Remove(selectedWing);
            selectedWing = null;

            CloseDeletePopup();
            RefreshWingsList();
        }

        private void CloseDeletePopup()
        {
            deletePopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
        }

        private void OpenEditorPopupForWing(AirWing wing)
        {
            popupOverlay.style.display = DisplayStyle.Flex;
            editorPopup.style.display = DisplayStyle.Flex;

            editorTitle.text = isEditingExisting ? "Edit Air Wing" : "Create Air Wing";

            wingNameField.value = wing.Name;
            wingTypeDropdown.value = wing.WingType.ToString();
            aircraftCountField.value = Mathf.Clamp(GetWingAircraftCount(wing), 0, DefaultWingAircraftCount);

            PopulateAircraftTypeDropdown();
            var wingAircraftType = GetWingAircraftType(wing);
            aircraftTypeDropdown.value = wingAircraftType != null
                ? wingAircraftType.AircraftName
                : aircraftTypeDropdown.choices.FirstOrDefault();

            var squadronA = GetEditableSquadron(wing, 0, "Squadron A");
            var squadronB = GetEditableSquadron(wing, 1, "Squadron B");

            wingPatchPathField.value = wing.PatchSpritePath ?? string.Empty;
            sq1NameField.value = squadronA.Name;
            sq1PatchPathField.value = squadronA.PatchSpritePath ?? string.Empty;
            sq2NameField.value = squadronB.Name;
            sq2PatchPathField.value = squadronB.PatchSpritePath ?? string.Empty;

            RefreshSpritePreview(wingPatchPathField.value, wingPatchPreview);
            RefreshSpritePreview(sq1PatchPathField.value, sq1PatchPreview);
            RefreshSpritePreview(sq2PatchPathField.value, sq2PatchPreview);

            editorPopup.userData = wing;
        }

        private void CloseEditorPopup()
        {
            editorPopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
            editorPopup.userData = null;
        }

        private void OnSaveClicked()
        {
            if (campaign == null || _selectedCountry == null) return;
            if (editorPopup.userData is not AirWing wing) return;

            wing.Name = string.IsNullOrWhiteSpace(wingNameField.value) ? "Unnamed Wing" : wingNameField.value.Trim();
            wing.WingType = Enum.TryParse<AirWingType>(wingTypeDropdown.value, out var t) ? t : AirWingType.Fighter;
            wing.PatchSpritePath = wingPatchPathField.value ?? string.Empty;

            wing.CountryId = _selectedCountry.ID;
            wing.Squadrons = BuildSquadronsFromEditor(GetAircraftFromDropdown(),
                Mathf.Clamp(aircraftCountField.value, 0, DefaultWingAircraftCount));

            // We allow placement at current cursor tile if valid; otherwise leave unchanged
            var desired = Editor.lastPaintedCell;
            if (IsValidAirfieldTile(desired))
                wing.HomeAirfieldCell = desired;

            if (isEditingExisting)
            {
                var idx = Editor.editingCampaign.airWingSpawns.FindIndex(w => w == selectedWing);
                if (idx >= 0)
                {
                    Editor.editingCampaign.airWingSpawns[idx] = wing;
                    selectedWing = wing;
                }
            }
            else
            {
                Editor.editingCampaign.airWingSpawns.Add(wing);
                selectedWing = wing;
            }

            CloseEditorPopup();
            RefreshWingsList();
        }

        private void PopulateAircraftTypeDropdown()
        {
            if (aircraftTypeDropdown == null) return;

            var choices = _selectedCountry?.AllowedAircraft?.Select(a => a.AircraftName).ToList()
                         ?? new List<string>();

            if (choices.Count == 0)
                choices.Add("(No allowed aircraft)");

            aircraftTypeDropdown.choices = choices;
            if (string.IsNullOrEmpty(aircraftTypeDropdown.value))
                aircraftTypeDropdown.value = choices[0];
        }

        private AircraftData GetDefaultAircraftForCountry()
        {
            return _selectedCountry?.AllowedAircraft?.FirstOrDefault();
        }

        private AircraftData GetAircraftFromDropdown()
        {
            if (_selectedCountry?.AllowedAircraft == null) return null;
            return _selectedCountry.AllowedAircraft
                .FirstOrDefault(a => a.AircraftName == aircraftTypeDropdown.value)
                ?? _selectedCountry.AllowedAircraft.FirstOrDefault();
        }

        private int GetWingAircraftCount(AirWing wing)
        {
            return wing?.Squadrons?.Sum(s => Mathf.Max(0, s?.AircraftCount ?? 0)) ?? 0;
        }

        private AircraftData GetWingAircraftType(AirWing wing)
        {
            return wing?.Squadrons?.FirstOrDefault(s => s?.AircraftType != null)?.AircraftType;
        }

        private AirSquadron GetEditableSquadron(AirWing wing, int index, string defaultName)
        {
            var existingSquadron = wing?.Squadrons?.ElementAtOrDefault(index);
            if (existingSquadron != null)
                return existingSquadron;

            return new AirSquadron(defaultName, string.Empty, 0, GetDefaultAircraftForCountry());
        }

        private List<AirSquadron> BuildSquadronsFromEditor(AircraftData aircraftType, int totalAircraftCount)
        {
            var clampedAircraftCount = Mathf.Clamp(totalAircraftCount, 0, DefaultWingAircraftCount);
            return new List<AirSquadron>
            {
                BuildSquadronFromEditor(0, "Squadron A", aircraftType, clampedAircraftCount),
                BuildSquadronFromEditor(1, "Squadron B", aircraftType, clampedAircraftCount)
            };
        }

        private AirSquadron BuildSquadronFromEditor(int squadronIndex, string defaultName, AircraftData aircraftType,
            int totalAircraftCount)
        {
            var squadronNameField = squadronIndex == 0 ? sq1NameField : sq2NameField;
            var squadronPatchField = squadronIndex == 0 ? sq1PatchPathField : sq2PatchPathField;
            var squadronName = string.IsNullOrWhiteSpace(squadronNameField.value)
                ? defaultName
                : squadronNameField.value.Trim();

            return new AirSquadron(squadronName, squadronPatchField.value ?? string.Empty,
                GetAircraftCountForSquadron(squadronIndex, totalAircraftCount), aircraftType);
        }

        private int GetAircraftCountForSquadron(int squadronIndex, int totalAircraftCount)
        {
            var baseSquadronCount = totalAircraftCount / EditableSquadronCount;
            var extraAircraft = totalAircraftCount % EditableSquadronCount;
            return baseSquadronCount + (squadronIndex < extraAircraft ? 1 : 0);
        }

        private void BrowseSpriteIntoField(TextField pathField, VisualElement preview)
        {
#if UNITY_EDITOR
            string start = "Assets";
            if (!string.IsNullOrEmpty(pathField.value) && pathField.value.StartsWith("Assets"))
            {
                start = System.IO.Path.GetDirectoryName(pathField.value);
            }

            string selected = EditorUtility.OpenFilePanel("Select Patch Sprite", start, "png,jpg,jpeg,tga,psd");
            if (string.IsNullOrEmpty(selected)) return;

            if (selected.Replace("\\", "/").Contains("/Assets/"))
            {
                int idx = selected.Replace("\\", "/").LastIndexOf("/Assets/", StringComparison.Ordinal);
                pathField.value = selected.Substring(idx + 1);
            }
            else
            {
                pathField.value = selected;
            }

            RefreshSpritePreview(pathField.value, preview);
#endif
        }

        private void RefreshSpritePreview(string path, VisualElement preview)
        {
#if UNITY_EDITOR
            if (preview == null) return;

            var sprite = LoadSpriteFromPath(path);
            if (sprite != null)
            {
                preview.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                preview.style.backgroundImage = null;
            }
#endif
        }

#if UNITY_EDITOR
        private Sprite LoadSpriteFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            if (System.IO.File.Exists(path))
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            return null;
        }
#endif

        private AirWing CloneWing(AirWing src)
        {
            var clone = new AirWing
            {
                Name = src.Name,
                WingType = src.WingType,
                CountryId = src.CountryId,
                PatchSpritePath = src.PatchSpritePath,
                HomeAirfieldCell = src.HomeAirfieldCell,
                Squadrons = src.Squadrons?.Select(s => new AirSquadron(s.Name, s.PatchSpritePath, s.AircraftCount, s.AircraftType)).ToList()
                            ?? new List<AirSquadron>()
            };
            return clone;
        }
    }
}
