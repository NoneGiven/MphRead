using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Utility
{
    public class CollisionDataEditor
    {
        public List<Vector3> Points { get; } = new List<Vector3>();
        public Vector4 Plane { get; set; }
        public ushort LayerMask { get; set; }

        public bool Damaging { get => Check(CollisionFlags.Damaging); set => Update(CollisionFlags.Damaging, value); }
        public bool Reflect { get => Check(CollisionFlags.ReflectBeams); set => Update(CollisionFlags.ReflectBeams, value); }
        public bool Players { get => Check(CollisionFlags.IgnorePlayers); set => Update(CollisionFlags.IgnorePlayers, !value); }
        public bool Beams { get => Check(CollisionFlags.IgnoreBeams); set => Update(CollisionFlags.IgnoreBeams, !value); }
        public bool Scan { get => Check(CollisionFlags.IgnoreScan); set => Update(CollisionFlags.IgnoreScan, !value); }

        public int Slipperiness
        {
            get
            {
                return ((ushort)Flags & 0x18) >> 3;
            }
            set
            {
                if (value < 0 || value > 3)
                {
                    throw new ProgramException($"Invalid slipperiness value {value}.");
                }
                Flags = (CollisionFlags)((ushort)Flags & 0xFFE7 | (value << 3));
            }
        }

        public Terrain Terrain
        {
            get
            {
                return (Terrain)(((ushort)Flags & 0x1E0) >> 5);
            }
            set
            {
                if (!Enum.IsDefined(typeof(Terrain), value))
                {
                    throw new ProgramException($"Invalid terrain type {value}.");
                }
                Flags = (CollisionFlags)((ushort)Flags & 0xFE1F | ((byte)value << 5));
            }
        }

        public CollisionFlags Flags { get; set; }

        private bool Check(CollisionFlags flag)
        {
            return Flags.HasFlag(flag);
        }

        private void Update(CollisionFlags flag, bool value)
        {
            if (value)
            {
                Flags |= flag;
            }
            else
            {
                Flags &= ~flag;
            }
        }
    }

    public static class RepackCollision
    {
        public static byte[] RepackMphRoom(string room)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            CollisionInstance collision = Collision.GetCollision(meta, roomLayerMask: -1);
            List<CollisionDataEditor> editors = GetEditors(collision);
            return RepackMphCollision(editors, collision.Info.Portals);
        }

        public static byte[] RepackFhRoom(string room, RepackFilter filter = RepackFilter.All)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            int roomLayerMask = -1;
            if (filter != RepackFilter.All)
            {
                roomLayerMask = filter == RepackFilter.Multiplayer
                    ? SceneSetup.GetNodeLayer(GameMode.Battle, roomLayer: 0, playerCount: 2)
                    : SceneSetup.GetNodeLayer(GameMode.SinglePlayer, meta.NodeLayer, playerCount: 1);
            }
            CollisionInstance collision = Collision.GetCollision(meta, roomLayerMask);
            List<CollisionDataEditor> editors = GetEditors(collision);
            return RepackFhCollision(editors, collision.Info.Portals);
        }

        public static void TestCollision(string? room = null)
        {
            var allCollision = new List<(CollisionInstance, string)>();
            if (room == null)
            {
                foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
                {
                    if (!meta.Value.FirstHunt && !meta.Value.Hybrid && !allCollision.Any(a => a.Item2 == meta.Value.CollisionPath))
                    {
                        allCollision.Add((Collision.GetCollision(meta.Value, roomLayerMask: -1), meta.Value.CollisionPath));
                    }
                }
                foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.ModelMetadata)
                {
                    if (meta.Value.CollisionPath != null && !meta.Value.FirstHunt)
                    {
                        allCollision.Add((Collision.GetCollision(meta.Value), meta.Value.CollisionPath));
                        if (meta.Value.ExtraCollisionPath != null)
                        {
                            allCollision.Add((Collision.GetCollision(meta.Value, extra: true), meta.Value.ExtraCollisionPath));
                        }
                    }
                }
            }
            else
            {
                RoomMetadata meta = Metadata.RoomMetadata[room];
                allCollision.Add((Collision.GetCollision(meta, roomLayerMask: -1), meta.CollisionPath));
            }
            foreach ((CollisionInstance collision, string path) in allCollision)
            {
                List<CollisionDataEditor> editors = GetEditors(collision);
                byte[] bytes = RepackMphCollision(editors, collision.Info.Portals);
                //byte[] bytes = RepackMphCollisionSimple(info);
                //byte[] file = File.ReadAllBytes(Path.Combine(Paths.FileSystem, path));
                string outPath = Path.Combine(Paths.Export, "_pack", $"out_{Path.GetFileName(path)}");
                File.WriteAllBytes(outPath, bytes);
                //CompareCollision(info, bytes, file);
            }
            Nop();
        }

        private static List<CollisionDataEditor> GetEditors(CollisionInstance collision)
        {
            if (collision.Info is MphCollisionInfo mphInfo)
            {
                return GetEditors(mphInfo);
            }
            return GetEditors((FhCollisionInfo)collision.Info);
        }

        private static int GetPrimaryAxis(Vector3 normal)
        {
            float x = MathF.Abs(normal.X);
            float y = MathF.Abs(normal.Y);
            float z = MathF.Abs(normal.Z);
            if (y > x && y >= z)
            {
                return 1;
            }
            if (z > x && z > y)
            {
                return 2;
            }
            return 0;
        }

        private static List<CollisionDataEditor> GetEditors(MphCollisionInfo info)
        {
            var editors = new List<CollisionDataEditor>();
            foreach (CollisionData data in info.Data)
            {
                Vector4 plane = info.Planes[data.PlaneIndex];
                Vector3 normal = plane.Xyz;
                var editor = new CollisionDataEditor()
                {
                    LayerMask = (ushort)((data.LayerMask & 0xFFFC) | GetPrimaryAxis(normal)),
                    Flags = data.Flags,
                    Plane = new Vector4(normal, plane.W)
                };
                for (int i = 0; i < data.PointIndexCount; i++)
                {
                    editor.Points.Add(info.Points[info.PointIndices[data.PointStartIndex + i]]);
                }
                editors.Add(editor);
            }
            return editors;
        }

        // sktodo: handle Level MP5
        private static List<CollisionDataEditor> GetEditors(FhCollisionInfo info)
        {
            // data index list has the same count as the data list, contains indices of all non-portal data,
            // and has 0xCDCD entries "padding" the end of the list equal to the number of portals (allocation bug)
            Debug.Assert(info.Header.DataCount == info.DataIndices.Count);
            Debug.Assert(info.Header.DataCount == info.Data.Count);
            Debug.Assert(info.Header.DataCount - info.Header.PortalCount == info.DataIndices.Count(d => d != 0xCDCD));
            Debug.Assert(info.Header.PortalCount == info.DataIndices.Count(d => d == 0xCDCD));
            Debug.Assert(info.DataIndices.Count(d => d != 0xCDCD) == info.DataIndices.Distinct().Count(d => d != 0xCDCD));
            var editors = new List<CollisionDataEditor>();
            for (int i = 0; i < info.Header.DataCount - info.Header.PortalCount; i++)
            {
                FhCollisionData data = info.Data[i + (int)info.Header.PortalCount];
                Vector4 plane = info.Planes[data.PlaneIndex];
                Vector3 normal = plane.Xyz;
                int axis = GetPrimaryAxis(normal);
                var editor = new CollisionDataEditor()
                {
                    LayerMask = (ushort)(4 | axis),
                    Plane = new Vector4(normal, plane.W)
                };
                for (int j = 0; j < data.VectorCount; j++)
                {
                    // use Point2Index so vertex order is the same as MPH
                    editor.Points.Add(info.Points[info.Vectors[data.VectorStartIndex + j].Point2Index]);
                }
                editors.Add(editor);
            }
            return editors;
        }

        private static void CompareCollision(MphCollisionInfo info, byte[] bytes, byte[] file)
        {
            MphCollisionInfo pack = GetCollision(bytes);
            Debug.Assert(Enumerable.SequenceEqual(pack.Header.Type, info.Header.Type));
            Debug.Assert(pack.Header.PointCount == info.Header.PointCount);
            Debug.Assert(pack.Header.PointOffset == info.Header.PointOffset);
            Debug.Assert(pack.Header.PlaneCount == info.Header.PlaneCount);
            Debug.Assert(pack.Header.PlaneOffset == info.Header.PlaneOffset);
            Debug.Assert(pack.Header.PointIndexCount == info.Header.PointIndexCount);
            Debug.Assert(pack.Header.PointIndexOffset == info.Header.PointIndexOffset);
            Debug.Assert(pack.Header.DataCount == info.Header.DataCount);
            Debug.Assert(pack.Header.DataOffset == info.Header.DataOffset);
            Debug.Assert(pack.Header.DataIndexCount == info.Header.DataIndexCount);
            Debug.Assert(pack.Header.DataIndexOffset == info.Header.DataIndexOffset);
            Debug.Assert(pack.Header.PartsX == info.Header.PartsX);
            Debug.Assert(pack.Header.PartsY == info.Header.PartsY);
            Debug.Assert(pack.Header.PartsZ == info.Header.PartsZ);
            Debug.Assert(pack.Header.MinPosition.X.Value == info.Header.MinPosition.X.Value);
            Debug.Assert(pack.Header.MinPosition.Y.Value == info.Header.MinPosition.Y.Value);
            Debug.Assert(pack.Header.MinPosition.Z.Value == info.Header.MinPosition.Z.Value);
            Debug.Assert(pack.Header.EntryCount == info.Header.EntryCount);
            Debug.Assert(pack.Header.EntryOffset == info.Header.EntryOffset);
            Debug.Assert(pack.Header.PortalCount == info.Header.PortalCount);
            Debug.Assert(pack.Header.PortalOffset == info.Header.PortalOffset);
            Debug.Assert(pack.Points.Count == info.Points.Count);
            for (int i = 0; i < pack.Points.Count; i++)
            {
                Debug.Assert(pack.Points[i].X == info.Points[i].X);
                Debug.Assert(pack.Points[i].Y == info.Points[i].Y);
                Debug.Assert(pack.Points[i].Z == info.Points[i].Z);
            }
            Debug.Assert(pack.Planes.Count == info.Planes.Count);
            for (int i = 0; i < pack.Planes.Count; i++)
            {
                Debug.Assert(pack.Planes[i].X == info.Planes[i].X);
                Debug.Assert(pack.Planes[i].Y == info.Planes[i].Y);
                Debug.Assert(pack.Planes[i].Z == info.Planes[i].Z);
                Debug.Assert(pack.Planes[i].W == info.Planes[i].W);
            }
            Debug.Assert(pack.PointIndices.Count == info.PointIndices.Count);
            Debug.Assert(Enumerable.SequenceEqual(pack.PointIndices, info.PointIndices));
            Debug.Assert(pack.Data.Count == info.Data.Count);
            for (int i = 0; i < pack.Data.Count; i++)
            {
                CollisionData data = pack.Data[i];
                CollisionData other = info.Data[i];
                Debug.Assert(data.Counter == other.Counter);
                Debug.Assert(data.PlaneIndex == other.PlaneIndex);
                Debug.Assert(data.Flags == other.Flags);
                Debug.Assert(data.LayerMask == other.LayerMask);
                Debug.Assert(data.PaddingA == other.PaddingA);
                Debug.Assert(data.PointIndexCount == other.PointIndexCount);
                Debug.Assert(data.PointStartIndex == other.PointStartIndex);
            }
            Debug.Assert(pack.DataIndices.Count == info.DataIndices.Count);
            Debug.Assert(Enumerable.SequenceEqual(pack.DataIndices, info.DataIndices));
            Debug.Assert(pack.Entries.Count == info.Entries.Count);
            for (int i = 0; i < pack.Entries.Count; i++)
            {
                Debug.Assert(pack.Entries[i].DataCount == info.Entries[i].DataCount);
                Debug.Assert(pack.Entries[i].DataStartIndex == info.Entries[i].DataStartIndex);
            }
            IReadOnlyList<RawCollisionPortal> portals
                = Read.DoOffsets<RawCollisionPortal>(bytes, pack.Header.PortalOffset, pack.Header.PortalCount);
            IReadOnlyList<RawCollisionPortal> otherPortals
                = Read.DoOffsets<RawCollisionPortal>(bytes, info.Header.PortalOffset, info.Header.PortalCount);
            for (int i = 0; i < portals.Count; i++)
            {
                RawCollisionPortal portal = portals[i];
                RawCollisionPortal other = otherPortals[i];
                Debug.Assert(Enumerable.SequenceEqual(portal.Name, other.Name));
                Debug.Assert(Enumerable.SequenceEqual(portal.NodeName1, other.NodeName1));
                Debug.Assert(Enumerable.SequenceEqual(portal.NodeName2, other.NodeName2));
                Debug.Assert(portal.Point1.X.Value == other.Point1.X.Value);
                Debug.Assert(portal.Point1.Y.Value == other.Point1.Y.Value);
                Debug.Assert(portal.Point1.Z.Value == other.Point1.Z.Value);
                Debug.Assert(portal.Point2.X.Value == other.Point2.X.Value);
                Debug.Assert(portal.Point2.Y.Value == other.Point2.Y.Value);
                Debug.Assert(portal.Point2.Z.Value == other.Point2.Z.Value);
                Debug.Assert(portal.Point3.X.Value == other.Point3.X.Value);
                Debug.Assert(portal.Point3.Y.Value == other.Point3.Y.Value);
                Debug.Assert(portal.Point3.Z.Value == other.Point3.Z.Value);
                Debug.Assert(portal.Point4.X.Value == other.Point4.X.Value);
                Debug.Assert(portal.Point4.Y.Value == other.Point4.Y.Value);
                Debug.Assert(portal.Point4.Z.Value == other.Point4.Z.Value);
                Debug.Assert(portal.Plane1.X.Value == other.Plane1.X.Value);
                Debug.Assert(portal.Plane1.Y.Value == other.Plane1.Y.Value);
                Debug.Assert(portal.Plane1.Z.Value == other.Plane1.Z.Value);
                Debug.Assert(portal.Plane1.W.Value == other.Plane1.W.Value);
                Debug.Assert(portal.Plane2.X.Value == other.Plane2.X.Value);
                Debug.Assert(portal.Plane2.Y.Value == other.Plane2.Y.Value);
                Debug.Assert(portal.Plane2.Z.Value == other.Plane2.Z.Value);
                Debug.Assert(portal.Plane2.W.Value == other.Plane2.W.Value);
                Debug.Assert(portal.Plane3.X.Value == other.Plane3.X.Value);
                Debug.Assert(portal.Plane3.Y.Value == other.Plane3.Y.Value);
                Debug.Assert(portal.Plane3.Z.Value == other.Plane3.Z.Value);
                Debug.Assert(portal.Plane3.W.Value == other.Plane3.W.Value);
                Debug.Assert(portal.Plane4.X.Value == other.Plane4.X.Value);
                Debug.Assert(portal.Plane4.Y.Value == other.Plane4.Y.Value);
                Debug.Assert(portal.Plane4.Z.Value == other.Plane4.Z.Value);
                Debug.Assert(portal.Plane4.W.Value == other.Plane4.W.Value);
                Debug.Assert(portal.Plane.X.Value == other.Plane.X.Value);
                Debug.Assert(portal.Plane.Y.Value == other.Plane.Y.Value);
                Debug.Assert(portal.Plane.Z.Value == other.Plane.Z.Value);
                Debug.Assert(portal.Plane.W.Value == other.Plane.W.Value);
                Debug.Assert(portal.Flags == other.Flags);
                Debug.Assert(portal.LayerMask == other.LayerMask);
                Debug.Assert(portal.PointCount == other.PointCount);
                Debug.Assert(portal.UnusedDE == other.UnusedDE);
                Debug.Assert(portal.UnusedDF == other.UnusedDF);
            }
            Debug.Assert(bytes.Length == file.Length);
            Debug.Assert(Enumerable.SequenceEqual(bytes, file));
        }

        private static MphCollisionInfo GetCollision(byte[] bytes)
        {
            CollisionHeader header = Read.ReadStruct<CollisionHeader>(bytes);
            return Collision.ReadMphCollision(header, bytes, roomLayerMask: -1);
        }

        private static byte[] RepackMphCollisionSimple(MphCollisionInfo info)
        {
            uint padInt = 0;
            ushort padShort = 0;
            byte padByte = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            stream.Position = Sizes.CollisionHeader;
            // points
            int pointOffset = (int)stream.Position;
            foreach (Vector3 point in info.Points)
            {
                writer.WriteVector3(point);
            }
            // planes
            int planeOffset = (int)stream.Position;
            foreach (Vector4 plane in info.Planes)
            {
                writer.WriteVector4(plane);
            }
            // point indices
            int pointIdxOffset = (int)stream.Position;
            foreach (ushort index in info.PointIndices)
            {
                writer.Write(index);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // data
            int dataOffset = (int)stream.Position;
            foreach (CollisionData data in info.Data)
            {
                writer.Write(padInt); // Counter
                writer.Write(data.PlaneIndex);
                writer.Write((ushort)data.Flags);
                writer.Write(data.LayerMask);
                writer.Write(padShort);
                writer.Write(data.PointIndexCount);
                writer.Write(data.PointStartIndex);
            }
            // data indices
            int dataIdxOffset = (int)stream.Position;
            foreach (ushort index in info.DataIndices)
            {
                writer.Write(index);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // entries
            int entryOffset = (int)stream.Position;
            foreach (CollisionEntry entry in info.Entries)
            {
                writer.Write(entry.DataCount);
                writer.Write(entry.DataStartIndex);
            }
            // portals
            int portalOffset = (int)stream.Position;
            foreach (CollisionPortal portal in info.Portals)
            {
                Debug.Assert(portal.Points.Count == 4);
                Debug.Assert(portal.Planes.Count == 4);
                writer.WriteString(portal.Name, 40);
                writer.WriteString(portal.NodeName1, 24);
                writer.WriteString(portal.NodeName2, 24);
                foreach (Vector3 point in portal.Points)
                {
                    writer.WriteVector3(point);
                }
                foreach (Vector4 vector in portal.Planes)
                {
                    writer.WriteVector4(vector);
                }
                writer.WriteVector4(portal.Plane);
                writer.Write(padShort); // Flags
                writer.Write(portal.LayerMask);
                writer.Write((ushort)portal.Points.Count);
                writer.Write(portal.Unknown00);
                writer.Write(portal.Unknown01);
            }
            stream.Position = 0;
            // header
            writer.Write("wc01".ToCharArray());
            writer.Write(info.Points.Count);
            writer.Write(pointOffset);
            writer.Write(info.Planes.Count);
            writer.Write(planeOffset);
            writer.Write(info.PointIndices.Count);
            writer.Write(pointIdxOffset);
            writer.Write(info.Data.Count);
            writer.Write(dataOffset);
            writer.Write(info.DataIndices.Count);
            writer.Write(dataIdxOffset);
            writer.Write(info.Header.PartsX);
            writer.Write(info.Header.PartsY);
            writer.Write(info.Header.PartsZ);
            writer.WriteVector3(info.MinPosition);
            writer.Write(info.Entries.Count);
            writer.Write(entryOffset);
            writer.Write(info.Portals.Count);
            writer.Write(portalOffset);
            return stream.ToArray();
        }

        private class CollisionDataPack
        {
            public CollisionDataEditor Editor { get; }
            public ushort PlaneIndex { get; set; }
            public ushort PointIndexCount { get; set; }
            public ushort PointStartIndex { get; set; }

            public CollisionDataPack(CollisionDataEditor editor, int planeIndex, int pointIndexCount, int pointStartIndex)
            {
                Editor = editor;
                PlaneIndex = (ushort)planeIndex;
                PointIndexCount = (ushort)pointIndexCount;
                PointStartIndex = (ushort)pointStartIndex;
            }
        }

        private class CollisionPortalPack
        {
            public CollisionPortal Portal { get; }
            public ushort PlaneIndex { get; set; }
            public ushort PointIndexCount { get; set; }
            public ushort PointStartIndex { get; set; }

            public CollisionPortalPack(CollisionPortal portal, int planeIndex, int pointIndexCount, int pointStartIndex)
            {
                Portal = portal;
                PlaneIndex = (ushort)planeIndex;
                PointIndexCount = (ushort)pointIndexCount;
                PointStartIndex = (ushort)pointStartIndex;
            }
        }

        private static bool TestIntersection(Vector3 point1, Vector3 point2, Vector3 V0, Vector3 V1, Vector3 V2, out float t)
        {
            t = 0;
            float eps = 0.00001f;
            float[] b = new float[3];
            Vector3 between = point2 - point1;
            Vector3 P = point1;
            Vector3 w = between.Normalized();
            /* If ray P + tw hits triangle V[0], V[1], V[2], then the
            function returns true, stores the barycentric coordinates in
            b[], and stores the distance to the intersection in t.
            Otherwise returns false and the other output parameters are
            undefined.*/
            // Edge vectors
            Vector3 e_1 = V1 - V0;
            Vector3 e_2 = V2 - V0;
            // Face normal
            Vector3 n = Vector3.Cross(e_1, e_2).Normalized();
            var q = Vector3.Cross(w, e_2);
            float a = Vector3.Dot(e_1, q);
            // Backfacing or nearly parallel?
            //if (Vector3.Dot(n, w) >= 0 || MathF.Abs(a) <= eps)
            //{
            //    return false;
            //}
            // nearly parallel?
            if (MathF.Abs(a) <= eps)
            {
                return false;
            }
            Vector3 s = (P - V0) / a;
            var r = Vector3.Cross(s, e_1);
            b[0] = Vector3.Dot(s, q);
            b[1] = Vector3.Dot(r, w);
            b[2] = 1.0f - b[0] - b[1];
            // Intersected outside triangle?
            if ((b[0] < 0.0f) || (b[1] < 0.0f) || (b[2] < 0.0f))
            {
                return false;
            }
            t = Vector3.Dot(e_2, r);
            return t >= 0.0f;
        }

        private static bool CheckIntersection(Vector3 point1, Vector3 point2, IReadOnlyList<Vector3> face)
        {
            Vector3 between = point2 - point1;
            Vector3 ro = point1;
            Vector3 rd = between.Normalized();
            for (int i = 0; i < face.Count - 2; i++)
            {
                Vector3 v0 = face[0];
                Vector3 v1 = face[i + 1];
                Vector3 v2 = face[i + 2];
                bool intersect = TestIntersection(point1, point2, v0, v1, v2, out float t);
                if (intersect && t <= between.Length)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ThrowIfInvalid(CollisionDataEditor data)
        {
            // todo: throw if the polygon is concave/malformed?
            if (data.Points.Count < 3)
            {
                throw new ProgramException("Collision face must have at least 3 vertices.");
            }
            if (data.Points.Count > 10)
            {
                throw new ProgramException("Collision face may not have more than 10 vertices.");
            }
        }

        private static List<ushort> GetDataInRegion(Vector3 minBounds, Vector3 maxBounds, IReadOnlyList<CollisionDataEditor> data)
        {
            var indices = new List<ushort>();
            float xStart = minBounds.X;
            float yStart = minBounds.Y;
            float zStart = minBounds.Z;
            float xEnd = maxBounds.X;
            float yEnd = maxBounds.Y;
            float zEnd = maxBounds.Z;
            var p1 = new Vector3(xStart, yStart, zStart);
            var p2 = new Vector3(xEnd, yStart, zStart);
            var p3 = new Vector3(xStart, yStart, zEnd);
            var p4 = new Vector3(xEnd, yStart, zEnd);
            var p5 = new Vector3(xStart, yEnd, zStart);
            var p6 = new Vector3(xEnd, yEnd, zStart);
            var p7 = new Vector3(xStart, yEnd, zEnd);
            var p8 = new Vector3(xEnd, yEnd, zEnd);
            var faces = new List<List<Vector3>>();
            faces.Add(new List<Vector3>() { p1, p5, p6, p2 });
            faces.Add(new List<Vector3>() { p1, p3, p4, p2 });
            faces.Add(new List<Vector3>() { p1, p5, p7, p3 });
            faces.Add(new List<Vector3>() { p3, p7, p8, p4 });
            faces.Add(new List<Vector3>() { p2, p6, p8, p4 });
            faces.Add(new List<Vector3>() { p5, p7, p8, p6 });
            var edges = new List<List<Vector3>>();
            edges.Add(new List<Vector3>() { p1, p5 });
            edges.Add(new List<Vector3>() { p3, p7 });
            edges.Add(new List<Vector3>() { p2, p6 });
            edges.Add(new List<Vector3>() { p4, p8 });
            edges.Add(new List<Vector3>() { p1, p3 });
            edges.Add(new List<Vector3>() { p2, p4 });
            edges.Add(new List<Vector3>() { p5, p7 });
            edges.Add(new List<Vector3>() { p6, p8 });
            edges.Add(new List<Vector3>() { p1, p2 });
            edges.Add(new List<Vector3>() { p5, p6 });
            edges.Add(new List<Vector3>() { p3, p4 });
            edges.Add(new List<Vector3>() { p7, p8 });
            for (int i = 0; i < data.Count; i++)
            {
                CollisionDataEditor item = data[i];
                if (item.Points.Select(p => p.X).Min() > xEnd
                    || item.Points.Select(p => p.X).Max() < xStart
                    || item.Points.Select(p => p.Y).Min() > yEnd
                    || item.Points.Select(p => p.Y).Max() < yStart
                    || item.Points.Select(p => p.Z).Min() > zEnd
                    || item.Points.Select(p => p.Z).Max() < zStart)
                {
                    continue;
                }
                bool intersects = false;
                foreach (Vector3 point in item.Points)
                {
                    if (point.X >= xStart && point.X < xEnd && point.Y >= yStart && point.Y < yEnd
                        && point.Z >= zStart && point.Z < zEnd)
                    {
                        intersects = true;
                        break;
                    }
                }
                if (!intersects)
                {
                    for (int j = 0; j < faces.Count && !intersects; j++)
                    {
                        List<Vector3> face = faces[j];
                        for (int k = 0; k < item.Points.Count - 1 && !intersects; k++)
                        {
                            intersects |= CheckIntersection(item.Points[k], item.Points[k + 1], face);
                        }
                        if (!intersects)
                        {
                            intersects |= CheckIntersection(item.Points[^1], item.Points[0], face);
                        }
                    }
                }
                if (!intersects)
                {
                    foreach (List<Vector3> edge in edges)
                    {
                        intersects |= CheckIntersection(edge[0], edge[1], item.Points);
                        if (intersects)
                        {
                            break;
                        }
                    }
                }
                if (intersects)
                {
                    indices.Add((ushort)i);
                }
            }
            return indices;
        }

        private static byte[] RepackMphCollision(IReadOnlyList<CollisionDataEditor> data, IReadOnlyList<CollisionPortal> portals)
        {
            uint padInt = 0;
            ushort padShort = 0;
            byte padByte = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            var points = new List<Vector3>();
            var planes = new List<Vector4>();
            var pointIdxs = new List<ushort>();
            var dataPack = new List<CollisionDataPack>();
            int partsX = 1;
            int partsY = 1;
            int partsZ = 1;
            float minX = Single.MaxValue;
            float minY = Single.MaxValue;
            float minZ = Single.MaxValue;
            float maxX = Single.MinValue;
            float maxY = Single.MinValue;
            float maxZ = Single.MinValue;
            var dataIdxs = new List<ushort>();
            var entries = new List<(ushort, ushort)>();

            Debug.Assert(data.Count > 0);
            for (int i = 0; i < data.Count; i++)
            {
                CollisionDataEditor item = data[i];
                ThrowIfInvalid(item);
                foreach (Vector3 point in item.Points)
                {
                    if (!points.Any(p => p == point))
                    {
                        points.Add(point);
                        minX = MathF.Min(minX, point.X);
                        minY = MathF.Min(minY, point.Y);
                        minZ = MathF.Min(minZ, point.Z);
                        maxX = MathF.Max(maxX, point.X);
                        maxY = MathF.Max(maxY, point.Y);
                        maxZ = MathF.Max(maxZ, point.Z);
                    }
                }
                //Vector3 normal = Vector3.Cross(item.Points[^2] - item.Points[^3], item.Points[^1] - item.Points[^2]).Normalized();
                //if (Single.IsNaN(normal.X))
                //{
                //    Debugger.Break();
                //    if (item.Points.Count > 3)
                //    {
                //        normal = Vector3.Cross(item.Points[1] - item.Points[0], item.Points[3] - item.Points[0]).Normalized();
                //    }
                //    if (Single.IsNaN(normal.X))
                //    {
                //        normal = Vector3.Zero;
                //    }
                //}
                //float w = normal.X * item.Points[^1].X + normal.Y * item.Points[^1].Y + normal.Z * item.Points[^1].Z;
                //var plane = new Vector4(normal, w);
                Vector4 plane = item.Plane;
                //CollisionPlane orig = info.Planes[info.Data[i].PlaneIndex];
                //plane = new Vector4(orig.Normal.ToFloatVector(), orig.Homogenous.FloatValue);
                int planeIndex = planes.IndexOf(p => p == plane);
                if (planeIndex == -1)
                {
                    planeIndex = planes.Count;
                    planes.Add(plane);
                }
                int idxCount = item.Points.Count;
                int idxStart = pointIdxs.Count;
                foreach (Vector3 point in item.Points)
                {
                    pointIdxs.Add((ushort)points.IndexOf(p => p == point));
                }
                pointIdxs.Add(pointIdxs[idxStart]);
                dataPack.Add(new CollisionDataPack(item, planeIndex, idxCount, idxStart));
            }
            while (minX + partsX * 4 <= maxX)
            {
                partsX++;
            }
            while (minY + partsY * 4 <= maxY)
            {
                partsY++;
            }
            while (minZ + partsZ * 4 <= maxZ)
            {
                partsZ++;
            }

            // sktodo: (re)move all inline testing code
            //Debug.Assert(minX == info.MinPosition.X);
            //Debug.Assert(minY == info.MinPosition.Y);
            //Debug.Assert(minZ == info.MinPosition.Z);
            //Debug.Assert(partsX == info.Header.PartsX);
            //Debug.Assert(partsY == info.Header.PartsY);
            //Debug.Assert(partsZ == info.Header.PartsZ);

            //Debug.Assert(dataPack.Count == info.Data.Count);
            //for (int i = 0; i < dataPack.Count; i++)
            //{
            //    CollisionDataPack pack = dataPack[i];
            //    CollisionData orig = info.Data[i];
            //    Debug.Assert(pack.Editor.LayerMask == orig.LayerMask);
            //    Debug.Assert(pack.Editor.Flags == orig.Flags);
            //    Vector4 plane = planes[pack.PlaneIndex];
            //    CollisionPlane old = info.Planes[orig.PlaneIndex];
            //    int planeX = (int)(plane.X * 4096f);
            //    int planeY = (int)(plane.Y * 4096f);
            //    int planeZ = (int)(plane.Z * 4096f);
            //    int planeW = (int)(plane.W * 4096f);
            //    //Debug.Assert(Math.Abs(planeX - old.Normal.X.Value) <= 1);
            //    //Debug.Assert(Math.Abs(planeY - old.Normal.Y.Value) <= 1);
            //    //Debug.Assert(Math.Abs(planeZ - old.Normal.Z.Value) <= 1);
            //    //Debug.Assert(Math.Abs(planeW - old.Homogenous.Value) <= 1);
            //    Debug.Assert(pack.PointIndexCount == orig.PointIndexCount);
            //    //Debug.Assert(pack.PointStartIndex == orig.PointStartIndex);
            //    for (int j = 0; j < pack.PointIndexCount; j++)
            //    {
            //        Vector3 point = points[pointIdxs[pack.PointStartIndex + j]];
            //        Vector3 prev = info.Points[info.PointIndices[orig.PointStartIndex + j]];
            //        Debug.Assert(point == prev);
            //    }
            //}
            Nop();

            for (int py = 0; py < partsY; py++)
            {
                for (int pz = 0; pz < partsZ; pz++)
                {
                    for (int px = 0; px < partsX; px++)
                    {
                        int index = py * partsX * partsZ + pz * partsX + px;
                        float xStart = minX + px * 4;
                        float xEnd = xStart + 4;
                        float yStart = minY + py * 4;
                        float yEnd = yStart + 4;
                        float zStart = minZ + pz * 4;
                        float zEnd = zStart + 4;
                        var minBounds = new Vector3(xStart, yStart, zStart);
                        var maxBounds = new Vector3(xEnd, yEnd, zEnd);
                        int idxStart = dataIdxs.Count;
                        dataIdxs.AddRange(GetDataInRegion(minBounds, maxBounds, data));
                        int idxCount = dataIdxs.Count - idxStart;
                        entries.Add(((ushort)idxCount, (ushort)idxStart));
                    }
                }
            }
            //Debug.Assert(entries.Count == info.Entries.Count);
            //for (int i = 0; i < entries.Count; i++)
            //{
            //    (ushort Count, ushort Index) entry = entries[i];
            //    CollisionEntry orig = info.Entries[i];
            //    Debug.Assert(entry.Count >= orig.DataCount);
            //    var newIdxs = new List<ushort>();
            //    for (int j = 0; j < entry.Count; j++)
            //    {
            //        newIdxs.Add(dataIdxs[entry.Index + j]);
            //    }
            //    for (int j = 0; j < orig.DataCount; j++)
            //    {
            //        Debug.Assert(newIdxs.Contains(info.DataIndices[orig.DataStartIndex + j]));
            //    }
            //}
            Nop();

            stream.Position = Sizes.CollisionHeader;
            // points
            int pointOffset = (int)stream.Position;
            foreach (Vector3 point in points)
            {
                writer.WriteVector3(point);
            }
            // planes
            int planeOffset = (int)stream.Position;
            foreach (Vector4 plane in planes)
            {
                writer.WriteVector4(plane);
            }
            // point indices
            int pointIdxOffset = (int)stream.Position;
            foreach (ushort index in pointIdxs)
            {
                writer.Write(index);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // data
            int dataOffset = (int)stream.Position;
            foreach (CollisionDataPack pack in dataPack)
            {
                writer.Write(padInt); // Counter
                writer.Write(pack.PlaneIndex);
                writer.Write((ushort)pack.Editor.Flags);
                writer.Write(pack.Editor.LayerMask);
                writer.Write(padShort);
                writer.Write(pack.PointIndexCount);
                writer.Write(pack.PointStartIndex);
            }
            // data indices
            int dataIdxOffset = (int)stream.Position;
            foreach (ushort index in dataIdxs)
            {
                writer.Write(index);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // entries
            int entryOffset = (int)stream.Position;
            foreach ((ushort dataCount, ushort dataStart) in entries)
            {
                writer.Write(dataCount);
                writer.Write(dataStart);
            }
            // portals
            int portalOffset = (int)stream.Position;
            foreach (CollisionPortal portal in portals)
            {
                Debug.Assert(portal.Points.Count == 4);
                Debug.Assert(portal.Planes.Count == 4);
                writer.WriteString(portal.Name, 40);
                writer.WriteString(portal.NodeName1, 24);
                writer.WriteString(portal.NodeName2, 24);
                foreach (Vector3 point in portal.Points)
                {
                    writer.WriteVector3(point);
                }
                foreach (Vector4 vector in portal.Planes)
                {
                    writer.WriteVector4(vector);
                }
                writer.WriteVector4(portal.Plane);
                writer.Write(padShort); // Flags
                writer.Write(portal.LayerMask);
                writer.Write((ushort)portal.Points.Count);
                writer.Write(portal.Unknown00); // UnusedDE
                writer.Write(portal.Unknown01); // UnusedDF
            }
            stream.Position = 0;
            // header
            writer.Write("wc01".ToCharArray());
            writer.Write(points.Count);
            writer.Write(pointOffset);
            writer.Write(planes.Count);
            writer.Write(planeOffset);
            writer.Write(pointIdxs.Count);
            writer.Write(pointIdxOffset);
            writer.Write(data.Count);
            writer.Write(dataOffset);
            writer.Write(dataIdxs.Count);
            writer.Write(dataIdxOffset);
            writer.Write(partsX);
            writer.Write(partsY);
            writer.Write(partsZ);
            writer.WriteFloat(minX);
            writer.WriteFloat(minY);
            writer.WriteFloat(minZ);
            writer.Write(entries.Count);
            writer.Write(entryOffset);
            writer.Write(portals.Count);
            writer.Write(portalOffset);
            return stream.ToArray();
        }

        private static Vector4 GetPlane(Vector3 point1, Vector3 point2, Vector3 normal)
        {
            // get plane perpendicular to normal in which vector between point2 and point1 lies
            // (and whose normal points away from the interior of the portal/plane)
            Vector3 p2to1 = point1 - point2;
            Vector3 p2to1n = p2to1.Normalized();
            Vector3 cross = Vector3.Cross(p2to1n, normal).Normalized();
            float dist = cross.X * point1.X + cross.Y * point1.Y + cross.Z * point1.Z;
            return new Vector4(cross, dist);
        }

        private class TreeNodePack
        {
            public Vector3 MinBounds { get; set; }
            public Vector3 MaxBounds { get; set; }
            public int LeftIndex { get; set; }
            public int RightIndex { get; set; }
        }

        private static byte[] RepackFhCollision(IReadOnlyList<CollisionDataEditor> data, IReadOnlyList<CollisionPortal> portals)
        {
            byte padByte = 0;
            ushort padShort = 0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            float minX = Single.MaxValue;
            float minY = Single.MaxValue;
            float minZ = Single.MaxValue;
            float maxX = Single.MinValue;
            float maxY = Single.MinValue;
            float maxZ = Single.MinValue;
            var portalPacks = new List<CollisionPortalPack>();
            var dataPacks = new List<CollisionDataPack>();
            var points = new List<Vector3>();
            var vectors = new List<(int, int, int)>();
            var planes = new List<Vector4>();

            void AddItems(IReadOnlyList<Vector3> verts, Vector3 normal)
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 point1 = verts[i == 0 ? verts.Count - 1 : i - 1];
                    Vector3 point2 = verts[i];
                    Vector4 plane = GetPlane(point1, point2, normal);
                    int point1Index = points.IndexOf(p => p == point1);
                    int point2Index = points.IndexOf(p => p == point2);
                    int planeIndex = planes.IndexOf(p => p == plane);
                    if (point1Index == -1)
                    {
                        point1Index = points.Count;
                        points.Add(point1);
                    }
                    if (point2Index == -1)
                    {
                        point2Index = points.Count;
                        points.Add(point2);
                    }
                    if (planeIndex == -1)
                    {
                        planeIndex = planes.Count;
                        planes.Add(plane);
                    }
                    vectors.Add((point1Index, point2Index, planeIndex));
                    minX = MathF.Min(minX, point2.X);
                    minY = MathF.Min(minY, point2.Y);
                    minZ = MathF.Min(minZ, point2.Z);
                    maxX = MathF.Max(maxX, point2.X);
                    maxY = MathF.Max(maxY, point2.Y);
                    maxZ = MathF.Max(maxZ, point2.Z);
                }
            }

            foreach (CollisionPortal portal in portals)
            {
                int planeIndex = planes.IndexOf(p => p == portal.Plane);
                if (planeIndex == -1)
                {
                    planeIndex = planes.Count;
                    planes.Add(portal.Plane);
                }
                int vectorIndex = vectors.Count;
                AddItems(portal.Points, portal.Plane.Xyz);
                portalPacks.Add(new CollisionPortalPack(portal, planeIndex, portal.Points.Count, vectorIndex));
            }
            foreach (CollisionDataEditor item in data)
            {
                int planeIndex = planes.IndexOf(p => p == item.Plane);
                if (planeIndex == -1)
                {
                    planeIndex = planes.Count;
                    planes.Add(item.Plane);
                }
                int vectorIndex = vectors.Count;
                AddItems(item.Points, item.Plane.Xyz);
                dataPacks.Add(new CollisionDataPack(item, planeIndex, item.Points.Count, vectorIndex));
            }

            // start with box covering entire collision, then split in two for the biggest dimension
            // repeat until the boxes have side lengths of 4-ish
            // for each of those boxes, do intersection tests and create the entry
            // note: first tree node entry is always a dummy/unreferenced item with the following values
            var treeNodes = new List<TreeNodePack>();
            float min = Fixed.ToFloat(0x50000000);
            float max = Fixed.ToFloat(unchecked((int)0xB0000000));
            treeNodes.Add(new TreeNodePack()
            {
                MinBounds = new Vector3(min, min, min),
                MaxBounds = new Vector3(max, max, max),
                LeftIndex = 0xCDCD,
                RightIndex = 0xCDCD
            });

            void MakeNodes(TreeNodePack parent)
            {
                float sizeX = parent.MaxBounds.X - parent.MinBounds.X;
                float sizeY = parent.MaxBounds.Y - parent.MinBounds.Y;
                float sizeZ = parent.MaxBounds.Z - parent.MinBounds.Z;
                if (sizeX < 8 && sizeY < 8 && sizeZ < 8)
                {
                    parent.LeftIndex = parent.RightIndex = 0x8000; // todo: entry index
                    return;
                }
                Vector3 newMax;
                Vector3 newMin;
                if (sizeX >= sizeY && sizeX >= sizeZ)
                {
                    newMax = new Vector3(parent.MaxBounds.X - sizeX / 2f, parent.MaxBounds.Y, parent.MaxBounds.Z);
                    newMin = new Vector3(parent.MinBounds.X + sizeX / 2f, parent.MinBounds.Y, parent.MinBounds.Z);
                }
                else if (sizeY > sizeX && sizeY >= sizeZ)
                {
                    newMax = new Vector3(parent.MaxBounds.X, parent.MaxBounds.Y - sizeY / 2f, parent.MaxBounds.Z);
                    newMin = new Vector3(parent.MinBounds.X, parent.MinBounds.Y + sizeY / 2f, parent.MinBounds.Z);
                }
                else
                {
                    newMax = new Vector3(parent.MaxBounds.X, parent.MaxBounds.Y, parent.MaxBounds.Z - sizeZ / 2f);
                    newMin = new Vector3(parent.MinBounds.X, parent.MinBounds.Y, parent.MinBounds.Z + sizeZ / 2f);
                }
                parent.LeftIndex = treeNodes.Count;
                var leftNode = new TreeNodePack()
                {
                    MinBounds = parent.MinBounds,
                    MaxBounds = newMax
                };
                treeNodes.Add(leftNode);
                parent.RightIndex = treeNodes.Count;
                var rightNode = new TreeNodePack()
                {
                    MinBounds = newMin,
                    MaxBounds = parent.MaxBounds
                };
                treeNodes.Add(rightNode);
                MakeNodes(leftNode);
                MakeNodes(rightNode);
            }

            var startNode = new TreeNodePack()
            {
                MinBounds = new Vector3(minX, minY, minZ),
                MaxBounds = new Vector3(maxX, maxY, maxZ)
            };
            treeNodes.Add(startNode);
            MakeNodes(startNode);

            var dataIdxs = new List<ushort>();
            var entries = new List<TreeNodePack>(); // this class's properties are convenient for entries too
            foreach (TreeNodePack node in treeNodes.Where(t => t.RightIndex == 0x8000))
            {
                node.LeftIndex = entries.Count;
                int idxStart = dataIdxs.Count;
                dataIdxs.AddRange(GetDataInRegion(node.MinBounds, node.MaxBounds, data));
                int idxCount = dataIdxs.Count - idxStart;
                var entry = new TreeNodePack();
                entry.MinBounds = node.MinBounds;
                entry.MaxBounds = node.MaxBounds;
                entry.LeftIndex = idxCount; // entries containing no data will be included
                entry.RightIndex = idxStart;
                entries.Add(entry);
            }
            for (int i = 0; i < dataIdxs.Count; i++)
            {
                dataIdxs[i] = (ushort)(dataIdxs[i] + portals.Count);
            }

            stream.Position = Sizes.FhCollisionHeader;
            // points
            int pointOffset = (int)stream.Position;
            foreach (Vector3 point in points)
            {
                writer.WriteVector3(point);
            }
            // planes
            int planeOffset = (int)stream.Position;
            foreach (Vector4 plane in planes)
            {
                writer.WriteVector4(plane);
            }
            // vectors
            int vectorOffset = (int)stream.Position;
            foreach ((int point1Index, int point2Index, int planeIndex) in vectors)
            {
                writer.Write((ushort)point1Index);
                writer.Write((ushort)point2Index);
                writer.Write((ushort)planeIndex);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // data
            int dataOffset = (int)stream.Position;
            foreach (CollisionPortalPack portal in portalPacks)
            {
                writer.Write(portal.PlaneIndex);
                writer.Write(portal.PointIndexCount);
                writer.Write(portal.PointStartIndex);
            }
            foreach (CollisionDataPack item in dataPacks)
            {
                writer.Write(item.PlaneIndex);
                writer.Write(item.PointIndexCount);
                writer.Write(item.PointStartIndex);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // data indices
            int dataIndexOffset = (int)stream.Position;
            foreach (ushort index in dataIdxs)
            {
                writer.Write(index);
            }
            while (stream.Position % 4 != 0)
            {
                writer.Write(padByte);
            }
            // entries
            int entryOffset = (int)stream.Position;
            foreach (TreeNodePack entry in entries)
            {
                writer.WriteVector3(entry.MinBounds);
                writer.WriteVector3(entry.MaxBounds);
                writer.Write((ushort)entry.LeftIndex);
                writer.Write((ushort)entry.RightIndex);
            }
            // tree node indices
            int treeNodeIndexOffset = (int)stream.Position;
            int treeNodeIndexCount = 1;
            writer.Write((ushort)1);
            writer.Write(padShort);
            // tree nodes
            int treeNodeOffset = (int)stream.Position;
            foreach (TreeNodePack treeNode in treeNodes)
            {
                writer.WriteVector3(treeNode.MinBounds);
                writer.WriteVector3(treeNode.MaxBounds);
                writer.Write((ushort)treeNode.LeftIndex);
                writer.Write((ushort)treeNode.RightIndex);
            }
            // portals
            int portalOffset = (int)stream.Position;
            foreach (CollisionPortalPack portal in portalPacks)
            {
                writer.WriteString(portal.Portal.Name, 40);
                writer.WriteString(portal.Portal.NodeName1, 16);
                writer.WriteString(portal.Portal.NodeName2, 16);
                writer.WriteVector4(portal.Portal.Plane);
                writer.Write(portal.PointIndexCount);
                writer.Write(portal.PointStartIndex);
                writer.Write(portal.Portal.Unknown00); // Field5C
                writer.Write(portal.Portal.Unknown01); // Field5D
                writer.Write(padShort); // Padding5E
            }
            // header
            stream.Position = 0;
            writer.Write(points.Count);
            writer.Write(pointOffset);
            writer.Write(planes.Count);
            writer.Write(planeOffset);
            writer.Write(vectors.Count);
            writer.Write(vectorOffset);
            writer.Write((ushort)(data.Count + portals.Count));
            writer.Write(padShort); // DataStartIndex
            writer.Write(dataOffset);
            writer.Write(dataIdxs.Count);
            writer.Write(dataIndexOffset);
            writer.Write(entries.Count);
            writer.Write(entryOffset);
            writer.Write(treeNodeIndexCount);
            writer.Write(treeNodeIndexOffset);
            writer.Write(treeNodes.Count);
            writer.Write(treeNodeOffset);
            writer.Write(portals.Count);
            writer.Write(portalOffset);
            return stream.ToArray();
        }

        private static void Nop()
        {
        }
    }
}
