using System;
using System.Collections.Generic;
using System.IO;
using MphRead.Formats;

namespace MphRead.Entities
{
    public class AiData
    {
        // todo: all field names (and also implementations)
        public AiFlags1 Flags1 { get; set; } // todo?: not currently convinced these need to be bitfields
        public AiFlags2 Flags2 { get; set; }
        public ushort HealthThreshold { get; set; }
        public uint Field110 { get; set; }

        public void Initialize()
        {
            Flags1 = AiFlags1.None;
            Flags2 = AiFlags2.None;
            HealthThreshold = 0;
            Field110 = 0;
        }
    }

    public partial class PlayerEntity
    {
        public AiData AiData { get; init; } = new AiData();
        public NodeData3? FieldF20 { get; set; } = null;
        public int BotLevel { get; set; } = 0;

        public void ProcessAi()
        {
            // skhere: ProcessAi()
        }
    }

    [Flags]
    public enum AiFlags1 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }

    [Flags]
    public enum AiFlags2 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Bit22 = 0x400000,
        Bit23 = 0x800000,
        Bit24 = 0x1000000,
        Bit25 = 0x2000000,
        Bit26 = 0x4000000,
        Bit27 = 0x8000000,
        Bit28 = 0x10000000,
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        Bit31 = 0x80000000
    }

    public static class ReadBotAi
    {
        // copied Kanden 0 for Samus and Guardian 0 for Guardian, but they're unused anyway
        private static readonly IReadOnlyList<IReadOnlyList<int>> _encounterAiOffsets =
        [
            //                  Sam    Kan    Tra    Syl    Nox    Spi    Wea    Gua
            /* encounter 0 */ [ 33152, 33152, 33696, 33836, 33556, 33372, 33976, 13480 ],
            /* encounter 1 */ [ 33152, 33196, 37576, 41948, 35428, 33416, 41492, 13480 ],
            /* encounter 3 */ [ 33152, 33152, 39420, 42772, 33556, 40312, 33976, 13480 ],
            /* encounter 4 */ [ 33152, 33152, 33696, 45176, 33556, 40556, 33976, 13480 ]
        ];

        private static byte[]? _aiPersonalityData = null;

        public static void LoadAll(GameMode mode)
        {
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.AiData.Initialize();
                if (!player.IsBot)
                {
                    continue;
                }
                int aiOffset = 32896; // default, Battle, BattleTeams
                if (mode == GameMode.SinglePlayer)
                {
                    int encounterState = GameState.EncounterState[i];
                    if (player.Hunter == Hunter.Guardian)
                    {
                        aiOffset = encounterState == 2 ? 32932 : 13480;
                    }
                    else
                    {
                        if (encounterState == 2)
                        {
                            aiOffset = 33232;
                        }
                        else
                        {
                            int index = encounterState switch
                            {
                                1 => 1,
                                3 => 2,
                                4 => 3,
                                _ => 0
                            };
                            // todo?: if replacing enemy hunters, consider loading the offset belonging to the one replaced
                            aiOffset = _encounterAiOffsets[index][(int)player.Hunter];
                        }
                        player.AiData.Flags1 |= AiFlags1.Bit0;
                    }
                }
                else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    aiOffset = 45696;
                }
                else if (mode == GameMode.Capture
                    || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
                {
                    aiOffset = 32968;
                }
                else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams
                    || mode == GameMode.Defender || mode == GameMode.DefenderTeams)
                {
                    aiOffset = 33012;
                }
                else if (mode == GameMode.PrimeHunter)
                {
                    aiOffset = 45220;
                }
                LoadData(player, aiOffset);
            }
        }

        private static void LoadData(PlayerEntity player, int offset)
        {
            if (_aiPersonalityData == null)
            {
                _aiPersonalityData = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin"));
            }
            ParseData1(player, offset, 1);
        }

        private static void ParseData1(PlayerEntity player, int offset, int a4)
        {
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            // skhere
        }

        // skdebug
        public static void TestRead()
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin")));
        }
    }
}
