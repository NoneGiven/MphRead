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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public readonly string MagicString;
        public readonly uint FileCount;
        public readonly uint TotalSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public readonly string Padding;

        public ArchiveHeader(string magicString, uint fileCount, uint totalSize, string padding)
        {
            MagicString = magicString;
            FileCount = fileCount;
            TotalSize = totalSize;
            Padding = padding;
        }

        public ArchiveHeader SwapBytes()
        {
            return new ArchiveHeader(MagicString, FileCount.SwapBytes(), TotalSize.SwapBytes(), Padding);
        }
    }

    // size: 64
    public readonly struct FileHeader
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public readonly string Filename;
        public readonly uint Offset;
        public readonly uint PaddedFileSize;
        public readonly uint TargetFileSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public readonly string Padding;

        public FileHeader(string filename, uint offset, uint paddedFileSize, uint targetFileSize, string padding)
        {
            Filename = filename;
            Offset = offset;
            PaddedFileSize = paddedFileSize;
            TargetFileSize = targetFileSize;
            Padding = padding;
        }

        public FileHeader SwapBytes()
        {
            return new FileHeader(Filename, Offset.SwapBytes(), PaddedFileSize.SwapBytes(), TargetFileSize.SwapBytes(), Padding);
        }
    }

    public static class ArchiveSizes
    {
        public static readonly int ArchiveHeader = Marshal.SizeOf(typeof(ArchiveHeader));
        public static readonly int FileHeader = Marshal.SizeOf(typeof(FileHeader));
    }

    public static class Archiver
    {
        private static readonly string _magicString = "SNDFILE";

        public static void Extract(string path)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            if (bytes.Length < ArchiveSizes.ArchiveHeader)
            {
                ThrowRead();
            }
            ArchiveHeader header = Read.ReadStruct<ArchiveHeader>(bytes[0..ArchiveSizes.ArchiveHeader]);
            header = header.SwapBytes();
            if (header.MagicString != _magicString || header.TotalSize != bytes.Length)
            {
                ThrowRead();
            }
            uint pointer = (uint)(ArchiveSizes.ArchiveHeader + ArchiveSizes.FileHeader * header.FileCount); // only for validation
            var files = new List<FileHeader>();
            foreach (FileHeader swap in Read.DoOffsets<FileHeader>(bytes, (uint)ArchiveSizes.ArchiveHeader, (int)header.FileCount))
            {
                FileHeader file = swap.SwapBytes();
                if (file.Filename == "" || file.PaddedFileSize == 0 || file.TargetFileSize == 0
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
            foreach (FileHeader file in files)
            {
                int start = (int)file.Offset;
                int end = start + (int)file.TargetFileSize;
                File.WriteAllBytes(file.Filename, bytes[start..end].ToArray());
            }
            Nop();
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
                    (uint)file.Length,
                    ""
                );
                entries.Add(entry);
                pointer += entry.PaddedFileSize;
            }
            var header = new ArchiveHeader(_magicString, (uint)filePaths.Count(), pointer, "");
            using var writer = new BinaryWriter(File.Open(destinationPath, FileMode.Create));
            writer.Write($"{_magicString}\0".ToCharArray());
            writer.Write(header.FileCount.SwapBytes());
            writer.Write(header.TotalSize.SwapBytes());
            for (int i = 0; i < 16; i++)
            {
                writer.Write('\0');
            }
            foreach (FileHeader entry in entries)
            {
                writer.Write(entry.Filename.ToCharArray());
                for (int i = 0; i < 32 - entry.Filename.Length; i++)
                {
                    writer.Write('\0');
                }
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
