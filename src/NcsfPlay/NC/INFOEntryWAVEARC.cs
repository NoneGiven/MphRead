using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Entry for WAVEARC Record.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// (Although it was wrong about the File ID being a 16-bit integer, it is actually a 24-bit integer and the other byte is flags.)
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class INFOEntryWAVEARC : INFOEntry, IEquatable<INFOEntryWAVEARC>, IEqualityOperators<INFOEntryWAVEARC, INFOEntryWAVEARC, bool>
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	protected override string DebuggerDisplay => $"INFO Entry (WAVEARC) - {base.DebuggerDisplay}File ID: {this.FileID}";

	/// <summary>
	/// The file ID within the <see cref="FATSection" /> for this entry.
	/// </summary>
	public uint FileID { get; set; }

	/// <summary>
	/// The flags for this entry. (Unused.)
	/// </summary>
	public byte Flags { get; set; }

	/// <summary>
	/// The <see cref="NC.SWAR" /> that this entry corresponds to, for easier access.
	/// </summary>
	public SWAR? SWAR { get; set; }

	/// <inheritdoc />
	/// <remarks>
	/// File ID (24-bit integer) + Flags (8-bit integer). (Should be 4 bytes.)
	/// </remarks>
	public override uint Size => 0x04;

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryWAVEARC" />.
	/// </summary>
	public INFOEntryWAVEARC()
	{
	}

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryWAVEARC" /> copied from an existing instance.
	/// </summary>
	/// <param name="other">The instance of <see cref="INFOEntryWAVEARC" /> to copy from.</param>
	public INFOEntryWAVEARC(INFOEntryWAVEARC other) : base(other)
	{
		this.FileID = other.FileID;
		this.Flags = other.Flags;
		this.SWAR = other.SWAR;
	}

	public override INFOEntryWAVEARC Read(ReadOnlySpan<byte> span)
	{
		this.FileID = BinaryPrimitives.ReadUInt32LittleEndian(span) & 0x00FFFFFF;
		this.Flags = span[0x03];
		return this;
	}

	public override void Write(Span<byte> span) => BinaryPrimitives.WriteUInt32LittleEndian(span, this.FileID | (uint)(this.Flags << 24));

	#region IEquatable<INFOEntryWAVEARC>

	public bool Equals(INFOEntryWAVEARC? other) => other is not null && this.SWAR == other.SWAR;

	public override bool Equals(object? obj) => obj is INFOEntryWAVEARC infoEntryWAVEARC && this.Equals(infoEntryWAVEARC);

	public override int GetHashCode() => this.SWAR?.GetHashCode() ?? 0;

	public static bool operator ==(INFOEntryWAVEARC? left, INFOEntryWAVEARC? right) => left?.Equals(right) ?? false;

	public static bool operator !=(INFOEntryWAVEARC? left, INFOEntryWAVEARC? right) => !(left == right);

	#endregion
}
