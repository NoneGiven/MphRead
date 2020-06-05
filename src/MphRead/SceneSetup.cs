using System;
using System.Collections.Generic;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public static class SceneSetup
    {
        private static readonly Random _random = new Random();

        public static (Model, RoomMetadata, IReadOnlyList<Model>) LoadRoom(string name, int layerMask)
        {
            RoomMetadata? metadata = Metadata.GetRoomByName(name);
            if (metadata == null)
            {
                throw new InvalidOperationException();
            }
            int roomLayerMask;
            if (layerMask != 0)
            {
                roomLayerMask = layerMask;
            }
            else if (metadata.LayerId != 0)
            {
                roomLayerMask = ((1 << metadata.LayerId) & 0xFF) << 6;
            }
            else
            {
                roomLayerMask = -1;
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
            FilterNodes(room, roomLayerMask);
            // todo?: scene min/max coordinates
            ComputeNodeMatrices(room, index: 0);
            IReadOnlyList<Model> entities = LoadEntities(metadata);
            // todo?: area ID/portals
            room.Type = ModelType.Room;
            return (room, metadata, entities);
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
                Matrix4 transform = ComputeNodeTransforms(node.Scale, node.Angle, node.Position);
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

        public static Matrix4 ComputeModelMatrices(Vector3 vector1, Vector3 vector2, Vector3 position)
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

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;
            transform.M44 = 1;

            return transform;
        }

        private static IReadOnlyList<Model> LoadEntities(RoomMetadata metadata)
        {
            var models = new List<Model>();
            if (metadata.EntityPath == null)
            {
                return models;
            }
            IReadOnlyList<Entity> entities = Read.GetEntities(metadata.EntityPath, metadata.LayerId);
            foreach (Entity entity in entities)
            {
                if (entity.Type == EntityType.Platform)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<PlatformEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Object)
                {
                    models.Add(LoadObject(((Entity<ObjectEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.PlayerSpawn)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<PlayerSpawnEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Door)
                {
                    models.Add(LoadDoor(((Entity<DoorEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Item)
                {
                    models.Add(LoadItem(((Entity<ItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Enemy)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<EnemyEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown7)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown7EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown8)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown8EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    models.AddRange(LoadJumpPad(((Entity<JumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.CameraPos)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<CameraPosEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown12)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown12EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown13)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown13EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<TeleporterEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown15)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown15EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Unknown16)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<Unknown16EntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<ArtifactEntityData>)entity).Data.Position));
                }
                else if (entity.Type == EntityType.CameraSeq)
                {
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    models.Add(LoadEntityPlaceholder(entity.Type, ((Entity<ForceFieldEntityData>)entity).Data.Position));
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

        private static void ComputeJumpPadBeamTransform(Model model, JumpPadEntityData data, Matrix4 parentTransform)
        {
            var up = new Vector3(0, 1, 0);
            var right = new Vector3(1, 0, 0);
            var beamVector = Vector3.Normalize(data.BeamVector.ToFloatVector());
            beamVector = Vector3ByMatrix4(beamVector, parentTransform);
            if (beamVector.X != 0 || beamVector.Z != 0)
            {
                model.Transform = ComputeModelMatrices(beamVector, up, model.Position);
            }
            else
            {
                model.Transform = ComputeModelMatrices(beamVector, right, model.Position);
            }
        }

        // todo: avoid loading things multiple times
        private static IReadOnlyList<Model> LoadJumpPad(JumpPadEntityData data)
        {
            var list = new List<Model>();
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            Model model1 = Read.GetModelByName(modelName);
            model1.Position = data.Position.ToFloatVector();
            model1.Transform = ComputeModelMatrices(data.BaseVector2.ToFloatVector(), data.BaseVector1.ToFloatVector(), model1.Position);
            list.Add(model1);
            Model model2 = Read.GetModelByName("JumpPad_Beam");
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.25f, model1.Position.Z);
            ComputeJumpPadBeamTransform(model2, data, model1.Transform);
            ComputeNodeMatrices(model2, index: 0);
            model2.ForceApplyTransform = true;
            list.Add(model2);
            return list;
        }

        private static Model LoadObject(ObjectEntityData data)
        {
            ObjectMetadata meta = Metadata.GetObjectById(22);
            Model model = Read.GetModelByName(meta.Name, meta.RecolorId);
            model.Position = data.Position.ToFloatVector();
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Object;
            return model;
        }

        private static Model LoadItem(ItemEntityData data)
        {
            (string name, float offset) = Metadata.Items[(int)data.ModelId];
            Model model = Read.GetModelByName(name);
            model.Position = new Vector3(
                data.Position.X.FloatValue,
                data.Position.Y.FloatValue + offset,
                data.Position.Z.FloatValue
            );
            model.Rotation = new Vector3(0, _random.Next(0x8000) / (float)0x7FFF * 360, 0);
            ComputeNodeMatrices(model, index: 0);
            model.Type = ModelType.Item;
            return model;
        }

        // todo: load correct palette from Alimbic texture share
        private static Model LoadDoor(DoorEntityData data)
        {
            //string modelName = Metadata.Doors[(int)data.ModelId];
            //Model model = Read.GetModelByName(modelName);
            Model model = Read.GetModelByName("SecretSwitch");
            model.Position = data.Position.ToFloatVector();
            model.Rotation = GetUnitRotation(data.Rotation);
            model.Type = ModelType.Generic;
            ComputeNodeMatrices(model, index: 0);
            return model;
        }

        private static readonly Dictionary<EntityType, ColorRgb> _colorOverrides = new Dictionary<EntityType, ColorRgb>()
        {
            { EntityType.Platform, new ColorRgb(0x2F, 0x4F, 0x4F) },
            { EntityType.Object, new ColorRgb(0x22, 0x8B, 0x22) },
            { EntityType.PlayerSpawn, new ColorRgb(0x7F, 0x00, 0x00) },
            { EntityType.Enemy, new ColorRgb(0x00, 0x00, 0x8B) },
            { EntityType.Unknown7, new ColorRgb(0xFF, 0x8C, 0x00) },
            { EntityType.Unknown8, new ColorRgb(0xFF, 0xFF, 0x00) },
            { EntityType.CameraPos, new ColorRgb(0x00, 0xFF, 0x00) },
            { EntityType.Unknown12, new ColorRgb(0x00, 0xFF, 0xFF) },
            { EntityType.Unknown13, new ColorRgb(0xFF, 0x00, 0xFF) },
            { EntityType.Unknown15, new ColorRgb(0x1E, 0x90, 0xFF) },
            { EntityType.Unknown16, new ColorRgb(0xFF, 0xDE, 0xAD) },
            { EntityType.CameraSeq, new ColorRgb(0xFF, 0x69, 0xB4) }
        };

        private static Model LoadEntityPlaceholder(EntityType type, Vector3Fx position)
        {
            Model model = Read.GetModelByName("pick_wpn_missile");
            if (_colorOverrides.ContainsKey(type))
            {
                foreach (Material material in model.Materials)
                {
                    material.OverrideColor = _colorOverrides[type];
                }
            }
            if (System.Diagnostics.Debugger.IsAttached &&
                (type == EntityType.Unknown12 || type == EntityType.Unknown13
                || type == EntityType.Unknown15 || type == EntityType.CameraSeq))
            {
                System.Diagnostics.Debugger.Break();
            }
            model.Position = new Vector3(position.X.FloatValue, position.Y.FloatValue, position.Z.FloatValue);
            model.Type = ModelType.Placeholder;
            ComputeNodeMatrices(model, index: 0);
            return model;
        }

        private static Vector3 GetUnitRotation(Vector3Fx rotation)
        {
            var start = new Vector3(0, 0, 1);
            Vector3 end = rotation.ToFloatVector();
            if (start == end)
            {
                return start;
            }
            var cross = Vector3.Cross(start, end);
            // if the vector is antiparallel to the default, just rotate 180 in Y
            if (cross.Length == 0)
            {
                return new Vector3(0, -180, 0);
            }
            cross.Normalize();
            float angle = MathHelper.RadiansToDegrees(Vector3.CalculateAngle(start, end));
            return new Vector3(cross.X * angle, cross.Y * angle, cross.Z * angle);
        }
    }
}
