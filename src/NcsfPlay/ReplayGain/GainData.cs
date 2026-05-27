namespace NCSFCommon.ReplayGain;

public class GainData
{
	readonly int[] accum = new int[ReplayGain.StepsPerDb * ReplayGain.MaxDb];

	public Span<int> Accum => this.accum.AsSpan();
	public double PeakSample { get; set; }
}
