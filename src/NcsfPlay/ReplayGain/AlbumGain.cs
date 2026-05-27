namespace NCSFCommon.ReplayGain;

public class AlbumGain
{
	readonly GainData albumData = new();

	public void AppendTrackData(TrackGain trackGain)
	{
		var sourceAccum = trackGain.GainData.Accum;
		for (int i = 0; i < sourceAccum.Length; ++i)
			this.albumData.Accum[i] += sourceAccum[i];
		this.albumData.PeakSample = double.Max(this.albumData.PeakSample, trackGain.GainData.PeakSample);
	}

	public double GetGain() => ReplayGain.AnalyzeResult(this.albumData.Accum);

	public double GetPeak() => this.albumData.PeakSample / ReplayGain.MaxSampleValue;
}
