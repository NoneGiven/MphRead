using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using DotNext;

namespace NCSFCommon;

public static class Common
{
	public static class ListMemory<T>
	{
		// NOTE: This is currently valid with .NET 7, but is A) unsafe and 2) not guaranteed to continue to work in future .NET versions.
		// Chances are, though, it will probably continue to work, as both _items and _size are listed as part of binary serialization and
		// to not be renamed.
		static readonly Lazy<Func<List<T>, Memory<T>>> asMemory = new(static () =>
		{
			var listType = typeof(List<T>);
			var listExp = Expression.Parameter(listType, "list");
			return Expression.Lambda<Func<List<T>, Memory<T>>>(
				Expression.New(
					typeof(Memory<T>).GetConstructor([typeof(T[]), typeof(int), typeof(int)])!,
					Expression.Field(listExp, listType.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!),
					Expression.Constant(0),
					Expression.Field(listExp, listType.GetField("_size", BindingFlags.NonPublic | BindingFlags.Instance)!)
				),
				listExp
			).Compile();
		});

		public static Func<List<T>, Memory<T>> AsMemory => ListMemory<T>.asMemory.Value;
	}

	/// <summary>
	/// Get a <see cref="Memory{T}" /> view over a <see cref="List{T}" />'s data.
	/// Items should not be added or removed from the <see cref="List{T}" /> while the <see cref="Memory{T}" /> is in use.
	/// </summary>
	/// <remarks>
	/// This method is based entirely off of the source code for <see cref="System.Runtime.InteropServices.CollectionsMarshal.AsSpan" />,
	/// but to create a <see cref="Memory{T}" /> instead. It is valid with at least .NET 7, but it uses reflection to obtain internal
	/// fields on <see cref="List{T}" /> and is not guaranteed to continue to work in future .NET versions.
	/// </remarks>
	/// <param name="list">The list to get the data view over.</param>
	public static Memory<T> AsMemory<T>(this List<T> list) => ListMemory<T>.AsMemory(list);

	/// <summary>
	/// Replacement for removed ToByte method from DotNext's EnumConverter, converts an enum type to a <see langword="byte" />.
	/// </summary>
	/// <typeparam name="T">The enum type to convert from.</typeparam>
	/// <param name="enum">The enum to convert from.</param>
	/// <returns>The enum as a <see langword="byte" />.</returns>
	public static byte ToByte<T>(this T @enum) where T : struct, Enum => EnumConverter.FromEnum<T, byte>(@enum);

	/// <summary>
	/// Replacement for removed ToEnum method from DotNext's EnumConverter, converts a <see langword="byte" /> byte to an enum.
	/// </summary>
	/// <typeparam name="T">The enum type to convert to.</typeparam>
	/// <param name="num">The <see langword="byte" /> to convert to an enum.</param>
	/// <returns>The <see langword="byte" /> as an enum.</returns>
	public static T ToEnum<T>(this byte num) where T : struct, Enum => EnumConverter.ToEnum<T, byte>(num);

	/// <summary>
	/// "DATA" as bytes.
	/// </summary>
	public static readonly ReadOnlyMemory<byte> DataBytes = "DATA"u8.ToArray();

	/// <summary>
	/// Reads a null-terminated string (with the characters being single bytes) from the span.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The string that was read.</returns>
	public static string ReadNullTerminatedString(this ReadOnlySpan<byte> span)
	{
		List<char> chars = [];
		char chr;
		int pos = 0;
		do
		{
			chr = (char)span[pos++];
			if (chr != 0)
				chars.Add(chr);
		} while (chr != 0);
		return new(chars.AsSpan());
	}

	/// <summary>
	/// Writes a null-terminated string (with the characters being single bytes) to the span.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	/// <param name="str">The string to write.</param>
	public static void WriteNullTerminatedString(this Span<byte> span, string str)
	{
		int pos = 0;
		foreach (char chr in str)
			span[pos++] = (byte)chr;
		span[pos] = 0;
	}

	/// <summary>
	/// SDAT Record Types.
	/// </summary>
	/// <remarks>
	/// List of types taken from the Nitro Composer Specification at
	/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">http://www.feshrine.net/hacking/doc/nds-sdat.html</see>
	/// </remarks>
	public enum SDATRecordType : byte
	{
		/// <summary>
		/// SEQ Record.
		/// </summary>
		Sequence,
		/// <summary>
		/// SEQARC Record.
		/// </summary>
		SequenceArchive,
		/// <summary>
		/// BANK Record.
		/// </summary>
		Bank,
		/// <summary>
		/// WAVARC Record.
		/// </summary>
		WaveArchive,
		/// <summary>
		/// PLAYER Record.
		/// </summary>
		Player,
		/// <summary>
		/// GROUP Record.
		/// </summary>
		Group,
		/// <summary>
		/// PLAYER2 Record.
		/// </summary>
		Player2,
		/// <summary>
		/// STRM Record.
		/// </summary>
		Stream
	}

	/// <summary>
	/// Verifies that the header that was read matches the expected header.
	/// </summary>
	/// <param name="actual">The actual header.</param>
	/// <param name="expected">The expected header.</param>
	/// <returns><see langword="true" /> if the headers match, <see langword="false" /> otherwise.</returns>
	public static bool VerifyHeader(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected) => actual.SequenceEqual(expected);

	/// <summary>
	/// Converts the given wildcard string (which contains "*" for matching any number of characters and "?" for matching a single
	/// character) into a <see cref="Regex" />.
	/// </summary>
	/// <param name="wildcard">The wildcard to convert.</param>
	/// <returns>The <see cref="Regex" /> of the wildcard.</returns>
	public static Regex WildcardStringToRegex(string wildcard) =>
		new($@"^{wildcard.Replace("?", ".").Replace("*", ".*")}$", RegexOptions.Compiled);

	/// <summary>
	/// How a file should be kept.
	/// </summary>
	public enum KeepType : byte
	{
		/// <summary>
		/// Signifies that the file will be excluded.
		/// </summary>
		Exclude,
		/// <summary>
		/// Signifies that the file will be included.
		/// </summary>
		Include,
		/// <summary>
		/// Signified that the file will neither be excluded or included (basically neither choice has been made).
		/// </summary>
		Neither
	}

	/// <summary>
	/// Stores if a filename should either be included, excluded or neither.
	/// </summary>
	/// <param name="Filename">The filename of this info.</param>
	/// <param name="Keep">The keep status for the file of this info.</param>
	public record class KeepInfo(string Filename, KeepType Keep);

	/// <summary>
	/// Determine if a filename should be included, excluded or neither.
	/// </summary>
	/// <param name="filename">The filename to check against.</param>
	/// <param name="sdatNumber">The optional SDAT number to check against.</param>
	/// <param name="includesAndExcludes">A list of previously done includes and excludes.</param>
	/// <returns>A <see cref="KeepType" /> value for if <paramref name="filename" /> should be included, excluded or neither.</returns>
	public static KeepType IncludeFilename(string filename, string sdatNumber, List<KeepInfo> includesAndExcludes)
	{
		var keep = KeepType.Neither;
		foreach (var info in includesAndExcludes)
		{
			string[] parts = info.Filename.Split('/');
			if (parts.Length > 2)
				ThrowHelper.ThrowArgumentException("Must have either no slash or only a single slash.");
			else if (parts.Length == 2)
			{
				if (Common.WildcardStringToRegex(parts[0]).IsMatch(sdatNumber) && Common.WildcardStringToRegex(parts[1]).IsMatch(filename))
					keep = info.Keep;
			}
			else if (Common.WildcardStringToRegex(info.Filename).IsMatch(filename))
				keep = info.Keep;
		}
		return keep;
	}

	/// <summary>
	/// Converts seconds into a string of minutes:seconds.
	/// </summary>
	/// <param name="seconds"></param>
	/// <returns></returns>
	public static string SecondsToString(float seconds)
	{
		int minutes = (int)(seconds / 60);
		seconds -= minutes * 60;

		return $"{minutes:D2}:{seconds:0#.####}";
	}

	public static int StringToMS(ReadOnlySpan<char> time)
	{
		int colons = System.MemoryExtensions.Count(time, ':');
		if (colons > 2)
			return ThrowHelper.ThrowFormatException<int>("Time cannot have more than 2 colons.");
		Span<Range> ranges = stackalloc Range[colons + 1];
		_ = time.Split(ranges, ':');
		return colons switch
		{
			1 => int.Parse(time[ranges[0]]) * 60 + int.Parse(time[ranges[1]]),
			2 => int.Parse(time[ranges[0]]) * 3600 + int.Parse(time[ranges[1]]) * 60 + int.Parse(time[ranges[2]]),
			_ => int.Parse(time)
		} * 1000;
	}

	/// <summary>
	/// Get the length that <paramref name="value" /> would be when converted to a variable-length value.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <returns>The length that <paramref name="value" /> would be when converted to a variable-length value.</returns>
	public static int VLVLength(this int value) => value switch
	{
		>= 0x1000_0000 => 5,
		>= 0x0020_0000 => 4,
		>= 0x0000_4000 => 3,
		>= 0x0000_0080 => 2,
		_ => 1
	};
}
