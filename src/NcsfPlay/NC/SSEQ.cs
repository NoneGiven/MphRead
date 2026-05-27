using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SSEQ (Sequence).
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
/// <param name="filename">The optional filename for the <see cref="SSEQ" />, prefixed with a position number.</param>
/// <param name="originalFilename">The optional original filename for the <see cref="SSEQ" />.</param>
public class SSEQ(string? filename = null, string? originalFilename = null) :
	NDSStandardHeader, IEquatable<SSEQ>, IEqualityOperators<SSEQ, SSEQ, bool>
{
	public override uint Magic { get; } = 0x0100FEFF;

	public override uint FileSize => this.Size;

	public override ushort HeaderSize { get; } = 0x10;

	public override ushort Blocks { get; } = 1;

	/// <summary>
	/// The filename copied from the <see cref="INFOEntryBANK" /> but prefixed with a position number as well.
	/// </summary>
	public string? Filename { get; set; } = filename;

	/// <summary>
	/// The filename copied from the <see cref="INFOEntryBANK" />.
	/// </summary>
	public string? OriginalFilename { get; set; } = originalFilename;

	readonly List<byte> data = [];

	/// <summary>
	/// This sequence's data.
	/// </summary>
	public ReadOnlyMemory<byte> Data => this.data.AsMemory();

	/// <summary>
	/// The entry number within the <see cref="INFORecord{T}" /> block containing SEQ entries.
	/// </summary>
	public int EntryNumber { get; set; } = -1;

	/// <summary>
	/// The associated <see cref="INFOEntrySEQ" /> for this sequence, if any.
	/// </summary>
	public INFOEntrySEQ? Info { get; set; }

	/// <summary>
	/// The full (header + data) size of this sequence.
	/// </summary>
	/// <remarks>
	/// <see cref="HeaderSize" /> + <see cref="DataSize" />.
	/// </remarks>
	public uint Size => this.HeaderSize + this.DataSize;

	/// <summary>
	/// The data size of this sequence.
	/// </summary>
	/// <remarks>
	/// "DATA" (4 bytes) + Size (32-bit integer) + Offset (32-bit integer) + number of bytes in <see cref="Data" />.
	/// (Should be at least 12 bytes.)
	/// </remarks>
	public uint DataSize => 0x0C + (uint)this.data.Count;

	/// <inheritdoc />
	/// <remarks>
	/// "SSEQ" as bytes.
	/// </remarks>
	protected override byte[] ExpectedHeader() => [.. "SSEQ"u8];

	/// <summary>
	/// Reads the data for the <see cref="SSEQ" />.
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
			ThrowHelper.ThrowInvalidDataException("SSEQ DATA structure invalid");
		uint size = BinaryPrimitives.ReadUInt32LittleEndian(span[0x14..]);
		// 0x0C is subtracted from size as the size includes the DATA header, the size itself and the data offset.
		CollectionsMarshal.SetCount(this.data, (int)(size - 0x0C));
		// The ReadUInt32LittleEndian call gets the data offset.
		span.Slice((int)BinaryPrimitives.ReadUInt32LittleEndian(span[0x18..]), (int)(size - 0x0C)).CopyTo(this.data.AsSpan());
	}

	/// <summary>
	/// Writes the data for the <see cref="SSEQ" />.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public override void Write(Span<byte> span)
	{
		base.Write(span);
		Common.DataBytes.Span.CopyTo(span[0x10..]);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], this.DataSize);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x18..], 0x1C); // Data offset, always 0x1C into the SSEQ.
		this.data.AsSpan().CopyTo(span[0x1C..]);
	}

	/// <summary>
	/// Replaces the entire data for the <see cref="SSEQ" />.
	/// </summary>
	/// <param name="newData">The new data.</param>
	public void ReplaceData(IEnumerable<byte> newData)
	{
		this.data.Clear();
		if (!newData.TryGetNonEnumeratedCount(out int count))
			count = newData.Count();
		_ = this.data.EnsureCapacity(count);
		this.data.AddRange(newData);
	}

	#region IEquatable<SSEQ>

	public bool Equals(SSEQ? other) =>
		other is not null && this.DataSize == other.DataSize && this.data.Count == other.data.Count && this.data.SequenceEqual(other.data);

	public override bool Equals(object? obj) => obj is SSEQ sseq && this.Equals(sseq);

	public override int GetHashCode() => HashCode.Combine(this.DataSize, this.data);

	public static bool operator ==(SSEQ? left, SSEQ? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SSEQ? left, SSEQ? right) => !(left == right);

	#endregion
}
