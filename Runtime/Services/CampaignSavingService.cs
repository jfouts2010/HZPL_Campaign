using System.IO;
using Models.Gameplay.Campaign;
using Newtonsoft.Json;
using UnityEngine;

namespace Services
{
    public static class CampaignSavingService
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new Vector3IntDictionaryConverter() },
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };
        
        public static void SaveCampaign(Campaign campaign, string filePath = null)
        {
            if (campaign == null)
            {
                Debug.LogError("Cannot save null campaign.");
                return;
            }

            string json = JsonConvert.SerializeObject(campaign, Settings);
            File.WriteAllText(filePath, json);

            Debug.Log($"Campaign saved to: {filePath}");
        }
        public static Campaign LoadCampaign(string fileName)
        {
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);
                return JsonConvert.DeserializeObject<Campaign>(json, Settings);
            }
            return null;
        }
    }
}