using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Archive
{
    // size: 32
    public readonly struct ArchiveHeader
    {
        // this is actaully 7 characters and a terminator, so we can use a string
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public readonly string MagicString;
        public readonly uint FileCount;
        public readonly uint TotalSize;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 4)]
        public readonly uint[] Padding;

        public ArchiveHeader(string magicString, uint fileCount, uint totalSize)
        {
            MagicString = magicString;
            FileCount = fileCount;
            TotalSize = totalSize;
            Padding = new uint[] { 0, 0, 0, 0 };
        }

        public ArchiveHeader SwapBytes()
        {
            return new ArchiveHeader(MagicString, FileCount.SwapBytes(), TotalSize.SwapBytes());
        }
    }

    // size: 64
    public readonly struct FileHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public readonly char[] Filename;
        public readonly uint Offset;
        public readonly uint PaddedFileSize;
        public readonly uint TargetFileSize;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 5)]
        public readonly uint[] Padding;

        public FileHeader(char[] filename, uint offset, uint paddedFileSize, uint targetFileSize)
        {
            Filename = filename;
            Offset = offset;
            PaddedFileSize = paddedFileSize;
            TargetFileSize = targetFileSize;
            Padding = new uint[] { 0, 0, 0, 0, 0 };
        }

        public FileHeader(string filename, uint offset, uint paddedFileSize, uint targetFileSize)
        {
            Filename = filename.PadRight(32, '\0').ToCharArray();
            Offset = offset;
            PaddedFileSize = paddedFileSize;
            TargetFileSize = targetFileSize;
            Padding = new uint[] { 0, 0, 0, 0, 0 };
        }

        public FileHeader SwapBytes()
        {
            return new FileHeader(Filename, Offset.SwapBytes(), PaddedFileSize.SwapBytes(), TargetFileSize.SwapBytes());
        }
    }

    public static class ArchiveSizes
    {
        public static readonly int ArchiveHeader = Marshal.SizeOf(typeof(ArchiveHeader));
        public static readonly int FileHeader = Marshal.SizeOf(typeof(FileHeader));
    }

    public static class Archiver
    {
        public static string MagicString { get; } = "SNDFILE";

        public static int Extract(string path, string? destination = null)
        {
            if (destination == null)
            {
                destination = Path.GetDirectoryName(path);
            }
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            if (bytes.Length < ArchiveSizes.ArchiveHeader)
            {
                ThrowRead();
            }
            ArchiveHeader header = Read.ReadStruct<ArchiveHeader>(bytes[0..ArchiveSizes.ArchiveHeader]);
            header = header.SwapBytes();
            if (header.MagicString != MagicString || header.TotalSize != bytes.Length)
            {
                ThrowRead();
            }
            uint pointer = (uint)(ArchiveSizes.ArchiveHeader + ArchiveSizes.FileHeader * header.FileCount); // only for validation
            var files = new List<FileHeader>();
            foreach (FileHeader swap in Read.DoOffsets<FileHeader>(bytes, (uint)ArchiveSizes.ArchiveHeader, (int)header.FileCount))
            {
                FileHeader file = swap.SwapBytes();
                string filename = file.Filename.MarshalString();
                if (filename == "" || file.PaddedFileSize == 0 || file.TargetFileSize == 0
                    || file.Offset > header.TotalSize || file.Offset < ArchiveSizes.ArchiveHeader
                    || file.PaddedFileSize > header.TotalSize || file.TargetFileSize > header.TotalSize
                    || file.PaddedFileSize < file.TargetFileSize || NearestMultiple(file.TargetFileSize, 32) != file.PaddedFileSize
                    || pointer != file.Offset)
                {
                    ThrowRead();
                }
                pointer += file.PaddedFileSize;
                files.Add(file);
            }
            if (files.Count == 0 || files[^1].Offset + files[^1].PaddedFileSize != header.TotalSize)
            {
                ThrowRead();
            }
            int filesWritten = 0;
            foreach (FileHeader file in files)
            {
                string filename = file.Filename.MarshalString();
                int start = (int)file.Offset;
                int end = start + (int)file.TargetFileSize;
                string output = Path.Combine(destination!, filename);
                File.WriteAllBytes(output, bytes[start..end].ToArray());
                filesWritten++;
            }
            return filesWritten;
        }

        public static void Archive(string destinationPath, IEnumerable<string> filePaths)
        {
            if (filePaths == null || filePaths.Count() == 0)
            {
                ThrowWrite();
            }
            var files = new List<byte[]>();
            var entries = new List<FileHeader>();
            uint pointer = (uint)(ArchiveSizes.ArchiveHeader + ArchiveSizes.FileHeader * filePaths.Count());
            foreach (string filePath in filePaths)
            {
                string filename = Path.GetFileName(filePath);
                if (filename.Length > 32)
                {
                    ThrowWrite();
                }
                byte[] file = File.ReadAllBytes(filePath);
                files.Add(file);
                var entry = new FileHeader(
                    filename,
                    pointer,
                    NearestMultiple((uint)file.Length, 32),
                    (uint)file.Length
                );
                entries.Add(entry);
                pointer += entry.PaddedFileSize;
            }
            var header = new ArchiveHeader(MagicString, (uint)filePaths.Count(), pointer);
            using var writer = new BinaryWriter(File.Open(destinationPath, FileMode.Create));
            writer.Write($"{MagicString}\0".ToCharArray());
            writer.Write(header.FileCount.SwapBytes());
            writer.Write(header.TotalSize.SwapBytes());
            for (int i = 0; i < 16; i++)
            {
                writer.Write('\0');
            }
            foreach (FileHeader entry in entries)
            {
                writer.Write(entry.Filename);
                writer.Write(entry.Offset.SwapBytes());
                writer.Write(entry.PaddedFileSize.SwapBytes());
                writer.Write(entry.TargetFileSize.SwapBytes());
                for (int i = 0; i < 20; i++)
                {
                    writer.Write('\0');
                }
            }
            for (int i = 0; i < files.Count; i++)
            {
                writer.Write(files[i]);
                uint padding = entries[i].PaddedFileSize - entries[i].TargetFileSize;
                for (uint j = 0; j < padding; j++)
                {
                    writer.Write((byte)0);
                }
            }
            Debug.Assert(writer.BaseStream.Length == pointer);
        }

        [DoesNotReturn]
        private static void ThrowRead()
        {
            throw new InvalidOperationException("Could not read archive.");
        }

        [DoesNotReturn]
        private static void ThrowWrite()
        {
            throw new InvalidOperationException("Could not write archive.");
        }

        private static uint NearestMultiple(uint value, uint of)
        {
            if (value <= of)
            {
                return value;
            }
            while (value % of != 0)
            {
                value += 1;
            }
            return value;
        }

        public static uint SwapBytes(this uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes);
        }

        private static void Nop() { }
    }
}
