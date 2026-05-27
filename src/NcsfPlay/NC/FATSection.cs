using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - FAT (File Allocation Table) Section.<br />
/// This section stores a collection of <see cref="FATRecord" />s.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class FATSection
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	string DebuggerDisplay => $"FAT Section - # of Records: {this.records.Count}";

	/// <summary>
	/// "FAT " as bytes.
	/// </summary>
	static readonly ReadOnlyMemory<byte> Header = "FAT "u8.ToArray();

	readonly List<FATRecord> records = [];

	/// <summary>
	/// The records contained within this section.
	/// </summary>
	public ReadOnlySpan<FATRecord> Records => this.records.AsSpan();

	/// <summary>
	/// The size of this section.
	/// </summary>
	/// <remarks>
	/// Header (4 bytes) + Size (32-bit integer) + Count (32-bit integer) + the size of each <see cref="FATRecord" />.
	/// (Will be at least 12 bytes.)
	/// </remarks>
	public uint Size => 0x0C + (uint)this.records.Count * FATRecord.RecordSize;

	/// <summary>
	/// Reads the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <exception cref="InvalidDataException">If the header is incorrect.</exception>
	public void Read(ReadOnlySpan<byte> span)
	{
		if (!Common.VerifyHeader(span[..0x04], FATSection.Header.Span))
			ThrowHelper.ThrowInvalidDataException("SDAT FAT Section invalid");
		// Skipping size as we are just calculating that on the fly.
		// Not storing count as the records list can be used for the count.
		uint count = BinaryPrimitives.ReadUInt32LittleEndian(span[0x08..]);
		this.records.Clear();
		_ = this.records.EnsureCapacity((int)count);
		uint pos = 0x0C;
		for (uint i = 0; i < count; ++i)
		{
			this.records.Add(new FATRecord().Read(span[(int)pos..]));
			pos += FATRecord.RecordSize;
		}
	}

	/// <summary>
	/// Writes the data for this section.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		FATSection.Header.Span.CopyTo(span);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], this.Size);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], (uint)this.records.Count);
		uint pos = 0x0C;
		foreach (var record in this.records)
		{
			record.Write(span[(int)pos..]);
			pos += FATRecord.RecordSize;
		}
	}

	/// <summary>
	/// Resizes the records list for this section.
	/// </summary>
	/// <remarks>
	/// WARNING:
	/// If the <paramref name="newSize" /> is less than the current size, all records after <paramref name="newSize" /> will be lost.
	/// </remarks>
	/// <param name="newSize">The new size for the records list.</param>
	public void ResizeRecords(uint newSize)
	{
		int count = this.records.Count;
		CollectionsMarshal.SetCount(this.records, (int)newSize);
		if (newSize > count)
			for (uint i = (uint)count; i < newSize; ++i)
				this.records[(int)i] = new();
	}

	/// <summary>
	/// Sets the number of records for this section.
	/// </summary>
	/// <remarks>
	/// WARNING:
	/// This is a destructive operation as it clears the old records. All new records will be blank.
	/// </remarks>
	/// <param name="count">The number of records.</param>
	public void SetNumberOfRecords(uint count)
	{
		CollectionsMarshal.SetCount(this.records, (int)count);
		for (uint i = 0; i < count; ++i)
			this.records[(int)i] = new();
	}

	/// <summary>
	/// Appends two <see cref="FATSection"/>s to each other.
	/// </summary>
	/// <remarks>
	/// One of the sections can be <see langword="null" /> but not both.
	/// This will not make any changes to the offsets of these records, that is expected to be corrected in a later step.
	/// </remarks>
	/// <param name="fatSection1">The first <see cref="FATSection" />.</param>
	/// <param name="fatSection2">The second <see cref="FATSection" />.</param>
	/// <returns>The combined <see cref="FATSection" />.</returns>
	/// <exception cref="InvalidOperationException">If both FAT sections are <see langword="null" />.</exception>
	public static FATSection operator +(FATSection? fatSection1, FATSection? fatSection2)
	{
		if (fatSection1 is null && fatSection2 is null)
			ThrowHelper.ThrowInvalidOperationException("At least one of the FAT sections to add together must be non-null.");

		fatSection1 ??= new();
		fatSection2 ??= new();

		FATSection newFATSection = new();

		newFATSection.records.AddRange(fatSection1.records);
		newFATSection.records.AddRange(fatSection2.records);

		return newFATSection;
	}
}
