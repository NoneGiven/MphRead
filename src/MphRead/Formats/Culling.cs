using System;
using OpenTK.Mathematics;

namespace MphRead.Formats.Culling
{
    public struct NodeRef
    {
        public int PartIndex;
        public int NodeIndex;

        public static readonly NodeRef None = new NodeRef { PartIndex = -1, NodeIndex = -1 };

        public NodeRef(int partIndex, int nodeIndex)
        {
            PartIndex = partIndex;
            NodeIndex = nodeIndex;
        }

        public static bool operator ==(NodeRef lhs, NodeRef rhs)
        {
            return lhs.PartIndex == rhs.PartIndex && lhs.NodeIndex == rhs.NodeIndex;
        }

        public static bool operator !=(NodeRef lhs, NodeRef rhs)
        {
            return lhs.PartIndex != rhs.PartIndex || lhs.NodeIndex != rhs.NodeIndex;
        }

        public override bool Equals(object? obj)
        {
            return obj is NodeRef other && PartIndex == other.PartIndex && NodeIndex == other.NodeIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PartIndex, NodeIndex);
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
