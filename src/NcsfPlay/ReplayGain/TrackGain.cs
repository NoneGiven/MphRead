using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;

namespace NCSFCommon.ReplayGain;

public class TrackGain
{
	readonly int sampleSize;
	internal GainData GainData = new();

	readonly double[] lInPreBuf = new double[ReplayGain.MaxOrder * 2];
	readonly int lInPrePos = ReplayGain.MaxOrder;
	readonly double[] lStepBuf = new double[ReplayGain.MaxSamplesPerWindow + ReplayGain.MaxOrder];
	readonly int lStepPos = ReplayGain.MaxOrder;
	readonly double[] lOutBuf = new double[ReplayGain.MaxSamplesPerWindow + ReplayGain.MaxOrder];
	readonly int lOutPos = ReplayGain.MaxOrder;

	readonly double[] rInPreBuf = new double[ReplayGain.MaxOrder * 2];
	readonly int rInPrePos = ReplayGain.MaxOrder;
	readonly double[] rStepBuf = new double[ReplayGain.MaxSamplesPerWindow + ReplayGain.MaxOrder];
	readonly int rStepPos = ReplayGain.MaxOrder;
	readonly double[] rOutBuf = new double[ReplayGain.MaxSamplesPerWindow + ReplayGain.MaxOrder];
	readonly int rOutPos = ReplayGain.MaxOrder;

	readonly long sampleWindow;
	long totSamp;
	double lSum;
	double rSum;
	readonly FrequencyInfo freqInfo;

	public TrackGain(int sampleRate, int sampleSize)
	{
		if (!ReplayGain.IsSupportedFormat(sampleRate, sampleSize))
			throw new NotSupportedException("Unsupported format. Supported sample sizes are 16, 24.");

		this.freqInfo = ReplayGain.FreqInfos[Array.FindIndex(ReplayGain.FreqInfos, i => i.SampleRate == sampleRate)];

		this.sampleSize = sampleSize;

		this.sampleWindow = (int)double.Ceiling(sampleRate * ReplayGain.RmsWindowTime);
	}

	public void AnalyzeSamples(ReadOnlySpan<int> leftSamples, ReadOnlySpan<int> rightSamples)
	{
		if (leftSamples.Length != rightSamples.Length)
			throw new ArgumentException("leftSamples must be as big as rightSamples");

		int numSamples = leftSamples.Length;

		Span<double> leftDouble = new double[numSamples];
		Span<double> rightDouble = new double[numSamples];

		if (this.sampleSize == 16)
		{
			if (Avx.IsSupported)
			{
				var leftSampleVecs = MemoryMarshal.Cast<int, Vector128<int>>(leftSamples);
				var rightSampleVecs = MemoryMarshal.Cast<int, Vector128<int>>(rightSamples);
				int len = leftSampleVecs.Length;
				var leftDoubleVecs = MemoryMarshal.Cast<double, Vector256<double>>(leftDouble);
				var rightDoubleVecs = MemoryMarshal.Cast<double, Vector256<double>>(rightDouble);
				for (int i = 0; i < len; ++i)
				{
					leftDoubleVecs[i] = Avx.ConvertToVector256Double(leftSampleVecs[i]);
					rightDoubleVecs[i] = Avx.ConvertToVector256Double(rightSampleVecs[i]);
				}
				int offset = len * Vector256<double>.Count;
				if (offset != numSamples)
					for (; offset < numSamples; ++offset)
					{
						leftDouble[offset] = leftSamples[offset];
						rightDouble[offset] = rightDouble[offset];
					}
			}
			else
				for (int i = 0; i < numSamples; ++i)
				{
					leftDouble[i] = leftSamples[i];
					rightDouble[i] = rightSamples[i];
				}
		}
		else if (this.sampleSize == 24)
			for (int i = 0; i < numSamples; ++i)
			{
				leftDouble[i] = leftSamples[i] * ReplayGain.Factor24Bit;
				rightDouble[i] = rightSamples[i] * ReplayGain.Factor24Bit;
			}
		else
			throw new InvalidOperationException();

		for (int i = 0; i < numSamples; ++i)
		{
			double tmpPeak = double.Abs(leftDouble[i]);
			if (tmpPeak > this.GainData.PeakSample)
				this.GainData.PeakSample = tmpPeak;

			tmpPeak = double.Abs(rightDouble[i]);
			if (tmpPeak > this.GainData.PeakSample)
				this.GainData.PeakSample = tmpPeak;
		}

		this.AnalyzeSamples(leftDouble, rightDouble);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static double Sqr(double d) => d * d;

	static void FilterYule(ReadOnlySpan<double> input, Span<double> output, ref int inPos, ref int outPos, long nSamples,
		ReadOnlySpan<double> aKernel, ReadOnlySpan<double> bKernel)
	{
		while (nSamples-- != 0)
		{
			output[outPos] = 1e-10 +
				input[inPos] * bKernel[0] - output[outPos - 1] * aKernel[1] +
				input[inPos - 1] * bKernel[1] - output[outPos - 2] * aKernel[2] +
				input[inPos - 2] * bKernel[2] - output[outPos - 3] * aKernel[3] +
				input[inPos - 3] * bKernel[3] - output[outPos - 4] * aKernel[4] +
				input[inPos - 4] * bKernel[4] - output[outPos - 5] * aKernel[5] +
				input[inPos - 5] * bKernel[5] - output[outPos - 6] * aKernel[6] +
				input[inPos - 6] * bKernel[6] - output[outPos - 7] * aKernel[7] +
				input[inPos - 7] * bKernel[7] - output[outPos - 8] * aKernel[8] +
				input[inPos - 8] * bKernel[8] - output[outPos - 9] * aKernel[9] +
				input[inPos - 9] * bKernel[9] - output[outPos - 10] * aKernel[10] +
				input[inPos - 10] * bKernel[10];
			++outPos;
			++inPos;
		}
	}

	static void FilterButter(ReadOnlySpan<double> input, Span<double> output, ref int inPos, ref int outPos, long nSamples,
		ReadOnlySpan<double> aKernel, ReadOnlySpan<double> bKernel)
	{
		while (nSamples-- != 0)
		{
			output[outPos] =
				input[inPos] * bKernel[0] - output[outPos - 1] * aKernel[1] +
				input[inPos - 1] * bKernel[1] - output[outPos - 2] * aKernel[2] +
				input[inPos - 2] * bKernel[2];
			++outPos;
			++inPos;
		}
	}

	void AnalyzeSamples(ReadOnlySpan<double> leftSamples, ReadOnlySpan<double> rightSamples)
	{
		int numSamples = leftSamples.Length;

		long batchSamples = numSamples;
		long curSamplePos = 0;

		if (numSamples < ReplayGain.MaxOrder)
		{
			leftSamples.CopyTo(this.lInPreBuf.AsSpan(ReplayGain.MaxOrder));
			rightSamples.CopyTo(this.rInPreBuf.AsSpan(ReplayGain.MaxOrder));
		}
		else
		{
			leftSamples[..ReplayGain.MaxOrder].CopyTo(this.lInPreBuf.AsSpan(ReplayGain.MaxOrder));
			rightSamples[..ReplayGain.MaxOrder].CopyTo(this.rInPreBuf.AsSpan(ReplayGain.MaxOrder));
		}

		while (batchSamples > 0)
		{
			long curSamples = batchSamples > this.sampleWindow - this.totSamp ? this.sampleWindow - this.totSamp : batchSamples;
			int curLeftPos;
			int curRightPos;
			ReadOnlySpan<double> curLeft;
			ReadOnlySpan<double> curRight;
			if (curSamplePos < ReplayGain.MaxOrder)
			{
				curLeftPos = this.lInPrePos + (int)curSamplePos;
				curRightPos = this.rInPrePos + (int)curSamplePos;
				curLeft = this.lInPreBuf;
				curRight = this.rInPreBuf;
				if (curSamples > ReplayGain.MaxOrder - curSamplePos)
					curSamples = ReplayGain.MaxOrder - curSamplePos;
			}
			else
			{
				curLeftPos = curRightPos = (int)curSamplePos;
				curLeft = leftSamples;
				curRight = rightSamples;
			}

			int outPos = this.lStepPos + (int)this.totSamp;
			TrackGain.FilterYule(curLeft, this.lStepBuf, ref curLeftPos, ref outPos, curSamples,
				this.freqInfo.AYule, this.freqInfo.BYule);
			outPos = this.rStepPos + (int)this.totSamp;
			TrackGain.FilterYule(curRight, this.rStepBuf, ref curRightPos, ref outPos, curSamples,
				this.freqInfo.AYule, this.freqInfo.BYule);

			int inPos = this.lStepPos + (int)this.totSamp;
			outPos = this.lOutPos + (int)this.totSamp;
			TrackGain.FilterButter(this.lStepBuf, this.lOutBuf, ref inPos, ref outPos, curSamples,
				this.freqInfo.AButter, this.freqInfo.BButter);
			inPos = this.rStepPos + (int)this.totSamp;
			outPos = this.rOutPos + (int)this.totSamp;
			TrackGain.FilterButter(this.rStepBuf, this.rOutBuf, ref inPos, ref outPos, curSamples,
				this.freqInfo.AButter, this.freqInfo.BButter);

			curLeft = this.lOutBuf;
			curLeftPos = this.lOutPos + (int)this.totSamp;
			curRight = this.rOutBuf;
			curRightPos = this.rOutPos + (int)this.totSamp;

			for (long i = curSamples % 16; i-- != 0;)
			{
				this.lSum += TrackGain.Sqr(curLeft[curLeftPos++]);
				this.rSum += TrackGain.Sqr(curRight[curRightPos++]);
			}

			if (Avx.IsSupported)
			{
				var curLeftVec = MemoryMarshal.Cast<double, Vector256<double>>(curLeft[curLeftPos..]);
				var curRightVec = MemoryMarshal.Cast<double, Vector256<double>>(curRight[curRightPos..]);
				curLeftPos = curRightPos = 0;
				for (long i = curSamples / 16; i-- != 0;)
				{
					this.lSum += Vector256.Sum(
						Avx.Multiply(curLeftVec[curLeftPos], curLeftVec[curLeftPos]) +
						Avx.Multiply(curLeftVec[curLeftPos + 1], curLeftVec[curLeftPos + 1]) +
						Avx.Multiply(curLeftVec[curLeftPos + 2], curLeftVec[curLeftPos + 2]) +
						Avx.Multiply(curLeftVec[curLeftPos + 3], curLeftVec[curLeftPos + 3])
					);
					curLeftPos += 4;
					this.rSum += Vector256.Sum(
						Avx.Multiply(curRightVec[curRightPos], curRightVec[curRightPos]) +
						Avx.Multiply(curRightVec[curRightPos + 1], curRightVec[curRightPos + 1]) +
						Avx.Multiply(curRightVec[curRightPos + 2], curRightVec[curRightPos + 2]) +
						Avx.Multiply(curRightVec[curRightPos + 3], curRightVec[curRightPos + 3])
					);
					curRightPos += 4;
				}
			}
			else if (Sse2.IsSupported)
				for (long i = curSamples / 16; i-- != 0;)
				{
					var curLeftVec = MemoryMarshal.Cast<double, Vector128<double>>(curLeft[curLeftPos..]);
					this.lSum += Vector128.Sum(
						Sse2.Multiply(curLeftVec[0], curLeftVec[0]) +
						Sse2.Multiply(curLeftVec[1], curLeftVec[1]) +
						Sse2.Multiply(curLeftVec[2], curLeftVec[2]) +
						Sse2.Multiply(curLeftVec[3], curLeftVec[3]) +
						Sse2.Multiply(curLeftVec[4], curLeftVec[4]) +
						Sse2.Multiply(curLeftVec[5], curLeftVec[5]) +
						Sse2.Multiply(curLeftVec[6], curLeftVec[6]) +
						Sse2.Multiply(curLeftVec[7], curLeftVec[7])
					);
					curLeftPos += 4;
					var curRightVec = MemoryMarshal.Cast<double, Vector128<double>>(curRight[curRightPos..]);
					this.rSum += Vector128.Sum(
						Sse2.Multiply(curRightVec[0], curRightVec[0]) +
						Sse2.Multiply(curRightVec[1], curRightVec[1]) +
						Sse2.Multiply(curRightVec[2], curRightVec[2]) +
						Sse2.Multiply(curRightVec[3], curRightVec[3]) +
						Sse2.Multiply(curRightVec[4], curRightVec[4]) +
						Sse2.Multiply(curRightVec[5], curRightVec[5]) +
						Sse2.Multiply(curRightVec[6], curRightVec[6]) +
						Sse2.Multiply(curRightVec[7], curRightVec[7])
					);
					curRightPos += 4;
				}
			else
				for (long i = curSamples / 16; i-- != 0;)
				{
					this.lSum +=
						TrackGain.Sqr(curLeft[curLeftPos]) + TrackGain.Sqr(curLeft[curLeftPos + 1]) +
						TrackGain.Sqr(curLeft[curLeftPos + 2]) + TrackGain.Sqr(curLeft[curLeftPos + 3]) +
						TrackGain.Sqr(curLeft[curLeftPos + 4]) + TrackGain.Sqr(curLeft[curLeftPos + 5]) +
						TrackGain.Sqr(curLeft[curLeftPos + 6]) + TrackGain.Sqr(curLeft[curLeftPos + 7]) +
						TrackGain.Sqr(curLeft[curLeftPos + 8]) + TrackGain.Sqr(curLeft[curLeftPos + 9]) +
						TrackGain.Sqr(curLeft[curLeftPos + 10]) + TrackGain.Sqr(curLeft[curLeftPos + 11]) +
						TrackGain.Sqr(curLeft[curLeftPos + 12]) + TrackGain.Sqr(curLeft[curLeftPos + 13]) +
						TrackGain.Sqr(curLeft[curLeftPos + 14]) + TrackGain.Sqr(curLeft[curLeftPos + 15]);
					curLeftPos += 16;
					this.rSum +=
						TrackGain.Sqr(curRight[curRightPos]) + TrackGain.Sqr(curRight[curRightPos + 1]) +
						TrackGain.Sqr(curRight[curRightPos + 2]) + TrackGain.Sqr(curRight[curRightPos + 3]) +
						TrackGain.Sqr(curRight[curRightPos + 4]) + TrackGain.Sqr(curRight[curRightPos + 5]) +
						TrackGain.Sqr(curRight[curRightPos + 6]) + TrackGain.Sqr(curRight[curRightPos + 7]) +
						TrackGain.Sqr(curRight[curRightPos + 8]) + TrackGain.Sqr(curRight[curRightPos + 9]) +
						TrackGain.Sqr(curRight[curRightPos + 10]) + TrackGain.Sqr(curRight[curRightPos + 11]) +
						TrackGain.Sqr(curRight[curRightPos + 12]) + TrackGain.Sqr(curRight[curRightPos + 13]) +
						TrackGain.Sqr(curRight[curRightPos + 14]) + TrackGain.Sqr(curRight[curRightPos + 15]);
					curRightPos += 16;
				}

			batchSamples -= curSamples;
			curSamplePos += curSamples;
			this.totSamp += curSamples;
			if (this.totSamp == this.sampleWindow)
			{
				double val = ReplayGain.StepsPerDb * 10 * double.Log10((this.lSum + this.rSum) / this.totSamp * 0.5 + 1e-37);
				int ival = (int)double.Clamp(val, 0, this.GainData.Accum.Length - 1);
				++this.GainData.Accum[ival];
				this.lSum = this.rSum = 0;

				if (this.totSamp > int.MaxValue)
					throw new OverflowException("Too many samples! Change to long and recompile!");

				this.lOutBuf.AsSpan((int)this.totSamp, ReplayGain.MaxOrder).CopyTo(this.lOutBuf);
				this.rOutBuf.AsSpan((int)this.totSamp, ReplayGain.MaxOrder).CopyTo(this.rOutBuf);
				this.lStepBuf.AsSpan((int)this.totSamp, ReplayGain.MaxOrder).CopyTo(this.lStepBuf);
				this.rStepBuf.AsSpan((int)this.totSamp, ReplayGain.MaxOrder).CopyTo(this.rStepBuf);

				this.totSamp = 0;
			}
			if (this.totSamp > this.sampleWindow)
				throw new Exception("Gain analysis error!");
		}

		if (numSamples < ReplayGain.MaxOrder)
		{
			this.lInPreBuf.AsSpan(numSamples, ReplayGain.MaxOrder - numSamples).CopyTo(this.lInPreBuf);
			this.rInPreBuf.AsSpan(numSamples, ReplayGain.MaxOrder - numSamples).CopyTo(this.rInPreBuf);
			leftSamples[..numSamples].CopyTo(this.lInPreBuf.AsSpan(ReplayGain.MaxOrder - numSamples));
			rightSamples[..numSamples].CopyTo(this.rInPreBuf.AsSpan(ReplayGain.MaxOrder - numSamples));
		}
		else
		{
			leftSamples.Slice(numSamples - ReplayGain.MaxOrder, ReplayGain.MaxOrder).CopyTo(this.lInPreBuf);
			rightSamples.Slice(numSamples - ReplayGain.MaxOrder, ReplayGain.MaxOrder).CopyTo(this.rInPreBuf);
		}
	}

	public double GetGain() => ReplayGain.AnalyzeResult(this.GainData.Accum);

	public double GetPeak() => this.GainData.PeakSample / ReplayGain.MaxSampleValue;
}
