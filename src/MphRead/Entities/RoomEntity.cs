using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class RoomEntity : EntityBase
    {
        public CollisionInstance RoomCollision { get; }
        private readonly IReadOnlyList<CollisionPortal> _portals = new List<CollisionPortal>();
        private readonly IReadOnlyList<PortalNodeRef> _forceFields = new List<PortalNodeRef>();
        private IReadOnlyList<Node> Nodes => _models[0].Model.Nodes;
        private readonly RoomMetadata _meta;
        private readonly NodeData? _nodeData;
        private readonly float[]? _emptyMatrixStack;

        protected override bool UseNodeTransform => false; // default -- will use transform if setting is enabled
        public int RoomId { get; private set; }
        public RoomMetadata Metadata => _meta;

        public RoomEntity(string name, RoomMetadata meta, CollisionInstance collision, NodeData? nodeData,
            int layerMask, int roomId, Scene scene) : base(EntityType.Room, scene)
        {
            ModelInstance inst = Read.GetRoomModelInstance(name);
            _models.Add(inst);
            inst.SetAnimation(0);
            inst.Model.FilterNodes(layerMask);
            if (meta.Name == "UNIT2_C6")
            {
                // manually disable a decal that isn't rendered in-game because it's not on a surface
                Nodes[46].Enabled = false;
            }
            _meta = meta;
            Model model = inst.Model;
            _nodeData = nodeData;
            if (nodeData != null)
            {
                // using cached instance messes with placeholders since the room entity doesn't update its instances normally
                _models.Add(Read.GetModelInstance("pick_wpn_missile", noCache: true));
                _emptyMatrixStack = Array.Empty<float>();
            }
            var portals = new List<CollisionPortal>();
            var forceFields = new List<PortalNodeRef>();
            // portals are already filtered by layer mask
            portals.AddRange(collision.Info.Portals);
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
                && model.Nodes.TryFind(n => n.Name == meta.RoomNodeName && n.ChildIndex != -1, out Node? roomNode))
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
            RoomCollision = collision; // todo: transform if connector
            RoomId = roomId;
        }

        protected override void GetCollisionDrawInfo()
        {
            RoomCollision.Info.GetDrawInfo(RoomCollision.Info.Points, Type, _scene);
        }

        public override void GetDrawInfo()
        {
            if (!Hidden)
            {
                ModelInstance inst = _models[0];
                UpdateTransforms(inst, 0);
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Node pnode = Nodes[i];
                    if (!pnode.Enabled)
                    {
                        continue;
                    }
                    if (_scene.ShowAllNodes)
                    {
                        GetItems(inst, pnode);
                    }
                    else if (pnode.IsRoomPartNode)
                    {
                        int childIndex = pnode.ChildIndex;
                        if (childIndex != -1)
                        {
                            Node node = Nodes[childIndex];
                            Debug.Assert(node.ChildIndex == -1);
                            GetItems(inst, node);
                            int nextIndex = node.NextIndex;
                            while (nextIndex != -1)
                            {
                                node = Nodes[nextIndex];
                                GetItems(inst, node);
                                nextIndex = node.NextIndex;
                            }
                        }
                    }
                }
                if (_scene.ShowForceFields)
                {
                    for (int i = 0; i < _forceFields.Count; i++)
                    {
                        PortalNodeRef forceField = _forceFields[i];
                        Node pnode = Nodes[forceField.NodeIndex];
                        if (pnode.ChildIndex != -1)
                        {
                            Node node = Nodes[pnode.ChildIndex];
                            GetItems(inst, node, forceField.Portal);
                            int nextIndex = node.NextIndex;
                            while (nextIndex != -1)
                            {
                                node = Nodes[nextIndex];
                                GetItems(inst, node, forceField.Portal);
                                nextIndex = node.NextIndex;
                            }
                        }
                    }
                }
            }
            if (_scene.ShowCollision && (_scene.ColEntDisplay == EntityType.All || _scene.ColEntDisplay == Type))
            {
                GetCollisionDrawInfo();
            }
            if (_nodeData != null && _scene.ShowNodeData)
            {
                Debug.Assert(_emptyMatrixStack != null);
                Debug.Assert(_models.Count == 2);
                ModelInstance inst = _models[1];
                int polygonId = _scene.GetNextPolygonId();
                for (int i = 0; i < _nodeData.Data.Count; i++)
                {
                    IReadOnlyList<IReadOnlyList<NodeData3>> str1 = _nodeData.Data[i];
                    for (int j = 0; j < str1.Count; j++)
                    {
                        IReadOnlyList<NodeData3> str2 = str1[j];
                        for (int k = 0; k < str2.Count; k++)
                        {
                            NodeData3 str3 = str2[k];
                            GetNodeDataItem(inst, str3.Transform, str3.Color, polygonId);
                        }
                    }
                }
            }

            void GetNodeDataItem(ModelInstance inst, Matrix4 transform, Vector4 color, int polygonId)
            {
                Model model = inst.Model;
                Node node = model.Nodes[3];
                if (node.Enabled)
                {
                    int start = node.MeshId / 2;
                    for (int k = 0; k < node.MeshCount; k++)
                    {
                        Mesh mesh = model.Meshes[start + k];
                        if (!mesh.Visible)
                        {
                            continue;
                        }
                        Material material = model.Materials[mesh.MaterialId];
                        _scene.AddRenderItem(material, polygonId, 1, Vector3.Zero, GetLightInfo(), Matrix4.Identity,
                            transform, mesh.ListId, 0, _emptyMatrixStack, color, null, SelectionType.None, node.BillboardMode);
                    }
                }
            }

            void GetItems(ModelInstance inst, Node node, CollisionPortal? portal = null)
            {
                if (!node.Enabled)
                {
                    return;
                }
                Model model = inst.Model;
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
                        polygonId = _scene.GetNextPolygonId();
                        alpha = GetPortalAlpha(portal.Position, _scene.CameraPosition);
                    }
                    else if (material.RenderMode == RenderMode.Translucent)
                    {
                        polygonId = _scene.GetNextPolygonId();
                    }
                    Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node);
                    SelectionType selectionType = Selection.CheckSelection(this, inst, node, mesh);
                    _scene.AddRenderItem(material, polygonId, alpha, emission: Vector3.Zero, GetLightInfo(),
                        texcoordMatrix, node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues,
                        overrideColor: null, paletteOverride: null, selectionType, node.BillboardMode);
                }
            }
        }

        private float GetPortalAlpha(Vector3 portalPosition, Vector3 cameraPosition)
        {
            float between = (portalPosition - cameraPosition).Length;
            return MathF.Min(between / 8, 1);
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.Portal)
            {
                for (int i = 0; i < _portals.Count; i++)
                {
                    CollisionPortal portal = _portals[i];
                    int count = portal.Points.Count;
                    Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(count);
                    for (int j = 0; j < count; j++)
                    {
                        verts[j] = portal.Points[j];
                    }
                    float alpha = GetPortalAlpha(portal.Position, _scene.CameraPosition);
                    Vector4 color = portal.IsForceField
                        ? new Vector4(16 / 31f, 16 / 31f, 1f, alpha)
                        : new Vector4(16 / 31f, 1f, 16 / 31f, alpha);
                    _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Ngon, verts, count, noLines: true);
                }
            }
            else if (_scene.ShowVolumes == VolumeDisplay.KillPlane && !_meta.FirstHunt)
            {
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(4);
                verts[0] = new Vector3(10000f, _scene.KillHeight, 10000f);
                verts[1] = new Vector3(10000f, _scene.KillHeight, -10000f);
                verts[2] = new Vector3(-10000f, _scene.KillHeight, -10000f);
                verts[3] = new Vector3(-10000f, _scene.KillHeight, 10000f);
                var color = new Vector4(1f, 0f, 1f, 0.5f);
                _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Quad, verts, noLines: true);
            }
            else if ((_scene.ShowVolumes == VolumeDisplay.CameraLimit || _scene.ShowVolumes == VolumeDisplay.PlayerLimit) && _meta.HasLimits)
            {
                Vector3 minLimit = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? _meta.CameraMin : _meta.PlayerMin;
                Vector3 maxLimit = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? _meta.CameraMax : _meta.PlayerMax;
                Vector3[] bverts = ArrayPool<Vector3>.Shared.Rent(8);
                Vector3 point0 = minLimit;
                var sideX = new Vector3(maxLimit.X - minLimit.X, 0, 0);
                var sideY = new Vector3(0, maxLimit.Y - minLimit.Y, 0);
                var sideZ = new Vector3(0, 0, maxLimit.Z - minLimit.Z);
                bverts[0] = point0;
                bverts[1] = point0 + sideZ;
                bverts[2] = point0 + sideX;
                bverts[3] = point0 + sideX + sideZ;
                bverts[4] = point0 + sideY;
                bverts[5] = point0 + sideY + sideZ;
                bverts[6] = point0 + sideX + sideY;
                bverts[7] = point0 + sideX + sideY + sideZ;
                Vector4 color = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? new Vector4(1, 0, 0.69f, 0.5f) : new Vector4(1, 0, 0, 0.5f);
                _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Box, bverts, 8);
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
