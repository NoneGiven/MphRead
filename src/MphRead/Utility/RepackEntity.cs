using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MphRead.Editor;
using OpenTK.Mathematics;

namespace MphRead.Utility
{
    public static partial class Repack
    {
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
                    var entities = new List<EntityEditorBase>();
                    foreach (Entity entity in Read.GetEntities(meta.EntityPath, layerId: -1, firstHunt: true))
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
                    byte[] bytes = RepackFhEntities(entities, padEnd: meta.Name == "Level SP Survivor");
                    byte[] fileBytes = File.ReadAllBytes(Path.Combine(Paths.FhFileSystem, meta.EntityPath));
                    CompareFhEntities(bytes, fileBytes);
                    Nop();
                }
                else
                {
                    continue;
                    var entities = new List<EntityEditorBase>();
                    foreach (Entity entity in Read.GetEntities(meta.EntityPath, layerId: -1, firstHunt: false))
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
                    byte[] bytes = RepackEntities(entities);
                    byte[] fileBytes = File.ReadAllBytes(Path.Combine(Paths.FileSystem, meta.EntityPath));
                    CompareEntities(bytes, fileBytes);
                    Nop();
                }
            }
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
            writer.Write(entity.Field2E);
            writer.Write(entity.Field2F);
            writer.Write(entity.ScanData1);
            writer.Write(entity.ScanEventTarget);
            writer.Write(entity.ScanEventId);
            writer.Write(entity.ScanData2);
            writer.Write(entity.Field3A);
            foreach (Vector3 position in entity.Positions)
            {
                writer.WriteVector3(position);
            }
            foreach (Vector4 rotation in entity.Rotations)
            {
                writer.WriteVector4(rotation);
            }
            writer.WriteVector3(entity.PositionOffset);
            writer.Write(entity.Field160);
            writer.Write(entity.Field164);
            writer.WriteString(entity.PortalName, 16);
            writer.Write(entity.Field178);
            writer.Write(entity.Field17C);
            writer.Write(entity.Field180);
            writer.Write((uint)entity.Flags);
            writer.Write(entity.ContactDamage);
            writer.WriteVector3(entity.BeamSpawnDir);
            writer.WriteVector3(entity.BeamSpawnPos);
            writer.Write(entity.BeamId);
            writer.Write(entity.BeamInterval);
            writer.Write(entity.BeamOnIntervals);
            writer.Write(UInt16.MaxValue); // Unused1B0
            writer.Write(padShort); // Unused1B2
            writer.Write(entity.EffectId1);
            writer.Write(entity.Health);
            writer.Write(entity.Field1BC);
            writer.Write(entity.EffectId2);
            writer.Write(entity.EffectId3);
            writer.Write(entity.ItemChance);
            writer.Write(padByte); // Padding1C9
            writer.Write(padShort); // Padding1CA
            writer.Write(entity.ItemModel);
            writer.Write(entity.Field1D0);
            writer.Write(entity.Field1D4);
            writer.Write(entity.Message1Target);
            writer.Write(entity.Message1Id);
            writer.Write(entity.Message1Param1);
            writer.Write(entity.Message1Param2);
            writer.Write(entity.Message2Target);
            writer.Write(entity.Message2Id);
            writer.Write(entity.Message2Param1);
            writer.Write(entity.Message2Param2);
            writer.Write(entity.Message3Target);
            writer.Write(entity.Message3Id);
            writer.Write(entity.Message3Param1);
            writer.Write(entity.Message3Param2);
            writer.Write(entity.Field208);
            writer.Write(entity.Msg32Target1);
            writer.Write(entity.Msg32Message1);
            writer.Write(entity.Msg32Param11);
            writer.Write(entity.Msg32Param21);
            writer.Write(entity.Field218);
            writer.Write(entity.Msg32Target2);
            writer.Write(entity.Msg32Message2);
            writer.Write(entity.Msg32Param12);
            writer.Write(entity.Msg32Param22);
            writer.Write(entity.Field228);
            writer.Write(entity.Msg32Target3);
            writer.Write(entity.Msg32Message3);
            writer.Write(entity.Msg32Param13);
            writer.Write(entity.Msg32Param23);
            writer.Write(entity.Field238);
            writer.Write(entity.Msg32Target4);
            writer.Write(entity.Msg32Message4);
            writer.Write(entity.Msg32Param14);
            writer.Write(entity.Msg32Param24);
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
            writer.Write(entity.ScanEventTargetId);
            writer.Write(padShort); // Padding36
            writer.Write(entity.ScanEventId);
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
            writer.Write(entity.TargetRoomId);
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
            writer.Write(entity.CollectedMessageId);
            writer.Write(entity.CollectedMessageParam1);
            writer.Write(entity.CollectedMessageParam2);
        }

        private static void WriteMphEnemySpawn(EnemySpawnEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            writer.Write((uint)entity.EnemyType);
            writer.Write(entity.Subtype);
            writer.Write(entity.TextureId);
            writer.Write(entity.HunterWeapon);
            writer.Write(entity.Health);
            writer.Write(entity.HealthMax);
            writer.Write(entity.Field38);
            writer.Write(entity.Field3A);
            writer.Write(entity.Field3B);
            // union start
            writer.Write(entity.Field3C);
            writer.Write(entity.Field40);
            writer.Write(entity.Field44);
            writer.Write(entity.Field48);
            writer.Write(entity.Field4C);
            writer.Write(entity.Field50);
            writer.Write(entity.Field54);
            writer.Write(entity.Field58);
            writer.Write(entity.Field5C);
            writer.Write(entity.Field60);
            writer.Write(entity.Field64);
            writer.Write(entity.Field68);
            writer.Write(entity.Field6C);
            writer.Write(entity.Field70);
            writer.Write(entity.Field74);
            writer.Write(entity.Field78);
            writer.Write(entity.Field7C);
            writer.Write(entity.Field80);
            writer.Write(entity.Field84);
            writer.Write(entity.Field88);
            writer.Write(entity.Field8C);
            writer.Write(entity.Field90);
            writer.Write(entity.Field94);
            writer.Write(entity.Field98);
            writer.Write(entity.Field9C);
            writer.Write(entity.FieldA0);
            writer.Write(entity.FieldA4);
            writer.Write(entity.FieldA8);
            writer.Write(entity.FieldAC);
            writer.Write(entity.FieldB0);
            writer.Write(entity.FieldB4);
            writer.Write(entity.FieldB8);
            writer.Write(entity.FieldBC);
            writer.Write(entity.FieldC0);
            writer.Write(entity.FieldC4);
            writer.Write(entity.FieldC8);
            writer.Write(entity.FieldCC);
            writer.Write(entity.FieldD0);
            writer.Write(entity.FieldD4);
            writer.Write(entity.FieldD8);
            writer.Write(entity.FieldDC);
            writer.Write(entity.FieldE0);
            writer.Write(entity.FieldE4);
            writer.Write(entity.FieldE8);
            writer.Write(entity.FieldEC);
            writer.Write(entity.FieldF0);
            writer.Write(entity.FieldF4);
            writer.Write(entity.FieldF8);
            writer.Write(entity.FieldFC);
            writer.Write(entity.Field100);
            writer.Write(entity.Field104);
            writer.Write(entity.Field108);
            writer.Write(entity.Field10C);
            writer.Write(entity.Field110);
            writer.Write(entity.Field114);
            writer.Write(entity.Field118);
            writer.Write(entity.Field11C);
            writer.Write(entity.Field120);
            writer.Write(entity.Field124);
            writer.Write(entity.Field128);
            writer.Write(entity.Field12C);
            writer.Write(entity.Field130);
            writer.Write(entity.Field134);
            writer.Write(entity.Field138);
            writer.Write(entity.Field13C);
            writer.Write(entity.Field140);
            writer.Write(entity.Field144);
            writer.Write(entity.Field148);
            writer.Write(entity.Field14C);
            writer.Write(entity.Field150);
            writer.Write(entity.Field154);
            writer.Write(entity.Field158);
            writer.Write(entity.Field15C);
            writer.Write(entity.Field160);
            writer.Write(entity.Field164);
            writer.Write(entity.Field168);
            writer.Write(entity.Field16C);
            writer.Write(entity.Field170);
            writer.Write(entity.Field174);
            writer.Write(entity.Field178);
            writer.Write(entity.Field17C);
            writer.Write(entity.Field180);
            writer.Write(entity.Field184);
            writer.Write(entity.Field188);
            writer.Write(entity.Field18C);
            writer.Write(entity.Field190);
            writer.Write(entity.Field194);
            writer.Write(entity.Field198);
            writer.Write(entity.Field19C);
            writer.Write(entity.Field1A0);
            writer.Write(entity.Field1A4);
            writer.Write(entity.Field1A8);
            writer.Write(entity.Field1AC);
            writer.Write(entity.Field1B0);
            writer.Write(entity.Field1B4);
            // union end
            writer.Write(entity.Field1B8);
            writer.Write(entity.SomeLimit);
            writer.Write(entity.Field1BB);
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
            writer.Write(entity.Field1E2);
            writer.Write(entity.MessageId1);
            writer.Write(entity.EntityId2);
            writer.Write(entity.Field1EA);
            writer.Write(entity.MessageId2);
            writer.Write(entity.EntityId3);
            writer.Write(entity.Field1F2);
            writer.Write(entity.MessageId3);
            writer.Write(entity.ItemModel);
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
            writer.Write(entity.TriggerFlags);
            writer.Write(padShort); // Padding76
            writer.Write(entity.TriggerThreshold);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // Padding7E
            writer.Write((uint)entity.ParentEvent);
            writer.Write(entity.ParentEventParam1);
            writer.Write(entity.ParentEventParam2);
            writer.Write(entity.ChildId);
            writer.Write(padShort); // Padding8E
            writer.Write((uint)entity.ChildEvent);
            writer.Write(entity.ChildEventParam1);
            writer.Write(entity.ChildEventParam2);
        }

        private static void WriteMphAreaVolume(AreaVolumeEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            writer.WriteVolume(entity.Volume);
            writer.Write(UInt16.MaxValue); // Unused64
            writer.WriteByte(entity.Active);
            writer.WriteByte(entity.AlwaysActive);
            writer.WriteByte(entity.AllowMultiple);
            writer.Write(entity.EventDelay);
            writer.Write(entity.Unused6A);
            writer.Write((uint)entity.InsideEvent);
            writer.Write(entity.InsideEventParam1);
            writer.Write(entity.InsideEventParam2);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // Padding7A
            writer.Write((uint)entity.ExitEvent);
            writer.Write(entity.ExitEventParam1);
            writer.Write(entity.ExitEventParam2);
            writer.Write(entity.ChildId);
            writer.Write(entity.Cooldown);
            writer.Write(entity.Priority);
            writer.Write(entity.Flags);
        }

        private static void WriteMphJumpPad(JumpPadEntityEditor entity, BinaryWriter writer)
        {
            byte padByte = 0;
            ushort padShort = 0;
            uint beamType = 0;
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
            writer.Write(beamType);
            writer.Write(entity.Flags);
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
            writer.Write(entity.Message1Id);
            writer.Write(entity.Message2Target);
            writer.Write(padShort); // Padding32
            writer.Write(entity.Message2Id);
            writer.Write(entity.Message3Target);
            writer.Write(padShort); // Padding3A
            writer.Write(entity.Message3Id);
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
            writer.Write(entity.MessageId);
            writer.Write(entity.MessageParam);
        }

        private static void WriteMphForceField(ForceFieldEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.ForceFieldType);
            writer.WriteFloat(entity.Width);
            writer.WriteFloat(entity.Height);
            writer.WriteByte(entity.Active);
        }

        private static byte[] RepackFhEntities(IReadOnlyList<EntityEditorBase> entities, bool padEnd)
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
                offsets.Add((int)stream.Position);
                WriteFhEntity(entity, writer);
                if (i < entities.Count - 1 || padEnd)
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
            writer.Write(entity.Field28);
            writer.Write(entity.Field2C);
            writer.Write(entity.Field30);
            writer.Write(entity.Field31);
            writer.Write(padShort); // Padding32
            writer.WriteFhVolume(entity.Volume);
            foreach (Vector3 position in entity.Positions)
            {
                writer.WriteVector3(position);
            }
            writer.Write(entity.FieldD4);
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
            writer.Write(entity.Field2C);
        }

        private static void WriteFhEnemySpawn(FhEnemySpawnEntityEditor entity, BinaryWriter writer)
        {
            writer.Write(entity.Field24);
            writer.Write(entity.Field28);
            writer.Write(entity.Field2C);
            writer.Write(entity.Field30);
            writer.Write(entity.Field34);
            writer.Write(entity.Field38);
            writer.Write(entity.Field3C);
            writer.Write(entity.Field40);
            writer.Write(entity.Field44);
            writer.Write(entity.Field48);
            writer.Write(entity.Field4C);
            writer.Write(entity.Field50);
            writer.Write(entity.Field54);
            writer.Write(entity.Field58);
            writer.Write(entity.Field5C);
            writer.Write(entity.Field60);
            writer.Write(entity.Field64);
            writer.Write(entity.Field68);
            writer.Write(entity.Field6C);
            writer.Write(entity.Field70);
            writer.Write(entity.Field74);
            writer.Write(entity.Field78);
            writer.Write(entity.Field7C);
            writer.Write(entity.Field80);
            writer.Write(entity.Field84);
            writer.Write(entity.Field88);
            writer.Write(entity.Field8C);
            writer.Write(entity.Field90);
            writer.Write(entity.Field94);
            writer.Write(entity.Field98);
            writer.Write(entity.Field9C);
            writer.Write(entity.FieldA0);
            writer.Write(entity.FieldA4);
            writer.Write(entity.FieldA8);
            writer.Write(entity.FieldAC);
            writer.Write(entity.FieldB0);
            writer.Write(entity.FieldB4);
            writer.Write(entity.FieldB8);
            writer.Write(entity.FieldBC);
            writer.Write(entity.FieldC0);
            writer.Write(entity.FieldC4);
            writer.Write(entity.FieldC8);
            writer.Write(entity.FieldCC);
            writer.Write(entity.FieldD0);
            writer.Write(entity.FieldD4);
            writer.Write(entity.FieldD8);
            writer.Write(entity.FieldDC);
            writer.Write(entity.FieldE0);
            writer.Write(entity.EnemyType);
            writer.Write(entity.FieldE8);
            writer.Write(entity.Cooldown);
            writer.Write(entity.FieldEE);
            writer.WriteString(entity.SpawnNodeName, 16);
            writer.Write(entity.ParentId);
            writer.Write(entity.Field102);
            writer.Write(entity.Field104);
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
            writer.Write(entity.Flags);
            writer.Write(entity.Threshold);
            writer.Write(entity.ParentId);
            writer.Write(padShort); // PaddingF6
            writer.Write((uint)entity.ParentEvent);
            writer.Write(entity.ParentParam1);
            writer.Write(entity.ChildId);
            writer.Write(padShort); // Padding102
            writer.Write((uint)entity.ChildEvent);
            writer.Write(entity.ChildParam1);
        }

        private static void WriteFhAreaVolume(FhAreaVolumeEntityEditor entity, BinaryWriter writer)
        {
            ushort padShort = 0;
            Debug.Assert(Enum.IsDefined(typeof(FhTriggerType), entity.Subtype) && entity.Subtype != FhTriggerType.Threshold);
            writer.Write((uint)entity.Subtype);
            writer.WriteFhVolume(entity.Box);
            writer.WriteFhVolume(entity.Sphere);
            writer.WriteFhVolume(entity.Cylinder);
            writer.Write((uint)entity.InsideEvent);
            writer.Write(entity.InsideParam1);
            writer.Write((uint)entity.ExitEvent);
            writer.Write(entity.ExitParam1);
            writer.Write(entity.Cooldown);
            writer.Write(padShort); // PaddingFA
            writer.Write(entity.Flags);
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
            writer.Write(entity.FieldFC);
            writer.Write(entity.ModelId);
            writer.Write(entity.BeamType);
            writer.Write(entity.Flags);
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
