using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SYMB (Symbol/Filename) Record.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class SYMBRecord
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	string DebuggerDisplay => $"SYMB Record - # of Entries: {this.entries.Count}";

	readonly List<(uint Offset, string? Name)> entries = [];

	/// <summary>
	/// The entries contained within this record.
	/// </summary>
	public ReadOnlySpan<(uint Offset, string? Name)> Entries => this.entries.AsSpan();

	/// <summary>
	/// The full (header + names) size of this record.
	/// </summary>
	/// <remarks>
	/// <see cref="HeaderSize" /> + <see cref="SizeOfNames" />.
	/// </remarks>
	public uint Size => this.HeaderSize + this.SizeOfNames;

	/// <summary>
	/// The size of the header of this record. (Will be at least 4 bytes.)
	/// </summary>
	public uint HeaderSize => 0x04 + 4 * (uint)this.entries.Count;

	/// <summary>
	/// The size of just the names of this record.
	/// </summary>
	public uint SizeOfNames => (uint)this.entries.Sum(static e => string.IsNullOrEmpty(e.Name) ? 0 : e.Name.Length + 1);

	/// <summary>
	/// Reads the data for this record.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <param name="offset">
	/// The offset from the parent <see cref="SYMBSection" />, needed because <paramref name="span" /> is passed in from the start of the
	/// record instead.
	/// </param>
	public void Read(ReadOnlySpan<byte> span, uint offset)
	{
		uint count = BinaryPrimitives.ReadUInt32LittleEndian(span); // Not storing count as the entries list can be used for the count.
		var entryOffsets = span.Slice(0x04, (int)(4 * count)).Cast<byte, uint>();
		this.entries.Clear();
		_ = this.entries.EnsureCapacity((int)count);
		foreach (uint entryOffset in entryOffsets)
		{
			string? name = null;
			if (entryOffset != 0)
				name = span[(int)(entryOffset - offset)..].ReadNullTerminatedString();
			this.entries.Add((entryOffset, name));
		}
	}

	/// <summary>
	/// Fixes the offsets to the entries of this record.
	/// </summary>
	/// <param name="startOffset">
	/// The offset from the parent <see cref="SYMBSection" /> where the entries of this record should start from.
	/// </param>
	public void FixOffsets(uint startOffset)
	{
		uint offset = startOffset;
		foreach (ref var entry in this.entries.AsSpan())
		{
			entry.Offset = offset;
			offset += string.IsNullOrEmpty(entry.Name) ? 0U : (uint)entry.Name.Length + 1U;
		}
	}

	/// <summary>
	/// Writes the header of this record, which consists of just the offsets.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void WriteHeader(Span<byte> span)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)this.entries.Count);
		int pos = 0x04;
		foreach (var (Offset, _) in this.entries)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], Offset);
			pos += 0x04;
		}
	}

	/// <summary>
	/// Writers the data of this record, which consists of just the entries.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void WriteData(Span<byte> span)
	{
		int pos = 0x00;
		foreach (var (_, Name) in this.entries)
			if (!string.IsNullOrEmpty(Name))
			{
				span[pos..].WriteNullTerminatedString(Name);
				pos += Name.Length + 1;
			}
	}

	/// <summary>
	/// Appends two <see cref="SYMBRecord" />s to each other.
	/// </summary>
	/// <remarks>
	/// This will not make any changes to the offsets of these records, that is expected to be corrected in a later step.
	/// </remarks>
	/// <param name="symbRecord1">The first <see cref="SYMBRecord" />.</param>
	/// <param name="symbRecord2">The second <see cref="SYMBRecord" />.</param>
	/// <returns>The combined <see cref="SYMBRecord" />.</returns>
	public static SYMBRecord operator +(SYMBRecord symbRecord1, SYMBRecord symbRecord2)
	{
		SYMBRecord newSYMBRecord = new();

		newSYMBRecord.entries.AddRange(symbRecord1.entries);
		newSYMBRecord.entries.AddRange(symbRecord2.entries);

		return newSYMBRecord;
	}

	/// <summary>
	/// Sets the number of entries for this record.
	/// </summary>
	/// <remarks>
	/// WARNING: This is a destructive operation as it clears the old entries. All entries will be blank (0 offset and null entry).
	/// </remarks>
	/// <param name="count">The number of entries.</param>
	public void SetNumberOfEntries(uint count)
	{
		CollectionsMarshal.SetCount(this.entries, (int)count);
		this.entries.AsSpan().Clear();
	}

	/// <summary>
	/// Expands the number of entries for this record. Will do nothing if the given count is not greater than the current count.
	/// </summary>
	/// <param name="count">The count of entries to expand to.</param>
	public void ExpandNumberOfEntries(uint count)
	{
		if (count > this.entries.Count)
			CollectionsMarshal.SetCount(this.entries, (int)count);
	}

	/// <summary>
	/// Sets an entry to the given value.
	/// </summary>
	/// <param name="i">The index of the entry to set.</param>
	/// <param name="entry">The value of the entry to set.</param>
	/// <exception cref="ArgumentOutOfRangeException">If the given index is out of range.</exception>
	public void SetEntry(uint i, (uint Offset, string? Entry) entry)
	{
		Guard.IsInRange(i, 0, this.entries.Count);

		this.entries[(int)i] = entry;
	}
}
