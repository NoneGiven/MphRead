using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Utility
{
    public static class RepackCollision
    {
        public static void TestCollision()
        {
            var allCollision = new List<(CollisionInstance, string)>();
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (!meta.Value.FirstHunt && !meta.Value.Hybrid)
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
            foreach ((CollisionInstance collision, string path) in allCollision)
            {
                var info = (MphCollisionInfo)collision.Info;
                byte[] bytes = RepackMphCollisionSimple(info);
                byte[] file = File.ReadAllBytes(Path.Combine(Paths.FileSystem, path));
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
                    Debug.Assert(pack.Planes[i].Normal.X.Value == info.Planes[i].Normal.X.Value);
                    Debug.Assert(pack.Planes[i].Normal.Y.Value == info.Planes[i].Normal.Y.Value);
                    Debug.Assert(pack.Planes[i].Normal.Z.Value == info.Planes[i].Normal.Z.Value);
                    Debug.Assert(pack.Planes[i].Homogenous.Value == info.Planes[i].Homogenous.Value);
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
                    Debug.Assert(portal.Vector1.X.Value == other.Vector1.X.Value);
                    Debug.Assert(portal.Vector1.Y.Value == other.Vector1.Y.Value);
                    Debug.Assert(portal.Vector1.Z.Value == other.Vector1.Z.Value);
                    Debug.Assert(portal.Vector1.W.Value == other.Vector1.W.Value);
                    Debug.Assert(portal.Vector2.X.Value == other.Vector2.X.Value);
                    Debug.Assert(portal.Vector2.Y.Value == other.Vector2.Y.Value);
                    Debug.Assert(portal.Vector2.Z.Value == other.Vector2.Z.Value);
                    Debug.Assert(portal.Vector2.W.Value == other.Vector2.W.Value);
                    Debug.Assert(portal.Vector3.X.Value == other.Vector3.X.Value);
                    Debug.Assert(portal.Vector3.Y.Value == other.Vector3.Y.Value);
                    Debug.Assert(portal.Vector3.Z.Value == other.Vector3.Z.Value);
                    Debug.Assert(portal.Vector3.W.Value == other.Vector3.W.Value);
                    Debug.Assert(portal.Vector4.X.Value == other.Vector4.X.Value);
                    Debug.Assert(portal.Vector4.Y.Value == other.Vector4.Y.Value);
                    Debug.Assert(portal.Vector4.Z.Value == other.Vector4.Z.Value);
                    Debug.Assert(portal.Vector4.W.Value == other.Vector4.W.Value);
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
            Nop();
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
            foreach (CollisionPlane plane in info.Planes)
            {
                writer.WriteVector3(plane.Normal.ToFloatVector());
                writer.WriteFloat(plane.Homogenous.FloatValue);
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
                Debug.Assert(portal.Vectors.Count == 4);
                writer.WriteString(portal.Name, 40);
                writer.WriteString(portal.NodeName1, 24);
                writer.WriteString(portal.NodeName2, 24);
                foreach (Vector3 point in portal.Points)
                {
                    writer.WriteVector3(point);
                }
                foreach (Vector4 vector in portal.Vectors)
                {
                    writer.WriteVector4(vector);
                }
                writer.WriteVector4(portal.Plane);
                writer.Write(padShort); // Flags
                writer.Write(portal.LayerMask);
                writer.Write((ushort)portal.Points.Count);
                writer.Write(portal.UnusedDE);
                writer.Write(portal.UnusedDF);
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

        private static void Nop()
        {
        }
    }
}
