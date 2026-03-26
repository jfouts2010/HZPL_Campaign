using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using Monobehaviours.UI;
using ScriptableObjects.Gameplay.Units;
using UnityEngine;

namespace Models.Gameplay
{
    public class EditorDivisionManager
    {
        private sealed class EditorDivisionRecord
        {
            public Guid TemplateID { get; }
            public Guid CountryID { get; }
            public Alliance Alliance { get; }
            public Vector3Int Position { get; }

            public EditorDivisionRecord(DivisionTemplate divisionTemplate, Alliance alliance, Vector3Int position)
            {
                TemplateID = divisionTemplate.ID;
                CountryID = divisionTemplate.CountryID;
                Alliance = alliance;
                Position = position;
            }
        }

        private readonly List<EditorDivisionRecord> _allDivisions = new List<EditorDivisionRecord>();
        private readonly Dictionary<Vector3Int, GameObject> _spawnedSprites = new Dictionary<Vector3Int, GameObject>();

        private TilemapEditor _editor;
        private GameObject _landUnitPrefab;

        public void Initialize(TilemapEditor editor)
        {
            if (_editor == editor)
                return;

            _editor = editor;
            _landUnitPrefab = Resources.Load<GameObject>("LandUnit");
            ClearRuntimeState();
        }

        public void Rebuild(Models.Gameplay.Campaign.Campaign campaign)
        {
            ClearRuntimeState();

            if (campaign?.unitSpawnPoints == null)
                return;

            var templatesById = (campaign.divisionTemplates ?? new List<DivisionTemplate>())
                .Where(template => template != null)
                .GroupBy(template => template.ID)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var spawn in campaign.unitSpawnPoints)
            {
                if (spawn == null)
                    continue;

                if (!templatesById.TryGetValue(spawn.TemplateID, out var template))
                    continue;

                SpawnDivisionInternal(template, ResolveAlliance(campaign, spawn, template), spawn.Position);
            }

            GenerateMapUnitSprites();
        }

        public void DeleteAllCountryUnits(Guid countryID)
        {
            if (_allDivisions.RemoveAll(division => division.CountryID == countryID) > 0)
                GenerateMapUnitSprites();
        }

        public void DeleteAllTemplateUnits(Guid templateID)
        {
            if (_allDivisions.RemoveAll(division => division.TemplateID == templateID) > 0)
                GenerateMapUnitSprites();
        }

        public void SpawnDivision(DivisionTemplate divisionTemplate, Alliance alliance, Vector3Int pos)
        {
            if (divisionTemplate == null)
                return;

            SpawnDivisionInternal(divisionTemplate, alliance, pos);
            GenerateMapUnitSprites();
        }

        public void DeleteAllUnitsOnCell(Vector3Int pos)
        {
            if (_allDivisions.RemoveAll(division => division.Position == pos) > 0)
                GenerateMapUnitSprites();
        }

        public void ClearRuntimeState()
        {
            _allDivisions.Clear();
            ClearSpawnedSprites();
        }

        private void SpawnDivisionInternal(DivisionTemplate divisionTemplate, Alliance alliance, Vector3Int pos)
        {
            _allDivisions.Add(new EditorDivisionRecord(divisionTemplate, alliance, pos));
        }

        private void GenerateMapUnitSprites()
        {
            ClearSpawnedSprites();

            if (_editor == null || _editor.tilemapManager == null || _landUnitPrefab == null)
                return;

            var tileData = _editor.editingCampaign?.tileData;
            if (tileData == null)
                return;

            foreach (var divisionsOnTile in _allDivisions.GroupBy(division => division.Position))
            {
                if (!tileData.TryGetValue(divisionsOnTile.Key, out var cellData) || !cellData.LandTile)
                    continue;

                var sprite = UnityEngine.Object.Instantiate(_landUnitPrefab, _editor.tilemapManager.transform);
                sprite.transform.position = _editor.tilemapManager.GetCellCenterWorld(divisionsOnTile.Key);
                _spawnedSprites[divisionsOnTile.Key] = sprite;

                var spriteManager = sprite.GetComponent<UnitSpriteManager>();
                if (spriteManager != null)
                    spriteManager.UpdateBattalionInfo(divisionsOnTile.Count(), 1f, 1f);
            }
        }

        private void ClearSpawnedSprites()
        {
            foreach (var sprite in _spawnedSprites.Values)
            {
                if (sprite != null)
                    UnityEngine.Object.Destroy(sprite);
            }

            _spawnedSprites.Clear();
        }

        private static Alliance ResolveAlliance(Models.Gameplay.Campaign.Campaign campaign, UnitSpawn spawn,
            DivisionTemplate template)
        {
            if (campaign?.CountryAlliance != null)
            {
                if (spawn.CountryID != Guid.Empty &&
                    campaign.CountryAlliance.TryGetValue(spawn.CountryID, out var spawnAlliance))
                {
                    return spawnAlliance;
                }

                if (template.CountryID != Guid.Empty &&
                    campaign.CountryAlliance.TryGetValue(template.CountryID, out var templateAlliance))
                {
                    return templateAlliance;
                }
            }

            return Alliance.Neutral;
        }
    }
}
