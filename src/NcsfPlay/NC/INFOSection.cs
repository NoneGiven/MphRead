using System.Buffers.Binary;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using DotNext;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Section.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class INFOSection
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	string DebuggerDisplay => $"INFO Section - Record Offsets: {{{string.Join(", ", this.recordOffsets)}}}";

	/// <summary>
	/// "INFO" as bytes.
	/// </summary>
	static readonly ReadOnlyMemory<byte> Header = "INFO"u8.ToArray();

	/// <summary>
	/// The size of this section.
	/// </summary>
	/// <remarks>
	/// Header (4 bytes) + Size (32-bit integer) + 8 Record Offsets (each 32-bit integers) + 24 bytes of Reserved +
	/// 4 Count values for the unused Records
	/// (each 32-bit integers, their counts stored as 0s, the unused Records being SEQARC, GROUP, PLAYER2 and STRM) +
	/// size of each actually used Record (the used Records being SEQ, BANK, WAVEARC and PLAYER). (Will be at least 96 bytes.)
	/// </remarks>
	public uint Size => 0x50 + this.SEQRecord.Size + this.BANKRecord.Size + this.WAVEARCRecord.Size + this.PLAYERRecord.Size;

	readonly uint[] recordOffsets = new uint[8];

	/// <summary>
	/// The 8 offsets for the record blocks contained in this section.
	/// </summary>
	public ReadOnlySpan<uint> RecordOffsets => this.recordOffsets.AsSpan();

	/// <summary>
	/// The SEQ record block.
	/// </summary>
	public INFORecord<INFOEntrySEQ> SEQRecord { get; private set; } = new();

	/// <summary>
	/// The BANK record block.
	/// </summary>
	public INFORecord<INFOEntryBANK> BANKRecord { get; private set; } = new();

	/// <summary>
	/// The WAVEARC record block.
	/// </summary>
	public INFORecord<INFOEntryWAVEARC> WAVEARCRecord { get; private set; } = new();

	/// <summary>
	/// The PLAYER record block.
	/// </summary>
	public INFORecord<INFOEntryPLAYER> PLAYERRecord { get; private set; } = new();

	/// <summary>
	/// Reads the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <exception cref="InvalidDataException">If the header is incorrect.</exception>
	public void Read(ReadOnlySpan<byte> span)
	{
		if (!Common.VerifyHeader(span[..0x04], INFOSection.Header.Span))
			ThrowHelper.ThrowInvalidDataException("SDAT INFO Section invalid");
		// Skipping size as we are just calculating that on the fly.
		// Record offsets start at byte 8 of the INFO section.
		span[0x08..0x28].Cast<byte, uint>().CopyTo(this.recordOffsets);
		var (sequenceOffset, bankOffset, waveArchiveOffset, playerOffset) = (
			this.recordOffsets[Common.SDATRecordType.Sequence.ToByte()],
			this.recordOffsets[Common.SDATRecordType.Bank.ToByte()],
			this.recordOffsets[Common.SDATRecordType.WaveArchive.ToByte()],
			this.recordOffsets[Common.SDATRecordType.Player.ToByte()]
		);
		if (sequenceOffset != 0)
			this.SEQRecord.Read(span[(int)sequenceOffset..], sequenceOffset);
		if (bankOffset != 0)
			this.BANKRecord.Read(span[(int)bankOffset..], bankOffset);
		if (waveArchiveOffset != 0)
			this.WAVEARCRecord.Read(span[(int)waveArchiveOffset..], waveArchiveOffset);
		if (playerOffset != 0)
			this.PLAYERRecord.Read(span[(int)playerOffset..], playerOffset);
	}

	/// <summary>
	/// Fixes the offsets of the record blocks in this section.
	/// </summary>
	public void FixOffsets()
	{
		// Starting from 0x40 for the SEQ Record, each record is offset from the previous one by a 32-bit value
		// (the count of entries in that record) and for the records we are concerned with,
		// also by the number of entries in the record (which are also 32-bit values).
		this.recordOffsets[Common.SDATRecordType.Sequence.ToByte()] = 0x40;
		this.recordOffsets[Common.SDATRecordType.SequenceArchive.ToByte()] =
			this.recordOffsets[Common.SDATRecordType.Sequence.ToByte()] + 4 + 4 * (uint)this.SEQRecord.Entries.Length;
		this.recordOffsets[Common.SDATRecordType.Bank.ToByte()] = this.recordOffsets[Common.SDATRecordType.SequenceArchive.ToByte()] + 4;
		this.recordOffsets[Common.SDATRecordType.WaveArchive.ToByte()] =
			this.recordOffsets[Common.SDATRecordType.Bank.ToByte()] + 4 + 4 * (uint)this.BANKRecord.Entries.Length;
		this.recordOffsets[Common.SDATRecordType.Player.ToByte()] =
			this.recordOffsets[Common.SDATRecordType.WaveArchive.ToByte()] + 4 + 4 * (uint)this.WAVEARCRecord.Entries.Length;
		this.recordOffsets[Common.SDATRecordType.Group.ToByte()] =
			this.recordOffsets[Common.SDATRecordType.Player.ToByte()] + 4 + 4 * (uint)this.PLAYERRecord.Entries.Length;
		this.recordOffsets[Common.SDATRecordType.Player2.ToByte()] = this.recordOffsets[Common.SDATRecordType.Group.ToByte()] + 4;
		this.recordOffsets[Common.SDATRecordType.Stream.ToByte()] = this.recordOffsets[Common.SDATRecordType.Player2.ToByte()] + 4;
		uint offset = this.recordOffsets[Common.SDATRecordType.Stream.ToByte()] + 4;
		this.SEQRecord.FixOffsets(offset);
		offset += this.SEQRecord.SizeOfEntries;
		this.BANKRecord.FixOffsets(offset);
		offset += this.BANKRecord.SizeOfEntries;
		this.WAVEARCRecord.FixOffsets(offset);
		offset += this.WAVEARCRecord.SizeOfEntries;
		this.PLAYERRecord.FixOffsets(offset);
	}

	/// <summary>
	/// Writes the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		// Clear the block we are going to use, so we don't have to clear any later on.
		span[..(int)this.Size].Clear();
		INFOSection.Header.Span.CopyTo(span);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], this.Size);
		this.RecordOffsets.AsBytes().CopyTo(span[0x08..]);
		// Bytes 0x28-0x3F are the Reserved space, and are 0 from our earlier clear.
		this.SEQRecord.WriteHeader(span[0x40..]);
		uint pos = 0x40 + this.SEQRecord.HeaderSize;
		// The SEQARC record is unused and its count is 0 from the earlier clear.
		pos += 0x04;
		this.BANKRecord.WriteHeader(span[(int)pos..]);
		pos += this.BANKRecord.HeaderSize;
		this.WAVEARCRecord.WriteHeader(span[(int)pos..]);
		pos += this.WAVEARCRecord.HeaderSize;
		this.PLAYERRecord.WriteHeader(span[(int)pos..]);
		pos += this.PLAYERRecord.HeaderSize;
		// The GROUP, PLAYER2 and STRM records are unused and their counts are 0 from the earlier clear.
		pos += 0x0C;
		this.SEQRecord.WriteData(span[(int)pos..]);
		pos += this.SEQRecord.SizeOfEntries;
		this.BANKRecord.WriteData(span[(int)pos..]);
		pos += this.BANKRecord.SizeOfEntries;
		this.WAVEARCRecord.WriteData(span[(int)pos..]);
		pos += this.WAVEARCRecord.SizeOfEntries;
		this.PLAYERRecord.WriteData(span[(int)pos..]);
	}

	/// <summary>
	/// Appends two <see cref="INFOSection"/>s to each other.
	/// </summary>
	/// <remarks>
	/// One of the sections can be <see langword="null" /> but not both.
	/// This will not make any changes to the offsets of these records, that is expected to be corrected in a later step.
	/// </remarks>
	/// <param name="infoSection1">The first <see cref="INFOSection" />.</param>
	/// <param name="infoSection2">The second <see cref="INFOSection" />.</param>
	/// <returns>The combined <see cref="INFOSection" />.</returns>
	/// <exception cref="InvalidOperationException">If both INFO sections are <see langword="null" />.</exception>
	public static INFOSection operator +(INFOSection? infoSection1, INFOSection? infoSection2)
	{
		if (infoSection1 is null && infoSection2 is null)
			ThrowHelper.ThrowInvalidOperationException("At least one of the INFO sections to add together must be non-null.");

		infoSection1 ??= new();
		infoSection2 ??= new();

		return new()
		{
			SEQRecord = infoSection1.SEQRecord + infoSection2.SEQRecord,
			BANKRecord = infoSection1.BANKRecord + infoSection2.BANKRecord,
			WAVEARCRecord = infoSection1.WAVEARCRecord + infoSection2.WAVEARCRecord,
			PLAYERRecord = infoSection1.PLAYERRecord + infoSection2.PLAYERRecord
		};
	}
}
