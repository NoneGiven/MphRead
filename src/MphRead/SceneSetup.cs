using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Formats.Collision;

namespace MphRead
{
    public static class SceneSetup
    {
        // todo: artifact flags
        public static (RoomEntity, RoomMetadata, CollisionInfo, IReadOnlyList<EntityBase>, int)
            LoadRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
                BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1, Scene? scene = null)
        {
            (RoomMetadata? metadata, int roomId) = Metadata.GetRoomByName(name);
            int areaId = Metadata.GetAreaInfo(roomId);
            if (metadata == null)
            {
                throw new ProgramException("No room with this name is known.");
            }
            if (mode == GameMode.None)
            {
                mode = metadata.Multiplayer ? GameMode.Battle : GameMode.SinglePlayer;
            }
            if (playerCount < 1 || playerCount > 4)
            {
                if (mode == GameMode.SinglePlayer)
                {
                    playerCount = 1;
                }
                else
                {
                    playerCount = 2;
                }
            }
            if (entityLayerId < 0 || entityLayerId > 15)
            {
                if (mode == GameMode.SinglePlayer)
                {
                    // todo: finer state changes for target layer ID (forced fights);
                    // there are two doors with ID 3 in UNIT1_RM6, the rest are set in-game
                    entityLayerId = ((int)bossFlags >> 2 * areaId) & 3;
                }
                else
                {
                    entityLayerId = Metadata.GetMultiplayerEntityLayer(mode, playerCount);
                }
            }
            if (nodeLayerMask == 0)
            {
                if (mode == GameMode.SinglePlayer)
                {
                    if (metadata.NodeLayer > 0)
                    {
                        nodeLayerMask = nodeLayerMask & 0xC03F | (((1 << metadata.NodeLayer) & 0xFF) << 6);
                    }
                }
                else
                {
                    nodeLayerMask |= (int)NodeLayer.MultiplayerU;
                    if (playerCount <= 2)
                    {
                        nodeLayerMask |= (int)NodeLayer.MultiplayerLod0;
                    }
                    else
                    {
                        nodeLayerMask |= (int)NodeLayer.MultiplayerLod1;
                    }
                    if (mode == GameMode.Capture)
                    {
                        nodeLayerMask |= (int)NodeLayer.CaptureTheFlag;
                    }
                }
            }
            IReadOnlyList<EntityBase> entities = LoadEntities(metadata, areaId, entityLayerId, mode);
            CollisionInfo collision = Collision.ReadCollision(metadata.CollisionPath, metadata.FirstHunt || metadata.Hybrid, nodeLayerMask);
            // todo: once ReadCollision is filering things, we don't need to pass nodeLayerMask here or return it
            LoadResources(scene);
            var room = new RoomEntity(name, metadata, collision, nodeLayerMask);
            return (room, metadata, collision, entities, nodeLayerMask);
        }

        private static IReadOnlyList<EntityBase> LoadEntities(RoomMetadata metadata, int areaId, int layerId, GameMode mode)
        {
            var results = new List<EntityBase>();
            if (metadata.EntityPath == null)
            {
                return results;
            }
            // only FirstHunt is passed here, not Hybrid -- model/anim/col should be loaded from FH, and ent/node from MPH
            IReadOnlyList<Entity> entities = Read.GetEntities(metadata.EntityPath, layerId, metadata.FirstHunt);
            foreach (Entity entity in entities)
            {
                if (entity.Type == EntityType.Platform)
                {
                    results.Add(new PlatformEntity(((Entity<PlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhPlatform)
                {
                    results.Add(new FhPlatformEntity(((Entity<FhPlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Object)
                {
                    results.Add(new ObjectEntity(((Entity<ObjectEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PlayerSpawn || entity.Type == EntityType.FhPlayerSpawn)
                {
                    results.Add(new PlayerSpawnEntity(((Entity<PlayerSpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Door)
                {
                    results.Add(new DoorEntity(((Entity<DoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhDoor)
                {
                    results.Add(new FhDoorEntity(((Entity<FhDoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.ItemSpawn)
                {
                    results.Add(new ItemSpawnEntity(((Entity<ItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhItem)
                {
                    results.Add(new FhItemSpawnEntity(((Entity<FhItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.EnemySpawn)
                {
                    results.Add(new EnemySpawnEntity(((Entity<EnemyEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhEnemy)
                {
                    results.Add(new FhEnemySpawnEntity(((Entity<FhEnemyEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.TriggerVolume)
                {
                    results.Add(new TriggerVolumeEntity(((Entity<TriggerVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhTriggerVolume)
                {
                    results.Add(new FhTriggerVolumeEntity(((Entity<FhTriggerVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.AreaVolume)
                {
                    results.Add(new AreaVolumeEntity(((Entity<AreaVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhAreaVolume)
                {
                    results.Add(new FhAreaVolumeEntity(((Entity<FhAreaVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    results.Add(new JumpPadEntity(((Entity<JumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhJumpPad)
                {
                    results.Add(new FhJumpPadEntity(((Entity<FhJumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PointModule || entity.Type == EntityType.FhPointModule)
                {
                    results.Add(new PointModuleEntity(((Entity<PointModuleEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.MorphCamera)
                {
                    results.Add(new MorphCameraEntity(((Entity<MorphCameraEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhMorphCamera)
                {
                    results.Add(new FhMorphCameraEntity(((Entity<FhMorphCameraEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.OctolithFlag)
                {
                    results.Add(new OctolithFlagEntity(((Entity<OctolithFlagEntityData>)entity).Data, mode));
                }
                else if (entity.Type == EntityType.FlagBase)
                {
                    results.Add(new FlagBaseEntity(((Entity<FlagBaseEntityData>)entity).Data, mode));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    results.Add(new TeleporterEntity(((Entity<TeleporterEntityData>)entity).Data, areaId, mode != GameMode.SinglePlayer));
                }
                else if (entity.Type == EntityType.NodeDefense)
                {
                    results.Add(new NodeDefenseEntity(((Entity<NodeDefenseEntityData>)entity).Data, mode));
                }
                else if (entity.Type == EntityType.LightSource)
                {
                    results.Add(new LightSourceEntity(((Entity<LightSourceEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    results.Add(new ArtifactEntity(((Entity<ArtifactEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.CameraSequence)
                {
                    results.Add(new CameraSequenceEntity(((Entity<CameraSequenceEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    results.Add(new ForceFieldEntity(((Entity<ForceFieldEntityData>)entity).Data));
                }
                else
                {
                    throw new ProgramException($"Invalid entity type {entity.Type}");
                }
            }
            return results;
        }

        private static void LoadResources(Scene? scene)
        {
            // todo: this could also allocate effect lists and stuff, since we don't need those if there's no room
            // todo: sort this all out by game mode/etc. for what's actually needed
            // todo: add an assert if any loading occurs after room init (besides manual model/entity loading)
            if (scene != null)
            {
                LoadBombResources(scene);
                LoadBeamEffectResources(scene);
                LoadBeamProjectileResources(scene);
                LoadRoomResources(scene);
            }
        }

        public static void LoadHunterResources(Hunter hunter, Scene scene)
        {
            // todo: lods
            scene.LoadModel("doubleDamage_img");
            scene.LoadModel("alt_ice");
            scene.LoadModel(hunter == Hunter.Noxus || hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            foreach (string modelName in Metadata.HunterModels[hunter])
            {
                scene.LoadModel(modelName);
            }
            if (hunter == Hunter.Samus)
            {
                scene.LoadModel("trail");
            }
        }

        private static void LoadBombResources(Scene scene)
        {
            scene.LoadModel("KandenAlt_TailBomb");
            scene.LoadModel("arcWelder");
            scene.LoadModel("arcWelder1");
            scene.LoadEffect(9);
            scene.LoadEffect(113);
            scene.LoadEffect(119);
            scene.LoadEffect(129);
            scene.LoadEffect(145);
            scene.LoadEffect(146);
            scene.LoadEffect(149);
            scene.LoadEffect(150);
            scene.LoadEffect(151);
            scene.LoadEffect(152);
            scene.LoadEffect(153);
        }

        private static void LoadBeamEffectResources(Scene scene)
        {
            scene.LoadModel("iceWave");
            scene.LoadModel("sniperBeam");
            scene.LoadModel("cylBossLaserBurn");
        }

        private static void LoadBeamProjectileResources(Scene scene)
        {
            scene.LoadModel("iceShard");
            scene.LoadModel("energyBeam");
            scene.LoadModel("trail");
            scene.LoadModel("electroTrail");
            scene.LoadModel("arcWelder");
            scene.LoadEffect(57);
            scene.LoadEffect(58);
            scene.LoadEffect(59);
            scene.LoadEffect(60);
            scene.LoadEffect(61);
            scene.LoadEffect(62);
            scene.LoadEffect(63);
            scene.LoadEffect(78);
            scene.LoadEffect(85);
            scene.LoadEffect(86);
            scene.LoadEffect(92);
            scene.LoadEffect(98);
            scene.LoadEffect(99);
            scene.LoadEffect(100);
            scene.LoadEffect(121);
            scene.LoadEffect(122);
            scene.LoadEffect(123);
            scene.LoadEffect(124);
            scene.LoadEffect(125);
            scene.LoadEffect(126);
            scene.LoadEffect(130);
            scene.LoadEffect(134);
            scene.LoadEffect(137);
            scene.LoadEffect(140);
            scene.LoadEffect(141);
            scene.LoadEffect(142);
            scene.LoadEffect(171);
            scene.LoadEffect(211);
            scene.LoadEffect(237);
            scene.LoadEffect(238);
            scene.LoadEffect(246);
        }

        private static void LoadRoomResources(Scene scene)
        {
            scene.LoadEffect(1);
            scene.LoadEffect(2);
            scene.LoadEffect(5);
            scene.LoadEffect(6);
            scene.LoadEffect(7);
            scene.LoadEffect(8);
            scene.LoadEffect(11);
            scene.LoadEffect(12);
            scene.LoadEffect(13);
            scene.LoadEffect(14);
            scene.LoadEffect(15);
            scene.LoadEffect(16);
            scene.LoadEffect(17);
            scene.LoadEffect(18);
            scene.LoadEffect(19);
            scene.LoadEffect(20);
            scene.LoadEffect(21);
            scene.LoadEffect(22);
            scene.LoadEffect(23);
            scene.LoadEffect(24);
            scene.LoadEffect(25);
            scene.LoadEffect(26);
            scene.LoadEffect(27);
            scene.LoadEffect(28);
            scene.LoadEffect(31);
            scene.LoadEffect(33);
            scene.LoadEffect(99);
            scene.LoadEffect(115);
            scene.LoadEffect(154);
            scene.LoadEffect(155);
            scene.LoadEffect(156);
            scene.LoadEffect(157);
            scene.LoadEffect(158);
            scene.LoadEffect(159);
            scene.LoadEffect(160);
            scene.LoadEffect(161);
            scene.LoadEffect(173);
            scene.LoadEffect(190);
            scene.LoadEffect(191);
            scene.LoadEffect(192);
            scene.LoadEffect(231);
            scene.LoadEffect(239);
            // todo: lore
            scene.LoadModel(Read.GetSingleParticle(SingleType.Death).Model);
            scene.LoadModel(Read.GetSingleParticle(SingleType.Fuzzball).Model);
        }

        public static BeamProjectileEntity[] CreateBeamList(int size)
        {
            Debug.Assert(size > 0);
            var beams = new BeamProjectileEntity[size];
            for (int i = 0; i < size; i++)
            {
                beams[i] = new BeamProjectileEntity();
            }
            return beams;
        }
    }
}
