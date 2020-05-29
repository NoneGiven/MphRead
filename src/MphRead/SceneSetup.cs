using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MphRead
{
    public static class SceneSetup
    {
        private static readonly Random _random = new Random();

        public static (Model, IReadOnlyList<Model>) LoadRoom(string name, int layerMask)
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
            // todo: do whatever with NodePosition/NodeInitialPosition?
            // todo: use this name and ID?
            string nodeName = "rmMain";
            int nodeId = -1;
            int nodeIndex = room.Nodes.IndexOf(b => b.Name.StartsWith("rm"));
            if (nodeIndex != -1)
            {
                nodeName = room.Nodes[nodeIndex].Name;
                nodeId = room.Nodes[nodeIndex].ChildIndex;
            }
            FilterNodes(room, roomLayerMask);
            // todo: scene min/max coordinates?
            ComputeMatrices(room, index: 0);
            // todo: load animations
            IReadOnlyList<Model> entities = LoadEntities(metadata);
            // todo: area ID/portals?
            room.Type = ModelType.Room;
            return (room, entities);
        }

        private static void FilterNodes(Model model, int layerMask)
        {
            foreach (Node node in model.Nodes.Where(b => b.Name.StartsWith("_")))
            {
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

        public static void ComputeMatrices(Model model, int index)
        {
            if (model.Nodes.Count == 0 || index == UInt16.MaxValue)
            {
                return;
            }
            Matrix4x4 transform = default;
            for (int i = index; i != UInt16.MaxValue;)
            {
                Node node = model.Nodes[i];
                ComputeTansforms(ref transform, node.Scale, node.Angle, node.Position);
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
                    ComputeMatrices(model, node.ChildIndex);
                }
                // todo: do whatever with NodePosition/NodeInitialPosition?
                i = node.NextIndex;
            }
        }

        private static void ComputeTansforms(ref Matrix4x4 transform, Vector3 scale, Vector3 angle, Vector3 position)
        {
            float sin_ax = MathF.Sin(angle.X);
            float sin_ay = MathF.Sin(angle.Y);
            float sin_az = MathF.Sin(angle.Z);
            float cos_ax = MathF.Cos(angle.X);
            float cos_ay = MathF.Cos(angle.Y);
            float cos_az = MathF.Cos(angle.Z);

            float v18 = cos_ax * cos_az;
            float v19 = cos_ax * sin_az;
            float v20 = cos_ax * cos_ay;

            float v22 = sin_ax * sin_ay;

            float v17 = v19 * sin_ay;

            transform.M11 = scale.X * cos_ay * cos_az;
            transform.M12 = scale.X * cos_ay * sin_az;
            transform.M13 = scale.X * -sin_ay;

            transform.M21 = scale.Y * ((v22 * cos_az) - v19);
            transform.M22 = scale.Y * ((v22 * sin_az) + v18);
            transform.M23 = scale.Y * sin_ax * cos_ay;

            transform.M31 = scale.Z * (v18 * sin_ay + sin_ax * sin_az);
            transform.M32 = scale.Z * ((v17 + (v19 * sin_ay)) - (sin_ax * cos_az));
            transform.M33 = scale.Z * v20;

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;

            transform.M14 = 0;
            transform.M24 = 0;
            transform.M34 = 0;
            transform.M44 = 1;
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
                if (entity.Type == EntityType.JumpPad)
                {
                    models.AddRange(LoadJumpPad(((Entity<JumpPadEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Item)
                {
                    models.Add(LoadItem(((Entity<ItemEntityData>)entity).Data));
                }
                else if (entity.Type == EntityType.Pickup)
                {
                    // todo: pickups? delayed items?
                    ItemEntityData data = ((Entity<ItemEntityData>)entity).Data;
                }
            }
            return models;
        }

        // todo: avoid loading things multiple times
        private static IReadOnlyList<Model> LoadJumpPad(JumpPadEntityData data)
        {
            // todo: load animations
            var list = new List<Model>();
            string modelName = Metadata.JumpPads[(int)data.ModelId];
            Model model1 = Read.GetModelByName(modelName);
            model1.Position = new Vector3(data.Position);
            list.Add(model1);
            Model model2 = Read.GetModelByName("JumpPad_Beam");
            model2.Position = new Vector3(model1.Position.X, model1.Position.Y + 0.2f, model1.Position.Z);
            model2.Rotation = new Vector3(90, 0, 0);
            list.Add(model2);
            return list;
        }

        private static Model LoadItem(ItemEntityData data)
        {
            // todo: load animations
            (string name, float offset) = Metadata.Items[(int)data.ModelId];
            Model model = Read.GetModelByName(name);
            model.Position = new Vector3(
                data.Position.X.FloatValue,
                data.Position.Y.FloatValue + offset,
                data.Position.Z.FloatValue
            );
            model.Rotation = new Vector3(0, _random.Next(0x8000) / (float)0x7FFF * 360, 0);
            model.Type = ModelType.Item;
            return model;
        }
    }
}
