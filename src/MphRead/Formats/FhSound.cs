using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead.Formats.Sound
{
    public static partial class SoundRead
    {
        public static void ExportFhSamples(bool adpcmRoundingError = false)
        {
            ExportSamples(ReadFhSoundSamples(), adpcmRoundingError);
        }

        public static IReadOnlyList<SoundSample> ReadFhSoundSamples()
        {
            string path = @"D:\Cdrv\MPH\_FS\firsthunt\root\data\sound\BGMDATA.BIN";
            //string path = @"D:\Cdrv\MPH\_FS\firsthunt\root\data\sound\GLOBALSFXDATA.BIN";
            //string path = @"D:\Cdrv\MPH\_FS\firsthunt\root\data\sound\MENUSFXDATA.BIN";
            //string path = @"D:\Cdrv\MPH\_FS\firsthunt\root\data\sound\SFXDATA.BIN";
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            var samples = new List<SoundSample>();
            uint id = 0;
            IReadOnlyList<uint> offsets = Read.DoOffsets<uint>(bytes, 4, count);
            foreach (uint offset in offsets)
            {
                FhSoundSampleHeader header = Read.DoOffset<FhSoundSampleHeader>(bytes, offset);
                if (header.PaddingA != 0)
                {
                    Debugger.Break();
                }
                if (header.DataSize <= 4)
                {
                    samples.Add(SoundSample.CreateNull(id));
                }
                else
                {
                    // sktodo: looping
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
        public readonly ushort PaddingA;
        public readonly ushort Volume;
        public readonly byte FieldE;
        public readonly byte FieldF;
        public readonly uint Field10;
        public readonly uint Field14;
    }
}
