using System.Collections.Generic;
using Models.Gameplay.Campaign;
using ScriptableObjects.Gameplay.Tiles;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public abstract class EditorMode
    {
        protected TileHighlighter highlighter;
        protected VisualElement _tab;
        protected TilemapEditor Editor;
        protected List<LandmassTiles> availableTiles => Editor.availableLandTiles;

        public EditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter)
        {
            _tab = tab;
            Editor = editor;
            highlighter = _highlighter;
        }

        public virtual bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
            // Check if the pointer is over a UI element (UI Toolkit or UGUI)
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return false; // Exit early if we are clicking UI
            }

            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
            {
                return false; //no info for tile
            }

            highlighter.HighlightTile(cellPos);
            return true;
        }

        public abstract void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCall);

        public virtual void SetEditorMode()
        {
            _tab.style.display = DisplayStyle.Flex;
        }

        public virtual void DisableEditorMode()
        {
            _tab.style.display = DisplayStyle.None;
        }

        public abstract void SetCampaign();
    }
}