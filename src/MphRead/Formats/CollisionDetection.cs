using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Formats
{
    // sktodo: entity collision
    public class CollisionCandidate
    {
        public MphCollisionInfo Collision { get; set; }
        public CollisionEntry Entry { get; set; }

        public CollisionCandidate(MphCollisionInfo collision, CollisionEntry entry)
        {
            Collision = collision;
            Entry = entry;
        }
    }

    public enum TestFlags
    {
        None = 0x0,
        AffectsPlayers = 0x2000,
        AffectsBeams = 0x4000,
        IncludeEntities = 0x4000,
    }

    public struct CollisionResult
    {
        public int Field0;
        public int Field14;
        public float Distance; // percentage
        public Vector3 Position;
        public Vector4 Plane;
        public CollisionFlags Flags;

        // bits 5-8
        public Terrain Terrain => (Terrain)(((ushort)Flags & 0x1E0) >> 5);
    }

    public static class CollisionDetection
    {
        private static readonly List<CollisionCandidate> _activeItems = new List<CollisionCandidate>(2048);
        private static readonly Queue<CollisionCandidate> _inactiveItems = new Queue<CollisionCandidate>(2048);

        public static void Init()
        {
            for (int i = 0; i < 2048; i++)
            {
                _inactiveItems.Enqueue(new CollisionCandidate(null!, default));
            }
        }

        // sktodo: allow passing in a candidate list, query if not passed
        public static bool CheckBetweenPoints(Vector3 point1, Vector3 point2, TestFlags flags, Scene scene, ref CollisionResult result)
        {
            bool collided = false;
            ushort mask = 0;
            if (flags.TestFlag(TestFlags.AffectsPlayers))
            {
                mask |= (ushort)CollisionFlags.IgnorePlayers;
            }
            if (flags.TestFlag(TestFlags.AffectsBeams))
            {
                mask |= (ushort)CollisionFlags.IgnoreBeams;
            }
            float minDist = Single.MaxValue;
            IReadOnlyList<CollisionCandidate> candidates = GetRoomCandidatesForPoints(point1, point2, scene);
            for (int i = 0; i < candidates.Count; i++)
            {
                CollisionCandidate candidate = candidates[i];
                MphCollisionInfo info = candidate.Collision;
                Debug.Assert(candidate.Entry.DataCount > 0);
                for (int j = 0; j < candidate.Entry.DataCount; j++)
                {
                    // sktodo: counter
                    CollisionData data = info.Data[info.DataIndices[candidate.Entry.DataStartIndex + j]];
                    if (((ushort)data.Flags & mask) == 0)
                    {
                        Vector4 plane = info.Planes[data.PlaneIndex];
                        float dot1 = Vector3.Dot(point1, plane.Xyz) - plane.W;
                        if (dot1 > 0)
                        {
                            float dot2 = Vector3.Dot(point2, plane.Xyz) - plane.W;
                            if (dot2 <= 0)
                            {
                                float dist = dot1 / (dot1 - dot2);
                                if (dist > 1)
                                {
                                    dist = 1;
                                }
                                else if (dist < 0)
                                {
                                    dist = 0;
                                }
                                if (dist < minDist)
                                {
                                    // point of intersection with plane
                                    var pos = new Vector3(
                                        point1.X + (point2.X - point1.X) * dist,
                                        point1.Y + (point2.Y - point1.Y) * dist,
                                        point1.Z + (point2.Z - point1.Z) * dist
                                    );
                                    if (CheckPointOnFace(pos, info, data))
                                    {
                                        minDist = dist;
                                        result.Field0 = 0;
                                        result.Field14 = 0;
                                        result.Position = pos;
                                        result.Plane = plane;
                                        result.Flags = data.Flags;
                                        result.Distance = dist;
                                        collided = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return collided;
        }

        private static bool CheckPointOnFace(Vector3 point, MphCollisionInfo info, CollisionData data)
        {
            int axis = data.LayerMask & 3;
            Debug.Assert(axis >= 0 && axis <= 2);
            if (axis == 0)
            {
                // normal is majority x axis
                Vector3 curVert = info.Points[info.PointIndices[data.PointStartIndex]];
                Vector3 firstVert = curVert;
                int v8 = 0;
                int v1 = curVert.Y <= point.Y
                    ? curVert.Z <= point.Z ? 2 : 1
                    : curVert.Z <= point.Z ? 3 : 0;
                int v39 = 0;
                Vector3 nextVert;
                do
                {
                    if (++v8 == data.PointIndexCount)
                    {
                        v8 = 0;
                    }
                    nextVert = info.Points[info.PointIndices[data.PointStartIndex + v8]];
                    int v13 = nextVert.Y <= point.Y
                        ? nextVert.Z <= point.Z ? 2 : 1
                        : nextVert.Z <= point.Z ? 3 : 0;
                    int v14 = v13 - v1;
                    switch (v14)
                    {
                    case -2:
                    case 2:
                        float v15 = (curVert.Y - nextVert.Y) / (curVert.Z - nextVert.Z);
                        if (nextVert.Y - (nextVert.Z - point.Z) * v15 > point.Y)
                        {
                            v14 = -v14;
                        }
                        break;
                    case -3:
                        v14 = 1;
                        break;
                    case 3:
                        v14 = -1;
                        break;
                    }
                    v39 += v14;
                    v1 = v13;
                    curVert = nextVert;
                }
                while (nextVert != firstVert);
                if (v39 == 4 || v39 == -4)
                {
                    return true;
                }
            }
            else if (axis == 1)
            {
                // normal is majority y axis
                Vector3 curVert = info.Points[info.PointIndices[data.PointStartIndex]];
                Vector3 firstVert = curVert;
                int v8 = 0;
                int v1 = curVert.X <= point.X
                    ? curVert.Z <= point.Z ? 2 : 1
                    : curVert.Z <= point.Z ? 3 : 0;
                int v39 = 0;
                Vector3 nextVert;
                do
                {
                    if (++v8 == data.PointIndexCount)
                    {
                        v8 = 0;
                    }
                    nextVert = info.Points[info.PointIndices[data.PointStartIndex + v8]];
                    int v13 = nextVert.X <= point.X
                        ? nextVert.Z <= point.Z ? 2 : 1
                        : nextVert.Z <= point.Z ? 3 : 0;
                    int v14 = v13 - v1;
                    switch (v14)
                    {
                    case -2:
                    case 2:
                        float v15 = (curVert.X - nextVert.X) / (curVert.Z - nextVert.Z);
                        if (nextVert.X - (nextVert.Z - point.Z) * v15 > point.X)
                        {
                            v14 = -v14;
                        }
                        break;
                    case -3:
                        v14 = 1;
                        break;
                    case 3:
                        v14 = -1;
                        break;
                    }
                    v39 += v14;
                    v1 = v13;
                    curVert = nextVert;
                }
                while (nextVert != firstVert);
                if (v39 == 4 || v39 == -4)
                {
                    return true;
                }
            }
            else // if (axis == 2)
            {
                // normal is majority z axis
                Vector3 curVert = info.Points[info.PointIndices[data.PointStartIndex]];
                Vector3 firstVert = curVert;
                int v8 = 0;
                int v1 = curVert.X <= point.X
                    ? curVert.Y <= point.Y ? 2 : 1
                    : curVert.Y <= point.Y ? 3 : 0;
                int v39 = 0;
                Vector3 nextVert;
                do
                {
                    if (++v8 == data.PointIndexCount)
                    {
                        v8 = 0;
                    }
                    nextVert = info.Points[info.PointIndices[data.PointStartIndex + v8]];
                    int v13 = nextVert.X <= point.X
                        ? nextVert.Y <= point.Y ? 2 : 1
                        : nextVert.Y <= point.Y ? 3 : 0;
                    int v14 = v13 - v1;
                    switch (v14)
                    {
                    case -2:
                    case 2:
                        float v15 = (curVert.X - nextVert.X) / (curVert.Y - nextVert.Y);
                        if (nextVert.X - (nextVert.Y - point.Y) * v15 > point.X)
                        {
                            v14 = -v14;
                        }
                        break;
                    case -3:
                        v14 = 1;
                        break;
                    case 3:
                        v14 = -1;
                        break;
                    }
                    v39 += v14;
                    v1 = v13;
                    curVert = nextVert;
                }
                while (nextVert != firstVert);
                if (v39 == 4 || v39 == -4)
                {
                    return true;
                }
            }
            return false;
        }

        private static IReadOnlyList<CollisionCandidate> GetRoomCandidatesForPoints(Vector3 point1, Vector3 point2, Scene scene)
        {
            while (_activeItems.Count > 0)
            {
                CollisionCandidate item = _activeItems[0];
                _activeItems.Remove(item);
                _inactiveItems.Enqueue(item);
            }
            if (scene.Collision.Count == 0 || scene.Collision[0].IsEntity)
            {
                return _activeItems;
            }
            // sktodo: handle FH collision
            var info = (MphCollisionInfo)scene.Collision[0].Info;
            float size = 4;
            int partsX = info.Header.PartsX;
            int partsY = info.Header.PartsY;
            int partsZ = info.Header.PartsZ;
            Vector3 minPos = info.MinPosition;
            var maxPos = new Vector3(
                minPos.X + partsX * size - Fixed.ToFloat(20),
                minPos.Y + partsY * size - Fixed.ToFloat(20),
                minPos.Z + partsZ * size - Fixed.ToFloat(20)
            );

            int TestBounds(Vector3 point)
            {
                int bits = 0;
                if (point.X < minPos.X)
                {
                    bits |= 0x1;
                }
                if (point.X > maxPos.X)
                {
                    bits |= 0x2;
                }
                if (point.Y < minPos.Y)
                {
                    bits |= 0x4;
                }
                if (point.Y > maxPos.Y)
                {
                    bits |= 0x8;
                }
                if (point.Z < minPos.Z)
                {
                    bits |= 0x10;
                }
                if (point.Z > maxPos.Z)
                {
                    bits |= 0x20;
                }
                return bits;
            }

            int test1 = TestBounds(point1);
            int test2 = TestBounds(point2);
            if ((test1 & test2) != 0)
            {
                // if any coordinate of both points is outside the bounds
                return _activeItems;
            }
            bool inside = false;
            if (test1 == 0)
            {
                // if no coordinates of point 1 are outside the bounds
                inside = true;
            }
            else
            {
                if ((test1 & 0x3) != 0)
                {
                    // x coordinate
                    float v7;
                    float v8;
                    if ((test1 & 0x1) != 0)
                    {
                        // less than min
                        v7 = minPos.X - point1.X;
                        v8 = minPos.X - point2.X;
                    }
                    else // if ((test1 & 0x2) != 0)
                    {
                        // greater than max
                        v7 = point1.X - maxPos.X;
                        v8 = point2.X - maxPos.X;
                    }
                    if (v7 >= 0 && v8 < 0)
                    {
                        float div = v7 / (v7 - v8);
                        var newPoint = new Vector3(
                            (point2.X - point1.X) * div + point1.X,
                            (point2.Y - point1.Y) * div + point1.Y,
                            (point2.Z - point1.Z) * div + point1.Z
                        );
                        if (newPoint.Y >= minPos.Y && newPoint.Y <= maxPos.Y
                          && newPoint.Z >= minPos.Z && newPoint.Z <= maxPos.Z)
                        {
                            point1 = newPoint;
                            inside = true;
                        }
                    }
                }
                if (!inside && (test1 & 0xC) != 0)
                {
                    // y coordinate
                    float v7;
                    float v8;
                    if ((test1 & 0x4) != 0)
                    {
                        // less than min
                        v7 = minPos.Y - point1.Y;
                        v8 = minPos.Y - point2.Y;
                    }
                    else // if ((test1 & 0x8) != 0)
                    {
                        // greater than max
                        v7 = point1.Y - maxPos.Y;
                        v8 = point2.Y - maxPos.Y;
                    }
                    if (v7 >= 0 && v8 < 0)
                    {
                        float div = v7 / (v7 - v8);
                        var newPoint = new Vector3(
                            (point2.X - point1.X) * div + point1.X,
                            (point2.Y - point1.Y) * div + point1.Y,
                            (point2.Z - point1.Z) * div + point1.Z
                        );
                        if (newPoint.X >= minPos.X && newPoint.X <= maxPos.X
                          && newPoint.Z >= minPos.Z && newPoint.Z <= maxPos.Z)
                        {
                            point1 = newPoint;
                            inside = true;
                        }
                    }
                }
                if (!inside && (test1 & 0x30) != 0)
                {
                    // y coordinate
                    float v7;
                    float v8;
                    if ((test1 & 0x10) != 0)
                    {
                        // less than min
                        v7 = minPos.Z - point1.Z;
                        v8 = minPos.Z - point2.Z;
                    }
                    else // if ((test1 & 0x20) != 0)
                    {
                        // greater than max
                        v7 = point1.Z - maxPos.Z;
                        v8 = point2.Z - maxPos.Z;
                    }
                    if (v7 >= 0 && v8 < 0)
                    {
                        float div = v7 / (v7 - v8);
                        var newPoint = new Vector3(
                            (point2.X - point1.X) * div + point1.X,
                            (point2.Y - point1.Y) * div + point1.Y,
                            (point2.Z - point1.Z) * div + point1.Z
                        );
                        if (newPoint.X >= minPos.X && newPoint.X <= maxPos.X
                          && newPoint.Y >= minPos.Y && newPoint.Y <= maxPos.Y)
                        {
                            point1 = newPoint;
                            inside = true;
                        }
                    }
                }
            }
            if (!inside)
            {
                return _activeItems;
            }
            Vector3 dir = (point2 - point1).Normalized();
            dir = new Vector3(
                Fixed.ToFloat(Fixed.ToInt(dir.X)),
                Fixed.ToFloat(Fixed.ToInt(dir.Y)),
                Fixed.ToFloat(Fixed.ToInt(dir.Z))
            );
            float step = Fixed.ToFloat(0x400);
            int curX = (int)((point1.X - minPos.X) * step);
            int endX = (int)((point2.X - minPos.X) * step);
            int curY = (int)((point1.Y - minPos.Y) * step);
            int endY = (int)((point2.Y - minPos.Y) * step);
            int curZ = (int)((point1.Z - minPos.Z) * step);
            int endZ = (int)((point2.Z - minPos.Z) * step);
            int xSign = dir.X <= 0 ? -1 : 1;
            int xLimit = dir.X <= 0 ? -1 : partsX;
            float xStart = dir.X <= 0
                ? curX * size + minPos.X
                : (curX + 1) * size + minPos.X;
            int ySign = dir.Y <= 0 ? -1 : 1;
            int yLimit = dir.Y <= 0 ? -1 : partsY;
            float yStart = dir.Y <= 0
                ? curY * size + minPos.Y
                : (curY + 1) * size + minPos.Y;
            int zSign = dir.Z <= 0 ? -1 : 1;
            int zLimit = dir.Z <= 0 ? -1 : partsZ;
            float zStart = dir.Z <= 0
                ? curZ * size + minPos.Z
                : (curZ + 1) * size + minPos.Z;
            float xNext = 1000000;
            float yNext = 1000000;
            float zNext = 1000000;
            float xInc = 0;
            float yInc = 0;
            float zInc = 0;
            if (dir.X != 0)
            {
                float div = 1f / dir.X;
                xNext = (xStart - point1.X) * div;
                xInc = xSign * div * size;
            }
            if (dir.Y != 0)
            {
                float div = 1f / dir.Y;
                yNext = (yStart - point1.Y) * div;
                yInc = ySign * div * size;
            }
            if (dir.Z != 0)
            {
                float div = 1f / dir.Z;
                zNext = (zStart - point1.Z) * div;
                zInc = zSign * div * size;
            }
            while (true)
            {
                if (curX >= 0 && curX < partsX && curY >= 0 && curY < partsY && curZ >= 0 && curZ < partsZ)
                {
                    int entryIndex = curX + partsX * (curZ + curY * partsZ);
                    CollisionEntry entry = info.Entries[entryIndex];
                    if (entry.DataCount > 0)
                    {
                        CollisionCandidate item = _inactiveItems.Dequeue();
                        item.Collision = info;
                        item.Entry = entry;
                        _activeItems.Add(item);
                    }
                }
                if (curX == endX && curY == endY && curZ == endZ)
                {
                    // if last block checked is the block where the destination point is
                    break;
                }
                // increment component to adjacent block based on the direction
                // --> if any block index surpasses its limit, exit
                if (xNext >= yNext)
                {
                    if (yNext >= zNext)
                    {
                        curZ += zSign;
                        if (curZ == zLimit)
                        {
                            break;
                        }
                        zNext += zInc;
                    }
                    else
                    {
                        curY += ySign;
                        if (curY == yLimit)
                        {
                            break;
                        }
                        yNext += yInc;
                    }
                }
                else if (xNext >= zNext)
                {
                    curZ += zSign;
                    if (curZ == zLimit)
                    {
                        break;
                    }
                    zNext += zInc;
                }
                else
                {
                    curX += xSign;
                    if (curX == xLimit)
                    {
                        break;
                    }
                    xNext += xInc;
                }
            }
            return _activeItems;
        }
    }
}
