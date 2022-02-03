using MphRead.Entities;

namespace MphRead
{
    public static class GameState
    {
        public static int ActivePlayers { get; set; } = 0;
        public static string[] Nicknames { get; } = new string[4] { "Player1", "Player2", "Player3", "Player4" };
        public static int[] Standings { get; } = new int[4];
        public static int[] WinningSlots { get; } = new int[4]; // sktodo: what is this?
        public static int PrimeHunter { get; set; } = -1;

        public static bool Teams { get; set; } = false;
        public static bool FriendlyFire { get; set; } = false;
        public static int PointGoal { get; } = 0; // also used for starting extra lives
        public static int DamageLevel = 1;
        public static bool OctolithReset { get; set; } = false;

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

        public static void Update(Scene scene)
        {
            GameMode mode = scene.GameMode;
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
                PlayerEntity player = PlayerEntity.Players[i];
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
                    PlayerEntity player = PlayerEntity.Players[i];
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
                    if (teamDeaths != prevTeamDeaths[lastTeam] && teamDeaths == PointGoal )
                    {
                        // todo: play voice
                    }
                }
            }
            ActivePlayers = 0;
            // skhere
        }
    }
}
