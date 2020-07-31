using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public class RoomMetadata
    {
        public string Name { get; }
        public string? InGameName { get; }
        public string ModelPath { get; }
        public string AnimationPath { get; }
        public string CollisionPath { get; }
        public string? TexturePath { get; }
        public string? EntityPath { get; }
        public string? NodePath { get; }
        public string? RoomNodeName { get; }
        public uint BattleTimeLimit { get; }
        public uint TimeLimit { get; }
        public short PointLimit { get; }
        public short NodeLayer { get; }
        public ushort FogEnabled { get; }
        public ushort Fog { get; }
        public ushort FogColor { get; }
        public uint FogSlope { get; }
        public uint FogOffset { get; }
        public ColorRgb Light1Color { get; }
        public Vector3 Light1Vector { get; }
        public ColorRgb Light2Color { get; }
        public Vector3 Light2Vector { get; }
        public Vector3 RoomSize { get; }
        public bool Multiplayer { get; }

        public RoomMetadata(string name, string? inGameName, string pathName, string modelPath,
            string animationPath, string collisionPath, string? texturePath, string? entityPath, string? nodePath,
            string? roomNodeName, uint battleTimeLimit, uint timeLimit, short pointLimit, short nodeLayer, ushort fogEnabled,
            ushort fog, ushort fogColor, uint fogSlope, uint fogOffset, ColorRgb light1Color,
            Vector3 light1Vector, ColorRgb light2Color, Vector3 light2Vector, Vector3 roomSize, bool multiplayer = false)
        {
            Name = name;
            InGameName = inGameName;
            // case-insensitive use of the name here
            ModelPath = $@"_archives\{pathName}\{modelPath}";
            AnimationPath = $@"_archives\{pathName}\{animationPath}";
            CollisionPath = $@"_archives\{pathName}\{collisionPath}";
            TexturePath = texturePath == null ? null : $@"levels\textures\{texturePath}";
            EntityPath = entityPath == null ? null : $@"levels\entities\{entityPath}";
            NodePath = nodePath == null ? null : $@"levels\nodeData\{nodePath}";
            RoomNodeName = roomNodeName;
            BattleTimeLimit = battleTimeLimit;
            TimeLimit = timeLimit;
            PointLimit = pointLimit;
            NodeLayer = nodeLayer;
            FogEnabled = fogEnabled;
            Fog = fog;
            FogColor = fogColor;
            FogSlope = fogSlope;
            FogOffset = fogOffset;
            Light1Color = light1Color;
            Light1Vector = light1Vector;
            Light2Color = light2Color;
            Light2Vector = light2Vector;
            RoomSize = roomSize;
            Multiplayer = multiplayer;
        }
    }

    public enum MdlSuffix
    {
        None,
        All,
        Model
    }

    public class ModelMetadata
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string? AnimationPath { get; }
        public string? CollisionPath { get; }
        public IReadOnlyList<RecolorMetadata> Recolors { get; }
        public bool UseLightSources { get; }

        public ModelMetadata(string name, string modelPath, string? animationPath, string? collisionPath,
            IReadOnlyList<RecolorMetadata> recolors, bool useLightSources = false)
        {
            Name = name;
            ModelPath = modelPath;
            AnimationPath = animationPath;
            CollisionPath = collisionPath;
            Recolors = recolors;
            UseLightSources = useLightSources;
        }

        public ModelMetadata(string name, string? animationPath, string? texturePath = null)
        {
            Name = name;
            ModelPath = $@"models\{name}_Model.bin";
            AnimationPath = animationPath;
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", ModelPath, texturePath ?? ModelPath)
            };
        }

        public ModelMetadata(string name, string remove, bool animation = true,
            string? animationPath = null, bool collision = false, bool firstHunt = false)
        {
            Name = name;
            string directory = firstHunt ? @"models\_fh" : "models";
            ModelPath = $@"{directory}\{name}_Model.bin";
            string removed = name.Replace(remove, "");
            if (animation)
            {
                AnimationPath = animationPath ?? $@"{directory}\{removed}_Anim.bin";
            }
            if (collision)
            {
                CollisionPath = $@"{directory}\{removed}_Collision.bin";
            }
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", ModelPath)
            };
        }

        public ModelMetadata(string name, IEnumerable<string> recolors, string? remove = null,
            bool animation = false, string? animationPath = null, bool texture = false, MdlSuffix mdlSuffix = MdlSuffix.None,
            string? archive = null, string? recolorName = null, bool useLightSources = false)
        {
            Name = name;
            string suffix = "";
            if (mdlSuffix != MdlSuffix.None)
            {
                suffix = "_mdl";
            }
            if (archive == null)
            {
                ModelPath = $@"models\{name}{suffix}_Model.bin";
            }
            else
            {
                ModelPath = $@"_archives\{archive}\{name}_Model.bin";
            }
            if (remove != null)
            {
                name = name.Replace(remove, "");
            }
            if (mdlSuffix != MdlSuffix.All)
            {
                suffix = "";
            }
            if (animation)
            {
                if (animationPath != null)
                {
                    AnimationPath = animationPath;
                }
                else if (archive != null)
                {
                    AnimationPath = $@"_archives\{archive}\{name}_Anim.bin";
                }
                else
                {
                    AnimationPath = $@"models\{name}{suffix}_Anim.bin";
                }
            }
            var recolorList = new List<RecolorMetadata>();
            foreach (string recolor in recolors)
            {
                string recolorString = $"{recolorName ?? name}_{recolor}";
                if (recolor.StartsWith("*"))
                {
                    recolorString = recolor.Replace("*", "");
                }
                string recolorModel = $@"models\{recolorString}_Model.bin";
                string texturePath = texture ? $@"models\{recolorString}_Tex.bin" : recolorModel;
                recolorList.Add(new RecolorMetadata(recolor, recolorModel, texturePath));
            }
            Recolors = recolorList;
            UseLightSources = useLightSources;
        }

        public ModelMetadata(string name, bool animation = true, bool collision = false,
            bool texture = false, string? share = null, MdlSuffix mdlSuffix = MdlSuffix.None,
            string? archive = null, string? addToAnim = null, bool firstHunt = false)
        {
            Name = name;
            string path;
            if (archive != null)
            {
                path = $@"_archives\{archive}";
            }
            else if (firstHunt)
            {
                path = @"models\_fh";
            }
            else
            {
                path = "models";
            }
            string suffix = "";
            if (mdlSuffix != MdlSuffix.None)
            {
                suffix = "_mdl";
            }
            ModelPath = $@"{path}\{name}{suffix}_Model.bin";
            if (mdlSuffix != MdlSuffix.All)
            {
                suffix = "";
            }
            if (animation)
            {
                AnimationPath = $@"{path}\{name}{addToAnim}{suffix}_Anim.bin";
            }
            if (collision)
            {
                CollisionPath = $@"{path}\{name}{suffix}_Collision.bin";
            }
            string recolorModel = ModelPath;
            if (share != null)
            {
                texture = false;
                recolorModel = share;
            }
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", recolorModel, texture ? $@"models\{name}{suffix}_Tex.bin" : recolorModel)
            };
        }
    }

    public class RecolorMetadata
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string TexturePath { get; }
        public string PalettePath { get; }
        public bool SeparatePaletteHeader { get; }
        private readonly Dictionary<int, IEnumerable<int>> _replaceIds = new Dictionary<int, IEnumerable<int>>();
        public IReadOnlyDictionary<int, IEnumerable<int>> ReplaceIds => _replaceIds;

        public RecolorMetadata(string name, string modelPath)
        {
            Name = name;
            ModelPath = modelPath;
            TexturePath = modelPath;
            PalettePath = modelPath;
        }

        public RecolorMetadata(string name, string modelPath, string texturePath)
        {
            Name = name;
            ModelPath = modelPath;
            TexturePath = texturePath;
            PalettePath = texturePath;
        }

        public RecolorMetadata(string name, string modelPath, string texturePath, string palettePath,
            bool separatePaletteHeader = false, Dictionary<int, IEnumerable<int>>? replaceIds = null)
        {
            Name = name;
            ModelPath = modelPath;
            TexturePath = texturePath;
            PalettePath = palettePath;
            SeparatePaletteHeader = separatePaletteHeader;
            if (replaceIds != null)
            {
                Debug.Assert(separatePaletteHeader);
                foreach (KeyValuePair<int, IEnumerable<int>> kvp in replaceIds)
                {
                    _replaceIds.Add(kvp.Key, kvp.Value);
                }
            }
        }
    }

    public class ObjectMetadata
    {
        public int SomeFlag { get; }
        public string Name { get; }
        public IReadOnlyList<int> AnimationIds { get; }
        public int RecolorId { get; }

        public ObjectMetadata(string name, int someFlag, int paletteId = 0, List<int>? animationIds = null)
        {
            Name = name;
            SomeFlag = someFlag;
            RecolorId = paletteId;
            if (animationIds == null)
            {
                AnimationIds = new List<int>() { 0, 0, 0, 0 };
            }
            else if (animationIds.Count != 4)
            {
                throw new ArgumentException(nameof(animationIds));
            }
            else
            {
                AnimationIds = animationIds;
            }
        }
    }

    public class PlatformMetadata
    {
        public int SomeFlag { get; }
        public string Name { get; }
        public IReadOnlyList<uint> AnimationIds { get; }
        public uint Field20 { get; }
        public uint Field24 { get; }

        public PlatformMetadata(string name, int someFlag = 0, List<uint>? animationIds = null,
            uint field20 = UInt32.MaxValue, uint field24 = UInt32.MaxValue)
        {
            Name = name;
            SomeFlag = someFlag;
            Field20 = field20;
            Field24 = field24;
            // instant_sleep_anim_id, wakeup_anim_id, instant_wakeup_anim_id, sleep_anim_id
            if (animationIds == null)
            {
                AnimationIds = new List<uint>() { UInt32.MaxValue, UInt32.MaxValue, UInt32.MaxValue, UInt32.MaxValue };
            }
            else if (animationIds.Count != 4)
            {
                throw new ArgumentException(nameof(animationIds));
            }
            else
            {
                AnimationIds = animationIds;
            }
        }
    }

    public class DoorMetadata
    {
        public string Name { get; }
        public string LockName { get; }
        public float LockOffset { get; }

        public DoorMetadata(string name, string lockName, float lockOffset)
        {
            Name = name;
            LockName = lockName;
            LockOffset = lockOffset;
        }
    }

    public static class Metadata
    {
        private static readonly IReadOnlyDictionary<GameMode, IReadOnlyList<int>> _modeLayers
            = new Dictionary<GameMode, IReadOnlyList<int>>()
        {
            { GameMode.Battle, new List<int>() { 0, 1, 2 } },
            { GameMode.BattleTeams, new List<int>() { 3 } },
            { GameMode.Survival, new List<int>() { 15 } },
            { GameMode.SurvivalTeams, new List<int>() { 15 } },
            { GameMode.Capture, new List<int>() { 12 } },
            { GameMode.Bounty, new List<int>() { 8, 9, 10 } },
            { GameMode.BountyTeams, new List<int>() { 11 } },
            { GameMode.Nodes, new List<int>() { 4, 5, 6 } },
            { GameMode.NodesTeams, new List<int>() { 7 } },
            { GameMode.Defender, new List<int>() { 14 } },
            { GameMode.DefenderTeams, new List<int>() { 14 } },
            { GameMode.PrimeHunter, new List<int>() { 0, 1, 2 } },
            { GameMode.Unknown15, new List<int>() { 13 } }
        };

        public static int GetMultiplayerEntityLayer(GameMode mode, int playerCount)
        {
            IReadOnlyList<int> list = _modeLayers[mode];
            if (list.Count == 1)
            {
                return list[0];
            }
            return list[playerCount == 3 ? 1 : (playerCount == 4 ? 2 : 0)];
        }

        // this is only set/used by Octolith
        // todo: set this as a uniform
        public static readonly IReadOnlyList<Vector3> ToonTable = new List<Vector3>()
        {
            GetTableColor(0x2000),
            GetTableColor(0x2000),
            GetTableColor(0x2020),
            GetTableColor(0x2021),
            GetTableColor(0x2021),
            GetTableColor(0x2041),
            GetTableColor(0x2441),
            GetTableColor(0x2461),
            GetTableColor(0x2461),
            GetTableColor(0x2462),
            GetTableColor(0x2482),
            GetTableColor(0x2482),
            GetTableColor(0x28C3),
            GetTableColor(0x2CE4),
            GetTableColor(0x3105),
            GetTableColor(0x3546),
            GetTableColor(0x3967),
            GetTableColor(0x3D88),
            GetTableColor(0x41C9),
            GetTableColor(0x45EA),
            GetTableColor(0x4A0B),
            GetTableColor(0x4E4B),
            GetTableColor(0x526C),
            GetTableColor(0x568D),
            GetTableColor(0x5ACE),
            GetTableColor(0x5EEF),
            GetTableColor(0x6310),
            GetTableColor(0x6751),
            GetTableColor(0x6B72),
            GetTableColor(0x6F93),
            GetTableColor(0x73D4),
            GetTableColor(0x77F5)
        };

        // todo: consolidate stuff like this
        private static Vector3 GetTableColor(ushort value)
        {
            int r = (value >> 0) & 0x1F;
            int g = (value >> 5) & 0x1F;
            int b = (value >> 10) & 0x1F;
            return new Vector3(r / 31.0f, g / 31.0f, b / 31.0f);
        }

        public static ModelMetadata? GetEntityByName(string name)
        {
            if (ModelMetadata.TryGetValue(name, out ModelMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        public static ModelMetadata? GetFirstHuntEntityByName(string name)
        {
            if (FirstHuntModels.TryGetValue(name, out ModelMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        public static ModelMetadata? GetEntityByPath(string path)
        {
            KeyValuePair<string, ModelMetadata> result = ModelMetadata.FirstOrDefault(r => r.Value.ModelPath == path);
            if (result.Key == null)
            {
                return null;
            }
            return result.Value;
        }

        public static (RoomMetadata?, int) GetRoomByName(string name)
        {
            if (RoomMetadata.TryGetValue(name, out RoomMetadata? metadata))
            {
                return (metadata, _roomIds.IndexOf(i => i == metadata.Name));
            }
            return (null, -1);
        }

        public static RoomMetadata? GetRoomById(int id)
        {
            if (id < 0 || id > _roomIds.Count)
            {
                throw new ArgumentException(nameof(id));
            }
            if (RoomMetadata.TryGetValue(_roomIds[id], out RoomMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        private static uint TimeLimit(uint minutes, uint seconds, uint frames)
        {
            return minutes * 3600 + seconds * 60 + frames;
        }

        private static ushort FogColor(byte r, byte g, byte b)
        {
            return (ushort)(r | g << 5 | b << 10);
        }

        public static int GetAreaInfo(int roomId)
        {
            int areaId = 8;
            if (roomId >= 27 && roomId < 36)
            {
                areaId = 0;
            }
            else if (roomId >= 36 && roomId < 45)
            {
                areaId = 1;
            }
            else if (roomId >= 45 && roomId < 56)
            {
                areaId = 2;
            }
            else if (roomId >= 56 && roomId < 65)
            {
                areaId = 3;
            }
            else if (roomId >= 65 && roomId < 72)
            {
                areaId = 4;
            }
            else if (roomId >= 72 && roomId < 77)
            {
                areaId = 5;
            }
            else if (roomId >= 77 && roomId < 83)
            {
                areaId = 6;
            }
            else if (roomId >= 83 && roomId < 89)
            {
                areaId = 7;
            }
            //bool multiplayer = roomId >= 93 && roomId <= 119;
            return areaId;
        }

        private static readonly IReadOnlyList<string> _roomIds
            = new List<string>()
            {
                /*   0 */ "UNIT1_CX",
                /*   1 */ "UNIT1_CX",
                /*   2 */ "UNIT1_CZ",
                /*   3 */ "UNIT1_CZ",
                /*   4 */ "UNIT1_MORPH_CX",
                /*   5 */ "UNIT1_MORPH_CX",
                /*   6 */ "UNIT1_MORPH_CZ",
                /*   7 */ "UNIT1_MORPH_CZ",
                /*   8 */ "UNIT2_CX",
                /*   9 */ "UNIT2_CX",
                /*  10 */ "UNIT2_CZ",
                /*  11 */ "UNIT2_CZ",
                /*  12 */ "UNIT3_CX",
                /*  13 */ "UNIT3_CX",
                /*  14 */ "UNIT3_CZ",
                /*  15 */ "UNIT3_CZ",
                /*  16 */ "UNIT4_CX",
                /*  17 */ "UNIT4_CX",
                /*  18 */ "UNIT4_CZ",
                /*  19 */ "UNIT4_CZ",
                /*  20 */ "CYLINDER_C1",
                /*  21 */ "BIGEYE_C1",
                /*  22 */ "UNIT1_RM1_CX",
                /*  23 */ "UNIT1_RM1_CX",
                /*  24 */ "GOREA_C1",
                /*  25 */ "UNIT3_MORPH_CZ",
                /*  26 */ "UNIT3_MORPH_CZ",
                /*  27 */ "UNIT1_LAND",
                /*  28 */ "UNIT1_C0",
                /*  29 */ "UNIT1_RM1",
                /*  30 */ "UNIT1_C4",
                /*  31 */ "UNIT1_RM6",
                /*  32 */ "CRYSTALROOM",
                /*  33 */ "UNIT1_RM4",
                /*  34 */ "UNIT1_TP1",
                /*  35 */ "UNIT1_B1",
                /*  36 */ "UNIT1_C1",
                /*  37 */ "UNIT1_C2",
                /*  38 */ "UNIT1_C5",
                /*  39 */ "UNIT1_RM2",
                /*  40 */ "UNIT1_RM3",
                /*  41 */ "UNIT1_RM5",
                /*  42 */ "UNIT1_C3",
                /*  43 */ "UNIT1_TP2",
                /*  44 */ "UNIT1_B2",
                /*  45 */ "UNIT2_LAND",
                /*  46 */ "UNIT2_C0",
                /*  47 */ "UNIT2_C1",
                /*  48 */ "UNIT2_RM1",
                /*  49 */ "UNIT2_C2",
                /*  50 */ "UNIT2_RM2",
                /*  51 */ "UNIT2_C3",
                /*  52 */ "UNIT2_RM3",
                /*  53 */ "UNIT2_C4",
                /*  54 */ "UNIT2_TP1",
                /*  55 */ "UNIT2_B1",
                /*  56 */ "UNIT2_C6",
                /*  57 */ "UNIT2_C7",
                /*  58 */ "UNIT2_RM4",
                /*  59 */ "UNIT2_RM5",
                /*  60 */ "UNIT2_RM6",
                /*  61 */ "UNIT2_RM7",
                /*  62 */ "UNIT2_RM8",
                /*  63 */ "UNIT2_TP2",
                /*  64 */ "UNIT2_B2",
                /*  65 */ "UNIT3_LAND",
                /*  66 */ "UNIT3_C0",
                /*  67 */ "UNIT3_C2",
                /*  68 */ "UNIT3_RM1",
                /*  69 */ "UNIT3_RM4",
                /*  70 */ "UNIT3_TP1",
                /*  71 */ "UNIT3_B1",
                /*  72 */ "UNIT3_C1",
                /*  73 */ "UNIT3_RM2",
                /*  74 */ "UNIT3_RM3",
                /*  75 */ "UNIT3_TP2",
                /*  76 */ "UNIT3_B2",
                /*  77 */ "UNIT4_LAND",
                /*  78 */ "UNIT4_RM1",
                /*  79 */ "UNIT4_RM3",
                /*  80 */ "UNIT4_C0",
                /*  81 */ "UNIT4_TP1",
                /*  82 */ "UNIT4_B1",
                /*  83 */ "UNIT4_C1",
                /*  84 */ "UNIT4_RM2",
                /*  85 */ "UNIT4_RM4",
                /*  86 */ "UNIT4_RM5",
                /*  87 */ "UNIT4_TP2",
                /*  88 */ "UNIT4_B2",
                /*  89 */ "Gorea_Land",
                /*  90 */ "Gorea_Peek",
                /*  91 */ "Gorea_b1",
                /*  92 */ "Gorea_b2",
                /*  93 */ "MP1 SANCTORUS",
                /*  94 */ "MP2 HARVESTER",
                /*  95 */ "MP3 PROVING GROUND",
                /*  96 */ "MP4 HIGHGROUND - EXPANDED",
                /*  97 */ "MP4 HIGHGROUND",
                /*  98 */ "MP5 FUEL SLUICE",
                /*  99 */ "MP6 HEADSHOT",
                /* 100 */ "MP7 PROCESSOR CORE",
                /* 101 */ "MP8 FIRE CONTROL",
                /* 102 */ "MP9 CRYOCHASM",
                /* 103 */ "MP10 OVERLOAD",
                /* 104 */ "MP11 BREAKTHROUGH",
                /* 105 */ "MP12 SIC TRANSIT",
                /* 106 */ "MP13 ACCELERATOR",
                /* 107 */ "MP14 OUTER REACH",
                /* 108 */ "CTF1 FAULT LINE - EXPANDED",
                /* 109 */ "CTF1_FAULT LINE",
                /* 110 */ "AD1 TRANSFER LOCK BT",
                /* 111 */ "AD1 TRANSFER LOCK DM",
                /* 112 */ "AD2 MAGMA VENTS",
                /* 113 */ "AD2 ALINOS PERCH",
                /* 114 */ "UNIT1 ALINOS LANDFALL",
                /* 115 */ "UNIT2 LANDING BAY",
                /* 116 */ "UNIT 3 VESPER STARPORT",
                /* 117 */ "UNIT 4 ARCTERRA BASE",
                /* 118 */ "Gorea Prison",
                /* 119 */ "E3 FIRST HUNT",
                // unused
                /* 120 */ "Level TestLevel",
                /* 121 */ "Level AbeTest",
                // unreferenced
                /* 122 */ "biodefense chamber 06",
                /* 123 */ "biodefense chamber 05",
                /* 124 */ "biodefense chamber 03",
                /* 125 */ "biodefense chamber 08",
                /* 126 */ "biodefense chamber 04",
                /* 127 */ "biodefense chamber 07",
                // First Hunt
                /* 128 */ "FH_MP1",
                /* 129 */ "FH_SURVIVOR",
                /* 130 */ "FH_MP2",
                /* 131 */ "FH_MP3",
                /* 132 */ "FH_MP5",
                /* 133 */ "FH_TEST",
                /* 134 */ "FH_REGULATOR",
                /* 135 */ "FH_MORPHBALL",
                /* 136 */ "FH_E3"
            };

        // unused: unit3_rm5_Ent.bin
        // unused: bigeyeroom_Ent.bin, cylinderroom_Ent.bin, Cylinder_C1_Ent.bin
        public static readonly IReadOnlyDictionary<string, RoomMetadata> RoomMetadata
            = new Dictionary<string, RoomMetadata>()
            {
                {
                    "UNIT1_CX",
                    new RoomMetadata(
                        "UNIT1_CX",
                        null,
                        "unit1_CX",
                        "unit1_CX_Model.bin",
                        "unit1_CX_Anim.bin",
                        "unit1_CX_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10f, 0f, 0f))
                },
                {
                    "UNIT1_CZ",
                    new RoomMetadata(
                        "UNIT1_CZ",
                        null,
                        "unit1_CZ",
                        "unit1_CZ_Model.bin",
                        "unit1_CZ_Anim.bin",
                        "unit1_CZ_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10f))
                },
                {
                    "UNIT1_MORPH_CX",
                    new RoomMetadata(
                        "UNIT1_MORPH_CX",
                        null,
                        "unit1_morph_CX",
                        "unit1_morph_CX_Model.bin",
                        "unit1_morph_CX_Anim.bin",
                        "unit1_morph_CX_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x4,
                        65180,
                        new ColorRgb(31, 18, 6),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 6, 4),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10f, 0f, 0f))
                },
                {
                    "UNIT1_MORPH_CZ",
                    new RoomMetadata(
                        "UNIT1_MORPH_CZ",
                        null,
                        "unit1_morph_CZ",
                        "unit1_morph_CZ_Model.bin",
                        "unit1_morph_CZ_Anim.bin",
                        "unit1_morph_CZ_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10f))
                },
                {
                    "UNIT2_CX",
                    new RoomMetadata(
                        "UNIT2_CX",
                        null,
                        "unit2_CX",
                        "unit2_CX_Model.bin",
                        "unit2_CX_Anim.bin",
                        "unit2_CX_Collision.bin",
                        null,
                        null, // unused: unit2_CX_Ent.bin
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10.378662f, 0f, 0f))
                },
                {
                    "UNIT2_CZ",
                    new RoomMetadata(
                        "UNIT2_CZ",
                        null,
                        "unit2_CZ",
                        "unit2_CZ_Model.bin",
                        "unit2_CZ_Anim.bin",
                        "unit2_CZ_Collision.bin",
                        null,
                        null, // unused: unit2_CZ_Ent.bin
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10.378662f))
                },
                {
                    "UNIT3_CX",
                    new RoomMetadata(
                        "UNIT3_CX",
                        null,
                        "unit3_CX",
                        "unit3_CX_Model.bin",
                        "unit3_CX_Anim.bin",
                        "unit3_CX_Collision.bin",
                        null,
                        null, // unused: unit3_CX_Ent.bin
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10f, 0f, 0f))
                },
                {
                    "UNIT3_CZ",
                    new RoomMetadata(
                        "UNIT3_CZ",
                        null,
                        "unit3_CZ",
                        "unit3_CZ_Model.bin",
                        "unit3_CZ_Anim.bin",
                        "unit3_CZ_Collision.bin",
                        null,
                        null, // unused: unit3_CZ_Ent.bin
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10f))
                },
                {
                    "UNIT4_CX",
                    new RoomMetadata(
                        "UNIT4_CX",
                        null,
                        "unit4_CX",
                        "unit4_CX_Model.bin",
                        "unit4_CX_Anim.bin",
                        "unit4_CX_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x4,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10f, 0f, 0f))
                },
                {
                    "UNIT4_CZ",
                    new RoomMetadata(
                        "UNIT4_CZ",
                        null,
                        "unit4_CZ",
                        "unit4_CZ_Model.bin",
                        "unit4_CZ_Anim.bin",
                        "unit4_CZ_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x4,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10f))
                },
                {
                    "CYLINDER_C1",
                    new RoomMetadata(
                        "CYLINDER_C1",
                        null,
                        "Cylinder_C1_CZ",
                        "Cylinder_C1_model.bin",
                        "Cylinder_C1_anim.bin",
                        "Cylinder_C1_collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(8, 28, 20),
                        0x4,
                        65535,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 2.295166f, 22.654053f))
                },
                {
                    "BIGEYE_C1",
                    new RoomMetadata(
                        "BIGEYE_C1",
                        null,
                        "BigEye_C1_CZ",
                        "bigeye_c1_model.bin",
                        "bigeye_c1_anim.bin",
                        "bigeye_c1_collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(8, 28, 20),
                        0x4,
                        65535,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, -1.869873f, 22.653809f))
                },
                {
                    "UNIT1_RM1_CX",
                    new RoomMetadata(
                        "UNIT1_RM1_CX",
                        null,
                        "UNIT1_RM1_CX",
                        "UNIT1_RM1_CX_Model.bin",
                        "UNIT1_RM1_CX_Anim.bin",
                        "UNIT1_RM1_CX_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65535,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(10f, 0f, 0f))
                },
                {
                    "GOREA_C1",
                    new RoomMetadata(
                        "GOREA_C1",
                        null,
                        "Gorea_C1_CZ",
                        "Gorea_c1_Model.bin",
                        "Gorea_c1_Anim.bin",
                        "Gorea_c1_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(8, 28, 20),
                        0x4,
                        65535,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 20f))
                },
                {
                    "UNIT3_MORPH_CZ",
                    new RoomMetadata(
                        "UNIT3_MORPH_CZ",
                        null,
                        "unit3_morph_CZ",
                        "unit3_morph_CZ_Model.bin",
                        "unit3_morph_CZ_Anim.bin",
                        "unit3_morph_CZ_Collision.bin",
                        null,
                        null,
                        null,
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x0,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(0f, 0f, 10f))
                },
                {
                    "UNIT1_LAND",
                    new RoomMetadata(
                        "UNIT1_LAND",
                        "Alinos Gateway",
                        "unit1_Land",
                        "unit1_Land_Model.bin",
                        "unit1_Land_Anim.bin",
                        "unit1_Land_Collision.bin",
                        "unit1_Land_Tex.bin",
                        "Unit1_Land_Ent.bin",
                        "unit1_Land_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT1_C0",
                    new RoomMetadata(
                        "UNIT1_C0",
                        "Echo Hall",
                        "unit1_C0",
                        "unit1_C0_Model.bin",
                        "unit1_C0_Anim.bin",
                        "unit1_C0_Collision.bin",
                        "unit1_c0_Tex.bin",
                        "Unit1_C0_Ent.bin",
                        "unit1_C0_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT1_RM1",
                    new RoomMetadata(
                        "UNIT1_RM1",
                        "High Ground",
                        "unit1_RM1",
                        "unit1_RM1_Model.bin",
                        "unit1_RM1_Anim.bin",
                        "unit1_RM1_Collision.bin",
                        "unit1_RM1_Tex.bin",
                        "unit1_RM1_Ent.bin",
                        "unit1_RM1_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT1_C4",
                    new RoomMetadata(
                        "UNIT1_C4",
                        "Magma Drop",
                        "unit1_C4",
                        "unit1_C4_Model.bin",
                        "unit1_C4_Anim.bin",
                        "unit1_C4_Collision.bin",
                        "unit1_c4_Tex.bin",
                        "Unit1_C4_Ent.bin",
                        "unit1_C4_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT1_RM6",
                    new RoomMetadata(
                        "UNIT1_RM6",
                        "Elder Passage",
                        "unit1_RM6",
                        "unit1_RM6_Model.bin",
                        "unit1_RM6_Anim.bin",
                        "unit1_RM6_Collision.bin",
                        "unit1_RM6_Tex.bin",
                        "unit1_RM6_Ent.bin",
                        "unit1_RM6_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-41f, 41f, 6f))
                },
                {
                    "CRYSTALROOM",
                    new RoomMetadata(
                        "CRYSTALROOM",
                        "Alimbic Cannon Control Room",
                        "crystalroom",
                        "crystalroom_Model.bin",
                        "crystalroom_anim.bin",
                        "crystalroom_collision.bin",
                        "crystalroom_Tex.bin",
                        "crystalroom_Ent.bin",
                        "crystalroom_node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(19, 29, 31),
                        0x4,
                        65535,
                        new ColorRgb(19, 29, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(32f, -72f, 72f))
                },
                {
                    "UNIT1_RM4",
                    new RoomMetadata(
                        "UNIT1_RM4",
                        "Combat Hall",
                        "mp3",
                        "mp3_Model.bin",
                        "mp3_Anim.bin",
                        "mp3_Collision.bin",
                        "mp3_Tex.bin",
                        "unit1_rm4_Ent.bin",
                        "unit1_RM4_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT1_TP1",
                    new RoomMetadata(
                        "UNIT1_TP1",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit1_TP1_Ent.bin",
                        "unit1_TP1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(20, 8, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT1_B1",
                    new RoomMetadata(
                        "UNIT1_B1",
                        null,
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "Unit1_b1_Ent.bin",
                        "unit2_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-11f, 10f, 0f))
                },
                {
                    "UNIT1_C1",
                    new RoomMetadata(
                        "UNIT1_C1",
                        "Alimbic Gardens",
                        "unit1_C1",
                        "unit1_C1_Model.bin",
                        "unit1_C1_Anim.bin",
                        "unit1_C1_Collision.bin",
                        "unit1_c1_Tex.bin",
                        "Unit1_C1_Ent.bin",
                        "unit1_C1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(13f, -27f, 26f))
                },
                {
                    "UNIT1_C2",
                    new RoomMetadata(
                        "UNIT1_C2",
                        "Thermal Vast",
                        "unit1_C2",
                        "unit1_C2_Model.bin",
                        "unit1_C2_Anim.bin",
                        "unit1_C2_Collision.bin",
                        "unit1_c2_Tex.bin",
                        "Unit1_C2_Ent.bin",
                        "unit1_C2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT1_C5",
                    new RoomMetadata(
                        "UNIT1_C5",
                        "Piston Cave",
                        "unit1_C5",
                        "unit1_C5_Model.bin",
                        "unit1_C5_Anim.bin",
                        "unit1_C5_Collision.bin",
                        "unit1_c5_Tex.bin",
                        "Unit1_C5_Ent.bin",
                        "unit1_RM5_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x4,
                        65300,
                        new ColorRgb(31, 18, 6),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 9, 4),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT1_RM2",
                    new RoomMetadata(
                        "UNIT1_RM2",
                        "Alinos Perch",
                        "unit1_rm2",
                        "unit1_rm2_Model.bin",
                        "unit1_rm2_Anim.bin",
                        "unit1_rm2_Collision.bin",
                        "unit1_RM2_Tex.bin",
                        "unit1_RM2_ent.bin",
                        "unit1_RM2_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-37f, 45f, -1f))
                },
                {
                    "UNIT1_RM3",
                    new RoomMetadata(
                        "UNIT1_RM3",
                        "Council Chamber",
                        "unit1_rm3",
                        "unit1_rm3_Model.bin",
                        "unit1_rm3_Anim.bin",
                        "unit1_rm3_Collision.bin",
                        "unit1_RM3_Tex.bin",
                        "unit1_rm3_Ent.bin",
                        "unit1_RM3_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(25f, -27f, 20f))
                },
                {
                    "UNIT1_RM5",
                    new RoomMetadata(
                        "UNIT1_RM5",
                        "Processor Core",
                        "mp7",
                        "mp7_Model.bin",
                        "mp7_Anim.bin",
                        "mp7_Collision.bin",
                        "mp7_Tex.bin",
                        "unit1_rm5_Ent.bin",
                        "unit1_RM5_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x4,
                        65180,
                        new ColorRgb(31, 18, 6),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 6, 4),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT1_C3",
                    new RoomMetadata(
                        "UNIT1_C3",
                        "Crash Site",
                        "unit1_C3",
                        "unit1_C3_Model.bin",
                        "unit1_C3_Anim.bin",
                        "unit1_C3_Collision.bin",
                        "unit1_c3_Tex.bin",
                        "Unit1_C3_Ent.bin",
                        "unit1_C3_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT1_TP2",
                    new RoomMetadata(
                        "UNIT1_TP2",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit1_TP2_Ent.bin",
                        "unit1_TP2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x2,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-37f, 45f, -1f))
                },
                {
                    "UNIT1_B2",
                    new RoomMetadata(
                        "UNIT1_B2",
                        null,
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "Unit1_b2_Ent.bin",
                        "unit2_b1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(25f, -27f, 20f))
                },
                {
                    "UNIT2_LAND",
                    new RoomMetadata(
                        "UNIT2_LAND",
                        "Celestial Gateway",
                        "unit2_Land",
                        "unit2_Land_Model.bin",
                        "unit2_Land_Anim.bin",
                        "unit2_Land_Collision.bin",
                        "unit2_Land_Tex.bin",
                        "unit2_Land_Ent.bin",
                        "unit2_Land_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 24, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_C0",
                    new RoomMetadata(
                        "UNIT2_C0",
                        "Helm Room",
                        "unit2_C0",
                        "unit2_C0_Model.bin",
                        "unit2_C0_Anim.bin",
                        "unit2_C0_Collision.bin",
                        "unit2_c0_Tex.bin",
                        "unit2_C0_Ent.bin",
                        "unit2_C0_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT2_C1",
                    new RoomMetadata(
                        "UNIT2_C1",
                        "Meditation Room",
                        "unit2_C1",
                        "unit2_C1_Model.bin",
                        "unit2_C1_Anim.bin",
                        "unit2_C1_Collision.bin",
                        "unit2_c1_Tex.bin",
                        "unit2_C1_Ent.bin",
                        "unit2_C1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-24f, 27f, 0f))
                },
                {
                    "UNIT2_RM1",
                    new RoomMetadata(
                        "UNIT2_RM1",
                        "Data Shrine 01",
                        "mp1",
                        "mp1_Model.bin",
                        "mp1_Anim.bin",
                        "mp1_Collision.bin",
                        "mp1_Tex.bin",
                        "unit2_RM1_Ent.bin",
                        "unit2_RM1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(17f, -14f, 14f))
                },
                {
                    "UNIT2_C2",
                    new RoomMetadata(
                        "UNIT2_C2",
                        "Fan Room Alpha",
                        "unit2_C2",
                        "unit2_C2_Model.bin",
                        "unit2_C2_Anim.bin",
                        "unit2_C2_Collision.bin",
                        "unit2_c2_Tex.bin",
                        "unit2_C2_Ent.bin",
                        "unit2_C2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_RM2",
                    new RoomMetadata(
                        "UNIT2_RM2",
                        "Data Shrine 02",
                        "mp1",
                        "mp1_Model.bin",
                        "mp1_Anim.bin",
                        "mp1_Collision.bin",
                        "mp1_Tex.bin",
                        "unit2_RM2_Ent.bin",
                        "unit2_RM2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x2,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT2_C3",
                    new RoomMetadata(
                        "UNIT2_C3",
                        "Fan Room Beta",
                        "unit2_C3",
                        "unit2_C3_Model.bin",
                        "unit2_C3_Anim.bin",
                        "unit2_C3_Collision.bin",
                        "unit2_c3_Tex.bin",
                        "unit2_C3_Ent.bin",
                        "unit2_C3_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-49f, 48f, 0f))
                },
                {
                    "UNIT2_RM3",
                    new RoomMetadata(
                        "UNIT2_RM3",
                        "Data Shrine 03",
                        "unit2_RM3",
                        "unit2_RM3_Model.bin",
                        "unit2_RM3_Anim.bin",
                        "unit2_RM3_Collision.bin",
                        "unit2_RM3_Tex.bin",
                        "unit2_RM3_Ent.bin",
                        "unit2_RM3_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x3,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(48f, -49f, 47f))
                },
                {
                    "UNIT2_C4",
                    new RoomMetadata(
                        "UNIT2_C4",
                        "Synergy Core",
                        "unit2_C4",
                        "unit2_C4_Model.bin",
                        "unit2_C4_Anim.bin",
                        "unit2_C4_Collision.bin",
                        "unit2_c4_Tex.bin",
                        "unit2_C4_Ent.bin",
                        "unit2_C4_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-70f, 70f, -21.227783f))
                },
                {
                    "UNIT2_TP1",
                    new RoomMetadata(
                        "UNIT2_TP1",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit2_TP1_Ent.bin",
                        "unit2_TP1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x3,
                        1,
                        0x0,
                        FogColor(17, 29, 16),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(48f, -70f, 70f))
                },
                {
                    "UNIT2_B1",
                    new RoomMetadata(
                        "UNIT2_B1",
                        null,
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "Unit2_b1_Ent.bin",
                        "unit2_b1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_C6",
                    new RoomMetadata(
                        "UNIT2_C6",
                        "Tetra Vista",
                        "unit2_C6",
                        "unit2_C6_Model.bin",
                        "unit2_C6_Anim.bin",
                        "unit2_C6_Collision.bin",
                        "unit2_c6_Tex.bin",
                        "Unit2_C6_Ent.bin",
                        "unit2_C6_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(18, 31, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT2_C7",
                    new RoomMetadata(
                        "UNIT2_C7",
                        "New Arrival Registration",
                        "unit2_C7",
                        "unit2_C7_Model.bin",
                        "unit2_C7_Anim.bin",
                        "unit2_C7_Collision.bin",
                        "unit2_c7_Tex.bin",
                        "Unit2_C7_Ent.bin",
                        "unit2_C7_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_RM4",
                    new RoomMetadata(
                        "UNIT2_RM4",
                        "Transfer Lock",
                        "unit2_RM4",
                        "unit2_RM4_Model.bin",
                        "unit2_RM4_Anim.bin",
                        "unit2_RM4_Collision.bin",
                        "unit2_RM4_Tex.bin",
                        "Unit2_RM4_Ent.bin",
                        "unit2_RM4_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x1,
                        0x1,
                        1,
                        0x0,
                        FogColor(29, 20, 10),
                        0x4,
                        65152,
                        new ColorRgb(29, 20, 10),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT2_RM5",
                    new RoomMetadata(
                        "UNIT2_RM5",
                        "Incubation Vault 01",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "Unit2_RM5_Ent.bin",
                        "unit2_RM5_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x1,
                        0x1,
                        1,
                        0x0,
                        FogColor(0, 25, 31),
                        0x4,
                        31727,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-43f, 44f, 0f))
                },
                {
                    "UNIT2_RM6",
                    new RoomMetadata(
                        "UNIT2_RM6",
                        "Incubation Vault 02",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "Unit2_RM6_Ent.bin",
                        "unit2_RM6_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x2,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(33f, -33f, 55f))
                },
                {
                    "UNIT2_RM7",
                    new RoomMetadata(
                        "UNIT2_RM7",
                        "Incubation Vault 03",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "Unit2_RM7_Ent.bin",
                        "unit2_RM7_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x3,
                        1,
                        0x0,
                        FogColor(0, 31, 10),
                        0x4,
                        31727,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_RM8",
                    new RoomMetadata(
                        "UNIT2_RM8",
                        "Docking Bay",
                        "unit2_RM8",
                        "unit2_RM8_Model.bin",
                        "unit2_RM8_Anim.bin",
                        "unit2_RM8_Collision.bin",
                        "unit2_RM8_Tex.bin",
                        "unit2_RM8_Ent.bin",
                        "unit2_RM8_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x1,
                        0x1,
                        0,
                        0x0,
                        FogColor(29, 20, 10),
                        0x4,
                        65152,
                        new ColorRgb(29, 20, 10),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT2_TP2",
                    new RoomMetadata(
                        "UNIT2_TP2",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit2_TP2_Ent.bin",
                        "unit2_TP2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x4,
                        1,
                        0x0,
                        FogColor(17, 29, 16),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT2_B2",
                    new RoomMetadata(
                        "UNIT2_B2",
                        null,
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "Unit2_b2_Ent.bin",
                        "unit2_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT3_LAND",
                    new RoomMetadata(
                        "UNIT3_LAND",
                        "VDO Gateway",
                        "unit3_Land",
                        "unit3_Land_Model.bin",
                        "unit3_Land_Anim.bin",
                        "unit3_Land_Collision.bin",
                        "unit3_Land_Tex.bin",
                        "unit3_Land_Ent.bin",
                        "unit3_Land_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT3_C0",
                    new RoomMetadata(
                        "UNIT3_C0",
                        "Bioweaponry Lab",
                        "unit3_C0",
                        "unit3_C0_Model.bin",
                        "unit3_C0_Anim.bin",
                        "unit3_C0_Collision.bin",
                        "unit3_c0_Tex.bin",
                        "unit3_C0_Ent.bin",
                        "unit3_C0_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT3_C2",
                    new RoomMetadata(
                        "UNIT3_C2",
                        "Cortex CPU",
                        "unit3_c2",
                        "unit3_c2_Model.bin",
                        "unit3_c2_Anim.bin",
                        "unit3_c2_Collision.bin",
                        "unit3_c2_Tex.bin",
                        "Unit3_C2_Ent.bin",
                        "unit3_c2_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x32,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-22f, 18f, -2f))
                },
                {
                    "UNIT3_RM1",
                    new RoomMetadata(
                        "UNIT3_RM1",
                        "Weapons Complex",
                        "unit3_rm1",
                        "unit3_rm1_Model.bin",
                        "unit3_rm1_Anim.bin",
                        "unit3_rm1_Collision.bin",
                        "unit3_rm1_Tex.bin",
                        "Unit3_RM1_Ent.bin",
                        "unit3_RM1_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x32,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(13.299805f, -15f, 11f))
                },
                {
                    "UNIT3_RM4",
                    new RoomMetadata(
                        "UNIT3_RM4",
                        "Compression Chamber",
                        "mp5",
                        "mp5_Model.bin",
                        "mp5_Anim.bin",
                        "mp5_Collision.bin",
                        "mp5_Tex.bin",
                        "unit3_rm4_Ent.bin",
                        "unit3_RM4_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x32,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT3_TP1",
                    new RoomMetadata(
                        "UNIT3_TP1",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit3_TP1_Ent.bin",
                        "unit3_TP1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x5,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT3_B1",
                    new RoomMetadata(
                        "UNIT3_B1",
                        null,
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "Unit3_b1_Ent.bin",
                        "unit2_b1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT3_C1",
                    new RoomMetadata(
                        "UNIT3_C1",
                        "Ascension",
                        "unit3_C1",
                        "unit3_C1_Model.bin",
                        "unit3_C1_Anim.bin",
                        "unit3_C1_Collision.bin",
                        "unit3_c1_Tex.bin",
                        "unit3_C1_Ent.bin",
                        "unit3_C1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT3_RM2",
                    new RoomMetadata(
                        "UNIT3_RM2",
                        "Fuel Stack",
                        "unit3_rm2",
                        "unit3_rm2_Model.bin",
                        "unit3_rm2_Anim.bin",
                        "unit3_rm2_Collision.bin",
                        "unit3_rm2_Tex.bin",
                        "Unit3_RM2_Ent.bin",
                        "unit3_RM2_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT3_RM3",
                    new RoomMetadata(
                        "UNIT3_RM3",
                        "Stasis Bunker",
                        "e3Level",
                        "e3Level_Model.bin",
                        "e3Level_Anim.bin",
                        "e3Level_Collision.bin",
                        "e3Level_Tex.bin",
                        "Unit3_RM3_Ent.bin",
                        "unit3_RM3_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT3_TP2",
                    new RoomMetadata(
                        "UNIT3_TP2",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit3_TP2_Ent.bin",
                        "unit3_TP2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x6,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-19f, 27f, -1f))
                },
                {
                    "UNIT3_B2",
                    new RoomMetadata(
                        "UNIT3_B2",
                        null,
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "Unit3_b2_Ent.bin",
                        "unit2_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x2,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(15f, -29f, 26f))
                },
                {
                    "UNIT4_LAND",
                    new RoomMetadata(
                        "UNIT4_LAND",
                        "Arcterra Gateway",
                        "unit4_Land",
                        "unit4_Land_Model.bin",
                        "unit4_Land_Anim.bin",
                        "unit4_Land_Collision.bin",
                        "unit4_Land_Tex.bin",
                        "unit4_Land_Ent.bin",
                        "unit4_Land_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 16),
                        0x4,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT4_RM1",
                    new RoomMetadata(
                        "UNIT4_RM1",
                        "Ice Hive",
                        "unit4_RM1",
                        "unit4_RM1_Model.bin",
                        "unit4_RM1_Anim.bin",
                        "unit4_RM1_Collision.bin",
                        "unit4_RM1_Tex.bin",
                        "Unit4_RM1_Ent.bin",
                        "unit4_RM1_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x4,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT4_RM3",
                    new RoomMetadata(
                        "UNIT4_RM3",
                        "Sic Transit",
                        "mp12",
                        "mp12_Model.bin",
                        "mp12_Anim.bin",
                        "mp12_Collision.bin",
                        "mp12_Tex.bin",
                        "unit4_rm3_Ent.bin",
                        "unit4_RM3_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 16),
                        0x4,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-23f, 41f, -29f))
                },
                {
                    "UNIT4_C0",
                    new RoomMetadata(
                        "UNIT4_C0",
                        "Frost Labyrinth",
                        "unit4_C0",
                        "unit4_C0_Model.bin",
                        "unit4_C0_Anim.bin",
                        "unit4_C0_Collision.bin",
                        "unit4_c0_Tex.bin",
                        "unit4_C0_Ent.bin",
                        "unit4_C0_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x4,
                        65300,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(30f, -27f, 42f))
                },
                {
                    "UNIT4_TP1",
                    new RoomMetadata(
                        "UNIT4_TP1",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit4_TP1_Ent.bin",
                        "unit4_TP1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x7,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT4_B1",
                    new RoomMetadata(
                        "UNIT4_B1",
                        null,
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "unit4_b1_Ent.bin",
                        "unit2_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT4_C1",
                    new RoomMetadata(
                        "UNIT4_C1",
                        "Drip Moat",
                        "unit4_c1",
                        "unit4_c1_Model.bin",
                        "unit4_c1_Anim.bin",
                        "unit4_c1_Collision.bin",
                        "unit4_c1_Tex.bin",
                        "unit4_C1_Ent.bin",
                        "unit4_c1_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x5,
                        0x1,
                        1,
                        0x1,
                        FogColor(0, 0, 0),
                        0x4,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-32f, 31f, -3.599854f))
                },
                {
                    "UNIT4_RM2",
                    new RoomMetadata(
                        "UNIT4_RM2",
                        "Subterranean",
                        "unit4_rm2",
                        "unit4_rm2_Model.bin",
                        "unit4_rm2_Anim.bin",
                        "unit4_rm2_Collision.bin",
                        "unit4_rm2_Tex.bin",
                        "Unit4_RM2_Ent.bin",
                        "unit4_RM2_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x5,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 16),
                        0x4,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(24f, -30f, 29f))
                },
                {
                    "UNIT4_RM4",
                    new RoomMetadata(
                        "UNIT4_RM4",
                        "Sanctorus",
                        "mp11",
                        "mp11_Model.bin",
                        "mp11_Anim.bin",
                        "mp11_Collision.bin",
                        "mp11_Tex.bin",
                        "unit4_rm4_Ent.bin",
                        "unit4_RM4_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x4,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "UNIT4_RM5",
                    new RoomMetadata(
                        "UNIT4_RM5",
                        "Fault Line",
                        "unit4_rm5",
                        "unit4_rm5_Model.bin",
                        "unit4_rm5_Anim.bin",
                        "unit4_rm5_Collision.bin",
                        "unit4_rm5_Tex.bin",
                        "Unit4_RM5_Ent.bin",
                        "unit4_RM5_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x5,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 16),
                        0x4,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "UNIT4_TP2",
                    new RoomMetadata(
                        "UNIT4_TP2",
                        null,
                        "TeleportRoom",
                        "TeleportRoom_Model.bin",
                        "TeleportRoom_Anim.bin",
                        "TeleportRoom_Collision.bin",
                        "TeleportRoom_Tex.bin",
                        "Unit4_TP2_Ent.bin",
                        "unit4_TP2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x7,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x6,
                        65350,
                        new ColorRgb(8, 28, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-72f, 70f, 0f))
                },
                {
                    "UNIT4_B2",
                    new RoomMetadata(
                        "UNIT4_B2",
                        null,
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "unit4_b2_Ent.bin",
                        "unit2_b1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(12, 6, 18),
                        0x4,
                        65180,
                        new ColorRgb(12, 6, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(31, 25, 21),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(23.5f, -26f, 26f))
                },
                {
                    "Gorea_Land",
                    new RoomMetadata(
                        "Gorea_Land",
                        null,
                        "Gorea_Land",
                        "Gorea_Land_Model.bin",
                        "Gorea_Land_Anim.bin",
                        "Gorea_Land_Collision.bin",
                        "Gorea_Land_Tex.bin",
                        "Gorea_Land_Ent.bin",
                        "Gorea_Land_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(17, 29, 16),
                        0x6,
                        65330,
                        new ColorRgb(17, 29, 16),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "Gorea_Peek",
                    new RoomMetadata(
                        "Gorea_Peek",
                        null,
                        "Gorea_b2",
                        "Gorea_b2_Model.bin",
                        "Gorea_b2_Anim.bin",
                        "Gorea_b2_Collision.bin",
                        "Gorea_b2_Tex.bin",
                        "Gorea_Peek_Ent.bin",
                        "Gorea_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(17, 29, 16),
                        0x5,
                        65535,
                        new ColorRgb(19, 27, 16),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 6, 12),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f))
                },
                {
                    "Gorea_b1",
                    new RoomMetadata(
                        "Gorea_b1",
                        null,
                        "Gorea_b1",
                        "Gorea_b1_Model.bin",
                        "Gorea_b1_Anim.bin",
                        "Gorea_b1_Collision.bin",
                        "Gorea_b1_Tex.bin",
                        "Gorea_b1_Ent.bin",
                        "Gorea_b1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x1,
                        FogColor(9, 18, 24),
                        0x4,
                        32550,
                        new ColorRgb(18, 24, 27),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 18, 24),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-72f, 70f, 0f))
                },
                {
                    "Gorea_b2",
                    new RoomMetadata(
                        "Gorea_b2",
                        null,
                        "Gorea_b2",
                        "Gorea_b2_Model.bin",
                        "Gorea_b2_Anim.bin",
                        "Gorea_b2_Collision.bin",
                        "Gorea_b2_Tex.bin",
                        "gorea_b2_Ent.bin",
                        "Gorea_b2_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x2,
                        1,
                        0x0,
                        FogColor(16, 30, 25),
                        0x4,
                        65535,
                        new ColorRgb(18, 16, 14),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(27, 18, 9),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(23.5f, -26f, 26f))
                },
                {
                    "MP1 SANCTORUS",
                    new RoomMetadata(
                        "MP1 SANCTORUS",
                        "Data Shrine",
                        "mp1",
                        "mp1_Model.bin",
                        "mp1_Anim.bin",
                        "mp1_Collision.bin",
                        "mp1_Tex.bin",
                        "mp1_Ent.bin",
                        "mp1_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "MP2 HARVESTER",
                    new RoomMetadata(
                        "MP2 HARVESTER",
                        "Harvester",
                        "mp2",
                        "mp2_Model.bin",
                        "mp2_Anim.bin",
                        "mp2_Collision.bin",
                        "mp2_Tex.bin",
                        "mp2_Ent.bin",
                        "mp2_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x1,
                        FogColor(25, 30, 20),
                        0x4,
                        65152,
                        new ColorRgb(25, 30, 20),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 11, 6),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "MP3 PROVING GROUND",
                    new RoomMetadata(
                        "MP3 PROVING GROUND",
                        "Combat Hall",
                        "mp3",
                        "mp3_Model.bin",
                        "mp3_Anim.bin",
                        "mp3_Collision.bin",
                        "mp3_Tex.bin",
                        "mp3_Ent.bin",
                        "mp3_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x5,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0.099854f, -1f, 0f),
                        new Vector3(-76f, 81f, -7.127930f),
                        multiplayer: true)
                },
                {
                    "MP4 HIGHGROUND - EXPANDED",
                    new RoomMetadata(
                        "MP4 HIGHGROUND - EXPANDED",
                        "Elder Passage",
                        "mp4",
                        "mp4_Model.bin",
                        "mp4_Anim.bin",
                        "mp4_Collision.bin",
                        "mp4_Tex.bin",
                        "mp4_Ent.bin",
                        "mp4_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x5,
                        65200,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(32f, -42f, 31f),
                        multiplayer: true)
                },
                {
                    "MP4 HIGHGROUND",
                    new RoomMetadata(
                        "MP4 HIGHGROUND",
                        "High Ground",
                        "unit1_rm1",
                        "unit1_rm1_Model.bin",
                        "unit1_rm1_Anim.bin",
                        "unit1_rm1_Collision.bin",
                        "unit1_rm1_Tex.bin",
                        "mp4_dm1_Ent.bin",
                        "mp4_dm1_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x5,
                        65200,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-76f, 81f, -7.127930f),
                        multiplayer: true)
                },
                {
                    "MP5 FUEL SLUICE",
                    new RoomMetadata(
                        "MP5 FUEL SLUICE",
                        "Compression Chamber",
                        "mp5",
                        "mp5_Model.bin",
                        "mp5_Anim.bin",
                        "mp5_Collision.bin",
                        "mp5_Tex.bin",
                        "mp5_Ent.bin",
                        "mp5_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x3,
                        65152,
                        new ColorRgb(18, 22, 30),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(32f, -40.748779f, 31f),
                        multiplayer: true)
                },
                {
                    "MP6 HEADSHOT",
                    new RoomMetadata(
                        "MP6 HEADSHOT",
                        "Head Shot",
                        "mp6",
                        "mp6_Model.bin",
                        "mp6_Anim.bin",
                        "mp6_Collision.bin",
                        "mp6_Tex.bin",
                        "mp6_Ent.bin",
                        "mp6_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x3,
                        65152,
                        new ColorRgb(18, 22, 30),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-72f, 72f, -3f),
                        multiplayer: true)
                },
                {
                    "MP7 PROCESSOR CORE",
                    new RoomMetadata(
                        "MP7 PROCESSOR CORE",
                        "Processor Core",
                        "mp7",
                        "mp7_Model.bin",
                        "mp7_Anim.bin",
                        "mp7_Collision.bin",
                        "mp7_Tex.bin",
                        "mp7_Ent.bin",
                        "mp7_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 18, 6),
                        0x6,
                        65300,
                        new ColorRgb(31, 18, 6),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 6, 4),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(23f, -42f, 42f),
                        multiplayer: true)
                },
                {
                    "MP8 FIRE CONTROL",
                    new RoomMetadata(
                        "MP8 FIRE CONTROL",
                        "Weapons Complex",
                        "mp8",
                        "mp8_Model.bin",
                        "mp8_Anim.bin",
                        "mp8_Collision.bin",
                        "mp8_Tex.bin",
                        "mp8_Ent.bin",
                        "mp8_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x32,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 24, 31),
                        0x3,
                        65152,
                        new ColorRgb(18, 22, 30),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "MP9 CRYOCHASM",
                    new RoomMetadata(
                        "MP9 CRYOCHASM",
                        "Ice Hive",
                        "mp9",
                        "mp9_Model.bin",
                        "mp9_Anim.bin",
                        "mp9_Collision.bin",
                        "mp9_Tex.bin",
                        "mp9_Ent.bin",
                        "mp9_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x5,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "MP10 OVERLOAD",
                    new RoomMetadata(
                        "MP10 OVERLOAD",
                        "Incubation Vault",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "mp10_Ent.bin",
                        "mp10_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x5,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-68f, 60f, 0f),
                        multiplayer: true)
                },
                {
                    "MP11 BREAKTHROUGH",
                    new RoomMetadata(
                        "MP11 BREAKTHROUGH",
                        "Sanctorus",
                        "mp11",
                        "mp11_Model.bin",
                        "mp11_Anim.bin",
                        "mp11_Collision.bin",
                        "mp11_Tex.bin",
                        "mp11_Ent.bin",
                        "mp11_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(20, 27, 31),
                        0x5,
                        65152,
                        new ColorRgb(20, 27, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(8, 8, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(36f, -27f, 26f),
                        multiplayer: true)
                },
                {
                    "MP12 SIC TRANSIT",
                    new RoomMetadata(
                        "MP12 SIC TRANSIT",
                        "Sic Transit",
                        "mp12",
                        "mp12_Model.bin",
                        "mp12_Anim.bin",
                        "mp12_Collision.bin",
                        "mp12_Tex.bin",
                        "mp12_Ent.bin",
                        "mp12_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 18),
                        0x6,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "MP13 ACCELERATOR",
                    new RoomMetadata(
                        "MP13 ACCELERATOR",
                        "Fuel Stack",
                        "mp13",
                        "mp13_Model.bin",
                        "mp13_Anim.bin",
                        "mp13_Collision.bin",
                        "mp13_Tex.bin",
                        "mp13_Ent.bin",
                        "mp13_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x3,
                        65152,
                        new ColorRgb(18, 22, 30),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "MP14 OUTER REACH",
                    new RoomMetadata(
                        "MP14 OUTER REACH",
                        "Outer Reach",
                        "mp14",
                        "mp14_Model.bin",
                        "mp14_Anim.bin",
                        "mp14_Collision.bin",
                        "mp14_tex.bin",
                        "mp14_Ent.bin",
                        "mp14_Node.bin",
                        null,
                        TimeLimit(8, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x4,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-68f, 60f, 0f),
                        multiplayer: true)
                },
                {
                    "CTF1 FAULT LINE - EXPANDED",
                    new RoomMetadata(
                        "CTF1 FAULT LINE - EXPANDED",
                        "Fault Line",
                        "ctf1",
                        "ctf1_Model.bin",
                        "ctf1_Anim.bin",
                        "ctf1_Collision.bin",
                        "ctf1_Tex.bin",
                        "ctf1_Ent.bin",
                        "ctf1_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x5,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 18),
                        0x6,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(39f, -27f, 26f),
                        multiplayer: true)
                },
                {
                    "CTF1_FAULT LINE",
                    new RoomMetadata(
                        "CTF1_FAULT LINE",
                        "Subterranean",
                        "unit4_rm5",
                        "unit4_rm5_Model.bin",
                        "unit4_rm5_Anim.bin",
                        "unit4_rm5_Collision.bin",
                        "unit4_rm5_Tex.bin",
                        "ctf1_dm1_Ent.bin",
                        "ctf1_dm1_Node.bin",
                        null,
                        TimeLimit(5, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x5,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 18),
                        0x6,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "AD1 TRANSFER LOCK BT",
                    new RoomMetadata(
                        "AD1 TRANSFER LOCK BT",
                        "Transfer Lock",
                        "ad1",
                        "ad1_Model.bin",
                        "ad1_Anim.bin",
                        "ad1_Collision.bin",
                        "ad1_Tex.bin",
                        "ad1_Ent.bin",
                        "ad1_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x1,
                        0x1,
                        0,
                        0x0,
                        FogColor(29, 20, 10),
                        0x4,
                        65152,
                        new ColorRgb(29, 20, 10),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "AD1 TRANSFER LOCK DM",
                    new RoomMetadata(
                        "AD1 TRANSFER LOCK DM",
                        "Transfer Lock",
                        "unit2_rm4",
                        "unit2_rm4_Model.bin",
                        "unit2_rm4_Anim.bin",
                        "unit2_rm4_Collision.bin",
                        "unit2_rm4_Tex.bin",
                        "ad1_dm1_Ent.bin",
                        "ad1_dm1_Node.bin",
                        "rmGoal",
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x1,
                        0x1,
                        1,
                        0x0,
                        FogColor(29, 20, 10),
                        0x4,
                        65300,
                        new ColorRgb(29, 20, 10),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-71f, 85f, 2f),
                        multiplayer: true)
                },
                {
                    "AD2 MAGMA VENTS",
                    new RoomMetadata(
                        "AD2 MAGMA VENTS",
                        "Council Chamber",
                        "ad2",
                        "ad2_Model.bin",
                        "ad2_Anim.bin",
                        "ad2_Collision.bin",
                        "ad2_Tex.bin",
                        "ad2_Ent.bin",
                        "ad2_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x5,
                        65200,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(21f, -53f, 59f),
                        multiplayer: true)
                },
                {
                    "AD2 ALINOS PERCH",
                    new RoomMetadata(
                        "AD2 ALINOS PERCH",
                        "Alinos Perch",
                        "unit1_rm2",
                        "unit1_rm2_Model.bin",
                        "unit1_rm2_Anim.bin",
                        "unit1_rm2_Collision.bin",
                        "unit1_rm2_Tex.bin",
                        "ad2_dm1_Ent.bin",
                        "ad2_dm1_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x5,
                        65200,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "UNIT1 ALINOS LANDFALL",
                    new RoomMetadata(
                        "UNIT1 ALINOS LANDFALL",
                        "Alinos Gateway",
                        "unit1_Land",
                        "unit1_Land_Model.bin",
                        "unit1_Land_Anim.bin",
                        "unit1_Land_Collision.bin",
                        "unit1_Land_Tex.bin",
                        "Unit1_Land_dm1_Ent.bin",
                        "unit1_Land_dm1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(31, 24, 18),
                        0x4,
                        65180,
                        new ColorRgb(31, 24, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(13, 12, 7),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "UNIT2 LANDING BAY",
                    new RoomMetadata(
                        "UNIT2 LANDING BAY",
                        "Celestial Gateway",
                        "unit2_Land",
                        "unit2_Land_Model.bin",
                        "unit2_Land_Anim.bin",
                        "unit2_Land_Collision.bin",
                        "unit2_Land_Tex.bin",
                        "unit2_land_dm1_Ent.bin",
                        "unit2_Land_dm1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgb(24, 31, 24),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(7, 11, 15),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-23f, 25f, -11f),
                        multiplayer: true)
                },
                {
                    "UNIT 3 VESPER STARPORT",
                    new RoomMetadata(
                        "UNIT 3 VESPER STARPORT",
                        "VDO Gateway",
                        "unit3_Land",
                        "unit3_Land_Model.bin",
                        "unit3_Land_Anim.bin",
                        "unit3_Land_Collision.bin",
                        "unit3_Land_Tex.bin",
                        "unit3_Land_dm1_Ent.bin",
                        "unit3_Land_dm1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x3,
                        65152,
                        new ColorRgb(18, 22, 30),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(28f, -27f, 26f),
                        multiplayer: true)
                },
                {
                    "UNIT 4 ARCTERRA BASE",
                    new RoomMetadata(
                        "UNIT 4 ARCTERRA BASE",
                        "Arcterra Gateway",
                        "unit4_land",
                        "unit4_land_Model.bin",
                        "unit4_land_Anim.bin",
                        "unit4_land_Collision.bin",
                        "unit4_land_Tex.bin",
                        "unit4_Land_dm1_Ent.bin",
                        "unit4_land_dm1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(10, 14, 18),
                        0x6,
                        65300,
                        new ColorRgb(10, 14, 18),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(4, 4, 8),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "Gorea Prison",
                    new RoomMetadata(
                        "Gorea Prison",
                        "Oubliette",
                        "Gorea_b2",
                        "Gorea_b2_Model.bin",
                        "Gorea_b2_Anim.bin",
                        "Gorea_b2_Collision.bin",
                        "Gorea_b2_Tex.bin",
                        "gorea_b2_dm_Ent.bin",
                        "Gorea_b2_dm_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x64,
                        0x1,
                        1,
                        0x0,
                        FogColor(16, 30, 25),
                        0x4,
                        65535,
                        new ColorRgb(18, 16, 14),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(27, 18, 9),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(300f, -300f, 300f),
                        multiplayer: true)
                },
                {
                    "E3 FIRST HUNT",
                    new RoomMetadata(
                        "E3 FIRST HUNT",
                        "Stasis Bunker",
                        "e3Level",
                        "e3Level_Model.bin",
                        "e3Level_Anim.bin",
                        "e3Level_Collision.bin",
                        "e3level_Tex.bin",
                        "e3Level_Ent.bin",
                        "e3Level_Node.bin",
                        null,
                        TimeLimit(6, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x3,
                        0x1,
                        1,
                        0x0,
                        FogColor(24, 20, 31),
                        0x5,
                        65152,
                        new ColorRgb(24, 20, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(9, 8, 14),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-44f, 42f, -5f),
                        multiplayer: true)
                },
                // model, animation, collision files for these two test levels must be taken from First Hunt
                {
                    "Level TestLevel",
                    new RoomMetadata(
                        "Level TestLevel",
                        "Test Level",
                        "testLevel",
                        "testLevel_Model.bin",
                        "testLevel_Anim.bin",
                        "testLevel_Collision.bin",
                        null,
                        "testlevel_Ent.bin",
                        "testLevel_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        1,
                        0x0,
                        FogColor(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(10, 10, 31),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(29f, -42f, 42f))
                },
                {
                    "Level AbeTest",
                    new RoomMetadata(
                        "Level AbeTest",
                        "Abe Test Level",
                        "testLevel",
                        "testLevel_Model.bin",
                        "testLevel_Anim.bin",
                        "testLevel_Collision.bin",
                        null,
                        "testLevelAbe1_Ent.bin",
                        "testLevelAbe1_Node.bin",
                        null,
                        TimeLimit(20, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x32,
                        0x0,
                        1,
                        0x0,
                        FogColor(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.099854f, -1f, 0f),
                        new ColorRgb(10, 10, 31),
                        new Vector3(0f, 0.999756f, -0.099854f),
                        new Vector3(-300f, 300f, -300f))
                },
                // these levels are unused/unreferenced in the game, so some values are guesses
                {
                    "biodefense chamber 06",
                    new RoomMetadata(
                        "biodefense chamber 06",
                        "Early Processor Core",
                        "unit1_b2",
                        "unit1_b2_Model.bin",
                        "unit1_b2_Anim.bin",
                        "unit1_b2_Collision.bin",
                        null,
                        null, // Unit1_b2_Ent is used in Cretaphid boss room
                        "unit1_b2_node.bin", // todo?
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "biodefense chamber 05",
                    new RoomMetadata(
                        "biodefense chamber 05",
                        "Early Stasis Bunker",
                        "unit2_b2",
                        "unit2_b2_Model.bin",
                        "unit2_b2_Anim.bin",
                        "unit2_b2_Collision.bin",
                        null,
                        null, // Unit2_b2_Ent is used in Slench boss room
                        null, // unit2_b2_node is used in Slench boos room
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "biodefense chamber 03",
                    new RoomMetadata(
                        "biodefense chamber 03",
                        "Early Head Shot",
                        "unit3_b1",
                        "unit3_b1_Model.bin",
                        "unit3_b1_Anim.bin",
                        "unit3_b1_Collision.bin",
                        null,
                        null, // Unit3_b1_Ent is used in Cretaphid boss room
                        "unit3_b1_node.bin", // todo?
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "biodefense chamber 08",
                    new RoomMetadata(
                        "biodefense chamber 08",
                        "Early Fuel Stack",
                        "unit3_b2",
                        "unit3_b2_Model.bin",
                        "unit3_b2_Anim.bin",
                        "unit3_b2_Collision.bin",
                        null,
                        null, // Unit3_b2_Ent is used in Slench boss room
                        "unit3_b2_node.bin", // todo?
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "biodefense chamber 04",
                    new RoomMetadata(
                        "biodefense chamber 04",
                        "Early Sanctorus",
                        "unit4_b1",
                        "unit4_b1_Model.bin",
                        "unit4_b1_Anim.bin",
                        "unit4_b1_Collision.bin",
                        null,
                        null, // unit4_b1_Ent is used in Slench boss room
                        "unit4_b1_Node.bin", // todo: ?
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "biodefense chamber 07",
                    new RoomMetadata(
                        "biodefense chamber 07",
                        "Early Sic Transit",
                        "unit4_b2",
                        "unit4_b2_Model.bin",
                        "unit4_b2_Anim.bin",
                        "unit4_b2_Collision.bin",
                        null,
                        null, // unit4_b2_Ent is used in Cretaphid boss room
                        "unit4_b2_Node.bin", // todo: ?
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                // First Hunt
                {
                    "FH_MP1",
                    new RoomMetadata(
                        "FH_MP1",
                        "Trooper Module",
                        @"_fh\mp1",
                        "mp1_Model.bin",
                        "mp1_Anim.bin",
                        "mp1_Collision.bin",
                        null,
                        @"_fh\mp1_Ent.bin",
                        @"_fh\mp1_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "FH_SURVIVOR",
                    new RoomMetadata(
                        "FH_SURVIVOR",
                        "Survivor",
                        @"_fh\mp2",
                        "mp2_Model.bin",
                        "mp2_Anim.bin",
                        "mp2_Collision.bin",
                        null,
                        @"_fh\survivor_Ent.bin", // survivor_Ent.bin
                        @"_fh\survivor_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "FH_MP2",
                    new RoomMetadata(
                        "FH_MP2",
                        "Assault Cradle",
                        @"_fh\mp2",
                        "mp2_Model.bin",
                        "mp2_Anim.bin",
                        "mp2_Collision.bin",
                        null,
                        @"_fh\mp2_Ent.bin",
                        @"_fh\mp2_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "FH_MP3",
                    new RoomMetadata(
                        "FH_MP3",
                        "Ancient Vestige",
                        @"_fh\mp3",
                        "mp3_Model.bin",
                        "mp3_Anim.bin",
                        "mp3_Collision.bin",
                        null,
                        @"_fh\mp3_Ent.bin",
                        @"_fh\mp3_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "FH_MP5",
                    new RoomMetadata(
                        "FH_MP5",
                        "Early Head Shot (First Hunt)",
                        @"_fh\mp5",
                        "mp5_Model.bin",
                        "mp5_Anim.bin",
                        "mp5_Collision.bin",
                        null,
                        @"_fh\mp5_Ent.bin",
                        @"_fh\mp5_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                },
                {
                    "FH_TEST",
                    new RoomMetadata(
                        "FH_TEST",
                        "Test Level (First Hunt)",
                        @"_fh\testLevel",
                        "testLevel_Model.bin",
                        "testlevel_Anim.bin",
                        "testlevel_Collision.bin",
                        null,
                        @"_fh\testlevel_Ent.bin",
                        @"_fh\testLevel_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "FH_REGULATOR",
                    new RoomMetadata(
                        "FH_REGULATOR",
                        "Regulator",
                        @"_fh\blueRoom",
                        "blueRoom_Model.bin",
                        "blueRoom_Anim.bin",
                        "blueRoom_Collision.bin",
                        null,
                        @"_fh\regulator_Ent.bin", // regulator_Ent.bin
                        @"_fh\regulator_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "FH_MORPHBALL",
                    new RoomMetadata(
                        "FH_MORPHBALL",
                        "Morph Ball",
                        @"_fh\e3Level",
                        "e3Level_Model.bin",
                        "e3Level_Anim.bin",
                        "e3Level_Collision.bin",
                        null,
                        @"_fh\morphBall_Ent.bin", // morphBall_Ent.bin
                        @"_fh\morphBall_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f))
                },
                {
                    "FH_E3",
                    new RoomMetadata(
                        "FH_E3",
                        "Stasis Bunker (First Hunt)",
                        @"_fh\e3Level",
                        "e3Level_Model.bin",
                        "e3Level_Anim.bin",
                        "e3Level_Collision.bin",
                        null,
                        @"_fh\e3Level_Ent.bin",
                        @"_fh\e3Level_Node.bin",
                        null,
                        TimeLimit(10, 0, 0),
                        TimeLimit(2, 0, 0),
                        0x0,
                        0x0,
                        0,
                        0x0,
                        0,
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.4082031f, -0.8164063f, -0.4082031f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0.0f, 0.96875f, -0.2441406f),
                        new Vector3(-300f, 300f, -300f),
                        multiplayer: true)
                }
            };

        public static readonly IReadOnlyList<DoorMetadata> Doors = new List<DoorMetadata>()
        {
            /* 0 */ new DoorMetadata("AlimbicDoor", "AlimbicDoorLock", 1.39990234f),
            /* 1 */ new DoorMetadata("AlimbicMorphBallDoor", "AlimbicMorphBallDoorLock", 0.6999512f),
            /* 2 */ new DoorMetadata("AlimbicBossDoor", "AlimbicBossDoorLock", 3.5f),
            /* 3 */ new DoorMetadata("AlimbicThinDoor", "ThinDoorLock", 1.39990234f)
        };

        public static readonly IReadOnlyList<int> DoorPalettes = new List<int>()
        {
            0, 1, 2, 7, 6, 3, 4, 5, 0, 0
        };

        public static readonly IReadOnlyList<string> JumpPads = new List<string>()
        {
            /* 0 */ "JumpPad",
            /* 1 */ "JumpPad_Alimbic",
            /* 2 */ "JumpPad_Ice",
            /* 3 */ "JumpPad_IceStation",
            /* 4 */ "JumpPad_Lava",
            /* 5 */ "JumpPad_Station"
        };

        public static readonly IReadOnlyList<string> Items
            = new List<string>()
        {
            /*  0 */ "pick_health_B",
            /*  1 */ "pick_health_A",
            /*  2 */ "pick_health_C",
            /*  3 */ "pick_dblDamage",
            /*  4 */ "PickUp_EnergyExp",
            /*  5 */ "pick_wpn_electro",
            /*  6 */ "PickUp_MissileExp",
            /*  7 */ "pick_wpn_jackhammer",
            /*  8 */ "pick_wpn_snipergun",
            /*  9 */ "pick_wpn_shotgun",
            /* 10 */ "pick_wpn_mortar",
            /* 11 */ "pick_wpn_ghostbuster",
            /* 12 */ "pick_wpn_gorea",
            /* 13 */ "pick_ammo_green",
            /* 14 */ "pick_ammo_green",
            /* 15 */ "pick_ammo_orange",
            /* 16 */ "pick_ammo_orange",
            /* 17 */ "pick_invis",
            /* 18 */ "PickUp_AmmoExp",
            /* 19 */ "Artifact_Key",
            /* 20 */ "pick_deathball",
            /* 21 */ "pick_wpn_all",
            // unused
            /* 22 */ "pick_wpn_missile"
        };

        public static readonly IReadOnlyList<string> FhItems
            = new List<string>()
        {
            /* 0 */ "pick_ammo_A",
            /* 1 */ "pick_ammo_B",
            /* 2 */ "pick_health_A",
            /* 3 */ "pick_health_B",
            /* 4 */ "pick_dblDamage",
            /* 5 */ "pick_morphball", // unused
            /* 6 */ "pick_wpn_electro",
            /* 7 */ "pick_wpn_missile"
        };

        private static readonly IReadOnlyList<ObjectMetadata> _objects = new List<ObjectMetadata>()
        {
            /*  0 */ new ObjectMetadata("AlimbicGhost_01", 0),
            /*  1 */ new ObjectMetadata("AlimbicLightPole", 0),
            /*  2 */ new ObjectMetadata("AlimbicStationShieldControl", 0),
            /*  3 */ new ObjectMetadata("AlimbicComputerStationControl", 0),
            /*  4 */ new ObjectMetadata("AlimbicEnergySensor", 0),
            /*  5 */ new ObjectMetadata("SamusShip", 0), // unused
            /*  6 */ new ObjectMetadata("Guardbot01_Dead", 0),
            /*  7 */ new ObjectMetadata("Guardbot02_Dead", 0),
            /*  8 */ new ObjectMetadata("Guardian_Dead", 0),
            /*  9 */ new ObjectMetadata("Psychobit_Dead", 0),
            /* 10 */ new ObjectMetadata("AlimbicLightPole02", 0),
            /* 11 */ new ObjectMetadata("AlimbicComputerStationControl02", 0),
            /* 12 */ new ObjectMetadata("Generic_Console", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 13 */ new ObjectMetadata("Generic_Monitor", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 14 */ new ObjectMetadata("Generic_Power", 0),
            /* 15 */ new ObjectMetadata("Generic_Scanner", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 16 */ new ObjectMetadata("Generic_Switch", 1, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 17 */ new ObjectMetadata("Alimbic_Console", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 18 */ new ObjectMetadata("Alimbic_Monitor", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 19 */ new ObjectMetadata("Alimbic_Power", 0),
            /* 20 */ new ObjectMetadata("Alimbic_Scanner", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 21 */ new ObjectMetadata("Alimbic_Switch", 1, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 22 */ new ObjectMetadata("Lava_Console", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 23 */ new ObjectMetadata("Lava_Monitor", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 24 */ new ObjectMetadata("Lava_Power", 0),
            /* 25 */ new ObjectMetadata("Lava_Scanner", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 26 */ new ObjectMetadata("Lava_Switch", 1, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 27 */ new ObjectMetadata("Ice_Console", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 28 */ new ObjectMetadata("Ice_Monitor", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 29 */ new ObjectMetadata("Ice_Power", 0),
            /* 30 */ new ObjectMetadata("Ice_Scanner", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 31 */ new ObjectMetadata("Ice_Switch", 1, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 32 */ new ObjectMetadata("Ruins_Console", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 33 */ new ObjectMetadata("Ruins_Monitor", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 34 */ new ObjectMetadata("Ruins_Power", 0),
            /* 35 */ new ObjectMetadata("Ruins_Scanner", 0, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 36 */ new ObjectMetadata("Ruins_Switch", 1, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 37 */ new ObjectMetadata("PlantCarnivarous_Branched", 0),
            /* 38 */ new ObjectMetadata("PlantCarnivarous_Pod", 0),
            /* 39 */ new ObjectMetadata("PlantCarnivarous_PodLeaves", 0),
            /* 40 */ new ObjectMetadata("PlantCarnivarous_Vine", 0),
            /* 41 */ new ObjectMetadata("GhostSwitch", 0),
            /* 42 */ new ObjectMetadata("Switch", 1),
            /* 43 */ new ObjectMetadata("Guardian_Stasis", 0, animationIds: new List<int>() { 0xFF, 0, 0, 0 }),
            /* 44 */ new ObjectMetadata("AlimbicStatue_lod0", 0, animationIds: new List<int>() { 0xFF, 0, 0, 0 }),
            /* 45 */ new ObjectMetadata("AlimbicCapsule", 0),
            /* 46 */ new ObjectMetadata("SniperTarget", 1, animationIds: new List<int>() { 0, 2, 1, 0 }),
            /* 47 */ new ObjectMetadata("SecretSwitch", 0, 1, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 48 */ new ObjectMetadata("SecretSwitch", 0, 2, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 49 */ new ObjectMetadata("SecretSwitch", 0, 3, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 50 */ new ObjectMetadata("SecretSwitch", 0, 4, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 51 */ new ObjectMetadata("SecretSwitch", 0, 5, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 52 */ new ObjectMetadata("SecretSwitch", 0, 6, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 53 */ new ObjectMetadata("WallSwitch", 1, animationIds: new List<int>() { 2, 0, 1, 0 })
        };

        public static ObjectMetadata GetObjectById(int id)
        {
            if (id < 0 || id > _objects.Count)
            {
                throw new ArgumentException(nameof(id));
            }
            return _objects[id];
        }

        private static readonly IReadOnlyList<PlatformMetadata?> _platforms = new List<PlatformMetadata?>()
        {
            /*  0 */ new PlatformMetadata("platform"),
            /*  1 */ null, // duplicate of 0
            /*  2 */ null, // todo: figure out what this "platform" is
            /*  3 */ new PlatformMetadata("Elevator"),
            /*  4 */ new PlatformMetadata("smasher"),
            /*  5 */ new PlatformMetadata("Platform_Unit4_C1", someFlag: 1),
            /*  6 */ new PlatformMetadata("pillar"),
            /*  7 */ new PlatformMetadata("Door_Unit4_RM1"),
            /*  8 */ new PlatformMetadata("SyluxShip", animationIds: new List<uint>() { UInt32.MaxValue, 1, 0, 2 }),
            /*  9 */ new PlatformMetadata("pistonmp7"),
            /* 10 */ new PlatformMetadata("unit3_brain", animationIds: new List<uint>() { 0, 0, 0, 0 }),
            /* 11 */ new PlatformMetadata("unit4_mover1", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 12 */ new PlatformMetadata("unit4_mover2", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 13 */ new PlatformMetadata("ElectroField1", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 14 */ new PlatformMetadata("Unit3_platform1"),
            /* 15 */ new PlatformMetadata("unit3_pipe1", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 16 */ new PlatformMetadata("unit3_pipe2", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 17 */ new PlatformMetadata("cylinderbase"),
            /* 18 */ new PlatformMetadata("unit3_platform"),
            /* 19 */ new PlatformMetadata("unit3_platform2"),
            /* 20 */ new PlatformMetadata("unit3_jar", animationIds: new List<uint>() { 0, 2, 1, 0 }, field20: 0, field24: 0),
            /* 21 */ new PlatformMetadata("SyluxTurret", animationIds: new List<uint>() { 3, 2, 1, 0 }, field20: 0, field24: 0),
            /* 22 */ new PlatformMetadata("unit3_jartop", animationIds: new List<uint>() { 0, 2, 1, 0 }, field20: 0, field24: 0),
            /* 23 */ new PlatformMetadata("SamusShip", animationIds: new List<uint>() { 1, 3, 2, 4 }, field20: 0, field24: 0),
            /* 24 */ new PlatformMetadata("unit1_land_plat1"),
            /* 25 */ new PlatformMetadata("unit1_land_plat2"),
            /* 26 */ new PlatformMetadata("unit1_land_plat3"),
            /* 27 */ new PlatformMetadata("unit1_land_plat4"),
            /* 28 */ new PlatformMetadata("unit1_land_plat5"),
            /* 29 */ new PlatformMetadata("unit2_c4_plat"),
            /* 30 */ new PlatformMetadata("unit2_land_elev"),
            /* 31 */ new PlatformMetadata("unit4_platform1"),
            /* 32 */ new PlatformMetadata("Crate01", animationIds: new List<uint>() { UInt32.MaxValue, UInt32.MaxValue, 0, 1 }),
            /* 33 */ new PlatformMetadata("unit1_mover1", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 34 */ new PlatformMetadata("unit1_mover2"),
            /* 35 */ new PlatformMetadata("unit2_mover1"),
            /* 36 */ new PlatformMetadata("unit4_mover3"),
            /* 37 */ new PlatformMetadata("unit4_mover4"),
            /* 38 */ new PlatformMetadata("unit3_mover1"),
            /* 39 */ new PlatformMetadata("unit2_c1_mover"),
            /* 40 */ new PlatformMetadata("unit3_mover2", animationIds: new List<uint>() { 0, 0, 0, 0 }, field20: 0, field24: 0),
            /* 41 */ new PlatformMetadata("piston_gorealand"),
            /* 42 */ new PlatformMetadata("unit4_tp2_artifact_wo"),
            /* 43 */ new PlatformMetadata("unit4_tp1_artifact_wo"),
            /* 44 */ new PlatformMetadata("SamusShip", animationIds: new List<uint>() { 1, 0, 2, 4 }, field20: 0, field24: 0)
        };

        public static PlatformMetadata? GetPlatformById(int id)
        {
            if (id < 0 || id > _platforms.Count)
            {
                throw new ArgumentException(nameof(id));
            }
            if (id == 1)
            {
                id = 0;
            }
            return _platforms[id];
        }

        // todo: e.g. lod1 in the model folder should have the animation files from the lod0 archive
        public static readonly IReadOnlyDictionary<string, ModelMetadata> ModelMetadata
            = new Dictionary<string, ModelMetadata>()
            {
                {
                    "AlimbicBossDoorLock",
                    new ModelMetadata("AlimbicBossDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicBossDoor",
                    new ModelMetadata("AlimbicBossDoor")
                },
                {
                    "AlimbicCapsule",
                    new ModelMetadata("AlimbicCapsule", collision: true)
                },
                {
                    "AlimbicComputerStationControl",
                    new ModelMetadata("AlimbicComputerStationControl")
                },
                {
                    "AlimbicComputerStationControl02",
                    new ModelMetadata("AlimbicComputerStationControl02")
                },
                {
                    "AlimbicDoorLock",
                    new ModelMetadata("AlimbicDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicDoor",
                    new ModelMetadata("AlimbicDoor",
                        modelPath: @"models\AlimbicDoor_Model.bin",
                        animationPath: @"models\AlimbicDoor_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 7, new List<int> { 1 } } })
                        })
                },
                {
                    "AlimbicEnergySensor",
                    new ModelMetadata("AlimbicEnergySensor")
                },
                {
                    "AlimbicGhost_01",
                    new ModelMetadata("AlimbicGhost_01")
                },
                {
                    "AlimbicLightPole",
                    new ModelMetadata("AlimbicLightPole", collision: true)
                },
                {
                    "AlimbicLightPole02",
                    new ModelMetadata("AlimbicLightPole02", collision: true)
                },
                {
                    "AlimbicMorphBallDoor",
                    new ModelMetadata("AlimbicMorphBallDoor",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicMorphBallDoorLock",
                    new ModelMetadata("AlimbicMorphBallDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicStationShieldControl",
                    new ModelMetadata("AlimbicStationShieldControl")
                },
                {
                    "AlimbicStatue_lod0",
                    new ModelMetadata("AlimbicStatue_lod0", remove: "_lod0", collision: true)
                },
                {
                    "AlimbicThinDoor",
                    new ModelMetadata("AlimbicThinDoor",
                        modelPath: @"models\AlimbicThinDoor_Model.bin",
                        animationPath: @"models\AlimbicThinDoor_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 7, new List<int> { 1, 2 } } })
                        })
                },
                {
                    "Alimbic_Console",
                    new ModelMetadata("Alimbic_Console",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Monitor",
                    new ModelMetadata("Alimbic_Monitor",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Power",
                    new ModelMetadata("Alimbic_Power",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Scanner",
                    new ModelMetadata("Alimbic_Scanner",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Switch",
                    new ModelMetadata("Alimbic_Switch",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Turret",
                    new ModelMetadata("Alimbic_Turret",
                        recolors: new List<string>()
                        {
                            "img_00",
                            "img_04",
                            "img_05"
                        },
                        animationPath: @"models\AlimbicTurret_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "alt_ice",
                    new ModelMetadata("alt_ice",
                        modelPath: @"_archives\common\alt_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        }, useLightSources: true)
                },
                {
                    "arcWelder1",
                    new ModelMetadata("arcWelder1", animation: false)
                },
                {
                    "arcWelder2",
                    new ModelMetadata("arcWelder2", animation: false)
                },
                {
                    "arcWelder3",
                    new ModelMetadata("arcWelder3", animation: false)
                },
                {
                    "arcWelder4",
                    new ModelMetadata("arcWelder4", animation: false)
                },
                {
                    "arcWelder5",
                    new ModelMetadata("arcWelder5", animation: false)
                },
                {
                    "ArtifactBase",
                    new ModelMetadata("ArtifactBase")
                },
                {
                    "Artifact_Key",
                    new ModelMetadata("Artifact_Key")
                },
                {
                    "Artifact01",
                    new ModelMetadata("Artifact01",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact02",
                    new ModelMetadata("Artifact02",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact03",
                    new ModelMetadata("Artifact03",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact04",
                    new ModelMetadata("Artifact04",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact05",
                    new ModelMetadata("Artifact05",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact06",
                    new ModelMetadata("Artifact06",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact07",
                    new ModelMetadata("Artifact07",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact08",
                    new ModelMetadata("Artifact08",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "balljump",
                    new ModelMetadata("balljump", animation: false)
                },
                {
                    "balljump_ray",
                    new ModelMetadata("balljump_ray")
                },
                {
                    "BarbedWarWasp",
                    new ModelMetadata("BarbedWarWasp",
                        recolors: new List<string>()
                        {
                            "img_00",
                            "img_02",
                            "img_03"
                        },
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "BigEyeBall",
                    new ModelMetadata("BigEyeBall")
                },
                {
                    "BigEyeNest",
                    new ModelMetadata("BigEyeNest")
                },
                {
                    "BigEyeShield",
                    new ModelMetadata("BigEyeShield")
                },
                {
                    "BigEyeSynapse_01",
                    new ModelMetadata("BigEyeSynapse_01", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_02",
                    new ModelMetadata("BigEyeSynapse_02", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_03",
                    new ModelMetadata("BigEyeSynapse_03", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_04",
                    new ModelMetadata("BigEyeSynapse_04", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeTurret",
                    new ModelMetadata("BigEyeTurret")
                },
                {
                    "blastcap",
                    new ModelMetadata("blastcap")
                },
                {
                    "brain_unit3_c2",
                    new ModelMetadata("brain_unit3_c2", animation: false)
                },
                {
                    "Chomtroid",
                    new ModelMetadata("Chomtroid", animation: false)
                },
                {
                    "Crate01",
                    new ModelMetadata("Crate01", collision: true)
                },
                {
                    "cylBossLaserBurn",
                    new ModelMetadata("cylBossLaserBurn")
                },
                {
                    "cylBossLaserColl",
                    new ModelMetadata("cylBossLaserColl")
                },
                {
                    "cylBossLaserG",
                    new ModelMetadata("cylBossLaserG")
                },
                {
                    "cylBossLaserY",
                    new ModelMetadata("cylBossLaserY")
                },
                {
                    "cylBossLaser",
                    new ModelMetadata("cylBossLaser")
                },
                {
                    "cylinderbase",
                    new ModelMetadata("cylinderbase", animation: false)
                },
                {
                    "CylinderBossEye",
                    new ModelMetadata("CylinderBossEye")
                },
                {
                    "CylinderBoss",
                    new ModelMetadata("CylinderBoss")
                },
                {
                    "deathParticle",
                    new ModelMetadata("deathParticle", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "deepspace",
                    new ModelMetadata("deepspace", archive: "shipSpace")
                },
                {
                    "Door_Unit4_RM1",
                    new ModelMetadata("Door_Unit4_RM1", animation: false, collision: true)
                },
                {
                    "DripStank_lod0",
                    new ModelMetadata("DripStank_lod0", remove: "_lod0")
                },
                {
                    "ElectroField1",
                    new ModelMetadata("ElectroField1", collision: true)
                },
                {
                    "Elevator",
                    new ModelMetadata("Elevator", animation: false, collision: true)
                },
                {
                    "EnemySpawner",
                    new ModelMetadata("EnemySpawner",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "energyBeam",
                    new ModelMetadata("energyBeam")
                },
                {
                    "filter",
                    new ModelMetadata("filter", animation: false, archive: "common")
                },
                {
                    "flagbase_bounty",
                    new ModelMetadata("flagbase_bounty")
                },
                {
                    "flagbase_cap",
                    new ModelMetadata("flagbase_cap")
                },
                {
                    "flagbase_ctf",
                    new ModelMetadata("flagbase_ctf",
                        recolors: new List<string>()
                        {
                            "orange_img",
                            "green_img"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "ForceField",
                    new ModelMetadata("ForceField",
                        modelPath: @"models\ForceField_Model.bin",
                        animationPath: @"models\ForceField_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 7, new List<int> { 0 } } })
                        })
                },
                // todo: these probably also use AlimbicPalettes
                {
                    "ForceFieldLock",
                    new ModelMetadata("ForceFieldLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "furlEffect",
                    new ModelMetadata("furlEffect")
                },
                {
                    "geemer",
                    new ModelMetadata("geemer")
                },
                {
                    "Generic_Console",
                    new ModelMetadata("Generic_Console",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Monitor",
                    new ModelMetadata("Generic_Monitor",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Power",
                    new ModelMetadata("Generic_Power",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Scanner",
                    new ModelMetadata("Generic_Scanner",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Switch",
                    new ModelMetadata("Generic_Switch",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "geo1",
                    new ModelMetadata("geo1", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "GhostSwitch",
                    new ModelMetadata("GhostSwitch",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "Gorea1A_lod0",
                    new ModelMetadata("Gorea1A_lod0", remove: "_lod0")
                },
                {
                    "Gorea1B_lod0",
                    new ModelMetadata("Gorea1B_lod0", remove: "_lod0")
                },
                {
                    "Gorea2_lod0",
                    new ModelMetadata("Gorea2_lod0", remove: "_lod0")
                },
                {
                    "goreaArmRegen",
                    new ModelMetadata("goreaArmRegen")
                },
                {
                    "goreaGeo",
                    new ModelMetadata("goreaGeo", animation: false, texture: true)
                },
                {
                    "goreaGrappleBeam",
                    new ModelMetadata("goreaGrappleBeam")
                },
                {
                    "goreaLaserColl",
                    new ModelMetadata("goreaLaserColl")
                },
                {
                    "goreaLaser",
                    new ModelMetadata("goreaLaser")
                },
                {
                    "goreaMeteor",
                    new ModelMetadata("goreaMeteor")
                },
                {
                    "goreaMindTrick",
                    new ModelMetadata("goreaMindTrick")
                },
                {
                    "gorea_gun",
                    new ModelMetadata("gorea_gun")
                },
                {
                    "Guardbot01_Dead",
                    new ModelMetadata("Guardbot01_Dead", animation: false)
                },
                {
                    "Guardbot02_Dead",
                    new ModelMetadata("Guardbot02_Dead", animation: false)
                },
                {
                    "GuardBot1",
                    new ModelMetadata("GuardBot1",
                        recolors: new List<string>()
                        {
                            "img_00",
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04"
                        },
                        animationPath: @"models\GuardBot01_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "GuardBot2_lod0",
                    new ModelMetadata("GuardBot2_lod0",
                        remove: "_lod0",
                        animationPath: @"models\GuardBot02_Anim.bin")
                },
                {
                    "Guardian_Dead",
                    new ModelMetadata("Guardian_Dead", animation: false)
                },
                // Note: pal_02-04 are copies of 01 (with the same 3 unused textures/palettes),
                // and pal_Team01-02 are broken if extracted with the main model's header info.
                {
                    "Guardian_lod0",
                    new ModelMetadata("Guardian_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01"
                        },
                        texture: true,
                        archive: "Guardian",
                        useLightSources: true)
                },
                {
                    "Guardian_lod1",
                    new ModelMetadata("Guardian_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01"
                        },
                        texture: true,
                        useLightSources: true)
                },
                // next two not part of the game's files, edited to allow choosing the unused recolors
                {
                    "GuardianR_lod0",
                    new ModelMetadata("GuardianR_lod0",
                        modelPath: @"_archives\Guardian\Guardian_lod0_Model.bin",
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\GuardianR_pal_01_Model.bin",
                                texturePath: @"models\Guardian_pal_01_Tex.bin",
                                palettePath: @"models\Guardian_pal_01_Tex.bin"),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\GuardianR_pal_02_Model.bin",
                                texturePath: @"models\Guardian_pal_02_Tex.bin",
                                palettePath: @"models\Guardian_pal_02_Tex.bin"),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\GuardianR_pal_03_Model.bin",
                                texturePath: @"models\Guardian_pal_03_Tex.bin",
                                palettePath: @"models\Guardian_pal_03_Tex.bin"),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\GuardianR_pal_04_Model.bin",
                                texturePath: @"models\Guardian_pal_04_Tex.bin",
                                palettePath: @"models\Guardian_pal_04_Tex.bin"),
                            new RecolorMetadata("pal_Team01",
                                modelPath: @"models\GuardianR_pal_Team01_Model.bin",
                                texturePath: @"models\GuardianR_pal_Team01_Tex.bin",
                                palettePath: @"models\GuardianR_pal_Team01_Tex.bin"),
                            new RecolorMetadata("pal_Team02",
                                modelPath: @"models\GuardianR_pal_Team02_Model.bin",
                                texturePath: @"models\GuardianR_pal_Team02_Tex.bin",
                                palettePath: @"models\GuardianR_pal_Team02_Tex.bin")
                        }, useLightSources: true)
                },
                {
                    "GuardianR_lod1",
                    new ModelMetadata("GuardianR_lod1",
                        modelPath: @"models\Guardian_lod1_Model.bin",
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\GuardianR_pal_01_Model.bin",
                                texturePath: @"models\Guardian_pal_01_Tex.bin",
                                palettePath: @"models\Guardian_pal_01_Tex.bin"),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\GuardianR_pal_02_Model.bin",
                                texturePath: @"models\Guardian_pal_02_Tex.bin",
                                palettePath: @"models\Guardian_pal_02_Tex.bin"),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\GuardianR_pal_03_Model.bin",
                                texturePath: @"models\Guardian_pal_03_Tex.bin",
                                palettePath: @"models\Guardian_pal_03_Tex.bin"),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\GuardianR_pal_04_Model.bin",
                                texturePath: @"models\Guardian_pal_04_Tex.bin",
                                palettePath: @"models\Guardian_pal_04_Tex.bin"),
                            new RecolorMetadata("pal_Team01",
                                modelPath: @"models\GuardianR_pal_Team01_Model.bin",
                                texturePath: @"models\GuardianR_pal_Team01_Tex.bin",
                                palettePath: @"models\GuardianR_pal_Team01_Tex.bin"),
                            new RecolorMetadata("pal_Team02",
                                modelPath: @"models\GuardianR_pal_Team02_Model.bin",
                                texturePath: @"models\GuardianR_pal_Team02_Tex.bin",
                                palettePath: @"models\GuardianR_pal_Team02_Tex.bin")
                        }, useLightSources: true)
                },
                {
                    "Guardian_Stasis",
                    new ModelMetadata("Guardian_Stasis")
                },
                {
                    "gunSmoke",
                    new ModelMetadata("gunSmoke", archive: "common")
                },
                {
                    "Ice_Console",
                    new ModelMetadata("Ice_Console",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Monitor",
                    new ModelMetadata("Ice_Monitor",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Power",
                    new ModelMetadata("Ice_Power",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Scanner",
                    new ModelMetadata("Ice_Scanner",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Switch",
                    new ModelMetadata("Ice_Switch",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "iceShard",
                    new ModelMetadata("iceShard", animation: false, archive: "common")
                },
                {
                    "iceWave",
                    new ModelMetadata("iceWave", archive: "common")
                },
                {
                    "items_base",
                    new ModelMetadata("items_base", animation: false, archive: "common")
                },
                {
                    "JumpPad_Alimbic",
                    new ModelMetadata("JumpPad_Alimbic")
                },
                {
                    "JumpPad_Beam",
                    new ModelMetadata("JumpPad_Beam")
                },
                {
                    "JumpPad_IceStation",
                    new ModelMetadata("JumpPad_IceStation")
                },
                {
                    "JumpPad_Ice",
                    new ModelMetadata("JumpPad_Ice")
                },
                {
                    "JumpPad_Lava",
                    new ModelMetadata("JumpPad_Lava")
                },
                {
                    "JumpPad",
                    new ModelMetadata("JumpPad")
                },
                {
                    "JumpPad_Station",
                    new ModelMetadata("JumpPad_Station")
                },
                {
                    "Kanden_lod0",
                    new ModelMetadata("Kanden_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Kanden",
                        useLightSources: true) // todo: confirm lod0 vs. lod1 for using light sources
                },
                {
                    "Kanden_lod1",
                    new ModelMetadata("Kanden_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Kanden\Kanden_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "KandenAlt_lod0",
                    new ModelMetadata("KandenAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Kanden",
                        recolorName: "Kanden",
                        useLightSources: true)
                },
                // note: confirmed this does not use light sources
                {
                    "KandenAlt_TailBomb",
                    new ModelMetadata("KandenAlt_TailBomb",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: false,
                        texture: true,
                        archive: "Kanden",
                        recolorName: "Kanden")
                },
                {
                    "KandenGun",
                    new ModelMetadata("KandenGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localKanden",
                        useLightSources: true)
                },
                {
                    "koth_data_flow",
                    new ModelMetadata("koth_data_flow", animation: false)
                },
                {
                    "koth_terminal",
                    new ModelMetadata("koth_terminal", animation: false)
                },
                {
                    "LavaDemon",
                    new ModelMetadata("LavaDemon",
                        recolors: new List<string>()
                        {
                            "img_00",
                            "img_03"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Console",
                    new ModelMetadata("Lava_Console",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Monitor",
                    new ModelMetadata("Lava_Monitor",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                // todo: file32Material uses texture+palette 8, but there are only 8 of each in LavaEquipTextureShare
                // --> in the other "_Power" models, there are 9 of each in the texture share, so index 8 works for them
                // --> need to do some in-game checking to see if this share is actually used, and if so, how it works
                // for now, referencing RuinsEquipTextureShare here to get it to render
                {
                    "Lava_Power",
                    new ModelMetadata("Lava_Power",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Scanner",
                    new ModelMetadata("Lava_Scanner",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Switch",
                    new ModelMetadata("Lava_Switch",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "lines",
                    new ModelMetadata("lines", addToAnim: "_Idle", archive: "frontend2d")
                },
                {
                    "MoverTest",
                    new ModelMetadata("MoverTest")
                },
                {
                    "Nox_lod0",
                    new ModelMetadata("Nox_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Nox",
                        useLightSources: true)
                },
                {
                    "Nox_lod1",
                    new ModelMetadata("Nox_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Nox\Nox_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "NoxAlt_lod0",
                    new ModelMetadata("NoxAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Nox",
                        recolorName: "Nox",
                        useLightSources: true)
                },
                {
                    "NoxGun",
                    new ModelMetadata("NoxGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localNox",
                        useLightSources: true)
                },
                {
                    "nox_ice",
                    new ModelMetadata("nox_ice",
                        modelPath: @"_archives\common\nox_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        }, useLightSources: true)
                },
                {
                    "octolith_ctf",
                    new ModelMetadata("octolith_ctf",
                        recolors: new List<string>()
                        {
                            "orange_img",
                            "green_img",
                            "*octolith_bounty_img"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Octolith",
                    new ModelMetadata("Octolith")
                },
                {
                    "octolith_simple",
                    new ModelMetadata("octolith_simple", animation: false)
                },
                {
                    "particles",
                    new ModelMetadata("particles", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "particles2",
                    new ModelMetadata("particles2", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "PickUp_AmmoExp",
                    new ModelMetadata("PickUp_AmmoExp", animation: false)
                },
                {
                    "PickUp_EnergyExp",
                    new ModelMetadata("PickUp_EnergyExp")
                },
                {
                    "PickUp_MissileExp",
                    new ModelMetadata("PickUp_MissileExp")
                },
                {
                    "pick_ammo_green",
                    new ModelMetadata("pick_ammo_green", animation: false)
                },
                {
                    "pick_ammo_orange",
                    new ModelMetadata("pick_ammo_orange", animation: false)
                },
                {
                    "pick_dblDamage",
                    new ModelMetadata("pick_dblDamage", animation: false)
                },
                {
                    "pick_deathball",
                    new ModelMetadata("pick_deathball", animation: false)
                },
                {
                    "pick_health_A",
                    new ModelMetadata("pick_health_A", animation: false)
                },
                {
                    "pick_health_B",
                    new ModelMetadata("pick_health_B", animation: false)
                },
                {
                    "pick_health_C",
                    new ModelMetadata("pick_health_C", animation: false)
                },
                {
                    "pick_invis",
                    new ModelMetadata("pick_invis", animation: false)
                },
                {
                    "pick_wpn_all",
                    new ModelMetadata("pick_wpn_all", animation: false)
                },
                {
                    "pick_wpn_electro",
                    new ModelMetadata("pick_wpn_electro", animation: false)
                },
                {
                    "pick_wpn_ghostbuster",
                    new ModelMetadata("pick_wpn_ghostbuster", animation: false)
                },
                {
                    "pick_wpn_gorea",
                    new ModelMetadata("pick_wpn_gorea", animation: false)
                },
                {
                    "pick_wpn_jackhammer",
                    new ModelMetadata("pick_wpn_jackhammer", animation: false)
                },
                {
                    "pick_wpn_missile",
                    new ModelMetadata("pick_wpn_missile", animation: false)
                },
                {
                    "pick_wpn_mortar",
                    new ModelMetadata("pick_wpn_mortar", animation: false)
                },
                {
                    "pick_wpn_shotgun",
                    new ModelMetadata("pick_wpn_shotgun", animation: false)
                },
                {
                    "pick_wpn_snipergun",
                    new ModelMetadata("pick_wpn_snipergun", animation: false)
                },
                {
                    "pillar",
                    new ModelMetadata("pillar", animation: false, collision: true)
                },
                {
                    "pistonmp7",
                    new ModelMetadata("pistonmp7", animation: false, collision: true)
                },
                {
                    "piston_gorealand",
                    new ModelMetadata("piston_gorealand", animation: false, collision: true)
                },
                {
                    "PlantCarnivarous_Branched",
                    new ModelMetadata("PlantCarnivarous_Branched")
                },
                {
                    "PlantCarnivarous_PodLeaves",
                    new ModelMetadata("PlantCarnivarous_PodLeaves")
                },
                {
                    "PlantCarnivarous_Pod",
                    new ModelMetadata("PlantCarnivarous_Pod")
                },
                {
                    "PlantCarnivarous_Vine",
                    new ModelMetadata("PlantCarnivarous_Vine")
                },
                {
                    "platform",
                    new ModelMetadata("platform", animation: false, collision: true)
                },
                {
                    "Platform_Unit4_C1",
                    new ModelMetadata("Platform_Unit4_C1", animation: false, collision: true)
                },
                {
                    "PowerBomb",
                    new ModelMetadata("PowerBomb")
                },
                {
                    "Psychobit_Dead",
                    new ModelMetadata("Psychobit_Dead", animation: false)
                },
                {
                    "PsychoBit",
                    new ModelMetadata("PsychoBit",
                        recolors: new List<string>()
                        {
                            "img_00",
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "quads",
                    new ModelMetadata("quads", animation: false)
                },
                {
                    "Ruins_Console",
                    new ModelMetadata("Ruins_Console",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Monitor",
                    new ModelMetadata("Ruins_Monitor",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Power",
                    new ModelMetadata("Ruins_Power",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Scanner",
                    new ModelMetadata("Ruins_Scanner",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Switch",
                    new ModelMetadata("Ruins_Switch",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "SamusShip",
                    new ModelMetadata("SamusShip", collision: true)
                },
                {
                    "Samus_lod0",
                    new ModelMetadata("Samus_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_team01",
                            "pal_team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Samus",
                        useLightSources: true)
                },
                {
                    "Samus_lod1",
                    new ModelMetadata("Samus_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_team01",
                            "pal_team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Samus\Samus_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "SamusAlt_lod0",
                    new ModelMetadata("SamusAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_team01",
                            "pal_team02"
                        },
                        texture: true,
                        archive: "Samus",
                        recolorName: "Samus",
                        useLightSources: true)
                },
                {
                    "SamusGun",
                    new ModelMetadata("SamusGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localSamus",
                        useLightSources: true)
                },
                {
                    "samus_ice",
                    new ModelMetadata("samus_ice",
                        modelPath: @"_archives\common\samus_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        }, useLightSources: true)
                },
                {
                    "SecretSwitch",
                    new ModelMetadata("SecretSwitch",
                        modelPath: @"models\SecretSwitch_Model.bin",
                        animationPath: @"models\SecretSwitch_Anim.bin",
                        collisionPath: @"models\SecretSwitch_Collision.bin",
                        new List<RecolorMetadata>()
                        {
                            // default and pal_06 are identical
                            new RecolorMetadata("default",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_Model.bin"),
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_01_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_02_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_03_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_04_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_05_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_06_Model.bin",
                                separatePaletteHeader: true)
                        })
                },
                {
                    "shriekbat",
                    new ModelMetadata("shriekbat")
                },
                {
                    "slots",
                    new ModelMetadata("slots", addToAnim: "_Idle", archive: "frontend2d")
                },
                {
                    "smasher",
                    new ModelMetadata("smasher", animation: false, collision: true)
                },
                {
                    "sniperBeam",
                    new ModelMetadata("sniperBeam", archive: "common")
                },
                {
                    "SniperTarget",
                    new ModelMetadata("SniperTarget")
                },
                {
                    "SphinkTick_lod0",
                    new ModelMetadata("SphinkTick_lod0", remove: "_lod0")
                },
                {
                    "Spire_lod0",
                    new ModelMetadata("Spire_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Spire",
                        useLightSources: true)
                },
                {
                    "Spire_lod1",
                    new ModelMetadata("Spire_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Spire\Spire_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "SpireAlt_lod0",
                    new ModelMetadata("SpireAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Spire",
                        recolorName: "Spire",
                        useLightSources: true)
                },
                {
                    "SpireGun",
                    new ModelMetadata("SpireGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localSpire",
                        useLightSources: true)
                },
                {
                    "splashRing",
                    new ModelMetadata("splashRing")
                },
                {
                    "Switch",
                    new ModelMetadata("Switch",
                        animation: false,
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Sylux_lod0",
                    new ModelMetadata("Sylux_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Sylux",
                        useLightSources: true)
                },
                {
                    "Sylux_lod1",
                    new ModelMetadata("Sylux_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Sylux\Sylux_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "SyluxAlt_lod0",
                    new ModelMetadata("SyluxAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Sylux",
                        recolorName: "Sylux",
                        useLightSources: true)
                },
                {
                    "SyluxGun",
                    new ModelMetadata("SyluxGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localSylux",
                        useLightSources: true)
                },
                {
                    "SyluxShip",
                    new ModelMetadata("SyluxShip", collision: true)
                },
                {
                    "SyluxTurret",
                    new ModelMetadata("SyluxTurret")
                },
                {
                    "TearParticle",
                    new ModelMetadata("TearParticle", animation: false, texture: true)
                },
                {
                    "Teleporter",
                    new ModelMetadata("Teleporter",
                        modelPath: @"models\Teleporter_mdl_Model.bin",
                        animationPath: @"models\Teleporter_mdl_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_01_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_02_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_03_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_04_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_05_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_06_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_07_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_08_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_09",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_09_Model.bin",
                                separatePaletteHeader: true)
                        })
                },
                {
                    "TeleporterSmall",
                    new ModelMetadata("TeleporterSmall",
                        modelPath: @"models\TeleporterSmall_mdl_Model.bin",
                        animationPath: @"models\TeleporterSmall_mdl_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_01_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_02_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_03_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_04_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_05_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_06_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_07_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_08_Model.bin",
                                separatePaletteHeader: true),
                            new RecolorMetadata("pal_09",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_09_Model.bin",
                                separatePaletteHeader: true)
                        })
                },
                {
                    "TeleporterMP",
                    new ModelMetadata("TeleporterMP")
                },
                {
                    "Temroid_lod0",
                    new ModelMetadata("Temroid_lod0", remove: "_lod0", animation: false)
                },
                {
                    "ThinDoorLock",
                    new ModelMetadata("ThinDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "Trace_lod0",
                    new ModelMetadata("Trace_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Trace",
                        useLightSources: true)
                },
                {
                    "Trace_lod1",
                    new ModelMetadata("Trace_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Trace\Trace_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "TraceAlt_lod0",
                    new ModelMetadata("TraceAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Trace",
                        recolorName: "Trace",
                        useLightSources: true)
                },
                {
                    "TraceGun",
                    new ModelMetadata("TraceGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localTrace",
                        useLightSources: true)
                },
                {
                    "trail",
                    new ModelMetadata("trail", animation: false, archive: "common")
                },
                {
                    "unit1_land_plat1",
                    new ModelMetadata("unit1_land_plat1", animation: false, collision: true)
                },
                {
                    "unit1_land_plat2",
                    new ModelMetadata("unit1_land_plat2", animation: false, collision: true)
                },
                {
                    "unit1_land_plat3",
                    new ModelMetadata("unit1_land_plat3", animation: false, collision: true)
                },
                {
                    "unit1_land_plat4",
                    new ModelMetadata("unit1_land_plat4", animation: false, collision: true)
                },
                {
                    "unit1_land_plat5",
                    new ModelMetadata("unit1_land_plat5", animation: false, collision: true)
                },
                {
                    "unit1_mover1",
                    new ModelMetadata("unit1_mover1", collision: true)
                },
                {
                    "unit1_mover2",
                    new ModelMetadata("unit1_mover2", animation: false, collision: true)
                },
                {
                    "unit2_c1_mover",
                    new ModelMetadata("unit2_c1_mover", animation: false, collision: true)
                },
                {
                    "unit2_c4_plat",
                    new ModelMetadata("unit2_c4_plat", animation: false, collision: true)
                },
                {
                    "unit2_land_elev",
                    new ModelMetadata("unit2_land_elev", animation: false, collision: true)
                },
                {
                    "unit2_mover1",
                    new ModelMetadata("unit2_mover1", animation: false, collision: true)
                },
                {
                    "unit3_brain",
                    new ModelMetadata("unit3_brain", collision: true)
                },
                {
                    "unit3_jar",
                    new ModelMetadata("unit3_jar")
                },
                {
                    "unit3_jartop",
                    new ModelMetadata("unit3_jartop")
                },
                {
                    "unit3_mover1",
                    new ModelMetadata("unit3_mover1", animation: false, collision: true)
                },
                {
                    "unit3_mover2",
                    new ModelMetadata("unit3_mover2", collision: true)
                },
                {
                    "unit3_pipe1",
                    new ModelMetadata("unit3_pipe1", collision: true)
                },
                {
                    "unit3_pipe2",
                    new ModelMetadata("unit3_pipe2", collision: true)
                },
                {
                    "Unit3_platform1",
                    new ModelMetadata("Unit3_platform1", collision: true)
                },
                {
                    "unit3_platform",
                    new ModelMetadata("unit3_platform", animation: false, collision: true)
                },
                {
                    "unit3_platform2",
                    new ModelMetadata("unit3_platform2", animation: false, collision: true)
                },
                {
                    "unit4_mover1",
                    new ModelMetadata("unit4_mover1", collision: true)
                },
                {
                    "unit4_mover2",
                    new ModelMetadata("unit4_mover2", collision: true)
                },
                {
                    "unit4_mover3",
                    new ModelMetadata("unit4_mover3", animation: false, collision: true)
                },
                {
                    "unit4_mover4",
                    new ModelMetadata("unit4_mover4", animation: false, collision: true)
                },
                {
                    "unit4_platform1",
                    new ModelMetadata("unit4_platform1", animation: false, collision: true)
                },
                {
                    "unit4_tp1_artifact_wo",
                    new ModelMetadata("unit4_tp1_artifact_wo", animation: false, collision: true)
                },
                {
                    "unit4_tp2_artifact_wo",
                    new ModelMetadata("unit4_tp2_artifact_wo", animation: false, collision: true)
                },
                {
                    "WallSwitch",
                    new ModelMetadata("WallSwitch")
                },
                {
                    "warwasp_lod0",
                    new ModelMetadata("warwasp_lod0", remove: "_lod0")
                },
                {
                    "Weavel_lod0",
                    new ModelMetadata("Weavel_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Weavel",
                        useLightSources: true)
                },
                {
                    "Weavel_lod1",
                    new ModelMetadata("Weavel_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        animationPath: $@"_archives\Weavel\Weavel_Anim.bin",
                        texture: true,
                        useLightSources: true)
                },
                {
                    "WeavelAlt_lod0",
                    new ModelMetadata("WeavelAlt_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Weavel",
                        recolorName: "Weavel",
                        useLightSources: true)
                },
                {
                    "WeavelAlt_Turret_lod0",
                    new ModelMetadata("WeavelAlt_Turret_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "Weavel",
                        recolorName: "Weavel",
                        useLightSources: true)
                },
                {
                    "WeavelGun",
                    new ModelMetadata("WeavelGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        animation: true,
                        texture: true,
                        archive: "localWeavel",
                        useLightSources: true)
                },
                {
                    "zoomer",
                    new ModelMetadata("zoomer")
                },
                // 2D images only, no mesh/dlist, probably just swapped in for other textures on models
                {
                    "doubleDamage_img",
                    new ModelMetadata("doubleDamage_img", animation: false, archive: "common")
                },
                // todo?: seemingly 2D images only, no polygons render even though they have a mesh/dlist
                {
                    "arcWelder",
                    new ModelMetadata("arcWelder", animation: false, archive: "common")
                },
                {
                    "electroTrail",
                    new ModelMetadata("electroTrail", animation: false, archive: "common")
                }
            };

        public static readonly IReadOnlyDictionary<string, ModelMetadata> FirstHuntModels
            = new Dictionary<string, ModelMetadata>()
            {
                {
                    "ballDeath",
                    new ModelMetadata("ballDeath", firstHunt: true)
                },
                {
                    "balljump",
                    new ModelMetadata("balljump", animation: false, firstHunt: true)
                },
                {
                    "balljump_ray",
                    new ModelMetadata("balljump_ray", firstHunt: true)
                },
                {
                    "bomb",
                    new ModelMetadata("bomb", firstHunt: true)
                },
                {
                    "bombLite",
                    new ModelMetadata("bombLite", firstHunt: true)
                },
                {
                    "bombStart",
                    new ModelMetadata("bombStart", firstHunt: true)
                },
                {
                    "bombStartLite",
                    new ModelMetadata("bombStartLite", firstHunt: true)
                },
                {
                    "bombStartLiter",
                    new ModelMetadata("bombStartLiter", firstHunt: true)
                },
                {
                    "dashEffect",
                    new ModelMetadata("dashEffect", firstHunt: true)
                },
                {
                    "door",
                    new ModelMetadata("door", firstHunt: true)
                },
                {
                    "door2",
                    new ModelMetadata("door2", firstHunt: true)
                },
                {
                    "door2_holo",
                    new ModelMetadata("door2_holo", firstHunt: true)
                },
                {
                    "effWaspDeath",
                    new ModelMetadata("effWaspDeath", firstHunt: true)
                },
                {
                    "furlEffect",
                    new ModelMetadata("furlEffect", firstHunt: true)
                },
                {
                    "fuzzball",
                    new ModelMetadata("fuzzball", animation: false, firstHunt: true)
                },
                {
                    "genericmover",
                    new ModelMetadata("genericmover", collision: true, firstHunt: true)
                },
                {
                    "gun_idle",
                    new ModelMetadata("gun_idle", remove: "_idle", firstHunt: true)
                },
                {
                    "gunEffElectroCharge",
                    new ModelMetadata("gunEffElectroCharge", firstHunt: true)
                },
                {
                    "gunEffMissileCharge",
                    new ModelMetadata("gunEffMissileCharge", firstHunt: true)
                },
                {
                    "gunLobFlash",
                    new ModelMetadata("gunLobFlash", firstHunt: true)
                },
                {
                    "gunMuzzleFlash",
                    new ModelMetadata("gunMuzzleFlash", firstHunt: true)
                },
                {
                    "gunSmoke",
                    new ModelMetadata("gunSmoke", firstHunt: true)
                },
                {
                    // unused
                    "jumpad_ray",
                    new ModelMetadata("jumpad_ray", animation: false, firstHunt: true)
                },
                {
                    "jumppad_base",
                    new ModelMetadata("jumppad_base", animation: false, firstHunt: true)
                },
                {
                    // this has an animation file (unlike jumpad_ray), but it is not used
                    "jumppad_ray",
                    new ModelMetadata("jumppad_ray", animation: false, firstHunt: true)
                },
                {
                    "lightningCol",
                    new ModelMetadata("lightningCol", firstHunt: true)
                },
                {
                    "lightningColLite",
                    new ModelMetadata("lightningColLite", firstHunt: true)
                },
                {
                    "lightningColLiter",
                    new ModelMetadata("lightningColLiter", firstHunt: true)
                },
                {
                    "lightningColLiterER",
                    new ModelMetadata("lightningColLiterER", firstHunt: true)
                },
                {
                    "lightningLob",
                    new ModelMetadata("lightningLob", firstHunt: true)
                },
                {
                    "metroid",
                    new ModelMetadata("metroid", firstHunt: true)
                },
                {
                    "Metroid_Lo",
                    new ModelMetadata("Metroid_Lo", remove: "_Lo", firstHunt: true)
                },
                {
                    "missileCollide",
                    new ModelMetadata("missileCollide", firstHunt: true)
                },
                {
                    "missileColLite",
                    new ModelMetadata("missileColLite", firstHunt: true)
                },
                {
                    "missileColLiter",
                    new ModelMetadata("missileColLiter", firstHunt: true)
                },
                {
                    "missileColLiterER",
                    new ModelMetadata("missileColLiterER", firstHunt: true)
                },
                {
                    "Mochtroid",
                    new ModelMetadata("Mochtroid", firstHunt: true)
                },
                {
                    "Mochtroid_Lo",
                    new ModelMetadata("Mochtroid_Lo", remove: "_Lo", firstHunt: true)
                },
                {
                    "morphBall",
                    new ModelMetadata("morphBall", animation: false, firstHunt: true)
                },
                {
                    "morphBall_Blue",
                    new ModelMetadata("morphBall_Blue", animation: false, firstHunt: true)
                },
                {
                    "morphBall_Green",
                    new ModelMetadata("morphBall_Green", animation: false, firstHunt: true)
                },
                {
                    "morphBall_White",
                    new ModelMetadata("morphBall_White", animation: false, firstHunt: true)
                },
                {
                    "pb_charged",
                    new ModelMetadata("pb_charged", firstHunt: true)
                },
                {
                    "pb_normal",
                    new ModelMetadata("pb_normal", firstHunt: true)
                },
                {
                    "pick_ammo_A",
                    new ModelMetadata("pick_ammo_A", animation: false, firstHunt: true)
                },
                {
                    "pick_ammo_B",
                    new ModelMetadata("pick_ammo_B", animation: false, firstHunt: true)
                },
                {
                    "pick_dblDamage",
                    new ModelMetadata("pick_dblDamage", animation: false, firstHunt: true)
                },
                {
                    "pick_health_A",
                    new ModelMetadata("pick_health_A", animation: false, firstHunt: true)
                },
                {
                    "pick_health_B",
                    new ModelMetadata("pick_health_B", animation: false, firstHunt: true)
                },
                {
                    "pick_morphball",
                    new ModelMetadata("pick_morphball", animation: false, firstHunt: true)
                },
                {
                    "pick_wpn_electro",
                    new ModelMetadata("pick_wpn_electro", animation: false, firstHunt: true)
                },
                {
                    "pick_wpn_missile",
                    new ModelMetadata("pick_wpn_missile", animation: false, firstHunt: true)
                },
                {
                    "platform",
                    new ModelMetadata("platform", animation: false, collision: true, firstHunt: true)
                },
                {
                    "samus_hi_blue",
                    new ModelMetadata("samus_hi_blue", remove: "_hi_blue", firstHunt: true)
                },
                {
                    "samus_hi_green",
                    new ModelMetadata("samus_hi_green", remove: "_hi_green", firstHunt: true)
                },
                {
                    "samus_hi_white",
                    new ModelMetadata("samus_hi_white", remove: "_hi_white", firstHunt: true)
                },
                {
                    "samus_hi_yellow",
                    new ModelMetadata("samus_hi_yellow", remove: "_hi_yellow", firstHunt: true)
                },
                {
                    "samus_low_yellow",
                    new ModelMetadata("samus_low_yellow", remove: "_low_yellow", firstHunt: true)
                },
                {
                    "spawnEffect",
                    new ModelMetadata("spawnEffect", firstHunt: true)
                },
                {
                    "trail",
                    new ModelMetadata("trail", animation: false, firstHunt: true)
                },
                {
                    "warWasp",
                    new ModelMetadata("warWasp", firstHunt: true)
                },
                {
                    "zoomer",
                    new ModelMetadata("zoomer", firstHunt: true)
                }
            };
    }
}
