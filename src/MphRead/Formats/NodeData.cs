using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace MphRead.Formats
{
    public static class ReadNodeData
    {
        public static void TestAll()
        {
            foreach (string path in Directory.EnumerateFiles(Paths.Combine(Paths.FileSystem, @"levels\nodeData")))
            {
                if (!path.EndsWith(@"levels\nodeData\unit2_Land_Node.bin")) // todo: version 4
                {
                    ReadData(Paths.Combine(@"levels\nodeData", Path.GetFileName(path)));
                }
            }
            Nop();
        }

        public static NodeData ReadData(string path)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Paths.Combine(Paths.FileSystem, path)));
            ushort version = Read.SpanReadUshort(bytes, 0);
            if (version == 0)
            {
                // todo: this
                ReadFhNodeData(bytes);
                return null!;
            }
            // todo: version 4 (unit2_Land_Node.bin)
            if (version != 6)
            {
                throw new ProgramException($"Unexpected node data version {version}.");
            }
            NodeDataHeader header = Read.ReadStruct<NodeDataHeader>(bytes);
            int setIndexCount = header.IndexCount;
            // todo: when there are multiple sets and when the sets contain multiple lists,
            // we should allow toggling which one is displayed in the room (sub-volume display)
            Debug.Assert(setIndexCount == 0 || setIndexCount == 1);
            Debug.Assert(header.DataOffset == header.IndexOffset + 2);
            var types = new HashSet<uint>();
            // track the minimum Offset1 so each data3's Offset1/Offset2 can be converted to an index
            uint min = UInt32.MaxValue;
            IReadOnlyList<ushort> setIndices = Read.DoOffsets<ushort>(bytes, header.IndexOffset, setIndexCount);
            var data = new List<IReadOnlyList<IReadOnlyList<NodeDataStruct3>>>();
            IReadOnlyList<NodeDataStruct1> str1s = Read.DoOffsets<NodeDataStruct1>(bytes, header.DataOffset, header.DataCount);
            foreach (NodeDataStruct1 str1 in str1s)
            {
                var sub = new List<IReadOnlyList<NodeDataStruct3>>();
                IReadOnlyList<NodeDataStruct2> str2s = Read.DoOffsets<NodeDataStruct2>(bytes, str1.Offset2, str1.Count);
                foreach (NodeDataStruct2 str2 in str2s)
                {
                    IReadOnlyList<NodeDataStruct3> str3s = Read.DoOffsets<NodeDataStruct3>(bytes, str2.Offset3, str2.Count);
                    foreach (NodeDataStruct3 str3 in str3s)
                    {
                        min = Math.Min(min, str3.Offset1);
                        types.Add(str3.NodeType);
                    }
                    sub.Add(str3s);
                }
                data.Add(sub);
            }
            int valueCount = (int)((bytes.Length - min) / 2);
            IReadOnlyList<ushort> values = Read.DoOffsets<ushort>(bytes, min, valueCount);
            var cast = new List<IReadOnlyList<IReadOnlyList<NodeData3>>>();
            foreach (IReadOnlyList<IReadOnlyList<NodeDataStruct3>> sub in data)
            {
                var newSub = new List<IReadOnlyList<NodeData3>>();
                foreach (IReadOnlyList<NodeDataStruct3> str3s in sub)
                {
                    var cast3s = new List<NodeData3>();
                    foreach (NodeDataStruct3 str3 in str3s)
                    {
                        // determine an index for where the offset points in the entire list
                        int index1 = (int)((str3.Offset1 - min) / 2);
                        int index2 = (int)((str3.Offset2 - min) / 2);
                        cast3s.Add(new NodeData3(str3, index1, index2, values));
                    }
                    newSub.Add(cast3s);
                }
                cast.Add(newSub);
            }
            var nodeData = new NodeData(header, setIndices, cast);
            return nodeData;
        }

        private static void ReadFhNodeData(ReadOnlySpan<byte> bytes)
        {
            ushort count = Read.SpanReadUshort(bytes, 2);
            IReadOnlyList<FhNodeData> headers = Read.DoOffsets<FhNodeData>(bytes, 4, (uint)count);
            if (headers.Any(h => h.Field0 == 1))
            {
                Debugger.Break();
            }
        }

        public static NodeData3? FindClosestNode(NodeData nodeData, Vector3 position, bool useMaxDist = false)
        {
            Debug.Assert(nodeData.Data.Count > 0 && nodeData.Data[0].Count > 0);
            IReadOnlyList<NodeData3> list = nodeData.Data[0][0];
            NodeData3? result = null;
            float minDist = Single.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                NodeData3 data = list[i];
                float dist = Vector3.DistanceSquared(position, data.Position);
                if (dist < minDist)
                {
                    result = data;
                    minDist = dist;
                }
            }
            if (result != null && useMaxDist && minDist > result.MaxDistance * result.MaxDistance)
            {
                result = null;
            }
            return result;
        }

        private static void Nop()
        {
        }
    }

    public class NodeData
    {
        public NodeDataHeader Header { get; }
        public IReadOnlyList<ushort> SetIndices { get; }
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<NodeData3>>> Data { get; }

        public bool Simple => Data.Count == 1 && Data[0].Count == 1;

        public bool[] SetSelector { get; } = new bool[16];

        public NodeData(NodeDataHeader header, IReadOnlyList<ushort> setIndices,
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NodeData3>>> data)
        {
            Header = header;
            SetIndices = setIndices;
            Data = data;
        }
    }

    public enum NodeType : ushort
    {
        Navigation = 0,
        UnknownGreen = 1,
        Aerial = 2,
        Vantage = 3,
        AltForm = 4,
        Hazard = 5
    }

    public class NodeData3
    {
        public NodeType NodeType { get; }
        public ushort Id { get; }
        public uint Field4 { get; }
        public int Count2 { get; }
        public Vector3 Position { get; }
        public float MaxDistance { get; }
        // Offset1 has no defined count, so we just include the whole list
        // and keep indices for use as a starting point for Offset1 and Offset2
        public int Index1 { get; }
        public int Index2 { get; }
        public IReadOnlyList<ushort> Values { get; }

        public Matrix4 Transform { get; }
        public Vector4 Color { get; }

        private static readonly IReadOnlyList<Vector4> _nodeDataColors = new List<Vector4>()
        {
            new Vector4(1, 0, 0, 1), // 0 - red     (navigation)
            new Vector4(0, 1, 0, 1), // 1 - green   (?)
            new Vector4(0, 0, 1, 1), // 2 - blue    (aerial movement)
            new Vector4(0, 1, 1, 1), // 3 - cyan    (vantage point)
            new Vector4(1, 0, 1, 1), // 4 - magenta (alt form)
            new Vector4(1, 1, 0, 1), // 5 - yellow  (hazard)
        };

        public NodeData3(NodeDataStruct3 raw, int index1, int index2, IReadOnlyList<ushort> values)
        {
            NodeType = (NodeType)raw.NodeType;
            Id = raw.Id;
            Field4 = raw.Field4;
            Count2 = raw.Count2;
            Position = raw.Position.ToFloatVector();
            MaxDistance = Fixed.ToFloat(raw.MaxDistance);
            Index1 = index1;
            Index2 = index2;
            Values = values;
            Transform = Matrix4.CreateTranslation(Position);
            Color = _nodeDataColors[raw.NodeType];
        }
    }

    // size: 14
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly struct NodeDataHeader
    {
        public readonly ushort Version;
        public readonly ushort DataCount;
        public readonly uint IndexOffset;
        public readonly uint DataOffset;
        public readonly ushort IndexCount;
    }

    // size: 8
    public readonly struct NodeDataStruct1
    {
        public readonly uint Offset2;
        public readonly ushort Count;
        public readonly ushort Padding6; // always 0x5C
    }

    // size: 8
    public readonly struct NodeDataStruct2
    {
        public readonly uint Offset3;
        public readonly ushort Count;
        public readonly ushort Padding6;  // always 0x5C
    }

    // size: 36
    public readonly struct NodeDataStruct3
    {
        public readonly ushort NodeType;
        public readonly ushort Id;
        public readonly ushort Field4;
        public readonly ushort Count2; // count for Offset2
        public readonly Vector3Fx Position;
        public readonly int MaxDistance;
        public readonly uint Offset1;
        public readonly uint Offset2;
        public readonly uint Offset3; // always 0
    }

    // size: 24
    public readonly struct FhNodeData
    {
        public readonly ushort Field0;
        public readonly ushort Field2;
        public readonly uint Field4;
        public readonly uint Field8;
        public readonly uint FieldC;
        public readonly uint Field10;
        public readonly uint Field14;
    }
}
