using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead
{
    // size: 112
    public class RoomDescription
    {
        public readonly string Name;
        public readonly string ModelPath;
        public readonly string AnimationPath;
        public readonly string? TexturePath;
        public readonly string CollisionPath;
        public readonly string? EntityPath;
        public readonly string? NodeDataPath;
        public readonly string? RoomNodeName;
        public readonly uint BattleTimeLimit;
        public readonly uint TimeLimit;
        public readonly ushort PointLimit;
        public readonly ushort LayerId;
        public readonly uint FarClipDistance;
        public readonly ushort FogEnabled;
        public readonly ushort ClearFog;
        public readonly ushort FogColor;
        public readonly ushort Padding36;
        public readonly uint FogSlope;
        public readonly uint FogOffset;
        public readonly ColorRgb Light1Color;
        public readonly byte Padding43;
        public readonly Vector3Fx Light1Vector;
        public readonly ColorRgb Light2Color;
        public readonly byte Padding53;
        public readonly Vector3Fx Light2Vector;
        public readonly string InternalName;
        public readonly string ArchivePath;
        public readonly uint Field68;
        public readonly uint Field6C;

        public RoomDescription(string name, string modelPath, string animationPath, string? texturePath, string collisionPath,
            string? entityPath, string? nodeDataPath, string? roomNodeName, uint battleTimeLimit, uint timeLimit, ushort pointLimit,
            ushort layerId, uint farClipDistance, ushort fogEnabled, ushort clearFog, ushort fogColor, ushort padding36, uint fogSlope,
            uint fogOffset, ColorRgb light1Color, byte padding43, Vector3Fx light1Vector, ColorRgb light2Color, byte padding53,
            Vector3Fx light2Vector, string internalName, string archivePath, uint field68, uint field6C)
        {
            Name = name;
            ModelPath = modelPath;
            AnimationPath = animationPath;
            TexturePath = texturePath;
            CollisionPath = collisionPath;
            EntityPath = entityPath;
            NodeDataPath = nodeDataPath;
            RoomNodeName = roomNodeName;
            BattleTimeLimit = battleTimeLimit;
            TimeLimit = timeLimit;
            PointLimit = pointLimit;
            LayerId = layerId;
            FarClipDistance = farClipDistance;
            FogEnabled = fogEnabled;
            ClearFog = clearFog;
            FogColor = fogColor;
            Padding36 = padding36;
            FogSlope = fogSlope;
            FogOffset = fogOffset;
            Light1Color = light1Color;
            Padding43 = padding43;
            Light1Vector = light1Vector;
            Light2Color = light2Color;
            Padding53 = padding53;
            Light2Vector = light2Vector;
            InternalName = internalName;
            ArchivePath = archivePath;
            Field68 = field68;
            Field6C = field6C;
        }
    }

    public class RoomMetadata
    {
        public string Name { get; }
        public string? InGameName { get; }
        public string Archive { get; }
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
        public bool FogEnabled { get; }
        // future?: currently only used to set the clear color for FH rooms
        public bool ClearFog { get; }
        public ColorRgb FogColor { get; }
        public int FogSlope { get; }
        public ushort FogOffset { get; }
        public float FarClip { get; }
        public ColorRgb Light1Color { get; }
        public Vector3 Light1Vector { get; }
        public ColorRgb Light2Color { get; }
        public Vector3 Light2Vector { get; }
        public uint Field68 { get; }
        public uint Field6C { get; }
        public bool Multiplayer { get; }
        public bool FirstHunt { get; }

        public RoomMetadata(string name, string? inGameName, string archive, string modelPath, string animationPath, string collisionPath,
            string? texturePath, string? entityPath, string? nodePath, string? roomNodeName, uint battleTimeLimit, uint timeLimit,
            short pointLimit, short nodeLayer, bool fogEnabled, bool clearFog, ColorRgb fogColor, int fogSlope, ushort fogOffset,
            ColorRgb light1Color, Vector3 light1Vector, ColorRgb light2Color, Vector3 light2Vector, int farClip, uint field68,
            uint field6C, bool multiplayer = false, bool firstHunt = false)
        {
            Name = name;
            InGameName = inGameName;
            Archive = archive;
            ModelPath = $@"_archives\{archive}\{modelPath}";
            AnimationPath = $@"_archives\{archive}\{animationPath}";
            CollisionPath = $@"_archives\{archive}\{collisionPath}";
            TexturePath = texturePath == null ? null : $@"levels\textures\{texturePath}";
            EntityPath = entityPath == null ? null : $@"levels\entities\{entityPath}";
            NodePath = nodePath == null ? null : $@"levels\nodeData\{nodePath}";
            RoomNodeName = roomNodeName;
            BattleTimeLimit = battleTimeLimit;
            TimeLimit = timeLimit;
            PointLimit = pointLimit;
            NodeLayer = nodeLayer;
            FogEnabled = fogEnabled;
            ClearFog = clearFog;
            FogColor = fogColor;
            FogSlope = fogSlope;
            FogOffset = fogOffset;
            Light1Color = light1Color;
            Light1Vector = light1Vector.Normalized();
            Light2Color = light2Color;
            Light2Vector = light2Vector.Normalized();
            Multiplayer = multiplayer;
            FarClip = Fixed.ToFloat(farClip);
            Field68 = field68;
            Field6C = field6C;
            FirstHunt = firstHunt;
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
            if (animationPath != null)
            {
                AnimationPath = animationPath;
            }
            else if (animation)
            {
                if (archive != null)
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

        public ModelMetadata(string name, bool animation = true, bool collision = false, bool texture = false,
            string? share = null, MdlSuffix mdlSuffix = MdlSuffix.None, string? archive = null,
            string? addToAnim = null, bool firstHunt = false, string? animationPath = null)
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
                AnimationPath = animationPath ?? $@"{path}\{name}{addToAnim}{suffix}_Anim.bin";
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
        public string? ReplacePath { get; }
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
            bool separatePaletteHeader = false, Dictionary<int, IEnumerable<int>>? replaceIds = null, bool separateReplace = false)
        {
            Name = name;
            ModelPath = modelPath;
            TexturePath = texturePath;
            if (separateReplace)
            {
                PalettePath = texturePath;
                ReplacePath = palettePath;
            }
            else
            {
                PalettePath = palettePath;
            }
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

        public static readonly Vector3 OctolithLight1Vector = new Vector3(0, 0.3005371f, -0.5f);
        public static readonly Vector3 OctolithLight2Vector = new Vector3(0, 0, -0.5f);
        public static readonly Vector3 OctolithLightColor = new Vector3(1, 1, 1);

        // this is only set/used by Octolith
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

        public static readonly IReadOnlyList<int> AdpcmTable = new List<int>()
        {
            7, 8, 9, 10, 11, 12, 13, 14,
            16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66,
            73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658,
            724, 796, 876, 963, 1060, 1166, 1282, 1411,
            1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
            3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
            7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
            32767
        };

        public static readonly IReadOnlyList<int> ImaIndexTable = new List<int>()
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8
        };

        public static readonly IReadOnlyList<string> MusicSeqs = new List<string>()
        {
            "SEQ_BRINSTAR",
            "SEQ_MP1",
            "SEQ_MP2",
            "SEQ_PARASITE",
            "SEQ_SHIP",
            "SEQ_YELLOW",
            "SEQ_RESULTS",
            "SEQ_TIMEOUT",
            "SEQ_WIN",
            "SEQ_GARLIC",
            "SEQ_MP2_X",
            "SEQ_PARASITE_X",
            "SEQ_RED",
            "SEQ_BLUE",
            "SEQ_AMBIENT_1",
            "SEQ_TELEPORT",
            "SEQ_DRONE",
            "SEQ_MENU1",
            "SEQ_GREY",
            "SEQ_SAFFRON",
            "SEQ_GUMBO",
            "SEQ_INTRO_SYLUX",
            "SEQ_INTRO_TRACE",
            "SEQ_INTRO_NOXUS",
            "SEQ_INTRO_WEAVEL",
            "SEQ_INTRO_KANDEN",
            "SEQ_INTRO_SPIRE",
            "SEQ_FLY_IN_2",
            "SEQ_FLY_IN_1",
            "SEQ_FLY_IN_3",
            "SEQ_FLY_IN_4",
            "SEQ_SHIP_LAND1",
            "SEQ_SHIP_LAND2",
            "SEQ_SHIP_LAND3",
            "SEQ_SHIP_LAND4",
            "SEQ_GET_WEAPON",
            "SEQ_GET_OCTOLITH",
            "SEQ_NEW_GAME",
            "SEQ_BEAT_HUNTER1",
            "SEQ_INTRO_GUARDIAN",
            "SEQ_GUARDIAN",
            "SEQ_BEAT_CYLBOSS1",
            "SEQ_GREEN",
            "SEQ_CHUTNEY",
            "SEQ_DILL",
            "SEQ_GOREA_1",
            "SEQ_ENEMY_1",
            "SEQ_GOREA_2",
            "SEQ_PEPPER",
            "SEQ_SINGLE_CART_MENU",
            "SEQ_SINGLE_CART_INGAME",
            "SEQ_SINGLE_CART_TIMEOUT",
            "SEQ_OREGANO",
            "SEQ_ENEMY_2",
            "SEQ_WHITE",
            "SEQ_ENERGY_TIMER",
            "SEQ_BLACK",
            "SEQ_INDIGO",
            "SEQ_CREDITS",
            "SEQ_FLY_IN_GOREA"
        };

        public static ModelMetadata? GetModelByName(string name)
        {
            if (ModelMetadata.TryGetValue(name, out ModelMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        public static ModelMetadata? GetFirstHuntModelByName(string name)
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

        public static readonly IReadOnlyList<DoorMetadata> Doors = new List<DoorMetadata>()
        {
            /* 0 */ new DoorMetadata("AlimbicDoor", "AlimbicDoorLock", 1.39990234f),
            /* 1 */ new DoorMetadata("AlimbicMorphBallDoor", "AlimbicMorphBallDoorLock", 0.6999512f),
            /* 2 */ new DoorMetadata("AlimbicBossDoor", "AlimbicBossDoorLock", 3.5f),
            /* 3 */ new DoorMetadata("AlimbicThinDoor", "ThinDoorLock", 1.39990234f)
        };

        public static readonly IReadOnlyList<string> FhDoors = new List<string>()
        {
            /* 0 */ "door",
            /* 1 */ "door2",
            /* 2 */ "door2_holo"
        };

        // 0-7 are for beam doors, 8 is unused?, 9 is for regular doors
        // (only a few rooms use index 0, since the usual thing is to use index 9)
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
            /* 13 */ "pick_ammo_green", // small
            /* 14 */ "pick_ammo_green", // big
            /* 15 */ "pick_ammo_orange", // small
            /* 16 */ "pick_ammo_orange", // big
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
            // todo: what's the difference between this and 23?
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

        // todo: organize/enum
        public static Vector3 GetEventColor(Message eventId)
        {
            if (eventId == Message.None) // black
            {
                return new Vector3(0, 0, 0);
            }
            if (eventId == Message.SetActive) // purple
            {
                return new Vector3(0.615f, 0, 0.909f);
            }
            if (eventId == Message.Damage) // red
            {
                return new Vector3(1, 0, 0);
            }
            if (eventId == Message.Gravity) // light blue
            {
                return new Vector3(0.141f, 1f, 1f);
            }
            if (eventId == Message.Activate) // green
            {
                return new Vector3(0, 1, 0);
            }
            if (eventId == Message.Death) // dark blue
            {
                return new Vector3(0f, 0f, 0.858f);
            }
            if (eventId == Message.SavePoint) // light yellow
            {
                return new Vector3(1f, 1f, 0.6f);
            }
            if (eventId == Message.Unknown25) // pale orange
            {
                return new Vector3(1f, 0.792f, 0.6f);
            }
            if (eventId == Message.Unknown35) // yellow
            {
                return new Vector3(0.964f, 1f, 0.058f);
            }
            if (eventId == Message.Unknown44) // gray
            {
                return new Vector3(0.5f, 0.5f, 0.5f);
            }
            if (eventId == Message.Unknown46) // periwinkle
            {
                return new Vector3(0.596f, 0.658f, 0.964f);
            }
            if (eventId == Message.Unknown56) // salmon
            {
                return new Vector3(0.964f, 0.596f, 0.596f);
            }
            if (eventId == Message.Checkpoint) // magenta
            {
                return new Vector3(0.972f, 0.086f, 0.831f);
            }
            if (eventId == Message.EscapeStart) // mint
            {
                return new Vector3(0.619f, 0.980f, 0.678f);
            }
            if (eventId == Message.Unknown9) // dark red
            {
                return new Vector3(0.549f, 0.18f, 0.18f);
            }
            if (eventId == Message.Unknown12) // dark teal
            {
                return new Vector3(0.094f, 0.506f, 0.51f);
            }
            if (eventId == Message.Unknown16) // navy blue
            {
                return new Vector3(0.094f, 0.094f, 0.557f);
            }
            if (eventId == Message.Unknown17) // olive
            {
                return new Vector3(0.647f, 0.663f, 0.169f);
            }
            if (eventId == Message.Unknown26) // dark green
            {
                return new Vector3(0.118f, 0.588f, 0.118f);
            }
            if (eventId == Message.Unknown27) // light purple
            {
                return new Vector3(0.784f, 0.325f, 1f);
            }
            if (eventId == Message.Unknown28) // orange
            {
                return new Vector3(1f, 0.612f, 0.153f);
            }
            if (eventId == Message.Unknown33) // lavender
            {
                return new Vector3(0.906f, 0.702f, 1f);
            }
            if (eventId == Message.Unknown34) // pale blue
            {
                return new Vector3(0.784f, 0.984f, 0.988f);
            }
            if (eventId == Message.Unknown36) // light red
            {
                return new Vector3(1f, 0.325f, 0.294f);
            }
            if (eventId == Message.Unknown42) // pink
            {
                return new Vector3(0.988f, 0.463f, 0.824f);
            }
            if (eventId == Message.Unknown45) // sea green
            {
                return new Vector3(0.165f, 0.894f, 0.678f);
            }
            if (eventId == Message.Unknown53) // brown
            {
                return new Vector3(0.549f, 0.345f, 0.102f);
            }
            if (eventId == Message.Unknown54) // pale green
            {
                return new Vector3(0.471f, 0.769f, 0.525f);
            }
            if (eventId == Message.Unknown60) // light orange
            {
                return new Vector3(1f, 0.765f, 0.49f);
            }
            if (eventId == Message.Unknown61) // sky blue
            {
                return new Vector3(0.165f, 0.816f, 0.894f);
            }
            // unknown - white (no other IDs are used by trigger/area volumes)
            return new Vector3(1, 1, 1);
        }

        public static Vector3 GetEventColor(FhMessage eventId)
        {
            if (eventId == FhMessage.None) // black
            {
                return new Vector3(0, 0, 0);
            }
            if (eventId == FhMessage.Unknown5) // green
            {
                return new Vector3(0, 1, 0);
            }
            if (eventId == FhMessage.Unlock) // navy blue
            {
                return new Vector3(0.094f, 0.094f, 0.557f);
            }
            if (eventId == FhMessage.SetActive) // purple
            {
                return new Vector3(0.615f, 0, 0.909f);
            }
            if (eventId == FhMessage.Death) // dark blue
            {
                return new Vector3(0f, 0f, 0.858f);
            }
            // unknown - white (no other IDs are used by trigger/area volumes)
            return new Vector3(1, 1, 1);
        }

        // todo: files not referenced by this list: powerBeamNoSplatMP_PS.bin, sparksDown_PS.bin
        public static readonly IReadOnlyList<string> Effects = new List<string>()
        {
            /*   0 */ "", // unused?
            /*   1 */ "_archives/effects/powerBeam_PS.bin",
            /*   2 */ "_archives/effects/powerBeamNoSplat_PS.bin",
            /*   3 */ "effects/blastCapHit_PS.bin",
            /*   4 */ "effects/blastCapBlow_PS.bin",
            /*   5 */ "_archives/effects/missile1_PS.bin",
            /*   6 */ "_archives/effects/mortar1_PS.bin",
            /*   7 */ "_archives/effects/shotGunCol_PS.bin",
            /*   8 */ "_archives/effects/shotGunShrapnel_PS.bin",
            /*   9 */ "effects/bombStart_PS.bin",
            /*  10 */ "effects/ballDeath_PS.bin",
            /*  11 */ "_archives/effects/jackHammerCol_PS.bin",
            /*  12 */ "effects/effectiveHitPB_PS.bin",
            /*  13 */ "effects/effectiveHitElectric_PS.bin",
            /*  14 */ "effects/effectiveHitMsl_PS.bin",
            /*  15 */ "effects/effectiveHitJack_PS.bin",
            /*  16 */ "effects/effectiveHitSniper_PS.bin",
            /*  17 */ "effects/effectiveHitIce_PS.bin",
            /*  18 */ "effects/effectiveHitMortar_PS.bin",
            /*  19 */ "effects/effectiveHitGhost_PS.bin",
            /*  20 */ "effects/sprEffectivePB_PS.bin",
            /*  21 */ "effects/sprEffectiveElectric_PS.bin",
            /*  22 */ "effects/sprEffectiveMsl_PS.bin",
            /*  23 */ "effects/sprEffectiveJack_PS.bin",
            /*  24 */ "effects/sprEffectiveSniper_PS.bin",
            /*  25 */ "effects/sprEffectiveIce_PS.bin",
            /*  26 */ "effects/sprEffectiveMortar_PS.bin",
            /*  27 */ "effects/sprEffectiveGhost_PS.bin",
            /*  28 */ "_archives/effects/sniperCol_PS.bin",
            /*  29 */ "effects/shriekBatTrail_PS.bin",
            /*  30 */ "effects/samusFurl_PS.bin",
            /*  31 */ "effects/spawnEffect_PS.bin",
            /*  32 */ "effects/test_PS.bin",
            /*  33 */ "effects/spawnEffectMP_PS.bin",
            /*  34 */ "effects/burstFlame_PS.bin",
            /*  35 */ "effects/gunSmoke_PS.bin",
            /*  36 */ "effects/jetFlame_PS.bin",
            /*  37 */ "effects/spireAltSlam_PS.bin",
            /*  38 */ "effects/steamBurst_PS.bin",
            /*  39 */ "effects/steamSamusShip_PS.bin",
            /*  40 */ "effects/steamDoorway_PS.bin",
            /*  41 */ "effects/goreaArmChargeUp_PS.bin",
            /*  42 */ "effects/goreaBallExplode_PS.bin",
            /*  43 */ "effects/goreaShoulderDamageLoop_PS.bin",
            /*  44 */ "effects/goreaShoulderHits_PS.bin",
            /*  45 */ "effects/goreaShoulderKill_PS.bin",
            /*  46 */ "effects/goreaChargeElc_PS.bin",
            /*  47 */ "effects/goreaChargeIce_PS.bin",
            /*  48 */ "effects/goreaChargeJak_PS.bin",
            /*  49 */ "effects/goreaChargeMrt_PS.bin",
            /*  50 */ "effects/goreaChargeSnp_PS.bin",
            /*  51 */ "effects/goreaFireElc_PS.bin",
            /*  52 */ "effects/goreaFireGst_PS.bin",
            /*  53 */ "effects/goreaFireIce_PS.bin",
            /*  54 */ "effects/goreaFireJak_PS.bin",
            /*  55 */ "effects/goreaFireMrt_PS.bin",
            /*  56 */ "effects/goreaFireSnp_PS.bin",
            /*  57 */ "effects/muzzleElc_PS.bin",
            /*  58 */ "effects/muzzleGst_PS.bin",
            /*  59 */ "effects/muzzleIce_PS.bin",
            /*  60 */ "effects/muzzleJak_PS.bin",
            /*  61 */ "effects/muzzleMrt_PS.bin",
            /*  62 */ "effects/muzzlePB_PS.bin",
            /*  63 */ "effects/muzzleSnp_PS.bin",
            /*  64 */ "effects/tear_PS.bin",
            /*  65 */ "effects/cylCrystalCharge_PS.bin",
            /*  66 */ "effects/cylCrystalKill_PS.bin",
            /*  67 */ "effects/cylCrystalShot_PS.bin",
            /*  68 */ "effects/tearSplat_PS.bin",
            /*  69 */ "effects/eyeShieldCharge_PS.bin",
            /*  70 */ "effects/eyeShieldHit_PS.bin",
            /*  71 */ "effects/goreaSlam_PS.bin",
            /*  72 */ "effects/goreaBallExplode2_PS.bin",
            /*  73 */ "effects/cylCrystalKill2_PS.bin",
            /*  74 */ "effects/cylCrystalKill3_PS.bin",
            /*  75 */ "effects/goreaCrystalExplode_PS.bin",
            /*  76 */ "effects/deathBio1_PS.bin",
            /*  77 */ "effects/deathMech1_PS.bin",
            /*  78 */ "effects/iceWave_PS.bin",
            /*  79 */ "effects/goreaMeteor_PS.bin",
            /*  80 */ "effects/goreaTeleport_PS.bin",
            /*  81 */ "effects/tearChargeUp_PS.bin",
            /*  82 */ "effects/eyeShield_PS.bin",
            /*  83 */ "effects/eyeShieldDefeat_PS.bin",
            /*  84 */ "effects/grateSparks_PS.bin",
            /*  85 */ "effects/electroCharge_PS.bin",
            /*  86 */ "effects/electroHit_PS.bin",
            /*  87 */ "effects/torch_PS.bin",
            /*  88 */ "effects/jetFlameBlue_PS.bin",
            /*  89 */ "effects/lavaBurstLarge_PS.bin",
            /*  90 */ "effects/lavaBurstSmall_PS.bin",
            /*  91 */ "effects/ember_PS.bin",
            /*  92 */ "effects/powerBeamCharge_PS.bin",
            /*  93 */ "effects/lavaDemonDive_PS.bin",
            /*  94 */ "effects/lavaDemonHurl_PS.bin",
            /*  95 */ "effects/lavaDemonRise_PS.bin",
            /*  96 */ "effects/iceDemonHurl_PS.bin",
            /*  97 */ "effects/lavaBurstExtraLarge_PS.bin",
            /*  98 */ "effects/powerBeamChargeNoSplat_PS.bin",
            /*  99 */ "effects/powerBeamHolo_PS.bin",
            /* 100 */ "effects/powerBeamLava_PS.bin",
            /* 101 */ "effects/hangingDrip_PS.bin",
            /* 102 */ "effects/hangingSpit_PS.bin",
            /* 103 */ "effects/hangingSplash_PS.bin",
            /* 104 */ "effects/goreaEyeFlash_PS.bin",
            /* 105 */ "effects/smokeBurst_PS.bin",
            /* 106 */ "effects/sparks_PS.bin",
            /* 107 */ "effects/sparksFall_PS.bin", // no such file?
            /* 108 */ "effects/shriekBatCol_PS.bin",
            /* 109 */ "effects/eyeTurretCharge_PS.bin",
            /* 110 */ "effects/lavaDemonSplat_PS.bin",
            /* 111 */ "effects/tearDrips_PS.bin",
            /* 112 */ "effects/syluxShipExhaust_PS.bin",
            /* 113 */ "effects/bombStartSylux_PS.bin",
            /* 114 */ "effects/lockDefeat_PS.bin",
            /* 115 */ "effects/ineffectivePsycho_PS.bin",
            /* 116 */ "effects/cylCrystalProjectile_PS.bin",
            /* 117 */ "effects/cylWeakSpotShot_PS.bin",
            /* 118 */ "effects/eyeLaser_PS.bin",
            /* 119 */ "effects/bombStartMP_PS.bin",
            /* 120 */ "effects/enemyMslCol_PS.bin",
            /* 121 */ "effects/powerBeamHoloBG_PS.bin",
            /* 122 */ "effects/powerBeamHoloB_PS.bin",
            /* 123 */ "effects/powerBeamIce_PS.bin",
            /* 124 */ "effects/powerBeamRock_PS.bin",
            /* 125 */ "effects/powerBeamSand_PS.bin",
            /* 126 */ "effects/powerBeamSnow_PS.bin",
            /* 127 */ "effects/bubblesRising_PS.bin",
            /* 128 */ "effects/bombKanden_PS.bin",
            /* 129 */ "effects/collapsingStreaks_PS.bin",
            /* 130 */ "effects/fireProjectile_PS.bin",
            /* 131 */ "effects/iceDemonSplat_PS.bin",
            /* 132 */ "effects/iceDemonRise_PS.bin",
            /* 133 */ "effects/iceDemonDive_PS.bin",
            /* 134 */ "effects/hammerProjectile_PS.bin",
            /* 135 */ "effects/synapseKill_PS.bin",
            /* 136 */ "effects/samusDash_PS.bin",
            /* 137 */ "effects/electroProjectile_PS.bin",
            /* 138 */ "effects/cylHomingProjectile_PS.bin",
            /* 139 */ "effects/cylHomingKill_PS.bin",
            /* 140 */ "effects/energyRippleB_PS.bin",
            /* 141 */ "effects/energyRippleBG_PS.bin",
            /* 142 */ "effects/energyRippleO_PS.bin",
            /* 143 */ "effects/columnCrash_PS.bin",
            /* 144 */ "effects/artifactKeyEffect_PS.bin",
            /* 145 */ "effects/bombBlue_PS.bin",
            /* 146 */ "effects/bombSylux_PS.bin",
            /* 147 */ "effects/columnBreak_PS.bin",
            /* 148 */ "effects/grappleEnd_PS.bin",
            /* 149 */ "effects/bombStartSyluxG_PS.bin",
            /* 150 */ "effects/bombStartSyluxO_PS.bin",
            /* 151 */ "effects/bombStartSyluxP_PS.bin",
            /* 152 */ "effects/bombStartSyluxR_PS.bin",
            /* 153 */ "effects/bombStartSyluxW_PS.bin",
            /* 154 */ "effects/mpEffectivePB_PS.bin",
            /* 155 */ "effects/mpEffectiveElectric_PS.bin",
            /* 156 */ "effects/mpEffectiveMsl_PS.bin",
            /* 157 */ "effects/mpEffectiveJack_PS.bin",
            /* 158 */ "effects/mpEffectiveSniper_PS.bin",
            /* 159 */ "effects/mpEffectiveIce_PS.bin",
            /* 160 */ "effects/mpEffectiveMortar_PS.bin",
            /* 161 */ "effects/mpEffectiveGhost_PS.bin",
            /* 162 */ "effects/pipeTricity_PS.bin",
            /* 163 */ "effects/breakableExplode_PS.bin",
            /* 164 */ "effects/goreaCrystalHit_PS.bin",
            /* 165 */ "effects/chargeElc_PS.bin",
            /* 166 */ "effects/chargeIce_PS.bin",
            /* 167 */ "effects/chargeJak_PS.bin",
            /* 168 */ "effects/chargeMrt_PS.bin",
            /* 169 */ "effects/chargePB_PS.bin",
            /* 170 */ "effects/chargeMsl_PS.bin",
            /* 171 */ "effects/electroChargeNA_PS.bin",
            /* 172 */ "effects/mortarSecondary_PS.bin", // no such file?
            /* 173 */ "_archives/effects/jackHammerColNA_PS.bin",
            /* 174 */ "effects/goreaMeteorLaunch_PS.bin",
            /* 175 */ "effects/goreaReveal_PS.bin",
            /* 176 */ "effects/goreaMeteorDamage_PS.bin",
            /* 177 */ "effects/goreaMeteorDestroy_PS.bin",
            /* 178 */ "effects/goreaMeteorHit_PS.bin",
            /* 179 */ "effects/goreaGrappleDamage_PS.bin",
            /* 180 */ "effects/goreaGrappleDie_PS.bin",
            /* 181 */ "effects/deathBall_PS.bin",
            /* 182 */ "effects/nozzleJet_PS.bin",
            /* 183 */ "effects/syluxMissile_PS.bin",
            /* 184 */ "effects/syluxMissileCol_PS.bin",
            /* 185 */ "effects/syluxMissileFlash_PS.bin",
            /* 186 */ "effects/sphereTricity_PS.bin",
            /* 187 */ "effects/flamingAltForm_PS.bin",
            /* 188 */ "effects/flamingGun_PS.bin",
            /* 189 */ "effects/flamingHunter_PS.bin",
            /* 190 */ "_archives/effects/missileCharged_PS.bin",
            /* 191 */ "_archives/effects/mortarCharged_PS.bin",
            /* 192 */ "_archives/effects/mortarChargedAffinity_PS.bin",
            /* 193 */ "effects/deathBio2_PS.bin",
            /* 194 */ "effects/chargeLoopElc_PS.bin",
            /* 195 */ "effects/chargeLoopIce_PS.bin",
            /* 196 */ "effects/chargeLoopMrt_PS.bin",
            /* 197 */ "effects/chargeLoopMsl_PS.bin",
            /* 198 */ "effects/chargeLoopPB_PS.bin",
            /* 199 */ "effects/sphereTricitySmall_PS.bin",
            /* 200 */ "effects/generatorExplosion_PS.bin",
            /* 201 */ "effects/eyeDamageLoop_PS.bin",
            /* 202 */ "effects/eyeHit_PS.bin",
            /* 203 */ "effects/eyelKill_PS.bin",
            /* 204 */ "effects/eyeKill2_PS.bin",
            /* 205 */ "effects/eyeKill3_PS.bin",
            /* 206 */ "effects/eyeFinalKill_PS.bin",
            /* 207 */ "effects/chargeTurret_PS.bin",
            /* 208 */ "effects/flashTurret_PS.bin",
            /* 209 */ "effects/ultimateProjectile_PS.bin",
            /* 210 */ "effects/goreaLaserCharge_PS.bin",
            /* 211 */ "effects/mortarProjectile_PS.bin",
            /* 212 */ "effects/fallingSnow_PS.bin",
            /* 213 */ "effects/fallingDust_PS.bin",
            /* 214 */ "effects/fallingRock_PS.bin",
            /* 215 */ "effects/deathMech2_PS.bin",
            /* 216 */ "effects/deathAlt_PS.bin",
            /* 217 */ "effects/iceDemonDeath_PS.bin",
            /* 218 */ "effects/lavaDemonDeath_PS.bin",
            /* 219 */ "effects/deathBio3_PS.bin",
            /* 220 */ "effects/deathBio4_PS.bin",
            /* 221 */ "effects/deathBio5_PS.bin",
            /* 222 */ "effects/deathStatue_PS.bin",
            /* 223 */ "effects/deathTick_PS.bin",
            /* 224 */ "effects/goreaLaserCol_PS.bin",
            /* 225 */ "effects/goreaHurt_PS.bin",
            /* 226 */ "effects/explosionAbove_PS.bin",
            /* 227 */ "effects/fireFlurry_PS.bin",
            /* 228 */ "effects/snowFlurry_PS.bin",
            /* 229 */ "effects/enemySpawn_PS.bin",
            /* 230 */ "effects/teleporter_PS.bin",
            /* 231 */ "_archives/effects/iceShatter_PS.bin",
            /* 232 */ "effects/sphereTricityDeath_PS.bin",
            /* 233 */ "effects/greenFlurry_PS.bin",
            /* 234 */ "effects/pmagAbsorb_PS.bin",
            /* 235 */ "effects/noxHit_PS.bin",
            /* 236 */ "effects/spireBurst_PS.bin",
            /* 237 */ "effects/electroProjectileUncharged_PS.bin",
            /* 238 */ "effects/enemyProjectile1_PS.bin",
            /* 239 */ "effects/enemyCol1_PS.bin",
            /* 240 */ "effects/psychoCharge_PS.bin",
            /* 241 */ "effects/hammerProjectileSml_PS.bin",
            /* 242 */ "effects/nozzleJetOff_PS.bin",
            /* 243 */ "effects/powerBeamChargeNoSplatMP_PS.bin", // no such file?
            /* 244 */ "effects/doubleDamageGun_PS.bin",
            /* 245 */ "effects/ultimateCol_PS.bin",
            /* 246 */ "effects/enemyMortarProjectile_PS.bin"
        };

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
            return minutes * 1800 + seconds * 30 + frames;
        }

        private static Vector3 VectorParse(string x, string y, string z)
        {
            return new Vector3(
                Int32.Parse(x.Replace("0x", ""), NumberStyles.HexNumber),
                Int32.Parse(y.Replace("0x", ""), NumberStyles.HexNumber),
                Int32.Parse(z.Replace("0x", ""), NumberStyles.HexNumber));
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
                /* 128, 0 */ "FH_MP1",
                /* 129, 1 */ "FH_MP2",
                /* 130, 2 */ "FH_MP3",
                /* 131, 3 */ "FH_MORPHBALL",
                /* 132, 4 */ "FH_REGULATOR",
                /* 133, 5 */ "FH_SURVIVOR",
                /* 134, 6 */ "FH_TEST",
                /* 135, 7 */ "FH_MP5",
                /* 136, 8 */ "FH_MP1", // todo
                /* 137, 9 */ "FH_E3"
            };

        // todo: unused?
        // unit3_rm5_Ent.bin
        // unit2_CX_Ent.bin, unit2_CZ_Ent.bin, unit3_CX_Ent.bin, unit3_CZ_Ent.bin
        // bigeyeroom_Ent.bin, cylinderroom_Ent.bin, Cylinder_C1_Ent.bin
        // FH leftovers: morphBall_Ent.bin, regulator_Ent.bin, survivor_Ent.bin
        public static readonly IReadOnlyDictionary<string, RoomMetadata> RoomMetadata
            = new Dictionary<string, RoomMetadata>()
            {
                {
                    "UNIT1_CX",
                    new RoomMetadata(
                        name: "UNIT1_CX",
                        inGameName: null,
                        archive: "unit1_CX",
                        modelPath: "unit1_cx_model.bin",
                        animationPath: "unit1_cx_anim.bin",
                        collisionPath: "unit1_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT1_CZ",
                    new RoomMetadata(
                        name: "UNIT1_CZ",
                        inGameName: null,
                        archive: "unit1_CZ",
                        modelPath: "unit1_cz_model.bin",
                        animationPath: "unit1_cz_anim.bin",
                        collisionPath: "unit1_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT1_MORPH_CX",
                    new RoomMetadata(
                        name: "UNIT1_MORPH_CX",
                        inGameName: null,
                        archive: "unit1_morph_CX",
                        modelPath: "unit1_morph_cx_model.bin",
                        animationPath: "unit1_morph_cx_anim.bin",
                        collisionPath: "unit1_morph_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 18, 6),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 6, 4),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT1_MORPH_CZ",
                    new RoomMetadata(
                        name: "UNIT1_MORPH_CZ",
                        inGameName: null,
                        archive: "unit1_morph_CZ",
                        modelPath: "unit1_morph_cz_model.bin",
                        animationPath: "unit1_morph_cz_anim.bin",
                        collisionPath: "unit1_morph_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT2_CX",
                    new RoomMetadata(
                        name: "UNIT2_CX",
                        inGameName: null,
                        archive: "unit2_CX",
                        modelPath: "unit2_cx_model.bin",
                        animationPath: "unit2_cx_anim.bin",
                        collisionPath: "unit2_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT2_CZ",
                    new RoomMetadata(
                        name: "UNIT2_CZ",
                        inGameName: null,
                        archive: "unit2_CZ",
                        modelPath: "unit2_cz_model.bin",
                        animationPath: "unit2_cz_anim.bin",
                        collisionPath: "unit2_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT3_CX",
                    new RoomMetadata(
                        name: "UNIT3_CX",
                        inGameName: null,
                        archive: "unit3_CX",
                        modelPath: "unit3_cx_model.bin",
                        animationPath: "unit3_cx_anim.bin",
                        collisionPath: "unit3_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT3_CZ",
                    new RoomMetadata(
                        name: "UNIT3_CZ",
                        inGameName: null,
                        archive: "unit3_CZ",
                        modelPath: "unit3_cz_model.bin",
                        animationPath: "unit3_cz_anim.bin",
                        collisionPath: "unit3_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT4_CX",
                    new RoomMetadata(
                        name: "UNIT4_CX",
                        inGameName: null,
                        archive: "unit4_CX",
                        modelPath: "unit4_cx_model.bin",
                        animationPath: "unit4_cx_anim.bin",
                        collisionPath: "unit4_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT4_CZ",
                    new RoomMetadata(
                        name: "UNIT4_CZ",
                        inGameName: null,
                        archive: "unit4_CZ",
                        modelPath: "unit4_cz_model.bin",
                        animationPath: "unit4_cz_anim.bin",
                        collisionPath: "unit4_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "CYLINDER_C1",
                    new RoomMetadata(
                        name: "CYLINDER_C1",
                        inGameName: null,
                        archive: "Cylinder_C1_CZ",
                        modelPath: "Cylinder_C1_model.bin",
                        animationPath: "Cylinder_C1_anim.bin",
                        collisionPath: "Cylinder_C1_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(8, 28, 20),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "BIGEYE_C1",
                    new RoomMetadata(
                        name: "BIGEYE_C1",
                        inGameName: null,
                        archive: "BigEye_C1_CZ",
                        modelPath: "bigeye_c1_model.bin",
                        animationPath: "bigeye_c1_anim.bin",
                        collisionPath: "bigeye_c1_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(8, 28, 20),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT1_RM1_CX",
                    new RoomMetadata(
                        name: "UNIT1_RM1_CX",
                        inGameName: null,
                        archive: "unit1_RM1_CX",
                        modelPath: "unit1_rm1_cx_model.bin",
                        animationPath: "unit1_rm1_cx_anim.bin",
                        collisionPath: "unit1_rm1_cx_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "GOREA_C1",
                    new RoomMetadata(
                        name: "GOREA_C1",
                        inGameName: null,
                        archive: "Gorea_C1_CZ",
                        modelPath: "Gorea_c1_Model.bin",
                        animationPath: "Gorea_c1_Anim.bin",
                        collisionPath: "Gorea_c1_Collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(8, 28, 20),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT3_MORPH_CZ",
                    new RoomMetadata(
                        name: "UNIT3_MORPH_CZ",
                        inGameName: null,
                        archive: "unit3_morph_CZ",
                        modelPath: "unit3_morph_cz_model.bin",
                        animationPath: "unit3_morph_cz_anim.bin",
                        collisionPath: "unit3_morph_cz_collision.bin",
                        texturePath: null,
                        entityPath: null,
                        nodePath: null,
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x0)
                },
                {
                    "UNIT1_LAND",
                    new RoomMetadata(
                        name: "UNIT1_LAND",
                        inGameName: "Alinos Gateway",
                        archive: "unit1_Land",
                        modelPath: "unit1_land_model.bin",
                        animationPath: "unit1_land_anim.bin",
                        collisionPath: "unit1_land_collision.bin",
                        texturePath: "unit1_land_tex.bin",
                        entityPath: "Unit1_Land_Ent.bin",
                        nodePath: "unit1_Land_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 6553600,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C0",
                    new RoomMetadata(
                        name: "UNIT1_C0",
                        inGameName: "Echo Hall",
                        archive: "unit1_C0",
                        modelPath: "unit1_c0_model.bin",
                        animationPath: "unit1_c0_anim.bin",
                        collisionPath: "unit1_c0_collision.bin",
                        texturePath: "unit1_c0_tex.bin",
                        entityPath: "Unit1_C0_Ent.bin",
                        nodePath: "unit1_C0_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM1",
                    new RoomMetadata(
                        name: "UNIT1_RM1",
                        inGameName: "High Ground",
                        archive: "unit1_RM1",
                        modelPath: "unit1_RM1_model.bin",
                        animationPath: "unit1_RM1_anim.bin",
                        collisionPath: "unit1_RM1_collision.bin",
                        texturePath: "unit1_rm1_tex.bin",
                        entityPath: "unit1_RM1_Ent.bin",
                        nodePath: "unit1_RM1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C4",
                    new RoomMetadata(
                        name: "UNIT1_C4",
                        inGameName: "Magma Drop",
                        archive: "unit1_C4",
                        modelPath: "unit1_c4_model.bin",
                        animationPath: "unit1_c4_anim.bin",
                        collisionPath: "unit1_c4_collision.bin",
                        texturePath: "unit1_c4_tex.bin",
                        entityPath: "Unit1_C4_Ent.bin",
                        nodePath: "unit1_C4_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM6",
                    new RoomMetadata(
                        name: "UNIT1_RM6",
                        inGameName: "Elder Passage",
                        archive: "unit1_RM6",
                        modelPath: "unit1_rm6_model.bin",
                        animationPath: "unit1_rm6_anim.bin",
                        collisionPath: "unit1_rm6_collision.bin",
                        texturePath: "unit1_rm6_tex.bin",
                        entityPath: "unit1_RM6_Ent.bin",
                        nodePath: "unit1_RM6_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "CRYSTALROOM",
                    new RoomMetadata(
                        name: "CRYSTALROOM",
                        inGameName: "Alimbic Cannon Control Room",
                        archive: "crystalroom",
                        modelPath: "crystalroom_model.bin",
                        animationPath: "crystalroom_anim.bin",
                        collisionPath: "crystalroom_collision.bin",
                        texturePath: "crystalroom_tex.bin",
                        entityPath: "crystalroom_Ent.bin",
                        nodePath: "crystalroom_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(19, 29, 31),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(19, 29, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM4",
                    new RoomMetadata(
                        name: "UNIT1_RM4",
                        inGameName: "Combat Hall",
                        archive: "mp3",
                        modelPath: "mp3_Model.bin",
                        animationPath: "mp3_Anim.bin",
                        collisionPath: "mp3_Collision.bin",
                        texturePath: "mp3_Tex.bin",
                        entityPath: "unit1_rm4_Ent.bin",
                        nodePath: "unit1_RM4_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_TP1",
                    new RoomMetadata(
                        name: "UNIT1_TP1",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit1_TP1_Ent.bin",
                        nodePath: "unit1_TP1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(20, 8, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_B1",
                    new RoomMetadata(
                        name: "UNIT1_B1",
                        inGameName: null,
                        archive: "bigeyeroom",
                        modelPath: "bigeyeroom_model.bin",
                        animationPath: "bigeyeroom_anim.bin",
                        collisionPath: "bigeyeroom_collision.bin",
                        texturePath: "bigeyeroom_tex.bin",
                        entityPath: "Unit1_b1_Ent.bin",
                        nodePath: "unit2_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C1",
                    new RoomMetadata(
                        name: "UNIT1_C1",
                        inGameName: "Alimbic Gardens",
                        archive: "unit1_C1",
                        modelPath: "unit1_c1_model.bin",
                        animationPath: "unit1_c1_anim.bin",
                        collisionPath: "unit1_c1_collision.bin",
                        texturePath: "unit1_c1_tex.bin",
                        entityPath: "Unit1_C1_Ent.bin",
                        nodePath: "unit1_C1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C2",
                    new RoomMetadata(
                        name: "UNIT1_C2",
                        inGameName: "Thermal Vast",
                        archive: "unit1_C2",
                        modelPath: "unit1_c2_model.bin",
                        animationPath: "unit1_c2_anim.bin",
                        collisionPath: "unit1_c2_collision.bin",
                        texturePath: "unit1_c2_tex.bin",
                        entityPath: "Unit1_C2_Ent.bin",
                        nodePath: "unit1_C2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C5",
                    new RoomMetadata(
                        name: "UNIT1_C5",
                        inGameName: "Piston Cave",
                        archive: "unit1_C5",
                        modelPath: "unit1_c5_model.bin",
                        animationPath: "unit1_c5_anim.bin",
                        collisionPath: "unit1_c5_collision.bin",
                        texturePath: "unit1_c5_tex.bin",
                        entityPath: "Unit1_C5_Ent.bin",
                        nodePath: "unit1_RM5_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(31, 18, 6),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 9, 4),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM2",
                    new RoomMetadata(
                        name: "UNIT1_RM2",
                        inGameName: "Alinos Perch",
                        archive: "unit1_RM2",
                        modelPath: "unit1_rm2_model.bin",
                        animationPath: "unit1_rm2_anim.bin",
                        collisionPath: "unit1_rm2_collision.bin",
                        texturePath: "unit1_rm2_tex.bin",
                        entityPath: "unit1_RM2_ent.bin",
                        nodePath: "unit1_RM2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM3",
                    new RoomMetadata(
                        name: "UNIT1_RM3",
                        inGameName: "Council Chamber",
                        archive: "unit1_RM3",
                        modelPath: "unit1_rm3_model.bin",
                        animationPath: "unit1_rm3_anim.bin",
                        collisionPath: "unit1_rm3_collision.bin",
                        texturePath: "unit1_rm3_tex.bin",
                        entityPath: "unit1_rm3_Ent.bin",
                        nodePath: "unit1_RM3_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_RM5",
                    new RoomMetadata(
                        name: "UNIT1_RM5",
                        inGameName: "Processor Core",
                        archive: "mp7",
                        modelPath: "mp7_model.bin",
                        animationPath: "mp7_anim.bin",
                        collisionPath: "mp7_collision.bin",
                        texturePath: "mp7_tex.bin",
                        entityPath: "unit1_rm5_Ent.bin",
                        nodePath: "unit1_RM5_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 18, 6),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 6, 4),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_C3",
                    new RoomMetadata(
                        name: "UNIT1_C3",
                        inGameName: "Crash Site",
                        archive: "unit1_C3",
                        modelPath: "unit1_c3_model.bin",
                        animationPath: "unit1_c3_anim.bin",
                        collisionPath: "unit1_c3_collision.bin",
                        texturePath: "unit1_c3_tex.bin",
                        entityPath: "Unit1_C3_Ent.bin",
                        nodePath: "unit1_C3_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_TP2",
                    new RoomMetadata(
                        name: "UNIT1_TP2",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit1_TP2_Ent.bin",
                        nodePath: "unit1_TP2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 2,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT1_B2",
                    new RoomMetadata(
                        name: "UNIT1_B2",
                        inGameName: null,
                        archive: "cylinderroom",
                        modelPath: "cylinderroom_model.bin",
                        animationPath: "cylinderroom_anim.bin",
                        collisionPath: "cylinderroom_collision.bin",
                        texturePath: "cylinderroom_tex.bin",
                        entityPath: "Unit1_b2_Ent.bin",
                        nodePath: "unit2_b1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_LAND",
                    new RoomMetadata(
                        name: "UNIT2_LAND",
                        inGameName: "Celestial Gateway",
                        archive: "unit2_Land",
                        modelPath: "unit2_Land_model.bin",
                        animationPath: "unit2_Land_anim.bin",
                        collisionPath: "unit2_Land_collision.bin",
                        texturePath: "unit2_land_tex.bin",
                        entityPath: "unit2_Land_Ent.bin",
                        nodePath: "unit2_Land_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 24, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 6553600,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C0",
                    new RoomMetadata(
                        name: "UNIT2_C0",
                        inGameName: "Helm Room",
                        archive: "unit2_C0",
                        modelPath: "unit2_c0_model.bin",
                        animationPath: "unit2_c0_anim.bin",
                        collisionPath: "unit2_c0_collision.bin",
                        texturePath: "unit2_c0_tex.bin",
                        entityPath: "unit2_C0_Ent.bin",
                        nodePath: "unit2_C0_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 6553600,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C1",
                    new RoomMetadata(
                        name: "UNIT2_C1",
                        inGameName: "Meditation Room",
                        archive: "unit2_C1",
                        modelPath: "unit2_c1_model.bin",
                        animationPath: "unit2_c1_anim.bin",
                        collisionPath: "unit2_c1_collision.bin",
                        texturePath: "unit2_c1_tex.bin",
                        entityPath: "unit2_C1_Ent.bin",
                        nodePath: "unit2_C1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM1",
                    new RoomMetadata(
                        name: "UNIT2_RM1",
                        inGameName: "Data Shrine 01",
                        archive: "mp1",
                        modelPath: "mp1_Model.bin",
                        animationPath: "mp1_Anim.bin",
                        collisionPath: "mp1_Collision.bin",
                        texturePath: "mp1_tex.bin",
                        entityPath: "unit2_RM1_Ent.bin",
                        nodePath: "unit2_RM1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C2",
                    new RoomMetadata(
                        name: "UNIT2_C2",
                        inGameName: "Fan Room Alpha",
                        archive: "unit2_C2",
                        modelPath: "unit2_c2_model.bin",
                        animationPath: "unit2_c2_anim.bin",
                        collisionPath: "unit2_c2_collision.bin",
                        texturePath: "unit2_c2_tex.bin",
                        entityPath: "unit2_C2_Ent.bin",
                        nodePath: "unit2_C2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM2",
                    new RoomMetadata(
                        name: "UNIT2_RM2",
                        inGameName: "Data Shrine 02",
                        archive: "mp1",
                        modelPath: "mp1_Model.bin",
                        animationPath: "mp1_Anim.bin",
                        collisionPath: "mp1_Collision.bin",
                        texturePath: "mp1_tex.bin",
                        entityPath: "unit2_RM2_Ent.bin",
                        nodePath: "unit2_RM2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 2,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C3",
                    new RoomMetadata(
                        name: "UNIT2_C3",
                        inGameName: "Fan Room Beta",
                        archive: "unit2_C3",
                        modelPath: "unit2_c3_model.bin",
                        animationPath: "unit2_c3_anim.bin",
                        collisionPath: "unit2_c3_collision.bin",
                        texturePath: "unit2_c3_tex.bin",
                        entityPath: "unit2_C3_Ent.bin",
                        nodePath: "unit2_C3_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM3",
                    new RoomMetadata(
                        name: "UNIT2_RM3",
                        inGameName: "Data Shrine 03",
                        archive: "unit2_RM3",
                        modelPath: "unit2_RM3_model.bin",
                        animationPath: "unit2_RM3_anim.bin",
                        collisionPath: "unit2_RM3_collision.bin",
                        texturePath: "unit2_rm3_tex.bin",
                        entityPath: "unit2_RM3_Ent.bin",
                        nodePath: "unit2_RM3_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 3,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C4",
                    new RoomMetadata(
                        name: "UNIT2_C4",
                        inGameName: "Synergy Core",
                        archive: "unit2_C4",
                        modelPath: "unit2_c4_model.bin",
                        animationPath: "unit2_c4_anim.bin",
                        collisionPath: "unit2_c4_collision.bin",
                        texturePath: "unit2_c4_tex.bin",
                        entityPath: "unit2_C4_Ent.bin",
                        nodePath: "unit2_C4_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_TP1",
                    new RoomMetadata(
                        name: "UNIT2_TP1",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit2_TP1_Ent.bin",
                        nodePath: "unit2_TP1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 3,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(17, 29, 16),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_B1",
                    new RoomMetadata(
                        name: "UNIT2_B1",
                        inGameName: null,
                        archive: "cylinderroom",
                        modelPath: "cylinderroom_model.bin",
                        animationPath: "cylinderroom_anim.bin",
                        collisionPath: "cylinderroom_collision.bin",
                        texturePath: "cylinderroom_tex.bin",
                        entityPath: "Unit2_b1_Ent.bin",
                        nodePath: "unit2_b1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C6",
                    new RoomMetadata(
                        name: "UNIT2_C6",
                        inGameName: "Tetra Vista",
                        archive: "unit2_C6",
                        modelPath: "unit2_c6_model.bin",
                        animationPath: "unit2_c6_anim.bin",
                        collisionPath: "unit2_c6_collision.bin",
                        texturePath: "unit2_c6_tex.bin",
                        entityPath: "Unit2_C6_Ent.bin",
                        nodePath: "unit2_C6_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 31, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 16384000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_C7",
                    new RoomMetadata(
                        name: "UNIT2_C7",
                        inGameName: "New Arrival Registration",
                        archive: "unit2_C7",
                        modelPath: "unit2_c7_model.bin",
                        animationPath: "unit2_c7_anim.bin",
                        collisionPath: "unit2_c7_collision.bin",
                        texturePath: "unit2_c7_tex.bin",
                        entityPath: "Unit2_C7_Ent.bin",
                        nodePath: "unit2_C7_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 16384000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM4",
                    new RoomMetadata(
                        name: "UNIT2_RM4",
                        inGameName: "Transfer Lock",
                        archive: "unit2_RM4",
                        modelPath: "unit2_rm4_model.bin",
                        animationPath: "unit2_rm4_anim.bin",
                        collisionPath: "unit2_rm4_collision.bin",
                        texturePath: "unit2_rm4_tex.bin",
                        entityPath: "Unit2_RM4_Ent.bin",
                        nodePath: "unit2_RM4_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 1,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(29, 20, 10),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(29, 20, 10),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM5",
                    new RoomMetadata(
                        name: "UNIT2_RM5",
                        inGameName: "Incubation Vault 01",
                        archive: "mp10",
                        modelPath: "mp10_model.bin",
                        animationPath: "mp10_anim.bin",
                        collisionPath: "mp10_collision.bin",
                        texturePath: "mp10_tex.bin",
                        entityPath: "Unit2_RM5_Ent.bin",
                        nodePath: "unit2_RM5_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 1,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(0, 25, 31),
                        fogSlope: 4,
                        fogOffset: 31727,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM6",
                    new RoomMetadata(
                        name: "UNIT2_RM6",
                        inGameName: "Incubation Vault 02",
                        archive: "mp10",
                        modelPath: "mp10_model.bin",
                        animationPath: "mp10_anim.bin",
                        collisionPath: "mp10_collision.bin",
                        texturePath: "mp10_tex.bin",
                        entityPath: "Unit2_RM6_Ent.bin",
                        nodePath: "unit2_RM6_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 2,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM7",
                    new RoomMetadata(
                        name: "UNIT2_RM7",
                        inGameName: "Incubation Vault 03",
                        archive: "mp10",
                        modelPath: "mp10_model.bin",
                        animationPath: "mp10_anim.bin",
                        collisionPath: "mp10_collision.bin",
                        texturePath: "mp10_tex.bin",
                        entityPath: "Unit2_RM7_Ent.bin",
                        nodePath: "unit2_RM7_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 3,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(0, 31, 10),
                        fogSlope: 4,
                        fogOffset: 31727,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_RM8",
                    new RoomMetadata(
                        name: "UNIT2_RM8",
                        inGameName: "Docking Bay",
                        archive: "unit2_RM8",
                        modelPath: "unit2_rm8_model.bin",
                        animationPath: "unit2_rm8_anim.bin",
                        collisionPath: "unit2_rm8_collision.bin",
                        texturePath: "unit2_rm8_tex.bin",
                        entityPath: "unit2_RM8_Ent.bin",
                        nodePath: "unit2_RM8_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 1,
                        nodeLayer: 1,
                        fogEnabled: false,
                        clearFog: false,
                        fogColor: new ColorRgb(29, 20, 10),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(29, 20, 10),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_TP2",
                    new RoomMetadata(
                        name: "UNIT2_TP2",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit2_TP2_Ent.bin",
                        nodePath: "unit2_TP2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 4,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(17, 29, 16),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT2_B2",
                    new RoomMetadata(
                        name: "UNIT2_B2",
                        inGameName: null,
                        archive: "bigeyeroom",
                        modelPath: "bigeyeroom_model.bin",
                        animationPath: "bigeyeroom_anim.bin",
                        collisionPath: "bigeyeroom_collision.bin",
                        texturePath: "bigeyeroom_tex.bin",
                        entityPath: "Unit2_b2_Ent.bin",
                        nodePath: "unit2_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_LAND",
                    new RoomMetadata(
                        name: "UNIT3_LAND",
                        inGameName: "VDO Gateway",
                        archive: "unit3_Land",
                        modelPath: "unit3_land_model.bin",
                        animationPath: "unit3_land_anim.bin",
                        collisionPath: "unit3_land_collision.bin",
                        texturePath: "unit3_land_tex.bin",
                        entityPath: "unit3_Land_Ent.bin",
                        nodePath: "unit3_Land_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 6553600,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_C0",
                    new RoomMetadata(
                        name: "UNIT3_C0",
                        inGameName: "Bioweaponry Lab",
                        archive: "unit3_C0",
                        modelPath: "unit3_c0_model.bin",
                        animationPath: "unit3_c0_anim.bin",
                        collisionPath: "unit3_c0_collision.bin",
                        texturePath: "unit3_c0_tex.bin",
                        entityPath: "unit3_C0_Ent.bin",
                        nodePath: "unit3_C0_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_C2",
                    new RoomMetadata(
                        name: "UNIT3_C2",
                        inGameName: "Cortex CPU",
                        archive: "unit3_C2",
                        modelPath: "unit3_c2_model.bin",
                        animationPath: "unit3_c2_anim.bin",
                        collisionPath: "unit3_c2_collision.bin",
                        texturePath: "unit3_c2_tex.bin",
                        entityPath: "Unit3_C2_Ent.bin",
                        nodePath: "unit3_c2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 50,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_RM1",
                    new RoomMetadata(
                        name: "UNIT3_RM1",
                        inGameName: "Weapons Complex",
                        archive: "unit3_RM1",
                        modelPath: "unit3_rm1_model.bin",
                        animationPath: "unit3_rm1_anim.bin",
                        collisionPath: "unit3_rm1_collision.bin",
                        texturePath: "unit3_rm1_Tex.bin",
                        entityPath: "Unit3_RM1_Ent.bin",
                        nodePath: "unit3_RM1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 50,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_RM4",
                    new RoomMetadata(
                        name: "UNIT3_RM4",
                        inGameName: "Compression Chamber",
                        archive: "mp5",
                        modelPath: "mp5_Model.bin",
                        animationPath: "mp5_Anim.bin",
                        collisionPath: "mp5_Collision.bin",
                        texturePath: "mp5_tex.bin",
                        entityPath: "unit3_rm4_Ent.bin",
                        nodePath: "Unit3_RM4_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 50,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_TP1",
                    new RoomMetadata(
                        name: "UNIT3_TP1",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit3_TP1_Ent.bin",
                        nodePath: "unit3_TP1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 5,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_B1",
                    new RoomMetadata(
                        name: "UNIT3_B1",
                        inGameName: null,
                        archive: "cylinderroom",
                        modelPath: "cylinderroom_model.bin",
                        animationPath: "cylinderroom_anim.bin",
                        collisionPath: "cylinderroom_collision.bin",
                        texturePath: "cylinderroom_tex.bin",
                        entityPath: "Unit3_b1_Ent.bin",
                        nodePath: "unit2_b1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_C1",
                    new RoomMetadata(
                        name: "UNIT3_C1",
                        inGameName: "Ascension",
                        archive: "unit3_C1",
                        modelPath: "unit3_c1_model.bin",
                        animationPath: "unit3_c1_anim.bin",
                        collisionPath: "unit3_c1_collision.bin",
                        texturePath: "unit3_c1_tex.bin",
                        entityPath: "unit3_C1_Ent.bin",
                        nodePath: "unit3_C1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_RM2",
                    new RoomMetadata(
                        name: "UNIT3_RM2",
                        inGameName: "Fuel Stack",
                        archive: "unit3_RM2",
                        modelPath: "unit3_rm2_model.bin",
                        animationPath: "unit3_rm2_anim.bin",
                        collisionPath: "unit3_rm2_collision.bin",
                        texturePath: "unit3_rm2_tex.bin",
                        entityPath: "Unit3_RM2_Ent.bin",
                        nodePath: "unit3_RM2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 2457600,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_RM3",
                    new RoomMetadata(
                        name: "UNIT3_RM3",
                        inGameName: "Stasis Bunker",
                        archive: "e3Level",
                        modelPath: "e3Level_Model.bin",
                        animationPath: "e3Level_Anim.bin",
                        collisionPath: "e3Level_Collision.bin",
                        texturePath: "e3level_tex.bin",
                        entityPath: "Unit3_RM3_Ent.bin",
                        nodePath: "unit3_rm3_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_TP2",
                    new RoomMetadata(
                        name: "UNIT3_TP2",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit3_TP2_Ent.bin",
                        nodePath: "unit3_TP2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 6,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT3_B2",
                    new RoomMetadata(
                        name: "UNIT3_B2",
                        inGameName: null,
                        archive: "bigeyeroom",
                        modelPath: "bigeyeroom_model.bin",
                        animationPath: "bigeyeroom_anim.bin",
                        collisionPath: "bigeyeroom_collision.bin",
                        texturePath: "bigeyeroom_tex.bin",
                        entityPath: "Unit3_b2_Ent.bin",
                        nodePath: "unit2_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 2,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_LAND",
                    new RoomMetadata(
                        name: "UNIT4_LAND",
                        inGameName: "Arcterra Gateway",
                        archive: "unit4_Land",
                        modelPath: "unit4_land_model.bin",
                        animationPath: "unit4_land_anim.bin",
                        collisionPath: "unit4_land_collision.bin",
                        texturePath: "unit4_land_tex.bin",
                        entityPath: "unit4_Land_Ent.bin",
                        nodePath: "unit4_Land_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 16),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_RM1",
                    new RoomMetadata(
                        name: "UNIT4_RM1",
                        inGameName: "Ice Hive",
                        archive: "unit4_rm1",
                        modelPath: "unit4_rm1_model.bin",
                        animationPath: "unit4_rm1_anim.bin",
                        collisionPath: "unit4_rm1_collision.bin",
                        texturePath: "unit4_rm1_Tex.bin",
                        entityPath: "Unit4_RM1_Ent.bin",
                        nodePath: "unit4_RM1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 327680,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_RM3",
                    new RoomMetadata(
                        name: "UNIT4_RM3",
                        inGameName: "Sic Transit",
                        archive: "mp12",
                        modelPath: "mp12_model.bin",
                        animationPath: "mp12_anim.bin",
                        collisionPath: "mp12_collision.bin",
                        texturePath: "mp12_Tex.bin",
                        entityPath: "unit4_rm3_Ent.bin",
                        nodePath: "Unit4_RM3_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 16),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_C0",
                    new RoomMetadata(
                        name: "UNIT4_C0",
                        inGameName: "Frost Labyrinth",
                        archive: "unit4_C0",
                        modelPath: "unit4_c0_model.bin",
                        animationPath: "unit4_c0_anim.bin",
                        collisionPath: "unit4_c0_collision.bin",
                        texturePath: "unit4_c0_tex.bin",
                        entityPath: "unit4_C0_Ent.bin",
                        nodePath: "unit4_C0_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_TP1",
                    new RoomMetadata(
                        name: "UNIT4_TP1",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit4_TP1_Ent.bin",
                        nodePath: "unit4_TP1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 7,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_B1",
                    new RoomMetadata(
                        name: "UNIT4_B1",
                        inGameName: null,
                        archive: "bigeyeroom",
                        modelPath: "bigeyeroom_model.bin",
                        animationPath: "bigeyeroom_anim.bin",
                        collisionPath: "bigeyeroom_collision.bin",
                        texturePath: "bigeyeroom_tex.bin",
                        entityPath: "unit4_b1_Ent.bin",
                        nodePath: "unit2_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_C1",
                    new RoomMetadata(
                        name: "UNIT4_C1",
                        inGameName: "Drip Moat",
                        archive: "unit4_C1",
                        modelPath: "unit4_c1_model.bin",
                        animationPath: "unit4_c1_anim.bin",
                        collisionPath: "unit4_c1_collision.bin",
                        texturePath: "unit4_c1_tex.bin",
                        entityPath: "unit4_C1_Ent.bin",
                        nodePath: "unit4_c1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 5,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: true,
                        fogColor: new ColorRgb(0, 0, 0),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_RM2",
                    new RoomMetadata(
                        name: "UNIT4_RM2",
                        inGameName: "Subterranean",
                        archive: "unit4_rm2",
                        modelPath: "unit4_rm2_model.bin",
                        animationPath: "unit4_rm2_anim.bin",
                        collisionPath: "unit4_rm2_collision.bin",
                        texturePath: "unit4_rm2_tex.bin",
                        entityPath: "Unit4_RM2_Ent.bin",
                        nodePath: "unit4_RM2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 5,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 16),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_RM4",
                    new RoomMetadata(
                        name: "UNIT4_RM4",
                        inGameName: "Sanctorus",
                        archive: "mp11",
                        modelPath: "mp11_model.bin",
                        animationPath: "mp11_anim.bin",
                        collisionPath: "mp11_collision.bin",
                        texturePath: "mp11_tex.bin",
                        entityPath: "unit4_rm4_Ent.bin",
                        nodePath: "Unit4_RM4_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 327680,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_RM5",
                    new RoomMetadata(
                        name: "UNIT4_RM5",
                        inGameName: "Fault Line",
                        archive: "unit4_rm5",
                        modelPath: "unit4_rm5_model.bin",
                        animationPath: "unit4_rm5_anim.bin",
                        collisionPath: "unit4_rm5_collision.bin",
                        texturePath: "unit4_rm5_tex.bin",
                        entityPath: "Unit4_RM5_Ent.bin",
                        nodePath: "Unit4_RM5_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 5,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 16),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_TP2",
                    new RoomMetadata(
                        name: "UNIT4_TP2",
                        inGameName: null,
                        archive: "TeleportRoom",
                        modelPath: "TeleportRoom_model.bin",
                        animationPath: "TeleportRoom_anim.bin",
                        collisionPath: "TeleportRoom_collision.bin",
                        texturePath: "teleportroom_tex.bin",
                        entityPath: "Unit4_TP2_Ent.bin",
                        nodePath: "unit4_TP2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 7,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 6,
                        fogOffset: 65350,
                        light1Color: new ColorRgb(8, 28, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "UNIT4_B2",
                    new RoomMetadata(
                        name: "UNIT4_B2",
                        inGameName: null,
                        archive: "cylinderroom",
                        modelPath: "cylinderroom_model.bin",
                        animationPath: "cylinderroom_anim.bin",
                        collisionPath: "cylinderroom_collision.bin",
                        texturePath: "cylinderroom_tex.bin",
                        entityPath: "unit4_b2_Ent.bin",
                        nodePath: "unit2_b1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(12, 6, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(12, 6, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(31, 25, 21),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "Gorea_Land",
                    new RoomMetadata(
                        name: "Gorea_Land",
                        inGameName: null,
                        archive: "Gorea_Land",
                        modelPath: "Gorea_Land_Model.bin",
                        animationPath: "Gorea_Land_Anim.bin",
                        collisionPath: "Gorea_Land_collision.bin",
                        texturePath: "Gorea_Land_tex.bin",
                        entityPath: "Gorea_Land_Ent.bin",
                        nodePath: "Gorea_Land_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(17, 29, 16),
                        fogSlope: 6,
                        fogOffset: 65330,
                        light1Color: new ColorRgb(17, 29, 16),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 8192000,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "Gorea_Peek",
                    new RoomMetadata(
                        name: "Gorea_Peek",
                        inGameName: null,
                        archive: "Gorea_b2",
                        modelPath: "gorea_b2_Model.bin",
                        animationPath: "gorea_b2_Anim.bin",
                        collisionPath: "Gorea_b2_collision.bin",
                        texturePath: "gorea_b2_tex.bin",
                        entityPath: "Gorea_Peek_Ent.bin",
                        nodePath: "gorea_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(17, 29, 16),
                        fogSlope: 5,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(19, 27, 16),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 6, 12),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFBA000,
                        field6C: 0x6)
                },
                {
                    "Gorea_b1",
                    new RoomMetadata(
                        name: "Gorea_b1",
                        inGameName: null,
                        archive: "Gorea_b1",
                        modelPath: "Gorea_b1_Model.bin",
                        animationPath: "Gorea_b1_Anim.bin",
                        collisionPath: "Gorea_b1_collision.bin",
                        texturePath: "Gorea_b1_tex.bin",
                        entityPath: "Gorea_b1_Ent.bin",
                        nodePath: "Gorea_b1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: true,
                        fogColor: new ColorRgb(9, 18, 24),
                        fogSlope: 4,
                        fogOffset: 32550,
                        light1Color: new ColorRgb(18, 24, 27),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 18, 24),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "Gorea_b2",
                    new RoomMetadata(
                        name: "Gorea_b2",
                        inGameName: null,
                        archive: "Gorea_b2",
                        modelPath: "gorea_b2_Model.bin",
                        animationPath: "gorea_b2_Anim.bin",
                        collisionPath: "Gorea_b2_collision.bin",
                        texturePath: "gorea_b2_tex.bin",
                        entityPath: "gorea_b2_Ent.bin",
                        nodePath: "gorea_b2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 2,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(16, 30, 25),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(18, 16, 14),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(27, 18, 9),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFBA000,
                        field6C: 0x6)
                },
                {
                    "MP1 SANCTORUS",
                    new RoomMetadata(
                        name: "MP1 SANCTORUS",
                        inGameName: "Data Shrine",
                        archive: "mp1",
                        modelPath: "mp1_Model.bin",
                        animationPath: "mp1_Anim.bin",
                        collisionPath: "mp1_Collision.bin",
                        texturePath: "mp1_tex.bin",
                        entityPath: "mp1_Ent.bin",
                        nodePath: "mp1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 368640,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "MP2 HARVESTER",
                    new RoomMetadata(
                        name: "MP2 HARVESTER",
                        inGameName: "Harvester",
                        archive: "mp2",
                        modelPath: "mp2_model.bin",
                        animationPath: "mp2_anim.bin",
                        collisionPath: "mp2_collision.bin",
                        texturePath: "mp2_tex.bin",
                        entityPath: "mp2_Ent.bin",
                        nodePath: "mp2_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: true,
                        fogColor: new ColorRgb(25, 30, 20),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(25, 30, 20),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 11, 6),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 3072000,
                        field68: 0x1000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "MP3 PROVING GROUND",
                    new RoomMetadata(
                        name: "MP3 PROVING GROUND",
                        inGameName: "Combat Hall",
                        archive: "mp3",
                        modelPath: "mp3_Model.bin",
                        animationPath: "mp3_Anim.bin",
                        collisionPath: "mp3_Collision.bin",
                        texturePath: "mp3_Tex.bin",
                        entityPath: "mp3_Ent.bin",
                        nodePath: "mp3_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 5,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        farClip: 1433600,
                        field68: 0xFFFE2000,
                        field6C: 0x2,
                        multiplayer: true)
                },
                {
                    "MP4 HIGHGROUND - EXPANDED",
                    new RoomMetadata(
                        name: "MP4 HIGHGROUND - EXPANDED",
                        inGameName: "Elder Passage",
                        archive: "mp4",
                        modelPath: "mp4_model.bin",
                        animationPath: "mp4_anim.bin",
                        collisionPath: "mp4_collision.bin",
                        texturePath: "mp4_Tex.bin",
                        entityPath: "mp4_Ent.bin",
                        nodePath: "mp4_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 5,
                        fogOffset: 65200,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1687552,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "MP4 HIGHGROUND",
                    new RoomMetadata(
                        name: "MP4 HIGHGROUND",
                        inGameName: "High Ground",
                        archive: "unit1_RM1",
                        modelPath: "unit1_RM1_model.bin",
                        animationPath: "unit1_RM1_anim.bin",
                        collisionPath: "unit1_RM1_collision.bin",
                        texturePath: "unit1_rm1_tex.bin",
                        entityPath: "mp4_dm1_Ent.bin",
                        nodePath: "mp4_dm1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 5,
                        fogOffset: 65200,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1740800,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "MP5 FUEL SLUICE",
                    new RoomMetadata(
                        name: "MP5 FUEL SLUICE",
                        inGameName: "Compression Chamber",
                        archive: "mp5",
                        modelPath: "mp5_Model.bin",
                        animationPath: "mp5_Anim.bin",
                        collisionPath: "mp5_Collision.bin",
                        texturePath: "mp5_tex.bin",
                        entityPath: "mp5_Ent.bin",
                        nodePath: "mp5_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 3,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 22, 30),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x2,
                        multiplayer: true)
                },
                {
                    "MP6 HEADSHOT",
                    new RoomMetadata(
                        name: "MP6 HEADSHOT",
                        inGameName: "Head Shot",
                        archive: "mp6",
                        modelPath: "mp6_model.bin",
                        animationPath: "mp6_anim.bin",
                        collisionPath: "mp6_collision.bin",
                        texturePath: "mp6_tex.bin",
                        entityPath: "mp6_Ent.bin",
                        nodePath: "mp6_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 3,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 22, 30),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 5734400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "MP7 PROCESSOR CORE",
                    new RoomMetadata(
                        name: "MP7 PROCESSOR CORE",
                        inGameName: "Processor Core",
                        archive: "mp7",
                        modelPath: "mp7_model.bin",
                        animationPath: "mp7_anim.bin",
                        collisionPath: "mp7_collision.bin",
                        texturePath: "mp7_tex.bin",
                        entityPath: "mp7_Ent.bin",
                        nodePath: "mp7_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 18, 6),
                        fogSlope: 6,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(31, 18, 6),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 6, 4),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x2,
                        multiplayer: true)
                },
                {
                    "MP8 FIRE CONTROL",
                    new RoomMetadata(
                        name: "MP8 FIRE CONTROL",
                        inGameName: "Weapons Complex",
                        archive: "mp8",
                        modelPath: "mp8_model.bin",
                        animationPath: "mp8_anim.bin",
                        collisionPath: "mp8_collision.bin",
                        texturePath: "mp8_Tex.bin",
                        entityPath: "mp8_Ent.bin",
                        nodePath: "mp8_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 50,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 24, 31),
                        fogSlope: 3,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 22, 30),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 4096000,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "MP9 CRYOCHASM",
                    new RoomMetadata(
                        name: "MP9 CRYOCHASM",
                        inGameName: "Ice Hive",
                        archive: "mp9",
                        modelPath: "mp9_model.bin",
                        animationPath: "mp9_anim.bin",
                        collisionPath: "mp9_collision.bin",
                        texturePath: "mp9_tex.bin",
                        entityPath: "mp9_Ent.bin",
                        nodePath: "mp9_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 327680,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "MP10 OVERLOAD",
                    new RoomMetadata(
                        name: "MP10 OVERLOAD",
                        inGameName: "Incubation Vault",
                        archive: "mp10",
                        modelPath: "mp10_model.bin",
                        animationPath: "mp10_anim.bin",
                        collisionPath: "mp10_collision.bin",
                        texturePath: "mp10_tex.bin",
                        entityPath: "mp10_Ent.bin",
                        nodePath: "mp10_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 409600,
                        field68: 0xFFFE2000,
                        field6C: 0x2,
                        multiplayer: true)
                },
                {
                    "MP11 BREAKTHROUGH",
                    new RoomMetadata(
                        name: "MP11 BREAKTHROUGH",
                        inGameName: "Sanctorus",
                        archive: "mp11",
                        modelPath: "mp11_model.bin",
                        animationPath: "mp11_anim.bin",
                        collisionPath: "mp11_collision.bin",
                        texturePath: "mp11_tex.bin",
                        entityPath: "mp11_Ent.bin",
                        nodePath: "mp11_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(20, 27, 31),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(20, 27, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(8, 8, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 409600,
                        field68: 0xFFFE2000,
                        field6C: 0x2,
                        multiplayer: true)
                },
                {
                    "MP12 SIC TRANSIT",
                    new RoomMetadata(
                        name: "MP12 SIC TRANSIT",
                        inGameName: "Sic Transit",
                        archive: "mp12",
                        modelPath: "mp12_model.bin",
                        animationPath: "mp12_anim.bin",
                        collisionPath: "mp12_collision.bin",
                        texturePath: "mp12_Tex.bin",
                        entityPath: "mp12_Ent.bin",
                        nodePath: "mp12_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 18),
                        fogSlope: 6,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "MP13 ACCELERATOR",
                    new RoomMetadata(
                        name: "MP13 ACCELERATOR",
                        inGameName: "Fuel Stack",
                        archive: "mp13",
                        modelPath: "mp13_model.bin",
                        animationPath: "mp13_anim.bin",
                        collisionPath: "mp13_collision.bin",
                        texturePath: "mp13_tex.bin",
                        entityPath: "mp13_Ent.bin",
                        nodePath: "mp13_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 3,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 22, 30),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "MP14 OUTER REACH",
                    new RoomMetadata(
                        name: "MP14 OUTER REACH",
                        inGameName: "Outer Reach",
                        archive: "mp14",
                        modelPath: "mp14_model.bin",
                        animationPath: "mp14_anim.bin",
                        collisionPath: "mp14_collision.bin",
                        texturePath: "mp14_tex.bin",
                        entityPath: "mp14_Ent.bin",
                        nodePath: "mp14_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(16, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 4915200,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "CTF1 FAULT LINE - EXPANDED",
                    new RoomMetadata(
                        name: "CTF1 FAULT LINE - EXPANDED",
                        inGameName: "Fault Line",
                        archive: "ctf1",
                        modelPath: "ctf1_model.bin",
                        animationPath: "ctf1_anim.bin",
                        collisionPath: "ctf1_collision.bin",
                        texturePath: "ctf1_tex.bin",
                        entityPath: "ctf1_Ent.bin",
                        nodePath: "ctf1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 5,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 18),
                        fogSlope: 6,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "CTF1_FAULT LINE",
                    new RoomMetadata(
                        name: "CTF1_FAULT LINE",
                        inGameName: "Subterranean",
                        archive: "unit4_rm5",
                        modelPath: "unit4_rm5_model.bin",
                        animationPath: "unit4_rm5_anim.bin",
                        collisionPath: "unit4_rm5_collision.bin",
                        texturePath: "unit4_rm5_tex.bin",
                        entityPath: "ctf1_dm1_Ent.bin",
                        nodePath: "ctf1_dm1_NODE.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(10, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 5,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 18),
                        fogSlope: 6,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "AD1 TRANSFER LOCK BT",
                    new RoomMetadata(
                        name: "AD1 TRANSFER LOCK BT",
                        inGameName: "Transfer Lock",
                        archive: "ad1",
                        modelPath: "ad1_model.bin",
                        animationPath: "ad1_anim.bin",
                        collisionPath: "ad1_collision.bin",
                        texturePath: "ad1_tex.bin",
                        entityPath: "ad1_Ent.bin",
                        nodePath: "ad1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 1,
                        nodeLayer: 1,
                        fogEnabled: false,
                        clearFog: false,
                        fogColor: new ColorRgb(29, 20, 10),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(29, 20, 10),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 5734400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "AD1 TRANSFER LOCK DM",
                    new RoomMetadata(
                        name: "AD1 TRANSFER LOCK DM",
                        inGameName: "Transfer Lock",
                        archive: "unit2_RM4",
                        modelPath: "unit2_rm4_model.bin",
                        animationPath: "unit2_rm4_anim.bin",
                        collisionPath: "unit2_rm4_collision.bin",
                        texturePath: "unit2_rm4_tex.bin",
                        entityPath: "ad1_dm1_Ent.bin",
                        nodePath: "ad1_dm1_NODE.bin",
                        roomNodeName: "rmGoal",
                        battleTimeLimit: TimeLimit(20, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 1,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(29, 20, 10),
                        fogSlope: 4,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(29, 20, 10),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 3276800,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "AD2 MAGMA VENTS",
                    new RoomMetadata(
                        name: "AD2 MAGMA VENTS",
                        inGameName: "Council Chamber",
                        archive: "ad2",
                        modelPath: "ad2_model.bin",
                        animationPath: "ad2_anim.bin",
                        collisionPath: "ad2_collision.bin",
                        texturePath: "ad2_tex.bin",
                        entityPath: "ad2_Ent.bin",
                        nodePath: "ad2_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 5,
                        fogOffset: 65200,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "AD2 ALINOS PERCH",
                    new RoomMetadata(
                        name: "AD2 ALINOS PERCH",
                        inGameName: "Alinos Perch",
                        archive: "unit1_RM2",
                        modelPath: "unit1_rm2_model.bin",
                        animationPath: "unit1_rm2_anim.bin",
                        collisionPath: "unit1_rm2_collision.bin",
                        texturePath: "unit1_rm2_tex.bin",
                        entityPath: "ad2_dm1_Ent.bin",
                        nodePath: "ad2_dm1_NODE.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 5,
                        fogOffset: 65200,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "UNIT1 ALINOS LANDFALL",
                    new RoomMetadata(
                        name: "UNIT1 ALINOS LANDFALL",
                        inGameName: "Alinos Gateway",
                        archive: "unit1_Land",
                        modelPath: "unit1_land_model.bin",
                        animationPath: "unit1_land_anim.bin",
                        collisionPath: "unit1_land_collision.bin",
                        texturePath: "unit1_land_tex.bin",
                        entityPath: "Unit1_Land_dm1_Ent.bin",
                        nodePath: "unit1_Land_dm1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(31, 24, 18),
                        fogSlope: 4,
                        fogOffset: 65180,
                        light1Color: new ColorRgb(31, 24, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(13, 12, 7),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 4915200,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "UNIT2 LANDING BAY",
                    new RoomMetadata(
                        name: "UNIT2 LANDING BAY",
                        inGameName: "Celestial Gateway",
                        archive: "unit2_Land",
                        modelPath: "unit2_Land_model.bin",
                        animationPath: "unit2_Land_anim.bin",
                        collisionPath: "unit2_Land_collision.bin",
                        texturePath: "unit2_land_tex.bin",
                        entityPath: "unit2_land_dm1_Ent.bin",
                        nodePath: "unit2_land_dm1_NODE.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(18, 31, 18),
                        fogSlope: 4,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 31, 24),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(7, 11, 15),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 5734400,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "UNIT 3 VESPER STARPORT",
                    new RoomMetadata(
                        name: "UNIT 3 VESPER STARPORT",
                        inGameName: "VDO Gateway",
                        archive: "unit3_Land",
                        modelPath: "unit3_land_model.bin",
                        animationPath: "unit3_land_anim.bin",
                        collisionPath: "unit3_land_collision.bin",
                        texturePath: "unit3_land_tex.bin",
                        entityPath: "unit3_Land_dm1_Ent.bin",
                        nodePath: "unit3_Land_dm1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 3,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(18, 22, 30),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "UNIT 4 ARCTERRA BASE",
                    new RoomMetadata(
                        name: "UNIT 4 ARCTERRA BASE",
                        inGameName: "Arcterra Gateway",
                        archive: "unit4_Land",
                        modelPath: "unit4_land_model.bin",
                        animationPath: "unit4_land_anim.bin",
                        collisionPath: "unit4_land_collision.bin",
                        texturePath: "unit4_land_tex.bin",
                        entityPath: "unit4_Land_dm1_Ent.bin",
                        nodePath: "unit4_Land_dm1_node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(10, 14, 18),
                        fogSlope: 6,
                        fogOffset: 65300,
                        light1Color: new ColorRgb(10, 14, 18),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(4, 4, 8),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "Gorea Prison",
                    new RoomMetadata(
                        name: "Gorea Prison",
                        inGameName: "Oubliette",
                        archive: "Gorea_b2",
                        modelPath: "gorea_b2_Model.bin",
                        animationPath: "gorea_b2_Anim.bin",
                        collisionPath: "Gorea_b2_collision.bin",
                        texturePath: "gorea_b2_tex.bin",
                        entityPath: "gorea_b2_dm_Ent.bin",
                        nodePath: "gorea_b2_dm_NODE.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 100,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(16, 30, 25),
                        fogSlope: 4,
                        fogOffset: 65535,
                        light1Color: new ColorRgb(18, 16, 14),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(27, 18, 9),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFBA000,
                        field6C: 0x4,
                        multiplayer: true)
                },
                {
                    "E3 FIRST HUNT",
                    new RoomMetadata(
                        name: "E3 FIRST HUNT",
                        inGameName: "Stasis Bunker",
                        archive: "e3Level",
                        modelPath: "e3Level_Model.bin",
                        animationPath: "e3Level_Anim.bin",
                        collisionPath: "e3Level_Collision.bin",
                        texturePath: "e3level_tex.bin",
                        entityPath: "e3Level_Ent.bin",
                        nodePath: "e3Level_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(12, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 3,
                        nodeLayer: 1,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(24, 20, 31),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(24, 20, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(9, 8, 14),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 1638400,
                        field68: 0xFFFE2000,
                        field6C: 0x3,
                        multiplayer: true)
                },
                {
                    "Level TestLevel",
                    new RoomMetadata(
                        name: "Level TestLevel",
                        inGameName: "Test Level",
                        archive: @"_fh\testLevel",
                        modelPath: "testLevel_Model.bin",
                        animationPath: "testlevel_Anim.bin",
                        collisionPath: "testlevel_Collision.bin",
                        texturePath: null,
                        entityPath: "testlevel_Ent.bin",
                        nodePath: "testLevel_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 0,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(8, 16, 31),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(31, 31, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(10, 10, 31),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
                },
                {
                    "Level AbeTest",
                    new RoomMetadata(
                        name: "Level AbeTest",
                        inGameName: "Abe Test Level",
                        archive: @"_fh\testLevel",
                        modelPath: "testLevel_Model.bin",
                        animationPath: "testlevel_Anim.bin",
                        collisionPath: "testlevel_Collision.bin",
                        texturePath: null,
                        entityPath: "testLevelAbe1_Ent.bin",
                        nodePath: "testLevelAbe1_Node.bin",
                        roomNodeName: null,
                        battleTimeLimit: TimeLimit(40, 0, 0),
                        timeLimit: TimeLimit(4, 0, 0),
                        pointLimit: 50,
                        nodeLayer: 0,
                        fogEnabled: true,
                        clearFog: false,
                        fogColor: new ColorRgb(8, 16, 31),
                        fogSlope: 5,
                        fogOffset: 65152,
                        light1Color: new ColorRgb(31, 31, 31),
                        light1Vector: new Vector3(0.099365234f, -0.99487305f, 0f),
                        light2Color: new ColorRgb(10, 10, 31),
                        light2Vector: new Vector3(0f, 0.99487305f, -0.099365234f),
                        farClip: 819200,
                        field68: 0xFFFE2000,
                        field6C: 0x6)
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
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
                        false,
                        false,
                        new ColorRgb(0, 0, 0),
                        0,
                        0,
                        new ColorRgb(31, 31, 31),
                        new Vector3(1f, 0f, 0f),
                        new ColorRgb(31, 31, 31),
                        new Vector3(0f, -1f, 0f),
                        1638400,
                        0x0,
                        0x0,
                        multiplayer: true)
                },
                // todo: room ID 8 has the same files as MP1, but a few different parameters
                // First Hunt
                {
                    "FH_MP1",
                    new RoomMetadata(
                        "Level MP1",
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
                        true,
                        true,
                        new ColorRgb(31, 31, 31),
                        0x2,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        368640,
                        0x0,
                        0x0,
                        multiplayer: true,
                        firstHunt: true)
                },
                {
                    "FH_MP2",
                    new RoomMetadata(
                        "Level MP2",
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
                        true,
                        true,
                        new ColorRgb(6, 12, 11),
                        0x5,
                        64900,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        multiplayer: true,
                        firstHunt: true)
                },
                {
                    "FH_MP3",
                    new RoomMetadata(
                        "Level MP3",
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
                        true,
                        false,
                        new ColorRgb(29, 26, 20),
                        0x4,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        81920000,
                        0x0,
                        0x0,
                        multiplayer: true,
                        firstHunt: true)
                },
                {
                    "FH_MORPHBALL",
                    new RoomMetadata(
                        "Level SP Morphball",
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
                        true,
                        false,
                        new ColorRgb(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        firstHunt: true)
                },
                {
                    "FH_REGULATOR",
                    new RoomMetadata(
                        "Level SP Regulator",
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
                        false,
                        false,
                        new ColorRgb(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        firstHunt: true)
                },
                {
                    "FH_SURVIVOR",
                    new RoomMetadata(
                        "Level SP Survivor",
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
                        true,
                        true,
                        new ColorRgb(1, 6, 5),
                        0x5,
                        64900,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        firstHunt: true)
                },
                {
                    "FH_TEST",
                    new RoomMetadata(
                        "Level TestLevel",
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
                        true,
                        false,
                        new ColorRgb(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        firstHunt: true)
                },
                {
                    "FH_MP5",
                    new RoomMetadata(
                        "Level MP5",
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
                        true,
                        false,
                        new ColorRgb(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        multiplayer: true,
                        firstHunt: true)
                },
                {
                    "FH_E3",
                    new RoomMetadata(
                        "E3 level",
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
                        true,
                        false,
                        new ColorRgb(8, 16, 31),
                        0x5,
                        65152,
                        new ColorRgb(31, 31, 31),
                        new Vector3(0.25f, -0.5f, -0.25f),
                        new ColorRgb(4, 4, 16),
                        new Vector3(0, 1, -0.25f),
                        245760,
                        0x0,
                        0x0,
                        multiplayer: true,
                        firstHunt: true)
                }
            };

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
                    "arcWelder",
                    new ModelMetadata("arcWelder", animation: false, archive: "common")
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
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact02",
                    new ModelMetadata("Artifact02",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact03",
                    new ModelMetadata("Artifact03",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact04",
                    new ModelMetadata("Artifact04",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact05",
                    new ModelMetadata("Artifact05",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact06",
                    new ModelMetadata("Artifact06",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact07",
                    new ModelMetadata("Artifact07",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact08",
                    new ModelMetadata("Artifact08",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animationPath: @"models\Artifact_Anim.bin",
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
                        mdlSuffix: MdlSuffix.Model,
                        animationPath: @"models\warWasp_Anim.bin")
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
                    new ModelMetadata("BigEyeSynapse_01", animation: true, animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_02",
                    new ModelMetadata("BigEyeSynapse_02", animation: true, animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_03",
                    new ModelMetadata("BigEyeSynapse_03", animation: true, animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_04",
                    new ModelMetadata("BigEyeSynapse_04", animation: true, animationPath: @"models\BigEyeSynapse_Anim.bin")
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
                    "electroTrail",
                    new ModelMetadata("electroTrail", animation: false, archive: "common")
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
                // todo: confirm all texture shares with load_object
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
                {
                    "ForceFieldLock",
                    new ModelMetadata("ForceFieldLock",
                        modelPath: @"models\ForceFieldLock_mdl_Model.bin",
                        animationPath: @"models\ForceFieldLock_mdl_Anim.bin",
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("pal_01",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separatePaletteHeader: true,
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 7, new List<int> { 3 } } })
                        })
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
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
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
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
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
                // effectsBase
                {
                    "deathParticle",
                    new ModelMetadata("deathParticle", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "geo1",
                    new ModelMetadata("geo1", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "particles",
                    new ModelMetadata("particles", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "particles2",
                    new ModelMetadata("particles2", animation: false, texture: true, archive: "effectsBase")
                },
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
