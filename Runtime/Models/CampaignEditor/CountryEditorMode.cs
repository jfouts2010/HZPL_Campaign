using System;
using System.Collections.Generic;
using System.Linq;
using Monobehaviours.Singletons;
using ScriptableObjects.Gameplay.Units;
using Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Manages which countries are available in the campaign (from the module's country pool).
    ///
    /// Refactor note:
    /// Campaigns no longer hold an instantiated "CampaignCountry" wrapper; they hold plain CountryData
    /// and reference other campaign data (division templates, spawns, alliances, etc.) by Country ID.
    /// </summary>
    public class CountryEditorMode : EditorMode
    {
        private ListView availableCountriesListView;
        private ListView selectedCountriesListView;
        private Button addCountryBtn;
        private Button removeCountryBtn;

        // Detail panel for selected country
        private VisualElement countryDetailPanel;
        private Label countryNameLabel;
        private VisualElement countryFlagPreview;
        private ListView countryBattalionsListView;
        private ListView countryDivisionsListView;

        private List<CountryData> AllCountryData =>
            ModuleSingleton.Instance.ModuleData.ModuleCountries?.ToList() ?? new List<CountryData>();

        // IMPORTANT: Keep the same list instance for ListView to avoid stale UI bindings.
        private readonly List<CountryData> availableCountryData = new List<CountryData>();

        private CountryData selectedAvailableCountry;
        private CountryData selectedCampaignCountry;

        // Cached list for division templates shown in the detail panel (stable instance for UI Toolkit).
        private readonly List<DivisionTemplate> selectedCountryDivisionTemplates = new List<DivisionTemplate>();

        private List<CountryData> CampaignCountries => Editor.editingCampaign.CampaignCountries;

        public CountryEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter)
            : base(tab, editor, _highlighter)
        {
            InitializeUI();
            RefreshLists();
        }

        private void InitializeUI()
        {
            availableCountriesListView = _tab.Q<ListView>("available-countries-listview");
            selectedCountriesListView = _tab.Q<ListView>("selected-countries-listview");
            addCountryBtn = _tab.Q<Button>("add-country-btn");
            removeCountryBtn = _tab.Q<Button>("remove-country-btn");

            countryDetailPanel = _tab.Q<VisualElement>("country-detail-panel");
            countryNameLabel = _tab.Q<Label>("country-detail-name");
            countryFlagPreview = _tab.Q<VisualElement>("country-detail-flag");
            countryBattalionsListView = _tab.Q<ListView>("country-detail-battalion-listview");
            countryDivisionsListView = _tab.Q<ListView>("country-detail-divisions-listview");

            addCountryBtn.clicked += OnAddCountryClicked;
            removeCountryBtn.clicked += OnRemoveCountryClicked;

            SetupAvailableCountriesListView();
            SetupSelectedCountriesListView();
            SetupDetailListViews();

            HideDetailPanel();
        }

        private void SetupAvailableCountriesListView()
        {
            // Bind to our stable list instance once.
            availableCountriesListView.itemsSource = availableCountryData;

            availableCountriesListView.makeItem = () =>
            {
                var container = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingTop = 5,
                        paddingBottom = 5,
                        paddingLeft = 10,
                        paddingRight = 10
                    }
                };

                var flag = new VisualElement
                {
                    style =
                    {
                        width = 40,
                        height = 24,
                        marginRight = 10
                    }
                };

                var nameLabel = new Label { style = { flexGrow = 1 } };

                container.Add(flag);
                container.Add(nameLabel);
                return container;
            };

            availableCountriesListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= availableCountryData.Count) return;

                var countryData = availableCountryData[index];
                var flag = element.ElementAt(0);
                var nameLabel = element.Q<Label>();

                nameLabel.text = countryData.CountryName;
                flag.style.backgroundImage = countryData.FlagSprite != null
                    ? new StyleBackground(countryData.FlagSprite)
                    : null;
            };

            availableCountriesListView.selectionChanged += OnAvailableCountrySelected;
            availableCountriesListView.fixedItemHeight = 40;
        }

        private void SetupSelectedCountriesListView()
        {
            selectedCountriesListView.makeItem = () =>
            {
                var container = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingTop = 5,
                        paddingBottom = 5,
                        paddingLeft = 10,
                        paddingRight = 10
                    }
                };

                var flag = new VisualElement
                {
                    style =
                    {
                        width = 40,
                        height = 24,
                        marginRight = 10
                    }
                };

                var infoContainer = new VisualElement { style = { flexGrow = 1 } };

                var nameLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } };

                var statsLabel = new Label
                {
                    style =
                    {
                        fontSize = 11,
                        color = new Color(0.7f, 0.7f, 0.7f)
                    }
                };

                infoContainer.Add(nameLabel);
                infoContainer.Add(statsLabel);

                container.Add(flag);
                container.Add(infoContainer);

                return container;
            };

            selectedCountriesListView.bindItem = (element, index) =>
            {
                var countries = CampaignCountries;
                if (index < 0 || index >= countries.Count) return;

                var countryData = countries[index];
                if (countryData == null) return;

                var flag = element.ElementAt(0);
                var infoContainer = element.ElementAt(1);
                var nameLabel = infoContainer.ElementAt(0) as Label;
                var statsLabel = infoContainer.ElementAt(1) as Label;

                nameLabel.text = countryData.CountryName;

                int battalionCount = countryData.AllowedBattalions?.Count ?? 0;
                int mobileAirDefenseBattalionCount = GetSelfPropelledSamBattalionCount(countryData);
                int divisionCount =
                    Editor?.editingCampaign?.divisionTemplates?.Count(d => d.CountryID == countryData.ID) ?? 0;
                statsLabel.text =
                    $"Battalions: {battalionCount} | SP SAM: {mobileAirDefenseBattalionCount} | Divisions: {divisionCount}";

                flag.style.backgroundImage = countryData.FlagSprite != null
                    ? new StyleBackground(countryData.FlagSprite)
                    : null;
            };

            selectedCountriesListView.selectionChanged += OnCampaignCountrySelected;
            selectedCountriesListView.fixedItemHeight = 50;
        }

        private void SetupDetailListViews()
        {
            // Battalions list
            countryBattalionsListView.makeItem = () =>
            {
                var container = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 10,
                        paddingTop = 5,
                        paddingBottom = 5,
                        paddingRight = 10
                    }
                };

                var nameLabel = new Label { style = { flexGrow = 1 } };
                var capabilityLabel = new Label
                {
                    style =
                    {
                        fontSize = 10,
                        color = new Color(0.6f, 0.85f, 1f)
                    }
                };

                container.Add(nameLabel);
                container.Add(capabilityLabel);
                return container;
            };

            countryBattalionsListView.bindItem = (element, index) =>
            {
                if (selectedCampaignCountry?.AllowedBattalions == null) return;
                var battalions = ModuleSingleton.Instance.ModuleData.ModuleBattalions
                    .Where(p => selectedCampaignCountry.AllowedBattalions.Contains(p.ID)).ToList();
                if (index < 0 || index >= battalions.Count) return;

                var battalion = battalions[index];
                var nameLabel = element.ElementAt(0) as Label;
                var capabilityLabel = element.ElementAt(1) as Label;

                nameLabel.text = battalion.BattalionName;
                capabilityLabel.text = battalion.HasSelfPropelledSamCapability ? "SP SAM" : string.Empty;
            };

            countryBattalionsListView.fixedItemHeight = 32;

            // Divisions list
            countryDivisionsListView.itemsSource = selectedCountryDivisionTemplates;

            countryDivisionsListView.makeItem = () =>
            {
                var container = new VisualElement
                {
                    style =
                    {
                        paddingLeft = 10,
                        paddingTop = 5,
                        paddingBottom = 5
                    }
                };

                var nameLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } };

                var compositionLabel = new Label
                {
                    style =
                    {
                        fontSize = 11,
                        color = new Color(0.7f, 0.7f, 0.7f)
                    }
                };

                container.Add(nameLabel);
                container.Add(compositionLabel);
                return container;
            };

            countryDivisionsListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= selectedCountryDivisionTemplates.Count) return;

                var division = selectedCountryDivisionTemplates[index];
                var nameLabel = element.ElementAt(0) as Label;
                var compositionLabel = element.ElementAt(1) as Label;
                var resolvedTemplate = DivisionTemplateResolver.Resolve(division, ModuleSingleton.Instance.ModuleData);

                nameLabel.text = division.DivisionName;
                int totalBattalions = division.Composition?.Sum(c => c.count) ?? 0;
                compositionLabel.text = $"{totalBattalions} Battalions total" +
                                        (resolvedTemplate.HasMobileAirDefense ? " | Mobile AD" : string.Empty);
            };

            countryDivisionsListView.fixedItemHeight = 45;
        }

        private void RefreshLists()
        {
            RefreshAvailableCountries();
            RefreshSelectedCountries();
        }

        private void RefreshAvailableCountries()
        {
            availableCountryData.Clear();

            var all = AllCountryData;
            if (all.Count == 0)
            {
                availableCountriesListView.Rebuild();
                return;
            }

            var selectedIds = new HashSet<Guid>(CampaignCountries.Select(c => c.ID));

            // Filter out anything already in campaign.
            var remaining = all
                .Where(cd => cd != null && !selectedIds.Contains(cd.ID))
                .OrderBy(cd => cd.CountryName);

            availableCountryData.AddRange(remaining);

            // Because we keep the same list instance, just rebuild.
            availableCountriesListView.Rebuild();

            availableCountriesListView.ClearSelection();
            selectedAvailableCountry = null;
            addCountryBtn.SetEnabled(false);
        }

        private void RefreshSelectedCountries()
        {
            if (Editor?.editingCampaign == null) return;

            selectedCountriesListView.itemsSource = CampaignCountries;
            selectedCountriesListView.Rebuild();

            selectedCountriesListView.ClearSelection();
            selectedCampaignCountry = null;
            removeCountryBtn.SetEnabled(false);
        }

        private void OnAvailableCountrySelected(IEnumerable<object> selectedItems)
        {
            selectedAvailableCountry = selectedItems.FirstOrDefault() as CountryData;
            addCountryBtn.SetEnabled(selectedAvailableCountry != null);
            HideDetailPanel();
        }

        private void OnCampaignCountrySelected(IEnumerable<object> selectedItems)
        {
            selectedCampaignCountry = selectedItems.FirstOrDefault() as CountryData;
            removeCountryBtn.SetEnabled(selectedCampaignCountry != null);

            if (selectedCampaignCountry != null)
                ShowDetailPanel();
            else
                HideDetailPanel();
        }

        private void OnAddCountryClicked()
        {
            if (selectedAvailableCountry == null) return;
            if (Editor?.editingCampaign == null) return;

            // Prevent duplicates
            if (CampaignCountries.Any(c => c.ID == selectedAvailableCountry.ID))
                return;

            Editor.editingCampaign.Countries.Add(selectedAvailableCountry.ID);

            // Ensure alliance map has an entry for the new country.
            if (Editor.editingCampaign.CountryAlliance != null &&
                !Editor.editingCampaign.CountryAlliance.ContainsKey(selectedAvailableCountry.ID))
            {
                Editor.editingCampaign.CountryAlliance[selectedAvailableCountry.ID] = Alliance.Neutral;
            }

            RefreshLists();
            selectedAvailableCountry = null;
            addCountryBtn.SetEnabled(false);
        }

        private void OnRemoveCountryClicked()
        {
            if (selectedCampaignCountry == null) return;
            if (Editor?.editingCampaign == null) return;

            var countryId = selectedCampaignCountry.ID;

            // Remove units/spawns/airwings associated with this country.
            Editor.divisionManager.DeleteAllCountryUnits(countryId);

            if (Editor.editingCampaign.unitSpawnPoints != null)
            {
                foreach (var spawn in Editor.editingCampaign.unitSpawnPoints.Where(p => p.CountryID == countryId)
                             .ToList())
                    Editor.editingCampaign.unitSpawnPoints.Remove(spawn);
            }

            if (Editor.editingCampaign.airWingSpawns != null)
                Editor.editingCampaign.airWingSpawns.RemoveAll(p => p.CountryId == countryId);

            // Remove division templates that belong to this country.
            if (Editor.editingCampaign.divisionTemplates != null)
                Editor.editingCampaign.divisionTemplates.RemoveAll(d => d.CountryID == countryId);

            // Remove alliance mapping entry, if present.
            if (Editor.editingCampaign.CountryAlliance != null)
                Editor.editingCampaign.CountryAlliance.Remove(countryId);

            CampaignCountries.RemoveAll(c => c.ID == countryId);

            RefreshLists();
            selectedCampaignCountry = null;
            removeCountryBtn.SetEnabled(false);

            Editor.tilemapManager.RefreshTilemaps();
            HideDetailPanel();
        }

        private void ShowDetailPanel()
        {
            if (selectedCampaignCountry == null)
            {
                HideDetailPanel();
                return;
            }

            countryDetailPanel.style.display = DisplayStyle.Flex;
            countryNameLabel.text = selectedCampaignCountry.CountryName;

            countryFlagPreview.style.backgroundImage = selectedCampaignCountry.FlagSprite != null
                ? new StyleBackground(selectedCampaignCountry.FlagSprite)
                : null;

            // Battalions
            countryBattalionsListView.itemsSource = selectedCampaignCountry.AllowedBattalions;
            countryBattalionsListView.Rebuild();

            // Divisions (filter from campaign division templates by CountryID)
            selectedCountryDivisionTemplates.Clear();
            var allDivisions = Editor?.editingCampaign?.divisionTemplates;
            if (allDivisions != null)
                selectedCountryDivisionTemplates.AddRange(allDivisions
                    .Where(d => d.CountryID == selectedCampaignCountry.ID).OrderBy(d => d.DivisionName));

            countryDivisionsListView.Rebuild();
        }

        private void HideDetailPanel()
        {
            countryDetailPanel.style.display = DisplayStyle.None;
            selectedCountryDivisionTemplates.Clear();
        }

        private int GetSelfPropelledSamBattalionCount(CountryData countryData)
        {
            if (countryData?.AllowedBattalions == null)
                return 0;

            var battalionIds = new HashSet<Guid>(countryData.AllowedBattalions);
            return ModuleSingleton.Instance.ModuleData.ModuleBattalions.Count(
                battalion => battalionIds.Contains(battalion.ID) && battalion.HasSelfPropelledSamCapability);
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
            if (Editor == null) return;
            RefreshLists();
            HideDetailPanel();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshLists();
        }
    }
}
