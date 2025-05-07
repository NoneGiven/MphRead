using System;
using System.Collections.Generic;
using System.IO;
using MphRead.Formats;

namespace MphRead.Entities
{
    public class AiData
    {
        public AiPersonalityData1 Personality { get; set; } = null!;
        public bool Flags1 { get; set; }
        public AiFlags2 Flags2 { get; set; }
        public AiFlags3 Flags3 { get; set; }
        public ushort HealthThreshold { get; set; }
        public uint Field110 { get; set; }
        public int Field118 { get; set; }

        public void Initialize()
        {
            Flags1 = false;
            Flags2 = AiFlags2.None;
            Flags3 = AiFlags3.None;
            HealthThreshold = 0;
            Field110 = 0;
            Field118 = 0;
        }
    }

    public partial class PlayerEntity
    {
        public AiData AiData { get; init; } = new AiData();
        public NodeData3? FieldF20 { get; set; } = null;
        public int BotLevel { get; set; } = 0;

        public void ProcessAi()
        {
            // sktodo-ai: ProcessAi()
        }
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
        Unused22 = 0x400000,
        Unused23 = 0x800000,
        Unused24 = 0x1000000,
        Unused25 = 0x2000000,
        Unused26 = 0x4000000,
        Unused27 = 0x8000000,
        Unused28 = 0x10000000,
        Unused29 = 0x20000000,
        Unused30 = 0x40000000,
        Unused31 = 0x80000000
    }

    [Flags]
    public enum AiFlags3 : uint
    {
        None = 0,
        NoInput = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20
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
                        player.AiData.Flags1 = true;
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
                player.AiData.Personality = LoadData(aiOffset);
            }
        }

        private static string _cachedVersion = "";
        private static byte[]? _aiPersonalityData = null;

        private static AiPersonalityData1 LoadData(int offset)
        {
            if (Paths.MphKey != _cachedVersion)
            {
                _aiPersonalityData = null;
                _data1Cache.Clear();
                _data2Cache.Clear();
                _cachedVersion = Paths.MphKey;
            }
            if (_aiPersonalityData == null)
            {
                _aiPersonalityData = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin"));
            }
            return ParseData1(offset, count: 1)[0];
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData1>> _data1Cache = [];
        private static readonly Dictionary<int, IReadOnlyList<int>> _data3Cache = [];

        private static IReadOnlyList<AiPersonalityData1> ParseData1(int offset, int count)
        {
            if (_data1Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData1>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData1>(count);
            IReadOnlyList<AiData1> data1s = Read.DoOffsets<AiData1>(bytes, offset, count);
            for (int i = 0; i < data1s.Count; i++)
            {
                AiData1 data1 = data1s[i];
                IReadOnlyList<AiPersonalityData1> data1Children = [];
                if (data1.Data1Count > 0 && data1.Data1Offset != offset)
                {
                    data1Children = ParseData1(data1.Data1Offset, data1.Data1Count);
                }
                IReadOnlyList<AiPersonalityData2> data2 = [];
                if (data1.Data2Count > 0)
                {
                    data2 = ParseData2(data1.Data2Offset, data1.Data2Count);
                }
                IReadOnlyList<int> data3a = [];
                if (data1.Data3aCount > 0)
                {
                    if (_data3Cache.TryGetValue(data1.Data3aOffset, out IReadOnlyList<int>? cached3a))
                    {
                        data3a = cached3a;
                    }
                    else
                    {
                        data3a = Read.DoOffsets<int>(bytes, data1.Data3aOffset, data1.Data3aCount);
                        _data3Cache.Add(data1.Data3aOffset, data3a);
                    }
                }
                IReadOnlyList<int> data3b = [];
                if (data1.Data3bCount > 0)
                {
                    if (_data3Cache.TryGetValue(data1.Data3bOffset, out IReadOnlyList<int>? cached3b))
                    {
                        data3b = cached3b;
                    }
                    else
                    {
                        data3b = Read.DoOffsets<int>(bytes, data1.Data3bOffset, data1.Data3bCount);
                        _data3Cache.Add(data1.Data3bOffset, data3b);
                    }
                }
                results.Add(new AiPersonalityData1(data1.Field0, data1Children, data2, data3a, data3b));
            }
            _data1Cache.Add(offset, results);
            return results;
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData2>> _data2Cache = [];

        private static IReadOnlyList<AiPersonalityData2> ParseData2(int offset, int count)
        {
            if (_data2Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData2>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData2>(count);
            IReadOnlyList<AiData2> data2s = Read.DoOffsets<AiData2>(bytes, offset, count);
            for (int i = 0; i < data2s.Count; i++)
            {
                AiData2 data2 = data2s[i];
                IReadOnlyList<AiPersonalityData4> data4 = [];
                if (data2.Data4Count > 0)
                {
                    data4 = ParseData4(data2.Data4Offset, data2.Data4Count);
                }
                AiPersonalityData5 data5 = _emptyParams;
                if (data2.Data5Offset != 0)
                {
                    data5 = ParseData5(data2.Data5Type, data2.Data5Offset);
                }
                results.Add(new AiPersonalityData2(data2.FieldC, data2.Field10, data4, data2.Data5Type, data5));
            }
            _data2Cache.Add(offset, results);
            return results;
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData4>> _data4Cache = [];

        private static IReadOnlyList<AiPersonalityData4> ParseData4(int offset, int count)
        {
            if (_data4Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData4>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData4>(count);
            IReadOnlyList<AiData4> data4s = Read.DoOffsets<AiData4>(bytes, offset, count);
            for (int i = 0; i < data4s.Count; i++)
            {
                AiData4 data4 = data4s[i];
                AiPersonalityData5 data5 = _emptyParams;
                if (data4.Data5Offset != 0)
                {
                    data5 = ParseData5(data4.Data5Type, data4.Data5Offset);
                }
                results.Add(new AiPersonalityData4(data4.Data5Type, data5));
            }
            _data4Cache.Add(offset, results);
            return results;
        }

        private static readonly AiPersonalityData5 _emptyParams = new AiPersonalityData5();
        private static readonly Dictionary<int, AiPersonalityData5> _data5Cache = [];

        private static AiPersonalityData5 ParseData5(int type, int offset)
        {
            if (_data5Cache.TryGetValue(offset, out AiPersonalityData5? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            uint param1 = Read.SpanReadUint(bytes, offset);
            uint param2 = type == 210 ? Read.SpanReadUint(bytes, offset + 4) : 0;
            return new AiPersonalityData5(param1, param2);
        }

        // skdebug
        public static void TestRead()
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin")));
            var offsets = new List<int>()
            {
                13480, 32896, 32932, 32968, 33012, 33152, 33196, 33232, 33372, 33416, 33556, 33696, 33836,
                33976, 35428, 37576, 39420, 40312, 40556, 41492, 41948, 42772, 45176, 45220, 45696
            };
            var results = new List<AiPersonalityData1>();
            foreach (int offset in offsets)
            {
                results.Add(LoadData(offset));
            }
            _ = 5;
            _ = 5;
        }

        // size: 36
        public readonly struct AiData1
        {
            public readonly int Field0;
            public readonly int Data1Count;
            public readonly int Data1Offset;
            public readonly int Data2Count;
            public readonly int Data2Offset;
            public readonly int Data3aCount;
            public readonly int Data3aOffset;
            public readonly int Data3bCount;
            public readonly int Data3bOffset;
        }

        // size: 24
        public readonly struct AiData2
        {
            public readonly int Data5Type;
            public readonly int Data4Count;
            public readonly int Data4Offset;
            public readonly int FieldC;
            public readonly int Field10;
            public readonly int Data5Offset;
        }

        // size: 8
        public readonly struct AiData4
        {
            public readonly int Data5Type;
            public readonly int Data5Offset;
        }
    }

    public class AiPersonalityData1
    {
        public int Field0 { get; init; }
        public IReadOnlyList<AiPersonalityData1> Data1 { get; init; }
        public IReadOnlyList<AiPersonalityData2> Data2 { get; init; }
        public IReadOnlyList<int> Data3a { get; init; }
        public IReadOnlyList<int> Data3b { get; init; }

        public AiPersonalityData1(int field0, IReadOnlyList<AiPersonalityData1> data1,
            IReadOnlyList<AiPersonalityData2> data2, IReadOnlyList<int> data3a, IReadOnlyList<int> data3b)
        {
            Field0 = field0;
            Data1 = data1;
            Data2 = data2;
            Data3a = data3a;
            Data3b = data3b;
        }
    }

    public class AiPersonalityData2
    {
        public int FieldC { get; init; }
        public int Field10 { get; init; }
        public IReadOnlyList<AiPersonalityData4> Data4 { get; init; }
        public int Data5Type { get; init; }
        public AiPersonalityData5 Data5 { get; init; }

        public AiPersonalityData2(int fieldC, int field10, IReadOnlyList<AiPersonalityData4> data4,
            int data5Type, AiPersonalityData5 data5)
        {
            FieldC = fieldC;
            Field10 = field10;
            Data4 = data4;
            Data5Type = data5Type;
            Data5 = data5;
        }
    }

    public class AiPersonalityData4
    {
        public int Data5Type { get; init; }
        public AiPersonalityData5 Data5 { get; init; }

        public AiPersonalityData4(int data5Type, AiPersonalityData5 data5)
        {
            Data5Type = data5Type;
            Data5 = data5;
        }
    }

    public class AiPersonalityData5
    {
        public uint Param1 { get; init; }
        public uint Param2 { get; init; }
        // skdebug: see if an empty one ever gets used for parameters, since it would be a null ref in-game
        public bool IsEmpty { get; init; }

        public AiPersonalityData5()
        {
            IsEmpty = true;
        }

        public AiPersonalityData5(uint param1, uint param2)
        {
            Param1 = param1;
            Param2 = param2;
        }
    }
}
