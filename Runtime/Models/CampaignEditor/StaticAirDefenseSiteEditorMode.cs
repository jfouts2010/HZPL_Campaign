using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using Monobehaviours.Singletons;
using Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class StaticAirDefenseSiteEditorMode : EditorMode
    {
        private readonly TilemapEditor _editor;

        private ListView sitesListView;
        private Button createBtn;
        private Button editBtn;
        private Button deleteBtn;
        private Label selectedSiteLabel;
        private Label selectedSiteSummaryLabel;
        private Label placementHintLabel;

        private VisualElement popupOverlay;
        private VisualElement editorPopup;
        private VisualElement deletePopup;
        private Label editorTitle;
        private TextField siteNameField;
        private DropdownField ownerAllianceDropdown;
        private Toggle keyNodeToggle;
        private Label tileLabel;
        private ListView availableComponentsListView;
        private ListView selectedComponentsListView;
        private Label resolvedSummaryLabel;
        private Label resolvedMissilesLabel;
        private Label resolvedDiagnosticsLabel;
        private Button saveBtn;
        private Button cancelBtn;

        private Label deleteMessage;
        private Button deleteConfirmBtn;
        private Button deleteCancelBtn;

        private readonly List<StaticAirDefenseSiteDefinition> visibleSites = new List<StaticAirDefenseSiteDefinition>();
        private readonly List<StaticAirDefenseSiteComponentData> availableComponents =
            new List<StaticAirDefenseSiteComponentData>();
        private readonly List<StaticAirDefenseSiteComponentData> selectedComponents =
            new List<StaticAirDefenseSiteComponentData>();
        private readonly Dictionary<Guid, int> tempComponentComposition = new Dictionary<Guid, int>();

        private StaticAirDefenseSiteDefinition selectedSite;
        private bool isEditingExisting;

        private Campaign campaign => _editor.editingCampaign;

        public StaticAirDefenseSiteEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter)
            : base(tab, editor, highlighter)
        {
            _editor = editor;
            WireUi();
        }

        private void WireUi()
        {
            sitesListView = _tab.Q<ListView>("air-defense-sites-listview");
            createBtn = _tab.Q<Button>("air-defense-create-btn");
            editBtn = _tab.Q<Button>("air-defense-edit-btn");
            deleteBtn = _tab.Q<Button>("air-defense-delete-btn");
            selectedSiteLabel = _tab.Q<Label>("air-defense-selected-label");
            selectedSiteSummaryLabel = _tab.Q<Label>("air-defense-selected-summary");
            placementHintLabel = _tab.Q<Label>("air-defense-placement-hint");

            var root = _tab.panel.visualTree;
            popupOverlay = root.Q<VisualElement>("popup-overlay");
            editorPopup = root.Q<VisualElement>("air-defense-editor-popup");
            deletePopup = root.Q<VisualElement>("air-defense-delete-popup");
            editorTitle = root.Q<Label>("air-defense-editor-title");
            siteNameField = root.Q<TextField>("air-defense-name-field");
            ownerAllianceDropdown = root.Q<DropdownField>("air-defense-owner-dropdown");
            keyNodeToggle = root.Q<Toggle>("air-defense-key-node-toggle");
            tileLabel = root.Q<Label>("air-defense-tile-label");
            availableComponentsListView = root.Q<ListView>("air-defense-available-components-listview");
            selectedComponentsListView = root.Q<ListView>("air-defense-selected-components-listview");
            resolvedSummaryLabel = root.Q<Label>("air-defense-resolved-summary-label");
            resolvedMissilesLabel = root.Q<Label>("air-defense-resolved-missiles-label");
            resolvedDiagnosticsLabel = root.Q<Label>("air-defense-resolved-diagnostics-label");
            saveBtn = root.Q<Button>("air-defense-save-btn");
            cancelBtn = root.Q<Button>("air-defense-cancel-btn");
            deleteMessage = root.Q<Label>("air-defense-delete-message");
            deleteConfirmBtn = root.Q<Button>("air-defense-delete-confirm-btn");
            deleteCancelBtn = root.Q<Button>("air-defense-delete-cancel-btn");

            ownerAllianceDropdown.choices = Enum.GetNames(typeof(Alliance)).ToList();

            SetupSiteListView();
            SetupComponentListViews();

            createBtn.clicked += OnCreateClicked;
            editBtn.clicked += OnEditClicked;
            deleteBtn.clicked += OnDeleteClicked;
            saveBtn.clicked += OnSaveClicked;
            cancelBtn.clicked += CloseEditorPopup;
            deleteConfirmBtn.clicked += OnDeleteConfirmClicked;
            deleteCancelBtn.clicked += CloseDeletePopup;

            siteNameField.RegisterValueChangedCallback(_ => UpdateResolvedSummary());
            ownerAllianceDropdown.RegisterValueChangedCallback(_ => UpdateResolvedSummary());
            keyNodeToggle.RegisterValueChangedCallback(_ => UpdateResolvedSummary());

            RefreshSelectedSiteDisplay();
            UpdateButtonsEnabled();
        }

        public override void SetCampaign()
        {
            campaign?.EnsureAirDataInitialized();
            RefreshSitesList();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            campaign?.EnsureAirDataInitialized();
            RefreshSitesList();
            RefreshSelectedSiteDisplay();
            HighlightSelectedSite();
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

            var clickedSite = FindSiteAtCell(cellPos);
            if (clickedSite != null && (selectedSite == null || clickedSite.Id != selectedSite.Id))
            {
                selectedSite = clickedSite;
                RefreshSitesList();
                return true;
            }

            if (selectedSite == null)
                return clickedSite != null;

            if (lastPaintedCell.HasValue && lastPaintedCell.Value == cellPos)
                return false;

            if (clickedSite != null && clickedSite.Id != selectedSite.Id)
                return false;

            selectedSite.Tile = cellPos;
            RefreshSitesList();
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (selectedSite == null)
                return;

            if (selectedSite.Tile != cellPos)
                return;

            selectedSite = null;
            RefreshSitesList();
        }

        private void SetupSiteListView()
        {
            sitesListView.makeItem = () =>
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

            sitesListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= visibleSites.Count)
                    return;

                var site = visibleSites[index];
                var nameLabel = element.ElementAt(0) as Label;
                var infoLabel = element.ElementAt(1) as Label;

                nameLabel.text = site.Name;
                infoLabel.text =
                    $"{site.OwnerAlliance} @ {FormatTile(site.Tile)} | {site.TotalComponentCount} components";
            };

            sitesListView.selectionChanged += OnSiteSelectionChanged;
            sitesListView.fixedItemHeight = 42;
        }

        private void SetupComponentListViews()
        {
            availableComponentsListView.makeItem = () =>
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

                var detailLabel = new Label();
                detailLabel.style.fontSize = 10;
                detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                infoContainer.Add(nameLabel);
                infoContainer.Add(detailLabel);

                var addBtn = new Button { text = "Add" };
                addBtn.AddToClassList("action-btn");
                addBtn.style.minWidth = 60;

                container.Add(infoContainer);
                container.Add(addBtn);
                return container;
            };

            availableComponentsListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= availableComponents.Count)
                    return;

                var component = availableComponents[index];
                var infoContainer = element.ElementAt(0);
                var nameLabel = infoContainer.ElementAt(0) as Label;
                var detailLabel = infoContainer.ElementAt(1) as Label;
                var addBtn = element.Q<Button>();

                nameLabel.text = component.ComponentName;
                detailLabel.text =
                    $"{component.ComponentType} | {AirDefenseEditorFormatting.FormatNetworkRoles(component.NetworkRole)}";

                addBtn.clickable = new Clickable(() => { });
                addBtn.clicked += () => OnAddComponentClicked(component);
                addBtn.SetEnabled(!tempComponentComposition.ContainsKey(component.ID));
            };

            availableComponentsListView.fixedItemHeight = 42;

            selectedComponentsListView.makeItem = () =>
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

                var detailLabel = new Label();
                detailLabel.style.fontSize = 10;
                detailLabel.style.color = new Color(0.7f, 0.7f, 0.7f);

                infoContainer.Add(nameLabel);
                infoContainer.Add(detailLabel);

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

                container.Add(infoContainer);
                container.Add(countContainer);
                container.Add(removeBtn);
                return container;
            };

            selectedComponentsListView.bindItem = (element, index) =>
            {
                if (index < 0 || index >= selectedComponents.Count)
                    return;

                var component = selectedComponents[index];
                var infoContainer = element.ElementAt(0);
                var nameLabel = infoContainer.ElementAt(0) as Label;
                var detailLabel = infoContainer.ElementAt(1) as Label;
                var buttons = element.Query<Button>().ToList();
                var labels = element.Query<Label>().ToList();
                var countLabel = labels.Count > 2 ? labels[2] : null;

                nameLabel.text = component.ComponentName;
                detailLabel.text =
                    $"{component.ComponentType} | {AirDefenseEditorFormatting.FormatNetworkRoles(component.NetworkRole)}";

                if (countLabel != null && tempComponentComposition.TryGetValue(component.ID, out var count))
                    countLabel.text = count.ToString();

                foreach (var button in buttons)
                    button.clickable = new Clickable(() => { });

                buttons[0].clicked += () => OnDecrementComponentCount(component);
                buttons[1].clicked += () => OnIncrementComponentCount(component);
                buttons[2].clicked += () => OnRemoveComponentClicked(component);
            };

            selectedComponentsListView.fixedItemHeight = 48;
        }

        private void OnSiteSelectionChanged(IEnumerable<object> selection)
        {
            selectedSite = selection?.FirstOrDefault() as StaticAirDefenseSiteDefinition;
            RefreshSelectedSiteDisplay();
            HighlightSelectedSite();
            UpdateButtonsEnabled();
        }

        private void RefreshSitesList()
        {
            visibleSites.Clear();

            if (campaign != null)
            {
                campaign.EnsureAirDataInitialized();
                visibleSites.AddRange((campaign.StaticAirDefenseSites ?? new List<StaticAirDefenseSiteDefinition>())
                    .Where(site => site != null)
                    .OrderBy(site => site.OwnerAlliance)
                    .ThenBy(site => site.Name));
            }

            sitesListView.itemsSource = visibleSites;
            sitesListView.Rebuild();

            if (selectedSite != null)
            {
                var selectedIndex = visibleSites.FindIndex(site => site.Id == selectedSite.Id);
                if (selectedIndex >= 0)
                {
                    sitesListView.SetSelection(selectedIndex);
                    selectedSite = visibleSites[selectedIndex];
                }
                else
                {
                    selectedSite = null;
                    sitesListView.ClearSelection();
                }
            }
            else
            {
                sitesListView.ClearSelection();
            }

            RefreshSelectedSiteDisplay();
            HighlightSelectedSite();
            UpdateButtonsEnabled();
        }

        private void RefreshSelectedSiteDisplay()
        {
            if (selectedSite == null)
            {
                selectedSiteLabel.text = "No site selected.";
                selectedSiteSummaryLabel.text = string.Empty;
                placementHintLabel.text =
                    "Select a site, then click on the map to move it. Clicking a tile that already contains a site selects that site.";
                return;
            }

            var resolved = StaticAirDefenseSiteResolver.Resolve(selectedSite, ModuleSingleton.Instance.ModuleData);
            selectedSiteLabel.text =
                $"Selected: {selectedSite.Name} ({selectedSite.OwnerAlliance}){(selectedSite.IsKeyIadsNode ? " | Key Node" : string.Empty)}";
            selectedSiteSummaryLabel.text =
                $"Tile: {FormatTile(selectedSite.Tile)} | Components: {selectedSite.TotalComponentCount}\n" +
                AirDefenseEditorFormatting.FormatResolvedStaticSiteSummary(resolved);
            placementHintLabel.text =
                "Click an empty tile to move the selected site. Right click the selected tile to clear the selection.";
        }

        private void HighlightSelectedSite()
        {
            if (selectedSite == null)
            {
                highlighter?.ClearHighlight();
                return;
            }

            highlighter?.HighlightTile(selectedSite.Tile);
        }

        private void UpdateButtonsEnabled()
        {
            var hasCampaign = campaign != null;
            createBtn.SetEnabled(hasCampaign);
            editBtn.SetEnabled(hasCampaign && selectedSite != null);
            deleteBtn.SetEnabled(hasCampaign && selectedSite != null);
        }

        private void OnCreateClicked()
        {
            if (campaign == null)
                return;

            campaign.EnsureAirDataInitialized();
            isEditingExisting = false;

            var initialTile = GetAvailablePlacementCell(Guid.NewGuid(), _editor.lastPaintedCell);
            var site = new StaticAirDefenseSiteDefinition
            {
                Tile = initialTile,
                OwnerAlliance = GetAllianceForCell(initialTile),
                Name = "New Static Site",
                IsKeyIadsNode = false
            };

            OpenEditorPopup(CloneSite(site));
        }

        private void OnEditClicked()
        {
            if (selectedSite == null)
                return;

            isEditingExisting = true;
            OpenEditorPopup(CloneSite(selectedSite));
        }

        private void OnDeleteClicked()
        {
            if (selectedSite == null)
                return;

            deleteMessage.text = $"Delete site '{selectedSite.Name}'? This cannot be undone.";
            popupOverlay.style.display = DisplayStyle.Flex;
            deletePopup.style.display = DisplayStyle.Flex;
        }

        private void OnDeleteConfirmClicked()
        {
            if (campaign == null || selectedSite == null)
                return;

            campaign.EnsureAirDataInitialized();
            campaign.StaticAirDefenseSites.RemoveAll(site => site.Id == selectedSite.Id);
            selectedSite = null;

            CloseDeletePopup();
            RefreshSitesList();
        }

        private void CloseDeletePopup()
        {
            deletePopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
        }

        private void OpenEditorPopup(StaticAirDefenseSiteDefinition site)
        {
            popupOverlay.style.display = DisplayStyle.Flex;
            editorPopup.style.display = DisplayStyle.Flex;
            editorPopup.userData = site;

            editorTitle.text = isEditingExisting ? "Edit Static Air Defense Site" : "Create Static Air Defense Site";
            siteNameField.value = site.Name ?? string.Empty;
            ownerAllianceDropdown.value = site.OwnerAlliance.ToString();
            keyNodeToggle.value = site.IsKeyIadsNode;
            tileLabel.text = FormatTile(site.Tile);

            tempComponentComposition.Clear();
            foreach (var component in site.Components ?? Enumerable.Empty<StaticAirDefenseSiteDefinition.ComponentComposition>())
            {
                if (component == null || component.Count <= 0)
                    continue;

                tempComponentComposition[component.ComponentId] = component.Count;
            }

            RefreshComponentLists();
            UpdateResolvedSummary();
        }

        private void CloseEditorPopup()
        {
            editorPopup.style.display = DisplayStyle.None;
            popupOverlay.style.display = DisplayStyle.None;
            editorPopup.userData = null;
            tempComponentComposition.Clear();
        }

        private void RefreshComponentLists()
        {
            availableComponents.Clear();
            selectedComponents.Clear();

            availableComponents.AddRange((ModuleSingleton.Instance.ModuleData?.ModuleAirDefenseSiteComponents ??
                                          new List<StaticAirDefenseSiteComponentData>())
                .Where(component => component != null)
                .OrderBy(component => component.ComponentName));

            var byId = availableComponents.ToDictionary(component => component.ID, component => component);
            selectedComponents.AddRange(tempComponentComposition.Keys
                .Where(byId.ContainsKey)
                .Select(id => byId[id])
                .OrderBy(component => component.ComponentName));

            availableComponentsListView.itemsSource = availableComponents;
            selectedComponentsListView.itemsSource = selectedComponents;
            availableComponentsListView.Rebuild();
            selectedComponentsListView.Rebuild();

            UpdateResolvedSummary();
        }

        private void OnAddComponentClicked(StaticAirDefenseSiteComponentData component)
        {
            if (component == null || tempComponentComposition.ContainsKey(component.ID))
                return;

            tempComponentComposition[component.ID] = 1;
            RefreshComponentLists();
        }

        private void OnIncrementComponentCount(StaticAirDefenseSiteComponentData component)
        {
            if (component == null || !tempComponentComposition.ContainsKey(component.ID))
                return;

            tempComponentComposition[component.ID]++;
            selectedComponentsListView.Rebuild();
            availableComponentsListView.Rebuild();
            UpdateResolvedSummary();
        }

        private void OnDecrementComponentCount(StaticAirDefenseSiteComponentData component)
        {
            if (component == null || !tempComponentComposition.ContainsKey(component.ID))
                return;

            tempComponentComposition[component.ID]--;
            if (tempComponentComposition[component.ID] <= 0)
            {
                tempComponentComposition.Remove(component.ID);
                RefreshComponentLists();
                return;
            }

            selectedComponentsListView.Rebuild();
            UpdateResolvedSummary();
        }

        private void OnRemoveComponentClicked(StaticAirDefenseSiteComponentData component)
        {
            if (component == null)
                return;

            tempComponentComposition.Remove(component.ID);
            RefreshComponentLists();
        }

        private void UpdateResolvedSummary()
        {
            if (editorPopup.userData is not StaticAirDefenseSiteDefinition baseSite)
                return;

            var previewSite = BuildPreviewSite(baseSite);
            var resolved = StaticAirDefenseSiteResolver.Resolve(previewSite, ModuleSingleton.Instance.ModuleData);

            tileLabel.text = FormatTile(previewSite.Tile);
            resolvedSummaryLabel.text = AirDefenseEditorFormatting.FormatResolvedStaticSiteSummary(resolved);
            resolvedMissilesLabel.text =
                $"Missiles: {AirDefenseEditorFormatting.FormatGuidQuantityMap(resolved.InitialMissileInventory)}";

            if (resolved.MissingComponentIds.Count > 0)
            {
                resolvedDiagnosticsLabel.text =
                    $"Missing components: {AirDefenseEditorFormatting.FormatGuidCollection(resolved.MissingComponentIds)}";
            }
            else if (previewSite.Components.Count == 0)
            {
                resolvedDiagnosticsLabel.text = "No components selected yet.";
            }
            else
            {
                resolvedDiagnosticsLabel.text = string.Empty;
            }
        }

        private void OnSaveClicked()
        {
            if (campaign == null || editorPopup.userData is not StaticAirDefenseSiteDefinition editedSite)
                return;

            campaign.EnsureAirDataInitialized();

            editedSite.Name = string.IsNullOrWhiteSpace(siteNameField.value)
                ? "Unnamed Static Site"
                : siteNameField.value.Trim();
            editedSite.OwnerAlliance = ParseAlliance(ownerAllianceDropdown.value);
            editedSite.IsKeyIadsNode = keyNodeToggle.value;
            editedSite.Tile = GetAvailablePlacementCell(editedSite.Id, editedSite.Tile);
            editedSite.Components = tempComponentComposition
                .Where(entry => entry.Value > 0)
                .OrderBy(entry => ResolveComponentName(entry.Key))
                .Select(entry => new StaticAirDefenseSiteDefinition.ComponentComposition(entry.Key, entry.Value))
                .ToList();

            if (isEditingExisting)
            {
                var index = campaign.StaticAirDefenseSites.FindIndex(site => site.Id == editedSite.Id);
                if (index >= 0)
                    campaign.StaticAirDefenseSites[index] = editedSite;
            }
            else
            {
                campaign.StaticAirDefenseSites.Add(editedSite);
            }

            selectedSite = campaign.StaticAirDefenseSites.FirstOrDefault(site => site.Id == editedSite.Id);
            CloseEditorPopup();
            RefreshSitesList();
        }

        private StaticAirDefenseSiteDefinition BuildPreviewSite(StaticAirDefenseSiteDefinition baseSite)
        {
            var previewSite = CloneSite(baseSite);
            previewSite.Name = string.IsNullOrWhiteSpace(siteNameField.value) ? baseSite.Name : siteNameField.value.Trim();
            previewSite.OwnerAlliance = ParseAlliance(ownerAllianceDropdown.value);
            previewSite.IsKeyIadsNode = keyNodeToggle.value;
            previewSite.Components = tempComponentComposition
                .Where(entry => entry.Value > 0)
                .Select(entry => new StaticAirDefenseSiteDefinition.ComponentComposition(entry.Key, entry.Value))
                .ToList();
            return previewSite;
        }

        private StaticAirDefenseSiteDefinition CloneSite(StaticAirDefenseSiteDefinition site)
        {
            return new StaticAirDefenseSiteDefinition
            {
                Id = site.Id,
                Name = site.Name,
                Tile = site.Tile,
                OwnerAlliance = site.OwnerAlliance,
                IsKeyIadsNode = site.IsKeyIadsNode,
                Components = (site.Components ?? new List<StaticAirDefenseSiteDefinition.ComponentComposition>())
                    .Where(component => component != null)
                    .Select(component =>
                        new StaticAirDefenseSiteDefinition.ComponentComposition(component.ComponentId, component.Count))
                    .ToList()
            };
        }

        private StaticAirDefenseSiteDefinition FindSiteAtCell(Vector3Int cellPos)
        {
            return campaign?.StaticAirDefenseSites?.FirstOrDefault(site => site != null && site.Tile == cellPos);
        }

        private bool IsValidTile(Vector3Int cellPos)
        {
            return campaign?.tileData != null && campaign.tileData.ContainsKey(cellPos);
        }

        private bool IsOccupiedByOtherSite(Vector3Int cellPos, Guid siteId)
        {
            return campaign?.StaticAirDefenseSites?.Any(site =>
                       site != null && site.Id != siteId && site.Tile == cellPos) == true;
        }

        private Vector3Int GetAvailablePlacementCell(Guid siteId, Vector3Int preferredCell)
        {
            if (IsValidTile(preferredCell) && !IsOccupiedByOtherSite(preferredCell, siteId))
                return preferredCell;

            if (IsValidTile(_editor.lastPaintedCell) && !IsOccupiedByOtherSite(_editor.lastPaintedCell, siteId))
                return _editor.lastPaintedCell;

            if (campaign?.tileData != null)
            {
                foreach (var tile in campaign.tileData.Keys.OrderBy(tile => tile.x).ThenBy(tile => tile.y))
                {
                    if (!IsOccupiedByOtherSite(tile, siteId))
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

        private Alliance ParseAlliance(string value)
        {
            return Enum.TryParse(value, out Alliance alliance) ? alliance : Alliance.Neutral;
        }

        private string ResolveComponentName(Guid componentId)
        {
            return ModuleSingleton.Instance.ModuleData?.AirDefenseSiteComponentsById != null &&
                   ModuleSingleton.Instance.ModuleData.AirDefenseSiteComponentsById.TryGetValue(componentId, out var component)
                ? component.ComponentName
                : componentId.ToString();
        }

        private static string FormatTile(Vector3Int tile)
        {
            return $"({tile.x}, {tile.y}, {tile.z})";
        }
    }
}
