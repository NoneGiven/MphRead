using System;
using System.Globalization;
using MphRead.Sound;

namespace MphRead.Mods
{
    /// <summary>
    /// Makes the settings file mean something on the launcher's path.
    ///
    /// settings.json was only ever read into the engine by
    /// <c>Menu.ShowMenuPrompts</c>, which is the console menu. It parses the
    /// same file into a set of private statics and then hands those to the
    /// engine in two places -- volumes straight away, match rules from
    /// Renderer once the mode is known -- and it does the second only when a
    /// flag it sets itself is on.
    ///
    /// The launcher never runs any of that. It loads the file for its own
    /// screens, writes the file back when somebody presses save, and starts
    /// the game; so the music slider moved, the number was stored, and
    /// nothing on the machine ever read it. The same was true of the sound
    /// effects volume, the language, and every match rule: point goal, time
    /// limit, damage level, teams, friendly fire, hunter radar, affinity
    /// weapons. They were settings in the sense that they could be changed.
    ///
    /// This is the missing half, kept apart from Menu on purpose: Menu's
    /// state belongs to a console session that may never have started.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>
        /// The settings this process is playing under, once something has
        /// applied a set. Null when only the console menu has run, which owns
        /// its own copy and applies it its own way.
        /// </summary>
        public static MenuSettings? Current { get; private set; }

        /// <summary>
        /// Apply everything that can be applied the moment it changes: the
        /// two volumes and the text language.
        ///
        /// Called when the launcher loads the file and again whenever the
        /// settings window commits, which is what makes the music slider take
        /// effect while a match is running rather than at the next launch.
        /// </summary>
        public static void Apply(MenuSettings settings)
        {
            Current = settings;
            if (TryVolume(settings.SfxVolume, out float sfx))
            {
                Sfx.Volume = sfx;
            }
            if (TryVolume(settings.MusicVolume, out float music))
            {
                // Not a plain assignment: the gain only reaches the stream
                // when a track starts, so a volume set mid-match would not be
                // heard until the next one.
                Music.SetUserVolume(music);
            }
            if (Enum.TryParse(settings.Language, out Language language))
            {
                // Korean builds have no localised text of their own; the
                // console path makes the same substitution.
                Scene.Language = Paths.MphKey == "AMHK0" ? Language.Japanese : language;
            }
            // Read by the renderer as it builds each scene, so a change here
            // reaches the next match; the resolution scale reaches the current
            // one on its next resize.
            RenderOptions.ResolutionScale = RenderOptions.ParseScale(settings.ResolutionScale,
                RenderOptions.ResolutionScale);
            RenderOptions.Lighting = RenderOptions.ParseOnOff(settings.Lighting, RenderOptions.Lighting);
            RenderOptions.Fog = RenderOptions.ParseOnOff(settings.Fog, RenderOptions.Fog);
            RenderOptions.TextureFiltering = RenderOptions.ParseOnOff(settings.TextureFiltering,
                RenderOptions.TextureFiltering);
            RenderOptions.ShowFps = RenderOptions.ParseOnOff(settings.ShowFps, RenderOptions.ShowFps);
            // The picture's rate and whether the frames between simulation
            // steps are blended. Neither touches the simulation, which runs at
            // 60 Hz whatever these say -- see Mods/Render/FrameTiming.cs.
            Render.FrameTiming.FrameRateCap = Render.FrameTiming.ParseCap(settings.FrameRateCap,
                Render.FrameTiming.FrameRateCap);
            Render.FrameTiming.Interpolate = RenderOptions.ParseOnOff(settings.Interpolation,
                Render.FrameTiming.Interpolate);
            RenderOptions.CelShading = RenderOptions.ParseOnOff(settings.CelShading,
                RenderOptions.CelShading);
            // Steps and outline strength are no longer player-configurable --
            // locked at 8 steps / 50%, regardless of what an old settings.json
            // (from before this was locked down) still has saved.
            RenderOptions.CelBands = 8;
            RenderOptions.CelEdge = 0.5f;
        }

        /// <summary>
        /// Apply the match rules, after <see cref="GameState.Setup"/> has
        /// chosen the defaults for the mode.
        ///
        /// Order is the whole reason this is separate: Setup writes a point
        /// goal and a time limit derived from the mode, so anything applied
        /// before it is overwritten and anything applied instead of it would
        /// have to know every mode's defaults. The console menu's equivalent
        /// sits at the same call site for the same reason.
        ///
        /// A server's rules win over these. It publishes the point goal and
        /// the clock in its match state, which is adopted a few frames later
        /// -- the local numbers are what a client plays by until the server
        /// says otherwise, rather than a second opinion about a running
        /// match.
        /// </summary>
        public static void ApplyMatchRules()
        {
            MenuSettings? settings = Current;
            if (settings == null || !GameState.Multiplayer)
            {
                return;
            }
            if (TryTime(settings.TimeLimit, out float timeLimit) && timeLimit > 0)
            {
                GameState.MatchTime = timeLimit;
            }
            if (TryTime(settings.TimeGoal, out float timeGoal) && timeGoal > 0)
            {
                GameState.TimeGoal = timeGoal;
            }
            if (Int32.TryParse(settings.PointGoal, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int pointGoal) && pointGoal > 0)
            {
                GameState.PointGoal = pointGoal;
            }
            GameState.DamageLevel = settings.DamageLevel switch
            {
                "low" => 0,
                "high" => 2,
                "medium" => 1,
                _ => GameState.DamageLevel
            };
            GameState.FriendlyFire = settings.FriendlyFire == "on";
            GameState.RadarPlayers = settings.HunterRadar == "on";
            GameState.AffinityWeapons = settings.AffinityWeapons == "on";
            GameState.OctolithReset = settings.PointGoal != "off";
            // Teams is not set here. GameState.Setup derives it from the mode,
            // and the launcher passes the choice through as the team id it
            // gives each player -- turning it on underneath a free-for-all
            // would put everybody on team zero with nobody to shoot.
        }

        /// <summary>
        /// "0.5" -> 0.5, clamped.
        ///
        /// Invariant first, because that is how the settings window writes it,
        /// and then the machine's own format, because the console menu writes
        /// these with <c>decimal.ToString()</c> -- so a file last saved from
        /// the menu on a French or German system holds "0,5". Reading only one
        /// of the two silently discards whichever half of the settings the
        /// other screen wrote.
        /// </summary>
        private static bool TryVolume(string? value, out float volume)
        {
            volume = 0;
            if (!Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float parsed)
                && !Single.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture,
                    out parsed))
            {
                return false;
            }
            volume = Math.Clamp(parsed, 0, 1);
            return true;
        }

        /// <summary>"7:00" or "7" -> seconds. The format the file already uses.</summary>
        private static bool TryTime(string? value, out float seconds)
        {
            seconds = 0;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string[] parts = value.Trim().Split(':');
            if (parts.Length > 3)
            {
                return false;
            }
            float total = 0;
            foreach (string part in parts)
            {
                if (!Int32.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int number) || number < 0)
                {
                    return false;
                }
                total = total * 60 + number;
            }
            seconds = total;
            return true;
        }
    }
}
