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
        private static readonly Dictionary<string, CollisionInfo> _cache = new Dictionary<string, CollisionInfo>();
        private static readonly Dictionary<string, CollisionInfo> _fhCache = new Dictionary<string, CollisionInfo>();

        public static CollisionInstance GetCollision(ModelMetadata meta, bool extra = false)
        {
            string? path = extra ? meta.ExtraCollisionPath : meta.CollisionPath;
            Debug.Assert(path != null);
            string name = meta.Name;
            if (name == "AlimbicCapsule" && extra)
            {
                name = "AlmbCapsuleShld";
            }
            return GetCollision(path, name, meta.FirstHunt, roomLayerMask: -1);
        }

        public static CollisionInstance GetCollision(RoomMetadata meta, int roomLayerMask = 0)
        {
            if (roomLayerMask == 0 && meta.NodeLayer > 0)
            {
                roomLayerMask = ((1 << meta.NodeLayer) & 0xFF) << 6;
            }
            return GetCollision(meta.CollisionPath, meta.Name, meta.FirstHunt || meta.Hybrid, roomLayerMask);
        }

        private static CollisionInstance GetCollision(string path, string name, bool firstHunt, int roomLayerMask)
        {
            Dictionary<string, CollisionInfo> cache = firstHunt ? _fhCache : _cache;
            if (roomLayerMask == -1 && cache.TryGetValue(path, out CollisionInfo? info))
            {
                return new CollisionInstance(name, info);
            }
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(firstHunt ? Paths.FhFileSystem : Paths.FileSystem, path)));
            CollisionHeader header = Read.ReadStruct<CollisionHeader>(bytes);
            if (header.Type.MarshalString() == "wc01")
            {
                info = ReadMphCollision(header, bytes, roomLayerMask);
            }
            else
            {
                info = ReadFhCollision(bytes);
            }
            if (roomLayerMask == -1)
            {
                cache.Add(path, info);
            }
            return new CollisionInstance(name, info);
        }

        private static MphCollisionInfo ReadMphCollision(CollisionHeader header, ReadOnlySpan<byte> bytes, int roomLayerMask)
        {
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.PointOffset, header.PointCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            IReadOnlyList<ushort> shorts = Read.DoOffsets<ushort>(bytes, header.VectorIndexOffset, header.VectorIndexCount);
            IReadOnlyList<CollisionData> data = Read.DoOffsets<CollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<ushort> indices = Read.DoOffsets<ushort>(bytes, header.DataIndexOffset, header.DataIndexCount);
            IReadOnlyList<CollisionEntry> entries = Read.DoOffsets<CollisionEntry>(bytes, header.EntryOffset, header.EntryCount);
            var portals = new List<CollisionPortal>();
            foreach (RawCollisionPortal portal in Read.DoOffsets<RawCollisionPortal>(bytes, header.PortalOffset, header.PortalCount))
            {
                if ((portal.LayerMask & 4) != 0 || roomLayerMask == -1 || (portal.LayerMask & roomLayerMask) != 0)
                {
                    portals.Add(new CollisionPortal(portal));
                }
            }
            var indexMap = new Dictionary<ushort, ushort>();
            var finalData = new List<CollisionData>();
            var finalIndices = new List<ushort>();
            var finalEntries = new List<CollisionEntry>();
            foreach (CollisionEntry entry in entries.Where(e => e.DataCount > 0))
            {
                ushort newCount = 0;
                ushort newStartIndex = (ushort)finalIndices.Count;
                for (int i = 0; i < entry.DataCount; i++)
                {
                    ushort oldIndex = indices[entry.DataStartIndex + i];
                    if (indexMap.TryGetValue(oldIndex, out ushort newIndex))
                    {
                        finalIndices.Add(newIndex);
                        newCount++;
                    }
                    else
                    {
                        CollisionData item = data[oldIndex];
                        if ((item.LayerMask & 4) != 0 || roomLayerMask == -1 || (item.LayerMask & roomLayerMask) != 0)
                        {
                            newIndex = (ushort)finalData.Count;
                            finalIndices.Add(newIndex);
                            finalData.Add(item);
                            newCount++;
                            indexMap.Add(oldIndex, newIndex);
                        }
                    }
                }
                finalEntries.Add(new CollisionEntry(newCount, newStartIndex));
            }
            data = finalData;
            indices = finalIndices;
            entries = finalEntries;
            return new MphCollisionInfo(header, points, planes, shorts, data, indices, entries, portals);
        }

        private static FhCollisionInfo ReadFhCollision(ReadOnlySpan<byte> bytes)
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
                portals.Add(new CollisionPortal(portal, vectors, points));
            }
            return new FhCollisionInfo(header, points, planes, data, vectors, dataIndices, portals);
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
        public readonly CollisionFlags Flags;
        public readonly ushort LayerMask;
        public readonly ushort FieldA;
        public readonly ushort PointIndexCount;
        public readonly ushort PointStartIndex;

        // bits 3-4
        public int Slipperiness => ((ushort)Flags & 0x18) >> 3;

        // bits 5-8
        public Terrain Terrain => (Terrain)(((ushort)Flags & 0x1E0) >> 5);

        public bool IgnorePlayers => Flags.HasFlag(CollisionFlags.IgnorePlayers);

        public bool IgnoreBeams => Flags.HasFlag(CollisionFlags.IgnoreBeams);
    }

    // size: 4
    public readonly struct CollisionEntry
    {
        public readonly ushort DataCount;
        public readonly ushort DataStartIndex;

        public CollisionEntry(ushort dataCount, ushort dataStartIndex)
        {
            DataCount = dataCount;
            DataStartIndex = dataStartIndex;
        }
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
        public IReadOnlyList<Vector3> Points { get; }
        public Vector3 Position { get; }

        public CollisionPortal(RawCollisionPortal raw)
        {
            Debug.Assert(raw.VectorCount == 4);
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = raw.LayerMask;
            IsForceField = Name.StartsWith("pmag");
            var points = new List<Vector3>();
            points.Add(raw.Vector1.ToFloatVector());
            points.Add(raw.Vector2.ToFloatVector());
            points.Add(raw.Vector3.ToFloatVector());
            points.Add(raw.Vector4.ToFloatVector());
            Points = points;
            Position = new Vector3(
                points.Sum(p => p.X) / points.Count,
                points.Sum(p => p.Y) / points.Count,
                points.Sum(p => p.Z) / points.Count
            );
        }

        public CollisionPortal(FhCollisionPortal raw, IReadOnlyList<FhCollisionVector> rawVectors, IReadOnlyList<Vector3Fx> rawPoints)
        {
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = 4; // always on
            var points = new List<Vector3>();
            for (int i = 0; i < raw.VectorCount; i++)
            {
                FhCollisionVector vector = rawVectors[raw.VectorStartIndex + i];
                Vector3Fx point = rawPoints[vector.Point1Index];
                points.Add(point.ToFloatVector());
            }
            Points = points;
            Position = new Vector3(
                points.Sum(p => p.X) / points.Count,
                points.Sum(p => p.Y) / points.Count,
                points.Sum(p => p.Z) / points.Count
            );
        }
    }

    [Flags]
    public enum CollisionFlags : ushort
    {
        None = 0x0,
        Damaging = 0x1,
        Bit01 = 0x2,
        Bit02 = 0x4,
        // bits 3-4: slipperiness
        // bits 5-8: terrain type
        ReflectBeams = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        IgnorePlayers = 0x2000,
        IgnoreBeams = 0x4000,
        IgnoreScan = 0x8000
    }

    public class CollisionInstance
    {
        public string Name { get; }
        public bool Active { get; set; } = true;
        public CollisionInfo Info { get; }

        public CollisionInstance(string name, CollisionInfo info)
        {
            Name = name;
            Info = info;
        }
    }

    public abstract class CollisionInfo
    {
        public bool FirstHunt { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public IReadOnlyList<CollisionPlane> Planes { get; }
        public IReadOnlyList<CollisionPortal> Portals { get; }

        public CollisionInfo(IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<CollisionPortal> portals, bool firstHunt)
        {
            Points = points.Select(v => v.ToFloatVector()).ToList();
            Planes = planes;
            Portals = portals;
            FirstHunt = firstHunt;
        }

        public abstract void GetDrawInfo(List<Vector3> points, EntityType entityType, Scene scene);
    }

    public class MphCollisionInfo : CollisionInfo
    {
        public CollisionHeader Header { get; }
        public IReadOnlyList<ushort> PointIndices { get; }
        public IReadOnlyList<CollisionData> Data { get; }
        public IReadOnlyList<ushort> DataIndices { get; }
        public IReadOnlyList<CollisionEntry> Entries { get; }

        public MphCollisionInfo(CollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<ushort> ptIdxs, IReadOnlyList<CollisionData> data, IReadOnlyList<ushort> dataIdxs, IReadOnlyList<CollisionEntry> entries,
            IReadOnlyList<CollisionPortal> portals)
            : base(points, planes, portals, firstHunt: false)
        {
            Header = header;
            PointIndices = ptIdxs;
            Data = data;
            DataIndices = dataIdxs;
            Entries = entries;
        }

        private static readonly IReadOnlyList<Vector4> _colors = new List<Vector4>()
        {
            /*  0 */ new Vector4(0.69f, 0.69f, 0.69f, 1f), // metal (gray)
            /*  1 */ new Vector4(1f, 0.612f, 0.153f, 1f), // orange holo (orange)
            /*  2 */ new Vector4(0f, 1f, 0f, 1f), // green holo (green)
            /*  3 */ new Vector4(0f, 0f, 0.858f, 1f), // blue holo (blue)
            /*  4 */ new Vector4(0.141f, 1f, 1f, 1f), // ice (light blue)
            /*  5 */ new Vector4(1f, 1f, 1f, 1f), // snow (white)
            /*  6 */ new Vector4(0.964f, 1f, 0.058f, 1f), // sand (yellow)
            /*  7 */ new Vector4(0.505f, 0.364f, 0.211f, 1f), // rock (brown)
            /*  8 */ new Vector4(0.984f, 0.701f, 0.576f, 1f), // lava (salmon)
            /*  9 */ new Vector4(0.988f, 0.463f, 0.824f, 1f), // acid (pink)
            /* 10 */ new Vector4(0.615f, 0f, 0.909f, 1f), // Gorea (purple)
            /* 11 */ new Vector4(0.85f, 0.85f, 0.85f, 1f) // unused (dark gray)
        };

        public override void GetDrawInfo(List<Vector3> points, EntityType entityType, Scene scene)
        {
            // todo: visualize extra things like slipperiness, reflection, damage
            int polygonId = scene.GetNextPolygonId();
            for (int i = 0; i < Data.Count; i++)
            {
                // todo?: what is the purpose of CollisionEntry? why are there so many and why do they reference the same CollisionData?
                CollisionData data = Data[i];
                if (scene.ColTerDisplay != Terrain.All && scene.ColTerDisplay != data.Terrain)
                {
                    continue;
                }
                if ((scene.ColTypeDisplay == CollisionType.Player && data.IgnorePlayers)
                    || (scene.ColTypeDisplay == CollisionType.Beam && data.IgnoreBeams)
                    || (scene.ColTypeDisplay == CollisionType.Both && (data.IgnorePlayers || data.IgnoreBeams)))
                {
                    continue;
                }
                Vector4 color;
                if (scene.ColDisplayColor == CollisionColor.Entity)
                {
                    if (entityType == EntityType.Platform)
                    {
                        // teal
                        color = new Vector4(0.109f, 0.768f, 0.850f, 1f);
                    }
                    else if (entityType == EntityType.Object)
                    {
                        // magenta
                        color = new Vector4(0.952f, 0.105f, 0.635f, 1f);
                    }
                    else
                    {
                        // orange (room)
                        color = new Vector4(0.952f, 0.694f, 0.105f, 1f);
                    }
                }
                else if (scene.ColDisplayColor == CollisionColor.Terrain)
                {
                    color = _colors[(int)data.Terrain];
                }
                else if (scene.ColDisplayColor == CollisionColor.Type)
                {
                    if (data.IgnoreBeams)
                    {
                        // yellow
                        color = new Vector4(0.956f, 0.933f, 0.203f, 1f);
                    }
                    else if (data.IgnorePlayers)
                    {
                        // green
                        color = new Vector4(0.250f, 0.807f, 0.250f, 1f);
                    }
                    else
                    {
                        // purple (both)
                        color = new Vector4(0.807f, 0.250f, 0.776f, 1f);
                    }
                }
                else
                {
                    color = new Vector4(1, 0, 0, 1);
                }
                color.W = scene.ColDisplayAlpha;
                Debug.Assert(data.PointIndexCount >= 3 && data.PointIndexCount <= 10);
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(data.PointIndexCount);
                for (int j = 0; j < data.PointIndexCount; j++)
                {
                    ushort pointIndex = PointIndices[data.PointStartIndex + j];
                    verts[j] = points[pointIndex];
                }
                scene.AddRenderItem(CullingMode.Back, polygonId, color, RenderItemType.Ngon, verts, data.PointIndexCount);
            }
        }
    }

    // size: 96
    public readonly struct FhCollisionPortal
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public readonly char[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName1; // side 0 room node
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public readonly char[] NodeName2; // side 1 room node
        public readonly CollisionPlane Plane;
        public readonly ushort VectorCount;
        public readonly ushort VectorStartIndex;
        public readonly byte Field5C;
        public readonly byte Field5D;
        public readonly ushort Padding5E;
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

        public FhCollisionInfo(FhCollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<FhCollisionData> data, IReadOnlyList<FhCollisionVector> vectors, IReadOnlyList<ushort> dataIndices,
            IReadOnlyList<CollisionPortal> portals) : base(points, planes, portals, firstHunt: true)
        {
            Header = header;
            Data = data;
            Vectors = vectors;
            DataIndices = dataIndices;
        }

        public override void GetDrawInfo(List<Vector3> points, EntityType entityType, Scene scene)
        {
            var color = new Vector4(Vector3.UnitX, 0.5f);
            int polygonId = scene.GetNextPolygonId();
            for (int i = 0; i < Header.DataCount; i++)
            {
                ushort dataIndex = DataIndices[Header.DataStartIndex + i];
                if (dataIndex > Data.Count)
                {
                    // this happens in FH_E3
                    continue;
                }
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
