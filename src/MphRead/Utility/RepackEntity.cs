using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MphRead.Editor;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Utility
{
    public static partial class Repack
    {
        public static byte[] RepackMphEntities(string room)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            Debug.Assert(meta.EntityPath != null);
            List<EntityEditorBase> entities = meta.FirstHunt ? GetFhEntities(meta.EntityPath) : GetEntities(meta.EntityPath);
            List<EntityEditorBase> converted = ConvertFhToMph(entities);
            return RepackEntities(converted);
        }

        public static byte[] RepackFhEntities(string room)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            Debug.Assert(meta.EntityPath != null);
            List<EntityEditorBase> entities = meta.FirstHunt ? GetFhEntities(meta.EntityPath) : GetEntities(meta.EntityPath);
            List<EntityEditorBase> converted = ConvertMphToFh(entities);
            return RepackFhEntities(converted);
        }

        public static byte[] TestEntityEdit()
        {
            RoomMetadata meta = Metadata.RoomMetadata["MP3 PROVING GROUND"];
            RoomMetadata meta2 = Metadata.RoomMetadata["MP12 SIC TRANSIT"];
            string? entityPath = meta.EntityPath;
            List<EntityEditorBase> entities;
            if (entityPath != null)
            {
                entities = meta.FirstHunt ? GetFhEntities(entityPath) : GetEntities(entityPath);
            }
            else
            {
                entityPath = $"{meta.Name}_ent.bin";
                entities = new List<EntityEditorBase>();
            }
            Debug.Assert(meta2.EntityPath != null);
            List<EntityEditorBase> entities2 = meta2.FirstHunt ? GetFhEntities(meta2.EntityPath) : GetEntities(meta2.EntityPath);
            foreach (EntityEditorBase entity in entities)
            {
                entity.NodeName = "rmMain";
            }
            //short id = 0;
            //foreach (EntityEditorBase entity in entities2.Where(e => e.Type == EntityType.PlayerSpawn))
            //{
            //    entity.NodeName = "rmMain";
            //    entity.LayerMask = 0xFFFF;
            //    entity.Id = id++;
            //    entities.Add(entity);
            //}
            byte[] bytes = meta.FirstHunt ? RepackFhEntities(entities) : RepackEntities(entities);
            string path = Path.Combine(Paths.Export, "_pack", Path.GetFileName(entityPath));
            File.WriteAllBytes(path, bytes);
            Nop();
            return bytes;
        }

        public static void TestEntities()
        {
            foreach (RoomMetadata meta in Metadata.RoomMetadata.Values)
            {
                if (meta.EntityPath == null)
                {
                    continue;
                }
                if (meta.FirstHunt) // hybrid uses MPH entities
                {
                    IReadOnlyList<EntityEditorBase> entities = GetFhEntities(meta.EntityPath);
                    byte[] bytes = RepackFhEntities(entities);
                    byte[] fileBytes = File.ReadAllBytes(Path.Combine(Paths.FhFileSystem, meta.EntityPath));
                    CompareFhEntities(bytes, fileBytes);
                    Nop();
                }
                else
                {
                    IReadOnlyList<EntityEditorBase> entities = GetEntities(meta.EntityPath);
                    byte[] bytes = RepackEntities(entities);
                    byte[] fileBytes = File.ReadAllBytes(Path.Combine(Paths.FileSystem, meta.EntityPath));
                    CompareEntities(bytes, fileBytes);
                    Nop();
                }
            }
            Nop();
        }

        private static List<EntityEditorBase> GetFhEntities(string path)
        {
            var entities = new List<EntityEditorBase>();
            foreach (Entity entity in Read.GetEntities(path, layerId: -1, firstHunt: true))
            {
                if (entity.Type == EntityType.FhPlatform)
                {
                    entities.Add(new FhPlatformEntityEditor(entity, ((Entity<FhPlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhPlayerSpawn)
                {
                    entities.Add(new PlayerSpawnEntityEditor(entity, ((Entity<PlayerSpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhDoor)
                {
                    entities.Add(new FhDoorEntityEditor(entity, ((Entity<FhDoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhItemSpawn)
                {
                    entities.Add(new FhItemSpawnEntityEditor(entity, ((Entity<FhItemSpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhEnemySpawn)
                {
                    entities.Add(new FhEnemySpawnEntityEditor(entity, ((Entity<FhEnemySpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhTriggerVolume)
                {
                    entities.Add(new FhTriggerVolumeEntityEditor(entity, ((Entity<FhTriggerVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhAreaVolume)
                {
                    entities.Add(new FhAreaVolumeEntityEditor(entity, ((Entity<FhAreaVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhJumpPad)
                {
                    entities.Add(new FhJumpPadEntityEditor(entity, ((Entity<FhJumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhPointModule)
                {
                    entities.Add(new PointModuleEntityEditor(entity, ((Entity<PointModuleEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhMorphCamera)
                {
                    entities.Add(new MorphCameraEntityEditor(entity, ((Entity<FhMorphCameraEntityData>)entity).Data));
                }
            }
            return entities;
        }

        private static List<EntityEditorBase> GetEntities(string path)
        {
            var entities = new List<EntityEditorBase>();
            foreach (Entity entity in Read.GetEntities(path, layerId: -1, firstHunt: false))
            {
                if (entity.Type == EntityType.Platform)
                {
                    entities.Add(new PlatformEntityEditor(entity, ((Entity<PlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Object)
                {
                    entities.Add(new ObjectEntityEditor(entity, ((Entity<ObjectEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PlayerSpawn)
                {
                    entities.Add(new PlayerSpawnEntityEditor(entity, ((Entity<PlayerSpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Door)
                {
                    entities.Add(new DoorEntityEditor(entity, ((Entity<DoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.ItemSpawn)
                {
                    entities.Add(new ItemSpawnEntityEditor(entity, ((Entity<ItemSpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.EnemySpawn)
                {
                    entities.Add(new EnemySpawnEntityEditor(entity, ((Entity<EnemySpawnEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.TriggerVolume)
                {
                    entities.Add(new TriggerVolumeEntityEditor(entity, ((Entity<TriggerVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.AreaVolume)
                {
                    entities.Add(new AreaVolumeEntityEditor(entity, ((Entity<AreaVolumeEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    entities.Add(new JumpPadEntityEditor(entity, ((Entity<JumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PointModule)
                {
                    entities.Add(new PointModuleEntityEditor(entity, ((Entity<PointModuleEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.MorphCamera)
                {
                    entities.Add(new MorphCameraEntityEditor(entity, ((Entity<MorphCameraEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.OctolithFlag)
                {
                    entities.Add(new OctolithFlagEntityEditor(entity, ((Entity<OctolithFlagEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FlagBase)
                {
                    entities.Add(new FlagBaseEntityEditor(entity, ((Entity<FlagBaseEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    entities.Add(new TeleporterEntityEditor(entity, ((Entity<TeleporterEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.NodeDefense)
                {
                    entities.Add(new NodeDefenseEntityEditor(entity, ((Entity<NodeDefenseEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.LightSource)
                {
                    entities.Add(new LightSourceEntityEditor(entity, ((Entity<LightSourceEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    entities.Add(new ArtifactEntityEditor(entity, ((Entity<ArtifactEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.CameraSequence)
                {
                    entities.Add(new CameraSequenceEntityEditor(entity, ((Entity<CameraSequenceEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    entities.Add(new ForceFieldEntityEditor(entity, ((Entity<ForceFieldEntityData>)entity).Data));
                }
            }
            return entities;
        }

        private static List<EntityEditorBase> ConvertFhToMph(List<EntityEditorBase> entities)
        {
            static Message GetMessage(FhMessage message)
            {
                return message switch
                {
                    FhMessage.None => Message.None,
                    FhMessage.Activate => Message.Activate,
                    FhMessage.Destroyed => Message.Destroyed,
                    FhMessage.Damage => Message.Damage,
                    FhMessage.Trigger => Message.Trigger,
                    FhMessage.Gravity => Message.Gravity,
                    FhMessage.Unlock => Message.Unlock,
                    FhMessage.SetActive => Message.SetActive,
                    FhMessage.Complete => Message.Complete,
                    FhMessage.Impact => Message.Impact,
                    FhMessage.Death => Message.Death,
                    FhMessage.Unknown21 => Message.Unknown22,
                    _ => Message.UnlockOubliette
                };
            }
            static TriggerFlags GetFlags(FhTriggerFlags fhFlags, FhTriggerType subtype)
            {
                TriggerFlags flags = TriggerFlags.None;
                if (subtype != FhTriggerType.Threshold)
                {
                    if (fhFlags.HasFlag(FhTriggerFlags.Beam))
                    {
                        flags |= TriggerFlags.PowerBeam;
                        flags |= TriggerFlags.VoltDriver;
                        flags |= TriggerFlags.Missile;
                        flags |= TriggerFlags.Battlehammer;
                        flags |= TriggerFlags.Imperialist;
                        flags |= TriggerFlags.Judicator;
                        flags |= TriggerFlags.Magmaul;
                        flags |= TriggerFlags.ShockCoil;
                    }
                    if (fhFlags.HasFlag(FhTriggerFlags.PlayerBiped))
                    {
                        flags |= TriggerFlags.PlayerBiped;
                    }
                    if (fhFlags.HasFlag(FhTriggerFlags.PlayerAlt))
                    {
                        flags |= TriggerFlags.PlayerAlt;
                    }
                }
                return flags;
            }
            var converted = new List<EntityEditorBase>();
            foreach (EntityEditorBase entity in entities)
            {
                if (entity is FhPlatformEntityEditor platform)
                {
                    // GroupId, Unused2C, and Volume are ignored
                    var mphPlatform = new PlatformEntityEditor()
                    {
                        Id = platform.Id,
                        Active = true,
                        BackwardSpeed = platform.Speed / 2f,
                        BeamHitMessage = Message.None,
                        BeamHitMsgParam1 = 0,
                        BeamHitMsgParam2 = 0,
                        BeamHitMsgTarget = 0xFFFF,
                        BeamId = 0,
                        BeamInterval = 10,
                        BeamOnIntervals = 1,
                        BeamSpawnDir = new Vector3(6, 0, 0),
                        BeamSpawnPos = Vector3.Zero,
                        ContactDamage = 1,
                        DamageEffectId = 0,
                        DeadEffectId = 0,
                        DeadMessage = Message.None,
                        DeadMsgParam1 = 0,
                        DeadMsgParam2 = 0,
                        DeadMsgTarget = 0xFFFF,
                        Delay = platform.Delay,
                        Effectiveness = 1,
                        Facing = platform.Facing,
                        Flags = PlatformFlags.Bit15,
                        ForCutscene = false,
                        ForwardSpeed = platform.Speed,
                        Health = 100,
                        ItemChance = 100,
                        ItemType = ItemType.None,
                        LayerMask = 0xFFFF,
                        LifetimeMessage1 = Message.None,
                        LifetimeMessage2 = Message.None,
                        LifetimeMessage3 = Message.None,
                        LifetimeMessage4 = Message.None,
                        LifetimeMsg1Index = 255,
                        LifetimeMsg1Param1 = 0,
                        LifetimeMsg1Param2 = 0,
                        LifetimeMsg1Target = -1,
                        LifetimeMsg2Index = 255,
                        LifetimeMsg2Param1 = 0,
                        LifetimeMsg2Param2 = 0,
                        LifetimeMsg2Target = -1,
                        LifetimeMsg3Index = 255,
                        LifetimeMsg3Param1 = 0,
                        LifetimeMsg3Param2 = 0,
                        LifetimeMsg3Target = -1,
                        LifetimeMsg4Index = 255,
                        LifetimeMsg4Param1 = 0,
                        LifetimeMsg4Param2 = 0,
                        LifetimeMsg4Target = -1,
                        ModelId = 1,
                        MovementType = 0,
                        NoPort = platform.NoPortal == 0 ? 0u : 1u,
                        NodeName = platform.NodeName,
                        ParentId = -1,
                        PlayerColMessage = Message.None,
                        PlayerColMsgParam1 = 0,
                        PlayerColMsgParam2 = 0,
                        PlayerColMsgTarget = 0xFFFF,
                        PortalName = platform.PortalName.Trim(),
                        Position = platform.Position,
                        PositionCount = platform.PositionCount,
                        PositionOffset = Vector3.Zero,
                        ResistEffectId = 0,
                        ReverseType = 0,
                        ScanData1 = 0,
                        ScanData2 = 0,
                        ScanMessage = Message.None,
                        ScanMsgTarget = -1,
                        Unused1D0 = 0,
                        Unused1D4 = UInt32.MaxValue,
                        Up = platform.Up
                    };
                    for (int i = 0; i < platform.PositionCount; i++)
                    {
                        mphPlatform.Positions.Add(platform.Positions[i]);
                    }
                    for (int i = 0; i < 10 - platform.PositionCount; i++)
                    {
                        mphPlatform.Positions.Add(Vector3.Zero);
                    }
                    Debug.Assert(mphPlatform.Positions.Count == 10);
                    for (int i = 0; i < 10; i++)
                    {
                        mphPlatform.Rotations.Add(Vector4.UnitW);
                    }
                    Debug.Assert(mphPlatform.Rotations.Count == 10);
                    converted.Add(mphPlatform);
                }
                else if (entity is FhDoorEntityEditor door)
                {
                    converted.Add(new DoorEntityEditor()
                    {
                        Id = door.Id,
                        ConnectorId = 255,
                        DoorNodeName = "",
                        EntityFilename = " ",
                        Facing = door.Facing,
                        Field42 = 255,
                        Field43 = 255,
                        Flags = (byte)door.Flags,
                        LayerMask = 0xFFFF,
                        ModelId = 0,
                        NodeName = door.NodeName,
                        PaletteId = 9,
                        Position = door.Position,
                        RoomName = door.RoomName,
                        TargetLayerId = 255,
                        Up = door.Up
                    });
                }
                else if (entity is FhItemSpawnEntityEditor itemSpawn)
                {
                    ItemType itemType = itemSpawn.ItemType switch
                    {
                        FhItemType.AmmoSmall => ItemType.UASmall,
                        FhItemType.AmmoBig => ItemType.UABig,
                        FhItemType.HealthSmall => ItemType.HealthSmall,
                        FhItemType.HealthBig => ItemType.HealthBig,
                        FhItemType.DoubleDamage => ItemType.DoubleDamage,
                        FhItemType.ElectroLob => ItemType.VoltDriver,
                        FhItemType.Missile => ItemType.MissileBig,
                        _ => ItemType.None // can't convert PickMorphBall
                    };
                    if (itemType == ItemType.None)
                    {
                        Console.WriteLine($"FH to MPH: Skipping item spawn entity ID {entity.Id} with item type {itemSpawn.ItemType}.");
                        continue;
                    }
                    // Field2C is ignored
                    converted.Add(new ItemSpawnEntityEditor()
                    {
                        Id = itemSpawn.Id,
                        AlwaysActive = false,
                        CollectedMessage = Message.None,
                        CollectedMsgParam1 = 0,
                        CollectedMsgParam2 = 0,
                        Enabled = true,
                        Facing = itemSpawn.Facing,
                        HasBase = false,
                        ItemType = itemType,
                        LayerMask = 0xFFFF,
                        MaxSpawnCount = itemSpawn.SpawnLimit,
                        NodeName = itemSpawn.NodeName,
                        ParentId = 0xFFFF,
                        Position = itemSpawn.Position,
                        SomeEntityId = -1,
                        SpawnDelay = 0,
                        SpawnInterval = itemSpawn.CooldownTime,
                        Up = itemSpawn.Up
                    });
                }
                else if (entity is FhEnemySpawnEntityEditor enemySpawn)
                {
                    // EndFrame is ignored
                    var mphSpawn = new EnemySpawnEntityEditor()
                    {
                        Id = enemySpawn.Id,
                        Active = true,
                        ActiveDistance = 30,
                        AlwaysActive = true,
                        CooldownTime = enemySpawn.Cooldown,
                        EnemyType = (EnemyType)enemySpawn.EnemyType,
                        EntityId1 = enemySpawn.ParentId,
                        EntityId2 = -1,
                        EntityId3 = -1,
                        Facing = enemySpawn.Facing,
                        Field1CC = 143360,
                        InitialCooldown = 0,
                        ItemChance = 100,
                        ItemType = ItemType.None,
                        LayerMask = 0xFFFF,
                        Message1 = GetMessage(enemySpawn.EmptyMessage),
                        Message2 = Message.None,
                        Message3 = Message.None,
                        NodeName = enemySpawn.NodeName,
                        Position = enemySpawn.Position,
                        LinkedEntityId = -1,
                        SpawnCount = enemySpawn.SpawnCount,
                        SpawnLimit = enemySpawn.SpawnLimit,
                        SpawnTotal = enemySpawn.SpawnTotal,
                        SpawnNodeName = enemySpawn.SpawnNodeName,
                        SpawnerModel = 0,
                        Up = enemySpawn.Up
                    };
                    if (enemySpawn.EnemyType == FhEnemyType.Metroid || enemySpawn.EnemyType == FhEnemyType.Mochtroid1)
                    {
                        mphSpawn.Volume0 = new CollisionVolume(Vector3.Zero, 1);
                        mphSpawn.Volume1 = enemySpawn.Box;
                        mphSpawn.Facing = Vector3.UnitZ;
                        mphSpawn.Position = Vector3.Zero;
                        mphSpawn.Unknown00 = 0;
                        mphSpawn.Unknown01 = 0;
                    }
                    else if (enemySpawn.EnemyType == FhEnemyType.Mochtroid2 || enemySpawn.EnemyType == FhEnemyType.Mochtroid3
                        || enemySpawn.EnemyType == FhEnemyType.Mochtroid4)
                    {
                        mphSpawn.Volume0 = new CollisionVolume(Vector3.Zero, 1);
                        mphSpawn.Volume1 = enemySpawn.Cylinder;
                        mphSpawn.Position = Vector3.Zero;
                        mphSpawn.Unknown00 = 0;
                        mphSpawn.Unknown01 = 0;
                    }
                    else if (enemySpawn.EnemyType == FhEnemyType.Zoomer)
                    {
                        mphSpawn.Volume0 = new CollisionVolume(Vector3.Zero, 1);
                        mphSpawn.Volume1 = enemySpawn.Sphere;
                    }
                    else
                    {
                        // no need to convert war wasp
                        throw new ProgramException($"Invalid FH enemy type {enemySpawn.EnemyType}");
                    }
                    converted.Add(mphSpawn);
                }
                else if (entity is FhTriggerVolumeEntityEditor trigger)
                {
                    var volume = new CollisionVolume(Vector3.Zero, 1);
                    TriggerFlags flags = GetFlags(trigger.TriggerFlags, trigger.Subtype);
                    flags |= TriggerFlags.IncludeBots;
                    TriggerType subtype = TriggerType.Normal;
                    if (trigger.Subtype == FhTriggerType.Threshold)
                    {
                        subtype = TriggerType.Threshold;
                    }
                    else
                    {
                        volume = trigger.Subtype switch
                        {
                            FhTriggerType.Box => trigger.Box,
                            FhTriggerType.Cylinder => trigger.Cylinder,
                            FhTriggerType.Sphere => trigger.Sphere,
                            _ => throw new ProgramException($"Invalid FH trigger type {trigger.Subtype}")
                        };
                    }
                    Message childMsg = GetMessage(trigger.ChildMessage);
                    Message parentMsg = GetMessage(trigger.ParentMessage);
                    if (childMsg == Message.UnlockOubliette)
                    {
                        Console.WriteLine($"FH to MPH: Skipping trigger entity ID {entity.Id} with child message {trigger.ChildMessage}.");
                        continue;
                    }
                    if (parentMsg == Message.UnlockOubliette)
                    {
                        Console.WriteLine($"FH to MPH: Skipping trigger entity ID {entity.Id} with parent message {trigger.ParentMessage}.");
                    }
                    converted.Add(new TriggerVolumeEntityEditor()
                    {
                        Id = trigger.Id,
                        Active = true,
                        AlwaysActive = false,
                        CheckDelay = 0,
                        ChildId = trigger.ChildId,
                        ChildMessage = childMsg,
                        ChildMsgParam1 = trigger.ChildMsgParam1,
                        ChildMsgParam2 = 0,
                        DeactivateAfterUse = trigger.OneUse != 0,
                        Facing = trigger.Facing,
                        LayerMask = 0xFFFF,
                        NodeName = trigger.NodeName,
                        ParentId = trigger.ParentId,
                        ParentMessage = parentMsg,
                        ParentMsgParam1 = trigger.ParentMsgParam1,
                        ParentMsgParam2 = 0,
                        Position = trigger.Position,
                        RepeatDelay = trigger.Cooldown,
                        RequiredStateBit = 0,
                        Subtype = subtype,
                        TriggerFlags = flags,
                        TriggerThreshold = trigger.Threshold,
                        Up = trigger.Up,
                        Volume = volume,
                    });
                }
                else if (entity is FhAreaVolumeEntityEditor areaVolume)
                {
                    TriggerFlags flags = GetFlags(areaVolume.TriggerFlags, areaVolume.Subtype);
                    CollisionVolume volume = areaVolume.Subtype switch
                    {
                        FhTriggerType.Box => areaVolume.Box,
                        FhTriggerType.Cylinder => areaVolume.Cylinder,
                        FhTriggerType.Sphere => areaVolume.Sphere,
                        _ => throw new ProgramException($"Invalid FH area volume type {areaVolume.Subtype}")
                    };
                    Message insideMsg = GetMessage(areaVolume.InsideMessage);
                    Message exitMsg = GetMessage(areaVolume.ExitMessage);
                    if (insideMsg == Message.UnlockOubliette)
                    {
                        Console.WriteLine($"FH to MPH: Skipping area volume entity ID {entity.Id} with inside message {areaVolume.InsideMessage}.");
                        continue;
                    }
                    if (exitMsg == Message.UnlockOubliette)
                    {
                        Console.WriteLine($"FH to MPH: Skipping area volume entity ID {entity.Id} with exit message {areaVolume.ExitMessage}.");
                    }
                    converted.Add(new AreaVolumeEntityEditor()
                    {
                        Id = areaVolume.Id,
                        Active = true,
                        AllowMultiple = false,
                        AlwaysActive = false,
                        ChildId = -1,
                        Cooldown = areaVolume.Cooldown,
                        ExitMessage = exitMsg,
                        ExitMsgParam1 = areaVolume.ExitMsgParam1,
                        ExitMsgParam2 = 0,
                        Facing = areaVolume.Facing,
                        InsideMessage = insideMsg,
                        InsideMsgParam1 = areaVolume.InsideMsgParam1,
                        InsideMsgParam2 = 0,
                        LayerMask = 0xFFFF,
                        MessageDelay = 1,
                        NodeName = areaVolume.NodeName,
                        ParentId = -1,
                        Position = areaVolume.Position,
                        Priority = 0,
                        TriggerFlags = flags,
                        Unused6A = 0,
                        Up = areaVolume.Up,
                        Volume = volume,
                    });
                }
                else if (entity is FhJumpPadEntityEditor jumpPad)
                {
                    TriggerFlags flags = GetFlags(jumpPad.TriggerFlags, jumpPad.VolumeType);
                    CollisionVolume volume = jumpPad.VolumeType switch
                    {
                        FhTriggerType.Box => jumpPad.Box,
                        FhTriggerType.Cylinder => jumpPad.Cylinder,
                        FhTriggerType.Sphere => jumpPad.Sphere,
                        _ => throw new ProgramException($"Invalid FH jump pad volume type {jumpPad.VolumeType}")
                    };
                    // FH beam vectors are absolute, so we need to make it relative to the parent transform
                    var transform = new Matrix3(EntityBase.GetTransformMatrix(jumpPad.Facing, jumpPad.Up));
                    Vector3 beamVector = jumpPad.BeamVector;
                    if (transform != Matrix3.Identity)
                    {
                        beamVector *= transform.Inverted();
                    }
                    //BeamType is ignored
                    converted.Add(new JumpPadEntityEditor()
                    {
                        Id = jumpPad.Id,
                        Active = true,
                        BeamVector = beamVector,
                        ControlLockTime = (ushort)jumpPad.ControlLockTime,
                        CooldownTime = (ushort)jumpPad.CooldownTime,
                        Facing = jumpPad.Facing,
                        TriggerFlags = flags,
                        LayerMask = 0xFFFF,
                        ModelId = 0,
                        NodeName = jumpPad.NodeName,
                        ParentId = 0xFFFF,
                        Position = jumpPad.Position,
                        Speed = jumpPad.Speed,
                        Unused28 = 0,
                        Up = jumpPad.Up,
                        Volume = volume
                    });
                }
                else if (entity is PlayerSpawnEntityEditor || entity is MorphCameraEntityEditor)
                {
                    entity.Type = entity.Type switch
                    {
                        EntityType.FhPlayerSpawn => EntityType.PlayerSpawn,
                        EntityType.FhMorphCamera => EntityType.MorphCamera,
                        _ => throw new InvalidOperationException()
                    };
                    entity.LayerMask = 0xFFFF;
                    converted.Add(entity);
                }
                else
                {
                    // point modules won't load in MPH
                    Console.WriteLine($"FH to MPH: Skipping entity ID {entity.Id} of type {entity.Type}.");
                }
            }
            IEnumerable<PlayerSpawnEntityEditor> playerSpawns = converted
                .Where(c => c.Type == EntityType.PlayerSpawn).Select(c => (PlayerSpawnEntityEditor)c);
            if (!playerSpawns.Any())
            {
                Console.WriteLine("FH to MPH: Warning: No player spawn entities are present.");
            }
            else if (!playerSpawns.Any(p => p.Active))
            {
                playerSpawns.First().Active = true;
            }
            return converted;
        }

        private static List<EntityEditorBase> ConvertMphToFh(List<EntityEditorBase> entities)
        {
            static FhMessage GetMessage(Message message)
            {
                return message switch
                {
                    Message.None => FhMessage.None,
                    Message.Activate => FhMessage.Activate,
                    Message.Destroyed => FhMessage.Destroyed,
                    Message.Damage => FhMessage.Damage,
                    Message.Trigger => FhMessage.Trigger,
                    Message.Gravity => FhMessage.Gravity,
                    Message.Unlock => FhMessage.Unlock,
                    Message.SetActive => FhMessage.SetActive,
                    Message.Complete => FhMessage.Complete,
                    Message.Impact => FhMessage.Impact,
                    Message.Death => FhMessage.Death,
                    Message.Unknown22 => FhMessage.Unknown21,
                    _ => (FhMessage)255
                };
            }
            static FhTriggerFlags GetFlags(TriggerFlags mphFlags, TriggerType subtype)
            {
                FhTriggerFlags flags = FhTriggerFlags.None;
                if (subtype == TriggerType.Normal)
                {
                    if (mphFlags.HasFlag(TriggerFlags.PowerBeam) || mphFlags.HasFlag(TriggerFlags.VoltDriver)
                        || mphFlags.HasFlag(TriggerFlags.Missile) || mphFlags.HasFlag(TriggerFlags.Battlehammer)
                        || mphFlags.HasFlag(TriggerFlags.Imperialist) || mphFlags.HasFlag(TriggerFlags.Judicator)
                        || mphFlags.HasFlag(TriggerFlags.ShockCoil) || mphFlags.HasFlag(TriggerFlags.ShockCoil))
                    {
                        flags |= FhTriggerFlags.Beam;
                    }
                    if (mphFlags.HasFlag(TriggerFlags.PlayerBiped))
                    {
                        flags |= FhTriggerFlags.PlayerBiped;
                    }
                    if (mphFlags.HasFlag(TriggerFlags.PlayerAlt))
                    {
                        flags |= FhTriggerFlags.PlayerAlt;
                    }
                }
                return flags;
            }
            var converted = new List<EntityEditorBase>();
            foreach (EntityEditorBase entity in entities)
            {
                if (entity is PlatformEntityEditor platform)
                {
                    if (platform.PositionCount > 8)
                    {
                        Console.WriteLine($"MPH to FH: Skipping platform entity ID {entity.Id} with more than 8 positions.");
                        continue;
                    }
                    while (platform.Positions.Count > 8)
                    {
                        platform.Positions.RemoveAt(platform.Positions.Count - 1);
                    }
                    while (platform.Rotations.Count > 8)
                    {
                        platform.Rotations.RemoveAt(platform.Rotations.Count - 1);
                    }
                    if (platform.Rotations.Take(platform.PositionCount).Distinct().Count() > 1)
                    {
                        Console.WriteLine($"MPH to FH: Warning: Platform entity ID {entity.Id} has multiple rotation values.");
                    }
                    converted.Add(new FhPlatformEntityEditor()
                    {
                        Id = platform.Id,
                        Delay = platform.Delay,
                        Facing = platform.Facing,
                        GroupId = 0,
                        NodeName = platform.NodeName,
                        NoPortal = platform.NoPort,
                        PortalName = platform.PortalName,
                        Position = platform.Position,
                        PositionCount = (byte)platform.PositionCount,
                        Positions = platform.Positions,
                        Speed = platform.ForwardSpeed,
                        Up = platform.Up,
                        Volume = new CollisionVolume(Vector3.Zero, 1)
                    });
                }
                else if (entity is DoorEntityEditor door)
                {
                    converted.Add(new FhDoorEntityEditor()
                    {
                        Id = door.Id,
                        Facing = door.Facing,
                        Flags = door.Flags,
                        ModelId = 0,
                        NodeName = door.NodeName,
                        Position = door.Position,
                        RoomName = door.RoomName,
                        Up = door.Up
                    });
                }
                else if (entity is ItemSpawnEntityEditor itemSpawn)
                {
                    FhItemType itemType = itemSpawn.ItemType switch
                    {
                        ItemType.AffinityWeapon => FhItemType.AmmoBig,
                        ItemType.Battlehammer => FhItemType.ElectroLob,
                        ItemType.DoubleDamage => FhItemType.DoubleDamage,
                        ItemType.HealthBig => FhItemType.HealthBig,
                        ItemType.HealthMedium => FhItemType.HealthSmall,
                        ItemType.HealthSmall => FhItemType.HealthSmall,
                        ItemType.Imperialist => FhItemType.ElectroLob,
                        ItemType.Judicator => FhItemType.ElectroLob,
                        ItemType.Magmaul => FhItemType.ElectroLob,
                        ItemType.MissileBig => FhItemType.Missile,
                        ItemType.MissileSmall => FhItemType.Missile,
                        ItemType.ShockCoil => FhItemType.ElectroLob,
                        ItemType.UABig => FhItemType.AmmoBig,
                        ItemType.UASmall => FhItemType.AmmoSmall,
                        ItemType.VoltDriver => FhItemType.ElectroLob,
                        ItemType.Cloak => FhItemType.DoubleDamage,
                        ItemType.Deathalt => FhItemType.DoubleDamage,
                        ItemType.EnergyTank => FhItemType.HealthBig,
                        ItemType.MissileExpansion => FhItemType.Missile,
                        ItemType.OmegaCannon => FhItemType.AmmoBig,
                        ItemType.UAExpansion => FhItemType.AmmoBig,
                        _ => FhItemType.None
                    };
                    converted.Add(new FhItemSpawnEntityEditor()
                    {
                        Id = itemSpawn.Id,
                        CooldownTime = itemSpawn.SpawnInterval,
                        Facing = itemSpawn.Facing,
                        Unused2C = 0,
                        ItemType = itemType,
                        NodeName = itemSpawn.NodeName,
                        Position = itemSpawn.Position,
                        SpawnLimit = itemSpawn.MaxSpawnCount,
                        Up = itemSpawn.Up
                    });
                }
                else if (entity is EnemySpawnEntityEditor enemySpawn)
                {
                    if (!_validEnemiesFh.Contains(enemySpawn.EnemyType))
                    {
                        Console.WriteLine($"MPH to FH: Skipping enemy spawn entity ID {entity.Id} with enemy type {enemySpawn.EnemyType}.");
                        continue;
                    }
                    FhEnemyType enemyType = enemySpawn.EnemyType switch
                    {
                        EnemyType.WarWasp => FhEnemyType.WarWasp,
                        EnemyType.BarbedWarWasp => FhEnemyType.WarWasp,
                        EnemyType.Zoomer => FhEnemyType.Zoomer,
                        EnemyType.Geemer => FhEnemyType.Zoomer,
                        EnemyType.Temroid => FhEnemyType.Metroid,
                        EnemyType.Petrasyl1 => FhEnemyType.Mochtroid1,
                        EnemyType.Petrasyl2 => FhEnemyType.Mochtroid2,
                        EnemyType.Petrasyl3 => FhEnemyType.Mochtroid3,
                        EnemyType.Petrasyl4 => FhEnemyType.Mochtroid4,
                        _ => throw new InvalidOperationException()
                    };
                    var fhSpawn = new FhEnemySpawnEntityEditor()
                    {
                        Id = enemySpawn.Id,
                        Box = enemySpawn.Volume1,
                        Cooldown = enemySpawn.CooldownTime,
                        Cylinder = enemySpawn.Volume1,
                        EmptyMessage = GetMessage(enemySpawn.Message1),
                        EndFrame = 0,
                        EnemyType = enemyType,
                        Facing = enemySpawn.Facing,
                        NodeName = enemySpawn.NodeName,
                        ParentId = enemySpawn.EntityId1,
                        Position = enemySpawn.Position,
                        SpawnCount = enemySpawn.SpawnCount,
                        SpawnLimit = enemySpawn.SpawnLimit,
                        SpawnNodeName = enemySpawn.SpawnNodeName,
                        SpawnTotal = enemySpawn.SpawnTotal,
                        Sphere = enemySpawn.Volume1,
                        Up = enemySpawn.Up
                    };
                    converted.Add(fhSpawn);
                }
                else if (entity is TriggerVolumeEntityEditor trigger)
                {
                    if (trigger.Subtype == TriggerType.Relay || trigger.Subtype == TriggerType.Automatic
                        || trigger.Subtype == TriggerType.StateBits)
                    {
                        Console.WriteLine($"MPH to FH: Skipping trigger entity ID {entity.Id} with subtype {trigger.Subtype}.");
                        continue;
                    }
                    var volume = new CollisionVolume(Vector3.Zero, 1);
                    FhTriggerType subtype = FhTriggerType.Threshold;
                    if (trigger.Subtype != TriggerType.Threshold)
                    {
                        subtype = trigger.Volume.Type switch
                        {
                            VolumeType.Box => FhTriggerType.Box,
                            VolumeType.Cylinder => FhTriggerType.Cylinder,
                            VolumeType.Sphere => FhTriggerType.Sphere,
                            _ => throw new InvalidOperationException()
                        };
                        volume = trigger.Volume;
                    }
                    FhMessage childMsg = GetMessage(trigger.ChildMessage);
                    FhMessage parentMsg = GetMessage(trigger.ParentMessage);
                    if ((int)childMsg == 255)
                    {
                        Console.WriteLine($"MPH to FH: Skipping trigger entity ID {entity.Id} with child message {trigger.ChildMessage}.");
                        continue;
                    }
                    if ((int)parentMsg == 255)
                    {
                        Console.WriteLine($"MPH to FH: Skipping trigger entity ID {entity.Id} with parent message {trigger.ParentMessage}.");
                    }
                    converted.Add(new FhTriggerVolumeEntityEditor()
                    {
                        Id = trigger.Id,
                        Box = trigger.Volume,
                        ChildId = trigger.ChildId,
                        ChildMessage = childMsg,
                        ChildMsgParam1 = trigger.ChildMsgParam1,
                        Cooldown = trigger.RepeatDelay,
                        Cylinder = trigger.Volume,
                        Facing = trigger.Facing,
                        NodeName = trigger.NodeName,
                        OneUse = trigger.DeactivateAfterUse ? (ushort)1 : (ushort)0,
                        ParentId = trigger.ParentId,
                        ParentMessage = parentMsg,
                        ParentMsgParam1 = trigger.ParentMsgParam1,
                        Position = trigger.Position,
                        Sphere = trigger.Volume,
                        Subtype = subtype,
                        Threshold = trigger.TriggerThreshold,
                        TriggerFlags = GetFlags(trigger.TriggerFlags, trigger.Subtype),
                        Up = trigger.Up
                    });
                }
                else if (entity is AreaVolumeEntityEditor areaVolume)
                {
                    FhTriggerType subtype = areaVolume.Volume.Type switch
                    {
                        VolumeType.Box => FhTriggerType.Box,
                        VolumeType.Cylinder => FhTriggerType.Cylinder,
                        VolumeType.Sphere => FhTriggerType.Sphere,
                        _ => throw new InvalidOperationException()
                    };
                    CollisionVolume volume = areaVolume.Volume;
                    FhMessage insideMsg = GetMessage(areaVolume.InsideMessage);
                    FhMessage exitMsg = GetMessage(areaVolume.ExitMessage);
                    if ((int)insideMsg == 255)
                    {
                        Console.WriteLine($"MPH to FH: Skipping area volume entity ID {entity.Id} with inside message {areaVolume.InsideMessage}.");
                        continue;
                    }
                    if ((int)exitMsg == 255)
                    {
                        Console.WriteLine($"MPH to FH: Skipping area volume entity ID {entity.Id} with exit message {areaVolume.ExitMessage}.");
                    }
                    // set the volume is all three fields -- avoids issue with cylinder
                    converted.Add(new FhAreaVolumeEntityEditor()
                    {
                        Id = areaVolume.Id,
                        Box = volume,
                        Cooldown = areaVolume.Cooldown,
                        Cylinder = volume,
                        ExitMessage = exitMsg,
                        ExitMsgParam1 = areaVolume.ExitMsgParam1,
                        Facing = areaVolume.Facing,
                        InsideMessage = insideMsg,
                        InsideMsgParam1 = areaVolume.InsideMsgParam1,
                        NodeName = areaVolume.NodeName,
                        Position = areaVolume.Position,
                        Sphere = volume,
                        Subtype = subtype,
                        TriggerFlags = GetFlags(areaVolume.TriggerFlags, TriggerType.Normal),
                        Up = areaVolume.Up
                    });
                }
                else if (entity is JumpPadEntityEditor jumpPad)
                {
                    FhTriggerType volumeType = jumpPad.Volume.Type switch
                    {
                        VolumeType.Box => FhTriggerType.Box,
                        VolumeType.Cylinder => FhTriggerType.Cylinder,
                        VolumeType.Sphere => FhTriggerType.Sphere,
                        _ => throw new InvalidOperationException()
                    };
                    // FH beam vectors need to be absolute
                    var transform = new Matrix3(EntityBase.GetTransformMatrix(jumpPad.Facing, jumpPad.Up));
                    Vector3 beamVector = jumpPad.BeamVector;
                    if (transform != Matrix3.Identity)
                    {
                        beamVector *= transform;
                    }
                    converted.Add(new FhJumpPadEntityEditor()
                    {
                        Id = jumpPad.Id,
                        BeamType = 0,
                        BeamVector = beamVector,
                        Box = jumpPad.Volume,
                        ControlLockTime = jumpPad.ControlLockTime,
                        CooldownTime = jumpPad.CooldownTime,
                        Cylinder = jumpPad.Volume,
                        Facing = jumpPad.Facing,
                        ModelId = 0,
                        NodeName = jumpPad.NodeName,
                        Position = jumpPad.Position,
                        Speed = jumpPad.Speed,
                        Sphere = jumpPad.Volume,
                        TriggerFlags = GetFlags(jumpPad.TriggerFlags, TriggerType.Normal),
                        Up = jumpPad.Up,
                        VolumeType = volumeType
                    });
                }
                else if (entity is PlayerSpawnEntityEditor || entity is MorphCameraEntityEditor)
                {
                    entity.Type = entity.Type switch
                    {
                        EntityType.PlayerSpawn => EntityType.FhPlayerSpawn,
                        EntityType.MorphCamera => EntityType.FhMorphCamera,
                        _ => throw new InvalidOperationException()
                    };
                    converted.Add(entity);
                }
                else
                {
                    Console.WriteLine($"MPH to FH: Skipping entity ID {entity.Id} of type {entity.Type}.");
                }
            }
            IEnumerable<PlayerSpawnEntityEditor> playerSpawns = converted
                .Where(c => c.Type == EntityType.FhPlayerSpawn).Select(c => (PlayerSpawnEntityEditor)c);
            if (!playerSpawns.Any())
            {
                Console.WriteLine("MPH to FH: Warning: No player spawn entities are present.");
            }
            else if (!playerSpawns.Any(p => p.Active))
            {
                playerSpawns.First().Active = true;
            }
            return converted;
        }

        public static void CompareRooms(string room1, string room2, string game1 = "amhe1", string game2 = "amhe1")
        {
            RoomMetadata meta1 = Metadata.RoomMetadata[room1];
            RoomMetadata meta2 = Metadata.RoomMetadata[room2];
            Debug.Assert(meta1.EntityPath != null && meta2.EntityPath != null);
            string path1 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game1, meta1.EntityPath);
            string path2 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game2, meta2.EntityPath);
            CompareEntities(File.ReadAllBytes(path1), File.ReadAllBytes(path2));
            Nop();
        }

        private static void CompareEntities(byte[] pack, byte[] file)
        {
            Debug.Assert(pack.Length == file.Length);
            EntityHeader packHeader = Read.ReadStruct<EntityHeader>(pack);
            EntityHeader fileHeader = Read.ReadStruct<EntityHeader>(file);
            Debug.Assert(packHeader.Version == fileHeader.Version);
            for (int i = 0; i < 16; i++)
            {
                Debug.Assert(packHeader.Lengths[i] == fileHeader.Lengths[i]);
            }
            IReadOnlyList<EntityEntry> packEntries = GetEntries(pack);
            IReadOnlyList<EntityEntry> fileEntries = GetEntries(file);
            Debug.Assert(packEntries.Count == fileEntries.Count);
            for (int i = 0; i < packEntries.Count; i++)
            {
                EntityEntry packEntry = packEntries[i];
                EntityEntry fileEntry = fileEntries[i];
                Debug.Assert(packEntry.DataOffset == fileEntry.DataOffset);
                Debug.Assert(packEntry.LayerMask == fileEntry.LayerMask);
                Debug.Assert(packEntry.Length == fileEntry.Length);
                Debug.Assert(Enumerable.SequenceEqual(packEntry.NodeName, fileEntry.NodeName));
            }
            Debug.Assert(Enumerable.SequenceEqual(pack, file));
            Nop();
        }

        private static IReadOnlyList<EntityEntry> GetEntries(byte[] bytes)
        {
            var entries = new List<EntityEntry>();
            int position = Sizes.EntityHeader;
            while (true)
            {
                EntityEntry entry = Read.DoOffset<EntityEntry>(bytes, position);
                if (entry.DataOffset == 0)
                {
                    break;
                }
                entries.Add(entry);
                position += Sizes.EntityEntry;
                if (position > bytes.Length)
                {
                    Debug.Assert(false);
                    break;
                }
            }
            return entries;
        }

        private static void CompareFhEntities(byte[] pack, byte[] file)
        {
            Debug.Assert(pack.Length == file.Length);
            uint packVersion = Read.ReadStruct<uint>(pack);
            uint fileVersion = Read.ReadStruct<uint>(file);
            Debug.Assert(packVersion == fileVersion);
            IReadOnlyList<FhEntityEntry> packEntries = GetFhEntries(pack);
            IReadOnlyList<FhEntityEntry> fileEntries = GetFhEntries(file);
            Debug.Assert(packEntries.Count == fileEntries.Count);
            for (int i = 0; i < packEntries.Count; i++)
            {
                FhEntityEntry packEntry = packEntries[i];
                FhEntityEntry fileEntry = fileEntries[i];
                Debug.Assert(packEntry.DataOffset == fileEntry.DataOffset);
                Debug.Assert(Enumerable.SequenceEqual(packEntry.NodeName, fileEntry.NodeName));
                CompareData((int)packEntry.DataOffset, pack, file);
            }
            Debug.Assert(Enumerable.SequenceEqual(pack, file));
            Nop();
        }

        private static void CompareData(int offset, byte[] pack, byte[] file)
        {
            EntityDataHeader packDataHeader = Read.DoOffset<EntityDataHeader>(pack, offset);
            EntityDataHeader fileDataHeader = Read.DoOffset<EntityDataHeader>(file, offset);
            Debug.Assert(packDataHeader.Type == fileDataHeader.Type);
            Debug.Assert(packDataHeader.EntityId == fileDataHeader.EntityId);
            Debug.Assert(packDataHeader.Position.X.Value == fileDataHeader.Position.X.Value);
            Debug.Assert(packDataHeader.Position.Y.Value == fileDataHeader.Position.Y.Value);
            Debug.Assert(packDataHeader.Position.Z.Value == fileDataHeader.Position.Z.Value);
            Debug.Assert(packDataHeader.UpVector.X.Value == fileDataHeader.UpVector.X.Value);
            Debug.Assert(packDataHeader.UpVector.Y.Value == fileDataHeader.UpVector.Y.Value);
            Debug.Assert(packDataHeader.UpVector.Z.Value == fileDataHeader.UpVector.Z.Value);
            Debug.Assert(packDataHeader.FacingVector.X.Value == fileDataHeader.FacingVector.X.Value);
            Debug.Assert(packDataHeader.FacingVector.Y.Value == fileDataHeader.FacingVector.Y.Value);
            Debug.Assert(packDataHeader.FacingVector.Z.Value == fileDataHeader.FacingVector.Z.Value);
            int end = 0;
            if (packDataHeader.Type + 100 == (ushort)EntityType.FhPlatform)
            {
                end = offset + Marshal.SizeOf<FhPlatformEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhPlayerSpawn)
            {
                end = offset + Marshal.SizeOf<PlayerSpawnEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhDoor)
            {
                end = offset + Marshal.SizeOf<FhDoorEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhItemSpawn)
            {
                end = offset + Marshal.SizeOf<FhItemSpawnEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhEnemySpawn)
            {
                end = offset + Marshal.SizeOf<FhEnemySpawnEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhTriggerVolume)
            {
                end = offset + Marshal.SizeOf<FhTriggerVolumeEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhAreaVolume)
            {
                end = offset + Marshal.SizeOf<FhAreaVolumeEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhJumpPad)
            {
                end = offset + Marshal.SizeOf<FhJumpPadEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhPointModule)
            {
                end = offset + Marshal.SizeOf<PointModuleEntityData>();
            }
            else if (packDataHeader.Type + 100 == (ushort)EntityType.FhMorphCamera)
            {
                end = offset + Marshal.SizeOf<FhMorphCameraEntityData>();
            }
            if (!Enumerable.SequenceEqual(pack[offset..end], file[offset..end]))
            {
                if (packDataHeader.Type + 100 == (ushort)EntityType.FhPlatform)
                {
                    FhPlatformEntityData packData = Read.DoOffset<FhPlatformEntityData>(pack, offset);
                    FhPlatformEntityData fileData = Read.DoOffset<FhPlatformEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhPlayerSpawn)
                {
                    PlayerSpawnEntityData packData = Read.DoOffset<PlayerSpawnEntityData>(pack, offset);
                    PlayerSpawnEntityData fileData = Read.DoOffset<PlayerSpawnEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhDoor)
                {
                    FhDoorEntityData packData = Read.DoOffset<FhDoorEntityData>(pack, offset);
                    FhDoorEntityData fileData = Read.DoOffset<FhDoorEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhItemSpawn)
                {
                    FhItemSpawnEntityData packData = Read.DoOffset<FhItemSpawnEntityData>(pack, offset);
                    FhItemSpawnEntityData fileData = Read.DoOffset<FhItemSpawnEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhEnemySpawn)
                {
                    FhEnemySpawnEntityData packData = Read.DoOffset<FhEnemySpawnEntityData>(pack, offset);
                    FhEnemySpawnEntityData fileData = Read.DoOffset<FhEnemySpawnEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhTriggerVolume)
                {
                    FhTriggerVolumeEntityData packData = Read.DoOffset<FhTriggerVolumeEntityData>(pack, offset);
                    FhTriggerVolumeEntityData fileData = Read.DoOffset<FhTriggerVolumeEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhAreaVolume)
                {
                    FhAreaVolumeEntityData packData = Read.DoOffset<FhAreaVolumeEntityData>(pack, offset);
                    FhAreaVolumeEntityData fileData = Read.DoOffset<FhAreaVolumeEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhJumpPad)
                {
                    FhJumpPadEntityData packData = Read.DoOffset<FhJumpPadEntityData>(pack, offset);
                    FhJumpPadEntityData fileData = Read.DoOffset<FhJumpPadEntityData>(file, offset);
                    Debug.Assert(packData.VolumeType == fileData.VolumeType);
                    Debug.Assert(packData.Box.BoxVector1.X.Value == fileData.Box.BoxVector1.X.Value);
                    Debug.Assert(packData.Box.BoxVector1.Y.Value == fileData.Box.BoxVector1.Y.Value);
                    Debug.Assert(packData.Box.BoxVector1.Z.Value == fileData.Box.BoxVector1.Z.Value);
                    Debug.Assert(packData.Box.BoxVector2.X.Value == fileData.Box.BoxVector2.X.Value);
                    Debug.Assert(packData.Box.BoxVector2.Y.Value == fileData.Box.BoxVector2.Y.Value);
                    Debug.Assert(packData.Box.BoxVector2.Z.Value == fileData.Box.BoxVector2.Z.Value);
                    Debug.Assert(packData.Box.BoxVector3.X.Value == fileData.Box.BoxVector3.X.Value);
                    Debug.Assert(packData.Box.BoxVector3.Y.Value == fileData.Box.BoxVector3.Y.Value);
                    Debug.Assert(packData.Box.BoxVector3.Z.Value == fileData.Box.BoxVector3.Z.Value);
                    Debug.Assert(packData.Box.BoxPosition.X.Value == fileData.Box.BoxPosition.X.Value);
                    Debug.Assert(packData.Box.BoxPosition.Y.Value == fileData.Box.BoxPosition.Y.Value);
                    Debug.Assert(packData.Box.BoxPosition.Z.Value == fileData.Box.BoxPosition.Z.Value);
                    Debug.Assert(packData.Box.BoxDot1.Value == fileData.Box.BoxDot1.Value);
                    Debug.Assert(packData.Box.BoxDot2.Value == fileData.Box.BoxDot2.Value);
                    Debug.Assert(packData.Box.BoxDot3.Value == fileData.Box.BoxDot3.Value);

                    Debug.Assert(packData.Sphere.BoxVector1.X.Value == fileData.Sphere.BoxVector1.X.Value);
                    Debug.Assert(packData.Sphere.BoxVector1.Y.Value == fileData.Sphere.BoxVector1.Y.Value);
                    Debug.Assert(packData.Sphere.BoxVector1.Z.Value == fileData.Sphere.BoxVector1.Z.Value);
                    Debug.Assert(packData.Sphere.BoxVector2.X.Value == fileData.Sphere.BoxVector2.X.Value);
                    Debug.Assert(packData.Sphere.BoxVector2.Y.Value == fileData.Sphere.BoxVector2.Y.Value);
                    Debug.Assert(packData.Sphere.BoxVector2.Z.Value == fileData.Sphere.BoxVector2.Z.Value);
                    Debug.Assert(packData.Sphere.BoxVector3.X.Value == fileData.Sphere.BoxVector3.X.Value);
                    Debug.Assert(packData.Sphere.BoxVector3.Y.Value == fileData.Sphere.BoxVector3.Y.Value);
                    Debug.Assert(packData.Sphere.BoxVector3.Z.Value == fileData.Sphere.BoxVector3.Z.Value);
                    Debug.Assert(packData.Sphere.BoxPosition.X.Value == fileData.Sphere.BoxPosition.X.Value);
                    Debug.Assert(packData.Sphere.BoxPosition.Y.Value == fileData.Sphere.BoxPosition.Y.Value);
                    Debug.Assert(packData.Sphere.BoxPosition.Z.Value == fileData.Sphere.BoxPosition.Z.Value);
                    Debug.Assert(packData.Sphere.BoxDot1.Value == fileData.Sphere.BoxDot1.Value);
                    Debug.Assert(packData.Sphere.BoxDot2.Value == fileData.Sphere.BoxDot2.Value);
                    Debug.Assert(packData.Sphere.BoxDot3.Value == fileData.Sphere.BoxDot3.Value);

                    Debug.Assert(packData.Cylinder.BoxVector1.X.Value == fileData.Cylinder.BoxVector1.X.Value);
                    Debug.Assert(packData.Cylinder.BoxVector1.Y.Value == fileData.Cylinder.BoxVector1.Y.Value);
                    Debug.Assert(packData.Cylinder.BoxVector1.Z.Value == fileData.Cylinder.BoxVector1.Z.Value);
                    Debug.Assert(packData.Cylinder.BoxVector2.X.Value == fileData.Cylinder.BoxVector2.X.Value);
                    Debug.Assert(packData.Cylinder.BoxVector2.Y.Value == fileData.Cylinder.BoxVector2.Y.Value);
                    Debug.Assert(packData.Cylinder.BoxVector2.Z.Value == fileData.Cylinder.BoxVector2.Z.Value);
                    Debug.Assert(packData.Cylinder.BoxVector3.X.Value == fileData.Cylinder.BoxVector3.X.Value);
                    Debug.Assert(packData.Cylinder.BoxVector3.Y.Value == fileData.Cylinder.BoxVector3.Y.Value);
                    Debug.Assert(packData.Cylinder.BoxVector3.Z.Value == fileData.Cylinder.BoxVector3.Z.Value);
                    Debug.Assert(packData.Cylinder.BoxPosition.X.Value == fileData.Cylinder.BoxPosition.X.Value);
                    Debug.Assert(packData.Cylinder.BoxPosition.Y.Value == fileData.Cylinder.BoxPosition.Y.Value);
                    Debug.Assert(packData.Cylinder.BoxPosition.Z.Value == fileData.Cylinder.BoxPosition.Z.Value);
                    Debug.Assert(packData.Cylinder.BoxDot1.Value == fileData.Cylinder.BoxDot1.Value);
                    Debug.Assert(packData.Cylinder.BoxDot2.Value == fileData.Cylinder.BoxDot2.Value);
                    Debug.Assert(packData.Cylinder.BoxDot3.Value == fileData.Cylinder.BoxDot3.Value);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhPointModule)
                {
                    PointModuleEntityData packData = Read.DoOffset<PointModuleEntityData>(pack, offset);
                    PointModuleEntityData fileData = Read.DoOffset<PointModuleEntityData>(file, offset);
                    Debugger.Break();
                }
                else if (packDataHeader.Type + 100 == (ushort)EntityType.FhMorphCamera)
                {
                    FhMorphCameraEntityData packData = Read.DoOffset<FhMorphCameraEntityData>(pack, offset);
                    FhMorphCameraEntityData fileData = Read.DoOffset<FhMorphCameraEntityData>(file, offset);
                    Debugger.Break();
                }
            }
            Nop();
        }

        private static IReadOnlyList<FhEntityEntry> GetFhEntries(byte[] bytes)
        {
            var entries = new List<FhEntityEntry>();
            int position = sizeof(uint);
            while (true)
            {
                FhEntityEntry entry = Read.DoOffset<FhEntityEntry>(bytes, position);
                if (entry.DataOffset == 0)
                {
                    break;
                }
                entries.Add(entry);
                position += Sizes.FhEntityEntry;
                if (position > bytes.Length)
                {
                    Debug.Assert(false);
                    break;
                }
            }
            return entries;
        }

        private static readonly HashSet<EntityType> _validTypesMph = new HashSet<EntityType>()
        {
            EntityType.Platform,
            EntityType.Object,
            EntityType.PlayerSpawn,
            EntityType.Door,
            EntityType.ItemSpawn,
            EntityType.EnemySpawn,
            EntityType.TriggerVolume,
            EntityType.AreaVolume,
            EntityType.JumpPad,
            EntityType.PointModule,
            EntityType.MorphCamera,
            EntityType.OctolithFlag,
            EntityType.FlagBase,
            EntityType.Teleporter,
            EntityType.NodeDefense,
            EntityType.LightSource,
            EntityType.Artifact,
            EntityType.CameraSequence,
            EntityType.ForceField
        };

        private static readonly HashSet<EntityType> _validTypesFh = new HashSet<EntityType>()
        {
            EntityType.FhUnknown0,
            EntityType.FhPlayerSpawn,
            EntityType.FhUnknown2,
            EntityType.FhDoor,
            EntityType.FhItemSpawn,
            EntityType.FhEnemySpawn,
            EntityType.FhTriggerVolume,
            EntityType.FhAreaVolume,
            EntityType.FhPlatform,
            EntityType.FhJumpPad,
            EntityType.FhPointModule,
            EntityType.FhMorphCamera
        };

        private static readonly HashSet<EnemyType> _validEnemiesFh = new HashSet<EnemyType>()
        {
            EnemyType.WarWasp,
            EnemyType.BarbedWarWasp,
            EnemyType.Zoomer,
            EnemyType.Geemer,
            EnemyType.Temroid,
            EnemyType.Petrasyl1,
            EnemyType.Petrasyl2,
            EnemyType.Petrasyl3,
            EnemyType.Petrasyl4
        };

        private static void ThrowIfInvalid(EntityEditorBase entity, bool firstHunt)
        {
            if (entity.Id < 0)
            {
                throw new ProgramException("File entities must have a positive entity ID.");
            }
            if (!Enum.IsDefined(typeof(EntityType), entity.Type))
            {
                throw new ProgramException($"Unknown entity type {(int)entity.Type}.");
            }
            if (!firstHunt && _validTypesFh.Contains(entity.Type))
            {
                throw new ProgramException($"Cannot add FH entity type {entity.Type} to MPH entity file.");
            }
            if (firstHunt && _validTypesMph.Contains(entity.Type))
            {
                throw new ProgramException($"Cannot add MPH entity type {entity.Type} to FH entity file.");
            }
            if ((!firstHunt && !_validTypesMph.Contains(entity.Type)) || (firstHunt && !_validTypesFh.Contains(entity.Type)))
            {
                throw new ProgramException($"Cannot add entity type {entity.Type} to entity file.");
            }
        }

        public static void PrintLayers(ushort mask)
        {
            var sp = new List<string>();
            var mp = new List<string>();
            if ((mask & 1) != 0)
            {
                sp.Add("Initial");
                mp.Add("Battle/Prime Hunter 2P");
            }
            if ((mask & 2) != 0)
            {
                sp.Add("Cleared");
                mp.Add("Battle/Prime Hunter 3P");
            }
            if ((mask & 4) != 0)
            {
                sp.Add("Layer 2");
                mp.Add("Battle/Prime Hunter 4P");
            }
            if ((mask & 8) != 0)
            {
                sp.Add("Layer 3");
                mp.Add("Battle Teams");
            }
            if ((mask & 0x10) != 0)
            {
                mp.Add("Nodes 2P");
            }
            if ((mask & 0x20) != 0)
            {
                mp.Add("Nodes 3P");
            }
            if ((mask & 0x40) != 0)
            {
                mp.Add("Nodes 4P");
            }
            if ((mask & 0x80) != 0)
            {
                mp.Add("Nodes Teams");
            }
            if ((mask & 0x100) != 0)
            {
                mp.Add("Bounty 2P");
            }
            if ((mask & 0x200) != 0)
            {
                mp.Add("Bounty 3P");
            }
            if ((mask & 0x400) != 0)
            {
                mp.Add("Bounty 4P");
            }
            if ((mask & 0x800) != 0)
            {
                mp.Add("Bounty Teams");
            }
            if ((mask & 0x1000) != 0)
            {
                mp.Add("Capture");
            }
            if ((mask & 0x2000) != 0)
            {
                mp.Add("Mode 15");
            }
            if ((mask & 0x4000) != 0)
            {
                mp.Add("Defender");
            }
            if ((mask & 0x8000) != 0)
            {
                mp.Add("Survival");
            }
            if (sp.Count == 0)
            {
                sp.Add("None");
            }
            if (mp.Count == 0)
            {
                mp.Add("None");
            }
            Console.WriteLine($"1P: {String.Join(", ", sp)}");
            Console.WriteLine($"MP: {String.Join(", ", mp)}");
        }

        private static byte[] RepackEntities(IReadOnlyList<EntityEditorBase> entities)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            // header
            uint version = 2;
            ushort[] lengths = new ushort[16];
            foreach (EntityEditorBase entity in entities)
            {
                ThrowIfInvalid(entity, firstHunt: false);
                for (int i = 0; i < 16; i++)
                {
                    if ((entity.LayerMask & (1 << i)) != 0)
                    {
                        lengths[i]++;
                    }
                }
            }
            writer.Write(version);
            foreach (ushort length in lengths)
            {
                writer.Write(length);
            }
            Debug.Assert(stream.Position == Sizes.EntityHeader);
            // entity data
            stream.Position += Sizes.EntityEntry * (entities.Count + 1);
            var results = new List<(int, int)>();
            for (int i = 0; i < entities.Count; i++)
            {
                EntityEditorBase entity = entities[i];
                int offset = (int)stream.Position;
                int size = WriteEntity(entity, writer);
                results.Add((offset, size));
                if (i < entities.Count - 1)
                {
                    while (stream.Position % 4 != 0)
                    {
                        writer.Write(padByte);
                    }
                }
            }
            // entity entries
            stream.Position = Sizes.EntityHeader;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityEditorBase entity = entities[i];
                (int offset, int size) = results[i];
                writer.WriteString(entity.NodeName, 16);
                writer.Write(entity.LayerMask);
                writer.Write((ushort)size);
                writer.Write(offset);
            }
            // entry terminator
            writer.WriteString("", 16);
            writer.Write(padShort);
            writer.Write(padShort);
            writer.Write(padInt);
            return stream.ToArray();
        }

        private static int WriteEntity(EntityEditorBase entity, BinaryWriter writer)
        {
            long position = writer.BaseStream.Position;
            writer.Write((ushort)entity.Type);
            writer.Write(entity.Id);
            writer.WriteVector3(entity.Position);
            writer.WriteVector3(entity.Up);
            writer.WriteVector3(entity.Facing);
            if (entity.Type == EntityType.Platform)
            {
                WriteMphPlatform((PlatformEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.Object)
            {
                WriteMphObject((ObjectEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.PlayerSpawn)
            {
                WritePlayerSpawn((PlayerSpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.Door)
            {
                WriteMphDoor((DoorEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.ItemSpawn)
            {
                WriteMphItemSpawn((ItemSpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.EnemySpawn)
            {
                WriteMphEnemySpawn((EnemySpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.TriggerVolume)
            {
                WriteMphTriggerVolume((TriggerVolumeEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.AreaVolume)
            {
                WriteMphAreaVolume((AreaVolumeEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.JumpPad)
            {
                WriteMphJumpPad((JumpPadEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.PointModule)
            {
                WritePointModule((PointModuleEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.MorphCamera)
            {
                WriteMphMorphCamera((MorphCameraEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.OctolithFlag)
            {
                WriteMphOctolithFlag((OctolithFlagEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FlagBase)
            {
                WriteMphFlagBase((FlagBaseEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.Teleporter)
            {
                WriteMphTeleporter((TeleporterEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.NodeDefense)
            {
                WriteMphNodeDefense((NodeDefenseEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.LightSource)
            {
                WriteMphLightSource((LightSourceEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.Artifact)
            {
                WriteMphArtifact((ArtifactEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.CameraSequence)
            {
                WriteMphCameraSequence((CameraSequenceEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.ForceField)
            {
                WriteMphForceField((ForceFieldEntityEditor)entity, writer);
            }
            return (int)(writer.BaseStream.Position - position);
        }

        private static void WriteMphPlatform(PlatformEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            Debug.Assert(entity.Positions.Count == 10);
            Debug.Assert(entity.Rotations.Count == 10);
            writer.Write(entity.NoPort);
            writer.Write(entity.ModelId);
            writer.Write(entity.ParentId);
            writer.WriteByte(entity.Active);
            writer.Write(entity.Delay);
            writer.Write(entity.ScanData1);
            writer.Write(entity.ScanMsgTarget);
            writer.Write((uint)entity.ScanMessage);
            writer.Write(entity.ScanData2);
            writer.Write(entity.PositionCount);
            foreach (Vector3 position in entity.Positions)
            {
                writer.WriteVector3(position);
            }
            foreach (Vector4 rotation in entity.Rotations)
            {
                writer.WriteVector4(rotation);
            }
            writer.WriteVector3(entity.PositionOffset);
            writer.WriteFloat(entity.ForwardSpeed);
            writer.WriteFloat(entity.BackwardSpeed);
            writer.WriteString(entity.PortalName, 16);
            writer.Write(entity.MovementType);
            writer.WriteInt(entity.ForCutscene);
            writer.Write(entity.ReverseType);
            writer.Write((uint)entity.Flags);
            writer.Write(entity.ContactDamage);
            writer.WriteVector3(entity.BeamSpawnDir);
            writer.WriteVector3(entity.BeamSpawnPos);
            writer.Write(entity.BeamId);
            writer.Write(entity.BeamInterval);
            writer.Write(entity.BeamOnIntervals);
            writer.Write(UInt16.MaxValue); // Unused1B0
            writer.Write(padShort); // Unused1B2
            writer.Write(entity.ResistEffectId);
            writer.Write(entity.Health);
            writer.Write(entity.Effectiveness);
            writer.Write(entity.DamageEffectId);
            writer.Write(entity.DeadEffectId);
            writer.Write(entity.ItemChance);
            writer.Write(padByte); // Padding1C9
            writer.Write(padShort); // Padding1CA
            writer.Write((int)entity.ItemType);
            writer.Write(entity.Unused1D0);
            writer.Write(entity.Unused1D4);
            writer.Write(entity.BeamHitMsgTarget);
            writer.Write((uint)entity.BeamHitMessage);
            writer.Write(entity.BeamHitMsgParam1);
            writer.Write(entity.BeamHitMsgParam2);
            writer.Write(entity.PlayerColMsgTarget);
            writer.Write((uint)entity.PlayerColMessage);
            writer.Write(entity.PlayerColMsgParam1);
            writer.Write(entity.PlayerColMsgParam2);
            writer.Write(entity.DeadMsgTarget);
            writer.Write((uint)entity.DeadMessage);
            writer.Write(entity.DeadMsgParam1);
            writer.Write(entity.DeadMsgParam2);
            writer.Write(entity.LifetimeMsg1Index);
            writer.Write(entity.LifetimeMsg1Target);
            writer.Write((uint)entity.LifetimeMessage1);
            writer.Write(entity.LifetimeMsg1Param1);
            writer.Write(entity.LifetimeMsg1Param2);
            writer.Write(entity.LifetimeMsg2Index);
            writer.Write(entity.LifetimeMsg2Target);
            writer.Write((uint)entity.LifetimeMessage2);
            writer.Write(entity.LifetimeMsg2Param1);
            writer.Write(entity.LifetimeMsg2Param2);
            writer.Write(entity.LifetimeMsg3Index);
            writer.Write(entity.LifetimeMsg3Target);
            writer.Write((uint)entity.LifetimeMessage3);
            writer.Write(entity.LifetimeMsg3Param1);
            writer.Write(entity.LifetimeMsg3Param2);
            writer.Write(entity.LifetimeMsg4Index);
            writer.Write(entity.LifetimeMsg4Target);
            writer.Write((uint)entity.LifetimeMessage4);
            writer.Write(entity.LifetimeMsg4Param1);
            writer.Write(entity.LifetimeMsg4Param2);
        }

        private static void WriteMphObject(ObjectEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            writer.Write(entity.Flags);
            writer.Write(padByte); // Padding25
            writer.Write(padShort); // Padding26
            writer.Write(entity.EffectFlags);
            writer.Write(entity.ModelId);
            writer.Write(entity.LinkedEntity);
            writer.Write(entity.ScanId);
            writer.Write(entity.ScanMsgTarget);
            writer.Write(padShort); // Padding36
            writer.Write((uint)entity.ScanMessage);
            writer.Write(entity.EffectId);
            writer.Write(entity.EffectInterval);
            writer.Write(entity.EffectOnIntervals);
            writer.WriteVector3(entity.EffectPositionOffset);
            writer.WriteVolume(entity.Volume);
        }

        private static void WritePlayerSpawn(PlayerSpawnEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.Availability);
            writer.WriteByte(entity.Active);
            writer.Write(entity.TeamIndex);
        }

        private static void WriteMphDoor(DoorEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteString(entity.DoorNodeName, 16);
            writer.Write(entity.PaletteId);
            writer.Write(entity.ModelId);
            writer.Write(entity.ConnectorId);
            writer.Write(entity.TargetLayerId);
            writer.Write(entity.Flags);
            writer.Write(entity.Field42);
            writer.Write(entity.Field43);
            writer.WriteString(entity.EntityFilename, 16);
            writer.WriteString(entity.RoomName, 16);
        }

        private static void WriteMphItemSpawn(ItemSpawnEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            writer.Write(entity.ParentId);
            writer.Write((uint)entity.ItemType);
            writer.WriteByte(entity.Enabled);
            writer.WriteByte(entity.HasBase);
            writer.WriteByte(entity.AlwaysActive);
            writer.Write(padByte); // Padding2F
            writer.Write(entity.MaxSpawnCount);
            writer.Write(entity.SpawnInterval);
            writer.Write(entity.SpawnDelay);
            writer.Write(entity.SomeEntityId);
            writer.Write((uint)entity.CollectedMessage);
            writer.Write(entity.CollectedMsgParam1);
            writer.Write(entity.CollectedMsgParam2);
        }

        private static void WriteMphEnemySpawn(EnemySpawnEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint padInt = 0;
            writer.Write((uint)entity.EnemyType);
            if (entity.SpawnerType == 0)
            {
                writer.WriteVolume(entity.Volume0);
                writer.WriteVolume(entity.Volume1);
                writer.WriteVolume(entity.Volume2);
                writer.WriteVolume(entity.Volume3);
                for (int i = 0; i < 36; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 2)
            {
                writer.WriteVolume(entity.Volume0);
                writer.WriteVector3(entity.PathVector);
                writer.WriteVolume(entity.Volume1);
                writer.WriteVolume(entity.Volume2);
                for (int i = 0; i < 49; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 3)
            {
                writer.WriteVolume(entity.Volume0);
                writer.Write(entity.Unused68);
                writer.Write(entity.Unused6C);
                writer.Write(entity.Unused70);
                writer.Write(entity.Unused74);
                writer.Write(entity.Unused78);
                writer.Write(entity.Unused7C);
                writer.Write(entity.Unused80);
                writer.WriteVector3(entity.EnemyFacing);
                writer.WriteVector3(entity.EnemyPosition);
                writer.Write(entity.Unknown00);
                writer.Write(entity.UnusedA0);
                writer.Write(entity.Unknown01);
                for (int i = 0; i < 68; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 4)
            {
                writer.WriteVolume(entity.Volume0);
                writer.Write(entity.Unused68);
                writer.Write(entity.Unused6C);
                writer.Write(entity.Unused70);
                writer.Write(entity.Unused74);
                writer.WriteVector3(entity.EnemyPosition);
                writer.Write(entity.Unknown00);
                writer.Write(entity.Unknown01);
                for (int i = 0; i < 75; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 1 || entity.SpawnerType == 8)
            {
                Debug.Assert(entity.MovementVectors.Count == 16);
                void WriteWarWaspFields()
                {
                    writer.WriteVolume(entity.Volume0);
                    writer.WriteVolume(entity.Volume1);
                    writer.WriteVolume(entity.Volume2);
                    foreach (Vector3 vector in entity.MovementVectors)
                    {
                        writer.WriteVector3(vector);
                    }
                    writer.Write(entity.Unknown02);
                    writer.Write(padByte); // Padding1A9
                    writer.Write(padShort); // Padding1AA
                    writer.Write(entity.MovementType);
                }
                if (entity.SpawnerType == 1)
                {
                    WriteWarWaspFields();
                    writer.Write(padInt); // Padding1B0;
                    writer.Write(padInt); // Padding1B4;
                }
                else
                {
                    writer.Write(entity.EnemySubtype);
                    writer.Write(entity.EnemyVersion);
                    WriteWarWaspFields();
                }
            }
            else if (entity.SpawnerType == 5)
            {
                writer.Write(entity.EnemySubtype);
                writer.WriteVolume(entity.Volume0);
                writer.WriteVolume(entity.Volume1);
                writer.WriteVolume(entity.Volume2);
                writer.WriteVolume(entity.Volume3);
                for (int i = 0; i < 35; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 6)
            {
                writer.Write(entity.EnemySubtype);
                writer.Write(entity.EnemyVersion);
                writer.WriteVolume(entity.Volume0);
                writer.WriteVolume(entity.Volume1);
                writer.WriteVolume(entity.Volume2);
                writer.WriteVolume(entity.Volume3);
                for (int i = 0; i < 34; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 7)
            {
                writer.Write(entity.EnemyHealth);
                writer.Write(entity.EnemyDamage);
                writer.Write(entity.EnemySubtype);
                writer.WriteVolume(entity.Volume0);
                for (int i = 0; i < 82; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 9)
            {
                writer.Write((uint)entity.Hunter);
                writer.Write(entity.EncounterType);
                writer.Write(entity.HunterWeapon);
                writer.Write(entity.HunterHealth);
                writer.Write(entity.HunterHealthMax);
                writer.Write(entity.Unknown03);
                writer.Write(entity.HunterColor);
                writer.Write(entity.HunterChance);
                for (int i = 0; i < 95; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 10)
            {
                writer.Write(entity.EnemySubtype);
                writer.Write(entity.EnemyVersion);
                writer.WriteVolume(entity.Volume0);
                writer.WriteVolume(entity.Volume1);
                writer.Write(entity.Unknown04);
                for (int i = 0; i < 65; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 11)
            {
                Debug.Assert(entity.Volume0.Type == VolumeType.Sphere);
                Debug.Assert(entity.Volume1.Type == VolumeType.Sphere);
                writer.WriteVector3(entity.Volume0.SpherePosition);
                writer.WriteFloat(entity.Volume0.SphereRadius);
                writer.WriteVector3(entity.Volume1.SpherePosition);
                writer.WriteFloat(entity.Volume1.SphereRadius);
                for (int i = 0; i < 92; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (entity.SpawnerType == 12)
            {
                writer.WriteVector3(entity.Unknown05);
                writer.Write(entity.Unknown06);
                writer.Write(entity.Unknown07);
                for (int i = 0; i < 95; i++)
                {
                    writer.Write(padInt);
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
            writer.Write(entity.LinkedEntityId);
            writer.Write(entity.SpawnLimit);
            writer.Write(entity.SpawnTotal);
            writer.Write(entity.SpawnCount);
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.AlwaysActive);
            writer.Write(entity.ItemChance);
            writer.Write(entity.SpawnerModel);
            writer.Write(entity.CooldownTime);
            writer.Write(entity.InitialCooldown);
            writer.Write(padShort); // Padding1C6
            writer.WriteFloat(entity.ActiveDistance);
            writer.Write(entity.Field1CC);
            writer.WriteString(entity.SpawnNodeName, 16);
            writer.Write(entity.EntityId1);
            writer.Write(padShort); // Padding1E2
            writer.Write((uint)entity.Message1);
            writer.Write(entity.EntityId2);
            writer.Write(padShort); // Padding1EA
            writer.Write((uint)entity.Message2);
            writer.Write(entity.EntityId3);
            writer.Write(padShort); // Padding1F2
            writer.Write((uint)entity.Message3);
            writer.Write((int)entity.ItemType);
        }

        private static void WriteMphTriggerVolume(TriggerVolumeEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            writer.Write((uint)entity.Subtype);
            writer.WriteVolume(entity.Volume);
            writer.Write(UInt16.MaxValue); // Unused68
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.AlwaysActive);
            writer.WriteByte(entity.DeactivateAfterUse);
            writer.Write(padByte); // Padding6D
            writer.Write(entity.RepeatDelay);
            writer.Write(entity.CheckDelay);
            writer.Write(entity.RequiredStateBit);
            writer.Write((uint)entity.TriggerFlags);
            writer.Write(entity.TriggerThreshold);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // Padding7E
            writer.Write((uint)entity.ParentMessage);
            writer.Write(entity.ParentMsgParam1);
            writer.Write(entity.ParentMsgParam2);
            writer.Write(entity.ChildId);
            writer.Write(padShort); // Padding8E
            writer.Write((uint)entity.ChildMessage);
            writer.Write(entity.ChildMsgParam1);
            writer.Write(entity.ChildMsgParam2);
        }

        private static void WriteMphAreaVolume(AreaVolumeEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            writer.WriteVolume(entity.Volume);
            writer.Write(UInt16.MaxValue); // Unused64
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.AlwaysActive);
            writer.WriteByte(entity.AllowMultiple);
            writer.Write(entity.MessageDelay);
            writer.Write(entity.Unused6A);
            writer.Write((uint)entity.InsideMessage);
            writer.Write(entity.InsideMsgParam1);
            writer.Write(entity.InsideMsgParam2);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // Padding7A
            writer.Write((uint)entity.ExitMessage);
            writer.Write(entity.ExitMsgParam1);
            writer.Write(entity.ExitMsgParam2);
            writer.Write(entity.ChildId);
            writer.Write(entity.Cooldown);
            writer.Write(entity.Priority);
            writer.Write((uint)entity.TriggerFlags);
        }

        private static void WriteMphJumpPad(JumpPadEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            writer.Write(entity.ParentId);
            writer.Write(entity.Unused28);
            writer.WriteVolume(entity.Volume);
            writer.WriteVector3(entity.BeamVector);
            writer.WriteFloat(entity.Speed);
            writer.Write(entity.ControlLockTime);
            writer.Write(entity.CooldownTime);
            writer.WriteByte(entity.Active);
            writer.Write(padByte); // Padding81
            writer.Write(padShort); // Padding82
            writer.Write(entity.ModelId);
            writer.Write(entity.BeamType);
            writer.Write((uint)entity.TriggerFlags);
        }

        private static void WritePointModule(PointModuleEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.NextId);
            writer.Write(entity.PrevId);
            writer.WriteByte(entity.Active);
        }

        private static void WriteMphMorphCamera(MorphCameraEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteVolume(entity.Volume);
        }

        private static void WriteFhMorphCamera(MorphCameraEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteFhVolume(entity.Volume);
        }

        private static void WriteMphOctolithFlag(OctolithFlagEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.TeamId);
        }

        private static void WriteMphFlagBase(FlagBaseEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.TeamId);
            writer.WriteVolume(entity.Volume);
        }

        private static void WriteMphTeleporter(TeleporterEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            writer.Write(entity.Field24);
            writer.Write(entity.Field25);
            writer.Write(entity.ArtifactId);
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.Invisible);
            writer.WriteString(entity.TargetRoom, 15);
            writer.Write(padShort); // Unused38
            writer.Write(UInt16.MaxValue); // Unused3A
            writer.WriteVector3(entity.TargetPosition);
            writer.WriteString(entity.TeleporterNodeName, 16);
        }

        private static void WriteMphNodeDefense(NodeDefenseEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteVolume(entity.Volume);
        }

        private static void WriteMphLightSource(LightSourceEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteVolume(entity.Volume);
            writer.WriteByte(entity.Light1Enabled);
            writer.WriteColorRgb(entity.Light1Color);
            writer.WriteVector3(entity.Light1Vector);
            writer.WriteByte(entity.Light2Enabled);
            writer.WriteColorRgb(entity.Light2Color);
            writer.WriteVector3(entity.Light2Vector);
        }

        private static void WriteMphArtifact(ArtifactEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            writer.Write(entity.ModelId);
            writer.Write(entity.ArtifactId);
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.HasBase);
            writer.Write(entity.Message1Target);
            writer.Write(padShort); // Padding2A
            writer.Write((uint)entity.Message1);
            writer.Write(entity.Message2Target);
            writer.Write(padShort); // Padding32
            writer.Write((uint)entity.Message2);
            writer.Write(entity.Message3Target);
            writer.Write(padShort); // Padding3A
            writer.Write((uint)entity.Message3);
            writer.Write(entity.LinkedEntityId);
        }

        private static void WriteMphCameraSequence(CameraSequenceEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.SequenceId);
            writer.Write(entity.Field25);
            writer.WriteByte(entity.Loop);
            writer.Write(entity.Field27);
            writer.Write(entity.Field28);
            writer.Write(entity.Field29);
            writer.Write(entity.DelayFrames);
            writer.Write(entity.PlayerId1);
            writer.Write(entity.PlayerId2);
            writer.Write(entity.Entity1);
            writer.Write(entity.Entity2);
            writer.Write(entity.MessageTargetId);
            writer.Write((uint)entity.Message);
            writer.Write(entity.MessageParam);
        }

        private static void WriteMphForceField(ForceFieldEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.ForceFieldType);
            writer.WriteFloat(entity.Width);
            writer.WriteFloat(entity.Height);
            writer.WriteByte(entity.Active);
        }

        private static byte[] RepackFhEntities(IReadOnlyList<EntityEditorBase> entities)
        {
            byte padByte = 0;
            uint padInt = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            // header
            uint version = 1;
            writer.Write(version);
            // entity data
            stream.Position += Sizes.FhEntityEntry * (entities.Count + 1);
            var offsets = new List<int>();
            for (int i = 0; i < entities.Count; i++)
            {
                EntityEditorBase entity = entities[i];
                ThrowIfInvalid(entity, firstHunt: true);
                offsets.Add((int)stream.Position);
                WriteFhEntity(entity, writer);
                if (i < entities.Count - 1)
                {
                    while (stream.Position % 4 != 0)
                    {
                        writer.Write(padByte);
                    }
                }
            }
            // entity entries
            stream.Position = sizeof(uint);
            for (int i = 0; i < entities.Count; i++)
            {
                EntityEditorBase entity = entities[i];
                writer.WriteString(entity.NodeName, 16);
                writer.Write(offsets[i]);
            }
            // entry terminator
            writer.WriteString("", 16);
            writer.Write(padInt);
            return stream.ToArray();
        }

        private static void WriteFhEntity(EntityEditorBase entity, BinaryWriter writer)
        {
            writer.Write((ushort)((ushort)entity.Type - 100));
            writer.Write(entity.Id);
            writer.WriteVector3(entity.Position);
            writer.WriteVector3(entity.Up);
            writer.WriteVector3(entity.Facing);
            if (entity.Type == EntityType.FhPlatform)
            {
                WriteFhPlatform((FhPlatformEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhPlayerSpawn)
            {
                WritePlayerSpawn((PlayerSpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhDoor)
            {
                WriteFhDoor((FhDoorEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhItemSpawn)
            {
                WriteFhItemSpawn((FhItemSpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhEnemySpawn)
            {
                WriteFhEnemySpawn((FhEnemySpawnEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhTriggerVolume)
            {
                WriteFhTriggerVolume((FhTriggerVolumeEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhAreaVolume)
            {
                WriteFhAreaVolume((FhAreaVolumeEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhJumpPad)
            {
                WriteFhJumpPad((FhJumpPadEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhPointModule)
            {
                WritePointModule((PointModuleEntityEditor)entity, writer);
            }
            else if (entity.Type == EntityType.FhMorphCamera)
            {
                WriteFhMorphCamera((MorphCameraEntityEditor)entity, writer);
            }
        }

        private static void WriteFhPlatform(FhPlatformEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            Debug.Assert(entity.Positions.Count == 8);
            writer.Write(entity.NoPortal);
            writer.Write(entity.GroupId);
            writer.Write(entity.Unused2C);
            writer.Write(entity.Delay);
            writer.Write(entity.PositionCount);
            writer.Write(padShort); // Padding32
            writer.WriteFhVolume(entity.Volume);
            foreach (Vector3 position in entity.Positions)
            {
                writer.WriteVector3(position);
            }
            writer.WriteFloat(entity.Speed);
            writer.WriteString(entity.PortalName, 16);
        }

        private static void WriteFhDoor(FhDoorEntityEditor entity, BinaryWriter writer)
        {
            writer.WriteString(entity.RoomName, 16);
            writer.Write(entity.Flags);
            writer.Write(entity.ModelId);
        }

        private static void WriteFhItemSpawn(FhItemSpawnEntityEditor entity, BinaryWriter writer)
        {
            writer.Write((uint)entity.ItemType);
            writer.Write(entity.SpawnLimit);
            writer.Write(entity.CooldownTime);
            writer.Write(entity.Unused2C);
        }

        private static void WriteFhEnemySpawn(FhEnemySpawnEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            writer.WriteFhVolume(entity.Box);
            writer.WriteFhVolume(entity.Cylinder);
            writer.WriteFhVolume(entity.Sphere);
            writer.Write((uint)entity.EnemyType);
            writer.Write(entity.SpawnTotal);
            writer.Write(entity.SpawnLimit);
            writer.Write(entity.SpawnCount);
            writer.Write(padByte); // PaddingEB
            writer.Write(entity.Cooldown);
            writer.Write(entity.EndFrame);
            writer.WriteString(entity.SpawnNodeName, 16);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // Padding102
            writer.Write((uint)entity.EmptyMessage);
        }

        private static void WriteFhTriggerVolume(FhTriggerVolumeEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            Debug.Assert(Enum.IsDefined(typeof(FhTriggerType), entity.Subtype));
            writer.Write((uint)entity.Subtype);
            writer.WriteFhVolume(entity.Box);
            writer.WriteFhVolume(entity.Sphere);
            writer.WriteFhVolume(entity.Cylinder);
            writer.Write(entity.OneUse);
            writer.Write(entity.Cooldown);
            writer.Write((uint)entity.TriggerFlags);
            writer.Write(entity.Threshold);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // PaddingF6
            writer.Write((uint)entity.ParentMessage);
            writer.Write(entity.ParentMsgParam1);
            writer.Write(entity.ChildId);
            writer.Write(padShort); // Padding102
            writer.Write((uint)entity.ChildMessage);
            writer.Write(entity.ChildMsgParam1);
        }

        private static void WriteFhAreaVolume(FhAreaVolumeEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            Debug.Assert(Enum.IsDefined(typeof(FhTriggerType), entity.Subtype) && entity.Subtype != FhTriggerType.Threshold);
            writer.Write((uint)entity.Subtype);
            writer.WriteFhVolume(entity.Box);
            writer.WriteFhVolume(entity.Sphere);
            writer.WriteFhVolume(entity.Cylinder);
            writer.Write((uint)entity.InsideMessage);
            writer.Write(entity.InsideMsgParam1);
            writer.Write((uint)entity.ExitMessage);
            writer.Write(entity.ExitMsgParam1);
            writer.Write(entity.Cooldown);
            writer.Write(padShort); // PaddingFA
            writer.Write((uint)entity.TriggerFlags);
        }

        private static void WriteFhJumpPad(FhJumpPadEntityEditor entity, BinaryWriter writer)
        {
            Debug.Assert(Enum.IsDefined(typeof(FhTriggerType), entity.VolumeType) && entity.VolumeType != FhTriggerType.Threshold);
            writer.Write((uint)entity.VolumeType);
            writer.WriteFhVolume(entity.Box);
            writer.WriteFhVolume(entity.Sphere);
            writer.WriteFhVolume(entity.Cylinder);
            writer.Write(entity.CooldownTime);
            writer.WriteVector3(entity.BeamVector);
            writer.WriteFloat(entity.Speed);
            writer.Write(entity.ControlLockTime);
            writer.Write(entity.ModelId);
            writer.Write(entity.BeamType);
            writer.Write((uint)entity.TriggerFlags);
        }

        public static void WriteVolume(this BinaryWriter writer, CollisionVolume volume)
        {
            uint padInt = 0;
            Debug.Assert(Enum.IsDefined(typeof(VolumeType), volume.Type));
            writer.Write((uint)volume.Type);
            if (volume.Type == VolumeType.Box)
            {
                writer.WriteVector3(volume.BoxVector1);
                writer.WriteVector3(volume.BoxVector2);
                writer.WriteVector3(volume.BoxVector3);
                writer.WriteVector3(volume.BoxPosition);
                writer.WriteFloat(volume.BoxDot1);
                writer.WriteFloat(volume.BoxDot2);
                writer.WriteFloat(volume.BoxDot3);
            }
            else if (volume.Type == VolumeType.Cylinder)
            {
                writer.WriteVector3(volume.CylinderVector);
                writer.WriteVector3(volume.CylinderPosition);
                writer.WriteFloat(volume.CylinderRadius);
                writer.WriteFloat(volume.CylinderDot);
                for (int i = 0; i < 7; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (volume.Type == VolumeType.Sphere)
            {
                writer.WriteVector3(volume.SpherePosition);
                writer.WriteFloat(volume.SphereRadius);
                for (int i = 0; i < 11; i++)
                {
                    writer.Write(padInt);
                }
            }
        }

        public static void WriteFhVolume(this BinaryWriter writer, CollisionVolume volume)
        {
            uint padInt = 0;
            Debug.Assert(Enum.IsDefined(typeof(VolumeType), volume.Type));
            if (volume.Type == VolumeType.Box)
            {
                writer.Write((uint)FhVolumeType.Box);
                writer.WriteVector3(volume.BoxPosition);
                writer.WriteVector3(volume.BoxVector1);
                writer.WriteVector3(volume.BoxVector2);
                writer.WriteVector3(volume.BoxVector3);
                writer.WriteFloat(volume.BoxDot1);
                writer.WriteFloat(volume.BoxDot2);
                writer.WriteFloat(volume.BoxDot3);
            }
            else if (volume.Type == VolumeType.Cylinder)
            {
                writer.Write((uint)FhVolumeType.Cylinder);
                writer.WriteVector3(volume.CylinderPosition);
                writer.WriteVector3(volume.CylinderVector);
                writer.WriteFloat(volume.CylinderDot);
                writer.WriteFloat(volume.CylinderRadius);
                for (int i = 0; i < 7; i++)
                {
                    writer.Write(padInt);
                }
            }
            else if (volume.Type == VolumeType.Sphere)
            {
                writer.Write((uint)FhVolumeType.Sphere);
                writer.WriteVector3(volume.SpherePosition);
                writer.WriteFloat(volume.SphereRadius);
                for (int i = 0; i < 11; i++)
                {
                    writer.Write(padInt);
                }
            }
        }
    }
}
