using System;
using System.Collections.Generic;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public static class SceneSetup
    {
        private static readonly Random _random = new Random();

        public static (Model, RoomMetadata, IReadOnlyList<Model>) LoadRoom(string name, NodeLayer layerMask, int layerId, GameMode mode)
        {
            (RoomMetadata? metadata, int roomId) = Metadata.GetRoomByName(name);
            if (metadata == null)
            {
                throw new InvalidOperationException();
            }
            if (layerMask == NodeLayer.None)
            {
                layerMask = (NodeLayer)(((1 << metadata.LayerId) & 0xFF) << 6);
            }
            Model room = Read.GetRoomByName(name);
            // todo?: do whatever with NodePosition/NodeInitialPosition
            // todo?: use this name and ID
            string nodeName = "rmMain";
            int nodeId = -1;
            int nodeIndex = room.Nodes.IndexOf(b => b.Name.StartsWith("rm"));
            if (nodeIndex != -1)
            {
                nodeName = room.Nodes[nodeIndex].Name;
                nodeId = room.Nodes[nodeIndex].ChildIndex;
            }
            FilterNodes(room, layerMask);
            // todo?: scene min/max coordinates
            ComputeNodeMatrices(room, index: 0);
            int areaId = Metadata.GetAreaInfo(roomId);
            IReadOnlyList<Model> entities = LoadEntities(metadata, areaId, layerId, mode);
            // todo?: area ID/portals
            room.Type = ModelType.Room;
            return (room, metadata, entities);
        }

        private static void FilterNodes(Model model, NodeLayer layerMask)
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
                int flags = 0;
                // we actually have to step through 4 characters at a time rather than using Contains,
                // based on the game's behavior with e.g. "_ml_s010blocks", which is not visible in SP or MP;
                // while it presumably would be in SP since it contains "_s01", that isn't picked up
                for (int i = 0; node.Name.Length - i >= 4; i += 4)
                {
                    string chunk = node.Name.Substring(i, 4);
                    if (chunk.StartsWith("_s") && Int32.TryParse(chunk.Substring(2), out int id))
                    {
                        flags = (int)((uint)flags & 0xC03F | (((uint)flags << 18 >> 24) | (uint)(1 << id)) << 6);
                    }
                    else if (chunk == "_ml0")
                    {
                        flags |= (int)NodeLayer.Multiplayer0;
                    }
                    else if (chunk == "_ml1")
                    {
                        flags |= (int)NodeLayer.Multiplayer1;
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
                if ((flags & (int)layerMask) == 0)
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
                    node.Position.Z / model.Scale.Z);
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
                // todo?: do whatever with NodePosition/NodeInitialPosition
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

        public static void ComputeModelMatrices(Model model, Vector3 vector1, Vector3 vector2)
        {
            Vector3 up = Vector3.Cross(vector2, vector1).Normalized();
            var direction = Vector3.Cross(vector1, up);

            Matrix4 transform = default;

            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;
            transform.M14 = 0;

            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;
            transform.M24 = 0;

            transform.M31 = vector1.X;
            transform.M32 = vector1.Y;
            transform.M33 = vector1.Z;
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
                if (entity.Type == EntityType.Platform)
                {
                    models.Add(LoadPlatform(((Entity<PlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhPlatform)
                {
                    models.Add(LoadFhPlatform(((Entity<FhPlatformEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Object)
                {
                    ObjectEntityData data = ((Entity<ObjectEntityData>)entity).Data;
                    // todo: handle "-1" objects (scan points?)
                    if (data.ModelId == UInt32.MaxValue)
                    {
                        models.Add(LoadEntityPlaceholder(entity.Type, data.Position));
                    }
                    else
                    {
                        models.Add(LoadObject(data));
                    }
                }
                else if (entity.Type == EntityType.PlayerSpawn)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<PlayerSpawnEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.FhPlayerSpawn)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<FhPlayerSpawnEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Door)
                {
                    models.Add(LoadDoor(((Entity<DoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhDoor)
                {
                    models.Add(LoadDoor(((Entity<FhDoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Item)
                {
                    models.AddRange(LoadItem(((Entity<ItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhItem)
                {
                    models.Add(LoadItem(((Entity<FhItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Enemy)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<EnemyEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.FhEnemy)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<FhEnemyEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown7)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown7EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.FhUnknown9)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<FhUnknown9EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown8)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown8EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.FhUnknown10)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<FhUnknown10EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    models.AddRange(LoadJumpPad(((Entity<JumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhJumpPad)
                {
                    models.AddRange(LoadJumpPad(((Entity<FhJumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PointModule)
                {
                    models.Add(LoadPointModule(((Entity<PointModuleEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.FhPointModule)
                {
                    models.Add(LoadPointModule(((Entity<FhPointModuleEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.CameraPosition)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<CameraPositionEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.FhCameraPosition)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<FhCameraPositionEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.OctolithFlag)
                {
                    models.Add(LoadOctolithFlag(((Entity<OctolithFlagEntityData>)entity).Data, mode));
                }
                else if (entity.Type == EntityType.NodeBase)
                {
                    models.Add(LoadNodeBase(((Entity<NodeBaseEntityData>)entity).Data, mode));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    models.Add(LoadTeleporter(((Entity<TeleporterEntityData>)entity).Data, areaId, mode != GameMode.SinglePlayer));
                }
                else if (entity.Type == EntityType.Unknown15)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown15EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.LightSource)
                {
                    Model model = LoadEntityPlaceholder(entity.Type, ((Entity<LightSourceEntityData>)entity).Data.Position);
                    model.Entity = entity;
                    models.Add(model);
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<ArtifactEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.CameraSequence)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<CameraSequenceEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    models.Add(LoadForceField(((Entity<ForceFieldEntityData>)entity).Data));
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
        private static IEnumerable<Model> LoadJumpPad(JumpPadEntityData data)
        {
            var list = new List<Model>();
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            Model model1 = Read.GetModelByName(modelName);
            model1.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model1, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            model1.Type = ModelType.JumpPad;
            list.Add(model1);
            Model model2 = Read.GetModelByName("JumpPad_Beam");
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.25f, model1.Position.Z);
            ComputeJumpPadBeamTransform(model2, data.BeamVector, model1.Transform);
            ComputeNodeMatrices(model2, index: 0);
            model2.Type = ModelType.JumpPadBeam;
            list.Add(model2);
            return list;
        }

        private static IReadOnlyList<Model> LoadJumpPad(FhJumpPadEntityData data)
        {
            var list = new List<Model>();
            string name = data.ModelId == 1 ? "balljump" : "jumppad_base";
            Model model1 = Read.GetModelByName(name, firstHunt: true);
            model1.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model1, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            model1.Type = ModelType.JumpPad;
            list.Add(model1);
            name = data.ModelId == 1 ? "balljump_ray" : "jumppad_ray";
            Model model2 = Read.GetModelByName(name, firstHunt: true);
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.25f, model1.Position.Z);
            ComputeJumpPadBeamTransform(model2, data.BeamVector, Matrix4.Identity);
            ComputeNodeMatrices(model2, index: 0);
            model2.Type = ModelType.JumpPadBeam;
            if (data.ModelId == 0)
            {
                model2.Rotating = true;
                model2.SpinAxis = Vector3.UnitZ;
            }
            list.Add(model2);
            return list;
        }

        private static Model LoadObject(ObjectEntityData data)
        {
            int modelId = (int)data.ModelId;
            ObjectMetadata meta = Metadata.GetObjectById(modelId);
            Model model = Read.GetModelByName(meta.Name, meta.RecolorId);
            model.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Object;
            if (modelId == 0)
            {
                model.ScanVisorOnly = true;
            }
            return model;
        }

        // todo: use more properties
        private static Model LoadPlatform(PlatformEntityData data)
        {
            PlatformMetadata? meta = Metadata.GetPlatformById((int)data.ModelId);
            if (meta == null)
            {
                return LoadEntityPlaceholder(EntityType.Platform, data.Position);
            }
            Model model = Read.GetModelByName(meta.Name);
            model.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        // todo: use more properties
        private static Model LoadFhPlatform(FhPlatformEntityData data)
        {
            Model model = Read.GetModelByName("platform", firstHunt: true);
            model.Position = data.Position.ToFloatVector();
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        private static Model LoadTeleporter(TeleporterEntityData data, int paletteId, bool multiplayer)
        {
            if (data.Invisible != 0)
            {
                return LoadEntityPlaceholder(EntityType.Teleporter, data.Position);
            }
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
            model.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        private static IEnumerable<Model> LoadItem(ItemEntityData data)
        {
            var models = new List<Model>();
            Model model;
            if (data.Enabled != 0)
            {
                model = Read.GetModelByName(Metadata.Items[(int)data.ModelId]);
                float offset = data.Position.Y.Value <= -2663
                    ? Fixed.ToFloat(2663)
                    : data.Position.Y.Value == 6393
                        ? Fixed.ToFloat(2812)
                        : Fixed.ToFloat(2662);
                model.Position = new Vector3(
                    data.Position.X.FloatValue,
                    data.Position.Y.FloatValue + offset,
                    data.Position.Z.FloatValue
                );
                ComputeNodeMatrices(model, index: 0);
                model.Type = ModelType.Item;
                model.Rotating = true;
                model.Floating = true;
                model.Spin = _random.Next(0x8000) / (float)0x7FFF * 360;
                models.Add(model);
            }
            if (data.HasBase != 0)
            {
                // todo: does the base need rotation?
                model = Read.GetModelByName("items_base");
                model.Position = data.Position.ToFloatVector();
                ComputeNodeMatrices(model, index: 0);
                model.Type = ModelType.Generic;
                models.Add(model);
            }
            return models;
        }

        // todo: do these have height offsets?
        private static Model LoadItem(FhItemEntityData data)
        {
            string name = Metadata.FhItems[(int)data.ModelId];
            Model model = Read.GetModelByName(name, firstHunt: true);
            model.Position = data.Position.ToFloatVector();
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Item;
            model.Rotating = true;
            model.Floating = true;
            model.Spin = _random.Next(0x8000) / (float)0x7FFF * 360;
            return model;
        }

        // todo: splitting up the bases and flags like this seems to work for CTF, but not Bounty
        // (the destination has a base, but the flag doesn't)
        private static Model LoadNodeBase(NodeBaseEntityData data, GameMode mode)
        {
            Model nodeBase;
            if (mode == GameMode.Capture)
            {
                nodeBase = Read.GetModelByName("flagbase_ctf", 0); // sktodo: team ID
            }
            else // if mode == GameMode.Bounty
            {
                // todo: flagbase_cap loads in somewhere in Bounty mode
                nodeBase = Read.GetModelByName("flagbase_bounty");
            }
            nodeBase.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(nodeBase, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(nodeBase, index: 0);
            return nodeBase;
        }

        private static Model LoadOctolithFlag(OctolithFlagEntityData data, GameMode mode)
        {
            Model octolith = Read.GetModelByName("octolith_ctf", mode == GameMode.Capture ? data.TeamId : 2);
            // todo: height offset
            octolith.Position = new Vector3(
                    data.Position.X.FloatValue,
                    data.Position.Y.FloatValue + 1.15f,
                    data.Position.Z.FloatValue
                );
            ComputeModelMatrices(octolith, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(octolith, index: 0);
            octolith.Type = ModelType.Generic;
            return octolith;
        }

        private static Model LoadPointModule(PointModuleEntityData data)
        {
            return LoadPointModule(data.Position.ToFloatVector());
        }

        private static Model LoadPointModule(FhPointModuleEntityData data)
        {
            return LoadPointModule(data.Position.ToFloatVector());
        }

        private static Model LoadPointModule(Vector3 position)
        {
            // todo: not all of these are visible at once -- some may not be visible ever?
            Model model = Read.GetModelByName("pick_morphball", firstHunt: true);
            model.Position = position;
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        // todo: enable drawing door lock, also use "flags" to determine lock/color state
        private static Model LoadDoor(DoorEntityData data)
        {
            DoorMetadata meta = Metadata.Doors[(int)data.ModelId];
            int recolorId = 0;
            // AlimbicDoor, AlimbicThinDoor
            if (data.ModelId == 0 || data.ModelId == 3)
            {
                recolorId = Metadata.DoorPalettes[(int)data.PaletteId];
            }
            Model model = Read.GetModelByName(meta.Name, recolorId);
            model.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        // todo: confirm that only the normal door is used
        private static Model LoadDoor(FhDoorEntityData data)
        {
            Model model = Read.GetModelByName("door", firstHunt: true);
            model.Position = data.Position.ToFloatVector();
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Generic;
            return model;
        }

        // todo: load lock, fade in/out "animation"
        private static Model LoadForceField(ForceFieldEntityData data)
        {
            Model model = Read.GetModelByName("ForceField", Metadata.DoorPalettes[(int)data.Type]);
            model.Position = data.Position.ToFloatVector();
            model.Scale = new Vector3(data.Width.FloatValue, data.Height.FloatValue, 1.0f);
            ComputeModelMatrices(model, data.Vector2.ToFloatVector(), data.Vector1.ToFloatVector());
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Object;
            return model;
        }

        private static readonly Dictionary<EntityType, ColorRgb> _colorOverrides = new Dictionary<EntityType, ColorRgb>()
        {
            { EntityType.Platform, new ColorRgb(0x2F, 0x4F, 0x4F) }, // currently used for ID 2
            { EntityType.Object, new ColorRgb(0x22, 0x8B, 0x22) }, // currently used for ID -1
            { EntityType.Enemy, new ColorRgb(0x00, 0x00, 0x8B) },
            { EntityType.FhEnemy, new ColorRgb(0x00, 0x00, 0x8B) },
            { EntityType.Unknown7, new ColorRgb(0xFF, 0x8C, 0x00) },
            { EntityType.FhUnknown9, new ColorRgb(0xFF, 0x8C, 0x00) },
            { EntityType.Unknown8, new ColorRgb(0xFF, 0xFF, 0x00) },
            { EntityType.FhUnknown10, new ColorRgb(0xFF, 0xFF, 0x00) },
            { EntityType.OctolithFlag, new ColorRgb(0x00, 0xFF, 0xFF) },
            { EntityType.NodeBase, new ColorRgb(0xFF, 0x00, 0xFF) },
            { EntityType.Unknown15, new ColorRgb(0x1E, 0x90, 0xFF) },
            // "permanent" placeholders
            { EntityType.PlayerSpawn, new ColorRgb(0x7F, 0x00, 0x00) },
            { EntityType.FhPlayerSpawn, new ColorRgb(0x7F, 0x00, 0x00) },
            { EntityType.CameraPosition, new ColorRgb(0x00, 0xFF, 0x00) },
            { EntityType.FhCameraPosition, new ColorRgb(0x00, 0xFF, 0x00) },
            { EntityType.Teleporter, new ColorRgb(0xFF, 0xFF, 0xFF) },
            { EntityType.LightSource, new ColorRgb(0xFF, 0xDE, 0xAD) },
            { EntityType.CameraSequence, new ColorRgb(0xFF, 0x69, 0xB4) }
        };

        private static Model LoadEntityPlaceholder(EntityType type, Vector3Fx position)
        {
            Model model = Read.GetModelByName("pick_wpn_missile");
            if (_colorOverrides.ContainsKey(type))
            {
                foreach (Mesh mesh in model.Meshes)
                {
                    mesh.OverrideColor = mesh.PlaceholderColor = _colorOverrides[type].AsVector4();
                }
            }
            model.Position = new Vector3(position.X.FloatValue, position.Y.FloatValue, position.Z.FloatValue);
            model.EntityType = type;
            model.Type = ModelType.Placeholder;
            ComputeNodeMatrices(model, index: 0);
            return model;
        }
    }
}
