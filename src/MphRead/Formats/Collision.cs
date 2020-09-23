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
        public static void ReadCollision(string path, int roomLayerMask = -1)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            CollisionHeader header = Read.ReadStruct<CollisionHeader>(bytes);
            if (new string(header.Type) != "wc01")
            {
                throw new ProgramException("Invalid collision format.");
            }
            IReadOnlyList<Vector3Fx> vectors = Read.DoOffsets<Vector3Fx>(bytes, header.VectorOffset, header.VectorCount);
            IReadOnlyList<CollisionPlane> planes = Read.DoOffsets<CollisionPlane>(bytes, header.PlaneOffset, header.PlaneCount);
            IReadOnlyList<ushort> shorts = Read.DoOffsets<ushort>(bytes, header.ShortOffset, header.ShortCount);
            IReadOnlyList<CollisionData> data = Read.DoOffsets<CollisionData>(bytes, header.DataOffset, header.DataCount);
            IReadOnlyList<ushort> indices = Read.DoOffsets<ushort>(bytes, header.IndexOffset, header.IndexCount);
            IReadOnlyList<CollisionEntry> entries = Read.DoOffsets<CollisionEntry>(bytes, header.EntryOffset, header.EntryCount);
            IReadOnlyList<CollisionPortal> portals = Read.DoOffsets<CollisionPortal>(bytes, header.PortalOffset, header.PortalCount);
            foreach (CollisionEntry entry in entries.Where(e => e.Count > 0))
            {
                Debug.Assert(entry.Count < 512);
                var enabledIndices = new List<ushort>();
                for (int i = 0; i < entry.Count; i++)
                {
                    ushort index = indices[entry.StartIndex + i];
                    ushort layerMask = data[index].LayerMask;
                    if ((layerMask & 4) != 0 || roomLayerMask == -1 || (layerMask & roomLayerMask) != 0)
                    {
                        enabledIndices.Add(index);
                    }
                }
            }
            Nop();
        }

        private static void Nop() { }
    }

    // size: 84
    public readonly struct CollisionHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly char[] Type; // wc01 - no terminator
        public readonly uint VectorCount;
        public readonly uint VectorOffset;
        public readonly uint PlaneCount;
        public readonly uint PlaneOffset;
        public readonly uint ShortCount;
        public readonly uint ShortOffset;
        public readonly uint DataCount;
        public readonly uint DataOffset;
        public readonly uint IndexCount;
        public readonly uint IndexOffset;
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
        public readonly uint Field4;
        public readonly ushort LayerMask;
        public readonly ushort FieldA;
        public readonly uint FieldC;
    }

    // size: 4
    public readonly struct CollisionEntry
    {
        public readonly ushort Count;
        public readonly ushort StartIndex;
    }

    // size: 224
    public readonly struct CollisionPortal
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
        public readonly string Name;
        public readonly uint Field18;
        public readonly uint Field1C;
        public readonly uint Field20;
        public readonly uint Field24;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
        public readonly string NodeName1; // side 0 room node
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
        public readonly string NodeName2; // side 1 room node
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
}
