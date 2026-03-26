using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Random = System.Random;

namespace Models.CampaignEditor
{
    public class AreaEditorMode : EditorMode
    {
        private ListView areaListView;
        private Button addBtn;
        private Button deleteBtn;
        private TextField nameField;
        private EnumField typeField;
        private Label validationLabel;

        private Area selectedArea;

        public AreaEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter)
            : base(tab, editor, highlighter)
        {
            QueryUI();
            BindUI();
        }

        private void QueryUI()
        {
            areaListView = _tab.Q<ListView>("area-listview");
            addBtn = _tab.Q<Button>("area-add-btn");
            deleteBtn = _tab.Q<Button>("area-delete-btn");
            nameField = _tab.Q<TextField>("area-name-field");
            typeField = _tab.Q<EnumField>("area-type-field");
            validationLabel = _tab.Q<Label>("area-validation-label");
        }

        private void BindUI()
        {
            if (typeField != null) typeField.Init(AreaType.Land);

            addBtn.clicked += CreateArea;
            deleteBtn.clicked += DeleteSelectedArea;

            areaListView.makeItem = () =>
            {
                var row = new VisualElement
                    { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var colorSwatch = new VisualElement { name = "swatch" };
                colorSwatch.style.width = 12;
                colorSwatch.style.height = 12;
                colorSwatch.style.marginRight = 8;
                colorSwatch.style.borderTopLeftRadius = 3;
                colorSwatch.style.borderTopRightRadius = 3;
                colorSwatch.style.borderBottomLeftRadius = 3;
                colorSwatch.style.borderBottomRightRadius = 3;
                row.Add(colorSwatch);

                var label = new Label { name = "label" };
                label.style.flexGrow = 1;
                row.Add(label);
                return row;
            };

            areaListView.bindItem = (e, i) =>
            {
                if (Editor.editingCampaign == null) return;
                var areas = Editor.editingCampaign.areas;
                if (i < 0 || i >= areas.Count) return;

                var a = areas[i];
                var label = e.Q<Label>("label");
                var swatch = e.Q<VisualElement>("swatch");
                label.text = $"{a.Name} ({a.Type})";
                swatch.style.backgroundColor = Editor.editingCampaign.GetAreaColor(a.Id);
            };

            areaListView.selectionChanged += objects =>
            {
                selectedArea = objects?.FirstOrDefault() as Area;
                RefreshSelectedAreaUI();
                // Editor.tilemapManager.RefreshTilemaps();
            };

            nameField.RegisterValueChangedCallback(evt =>
            {
                if (selectedArea == null) return;
                selectedArea.Name = evt.newValue;
                areaListView.Rebuild();
            });

            typeField.RegisterValueChangedCallback(evt =>
            {
                if (selectedArea == null) return;
                var newType = (AreaType)evt.newValue;
                if (newType == selectedArea.Type) return;

                // Validate: can't switch type if tiles contain incompatible terrain (based on existing landmassTileID heuristic)
                if (!CanAreaBeType(selectedArea, newType))
                {
                    validationLabel.text = "Cannot change type: area contains tiles incompatible with selected type.";
                    typeField.SetValueWithoutNotify(selectedArea.Type);
                    return;
                }

                selectedArea.Type = newType;
                validationLabel.text = "";
                areaListView.Rebuild();
            });
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            EnsureCampaignHasAreas();
            RefreshList();
            Editor.tilemapManager.overlayTilemap.gameObject.GetComponent<TilemapRenderer>().enabled = true;
            ShowValidation();
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();
            Editor.tilemapManager.overlayTilemap.gameObject.GetComponent<TilemapRenderer>().enabled = false;
            validationLabel.text = "";
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            bool success = base.PaintTile(cellPos, lastPaintedCell);
            if (!success) return false;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return false;
            if (Editor.editingCampaign == null || selectedArea == null) return false;

            // Ensure tile exists
            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos)) return false;

            // Enforce land/water rules:
            var tile = Editor.editingCampaign.tileData[cellPos];
            var requiredLand = selectedArea.Type == AreaType.Land;
            if (requiredLand != tile.LandTile)
            {
                validationLabel.text = requiredLand
                    ? "This area is Land. You can only paint tiles that are Land (have a landmass tile)."
                    : "This area is Water. You can only paint tiles that are Water (no landmass tile).";
                return false;
            }

            // Assign tile to this area
            tile.areaId = selectedArea.Id;
            Editor.tilemapManager.UpdateTile(cellPos);

            validationLabel.text = "";
            ShowValidation();
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (Editor.editingCampaign == null) return;
            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos)) return;
            if (lastPaintedCell.HasValue && cellPos.Equals(lastPaintedCell.Value))
                return;
            var tile = Editor.editingCampaign.tileData[cellPos];
            tile.areaId = Guid.Empty;
            Editor.tilemapManager.UpdateTile(cellPos);

            ShowValidation();
        }

        public override void SetCampaign()
        {
            EnsureCampaignHasAreas();
            RefreshList();
            //Editor.tilemapManager.RefreshTilemaps();
            ShowValidation();
        }

        private void EnsureCampaignHasAreas()
        {
            if (Editor.editingCampaign == null) return;

            if (Editor.editingCampaign.areas == null)
                Editor.editingCampaign.areas = new List<Area>();
        }

        private void RefreshList()
        {
            if (Editor.editingCampaign == null) return;

            areaListView.itemsSource = Editor.editingCampaign.areas;
            areaListView.Rebuild();

            if (selectedArea == null && Editor.editingCampaign.areas.Count > 0)
            {
                selectedArea = Editor.editingCampaign.areas[0];
                areaListView.SetSelection(0);
            }

            RefreshSelectedAreaUI();
        }

        private void RefreshSelectedAreaUI()
        {
            if (selectedArea == null)
            {
                nameField?.SetValueWithoutNotify("");
                typeField?.SetValueWithoutNotify(AreaType.Land);
                deleteBtn?.SetEnabled(false);
                return;
            }

            nameField?.SetValueWithoutNotify(selectedArea.Name);
            typeField?.SetValueWithoutNotify(selectedArea.Type);
            deleteBtn?.SetEnabled(true);
        }

        private void CreateArea()
        {
            if (Editor.editingCampaign == null) return;
            Random rnd = new Random();
            Color randomColor =
                new Color((float)rnd.NextDouble(), (float)rnd.NextDouble(), (float)rnd.NextDouble(), 1f);
            var newArea = new Area($"Area {Editor.editingCampaign.areas.Count + 1}", AreaType.Land, randomColor);
            Editor.editingCampaign.areas.Add(newArea);

            RefreshList();

            var idx = Editor.editingCampaign.areas.Count - 1;
            areaListView.SetSelection(idx);
            areaListView.ScrollToItem(idx);
        }

        private void DeleteSelectedArea()
        {
            if (Editor.editingCampaign == null || selectedArea == null) return;

            // Remove all tile refs
            foreach (var kvp in Editor.editingCampaign.tileData)
            {
                if (kvp.Value.areaId == selectedArea.Id)
                    kvp.Value.areaId = Guid.Empty;
            }

            Editor.editingCampaign.areas.Remove(selectedArea);
            selectedArea = null;

            RefreshList();
            Editor.tilemapManager.RefreshTilemaps();
            ShowValidation();
        }


        private void ShowValidation()
        {
            if (Editor.editingCampaign == null || validationLabel == null) return;

            int missing = Editor.editingCampaign.tileData.Values.Count(t => t.areaId == Guid.Empty);
            if (missing == 0)
            {
                validationLabel.text = "✅ All tiles are assigned to an area.";
                return;
            }

            validationLabel.text =
                $"⚠ {missing} tiles are not assigned to an area. Every tile must belong to an area to be playable.";
        }

        private bool CanAreaBeType(Area area, AreaType newType)
        {
            if (Editor.editingCampaign == null) return true;

            foreach (var kvp in Editor.editingCampaign.tileData)
            {
                if (kvp.Value.areaId != area.Id) continue;

                bool isLand = kvp.Value.landmassTileID != Guid.Empty;
                if (newType == AreaType.Land && !isLand) return false;
                if (newType == AreaType.Water && isLand) return false;
            }

            return true;
        }
    }
}