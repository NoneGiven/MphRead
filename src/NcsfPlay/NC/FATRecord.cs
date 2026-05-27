using System.Buffers.Binary;
using System.Diagnostics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - FAT (File Allocation Table) Record.<br />
/// These records store an individual file's offset and size.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class FATRecord
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	string DebuggerDisplay => $"FAT Record - Offset: {this.Offset}, Size: {this.Size}";

	/// <summary>
	/// The offset of this file relative to the start of the <see cref="SDAT" />.
	/// </summary>
	public uint Offset { get; set; }

	/// <summary>
	/// The size of this file in the FILE section.
	/// </summary>
	public uint Size { get; set; }

	/// <summary>
	/// The size of each record.
	/// </summary>
	/// <remarks>
	/// Offset (32-bit integer) + Size (32-bit integer) + 2 reserved values (each 32-bit integers, typically stored as 0).
	/// (Should be 16 bytes.)
	/// </remarks>
	public const uint RecordSize = 0x10;

	/// <summary>
	/// Reads the data for this record.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The record itself.</returns>
	public FATRecord Read(ReadOnlySpan<byte> span)
	{
		this.Offset = BinaryPrimitives.ReadUInt32LittleEndian(span);
		this.Size = BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]);
		// Skipping the 2 reserved 32-bit integers.
		return this;
	}

	/// <summary>
	/// Writes the data for this record.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(span, this.Offset);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], this.Size);
		span[0x08..0x10].Clear(); // Writes out the 2 reserved 32-bit integers as 0s.
	}
}
