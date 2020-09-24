using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats.Collision;
using MphRead.Models;
using OpenTK.Mathematics;

namespace MphRead
{
    public static class SceneSetup
    {
        private static readonly Random _random = new Random();

        // todo: artifact flags
        public static (Model, RoomMetadata, CollisionInfo, IReadOnlyList<Model>, int) LoadRoom(string name,
            GameMode mode = GameMode.None, int playerCount = 0, BossFlags bossFlags = BossFlags.None,
            int nodeLayerMask = 0, int entityLayerId = -1)
        {
            (RoomMetadata? metadata, int roomId) = Metadata.GetRoomByName(name);
            int areaId = Metadata.GetAreaInfo(roomId);
            if (metadata == null)
            {
                throw new InvalidOperationException();
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
            IReadOnlyList<Model> entities = LoadEntities(metadata, areaId, entityLayerId, mode);
            CollisionInfo collision = Collision.ReadCollision(metadata.CollisionPath, nodeLayerMask);
            // todo: once ReadCollision is filering things, we don't need to pass nodeLayerMask here or return it
            var room = new RoomModel(Read.GetRoomByName(name), metadata, collision, nodeLayerMask);
            FilterNodes(room, nodeLayerMask);
            ComputeNodeMatrices(room, index: 0);
            return (room, metadata, collision, entities, nodeLayerMask);
        }

        private static void FilterNodes(Model model, int layerMask)
        {
            foreach (Node node in model.Nodes)
            {
                // todo: there's probably some node or mesh property that hides these things
                if (node.Name.Contains("etag"))
                {
                    node.Enabled = false;
                    continue;
                }
                if (!node.Name.StartsWith("_"))
                {
                    continue;
                }

                // todo: refactor this
                int flags = 0;
                // we actually have to step through 4 characters at a time rather than using Contains,
                // based on the game's behavior with e.g. "_ml_s010blocks", which is not visible in SP or MP;
                // while it presumably would be in SP since it contains "_s01", that isn't picked up
                for (int i = 0; node.Name.Length - i >= 4; i += 4)
                {
                    string chunk = node.Name.Substring(i, 4);
                    if (chunk.StartsWith("_s") && Int32.TryParse(chunk[2..], out int id))
                    {
                        flags = (int)((uint)flags & 0xC03F | (((uint)flags << 18 >> 24) | (uint)(1 << id)) << 6);
                    }
                    else if (chunk == "_ml0")
                    {
                        flags |= (int)NodeLayer.MultiplayerLod0;
                    }
                    else if (chunk == "_ml1")
                    {
                        flags |= (int)NodeLayer.MultiplayerLod1;
                    }
                    else if (chunk == "_mpu")
                    {
                        flags |= (int)NodeLayer.MultiplayerU;
                    }
                    else if (chunk == "_ctf")
                    {
                        flags |= (int)NodeLayer.CaptureTheFlag;
                    }
                }
                if ((flags & layerMask) == 0)
                {
                    node.Enabled = false;
                }
            }
        }

        public static void ComputeNodeMatrices(Model model, int index)
        {
            if (model.Nodes.Count == 0 || index == UInt16.MaxValue)
            {
                return;
            }
            for (int i = index; i != UInt16.MaxValue;)
            {
                Node node = model.Nodes[i];
                // the scale division isn't done by the game, which is why transforms on room nodes don't work,
                // which is probably why they're disabled. they can be reenabled with a switch in the viewer
                var position = new Vector3(
                    node.Position.X / model.Scale.X,
                    node.Position.Y / model.Scale.Y,
                    node.Position.Z / model.Scale.Z
                );
                Matrix4 transform = ComputeNodeTransforms(node.Scale, node.Angle, position);
                if (node.ParentIndex == UInt16.MaxValue)
                {
                    node.Transform = transform;
                }
                else
                {
                    node.Transform = transform * model.Nodes[node.ParentIndex].Transform;
                }
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    ComputeNodeMatrices(model, node.ChildIndex);
                }
                i = node.NextIndex;
            }
        }

        // todo: rename/relocate
        public static Matrix4 ComputeNodeTransforms(Vector3 scale, Vector3 angle, Vector3 position)
        {
            float sinAx = MathF.Sin(angle.X);
            float sinAy = MathF.Sin(angle.Y);
            float sinAz = MathF.Sin(angle.Z);
            float cosAx = MathF.Cos(angle.X);
            float cosAy = MathF.Cos(angle.Y);
            float cosAz = MathF.Cos(angle.Z);

            float v18 = cosAx * cosAz;
            float v19 = cosAx * sinAz;
            float v20 = cosAx * cosAy;

            float v22 = sinAx * sinAy;

            float v17 = v19 * sinAy;

            Matrix4 transform = default;

            transform.M11 = scale.X * cosAy * cosAz;
            transform.M12 = scale.X * cosAy * sinAz;
            transform.M13 = scale.X * -sinAy;

            transform.M21 = scale.Y * ((v22 * cosAz) - v19);
            transform.M22 = scale.Y * ((v22 * sinAz) + v18);
            transform.M23 = scale.Y * sinAx * cosAy;

            transform.M31 = scale.Z * (v18 * sinAy + sinAx * sinAz);
            transform.M32 = scale.Z * (v17 + (v19 * sinAy) - (sinAx * cosAz));
            transform.M33 = scale.Z * v20;

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;

            transform.M14 = 0;
            transform.M24 = 0;
            transform.M34 = 0;
            transform.M44 = 1;

            return transform;
        }

        public static Matrix3 GetTransformMatrix(Vector3 vector1, Vector3 vector2)
        {
            Vector3 up = Vector3.Cross(vector2, vector1).Normalized();
            var direction = Vector3.Cross(vector1, up);

            Matrix3 transform = default;

            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;

            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;

            transform.M31 = vector1.X;
            transform.M32 = vector1.Y;
            transform.M33 = vector1.Z;

            return transform;
        }

        public static void ComputeModelMatrices(Model model, Vector3 vector2, Vector3 vector1)
        {
            Vector3 up = Vector3.Cross(vector1, vector2).Normalized();
            var direction = Vector3.Cross(vector2, up);

            Matrix4 transform = default;

            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;
            transform.M14 = 0;

            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;
            transform.M24 = 0;

            transform.M31 = vector2.X;
            transform.M32 = vector2.Y;
            transform.M33 = vector2.Z;
            transform.M34 = 0;

            transform.M41 = model.Position.X;
            transform.M42 = model.Position.Y;
            transform.M43 = model.Position.Z;
            transform.M44 = 1;

            Matrix4 scaleMatrix = model.Transform.ClearTranslation().ClearRotation();
            model.Transform = scaleMatrix * transform;
        }

        private static IReadOnlyList<Model> LoadEntities(RoomMetadata metadata, int areaId, int layerId, GameMode mode)
        {
            var models = new List<Model>();
            if (metadata.EntityPath == null)
            {
                return models;
            }
            IReadOnlyList<Entity> entities = Read.GetEntities(metadata.EntityPath, layerId);
            foreach (Entity entity in entities)
            {
                int count = models.Count;
                if (entity.Type == EntityType.Platform)
                {
                    models.Add(LoadPlatform((Entity<PlatformEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhPlatform)
                {
                    models.Add(LoadFhPlatform((Entity<FhPlatformEntityData>)entity));
                }
                else if (entity.Type == EntityType.Object)
                {
                    var objectEntity = (Entity<ObjectEntityData>)entity;
                    // todo: handle "-1" objects (scan points?)
                    if (objectEntity.Data.ModelId == UInt32.MaxValue)
                    {
                        models.Add(LoadEntityPlaceholder(objectEntity));
                    }
                    else
                    {
                        models.Add(LoadObject(objectEntity));
                    }
                }
                else if (entity.Type == EntityType.PlayerSpawn || entity.Type == EntityType.FhPlayerSpawn)
                {
                    // todo: compute model matrices for placeholders to show e.g. player spawn angle
                    models.Add(LoadEntityPlaceholder((Entity<PlayerSpawnEntityData>)entity));
                    var ent = (Entity<PlayerSpawnEntityData>)entity;
                    Console.WriteLine($"{ent.NodeName}, {ent.Position}, {ent.LayerMask}, {ent.EntityId}, {ent.Data.Active}, {ent.Data.Initial}, {ent.Data.TeamIndex}");
                }
                else if (entity.Type == EntityType.Door)
                {
                    models.AddRange(LoadDoor((Entity<DoorEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhDoor)
                {
                    models.Add(LoadDoor((Entity<FhDoorEntityData>)entity));
                }
                else if (entity.Type == EntityType.Item)
                {
                    models.AddRange(LoadItem((Entity<ItemEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhItem)
                {
                    models.Add(LoadItem((Entity<FhItemEntityData>)entity));
                }
                else if (entity.Type == EntityType.Enemy)
                {
                    models.Add(LoadEntityPlaceholder((Entity<EnemyEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhEnemy)
                {
                    models.Add(LoadEntityPlaceholder((Entity<FhEnemyEntityData>)entity));
                }
                else if (entity.Type == EntityType.TriggerVolume)
                {
                    models.Add(LoadEntityPlaceholder((Entity<TriggerVolumeEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhTriggerVolume)
                {
                    models.Add(LoadEntityPlaceholder((Entity<FhTriggerVolumeEntityData>)entity));
                }
                else if (entity.Type == EntityType.AreaVolume)
                {
                    models.Add(LoadEntityPlaceholder((Entity<AreaVolumeEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhAreaVolume)
                {
                    models.Add(LoadEntityPlaceholder((Entity<FhAreaVolumeEntityData>)entity));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    IEnumerable<Model> model = LoadJumpPad((Entity<JumpPadEntityData>)entity);
                    Debug.Assert(model.Count() == 2);
                    model.First().Entity = entity;
                    models.AddRange(model);
                }
                else if (entity.Type == EntityType.FhJumpPad)
                {
                    models.AddRange(LoadJumpPad((Entity<FhJumpPadEntityData>)entity));
                }
                else if (entity.Type == EntityType.PointModule || entity.Type == EntityType.FhPointModule)
                {
                    models.Add(LoadPointModule((Entity<PointModuleEntityData>)entity));
                }
                else if (entity.Type == EntityType.MorphCamera)
                {
                    models.Add(LoadEntityPlaceholder((Entity<MorphCameraEntityData>)entity));
                }
                else if (entity.Type == EntityType.FhMorphCamera)
                {
                    models.Add(LoadEntityPlaceholder((Entity<FhMorphCameraEntityData>)entity));
                }
                else if (entity.Type == EntityType.OctolithFlag)
                {
                    models.AddRange(LoadOctolithFlag((Entity<OctolithFlagEntityData>)entity, mode));
                }
                else if (entity.Type == EntityType.FlagBase)
                {
                    models.AddRange(LoadFlagBase((Entity<FlagBaseEntityData>)entity, mode));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    models.Add(LoadTeleporter((Entity<TeleporterEntityData>)entity, areaId, mode != GameMode.SinglePlayer));
                }
                else if (entity.Type == EntityType.NodeDefense)
                {
                    models.AddRange(LoadNodeDefense((Entity<NodeDefenseEntityData>)entity, mode));
                }
                else if (entity.Type == EntityType.LightSource)
                {
                    models.Add(LoadEntityPlaceholder((Entity<LightSourceEntityData>)entity));
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    models.AddRange(LoadArtifact((Entity<ArtifactEntityData>)entity));
                }
                else if (entity.Type == EntityType.CameraSequence)
                {
                    models.Add(LoadEntityPlaceholder((Entity<CameraSequenceEntityData>)entity));
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    models.AddRange(LoadForceField((Entity<ForceFieldEntityData>)entity));
                }
                else
                {
                    throw new ProgramException($"Invalid entity type {entity.Type}");
                }
                int added = models.Count - count;
                for (int i = models.Count - added; i < models.Count; i++)
                {
                    models[i].EntityLayer = entity.LayerMask;
                    models[i].EntityType = entity.Type;
                }
            }
            return models;
        }

        private static Vector3 Vector3ByMatrix4(Vector3 vector, Matrix4 matrix)
        {
            return new Vector3(
                vector.X * matrix.M11 + vector.Y * matrix.M21 + vector.Z * matrix.M31,
                vector.X * matrix.M12 + vector.Y * matrix.M22 + vector.Z * matrix.M32,
                vector.X * matrix.M13 + vector.Y * matrix.M23 + vector.Z * matrix.M33
            );
        }

        private static void ComputeJumpPadBeamTransform(Model model, Vector3Fx beamVector, Matrix4 parentTransform)
        {
            var up = new Vector3(0, 1, 0);
            var right = new Vector3(1, 0, 0);
            var vector = Vector3.Normalize(beamVector.ToFloatVector());
            vector = Vector3ByMatrix4(vector, parentTransform);
            if (vector.X != 0 || vector.Z != 0)
            {
                ComputeModelMatrices(model, vector, up);
            }
            else
            {
                ComputeModelMatrices(model, vector, right);
            }
        }

        // todo: avoid loading the same entity multiple times
        private static IEnumerable<Model> LoadJumpPad(Entity<JumpPadEntityData> entity)
        {
            JumpPadEntityData data = entity.Data;
            var list = new List<Model>();
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            Model model1 = Read.GetModelByName(modelName);
            model1.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model1, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            model1.Type = ModelType.JumpPad;
            model1.Entity = entity;
            list.Add(model1);
            Model model2 = Read.GetModelByName("JumpPad_Beam");
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.25f, model1.Position.Z);
            ComputeJumpPadBeamTransform(model2, data.BeamVector, model1.Transform);
            ComputeNodeMatrices(model2, index: 0);
            model2.Type = ModelType.JumpPadBeam;
            model2.Entity = entity;
            // todo: room state
            if (data.Active == 0)
            {
                model2.Visible = false;
            }
            list.Add(model2);
            return list;
        }

        private static IReadOnlyList<Model> LoadJumpPad(Entity<FhJumpPadEntityData> entity)
        {
            FhJumpPadEntityData data = entity.Data;
            var list = new List<Model>();
            string name = data.ModelId == 1 ? "balljump" : "jumppad_base";
            Model model1 = Read.GetModelByName(name, firstHunt: true);
            model1.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model1, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            model1.Type = ModelType.JumpPad;
            model1.Entity = entity;
            list.Add(model1);
            name = data.ModelId == 1 ? "balljump_ray" : "jumppad_ray";
            Model model2 = Read.GetModelByName(name, firstHunt: true);
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.25f, model1.Position.Z);
            ComputeJumpPadBeamTransform(model2, data.BeamVector, Matrix4.Identity);
            ComputeNodeMatrices(model2, index: 0);
            model2.Type = ModelType.JumpPadBeam;
            model2.Entity = entity;
            if (data.ModelId == 0)
            {
                model2.Rotating = true;
                model2.SpinAxis = Vector3.UnitZ;
                model2.SpinSpeed = 0.35f;
            }
            list.Add(model2);
            return list;
        }

        private static Model LoadObject(Entity<ObjectEntityData> entity)
        {
            ObjectEntityData data = entity.Data;
            int modelId = (int)data.ModelId;
            ObjectMetadata meta = Metadata.GetObjectById(modelId);
            Model model = Read.GetModelByName(meta.Name, meta.RecolorId);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Object;
            model.Entity = entity;
            if (modelId == 0)
            {
                model.ScanVisorOnly = true;
            }
            // temporary
            if (meta.Name == "AlimbicCapsule")
            {
                model.Animations.NodeGroupId = -1;
                model.Animations.MaterialGroupId = -1;
            }
            else if (meta.Name == "WallSwitch")
            {
                model.Animations.NodeGroupId = -1;
                model.Animations.MaterialGroupId = -1;
            }
            return model;
        }

        // todo: use more properties (item, movement, linked entities)
        private static Model LoadPlatform(Entity<PlatformEntityData> entity)
        {
            PlatformEntityData data = entity.Data;
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                return LoadEntityPlaceholder(entity);
            }
            Model model = Read.GetModelByName(meta.Name);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            // temporary
            if (meta.Name == "SamusShip")
            {
                model.Animations.NodeGroupId = 1;
            }
            else if (meta.Name == "SyluxTurret")
            {
                model.Animations.NodeGroupId = -1;
            }
            return model;
        }

        // todo: use more properties
        private static Model LoadFhPlatform(Entity<FhPlatformEntityData> entity)
        {
            FhPlatformEntityData data = entity.Data;
            Model model = Read.GetModelByName("platform", firstHunt: true);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            return model;
        }

        private static Model LoadTeleporter(Entity<TeleporterEntityData> entity, int paletteId, bool multiplayer)
        {
            TeleporterEntityData data = entity.Data;
            if (data.Invisible != 0)
            {
                return LoadEntityPlaceholder(entity);
            }
            // todo: how to use ArtifactId?
            int flags = data.ArtifactId < 8 && data.Invisible == 0 ? 2 : 0;
            string modelName;
            if ((flags & 2) == 0)
            {
                modelName = multiplayer ? "TeleporterMP" : "TeleporterSmall";
            }
            else
            {
                modelName = "Teleporter";
            }
            Model model = Read.GetModelByName(modelName, multiplayer ? 0 : paletteId);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            return model;
        }

        private static IEnumerable<Model> LoadItem(Entity<ItemEntityData> entity)
        {
            ItemEntityData data = entity.Data;
            var models = new List<Model>();
            Model model;
            if (data.Enabled != 0)
            {
                model = Read.GetModelByName(Metadata.Items[(int)data.ModelId]);
                model.Position = new Vector3(
                    data.Header.Position.X.FloatValue,
                    data.Header.Position.Y.FloatValue + GetItemHeightOffset(data.Header.Position.Y),
                    data.Header.Position.Z.FloatValue
                );
                ComputeNodeMatrices(model, index: 0);
                model.Type = ModelType.Item;
                model.Entity = entity;
                model.Rotating = true;
                model.Floating = true;
                model.Spin = GetItemRotation();
                model.SpinSpeed = 0.35f;
                models.Add(model);
            }
            if (data.HasBase != 0)
            {
                // todo: does the base need rotation?
                model = Read.GetModelByName("items_base");
                model.Position = data.Header.Position.ToFloatVector();
                ComputeNodeMatrices(model, index: 0);
                model.Type = ModelType.Generic;
                model.Entity = entity;
                models.Add(model);
            }
            return models;
        }

        // todo: do these have height offsets?
        private static Model LoadItem(Entity<FhItemEntityData> entity)
        {
            FhItemEntityData data = entity.Data;
            string name = Metadata.FhItems[(int)data.ModelId];
            Model model = Read.GetModelByName(name, firstHunt: true);
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            model.Position = data.Header.Position.ToFloatVector().AddY(0.5f);
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Item;
            model.Entity = entity;
            model.Rotating = true;
            model.Floating = true;
            model.Spin = GetItemRotation();
            model.SpinSpeed = 0.35f;
            return model;
        }

        private static ushort _itemRotation = 0;

        public static float GetItemRotation()
        {
            float rotation = _itemRotation / (float)(UInt16.MaxValue + 1) * 360f;
            _itemRotation += 0x2000;
            return rotation;
        }

        private static IEnumerable<Model> LoadOctolithFlag(Entity<OctolithFlagEntityData> entity, GameMode mode)
        {
            OctolithFlagEntityData data = entity.Data;
            var models = new List<Model>();
            if (mode == GameMode.Capture || mode == GameMode.Bounty)
            {
                Model octolith = Read.GetModelByName("octolith_ctf", mode == GameMode.Capture ? data.TeamId : 2);
                // sktodo: exact height offset
                octolith.Position = new Vector3(
                    data.Header.Position.X.FloatValue,
                    data.Header.Position.Y.FloatValue + 1.15f,
                    data.Header.Position.Z.FloatValue
                );
                ComputeModelMatrices(octolith, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                ComputeNodeMatrices(octolith, index: 0);
                octolith.Type = ModelType.Generic;
                octolith.Entity = entity;
                models.Add(octolith);
                if (mode == GameMode.Bounty)
                {
                    Model flagBase = Read.GetModelByName("flagbase_bounty");
                    flagBase.Position = data.Header.Position.ToFloatVector();
                    ComputeModelMatrices(flagBase, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                    ComputeNodeMatrices(flagBase, index: 0);
                    flagBase.Type = ModelType.Generic;
                    flagBase.Entity = entity;
                    models.Add(flagBase);
                }
            }
            return models;
        }

        private static IEnumerable<Model> LoadFlagBase(Entity<FlagBaseEntityData> entity, GameMode mode)
        {
            FlagBaseEntityData data = entity.Data;
            var models = new List<Model>();
            // note: setup like this is necessary because e.g. Sic Transit has OctolithFlags/FlagBases
            // enabled in Defender mode according to their layer masks, but they don't appear in-game
            if (mode == GameMode.Capture || mode == GameMode.Bounty)
            {
                string name = mode == GameMode.Capture ? "flagbase_ctf" : "flagbase_cap";
                int paletteId = mode == GameMode.Capture ? (int)data.TeamId : 0;
                Model flagBase = Read.GetModelByName(name, paletteId);
                flagBase.Position = data.Header.Position.ToFloatVector();
                ComputeModelMatrices(flagBase, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                ComputeNodeMatrices(flagBase, index: 0);
                flagBase.Type = ModelType.Generic;
                flagBase.Entity = entity;
                models.Add(flagBase);
            }
            return models;
        }

        private static IEnumerable<Model> LoadNodeDefense(Entity<NodeDefenseEntityData> entity, GameMode mode)
        {
            NodeDefenseEntityData data = entity.Data;
            var models = new List<Model>();
            if (mode == GameMode.Defender || mode == GameMode.Nodes)
            {
                Model node = Read.GetModelByName("koth_data_flow");
                node.Position = data.Header.Position.ToFloatVector();
                ComputeModelMatrices(node, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                ComputeNodeMatrices(node, index: 0);
                node.Type = ModelType.Generic;
                node.Entity = entity;
                models.Add(node);
                // todo: spinning when active
                // sktodo: exact height offset
                Model circle = Read.GetModelByName("koth_terminal");
                circle.Position = new Vector3(
                    data.Header.Position.X.FloatValue,
                    data.Header.Position.Y.FloatValue + 0.5f,
                    data.Header.Position.Z.FloatValue
                );
                circle.Scale = new Vector3(data.Scale.FloatValue, data.Scale.FloatValue, data.Scale.FloatValue);
                ComputeModelMatrices(circle, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                ComputeNodeMatrices(circle, index: 0);
                circle.Type = ModelType.Generic;
                circle.Entity = entity;
                models.Add(circle);
            }
            return models;
        }

        private static Model LoadPointModule(Entity<PointModuleEntityData> entity)
        {
            // todo: not all of these are visible at once -- some may not be visible ever?
            // --> but it's not simply using the Active property
            Model model = Read.GetModelByName("pick_morphball", firstHunt: true);
            model.Position = entity.Position;
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            return model;
        }

        private static IEnumerable<Model> LoadDoor(Entity<DoorEntityData> entity)
        {
            var models = new List<Model>();
            DoorEntityData data = entity.Data;
            DoorMetadata meta = Metadata.Doors[(int)data.ModelId];
            int recolorId = 0;
            // AlimbicDoor, AlimbicThinDoor
            if (data.ModelId == 0 || data.ModelId == 3)
            {
                recolorId = Metadata.DoorPalettes[(int)data.PaletteId];
            }
            // in practice (actual palette indices, not the index into the metadata):
            // - standard = 0, 1, 2, 3, 4, 6
            // - morph ball = 0
            // - boss = 0
            // - thin = 0, 7
            Vector3 vec1 = data.Header.UpVector.ToFloatVector();
            Model model = Read.GetModelByName(meta.Name, recolorId);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), vec1);
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            // todo: remove temporary code like this once animations are being selected properly
            model.Animations.NodeGroupId = -1;
            model.Animations.MaterialGroupId = -1;
            models.Add(model);
            Model doorLock = Read.GetModelByName(meta.LockName);
            Vector3 position = model.Position;
            position.X += meta.LockOffset * vec1.X;
            position.Y += meta.LockOffset * vec1.Y;
            position.Z += meta.LockOffset * vec1.Z;
            doorLock.Position = position;
            ComputeModelMatrices(doorLock, data.Header.RightVector.ToFloatVector(), vec1);
            ComputeNodeMatrices(doorLock, index: 0);
            doorLock.Type = ModelType.Generic;
            doorLock.Entity = entity;
            doorLock.Visible = false; // todo: use flags to determine lock/color state
            models.Add(doorLock);
            return models;
        }

        private static Model LoadDoor(Entity<FhDoorEntityData> entity)
        {
            FhDoorEntityData data = entity.Data;
            Model model = Read.GetModelByName(Metadata.FhDoors[(int)data.ModelId], firstHunt: true);
            model.Position = data.Header.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            // temporary
            model.Animations.NodeGroupId = -1;
            model.Animations.MaterialGroupId = -1;
            return model;
        }

        private static IEnumerable<Model> LoadArtifact(Entity<ArtifactEntityData> entity)
        {
            ArtifactEntityData data = entity.Data;
            var models = new List<Model>();
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            Model model = Read.GetModelByName(name);
            float offset = data.ModelId >= 8 ? GetOctolithHeightOffset() : model.Nodes[0].CullRadius.FloatValue;
            model.Position = new Vector3(
                data.Header.Position.X.FloatValue,
                data.Header.Position.Y.FloatValue + offset,
                data.Header.Position.Z.FloatValue
            );
            ComputeModelMatrices(model, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            model.Entity = entity;
            if (data.ModelId >= 8)
            {
                model.Rotating = true;
                model.SpinSpeed = 0.25f;
                // todo: this (and some other entity setup stuff) should be applied no matter how the model is loaded
                model.UseLightOverride = true;
            }
            models.Add(model);
            if (data.HasBase != 0)
            {
                Model baseModel = Read.GetModelByName("ArtifactBase");
                offset = GetArtifactBaseHeightOffset(data.Header.Position.Y.Value + model.Nodes[0].CullRadius.Value);
                baseModel.Position = new Vector3(
                    data.Header.Position.X.FloatValue,
                    model.Position.Y + offset,
                    data.Header.Position.Z.FloatValue
                );
                ComputeModelMatrices(baseModel, data.Header.RightVector.ToFloatVector(), data.Header.UpVector.ToFloatVector());
                ComputeNodeMatrices(baseModel, index: 0);
                baseModel.Type = ModelType.Generic;
                baseModel.Entity = entity;
                models.Add(baseModel);
            }
            return models;
        }

        // todo: fade in/out "animation"
        private static IEnumerable<Model> LoadForceField(Entity<ForceFieldEntityData> entity)
        {
            var models = new List<Model>();
            ForceFieldEntityData data = entity.Data;
            int recolor = Metadata.DoorPalettes[(int)data.Type];
            Model model = Read.GetModelByName("ForceField", recolor);
            model.Position = data.Header.Position.ToFloatVector();
            model.Scale = new Vector3(data.Width.FloatValue, data.Height.FloatValue, 1.0f);
            Vector3 vec1 = data.Header.UpVector.ToFloatVector();
            Vector3 vec2 = data.Header.RightVector.ToFloatVector();
            ComputeModelMatrices(model, vec2, vec1);
            ComputeNodeMatrices(model, index: 0);
            model.Visible = data.Active != 0;
            model.Type = ModelType.Object;
            model.Entity = entity;
            models.Add(model);
            if (data.Active != 0 && data.Type != 9)
            {
                Model enemy = new ForceFieldLockModel(Read.GetModelByName("ForceFieldLock", recolor));
                Vector3 position = model.Position;
                position.X += Fixed.ToFloat(409) * vec2.X;
                position.Y += Fixed.ToFloat(409) * vec2.Y;
                position.Z += Fixed.ToFloat(409) * vec2.Z;
                enemy.Position = enemy.InitialPosition = position;
                enemy.Vector1 = vec1;
                enemy.Vector2 = vec2;
                ComputeModelMatrices(enemy, vec2, vec1);
                ComputeNodeMatrices(enemy, index: 0);
                enemy.Type = ModelType.Object;
                enemy.Entity = entity;
                models.Add(enemy);
            }
            return models;
        }

        private static float GetItemHeightOffset(Fixed value)
        {
            return value.Value <= -2663
                ? Fixed.ToFloat(2663)
                : value.Value == 6393
                    ? Fixed.ToFloat(2812)
                    : Fixed.ToFloat(2662);
        }

        private static float GetOctolithHeightOffset()
        {
            return Fixed.ToFloat(7168);
        }

        private static float GetArtifactBaseHeightOffset(int value)
        {
            return value <= 3116 ? Fixed.ToFloat(-3116) : Fixed.ToFloat(-3117);
        }

        private static readonly Dictionary<EntityType, ColorRgb> _colorOverrides = new Dictionary<EntityType, ColorRgb>()
        {
            { EntityType.Platform, new ColorRgb(0x2F, 0x4F, 0x4F) }, // currently used for ID 2
            { EntityType.Object, new ColorRgb(0x22, 0x8B, 0x22) }, // currently used for ID -1
            { EntityType.Enemy, new ColorRgb(0x00, 0x00, 0x8B) },
            { EntityType.FhEnemy, new ColorRgb(0x00, 0x00, 0x8B) },
            { EntityType.TriggerVolume, new ColorRgb(0xFF, 0x8C, 0x00) },
            { EntityType.FhTriggerVolume, new ColorRgb(0xFF, 0x8C, 0x00) },
            { EntityType.AreaVolume, new ColorRgb(0xFF, 0xFF, 0x00) },
            { EntityType.FhAreaVolume, new ColorRgb(0xFF, 0xFF, 0x00) },
            // "permanent" placeholders
            { EntityType.PlayerSpawn, new ColorRgb(0x7F, 0x00, 0x00) },
            { EntityType.FhPlayerSpawn, new ColorRgb(0x7F, 0x00, 0x00) },
            { EntityType.MorphCamera, new ColorRgb(0x00, 0xFF, 0x00) },
            { EntityType.FhMorphCamera, new ColorRgb(0x00, 0xFF, 0x00) },
            { EntityType.Teleporter, new ColorRgb(0xFF, 0xFF, 0xFF) }, // used for invisible teleporters
            { EntityType.LightSource, new ColorRgb(0xFF, 0xDE, 0xAD) },
            { EntityType.CameraSequence, new ColorRgb(0xFF, 0x69, 0xB4) }
        };

        private static Model LoadEntityPlaceholder(Entity entity)
        {
            Model model = Read.GetModelByName("pick_wpn_missile");
            if (_colorOverrides.ContainsKey(entity.Type))
            {
                foreach (Mesh mesh in model.Meshes)
                {
                    mesh.PlaceholderColor = _colorOverrides[entity.Type].AsVector4();
                }
            }
            model.Position = entity.Position;
            model.EntityType = entity.Type;
            model.Type = ModelType.Placeholder;
            model.Entity = entity;
            ComputeNodeMatrices(model, index: 0);
            return model;
        }
    }
}
