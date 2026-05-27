using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Entry for PLAYER Record.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Originally used data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// (Although it was completely wrong about what this entry contained.)
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class INFOEntryPLAYER : INFOEntry, IEquatable<INFOEntryPLAYER>, IEqualityOperators<INFOEntryPLAYER, INFOEntryPLAYER, bool>
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	protected override string DebuggerDisplay =>
		$"INFO Entry (PLAYER) - {base.DebuggerDisplay}Max Sequences: {this.MaxSequences}, Channel Mask: 0x{this.ChannelMask:X4}, HeapSize: {this.HeapSize}";

	/// <summary>
	/// The maximum number of sequences that can be played at any given time for this player. (Unused.)
	/// </summary>
	public byte MaxSequences { get; set; }

	/// <summary>
	/// Padding byte, probably 0.
	/// </summary>
	public byte Padding { get; set; }

	/// <summary>
	/// The 16-bit channel mask signifying which channels are unused (1 bits are used channels, 0 bits are unused channels).
	/// </summary>
	/// <remarks>
	/// Both because of legacy purposes and because some games are like this, a channel mask of 0x0000 will be treated as 0xFFFF instead.
	/// </remarks>
	public ushort ChannelMask { get; set; }

	/// <summary>
	/// The heap size of this player. (Unused.)
	/// </summary>
	public uint HeapSize { get; set; }

	/// <inheritdoc />
	/// <remarks>
	/// Max Sequences (8-bit integer) + Padding (8-bit integer) + Channel Max (16-bit integer) + Heap Size (32-bit integer).
	/// (Should be 8 bytes.)
	/// </remarks>
	public override uint Size => 0x08;

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryPLAYER" />.
	/// </summary>
	public INFOEntryPLAYER()
	{
	}

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntryPLAYER" /> copied from an existing instance.
	/// </summary>
	/// <param name="other">The instance of <see cref="INFOEntryPLAYER" /> to copy from.</param>
	public INFOEntryPLAYER(INFOEntryPLAYER other) : base(other)
	{
		this.MaxSequences = other.MaxSequences;
		this.Padding = other.Padding;
		this.ChannelMask = other.ChannelMask;
		this.HeapSize = other.HeapSize;
	}

	public override INFOEntryPLAYER Read(ReadOnlySpan<byte> span)
	{
		this.MaxSequences = span[0x00];
		this.Padding = span[0x01];
		this.ChannelMask = BinaryPrimitives.ReadUInt16LittleEndian(span[0x02..]);
		this.HeapSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]);
		return this;
	}

	public override void Write(Span<byte> span)
	{
		span[0x00] = this.MaxSequences;
		span[0x01] = this.Padding;
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x02..], this.ChannelMask);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], this.HeapSize);
	}

	#region IEquatable<INFOEntryPLAYER>

	public bool Equals(INFOEntryPLAYER? other) => other is not null && this.MaxSequences == other.MaxSequences &&
		this.Padding == other.Padding && this.ChannelMask == other.ChannelMask && this.HeapSize == other.HeapSize;

	public override bool Equals(object? obj) => obj is INFOEntryPLAYER infoEntryPLAYER && this.Equals(infoEntryPLAYER);

	public override int GetHashCode() => HashCode.Combine(this.MaxSequences, this.Padding, this.ChannelMask, this.HeapSize);

	public static bool operator ==(INFOEntryPLAYER? left, INFOEntryPLAYER? right) => left?.Equals(right) ?? false;

	public static bool operator !=(INFOEntryPLAYER? left, INFOEntryPLAYER? right) => !(left == right);

	#endregion
}
