using System;
using System.Buffers;
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
            return new MphCollisionInfo(name, header, points, planes, shorts, data, indices, entries, portals, enabledIndices);
        }

        private static FhCollisionInfo ReadFhCollision(string path, ReadOnlySpan<byte> bytes)
        {
            FhCollisionHeader header = Read.ReadStruct<FhCollisionHeader>(bytes);
            IReadOnlyList<FhCollisionData> data = Read.DoOffsets<FhCollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<FhCollisionVector> vectors = Read.DoOffsets<FhCollisionVector>(bytes, header.VectorOffset, header.VectorCount);
            IReadOnlyList<ushort> dataIndices = Read.DoOffsets<ushort>(bytes, header.DataIndexOffset, header.DataIndexCount);
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.PointOffset, header.PointCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            var portals = new List<CollisionPortal>();
            foreach (FhCollisionPortal portal in Read.DoOffsets<FhCollisionPortal>(bytes, header.PortalOffset, header.PortalCount))
            {
                portals.Add(new CollisionPortal(portal));
            }
            string name = Path.GetFileNameWithoutExtension(path).Replace("_collision", "").Replace("_Collision", "");
            return new FhCollisionInfo(name, header, points, planes, data, vectors, dataIndices, portals);
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

    // size: 16
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

        // sktodo: set up FH collision portals
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

    public abstract class CollisionInfo
    {
        public bool Active { get; set; } = true;
        public bool FirstHunt { get; }
        public string Name { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public IReadOnlyList<CollisionPlane> Planes { get; }
        public IReadOnlyList<CollisionPortal> Portals { get; }

        public CollisionInfo(string name, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<CollisionPortal> portals, bool firstHunt)
        {
            Name = name;
            Points = points.Select(v => v.ToFloatVector()).ToList();
            Planes = planes;
            Portals = portals;
            FirstHunt = firstHunt;
        }

        public abstract void GetDrawInfo(List<Vector3> points, Scene scene);
    }

    public class MphCollisionInfo : CollisionInfo
    {
        public CollisionHeader Header { get; }
        public IReadOnlyList<ushort> PointIndices { get; }
        public IReadOnlyList<CollisionData> Data { get; }
        public IReadOnlyList<ushort> DataIndices { get; }
        public IReadOnlyList<CollisionEntry> Entries { get; }
        // todo: update classes based on usage
        public IReadOnlyDictionary<uint, HashSet<ushort>> EnabledDataIndices { get; }

        private readonly HashSet<ushort> _dataIds = new HashSet<ushort>();

        public MphCollisionInfo(string name, CollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<ushort> ptIdxs, IReadOnlyList<CollisionData> data, IReadOnlyList<ushort> dataIdxs, IReadOnlyList<CollisionEntry> entries,
            IReadOnlyList<CollisionPortal> portals, IReadOnlyDictionary<uint, HashSet<ushort>> enabledIndices)
            : base(name, points, planes, portals, firstHunt: false)
        {
            Header = header;
            PointIndices = ptIdxs;
            Data = data;
            DataIndices = dataIdxs;
            Entries = entries;
            EnabledDataIndices = enabledIndices;
        }

        public override void GetDrawInfo(List<Vector3> points, Scene scene)
        {
            _dataIds.Clear();
            // sktodo: toggles to differentiate beam collision, player collision, show terrain types, etc. (colors)
            var color = new Vector4(Vector3.UnitX, 0.5f);
            int polygonId = scene.GetNextPolygonId();
            for (int j = 0; j < Entries.Count; j++)
            {
                CollisionEntry entry = Entries[j];
                for (int k = 0; k < entry.DataCount; k++)
                {
                    ushort dataIndex = DataIndices[entry.DataStartIndex + k];
                    // ctodo: revisit the dataIds hack; why are we getting multiple entries referencing the same data?
                    if (!_dataIds.Contains(dataIndex) && EnabledDataIndices[entry.DataStartIndex].Contains(dataIndex))
                    {
                        _dataIds.Add(dataIndex);
                        CollisionData data = Data[dataIndex];
                        Debug.Assert(data.PointIndexCount >= 3 && data.PointIndexCount <= 10);
                        Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(data.PointIndexCount);
                        for (int l = 0; l < data.PointIndexCount; l++)
                        {
                            ushort pointIndex = PointIndices[data.PointStartIndex + l];
                            verts[l] = points[pointIndex];
                        }
                        scene.AddRenderItem(CullingMode.Back, polygonId, color, RenderItemType.Ngon, verts, data.PointIndexCount);
                    }
                }
            }
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
        public readonly uint PointCount;
        public readonly uint PointOffset;
        public readonly uint PlaneCount;
        public readonly uint PlaneOffset;
        public readonly uint VectorCount;
        public readonly uint VectorOffset;
        public readonly ushort DataCount;
        public readonly ushort DataStartIndex;
        public readonly uint DataOffset;
        public readonly uint DataIndexCount;
        public readonly uint DataIndexOffset;
        public readonly uint Count6; // size: 28
        public readonly uint Offset6; // link-related
        public readonly uint Count7; // size: 4
        public readonly uint Offset7; // link-related
        public readonly uint Count8; // size: 28
        public readonly uint Offset8; // link-related
        public readonly uint PortalCount;
        public readonly uint PortalOffset;
    }

    // size: 6
    public readonly struct FhCollisionData
    {
        public readonly ushort PlaneIndex;
        public readonly ushort VectorCount;
        public readonly ushort VectorStartIndex;
    }

    // size: 6
    public readonly struct FhCollisionVector
    {
        public readonly ushort Point1Index;
        public readonly ushort Point2Index;
        public readonly ushort PlaneIndex;
    }

    public class FhCollisionInfo : CollisionInfo
    {
        public FhCollisionHeader Header { get; }
        public IReadOnlyList<FhCollisionData> Data { get; }
        public IReadOnlyList<FhCollisionVector> Vectors { get; }
        public IReadOnlyList<ushort> DataIndices { get; }

        public FhCollisionInfo(string name, FhCollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<FhCollisionData> data, IReadOnlyList<FhCollisionVector> vectors, IReadOnlyList<ushort> dataIndices,
            IReadOnlyList<CollisionPortal> portals) : base(name, points, planes, portals, firstHunt: true)
        {
            Header = header;
            Data = data;
            Vectors = vectors;
            DataIndices = dataIndices;
        }

        public override void GetDrawInfo(List<Vector3> points, Scene scene)
        {
            var color = new Vector4(Vector3.UnitX, 0.5f);
            int polygonId = scene.GetNextPolygonId();
            for (int i = 0; i < Header.DataCount; i++)
            {
                ushort dataIndex = DataIndices[Header.DataStartIndex + i];
                FhCollisionData data = Data[dataIndex];
                Debug.Assert(data.VectorCount >= 3 && data.VectorCount <= 6);
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(data.VectorCount);
                for (int j = 0; j < data.VectorCount; j++)
                {
                    FhCollisionVector vector = Vectors[data.VectorStartIndex + j];
                    verts[j] = points[vector.Point1Index];
                }
                scene.AddRenderItem(CullingMode.Back, polygonId, color, RenderItemType.Ngon, verts, data.VectorCount);
            }
        }
    }
}
