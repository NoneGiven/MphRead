using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace NCSFCommon;

/// <summary>
/// Methods for working with NCSF files.
/// </summary>
public static class NCSF
{
	/// <summary>
	/// "PSF" as bytes.
	/// </summary>
	static readonly ReadOnlyMemory<byte> PSFHeader = "PSF"u8.ToArray();

	/// <summary>
	/// "[TAG]" as bytes.
	/// </summary>
	static readonly ReadOnlyMemory<byte> TAGHeader = "[TAG]"u8.ToArray();

	/// <summary>
	/// Creates an NCSF file.
	/// </summary>
	/// <param name="filename">The filename to save the NCSF to.</param>
	/// <param name="reservedSectionData">The bytes of the reserved section of the PSF file.</param>
	/// <param name="programSectionData">
	/// The uncompressed bytes of the program section of the PSF file. (This will be zlib compressed by this method.)
	/// </param>
	/// <param name="tags">The tags for the NCSF, if any.</param>
	public static void MakeNCSF(string filename, ReadOnlySpan<byte> reservedSectionData, ReadOnlySpan<byte> programSectionData,
		TagList? tags = null)
	{
		// Create zlib compressed version of program section, if one was given.
		ReadOnlySpan<byte> programCompressedData = [];
		if (programSectionData.Length != 0)
		{
			using MemoryStream ms = new();
			using (ZLibStream zls = new(ms, CompressionLevel.SmallestSize, true))
				zls.Write(programSectionData);
			programCompressedData = ms.ToArray();
		}

		// Create file.
		using FileStream file = new(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
		using BinaryWriter bw = new(file);

		bw.Write(NCSF.PSFHeader.Span);
		bw.Write((byte)0x25);
		bw.Write(reservedSectionData.Length);
		bw.Write(programCompressedData.Length);
		if (programCompressedData.Length != 0)
			bw.Write(Crc32.HashToUInt32(programCompressedData));
		else
			bw.Write(0U);
		bw.Write(reservedSectionData);
		bw.Write(programCompressedData);
		if ((tags?.Count ?? 0) != 0)
		{
			bw.Write(NCSF.TAGHeader.Span);
			Span<Range> ranges = stackalloc Range[tags!.Max(static t => System.MemoryExtensions.Count(t.Value, '\n')) + 1];
			foreach (var (Name, Value) in tags!)
			{
				var valueSpan = Value.AsSpan();
				int numRanges = valueSpan.Split(ranges, '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				foreach (var range in ranges[..numRanges])
				{
					bw.Write(Encoding.UTF8.GetBytes($"{Name}={valueSpan[range]}"));
					bw.Write((byte)0x0A);
				}
			}
		}
	}

	/// <summary>
	/// Checks if the given file is a valid PSF, throwing an <see cref="InvalidDataException" /> if not.
	/// </summary>
	/// <param name="span">A <see cref="ReadOnlySpan{T}" /> to the file to check.</param>
	/// <param name="versionByte">The PSF version byte to check for.</param>
	/// <exception cref="InvalidDataException">If the file is not a valid PSF.</exception>
	public static void CheckForValidPSF(ReadOnlySpan<byte> span, byte versionByte)
	{
		// Various checks on the file's size will be done throughout.
		int fileSize = span.Length;
		if (fileSize < 0x04)
			ThrowHelper.ThrowInvalidDataException("File is too small.");

		// Verify it actually is a PSF file, as well as having the NCSF version byte.
		if (!span[..0x03].SequenceEqual(NCSF.PSFHeader.Span))
			ThrowHelper.ThrowInvalidDataException("Not a PSF file.");

		if (span[0x03] != versionByte)
			ThrowHelper.ThrowInvalidDataException($"Version byte of 0x{span[0x03]:X2} does not equal what we were looking for (0x{versionByte:X2}).");

		if (fileSize < 0x10)
			ThrowHelper.ThrowInvalidDataException("File is too small.");

		// Get the sizes on the reserved and program sections.
		uint reservedSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]);
		uint programCompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x08..]);

		// Check the reserved section.
		if (reservedSize != 0 && fileSize < reservedSize + 0x10)
			ThrowHelper.ThrowInvalidDataException("File is too small.");

		// Check the program section.
		if (programCompressedSize != 0 && fileSize < reservedSize + programCompressedSize + 0x10)
			ThrowHelper.ThrowInvalidDataException("File is too small.");
	}

	/// <summary>
	/// Extracts the program section from a PSF.
	/// </summary>
	/// <param name="span">A <see cref="ReadOnlySpan{T}" /> of the data to extract from.</param>
	/// <param name="versionByte">The PSF version byte to check for.</param>
	/// <param name="programHeaderSize">
	/// The number of bytes to be read from the compressed program section as a header for the program section.
	/// </param>
	/// <param name="programSectionOffset">The offset within the program section's header where the uncompressed size is stored.</param>
	/// <param name="addHeaderSize">
	/// If <see langword="true" />, the <paramref name="programHeaderSize"/> will be added to the size found in the header.
	/// </param>
	/// <returns>The uncompressed program section of the given PSF.</returns>
	public static byte[] GetProgramSectionFromPSF(ReadOnlySpan<byte> span, byte versionByte, uint programHeaderSize,
		uint programSectionOffset, bool addHeaderSize = false)
	{
		// Check to make sure the file is valid.
		NCSF.CheckForValidPSF(span, versionByte);

		// Get the sizes on the reserved and program sections.
		uint reservedSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x04..]);
		uint programCompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(span[0x08..]);

		// We need a program section to continue.
		if (programCompressedSize == 0)
			return [];

		// Uncompress the program section. The reason to use ZlibStream twice:
		// first usage is to get just the first set of bytes that tell the size of
		// the entire uncompressed stream, second usage gets everything.
		byte[] bytes;
		using (var memoryOwner = MemoryOwner<byte>.Allocate((int)programCompressedSize))
		{
			Span<byte> initialBytes = stackalloc byte[(int)programHeaderSize];
			span.Slice((int)(reservedSize + 0x10), (int)programCompressedSize).CopyTo(memoryOwner.Span);
			using (ZLibStream zls = new(memoryOwner.Memory.AsStream(), CompressionMode.Decompress))
				_ = zls.Read(initialBytes);
			uint size = BinaryPrimitives.ReadUInt32LittleEndian(initialBytes[(int)programSectionOffset..]) +
				(addHeaderSize ? programHeaderSize : 0);
			bytes = new byte[size];
			using (ZLibStream zls = new(memoryOwner.Memory.AsStream(), CompressionMode.Decompress))
				zls.ReadExactly(bytes);
		}

		return bytes;
	}

	/// <summary>
	/// Enumerates all the offsets of the search pattern within the data.
	/// </summary>
	/// <remarks>
	/// Algorithm is derived from
	/// <see href="http://www-igm.univ-mlv.fr/~lecroq/string/bndm.html">Backward Nondeterministic Dawg Matching</see>.
	/// </remarks>
	/// <param name="search">The search pattern.</param>
	/// <param name="memory">The <see cref="ReadOnlyMemory{T}" /> to search within.</param>
	/// <returns>A byte position within the data where the search pattern was found.</returns>
	public static IEnumerable<int> FindOffsetsInFile(ReadOnlyMemory<byte> search, ReadOnlyMemory<byte> memory)
	{
		int m = search.Length;
		int n = memory.Length;
		int[] B = new int[256];
		int s = 1;
		var searchSpan = search.Span;
		for (int i = m - 1; i >= 0; --i)
		{
			B[searchSpan[i]] |= s;
			s <<= 1;
		}
		int j = 0;
		while (j <= n - m)
		{
			int i = m - 1;
			int last = m;
			int d = ~0;
			while (i >= 0 && d != 0)
			{
				d &= B[memory.Span[j + i]];
				--i;
				if (d != 0)
				{
					if (i >= 0)
						last = i + 1;
					else
						yield return j;
				}
				d <<= 1;
			}
			j += last;
		}
	}

	/// <summary>
	/// Gets the tags from the end of a PSF that contains tags, with the tags being interpreted as a given encoding.
	/// </summary>
	/// <param name="span">A <see cref="ReadOnlySpan{T}"/> to the data to extract the tags from.</param>
	/// <param name="encoding">The <see cref="Encoding" /> to interpret the tags in.</param>
	/// <returns>The list of tags contained within the given PSF, interpreted as the given encoding.</returns>
	static TagList GetTagsFromPSFWithEncoding(ReadOnlySpan<byte> span, Encoding encoding)
	{
		TagList tags = [];

		Span<char> rawTags = stackalloc char[encoding.GetCharCount(span)];
		_ = encoding.GetChars(span, rawTags);
		Span<Range> tagPairRanges = stackalloc Range[System.MemoryExtensions.Count(rawTags, '\n') + 1];
		int numTags = ((ReadOnlySpan<char>)rawTags).Split(tagPairRanges, '\n',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		Span<Range> tagRanges = stackalloc Range[2];
		foreach (var range in tagPairRanges[..numTags])
		{
			var tag = rawTags[range];
			int pieces =
				((ReadOnlySpan<char>)tag).Split(tagRanges, '=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (pieces == 2)
			{
				string nameStr = $"{tag[tagRanges[0]]}";
				string valueStr = $"{tag[tagRanges[1]]}";
				tags.AddOrReplace((nameStr,
					tags.TryGetValue(nameStr, out var existingTag) ? $"{existingTag.Value}\n{valueStr}" : valueStr));
			}
		}

		return tags;
	}

	static readonly Encoding SystemCodePageEncoding =
		CodePagesEncodingProvider.Instance.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage)!;

	/// <summary>
	/// Gets the tags from the end of a PSF that contains tags.
	/// </summary>
	/// <remarks>
	/// The only reason this takes in a <see cref="ReadOnlyMemory{T}" /> instead a <see cref="ReadOnlySpan{T}" />
	/// is due to <see cref="FindOffsetsInFile" />.
	/// </remarks>
	/// <param name="memory">A <see cref="ReadOnlyMemory{T}"/> to the data to extract the tags from.</param>
	/// <param name="versionByte">The PSF version byte to check for.</param>
	/// <returns>The list of tags contained within the given PSF.</returns>
	public static TagList GetTagsFromPSF(ReadOnlyMemory<byte> memory, byte versionByte)
	{
		// Check to make sure the file is valid.
		NCSF.CheckForValidPSF(memory.Span, versionByte);

		// Get the starting offset of the tags.
		int tagOffset = NCSF.FindOffsetsInFile(NCSF.TAGHeader, memory).SingleOrDefault(-1);

		// Only continue on if we have tags.
		if (tagOffset != -1)
		{
			var tagsSpan = memory.Span[(tagOffset + NCSF.TAGHeader.Length)..];
			// First we read the tags using the system codepage, and then if we have a utf8 flag, we re-read the tags using UTF-8.
			var tags = NCSF.GetTagsFromPSFWithEncoding(tagsSpan, NCSF.SystemCodePageEncoding);
			if (tags.Contains("utf8"))
				tags = NCSF.GetTagsFromPSFWithEncoding(tagsSpan, Encoding.UTF8);

			return tags;
		}

		// There were no tags if we get here.
		return [];
	}

	static readonly short[] convertScaleLookupTable =
	[
		-32768, -421, -361, -325, -300, -281, -265, -252,
		-240, -230, -221, -212, -205, -198, -192, -186,
		-180, -175, -170, -165, -161, -156, -152, -148,
		-145, -141, -138, -134, -131, -128, -125, -122,
		-120, -117, -114, -112, -110, -107, -105, -103,
		-100, -98, -96, -94, -92, -90, -88, -86,
		-85, -83, -81, -79, -78, -76, -74, -73,
		-71, -70, -68, -67, -65, -64, -62, -61,
		-60, -58, -57, -56, -54, -53, -52, -51,
		-49, -48, -47, -46, -45, -43, -42, -41,
		-40, -39, -38, -37, -36, -35, -34, -33,
		-32, -31, -30, -29, -28, -27, -26, -25,
		-24, -23, -23, -22, -21, -20, -19, -18,
		-17, -17, -16, -15, -14, -13, -12, -12,
		-11, -10, -9, -9, -8, -7, -6, -6,
		-5, -4, -3, -3, -2, -1, -1, 0
	];

	/// <summary>
	/// Converts a scale value from an <see cref="NC.INFOEntrySEQ" /> into a value to be utilized for volume purposes.
	/// </summary>
	/// <param name="scale">The scale value to convert.</param>
	/// <returns>The volume value that <paramref name="scale" /> refers to.</returns>
	public static short ConvertScale(int scale)
	{
		// In theory the below condition should probably never happen.
		if ((scale & 0x80) != 0) // Supposedly invalid value...
			scale = 0x7F; // Use apparently correct default.
		return NCSF.convertScaleLookupTable[scale];
	}
}
