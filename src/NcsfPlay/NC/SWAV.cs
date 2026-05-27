using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.NC;

/// <summary>
/// SDAT - SWAV (Waveform/Sample).
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Uses data from
/// <see href="http://www.feshrine.net/hacking/doc/nds-sdat.html">Nintendo DS Nitro Composer (SDAT) Specification document</see>.
/// </para>
/// </remarks>
public class SWAV : IEquatable<SWAV>, IEqualityOperators<SWAV, SWAV, bool>
{
	static readonly int[] IMAIndexTable =
	[
		-1, -1, -1, -1, 2, 4, 6, 8,
		-1, -1, -1, -1, 2, 4, 6, 8
	];

	static readonly int[] IMAStepTable =
	[
		0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x0010, 0x0011,
		0x0013, 0x0015, 0x0017, 0x0019, 0x001C, 0x001F, 0x0022, 0x0025, 0x0029, 0x002D,
		0x0032, 0x0037, 0x003C, 0x0042, 0x0049, 0x0050, 0x0058, 0x0061, 0x006B, 0x0076,
		0x0082, 0x008F, 0x009D, 0x00AD, 0x00BE, 0x00D1, 0x00E6, 0x00FD, 0x0117, 0x0133,
		0x0151, 0x0173, 0x0198, 0x01C1, 0x01EE, 0x0220, 0x0256, 0x0292, 0x02D4, 0x031C,
		0x036C, 0x03C3, 0x0424, 0x048E, 0x0502, 0x0583, 0x0610, 0x06AB, 0x0756, 0x0812,
		0x08E0, 0x09C3, 0x0ABD, 0x0BD0, 0x0CFF, 0x0E4C, 0x0FBA, 0x114C, 0x1307, 0x14EE,
		0x1706, 0x1954, 0x1BDC, 0x1EA5, 0x21B6, 0x2515, 0x28CA, 0x2CDF, 0x315B, 0x364B,
		0x3BB9, 0x41B2, 0x4844, 0x4F7E, 0x5771, 0x602F, 0x69CE, 0x7462, 0x7FFF
	];

	/// <summary>
	/// The type of this waveform/sample.
	/// </summary>
	/// <remarks>
	/// 0 is 8-bit PCM, 1 is signed 16-bit PCM and 2 is IMA ADPCM.
	/// </remarks>
	public byte WaveType { get; set; }

	/// <summary>
	/// The looping status of this waveform/sample.
	/// </summary>
	/// <remarks>
	/// 0 if it doesn't loop, non-0 if it does loop.
	/// </remarks>
	public byte Loop { get; set; }

	/// <summary>
	/// The sample rate in Hz of this waveform/sample.
	/// </summary>
	public ushort SampleRate { get; set; }

	/// <summary>
	/// The timer for this waveform/sample.
	/// </summary>
	/// <remarks>
	/// It is derived from the sample rate as: dividing the ARM7 clock rate by twice the sample rate, truncating the value.
	/// </remarks>
	public ushort Time { get; set; }

	/// <summary>
	/// The loop start offset of this waveform/sample (as it is in the original file).
	/// </summary>
	public ushort OriginalLoopOffset { get; set; }

	/// <summary>
	/// The loop start offset of this waveform/sample (converted for 16-bit PCM).
	/// </summary>
	public uint LoopOffset { get; set; }

	/// <summary>
	/// The loop length (or the length of the entire wave for non-looping ones) of this waveform/sample (as it is in the original file).
	/// </summary>
	public uint OriginalLoopLength { get; set; }

	/// <summary>
	/// The loop length (or the length of the entire wave for non-looping ones) of this waveform/sample (converted for 16-bit PCM).
	/// </summary>
	public uint LoopLength { get; set; }

	readonly List<byte> originalData = [];

	/// <summary>
	/// The data for this waveform/sample (as it is in the original file).
	/// </summary>
	public ReadOnlySpan<byte> OriginalData => this.originalData.AsSpan();

	readonly List<float> data = [];

	/// <summary>
	/// The data for this waveform/sample (converted to 32-bit floating-point PCM).
	/// </summary>
	public ReadOnlySpan<float> Data => this.data.AsSpan();

	/// <summary>
	/// The size of this waveform/sample.
	/// </summary>
	/// <remarks>
	/// Wave Type (8-bit integer) + Loop (8-bit integer) + Sample Rate (16-bit integer) + Time (16-bit integer) +
	/// Loop Offset (16-bit integer) + Non-Loop Length (32-bit integer) + number of bytes in Original Data. (Should be at least 12 bytes.)
	/// </remarks>
	public uint Size => 0x0C + (uint)this.originalData.Count;

	/// <summary>
	/// Decodes a nibble (4-byte value) for IMA-ADPCM.
	/// </summary>
	/// <param name="nibble">The nibble to decode.</param>
	/// <param name="stepIndex">The current step index.</param>
	/// <param name="predictedValue">The current predicted value.</param>
	static void DecodeADPCMNibble(int nibble, ref int stepIndex, ref int predictedValue)
	{
		int step = SWAV.IMAStepTable[stepIndex];

		stepIndex += SWAV.IMAIndexTable[nibble];

		stepIndex = int.Clamp(stepIndex, 0, 88);

		// The following calculation for diff is to replicate a rounding-error in the calculation, this is equivalent to:
		// diff = ((nibble & 7) * 2 + 1) * step / 8;
		// Except this is just in theory, what comes below is what happens in practice.
		int diff = step >> 3;

		if ((nibble & 1) != 0)
			diff += step >> 2;
		if ((nibble & 2) != 0)
			diff += step >> 1;
		if ((nibble & 4) != 0)
			diff += step;

		// The following calculation for predictedValue is to replicate a clipping-error in the calculation,
		// the Min leaves -0x8000 unclipped while the Max clips -0x8000 to -0x7FFF.
		predictedValue = (nibble & 8) == 0 ? int.Min(predictedValue + diff, 0x7FFF) : int.Max(predictedValue - diff, -0x7FFF);
	}

	/// <summary>
	/// Decodes IMA-ADPCM to 16-bit PCM.
	/// </summary>
	/// <param name="len">The length of the IMA-ADPCM data.</param>
	public void DecodeADPCM(uint len)
	{
		var originalSpan = this.originalData.AsSpan();
		int predictedValue = BinaryPrimitives.ReadUInt16LittleEndian(originalSpan);
		int stepIndex = BinaryPrimitives.ReadUInt16LittleEndian(originalSpan[2..]);

		for (uint i = 0; i < len; ++i)
		{
			SWAV.DecodeADPCMNibble(originalSpan[(int)(i + 4)] & 0x0F, ref stepIndex, ref predictedValue);
			this.data.Add((float)predictedValue / short.MaxValue);

			SWAV.DecodeADPCMNibble((originalSpan[(int)(i + 4)] >> 4) & 0x0F, ref stepIndex, ref predictedValue);
			this.data.Add((float)predictedValue / short.MaxValue);
		}
	}

	/// <summary>
	/// Reads the data for the <see cref="SWAV" />.
	/// </summary>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> to read from.</param>
	/// <returns>The file itself.</returns>
	public SWAV Read(ReadOnlySpan<byte> span)
	{
		this.WaveType = span[0x00];
		this.Loop = span[0x01];
		this.SampleRate = BinaryPrimitives.ReadUInt16LittleEndian(span[0x02..]);
		this.Time = BinaryPrimitives.ReadUInt16LittleEndian(span[0x04..]);
		this.LoopOffset = this.OriginalLoopOffset = BinaryPrimitives.ReadUInt16LittleEndian(span[0x06..]);
		this.LoopLength = this.OriginalLoopLength = BinaryPrimitives.ReadUInt32LittleEndian(span[0x08..]);
		uint size = (this.LoopOffset + this.LoopLength) * 4;
		CollectionsMarshal.SetCount(this.originalData, (int)size);
		span.Slice(0x0C, (int)size).CopyTo(this.originalData.AsSpan());

		// Convert data accordingly.
		this.data.Clear();
		switch (this.WaveType)
		{
			case 0: // PCM 8-bit
				_ = this.data.EnsureCapacity((int)size);
				this.data.AddRange(this.originalData.Select(static o => (sbyte)o / (float)sbyte.MaxValue));
				this.LoopOffset *= 4;
				this.LoopLength *= 4;
				break;
			case 1: // PCM signed 16-bit
				size /= 2;
				_ = this.data.EnsureCapacity((int)size);
				this.data.AddRange(this.originalData.AsSpan().Cast<byte, short>().ToArray().Select(static o => (float)o / short.MaxValue));
				this.LoopOffset *= 2;
				this.LoopLength *= 2;
				break;
			case 2: // IMA ADPCM
				_ = this.data.EnsureCapacity((int)((size - 4) * 2));
				this.DecodeADPCM(size - 4);
				if (this.LoopOffset != 0)
					--this.LoopOffset;
				this.LoopOffset *= 8;
				this.LoopLength *= 8;
				break;
		}

		return this;
	}

	/// <summary>
	/// Writes the data for the <see cref="SWAV" />.
	/// </summary>
	/// <param name="span">The <see cref="Span{T}" /> to write to.</param>
	public void Write(Span<byte> span)
	{
		span[0x00] = this.WaveType;
		span[0x01] = this.Loop;
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x02..], this.SampleRate);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x04..], this.Time);
		BinaryPrimitives.WriteUInt16LittleEndian(span[0x06..], this.OriginalLoopOffset);
		BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], this.OriginalLoopLength);
		this.originalData.AsSpan().CopyTo(span[0x0C..]);
	}

	#region IEquatable<SWAV>

	public bool Equals(SWAV? other) => other is not null && this.WaveType == other.WaveType && this.Loop == other.Loop &&
		this.SampleRate == other.SampleRate && this.Time == other.Time && this.OriginalLoopOffset == other.OriginalLoopOffset &&
		this.OriginalLoopLength == other.OriginalLoopLength && this.originalData.AsSpan().SequenceEqual(other.originalData.AsSpan());

	public override bool Equals(object? obj) => obj is SWAV swav && this.Equals(swav);

	public override int GetHashCode() => HashCode.Combine(this.WaveType, this.Loop, this.SampleRate, this.Time, this.OriginalLoopOffset,
		this.OriginalLoopLength, this.originalData);

	public static bool operator ==(SWAV? left, SWAV? right) => left?.Equals(right) ?? false;

	public static bool operator !=(SWAV? left, SWAV? right) => !(left == right);

	#endregion
}
