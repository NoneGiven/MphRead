using System;
using System.Collections.Frozen;
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

        public static FrozenDictionary<string, string> Commit()
        {
            return Frozen.Create<string, string>(
            [
                new(nameof(SmoothCamSeqHandoff), SmoothCamSeqHandoff.ToString().ToLower()),
                new(nameof(BetterCamSeqNodeRef), BetterCamSeqNodeRef.ToString().ToLower()),
                new(nameof(NoStrayRespawnText), NoStrayRespawnText.ToString().ToLower()),
                new(nameof(CorrectBountySfx), CorrectBountySfx.ToString().ToLower()),
                new(nameof(NoDoubleEnemyDeath), NoDoubleEnemyDeath.ToString().ToLower()),
                new(nameof(NoSlenchRollTimerUnderflow), NoSlenchRollTimerUnderflow.ToString().ToLower())
            ]);
        }
    }

    public static class Features
    {
        // These below are read all over gameplay code, but no longer have a
        // settings-page control (the generic "Features" toggle page is
        // gone) -- they stay at these code defaults forever now, which is
        // what leaving them here (rather than only in Load/Commit) means:
        // still real gameplay tuning, just no longer player-configurable.
        public static bool NoRepeatEncounters { get; set; } = false; // false
        public static bool AllowInvalidTeams { get; set; } = true; // false
        public static bool TopScreenTargetInfo { get; set; } = true;  // "true"
        public static bool HudSway { get; set; } = true; // true
        public static bool TargetInfoSway { get; set; } = false; // "false"
        public static bool DelayedIdleSway { get; set; } = true; // false
        public static bool NoIdleSway { get; set; } = false; // false
        public static bool NoMapCentering { get; set; } = false; // false
        public static bool MaxRoomDetail { get; set; } = false; // false
        public static bool MaxPlayerDetail { get; set; } = true; // false
        public static bool LogSpatialAudio { get; set; } = false; // false
        public static bool HalfSecondAlarm { get; set; } = false; // false
        public static bool FullBoostCharge { get; set; } = false; // false
        public static bool BoostOpensDoors { get; set; } = false; // false
        public static bool AlternateHunters1P { get; set; } = true; // false

        /// <summary>
        /// Pro mode HUD: the whole competitive layout as one switch, instead
        /// of the seven separate questions below that had to be answered the
        /// same way to get it.
        ///
        /// It is not a preset that copies values into those settings -- it
        /// overrides them while it is on, and every one of them falls back to
        /// its code default when it goes off -- which is the game as the DS
        /// drew it. Neither state is a set of choices the player has to
        /// assemble: none of the six has a row in the settings any more, and
        /// none of them is written to settings.json. One switch decides the
        /// HUD, and the six ways of ending up somewhere between the two
        /// answers went with the rows that offered them.
        ///
        /// Turning off the helmet is part of it: the shell and the visor pane
        /// are what the readouts are drawn into, and the point of this mode is
        /// that the middle of the screen is the game.
        /// </summary>
        public static bool ProHud { get; set; } = false;

        /// <summary>
        /// How big the weapon list is drawn under <see cref="ProHud"/>.
        /// Large enough to read without looking straight at it, which is the
        /// whole job of that column.
        /// </summary>
        public const float ProHudWeaponListScale = 1.7f;

        // These, below, read as what the game should actually draw: Pro
        // mode's answer while that is on, the field's own default otherwise.
        // The setters are still here and still used -- `-nohelmet` writes two
        // of them, and upstream's console menu writes several -- they simply
        // have no launcher control and no line in settings.json any more.
        public static float HelmetOpacity
        {
            get => ProHud ? 0 : _helmetOpacity;
            set => _helmetOpacity = value;
        }

        private static float _helmetOpacity = 1; // 1

        public static float VisorOpacity
        {
            get => ProHud ? 0 : _visorOpacity;
            set => _visorOpacity = value;
        }

        private static float _visorOpacity = 0.5f; // 0.5

        /// <summary>
        /// How solid the readouts are drawn. No longer a setting: a HUD you
        /// have dimmed to a third is a HUD you cannot read in a fight, and it
        /// was one slider's worth of a question nobody was better off being
        /// asked. Still read all over the HUD code, and still the thing to
        /// change if a screen ever needs to fade the readouts out.
        /// </summary>
        public static float HudOpacity { get; set; } = 1; // 1
        public static float ReticleOpacity { get; set; } = 1; // 1

        /// <summary>Freezes the reticle's fire animation instead of letting it shrink and expand.</summary>
        public static bool FixedCrosshair
        {
            get => ProHud || _fixedCrosshair;
            set => _fixedCrosshair = value;
        }

        private static bool _fixedCrosshair = false;

        /// <summary>Replaces the reticle with a flat-coloured cross, coloured by current HP.</summary>
        public static bool CustomCrosshair
        {
            get => ProHud || _customCrosshair;
            set => _customCrosshair = value;
        }

        private static bool _customCrosshair = false;

        /// <summary>The acquired-weapons-and-ammo panel down the left of the HUD.</summary>
        public static bool ModernHud
        {
            get => ProHud || _modernHud;
            set => _modernHud = value;
        }

        private static bool _modernHud = false;

        /// <summary>
        /// How big that panel is drawn, as a multiplier on every one of its
        /// measurements -- row height, panel width, icon and ammo text.
        ///
        /// It exists because the right size for it is a matter of eyesight and
        /// screen, not of design: the same panel that is unreadable on a
        /// handheld is in the way on a monitor. 1.0 is the compact default.
        /// </summary>
        public static float WeaponListScale
        {
            get => ProHud ? ProHudWeaponListScale : _weaponListScale;
            set => _weaponListScale = value;
        }

        private static float _weaponListScale = 1f;

        /// <summary>Rides rigidly with the camera instead of lagging behind aim, and stops the mouse-driven HUD shift too -- Quake's static weapon.</summary>
        public static bool FixedWeapon
        {
            get => ProHud || _fixedWeapon;
            set => _fixedWeapon = value;
        }

        private static bool _fixedWeapon = false;

        public static void Load(IReadOnlyDictionary<string, string> values)
        {
            if (values.TryGetValue(nameof(ReticleOpacity), out string? value)
                && Single.TryParse(value, CultureInfo.InvariantCulture, out float single))
            {
                ReticleOpacity = single;
            }
            if (values.TryGetValue(nameof(ProHud), out value) && Boolean.TryParse(value, out bool boolean))
            {
                ProHud = boolean;
            }
            // Pro mode's crosshair: which shape, and how big. Both persist,
            // because a crosshair is a thing a player picks once and then does
            // not want to think about again.
            if (values.TryGetValue("CrosshairStyle", out value))
            {
                Mods.Render.Crosshair.Style = Mods.Render.Crosshair.ParseStyle(value,
                    Mods.Render.Crosshair.Style);
            }
            if (values.TryGetValue("CrosshairSize", out value))
            {
                Mods.Render.Crosshair.Size = Mods.Render.Crosshair.ParseSize(value,
                    Mods.Render.Crosshair.Size);
            }
        }

        /// <summary>
        /// What the launcher can still be asked about, which is one switch.
        ///
        /// Everything else here was reachable either through the generic
        /// reflection-built "Features" page or through a Display-page row of
        /// its own, and both are gone: the HUD is <see cref="ProHud"/>'s
        /// decision and the rest sit at their code defaults. What is left out
        /// of this is left out of <see cref="Load"/> too, so an old
        /// settings.json cannot go on answering a question nobody is asked
        /// any more.
        /// </summary>
        public static FrozenDictionary<string, string> Commit()
        {
            return Frozen.Create<string, string>(
            [
                new(nameof(ReticleOpacity), ReticleOpacity.ToString(CultureInfo.InvariantCulture)),
                new(nameof(ProHud), ProHud.ToString().ToLower()),
                new("CrosshairStyle", Mods.Render.Crosshair.Style.ToString()),
                new("CrosshairSize", Mods.Render.Crosshair.Size.ToString())
            ]);
        }
    }

    public static class Cheats
    {
        public static bool FreeWeaponSelect { get; set; } = false;
        public static bool UnlimitedJumps { get; set; } = false;
        public static bool NoRandomEncounters { get; set; } = false;
        public static bool UnlockAllDoors { get; set; } = false;
        public static bool ContinueFromCurrentRoom { get; set; } = false;
        public static bool SkipPlanetIntros { get; set; } = false;
        public static bool StartWithAllUpgrades { get; set; } = false;
        public static bool StartWithAllOctoliths { get; set; } = false;
        public static bool WalkThroughWalls { get; set; } = false;
        public static bool AlwaysFightGorea2 { get; set; } = false;
        public static bool QuadrupleDamage { get; set; } = false;

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
            if (values.TryGetValue(nameof(SkipPlanetIntros), out value) && Boolean.TryParse(value, out boolean))
            {
                SkipPlanetIntros = boolean;
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
            if (values.TryGetValue(nameof(AlwaysFightGorea2), out value) && Boolean.TryParse(value, out boolean))
            {
                AlwaysFightGorea2 = boolean;
            }
            if (values.TryGetValue(nameof(QuadrupleDamage), out value) && Boolean.TryParse(value, out boolean))
            {
                QuadrupleDamage = boolean;
            }
        }

        public static FrozenDictionary<string, string> Commit()
        {
            return Frozen.Create<string, string>(
            [
                new(nameof(FreeWeaponSelect), FreeWeaponSelect.ToString().ToLower()),
                new(nameof(UnlimitedJumps), UnlimitedJumps.ToString().ToLower()),
                new(nameof(NoRandomEncounters), NoRandomEncounters.ToString()),
                new(nameof(UnlockAllDoors), UnlockAllDoors.ToString()),
                new(nameof(ContinueFromCurrentRoom), ContinueFromCurrentRoom.ToString()),
                new(nameof(SkipPlanetIntros), SkipPlanetIntros.ToString()),
                new(nameof(StartWithAllUpgrades), StartWithAllUpgrades.ToString()),
                new(nameof(StartWithAllOctoliths), StartWithAllOctoliths.ToString().ToLower()),
                new(nameof(WalkThroughWalls), WalkThroughWalls.ToString().ToLower()),
                new(nameof(AlwaysFightGorea2), AlwaysFightGorea2.ToString().ToLower()),
                new(nameof(QuadrupleDamage), QuadrupleDamage.ToString().ToLower())
            ]);
        }
    }
}
