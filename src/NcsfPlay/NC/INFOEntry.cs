using System.Diagnostics;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - INFO Entry base class.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// </remarks>
public abstract class INFOEntry
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	protected virtual string DebuggerDisplay => $"Original Filename: {this.OriginalFilename}, SDAT #: {this.SDATNumber}, ";

	/// <summary>
	/// The original filename of the entry from within the <see cref="SYMBSection" />, if any,
	/// otherwise will usually be set to something unique.
	/// </summary>
	public string OriginalFilename { get; set; } = "";

	/// <summary>
	/// The SDAT "number" that this entry came from.
	/// </summary>
	public string SDATNumber { get; set; } = "";

	/// <summary>
	/// The size of this entry.
	/// </summary>
	public abstract uint Size { get; }

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntry" />.
	/// </summary>
	protected INFOEntry()
	{
	}

	/// <summary>
	/// Creates a new instance of <see cref="INFOEntry" /> copied from an existing instance.
	/// </summary>
	/// <param name="other">The instance of <see cref="INFOEntry" /> to copy from.</param>
	protected INFOEntry(INFOEntry other)
	{
		this.OriginalFilename = other.OriginalFilename;
		this.SDATNumber = other.SDATNumber;
	}

	/// <summary>
	/// Reads the data for this entry.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The entry itself.</returns>
	public abstract INFOEntry Read(ReadOnlySpan<byte> span);

	/// <summary>
	/// Writes the data for this entry.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public abstract void Write(Span<byte> span);

	/// <summary>
	/// Gets the full filename of this entry.
	/// </summary>
	/// <param name="multipleSDATs">If <see langword="true" />, the SDAT "number" will prefix the original filename.</param>
	/// <returns>The full filename.</returns>
	public string FullFilename(bool multipleSDATs) => $"{(multipleSDATs ? $"{this.SDATNumber}/" : "")}{this.OriginalFilename}";
}
