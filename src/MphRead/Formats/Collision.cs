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

        public static MphCollisionInfo ReadMphCollision(CollisionHeader header, ReadOnlySpan<byte> bytes, int roomLayerMask)
        {
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.PointOffset, header.PointCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            IReadOnlyList<ushort> pointIdxs = Read.DoOffsets<ushort>(bytes, header.PointIndexOffset, header.PointIndexCount);
            IReadOnlyList<CollisionData> data = Read.DoOffsets<CollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<ushort> dataIdxs = Read.DoOffsets<ushort>(bytes, header.DataIndexOffset, header.DataIndexCount);
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
            if (roomLayerMask == -1)
            {
                // preserve order
                finalData.AddRange(data);
                finalIndices.AddRange(dataIdxs);
                finalEntries.AddRange(entries);
            }
            else
            {
                foreach (CollisionEntry entry in entries)
                {
                    if (entry.DataCount > 0)
                    {
                        ushort newCount = 0;
                        ushort newStartIndex = (ushort)finalIndices.Count;
                        for (int i = 0; i < entry.DataCount; i++)
                        {
                            ushort oldIndex = dataIdxs[entry.DataStartIndex + i];
                            if (indexMap.TryGetValue(oldIndex, out ushort newIndex))
                            {
                                finalIndices.Add(newIndex);
                                newCount++;
                            }
                            else
                            {
                                CollisionData item = data[oldIndex];
                                if ((item.LayerMask & 4) != 0 || (item.LayerMask & roomLayerMask) != 0)
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
                    else
                    {
                        finalEntries.Add(entry);
                    }
                }
            }
            return new MphCollisionInfo(header, points, planes, pointIdxs, finalData, finalIndices, finalEntries, portals);
        }

        private static FhCollisionInfo ReadFhCollision(ReadOnlySpan<byte> bytes)
        {
            FhCollisionHeader header = Read.ReadStruct<FhCollisionHeader>(bytes);
            IReadOnlyList<FhCollisionData> data = Read.DoOffsets<FhCollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<FhCollisionVector> vectors = Read.DoOffsets<FhCollisionVector>(bytes, header.VectorOffset, header.VectorCount);
            IReadOnlyList<ushort> dataIndices = Read.DoOffsets<ushort>(bytes, header.DataIndexOffset, header.DataIndexCount);
            IReadOnlyList<Vector3Fx> points = Read.DoOffsets<Vector3Fx>(bytes, header.PointOffset, header.PointCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            IReadOnlyList<FhCollisionEntry> entryIndices = Read.DoOffsets<FhCollisionEntry>(bytes, header.EntryOffset, header.EntryCount);
            IReadOnlyList<int> treeNodeIndices = Read.DoOffsets<int>(bytes, header.TreeNodeIndexOffset, header.TreeNodeIndexCount);
            IReadOnlyList<FhCollisionTreeNode> treeNodes = Read.DoOffsets<FhCollisionTreeNode>(bytes, header.TreeNodeOffset, header.TreeNodeCount);
            var portals = new List<CollisionPortal>();
            foreach (FhCollisionPortal portal in Read.DoOffsets<FhCollisionPortal>(bytes, header.PortalOffset, header.PortalCount))
            {
                portals.Add(new CollisionPortal(portal, vectors, points));
            }
            return new FhCollisionInfo(header, points, planes, data, vectors, dataIndices, portals, entryIndices, treeNodeIndices, treeNodes);
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
        public readonly uint PointIndexCount;
        public readonly uint PointIndexOffset;
        public readonly uint DataCount;
        public readonly uint DataOffset;
        public readonly uint DataIndexCount;
        public readonly uint DataIndexOffset;
        public readonly int PartsX;
        public readonly int PartsY;
        public readonly int PartsZ;
        public readonly Vector3Fx MinPosition;
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
        public readonly int Counter; // only set at runtime
        public readonly ushort PlaneIndex;
        public readonly CollisionFlags Flags;
        public readonly ushort LayerMask;
        public readonly ushort PaddingA;
        public readonly ushort PointIndexCount;
        public readonly ushort PointStartIndex;

        // bits 3-4
        public int Slipperiness => ((ushort)Flags & 0x18) >> 3;

        // bits 5-8
        public Terrain Terrain => (Terrain)(((ushort)Flags & 0x1E0) >> 5);

        public bool IgnorePlayers => Flags.HasFlag(CollisionFlags.IgnorePlayers);

        public bool IgnoreBeams => Flags.HasFlag(CollisionFlags.IgnoreBeams);

        // bits 0-1
        public int Axis => LayerMask & 3;
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
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public readonly char[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly char[] NodeName1; // side 0 room node
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public readonly char[] NodeName2; // side 1 room node
        public readonly Vector3Fx Point1;
        public readonly Vector3Fx Point2;
        public readonly Vector3Fx Point3;
        public readonly Vector3Fx Point4;
        public readonly Vector4Fx Vector1;
        public readonly Vector4Fx Vector2;
        public readonly Vector4Fx Vector3;
        public readonly Vector4Fx Vector4;
        public readonly Vector4Fx Plane;
        public readonly ushort Flags;
        public readonly ushort LayerMask;
        public readonly ushort PointCount;
        public readonly byte UnusedDE;
        public readonly byte UnusedDF;
    }

    public class CollisionPortal
    {
        public string Name { get; }
        public string NodeName1 { get; }
        public string NodeName2 { get; }
        public ushort LayerMask { get; }
        public bool IsForceField { get; }
        public IReadOnlyList<Vector3> Points { get; }
        public IReadOnlyList<Vector4> Vectors { get; }
        public Vector4 Plane { get; }
        public Vector3 Position { get; }
        public ushort Flags { get; }
        public byte UnusedDE { get; }
        public byte UnusedDF { get; }

        public CollisionPortal(RawCollisionPortal raw)
        {
            Debug.Assert(raw.PointCount == 4);
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = raw.LayerMask;
            IsForceField = Name.StartsWith("pmag");
            var points = new List<Vector3>();
            points.Add(raw.Point1.ToFloatVector());
            points.Add(raw.Point2.ToFloatVector());
            points.Add(raw.Point3.ToFloatVector());
            points.Add(raw.Point4.ToFloatVector());
            Points = points;
            Position = new Vector3(
                points.Sum(p => p.X) / points.Count,
                points.Sum(p => p.Y) / points.Count,
                points.Sum(p => p.Z) / points.Count
            );
            var vectors = new List<Vector4>();
            vectors.Add(raw.Vector1.ToFloatVector());
            vectors.Add(raw.Vector2.ToFloatVector());
            vectors.Add(raw.Vector3.ToFloatVector());
            vectors.Add(raw.Vector4.ToFloatVector());
            Vectors = vectors;
            Plane = raw.Plane.ToFloatVector();
            Flags = raw.Flags;
            UnusedDE = raw.UnusedDE;
            UnusedDF = raw.UnusedDF;
        }

        public CollisionPortal(FhCollisionPortal raw, IReadOnlyList<FhCollisionVector> rawVectors, IReadOnlyList<Vector3Fx> rawPoints)
        {
            Name = raw.Name.MarshalString();
            NodeName1 = raw.NodeName1.MarshalString();
            NodeName2 = raw.NodeName2.MarshalString();
            LayerMask = 4; // always on
            var points = new List<Vector3>();
            for (int i = 0; i < raw.PointCount; i++)
            {
                FhCollisionVector vector = rawVectors[raw.PointStartIndex + i];
                Vector3Fx point = rawPoints[vector.Point1Index];
                points.Add(point.ToFloatVector());
            }
            Points = points;
            Position = new Vector3(
                points.Sum(p => p.X) / points.Count,
                points.Sum(p => p.Y) / points.Count,
                points.Sum(p => p.Z) / points.Count
            );
            Vectors = new List<Vector4>(); // todo: can probably derive these from FhCollisionVector
            Plane = new Vector4(raw.Plane.Normal.ToFloatVector(), raw.Plane.Homogenous.FloatValue);
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
        public Vector3 MinPosition { get; }

        public MphCollisionInfo(CollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<ushort> ptIdxs, IReadOnlyList<CollisionData> data, IReadOnlyList<ushort> dataIdxs,
            IReadOnlyList<CollisionEntry> entries, IReadOnlyList<CollisionPortal> portals)
            : base(points, planes, portals, firstHunt: false)
        {
            Header = header;
            PointIndices = ptIdxs;
            Data = data;
            DataIndices = dataIdxs;
            Entries = entries;
            MinPosition = header.MinPosition.ToFloatVector();
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
            //Entities.EntityBase? target = scene.Entities.FirstOrDefault(e => e.Type == EntityType.Model);
            //if (target != null)
            //{
            //    GetPartition(target.Position, points, entityType, scene);
            //    return;
            //}
            // todo: visualize extra things like slipperiness, reflection, damage
            int polygonId = scene.GetNextPolygonId();
            for (int i = 0; i < Data.Count; i++)
            {
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

        public Vector3i PartIndexFromEntry(int index)
        {
            int x = Header.PartsX;
            int xz = x * Header.PartsZ;
            int yInc = index / xz;
            int zInc = (index - yInc * xz) / x;
            int xInc = index - yInc * xz - zInc * x;
            return new Vector3i(xInc, yInc, zInc);
        }

        public int EntryIndexFromPoint(Vector3 point)
        {
            if (point.X < MinPosition.X || point.Y < MinPosition.Y || point.Z < MinPosition.Z)
            {
                return -1;
            }
            int xInc = (int)((point.X - MinPosition.X) / 4f);
            int yInc = (int)((point.Y - MinPosition.Y) / 4f);
            int zInc = (int)((point.Z - MinPosition.Z) / 4f);
            if (xInc >= Header.PartsX || yInc >= Header.PartsY || zInc >= Header.PartsZ)
            {
                return -1;
            }
            return yInc * Header.PartsX * Header.PartsZ + zInc * Header.PartsX + xInc;
        }

        public void GetPartition(Vector3 point, List<Vector3> points, EntityType entityType, Scene scene)
        {
            int entryIndex = EntryIndexFromPoint(point);
            int polygonId = scene.GetNextPolygonId();
            CollisionEntry entry = Entries[entryIndex];
            for (int i = 0; i < entry.DataCount; i++)
            {
                CollisionData data = Data[DataIndices[entry.DataStartIndex + i]];
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
            Vector3[] bverts = ArrayPool<Vector3>.Shared.Rent(8);
            Vector3 point0 = MinPosition;
            Vector3i partInc = PartIndexFromEntry(entryIndex);
            point0 = point0.AddX(partInc.X * 4).AddY(partInc.Y * 4).AddZ(partInc.Z * 4);
            var sideX = new Vector3(4, 0, 0);
            var sideY = new Vector3(0, 4, 0);
            var sideZ = new Vector3(0, 0, 4);
            bverts[0] = point0;
            bverts[1] = point0 + sideZ;
            bverts[2] = point0 + sideX;
            bverts[3] = point0 + sideX + sideZ;
            bverts[4] = point0 + sideY;
            bverts[5] = point0 + sideY + sideZ;
            bverts[6] = point0 + sideX + sideY;
            bverts[7] = point0 + sideX + sideY + sideZ;
            polygonId = scene.GetNextPolygonId();
            var bcolor = new Vector4(1, 0.3f, 1, 0.5f);
            scene.AddRenderItem(CullingMode.Front, polygonId, bcolor, RenderItemType.Box, bverts, 8);
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
        public readonly ushort PointCount;
        public readonly ushort PointStartIndex;
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
        public readonly uint EntryCount;
        public readonly uint EntryOffset;
        public readonly uint TreeNodeIndexCount;
        public readonly uint TreeNodeIndexOffset;
        public readonly uint TreeNodeCount;
        public readonly uint TreeNodeOffset;
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

    // size: 28
    public readonly struct FhCollisionEntry
    {
        public readonly Vector3Fx MinBounds;
        public readonly Vector3Fx MaxBounds;
        public readonly ushort LeftIndex;
        public readonly ushort RightIndex;
    }

    // size: 28
    public readonly struct FhCollisionTreeNode
    {
        public readonly Vector3Fx MinBounds;
        public readonly Vector3Fx MaxBounds;
        // for rooms, a RightIndex value of 0x8000 indicates the last node in a branch, and LeftIndex is then used as an entry index;
        // for entities, RightIndex and LeftIndex are always 0x8000 as there is no hierarchy to entity collision
        public readonly ushort LeftIndex;
        public readonly ushort RightIndex;
    }

    public class FhCollisionInfo : CollisionInfo
    {
        public FhCollisionHeader Header { get; }
        public IReadOnlyList<FhCollisionData> Data { get; }
        public IReadOnlyList<FhCollisionVector> Vectors { get; }
        public IReadOnlyList<ushort> DataIndices { get; }

        public IReadOnlyList<FhCollisionEntry> EntryIndices { get; }
        public IReadOnlyList<int> TreeNodeIndices { get; }
        public IReadOnlyList<FhCollisionTreeNode> TreeNodes { get; }

        public FhCollisionInfo(FhCollisionHeader header, IReadOnlyList<Vector3Fx> points, IReadOnlyList<CollisionPlane> planes,
            IReadOnlyList<FhCollisionData> data, IReadOnlyList<FhCollisionVector> vectors, IReadOnlyList<ushort> dataIndices,
            IReadOnlyList<CollisionPortal> portals, IReadOnlyList<FhCollisionEntry> entryIndices, IReadOnlyList<int> treeNodeIndices,
            IReadOnlyList<FhCollisionTreeNode> treeNodes) : base(points, planes, portals, firstHunt: true)
        {
            Header = header;
            Data = data;
            Vectors = vectors;
            DataIndices = dataIndices;
            EntryIndices = entryIndices;
            TreeNodeIndices = treeNodeIndices;
            TreeNodes = treeNodes;
        }

        public override void GetDrawInfo(List<Vector3> points, EntityType entityType, Scene scene)
        {
            var color = new Vector4(Vector3.UnitX, 0.5f);
            color.W = scene.ColDisplayAlpha;
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
                Debug.Assert(data.VectorCount >= 3 && data.VectorCount <= 8);
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
