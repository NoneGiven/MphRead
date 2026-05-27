using System.Buffers.Binary;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Entry for BANK Record.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// (Although it was wrong about the File ID being a 16-bit integer, it is actually a 32-bit integer.)
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class INFOEntryBANK : INFOEntry
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	protected override string DebuggerDisplay =>
		$"INFO Entry (BANK) - {base.DebuggerDisplay}File ID: {this.FileID}, WaveArchives: {{{string.Join(", ", this.waveArchives)}}}";

	/// <summary>
	/// The file ID within the <see cref="FATSection" /> for this entry.
	/// </summary>
	public uint FileID { get; set; }

	readonly ushort[] waveArchives = new ushort[4];

	/// <summary>
	/// The 4 associated wave archives, the values being the index within the WAVEARC Record in the <see cref="INFOSection" />.
	/// <see cref="ushort.MaxValue" /> is used for a wave archive that is not in use.
	/// </summary>
	public ReadOnlySpan<ushort> WaveArchives => this.waveArchives.AsSpan();

	/// <summary>
	/// The <see cref="NC.SBNK" /> that this entry corresponds to, for easier access.
	/// </summary>
	public SBNK? SBNK { get; set; }

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryBANK" />
	/// </summary>
	public INFOEntryBANK()
	{
	}

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryBANK" /> copied from an existing instance.
	/// </summary>
	/// <param name="other">The instance of <see cref="INFOEntryBANK" /> to copy from.</param>
	public INFOEntryBANK(INFOEntryBANK other) : base(other)
	{
		this.FileID = other.FileID;
		other.waveArchives.AsSpan().CopyTo(this.waveArchives);
		this.SBNK = other.SBNK;
	}

	/// <inheritdoc />
	/// <remarks>
	/// File ID (32-bit integer) + 4 Wave Archive references (each 16-bit integers). (Should be 12 bytes.)
	/// </remarks>
	public override uint Size { get; } = 0x0C;

	public override INFOEntryBANK Read(ReadOnlySpan<byte> span)
	{
		this.FileID = BinaryPrimitives.ReadUInt32LittleEndian(span);
		span[0x04..0x0C].Cast<byte, ushort>().CopyTo(this.waveArchives);
		return this;
	}

	public override void Write(Span<byte> span)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(span, this.FileID);
		this.WaveArchives.AsBytes().CopyTo(span[0x04..]);
	}

	/// <summary>
	/// Replaces the given wave archive.
	/// </summary>
	/// <param name="i">The index of the wave archive to replace.</param>
	/// <param name="newWaveArchive">The value to replace the wave archive with.</param>
	/// <exception cref="ArgumentOutOfRangeException">If <paramref name="i" /> is not between 0 and 3 inclusive.</exception>
	public void ReplaceWaveArchive(int i, ushort newWaveArchive)
	{
		Guard.IsBetweenOrEqualTo(i, 0, 3);

		this.waveArchives[i] = newWaveArchive;
	}

	/// <summary>
	/// Checks if the associated <see cref="SBNK" />s between two entries is the same.
	/// </summary>
	/// <param name="other">The other <see cref="INFOEntryBANK" /> to check.</param>
	/// <returns><see langword="true" /> if the <see cref="SBNK" />s are the same, <see langword="false" /> otherwise.</returns>
	public bool FileEquals(INFOEntryBANK? other) => other is not null && this.SBNK == other.SBNK;
}
