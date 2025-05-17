using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MphRead.Entities;

namespace MphRead.Formats
{
    public static class AiPersonality
    {
        // copied Kanden 0 for Samus and Guardian 0 for Guardian, but they're unused anyway
        private static readonly IReadOnlyList<IReadOnlyList<int>> _encounterAiOffsets =
        [
            //                  Sam    Kan    Tra    Syl    Nox    Spi    Wea    Gua
            /* encounter 0 */ [33152, 33152, 33696, 33836, 33556, 33372, 33976, 13480 ],
            /* encounter 1 */ [ 33152, 33196, 37576, 41948, 35428, 33416, 41492, 13480 ],
            /* encounter 3 */ [ 33152, 33152, 39420, 42772, 33556, 40312, 33976, 13480 ],
            /* encounter 4 */ [ 33152, 33152, 33696, 45176, 33556, 40556, 33976, 13480 ]
        ];

        public static void LoadAll(GameMode mode)
        {
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.AiData.Reset();
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
            int param1 = Read.SpanReadInt(bytes, offset);
            int param2 = type == 210 ? Read.SpanReadInt(bytes, offset + 4) : 0;
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
                if (offset == 33196)
                {
                    results[^1].PrintAll();
                }
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
        public int Func24Id { get; init; }
        public IReadOnlyList<AiPersonalityData1> Data1 { get; init; }
        public IReadOnlyList<AiPersonalityData2> Data2 { get; init; }
        public IReadOnlyList<int> Data3a { get; init; }
        public IReadOnlyList<int> Data3b { get; init; }

        public AiPersonalityData1(int field0, IReadOnlyList<AiPersonalityData1> data1,
            IReadOnlyList<AiPersonalityData2> data2, IReadOnlyList<int> data3a, IReadOnlyList<int> data3b)
        {
            Func24Id = field0;
            Data1 = data1;
            Data2 = data2;
            Data3a = data3a;
            Data3b = data3b;
        }

        public void PrintAll()
        {
            var names1 = new HashSet<string>();
            var names2 = new HashSet<string>();
            var names3 = new HashSet<string>();
            var names4 = new HashSet<string>();
            int id = 0;
            int firstId = 0;
            int depth = 0;
            var queue = new Queue<(AiPersonalityData1, int)>();
            int offset = 0;
            queue.Enqueue((this, 0));
            while (queue.Count > 0)
            {
                int count = queue.Count;
                while (count > 0)
                {
                    (AiPersonalityData1 node, int offs) = queue.Dequeue();
                    PrintNode(node, id++, firstId + offs, names1, names2, names3, names4);
                    foreach (AiPersonalityData1 child in node.Data1)
                    {
                        queue.Enqueue((child, offset));
                    }
                    offset += node.Data1.Count;
                    count--;
                }
                if (queue.Count > 0)
                {
                    depth++;
                    offset = 0;
                    firstId = id;
                    Debug.WriteLine("-------------------------------------------------------------------------------------------" +
                        "-------------------------------------------------------------------------------------------------------");
                    Debug.WriteLine("");
                }
            }
            Debug.WriteLine(String.Join(", ", names1.Order()));
            Debug.WriteLine(String.Join(", ", names4.Order()));
            Debug.WriteLine(String.Join(", ", names2.Order()));
            Debug.WriteLine(String.Join(", ", names3.Order()));
            _ = 5;
            _ = 5;
        }

        private static string GetLabel(int id)
        {
            string label = "Root";
            if (--id >= 0)
            {
                label = "";
                while (id >= 0)
                {
                    label = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[id % 26] + label;
                    id = id / 26 - 1;
                }
            }
            return label;
        }

        private static void PrintNode(AiPersonalityData1 node, int id, int firstId,
            HashSet<string> names1, HashSet<string> names2, HashSet<string> names3, HashSet<string> names4)
        {
            Debug.WriteLine(GetLabel(id));
            string d3a = "-";
            string d3b = "-";
            string f4s = "-";
            string f2s = "-";
            if (node.Data3a.Count != 0)
            {
                IEnumerable<string> names = PlayerEntity.PlayerAiData.GetFuncs1Names(node.Data3a);
                foreach (string item in names)
                {
                    names1.Add(item);
                }
                d3a = String.Join(", ", node.Data3a) + " -> " + String.Join(", ", names);
            }
            if (node.Data3b.Count != 0)
            {
                IEnumerable<string> names = PlayerEntity.PlayerAiData.GetFuncs1Names(node.Data3b);
                foreach (string item in names)
                {
                    names1.Add(item);
                }
                d3b = String.Join(", ", node.Data3b) + " -> " + String.Join(", ", names);
            }
            if (node.Func24Id != 0)
            {
                string name4 = PlayerEntity.PlayerAiData.GetFuncs4Name(node.Func24Id);
                string name2 = PlayerEntity.PlayerAiData.GetFuncs2Name(node.Func24Id);
                names4.Add(name4);
                names2.Add(name2);
                f4s = node.Func24Id.ToString() + " -> " + name4;
                f2s = node.Func24Id.ToString() + " -> " + name2;
            }
            Debug.WriteLine($"Init (3a): {d3a}");
            Debug.WriteLine($"Init (F4): {f4s}");
            Debug.WriteLine($"Proc (3b): {d3b}");
            Debug.WriteLine($"Proc (F2): {f2s}");
            if (node.Data2.Count == 0)
            {
                Debug.WriteLine("Switch(x): -");
            }
            else
            {
                int pad1 = (node.Data2.Count - 1).ToString().Length;
                int pad2 = node.Data2.Select(d => d.Func3Id).Max().ToString().Length;
                int pad3 = node.Data2.Select(d => d.Weight).Max().ToString().Length;
                for (int i = 0; i < node.Data2.Count; i++)
                {
                    AiPersonalityData2 data2 = node.Data2[i];
                    string str1 = i.ToString().PadLeft(pad1);
                    string str2 = data2.Func3Id.ToString().PadLeft(pad2);
                    string str3 = data2.Weight.ToString().PadLeft(pad3);
                    if (data2.Data4.Count > 0)
                    {
                        var ids = data2.Data4.Select(d => d.Func3Id);
                        IEnumerable<string> names = PlayerEntity.PlayerAiData.GetFuncs3Names(ids);
                        foreach (string item in names)
                        {
                            names3.Add(item);
                        }
                        string str4 = String.Join(", ", ids) + " -> " + String.Join(", ", names);
                        Debug.WriteLine($"Precon({str1}): {str4}");
                    }
                    string name = PlayerEntity.PlayerAiData.GetFuncs3Name(data2.Func3Id);
                    names3.Add(name);
                    Debug.WriteLine($"Switch({str1}): {str2}, {str3}, " +
                        $"s = {data2.Data1SelectIndex} ({GetLabel(data2.Data1SelectIndex + firstId)}) -> {name}");
                }
            }
            Debug.WriteLine("");
        }
    }

    public class AiPersonalityData2
    {
        public int Data1SelectIndex { get; init; }
        public int Weight { get; init; }
        public IReadOnlyList<AiPersonalityData4> Data4 { get; init; }
        public int Func3Id { get; init; }
        public AiPersonalityData5 Parameters { get; init; }

        public AiPersonalityData2(int selIndex, int weight, IReadOnlyList<AiPersonalityData4> data4,
            int fund3Id, AiPersonalityData5 param)
        {
            Data1SelectIndex = selIndex;
            Weight = weight;
            Data4 = data4;
            Func3Id = fund3Id;
            Parameters = param;
        }
    }

    public class AiPersonalityData4
    {
        public int Func3Id { get; init; }
        public AiPersonalityData5 Parameters { get; init; }

        public AiPersonalityData4(int data5Type, AiPersonalityData5 data5)
        {
            Func3Id = data5Type;
            Parameters = data5;
        }
    }

    public class AiPersonalityData5
    {
        public int Param1 { get; init; }
        public int Param2 { get; init; }
        // skdebug: see if an empty one ever gets used for parameters, since it would be a null ref in-game
        public bool IsEmpty { get; init; }

        public AiPersonalityData5()
        {
            IsEmpty = true;
        }

        public AiPersonalityData5(int param1, int param2)
        {
            Param1 = param1;
            Param2 = param2;
        }
    }
}
