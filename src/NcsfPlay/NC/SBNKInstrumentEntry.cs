using System.Buffers.Binary;
using System.Numerics;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SBNK (Sound Bank) Instrument Entry.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
public class SBNKInstrumentEntry : IEquatable<SBNKInstrumentEntry>, IEqualityOperators<SBNKInstrumentEntry, SBNKInstrumentEntry, bool>
{
	/// <summary>
	/// The record type of this entry.
	/// </summary>
	public byte Record { get; set; }

	/// <summary>
	/// The offset where this entry is located relative to the start of the <see cref="SBNK" />.
	/// </summary>
	public ushort Offset { get; set; }

	readonly List<SBNKInstrument> instruments = [];

	/// <summary>
	/// The instruments for this entry.
	/// </summary>
	public ReadOnlySpan<SBNKInstrument> Instruments => this.instruments.AsSpan();

	/// <summary>
	/// The full (header + data) size of this entry.
	/// </summary>
	/// <remarks>
	/// <see cref="HeaderSize" /> + <see cref="DataSize" />.
	/// </remarks>
	public uint Size => SBNKInstrumentEntry.HeaderSize + this.DataSize;

	/// <summary>
	/// The header size of an entry.
	/// </summary>
	/// <remarks>
	/// Record type (8-bit integer) + Offset (16-bit integer) + a reserved value (8-bit integer, should be 0). (Should be 4 bytes.)
	/// </remarks>
	public const uint HeaderSize = 0x04;

	/// <summary>
	/// The data size of this entry.
	/// </summary>
	/// <remarks>
	/// The following correspond to the Record types:
	/// <list type="table">
	/// <listheader>
	/// <term>Record Type</term>
	/// <description>Record Size</description>
	/// </listheader>
	/// <item>
	/// <term>1 through 5</term>
	/// <description>
	/// A single instrument definition. (Should be 10 bytes.)
	/// </description>
	/// </item>
	/// <item>
	/// <term>16</term>
	/// <description>
	/// A range of definitions (also known as a drum set). Including the header and the ranges, there is also a Low Note (8-bit integer) +
	/// High Note (8-bit integer). There should be (High Note - Low Note + 1) definitions in this instrument.
	/// Each definition contains an extra Record type (8-bit integer) and a padding byte (8-bit integer) for the definition.
	/// (Should be 2 + (12 * # of definitions) bytes.)
	/// </description>
	/// </item>
	/// <item>
	/// <term>17</term>
	/// <description>
	/// Regions of definitions (also known as a key split). Including the header and the ranges,
	/// there is also 8 High Notes (all 8-bit integers).
	/// Some of these can be 0.
	/// The number of definitions is equal to the number of High Notes until a 0 High Note is found (or we have done all 8).
	/// Each definition contains an extra Record type (8-bit integer) and a padding byte (8-bit integer) for the definition.
	/// (Should be 8 + (12 * # of definitions) bytes.)
	/// </description>
	/// </item>
	/// <item>
	/// <term>Anything Else (typically 0)</term>
	/// <description>
	/// An empty record. (Should be 0 bytes.)
	/// </description>
	/// </item>
	/// </list>
	/// </remarks>
	public uint DataSize => this.Record switch
	{
		> 0 and < 6 => SBNKInstrument.Size,
		16 => 0x02 + (SBNKInstrument.Size + 0x02) * (uint)this.instruments.Count,
		17 => 0x08 + (SBNKInstrument.Size + 0x02) * (uint)this.instruments.Count,
		_ => 0x00
	};

	/// <summary>
	/// Reads the header data for the entry.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The instrument entry itself.</returns>
	public SBNKInstrumentEntry ReadHeader(ReadOnlySpan<byte> span)
	{
		this.Record = span[0x00];
		this.Offset = BinaryPrimitives.ReadUInt16LittleEndian(span[0x01..]);
		// Skipping the reserved byte. (It is possible that said byte might actually be part of the offset, though.)
		return this;
	}

	/// <summary>
	/// Reads the instruments data for the entry.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	public void ReadInstruments(ReadOnlySpan<byte> span)
	{
		this.instruments.Clear();
		if (this.Record == 16)
		{
			// Drum set record, first 2 bytes contain the range and the rest are instruments.
			byte lowNote = span[0x00];
			byte highNote = span[0x01];
			byte num = (byte)(highNote - lowNote + 1);
			_ = this.instruments.EnsureCapacity(num);
			int pos = 0x02;
			for (byte i = 0; i < num; ++i)
			{
				this.instruments.Add(new SBNKInstrument((byte)(lowNote + i), (byte)(lowNote + i), span[pos]).Read(span[(pos + 2)..]));
				pos += (int)(SBNKInstrument.Size + 2);
			}
		}
		else if (this.Record == 17)
		{
			// Key split record, first 8 bytes contain the high notes and the rest are instruments.
			var thisRanges = span[..0x08];
			byte i = 0;
			_ = this.instruments.EnsureCapacity(8);
			int pos = 0x08;
			while (i < 8 && thisRanges[i] != 0)
			{
				this.instruments.Add(new SBNKInstrument((byte)(i != 0 ? thisRanges[i - 1] + 1 : 0), thisRanges[i], span[pos]).
					Read(span[(pos + 2)..]));
				pos += (int)(SBNKInstrument.Size + 2);
				++i;
			}
		}
		else if (this.Record != 0)
		{
			// Any other non-empty record, contains a single instrument.
			_ = this.instruments.EnsureCapacity(1);
			this.instruments.Add(new SBNKInstrument(0, 127, this.Record).Read(span));
		}
	}

	/// <summary>
	/// Fixes the offsets of the instruments in the entry.
	/// </summary>
	/// <param name="newOffset">The offset that this entry will be at in the parent <see cref="SBNK" />.</param>
	/// <returns>The size of the entry without the header.</returns>
	public ushort FixOffset(ushort newOffset)
	{
		this.Offset = newOffset;
		return (ushort)(this.Size - SBNKInstrumentEntry.HeaderSize);
	}

	/// <summary>
	/// Writes the header data for the entry.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void WriteHeader(Span<byte> span)
	{
		span[0x00] = this.Record;
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x01..], this.Offset);
		span[0x03] = 0;
	}

	/// <summary>
	/// Writes the instruments data for the entry.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void WriteData(Span<byte> span)
	{
		if (this.Record == 16)
		{
			byte lowNote = this.instruments[0].LowNote;
			byte highNote = this.instruments[^1].LowNote;
			byte num = (byte)(highNote - lowNote + 1);
			span[0x00] = lowNote;
			span[0x01] = highNote;
			uint pos = 0x02;
			for (byte i = 0; i < num; ++i)
			{
				var instrument = this.instruments[i];
				span[(int)pos] = instrument.Record;
				span[(int)(pos + 0x01)] = 0;
				instrument.Write(span[(int)(pos + 0x02)..]);
				pos += SBNKInstrument.Size + 0x02;
			}
		}
		else if (this.Record == 17)
		{
			byte actualRanges = (byte)this.instruments.Count;
			byte i;
			for (i = 0; i < actualRanges; ++i)
				span[i] = this.instruments[i].HighNote;
			// This puts 0s in the places where there were no instrument regions, so we always have 8.
			if (actualRanges != 8)
				span[actualRanges..0x08].Clear();
			uint pos = 0x08;
			for (i = 0; i < actualRanges; ++i)
			{
				var instrument = this.instruments[i];
				span[(int)pos] = instrument.Record;
				span[(int)(pos + 0x01)] = 0;
				instrument.Write(span[(int)(pos + 0x02)..]);
				pos += SBNKInstrument.Size + 0x02;
			}
		}
		else if (this.Record != 0)
			this.instruments[0].Write(span);
	}

	#region IEquatable<SBNKInstrumentEntry>

	public bool Equals(SBNKInstrumentEntry? other) => other is not null && this.Record == other.Record && this.Offset == other.Offset &&
		this.instruments.AsSpan().SequenceEqual(other.instruments.AsSpan());

	public override bool Equals(object? obj) => obj is SBNKInstrumentEntry sbnkInstrumentEntry && this.Equals(sbnkInstrumentEntry);

	public override int GetHashCode() => HashCode.Combine(this.Record, this.Offset, this.instruments);

	public static bool operator ==(SBNKInstrumentEntry? left, SBNKInstrumentEntry? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SBNKInstrumentEntry? left, SBNKInstrumentEntry? right) => !(left == right);

	#endregion
}
