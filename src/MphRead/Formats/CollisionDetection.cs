using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Formats
{
    public class CollisionCandidate
    {
        public EntityCollision? EntityCollision { get; set; }
        public CollisionInstance Collision { get; set; }
        public CollisionEntry Entry { get; set; }

        public CollisionCandidate(CollisionInstance collision, CollisionEntry entry)
        {
            Collision = collision;
            Entry = entry;
        }
    }

    public enum TestFlags
    {
        None = 0x0,
        Players = 0x2000,
        Beams = 0x4000,
        Scan = 0x8000
    }

    // Field0 is set in check_between_points_sphere and check_blocker_in_radius
    // Flags and Plane are set in check_blocker_between_points, check_between_points_sphere, and check_blocker_in_radius
    // Field14 is set in check_between_points_sphere and check_blocker_in_radius
    // Position, Distance, and EntityCollision are set in check_blocker_between_points and check_between_points_sphere
    // EdgePoint1 and EdgePoint2 are set in check_between_points_sphere
    public struct CollisionResult
    {
        public byte Field0; // todo: field name
        public CollisionFlags Flags;
        public Vector4 Plane;
        public float Field14; // todo: field name
        public Vector3 Position;
        public float Distance; // percentage
        public EntityCollision? EntityCollision;
        public Vector3 EdgePoint1; // when one side of sphere overlaps with the collision
        public Vector3 EdgePoint2;

        // bits 3-4
        public int Slipperiness => ((ushort)Flags & 0x18) >> 3;

        // bits 5-8
        public Terrain Terrain => (Terrain)(((ushort)Flags & 0x1E0) >> 5);
    }

    public static class CollisionDetection
    {
        private static readonly List<CollisionCandidate> _activeItems = new List<CollisionCandidate>(2048);
        private static readonly Queue<CollisionCandidate> _inactiveItems = new Queue<CollisionCandidate>(2048);
        // due to using linked lists, the game checks collision with room candidates in the reverse order as they were found,
        // then checks collision with entity candidates in the reverse order as they were found,
        // so we need this temporary collection to add everything to _activeItems in the right order
        private static readonly Stack<CollisionCandidate> _tempItems = new Stack<CollisionCandidate>(2048);

        public static void Init()
        {
            for (int i = 0; i < 2048; i++)
            {
                _inactiveItems.Enqueue(new CollisionCandidate(null!, default));
            }
        }

        public static bool CheckBetweenPoints(IReadOnlyList<CollisionCandidate> candidates, Vector3 point1, Vector3 point2,
            TestFlags flags, Scene scene, ref CollisionResult result)
        {
            return CheckBetweenPoints(candidates, point1, point2, flags, scene, ref result, hasCandidates: true);
        }

        public static bool CheckBetweenPoints(Vector3 point1, Vector3 point2, TestFlags flags, Scene scene, ref CollisionResult result)
        {
            return CheckBetweenPoints(null, point1, point2, flags, scene, ref result, hasCandidates: false);
        }

        private static bool CheckBetweenPoints(IReadOnlyList<CollisionCandidate>? candidates, Vector3 point1, Vector3 point2,
            TestFlags flags, Scene scene, ref CollisionResult result, bool hasCandidates)
        {
            _seenData.Clear();
            bool collided = false;
            ushort mask = 0;
            bool includeEntities = !flags.TestFlag(TestFlags.Scan);
            if (flags.TestFlag(TestFlags.Players))
            {
                mask |= (ushort)CollisionFlags.IgnorePlayers;
            }
            if (flags.TestFlag(TestFlags.Beams))
            {
                mask |= (ushort)CollisionFlags.IgnoreBeams;
            }
            EntityCollision? lastEntCol = null;
            Vector3 transPoint1 = point1;
            Vector3 transPoint2 = point2;
            float minDist = Single.MaxValue;
            if (!hasCandidates)
            {
                candidates = GetCandidatesForPoints(point1, point2, 0, includeEntities, scene);
            }
            Debug.Assert(candidates != null);
            for (int i = 0; i < candidates.Count; i++)
            {
                CollisionCandidate candidate = candidates[i];
                CollisionInstance inst = candidate.Collision;
                // sktodo: handle FH collision
                var info = (MphCollisionInfo)inst.Info;
                Debug.Assert(candidate.Entry.DataCount > 0);
                if (candidate.EntityCollision != lastEntCol)
                {
                    if (candidate.EntityCollision != null)
                    {
                        transPoint1 = Matrix.Vec3MultMtx4(point1, candidate.EntityCollision.Inverse1);
                        transPoint2 = Matrix.Vec3MultMtx4(point2, candidate.EntityCollision.Inverse1);
                    }
                    else
                    {
                        transPoint1 = point1 - inst.Translation;
                        transPoint2 = point2 - inst.Translation;
                    }
                }
                for (int j = 0; j < candidate.Entry.DataCount; j++)
                {
                    // todo: counter
                    CollisionData data = info.Data[info.DataIndices[candidate.Entry.DataStartIndex + j]];
                    if (((ushort)data.Flags & mask) != 0 || _seenData.Contains(data))
                    {
                        continue;
                    }
                    if (candidate.EntityCollision == null)
                    {
                        _seenData.Add(data);
                    }
                    Vector4 plane = info.Planes[data.PlaneIndex];
                    float dot1 = Vector3.Dot(transPoint1, plane.Xyz) - plane.W;
                    if (dot1 > 0)
                    {
                        float dot2 = Vector3.Dot(transPoint2, plane.Xyz) - plane.W;
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
                                    transPoint1.X + (transPoint2.X - transPoint1.X) * dist,
                                    transPoint1.Y + (transPoint2.Y - transPoint1.Y) * dist,
                                    transPoint1.Z + (transPoint2.Z - transPoint1.Z) * dist
                                );
                                if (CheckPointOnFace(pos, info, data))
                                {
                                    if (candidate.EntityCollision != null)
                                    {
                                        result.Position = Matrix.Vec3MultMtx4(pos, candidate.EntityCollision.Transform);
                                        Vector3 normal = Matrix.Vec3MultMtx3(plane.Xyz, candidate.EntityCollision.Transform);
                                        float w = Vector3.Dot(result.Position, normal);
                                        result.Plane = new Vector4(normal, w);
                                    }
                                    else
                                    {
                                        result.Position = pos;
                                        result.Plane = plane;
                                    }
                                    minDist = dist;
                                    result.Field0 = 0;
                                    result.Field14 = 0;
                                    result.Flags = data.Flags;
                                    result.Distance = dist;
                                    result.EntityCollision = candidate.EntityCollision;
                                    collided = true;
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

        private static void ClearCandidates()
        {
            while (_activeItems.Count > 0)
            {
                CollisionCandidate item = _activeItems[0];
                _activeItems.Remove(item);
                _inactiveItems.Enqueue(item);
            }
        }

        public static int CheckSphereBetweenPoints(IReadOnlyList<CollisionCandidate> candidates, Vector3 point1, Vector3 point2, float radius,
            int limit, bool includeOffset, TestFlags flags, Scene scene, CollisionResult[] results)
        {
            return CheckSphereBetweenPoints(candidates, point1, point2, radius, limit, includeOffset, flags, scene, results, hasCandidates: true);
        }

        public static int CheckSphereBetweenPoints(Vector3 point1, Vector3 point2, float radius, int limit, bool includeOffset,
            TestFlags flags, Scene scene, CollisionResult[] results)
        {
            return CheckSphereBetweenPoints(null, point1, point2, radius, limit, includeOffset, flags, scene, results, hasCandidates: false);
        }

        // todo: revisit this approach
        private static readonly HashSet<CollisionData> _seenData = new HashSet<CollisionData>(64);

        private static int CheckSphereBetweenPoints(IReadOnlyList<CollisionCandidate>? candidates, Vector3 point1, Vector3 point2, float radius,
            int limit, bool includeOffset, TestFlags flags, Scene scene, CollisionResult[] results, bool hasCandidates)
        {
            _seenData.Clear();
            int count = 0;
            ushort mask = 0;
            bool includeEntities = !flags.TestFlag(TestFlags.Scan);
            if (flags.TestFlag(TestFlags.Players))
            {
                mask |= (ushort)CollisionFlags.IgnorePlayers;
            }
            if (flags.TestFlag(TestFlags.Beams))
            {
                mask |= (ushort)CollisionFlags.IgnoreBeams;
            }
            EntityCollision? lastEntCol = null;
            Vector3 transPoint1 = point1;
            Vector3 transPoint2 = point2;
            if (!hasCandidates)
            {
                candidates = GetCandidatesForLimits(point1, point2, radius, null, Vector3.Zero, includeEntities: false, scene);
            }
            Debug.Assert(candidates != null);
            for (int i = 0; i < candidates.Count; i++)
            {
                CollisionCandidate candidate = candidates[i];
                CollisionInstance inst = candidate.Collision;
                // sktodo: handle FH collision
                var info = (MphCollisionInfo)inst.Info;
                Debug.Assert(candidate.Entry.DataCount > 0);
                if (candidate.EntityCollision != lastEntCol)
                {
                    if (candidate.EntityCollision != null)
                    {
                        transPoint1 = Matrix.Vec3MultMtx4(point1, candidate.EntityCollision.Inverse1);
                        transPoint2 = Matrix.Vec3MultMtx4(point2, candidate.EntityCollision.Inverse1);
                    }
                    else
                    {
                        transPoint1 = point1 - inst.Translation;
                        transPoint2 = point2 - inst.Translation;
                    }
                }
                for (int j = 0; j < candidate.Entry.DataCount; j++)
                {
                    if (count == limit)
                    {
                        break;
                    }
                    CollisionData data = info.Data[info.DataIndices[candidate.Entry.DataStartIndex + j]];
                    if (((ushort)data.Flags & mask) != 0 || _seenData.Contains(data))
                    {
                        continue;
                    }
                    if (candidate.EntityCollision == null)
                    {
                        _seenData.Add(data);
                    }
                    Vector4 plane = info.Planes[data.PlaneIndex];
                    float dot1 = Vector3.Dot(transPoint1, plane.Xyz) - plane.W;
                    if (dot1 <= 0)
                    {
                        // plane is behind the starting point
                        continue;
                    }
                    float dot2 = Vector3.Dot(transPoint2, plane.Xyz) - plane.W;
                    if (dot2 > radius)
                    {
                        // plane is more than radius units ahead of the ending point
                        continue;
                    }
                    float pct = 1;
                    if (MathF.Abs(dot1 - dot2) >= 1 / 4096f)
                    {
                        pct = Math.Clamp(dot1 / (dot1 - dot2), 0, 1);
                    }
                    Vector3 vec = transPoint1 + (transPoint2 - transPoint1) * pct;

                    float GetEdgeDotDifference(int pIndex)
                    {
                        int index = data.PointStartIndex + pIndex;
                        Vector3 dataPoint1 = info.Points[info.PointIndices[index]];
                        // index + 1 may exceed the count, in which case we get the copy of the first index
                        Vector3 dataPoint2 = info.Points[info.PointIndices[index + 1]];
                        Vector3 edgeDir = (dataPoint1 - dataPoint2).Normalized();
                        var cross = Vector3.Cross(edgeDir, plane.Xyz);
                        float crossDot1 = Vector3.Dot(cross, dataPoint2);
                        float crossDot2 = Vector3.Dot(vec, cross);
                        return crossDot2 - crossDot1;
                    }

                    Debug.Assert(data.PointIndexCount > 0);
                    bool fullCollision = true;
                    for (int p1 = 0; p1 < data.PointIndexCount; p1++)
                    {
                        float dotDiff = GetEdgeDotDifference(p1);
                        if (dotDiff < -0.03125f)
                        {
                            fullCollision = false;
                            // bug? - the first edge that we're outside of by the 0.03 margin may only be partially outside,
                            // so after the radius check we return this face as collided without testing any of the other edges,
                            // which we might be way outside of and thus not actually colliding with the face (e.g. High Ground)
                            // --> this may be compensated for by the some collision handling routines, but not all?
                            if (includeOffset && dotDiff >= -radius)
                            {
                                // unimpl-collision: see note below
                                int epIndex = data.PointStartIndex + p1;
                                Vector3 edgePoint1 = info.Points[info.PointIndices[epIndex]];
                                Vector3 edgePoint2 = info.Points[info.PointIndices[epIndex + 1]];
                                CollisionResult result = results[count];
                                result.Field0 = 1;
                                result.EntityCollision = candidate.EntityCollision;
                                result.Flags = data.Flags;
                                result.Field14 = dot2;
                                result.Distance = pct;
                                if (candidate.EntityCollision != null)
                                {
                                    Vector3 normal = Matrix.Vec3MultMtx3(plane.Xyz, candidate.EntityCollision.Transform);
                                    Vector3 wVec = Matrix.Vec3MultMtx4(plane.Xyz * plane.W, candidate.EntityCollision.Transform);
                                    float w = Vector3.Dot(wVec, normal);
                                    result.Plane = new Vector4(normal, w);
                                    result.Position = Matrix.Vec3MultMtx4(vec, candidate.EntityCollision.Transform);
                                    result.EdgePoint1 = Matrix.Vec3MultMtx4(edgePoint1, candidate.EntityCollision.Transform);
                                    result.EdgePoint2 = Matrix.Vec3MultMtx4(edgePoint2, candidate.EntityCollision.Transform);
                                }
                                else
                                {
                                    result.Plane = plane;
                                    result.Position = vec;
                                    result.EdgePoint1 = edgePoint1;
                                    result.EdgePoint2 = edgePoint2;
                                }
                                results[count++] = result;
                            }
                            break;
                        }
                    }
                    if (fullCollision)
                    {
                        // unimpl-collision: if a flag is set, only successuively closer results are added (inserted at the front of the list)
                        CollisionResult result = results[count];
                        result.Field0 = 0;
                        result.EntityCollision = candidate.EntityCollision;
                        result.Flags = data.Flags;
                        result.Field14 = dot2;
                        result.Distance = pct;
                        if (candidate.EntityCollision != null)
                        {
                            Vector3 normal = Matrix.Vec3MultMtx3(plane.Xyz, candidate.EntityCollision.Transform);
                            Vector3 wVec = Matrix.Vec3MultMtx4(plane.Xyz * plane.W, candidate.EntityCollision.Transform);
                            float w = Vector3.Dot(wVec, normal);
                            result.Plane = new Vector4(normal, w);
                            result.Position = Matrix.Vec3MultMtx4(vec, candidate.EntityCollision.Transform);
                        }
                        else
                        {
                            result.Plane = plane;
                            result.Position = vec;
                        }
                        results[count++] = result;
                    }

                }
                if (count == limit)
                {
                    break;
                }
            }
            return count;
        }

        public static bool CheckCylinderBetweenPoints(Vector3 point1, Vector3 point2, Vector3 cylPos,
            float cylHeight, float radii, ref CollisionResult result)
        {
            Vector3 travel = point2 - point1;
            float length = travel.Length;
            travel /= length;
            Vector3 vec1 = cylPos - point1;
            float dot = Vector3.Dot(travel, vec1);
            if (dot < -radii || dot > length + radii)
            {
                return false;
            }
            Vector3 vec2 = vec1 - travel * dot;
            if (vec2.Y > 0 || vec2.Y < -cylHeight)
            {
                return false;
            }
            if (vec2.X * vec2.X + vec2.Z * vec2.Z <= radii * radii)
            {
                result.Field0 = 0;
                result.EntityCollision = null;
                result.Flags = CollisionFlags.None;
                result.Position = cylPos - vec2;
                result.Distance = Math.Clamp(dot / length, 0, 1);
                result.Plane = new Vector4(-travel, 0);
                return true;
            }
            return false;
        }

        public static int CheckInRadius(Vector3 point, float radius, int limit, bool getSimpleNormal,
            TestFlags flags, Scene scene, CollisionResult[] results)
        {
            _seenData.Clear();
            int count = 0;
            ushort mask = 0;
            if (flags.TestFlag(TestFlags.Players))
            {
                mask |= (ushort)CollisionFlags.IgnorePlayers;
            }
            if (flags.TestFlag(TestFlags.Beams))
            {
                mask |= (ushort)CollisionFlags.IgnoreBeams;
            }
            var limitMin = new Vector3(point.X - radius, point.Y - radius, point.Z - radius);
            var limitMax = new Vector3(point.X + radius, point.Y + radius, point.Z + radius);
            IReadOnlyList<CollisionCandidate> candidates
                = GetCandidatesForLimits(null, Vector3.Zero, 0, limitMin, limitMax, includeEntities: false, scene);
            for (int i = 0; i < candidates.Count; i++)
            {
                CollisionCandidate candidate = candidates[i];
                CollisionInstance inst = candidate.Collision;
                // sktodo: handle FH collision
                var info = (MphCollisionInfo)inst.Info;
                Vector3 transPoint = point - inst.Translation;
                Debug.Assert(candidate.Entry.DataCount > 0);
                for (int j = 0; j < candidate.Entry.DataCount; j++)
                {
                    if (count == limit)
                    {
                        break;
                    }
                    // todo: counter
                    CollisionData data = info.Data[info.DataIndices[candidate.Entry.DataStartIndex + j]];
                    if (((ushort)data.Flags & mask) != 0 || _seenData.Contains(data))
                    {
                        continue;
                    }
                    if (candidate.EntityCollision == null)
                    {
                        _seenData.Add(data);
                    }
                    Vector4 plane = info.Planes[data.PlaneIndex];
                    float dot = Vector3.Dot(transPoint, plane.Xyz) - plane.W;
                    float resDot = dot;
                    if (dot <= 0 || dot > radius)
                    {
                        continue;
                    }

                    float GetEdgeDotDifference(int pIndex)
                    {
                        int index = data.PointStartIndex + pIndex;
                        Vector3 point1 = info.Points[info.PointIndices[index]];
                        // index + 1 may exceed the count, in which case we get the copy of the first index
                        Vector3 point2 = info.Points[info.PointIndices[index + 1]];
                        Vector3 edgeDir = (point1 - point2).Normalized();
                        var cross = Vector3.Cross(edgeDir, plane.Xyz);
                        float dot1 = Vector3.Dot(cross, point2);
                        float dot2 = Vector3.Dot(transPoint, cross);
                        return dot2 - dot1;
                    }

                    Debug.Assert(data.PointIndexCount > 0);
                    bool noNegCos = true;
                    bool foundBlocker = false;
                    int p1 = 0;
                    for (; p1 < data.PointIndexCount; p1++)
                    {
                        float dotDiff = GetEdgeDotDifference(p1);
                        if (dotDiff < -0.03125f)
                        {
                            noNegCos = false;
                            break;
                        }
                    }
                    if (noNegCos)
                    {
                        results[count].Field0 = 0;
                        results[count].Plane = plane;
                        foundBlocker = true;
                    }
                    if (!foundBlocker)
                    {
                        Debug.Assert(data.PointIndexCount > 0);
                        // pick up where left off with the p1 index
                        float dotDiff = GetEdgeDotDifference(p1);
                        // the game continues in the next loop based on this condition,
                        // but the condition doesn't change, so the whole loop is skipped
                        if (dotDiff < 0 && dotDiff >= -radius)
                        {
                            for (int p2 = 0; p2 < data.PointIndexCount; p2++)
                            {
                                int index = data.PointStartIndex + p2;
                                Vector3 point1 = info.Points[info.PointIndices[index]];
                                // index + 1 may exceed the count, in which case we get the copy of the first index
                                Vector3 point2 = info.Points[info.PointIndices[index + 1]];
                                Vector3 edge = point2 - point1;
                                float dot1 = Vector3.Dot(edge, edge);
                                float dot2 = Vector3.Dot(edge, transPoint - point1);
                                float div = dot2 / dot1;
                                if (div < 0 || div >= 1)
                                {
                                    Vector3 vec1 = transPoint - point1;
                                    float mag1 = vec1.Length;
                                    if (mag1 > radius)
                                    {
                                        continue;
                                    }
                                    foundBlocker = true;
                                    resDot = mag1;
                                    results[count].Field0 = 2;
                                    if (getSimpleNormal)
                                    {
                                        results[count].Plane = plane;
                                    }
                                    else
                                    {
                                        results[count].Plane = new Vector4(vec1 / mag1, plane.W);
                                    }
                                    break;
                                }
                                Vector3 vec2 = transPoint - (point1 + edge * div);
                                float mag2 = vec2.Length;
                                if (mag2 <= radius)
                                {
                                    foundBlocker = true;
                                    resDot = mag2;
                                    results[count].Field0 = 1;
                                    if (getSimpleNormal)
                                    {
                                        results[count].Plane = plane;
                                    }
                                    else
                                    {
                                        results[count].Plane = new Vector4(vec2 / mag2, plane.W);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    if (foundBlocker)
                    {
                        results[count].Flags = data.Flags;
                        results[count].EntityCollision = null;
                        results[count].Field14 = resDot;
                        count++;
                    }
                }
                if (count == limit)
                {
                    break;
                }
            }
            return count;
        }

        public static IReadOnlyList<CollisionCandidate> GetCandidatesForLimits(Vector3? point1, Vector3 point2, float margin,
            Vector3? limitMin, Vector3 limitMax, bool includeEntities, Scene scene)
        {
            // for some reason, this is used both for querying with points and with limits
            ClearCandidates();
            if (limitMin == null)
            {
                Debug.Assert(point1 != null);
                limitMin = new Vector3(
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Value.X), point2.X) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Value.Y), point2.Y) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Value.Z), point2.Z) - margin
                );
                limitMax = new Vector3(
                    MathF.Max(MathF.Max(Single.MinValue, point1.Value.X), point2.X) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Value.Y), point2.Y) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Value.Z), point2.Z) + margin
                );
            }
            GetRoomCandidatesForLimits(limitMin.Value, limitMax, scene);
            if (includeEntities && point1 != null)
            {
                GetEntityCandidates(limitMin.Value, limitMax, scene);
            }
            return _activeItems;
        }

        private static void GetRoomCandidatesForLimits(Vector3 limitMin, Vector3 limitMax, Scene scene)
        {
            if (scene.Room == null)
            {
                return;
            }
            for (int i = 0; i < scene.Room.RoomCollision.Count; i++)
            {
                // sktodo: handle FH collision
                CollisionInstance inst = scene.Room.RoomCollision[i];
                if (inst.Info.FirstHunt)
                {
                    continue;
                }
                var info = (MphCollisionInfo)inst.Info;
                float size = 4;
                int partsX = info.Header.PartsX;
                int partsY = info.Header.PartsY;
                int partsZ = info.Header.PartsZ;
                Vector3 minPos = info.MinPosition + inst.Translation;
                int minXPart = (int)((limitMin.X - minPos.X) / size);
                int maxXPart = (int)((limitMax.X - minPos.X) / size);
                int minYPart = (int)((limitMin.Y - minPos.Y) / size);
                int maxYPart = (int)((limitMax.Y - minPos.Y) / size);
                int minZPart = (int)((limitMin.Z - minPos.Z) / size);
                int maxZPart = (int)((limitMax.Z - minPos.Z) / size);
                if (maxXPart >= 0 && minXPart <= partsX && maxYPart >= 0 && minYPart <= partsY && maxZPart >= 0 && minZPart <= partsZ)
                {
                    minXPart = Math.Max(minXPart, 0);
                    minYPart = Math.Max(minYPart, 0);
                    minZPart = Math.Max(minZPart, 0);
                    maxXPart = Math.Min(maxXPart, partsX - 1);
                    maxYPart = Math.Min(maxYPart, partsY - 1);
                    maxZPart = Math.Min(maxZPart, partsZ - 1);
                    int xIndex = minXPart;
                    int yIndex = minYPart;
                    int zIndex = minZPart;
                    while (yIndex <= maxYPart)
                    {
                        while (zIndex <= maxZPart)
                        {
                            while (xIndex <= maxXPart)
                            {
                                int entryIndex = yIndex * partsX * partsZ + zIndex * partsX + xIndex;
                                CollisionEntry entry = info.Entries[entryIndex++];
                                if (entry.DataCount > 0)
                                {
                                    CollisionCandidate item = _inactiveItems.Dequeue();
                                    item.Collision = inst;
                                    item.Entry = entry;
                                    item.EntityCollision = null;
                                    _tempItems.Push(item);
                                }
                                xIndex++;
                            }
                            xIndex = minXPart;
                            zIndex++;
                        }
                        xIndex = minXPart;
                        zIndex = minZPart;
                        yIndex++;
                    }
                }
            }
            while (_tempItems.Count > 0)
            {
                _activeItems.Add(_tempItems.Pop());
            }
        }

        public static IReadOnlyList<CollisionCandidate> GetCandidatesForPoints(Vector3 point1, Vector3 point2, float margin,
            bool includeEntities, Scene scene)
        {
            ClearCandidates();
            GetRoomCandidatesForPoints(point1, point2, scene);
            if (includeEntities)
            {
                var limitMin = new Vector3(
                    MathF.Min(MathF.Min(Single.MaxValue, point1.X), point2.X) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Y), point2.Y) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Z), point2.Z) - margin
                );
                var limitMax = new Vector3(
                    MathF.Max(MathF.Max(Single.MinValue, point1.X), point2.X) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Y), point2.Y) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Z), point2.Z) + margin
                );
                GetEntityCandidates(limitMin, limitMax, scene);
            }
            return _activeItems;
        }

        private static void GetEntityCandidates(Vector3 limitMin, Vector3 limitMax, Scene scene)
        {
            for (int i = 0; i < scene.Entities.Count; i++)
            {
                EntityBase entity = scene.Entities[i];
                if (entity.Type != EntityType.Object && entity.Type != EntityType.Platform)
                {
                    continue;
                }
                for (int j = 0; j < 2; j++)
                {
                    EntityCollision? entCol = entity.EntityCollision[j];
                    // sktodo: handle FH collision
                    if (entCol?.Collision == null || !entCol.Collision.Active || entCol.Collision.Info.FirstHunt)
                    {
                        continue;
                    }
                    var entMin = new Vector3(
                        MathF.Min(Single.MaxValue, entCol.CurrentCenter.X) - entCol.MaxDistance,
                        MathF.Min(Single.MaxValue, entCol.CurrentCenter.Y) - entCol.MaxDistance,
                        MathF.Min(Single.MaxValue, entCol.CurrentCenter.Z) - entCol.MaxDistance
                    );
                    var entMax = new Vector3(
                        MathF.Max(Single.MinValue, entCol.CurrentCenter.X) + entCol.MaxDistance,
                        MathF.Max(Single.MinValue, entCol.CurrentCenter.Y) + entCol.MaxDistance,
                        MathF.Max(Single.MinValue, entCol.CurrentCenter.Z) + entCol.MaxDistance
                    );
                    if (entMin.X <= limitMax.X && entMax.X >= limitMin.X && entMin.Y <= limitMax.Y
                        && entMax.Y >= limitMin.Y && entMin.Z <= limitMax.Z && entMax.Z >= limitMin.Z)
                    {
                        CollisionInstance inst = entCol.Collision;
                        var info = (MphCollisionInfo)inst.Info;
                        int entryIndex = 0;
                        int xIndex = 0;
                        int yIndex = 0;
                        int zIndex = 0;
                        while (yIndex < info.Header.PartsY)
                        {
                            while (zIndex < info.Header.PartsZ)
                            {
                                while (xIndex < info.Header.PartsX)
                                {
                                    CollisionEntry entry = info.Entries[entryIndex++];
                                    if (entry.DataCount > 0)
                                    {
                                        CollisionCandidate item = _inactiveItems.Dequeue();
                                        item.Collision = inst;
                                        item.Entry = entry;
                                        item.EntityCollision = entCol;
                                        _tempItems.Push(item);
                                    }
                                    xIndex++;
                                }
                                xIndex = 0;
                                zIndex++;
                            }
                            xIndex = 0;
                            zIndex = 0;
                            yIndex++;
                        }
                    }
                }
            }
            while (_tempItems.Count > 0)
            {
                _activeItems.Add(_tempItems.Pop());
            }
        }

        private static void GetRoomCandidatesForPoints(Vector3 point1, Vector3 point2, Scene scene)
        {
            if (scene.Room == null)
            {
                return;
            }
            for (int i = 0; i < scene.Room.RoomCollision.Count; i++)
            {
                // sktodo: handle FH collision
                CollisionInstance inst = scene.Room.RoomCollision[i];
                if (inst.Info.FirstHunt)
                {
                    continue;
                }
                var info = (MphCollisionInfo)inst.Info;
                float size = 4;
                int partsX = info.Header.PartsX;
                int partsY = info.Header.PartsY;
                int partsZ = info.Header.PartsZ;
                Vector3 minPos = info.MinPosition + inst.Translation;
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
                    return;
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
                    return;
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
                            item.Collision = inst;
                            item.Entry = entry;
                            item.EntityCollision = null;
                            _tempItems.Push(item);
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
            }
            while (_tempItems.Count > 0)
            {
                _activeItems.Add(_tempItems.Pop());
            }
        }

        public static bool CheckSphereOverlapVolume(CollisionVolume other, Vector3 pos, float radius, ref CollisionResult result)
        {
            if (other.Type == VolumeType.Cylinder)
            {
                Vector3 between = pos - other.CylinderPosition;
                float dot = Vector3.Dot(other.CylinderVector, between);
                if (dot >= -radius && dot <= other.CylinderDot + radius)
                {
                    between -= other.CylinderVector * dot;
                    float radii = radius + other.CylinderRadius;
                    if (between.LengthSquared <= radii * radii)
                    {
                        result.Field0 = 2;
                        result.EntityCollision = null;
                        result.Flags = CollisionFlags.None;
                        if (dot < 0)
                        {
                            result.Plane = new Vector4(other.CylinderVector, 0);
                            result.Field14 = -dot;
                        }
                        else if (dot <= other.CylinderDot)
                        {
                            float mag = between.Length;
                            result.Plane = new Vector4(-between / mag, 0);
                            result.Field14 = mag - radius;
                        }
                        else
                        {
                            result.Plane = new Vector4(-other.CylinderVector, 0);
                            result.Field14 = dot - other.CylinderDot;
                        }
                        return true;
                    }
                }
            }
            else if (other.Type == VolumeType.Sphere)
            {
                Vector3 between = other.SpherePosition - pos;
                float radii = other.SphereRadius + radius;
                if (between.LengthSquared <= radii * radii)
                {
                    result.Field0 = 2;
                    result.EntityCollision = null;
                    result.Flags = CollisionFlags.None;
                    float mag = between.Length;
                    result.Plane = new Vector4(between / mag, 0);
                    result.Field14 = mag - radius;
                    return true;
                }
            }
            else if (other.Type == VolumeType.Box)
            {
                Vector3 between = pos - other.BoxPosition;
                float dot1 = Vector3.Dot(other.BoxVector1, between);
                if (dot1 >= -radius && dot1 <= other.BoxDot1 + radius)
                {
                    float dot2 = Vector3.Dot(other.BoxVector2, between);
                    if (dot2 >= -radius && dot2 <= other.BoxDot2 + radius)
                    {
                        float dot3 = Vector3.Dot(other.BoxVector3, between);
                        if (dot3 >= -radius && dot3 <= other.BoxDot3 + radius)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool CheckCylinderOverlapVolume(CollisionVolume other, Vector3 bottom, Vector3 top,
            float radius, ref CollisionResult result)
        {
            if (other.Type == VolumeType.Cylinder)
            {
                return CheckCylindersOverlap(bottom, top, other.CylinderPosition, other.CylinderVector, other.CylinderDot,
                    radius + other.CylinderRadius, ref result);
            }
            if (other.Type == VolumeType.Sphere)
            {
                return CheckCylinderOverlapSphere(bottom, top, other.SpherePosition, radius + other.SphereRadius, ref result);
            }
            return false;
        }

        public static bool CheckCylindersOverlap(Vector3 oneBottom, Vector3 oneTop, Vector3 twoBottom, Vector3 twoVector,
            float twoDot, float radii, ref CollisionResult result)
        {
            float v9 = 0;
            float v10 = 1;
            Vector3 a = oneBottom - twoBottom;
            Vector3 b = oneTop - twoBottom;
            float v11 = Vector3.Dot(a, twoVector);
            float v12 = Vector3.Dot(b, twoVector);
            if (v11 >= 0)
            {
                if (v11 > twoDot)
                {
                    if (v12 > twoDot)
                    {
                        return false;
                    }
                    if (v12 <= v11)
                    {
                        v9 = (v11 - twoDot) / (v11 - v12);
                    }
                    else
                    {
                        v9 = (v11 - twoDot) / (v12 - v11);
                    }
                }
            }
            else
            {
                if (v12 < 0)
                {
                    return false;
                }
                if (v12 <= v11)
                {
                    v9 = -v11 / (v11 - v12);
                }
                else
                {
                    v9 = -v11 / (v12 - v11);
                }
            }
            if (v12 >= 0)
            {
                if (v12 > twoDot)
                {
                    if (v12 <= v11)
                    {
                        v10 = 1 - (v12 - twoDot) / (v11 - v12);
                    }
                    else
                    {
                        v10 = 1 - (v12 - twoDot) / (v12 - v11);
                    }
                }
            }
            else if (v12 <= v11)
            {
                v10 = 1 - (-v12 / (v11 - v12));
            }
            else
            {
                v10 = 1 - (-v12 / (v12 - v11));
            }
            Vector3 c = twoVector * (v12 - v11);
            Vector3 d = oneTop - oneBottom;
            c = d - c;
            Vector3 e = twoBottom + twoVector * v11;
            float v15 = Vector3.Dot(c, c);
            Vector3 f = e - oneBottom;
            float v16 = Vector3.Dot(c, f);
            float v17 = v16 / v15;
            if (v17 >= v9)
            {
                if (v17 > v10)
                {
                    v17 = v10;
                }
            }
            else
            {
                v17 = v9;
            }
            Vector3 g = oneBottom + c * v17;
            Vector3 h = g - e;
            float v19 = Vector3.Dot(h, h);
            if (v19 > radii * radii)
            {
                return false;
            }
            float v20 = MathF.Sqrt(v19);
            float v21 = MathF.Sqrt(v15);
            float v22 = v17 - (radii - v20) / v21;
            if (v22 >= v9)
            {
                if (v22 > v10)
                {
                    v22 = v10;
                }
            }
            else
            {
                v22 = v9;
            }
            result.Field0 = 0;
            result.EntityCollision = null;
            result.Flags = CollisionFlags.None;
            result.Position = oneBottom + d * v22;
            result.Distance = v22;
            if (d.X != 0 || d.Y != 0 || d.Z != 0)
            {
                d = d.Normalized();
            }
            else
            {
                d.X = 1;
            }
            result.Plane.X = -d.X;
            result.Plane.Y = -d.Y;
            result.Plane.Z = -d.Z;
            return true;
        }

        private static bool CheckCylinderOverlapVolumeHelper(CollisionVolume other, Vector3 bottom, Vector3 vector,
            float dot, float radius, ref CollisionResult result)
        {
            if (other.Type == VolumeType.Cylinder)
            {
                CollisionResult discard = default; // the game doesn't pass the result on
                Vector3 top = bottom + vector * dot;
                return CheckCylindersOverlap(bottom, top, other.CylinderPosition, other.CylinderVector,
                    other.CylinderDot, other.CylinderRadius + radius, ref discard);
            }
            if (other.Type == VolumeType.Sphere)
            {
                Vector3 between = other.SpherePosition - bottom;
                float dot1 = Vector3.Dot(between, vector);
                if (dot1 <= dot + other.SphereRadius && dot1 >= -other.SphereRadius)
                {
                    between -= vector * dot1;
                    float radii = other.SphereRadius + radius;
                    float dot2 = Vector3.Dot(between, between);
                    if (dot2 <= radii * radii)
                    {
                        result.Field0 = 2;
                        result.EntityCollision = null;
                        result.Flags = CollisionFlags.None;
                        if (dot < 0)
                        {
                            result.Plane = new Vector4(-other.SpherePosition, 0);
                            result.Field14 = -dot1;
                        }
                        else if (dot1 <= dot)
                        {
                            float mag = between.Length;
                            result.Plane = new Vector4(between / mag, 0);
                            result.Field14 = mag - radius;
                        }
                        else
                        {
                            result.Plane = new Vector4(other.SpherePosition, 0);
                            result.Field14 = dot1 - dot;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool CheckVolumesOverlap(CollisionVolume one, CollisionVolume two, ref CollisionResult result)
        {
            if (two.Type == VolumeType.Box)
            {
                if (one.Type == VolumeType.Sphere)
                {
                    // will return correctly for sphere-box, but won't update result
                    return CheckSphereOverlapVolume(two, one.SpherePosition, one.SphereRadius, ref result);
                }
                if (one.Type == VolumeType.Cylinder)
                {
                    // will always return false for cylinder-box
                    return CheckCylinderOverlapVolumeHelper(two, one.CylinderPosition, one.CylinderVector,
                        one.CylinderDot, one.CylinderRadius, ref result);
                }
                if (one.Type == VolumeType.Box)
                {
                    // will always return false for box-box
                    return false;
                }
            }
            else if (two.Type == VolumeType.Cylinder)
            {
                // will return correctly for sphere-cylinder
                // will return correctly for cylinder-cylinder, but won't update result
                // will always return false for box-cylinder
                return CheckCylinderOverlapVolumeHelper(one, two.CylinderPosition, two.CylinderVector,
                        two.CylinderDot, two.CylinderRadius, ref result);
            }
            else if (two.Type == VolumeType.Sphere)
            {
                // will return correctly for sphere-sphere
                // will return correctly for cylinder-sphere
                // will return correctly for box-sphere, but won't update result
                return CheckSphereOverlapVolume(one, two.SpherePosition, two.SphereRadius, ref result);
            }
            return false;
        }

        public static bool CheckCylinderOverlapSphere(Vector3 cylBot, Vector3 cylTop, Vector3 spherePos,
            float radii, ref CollisionResult result)
        {
            Vector3 a = cylTop - cylBot;
            float v7 = a.Length;
            Vector3 b = spherePos - cylBot;
            if (v7 <= 0)
            {
                if (b.LengthSquared <= radii * radii)
                {
                    result.Field0 = 0;
                    result.EntityCollision = null;
                    result.Flags = CollisionFlags.None;
                    result.Distance = 0;
                    result.Position = cylBot;
                    result.Plane.X = 1;
                    result.Plane.Y = 0;
                    result.Plane.Z = 0;
                    return true;
                }
            }
            else
            {
                a /= v7;
                float v12 = Vector3.Dot(a, b);
                if (v12 >= -radii && v12 <= v7 + radii)
                {
                    Vector3 c = b - (a * v12);
                    if (c.LengthSquared <= radii * radii)
                    {
                        result.Field0 = 0;
                        result.EntityCollision = null;
                        result.Flags = CollisionFlags.None;
                        Vector3 pos = spherePos - c;
                        float v15 = Vector3.Dot(c, c);
                        float v16 = MathF.Sqrt(radii * radii - v15);
                        Vector3 d = a * v16;
                        pos -= d;
                        result.Position = pos;
                        float dist = v12 / (v7 + 2 * radii);
                        if (dist > 1)
                        {
                            dist = 1;
                        }
                        else if (dist < 0)
                        {
                            dist = 0;
                        }
                        result.Distance = dist;
                        Vector3 normal = (pos - spherePos).Normalized();
                        result.Plane.X = normal.X;
                        result.Plane.Y = normal.Y;
                        result.Plane.Z = normal.Z;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool CheckCylinderIntersectPlane(Vector3 cylBot, Vector3 cylTop, Vector4 plane, ref CollisionResult result)
        {
            float v10 = plane.X * (cylTop.X - cylBot.X);
            float v13 = plane.Y * (cylTop.Y - cylBot.Y);
            float v14 = plane.Z * (cylTop.Z - cylBot.Z);
            float sum = v10 + v13 + v14;
            if (sum == 0)
            {
                return false;
            }
            float dist = (plane.W - (plane.X * cylBot.X + plane.Y * cylBot.Y + plane.Z * cylBot.Z)) / sum;
            if (dist < 0 || dist > 1)
            {
                return false;
            }
            result.Position = cylBot + dist * (cylTop - cylBot);
            result.Distance = dist;
            result.EntityCollision = null;
            return true;
        }

        public static bool CheckPortBetweenPoints(Portal portal, Vector3 point1, Vector3 point2, bool otherSide)
        {
            float dotPrev = Vector3.Dot(point1, portal.Plane.Xyz) - portal.Plane.W;
            float dotCur = Vector3.Dot(point2, portal.Plane.Xyz) - portal.Plane.W;
            // todo?: the game returns false after a check with flags bit 1, but I don't think that's ever set
            if (otherSide)
            {
                dotPrev *= -1;
                dotCur *= -1;
            }
            if (dotPrev > 0 && dotCur <= 0)
            {
                float div = dotPrev / (dotPrev - dotCur);
                Vector3 vec = point1 + (point2 - point1) * div;
                Debug.Assert(portal.Points.Count == portal.Planes.Count);
                Debug.Assert(portal.Planes.Count > 0);
                for (int i = 0; i < portal.Planes.Count; i++)
                {
                    Vector4 plane = portal.Planes[i];
                    if (Vector3.Dot(vec, plane.Xyz) - plane.W < Fixed.ToFloat(-4224))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
