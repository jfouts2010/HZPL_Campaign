using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using Newtonsoft.Json;
using ScriptableObjects.Gameplay.Units;
using Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Places concrete Division instances on the map from division templates
    /// </summary>
    public class UnitSpawnEditorMode : EditorMode
    {
        // UI uses the existing dropdown in TilemapEditor.uxml; we repurpose it to select an Alliance.
        private DropdownField allianceDropdown;
        private ListView divisionTemplatesListView;
        private Label selectedDivisionLabel;
        private VisualElement divisionPreview;
        private Label divisionDetailsLabel;
        private Label mobileAirDefenseLabel;
        private Label deployedCountLabel;

        private DivisionTemplate selectedTemplate;

        private Alliance _selectedAlliance = Alliance.Neutral;

        private List<DivisionTemplate> availableTemplates = new List<DivisionTemplate>();

        public UnitSpawnEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter) : base(tab,
            editor, _highlighter)
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            // NOTE: UXML still names this "unit-spawn-country-dropdown" - we now use it for Alliance selection.
            allianceDropdown = _tab.Q<DropdownField>("unit-spawn-country-dropdown");
            divisionTemplatesListView = _tab.Q<ListView>("unit-spawn-divisions-listview");
            selectedDivisionLabel = _tab.Q<Label>("unit-spawn-selected-label");
            divisionPreview = _tab.Q<VisualElement>("unit-spawn-division-preview");
            divisionDetailsLabel = _tab.Q<Label>("unit-spawn-division-details");
            mobileAirDefenseLabel = _tab.Q<Label>("unit-spawn-mobile-air-defense");
            deployedCountLabel = _tab.Q<Label>("unit-spawn-deployed-count");

            allianceDropdown.RegisterValueChangedCallback(evt => OnAllianceChanged(evt.newValue));

            SetupDivisionTemplatesListView();

            RefreshAllianceDropdown();
            UpdateSelectedDivisionDisplay();
        }

        private void SetupDivisionTemplatesListView()
        {
            divisionTemplatesListView.makeItem = () =>
            {
                var container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                container.style.paddingTop = 5;
                container.style.paddingBottom = 5;
                container.style.paddingLeft = 5;
                container.style.paddingRight = 5;

                var infoContainer = new VisualElement();
                infoContainer.style.flexGrow = 1;

                var nameLabel = new Label();
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                var compositionLabel = new Label();
                compositionLabel.style.fontSize = 11;
                compositionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                var countryLabel = new Label();
                countryLabel.style.fontSize = 10;
                countryLabel.style.color = new Color(0.7f, 0.7f, 0.9f);

                var deployedLabel = new Label();
                deployedLabel.style.fontSize = 10;
                deployedLabel.style.color = new Color(0.6f, 0.8f, 0.6f);

                infoContainer.Add(nameLabel);
                infoContainer.Add(compositionLabel);
                infoContainer.Add(countryLabel);
                infoContainer.Add(deployedLabel);

                container.Add(infoContainer);

                return container;
            };

            divisionTemplatesListView.bindItem = (element, index) =>
            {
                if (availableTemplates == null || index < 0 || index >= availableTemplates.Count)
                    return;

                var template = availableTemplates[index];
                var infoContainer = element.ElementAt(0);
                var nameLabel = infoContainer.ElementAt(0) as Label;
                var compositionLabel = infoContainer.ElementAt(1) as Label;
                var countryLabel = infoContainer.ElementAt(2) as Label;
                var deployedLabel = infoContainer.ElementAt(3) as Label;

                nameLabel.text = template.DivisionName;

                var country =
                    Editor.editingCampaign?.CampaignCountries?.FirstOrDefault(c => c.ID == template.CountryID);
                countryLabel.text = country != null ? $"Country: {country.CountryName}" : "Country: (missing)";

                if (template.Composition != null && template.Composition.Count > 0)
                {
                    int battalion = template.Composition.Sum(c => c.count);
                    var resolvedTemplate = DivisionTemplateResolver.Resolve(template);
                    compositionLabel.text = $"{battalion} Battalions" +
                                            (resolvedTemplate.HasMobileAirDefense ? " | Mobile AD" : string.Empty);
                }
                else
                {
                    compositionLabel.text = "Empty division";
                }

                // Count deployed divisions using this template (TODO: hook up if/when deployedDivisions exists in campaign editor data)
                deployedLabel.text = "Deployed: 0";
            };

            divisionTemplatesListView.selectionChanged += OnDivisionTemplateSelected;
            divisionTemplatesListView.fixedItemHeight = 60;
        }

        private void RefreshAllianceDropdown()
        {
            var campaign = Editor.editingCampaign;
            if (campaign == null || campaign.Countries == null || campaign.Countries.Count == 0)
            {
                allianceDropdown.choices = new List<string> { "No countries in campaign" };
                allianceDropdown.index = 0;
                allianceDropdown.SetEnabled(false);
                RefreshTemplatesList();
                return;
            }

            // Only show alliances that exist in the campaign mapping.
            var alliances = campaign.CountryAlliance
                .Where(kv => campaign.Countries.Contains(kv.Key))
                .Select(kv => kv.Value)
                .Distinct()
                .OrderBy(a => (int)a)
                .ToList();

            if (alliances.Count == 0)
            {
                // If there is no mapping yet, fall back to Neutral.
                alliances.Add(Alliance.Neutral);
            }

            allianceDropdown.SetEnabled(true);
            allianceDropdown.choices = alliances.Select(a => a.ToString()).ToList();
            allianceDropdown.index = 0;
            _selectedAlliance = alliances[0];

            RefreshTemplatesList();
        }

        private void OnAllianceChanged(string allianceName)
        {
            if (string.IsNullOrWhiteSpace(allianceName)) return;

            if (Enum.TryParse(allianceName, out Alliance parsed))
                _selectedAlliance = parsed;

            selectedTemplate = null;
            RefreshTemplatesList();
            UpdateSelectedDivisionDisplay();
        }

        private void RefreshTemplatesList()
        {
            availableTemplates.Clear();

            var campaign = Editor.editingCampaign;
            if (campaign == null)
            {
                divisionTemplatesListView.itemsSource = availableTemplates;
                divisionTemplatesListView.Rebuild();
                return;
            }

            // Find all countries in the selected alliance.
            var allianceCountries = campaign.GetAllianceData(_selectedAlliance);
            var allianceCountryIds = new HashSet<Guid>(allianceCountries.Select(c => c.ID));

            // Templates are stored globally on campaign and are keyed by CountryID.
            availableTemplates = (campaign.divisionTemplates ?? new List<DivisionTemplate>())
                .Where(t => t != null && allianceCountryIds.Contains(t.CountryID))
                .OrderBy(t => allianceCountries.FirstOrDefault(c => c.ID == t.CountryID)?.CountryName ?? string.Empty)
                .ThenBy(t => t.DivisionName)
                .ToList();

            divisionTemplatesListView.itemsSource = availableTemplates;
            divisionTemplatesListView.Rebuild();
        }

        private void OnDivisionTemplateSelected(IEnumerable<object> selectedItems)
        {
            var selectedList = selectedItems.ToList();
            if (selectedList.Count > 0 && selectedList[0] is DivisionTemplate template)
            {
                selectedTemplate = template;
                UpdateSelectedDivisionDisplay();
            }
        }

        private void UpdateSelectedDivisionDisplay()
        {
            if (selectedTemplate != null)
            {
                selectedDivisionLabel.text = $"Selected: {selectedTemplate.DivisionName}";
                selectedDivisionLabel.style.color = new Color(0.6f, 0.9f, 0.6f);
                var resolvedTemplate = DivisionTemplateResolver.Resolve(selectedTemplate);

                if (selectedTemplate.Composition != null && selectedTemplate.Composition.Count > 0)
                {
                    var battalion = resolvedTemplate.Composition
                        .Select(c => $"{c.Count}x {c.Battalion.BattalionName}")
                        .ToList();

                    int totalBattalion = selectedTemplate.TotalBattalionCount;
                    divisionDetailsLabel.text =
                        $"Composition ({totalBattalion} total):\n{string.Join("\n", battalion)}";
                }
                else
                {
                    divisionDetailsLabel.text = "No Battalions assigned";
                }

                if (resolvedTemplate.HasMobileAirDefense)
                {
                    mobileAirDefenseLabel.style.display = DisplayStyle.Flex;
                    mobileAirDefenseLabel.text =
                        "Mobile Air Defense:\n" +
                        AirDefenseEditorFormatting.FormatMobileAirDefenseSummary(resolvedTemplate.MobileAirDefense) +
                        "\nMissiles: " +
                        AirDefenseEditorFormatting.FormatGuidQuantityMap(
                            resolvedTemplate.MobileAirDefense.MissileInventoryByWeaponId);
                }
                else
                {
                    mobileAirDefenseLabel.style.display = DisplayStyle.None;
                    mobileAirDefenseLabel.text = string.Empty;
                }

                int count = Editor.editingCampaign.unitSpawnPoints
                    .Count(p => p.TemplateID == selectedTemplate.ID);
                deployedCountLabel.text = "Currently deployed: " + count;

                if (resolvedTemplate.DivisionIcon != null)
                {
                    divisionPreview.style.backgroundImage = new StyleBackground(resolvedTemplate.DivisionIcon);
                }
                else
                {
                    divisionPreview.style.backgroundImage = null;
                }
            }
            else
            {
                selectedDivisionLabel.text = "No division selected";
                selectedDivisionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                divisionDetailsLabel.text = "";
                mobileAirDefenseLabel.style.display = DisplayStyle.None;
                mobileAirDefenseLabel.text = "";
                deployedCountLabel.text = "";
                divisionPreview.style.backgroundImage = null;
            }
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!base.PaintTile(cellPos, lastPaintedCell)) return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;

            if (selectedTemplate == null)
            {
                Debug.LogWarning("Please select an alliance and division template before spawning.");
                return false;
            }

            if (Editor.editingCampaign.tileData[cellPos].controllingAlliance != _selectedAlliance)
            {
                Debug.LogWarning("Can Only spawn Divisions on tiles you control");
                return false;
            }

            SpawnDivision(cellPos);

            return true;
        }

        private void SpawnDivision(Vector3Int cellPos)
        {
            if (Editor.divisionManager != null)
            {
                var campaign = Editor.editingCampaign;
                var country = campaign?.CampaignCountries?.FirstOrDefault(c => c.ID == selectedTemplate.CountryID);
                if (country == null)
                {
                    Debug.LogWarning(
                        $"Cannot spawn division: CountryData not found for template country id {selectedTemplate.CountryID}");
                    return;
                }

                campaign.unitSpawnPoints.Add(new UnitSpawn(selectedTemplate.ID, cellPos, selectedTemplate.CountryID,
                    true));

                Editor.divisionManager.SpawnDivision(selectedTemplate, _selectedAlliance, cellPos);
            }

            divisionTemplatesListView.Rebuild();
            UpdateSelectedDivisionDisplay();
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return;

            if (Editor.divisionManager != null)
            {
                Editor.editingCampaign.unitSpawnPoints.RemoveAll(p => p.Position == cellPos);
                Editor.divisionManager.DeleteAllUnitsOnCell(cellPos);
            }
        }

        public override void SetCampaign()
        {
            RefreshAllianceDropdown();
            selectedTemplate = null;
            UpdateSelectedDivisionDisplay();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshAllianceDropdown();
        }
    }
}
