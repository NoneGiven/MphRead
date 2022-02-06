namespace MphRead
{
    public static class Bugfixes
    {
        public static bool SmoothCamSeqHandoff { get; set; } = false;
        public static bool BetterCamSeqNodeRef { get; set; } = true;
        public static bool NoStrayRespawnText { get; set; } = false;
    }

    public static class Features
    {
        public static bool AllowInvalidTeams { get; set; } = true;
        public static bool DebugWeaponSelect { get; set; } = true;
    }
}
