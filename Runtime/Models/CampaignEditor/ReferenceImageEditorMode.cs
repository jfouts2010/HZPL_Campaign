
using Models.Gameplay.Campaign;
using ScriptableObjects.Gameplay.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// Allows user to load a reference image into the scene (e.g. map sketch) and manipulate it.
    /// Uses the existing EditorMode tab architecture (UI Toolkit).
    /// </summary>
    public class ReferenceImageEditorMode : EditorMode
    {
        private readonly ReferenceImageController controller;

        // UI
        private Button uploadBtn;
        private Button changeBtn;
        private Toggle visibleToggle;
        private Toggle aheadToggle;
        private FloatField scaleField;
        private FloatField xField;
        private FloatField zField;
        private Button fitMapBtn;

        public ReferenceImageEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter, ReferenceImageController controller) : base(tab, editor, highlighter)
        {
            _tab = tab;
            Editor = editor;
            this.highlighter = highlighter;
            this.controller = controller;
            WireUI();
            RefreshUIFromController();
        }

        private void WireUI()
        {
            uploadBtn = _tab.Q<Button>("refimg-upload-btn");
            changeBtn = _tab.Q<Button>("refimg-change-btn");
            visibleToggle = _tab.Q<Toggle>("refimg-visible-toggle");
            aheadToggle = _tab.Q<Toggle>("refimg-ahead-toggle");
            scaleField = _tab.Q<FloatField>("refimg-scale-field");
            xField = _tab.Q<FloatField>("refimg-x-field");
            zField = _tab.Q<FloatField>("refimg-z-field");
            fitMapBtn = _tab.Q<Button>("refimg-fit-map-btn");

            if (uploadBtn != null) uploadBtn.clicked += PickAndLoadImage;
            if (changeBtn != null) changeBtn.clicked += PickAndLoadImage;

            if (visibleToggle != null)
                visibleToggle.RegisterValueChangedCallback(evt =>
                {
                    controller.SetVisible(evt.newValue);
                });

            if (aheadToggle != null)
                aheadToggle.RegisterValueChangedCallback(evt =>
                {
                    controller.SetAheadOfTilemaps(evt.newValue);
                });

            if (scaleField != null)
                scaleField.RegisterValueChangedCallback(evt =>
                {
                    controller.SetScale(evt.newValue);
                });

            void UpdatePos()
            {
                controller.SetPositionXZ(xField.value, zField.value);
            }

            if (xField != null) xField.RegisterValueChangedCallback(_ => UpdatePos());
            if (zField != null) zField.RegisterValueChangedCallback(_ => UpdatePos());

            if (fitMapBtn != null)
                fitMapBtn.clicked += FitMapToReference;
        }

        private void PickAndLoadImage()
        {
            var path = EditorUtility.OpenFilePanel("Select Reference Image", Application.dataPath, "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                controller.LoadImageFromPath(path);
                RefreshUIFromController();
            }
        }

        private void RefreshUIFromController()
        {
            if (controller == null) return;

            if (visibleToggle != null) visibleToggle.SetValueWithoutNotify(controller.Visible);
            if (aheadToggle != null) aheadToggle.SetValueWithoutNotify(controller.IsAhead);
            if (scaleField != null) scaleField.SetValueWithoutNotify(controller.transform.localScale.x);
            if (xField != null) xField.SetValueWithoutNotify(controller.transform.position.x);
            if (zField != null) zField.SetValueWithoutNotify(controller.transform.position.y);

            if (changeBtn != null)
                changeBtn.SetEnabled(controller.CurrentTexture != null);
            if (fitMapBtn != null)
                fitMapBtn.SetEnabled(controller.CurrentTexture != null);
        }

        private void FitMapToReference()
        {
            bool success = Editor.FitMapSizeToReferenceImage();
            if (!success)
            {
                Debug.LogWarning("ReferenceImageEditorMode: Unable to fit map to reference image.");
            }

            RefreshUIFromController();
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshUIFromController();
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            // no-op
            return false;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
            // no-op
        }

        public override void SetCampaign()
        {
            // nothing to do
        }
    }
}
