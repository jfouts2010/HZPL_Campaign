using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using Monobehaviours.Singletons;
using ScriptableObjects.Gameplay.Units;
using Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class DivisionEditorMode : EditorMode
    {
        private DropdownField countryDropdown;
        private ListView divisionTemplatesListView;
        private Button createDivisionBtn;
        private Button editDivisionBtn;
        private Button deleteDivisionBtn;

        // Popup elements
        private VisualElement popupOverlay;
        private VisualElement divisionEditorPopup;
        private VisualElement divisionDeletePopup;

        private Label editorTitle;
        private TextField divisionNameField;

        // Tabs
        private Button compositionTabBtn;
        private Button statsTabBtn;
        private VisualElement compositionTab;
        private VisualElement statsTab;

        // Stats labels (live-updated while editing)
        private Label statsStrengthLabel;
        private Label statsOrganizationLabel;
        private Label statsMoraleLabel;
        private Label statsSoftAttackLabel;
        private Label statsHardAttackLabel;
        private Label statsDefenseLabel;
        private Label statsToughnessLabel;
        private Label statsSoftnessLabel;
        private Label statsSpeedLabel;
        private Label statsCombatWidthLabel;
        private Label statsSupplyConsumptionLabel;
        private Label statsFuelConsumptionLabel;
        private VisualElement mobileAirDefenseSection;
        private Label mobileAirDefenseSummaryLabel;
        private Label mobileAirDefenseMissilesLabel;
        private ListView availableBattalionsListView;
        private ListView selectedBattalionsListView;
        private Button editorSaveBtn;
        private Button editorCancelBtn;

        private Label deleteMessage;
        private Button deleteConfirmBtn;
        private Button deleteCancelBtn;

        private CountryData _selectedCountry;

        // Backing list for the ListView so we can rebuild without constantly allocating.
        private readonly List<DivisionTemplate> _countryDivisionTemplates = new List<DivisionTemplate>();

        private DivisionTemplate editingDivision;
        private bool isEditMode;

        // Temporary composition during editing
        private Dictionary<Guid, int> tempBattalionComposition = new Dictionary<Guid, int>();

        private List<Guid> availableBattalionsIDs = new List<Guid>();
        private List<BattalionData> availBattalions = new List<BattalionData>();
        public DivisionEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter) : base(tab, editor, _highlighter)
        {
            InitializeUI();
            RefreshCountryDropdown();
        }

        private void InitializeUI()
        {
            countryDropdown = _tab.Q<DropdownField>("division-country-dropdown");
            divisionTemplatesListView = _tab.Q<ListView>("division-templates-listview");
            createDivisionBtn = _tab.Q<Button>("division-create-btn");
            editDivisionBtn = _tab.Q<Button>("division-edit-btn");
            deleteDivisionBtn = _tab.Q<Button>("division-delete-btn");

            popupOverlay = Editor.uiDocument.rootVisualElement.Q<VisualElement>("popup-overlay");
            divisionEditorPopup = popupOverlay.Q<VisualElement>("division-editor-popup");
            divisionDeletePopup = popupOverlay.Q<VisualElement>("division-delete-popup");

            editorTitle = divisionEditorPopup.Q<Label>("division-editor-title");
            divisionNameField = divisionEditorPopup.Q<TextField>("division-name-field");

            compositionTabBtn = divisionEditorPopup.Q<Button>("division-editor-composition-tab-btn");
            statsTabBtn = divisionEditorPopup.Q<Button>("division-editor-stats-tab-btn");
            compositionTab = divisionEditorPopup.Q<VisualElement>("division-editor-composition-tab");
            statsTab = divisionEditorPopup.Q<VisualElement>("division-editor-stats-tab");

            statsStrengthLabel = divisionEditorPopup.Q<Label>("division-stats-strength");
            statsOrganizationLabel = divisionEditorPopup.Q<Label>("division-stats-organization");
            statsMoraleLabel = divisionEditorPopup.Q<Label>("division-stats-morale");
            statsSoftAttackLabel = divisionEditorPopup.Q<Label>("division-stats-softattack");
            statsHardAttackLabel = divisionEditorPopup.Q<Label>("division-stats-hardattack");
            statsDefenseLabel = divisionEditorPopup.Q<Label>("division-stats-defense");
            statsToughnessLabel = divisionEditorPopup.Q<Label>("division-stats-toughness");
            statsSoftnessLabel = divisionEditorPopup.Q<Label>("division-stats-softness");
            statsSpeedLabel = divisionEditorPopup.Q<Label>("division-stats-speed");
            statsCombatWidthLabel = divisionEditorPopup.Q<Label>("division-stats-combatwidth");
            statsSupplyConsumptionLabel = divisionEditorPopup.Q<Label>("division-stats-supply");
            statsFuelConsumptionLabel = divisionEditorPopup.Q<Label>("division-stats-fuel");
            mobileAirDefenseSection = divisionEditorPopup.Q<VisualElement>("division-mobile-air-defense-section");
            mobileAirDefenseSummaryLabel = divisionEditorPopup.Q<Label>("division-mobile-air-defense-summary");
            mobileAirDefenseMissilesLabel = divisionEditorPopup.Q<Label>("division-mobile-air-defense-missiles");
            availableBattalionsListView = divisionEditorPopup.Q<ListView>("division-available-battalion-listview");
            selectedBattalionsListView = divisionEditorPopup.Q<ListView>("division-selected-battalion-listview");
            editorSaveBtn = divisionEditorPopup.Q<Button>("division-editor-save-btn");
            editorCancelBtn = divisionEditorPopup.Q<Button>("division-editor-cancel-btn");

            deleteMessage = divisionDeletePopup.Q<Label>("division-delete-message");
            deleteConfirmBtn = divisionDeletePopup.Q<Button>("division-delete-confirm-btn");
            deleteCancelBtn = divisionDeletePopup.Q<Button>("division-delete-cancel-btn");

            countryDropdown.RegisterValueChangedCallback(evt => OnCountryChanged(evt.newValue));
            createDivisionBtn.clicked += OnCreateDivisionClicked;
            editDivisionBtn.clicked += OnEditDivisionClicked;
            deleteDivisionBtn.clicked += OnDeleteDivisionClicked;
            editorSaveBtn.clicked += OnEditorSaveClicked;
            editorCancelBtn.clicked += OnEditorCancelClicked;
            deleteConfirmBtn.clicked += OnDeleteConfirmClicked;
            deleteCancelBtn.clicked += OnDeleteCancelClicked;

            compositionTabBtn.clicked += () => SetActiveEditorTab(isStatsTab: false);
            statsTabBtn.clicked += () => SetActiveEditorTab(isStatsTab: true);

            SetupDivisionTemplatesListView();
            SetupBattalionListViews();

            editDivisionBtn.SetEnabled(false);
            deleteDivisionBtn.SetEnabled(false);

            // Default tab state
            SetActiveEditorTab(isStatsTab: false);
        }

        private void SetActiveEditorTab(bool isStatsTab)
        {
            if (compositionTab == null || statsTab == null) return;

            compositionTab.style.display = isStatsTab ? DisplayStyle.None : DisplayStyle.Flex;
            statsTab.style.display = isStatsTab ? DisplayStyle.Flex : DisplayStyle.None;

            compositionTabBtn?.EnableInClassList("tab-btn-selected", !isStatsTab);
            statsTabBtn?.EnableInClassList("tab-btn-selected", isStatsTab);
        }

        private void SetupDivisionTemplatesListView()
        {
            divisionTemplatesListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.paddingLeft = 10;
                container.style.paddingTop = 5;
                container.style.paddingBottom = 5;

                var nameLabel = new Label();
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var compositionLabel = new Label();
                compositionLabel.style.fontSize = 11;
                compositionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                container.Add(nameLabel);
                container.Add(compositionLabel);

                return container;
            };

            divisionTemplatesListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= _countryDivisionTemplates.Count) return;

                var division = _countryDivisionTemplates[index];
                var nameLabel = element.ElementAt(0) as Label;
                var compositionLabel = element.ElementAt(1) as Label;

                nameLabel.text = division.DivisionName;

                if (division.Composition != null && division.Composition.Count > 0)
                {
                    var resolvedTemplate = DivisionTemplateResolver.Resolve(division, ModuleSingleton.Instance.ModuleData);
                    var battalionTypes = resolvedTemplate.Composition
                        .Select(c => $"{c.Count}x {c.Battalion.BattalionName}");
                    compositionLabel.text =
                        $"{division.TotalBattalionCount} Battalions: {string.Join(", ", battalionTypes)}" +
                        (resolvedTemplate.HasMobileAirDefense ? " | Mobile AD" : string.Empty);
                }
                else
                {
                    compositionLabel.text = "Empty division";
                }
            };

            divisionTemplatesListView.selectionChanged += OnDivisionTemplateSelected;
            divisionTemplatesListView.fixedItemHeight = 50;
        }

        private void SetupBattalionListViews()
        {
            // Available Battalion (with add button and count stepper)
            availableBattalionsListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.paddingTop = 5;
                container.style.paddingBottom = 5;
                container.style.paddingLeft = 5;
                container.style.paddingRight = 5;

                var nameLabel = new Label();
                nameLabel.style.flexGrow = 1;

                var addBtn = new Button { text = "Add" };
                addBtn.AddToClassList("action-btn");
                addBtn.style.minWidth = 60;

                container.Add(nameLabel);
                container.Add(addBtn);

                return container;
            };

            availableBattalionsListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= availableBattalionsIDs.Count) return;

                var battalion = availBattalions[index];
                var nameLabel = element.Q<Label>();
                var addBtn = element.Q<Button>();

                nameLabel.text = FormatBattalionName(battalion);

                // Clear old callbacks
                addBtn.clickable = new Clickable(() => { });
                addBtn.clicked += () => OnAddBattalionClicked(battalion);

                // Disable if already added
                addBtn.SetEnabled(!tempBattalionComposition.ContainsKey(battalion.ID));
            };

            availableBattalionsListView.fixedItemHeight = 30;

            // Selected Battalion list (with count +/-)
            selectedBattalionsListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.paddingTop = 5;
                container.style.paddingBottom = 5;
                container.style.paddingLeft = 5;
                container.style.paddingRight = 5;

                var nameLabel = new Label();
                nameLabel.style.flexGrow = 1;

                var countContainer = new VisualElement();
                countContainer.style.flexDirection = FlexDirection.Row;
                countContainer.style.alignItems = Align.Center;
                countContainer.style.marginRight = 10;

                var minusBtn = new Button { text = "-" };
                minusBtn.AddToClassList("action-btn");
                minusBtn.style.minWidth = 30;

                var countLabel = new Label { text = "1" };
                countLabel.style.minWidth = 30;
                countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                var plusBtn = new Button { text = "+" };
                plusBtn.AddToClassList("action-btn");
                plusBtn.style.minWidth = 30;

                countContainer.Add(minusBtn);
                countContainer.Add(countLabel);
                countContainer.Add(plusBtn);

                var removeBtn = new Button { text = "Remove" };
                removeBtn.AddToClassList("action-btn");
                removeBtn.style.minWidth = 70;

                container.Add(nameLabel);
                container.Add(countContainer);
                container.Add(removeBtn);

                return container;
            };

            selectedBattalionsListView.bindItem = (element, index) =>
            {
                var selectedBattalions = tempBattalionComposition.Keys
                    .Select(id => availBattalions.FirstOrDefault(b => b.ID == id))
                    .Where(b => b != null)
                    .ToList();

                if (index < 0 || index >= selectedBattalions.Count) return;

                var battalion = selectedBattalions[index];
                var nameLabel = element.Q<Label>();
                var buttons = element.Query<Button>().ToList();
                var countLabel = element.Query<Label>().ToList()[1]; // Second label is the count

                nameLabel.text = FormatBattalionName(battalion);
                countLabel.text = tempBattalionComposition[battalion.ID].ToString();

                // Clear old callbacks
                foreach (var btn in buttons)
                {
                    btn.clickable = new Clickable(() => { });
                }

                // Minus button
                buttons[0].clicked += () => OnDecrementBattalionCount(battalion);
                // Plus button
                buttons[1].clicked += () => OnIncrementBattalionCount(battalion);
                // Remove button
                buttons[2].clicked += () => OnRemoveBattalionClicked(battalion);
            };

            selectedBattalionsListView.fixedItemHeight = 35;
        }

        private void RefreshCountryDropdown()
        {
            var campaign = Editor.editingCampaign;
            var campaignCountries = campaign?.CampaignCountries;

            if (campaignCountries == null || campaignCountries.Count == 0)
            {
                countryDropdown.choices = new List<string> { "No countries available" };
                countryDropdown.index = 0;
                countryDropdown.SetEnabled(false);
                _selectedCountry = null;
                createDivisionBtn.SetEnabled(false);
                RefreshDivisionsList();
                return;
            }

            countryDropdown.SetEnabled(true);
            createDivisionBtn.SetEnabled(true);
            countryDropdown.choices = campaignCountries
                .Select(c => c.CountryName)
                .ToList();

            if (countryDropdown.choices.Count > 0)
            {
                countryDropdown.index = 0;
                _selectedCountry = campaignCountries[0];
                RefreshDivisionsList();
            }
        }

        private void OnCountryChanged(string countryName)
        {
            _selectedCountry = Editor.editingCampaign?.CampaignCountries
                ?.FirstOrDefault(c => c.CountryName == countryName);

            RefreshDivisionsList();
            editDivisionBtn.SetEnabled(false);
            deleteDivisionBtn.SetEnabled(false);
        }

        private void RefreshDivisionsList()
        {
            _countryDivisionTemplates.Clear();

            var campaign = Editor.editingCampaign;
            if (campaign == null || _selectedCountry == null)
            {
                divisionTemplatesListView.itemsSource = _countryDivisionTemplates;
                divisionTemplatesListView.Rebuild();
                return;
            }

            if (campaign.divisionTemplates == null)
                campaign.divisionTemplates = new List<DivisionTemplate>();

            _countryDivisionTemplates.AddRange(
                campaign.divisionTemplates.Where(d => d != null && d.CountryID == _selectedCountry.ID));

            divisionTemplatesListView.itemsSource = _countryDivisionTemplates;
            divisionTemplatesListView.Rebuild();
        }

        private void OnDivisionTemplateSelected(IEnumerable<object> selectedItems)
        {
            var selected = selectedItems.FirstOrDefault() as DivisionTemplate;
            editDivisionBtn.SetEnabled(selected != null);
            deleteDivisionBtn.SetEnabled(selected != null);
        }

        private void OnCreateDivisionClicked()
        {
            if (_selectedCountry == null) return;

            isEditMode = false;
            editingDivision = new DivisionTemplate("New Division");
            editingDivision.Composition = new List<DivisionTemplate.BattalionComposition>();
            editingDivision.CountryID = _selectedCountry.ID;

            OpenEditorPopup();
        }

        private void OnEditDivisionClicked()
        {
            var selected = divisionTemplatesListView.selectedItem as DivisionTemplate;
            if (selected == null) return;

            isEditMode = true;
            editingDivision = selected;

            OpenEditorPopup();
        }

        private void OnDeleteDivisionClicked()
        {
            var selected = divisionTemplatesListView.selectedItem as DivisionTemplate;
            if (selected == null) return;

            editingDivision = selected;
            deleteMessage.text = $"Are you sure you want to delete '{editingDivision.DivisionName}'?";
            ShowPopup(divisionDeletePopup);
        }

        private void OpenEditorPopup()
        {
            editorTitle.text = isEditMode ? "Edit Division Template" : "Create Division Template";

            divisionNameField.value = editingDivision.DivisionName;

            // Set up available Battalions from country
            availableBattalionsIDs = _selectedCountry?.AllowedBattalions ?? new List<Guid>();
            availBattalions = ModuleSingleton.Instance.ModuleData.ModuleBattalions
                .Where(p => availableBattalionsIDs.Contains(p.ID)).ToList();

            // Load existing composition
            tempBattalionComposition.Clear();
            if (editingDivision.Composition != null)
            {
                foreach (var comp in editingDivision.Composition)
                {
                    tempBattalionComposition[comp.BattalionID] = comp.count;
                }
            }

            RefreshBattalionLists();
            UpdateStatsUI();
            SetActiveEditorTab(isStatsTab: false);
            ShowPopup(divisionEditorPopup);
        }

        private void RefreshBattalionLists()
        {
            availableBattalionsListView.itemsSource = availBattalions;
            availableBattalionsListView.Rebuild();

            var selectedBattalions = tempBattalionComposition.Keys
                .Select(id => availBattalions.FirstOrDefault(b => b.ID == id))
                .Where(b => b != null)
                .ToList();

            selectedBattalionsListView.itemsSource = selectedBattalions;
            selectedBattalionsListView.Rebuild();

            UpdateStatsUI();
        }

        private void OnAddBattalionClicked(BattalionData battalion)
        {
            if (!tempBattalionComposition.ContainsKey(battalion.ID))
            {
                tempBattalionComposition[battalion.ID] = 1;
                RefreshBattalionLists();
            }
        }

        private void OnIncrementBattalionCount(BattalionData battalion)
        {
            if (tempBattalionComposition.ContainsKey(battalion.ID))
            {
                tempBattalionComposition[battalion.ID]++;
                selectedBattalionsListView.Rebuild();
                UpdateStatsUI();
            }
        }

        private void OnDecrementBattalionCount(BattalionData battalion)
        {
            if (tempBattalionComposition.ContainsKey(battalion.ID))
            {
                tempBattalionComposition[battalion.ID]--;
                if (tempBattalionComposition[battalion.ID] <= 0)
                {
                    tempBattalionComposition.Remove(battalion.ID);
                    RefreshBattalionLists();
                }
                else
                {
                    selectedBattalionsListView.Rebuild();
                    UpdateStatsUI();
                }
            }
        }

        private void OnRemoveBattalionClicked(BattalionData battalion)
        {
            tempBattalionComposition.Remove(battalion.ID);
            RefreshBattalionLists();
        }

        private void UpdateStatsUI()
        {
            // It is safe for these to be null if the UXML hasn't been updated yet.
            if (statsStrengthLabel == null) return;

            if (tempBattalionComposition.Any())
            {
                var previewTemplate = new DivisionTemplate
                {
                    ID = editingDivision.ID,
                    CountryID = editingDivision.CountryID,
                    DivisionName = editingDivision.DivisionName,
                    Composition = tempBattalionComposition
                        .Select(kvp => new DivisionTemplate.BattalionComposition(kvp.Key, kvp.Value))
                        .ToList()
                };
                var resolvedTemplate = DivisionTemplateResolver.Resolve(previewTemplate, ModuleSingleton.Instance.ModuleData);
                var stats = resolvedTemplate.Stats;

                statsStrengthLabel.text = stats.Strength.ToString();
                statsOrganizationLabel.text = stats.Organization.ToString();
                statsMoraleLabel.text = stats.Recovery.ToString("0.##");
                statsSoftAttackLabel.text = stats.SoftAttack.ToString("0.##");
                statsHardAttackLabel.text = stats.HardAttack.ToString("0.##");
                statsDefenseLabel.text = stats.Defense.ToString("0.##");
                statsToughnessLabel.text = stats.Toughness.ToString("0.##");
                statsSoftnessLabel.text = stats.Softness.ToString("0.##");
                statsSpeedLabel.text = stats.SpeedKMPERHOUR.ToString("0.##");
                statsCombatWidthLabel.text = stats.CombatWidth.ToString("0.##");
                statsSupplyConsumptionLabel.text = stats.SupplyConsumption.ToString("0.##");
                statsFuelConsumptionLabel.text = stats.FuelConsumption.ToString("0.##");
                UpdateMobileAirDefenseUi(resolvedTemplate.MobileAirDefense);
                return;
            }

            statsStrengthLabel.text = "0";
            statsOrganizationLabel.text = "0";
            statsMoraleLabel.text = "0";
            statsSoftAttackLabel.text = "0";
            statsHardAttackLabel.text = "0";
            statsDefenseLabel.text = "0";
            statsToughnessLabel.text = "0";
            statsSoftnessLabel.text = "0";
            statsSpeedLabel.text = "0";
            statsCombatWidthLabel.text = "0";
            statsSupplyConsumptionLabel.text = "0";
            statsFuelConsumptionLabel.text = "0";
            UpdateMobileAirDefenseUi(DivisionTemplateMobileAirDefenseStats.Empty);
        }

        private void UpdateMobileAirDefenseUi(DivisionTemplateMobileAirDefenseStats mobileAirDefense)
        {
            if (mobileAirDefenseSection == null || mobileAirDefenseSummaryLabel == null || mobileAirDefenseMissilesLabel == null)
                return;

            bool hasCapability = mobileAirDefense != null && mobileAirDefense.HasCapability;
            mobileAirDefenseSection.style.display = hasCapability ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasCapability)
            {
                mobileAirDefenseSummaryLabel.text = string.Empty;
                mobileAirDefenseMissilesLabel.text = string.Empty;
                return;
            }

            mobileAirDefenseSummaryLabel.text =
                AirDefenseEditorFormatting.FormatMobileAirDefenseSummary(mobileAirDefense);
            mobileAirDefenseMissilesLabel.text =
                $"Missiles: {AirDefenseEditorFormatting.FormatGuidQuantityMap(mobileAirDefense.MissileInventoryByWeaponId)}";
        }

        private static string FormatBattalionName(BattalionData battalion)
        {
            if (battalion == null)
                return string.Empty;

            return battalion.HasSelfPropelledSamCapability
                ? $"{battalion.BattalionName} [SP SAM]"
                : battalion.BattalionName;
        }

        private void OnEditorSaveClicked()
        {
            string name = divisionNameField.value?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("Division name cannot be empty");
                return;
            }

            if (tempBattalionComposition.Count == 0)
            {
                Debug.LogWarning("Division must have at least one Battalion");
                return;
            }

            editingDivision.DivisionName = name;
            if (_selectedCountry != null)
                editingDivision.CountryID = _selectedCountry.ID;
            editingDivision.Composition = new List<DivisionTemplate.BattalionComposition>();

            foreach (var kvp in tempBattalionComposition)
            {
                var battalion = availBattalions.FirstOrDefault(b => b.ID == kvp.Key);
                if (battalion != null)
                {
                    editingDivision.Composition.Add(new DivisionTemplate.BattalionComposition(battalion.ID, kvp.Value));
                }
            }

            if (!isEditMode)
            {
                if (Editor.editingCampaign.divisionTemplates == null)
                    Editor.editingCampaign.divisionTemplates = new List<DivisionTemplate>();

                Editor.editingCampaign.divisionTemplates.Add(editingDivision);
            }

            RefreshDivisionsList();
            HidePopup(divisionEditorPopup);
        }

        private void OnEditorCancelClicked()
        {
            HidePopup(divisionEditorPopup);
        }

        private void OnDeleteConfirmClicked()
        {
            Editor.editingCampaign?.divisionTemplates?.Remove(editingDivision);

            if (Editor.editingCampaign?.unitSpawnPoints != null)
                Editor.editingCampaign.unitSpawnPoints.RemoveAll(p => p.TemplateID == editingDivision.ID);

            Editor.divisionManager?.DeleteAllTemplateUnits(editingDivision.ID);

            RefreshDivisionsList();
            HidePopup(divisionDeletePopup);

            editDivisionBtn.SetEnabled(false);
            deleteDivisionBtn.SetEnabled(false);
        }

        private void OnDeleteCancelClicked()
        {
            HidePopup(divisionDeletePopup);
        }

        private void ShowPopup(VisualElement popup)
        {
            popupOverlay.style.display = DisplayStyle.Flex;
            popup.style.display = DisplayStyle.Flex;
        }

        private void HidePopup(VisualElement popup)
        {
            popup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            return false;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
        }

        public override void SetCampaign()
        {
            RefreshCountryDropdown();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshCountryDropdown();
        }
    }
}
