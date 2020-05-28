using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;

namespace MphRead
{
    public class RoomMetadata
    {
        public string Name { get; }
        public string InGameName { get; } // not in struct
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
        public short LayerId { get; }
        //public uint Unknown32 { get; }
        public ushort FogEnabled { get; }
        public ushort Fog { get; }
        public ushort FogColor { get; }
        //public short Unknown36 { get; }
        public uint FogSlope { get; }
        public uint FogOffset { get; }
        public ColorRgba Light1Color { get; }
        public Vector3 Light1Vector { get; }
        public ColorRgba Light2Color { get; }
        public Vector3 Light2Vector { get; }
        //public string ArchiveName { get; }
        //public string ArchivePath { get; }
        //public uint Unknown21 { get; }
        //public uint Unknown22 { get; }
        public Vector3 RoomSize { get; } // not in structure

        public RoomMetadata(string name, string inGameName, string pathName, string modelPath,
            string animationPath, string collisionPath, string? texturePath, string? entityPath, string? nodePath,
            string? roomNodeName, uint battleTimeLimit, uint timeLimit, short pointLimit, short layerId, ushort fogEnabled,
            ushort fog, ushort fogColor, uint fogSlope, uint fogOffset, ColorRgba light1Color,
            Vector3 light1Vector, ColorRgba light2Color, Vector3 light2Vector, Vector3 roomSize)
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
            LayerId = layerId;
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
        }
    }

    public enum MdlSuffix
    {
        None,
        All,
        Model
    }

    public class EntityMetadata
    {
        public string Name { get; }
        public string ModelPath { get; }
        public string? AnimationPath { get; }
        public string? CollisionPath { get; }
        public IReadOnlyList<RecolorMetadata> Recolors { get; }

        public EntityMetadata(string name, string modelPath, string? animationPath, string? collisionPath,
            IReadOnlyList<RecolorMetadata> recolors)
        {
            Name = name;
            ModelPath = modelPath;
            AnimationPath = animationPath;
            CollisionPath = collisionPath;
            Recolors = recolors;
        }

        public EntityMetadata(string name, string? animationPath, string? texturePath = null)
        {
            Name = name;
            ModelPath = $@"models\{name}_Model.bin";
            AnimationPath = animationPath;
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", ModelPath, texturePath ?? ModelPath)
            };
        }

        public EntityMetadata(string name, string remove, bool animation = true,
            string? animationPath = null, bool collision = false)
        {
            Name = name;
            ModelPath = $@"models\{name}_Model.bin";
            string removed = name.Replace(remove, "");
            if (animation)
            {
                AnimationPath = animationPath ?? $@"models\{removed}_Anim.bin";
            }
            if (collision)
            {
                CollisionPath = $@"models\{removed}_Collision.bin";
            }
            Recolors = new List<RecolorMetadata>()
            {
                new RecolorMetadata("default", ModelPath)
            };
        }

        public EntityMetadata(string name, IEnumerable<string> recolors, string? remove = null,
            bool animation = false, string? animationPath = null, bool texture = false,
            MdlSuffix mdlSuffix = MdlSuffix.None, string? archive = null, string? recolorName = null)
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
                string recolorModel = $@"models\{recolorName ?? name}_{recolor}_Model.bin";
                string texturePath = texture ? $@"models\{recolorName ?? name}_{recolor}_Tex.bin" : recolorModel;
                recolorList.Add(new RecolorMetadata(recolor, recolorModel, texturePath));
            }
            Recolors = recolorList;
        }

        public EntityMetadata(string name, bool animation = true, bool collision = false,
            bool texture = false, string? share = null, MdlSuffix mdlSuffix = MdlSuffix.None,
            string? archive = null, string? addToAnim = null)
        {
            Name = name;
            string path = archive == null ? "models" : $@"_archives\{archive}";
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
            bool separatePaletteHeader = false)
        {
            Name = name;
            ModelPath = modelPath;
            TexturePath = texturePath;
            PalettePath = palettePath;
            SeparatePaletteHeader = separatePaletteHeader;
        }
    }

    public static class Metadata
    {
        public static EntityMetadata? GetEntityByName(string name)
        {
            if (EntityMetadata.TryGetValue(name, out EntityMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        public static EntityMetadata? GetEntityByPath(string path)
        {
            KeyValuePair<string, EntityMetadata> result = EntityMetadata.FirstOrDefault(r => r.Value.ModelPath == path);
            if (result.Key == null)
            {
                return null;
            }
            return result.Value;
        }

        public static RoomMetadata? GetRoomByName(string name)
        {
            if (RoomMetadata.TryGetValue(name, out RoomMetadata? metadata))
            {
                return metadata;
            }
            return null;
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

        private static readonly IReadOnlyList<string> _roomIds
            = new List<string>()
            {
                "UNIT1_CX",
                "UNIT1_CX",
                "UNIT1_CZ",
                "UNIT1_CZ",
                "UNIT1_MORPH_CX",
                "UNIT1_MORPH_CX",
                "UNIT1_MORPH_CZ",
                "UNIT1_MORPH_CZ",
                "UNIT2_CX",
                "UNIT2_CX",
                "UNIT2_CZ",
                "UNIT2_CZ",
                "UNIT3_CX",
                "UNIT3_CX",
                "UNIT3_CZ",
                "UNIT3_CZ",
                "UNIT4_CX",
                "UNIT4_CX",
                "UNIT4_CZ",
                "UNIT4_CZ",
                "CYLINDER_C1",
                "BIGEYE_C1",
                "UNIT1_RM1_CX",
                "UNIT1_RM1_CX",
                "GOREA_C1",
                "UNIT3_MORPH_CZ",
                "UNIT3_MORPH_CZ",
                "UNIT1_LAND",
                "UNIT1_C0",
                "UNIT1_RM1",
                "UNIT1_C4",
                "UNIT1_RM6",
                "CRYSTALROOM",
                "UNIT1_RM4",
                "UNIT1_TP1",
                "UNIT1_B1",
                "UNIT1_C1",
                "UNIT1_C2",
                "UNIT1_C5",
                "UNIT1_RM2",
                "UNIT1_RM3",
                "UNIT1_RM5",
                "UNIT1_C3",
                "UNIT1_TP2",
                "UNIT1_B2",
                "UNIT2_LAND",
                "UNIT2_C0",
                "UNIT2_C1",
                "UNIT2_RM1",
                "UNIT2_C2",
                "UNIT2_RM2",
                "UNIT2_C3",
                "UNIT2_RM3",
                "UNIT2_C4",
                "UNIT2_TP1",
                "UNIT2_B1",
                "UNIT2_C6",
                "UNIT2_C7",
                "UNIT2_RM4",
                "UNIT2_RM5",
                "UNIT2_RM6",
                "UNIT2_RM7",
                "UNIT2_RM8",
                "UNIT2_TP2",
                "UNIT2_B2",
                "UNIT3_LAND",
                "UNIT3_C0",
                "UNIT3_C2",
                "UNIT3_RM1",
                "UNIT3_RM4",
                "UNIT3_TP1",
                "UNIT3_B1",
                "UNIT3_C1",
                "UNIT3_RM2",
                "UNIT3_RM3",
                "UNIT3_TP2",
                "UNIT3_B2",
                "UNIT4_LAND",
                "UNIT4_RM1",
                "UNIT4_RM3",
                "UNIT4_C0",
                "UNIT4_TP1",
                "UNIT4_B1",
                "UNIT4_C1",
                "UNIT4_RM2",
                "UNIT4_RM4",
                "UNIT4_RM5",
                "UNIT4_TP2",
                "UNIT4_B2",
                "Gorea_Land",
                "Gorea_Peek",
                "Gorea_b1",
                "Gorea_b2",
                "MP1 SANCTORUS",
                "MP2 HARVESTER",
                "MP3 PROVING GROUND",
                "MP4 HIGHGROUND - EXPANDED",
                "MP4 HIGHGROUND",
                "MP5 FUEL SLUICE",
                "MP6 HEADSHOT",
                "MP7 PROCESSOR CORE",
                "MP8 FIRE CONTROL",
                "MP9 CRYOCHASM",
                "MP10 OVERLOAD",
                "MP11 BREAKTHROUGH",
                "MP12 SIC TRANSIT",
                "MP13 ACCELERATOR",
                "MP14 OUTER REACH",
                "CTF1 FAULT LINE - EXPANDED",
                "CTF1_FAULT LINE",
                "AD1 TRANSFER LOCK BT",
                "AD1 TRANSFER LOCK DM",
                "AD2 MAGMA VENTS",
                "AD2 ALINOS PERCH",
                "UNIT1 ALINOS LANDFALL",
                "UNIT2 LANDING BAY",
                "UNIT 3 VESPER STARPORT",
                "UNIT 4 ARCTERRA BASE",
                "Gorea Prison",
                "E3 FIRST HUNT",
                "Level TestLevel",
                "Level AbeTest"
            };

        // todo: unused files unit1_b2, unit2_b2, unit3_b1, unit3_b2, unit4_b1, unit4_b2
        // per mph-viewer metadata, these use cylinderroom/bigeyeroom files instead
        // --> are they actually used in-game with those other files, or are the rooms unused altogether?
        // if the former, we should add extra indices for loading the unused files. if the latter, just replace the strings.
        public static readonly IReadOnlyDictionary<string, RoomMetadata> RoomMetadata
            = new Dictionary<string, RoomMetadata>()
            {
                {
                    "UNIT1_CX",
                    new RoomMetadata(
                        "UNIT1_CX",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10, 0, 0))
                },
                {
                    "UNIT1_CZ",
                    new RoomMetadata(
                        "UNIT1_CZ",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10))
                },
                {
                    "UNIT1_MORPH_CX",
                    new RoomMetadata(
                        "UNIT1_MORPH_CX",
                        "todo:inGameName",
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
                        new ColorRgba(31, 18, 6, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 6, 4, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10, 0, 0))
                },
                {
                    "UNIT1_MORPH_CZ",
                    new RoomMetadata(
                        "UNIT1_MORPH_CZ",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10))
                },
                {
                    "UNIT2_CX",
                    new RoomMetadata(
                        "UNIT2_CX",
                        "todo:inGameName",
                        "unit2_CX",
                        "unit2_CX_Model.bin",
                        "unit2_CX_Anim.bin",
                        "unit2_CX_Collision.bin",
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
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10.378662, 0, 0))
                },
                {
                    "UNIT2_CZ",
                    new RoomMetadata(
                        "UNIT2_CZ",
                        "todo:inGameName",
                        "unit2_CZ",
                        "unit2_CZ_Model.bin",
                        "unit2_CZ_Anim.bin",
                        "unit2_CZ_Collision.bin",
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
                        FogColor(18, 31, 18),
                        0x4,
                        65152,
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10.378662))
                },
                {
                    "UNIT3_CX",
                    new RoomMetadata(
                        "UNIT3_CX",
                        "todo:inGameName",
                        "unit3_CX",
                        "unit3_CX_Model.bin",
                        "unit3_CX_Anim.bin",
                        "unit3_CX_Collision.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10, 0, 0))
                },
                {
                    "UNIT3_CZ",
                    new RoomMetadata(
                        "UNIT3_CZ",
                        "todo:inGameName",
                        "unit3_CZ",
                        "unit3_CZ_Model.bin",
                        "unit3_CZ_Anim.bin",
                        "unit3_CZ_Collision.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10))
                },
                {
                    "UNIT4_CX",
                    new RoomMetadata(
                        "UNIT4_CX",
                        "todo:inGameName",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10, 0, 0))
                },
                {
                    "UNIT4_CZ",
                    new RoomMetadata(
                        "UNIT4_CZ",
                        "todo:inGameName",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10))
                },
                {
                    "CYLINDER_C1",
                    new RoomMetadata(
                        "CYLINDER_C1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 2.295166, 22.654053))
                },
                {
                    "BIGEYE_C1",
                    new RoomMetadata(
                        "BIGEYE_C1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, -1.869873, 22.653809))
                },
                {
                    "UNIT1_RM1_CX",
                    new RoomMetadata(
                        "UNIT1_RM1_CX",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(10, 0, 0))
                },
                {
                    "GOREA_C1",
                    new RoomMetadata(
                        "GOREA_C1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 20))
                },
                {
                    "UNIT3_MORPH_CZ",
                    new RoomMetadata(
                        "UNIT3_MORPH_CZ",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(0, 0, 10))
                },
                {
                    "UNIT1_LAND",
                    new RoomMetadata(
                        "UNIT1_LAND",
                        "todo:inGameName",
                        "unit1_Land",
                        "unit1_Land_Model.bin",
                        "unit1_Land_Anim.bin",
                        "unit1_Land_Collision.bin",
                        "unit1_Land_Tex.bin",
                        "unit1_Land_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1_C0",
                    new RoomMetadata(
                        "UNIT1_C0",
                        "todo:inGameName",
                        "unit1_C0",
                        "unit1_C0_Model.bin",
                        "unit1_C0_Anim.bin",
                        "unit1_C0_Collision.bin",
                        "unit1_c0_Tex.bin",
                        "unit1_C0_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT1_RM1",
                    new RoomMetadata(
                        "UNIT1_RM1",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1_C4",
                    new RoomMetadata(
                        "UNIT1_C4",
                        "todo:inGameName",
                        "unit1_C4",
                        "unit1_C4_Model.bin",
                        "unit1_C4_Anim.bin",
                        "unit1_C4_Collision.bin",
                        "unit1_c4_Tex.bin",
                        "unit1_C4_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT1_RM6",
                    new RoomMetadata(
                        "UNIT1_RM6",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-41, 41, 6))
                },
                {
                    "CRYSTALROOM",
                    new RoomMetadata(
                        "CRYSTALROOM",
                        "todo:inGameName",
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
                        new ColorRgba(19, 29, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(32, -72, 72))
                },
                {
                    "UNIT1_RM4",
                    new RoomMetadata(
                        "UNIT1_RM4",
                        "todo:inGameName",
                        "mp3",
                        "mp3_Model.bin",
                        "mp3_Anim.bin",
                        "mp3_Collision.bin",
                        "mp3_Tex.bin",
                        "unit1_RM4_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1_TP1",
                    new RoomMetadata(
                        "UNIT1_TP1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(20, 8, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT1_B1",
                    new RoomMetadata(
                        "UNIT1_B1",
                        "todo:inGameName",
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "unit1_b1_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-11, 10, 0))
                },
                {
                    "UNIT1_C1",
                    new RoomMetadata(
                        "UNIT1_C1",
                        "todo:inGameName",
                        "unit1_C1",
                        "unit1_C1_Model.bin",
                        "unit1_C1_Anim.bin",
                        "unit1_C1_Collision.bin",
                        "unit1_c1_Tex.bin",
                        "unit1_C1_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(13, -27, 26))
                },
                {
                    "UNIT1_C2",
                    new RoomMetadata(
                        "UNIT1_C2",
                        "todo:inGameName",
                        "unit1_C2",
                        "unit1_C2_Model.bin",
                        "unit1_C2_Anim.bin",
                        "unit1_C2_Collision.bin",
                        "unit1_c2_Tex.bin",
                        "unit1_C2_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1_C5",
                    new RoomMetadata(
                        "UNIT1_C5",
                        "todo:inGameName",
                        "unit1_C5",
                        "unit1_C5_Model.bin",
                        "unit1_C5_Anim.bin",
                        "unit1_C5_Collision.bin",
                        "unit1_c5_Tex.bin",
                        "unit1_C5_Ent.bin",
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
                        new ColorRgba(31, 18, 6, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 9, 4, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT1_RM2",
                    new RoomMetadata(
                        "UNIT1_RM2",
                        "todo:inGameName",
                        "unit1_rm2",
                        "unit1_rm2_Model.bin",
                        "unit1_rm2_Anim.bin",
                        "unit1_rm2_Collision.bin",
                        "unit1_RM2_Tex.bin",
                        "unit1_RM2_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-37, 45, -1))
                },
                {
                    "UNIT1_RM3",
                    new RoomMetadata(
                        "UNIT1_RM3",
                        "todo:inGameName",
                        "unit1_rm3",
                        "unit1_rm3_Model.bin",
                        "unit1_rm3_Anim.bin",
                        "unit1_rm3_Collision.bin",
                        "unit1_RM3_Tex.bin",
                        "unit1_RM3_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(25, -27, 20))
                },
                {
                    "UNIT1_RM5",
                    new RoomMetadata(
                        "UNIT1_RM5",
                        "todo:inGameName",
                        "mp7",
                        "mp7_Model.bin",
                        "mp7_Anim.bin",
                        "mp7_Collision.bin",
                        "mp7_Tex.bin",
                        "unit1_RM5_Ent.bin",
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
                        new ColorRgba(31, 18, 6, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 6, 4, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1_C3",
                    new RoomMetadata(
                        "UNIT1_C3",
                        "todo:inGameName",
                        "unit1_C3",
                        "unit1_C3_Model.bin",
                        "unit1_C3_Anim.bin",
                        "unit1_C3_Collision.bin",
                        "unit1_c3_Tex.bin",
                        "unit1_C3_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT1_TP2",
                    new RoomMetadata(
                        "UNIT1_TP2",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-37, 45, -1))
                },
                {
                    "UNIT1_B2",
                    new RoomMetadata(
                        "UNIT1_B2",
                        "todo:inGameName",
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "unit1_b2_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(25, -27, 20))
                },
                {
                    "UNIT2_LAND",
                    new RoomMetadata(
                        "UNIT2_LAND",
                        "todo:inGameName",
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
                        new ColorRgba(24, 24, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_C0",
                    new RoomMetadata(
                        "UNIT2_C0",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2_C1",
                    new RoomMetadata(
                        "UNIT2_C1",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-24, 27, 0))
                },
                {
                    "UNIT2_RM1",
                    new RoomMetadata(
                        "UNIT2_RM1",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(17, -14, 14))
                },
                {
                    "UNIT2_C2",
                    new RoomMetadata(
                        "UNIT2_C2",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_RM2",
                    new RoomMetadata(
                        "UNIT2_RM2",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2_C3",
                    new RoomMetadata(
                        "UNIT2_C3",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-49, 48, 0))
                },
                {
                    "UNIT2_RM3",
                    new RoomMetadata(
                        "UNIT2_RM3",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(48, -49, 47))
                },
                {
                    "UNIT2_C4",
                    new RoomMetadata(
                        "UNIT2_C4",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-70, 70, -21.227783))
                },
                {
                    "UNIT2_TP1",
                    new RoomMetadata(
                        "UNIT2_TP1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(48, -70, 70))
                },
                {
                    "UNIT2_B1",
                    new RoomMetadata(
                        "UNIT2_B1",
                        "todo:inGameName",
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "unit2_b1_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_C6",
                    new RoomMetadata(
                        "UNIT2_C6",
                        "todo:inGameName",
                        "unit2_C6",
                        "unit2_C6_Model.bin",
                        "unit2_C6_Anim.bin",
                        "unit2_C6_Collision.bin",
                        "unit2_c6_Tex.bin",
                        "unit2_C6_Ent.bin",
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
                        new ColorRgba(18, 31, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2_C7",
                    new RoomMetadata(
                        "UNIT2_C7",
                        "todo:inGameName",
                        "unit2_C7",
                        "unit2_C7_Model.bin",
                        "unit2_C7_Anim.bin",
                        "unit2_C7_Collision.bin",
                        "unit2_c7_Tex.bin",
                        "unit2_C7_Ent.bin",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_RM4",
                    new RoomMetadata(
                        "UNIT2_RM4",
                        "todo:inGameName",
                        "unit2_RM4",
                        "unit2_RM4_Model.bin",
                        "unit2_RM4_Anim.bin",
                        "unit2_RM4_Collision.bin",
                        "unit2_RM4_Tex.bin",
                        "unit2_RM4_Ent.bin",
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
                        new ColorRgba(29, 20, 10, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2_RM5",
                    new RoomMetadata(
                        "UNIT2_RM5",
                        "todo:inGameName",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "unit2_RM5_Ent.bin",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-43, 44, 0))
                },
                {
                    "UNIT2_RM6",
                    new RoomMetadata(
                        "UNIT2_RM6",
                        "todo:inGameName",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "unit2_RM6_Ent.bin",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(33, -33, 55))
                },
                {
                    "UNIT2_RM7",
                    new RoomMetadata(
                        "UNIT2_RM7",
                        "todo:inGameName",
                        "mp10",
                        "mp10_Model.bin",
                        "mp10_Anim.bin",
                        "mp10_Collision.bin",
                        "mp10_Tex.bin",
                        "unit2_RM7_Ent.bin",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_RM8",
                    new RoomMetadata(
                        "UNIT2_RM8",
                        "todo:inGameName",
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
                        new ColorRgba(29, 20, 10, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2_TP2",
                    new RoomMetadata(
                        "UNIT2_TP2",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT2_B2",
                    new RoomMetadata(
                        "UNIT2_B2",
                        "todo:inGameName",
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "unit2_b2_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT3_LAND",
                    new RoomMetadata(
                        "UNIT3_LAND",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT3_C0",
                    new RoomMetadata(
                        "UNIT3_C0",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT3_C2",
                    new RoomMetadata(
                        "UNIT3_C2",
                        "todo:inGameName",
                        "unit3_c2",
                        "unit3_c2_Model.bin",
                        "unit3_c2_Anim.bin",
                        "unit3_c2_Collision.bin",
                        "unit3_c2_Tex.bin",
                        "unit3_c2_Ent.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-22, 18, -2))
                },
                {
                    "UNIT3_RM1",
                    new RoomMetadata(
                        "UNIT3_RM1",
                        "todo:inGameName",
                        "unit3_rm1",
                        "unit3_rm1_Model.bin",
                        "unit3_rm1_Anim.bin",
                        "unit3_rm1_Collision.bin",
                        "unit3_rm1_Tex.bin",
                        "unit3_RM1_Ent.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(13.299805, -15, 11))
                },
                {
                    "UNIT3_RM4",
                    new RoomMetadata(
                        "UNIT3_RM4",
                        "todo:inGameName",
                        "mp5",
                        "mp5_Model.bin",
                        "mp5_Anim.bin",
                        "mp5_Collision.bin",
                        "mp5_Tex.bin",
                        "unit3_RM4_Ent.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT3_TP1",
                    new RoomMetadata(
                        "UNIT3_TP1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT3_B1",
                    new RoomMetadata(
                        "UNIT3_B1",
                        "todo:inGameName",
                        "cylinderroom",
                        "cylinderroom_Model.bin",
                        "cylinderroom_Anim.bin",
                        "cylinderroom_Collision.bin",
                        "cylinderroom_Tex.bin",
                        "unit3_b1_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT3_C1",
                    new RoomMetadata(
                        "UNIT3_C1",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT3_RM2",
                    new RoomMetadata(
                        "UNIT3_RM2",
                        "todo:inGameName",
                        "unit3_rm2",
                        "unit3_rm2_Model.bin",
                        "unit3_rm2_Anim.bin",
                        "unit3_rm2_Collision.bin",
                        "unit3_rm2_Tex.bin",
                        "unit3_RM2_Ent.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT3_RM3",
                    new RoomMetadata(
                        "UNIT3_RM3",
                        "todo:inGameName",
                        "e3Level",
                        "e3Level_Model.bin",
                        "e3Level_Anim.bin",
                        "e3Level_Collision.bin",
                        "e3Level_Tex.bin",
                        "unit3_RM3_Ent.bin",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT3_TP2",
                    new RoomMetadata(
                        "UNIT3_TP2",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-19, 27, -1))
                },
                {
                    "UNIT3_B2",
                    new RoomMetadata(
                        "UNIT3_B2",
                        "todo:inGameName",
                        "bigeyeroom",
                        "bigeyeroom_Model.bin",
                        "bigeyeroom_Anim.bin",
                        "bigeyeroom_Collision.bin",
                        "bigeyeroom_Tex.bin",
                        "unit3_b2_Ent.bin",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(15, -29, 26))
                },
                {
                    "UNIT4_LAND",
                    new RoomMetadata(
                        "UNIT4_LAND",
                        "todo:inGameName",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT4_RM1",
                    new RoomMetadata(
                        "UNIT4_RM1",
                        "todo:inGameName",
                        "unit4_RM1",
                        "unit4_RM1_Model.bin",
                        "unit4_RM1_Anim.bin",
                        "unit4_RM1_Collision.bin",
                        "unit4_RM1_Tex.bin",
                        "unit4_RM1_Ent.bin",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT4_RM3",
                    new RoomMetadata(
                        "UNIT4_RM3",
                        "todo:inGameName",
                        "mp12",
                        "mp12_Model.bin",
                        "mp12_Anim.bin",
                        "mp12_Collision.bin",
                        "mp12_Tex.bin",
                        "unit4_RM3_Ent.bin",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-23, 41, -29))
                },
                {
                    "UNIT4_C0",
                    new RoomMetadata(
                        "UNIT4_C0",
                        "todo:inGameName",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(30, -27, 42))
                },
                {
                    "UNIT4_TP1",
                    new RoomMetadata(
                        "UNIT4_TP1",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT4_B1",
                    new RoomMetadata(
                        "UNIT4_B1",
                        "todo:inGameName",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT4_C1",
                    new RoomMetadata(
                        "UNIT4_C1",
                        "todo:inGameName",
                        "unit4_c1",
                        "unit4_c1_Model.bin",
                        "unit4_c1_Anim.bin",
                        "unit4_c1_Collision.bin",
                        "unit4_c1_Tex.bin",
                        "unit4_c1_Ent.bin",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-32, 31, -3.599854))
                },
                {
                    "UNIT4_RM2",
                    new RoomMetadata(
                        "UNIT4_RM2",
                        "todo:inGameName",
                        "unit4_rm2",
                        "unit4_rm2_Model.bin",
                        "unit4_rm2_Anim.bin",
                        "unit4_rm2_Collision.bin",
                        "unit4_rm2_Tex.bin",
                        "unit4_RM2_Ent.bin",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(24, -30, 29))
                },
                {
                    "UNIT4_RM4",
                    new RoomMetadata(
                        "UNIT4_RM4",
                        "todo:inGameName",
                        "mp11",
                        "mp11_Model.bin",
                        "mp11_Anim.bin",
                        "mp11_Collision.bin",
                        "mp11_Tex.bin",
                        "unit4_RM4_Ent.bin",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT4_RM5",
                    new RoomMetadata(
                        "UNIT4_RM5",
                        "todo:inGameName",
                        "unit4_rm5",
                        "unit4_rm5_Model.bin",
                        "unit4_rm5_Anim.bin",
                        "unit4_rm5_Collision.bin",
                        "unit4_rm5_Tex.bin",
                        "unit4_RM5_Ent.bin",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT4_TP2",
                    new RoomMetadata(
                        "UNIT4_TP2",
                        "todo:inGameName",
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
                        new ColorRgba(8, 28, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-72, 70, 0))
                },
                {
                    "UNIT4_B2",
                    new RoomMetadata(
                        "UNIT4_B2",
                        "todo:inGameName",
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
                        new ColorRgba(12, 6, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(31, 25, 21, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(23.5, -26, 26))
                },
                {
                    "Gorea_Land",
                    new RoomMetadata(
                        "Gorea_Land",
                        "todo:inGameName",
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
                        new ColorRgba(17, 29, 16, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "Gorea_Peek",
                    new RoomMetadata(
                        "Gorea_Peek",
                        "todo:inGameName",
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
                        new ColorRgba(19, 27, 16, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 6, 12, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "Gorea_b1",
                    new RoomMetadata(
                        "Gorea_b1",
                        "todo:inGameName",
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
                        new ColorRgba(18, 24, 27, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 18, 24, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-72, 70, 0))
                },
                {
                    "Gorea_b2",
                    new RoomMetadata(
                        "Gorea_b2",
                        "todo:inGameName",
                        "Gorea_b2",
                        "Gorea_b2_Model.bin",
                        "Gorea_b2_Anim.bin",
                        "Gorea_b2_Collision.bin",
                        "Gorea_b2_Tex.bin",
                        "Gorea_b2_Ent.bin",
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
                        new ColorRgba(18, 16, 14, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(27, 18, 9, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(23.5, -26, 26))
                },
                {
                    "MP1 SANCTORUS",
                    new RoomMetadata(
                        "MP1 SANCTORUS",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "MP2 HARVESTER",
                    new RoomMetadata(
                        "MP2 HARVESTER",
                        "todo:inGameName",
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
                        new ColorRgba(25, 30, 20, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 11, 6, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "MP3 PROVING GROUND",
                    new RoomMetadata(
                        "MP3 PROVING GROUND",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0.099854, -1, 0),
                        new Vector3(-76, 81, -7.127930))
                },
                {
                    "MP4 HIGHGROUND - EXPANDED",
                    new RoomMetadata(
                        "MP4 HIGHGROUND - EXPANDED",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(32, -42, 31))
                },
                {
                    "MP4 HIGHGROUND",
                    new RoomMetadata(
                        "MP4 HIGHGROUND",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-76, 81, -7.127930))
                },
                {
                    "MP5 FUEL SLUICE",
                    new RoomMetadata(
                        "MP5 FUEL SLUICE",
                        "todo:inGameName",
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
                        new ColorRgba(18, 22, 30, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(32, -40.748779, 31))
                },
                {
                    "MP6 HEADSHOT",
                    new RoomMetadata(
                        "MP6 HEADSHOT",
                        "todo:inGameName",
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
                        new ColorRgba(18, 22, 30, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-72, 72, -3))
                },
                {
                    "MP7 PROCESSOR CORE",
                    new RoomMetadata(
                        "MP7 PROCESSOR CORE",
                        "todo:inGameName",
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
                        new ColorRgba(31, 18, 6, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 6, 4, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(23, -42, 42))
                },
                {
                    "MP8 FIRE CONTROL",
                    new RoomMetadata(
                        "MP8 FIRE CONTROL",
                        "todo:inGameName",
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
                        new ColorRgba(18, 22, 30, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "MP9 CRYOCHASM",
                    new RoomMetadata(
                        "MP9 CRYOCHASM",
                        "todo:inGameName",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "MP10 OVERLOAD",
                    new RoomMetadata(
                        "MP10 OVERLOAD",
                        "todo:inGameName",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-68, 60, 0))
                },
                {
                    "MP11 BREAKTHROUGH",
                    new RoomMetadata(
                        "MP11 BREAKTHROUGH",
                        "todo:inGameName",
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
                        new ColorRgba(20, 27, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(8, 8, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(36, -27, 26))
                },
                {
                    "MP12 SIC TRANSIT",
                    new RoomMetadata(
                        "MP12 SIC TRANSIT",
                        "todo:inGameName",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "MP13 ACCELERATOR",
                    new RoomMetadata(
                        "MP13 ACCELERATOR",
                        "todo:inGameName",
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
                        new ColorRgba(18, 22, 30, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "MP14 OUTER REACH",
                    new RoomMetadata(
                        "MP14 OUTER REACH",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-68, 60, 0))
                },
                {
                    "CTF1 FAULT LINE - EXPANDED",
                    new RoomMetadata(
                        "CTF1 FAULT LINE - EXPANDED",
                        "todo:inGameName",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(39, -27, 26))
                },
                {
                    "CTF1_FAULT LINE",
                    new RoomMetadata(
                        "CTF1_FAULT LINE",
                        "todo:inGameName",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "AD1 TRANSFER LOCK BT",
                    new RoomMetadata(
                        "AD1 TRANSFER LOCK BT",
                        "todo:inGameName",
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
                        new ColorRgba(29, 20, 10, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "AD1 TRANSFER LOCK DM",
                    new RoomMetadata(
                        "AD1 TRANSFER LOCK DM",
                        "todo:inGameName",
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
                        new ColorRgba(29, 20, 10, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-71, 85, 2))
                },
                {
                    "AD2 MAGMA VENTS",
                    new RoomMetadata(
                        "AD2 MAGMA VENTS",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(21, -53, 59))
                },
                {
                    "AD2 ALINOS PERCH",
                    new RoomMetadata(
                        "AD2 ALINOS PERCH",
                        "todo:inGameName",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "UNIT1 ALINOS LANDFALL",
                    new RoomMetadata(
                        "UNIT1 ALINOS LANDFALL",
                        "todo:inGameName",
                        "unit1_Land",
                        "unit1_Land_Model.bin",
                        "unit1_Land_Anim.bin",
                        "unit1_Land_Collision.bin",
                        "unit1_Land_Tex.bin",
                        "unit1_Land_dm1_Ent.bin",
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
                        new ColorRgba(31, 24, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(13, 12, 7, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "UNIT2 LANDING BAY",
                    new RoomMetadata(
                        "UNIT2 LANDING BAY",
                        "todo:inGameName",
                        "unit2_Land",
                        "unit2_Land_Model.bin",
                        "unit2_Land_Anim.bin",
                        "unit2_Land_Collision.bin",
                        "unit2_Land_Tex.bin",
                        "unit2_Land_dm1_Ent.bin",
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
                        new ColorRgba(24, 31, 24, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(7, 11, 15, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-23, 25, -11))
                },
                {
                    "UNIT 3 VESPER STARPORT",
                    new RoomMetadata(
                        "UNIT 3 VESPER STARPORT",
                        "todo:inGameName",
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
                        new ColorRgba(18, 22, 30, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(28, -27, 26))
                },
                {
                    "UNIT 4 ARCTERRA BASE",
                    new RoomMetadata(
                        "UNIT 4 ARCTERRA BASE",
                        "todo:inGameName",
                        "unit4_land",
                        "unit4_land_Model.bin",
                        "unit4_land_Anim.bin",
                        "unit4_land_Collision.bin",
                        "unit4_land_Tex.bin",
                        "unit4_land_dm1_Ent.bin",
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
                        new ColorRgba(10, 14, 18, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(4, 4, 8, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                },
                {
                    "Gorea Prison",
                    new RoomMetadata(
                        "Gorea Prison",
                        "todo:inGameName",
                        "Gorea_b2",
                        "Gorea_b2_Model.bin",
                        "Gorea_b2_Anim.bin",
                        "Gorea_b2_Collision.bin",
                        "Gorea_b2_Tex.bin",
                        "Gorea_b2_dm_Ent.bin",
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
                        new ColorRgba(18, 16, 14, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(27, 18, 9, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(300, -300, 300))
                },
                {
                    "E3 FIRST HUNT",
                    new RoomMetadata(
                        "E3 FIRST HUNT",
                        "todo:inGameName",
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
                        new ColorRgba(24, 20, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(9, 8, 14, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-44, 42, -5))
                },
                {
                    "Level TestLevel",
                    new RoomMetadata(
                        "Level TestLevel",
                        "todo:inGameName",
                        "testLevel",
                        "testLevel_Model.bin",
                        "testLevel_Anim.bin",
                        "testLevel_Collision.bin",
                        null,
                        "testLevel_Ent.bin",
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
                        new ColorRgba(31, 31, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(10, 10, 31, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(29, -42, 42))
                },
                {
                    "Level AbeTest",
                    new RoomMetadata(
                        "Level AbeTest",
                        "todo:inGameName",
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
                        new ColorRgba(31, 31, 31, 0),
                        new Vector3(0.099854, -1, 0),
                        new ColorRgba(10, 10, 31, 0),
                        new Vector3(0, 0.999756, -0.099854),
                        new Vector3(-300, 300, -300))
                }
            };

        public static readonly IReadOnlyList<string> JumpPads = new List<string>()
        {
            "JumpPad",
            "JumpPad_Alimbic",
            "JumpPad_Ice",
            "JumpPad_IceStation",
            "JumpPad_Lava",
            "JumpPad_Station"
        };

        public static readonly IReadOnlyList<string> Items = new List<string>()
        {
            "pick_health_B",
            "pick_health_A",
            "pick_health_C",
            "pick_dblDamage",
            "PickUp_EnergyExp",
            "pick_wpn_electro",
            "PickUp_MissileExp",
            "pick_wpn_jackhammer",
            "pick_wpn_snipergun",
            "pick_wpn_shotgun",
            "pick_wpn_mortar",
            "pick_wpn_ghostbuster",
            "pick_wpn_gorea",
            "pick_ammo_green", // todo: dedup?
            "pick_ammo_green",
            "pick_ammo_orange",
            "pick_ammo_orange",
            "pick_invis",
            "PickUp_AmmoExp",
            "Artifact_Key",
            "pick_deathball",
            "pick_wpn_all"
        };

        public static readonly IReadOnlyDictionary<string, EntityMetadata> EntityMetadata
            = new Dictionary<string, EntityMetadata>()
            {
                {
                    "AlimbicBossDoorLock",
                    new EntityMetadata("AlimbicBossDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicBossDoor",
                    new EntityMetadata("AlimbicBossDoor")
                },
                {
                    "AlimbicCapsule",
                    new EntityMetadata("AlimbicCapsule", collision: true)
                },
                {
                    "AlimbicComputerStationControl",
                    new EntityMetadata("AlimbicComputerStationControl")
                },
                {
                    "AlimbicComputerStationControl02",
                    new EntityMetadata("AlimbicComputerStationControl02")
                },
                {
                    "AlimbicDoorLock",
                    new EntityMetadata("AlimbicDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicDoor",
                    new EntityMetadata("AlimbicDoor")
                },
                {
                    "AlimbicEnergySensor",
                    new EntityMetadata("AlimbicEnergySensor")
                },
                {
                    "AlimbicGhost_01",
                    new EntityMetadata("AlimbicGhost_01")
                },
                {
                    "AlimbicLightPole",
                    new EntityMetadata("AlimbicLightPole", collision: true)
                },
                {
                    "AlimbicLightPole02",
                    new EntityMetadata("AlimbicLightPole02", collision: true)
                },
                {
                    "AlimbicMorphBallDoor",
                    new EntityMetadata("AlimbicMorphBallDoor",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicMorphBallDoorLock",
                    new EntityMetadata("AlimbicMorphBallDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "AlimbicStationShieldControl",
                    new EntityMetadata("AlimbicStationShieldControl")
                },
                {
                    "AlimbicStatue_lod0",
                    new EntityMetadata("AlimbicStatue_lod0", remove: "_lod0", collision: true)
                },
                {
                    "AlimbicThinDoor",
                    new EntityMetadata("AlimbicThinDoor")
                },
                {
                    "Alimbic_Console",
                    new EntityMetadata("Alimbic_Console",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Monitor",
                    new EntityMetadata("Alimbic_Monitor",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Power",
                    new EntityMetadata("Alimbic_Power",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Scanner",
                    new EntityMetadata("Alimbic_Scanner",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Switch",
                    new EntityMetadata("Alimbic_Switch",
                        share: @"models\AlimbicEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Alimbic_Turret",
                    new EntityMetadata("Alimbic_Turret",
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
                    new EntityMetadata("alt_ice",
                        modelPath: @"_archives\common\alt_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        })
                },
                {
                    "arcWelder1",
                    new EntityMetadata("arcWelder1", animation: false)
                },
                {
                    "arcWelder2",
                    new EntityMetadata("arcWelder2", animation: false)
                },
                {
                    "arcWelder3",
                    new EntityMetadata("arcWelder3", animation: false)
                },
                {
                    "arcWelder4",
                    new EntityMetadata("arcWelder4", animation: false)
                },
                {
                    "arcWelder5",
                    new EntityMetadata("arcWelder5", animation: false)
                },
                {
                    "ArtifactBase",
                    new EntityMetadata("ArtifactBase")
                },
                {
                    "Artifact_Key",
                    new EntityMetadata("Artifact_Key")
                },
                {
                    "Artifact01",
                    new EntityMetadata("Artifact01",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact02",
                    new EntityMetadata("Artifact02",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact03",
                    new EntityMetadata("Artifact03",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact04",
                    new EntityMetadata("Artifact04",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact05",
                    new EntityMetadata("Artifact05",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact06",
                    new EntityMetadata("Artifact06",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact07",
                    new EntityMetadata("Artifact07",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Artifact08",
                    new EntityMetadata("Artifact08",
                        share: @"models\ArtifactTextureShare_img_Model.bin",
                        animation: false,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "balljump",
                    new EntityMetadata("balljump", animation: false)
                },
                {
                    "balljump_ray",
                    new EntityMetadata("balljump_ray")
                },
                {
                    "BarbedWarWasp",
                    new EntityMetadata("BarbedWarWasp",
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
                    new EntityMetadata("BigEyeBall")
                },
                {
                    "BigEyeNest",
                    new EntityMetadata("BigEyeNest")
                },
                {
                    "BigEyeShield",
                    new EntityMetadata("BigEyeShield")
                },
                {
                    "BigEyeSynapse_01",
                    new EntityMetadata("BigEyeSynapse_01", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_02",
                    new EntityMetadata("BigEyeSynapse_02", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_03",
                    new EntityMetadata("BigEyeSynapse_03", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeSynapse_04",
                    new EntityMetadata("BigEyeSynapse_04", animationPath: @"models\BigEyeSynapse_Anim.bin")
                },
                {
                    "BigEyeTurret",
                    new EntityMetadata("BigEyeTurret")
                },
                {
                    "blastcap",
                    new EntityMetadata("blastcap")
                },
                {
                    "brain_unit3_c2",
                    new EntityMetadata("brain_unit3_c2", animation: false)
                },
                {
                    "Chomtroid",
                    new EntityMetadata("Chomtroid", animation: false)
                },
                {
                    "Crate01",
                    new EntityMetadata("Crate01", collision: true)
                },
                {
                    "cylBossLaserBurn",
                    new EntityMetadata("cylBossLaserBurn")
                },
                {
                    "cylBossLaserColl",
                    new EntityMetadata("cylBossLaserColl")
                },
                {
                    "cylBossLaserG",
                    new EntityMetadata("cylBossLaserG")
                },
                {
                    "cylBossLaserY",
                    new EntityMetadata("cylBossLaserY")
                },
                {
                    "cylBossLaser",
                    new EntityMetadata("cylBossLaser")
                },
                {
                    "cylinderbase",
                    new EntityMetadata("cylinderbase")
                },
                {
                    "CylinderBossEye",
                    new EntityMetadata("CylinderBossEye")
                },
                {
                    "CylinderBoss",
                    new EntityMetadata("CylinderBoss")
                },
                {
                    "deathParticle",
                    new EntityMetadata("deathParticle", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "deepspace",
                    new EntityMetadata("deepspace", archive: "shipSpace")
                },
                {
                    "Door_Unit4_RM1",
                    new EntityMetadata("Door_Unit4_RM1", animation: false, collision: true)
                },
                {
                    "DripStank_lod0",
                    new EntityMetadata("DripStank_lod0", remove: "_lod0")
                },
                {
                    "ElectroField1",
                    new EntityMetadata("ElectroField1", collision: true)
                },
                {
                    "Elevator",
                    new EntityMetadata("Elevator", animation: false, collision: true)
                },
                {
                    "EnemySpawner",
                    new EntityMetadata("EnemySpawner",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "energyBeam",
                    new EntityMetadata("energyBeam")
                },
                {
                    "filter",
                    new EntityMetadata("filter", animation: false, archive: "common")
                },
                {
                    "flagbase_bounty",
                    new EntityMetadata("flagbase_bounty")
                },
                {
                    "flagbase_cap",
                    new EntityMetadata("flagbase_cap")
                },
                {
                    "flagbase_ctf",
                    new EntityMetadata("flagbase_ctf",
                        recolors: new List<string>()
                        {
                            "green_img",
                            "orange_img"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "ForceField",
                    new EntityMetadata("ForceField")
                },
                {
                    "ForceFieldLock",
                    new EntityMetadata("ForceFieldLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "furlEffect",
                    new EntityMetadata("furlEffect")
                },
                {
                    "geemer",
                    new EntityMetadata("geemer")
                },
                {
                    "Generic_Console",
                    new EntityMetadata("Generic_Console",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Monitor",
                    new EntityMetadata("Generic_Monitor",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Power",
                    new EntityMetadata("Generic_Power",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Scanner",
                    new EntityMetadata("Generic_Scanner",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Generic_Switch",
                    new EntityMetadata("Generic_Switch",
                        share: @"models\GenericEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "geo1",
                    new EntityMetadata("geo1", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "GhostSwitch",
                    new EntityMetadata("GhostSwitch",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "Gorea1A_lod0",
                    new EntityMetadata("Gorea1A_lod0", remove: "_lod0")
                },
                {
                    "Gorea1B_lod0",
                    new EntityMetadata("Gorea1B_lod0", remove: "_lod0")
                },
                {
                    "Gorea2_lod0",
                    new EntityMetadata("Gorea2_lod0", remove: "_lod0")
                },
                {
                    "goreaArmRegen",
                    new EntityMetadata("goreaArmRegen")
                },
                {
                    "goreaGeo",
                    new EntityMetadata("goreaGeo", animation: false, texture: true)
                },
                {
                    "goreaGrappleBeam",
                    new EntityMetadata("goreaGrappleBeam")
                },
                {
                    "goreaLaserColl",
                    new EntityMetadata("goreaLaserColl")
                },
                {
                    "goreaLaser",
                    new EntityMetadata("goreaLaser")
                },
                {
                    "goreaMeteor",
                    new EntityMetadata("goreaMeteor")
                },
                {
                    "goreaMindTrick",
                    new EntityMetadata("goreaMindTrick")
                },
                {
                    "gorea_gun",
                    new EntityMetadata("gorea_gun")
                },
                {
                    "Guardbot01_Dead",
                    new EntityMetadata("Guardbot01_Dead", animation: false)
                },
                {
                    "Guardbot02_Dead",
                    new EntityMetadata("Guardbot02_Dead", animation: false)
                },
                {
                    "GuardBot1",
                    new EntityMetadata("GuardBot1",
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
                    new EntityMetadata("GuardBot2_lod0",
                        remove: "_lod0",
                        animationPath: @"models\GuardBot02_Anim.bin")
                },
                {
                    "Guardian_Dead",
                    new EntityMetadata("Guardian_Dead", animation: false)
                },
                // Note: pal_02-04 are copies of 01, and
                // pal_Team01-02 are broken if extracted with the main model's header info
                // (uses palette 3 instead of 1, 7 instead of 0).
                {
                    "Guardian_lod0",
                    new EntityMetadata("Guardian_lod0",
                        remove: "_lod0",
                        recolors: new List<string>()
                        {
                            "pal_01"
                        },
                        texture: true,
                        archive: "Guardian")
                },
                {
                    "Guardian_lod1",
                    new EntityMetadata("Guardian_lod1",
                        remove: "_lod1",
                        recolors: new List<string>()
                        {
                            "pal_01"
                        },
                        texture: true)
                },
                //{
                //    "Guardian_pal_Team01",
                //    new EntityMetadata("Guardian_pal_Team01", animation: false, texture: true)
                //},
                //{
                //    "Guardian_pal_Team02",
                //    new EntityMetadata("Guardian_pal_Team02", animation: false, texture: true)
                //},
                {
                    "Guardian_Stasis",
                    new EntityMetadata("Guardian_Stasis")
                },
                {
                    "gunSmoke",
                    new EntityMetadata("gunSmoke", archive: "common")
                },
                {
                    "Ice_Console",
                    new EntityMetadata("Ice_Console",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Monitor",
                    new EntityMetadata("Ice_Monitor",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Power",
                    new EntityMetadata("Ice_Power",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Scanner",
                    new EntityMetadata("Ice_Scanner",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ice_Switch",
                    new EntityMetadata("Ice_Switch",
                        share: @"models\IceEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "iceShard",
                    new EntityMetadata("iceShard", animation: false, archive: "common")
                },
                {
                    "iceWave",
                    new EntityMetadata("iceWave", archive: "common")
                },
                {
                    "items_base",
                    new EntityMetadata("items_base", animation: false, archive: "common")
                },
                {
                    "JumpPad_Alimbic",
                    new EntityMetadata("JumpPad_Alimbic")
                },
                {
                    "JumpPad_Beam",
                    new EntityMetadata("JumpPad_Beam")
                },
                {
                    "JumpPad_IceStation",
                    new EntityMetadata("JumpPad_IceStation")
                },
                {
                    "JumpPad_Ice",
                    new EntityMetadata("JumpPad_Ice")
                },
                {
                    "JumpPad_Lava",
                    new EntityMetadata("JumpPad_Lava")
                },
                {
                    "JumpPad",
                    new EntityMetadata("JumpPad")
                },
                {
                    "JumpPad_Station",
                    new EntityMetadata("JumpPad_Station")
                },
                {
                    "Kanden_lod0",
                    new EntityMetadata("Kanden_lod0",
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
                        archive: "Kanden")
                },
                {
                    "Kanden_lod1",
                    new EntityMetadata("Kanden_lod1",
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
                        texture: true)
                },
                {
                    "KandenAlt_lod0",
                    new EntityMetadata("KandenAlt_lod0",
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
                        archive: "Kanden",
                        recolorName: "Kanden")
                },
                {
                    "KandenAlt_TailBomb",
                    new EntityMetadata("KandenAlt_TailBomb",
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
                    new EntityMetadata("KandenGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localKanden")
                },
                {
                    "koth_data_flow",
                    new EntityMetadata("koth_data_flow", animation: false)
                },
                {
                    "koth_terminal",
                    new EntityMetadata("koth_terminal", animation: false)
                },
                {
                    "LavaDemon",
                    new EntityMetadata("LavaDemon",
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
                    new EntityMetadata("Lava_Console",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Monitor",
                    new EntityMetadata("Lava_Monitor",
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
                    new EntityMetadata("Lava_Power",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Scanner",
                    new EntityMetadata("Lava_Scanner",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Lava_Switch",
                    new EntityMetadata("Lava_Switch",
                        share: @"models\LavaEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "lines",
                    new EntityMetadata("lines", addToAnim: "_Idle", archive: "frontend2d")
                },
                {
                    "MoverTest",
                    new EntityMetadata("MoverTest")
                },
                {
                    "Nox_lod0",
                    new EntityMetadata("Nox_lod0",
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
                        archive: "Nox")
                },
                {
                    "Nox_lod1",
                    new EntityMetadata("Nox_lod1",
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
                        texture: true)
                },
                {
                    "NoxAlt_lod0",
                    new EntityMetadata("NoxAlt_lod0",
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
                        archive: "Nox",
                        recolorName: "Nox")
                },
                {
                    "NoxGun",
                    new EntityMetadata("NoxGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localNox")
                },
                {
                    "nox_ice",
                    new EntityMetadata("nox_ice",
                        modelPath: @"_archives\common\nox_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        })
                },
                {
                    "octolith_bounty_img",
                    new EntityMetadata("octolith_bounty_img", animation: false)
                },
                {
                    "octolith_ctf",
                    new EntityMetadata("octolith_ctf",
                        recolors: new List<string>()
                        {
                            "green_img",
                            "orange_img"
                        },
                        animation: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Octolith",
                    new EntityMetadata("Octolith")
                },
                {
                    "octolith_simple",
                    new EntityMetadata("octolith_simple", animation: false)
                },
                {
                    "particles",
                    new EntityMetadata("particles", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "particles2",
                    new EntityMetadata("particles2", animation: false, texture: true, archive: "effectsBase")
                },
                {
                    "PickUp_AmmoExp",
                    new EntityMetadata("PickUp_AmmoExp", animation: false)
                },
                {
                    "PickUp_EnergyExp",
                    new EntityMetadata("PickUp_EnergyExp")
                },
                {
                    "PickUp_MissileExp",
                    new EntityMetadata("PickUp_MissileExp")
                },
                {
                    "pick_ammo_green",
                    new EntityMetadata("pick_ammo_green", animation: false)
                },
                {
                    "pick_ammo_orange",
                    new EntityMetadata("pick_ammo_orange", animation: false)
                },
                {
                    "pick_dblDamage",
                    new EntityMetadata("pick_dblDamage", animation: false)
                },
                {
                    "pick_deathball",
                    new EntityMetadata("pick_deathball", animation: false)
                },
                {
                    "pick_health_A",
                    new EntityMetadata("pick_health_A", animation: false)
                },
                {
                    "pick_health_B",
                    new EntityMetadata("pick_health_B", animation: false)
                },
                {
                    "pick_health_C",
                    new EntityMetadata("pick_health_C", animation: false)
                },
                {
                    "pick_invis",
                    new EntityMetadata("pick_invis", animation: false)
                },
                {
                    "pick_wpn_all",
                    new EntityMetadata("pick_wpn_all", animation: false)
                },
                {
                    "pick_wpn_electro",
                    new EntityMetadata("pick_wpn_electro", animation: false)
                },
                {
                    "pick_wpn_ghostbuster",
                    new EntityMetadata("pick_wpn_ghostbuster", animation: false)
                },
                {
                    "pick_wpn_gorea",
                    new EntityMetadata("pick_wpn_gorea", animation: false)
                },
                {
                    "pick_wpn_jackhammer",
                    new EntityMetadata("pick_wpn_jackhammer", animation: false)
                },
                {
                    "pick_wpn_missile",
                    new EntityMetadata("pick_wpn_missile", animation: false)
                },
                {
                    "pick_wpn_mortar",
                    new EntityMetadata("pick_wpn_mortar", animation: false)
                },
                {
                    "pick_wpn_shotgun",
                    new EntityMetadata("pick_wpn_shotgun", animation: false)
                },
                {
                    "pick_wpn_snipergun",
                    new EntityMetadata("pick_wpn_snipergun", animation: false)
                },
                {
                    "pillar",
                    new EntityMetadata("pillar", animation: false, collision: true)
                },
                {
                    "pistonmp7",
                    new EntityMetadata("pistonmp7", animation: false, collision: true)
                },
                {
                    "piston_gorealand",
                    new EntityMetadata("piston_gorealand", animation: false, collision: true)
                },
                {
                    "PlantCarnivarous_Branched",
                    new EntityMetadata("PlantCarnivarous_Branched")
                },
                {
                    "PlantCarnivarous_PodLeaves",
                    new EntityMetadata("PlantCarnivarous_PodLeaves")
                },
                {
                    "PlantCarnivarous_Pod",
                    new EntityMetadata("PlantCarnivarous_Pod")
                },
                {
                    "PlantCarnivarous_Vine",
                    new EntityMetadata("PlantCarnivarous_Vine")
                },
                {
                    "platform",
                    new EntityMetadata("platform", animation: false, collision: true)
                },
                {
                    "Platform_Unit4_C1",
                    new EntityMetadata("Platform_Unit4_C1", animation: false, collision: true)
                },
                {
                    "PowerBomb",
                    new EntityMetadata("PowerBomb")
                },
                {
                    "Psychobit_Dead",
                    new EntityMetadata("Psychobit_Dead", animation: false)
                },
                {
                    "PsychoBit",
                    new EntityMetadata("PsychoBit",
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
                    new EntityMetadata("quads", animation: false)
                },
                {
                    "Ruins_Console",
                    new EntityMetadata("Ruins_Console",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Monitor",
                    new EntityMetadata("Ruins_Monitor",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Power",
                    new EntityMetadata("Ruins_Power",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Scanner",
                    new EntityMetadata("Ruins_Scanner",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Ruins_Switch",
                    new EntityMetadata("Ruins_Switch",
                        share: @"models\RuinsEquipTextureShare_img_Model.bin",
                        collision: true,
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "SamusShip",
                    new EntityMetadata("SamusShip", collision: true)
                },
                {
                    "Samus_lod0",
                    new EntityMetadata("Samus_lod0",
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
                        archive: "Samus")
                },
                {
                    "Samus_lod1",
                    new EntityMetadata("Samus_lod1",
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
                        texture: true)
                },
                {
                    "SamusAlt_lod0",
                    new EntityMetadata("SamusAlt_lod0",
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
                        animation: false,
                        texture: true,
                        archive: "Samus",
                        recolorName: "Samus")
                },
                {
                    "SamusGun",
                    new EntityMetadata("SamusGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localSamus")
                },
                {
                    "samus_ice",
                    new EntityMetadata("samus_ice",
                        modelPath: @"_archives\common\samus_ice_mdl_Model.bin",
                        animationPath: null,
                        collisionPath: null,
                        new List<RecolorMetadata>()
                        {
                            new RecolorMetadata("default",
                                modelPath: @"_archives\common\samus_ice_img_Model.bin",
                                texturePath: @"_archives\common\samus_ice_img_Model.bin",
                                palettePath: @"_archives\common\samus_ice_img_Model.bin")
                        })
                },
                {
                    "SecretSwitch",
                    new EntityMetadata("SecretSwitch",
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
                    new EntityMetadata("shriekbat")
                },
                {
                    "slots",
                    new EntityMetadata("slots", addToAnim: "_Idle", archive: "frontend2d")
                },
                {
                    "smasher",
                    new EntityMetadata("smasher", animation: false, collision: true)
                },
                {
                    "sniperBeam",
                    new EntityMetadata("sniperBeam", archive: "common")
                },
                {
                    "SniperTarget",
                    new EntityMetadata("SniperTarget")
                },
                {
                    "SphinkTick_lod0",
                    new EntityMetadata("SphinkTick_lod0", remove: "_lod0")
                },
                {
                    "Spire_lod0",
                    new EntityMetadata("Spire_lod0",
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
                        archive: "Spire")
                },
                {
                    "Spire_lod1",
                    new EntityMetadata("Spire_lod1",
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
                        texture: true)
                },
                {
                    "SpireAlt_lod0",
                    new EntityMetadata("SpireAlt_lod0",
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
                        archive: "Spire",
                        recolorName: "Spire")
                },
                {
                    "SpireGun",
                    new EntityMetadata("SpireGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localSpire")
                },
                {
                    "splashRing",
                    new EntityMetadata("splashRing")
                },
                {
                    "Switch",
                    new EntityMetadata("Switch",
                        animation: false,
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.Model)
                },
                {
                    "Sylux_lod0",
                    new EntityMetadata("Sylux_lod0",
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
                        archive: "Sylux")
                },
                {
                    "Sylux_lod1",
                    new EntityMetadata("Sylux_lod1",
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
                        texture: true)
                },
                {
                    "SyluxAlt_lod0",
                    new EntityMetadata("SyluxAlt_lod0",
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
                        archive: "Sylux",
                        recolorName: "Sylux")
                },
                {
                    "SyluxGun",
                    new EntityMetadata("SyluxGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localSylux")
                },
                {
                    "TearParticle",
                    new EntityMetadata("TearParticle", animation: false, texture: true)
                },
                {
                    "Teleporter",
                    new EntityMetadata("Teleporter",
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
                    new EntityMetadata("TeleporterSmall",
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
                    new EntityMetadata("TeleporterMP")
                },
                {
                    "Temroid_lod0",
                    new EntityMetadata("Temroid_lod0", remove: "_lod0", animation: false)
                },
                {
                    "ThinDoorLock",
                    new EntityMetadata("ThinDoorLock",
                        share: @"models\AlimbicTextureShare_img_Model.bin",
                        mdlSuffix: MdlSuffix.All)
                },
                {
                    "Trace_lod0",
                    new EntityMetadata("Trace_lod0",
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
                        archive: "Trace")
                },
                {
                    "Trace_lod1",
                    new EntityMetadata("Trace_lod1",
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
                        texture: true)
                },
                {
                    "TraceAlt_lod0",
                    new EntityMetadata("TraceAlt_lod0",
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
                        archive: "Trace",
                        recolorName: "Trace")
                },
                {
                    "TraceGun",
                    new EntityMetadata("TraceGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localTrace")
                },
                {
                    "trail",
                    new EntityMetadata("trail", animation: false, archive: "common")
                },
                {
                    "unit1_land_plat1",
                    new EntityMetadata("unit1_land_plat1", animation: false, collision: true)
                },
                {
                    "unit1_land_plat2",
                    new EntityMetadata("unit1_land_plat2", animation: false, collision: true)
                },
                {
                    "unit1_land_plat3",
                    new EntityMetadata("unit1_land_plat3", animation: false, collision: true)
                },
                {
                    "unit1_land_plat4",
                    new EntityMetadata("unit1_land_plat4", animation: false, collision: true)
                },
                {
                    "unit1_land_plat5",
                    new EntityMetadata("unit1_land_plat5", animation: false, collision: true)
                },
                {
                    "unit1_mover1",
                    new EntityMetadata("unit1_mover1", collision: true)
                },
                {
                    "unit1_mover2",
                    new EntityMetadata("unit1_mover2", animation: false, collision: true)
                },
                {
                    "unit2_c1_mover",
                    new EntityMetadata("unit2_c1_mover", animation: false, collision: true)
                },
                {
                    "unit2_c4_plat",
                    new EntityMetadata("unit2_c4_plat", animation: false, collision: true)
                },
                {
                    "unit2_land_elev",
                    new EntityMetadata("unit2_land_elev", animation: false, collision: true)
                },
                {
                    "unit2_mover1",
                    new EntityMetadata("unit2_mover1", animation: false, collision: true)
                },
                {
                    "unit3_brain",
                    new EntityMetadata("unit3_brain", collision: true)
                },
                {
                    "unit3_jar",
                    new EntityMetadata("unit3_jar")
                },
                {
                    "unit3_jartop",
                    new EntityMetadata("unit3_jartop")
                },
                {
                    "unit3_mover1",
                    new EntityMetadata("unit3_mover1", animation: false, collision: true)
                },
                {
                    "unit3_mover2",
                    new EntityMetadata("unit3_mover2", collision: true)
                },
                {
                    "unit3_pipe1",
                    new EntityMetadata("unit3_pipe1", collision: true)
                },
                {
                    "unit3_pipe2",
                    new EntityMetadata("unit3_pipe2", collision: true)
                },
                {
                    "Unit3_platform1",
                    new EntityMetadata("Unit3_platform1", collision: true)
                },
                {
                    "unit3_platform",
                    new EntityMetadata("unit3_platform", animation: false, collision: true)
                },
                {
                    "unit3_platform2",
                    new EntityMetadata("unit3_platform2", animation: false, collision: true)
                },
                {
                    "unit4_mover2",
                    new EntityMetadata("unit4_mover2", collision: true)
                },
                {
                    "unit4_mover3",
                    new EntityMetadata("unit4_mover3", animation: false, collision: true)
                },
                {
                    "unit4_mover4",
                    new EntityMetadata("unit4_mover4", animation: false, collision: true)
                },
                {
                    "unit4_platform1",
                    new EntityMetadata("unit4_platform1", animation: false, collision: true)
                },
                {
                    "unit4_tp1_artifact_wo",
                    new EntityMetadata("unit4_tp1_artifact_wo", animation: false, collision: true)
                },
                {
                    "unit4_tp2_artifact_wo",
                    new EntityMetadata("unit4_tp2_artifact_wo", animation: false, collision: true)
                },
                {
                    "WallSwitch",
                    new EntityMetadata("WallSwitch")
                },
                {
                    "warwasp_lod0",
                    new EntityMetadata("warwasp_lod0", remove: "_lod0")
                },
                {
                    "Weavel_lod0",
                    new EntityMetadata("Weavel_lod0",
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
                        archive: "Weavel")
                },
                {
                    "Weavel_lod1",
                    new EntityMetadata("Weavel_lod1",
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
                        texture: true)
                },
                {
                    "WeavelAlt_lod0",
                    new EntityMetadata("WeavelAlt_lod0",
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
                        archive: "Weavel",
                        recolorName: "Weavel")
                },
                {
                    "WeavelAlt_Turret_lod0",
                    new EntityMetadata("WeavelAlt_Turret_lod0",
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
                        archive: "Weavel",
                        recolorName: "Weavel")
                },
                {
                    "WeavelGun",
                    new EntityMetadata("WeavelGun",
                        recolors: new List<string>()
                        {
                            "img_01",
                            "img_02",
                            "img_03",
                            "img_04",
                            "img_Team01",
                            "img_Team02"
                        },
                        texture: true,
                        archive: "localWeavel")
                },
                {
                    "zoomer",
                    new EntityMetadata("zoomer")
                },
                // 2D images only, no mesh/dlist, probably just swapped in for other textures on models
                {
                    "doubleDamage_img",
                    new EntityMetadata("doubleDamage_img", animation: false, archive: "common")
                },
                // todo?: seemingly 2D images only, no polygons render even though they have a mesh/dlist
                {
                    "arcWelder",
                    new EntityMetadata("arcWelder", animation: false, archive: "common")
                },
                {
                    "electroTrail",
                    new EntityMetadata("electroTrail", animation: false, archive: "common")
                }
            };
    }
}
