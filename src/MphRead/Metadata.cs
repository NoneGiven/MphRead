using System.Collections.Generic;
using System.Linq;

namespace MphRead
{
    public class LevelMetadata
    {
        // todo
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
            bool animation = false, string? animationPath = null, bool texture = false, MdlSuffix mdlSuffix = MdlSuffix.None)
        {
            Name = name;
            string suffix = "";
            if (mdlSuffix != MdlSuffix.None)
            {
                suffix = "_mdl";
            }
            ModelPath = $@"models\{name}{suffix}_Model.bin";
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
                AnimationPath = animationPath ?? $@"models\{name}{suffix}_Anim.bin";
            }
            var recolorList = new List<RecolorMetadata>();
            foreach (string recolor in recolors)
            {
                string recolorModel = $@"models\{name}_{recolor}_Model.bin";
                string texturePath = texture ? $@"models\{name}_{recolor}_Tex.bin" : recolorModel;
                recolorList.Add(new RecolorMetadata(recolor, recolorModel, texturePath));
            }
            Recolors = recolorList;
        }

        public EntityMetadata(string name, bool animation = true, bool collision = false,
            bool texture = false, string? share = null, MdlSuffix mdlSuffix = MdlSuffix.None)
        {
            Name = name;
            string suffix = "";
            if (mdlSuffix != MdlSuffix.None)
            {
                suffix = "_mdl";
            }
            ModelPath = $@"models\{name}{suffix}_Model.bin";
            if (mdlSuffix != MdlSuffix.All)
            {
                suffix = "";
            }
            if (animation)
            {
                AnimationPath = $@"models\{name}{suffix}_Anim.bin";
            }
            if (collision)
            {
                CollisionPath = $@"models\{name}{suffix}_Collision.bin";
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
        public static EntityMetadata? GetByName(string name)
        {
            if (EntityMetadata.TryGetValue(name, out EntityMetadata? metadata))
            {
                return metadata;
            }
            return null;
        }

        public static EntityMetadata? GetByPath(string path)
        {
            KeyValuePair<string, EntityMetadata> result = EntityMetadata.FirstOrDefault(r => r.Value.ModelPath == path);
            if (result.Key == null)
            {
                return null;
            }
            return result.Value;
        }

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
                    "MoverTest",
                    new EntityMetadata("MoverTest")
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
                    "smasher",
                    new EntityMetadata("smasher", animation: false, collision: true)
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
                    "zoomer",
                    new EntityMetadata("zoomer")
                }
            };
    }
}
