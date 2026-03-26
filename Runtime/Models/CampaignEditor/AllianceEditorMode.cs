using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Allows assigning countries in the current editing campaign into BlueFor / RedFor / Neutral alliances.
    /// Countries always start Neutral and can be moved between columns.
    /// </summary>
    public class AllianceEditorMode : EditorMode
    {
        private ListView neutralListView;
        private ListView blueListView;
        private ListView redListView;

        private Button toBlueBtn;
        private Button toRedBtn;
        private Button toNeutralBtn;

        private Label selectionLabel;

        private Campaign currentCampaign => Editor.editingCampaign;

        private CountryData _selectedCampaignCountry;
        private Alliance selectedAllianceColumn = Alliance.Neutral;

        public AllianceEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter tileHighlighter) : base(tab, editor, tileHighlighter)
        {
            SetupUI();
        }

        private void SetupUI()
        {
            neutralListView = _tab.Q<ListView>("alliance-neutral-list");
            blueListView = _tab.Q<ListView>("alliance-blue-list");
            redListView = _tab.Q<ListView>("alliance-red-list");

            toBlueBtn = _tab.Q<Button>("alliance-to-blue-btn");
            toRedBtn = _tab.Q<Button>("alliance-to-red-btn");
            toNeutralBtn = _tab.Q<Button>("alliance-to-neutral-btn");

            selectionLabel = _tab.Q<Label>("alliance-selection-label");

            if (neutralListView == null || blueListView == null || redListView == null)
            {
                Debug.LogError("AllianceEditorMode: Missing UXML elements. Ensure alliance-tab exists and has required element names.");
                return;
            }

            SetupCountryListView(neutralListView, Alliance.Neutral, OnNeutralSelectionChanged);
            SetupCountryListView(blueListView, Alliance.BlueFor, OnBlueSelectionChanged);
            SetupCountryListView(redListView, Alliance.RedFor, OnRedSelectionChanged);

            toBlueBtn.clicked += () => MoveSelectedTo(Alliance.BlueFor);
            toRedBtn.clicked += () => MoveSelectedTo(Alliance.RedFor);
            toNeutralBtn.clicked += () => MoveSelectedTo(Alliance.Neutral);

            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void SetupCountryListView(ListView listView, Alliance columnAlliance, Action<IEnumerable<object>> onSelectionChanged)
        {
            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.paddingLeft = 8;
                row.style.paddingRight = 8;

                var flag = new VisualElement();
                flag.name = "flag";
                flag.style.width = 28;
                flag.style.height = 18;
                flag.style.marginRight = 8;
                flag.style.flexShrink = 0;
                flag.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                var label = new Label();
                label.name = "name";
                label.style.flexGrow = 1;

                row.Add(flag);
                row.Add(label);
                return row;
            };

            listView.bindItem = (e, i) =>
            {
                if (listView.itemsSource == null || i < 0 || i >= listView.itemsSource.Count) return;

                var country = listView.itemsSource[i] as CountryData;
                if (country == null) return;

                var flag = e.Q<VisualElement>("flag");
                var label = e.Q<Label>("name");

                label.text = country.CountryName;

                flag.style.backgroundImage = null;
                var sprite = country.FlagSprite;
                if (sprite != null)
                {
                    flag.style.backgroundImage = new StyleBackground(sprite);
                }
            };

            listView.selectionType = SelectionType.Single;
            listView.selectionChanged += onSelectionChanged;
            listView.fixedItemHeight = 28;
        }

        public override void SetCampaign()
        {
            _selectedCampaignCountry = null;
            selectedAllianceColumn = Alliance.Neutral;
            
            RefreshLists();
            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void RefreshLists()
        {
            if (currentCampaign == null)
            {
                neutralListView.itemsSource = new List<CountryData>();
                blueListView.itemsSource = new List<CountryData>();
                redListView.itemsSource = new List<CountryData>();
                neutralListView.Rebuild();
                blueListView.Rebuild();
                redListView.Rebuild();
                return;
            }

            var neutral = currentCampaign.GetAllianceData(Alliance.Neutral);
            var blue = currentCampaign.GetAllianceData(Alliance.BlueFor);
            var red = currentCampaign.GetAllianceData(Alliance.RedFor);

            neutralListView.itemsSource = neutral;
            blueListView.itemsSource = blue;
            redListView.itemsSource = red;

            neutralListView.Rebuild();
            blueListView.Rebuild();
            redListView.Rebuild();
        }

        private void ClearOtherSelections(ListView except)
        {
            if (neutralListView != except) neutralListView.ClearSelection();
            if (blueListView != except) blueListView.ClearSelection();
            if (redListView != except) redListView.ClearSelection();
        }

        private void OnNeutralSelectionChanged(IEnumerable<object> selection)
        {
            ClearOtherSelections(neutralListView);
            _selectedCampaignCountry = selection?.FirstOrDefault() as CountryData;
            selectedAllianceColumn = Alliance.Neutral;
            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void OnBlueSelectionChanged(IEnumerable<object> selection)
        {
            ClearOtherSelections(blueListView);
            _selectedCampaignCountry = selection?.FirstOrDefault() as CountryData;
            selectedAllianceColumn = Alliance.BlueFor;
            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void OnRedSelectionChanged(IEnumerable<object> selection)
        {
            ClearOtherSelections(redListView);
            _selectedCampaignCountry = selection?.FirstOrDefault() as CountryData;
            selectedAllianceColumn = Alliance.RedFor;
            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void MoveSelectedTo(Alliance alliance)
        {
            if (currentCampaign == null || _selectedCampaignCountry == null) return;
            currentCampaign.CountryAlliance[_selectedCampaignCountry.ID] = alliance;

            // Keep selection, but switch to the destination column.
            selectedAllianceColumn = alliance;

            RefreshLists();
            ReSelectCountry(_selectedCampaignCountry, alliance);

            UpdateSelectionLabel();
            UpdateButtons();
        }

        private void ReSelectCountry(CountryData campaignCountry, Alliance alliance)
        {
            var targetList = GetListView(alliance);
            if (targetList == null || targetList.itemsSource == null) return;

            for (int i = 0; i < targetList.itemsSource.Count; i++)
            {
                if (ReferenceEquals(targetList.itemsSource[i], campaignCountry))
                {
                    targetList.SetSelection(i);
                    break;
                }
            }
        }

        private ListView GetListView(Alliance alliance)
        {
            switch (alliance)
            {
                case Alliance.BlueFor: return blueListView;
                case Alliance.RedFor: return redListView;
                default: return neutralListView;
            }
        }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel == null) return;

            if (_selectedCampaignCountry == null)
            {
                selectionLabel.text = "Selected: None";
            }
            else
            {
                selectionLabel.text = $"Selected: {_selectedCampaignCountry.CountryName} ({currentCampaign.CountryAlliance[_selectedCampaignCountry.ID]})";
            }
        }

        private void UpdateButtons()
        {
            bool hasSelection = _selectedCampaignCountry != null;

            if (toBlueBtn != null) toBlueBtn.SetEnabled(hasSelection && currentCampaign.CountryAlliance[_selectedCampaignCountry.ID] != Alliance.BlueFor);
            if (toRedBtn != null) toRedBtn.SetEnabled(hasSelection && currentCampaign.CountryAlliance[_selectedCampaignCountry.ID] != Alliance.RedFor);
            if (toNeutralBtn != null) toNeutralBtn.SetEnabled(hasSelection && currentCampaign.CountryAlliance[_selectedCampaignCountry.ID] != Alliance.Neutral);
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
            return false;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
            // No erasing in this mode.
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshLists();
            UpdateSelectionLabel();
            UpdateButtons();
        }
    }
}
