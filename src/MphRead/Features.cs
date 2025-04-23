using System;
using System.Collections.Generic;
using System.Globalization;

namespace MphRead
{
    public static class Bugfixes
    {
        public static bool SmoothCamSeqHandoff { get; set; } = false;
        public static bool BetterCamSeqNodeRef { get; set; } = true;
        public static bool NoStrayRespawnText { get; set; } = false;
        public static bool CorrectBountySfx { get; set; } = true;
        public static bool NoDoubleEnemyDeath { get; set; } = true;
        public static bool NoSlenchRollTimerUnderflow { get; set; } = true;

        public static void Load(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(nameof(SmoothCamSeqHandoff), out string? value) && Boolean.TryParse(value, out bool result))
            {
                SmoothCamSeqHandoff = result;
            }
            if (values.TryGetValue(nameof(BetterCamSeqNodeRef), out value) && Boolean.TryParse(value, out result))
            {
                BetterCamSeqNodeRef = result;
            }
            if (values.TryGetValue(nameof(NoStrayRespawnText), out value) && Boolean.TryParse(value, out result))
            {
                NoStrayRespawnText = result;
            }
            if (values.TryGetValue(nameof(CorrectBountySfx), out value) && Boolean.TryParse(value, out result))
            {
                CorrectBountySfx = result;
            }
            if (values.TryGetValue(nameof(NoDoubleEnemyDeath), out value) && Boolean.TryParse(value, out result))
            {
                NoDoubleEnemyDeath = result;
            }
            if (values.TryGetValue(nameof(NoSlenchRollTimerUnderflow), out value) && Boolean.TryParse(value, out result))
            {
                NoSlenchRollTimerUnderflow = result;
            }
        }

        public static IReadOnlyDictionary<string, string> Commit()
        {
            return new Dictionary<string, string>()
            {
                { nameof(SmoothCamSeqHandoff), SmoothCamSeqHandoff.ToString().ToLower() },
                { nameof(BetterCamSeqNodeRef), BetterCamSeqNodeRef.ToString().ToLower() },
                { nameof(NoStrayRespawnText), NoStrayRespawnText.ToString().ToLower() },
                { nameof(CorrectBountySfx), CorrectBountySfx.ToString().ToLower() },
                { nameof(NoDoubleEnemyDeath), NoDoubleEnemyDeath.ToString().ToLower() },
                { nameof(NoSlenchRollTimerUnderflow), NoSlenchRollTimerUnderflow.ToString().ToLower() }
            };
        }
    }

    public static class Features
    {
        public static bool NoRepeatEncounters { get; set; } = false; // false
        public static bool AllowInvalidTeams { get; set; } = true; // false
        public static bool TopScreenTargetInfo { get; set; } = true;  // "true"
        public static float HelmetOpacity { get; set; } = 1; // 1
        public static float VisorOpacity { get; set; } = 0.5f; // 0.5
        public static float HudOpacity { get; set; } = 1; // 1
        public static float ReticleOpacity { get; set; } = 1; // 1
        public static bool HudSway { get; set; } = true; // true
        public static bool TargetInfoSway { get; set; } = false; // "false"
        public static bool MaxRoomDetail { get; set; } = false; // false
        public static bool MaxPlayerDetail { get; set; } = true; // false
        public static bool LogSpatialAudio { get; set; } = false; // false
        public static bool HalfSecondAlarm { get; set; } = false; // false
        public static bool FullBoostCharge { get; set; } = false; // false
        public static bool AlternateHunters1P { get; set; } = true; // false

        public static void Load(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(nameof(NoRepeatEncounters), out string? value) && Boolean.TryParse(value, out bool boolean))
            {
                NoRepeatEncounters = boolean;
            }
            if (values.TryGetValue(nameof(AllowInvalidTeams), out value) && Boolean.TryParse(value, out boolean))
            {
                AllowInvalidTeams = boolean;
            }
            if (values.TryGetValue(nameof(TopScreenTargetInfo), out value) && Boolean.TryParse(value, out boolean))
            {
                TopScreenTargetInfo = boolean;
            }
            if (values.TryGetValue(nameof(HelmetOpacity), out value) && Single.TryParse(value, CultureInfo.InvariantCulture, out float single))
            {
                HelmetOpacity = single;
            }
            if (values.TryGetValue(nameof(VisorOpacity), out value) && Single.TryParse(value, CultureInfo.InvariantCulture, out single))
            {
                VisorOpacity = single;
            }
            if (values.TryGetValue(nameof(HudOpacity), out value) && Single.TryParse(value, CultureInfo.InvariantCulture, out single))
            {
                HudOpacity = single;
            }
            if (values.TryGetValue(nameof(ReticleOpacity), out value) && Single.TryParse(value, CultureInfo.InvariantCulture, out single))
            {
                ReticleOpacity = single;
            }
            if (values.TryGetValue(nameof(HudSway), out value) && Boolean.TryParse(value, out boolean))
            {
                HudSway = boolean;
            }
            if (values.TryGetValue(nameof(TargetInfoSway), out value) && Boolean.TryParse(value, out boolean))
            {
                TargetInfoSway = boolean;
            }
            if (values.TryGetValue(nameof(MaxRoomDetail), out value) && Boolean.TryParse(value, out boolean))
            {
                MaxRoomDetail = boolean;
            }
            if (values.TryGetValue(nameof(MaxPlayerDetail), out value) && Boolean.TryParse(value, out boolean))
            {
                MaxPlayerDetail = boolean;
            }
            if (values.TryGetValue(nameof(LogSpatialAudio), out value) && Boolean.TryParse(value, out boolean))
            {
                LogSpatialAudio = boolean;
            }
            if (values.TryGetValue(nameof(HalfSecondAlarm), out value) && Boolean.TryParse(value, out boolean))
            {
                HalfSecondAlarm = boolean;
            }
            if (values.TryGetValue(nameof(FullBoostCharge), out value) && Boolean.TryParse(value, out boolean))
            {
                FullBoostCharge = boolean;
            }
            if (values.TryGetValue(nameof(AlternateHunters1P), out value) && Boolean.TryParse(value, out boolean))
            {
                AlternateHunters1P = boolean;
            }
        }

        public static IReadOnlyDictionary<string, string> Commit()
        {
            return new Dictionary<string, string>()
            {
                { nameof(NoRepeatEncounters), NoRepeatEncounters.ToString().ToLower() },
                { nameof(AllowInvalidTeams), AllowInvalidTeams.ToString().ToLower() },
                { nameof(TopScreenTargetInfo), TopScreenTargetInfo.ToString().ToLower() },
                { nameof(HelmetOpacity), HelmetOpacity.ToString(CultureInfo.InvariantCulture) },
                { nameof(VisorOpacity), VisorOpacity.ToString(CultureInfo.InvariantCulture) },
                { nameof(HudOpacity), HudOpacity.ToString(CultureInfo.InvariantCulture) },
                { nameof(ReticleOpacity), ReticleOpacity.ToString(CultureInfo.InvariantCulture) },
                { nameof(HudSway), HudSway.ToString().ToLower() },
                { nameof(TargetInfoSway), TargetInfoSway.ToString().ToLower() },
                { nameof(MaxRoomDetail), MaxRoomDetail.ToString().ToLower() },
                { nameof(MaxPlayerDetail), MaxPlayerDetail.ToString().ToLower() },
                { nameof(LogSpatialAudio), LogSpatialAudio.ToString().ToLower() },
                { nameof(HalfSecondAlarm), HalfSecondAlarm.ToString().ToLower() },
                { nameof(FullBoostCharge), FullBoostCharge.ToString().ToLower() },
                { nameof(AlternateHunters1P), AlternateHunters1P.ToString().ToLower() }
            };
        }
    }

    public static class Cheats
    {
        public static bool FreeWeaponSelect { get; set; } = true;
        public static bool UnlimitedJumps { get; set; } = false;
        public static bool NoRandomEncounters { get; set; } = false;
        public static bool UnlockAllDoors { get; set; } = false;
        public static bool ContinueFromCurrentRoom { get; set; } = false;
        public static bool StartWithAllUpgrades { get; set; } = false;
        public static bool StartWithAllOctoliths { get; set; } = false;
        public static bool WalkThroughWalls { get; set; } = false;

        public static void Load(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(nameof(FreeWeaponSelect), out string? value) && Boolean.TryParse(value, out bool boolean))
            {
                FreeWeaponSelect = boolean;
            }
            if (values.TryGetValue(nameof(UnlimitedJumps), out value) && Boolean.TryParse(value, out boolean))
            {
                UnlimitedJumps = boolean;
            }
            if (values.TryGetValue(nameof(NoRandomEncounters), out value) && Boolean.TryParse(value, out boolean))
            {
                NoRandomEncounters = boolean;
            }
            if (values.TryGetValue(nameof(UnlockAllDoors), out value) && Boolean.TryParse(value, out boolean))
            {
                UnlockAllDoors = boolean;
            }
            if (values.TryGetValue(nameof(ContinueFromCurrentRoom), out value) && Boolean.TryParse(value, out boolean))
            {
                ContinueFromCurrentRoom = boolean;
            }
            if (values.TryGetValue(nameof(StartWithAllUpgrades), out value) && Boolean.TryParse(value, out boolean))
            {
                StartWithAllUpgrades = boolean;
            }
            if (values.TryGetValue(nameof(StartWithAllOctoliths), out value) && Boolean.TryParse(value, out boolean))
            {
                StartWithAllOctoliths = boolean;
            }
            if (values.TryGetValue(nameof(WalkThroughWalls), out value) && Boolean.TryParse(value, out boolean))
            {
                WalkThroughWalls = boolean;
            }
        }

        public static IReadOnlyDictionary<string, string> Commit()
        {
            return new Dictionary<string, string>()
            {
                { nameof(FreeWeaponSelect), FreeWeaponSelect.ToString().ToLower() },
                { nameof(UnlimitedJumps), UnlimitedJumps.ToString().ToLower() },
                { nameof(NoRandomEncounters), NoRandomEncounters.ToString() },
                { nameof(UnlockAllDoors), UnlockAllDoors.ToString() },
                { nameof(ContinueFromCurrentRoom), ContinueFromCurrentRoom.ToString() },
                { nameof(StartWithAllUpgrades), StartWithAllUpgrades.ToString() },
                { nameof(StartWithAllOctoliths), StartWithAllOctoliths.ToString().ToLower() },
                { nameof(WalkThroughWalls), WalkThroughWalls.ToString().ToLower() }
            };
        }
    }
}
