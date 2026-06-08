using System.IO;
using Models.Gameplay.Campaign;
using Newtonsoft.Json;
using UnityEngine;

namespace Services
{
    public static class CampaignSavingService
    {
        public static void SaveCampaign(Campaign campaign, string filePath = null)
        {
            if (campaign == null)
            {
                Debug.LogError("Cannot save null campaign.");
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                Debug.LogError("Cannot save campaign without a file path.");
                return;
            }
            
            CampaignTemplateHashService.ApplyHash(campaign);
            string json = JsonConvert.SerializeObject(campaign, CampaignTemplateHashService.Settings);
            File.WriteAllText(filePath, json);

            Debug.Log($"Campaign saved to: {filePath}");
        }
        public static Campaign LoadCampaign(string fileName)
        {
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                var campaign = JsonConvert.DeserializeObject<Campaign>(json, CampaignTemplateHashService.Settings);
                return campaign;
            }
            return null;
        }
    }
}
