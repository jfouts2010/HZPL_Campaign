using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Models.Gameplay.Campaign;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using Models.Gameplay.Campaign;
using UnityEngine;

namespace Services
{
    public static class CampaignTemplateHashService
    {
        // PRD cross-reference: docs/prd/campaign-template-runtime-refactor-prd.md
        // Template identity is SHA-256 over canonical JSON so paths and filenames are never authoritative.
        public const string AlgorithmDescription =
            "SHA-256 of UTF-8 canonical JSON with stable object field ordering, sorted tile dictionary entries, and root ContentHash excluded.";

        public static string ComputeHash(Campaign campaign)
        {
            if (campaign == null)
                throw new ArgumentNullException(nameof(campaign));

            campaign.EnsureAirDataInitialized();
            campaign.EnsureTemplateMetadataInitialized();

            var previousHash = campaign.ContentHash;
            campaign.ContentHash = string.Empty;

            try
            {
                var json = JsonConvert.SerializeObject(campaign, CampaignTemplateJsonSettings.Settings);
                var token = JToken.Parse(json);

                if (token is JObject root)
                    root.Remove(nameof(Campaign.ContentHash));

                var canonical = Canonicalize(token);
                var canonicalJson = canonical.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(canonicalJson);

                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(bytes);
                    return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            finally
            {
                campaign.ContentHash = previousHash ?? string.Empty;
            }
        }

        public static void ApplyHash(Campaign campaign)
        {
            if (campaign == null)
                throw new ArgumentNullException(nameof(campaign));

            campaign.ContentHash = ComputeHash(campaign);
        }

        public static bool IsHashCurrent(Campaign campaign)
        {
            if (campaign == null || string.IsNullOrWhiteSpace(campaign.ContentHash))
                return false;

            return string.Equals(campaign.ContentHash, ComputeHash(campaign), StringComparison.OrdinalIgnoreCase);
        }

        private static JToken Canonicalize(JToken token)
        {
            return token switch
            {
                JObject obj => CanonicalizeObject(obj),
                JArray array => CanonicalizeArray(array),
                JValue value => new JValue(value),
                _ => token.DeepClone()
            };
        }

        private static JObject CanonicalizeObject(JObject obj)
        {
            var canonical = new JObject();
            foreach (var property in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                canonical.Add(property.Name, Canonicalize(property.Value));

            return canonical;
        }

        private static JArray CanonicalizeArray(JArray array)
        {
            var canonicalItems = array.Select(Canonicalize).ToList();

            if (canonicalItems.All(IsVector3IntDictionaryEntry))
            {
                canonicalItems = canonicalItems
                    .OrderBy(GetVector3IntDictionaryEntryX)
                    .ThenBy(GetVector3IntDictionaryEntryY)
                    .ThenBy(GetVector3IntDictionaryEntryZ)
                    .ToList();
            }

            return new JArray(canonicalItems);
        }

        private static bool IsVector3IntDictionaryEntry(JToken token)
        {
            return token is JObject obj
                   && obj["key"] is JObject key
                   && key["x"] != null
                   && key["y"] != null
                   && key["z"] != null
                   && obj["value"] != null;
        }

        private static int GetVector3IntDictionaryEntryX(JToken token)
        {
            return token["key"]?["x"]?.Value<int>() ?? 0;
        }

        private static int GetVector3IntDictionaryEntryY(JToken token)
        {
            return token["key"]?["y"]?.Value<int>() ?? 0;
        }

        private static int GetVector3IntDictionaryEntryZ(JToken token)
        {
            return token["key"]?["z"]?.Value<int>() ?? 0;
        }
        public static bool TryFindByHash(string hash, out string filePath, out Campaign template)
        {
            return TryFindByHash(hash, Path.Combine(Application.persistentDataPath, "Campaigns"), out filePath, out template);
        }

        public static bool TryFindByHash(string hash, string campaignFolderPath, out string filePath, out Campaign template)
        {
            filePath = null;
            template = null;

            if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(campaignFolderPath) || !Directory.Exists(campaignFolderPath))
                return false;

            foreach (var candidatePath in Directory.GetFiles(campaignFolderPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                Campaign candidate;
                try
                {
                    candidate = CampaignSavingService.LoadCampaign(candidatePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CampaignTemplateIndex] Skipping unreadable template '{candidatePath}': {e.Message}");
                    continue;
                }

                if (candidate == null || string.IsNullOrWhiteSpace(candidate.ContentHash))
                    continue;

                if (!string.Equals(candidate.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!CampaignTemplateHashService.IsHashCurrent(candidate))
                {
                    Debug.LogWarning($"[CampaignTemplateIndex] Skipping template with stale content hash: {candidatePath}");
                    continue;
                }

                filePath = candidatePath;
                template = candidate;
                return true;
            }

            return false;
        }
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new Vector3IntDictionaryConverter() },
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto
        };
    }
}
