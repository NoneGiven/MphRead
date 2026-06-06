using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using NCSFCommon;

namespace NCSF123;

public enum VolumeType
{
    [Description("Disabled")]
    None,
    [Description("Use Volume Tag")]
    Volume,
    [Description("Track")]
    ReplayGainTrack,
    [Description("Album")]
    ReplayGainAlbum
}
public enum PeakType
{
    [Description("Disabled")]
    None,
    [Description("Track")]
    ReplayGainTrack,
    [Description("Album")]
    ReplayGainAlbum
}

// Partially based on my XSFFile clas from my in_xsf C++ project.
public class NCSFFile
{
    const int VersionByte = 0x25;
    const int ProgramSizeOffset = 8;
    const int ProgramHeaderSize = 12;

    readonly List<byte> rawData = [];
    readonly List<byte> reservedSection = [];
    readonly List<byte> programSection = [];

    public string FilePath { get; }
    public ReadOnlySpan<byte> ProgramSection => this.programSection.AsSpan();
    public ReadOnlySpan<byte> ReservedSection => this.reservedSection.AsSpan();
    public TagList Tags { get; private set; } = [];

    public int GetFadeMS(int defaultFade)
    {
        int fade = defaultFade;
        if (this.Tags.Contains("fade"))
            fade = Common.StringToMS(this.Tags["fade"].Value);
        return fade;
    }

    public int GetLengthMS(int defaultLength)
    {
        int length = 0;
        if (this.Tags.Contains("length"))
            length = Common.StringToMS(this.Tags["length"].Value);
        if (length == 0)
            length = defaultLength;
        return length;
    }

    public float GetVolume(VolumeType preferredVolumeType, PeakType preferredPeakType)
    {
        if (preferredVolumeType == VolumeType.None)
            return 1.0f;
        ReadOnlySpan<char> replaygainAlbumGain =
            this.Tags.Contains("replaygain_album_gain") ? this.Tags["replaygain_album_gain"].Value.Trim() : null;
        ReadOnlySpan<char> replaygainAlbumPeak =
            this.Tags.Contains("replaygain_album_peak") ? this.Tags["replaygain_album_peak"].Value.Trim() : null;
        ReadOnlySpan<char> replaygainTrackGain =
            this.Tags.Contains("replaygain_track_gain") ? this.Tags["replaygain_track_gain"].Value.Trim() : null;
        ReadOnlySpan<char> replaygainTrackPeak =
            this.Tags.Contains("replaygain_track_peak") ? this.Tags["replaygain_track_peak"].Value.Trim() : null;
        ReadOnlySpan<char> volume = this.Tags.Contains("volume") ? this.Tags["volume"].Value.Trim() : null;
        float gain = 0;
        bool hadReplayGain = false;
        if (preferredVolumeType == VolumeType.ReplayGainAlbum && replaygainAlbumGain.Length != 0)
        {
            // The following is to remove the dB if it exists, which is should
            int space = replaygainAlbumGain.IndexOf(' ');
            if (space != -1)
                replaygainAlbumGain = replaygainAlbumGain[..space];
            gain = float.Parse(replaygainAlbumGain);
            hadReplayGain = true;
        }
        if (!hadReplayGain && preferredVolumeType != VolumeType.Volume && replaygainTrackGain.Length != 0)
        {
            // The following is to remove the dB if it exists, which is should
            int space = replaygainTrackGain.IndexOf(' ');
            if (space != -1)
                replaygainTrackGain = replaygainTrackGain[..space];
            gain = float.Parse(replaygainTrackGain);
            hadReplayGain = true;
        }
        if (hadReplayGain)
        {
            float vol = float.Pow(10, gain / 20), peak = 1;
            if (preferredPeakType == PeakType.ReplayGainAlbum && replaygainAlbumPeak.Length != 0)
                peak = float.Parse(replaygainAlbumPeak);
            else if (preferredPeakType != PeakType.None && replaygainTrackPeak.Length != 0)
                peak = float.Parse(replaygainTrackPeak);
            return peak != 1 ? float.Min(vol, 1 / peak) : vol;
        }
        return volume.Length == 0 ? 1 : float.Parse(volume);
    }

    void ReadNCSF(string path, bool readTagsOnly = false)
    {
        //using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
        //using var mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
        //using var ma = mmf.CreateMemoryAccessor(0, (int)fs.Length, MemoryMappedFileAccess.Read);
        ReadOnlySpan<byte> fileBytes = File.ReadAllBytes(path);

        NCSFCommon.NCSF.CheckForValidPSF(fileBytes, NCSFFile.VersionByte);

        uint reservedSize = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes[0x04..]);
        uint programCompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes[0x08..]);
        CollectionsMarshal.SetCount(this.rawData, fileBytes.Length);
        fileBytes.CopyTo(this.rawData.AsSpan());

        if (!readTagsOnly)
        {
            if (reservedSize != 0)
            {
                CollectionsMarshal.SetCount(this.reservedSection, (int)reservedSize);
                fileBytes.Slice(0x10, (int)reservedSize).CopyTo(this.reservedSection.AsSpan());
            }

            if (programCompressedSize != 0)
            {
                Span<byte> programSection = NCSFCommon.NCSF.GetProgramSectionFromPSF(fileBytes, NCSFFile.VersionByte,
                    NCSFFile.ProgramHeaderSize, NCSFFile.ProgramSizeOffset);
                CollectionsMarshal.SetCount(this.programSection, programSection.Length);
                programSection.CopyTo(this.programSection.AsSpan());
            }
        }

        this.Tags = NCSFCommon.NCSF.GetTagsFromPSF(fileBytes, NCSFFile.VersionByte);
    }

    /*string FormattedTitleOptionalBlock(string block, out bool hadReplacement, uint level)
    {

    }*/

    public NCSFFile(string path) => this.ReadNCSF(this.FilePath = path);
}
