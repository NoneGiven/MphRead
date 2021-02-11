using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class RoomEntity : EntityBase
    {
        private readonly IReadOnlyList<CollisionPortal> _portals = new List<CollisionPortal>();
        private readonly IReadOnlyList<PortalNodeRef> _forceFields = new List<PortalNodeRef>();
        private IReadOnlyList<Node> Nodes => _models[0].Model.Nodes;

        protected override bool UseNodeTransform => false; // default -- will use transform if setting is enabled

        public RoomEntity(RoomMetadata meta, CollisionInfo collision, int layerMask) : base(EntityType.Room)
        {
            ModelInstance inst = Read.GetNewRoom(meta.Name);
            _models.Add(inst);
            FilterNodes(layerMask);
            if (meta.Name == "UNIT2_C6")
            {
                // manually disable a decal that isn't rendered in-game because it's not on a surface
                Nodes[46].Enabled = false;
            }
            NewModel model = inst.Model;
            var portals = new List<CollisionPortal>();
            var forceFields = new List<PortalNodeRef>();
            portals.AddRange(collision.Portals.Where(p => (p.LayerMask & 4) != 0 || (p.LayerMask & layerMask) != 0));
            if (portals.Count > 0)
            {
                IEnumerable<string> parts = portals.Select(p => p.NodeName1).Concat(portals.Select(p => p.NodeName2)).Distinct();
                foreach (Node node in inst.Model.Nodes)
                {
                    if (parts.Contains(node.Name))
                    {
                        node.IsRoomPartNode = true;
                    }
                }
                IEnumerable<CollisionPortal> pmags = portals.Where(p => p.Name.StartsWith("pmag"));
                foreach (CollisionPortal portal in pmags)
                {
                    for (int i = 0; i < model.Nodes.Count; i++)
                    {
                        if (model.Nodes[i].Name == $"geo{portal.Name[1..]}")
                        {
                            forceFields.Add(new PortalNodeRef(portal, i));
                            break;
                        }
                    }
                }
                // biodefense chamber 04 and 07 don't have the red portal geometry nodes
                Debug.Assert(forceFields.Count == pmags.Count()
                    || model.Name == "biodefense chamber 04" || model.Name == "biodefense chamber 07");
            }
            else if (meta.RoomNodeName != null
                && model.Nodes.TryFind(n => n.Name == meta.RoomNodeName && n.ChildIndex != UInt16.MaxValue, out Node? roomNode))
            {
                roomNode.IsRoomPartNode = true;
            }
            else
            {
                foreach (Node node in model.Nodes)
                {
                    if (node.Name.StartsWith("rm"))
                    {
                        node.IsRoomPartNode = true;
                        break;
                    }
                }
            }
            Debug.Assert(model.Nodes.Any(n => n.IsRoomPartNode));
            _portals = portals;
            _forceFields = forceFields;
        }

        private void FilterNodes(int layerMask)
        {
            foreach (Node node in Nodes)
            {
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

        public override void GetDrawInfo(NewScene scene)
        {
            ModelInstance inst = _models[0];
            for (int i = 0; i < Nodes.Count; i++)
            {
                Node pnode = Nodes[i];
                if (pnode.IsRoomPartNode && pnode.Enabled)
                {
                    int childIndex = pnode.ChildIndex;
                    if (childIndex != UInt16.MaxValue)
                    {
                        Node node = Nodes[childIndex];
                        Debug.Assert(node.ChildIndex == UInt16.MaxValue);
                        GetItems(inst, node);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != UInt16.MaxValue)
                        {
                            node = Nodes[nextIndex];
                            GetItems(inst, node);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }
            if (scene.ShowForceFields)
            {
                for (int i = 0; i < _forceFields.Count; i++)
                {
                    PortalNodeRef forceField = _forceFields[i];
                    Node pnode = Nodes[forceField.NodeIndex];
                    if (pnode.ChildIndex != UInt16.MaxValue)
                    {
                        Node node = Nodes[pnode.ChildIndex];
                        GetItems(inst, node, forceField.Portal);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != UInt16.MaxValue)
                        {
                            node = Nodes[nextIndex];
                            GetItems(inst, node, forceField.Portal);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }

            void GetItems(ModelInstance inst, Node node, CollisionPortal? portal = null)
            {
                if (!node.Enabled)
                {
                    return;
                }
                NewModel model = inst.Model;
                int start = node.MeshId / 2;
                for (int k = 0; k < node.MeshCount; k++)
                {
                    int polygonId = 0;
                    Mesh mesh = model.Meshes[start + k];
                    if (!mesh.Visible)
                    {
                        continue;
                    }
                    Material material = model.Materials[mesh.MaterialId];
                    float alpha = 1.0f;
                    if (portal != null)
                    {
                        polygonId = UInt16.MaxValue;
                        alpha = GetPortalAlpha(portal.Position, scene.CameraPosition);
                    }
                    else if (material.RenderMode == RenderMode.Translucent)
                    {
                        polygonId = scene.GetNextPolygonId();
                    }
                    Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node, scene);
                    scene.AddRenderItem(material, polygonId, alpha, emission: Vector3.Zero, GetLightInfo(scene),
                        texcoordMatrix, node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues,
                        overrideColor: null, paletteOverride: null);
                }
            }
        }

        private float GetPortalAlpha(Vector3 portalPosition, Vector3 cameraPosition)
        {
            float between = (portalPosition - cameraPosition).Length;
            return MathF.Min(between / 8, 1);
        }

        public override void GetDisplayVolumes(NewScene scene)
        {
            if (scene.ShowVolumes == VolumeDisplay.Portal)
            {
                for (int i = 0; i < _portals.Count; i++)
                {
                    CollisionPortal portal = _portals[i];
                    Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(4);
                    verts[0] = portal.Point1;
                    verts[1] = portal.Point2;
                    verts[2] = portal.Point3;
                    verts[3] = portal.Point4;
                    float alpha = GetPortalAlpha(portal.Position, scene.CameraPosition);
                    Vector4 color = portal.IsForceField
                        ? new Vector4(16 / 31f, 16 / 31f, 1f, alpha)
                        : new Vector4(16 / 31f, 1f, 16 / 31f, alpha);
                    scene.AddRenderItem(CullingMode.Neither, scene.GetNextPolygonId(), color, RenderItemType.Plane, verts);
                }
            }
            else if (scene.ShowVolumes == VolumeDisplay.KillPlane)
            {
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(4);
                verts[0] = new Vector3(10000f, scene.KillHeight, 10000f);
                verts[1] = new Vector3(10000f, scene.KillHeight, -10000f);
                verts[2] = new Vector3(-10000f, scene.KillHeight, -10000f);
                verts[3] = new Vector3(-10000f, scene.KillHeight, 10000f);
                var color = new Vector4(1f, 0f, 1f, 0.5f);
                scene.AddRenderItem(CullingMode.Neither, scene.GetNextPolygonId(), color, RenderItemType.Plane, verts);
            }
        }

        private readonly struct PortalNodeRef
        {
            public readonly CollisionPortal Portal;
            public readonly int NodeIndex;

            public PortalNodeRef(CollisionPortal portal, int nodeIndex)
            {
                Portal = portal;
                NodeIndex = nodeIndex;
            }
        }
    }
}
