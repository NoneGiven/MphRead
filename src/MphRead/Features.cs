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
        public static bool TopScreenTargetInfo { get; set; } = true;
        public static float HelmetOpacity { get; set; } = 1;
        public static float VisorOpacity { get; set; } = 0.5f;
    }

    public static class Cheats
    {
        public static bool FreeWeaponSelect { get; set; } = true;
        public static bool UnlimitedJumps { get; set; } = false;
    }
}
