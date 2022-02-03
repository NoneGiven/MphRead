using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead
{
    public enum MdlSuffix
    {
        None,
        All,
        Model
    }

    public enum MetaDir
    {
        Models,
        Hud,
        Stage,
        MainMenu,
        Logo,
        CharSelect,
        CreateJoin,
        GameOption,
        GamersCard,
        Keyboard,
        Keypad,
        MoviePlayer,
        MultiMaster,
        Multiplayer,
        PaxControls,
        Popup,
        Results,
        ScStartGame,
        StartGame,
        ToStart,
        TouchToStart,
        TouchToStart2,
        WifiCreate,
        WifiGames
    }

    public class ModelMetadata
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string? AnimationPath { get; }
        public string? AnimationShare { get; }
        public string? CollisionPath { get; }
        public string? ExtraCollisionPath { get; }
        public IReadOnlyList<RecolorMetadata> Recolors { get; }
        public bool UseLightSources { get; }
        public bool FirstHunt { get; }

        public ModelMetadata(string name, string modelPath, string? animationPath, string? collisionPath,
            IReadOnlyList<RecolorMetadata> recolors, string? animationShare = null, bool useLightSources = false)
        {
            Name = name;
            ModelPath = modelPath;
            AnimationPath = animationPath;
            CollisionPath = collisionPath;
            Recolors = recolors;
            UseLightSources = useLightSources;
            if (animationShare != null)
            {
                AnimationShare = animationShare;
            }
        }

        private readonly IReadOnlyDictionary<MetaDir, string> _dirs = new Dictionary<MetaDir, string>()
        {
            [MetaDir.CharSelect] = "characterselect",
            [MetaDir.CreateJoin] = "createjoin",
            [MetaDir.GameOption] = "gameoptions",
            [MetaDir.GamersCard] = "gamerscard",
            [MetaDir.Hud] = "hud",
            [MetaDir.Keyboard] = "keyboard",
            [MetaDir.Keypad] = "keypad",
            [MetaDir.Logo] = @"logo_screen\MAYA",
            [MetaDir.MainMenu] = "main menu",
            [MetaDir.Models] = "models",
            [MetaDir.MoviePlayer] = "movieplayer",
            [MetaDir.MultiMaster] = "multimaster",
            [MetaDir.Multiplayer] = "multiplayer",
            [MetaDir.PaxControls] = "pax_controls",
            [MetaDir.Popup] = "popup",
            [MetaDir.Results] = "results",
            [MetaDir.ScStartGame] = "sc_startgame",
            [MetaDir.Stage] = "stage",
            [MetaDir.StartGame] = "startgame",
            [MetaDir.ToStart] = "tostart",
            [MetaDir.TouchToStart] = "touchtostart",
            [MetaDir.TouchToStart2] = "touchtostart_2",
            [MetaDir.WifiCreate] = "wifi_createjoin",
            [MetaDir.WifiGames] = "wifi_games",
        };

        public ModelMetadata(string name, MetaDir dir, string? anim = null)
        {
            Name = name;
            string directory = _dirs[dir];
            ModelPath = $@"{directory}\{name}_Model.bin";
            AnimationPath = anim != null ? $@"{directory}\{anim}_Anim.bin" : null;
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", ModelPath, ModelPath)
            };
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
            string directory = "models";
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
            FirstHunt = firstHunt;
        }

        public ModelMetadata(string name, IEnumerable<string> recolors, string? remove = null, bool animation = false,
            string? animationPath = null, bool texture = false, MdlSuffix mdlSuffix = MdlSuffix.None, string? archive = null,
            string? recolorName = null, string? animationShare = null, bool useLightSources = false, bool firstHunt = false,
            bool noUnderscore = false)
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
            if (animationShare != null)
            {
                AnimationShare = animationShare;
            }
            var recolorList = new List<RecolorMetadata>();
            foreach (string recolor in recolors)
            {
                string recolorString = $"{recolorName ?? name}{(noUnderscore ? "" : "_")}{recolor}";
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
            FirstHunt = firstHunt;
        }

        public ModelMetadata(string name, bool animation = true, bool collision = false, bool texture = false,
            string? share = null, MdlSuffix mdlSuffix = MdlSuffix.None, string? archive = null, string? addToAnim = null,
            bool firstHunt = false, string? animationPath = null, string? extraCollision = null)
        {
            Name = name;
            string path;
            if (archive != null)
            {
                path = $@"_archives\{archive}";
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
            if (extraCollision != null)
            {
                ExtraCollisionPath = $@"{path}\{extraCollision}_Collision.bin";
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
            FirstHunt = firstHunt;
        }
    }

    public class RecolorMetadata
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string TexturePath { get; }
        public string PalettePath { get; }
        public string? ReplacePath { get; }
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
            Dictionary<int, IEnumerable<int>>? replaceIds = null, bool separateReplace = false)
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
            if (replaceIds != null)
            {
                foreach (KeyValuePair<int, IEnumerable<int>> kvp in replaceIds)
                {
                    _replaceIds.Add(kvp.Key, kvp.Value);
                }
            }
        }
    }

    public class ObjectMetadata
    {
        public bool Lighting { get; }
        public string Name { get; }
        public IReadOnlyList<int> AnimationIds { get; }
        public int RecolorId { get; }

        public ObjectMetadata(string name, bool lighting = false, int paletteId = 0, List<int>? animationIds = null)
        {
            Name = name;
            Lighting = lighting;
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

    public enum PlatAnimId
    {
        InstantSleep = 0,
        Wake = 1,
        InstantWake = 2,
        Sleep = 3
    }

    public class PlatformMetadata
    {
        public bool Lighting { get; }
        public string Name { get; }
        public IReadOnlyList<int> AnimationIds { get; }
        public int Unused20 { get; }
        public int Unused24 { get; }

        public PlatformMetadata(string name, bool lighting = false, List<int>? animationIds = null, int unused20 = -1, int unused24 = -1)
        {
            Name = name;
            Lighting = lighting;
            Unused20 = unused20;
            Unused24 = unused24;
            // instant_sleep_anim_id, wakeup_anim_id, instant_wakeup_anim_id, sleep_anim_id
            if (animationIds == null)
            {
                AnimationIds = new List<int>() { -1, -1, -1, -1 };
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
        public float Radius { get; }

        public DoorMetadata(string name, string lockName, float lockOffset, float radius)
        {
            Name = name;
            LockName = lockName;
            LockOffset = lockOffset;
            Radius = radius;
        }
    }

    public static partial class Metadata
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

        public static string GetLayerName(int layerId, bool multiplayer)
        {
            if (!multiplayer)
            {
                if (layerId == 0)
                {
                    return "FirstVisit";
                }
                if (layerId == 1)
                {
                    return "BossDefeated";
                }
                if (layerId == 2)
                {
                    return "SpLayer2";
                }
                if (layerId == 3)
                {
                    return "SpLayer3";
                }
                return $"NoLayer{layerId}";
            }
            return GetLayerNames(1 << layerId, multiplayer);
        }

        public static string GetLayerNames(int layerMask, bool multiplayer)
        {
            if (multiplayer)
            {
                var names = new List<string>();
                IEnumerable<int> layers = Enumerable.Range(0, 16).Where(i => (layerMask & (1 << i)) != 0);
                foreach (KeyValuePair<GameMode, IReadOnlyList<int>> kvp in _modeLayers)
                {
                    if (kvp.Value.All(v => layers.Contains(v)))
                    {
                        names.Add(kvp.Key.ToString());
                    }
                    else if (kvp.Value.Count > 1)
                    {
                        var players = new List<string>(0);
                        if (layers.Contains(kvp.Value[0]))
                        {
                            players.Add("2P");
                        }
                        if (layers.Contains(kvp.Value[1]))
                        {
                            players.Add("3P");
                        }
                        if (layers.Contains(kvp.Value[2]))
                        {
                            players.Add("4P");
                        }
                        if (players.Count > 0)
                        {
                            names.Add($"{kvp.Key}{String.Join('/', players)}");
                        }
                    }
                }
                return String.Join(" | ", names);
            }
            int masked = layerMask & 3;
            if (masked == 0)
            {
                return "FirstVisit";
            }
            if (masked == 1)
            {
                return "BossDefeated";
            }
            if (masked == 2)
            {
                return "SpLayer2";
            }
            if (masked == 3)
            {
                return "SpLayer3";
            }
            return $"UNKNOWN{layerMask}";
        }

        public static readonly Vector3 EmissionOrange = GetColor(0x14F0);
        public static readonly Vector3 EmissionGreen = GetColor(0x1565);
        public static readonly Vector3 EmissionGray = GetColor(0x35AD);
        public static readonly ColorRgb[] TeamColors = new ColorRgb[2]
        {
            new ColorRgb(31, 19, 0), // orange
            new ColorRgb(0, 31, 0)   // green
        };

        public static readonly Vector3 OctolithLight1Vector = new Vector3(0, 0.3005371f, -0.5f);
        public static readonly Vector3 OctolithLight2Vector = new Vector3(0, 0, -0.5f);
        public static readonly Vector3 OctolithLightColor = new Vector3(1, 1, 1);

        // this is only set/used by Octolith
        public static readonly IReadOnlyList<Vector3> ToonTable = new List<Vector3>()
        {
            GetColor(0x2000),
            GetColor(0x2000),
            GetColor(0x2020),
            GetColor(0x2021),
            GetColor(0x2021),
            GetColor(0x2041),
            GetColor(0x2441),
            GetColor(0x2461),
            GetColor(0x2461),
            GetColor(0x2462),
            GetColor(0x2482),
            GetColor(0x2482),
            GetColor(0x28C3),
            GetColor(0x2CE4),
            GetColor(0x3105),
            GetColor(0x3546),
            GetColor(0x3967),
            GetColor(0x3D88),
            GetColor(0x41C9),
            GetColor(0x45EA),
            GetColor(0x4A0B),
            GetColor(0x4E4B),
            GetColor(0x526C),
            GetColor(0x568D),
            GetColor(0x5ACE),
            GetColor(0x5EEF),
            GetColor(0x6310),
            GetColor(0x6751),
            GetColor(0x6B72),
            GetColor(0x6F93),
            GetColor(0x73D4),
            GetColor(0x77F5)
        };

        // todo: consolidate stuff like this
        private static Vector3 GetColor(ushort value)
        {
            int r = (value >> 0) & 0x1F;
            int g = (value >> 5) & 0x1F;
            int b = (value >> 10) & 0x1F;
            return new Vector3(r / 31.0f, g / 31.0f, b / 31.0f);
        }

        public static readonly IReadOnlyDictionary<string, IReadOnlyList<PaletteData>> PowerPalettes
            = new Dictionary<string, IReadOnlyList<PaletteData>>()
            {
                ["Alimbic_Power"] = new List<PaletteData>()
                {
                    new PaletteData(32576),
                    new PaletteData(32576),
                    new PaletteData(32608),
                    new PaletteData(32640),
                    new PaletteData(32711),
                    new PaletteData(32719),
                    new PaletteData(32758),
                    new PaletteData(32733)
                },
                ["Generic_Power"] = new List<PaletteData>()
                {
                    new PaletteData(19393),
                    new PaletteData(18369),
                    new PaletteData(17345),
                    new PaletteData(16321),
                    new PaletteData(19400),
                    new PaletteData(23535),
                    new PaletteData(26614),
                    new PaletteData(31741)
                },
                ["Ice_Power"] = new List<PaletteData>()
                {
                    new PaletteData(29453),
                    new PaletteData(29453),
                    new PaletteData(29485),
                    new PaletteData(29517),
                    new PaletteData(30578),
                    new PaletteData(30614),
                    new PaletteData(31705),
                    new PaletteData(32734)
                },
                ["Lava_Power"] = new List<PaletteData>()
                {
                    new PaletteData(671),
                    new PaletteData(639),
                    new PaletteData(607),
                    new PaletteData(575),
                    new PaletteData(7807),
                    new PaletteData(16127),
                    new PaletteData(23391),
                    new PaletteData(30719)
                }
            };

        public static readonly IReadOnlyDictionary<Hunter, float> HunterScales = new Dictionary<Hunter, float>
        {
            { Hunter.Samus, 1.0f },
            { Hunter.Kanden, Fixed.ToFloat(0x10F5) },
            { Hunter.Trace, 1.0f },
            { Hunter.Sylux, 1.0f },
            { Hunter.Noxus, 1.0f },
            { Hunter.Spire, Fixed.ToFloat(0x123D) },
            { Hunter.Weavel, 1.0f },
            { Hunter.Guardian, 1.0f }
        };

        public static readonly IReadOnlyDictionary<Hunter, IReadOnlyList<string>> HunterModels = new Dictionary<Hunter, IReadOnlyList<string>>
        {
            {
                Hunter.Samus,
                new List<string>() { "Samus_lod0", "Samus_lod1", "SamusAlt_lod0", "SamusGun" }
            },
            {
                Hunter.Kanden,
                new List<string>() { "Kanden_lod0", "Kanden_lod1", "KandenAlt_lod0", "KandenGun" }
            },
            {
                Hunter.Trace,
                new List<string>() { "Trace_lod0", "Trace_lod1", "TraceAlt_lod0", "TraceGun" }
            },
            {
                Hunter.Sylux,
                new List<string>() { "Sylux_lod0", "Sylux_lod1", "SyluxAlt_lod0", "SyluxGun" }
            },
            {
                Hunter.Noxus,
                new List<string>() { "Nox_lod0", "Nox_lod1", "NoxAlt_lod0", "NoxGun" }
            },
            {
                Hunter.Spire,
                new List<string>() { "Spire_lod0", "Spire_lod1", "SpireAlt_lod0", "SpireGun" }
            },
            {
                Hunter.Weavel,
                new List<string>() { "Weavel_lod0", "Weavel_lod1", "WeavelAlt_lod0", "WeavelGun" }
            },
            {
                Hunter.Guardian,
                new List<string>() { "Guardian_lod0", "Guardian_lod1", "SamusAlt_lod0", "SamusGun" }
            }
        };

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

        public static readonly IReadOnlyList<float> DamageLevels = new float[3]
        {
            0.75f, 1, 1.25f
        };

        public static ModelMetadata? GetModelByName(string name, MetaDir dir = MetaDir.Models)
        {
            if (name == "doubleDamage_img")
            {
                return DoubleDamageImg;
            }
            if (name == "ad2_dm2")
            {
                return Ad2Dm2;
            }
            if (dir == MetaDir.Logo)
            {
                if (LogoModels.TryGetValue(name, out ModelMetadata? metadata))
                {
                    return metadata;
                }
            }
            else if (dir == MetaDir.Multiplayer)
            {
                if (MultiplayerModels.TryGetValue(name, out ModelMetadata? metadata))
                {
                    return metadata;
                }
            }
            else if (dir == MetaDir.TouchToStart)
            {
                if (TouchToStartModels.TryGetValue(name, out ModelMetadata? metadata))
                {
                    return metadata;
                }
            }
            else if (dir == MetaDir.Hud)
            {
                if (HudModels.TryGetValue(name, out ModelMetadata? metadata))
                {
                    return metadata;
                }
            }
            else if (dir != MetaDir.Models)
            {
                if (FrontendModels.TryGetValue(name, out ModelMetadata? metadata))
                {
                    return metadata;
                }
            }
            else if (ModelMetadata.TryGetValue(name, out ModelMetadata? metadata))
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
            /* 0 */ new DoorMetadata("AlimbicDoor", "AlimbicDoorLock", 1.4f, 2.4f),
            /* 1 */ new DoorMetadata("AlimbicMorphBallDoor", "AlimbicMorphBallDoorLock", 0.7f, 1.0f),
            /* 2 */ new DoorMetadata("AlimbicBossDoor", "AlimbicBossDoorLock", 3.5f, 3.5f),
            /* 3 */ new DoorMetadata("AlimbicThinDoor", "ThinDoorLock", 1.4f, 2.0f)
        };

        public static readonly IReadOnlyList<string> FhDoors = new List<string>()
        {
            /* 0 */ "door",
            /* 1 */ "door2",
            /* 2 */ "door2_holo"
        };

        // 0-7 are for beam doors, 8 is an unused "bomb door", 9 is for regular doors
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
            /*  0 */ new ObjectMetadata("AlimbicGhost_01"),
            /*  1 */ new ObjectMetadata("AlimbicLightPole"),
            /*  2 */ new ObjectMetadata("AlimbicStationShieldControl"),
            /*  3 */ new ObjectMetadata("AlimbicComputerStationControl"),
            /*  4 */ new ObjectMetadata("AlimbicEnergySensor"),
            /*  5 */ new ObjectMetadata("SamusShip"), // unused
            /*  6 */ new ObjectMetadata("Guardbot01_Dead"),
            /*  7 */ new ObjectMetadata("Guardbot02_Dead"),
            /*  8 */ new ObjectMetadata("Guardian_Dead"),
            /*  9 */ new ObjectMetadata("Psychobit_Dead"),
            /* 10 */ new ObjectMetadata("AlimbicLightPole02"),
            /* 11 */ new ObjectMetadata("AlimbicComputerStationControl02"),
            /* 12 */ new ObjectMetadata("Generic_Console", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 13 */ new ObjectMetadata("Generic_Monitor", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 14 */ new ObjectMetadata("Generic_Power"),
            /* 15 */ new ObjectMetadata("Generic_Scanner", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 16 */ new ObjectMetadata("Generic_Switch", lighting: true, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 17 */ new ObjectMetadata("Alimbic_Console", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 18 */ new ObjectMetadata("Alimbic_Monitor", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 19 */ new ObjectMetadata("Alimbic_Power"),
            /* 20 */ new ObjectMetadata("Alimbic_Scanner", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 21 */ new ObjectMetadata("Alimbic_Switch", lighting: true, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 22 */ new ObjectMetadata("Lava_Console", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 23 */ new ObjectMetadata("Lava_Monitor", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 24 */ new ObjectMetadata("Lava_Power"),
            /* 25 */ new ObjectMetadata("Lava_Scanner",animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 26 */ new ObjectMetadata("Lava_Switch", lighting: true, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 27 */ new ObjectMetadata("Ice_Console", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 28 */ new ObjectMetadata("Ice_Monitor", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 29 */ new ObjectMetadata("Ice_Power"),
            /* 30 */ new ObjectMetadata("Ice_Scanner", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 31 */ new ObjectMetadata("Ice_Switch", lighting: true, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 32 */ new ObjectMetadata("Ruins_Console", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 33 */ new ObjectMetadata("Ruins_Monitor", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 34 */ new ObjectMetadata("Ruins_Power"),
            /* 35 */ new ObjectMetadata("Ruins_Scanner", animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 36 */ new ObjectMetadata("Ruins_Switch", lighting: true, animationIds: new List<int>() { 2, 1, 0, 0 }),
            /* 37 */ new ObjectMetadata("PlantCarnivarous_Branched"),
            /* 38 */ new ObjectMetadata("PlantCarnivarous_Pod"),
            /* 39 */ new ObjectMetadata("PlantCarnivarous_PodLeaves"),
            /* 40 */ new ObjectMetadata("PlantCarnivarous_Vine"),
            /* 41 */ new ObjectMetadata("GhostSwitch"),
            /* 42 */ new ObjectMetadata("Switch", lighting: true),
            /* 43 */ new ObjectMetadata("Guardian_Stasis", animationIds: new List<int>() { -1, 0, 0, 0 }),
            /* 44 */ new ObjectMetadata("AlimbicStatue_lod0", animationIds: new List<int>() { -1, 0, 0, 0 }),
            /* 45 */ new ObjectMetadata("AlimbicCapsule"),
            /* 46 */ new ObjectMetadata("SniperTarget", lighting: true, animationIds: new List<int>() { 0, 2, 1, 0 }),
            /* 47 */ new ObjectMetadata("SecretSwitch", paletteId: 1, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 48 */ new ObjectMetadata("SecretSwitch", paletteId: 2, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 49 */ new ObjectMetadata("SecretSwitch", paletteId: 3, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 50 */ new ObjectMetadata("SecretSwitch", paletteId: 4, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 51 */ new ObjectMetadata("SecretSwitch", paletteId: 5, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 52 */ new ObjectMetadata("SecretSwitch", paletteId: 6, animationIds: new List<int>() { 1, 2, 0, 0 }),
            /* 53 */ new ObjectMetadata("WallSwitch", lighting: true, animationIds: new List<int>() { 2, 0, 1, 0 })
        };

        public static ObjectMetadata GetObjectById(int id)
        {
            if (id < 0 || id > _objects.Count)
            {
                throw new ArgumentException(nameof(id));
            }
            return _objects[id];
        }

        public static ObjectMetadata GetObjectById(uint id)
        {
            return GetObjectById((int)id);
        }

        public static IReadOnlyList<Vector3> ObjectVisPosOffsets = new Vector3[54]
        {
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(-0.05f, 1.5f, -0.4f),
            new Vector3(-0.05f, 1.5f, -0.4f),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(-0.05f, 1.5f, 0),
            new Vector3(0.01f, 1.5f, 0),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(-0.03f, 1.5f, -0.3f),
            new Vector3(0, 1.5f, -0.3f),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(0, 1.5f, -0.2f),
            new Vector3(0, 1.5f, -0.2f),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(0, 1.4f, 0),
            new Vector3(0, 1.4f, 0),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(0, 1.75f, 0),
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero
        };

        public static readonly PlatformMetadata InvisiblePlat = new PlatformMetadata("N/A");

        private static readonly IReadOnlyList<PlatformMetadata?> _platforms = new List<PlatformMetadata?>()
        {
            /*  0 */ new PlatformMetadata("platform"),
            /*  1 */ null, // duplicate of 0
            /*  2 */ null, // no model
            /*  3 */ new PlatformMetadata("Elevator"),
            /*  4 */ new PlatformMetadata("smasher"),
            /*  5 */ new PlatformMetadata("Platform_Unit4_C1", lighting: true),
            /*  6 */ new PlatformMetadata("pillar"),
            /*  7 */ new PlatformMetadata("Door_Unit4_RM1"),
            /*  8 */ new PlatformMetadata("SyluxShip", animationIds: new List<int>() { -1, 1, 0, 2 }),
            /*  9 */ new PlatformMetadata("pistonmp7"),
            /* 10 */ new PlatformMetadata("unit3_brain", animationIds: new List<int>() { 0, 0, 0, 0 }),
            /* 11 */ new PlatformMetadata("unit4_mover1", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 12 */ new PlatformMetadata("unit4_mover2", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 13 */ new PlatformMetadata("ElectroField1", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 14 */ new PlatformMetadata("Unit3_platform1"),
            /* 15 */ new PlatformMetadata("unit3_pipe1", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 16 */ new PlatformMetadata("unit3_pipe2", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 17 */ new PlatformMetadata("cylinderbase"),
            /* 18 */ new PlatformMetadata("unit3_platform"),
            /* 19 */ new PlatformMetadata("unit3_platform2"),
            /* 20 */ new PlatformMetadata("unit3_jar", animationIds: new List<int>() { 0, 2, 1, 0 }, unused20: 0, unused24: 0),
            /* 21 */ new PlatformMetadata("SyluxTurret", animationIds: new List<int>() { 3, 2, 1, 0 }, unused20: 0, unused24: 0),
            /* 22 */ new PlatformMetadata("unit3_jartop", animationIds: new List<int>() { 0, 2, 1, 0 }, unused20: 0, unused24: 0),
            /* 23 */ new PlatformMetadata("SamusShip", animationIds: new List<int>() { 1, 3, 2, 4 }, unused20: 0, unused24: 0),
            /* 24 */ new PlatformMetadata("unit1_land_plat1"),
            /* 25 */ new PlatformMetadata("unit1_land_plat2"),
            /* 26 */ new PlatformMetadata("unit1_land_plat3"),
            /* 27 */ new PlatformMetadata("unit1_land_plat4"),
            /* 28 */ new PlatformMetadata("unit1_land_plat5"),
            /* 29 */ new PlatformMetadata("unit2_c4_plat"),
            /* 30 */ new PlatformMetadata("unit2_land_elev"),
            /* 31 */ new PlatformMetadata("unit4_platform1"),
            /* 32 */ new PlatformMetadata("Crate01", animationIds: new List<int>() { -1, -1, 0, 1 }),
            /* 33 */ new PlatformMetadata("unit1_mover1", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 34 */ new PlatformMetadata("unit1_mover2"),
            /* 35 */ new PlatformMetadata("unit2_mover1"),
            /* 36 */ new PlatformMetadata("unit4_mover3"),
            /* 37 */ new PlatformMetadata("unit4_mover4"),
            /* 38 */ new PlatformMetadata("unit3_mover1"),
            /* 39 */ new PlatformMetadata("unit2_c1_mover"),
            /* 40 */ new PlatformMetadata("unit3_mover2", animationIds: new List<int>() { 0, 0, 0, 0 }, unused20: 0, unused24: 0),
            /* 41 */ new PlatformMetadata("piston_gorealand"),
            /* 42 */ new PlatformMetadata("unit4_tp2_artifact_wo"),
            /* 43 */ new PlatformMetadata("unit4_tp1_artifact_wo"),
            // this version is used in Gorea_Land
            /* 44 */ new PlatformMetadata("SamusShip", animationIds: new List<int>() { 1, 0, 2, 4 }, unused20: 0, unused24: 0)
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

        public static readonly IReadOnlyList<string> WeaponNames = new List<string>()
        {
            "Power Beam", "Volt Driver", "Missiles", "Battlehammer", "Imperialist", "Judicator", "Magmaul", "Shock Coil", "Omega Cannon",
            "Platform", "Enemy"
        };

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
            if (eventId == Message.Unused25) // pale orange
            {
                return new Vector3(1f, 0.792f, 0.6f);
            }
            if (eventId == Message.PreventFormSwitch) // yellow
            {
                return new Vector3(0.964f, 1f, 0.058f);
            }
            if (eventId == Message.PlatformWakeup) // gray
            {
                return new Vector3(0.5f, 0.5f, 0.5f);
            }
            if (eventId == Message.DripMoatPlatform) // periwinkle
            {
                return new Vector3(0.596f, 0.658f, 0.964f);
            }
            if (eventId == Message.UnlockOubliette) // salmon
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
            if (eventId == Message.Trigger) // dark red
            {
                return new Vector3(0.549f, 0.18f, 0.18f);
            }
            if (eventId == Message.UpdateMusic) // dark teal
            {
                return new Vector3(0.094f, 0.506f, 0.51f);
            }
            if (eventId == Message.Unlock) // navy blue
            {
                return new Vector3(0.094f, 0.094f, 0.557f);
            }
            if (eventId == Message.Lock) // olive
            {
                return new Vector3(0.647f, 0.663f, 0.169f);
            }
            if (eventId == Message.ShowPrompt) // dark green
            {
                return new Vector3(0.118f, 0.588f, 0.118f);
            }
            if (eventId == Message.ShowWarning) // light purple
            {
                return new Vector3(0.784f, 0.325f, 1f);
            }
            if (eventId == Message.ShowOverlay) // orange
            {
                return new Vector3(1f, 0.612f, 0.153f);
            }
            if (eventId == Message.UnlockConnectors) // lavender
            {
                return new Vector3(0.906f, 0.702f, 1f);
            }
            if (eventId == Message.LockConnectors) // pale blue
            {
                return new Vector3(0.784f, 0.984f, 0.988f);
            }
            if (eventId == Message.Unknown36) // light red
            {
                return new Vector3(1f, 0.325f, 0.294f);
            }
            if (eventId == Message.SetTriggerState) // pink
            {
                return new Vector3(0.988f, 0.463f, 0.824f);
            }
            if (eventId == Message.PlatformSleep) // sea green
            {
                return new Vector3(0.165f, 0.894f, 0.678f);
            }
            if (eventId == Message.SetPlatformIndex) // brown
            {
                return new Vector3(0.549f, 0.345f, 0.102f);
            }
            if (eventId == Message.PlaySfxScript) // pale green
            {
                return new Vector3(0.471f, 0.769f, 0.525f);
            }
            if (eventId == Message.LoadOubliette) // light orange
            {
                return new Vector3(1f, 0.765f, 0.49f);
            }
            if (eventId == Message.EscapeCancel) // sky blue
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
            if (eventId == FhMessage.Activate) // green
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
        public static readonly IReadOnlyList<(string Name, string? Archive)> Effects = new List<(string, string?)>()
        {
            /*   0 */ ("", null), // no effect
            /*   1 */ ("powerBeam", "effects"),
            /*   2 */ ("powerBeamNoSplat", "effects"),
            /*   3 */ ("blastCapHit", null),
            /*   4 */ ("blastCapBlow", null),
            /*   5 */ ("missile1", "effects"),
            /*   6 */ ("mortar1", "effects"),
            /*   7 */ ("shotGunCol", "effects"),
            /*   8 */ ("shotGunShrapnel", "effects"),
            /*   9 */ ("bombStart", null),
            /*  10 */ ("ballDeath", null),
            /*  11 */ ("jackHammerCol", "effects"),
            /*  12 */ ("effectiveHitPB", null),
            /*  13 */ ("effectiveHitElectric", null),
            /*  14 */ ("effectiveHitMsl", null),
            /*  15 */ ("effectiveHitJack", null),
            /*  16 */ ("effectiveHitSniper", null),
            /*  17 */ ("effectiveHitIce", null),
            /*  18 */ ("effectiveHitMortar", null),
            /*  19 */ ("effectiveHitGhost", null),
            /*  20 */ ("sprEffectivePB", null),
            /*  21 */ ("sprEffectiveElectric", null),
            /*  22 */ ("sprEffectiveMsl", null),
            /*  23 */ ("sprEffectiveJack", null),
            /*  24 */ ("sprEffectiveSniper", null),
            /*  25 */ ("sprEffectiveIce", null),
            /*  26 */ ("sprEffectiveMortar", null),
            /*  27 */ ("sprEffectiveGhost", null),
            /*  28 */ ("sniperCol", "effects"),
            /*  29 */ ("shriekBatTrail", null),
            /*  30 */ ("samusFurl", null),
            /*  31 */ ("spawnEffect", null),
            /*  32 */ ("test", null),
            /*  33 */ ("spawnEffectMP", null),
            /*  34 */ ("burstFlame", null),
            /*  35 */ ("gunSmoke", null),
            /*  36 */ ("jetFlame", null),
            /*  37 */ ("spireAltSlam", null),
            /*  38 */ ("steamBurst", null),
            /*  39 */ ("steamSamusShip", null),
            /*  40 */ ("steamDoorway", null),
            /*  41 */ ("goreaArmChargeUp", null),
            /*  42 */ ("goreaBallExplode", null),
            /*  43 */ ("goreaShoulderDamageLoop", null),
            /*  44 */ ("goreaShoulderHits", null),
            /*  45 */ ("goreaShoulderKill", null),
            /*  46 */ ("goreaChargeElc", null),
            /*  47 */ ("goreaChargeIce", null),
            /*  48 */ ("goreaChargeJak", null),
            /*  49 */ ("goreaChargeMrt", null),
            /*  50 */ ("goreaChargeSnp", null),
            /*  51 */ ("goreaFireElc", null),
            /*  52 */ ("goreaFireGst", null),
            /*  53 */ ("goreaFireIce", null),
            /*  54 */ ("goreaFireJak", null),
            /*  55 */ ("goreaFireMrt", null),
            /*  56 */ ("goreaFireSnp", null),
            /*  57 */ ("muzzleElc", null),
            /*  58 */ ("muzzleGst", null),
            /*  59 */ ("muzzleIce", null),
            /*  60 */ ("muzzleJak", null),
            /*  61 */ ("muzzleMrt", null),
            /*  62 */ ("muzzlePB", null),
            /*  63 */ ("muzzleSnp", null),
            /*  64 */ ("tear", null),
            /*  65 */ ("cylCrystalCharge", null),
            /*  66 */ ("cylCrystalKill", null),
            /*  67 */ ("cylCrystalShot", null),
            /*  68 */ ("tearSplat", null),
            /*  69 */ ("eyeShieldCharge", null),
            /*  70 */ ("eyeShieldHit", null),
            /*  71 */ ("goreaSlam", null),
            /*  72 */ ("goreaBallExplode2", null),
            /*  73 */ ("cylCrystalKill2", null),
            /*  74 */ ("cylCrystalKill3", null),
            /*  75 */ ("goreaCrystalExplode", null),
            /*  76 */ ("deathBio1", null),
            /*  77 */ ("deathMech1", null),
            /*  78 */ ("iceWave", null),
            /*  79 */ ("goreaMeteor", null),
            /*  80 */ ("goreaTeleport", null),
            /*  81 */ ("tearChargeUp", null),
            /*  82 */ ("eyeShield", null),
            /*  83 */ ("eyeShieldDefeat", null),
            /*  84 */ ("grateSparks", null),
            /*  85 */ ("electroCharge", null),
            /*  86 */ ("electroHit", null),
            /*  87 */ ("torch", null),
            /*  88 */ ("jetFlameBlue", null),
            /*  89 */ ("lavaBurstLarge", null),
            /*  90 */ ("lavaBurstSmall", null),
            /*  91 */ ("ember", null),
            /*  92 */ ("powerBeamCharge", null),
            /*  93 */ ("lavaDemonDive", null),
            /*  94 */ ("lavaDemonHurl", null),
            /*  95 */ ("lavaDemonRise", null),
            /*  96 */ ("iceDemonHurl", null),
            /*  97 */ ("lavaBurstExtraLarge", null),
            /*  98 */ ("powerBeamChargeNoSplat", null),
            /*  99 */ ("powerBeamHolo", null),
            /* 100 */ ("powerBeamLava", null),
            /* 101 */ ("hangingDrip", null),
            /* 102 */ ("hangingSpit", null),
            /* 103 */ ("hangingSplash", null),
            /* 104 */ ("goreaEyeFlash", null),
            /* 105 */ ("smokeBurst", null),
            /* 106 */ ("sparks", null),
            /* 107 */ ("sparksFall", null), // no such file?
            /* 108 */ ("shriekBatCol", null),
            /* 109 */ ("eyeTurretCharge", null),
            /* 110 */ ("lavaDemonSplat", null),
            /* 111 */ ("tearDrips", null),
            /* 112 */ ("syluxShipExhaust", null),
            /* 113 */ ("bombStartSylux", null),
            /* 114 */ ("lockDefeat", null),
            /* 115 */ ("ineffectivePsycho", null),
            /* 116 */ ("cylCrystalProjectile", null),
            /* 117 */ ("cylWeakSpotShot", null),
            /* 118 */ ("eyeLaser", null),
            /* 119 */ ("bombStartMP", null),
            /* 120 */ ("enemyMslCol", null),
            /* 121 */ ("powerBeamHoloBG", null),
            /* 122 */ ("powerBeamHoloB", null),
            /* 123 */ ("powerBeamIce", null),
            /* 124 */ ("powerBeamRock", null),
            /* 125 */ ("powerBeamSand", null),
            /* 126 */ ("powerBeamSnow", null),
            /* 127 */ ("bubblesRising", null),
            /* 128 */ ("bombKanden", null),
            /* 129 */ ("collapsingStreaks", null),
            /* 130 */ ("fireProjectile", null),
            /* 131 */ ("iceDemonSplat", null),
            /* 132 */ ("iceDemonRise", null),
            /* 133 */ ("iceDemonDive", null),
            /* 134 */ ("hammerProjectile", null),
            /* 135 */ ("synapseKill", null),
            /* 136 */ ("samusDash", null),
            /* 137 */ ("electroProjectile", null),
            /* 138 */ ("cylHomingProjectile", null),
            /* 139 */ ("cylHomingKill", null),
            /* 140 */ ("energyRippleB", null),
            /* 141 */ ("energyRippleBG", null),
            /* 142 */ ("energyRippleO", null),
            /* 143 */ ("columnCrash", null),
            /* 144 */ ("artifactKeyEffect", null),
            /* 145 */ ("bombBlue", null),
            /* 146 */ ("bombSylux", null),
            /* 147 */ ("columnBreak", null),
            /* 148 */ ("grappleEnd", null),
            /* 149 */ ("bombStartSyluxG", null),
            /* 150 */ ("bombStartSyluxO", null),
            /* 151 */ ("bombStartSyluxP", null),
            /* 152 */ ("bombStartSyluxR", null),
            /* 153 */ ("bombStartSyluxW", null),
            /* 154 */ ("mpEffectivePB", null),
            /* 155 */ ("mpEffectiveElectric", null),
            /* 156 */ ("mpEffectiveMsl", null),
            /* 157 */ ("mpEffectiveJack", null),
            /* 158 */ ("mpEffectiveSniper", null),
            /* 159 */ ("mpEffectiveIce", null),
            /* 160 */ ("mpEffectiveMortar", null),
            /* 161 */ ("mpEffectiveGhost", null),
            /* 162 */ ("pipeTricity", null),
            /* 163 */ ("breakableExplode", null),
            /* 164 */ ("goreaCrystalHit", null),
            /* 165 */ ("chargeElc", null),
            /* 166 */ ("chargeIce", null),
            /* 167 */ ("chargeJak", null),
            /* 168 */ ("chargeMrt", null),
            /* 169 */ ("chargePB", null),
            /* 170 */ ("chargeMsl", null),
            /* 171 */ ("electroChargeNA", null),
            /* 172 */ ("mortarSecondary", null), // no such file?
            /* 173 */ ("jackHammerColNA", "effects"),
            /* 174 */ ("goreaMeteorLaunch", null),
            /* 175 */ ("goreaReveal", null),
            /* 176 */ ("goreaMeteorDamage", null),
            /* 177 */ ("goreaMeteorDestroy", null),
            /* 178 */ ("goreaMeteorHit", null),
            /* 179 */ ("goreaGrappleDamage", null),
            /* 180 */ ("goreaGrappleDie", null),
            /* 181 */ ("deathBall", null),
            /* 182 */ ("nozzleJet", null),
            /* 183 */ ("syluxMissile", null),
            /* 184 */ ("syluxMissileCol", null),
            /* 185 */ ("syluxMissileFlash", null),
            /* 186 */ ("sphereTricity", null),
            /* 187 */ ("flamingAltForm", null),
            /* 188 */ ("flamingGun", null),
            /* 189 */ ("flamingHunter", null),
            /* 190 */ ("missileCharged", "effects"),
            /* 191 */ ("mortarCharged", "effects"),
            /* 192 */ ("mortarChargedAffinity", "effects"),
            /* 193 */ ("deathBio2", null),
            /* 194 */ ("chargeLoopElc", null),
            /* 195 */ ("chargeLoopIce", null),
            /* 196 */ ("chargeLoopMrt", null),
            /* 197 */ ("chargeLoopMsl", null),
            /* 198 */ ("chargeLoopPB", null),
            /* 199 */ ("sphereTricitySmall", null),
            /* 200 */ ("generatorExplosion", null),
            /* 201 */ ("eyeDamageLoop", null),
            /* 202 */ ("eyeHit", null),
            /* 203 */ ("eyelKill", null),
            /* 204 */ ("eyeKill2", null),
            /* 205 */ ("eyeKill3", null),
            /* 206 */ ("eyeFinalKill", null),
            /* 207 */ ("chargeTurret", null),
            /* 208 */ ("flashTurret", null),
            /* 209 */ ("ultimateProjectile", null),
            /* 210 */ ("goreaLaserCharge", null),
            /* 211 */ ("mortarProjectile", null),
            /* 212 */ ("fallingSnow", null),
            /* 213 */ ("fallingDust", null),
            /* 214 */ ("fallingRock", null),
            /* 215 */ ("deathMech2", null),
            /* 216 */ ("deathAlt", null),
            /* 217 */ ("iceDemonDeath", null),
            /* 218 */ ("lavaDemonDeath", null),
            /* 219 */ ("deathBio3", null),
            /* 220 */ ("deathBio4", null),
            /* 221 */ ("deathBio5", null),
            /* 222 */ ("deathStatue", null),
            /* 223 */ ("deathTick", null),
            /* 224 */ ("goreaLaserCol", null),
            /* 225 */ ("goreaHurt", null),
            /* 226 */ ("explosionAbove", null),
            /* 227 */ ("fireFlurry", null),
            /* 228 */ ("snowFlurry", null),
            /* 229 */ ("enemySpawn", null),
            /* 230 */ ("teleporter", null),
            /* 231 */ ("iceShatter", "effects"),
            /* 232 */ ("sphereTricityDeath", null),
            /* 233 */ ("greenFlurry", null),
            /* 234 */ ("pmagAbsorb", null),
            /* 235 */ ("noxHit", null),
            /* 236 */ ("spireBurst", null),
            /* 237 */ ("electroProjectileUncharged", null),
            /* 238 */ ("enemyProjectile1", null),
            /* 239 */ ("enemyCol1", null),
            /* 240 */ ("psychoCharge", null),
            /* 241 */ ("hammerProjectileSml", null),
            /* 242 */ ("nozzleJetOff", null),
            /* 243 */ ("powerBeamChargeNoSplatMP", null), // no such file?
            /* 244 */ ("doubleDamageGun", null),
            /* 245 */ ("ultimateCol", null),
            /* 246 */ ("enemyMortarProjectile", null)
        };

        public static readonly IReadOnlyList<float> BeamRadiusValues = new float[4]
        {
            0.15f, 0.25f, 0.5f, 0.75f
        };

        public static readonly IReadOnlyList<int> BeamDrawEffects = new List<int>()
        {
            0, 237, 137, 0, 211, 130, 0, 0, 0, 0, 134, 209, 64, 0, 102, 94, 96, 0, 116, 138, 183, 238, 246
        };

        // bombStartSylux, bombStartSyluxR, bombStartSyluxP, bombStartSyluxW, bombStartSyluxO, bombStartSyluxG
        public static readonly IReadOnlyList<int> SyluxBombEffects = new List<int>()
        {
            113, 152, 151, 153, 150, 149
        };

        public static readonly IReadOnlyDictionary<SingleType, (string Model, string Particle)> SingleParticles
            = new Dictionary<SingleType, (string, string)>()
        {
            { SingleType.Death, ("deathParticle", "death") },
            { SingleType.Fuzzball, ("particles", "fuzzBall") }
        };

        public static readonly IReadOnlyDictionary<string, bool> PreloadResources = new Dictionary<string, bool>()
        {
            { "deathParticle", true },
            { "particles", true },
            { "particles2", true },
            { "TearParticle", true },
            { "icons", true },
            { "iceWave", true },
            { "sniperBeam", true },
            { "cylBossLaserBurn", true }
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

        public static int GetAreaInfo(int roomId)
        {
            // Oubliette
            int areaId = 8;
            if (roomId >= 27 && roomId < 36)
            {
                // Alinos 1
                areaId = 0;
            }
            else if (roomId >= 36 && roomId < 45)
            {
                // Alinos 2
                areaId = 1;
            }
            else if (roomId >= 45 && roomId < 56)
            {
                // Celestial Archives 1
                areaId = 2;
            }
            else if (roomId >= 56 && roomId < 65)
            {
                // Celestial Archives 2
                areaId = 3;
            }
            else if (roomId >= 65 && roomId < 72)
            {
                // Vesper Defense Outpost 1
                areaId = 4;
            }
            else if (roomId >= 72 && roomId < 77)
            {
                // Vesper Defense Outpost 2
                areaId = 5;
            }
            else if (roomId >= 77 && roomId < 83)
            {
                // Arcterra 1
                areaId = 6;
            }
            else if (roomId >= 83 && roomId < 89)
            {
                // Arcterra 2
                areaId = 7;
            }
            //bool multiplayer = roomId >= 93 && roomId <= 119;
            return areaId;
        }

        public static readonly Vector4 RedPalette = new Vector4(189 / 255f, 66 / 255f, 0f, 1f);

        public static readonly Vector4 WhitePalette = new Vector4(1f, 1f, 1f, 1f);

        public static readonly ModelMetadata DoubleDamageImg
            = new ModelMetadata("doubleDamage_img", animation: false, archive: "common");

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
                    new ModelMetadata("AlimbicCapsule", collision: true, extraCollision: "AlmbCapsuleShld")
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
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 1 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicDoor_Model.bin",
                                texturePath: @"models\AlimbicDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
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
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 1, 2 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicThinDoor_Model.bin",
                                texturePath: @"models\AlimbicThinDoor_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
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
                    new ModelMetadata("arcWelder1",
                        remove: "1",
                        recolors: new List<string>()
                        {
                            "1",
                            "2",
                            "3",
                            "4",
                            "5"
                        },
                        animation: false,
                        noUnderscore: true)
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
                    new ModelMetadata("Chomtroid", animation: true, animationPath: @"models\Mochtroid_Anim.bin")
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
                    new ModelMetadata("cylinderbase", animation: false, collision: true)
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
                    "hudfont",
                    new ModelMetadata("hudfont", animation: false)
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
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 0 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\ForceField_Model.bin",
                                texturePath: @"models\ForceField_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
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
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 0, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 1, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 2, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 3, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 4, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 5, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
                                separateReplace: true,
                                replaceIds: new Dictionary<int, IEnumerable<int>>() { { 6, new List<int> { 3 } } }),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\AlimbicTextureShare_img_Model.bin",
                                texturePath: @"models\AlimbicTextureShare_img_Model.bin",
                                palettePath: @"models\AlimbicPalettes_pal_Model.bin",
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
                            "pal_01",
                            "pal_02",
                            "pal_03",
                            "pal_04",
                            "pal_Team01",
                            "pal_Team02"
                        },
                        texture: true,
                        archive: "Guardian",
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
                        useLightSources: true)
                },
                {
                    "Guardian_lod1",
                    new ModelMetadata("Guardian_lod1",
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
                        texture: true,
                        animationPath: @"_archives\Guardian\Guardian_Anim.bin",
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
                        useLightSources: true)
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
                        useLightSources: true)
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                {
                    "Lava_Power",
                    new ModelMetadata("Lava_Power",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
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
                        animationShare: @"models\NoxSharedAnim_Anim.bin",
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
                        animationShare: @"models\NoxSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                                palettePath: @"_archives\common\samus_ice_img_Model.bin"),
                            // header values indicate that doubleDamage_img_Model.bin was generated as a recolor of samus_ice,
                            // although it's not used as one in practice -- note that the viewer uses the standalone member above
                            new RecolorMetadata("dbl_dmg",
                                modelPath: @"_archives\common\doubleDamage_img_Model.bin",
                                texturePath: @"_archives\common\doubleDamage_img_Model.bin",
                                palettePath: @"_archives\common\doubleDamage_img_Model.bin")
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
                                palettePath: @"models\SecretSwitch_pal_01_Model.bin"),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_02_Model.bin"),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_03_Model.bin"),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_04_Model.bin"),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_05_Model.bin"),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\SecretSwitch_Model.bin",
                                texturePath: @"models\SecretSwitch_Model.bin",
                                palettePath: @"models\SecretSwitch_pal_06_Model.bin")
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                                palettePath: @"models\Teleporter_pal_01_Model.bin"),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_02_Model.bin"),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_03_Model.bin"),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_04_Model.bin"),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_05_Model.bin"),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_06_Model.bin"),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_07_Model.bin"),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_08_Model.bin"),
                            new RecolorMetadata("pal_09",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_09_Model.bin")
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
                                palettePath: @"models\Teleporter_pal_01_Model.bin"),
                            new RecolorMetadata("pal_02",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_02_Model.bin"),
                            new RecolorMetadata("pal_03",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_03_Model.bin"),
                            new RecolorMetadata("pal_04",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_04_Model.bin"),
                            new RecolorMetadata("pal_05",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_05_Model.bin"),
                            new RecolorMetadata("pal_06",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_06_Model.bin"),
                            new RecolorMetadata("pal_07",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_07_Model.bin"),
                            new RecolorMetadata("pal_08",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_08_Model.bin"),
                            new RecolorMetadata("pal_09",
                                modelPath: @"models\TeleporterTextureShare_img_Model.bin",
                                texturePath: @"models\TeleporterTextureShare_img_Model.bin",
                                palettePath: @"models\Teleporter_pal_09_Model.bin")
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
                        animationShare: @"models\NoxSharedAnim_Anim.bin",
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
                        animationShare: @"models\NoxSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                        animationShare: @"models\SamusSharedAnim_Anim.bin",
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
                {
                    "TearParticle",
                    new ModelMetadata("TearParticle", animation: false, texture: true)
                }
                // todo: can't parse some out of bounds texture/palette offsets from this
                //{
                //    "icons",
                //    new ModelMetadata("icons",
                //        modelPath: @"hud\icons_Model.bin",
                //        animationPath: null,
                //        collisionPath: null,
                //        new List<RecolorMetadata>()
                //        {
                //            new RecolorMetadata("default",
                //                modelPath: @"hud\icons_Model.bin")
                //        }
                //    )
                //}
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
                    "jumppad_ray",
                    new ModelMetadata("jumppad_ray", firstHunt: true)
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
                    new ModelMetadata("morphBall",
                        recolors: new List<string>()
                        {
                            "*morphBall",
                            "Green",
                            "White",
                            "Blue"
                        },
                        animation: false,
                        firstHunt: true)
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
                    "samus_hi_yellow",
                    new ModelMetadata("samus_hi_yellow",
                        recolors: new List<string>()
                        {
                            "*samus_hi_yellow",
                            "hi_green",
                            "hi_white",
                            "hi_blue"
                        },
                        animationPath: @"models\samus_Anim.bin",
                        remove: "_hi_yellow",
                        firstHunt: true)
                },
                {
                    "samus_low_yellow",
                    new ModelMetadata("samus_low_yellow",
                        recolors: new List<string>()
                        {
                            "*samus_low_yellow",
                            "hi_green",
                            "hi_white",
                            "hi_blue"
                        },
                        animationPath: @"models\samus_Anim.bin",
                        remove: "_low_yellow",
                        firstHunt: true)
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
