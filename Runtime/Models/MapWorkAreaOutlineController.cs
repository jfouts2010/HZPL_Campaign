using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using UnityEngine;

namespace Models.Gameplay
{
    /// <summary>
    /// Draws a world-space outline around the currently editable campaign tile area.
    /// </summary>
    public class MapWorkAreaOutlineController : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float zOffset = -0.15f;
        [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 200;

        private Grid _grid;
        private LineRenderer _lineRenderer;
        private Material _runtimeMaterial;

        public void Initialize(Grid grid)
        {
            _grid = grid;
            EnsureRenderer();
        }

        public void Rebuild(IEnumerable<Vector3Int> cells)
        {
            EnsureRenderer();
            if (_grid == null || cells == null)
            {
                _lineRenderer.enabled = false;
                return;
            }

            var cellSet = new HashSet<Vector3Int>(cells);
            if (cellSet.Count == 0)
            {
                _lineRenderer.enabled = false;
                return;
            }

            var adjacency = new Dictionary<PointKey, List<PointKey>>();
            var pointLookup = new Dictionary<PointKey, Vector3>();

            foreach (var cell in cellSet)
            {
                for (int d = 0; d < 6; d++)
                {
                    var dir = (HexDirection)d;
                    var neighbor = GetNeighbor(cell, dir);
                    if (cellSet.Contains(neighbor))
                        continue;

                    var edge = GetSharedEdgePoints(cell, neighbor);
                    var a = new PointKey(edge.p0);
                    var b = new PointKey(edge.p1);

                    if (!adjacency.TryGetValue(a, out var aList))
                    {
                        aList = new List<PointKey>(2);
                        adjacency[a] = aList;
                    }
                    if (!adjacency.TryGetValue(b, out var bList))
                    {
                        bList = new List<PointKey>(2);
                        adjacency[b] = bList;
                    }

                    aList.Add(b);
                    bList.Add(a);

                    pointLookup[a] = edge.p0;
                    pointLookup[b] = edge.p1;
                }
            }

            if (adjacency.Count < 3)
            {
                _lineRenderer.enabled = false;
                return;
            }

            var start = adjacency.Keys
                .OrderBy(p => pointLookup[p].y)
                .ThenBy(p => pointLookup[p].x)
                .First();

            var ordered = new List<Vector3>(adjacency.Count);
            var prev = default(PointKey);
            var hasPrev = false;
            var current = start;

            int guard = adjacency.Count + 8;
            while (guard-- > 0)
            {
                if (!pointLookup.TryGetValue(current, out var point))
                    break;

                point.z += zOffset;
                ordered.Add(point);

                if (!adjacency.TryGetValue(current, out var neighbors) || neighbors.Count == 0)
                    break;

                PointKey next;
                if (!hasPrev)
                {
                    next = neighbors[0];
                }
                else if (neighbors.Count == 1)
                {
                    next = neighbors[0];
                }
                else
                {
                    next = neighbors[0].Equals(prev) ? neighbors[1] : neighbors[0];
                }

                prev = current;
                hasPrev = true;
                current = next;

                if (current.Equals(start))
                    break;
            }

            if (ordered.Count < 3)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.loop = true;
            _lineRenderer.positionCount = ordered.Count;
            _lineRenderer.SetPositions(ordered.ToArray());
        }

        private void EnsureRenderer()
        {
            if (_lineRenderer != null)
                return;

            _lineRenderer = gameObject.GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            _runtimeMaterial = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.material = _runtimeMaterial;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.numCapVertices = 2;
            _lineRenderer.numCornerVertices = 2;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            _lineRenderer.startColor = lineColor;
            _lineRenderer.endColor = lineColor;
            _lineRenderer.sortingLayerName = sortingLayerName;
            _lineRenderer.sortingOrder = sortingOrder;
            _lineRenderer.enabled = false;
        }

        private (Vector3 p0, Vector3 p1) GetSharedEdgePoints(Vector3Int cellA, Vector3Int cellB)
        {
            Vector3 centerA = _grid.GetCellCenterWorld(cellA);
            Vector3 centerB = _grid.GetCellCenterWorld(cellB);

            Vector2 dir = centerB - centerA;
            dir.Normalize();

            Vector3 cellSize = _grid.cellSize;
            float r = Mathf.Max(cellSize.x, cellSize.y) * 0.5f;

            Vector3 mid = (centerA + centerB) * 0.5f;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float edgeHalf = r * 0.5f;

            Vector3 p0 = mid + (Vector3)(perp * edgeHalf);
            Vector3 p1 = mid - (Vector3)(perp * edgeHalf);

            return (p0, p1);
        }

        private static Vector3Int GetNeighbor(Vector3Int cell, HexDirection dir)
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

        private readonly struct PointKey : IEquatable<PointKey>
        {
            private readonly int _x;
            private readonly int _y;

            public PointKey(Vector3 worldPoint)
            {
                _x = Mathf.RoundToInt(worldPoint.x * 100f);
                _y = Mathf.RoundToInt(worldPoint.y * 100f);
            }

            public bool Equals(PointKey other)
            {
                return _x == other._x && _y == other._y;
            }

            public override bool Equals(object obj)
            {
                return obj is PointKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_x * 397) ^ _y;
                }
            }
        }
    }
}
