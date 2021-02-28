using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace MphRead.Formats.Collision
{
    public static class Collision
    {
        // sktodo: cache the raw structs as with models -- don't cache rooms
        public static CollisionInfo ReadCollision(string path, bool firstHunt = false, int roomLayerMask = -1)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path)));
            CollisionHeader header = Read.ReadStruct<CollisionHeader>(bytes);
            if (header.Type.MarshalString() != "wc01")
            {
                return ReadFhCollision(path, bytes);
            }
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.PointOffset, header.PointCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            IReadOnlyList<ushort> shorts = Read.DoOffsets<ushort>(bytes, header.VectorIndexOffset, header.VectorIndexCount);
            IReadOnlyList<CollisionData> data = Read.DoOffsets<CollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<ushort> indices = Read.DoOffsets<ushort>(bytes, header.DataIndexOffset, header.DataIndexCount);
            IReadOnlyList<CollisionEntry> entries = Read.DoOffsets<CollisionEntry>(bytes, header.EntryOffset, header.EntryCount);
            var portals = new List<CollisionPortal>();
            foreach (RawCollisionPortal portal in Read.DoOffsets<RawCollisionPortal>(bytes, header.PortalOffset, header.PortalCount))
            {
                portals.Add(new CollisionPortal(portal));
            }
            var enabledIndices = new Dictionary<uint, HashSet<ushort>>();
            foreach (CollisionEntry entry in entries.Where(e => e.DataCount > 0))
            {
                // todo?: use the layer mask to actually filter the returned items instead of building enabledIndices
                Debug.Assert(entry.DataCount < 512);
                var enabled = new HashSet<ushort>();
                for (int i = 0; i < entry.DataCount; i++)
                {
                    ushort index = indices[entry.DataStartIndex + i];
                    ushort layerMask = data[index].LayerMask;
                    if ((layerMask & 4) != 0 || roomLayerMask == -1 || (layerMask & roomLayerMask) != 0)
                    {
                        enabled.Add(index);
                    }
                }
                Debug.Assert(!enabledIndices.ContainsKey(entry.DataStartIndex));
                enabledIndices.Add(entry.DataStartIndex, enabled);
            }
            string name = Path.GetFileNameWithoutExtension(path).Replace("_collision", "").Replace("_Collision", "");
            return new CollisionInfo(name, header, points, planes, shorts, data, indices, entries, portals, enabledIndices);
        }

        private static CollisionInfo ReadFhCollision(string path, ReadOnlySpan<byte> bytes)
        {
            // nxtodo: read and return the rest of the data
            FhCollisionHeader header = Read.ReadStruct<FhCollisionHeader>(bytes);
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.VectorOffset, header.VectorCount);
            var portals = new List<CollisionPortal>();
            foreach (FhCollisionPortal portal in Read.DoOffsets<FhCollisionPortal>(bytes, header.PortalOffset, header.PortalCount))
            {
                portals.Add(new CollisionPortal(portal));
            }
            string name = Path.GetFileNameWithoutExtension(path).Replace("_collision", "").Replace("_Collision", "");
            return new CollisionInfo(name, default, points, new List<CollisionPlane>(), new List<ushort>(), new List<CollisionData>(),
                new List<ushort>(), new List<CollisionEntry>(), portals, new Dictionary<uint, HashSet<ushort>>());
        }
    }

    // size: 84
    public readonly struct CollisionHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // wc01
        public readonly uint PointCount;
        public readonly uint PointOffset;
        public readonly uint PlaneCount;
        public readonly uint PlaneOffset;
        public readonly uint VectorIndexCount;
        public readonly uint VectorIndexOffset;
        public readonly uint DataCount;
        public readonly uint DataOffset;
        public readonly uint DataIndexCount;
        public readonly uint DataIndexOffset;
        public readonly uint Field2C;
        public readonly uint Field30;
        public readonly uint Field34;
        public readonly Vector3i Xyz;
        public readonly uint EntryCount;
        public readonly uint EntryOffset;
        public readonly uint PortalCount;
        public readonly uint PortalOffset;
    }

    // 16
    public readonly struct CollisionPlane
    {
        public readonly Vector3Fx Normal;
        public readonly Fixed Homogenous;
    }

    // size: 16
    public readonly struct CollisionData
    {
        public readonly uint Field0;
        public readonly ushort PlaneIndex;
        public readonly ushort Field9; // bits 5-8 = terrain type
        public readonly ushort LayerMask;
        public readonly ushort FieldA;
        public readonly ushort PointIndexCount;
        public readonly ushort PointStartIndex;
    }

    // size: 4
    public readonly struct CollisionEntry
    {
        public readonly ushort DataCount;
        public readonly ushort DataStartIndex;
    }

    // size: 224
    public readonly struct RawCollisionPortal
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Name;
        public readonly uint Field20;
        public readonly uint Field24;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly char[] NodeName1; // side 0 room node
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly char[] NodeName2; // side 1 room node
        public readonly Vector3Fx Vector1;
        public readonly Vector3Fx Vector2;
        public readonly Vector3Fx Vector3;
        public readonly Vector3Fx Vector4;
        public readonly Vector3Fx Vector5;
        public readonly uint Field94;
        public readonly Vector3Fx Vector6;
        public readonly uint FieldA4;
        public readonly Vector3Fx Vector7;
        public readonly uint FieldB4;
        public readonly Vector3Fx Vector8;
        public readonly uint FieldC4;
        public readonly Vector3Fx Normal;
        public readonly uint Plane;
        public readonly ushort Flags;
        public readonly ushort LayerMask;
        public readonly ushort VectorCount;
        public readonly ushort FieldDE;
    }

    public class CollisionPortal
    {
        public string Name { get; }
        public string NodeName1 { get; }
        public string NodeName2 { get; }
        public ushort LayerMask { get; }
        public bool IsForceField { get; }
        public Vector3 Point1 { get; }
        public Vector3 Point2 { get; }
        public Vector3 Point3 { get; }
        public Vector3 Point4 { get; }
        public Vector3 Position { get; }

        public CollisionPortal(RawCollisionPortal raw)
        {
            Debug.Assert(raw.VectorCount == 4);
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = raw.LayerMask;
            IsForceField = Name.StartsWith("pmag");
            Point1 = raw.Vector1.ToFloatVector();
            Point2 = raw.Vector2.ToFloatVector();
            Point3 = raw.Vector3.ToFloatVector();
            Point4 = raw.Vector4.ToFloatVector();
            Vector3 position = Vector3.Zero;
            position.X = (Point1.X + Point2.X + Point3.X + Point4.X) * 0.25f;
            position.Y = (Point1.Y + Point2.Y + Point3.Y + Point4.Y) * 0.25f;
            position.Z = (Point1.Z + Point2.Z + Point3.Z + Point4.Z) * 0.25f;
            Position = position;
        }

        // nxtodo: temporary
        public CollisionPortal(FhCollisionPortal raw)
        {
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = 4;
            IsForceField = Name.StartsWith("pmag");
            Point1 = Vector3.Zero;
            Point2 = Vector3.Zero;
            Point3 = Vector3.Zero;
            Point4 = Vector3.Zero;
            Position = Vector3.Zero;
        }
    }

    public class CollisionInfo
    {
        public bool Active { get; set; } = true;
        public string Name { get; }
        public CollisionHeader Header { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public IReadOnlyList<CollisionPlane> Planes { get; }
        public IReadOnlyList<ushort> PointIndices { get; }
        public IReadOnlyList<CollisionData> Data { get; }
        public IReadOnlyList<ushort> DataIndices { get; }
        public IReadOnlyList<CollisionEntry> Entries { get; }
        public IReadOnlyList<CollisionPortal> Portals { get; }
        // todo: update classes based on usage
        public IReadOnlyDictionary<uint, HashSet<ushort>> EnabledDataIndices { get; }

        public CollisionInfo(string name, CollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<ushort> ptIdxs, IReadOnlyList<CollisionData> data, IReadOnlyList<ushort> dataIdxs, IReadOnlyList<CollisionEntry> entries, IReadOnlyList<CollisionPortal> portals, IReadOnlyDictionary<uint, HashSet<ushort>> enabledIndices)
        {
            Name = name;
            Header = header;
            Points = points.Select(v => v.ToFloatVector()).ToList();
            Planes = planes;
            PointIndices = ptIdxs;
            Data = data;
            DataIndices = dataIdxs;
            Entries = entries;
            Portals = portals;
            EnabledDataIndices = enabledIndices;
        }
    }

    // size: 96
    public readonly struct FhCollisionPortal
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Name;
        public readonly uint Field20;
        public readonly uint Field24;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName1; // side 0 room node
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName2; // side 1 room node
        public readonly uint Field48;
        public readonly uint Field4C;
        public readonly uint Field50;
        public readonly uint Field54;
        public readonly uint Field58;
        public readonly byte Field5C;
        public readonly byte Field5D;
        public readonly ushort Field5E;
    }

    // size: 72
    public readonly struct FhCollisionHeader
    {
        public readonly uint VectorCount;
        public readonly uint VectorOffset;
        public readonly uint Count2; // size: 16
        public readonly uint Offset2;
        public readonly uint Count3; // size: 6
        public readonly uint Offset3;
        public readonly uint Count4; // size: 6
        public readonly uint Offset4;
        public readonly uint ShortCount;
        public readonly uint ShortOffset;
        public readonly uint Count6; // size: 28
        public readonly uint Offset6;
        public readonly uint IntCount;
        public readonly uint IntOffset;
        public readonly uint Count8; // size: 28
        public readonly uint Offset8;
        public readonly uint PortalCount;
        public readonly uint PortalOffset;
    }
}
