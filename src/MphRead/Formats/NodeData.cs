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
            foreach (string path in Directory.EnumerateFiles(Path.Combine(Paths.FileSystem, @"levels\nodeData")))
            {
                if (!path.EndsWith(@"levels\nodeData\unit2_Land_Node.bin")) // todo: version 4
                {
                    ReadData(Path.Combine(@"levels\nodeData", Path.GetFileName(path)));
                }
            }
            Nop();
        }

        public static NodeData ReadData(string path)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            ushort version = Read.SpanReadUshort(bytes, 0);
            if (version == 0)
            {
                ReadFhNodeData(bytes);
                return null!;
            }
            if (version != 6)
            {
                throw new ProgramException($"Unexpected node data version {version}.");
            }
            NodeDataHeader header = Read.ReadStruct<NodeDataHeader>(bytes);
            int shortCount = header.SomeCount;
            if (shortCount == 0)
            {
                Debug.Assert(header.Offset1 == header.Offset0 + 2);
                shortCount = 1;
            }
            var types = new HashSet<uint>();
            uint min = UInt32.MaxValue;
            IReadOnlyList<ushort> shorts = Read.DoOffsets<ushort>(bytes, header.Offset0, shortCount);
            var data = new List<IReadOnlyList<IReadOnlyList<NodeDataStruct3>>>();
            IReadOnlyList<NodeDataStruct1> str1s = Read.DoOffsets<NodeDataStruct1>(bytes, header.Offset1, header.Count);
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
                        types.Add(str3.Field0);
                    }
                    sub.Add(str3s);
                }
                data.Add(sub);
            }
            Console.WriteLine(path);
            Console.WriteLine(String.Join(", ", types.OrderBy(t => t)));
            Console.WriteLine();
            int indexCount = (int)((bytes.Length - min) / 2);
            IReadOnlyList<ushort> indices = Read.DoOffsets<ushort>(bytes, min, indexCount);
            var cast = new List<IReadOnlyList<IReadOnlyList<NodeData3>>>();
            foreach (IReadOnlyList<IReadOnlyList<NodeDataStruct3>> sub in data)
            {
                var newSub = new List<IReadOnlyList<NodeData3>>();
                foreach (IReadOnlyList<NodeDataStruct3> str3s in sub)
                {
                    var cast3s = new List<NodeData3>();
                    foreach (NodeDataStruct3 str3 in str3s)
                    {
                        int index1 = (int)((str3.Offset1 - min) / 2);
                        int index2 = (int)((str3.Offset2 - min) / 2);
                        cast3s.Add(new NodeData3(str3, index1, index2));
                    }
                    newSub.Add(cast3s);
                }
                cast.Add(newSub);
            }
            var nodeData = new NodeData(header, shorts, indices, cast);
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

        private static void Nop()
        {
        }
    }

    public class NodeData
    {
        public NodeDataHeader Header { get; }
        public IReadOnlyList<ushort> Shorts { get; }
        public IReadOnlyList<ushort> Indices { get; }
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<NodeData3>>> Data { get; }

        public NodeData(NodeDataHeader header, IReadOnlyList<ushort> shorts, IReadOnlyList<ushort> indices,
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NodeData3>>> data)
        {
            Header = header;
            Shorts = shorts;
            Indices = indices;
            Data = data;
        }
    }

    public class NodeData3
    {
        public ushort Field0 { get; }
        public ushort Field2 { get; }
        public uint Field4 { get; }
        public Vector3 Position { get; }
        public uint Field14 { get; }
        public int Index1 { get; }
        public int Index2 { get; }

        public Matrix4 Transform { get; }
        public Vector4 Color { get; }

        private static readonly IReadOnlyList<Vector4> _nodeDataColors = new List<Vector4>()
        {
            new Vector4(1, 0, 0, 1), // 0 - red
            new Vector4(0, 1, 0, 1), // 1 - green
            new Vector4(0, 0, 1, 1), // 2 - blue
            new Vector4(0, 1, 1, 1), // 3 - cyan
            new Vector4(1, 0, 1, 1), // 4 - magenta
            new Vector4(1, 1, 0, 1), // 5 - yellow
        };

        public NodeData3(NodeDataStruct3 raw, int index1, int index2)
        {
            Field0 = raw.Field0;
            Field2 = raw.Field2;
            Field4 = raw.Field4;
            Position = raw.Position.ToFloatVector();
            Field14 = raw.Field14;
            Index1 = index1;
            Index2 = index2;
            Transform = Matrix4.CreateTranslation(Position);
            Color = _nodeDataColors[Field0];
        }
    }

    // size: 14
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly struct NodeDataHeader
    {
        public readonly ushort Version;
        public readonly ushort Count;
        public readonly uint Offset0;
        public readonly uint Offset1;
        public readonly ushort SomeCount;
    }

    // size: 8
    public readonly struct NodeDataStruct1
    {
        public readonly uint Offset2;
        public readonly ushort Count;
        public readonly ushort Field6; // always 0x5C
    }

    // size: 8
    public readonly struct NodeDataStruct2
    {
        public readonly uint Offset3;
        public readonly ushort Count;
        public readonly ushort Field6;  // always 0x5C
    }

    // size: 36
    public readonly struct NodeDataStruct3
    {
        public readonly ushort Field0;
        public readonly ushort Field2;
        public readonly uint Field4;
        public readonly Vector3Fx Position;
        public readonly uint Field14;
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
