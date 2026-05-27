using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SBNK (Sound Bank).
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
/// <param name="filename">The optional filename of the <see cref="SBNK" />.</param>
public class SBNK(string? filename = null) : NDSStandardHeader, IEquatable<SBNK>, IEqualityOperators<SBNK, SBNK, bool>
{
	public override uint Magic { get; } = 0x0100FEFF;

	public override uint FileSize => this.Size;

	public override ushort HeaderSize { get; } = 0x10;

	public override ushort Blocks { get; } = 1;

	/// <summary>
	/// The filename copied from the <see cref="INFOEntryBANK" />.
	/// </summary>
	public string? Filename { get; set; } = filename;

	readonly List<SBNKInstrumentEntry> entries = [];

	/// <summary>
	/// The collection of instrument entries located within this file.
	/// </summary>
	public ReadOnlySpan<SBNKInstrumentEntry> Entries => this.entries.AsSpan();

	/// <summary>
	/// The entry number within the <see cref="INFORecord{T}" /> block containing BANK entries.
	/// </summary>
	public int EntryNumber { get; set; } = -1;

	/// <summary>
	/// The associated <see cref="INFOEntryBANK" /> for this bank, if any.
	/// </summary>
	public INFOEntryBANK? Info { get; set; }

	/// <summary>
	/// The full (header + data) size of this file.
	/// </summary>
	/// <remarks>
	/// <see cref="HeaderSize" /> + <see cref="DataSize" /> padded to the next 4 byte boundary.
	/// </remarks>
	public uint Size => this.HeaderSize + (uint)((this.DataSize + 3U) & ~0x03);

	/// <summary>
	/// The data size of this file.
	/// </summary>
	/// <remarks>
	/// "DATA" (4 bytes) + Size (32-bit integer) + 8 reserved values (each 32-bit integers) + Count (32-bit integer) +
	/// size of each <see cref="SBNKInstrumentEntry" />. (Should be at least 44 bytes.)
	/// </remarks>
	public uint DataSize => 0x2C + (uint)this.entries.Sum(static i => i.Size);

	/// <inheritdoc />
	/// <remarks>
	/// "SBNK" as bytes.
	/// </remarks>
	protected override byte[] ExpectedHeader() => [.. "SBNK"u8];

	/// <summary>
	/// Reads the data for the <see cref="SBNK" />.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <param name="failOnMissingFile">
	/// <see langword="true" /> if we should throw the exception if the header doesn't match, <see langword="false" /> otherwise.
	/// </param>
	/// <exception cref="InvalidDataException">
	/// If the header doesn't match when <paramref name="failOnMissingFile" /> is <see langword="true" />.
	/// </exception>
	public void Read(ReadOnlySpan<byte> span, bool failOnMissingFile)
	{
		try
		{
			this.Read(span);
		}
		catch (InvalidDataException)
		{
			if (failOnMissingFile)
				throw;
			else
				return;
		}
		if (!Common.VerifyHeader(span[0x10..0x14], Common.DataBytes.Span))
			ThrowHelper.ThrowInvalidDataException("SBNK DATA structure invalid");
		// Skipping size and the 8 32-bit integers marked as reserved.
		uint count = BinaryPrimitives.ReadUInt32LittleEndian(span[0x38..]);
		CollectionsMarshal.SetCount(this.entries, (int)count);
		// Headers are read first and the instrument data is read afterwards, to make it simpler to apply ranges to the span.
		int pos = 0x3C;
		for (uint i = 0; i < count; ++i)
		{
			this.entries[(int)i] = new SBNKInstrumentEntry().ReadHeader(span[pos..]);
			pos += (int)SBNKInstrumentEntry.HeaderSize;
		}
		foreach (var entry in this.entries)
			entry.ReadInstruments(span[entry.Offset..]);
	}

	/// <summary>
	/// Fixes the offsets of the instrument entries.
	/// </summary>
	public void FixOffsets()
	{
		ushort offset = (ushort)(0x3C + 4 * this.entries.Count);
		foreach (var inst in this.entries)
			offset += inst.FixOffset(offset);
	}

	/// <summary>
	/// Writes the data for the <see cref="SBNK" />.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public override void Write(Span<byte> span)
	{
		// Clear the block we are going to use, so we don't have to clear any later on.
		span[..(int)this.Size].Clear();
		base.Write(span);
		Common.DataBytes.Span.CopyTo(span[0x10..]);
		uint size = this.DataSize;
		uint sizeMulOf4 = (uint)((size + 3) & ~0x03);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], sizeMulOf4);
		// Bytes 0x18-0x37 are the 8 32-bit reserved values and are 0 from the earlier clear.
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x38..], (uint)this.entries.Count);
		// Headers are written first, instrument data is written after that.
		uint pos = 0x3C;
		foreach (var inst in this.entries)
		{
			inst.WriteHeader(span[(int)pos..]);
			pos += SBNKInstrumentEntry.HeaderSize;
		}
		foreach (var inst in this.entries)
		{
			inst.WriteData(span[(int)pos..]);
			pos += inst.DataSize;
		}
		// Because of the earlier clear, we don't need to handle the padding bytes at the end.
	}

	/// <summary>
	/// Replace the entire set of instrument entries for this <see cref="SBNK" />.
	/// </summary>
	/// <param name="instruments">The new instruments.</param>
	public void ReplaceInstruments(ReadOnlySpan<SBNKInstrumentEntry> instruments)
	{
		CollectionsMarshal.SetCount(this.entries, instruments.Length);
		instruments.CopyTo(this.entries.AsSpan());
	}

	#region IEquatable<SBNK>

	public bool Equals(SBNK? other) =>
		other is not null && this.DataSize == other.DataSize && this.entries.AsSpan().SequenceEqual(other.entries.AsSpan());

	public override bool Equals(object? obj) => obj is SBNK sbnk && this.Equals(sbnk);

	public override int GetHashCode() => HashCode.Combine(this.DataSize, this.entries);

	public static bool operator ==(SBNK? left, SBNK? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SBNK? left, SBNK? right) => !(left == right);

	#endregion
}
