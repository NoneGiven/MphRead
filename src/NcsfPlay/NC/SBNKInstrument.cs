using System.Buffers.Binary;
using System.Numerics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SBNK (Sound Bank) Instrument.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document
/// </see>.
/// </para>
/// </remarks>
/// <param name="lowNote">The lowest note value for the instrument.</param>
/// <param name="highNote">The highest note value for the instrument.</param>
/// <param name="record">The record type of the instrument.</param>
public class SBNKInstrument(byte lowNote, byte highNote, byte record) :
	IEquatable<SBNKInstrument>, IEqualityOperators<SBNKInstrument, SBNKInstrument, bool>
{
	/// <summary>
	/// The low note of this instrument.
	/// </summary>
	/// <remarks>
	/// This is not saved when writing this instrument as it is derived from the <see cref="SBNKInstrumentEntry" /> it is saved within.
	/// </remarks>
	public byte LowNote { get; set; } = lowNote;

	/// <summary>
	/// The high note of this instrument.
	/// </summary>
	/// <remarks>
	/// This is not saved when writing this instrument as it is derived from the <see cref="SBNKInstrumentEntry" /> it is saved within.
	/// </remarks>
	public byte HighNote { get; set; } = highNote;

	/// <summary>
	/// The record type of this instrument.
	/// </summary>
	/// <remarks>
	/// This is only saved when writing this instrument if the parent <see cref="SBNKInstrumentEntry" />
	/// has a record type of 16 (drum set) or 17 (key split).
	/// </remarks>
	public byte Record { get; set; } = record;

	/// <summary>
	/// The associated <see cref="NC.SWAV" />,
	/// the value being the index within the <see cref="NC.SWAR" /> that this instrument is associated with.
	/// </summary>
	public ushort SWAV { get; set; }

	/// <summary>
	/// The associated <see cref="NC.SWAR" />,
	/// the value being the index from the <see cref="SBNK" /> that the player is using for the current sequence.
	/// </summary>
	public ushort SWAR { get; set; }

	/// <summary>
	/// The note number of this instrument.
	/// </summary>
	public byte NoteNumber { get; set; }

	/// <summary>
	/// The attack rate of this instrument.
	/// </summary>
	public byte AttackRate { get; set; }

	/// <summary>
	/// The decay rate of this instrument.
	/// </summary>
	public byte DecayRate { get; set; }

	/// <summary>
	/// The sustain level of this instrument.
	/// </summary>
	public byte SustainLevel { get; set; }

	/// <summary>
	/// The release rate of this instrument.
	/// </summary>
	public byte ReleaseRate { get; set; }

	/// <summary>
	/// The pan of this instrument.
	/// </summary>
	public byte Pan { get; set; }

	/// <summary>
	/// The size of each instrument.
	/// </summary>
	/// <remarks>
	/// SWAV reference (16-bit integer) + SWAR reference (16-bit integer) + Note Number (8-bit integer) + Attack Rate (8-bit integer) +
	/// Decay Rate (8-bit integer) + Sustain Level (8-bit integer) + Release Rate (8-bit integer) + Pan (8-bit integer).
	/// (Should be 10 bytes.)
	/// </remarks>
	public const uint Size = 0x0A;

	/// <summary>
	/// Read the data for the instrument.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The instrument itself.</returns>
	public SBNKInstrument Read(ReadOnlySpan<byte> span)
	{
		this.SWAV = BinaryPrimitives.ReadUInt16LittleEndian(span);
		this.SWAR = BinaryPrimitives.ReadUInt16LittleEndian(span[0x02..]);
		this.NoteNumber = span[0x04];
		this.AttackRate = span[0x05];
		this.DecayRate = span[0x06];
		this.SustainLevel = span[0x07];
		this.ReleaseRate = span[0x08];
		this.Pan = span[0x09];
		return this;
	}

	/// <summary>
	/// Writes the data for the instrument.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		BinaryPrimitives.WriteUInt16LittleEndian(span, this.SWAV);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x02..], this.SWAR);
		span[0x04] = this.NoteNumber;
		span[0x05] = this.AttackRate;
		span[0x06] = this.DecayRate;
		span[0x07] = this.SustainLevel;
		span[0x08] = this.ReleaseRate;
		span[0x09] = this.Pan;
	}

	#region IEquatable<SBNKInstrumentRange>

	public bool Equals(SBNKInstrument? other) => other is not null && this.SWAV == other.SWAV && this.SWAR == other.SWAR &&
		this.NoteNumber == other.NoteNumber && this.AttackRate == other.AttackRate && this.DecayRate == other.DecayRate &&
		this.SustainLevel == other.SustainLevel && this.ReleaseRate == other.ReleaseRate && this.Pan == other.Pan;

	public override bool Equals(object? obj) => obj is SBNKInstrument sbnkInstrument && this.Equals(sbnkInstrument);

	public override int GetHashCode() => HashCode.Combine(this.SWAV, this.SWAR, this.NoteNumber, this.AttackRate, this.DecayRate,
		this.SustainLevel, this.ReleaseRate, this.Pan);

	public static bool operator ==(SBNKInstrument? left, SBNKInstrument? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SBNKInstrument? left, SBNKInstrument? right) => !(left == right);

	#endregion
}
