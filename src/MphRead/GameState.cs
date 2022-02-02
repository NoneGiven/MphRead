namespace MphRead
{
    public static class GameState
    {
        public static string[] Nicknames { get; } = new string[4] { "Player1", "Player2", "Player3", "Player4" };
        public static bool Teams { get; set; } = false;

        public static int[] Points { get; } = new int[4];

        public static bool OctolithReset { get; set; } = false;
        public static int[] OctolithStops { get; } = new int[4]; // field270 in game
        public static int[] OctolithDrops { get; } = new int[4]; // field268 in game
        public static int[] OctolithScores { get; } = new int[4]; // field260 in game

        public static float[] DefenseTime { get; } = new float[4];

        public static int[] NodesCaptured { get; } = new int[4]; // field260 in game
        public static int[] NodesLost { get; } = new int[4]; // field268 in game
    }
}
