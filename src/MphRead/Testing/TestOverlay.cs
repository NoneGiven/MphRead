using System;
using System.Collections.Generic;
using System.Linq;

namespace MphRead.Testing
{
    public static class TestOverlay
    {
        public static void Translate(int mask)
        {
            mask = 0x21;
            var active = new List<int>();
            for (int i = 0; i < 18; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    active.Add(OverlayMap[i]);
                }
            }
            Console.WriteLine(String.Join(", ", active.OrderBy(a => a)));
            Nop();
        }

        private static void Nop()
        {
        }

        public static readonly IReadOnlyList<int> OverlayMap = new List<int>()
        {
            /*  0 */ 4,
            /*  1 */ 6,
            /*  2 */ 17,
            /*  3 */ 5,
            /*  4 */ 16,
            /*  5 */ 0,
            /*  6 */ 7,
            /*  7 */ 1,
            /*  8 */ 2,
            /*  9 */ 3,
            /* 10 */ 8,
            /* 11 */ 15,
            /* 12 */ 10,
            /* 13 */ 9,
            /* 14 */ 11,
            /* 15 */ 12,
            /* 16 */ 13,
            /* 17 */ 14
        };
    }

    [Flags]
    public enum Overlay
    {
        None = 0x0,
        WiFiPlay = 0x1,
        DownloadPlay = 0x2,
        Unknown02 = 0x4,
        VoiceChat = 0x8,
        Unknown04 = 0x10,
        Frontend = 0x20,
        Unknown06 = 0x40,
        Movies = 0x80,
        ModelsEffects = 0x100,
        Unknown09 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000
    }
}
