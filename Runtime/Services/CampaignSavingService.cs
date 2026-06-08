using System.IO;
using Models.Gameplay.Campaign;
using Newtonsoft.Json;
using UnityEngine;

namespace Services
{
    public static class CampaignSavingService
    {
        public static void SaveCampaign(CampaignTemplate campaign, string filePath = null)
        {
            if (campaign == null)
            {
                Debug.LogError("Cannot save null campaign.");
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.LogError("Cannot save CampaignTemplate without a file path.");
                return;
            }
            
            CampaignTemplateHashService.ApplyHash(campaign);
            string json = JsonConvert.SerializeObject(campaign, CampaignTemplateHashService.Settings);
            File.WriteAllText(filePath, json);

            Debug.Log($"CampaignTemplate saved to: {filePath}");
        }
        public static CampaignTemplate LoadCampaign(string fileName)
        {
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                var campaign = JsonConvert.DeserializeObject<CampaignTemplate>(json, CampaignTemplateHashService.Settings);
                return campaign;
            }
            return null;
        }

        public static bool TryReadCampaignMetadata(string fileName, out CampaignTemplateMetadata metadata)
        {
            metadata = null;
            if (!File.Exists(fileName))
                return false;

            metadata = new CampaignTemplateMetadata();

            using var fileReader = File.OpenText(fileName);
            using var jsonReader = new JsonTextReader(fileReader);

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType != JsonToken.PropertyName)
                    continue;

                var propertyName = jsonReader.Value as string;
                if (string.IsNullOrEmpty(propertyName) || !jsonReader.Read())
                    continue;

                switch (propertyName)
                {
                    case nameof(CampaignTemplate.ModuleId):
                        metadata.ModuleId = jsonReader.Value as string;
                        break;
                    case nameof(CampaignTemplate.ContentHash):
                        metadata.ContentHash = jsonReader.Value as string;
                        break;
                    case nameof(CampaignTemplate.CampaignStartTime):
                        if (jsonReader.Value is System.DateTime startTime)
                            metadata.CampaignStartTime = startTime;
                        break;
                    default:
                        jsonReader.Skip();
                        break;
                }

                if (metadata.HasRequiredFields)
                    return true;
            }

            return metadata.HasRequiredFields;
        }
    }

    public sealed class CampaignTemplateMetadata
    {
        public string ModuleId;
        public string ContentHash;
        public System.DateTime? CampaignStartTime;

        public bool HasRequiredFields => !string.IsNullOrWhiteSpace(ModuleId);
    }
}
