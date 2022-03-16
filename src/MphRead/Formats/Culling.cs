using System;
using OpenTK.Mathematics;

namespace MphRead.Formats.Culling
{
    public struct NodeRef
    {
        public int PartIndex;
        public int NodeIndex;
        public int ModelIndex;

        public static readonly NodeRef None = new NodeRef
        {
            PartIndex = -1,
            NodeIndex = -1,
            ModelIndex = -1
        };

        public NodeRef(int partIndex, int nodeIndex, int modelIndex)
        {
            PartIndex = partIndex;
            NodeIndex = nodeIndex;
            ModelIndex = modelIndex;
        }

        public static bool operator ==(NodeRef lhs, NodeRef rhs)
        {
            return lhs.PartIndex == rhs.PartIndex && lhs.NodeIndex == rhs.NodeIndex
                && lhs.ModelIndex == rhs.ModelIndex;
        }

        public static bool operator !=(NodeRef lhs, NodeRef rhs)
        {
            return lhs.PartIndex != rhs.PartIndex || lhs.NodeIndex != rhs.NodeIndex
                || lhs.ModelIndex != rhs.ModelIndex;
        }

        public override bool Equals(object? obj)
        {
            return obj is NodeRef other && PartIndex == other.PartIndex
                && NodeIndex == other.NodeIndex && ModelIndex == other.ModelIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PartIndex, NodeIndex, ModelIndex);
        }
    }

    public class RoomPartVisInfo
    {
        public NodeRef NodeRef;
        public float ViewMinX;
        public float ViewMaxX;
        public float ViewMinY;
        public float ViewMaxY;
        public RoomPartVisInfo? Next;
    }

    public class RoomFrustumItem
    {
        public NodeRef NodeRef;
        public readonly FrustumInfo Info;
        public RoomFrustumItem? Next;

        public RoomFrustumItem()
        {
            NodeRef = NodeRef.None;
            Info = new FrustumInfo();
        }
    }

    public class FrustumInfo
    {
        public int Index;
        public int Count;
        public readonly FrustumPlane[] Planes = new FrustumPlane[10];
    }

    public struct FrustumPlane
    {
        public int XIndex1;
        public int XIndex2;
        public int YIndex1;
        public int YIndex2;
        public int ZIndex1;
        public int ZIndex2;
        public Vector4 Plane;
    }
}
