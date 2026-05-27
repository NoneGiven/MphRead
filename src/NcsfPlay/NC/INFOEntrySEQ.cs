using System.Buffers.Binary;
using System.Diagnostics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Entry for SEQ Record.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// (Although it was wrong about the File ID being a 16-bit integer, it is actually a 32-bit integer.)
/// </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class INFOEntrySEQ : INFOEntry
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	protected override string DebuggerDisplay =>
		$"INFO Entry (SEQ) - {base.DebuggerDisplay}File ID: {this.FileID}, Bank: {this.Bank}, Volume: {this.Volume}, Channel Priority: {this.ChannelPriority}, Player Priority: {this.PlayerPriority}, Player: {this.Player}";

	/// <summary>
	/// The file ID within the <see cref="FATSection" /> for this entry.
	/// </summary>
	public uint FileID { get; set; }

	/// <summary>
	/// The bank associated with this sequence, the value being the index within the BANK Record in the <see cref="INFOSection" />.
	/// </summary>
	public ushort Bank { get; set; }

	/// <summary>
	/// The volume for this sequence.
	/// </summary>
	public byte Volume { get; set; }

	/// <summary>
	/// The channel priority for this sequence. (Unused.)
	/// </summary>
	public byte ChannelPriority { get; set; }

	/// <summary>
	/// The player priority for this sequence. (Unused.)
	/// </summary>
	public byte PlayerPriority { get; set; }

	/// <summary>
	/// The player associated with this sequence, the value being the index within the PLAYER Record in the <see cref="INFOSection" />.
	/// </summary>
	public byte Player { get; set; }

	/// <summary>
	/// Reserved value, typically 0.
	/// </summary>
	public ushort Reserved { get; set; }

	/// <summary>
	/// The <see cref="NC.SSEQ" /> that this entry corresponds to, for easier access.
	/// </summary>
	public SSEQ? SSEQ { get; set; }

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntrySEQ" />.
	/// </summary>
	public INFOEntrySEQ()
	{
	}

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntrySEQ" /> copied from an existing instance.
	/// </summary>
	/// <param name="other">The instance of <see cref="INFOEntrySEQ" /> to copy from.</param>
	public INFOEntrySEQ(INFOEntrySEQ other) : base(other)
	{
		this.FileID = other.FileID;
		this.Bank = other.Bank;
		this.Volume = other.Volume;
		this.ChannelPriority = other.ChannelPriority;
		this.PlayerPriority = other.PlayerPriority;
		this.Player = other.Player;
		this.Reserved = other.Reserved;
		this.SSEQ = other.SSEQ;
	}

	/// <inheritdoc />
	/// <remarks>
	/// File ID (32-bit integer) + Bank reference (16-bit integer) + Volume (8-bit integer) + Channel Priority (8-bit integer) +
	/// Player Priority (8-bit integer) + Player reference (8-bit integer) + Reserved (16-bit integer). (Should be 12 bytes.)
	/// </remarks>
	public override uint Size => 0x0C;

	public override INFOEntrySEQ Read(ReadOnlySpan<byte> span)
	{
		this.FileID = BinaryPrimitives.ReadUInt32LittleEndian(span);
		this.Bank = BinaryPrimitives.ReadUInt16LittleEndian(span[0x04..]);
		this.Volume = span[0x06];
		this.ChannelPriority = span[0x07];
		this.PlayerPriority = span[0x08];
		this.Player = span[0x09];
		this.Reserved = BinaryPrimitives.ReadUInt16LittleEndian(span[0x0A..]);
		return this;
	}

	public override void Write(Span<byte> span)
	{
		BinaryPrimitives.WriteUInt32LittleEndian(span, this.FileID);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x04..], this.Bank);
		span[0x06] = this.Volume;
		span[0x07] = this.ChannelPriority;
		span[0x08] = this.PlayerPriority;
		span[0x09] = this.Player;
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x0A..], this.Reserved);
	}

	/// <summary>
	/// Checks if the associated <see cref="SSEQ" />s between two entries is the same.
	/// </summary>
	/// <param name="other">The other <see cref="INFOEntrySEQ" /> to check.</param>
	/// <returns><see langword="true" /> if the <see cref="SSEQ" />s are the same, <see langword="false" /> otherwise.</returns>
	public bool FileEquals(INFOEntrySEQ? other) => other is not null && this.SSEQ == other.SSEQ;
}
