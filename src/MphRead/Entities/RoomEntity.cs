using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats;
using MphRead.Formats.Collision;
using MphRead.Formats.Culling;
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
            int partId = 0;
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
                        node.RoomPartId = partId++;
                    }
                }
                foreach (Portal portal in portals)
                {
                    for (int i = 0; i < model.Nodes.Count; i++)
                    {
                        Node node = model.Nodes[i];
                        if (node.Name == portal.NodeName1)
                        {
                            Debug.Assert(node.RoomPartId >= 0);
                            Debug.Assert(node.ChildIndex != -1);
                            portal.NodeRef1 = new NodeRef(node.RoomPartId, node.ChildIndex);
                        }
                        if (node.Name == portal.NodeName2)
                        {
                            Debug.Assert(node.RoomPartId >= 0);
                            Debug.Assert(node.ChildIndex != -1);
                            portal.NodeRef2 = new NodeRef(node.RoomPartId, node.ChildIndex);
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
                roomNode.RoomPartId = 0;
            }
            else
            {
                foreach (Node node in model.Nodes)
                {
                    if (node.Name.StartsWith("rm"))
                    {
                        node.RoomPartId = 0;
                        break;
                    }
                }
            }
            Debug.Assert(model.Nodes.Any(n => n.RoomPartId >= 0));
            _portals = portals;
            _forceFields = forceFields;
            RoomCollision = collision; // todo: transform if connector
            RoomId = roomId;
            for (int i = 0; i < _roomPartMax; i++)
            {
                _partVisInfo[i] = new RoomPartVisInfo();
                _roomFrustumItems[i] = new RoomFrustumItem();
            }
        }

        protected override void GetCollisionDrawInfo()
        {
            RoomCollision.Info.GetDrawInfo(RoomCollision.Info.Points, Type, _scene);
        }

        private const int _roomPartMax = 32;

        // todo: need to maintain an array of audible room parts as well
        private readonly bool[] _activeRoomParts = new bool[_roomPartMax];

        private int _visNodeRefRecursionDepth = 0;

        private RoomPartVisInfo? _partVisInfoHead = null;
        private readonly RoomPartVisInfo[] _partVisInfo = new RoomPartVisInfo[_roomPartMax];

        private RoomPartVisInfo GetPartVisInfo(NodeRef nodeRef)
        {
            RoomPartVisInfo visInfo = _partVisInfo[nodeRef.PartIndex];
            if (!_activeRoomParts[nodeRef.PartIndex])
            {
                visInfo.NodeRef = nodeRef;
                visInfo.ViewMinX = 1;
                visInfo.ViewMaxX = 0;
                visInfo.ViewMinY = 1;
                visInfo.ViewMaxY = 0;
                visInfo.Next = _partVisInfoHead;
                _partVisInfoHead = visInfo;
            }
            return visInfo;
        }

        private int _roomFrustumIndex = 0;
        private readonly RoomFrustumItem[] _roomFrustumItems = new RoomFrustumItem[_roomPartMax];
        private readonly RoomFrustumItem?[] _roomFrustumLinks = new RoomFrustumItem[_roomPartMax];

        private RoomFrustumItem GetRoomFrustumItem()
        {
            Debug.Assert(_roomFrustumIndex != _roomPartMax);
            return _roomFrustumItems[_roomFrustumIndex];
        }

        // skhere
        private void ClearRoomPartState()
        {
            for (int i = 0; i < _roomPartMax; i++)
            {
                _activeRoomParts[i] = false;
                _roomFrustumLinks[i] = null;
            }
            _visNodeRefRecursionDepth = 0;
            _roomFrustumIndex = 0;
            _partVisInfoHead = null;
        }

        private void UpdateRoomParts()
        {
            NodeRef curNodeRef = PlayerEntity.Main.CameraInfo.NodeRef;
            if (_scene.CameraMode != CameraMode.Player || curNodeRef.PartIndex == -1)
            {
                return;
            }
            Debug.Assert(curNodeRef.NodeIndex != -1);
            // sktodo: handle multiple rooms (connectors)
            RoomPartVisInfo curVisInfo = GetPartVisInfo(curNodeRef);
            curVisInfo.ViewMinX = 0;
            curVisInfo.ViewMaxX = 1;
            curVisInfo.ViewMinY = 0;
            curVisInfo.ViewMaxY = 1;
            _activeRoomParts[curNodeRef.PartIndex] = true;
            RoomFrustumItem curRoomFrustum = GetRoomFrustumItem();
            _roomFrustumIndex++;
            curRoomFrustum.Info.Count = _scene.FrustumInfo.Count; // always 5
            curRoomFrustum.Info.Index = _scene.FrustumInfo.Index; // always 1
            for (int i = 0; i < curRoomFrustum.Info.Planes.Length; i++)
            {
                curRoomFrustum.Info.Planes[i] = _scene.FrustumInfo.Planes[i];
            }
            curRoomFrustum.NodeRef = curNodeRef;
            RoomFrustumItem? link = _roomFrustumLinks[curNodeRef.PartIndex];
            curRoomFrustum.Next = link;
            _roomFrustumLinks[curNodeRef.PartIndex] = curRoomFrustum;
            FindVisibleRoomParts(curRoomFrustum);
        }

        private void FindVisibleRoomParts(RoomFrustumItem frustumItem)
        {
            // sktodo
        }

        public NodeRef GetNodeRefByName(string nodeName)
        {
            Model model = _models[0].Model;
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                if (node.Name == nodeName)
                {
                    Debug.Assert(node.RoomPartId >= 0);
                    Debug.Assert(node.ChildIndex != -1);
                    return new NodeRef(node.RoomPartId, node.ChildIndex);
                }
            }
            return NodeRef.None;
        }

        public NodeRef UpdateNodeRef(NodeRef current, Vector3 prevPos, Vector3 curPos)
        {
            Debug.Assert(current.PartIndex != -1);
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (portal.NodeRef1.PartIndex == current.PartIndex)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: false))
                    {
                        return portal.NodeRef2;
                    }
                }
                if (portal.NodeRef2.PartIndex == current.PartIndex)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: true))
                    {
                        return portal.NodeRef1;
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
                ClearRoomPartState();
                UpdateRoomParts();
                if (_partVisInfoHead == null || _scene.ShowAllNodes)
                {
                    DrawAllNodes(inst);
                }
                else
                {
                    DrawRoomParts(inst);
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
        }

        private bool IsNodeVisible(FrustumInfo frustumInfo, Node node, int mask)
        {
            float[] bounds = node.Bounds;
            for (int i = 0; i < frustumInfo.Count; i++)
            {
                Debug.Assert((mask & (1 << i)) != 0); // sktodo: if this never happens, we can just get rid of mask
                FrustumPlane frustumPlane = frustumInfo.Planes[i];
                // frustum planes have outward facing normals -- near plane = false if min bounds are on outer side,
                // right plane = false if min bounds are on outer side, left plane = false if max bounds are on outer side,
                // bottom plane = false if max bounds are on outer side, top plane = false if min bounds are on outer side
                Vector4 plane = frustumPlane.Plane;
                if (plane.X * bounds[frustumPlane.XIndex2] + plane.Y * bounds[frustumPlane.YIndex2]
                    + plane.Z * bounds[frustumPlane.ZIndex2] - plane.W < 0)
                {
                    return false;
                }
                if (plane.X * bounds[frustumPlane.XIndex1] + plane.Y * bounds[frustumPlane.YIndex1]
                    + plane.Z * bounds[frustumPlane.ZIndex1] - plane.W >= 0)
                {
                    mask &= ~(1 << i);
                }
            }
            return true;
        }

        private void DrawRoomParts(ModelInstance inst)
        {
            RoomPartVisInfo? roomPart = _partVisInfoHead;
            while (roomPart != null)
            {
                RoomFrustumItem? frustumItem = _roomFrustumLinks[roomPart.NodeRef.PartIndex];
                int nodeIndex = roomPart.NodeRef.NodeIndex;
                Debug.Assert(frustumItem != null);
                Debug.Assert(nodeIndex != -1);
                while (nodeIndex != -1)
                {
                    Node? node = inst.Model.Nodes[nodeIndex];
                    Debug.Assert(node.ChildIndex == -1);
                    if (!node.Enabled || node.MeshCount == 0)
                    {
                        nodeIndex = node.NextIndex;
                        continue;
                    }
                    RoomFrustumItem? frustumLink = frustumItem;
                    while (frustumLink != null)
                    {
                        if (IsNodeVisible(frustumLink.Info, node, 0x8FFF))
                        {
                            GetItems(inst, node);
                            break;
                        }
                        frustumLink = frustumLink.Next;
                    }
                    nodeIndex = node.NextIndex;
                }
                roomPart = roomPart.Next;
            }
            // sktodo?
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

        private void DrawAllNodes(ModelInstance inst)
        {
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
                else if (pnode.RoomPartId >= 0)
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

        private void GetItems(ModelInstance inst, Node node, Portal? portal = null)
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

        // sktodo: make node bounds display a feature
        private int _nodeIndex = 16;

        public override void GetDisplayVolumes()
        {
            //Node node = _models[0].Model.Nodes[_nodeIndex];
            //float width = node.MaxBounds.X - node.MinBounds.X;
            //float height = node.MaxBounds.Y - node.MinBounds.Y;
            //float depth = node.MaxBounds.Z - node.MinBounds.Z;
            //var box = new CollisionVolume(Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ,
            //    node.MinBounds.WithZ(node.MaxBounds.Z), width, height, depth);
            //AddVolumeItem(box, Vector3.UnitX);
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
