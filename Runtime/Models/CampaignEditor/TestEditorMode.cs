using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Models.Gameplay.Campaign;
using UnityEngine;
using UnityEngine.UIElements;

namespace Models.CampaignEditor
{
    public class TestEditorMode : EditorMode
    {
        private Button generateBtn;
        private TextField outputField;

        public TestEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter highlighter) : base(tab, editor, highlighter)
        {
            WireUI();
        }

        private void WireUI()
        {
            generateBtn = _tab.Q<Button>("test-generate-boundary-btn");
            outputField = _tab.Q<TextField>("test-boundary-output-field");

            if (outputField != null)
                outputField.isReadOnly = true;

            if (generateBtn != null)
                generateBtn.clicked += GenerateBoundaryOutput;
        }

        private void GenerateBoundaryOutput()
        {
            if (Editor?.editingCampaign?.tileData == null || Editor.editingCampaign.tileData.Count == 0)
            {
                SetOutput("No campaign tile data.");
                return;
            }

            var tiles = Editor.editingCampaign.tileData;
            GetTileBounds(tiles.Keys, out int minX, out int maxX, out int minY, out int maxY);

            var missionBottomLeft = Editor.editingCampaign.BottomLeftCorner;
            var missionTopRight = Editor.editingCampaign.TopRightCorner;

            var redPolygons = BuildPolygons(Alliance.RedFor, tiles, minX, maxX, minY, maxY, missionBottomLeft, missionTopRight);
            var bluePolygons = BuildPolygons(Alliance.BlueFor, tiles, minX, maxX, minY, maxY, missionBottomLeft, missionTopRight);

            SetOutput(BuildZonesJson(redPolygons, bluePolygons));
        }

        private static List<List<Vector2>> BuildPolygons(
            Alliance alliance,
            Dictionary<Vector3Int, HZPLTileData> tiles,
            int minX,
            int maxX,
            int minY,
            int maxY,
            Vector2Int missionBottomLeft,
            Vector2Int missionTopRight)
        {
            var allianceTiles = new HashSet<Vector3Int>();
            foreach (var kvp in tiles)
            {
                var cell = kvp.Key;
                var data = kvp.Value;
                if (data != null && data.LandTile && data.controllingAlliance == alliance)
                    allianceTiles.Add(cell);
            }

            if (allianceTiles.Count == 0)
                return new List<List<Vector2>>();

            // Build undirected boundary-edge graph from alliance/non-alliance cell borders.
            var adjacency = new Dictionary<PointKey, List<PointKey>>();
            var points = new Dictionary<PointKey, Vector2>();

            foreach (var cell in allianceTiles)
            {
                foreach (var neighbor in GetHexNeighbors(cell))
                {
                    if (allianceTiles.Contains(neighbor))
                        continue;

                    var edge = GetSharedEdgePoints(cell, neighbor);
                    var aKey = new PointKey(edge.a);
                    var bKey = new PointKey(edge.b);

                    AddAdjacency(adjacency, aKey, bKey);
                    AddAdjacency(adjacency, bKey, aKey);
                    points[aKey] = edge.a;
                    points[bKey] = edge.b;
                }
            }

            var polygons = new List<List<Vector2>>();
            var visitedEdges = new HashSet<EdgeKey>();

            foreach (var start in adjacency.Keys.ToList())
            {
                if (!adjacency.TryGetValue(start, out var startNeighbors) || startNeighbors.Count == 0)
                    continue;

                foreach (var first in startNeighbors)
                {
                    var firstEdge = new EdgeKey(start, first);
                    if (visitedEdges.Contains(firstEdge))
                        continue;

                    var rawLoop = TraceLoop(start, first, adjacency, points, visitedEdges);
                    if (rawLoop.Count < 3)
                        continue;

                    var localBottomLeftCenter = GetCellCenter(new Vector3Int(minX, minY, 0));
                    var localTopRightCenter = GetCellCenter(new Vector3Int(maxX, maxY, 0));

                    var mapped = rawLoop.Select(p =>
                    {
                        float nx = NormalizeAndClamp(p.x, localBottomLeftCenter.x, localTopRightCenter.x);
                        float ny = NormalizeAndClamp(p.y, localBottomLeftCenter.y, localTopRightCenter.y);
                        float outX = Mathf.Lerp(missionBottomLeft.x, missionTopRight.x, nx);
                        float outY = Mathf.Lerp(missionBottomLeft.y, missionTopRight.y, ny);
                        return new Vector2(outX, outY);
                    }).ToList();

                    polygons.Add(mapped);
                }
            }

            return polygons;
        }

        private static string BuildZonesJson(List<List<Vector2>> redPolygons, List<List<Vector2>> bluePolygons)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("{");
            sb.AppendLine("  \"redZones\": [");
            AppendPolygons(sb, redPolygons, "  ");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"blueZones\": [");
            AppendPolygons(sb, bluePolygons, "  ");
            sb.AppendLine("  ]");
            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendPolygons(StringBuilder sb, List<List<Vector2>> polygons, string indent)
        {
            if (polygons == null || polygons.Count == 0)
                return;

            for (int polyIdx = 0; polyIdx < polygons.Count; polyIdx++)
            {
                var poly = polygons[polyIdx];
                sb.AppendLine($"{indent}  [");

                for (int i = 0; i < poly.Count; i++)
                {
                    var p = poly[i];
                    sb.AppendLine($"{indent}    [");
                    sb.AppendLine($"{indent}      {FormatNumber(p.y)},");
                    sb.AppendLine($"{indent}      {FormatNumber(p.x)}");
                    sb.Append($"{indent}    ]");
                    if (i < poly.Count - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.Append($"{indent}  ]");
                if (polyIdx < polygons.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
        }

        private static List<Vector2> TraceLoop(
            PointKey start,
            PointKey first,
            Dictionary<PointKey, List<PointKey>> adjacency,
            Dictionary<PointKey, Vector2> points,
            HashSet<EdgeKey> visitedEdges)
        {
            var loop = new List<Vector2>();
            var prev = start;
            var current = first;

            loop.Add(points[start]);

            int guard = adjacency.Count * 4 + 16;
            while (guard-- > 0)
            {
                visitedEdges.Add(new EdgeKey(prev, current));
                if (points.TryGetValue(current, out var currentPoint))
                    loop.Add(currentPoint);

                if (current.Equals(start))
                    break;

                if (!adjacency.TryGetValue(current, out var neighbors) || neighbors.Count == 0)
                    break;

                PointKey? next = null;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var candidate = neighbors[i];
                    if (candidate.Equals(prev))
                        continue;

                    var e = new EdgeKey(current, candidate);
                    if (!visitedEdges.Contains(e))
                    {
                        next = candidate;
                        break;
                    }
                }

                if (!next.HasValue)
                {
                    // If all outgoing edges are visited, try closing directly to start.
                    if (neighbors.Contains(start))
                        next = start;
                    else
                        break;
                }

                prev = current;
                current = next.Value;
            }

            return loop;
        }

        private static float NormalizeAndClamp(float value, float min, float max)
        {
            float range = max - min;
            if (range <= 0) return 0f;
            return Mathf.Clamp01((value - min) / range);
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        private static void GetTileBounds(IEnumerable<Vector3Int> cells, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            foreach (var cell in cells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }
        }

        private static IEnumerable<Vector3Int> GetHexNeighbors(Vector3Int cell)
        {
            bool isEvenRow = cell.y % 2 == 0;
            if (isEvenRow)
            {
                yield return cell + new Vector3Int(1, 0, 0);
                yield return cell + new Vector3Int(-1, 0, 0);
                yield return cell + new Vector3Int(0, 1, 0);
                yield return cell + new Vector3Int(-1, 1, 0);
                yield return cell + new Vector3Int(0, -1, 0);
                yield return cell + new Vector3Int(-1, -1, 0);
                yield break;
            }

            yield return cell + new Vector3Int(1, 0, 0);
            yield return cell + new Vector3Int(-1, 0, 0);
            yield return cell + new Vector3Int(1, 1, 0);
            yield return cell + new Vector3Int(0, 1, 0);
            yield return cell + new Vector3Int(1, -1, 0);
            yield return cell + new Vector3Int(0, -1, 0);
        }

        private static (Vector2 a, Vector2 b) GetSharedEdgePoints(Vector3Int cellA, Vector3Int cellB)
        {
            var centerA = GetCellCenter(cellA);
            var centerB = GetCellCenter(cellB);

            var dir = (centerB - centerA).normalized;
            var mid = (centerA + centerB) * 0.5f;
            var perp = new Vector2(-dir.y, dir.x);

            float centerDist = Vector2.Distance(centerA, centerB);
            float edgeHalf = centerDist / (2f * Mathf.Sqrt(3f));

            return (mid + perp * edgeHalf, mid - perp * edgeHalf);
        }

        private static Vector2 GetCellCenter(Vector3Int cell)
        {
            float x = cell.x + ((cell.y & 1) != 0 ? 0.5f : 0f);
            float y = cell.y * 0.8660254037844386f; // sqrt(3)/2
            return new Vector2(x, y);
        }

        private static void AddAdjacency(Dictionary<PointKey, List<PointKey>> adjacency, PointKey from, PointKey to)
        {
            if (!adjacency.TryGetValue(from, out var list))
            {
                list = new List<PointKey>(2);
                adjacency[from] = list;
            }

            if (!list.Contains(to))
                list.Add(to);
        }

        private readonly struct PointKey : IEquatable<PointKey>
        {
            private readonly int x;
            private readonly int y;

            public PointKey(Vector2 p)
            {
                x = Mathf.RoundToInt(p.x * 1000f);
                y = Mathf.RoundToInt(p.y * 1000f);
            }

            public bool Equals(PointKey other) => x == other.x && y == other.y;
            public override bool Equals(object obj) => obj is PointKey other && Equals(other);
            public override int GetHashCode() => (x * 397) ^ y;
            public bool IsBefore(PointKey other) => x < other.x || (x == other.x && y < other.y);
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly PointKey a;
            private readonly PointKey b;

            public EdgeKey(PointKey p1, PointKey p2)
            {
                // Canonical order for undirected edge
                if (p1.IsBefore(p2))
                {
                    a = p1;
                    b = p2;
                }
                else
                {
                    a = p2;
                    b = p1;
                }
            }

            public bool Equals(EdgeKey other) => a.Equals(other.a) && b.Equals(other.b);
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => (a.GetHashCode() * 397) ^ b.GetHashCode();
        }

        private void SetOutput(string text)
        {
            if (outputField != null)
                outputField.SetValueWithoutNotify(text);
        }

        public override bool PaintTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            return false;
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCall)
        {
        }

        public override void SetCampaign()
        {
        }
    }
}
