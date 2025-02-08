using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead.Formats.Sound
{
    public static partial class SoundRead
    {
        public static void ExportAllFh(bool adpcmRoundingError = false)
        {
            ExportFhBgm(adpcmRoundingError);
            ExportFhGlobalSfx(adpcmRoundingError);
            ExportFhMenuSfx(adpcmRoundingError);
            ExportFhSfx(adpcmRoundingError);
        }

        public static void ExportFhSfx(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadFhSfx(), adpcmRoundingError, prefix: "fh_");
        }

        public static void ExportFhBgm(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadFhBgm(), adpcmRoundingError, prefix: "fh_bgm_");
        }

        public static void ExportFhMenuSfx(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadFhMenuSfx(), adpcmRoundingError, prefix: "fh_menu_");
        }

        public static void ExportFhGlobalSfx(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadFhGlobalSfx(), adpcmRoundingError, prefix: "fh_lid_");
        }

        public static IReadOnlyList<SoundSample> ReadFhSfx()
        {
            return ReadFhSoundFile("SFXDATA.BIN");
        }

        public static IReadOnlyList<SoundSample> ReadFhBgm()
        {
            return ReadFhSoundFile("BGMDATA.BIN");
        }

        public static IReadOnlyList<SoundSample> ReadFhMenuSfx()
        {
            return ReadFhSoundFile("MENUSFXDATA.BIN");
        }

        public static IReadOnlyList<SoundSample> ReadFhGlobalSfx()
        {
            return ReadFhSoundFile("GLOBALSFXDATA.BIN");
        }

        public static IReadOnlyList<SoundSample> ReadFhSoundFile(string filename)
        {
            string path = Paths.Combine(Paths.FhFileSystem, "sound", filename);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            var samples = new List<SoundSample>();
            uint id = 0;
            IReadOnlyList<uint> offsets = Read.DoOffsets<uint>(bytes, 4, count);
            foreach (uint offset in offsets)
            {
                FhSoundSampleHeader header = Read.DoOffset<FhSoundSampleHeader>(bytes, offset);
                if (header.DataSize <= 4)
                {
                    samples.Add(SoundSample.CreateNull(id));
                }
                else
                {
                    long start = offset + Marshal.SizeOf<FhSoundSampleHeader>();
                    uint size = header.DataSize;
                    samples.Add(new SoundSample(id, offset, header, bytes.Slice(start, size)));
                }
                id++;
            }
            return samples;
        }
    }

    // size: 24
    public readonly struct FhSoundSampleHeader
    {
        public readonly uint DataSize;
        public readonly uint DataPointer; // always zero in the file
        public readonly ushort SampleRate;
        public readonly ushort FieldA; // probably padding
        public readonly ushort Volume;
        public readonly byte FieldE; // padding padding
        public readonly byte Format;
        public readonly uint LoopStart;
        public readonly uint LoopEnd;
    }
}
