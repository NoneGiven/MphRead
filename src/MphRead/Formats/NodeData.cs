using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MphRead.Formats
{
    public static class NodeData
    {
        public static void ReadNodeData(string path)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            ushort version = Read.SpanReadUshort(bytes, 0);
            if (version == 0)
            {
                ReadFhNodeData(bytes);
                return;
            }
            if (version != 6)
            {
                throw new ProgramException($"Unexpected node data version {version}.");
            }
            ushort count = Read.SpanReadUshort(bytes, 2);
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
