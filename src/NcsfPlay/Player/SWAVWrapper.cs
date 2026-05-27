using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using NCSFCommon;

namespace NCSFPlayer;

/// <summary>
/// A small wrapper class for an SWAV, meant to replace the need for a ring buffer to access the data.
/// </summary>
class SWAVWrapper
{
	readonly List<float> data;
	readonly NDSSoundRegister register;

	public SWAVWrapper(NDSSoundRegister register)
	{
		this.data = [];
		CollectionsMarshal.SetCount(this.data, register.Source!.Data.Length + 2 * Channel.SincWidth);
		var data = this.data.AsSpan();
		Span<float> span = stackalloc float[Channel.SincWidth];
		span.Fill(register.Source!.Data[0]); // TODO: Test if this works better with 0 or the first sample
		span.CopyTo(data);
		register.Source!.Data.CopyTo(data[Channel.SincWidth..]);
		if (register.RepeatMode == 1)
			register.Source!.Data.Slice((int)register.LoopStart, Channel.SincWidth).CopyTo(data[^Channel.SincWidth..]);
		else
		{
			span.Clear();
			span.CopyTo(data[^Channel.SincWidth..]);
		}
		this.register = register;
	}

	public ReadOnlySpan<float> Slice(int index, int len) =>
		this.data.AsSpan().Slice((int)this.register.SamplePosition + index + Channel.SincWidth, len);
}
