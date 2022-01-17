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
        private readonly IReadOnlyList<Portal> _portals = new List<Portal>();
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
            var portals = new List<Portal>();
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
                foreach (Portal portal in portals)
                {
                    for (int i = 0; i < model.Nodes.Count; i++)
                    {
                        Node node = model.Nodes[i];
                        if (node.Name == portal.NodeName1)
                        {
                            node.IsRoomPartNode = true;
                            portal.NodeIndex1 = i;
                        }
                        if (node.Name == portal.NodeName2)
                        {
                            node.IsRoomPartNode = true;
                            portal.NodeIndex2 = i;
                        }
                    }
                }
                IEnumerable<Portal> pmags = portals.Where(p => p.Name.StartsWith("pmag"));
                foreach (Portal portal in pmags)
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

        private readonly HashSet<int> _activeNodes = new HashSet<int>();

        // this assumes that mutliple non-pmag portals will not be visible "in a row,"
        // and that opaque pmags will prevent any out-of-control "chaining" through many parts
        // --> at least, these conditions will prevent rendering of unwanted parts that are not physically occluded
        private void UpdateRoomParts(int nodeIndex, Vector3 camPos)
        {
            FrustumInfo info = _scene.FrustumInfo;
            for (int i = 0; i < _portals.Count; i++)
            {
                // todo: will probably need active flag check for door portals or whatever
                Portal portal = _portals[i];
                int nextPart = -1;
                Debug.Assert(portal.NodeIndex1 != -1);
                Debug.Assert(portal.NodeIndex2 != -1);
                Debug.Assert(portal.NodeIndex1 != portal.NodeIndex2);
                bool otherSide = false;
                if (portal.NodeIndex1 == nodeIndex)
                {
                    nextPart = portal.NodeIndex2;
                }
                else if (portal.NodeIndex2 == nodeIndex)
                {
                    nextPart = portal.NodeIndex1;
                    otherSide = true;
                }
                // todo?: portal alpha could be remembered between iterations
                if (nextPart == -1 || _activeNodes.Contains(nextPart)
                    || portal.IsForceField && GetPortalAlpha(portal.Position, camPos) == 1)
                {
                    continue;
                }
                bool partActive = false;
                for (int j = 0; j < portal.Points.Count; j++)
                {
                    if (TestPointInFrustum(portal.Points[j], info))
                    {
                        partActive = true;
                        break;
                    }
                }
                if (!partActive)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, info.Position, info.FarTopLeft, otherSide))
                    {
                        partActive = true;
                    }
                    else if (CollisionDetection.CheckPortBetweenPoints(portal, info.Position, info.FarTopRight, otherSide))
                    {
                        partActive = true;
                    }
                    else if (CollisionDetection.CheckPortBetweenPoints(portal, info.Position, info.FarBottomLeft, otherSide))
                    {
                        partActive = true;
                    }
                    else if (CollisionDetection.CheckPortBetweenPoints(portal, info.Position, info.FarBottomRight, otherSide))
                    {
                        partActive = true;
                    }
                }
                if (partActive)
                {
                    _activeNodes.Add(nextPart);
                    UpdateRoomParts(nextPart, camPos);
                }
            }
        }

        private bool TestPointInFrustum(Vector3 point, FrustumInfo info)
        {
            if (Vector3.Dot(info.NearPlane.Xyz, point) + info.NearPlane.W < 0)
            {
                return false;
            }
            if (Vector3.Dot(info.FarPlane.Xyz, point) + info.FarPlane.W < 0)
            {
                return false;
            }
            if (Vector3.Dot(info.TopPlane.Xyz, point) + info.TopPlane.W < 0)
            {
                return false;
            }
            if (Vector3.Dot(info.BottomPlane.Xyz, point) + info.BottomPlane.W < 0)
            {
                return false;
            }
            if (Vector3.Dot(info.LeftPlane.Xyz, point) + info.LeftPlane.W < 0)
            {
                return false;
            }
            if (Vector3.Dot(info.RightPlane.Xyz, point) + info.RightPlane.W < 0)
            {
                return false;
            }
            return true;
        }

        private void UpdateRoomParts(ModelInstance inst)
        {
            if (_scene.CameraMode != CameraMode.Player)
            {
                for (int i = 0; i < inst.Model.Nodes.Count; i++)
                {
                    Node node = inst.Model.Nodes[i];
                    if (node.IsRoomPartNode)
                    {
                        node.RoomPartActive = true;
                    }
                }
                return;
            }
            CameraInfo camInfo = PlayerEntity.Main.CameraInfo;
            int nodeRef = camInfo.NodeRef;
            if (nodeRef == -1)
            {
                for (int i = 0; i < inst.Model.Nodes.Count; i++)
                {
                    Node node = inst.Model.Nodes[i];
                    if (node.IsRoomPartNode)
                    {
                        node.RoomPartActive = false;
                    }
                }
                return;
            }
            _activeNodes.Clear();
            _activeNodes.Add(nodeRef);
            if (PlayerEntity.Main.MorphCamera == null)
            {
                UpdateRoomParts(nodeRef, camInfo.Position);
            }
            for (int i = 0; i < inst.Model.Nodes.Count; i++)
            {
                Node node = inst.Model.Nodes[i];
                if (node.IsRoomPartNode)
                {
                    node.RoomPartActive = _activeNodes.Contains(i);
                }
            }
        }

        public int GetNodeRefByName(string nodeName)
        {
            Model model = _models[0].Model;
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                if (node.Name == nodeName)
                {
                    Debug.Assert(node.IsRoomPartNode);
                    return i;
                }
            }
            return -1;
        }

        public int UpdateNodeRef(int current, Vector3 prevPos, Vector3 curPos)
        {
            Debug.Assert(current != -1);
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (portal.NodeIndex1 == current)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: false))
                    {
                        return portal.NodeIndex2;
                    }
                }
                if (portal.NodeIndex2 == current)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: true))
                    {
                        return portal.NodeIndex1;
                    }
                }
            }
            return current;
        }

        public override void GetDrawInfo()
        {
            if (!Hidden)
            {
                ModelInstance inst = _models[0];
                UpdateTransforms(inst, 0);
                UpdateRoomParts(inst);
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
                    else if (pnode.IsRoomPartNode && pnode.RoomPartActive)
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

            void GetItems(ModelInstance inst, Node node, Portal? portal = null)
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
            between /= 8;
            if (between < 1 / 4096f)
            {
                between = 0;
            }
            return MathF.Min(between, 1);
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.Portal)
            {
                for (int i = 0; i < _portals.Count; i++)
                {
                    Portal portal = _portals[i];
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
            public readonly Portal Portal;
            public readonly int NodeIndex;

            public PortalNodeRef(Portal portal, int nodeIndex)
            {
                Portal = portal;
                NodeIndex = nodeIndex;
            }
        }
    }
}
