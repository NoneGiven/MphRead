using System;
using System.Collections.Generic;
using MphRead.Entities;

namespace MphRead
{
    public enum MatchState
    {
        InProgress = 0,
        GameOver = 1,
        Scoreboard = 2,
        Ending = 3
    }

    public static class GameState
    {
        public static MatchState MatchState { get; set; } = MatchState.InProgress;
        public static int ActivePlayers { get; set; } = 0;
        public static string[] Nicknames { get; } = new string[4] { "Player1", "Player2", "Player3", "Player4" };
        public static int[] Standings { get; } = new int[4];
        public static int[] TeamStandings { get; } = new int[4];
        public static int[] WinningSlots { get; } = new int[4]; // sktodo: what is this?
        public static int PrimeHunter { get; set; } = -1;

        public static bool Teams { get; set; } = false;
        public static bool FriendlyFire { get; set; } = false;
        public static int PointGoal { get; set; } = 0; // also used for starting extra lives
        public static float TimeGoal { get; set; } = 0; // also used for starting extra lives
        public static int DamageLevel { get; } = 1;
        public static bool OctolithReset { get; set; } = false;

        public static float MatchTime { get; set; } = -1;

        public static int[] Points { get; } = new int[4];
        public static int[] TeamPoints { get; } = new int[4];
        public static int[] Kills { get; } = new int[4];
        public static int[] TeamKills { get; } = new int[4];
        public static int[] Deaths { get; } = new int[4];
        public static int[] TeamDeaths { get; } = new int[4];
        public static float[] Time { get; } = new float[4]; // used for prime hunter time, player survival time
        public static float[] TeamTime { get; } = new float[4]; // used for defense time, max team survival time
        public static int[] BeamDamageMax { get; } = new int[4];
        public static int[] BeamDamageDealt { get; } = new int[4];
        public static int[] DamageCount { get; } = new int[4];
        public static int[] AltDamageCount { get; } = new int[4];
        public static int[] KillStreak { get; } = new int[4];
        public static int[] Suicides { get; } = new int[4];
        public static int[] FriendlyKills { get; } = new int[4];
        public static int[] HeadshotKills { get; } = new int[4];
        public static int[,] BeamKills { get; } = new int[4, 9];

        public static int[] OctolithScores { get; } = new int[4]; // field260 in-game
        public static int[] OctolithDrops { get; } = new int[4]; // field268 in-game
        public static int[] OctolithStops { get; } = new int[4]; // field270 in-game

        public static int[] NodesCaptured { get; } = new int[4]; // field260 in-game
        public static int[] NodesLost { get; } = new int[4]; // field268 in-game

        public static int[] KillsAsPrime { get; } = new int[4]; // field260 in-game
        public static int[] PrimesKilled { get; } = new int[4]; // field268 in-game


        public static void Setup(Scene scene)
        {
            GameMode mode = scene.GameMode;
            if (mode == GameMode.BattleTeams || mode == GameMode.SurvivalTeams || mode == GameMode.Capture
                || mode == GameMode.BountyTeams || mode == GameMode.NodesTeams || mode == GameMode.DefenderTeams)
            {
                Teams = true;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = PlayerEntity.Players[i];
                    if (player.LoadFlags.TestFlag(LoadFlags.Active))
                    {
                        player.Team = player.TeamIndex == 0 ? Team.Orange : Team.Green;
                        // todo: allow other colors (and I guess disable the emission then too)
                        player.Recolor = player.TeamIndex == 0 ? 4 : 5;
                    }
                }
            }
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
            {
                PointGoal = 7;
                MatchTime = 7 * 60;
            }
            else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                PointGoal = 2;
                MatchTime = 15 * 60;
            }
            else if (mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                PointGoal = 3;
                MatchTime = 15 * 60;
            }
            else if (mode == GameMode.Capture)
            {
                PointGoal = 5;
                MatchTime = 15 * 60;
            }
            else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                TimeGoal = 1.5f * 60;
                MatchTime = 15 * 60;
            }

            else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                PointGoal = 70;
                MatchTime = 15 * 60;
            }
            else if (mode == GameMode.PrimeHunter)
            {
                TimeGoal = 1.5f * 60;
                MatchTime = 15 * 60;
            }
        }

        public static void UpdateTime(Scene scene)
        {
            // todo: update license info etc.
            if (MatchTime > 0)
            {
                MatchTime = MathF.Max(MatchTime - scene.FrameTime, 0);
            }
        }

        public static void Update(Scene scene)
        {
            GameMode mode = scene.GameMode;
            IReadOnlyList<PlayerEntity> players = PlayerEntity.Players;
            int[] prevTeamPoints = new int[4];
            int[] prevTeamDeaths = new int[4];
            for (int i = 0; i < 4; i++)
            {
                prevTeamPoints[i] = TeamPoints[i];
                prevTeamDeaths[i] = TeamDeaths[i];
                TeamPoints[i] = 0;
                TeamDeaths[i] = 0;
                TeamKills[i] = 0;
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    TeamTime[i] = 0;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = players[i];
                if (!player.LoadFlags.TestFlag(LoadFlags.Initial))
                {
                    continue;
                }
                TeamPoints[player.TeamIndex] += Points[i];
                TeamDeaths[player.TeamIndex] += Deaths[i];
                TeamKills[player.TeamIndex] += Kills[i];
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    if (TeamTime[player.TeamIndex] < Time[i])
                    {
                        TeamTime[player.TeamIndex] = Time[i];
                    }
                }
                else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
                {
                    Time[i] = TeamTime[player.TeamIndex];
                }
            }
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams || mode == GameMode.Capture || mode == GameMode.Bounty
                || mode == GameMode.BountyTeams || mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                int teamPoints = TeamPoints[PlayerEntity.Main.TeamIndex];
                if (teamPoints != prevTeamPoints[PlayerEntity.Main.TeamIndex] && teamPoints == PointGoal - 1)
                {
                    // todo: play voice
                }
            }
            else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                int opponents = 0;
                int lastTeam = -1;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = players[i];
                    if (!player.LoadFlags.TestAny(LoadFlags.Active))
                    {
                        continue;
                    }
                    if (player.Health > 0 || TeamDeaths[player.TeamIndex] <= PointGoal)
                    {
                        if (player.TeamIndex != PlayerEntity.Main.TeamIndex)
                        {
                            opponents++;
                            lastTeam = player.TeamIndex;
                        }
                    }
                    if (TeamDeaths[player.TeamIndex] > PointGoal && player.RespawnTimer == PlayerEntity.RespawnTime)
                    {
                        // todo: play voice
                    }
                }
                if (PlayerEntity.Main.LoadFlags.TestAny(LoadFlags.Active) && opponents == 1 && lastTeam != -1)
                {
                    int teamDeaths = TeamDeaths[lastTeam];
                    if (teamDeaths != prevTeamDeaths[lastTeam] && teamDeaths == PointGoal)
                    {
                        // todo: play voice
                    }
                }
            }
            ActivePlayers = 0;
            if (Teams)
            {
                int a = 0;
                for (int t = 0; t < 2; t++)
                {
                    for (int p = 0; p < 4; p++)
                    {
                        PlayerEntity player = players[p];
                        if (player.TeamIndex == t)
                        {
                            Standings[p] = 3;
                            if (player.LoadFlags.TestFlag(LoadFlags.Active))
                            {
                                WinningSlots[a++] = p;
                                ActivePlayers++;
                            }
                        }
                    }
                }
            }
            else
            {
                int a = 0;
                for (int p = 0; p < 4; p++)
                {
                    Standings[p] = 3;
                    if (players[p].LoadFlags.TestFlag(LoadFlags.Active))
                    {
                        WinningSlots[a++] = p;
                        ActivePlayers++;
                    }
                }
            }
            for (int index = 0; index < ActivePlayers; index++)
            {
                for (int nextIndex = index + 1; nextIndex < ActivePlayers; nextIndex++)
                {
                    int slot = WinningSlots[index];
                    int nextSlot = WinningSlots[nextIndex];
                    int teamIndex = players[slot].TeamIndex;
                    int nextTeamIndex = players[nextSlot].TeamIndex;
                    // sktodo?: the game passes team_ids[wslot/nslot] instead of the player fields to CompareTeams
                    if (Teams && teamIndex != nextTeamIndex && CompareTeams(teamIndex, nextTeamIndex, mode) < 0
                        || ComparePlayers(slot, nextSlot, mode) < 0)
                    {
                        WinningSlots[index] = nextSlot;
                        WinningSlots[nextIndex] = slot;
                    }
                }
            }
            if (Teams)
            {
                int v47 = 0;
                int v48 = CompareTeams(0, 1, mode);
                int[] v57 = new int[2];
                if (v48 <= 0)
                {
                    v57[0] = v48 != 0 ? 1 : 0;
                    v57[1] = 0;
                }
                else
                {
                    v57[0] = 0;
                    v57[1] = 1;
                }
                for (int i = 0; i < ActivePlayers - 1; i++)
                {
                    int slot = WinningSlots[i];
                    int nextSlot = WinningSlots[i + 1];
                    int teamIndex = players[slot].TeamIndex;
                    Standings[slot] = v57[teamIndex];
                    TeamStandings[slot] = v47;
                    if (teamIndex != players[nextSlot].TeamIndex)
                    {
                        if (ComparePlayers(slot, nextSlot, mode) != 0)
                        {
                            v47++;
                        }
                    }
                    else
                    {
                        v47 = 0;
                    }
                }
                int index = ActivePlayers - 1;
                Standings[index] = v57[players[WinningSlots[index]].TeamIndex];
                TeamStandings[index] = v47;
            }
            else
            {
                int index;
                int v47 = 0;
                for (index = 0; index < ActivePlayers - 1; index++)
                {
                    int slot = WinningSlots[index];
                    Standings[slot] = v47;
                    if (ComparePlayers(slot, WinningSlots[index + 1], mode) != 0)
                    {
                        v47 = index + 1;
                    }
                }
                Standings[WinningSlots[index]] = v47;
            }
            // todo: update license info
        }

        private static int ComparePlayers(int slot1, int slot2, GameMode mode)
        {
            int points1 = Points[slot1];
            int points2 = Points[slot2];
            float time1 = Time[slot1];
            float time2 = Time[slot2];
            int deaths1 = Deaths[slot1];
            int deaths2 = Deaths[slot2];
            int kills1 = Kills[slot1];
            int kills2 = Kills[slot2];
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                if (time1 == time2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Capture || mode == GameMode.Nodes || mode == GameMode.NodesTeams
                || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                if (points1 == points2 && kills1 == kills2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.PrimeHunter)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }

        private static int CompareTeams(int slot1, int slot2, GameMode mode)
        {
            int points1 = TeamPoints[slot1];
            int points2 = TeamPoints[slot2];
            float time1 = TeamTime[slot1];
            float time2 = TeamTime[slot2];
            int deaths1 = TeamDeaths[slot1];
            int deaths2 = TeamDeaths[slot2];
            int kills1 = TeamKills[slot1];
            int kills2 = TeamKills[slot2];
            if (mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.SurvivalTeams)
            {
                if (time1 == time2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.DefenderTeams)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Capture || mode == GameMode.NodesTeams || mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && kills1 == kills2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }
    }
}
