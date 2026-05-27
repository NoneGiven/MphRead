using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SWAR (Wave Archive).
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
public class SWAR : NDSStandardHeader, IEquatable<SWAR>, IEqualityOperators<SWAR, SWAR, bool>
{
	public override uint Magic { get; } = 0x0100FEFF;

	public override uint FileSize => this.Size;

	public override ushort HeaderSize { get; } = 0x10;

	public override ushort Blocks { get; } = 1;

	/// <summary>
	/// The filename copied from the <see cref="INFOEntryBANK" />.
	/// </summary>
	public string? Filename { get; set; }

	readonly Dictionary<uint, SWAV> swavs = [];

	/// <summary>
	/// The dictionary of <see cref="SWAV" />s located within this file.
	/// </summary>
	public ReadOnlyDictionary<uint, SWAV> SWAVs { get; }

	public int EntryNumber { get; set; } = -1;

	/// <summary>
	/// The full (header + data) size of this wave archive.
	/// </summary>
	/// <remarks>
	/// <see cref="HeaderSize" /> + <see cref="DataSize" />.
	/// </remarks>
	public uint Size => this.HeaderSize + this.DataSize;

	/// <summary>
	/// The data size of this wave archive.
	/// </summary>
	/// <remarks>
	/// "DATA" (4 bytes) + Size (32-bit integer) + 8 reserved values (all 32-bit integers) + Count (32-bit integer) +
	/// Offsets (each 32-bit integers) + size of each <see cref="SWAV" />. (Should be at least 44 bytes.)
	/// </remarks>
	public uint DataSize => 0x2C + 4 * (uint)this.swavs.Count + (uint)this.swavs.Values.Sum(static swav => swav.Size);

	/// <summary>
	/// Creates a new instance of <see cref="SWAR" />.
	/// </summary>
	/// <param name="filename">The optional filename for the <see cref="SWAR" />.</param>
	public SWAR(string? filename = null)
	{
		this.Filename = filename;
		this.SWAVs = this.swavs.AsReadOnly();
	}

	/// <inheritdoc />
	/// <remarks>
	/// "SWAR" as bytes.
	/// </remarks>
	protected override byte[] ExpectedHeader() => [.. "SWAR"u8];

	/// <summary>
	/// Reads the data for the <see cref="SWAR" />.
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
			ThrowHelper.ThrowInvalidDataException("SWAR DATA structure invalid");
		// Skipping size and the 8 32-bit integers marked as reserved.
		// Count will then be at offset 0x38 of the SWAR.
		uint count = BinaryPrimitives.ReadUInt32LittleEndian(span[0x38..]);
		var offsets = span.Slice(0x3C, (int)(4 * count)).Cast<byte, uint>();
		for (int i = 0; i < count; ++i)
			if (offsets[i] != 0)
				this.swavs[(uint)i] = new SWAV().Read(span[(int)offsets[i]..]);
	}

	/// <summary>
	/// Writes the data for the <see cref="SWAR" />.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public override void Write(Span<byte> span)
	{
		// Clear the block we are going to use, so we don't have to clear any later on.
		span[..(int)this.Size].Clear();
		base.Write(span);
		Common.DataBytes.Span.CopyTo(span[0x10..]);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x14..], this.DataSize);
		// Bytes 0x18-0x37 are the 8 32-bit reserved values and are 0 from the earlier clear.
		uint count = (uint)this.swavs.Count;
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x38..], count);
		uint offset = 0x3C + 4 * count;
		uint pos = 0x3C;
		foreach (var swav in this.swavs.Values)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(span[(int)pos..], offset);
			offset += swav.Size;
			pos += 0x04;
		}
		foreach (var swav in this.swavs.Values)
		{
			swav.Write(span[(int)pos..]);
			pos += swav.Size;
		}
	}

	/// <summary>
	/// Replaces the entire dictionary for the <see cref="SWAR" />.
	/// </summary>
	/// <param name="swavs">The new dictionary.</param>
	public void ReplaceSWAVs(Dictionary<uint, SWAV> swavs)
	{
		this.swavs.Clear();
		foreach (var kvp in swavs)
			this.swavs[kvp.Key] = kvp.Value;
	}

	#region IEquatable<SWAR>

	public bool Equals(SWAR? other)
	{
		if (other is not null && this.DataSize == other.DataSize && this.swavs.Count == other.swavs.Count)
		{
			foreach (var kvp in this.swavs)
				if (!other.swavs.TryGetValue(kvp.Key, out var otherSWAV) || kvp.Value != otherSWAV)
					return false;
			return true;
		}
		return false;
	}

	public override bool Equals(object? obj) => obj is SWAR swar && this.Equals(swar);

	public override int GetHashCode() => HashCode.Combine(this.DataSize, this.swavs);

	public static bool operator ==(SWAR? left, SWAR? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SWAR? left, SWAR? right) => !(left == right);

	#endregion
}
