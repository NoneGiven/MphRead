namespace NCSFCommon.ReplayGain;

public record class FrequencyInfo(uint SampleRate, double[] BYule, double[] AYule, double[] BButter, double[] AButter);
