using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.HighPerformance.Buffers;
using NCSFCommon;
using NCSFCommon.NC;
using NCSFCommon.ReplayGain;

namespace MphRead.Testing
{
    public class TestNcsf
    {
        public static void Test()
        {
            ReadOnlySpan<byte> sdatBytes = File.ReadAllBytes(@"C:\Users\auser\Home\MPH\Sound\copy.sdat");

            string dirName = @"C:\Users\auser\Home\MPH\Sound\NCSF\NDStoNCSF\bin\Debug\net9.0\output";
            Directory.CreateDirectory(dirName);

            var finalSDAT = new SDAT();

            int sdatNumber = 1;

            var sdat = new SDAT();
            sdat.Read(sdatNumber.ToString(), sdatBytes);
            finalSDAT += sdat;

            finalSDAT.FixOffsetsAndSizes();

            using var memoryOwner = MemoryOwner<byte>.Allocate((int)finalSDAT.Size);
            finalSDAT.Write(memoryOwner.Span);

            var seqEntries = finalSDAT.INFOSection.SEQRecord.Entries;

            string ncsflibFilename = "mph.ncsflib";
            NCSFCommon.NCSF.MakeNCSF(Path.Combine(dirName, ncsflibFilename), [], memoryOwner.Span);
            TagList tags = [("_lib", ncsflibFilename), ("utf8", "1"), ("ncsfby", "MphRead")];
            AlbumGain albumGain = new();
            Dictionary<uint, TagList> fileTags = new(seqEntries.Length);
            for (uint i = 0, count = (uint)seqEntries.Length; i < count; ++i)
            {
                var (Offset, Entry) = seqEntries[(int)i];
                if (Offset != 0 && Entry is not null)
                {
                    if (Entry.SSEQ!.Filename!.StartsWith("SSEQ"))
                    {
                        Entry.SSEQ!.Filename = $"{i:X4} - {Entry.SSEQ!.Filename}";
                    }
                    string minincsfFilename = $"{Entry.SSEQ!.Filename}.minincsf";

                    var thisTags = tags.Clone();
                    string fullFilename = Entry.FullFilename(sdatNumber > 1);

                    thisTags.AddOrReplace(("origFilename", Entry.SSEQ.OriginalFilename!));
                    if (sdatNumber > 1)
                    {
                        thisTags.AddOrReplace(("origSDAT", Entry.SDATNumber));
                    }

                    //NCSFTimer.NCSF.GetTime(minincsfFilename, finalSDAT, Entry.SSEQ, thisTags, false, numberOfLoops: 2, fadeLoop: 10, fadeOneShot: 1);

                    fileTags[i] = thisTags;
                }
            }

            for (uint i = 0, count = (uint)seqEntries.Length; i < count; ++i)
            {
                var (Offset, Entry) = seqEntries[(int)i];
                if (Offset != 0 && Entry is not null)
                {
                    string minincsfFilename = $"{Entry.SSEQ!.Filename}.minincsf";

                    var thisTags = fileTags[i];

                    NCSFCommon.NCSF.MakeNCSF(Path.Combine(dirName, minincsfFilename), BitConverter.GetBytes(i), [], thisTags);
                }
            }
        }
    }
}
