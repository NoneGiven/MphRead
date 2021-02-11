using System.Collections.Generic;
using MphRead.Entities;
using MphRead.Formats.Collision;

namespace MphRead
{
    public static class SceneSetup
    {
        // todo: artifact flags
        public static (RoomEntity, RoomMetadata, CollisionInfo, IReadOnlyList<EntityBase>, int) LoadRoom(string name,
            GameMode mode = GameMode.None, int playerCount = 0, BossFlags bossFlags = BossFlags.None,
            int nodeLayerMask = 0, int entityLayerId = -1)
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
                int count = results.Count;
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
    }
}
