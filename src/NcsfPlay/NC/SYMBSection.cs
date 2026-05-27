using System.Buffers.Binary;
using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using DotNext;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SYMB (Symbol/Filename) Section.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class SYMBSection
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	string DebuggerDisplay => $"SYMB Section - Record Offsets: {{{string.Join(", ", this.recordOffsets)}}}";

	/// <summary>
	/// "SYMB" as bytes.
	/// </summary>
	static readonly ReadOnlyMemory<byte> Header = "SYMB"u8.ToArray();

	/// <summary>
	/// The size of this section.
	/// </summary>
	/// <remarks>
	/// Header (4 bytes) + Size (32-bit integer) + 8 Record Offsets (each 32-bit integers) + 24 bytes of Reserved +
	/// 4 Count values for the unused Records
	/// (each 32-bit integers, their counts stored as 0s, the unused Records being SEQARC, GROUP, PLAYER2 and STRM) +
	/// size of each actually-used Record (the used Records being SEQ, BANK, WAVEARC and PLAYER). (Will be at least 96 bytes.)
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
	public SYMBRecord SEQRecord { get; private set; } = new();

	/// <summary>
	/// The BANK record block.
	/// </summary>
	public SYMBRecord BANKRecord { get; private set; } = new();

	/// <summary>
	/// The WAVEARC record block.
	/// </summary>
	public SYMBRecord WAVEARCRecord { get; private set; } = new();

	/// <summary>
	/// The PLAYER record block.
	/// </summary>
	public SYMBRecord PLAYERRecord { get; private set; } = new();

	/// <summary>
	/// Reads the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <exception cref="InvalidDataException">If the header is incorrect.</exception>
	public SYMBSection Read(ReadOnlySpan<byte> span)
	{
		if (!Common.VerifyHeader(span[..0x04], SYMBSection.Header.Span))
			ThrowHelper.ThrowInvalidDataException("SDAT SYMB Section invalid");
		// Skipping size as we are just calculating that on the fly.
		// Record offsets start at byte 0x08 of the SYMB section.
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
		return this;
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
		offset += this.SEQRecord.SizeOfNames;
		this.BANKRecord.FixOffsets(offset);
		offset += this.BANKRecord.SizeOfNames;
		this.WAVEARCRecord.FixOffsets(offset);
		offset += this.WAVEARCRecord.SizeOfNames;
		this.PLAYERRecord.FixOffsets(offset);
	}

	/// <summary>
	/// Writes the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		uint size = this.Size;
		uint sizeMulOf4 = (uint)((size + 3) & ~0x03);
		// Clear the block we are going to use, so we don't have to clear any later on.
		span[..(int)sizeMulOf4].Clear();
		SYMBSection.Header.Span.CopyTo(span);
		// Must write the size padded to 4 bytes, even if the SDAT header stores the size without the padding...
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], sizeMulOf4);
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
		pos += this.SEQRecord.SizeOfNames;
		this.BANKRecord.WriteData(span[(int)pos..]);
		pos += this.BANKRecord.SizeOfNames;
		this.WAVEARCRecord.WriteData(span[(int)pos..]);
		pos += this.WAVEARCRecord.SizeOfNames;
		this.PLAYERRecord.WriteData(span[(int)pos..]);
		// Because of the earlier clear, we don't need to handle the padding bytes at the end.
	}

	/// <summary>
	/// Appends two <see cref="SYMBSection" />s to each other.
	/// </summary>
	/// <remarks>
	/// One of the sections can be <see langword="null" /> but not both.
	/// This will not make any changes to the offsets of these records, that is expected to be corrected in a later step.
	/// </remarks>
	/// <param name="symbSection1">The first <see cref="SYMBSection" />.</param>
	/// <param name="symbSection2">The second <see cref="SYMBSection" />.</param>
	/// <returns>The combined <see cref="SYMBSection" />.</returns>
	/// <exception cref="InvalidOperationException">If both INFO sections are <see langword="null" />.</exception>
	public static SYMBSection operator +(SYMBSection? symbSection1, SYMBSection? symbSection2)
	{
		if (symbSection1 is null && symbSection2 is null)
			ThrowHelper.ThrowInvalidOperationException("At least one of the SYMB sections to add together must be non-null.");

		symbSection1 ??= new();
		symbSection2 ??= new();

		return new()
		{
			SEQRecord = symbSection1.SEQRecord + symbSection2.SEQRecord,
			BANKRecord = symbSection1.BANKRecord + symbSection2.BANKRecord,
			WAVEARCRecord = symbSection1.WAVEARCRecord + symbSection2.WAVEARCRecord,
			PLAYERRecord = symbSection1.PLAYERRecord + symbSection2.PLAYERRecord
		};
	}
}
