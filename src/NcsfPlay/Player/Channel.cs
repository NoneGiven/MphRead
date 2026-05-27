using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace NCSFPlayer;

public class Channel : NCSFCommon.Channel
{
	const int α = 3; // For Lanczos
	const int SincResolution = 8192;
	internal const int SincWidth = 8;
	const int SincSamples = Channel.SincResolution * Channel.SincWidth;
	const int LanczosSamples = Channel.SincResolution * Channel.α;
	static readonly float[] SincLut = new float[Channel.SincSamples + 1];
	static readonly float[] WindowLut = new float[Channel.SincSamples + 1];
	static readonly float[] FlatTopWindowSincLut = new float[Channel.SincSamples + 1];
	static readonly float[] LanczosLut = new float[Channel.LanczosSamples + 1];

	SWAVWrapper swavWrapper = null!;

	static float Sinc(float x) => x == 0 ? 1 : float.Sin(x * float.Pi) / (x * float.Pi);

	static Channel()
	{
		float dx = 1.0f / Channel.SincResolution;
		float x = 0;
		for (int i = 0; i <= Channel.SincSamples; ++i, x += dx)
		{
			float y = x / Channel.SincWidth;
			Channel.SincLut[i] = float.Abs(x) < Channel.SincWidth ? Channel.Sinc(x) : 0;
			Channel.WindowLut[i] = 0.40897f + 0.5f * float.Cos(float.Pi * y) + 0.09103f * float.Cos(2 * float.Pi * y);
			Channel.FlatTopWindowSincLut[i] = (float.Abs(x) < Channel.SincWidth ? Channel.Sinc(x) : 0) *
				(0.21557895f + 0.41663158f * float.Cos(float.Pi * y) + 0.27723158f * float.Cos(float.Pi * y * 2) +
					0.083578947f * float.Cos(float.Pi * y * 3) + 0.006947368f * float.Cos(float.Pi * y * 4));
			if (i <= Channel.LanczosSamples)
				Channel.LanczosLut[i] = x < Channel.α ? Channel.Sinc(x) * Channel.Sinc(x / Channel.α) : 0;
		}
	}

	float LanczosInterpolate(double ratio)
	{
		// The below is really the best way to do this. Not using a lookup table for Lanczos slows it down to about half the speed.
		// - Sse with a lookup table is about as fast as this without a lookup table and slower still without a lookup table
		// - Avx with a lookup table is a tad slower as this without a lookup table and slower still without a lookup table
		var data = this.swavWrapper.Slice(-α + 1, 2 * α);
		float sum = 0;
		for (int i = -α + 1; i <= α; ++i)
			sum += data[i + α - 1] * Channel.LanczosLut[(int)double.Floor(double.Abs(ratio - i) * Channel.SincResolution)];
		return sum;
	}

	float SimpleSincInterpolate(double ratio)
	{
		var data = this.swavWrapper.Slice(-Channel.SincWidth + 1, 2 * Channel.SincWidth);
		float sum = 0;
		for (int i = 0; i < Channel.SincWidth * 2; ++i)
			sum += data[i] *
				Channel.FlatTopWindowSincLut[(int)double.Floor(double.Abs(ratio - (i - Channel.SincWidth + 1)) * Channel.SincResolution)];
		return sum;
	}

	float OldSincInterpolate(double ratio)
	{
		var data = this.swavWrapper.Slice(-Channel.SincWidth + 1, 2 * Channel.SincWidth);
		Span<float> kernel = stackalloc float[Channel.SincWidth * 2];
		float kernelSum = 0;
		int shift = (int)double.Floor(ratio * Channel.SincResolution);
		int step =
			this.Register.SampleIncrease > 1 ? (int)(Channel.SincResolution / this.Register.SampleIncrease) : Channel.SincResolution;
		int shiftAdj = shift * step / Channel.SincResolution;
		int windowStep = Channel.SincResolution;
		for (int i = Channel.SincWidth; i >= -(Channel.SincWidth - 1); --i)
		{
			int pos = i * step;
			int windowPos = i * windowStep;
			kernelSum += kernel[i + Channel.SincWidth - 1] =
				Channel.SincLut[int.Abs(shiftAdj - pos)] * Channel.WindowLut[int.Abs(shift - windowPos)];
		}
		float sum = 0;
		for (int i = 0; i < Channel.SincWidth * 2; ++i)
			sum += data[i] * kernel[i];
		return sum / kernelSum;
	}

	float SixPointLagrangeInterpolate(double ratio)
	{
		// The only minor speedup here is using float's FusedMultiplyAdd, all other methods are slower
		// - Sse, Avx and Fma intrinsics are slower by about 2.5-4x
		// - Not using FusedMultiplyAdd is barely slower than using it
		var data = this.swavWrapper.Slice(-2, 6);
		ratio -= 0.5;
		// All the data accesses here are +2 more than they should be, since our slice is -2
		float even1 = data[0] + data[5], odd1 = data[0] - data[5];
		float even2 = data[1] + data[4], odd2 = data[1] - data[4];
		float even3 = data[2] + data[3], odd3 = data[2] - data[3];
		float c0 = 0.01171875f * even1 - 0.09765625f * even2 + 0.5859375f * even3;
		float c1 = 25 / 384.0f * odd2 - 1.171875f * odd3 - 0.0046875f * odd1;
		float c2 = 0.40625f * even2 - 17 / 48.0f * even3 - 5 / 96.0f * even1;
		float c3 = odd1 / 48.0f - 13 / 48.0f * odd2 + 17 / 24.0f * odd3;
		float c4 = even1 / 48.0f - 0.0625f * even2 + even3 / 24.0f;
		float c5 = odd2 / 24.0f - odd3 / 12.0f - odd1 / 120.0f;
		return float.FusedMultiplyAdd(float.FusedMultiplyAdd(float.FusedMultiplyAdd(float.FusedMultiplyAdd(float.FusedMultiplyAdd(c5,
			(float)ratio, c4), (float)ratio, c3), (float)ratio, c2), (float)ratio, c1), (float)ratio, c0);
	}

	static readonly Vector128<float> fourPtLagrange_c0multipliers = Vector128.Create([0f, 1f, 0f, 0f]);
	static readonly Vector128<float> fourPtLagrange_c1multipliers = Vector128.Create([-1.0f / 3, -0.5f, 1, -1.0f / 6]);
	static readonly Vector128<float> fourPtLagrange_c2multipliers = Vector128.Create([0.5f, -1, 0.5f, 0]);
	static readonly Vector128<float> fourPtLagrange_c3multipliers = Vector128.Create([-1.0f / 6, 0.5f, -0.5f, 1.0f / 6]);

	float FourPointInterpolate(double ratio)
	{
		// Avx does not help here, it is actually a lot slower to do things with Avx.
		// Fma and Sse are a tad bit faster than doing a fused multiply-add with float.
		var data = this.swavWrapper.Slice(-1, 4);
		// All the data accesses here are +1 more than they should be, since our slice is -1
		if (Fma.IsSupported)
		{
			var vec = Vector128.Create(data);
			var c0 = Sse.Multiply(vec, Channel.fourPtLagrange_c0multipliers);
			var c1 = Sse.Multiply(vec, Channel.fourPtLagrange_c1multipliers);
			var c2 = Sse.Multiply(vec, Channel.fourPtLagrange_c2multipliers);
			var c3 = Sse.Multiply(vec, Channel.fourPtLagrange_c3multipliers);
			var ratioVec = Vector128.Create((float)ratio);
			return Vector128.Sum(Fma.MultiplyAdd(Fma.MultiplyAdd(Fma.MultiplyAdd(c3, ratioVec, c2), ratioVec, c1), ratioVec, c0));
		}
		else if (Sse.IsSupported)
		{
			var vec = Vector128.Create(data);
			var c0 = Sse.Multiply(vec, Channel.fourPtLagrange_c0multipliers);
			var c1 = Sse.Multiply(vec, Channel.fourPtLagrange_c1multipliers);
			var c2 = Sse.Multiply(vec, Channel.fourPtLagrange_c2multipliers);
			var c3 = Sse.Multiply(vec, Channel.fourPtLagrange_c3multipliers);
			var ratioVec = Vector128.Create((float)ratio);
			var c3_tmp = Sse.Multiply(c3, Sse.Multiply(ratioVec, Sse.Multiply(ratioVec, ratioVec)));
			var c2_tmp = Sse.Multiply(c2, Sse.Multiply(ratioVec, ratioVec));
			var c1_tmp = Sse.Multiply(c1, ratioVec);
			return Vector128.Sum(c3_tmp + c2_tmp + c1_tmp + c0);
		}
		else
		{
			float c0 = data[1];
			float c1 = data[2] - data[0] / 3.0f - 0.5f * data[1] - data[3] / 6.0f;
			float c2 = 0.5f * (data[0] + data[2]) - data[1];
			float c3 = (data[3] - data[0]) / 6.0f + 0.5f * (data[1] - data[2]);
			return float.FusedMultiplyAdd(float.FusedMultiplyAdd(float.FusedMultiplyAdd(c3, (float)ratio, c2), (float)ratio, c1),
				(float)ratio, c0);
		}
	}

	float LinearInterpolate(double ratio)
	{
		// The below is really the best way to do things, all other methods are barely better or much worse:
		// - Doing the lerp manually or using Fma intrinsics take roughly the same amount of time
		// - Using Sse intrinsics is slower
		// - Using Avx intrinsics is even slower
		var data = this.swavWrapper.Slice(0, 2);
		return float.Lerp(data[0], data[1], (float)ratio);
	}

	public float Interpolate()
	{
		double ratio = this.Register.SamplePosition;
		ratio -= (int)ratio;

		return (this.Player as Player)!.Interpolation switch
		{
			Interpolation.Lanczos => this.LanczosInterpolate(ratio),
			Interpolation.SimpleSinc => this.SimpleSincInterpolate(ratio),
			Interpolation.Sinc => this.OldSincInterpolate(ratio),
			Interpolation.SixPointLagrange => this.SixPointLagrangeInterpolate(ratio),
			Interpolation.FourPointLagrange => this.FourPointInterpolate(ratio),
			Interpolation.Linear => this.LinearInterpolate(ratio),
			_ => 0 // Should never happen
		};
	}

	public override float GenerateSample()
	{
		if (this.Register.SamplePosition < 0)
			return 0;

		if (this.Register.Format != 3)
			return (this.Player as Player)!.Interpolation == Interpolation.None ?
				this.Register.Source!.Data[(int)this.Register.SamplePosition] : this.Interpolate();
		else if (this.Id < 8)
			return 0;
		else if (this.Id < 14)
			return Channel.WaveDutyTable[this.Register.WaveDuty][(int)this.Register.SamplePosition & 0x7];
		else
		{
			if (this.Register.PSGLastCount != (uint)this.Register.SamplePosition)
			{
				uint max = (uint)this.Register.SamplePosition;
				for (uint i = this.Register.PSGLastCount; i < max; ++i)
					if ((this.Register.PSGX & 1) != 0)
					{
						this.Register.PSGX = (ushort)((this.Register.PSGX >> 1) ^ 0x6000);
						this.Register.PSGLast = -1;
					}
					else
					{
						this.Register.PSGX >>= 1;
						this.Register.PSGLast = 1;
					}

				this.Register.PSGLastCount = (uint)this.Register.SamplePosition;
			}

			return this.Register.PSGLast;
		}
	}

	public override void IncrementSample()
	{
		double samplePosition = this.Register.SamplePosition + this.Register.SampleIncrease;

		if (this.Register.Format != 3 && (this.Player as Player)!.Interpolation != Interpolation.None &&
			this.Register.SamplePosition < 0 && samplePosition >= 0)
			this.swavWrapper = new(this.Register);

		this.Register.SamplePosition = samplePosition;

		if (this.Register.Format != 3 && this.Register.SamplePosition >= this.Register.TotalLength)
		{
			if (this.Register.RepeatMode == 1)
				while (this.Register.SamplePosition >= this.Register.TotalLength)
					this.Register.SamplePosition -= this.Register.Length;
			else
			{
				this.Kill();
				this.swavWrapper = null!;
			}
		}
	}
}
