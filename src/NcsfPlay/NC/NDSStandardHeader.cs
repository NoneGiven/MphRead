using System.Buffers.Binary;
using CommunityToolkit.Diagnostics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - Nintendo DS Standard Header.<br />
/// This is a fairly common header for files in NitroFS, the file system used for Nintendo DS ROMs.<br />
/// It is also utilized by the files embedded within an SDAT file.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
public abstract class NDSStandardHeader
{
	readonly Lazy<byte[]> expectedHeader;

	/// <summary>
	/// The 4 byte header of the file.
	/// </summary>
	public ReadOnlySpan<byte> Header => this.expectedHeader.Value.AsSpan();

	/// <summary>
	/// The magic bytes of this particular file.
	/// </summary>
	public abstract uint Magic { get; }

	/// <summary>
	/// The total size of the file.
	/// </summary>
	public abstract uint FileSize { get; }

	/// <summary>
	/// The size of the header for this particular file.
	/// </summary>
	public abstract ushort HeaderSize { get; }

	/// <summary>
	/// The number of blocks contained in the file.
	/// </summary>
	public abstract ushort Blocks { get; }

	/// <summary>
	/// Creates a new instance of <see cref="NDSStandardHeader" />.
	/// </summary>
	protected NDSStandardHeader() => this.expectedHeader = new(this.ExpectedHeader);

	/// <summary>
	/// Gets the expected 4 bytes header of this particular file.
	/// </summary>
	/// <returns>The expected 4 bytes header of this particular file.</returns>
	protected abstract byte[] ExpectedHeader();

	/// <summary>
	/// Reads the NDS Standard Header to verify it matches what it should.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <exception cref="InvalidDataException">
	/// If any part of the header (except file size and number of blocks) doesn't match the expectation.
	/// </exception>
	protected void Read(ReadOnlySpan<byte> span)
	{
		// Not reading file size because this gets calculated on-the-fly.
		// Not reading blocks in, mostly because for SDAT, it could be 3 or 4 and
		// we won't know which it SHOULD be until after we've read the header.
		if (!Common.VerifyHeader(span[..0x04], this.expectedHeader.Value) ||
			BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]) != this.Magic ||
			BinaryPrimitives.ReadUInt16LittleEndian(span[0x0C..]) != this.HeaderSize)
			ThrowHelper.ThrowInvalidDataException($"NDS Standard Header for {this.GetType().Name} invalid");
	}

	/// <summary>
	/// Writes the NDS Standard Header.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public virtual void Write(Span<byte> span)
	{
		this.expectedHeader.Value.AsSpan().CopyTo(span);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], this.Magic);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], this.FileSize);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x0C..], this.HeaderSize);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x0E..], this.Blocks);
	}
}
