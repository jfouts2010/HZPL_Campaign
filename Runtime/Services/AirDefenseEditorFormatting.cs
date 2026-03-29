using System;
using System.Collections.Generic;
using System.Linq;
using Models.Gameplay.Campaign;
using ScriptableObjects.Gameplay.Units;

namespace Models.CampaignEditor
{
    internal static class AirDefenseEditorFormatting
    {
        public static string FormatNetworkRoles(AirDefenseNetworkRole roles)
        {
            if (roles == AirDefenseNetworkRole.None)
                return "None";

            return string.Join(", ",
                Enum.GetValues(typeof(AirDefenseNetworkRole))
                    .Cast<AirDefenseNetworkRole>()
                    .Where(role => role != AirDefenseNetworkRole.None && roles.HasFlag(role))
                    .Select(role => role switch
                    {
                        AirDefenseNetworkRole.CommandAndControl => "C2",
                        _ => role.ToString()
                    }));
        }

        public static string FormatGuidQuantityMap(IReadOnlyDictionary<Guid, int> values)
        {
            if (values == null || values.Count == 0)
                return "None";

            return string.Join(", ",
                values
                    .Where(entry => entry.Value > 0)
                    .OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key)
                    .Select(entry => $"{entry.Value}x {FormatShortGuid(entry.Key)}"));
        }

        public static string FormatGuidCollection(IReadOnlyCollection<Guid> values)
        {
            if (values == null || values.Count == 0)
                return "None";

            return string.Join(", ", values.OrderBy(value => value).Select(FormatShortGuid));
        }

        public static string FormatMobileAirDefenseSummary(DivisionTemplateMobileAirDefenseStats stats)
        {
            if (stats == null || !stats.HasCapability)
                return "No mobile air defense capability.";

            return
                $"Contributing battalions: {stats.ContributingBattalionCount}\n" +
                $"Roles: {FormatNetworkRoles(stats.NetworkRoles)}\n" +
                $"Launchers: {stats.TotalLauncherCount} | Channels: {stats.TotalChannelCount}\n" +
                $"Detect / Engage: {stats.BestDetectionRangeKm:0.#} / {stats.BestEngagementRangeKm:0.#} km\n" +
                $"Network quality: {stats.TotalNetworkQualityContribution:0.#} | Participation range: {stats.MaxNetworkParticipationRangeKm:0.#} km\n" +
                $"Radar quality: {stats.BestRadarQuality:0.#}";
        }

        public static string FormatResolvedStaticSiteSummary(ResolvedStaticAirDefenseSiteDefinition resolved)
        {
            if (resolved?.Definition == null)
                return "No resolved site data.";

            return
                $"Roles: {FormatNetworkRoles(resolved.NetworkRoles)}\n" +
                $"Base network quality: {resolved.BaseNetworkQuality:0.#}\n" +
                $"Participation range: {resolved.NetworkParticipationRangeKm:0.#} km\n" +
                $"Shooter channels: {resolved.InitialShooterChannels}\n" +
                $"Radar profiles: {FormatGuidCollection(resolved.RadarProfileIds)}";
        }

        public static string FormatShortGuid(Guid id)
        {
            if (id == Guid.Empty)
                return "(none)";

            return id.ToString("N").Substring(0, 8);
        }
    }
}
