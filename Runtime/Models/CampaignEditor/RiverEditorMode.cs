using System;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    /// <summary>
    /// River editor paints the *edges* between hex tiles.
    /// Left click: add river edge
    /// Right click: remove river edge
    ///
    /// Uses RiverPolylineOverlay to draw merged river paths (fewest possible LineRenderers).
    /// </summary>
    public class RiverEditorMode : EditorMode
    {
        private Toggle continuousPaintToggle;
        private Button clearAllRiversBtn;
        private Label selectedInfoLabel;

        private bool continuousPaint = true;

        private RiverEdgeKey? lastPaintedEdge = null;

        // Rebuild throttling (prevents rebuilding every frame while dragging)
        private float rebuildCooldown = 0.05f;
        private float rebuildTimer = 0f;
        private bool needsRebuild = false;

        public RiverEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter)
            : base(tab, editor, _highlighter)
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            continuousPaintToggle = _tab.Q<Toggle>("river-continuous-toggle");
            clearAllRiversBtn = _tab.Q<Button>("river-clear-all-btn");
            selectedInfoLabel = _tab.Q<Label>("river-selected-info");

            if (continuousPaintToggle != null)
            {
                continuousPaintToggle.value = true;
                continuousPaintToggle.RegisterValueChangedCallback(evt => continuousPaint = evt.newValue);
            }

            clearAllRiversBtn?.RegisterCallback<ClickEvent>(evt => ClearAllRivers());
        }

        public override void SetCampaign()
        {
            lastPaintedEdge = null;
            needsRebuild = false;

            Editor.tilemapManager.riverOverlay?.RebuildAll(Editor.editingCampaign.tileData);
            UpdateInfo(null);
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();

            lastPaintedEdge = null;
            needsRebuild = false;

            Editor.tilemapManager.riverOverlay?.RebuildAll(Editor.editingCampaign.tileData);
            UpdateInfo(null);
        }

        public override void DisableEditorMode()
        {
            base.DisableEditorMode();

            lastPaintedEdge = null;
            needsRebuild = false;

            Editor.tilemapManager.riverOverlay?.ShowHover(Vector3Int.zero, Vector3Int.zero, false);
        }

        /// <summary>
        /// Called every frame from TilemapEditor Update() when this mode is active.
        /// Handles hover highlight + throttled rebuild.
        /// </summary>
        public void Update(Vector3Int cellPos)
        {
            if (Editor == null || Editor.tilemapManager == null || Editor.editingCampaign == null)
                return;

            // Avoid UI interaction painting
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Editor.tilemapManager.riverOverlay?.ShowHover(Vector3Int.zero, Vector3Int.zero, false);
                return;
            }

            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
            {
                Editor.tilemapManager.riverOverlay?.ShowHover(Vector3Int.zero, Vector3Int.zero, false);
                return;
            }

            var edge = GetClosestEdgeKey(cellPos);
            if (edge == null)
            {
                Editor.tilemapManager.riverOverlay?.ShowHover(Vector3Int.zero, Vector3Int.zero, false);
                return;
            }

            Editor.tilemapManager.riverOverlay?.ShowHover(edge.Value.a, edge.Value.b, true);
            UpdateInfo(edge);

            // Throttled rebuild
            if (needsRebuild)
            {
                rebuildTimer += Time.deltaTime;
                if (rebuildTimer >= rebuildCooldown)
                {
                    rebuildTimer = 0f;
                    needsRebuild = false;
                    Editor.tilemapManager.riverOverlay.RebuildAll(Editor.editingCampaign.tileData);
                }
            }
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!base.PaintTile(cellPos, lastPaintedCell)) return false;
            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
                return false;

            var edge = GetClosestEdgeKey(cellPos);
            if (edge == null)
                return false;

            if (!continuousPaint && lastPaintedEdge.HasValue && lastPaintedEdge.Value.Equals(edge.Value))
                return false;

            if (lastPaintedEdge.HasValue && lastPaintedEdge.Value.Equals(edge.Value))
                return false;

            ApplyRiver(edge.Value, true);
            lastPaintedEdge = edge;
            return true;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            if (!Editor.editingCampaign.tileData.ContainsKey(cellPos))
                return;

            var edge = GetClosestEdgeKey(cellPos);
            if (edge == null) return;

            if (lastPaintedEdge.HasValue && lastPaintedEdge.Value.Equals(edge.Value))
                return;

            ApplyRiver(edge.Value, false);
            lastPaintedEdge = edge;
        }

        private bool ApplyRiver(RiverEdgeKey edge, bool value)
        {
            if (!Editor.editingCampaign.tileData.ContainsKey(edge.a))
                return false;
            if (!Editor.editingCampaign.tileData.ContainsKey(edge.b))
                return false;

            var aData = Editor.editingCampaign.tileData[edge.a];
            var bData = Editor.editingCampaign.tileData[edge.b];

            aData.SetRiver(edge.dirFromA, value);
            bData.SetRiver(edge.dirFromB, value);

            Editor.tilemapManager.UpdateTile(edge.a);
            Editor.tilemapManager.UpdateTile(edge.b);

            // We rebuild merged lines after edits, but throttled
            needsRebuild = true;
            return true;
        }

        private void ClearAllRivers()
        {
            foreach (var kvp in Editor.editingCampaign.tileData)
            {
                kvp.Value.ClearRivers();
                Editor.tilemapManager.UpdateTile(kvp.Key);
            }
            Editor.tilemapManager.riverOverlay?.RebuildAll(Editor.editingCampaign.tileData);

            lastPaintedEdge = null;
            needsRebuild = false;
            rebuildTimer = 0f;

            UpdateInfo(null);
            Debug.Log("Cleared all rivers in campaign.");
        }

        private void UpdateInfo(RiverEdgeKey? edge)
        {
            if (selectedInfoLabel == null) return;

            if (edge == null)
            {
                selectedInfoLabel.text = "Click near an edge between two tiles to paint a river.";
                return;
            }

            bool hasRiver = Editor.editingCampaign.tileData[edge.Value.a].HasRiver(edge.Value.dirFromA);
            selectedInfoLabel.text = $"Edge: {edge.Value.a} ↔ {edge.Value.b}   River: {(hasRiver ? "Yes" : "No")}";
        }

        #region Edge Picking

        private RiverEdgeKey? GetClosestEdgeKey(Vector3Int cellPos)
        {
            Vector3 world = GetMouseWorldPosition();
            Vector3 center = Editor.tilemapManager.grid.GetCellCenterWorld(cellPos);

            Vector2 delta = new Vector2(world.x - center.x, world.y - center.y);
            if (delta.sqrMagnitude < 0.0001f) delta = Vector2.right;

            float angle = Mathf.Atan2(delta.y, delta.x);
            int sector = Mathf.RoundToInt(angle / (Mathf.PI / 3f));
            sector = (sector % 6 + 6) % 6;

            HexDirection dir = SectorToDirection(sector);
            Vector3Int neighbor = GetNeighbor(cellPos, dir);

            if (!Editor.editingCampaign.tileData.ContainsKey(neighbor))
                return null;

            var a = cellPos;
            var b = neighbor;
            var dirFromA = dir;
            var dirFromB = dir.Opposite();

            if (CompareCells(b, a) < 0)
            {
                a = neighbor;
                b = cellPos;
                dirFromA = dir.Opposite();
                dirFromB = dir;
            }

            return new RiverEdgeKey(a, b, dirFromA, dirFromB);
        }

        private HexDirection SectorToDirection(int sector)
        {
            // 0: E, 1: NE, 2: NW, 3: W, 4: SW, 5: SE
            return sector switch
            {
                0 => HexDirection.E,
                1 => HexDirection.NE,
                2 => HexDirection.NW,
                3 => HexDirection.W,
                4 => HexDirection.SW,
                5 => HexDirection.SE,
                _ => HexDirection.E
            };
        }

        private Vector3Int GetNeighbor(Vector3Int cell, HexDirection dir)
        {
            bool isEvenRow = cell.y % 2 == 0;

            return dir switch
            {
                HexDirection.E => cell + new Vector3Int(1, 0, 0),
                HexDirection.W => cell + new Vector3Int(-1, 0, 0),

                HexDirection.NE => isEvenRow ? cell + new Vector3Int(0, 1, 0) : cell + new Vector3Int(1, 1, 0),
                HexDirection.NW => isEvenRow ? cell + new Vector3Int(-1, 1, 0) : cell + new Vector3Int(0, 1, 0),

                HexDirection.SE => isEvenRow ? cell + new Vector3Int(0, -1, 0) : cell + new Vector3Int(1, -1, 0),
                HexDirection.SW => isEvenRow ? cell + new Vector3Int(-1, -1, 0) : cell + new Vector3Int(0, -1, 0),

                _ => cell
            };
        }

        private static int CompareCells(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 screenPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f);
            return Camera.main.ScreenToWorldPoint(screenPosition);
        }

        #endregion

        private struct RiverEdgeKey : IEquatable<RiverEdgeKey>
        {
            public Vector3Int a;
            public Vector3Int b;
            public HexDirection dirFromA;
            public HexDirection dirFromB;

            public RiverEdgeKey(Vector3Int a, Vector3Int b, HexDirection dirFromA, HexDirection dirFromB)
            {
                this.a = a;
                this.b = b;
                this.dirFromA = dirFromA;
                this.dirFromB = dirFromB;
            }

            public bool Equals(RiverEdgeKey other)
            {
                return a == other.a && b == other.b && dirFromA == other.dirFromA && dirFromB == other.dirFromB;
            }

            public override bool Equals(object obj) => obj is RiverEdgeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = a.GetHashCode();
                    hash = (hash * 397) ^ b.GetHashCode();
                    hash = (hash * 397) ^ (int)dirFromA;
                    hash = (hash * 397) ^ (int)dirFromB;
                    return hash;
                }
            }
        }
    }
}