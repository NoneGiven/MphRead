namespace NCSFCommon;

/// <summary>
/// This structure is meant to be similar to what is stored in the actual Nintendo DS's sound registers.
/// Items that were not being used by this player have been removed, and items which help the simulated registers have been added.
/// </summary>
public class NDSSoundRegister
{
	#region Control Register (SOUNDxCNT - 0x040004x0)

	/// <summary>
	/// Volume Multiplier (Bits 0-7, 0..127=silent..loud)
	/// </summary>
	public byte VolumeMultiplier { get; set; }
	/// <summary>
	/// Volume Divisor (Bits 8-9, 0=normal, 1=Div2, 2=Div4, 3=Div16)
	/// </summary>
	public byte VolumeDivisor { get; set; }
	/// <summary>
	/// Panning (Bits 16-22, 0..127=left..right, 64=half volume on both left and right)
	/// </summary>
	public byte Panning { get; set; }
	/// <summary>
	/// Wave Duty (Bits 24-26, PSG Only)
	/// </summary>
	public byte WaveDuty { get; set; }
	/// <summary>
	/// Repeat Mode (Bits 27-28, 0=Manual, 1=Loop Infinite, 2=One-Shot, 3=Prohibited)
	/// </summary>
	public byte RepeatMode { get; set; }
	/// <summary>
	/// Format (Bits 29-30, 0=PCM8, 1=PCM16, 2=IMA-ADPCM, 3=PSG/Noise)
	/// </summary>
	public byte Format { get; set; }
	/// <summary>
	/// Enable (Bit 31, 0=Stop, 1=Start/Busy)
	/// </summary>
	public bool Enable { get; set; }

	#endregion

	#region Data Source Register (SOUNDxSAD - 0x040004x4)

	public NC.SWAV? Source { get; set; }

	#endregion

	#region Timer Register (SOUNDxTMR - 0x040004x8)

	public ushort Timer { get; set; }

	#endregion

	#region PSG Handling, not a DS register

	public ushort PSGX { get; set; }
	public float PSGLast { get; set; }
	public uint PSGLastCount { get; set; }

	#endregion

	#region The following are taken from DeSmuME

	public double SamplePosition { get; set; }
	public double SampleIncrease { get; set; }

	#endregion

	#region Loopstart Register (SOUNDxPNT - 0x040004xA but 32-bit instead of 16-bit)

	public uint LoopStart { get; set; }

	#endregion

	#region Length Register (SOUNDxLEN - 0x040004xC)

	public uint Length { get; set; }

	#endregion

	public uint TotalLength { get; set; }

	public void ClearControlRegister()
	{
		this.VolumeMultiplier = this.VolumeDivisor = this.Panning = this.WaveDuty = this.RepeatMode = this.Format = 0;
		this.Enable = false;
	}
}

public enum LFOTarget : byte
{
	Pitch,
	Volume,
	Pan
}

public class LFOParam
{
	public LFOTarget Target { get; set; } = LFOTarget.Pitch;
	public byte Speed { get; set; } = 16;
	public byte Depth { get; set; }
	public byte Range { get; set; } = 1;
	public ushort Delay { get; set; }

	public void CopyTo(LFOParam other)
	{
		other.Target = this.Target;
		other.Speed = this.Speed;
		other.Depth = this.Depth;
		other.Range = this.Range;
		other.Delay = this.Delay;
	}
}

public class LFO
{
	public LFOParam Param { get; internal set; } = new();
	public ushort DelayCounter { get; set; }
	public ushort Counter { get; set; }

	/// <remarks>
	/// Original function: SND_StartLfo in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Start() => this.Counter = this.DelayCounter = 0;

	/// <remarks>
	/// Original function: SND_UpdateLfo in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Update()
	{
		if (this.DelayCounter < this.Param.Delay)
			++this.DelayCounter;
		else
		{
			uint tmp = this.Counter;
			tmp += (uint)(this.Param.Speed << 6);
			tmp >>= 8;
			while (tmp >= 0x80)
				tmp -= 0x80;
			this.Counter += (ushort)(this.Param.Speed << 6);
			this.Counter &= 0xFF;
			this.Counter |= (ushort)(tmp << 8);
		}
	}

	/// <remarks>
	/// Comes from sLfoSinTable in wram2.s of the Pokémon Diamond decompilation.
	/// </remarks>
	static readonly sbyte[] SinTable =
	[
		0, 6, 12, 19, 25, 31, 37, 43, 49, 54, 60, 65, 71, 76, 81, 85, 90, 94,
		98, 102, 106, 109, 112, 115, 117, 120, 122, 123, 125, 126, 126, 127, 127
	];

	/// <remarks>
	/// Original function: SND_SinIdx in SND_util.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="x"></param>
	/// <returns></returns>
	static sbyte SinIndex(int x) =>
		// BUG: UB for out of range values.
		x switch
		{
			< 0x20 => LFO.SinTable[x],
			< 0x40 => LFO.SinTable[0x40 - x],
			< 0x60 => (sbyte)-LFO.SinTable[x - 0x40],
			_ => (sbyte)-LFO.SinTable[0x20 - (x - 0x60)]
		};

	/// <remarks>
	/// Original function: SND_GetLfoValue in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int GetValue() => this.Param.Depth == 0 || this.DelayCounter < this.Param.Delay ? 0 :
		LFO.SinIndex((int)((uint)this.Counter >> 8)) * this.Param.Depth * this.Param.Range;
}

/// <summary>
/// The type of channel to allocate.
/// </summary>
public enum ChannelType : byte
{
	PCM,
	PSG,
	Noise
}

public enum ChannelState : byte
{
	Attack,
	Decay,
	Sustain,
	Release
}

[Flags]
public enum ChannelFlag : byte
{
	Active = 1 << 0,
	Start = 1 << 1,
	AutoSweep = 1 << 2
}

[Flags]
public enum ChannelSyncFlag : byte
{
	Stop = 1 << 0,
	Start = 1 << 1,
	Timer = 1 << 2,
	Volume = 1 << 3,
	Pan = 1 << 4
}

/// <summary>
/// SDAT - Channel. Base version common to both timing and playing.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Adapted from source code of the <see href="https://github.com/pret/pokediamond">Pokémon Diamond decompilation by pret</see>.
/// </para>
/// <para>
/// Some code/concepts from <see href="https://github.com/TASVideos/desmume/">DeSmuME</see>.
/// </para>
/// </remarks>
public abstract class Channel
{
	public byte Id { get; set; }
	ChannelType type;
	public ChannelState EnvelopeStatus { get; set; }

	public ChannelFlag Flags { get; set; }
	public ChannelSyncFlag SyncFlags { get; set; }

	public byte PanRange { get; set; }
	byte rootMidiKey;

	public byte MidiKey { get; set; }
	public byte Velocity { get; set; }
	sbyte initialPan;
	public sbyte UserPan { get; set; }

	public short UserDecay { get; set; }
	public short UserPitch { get; set; }

	int envelopeAttenuation;
	public int SweepCounter { get; set; }
	public int SweepLength { get; set; }

	byte envelopeAttack;
	byte envelopeSustain;
	ushort envelopeDecay;
	ushort envelopeRelease;
	public byte Priority { get; set; }
	byte pan;
	ushort volume;
	ushort timer;

	public LFO LFO { get; } = new();

	public short SweepPitch { get; set; }

	public int Length { get; set; }

	NC.SWAV? waveData;
	int dutyCycle;
	ushort waveTimer;

	public Action<Channel, bool>? Callback { get; set; }

	public Player Player { get; set; } = null!;
	public NDSSoundRegister Register { get; } = new();

	/// <remarks>
	/// Original function: SND_ExChannelInit in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="id"></param>
	public void Init(int id)
	{
		this.Id = (byte)id;
		this.SyncFlags = 0;
		this.Register.ClearControlRegister();
		this.Flags |= ChannelFlag.Active;
	}

	/// <remarks>
	/// Original function: SND_UpdateExChannel in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Update()
	{
		if (this.SyncFlags != 0)
		{
			if (this.SyncFlags.HasFlag(ChannelSyncFlag.Stop))
				// Basically what is done in SND_StopChannel in SND_channel.c of the Pokémon Diamond decompilation when hold is not set.
				this.Register.Enable = false;

			if (this.SyncFlags.HasFlag(ChannelSyncFlag.Start))
			{
				this.Register.ClearControlRegister();
				// All the below are from the below-used functions in SND_channel.c of the Pokémon Diamond decompilation.
				this.Register.Panning = this.pan;
				this.Register.VolumeMultiplier = (byte)(this.volume & 0xFF);
				this.Register.VolumeDivisor = (byte)(this.volume >> 8);

				switch (this.type)
				{
					case ChannelType.PCM:
						// Basically what is done in SND_SetupChannelPcm in SND_channel.c of the Pokémon Diamond decompilation.
						this.Register.Format = (byte)(this.waveData!.WaveType & 3);
						this.Register.RepeatMode = (byte)(this.waveData.Loop != 0 ? 1 : 2);
						this.Register.LoopStart = this.waveData.LoopOffset;
						this.Register.Length = this.waveData.LoopLength;
						// This is for my code and not from the decompilation.
						this.Register.TotalLength = this.Register.LoopStart + this.Register.Length;
						this.Register.Source = this.waveData;
						break;
					case ChannelType.PSG:
						// Basically what is done in SND_SetupChannelPsg in SND_channel.c of the Pokémon Diamond decompilation.
						this.Register.Format = 3;
						this.Register.WaveDuty = (byte)this.dutyCycle;
						break;
					case ChannelType.Noise:
						// Basically what is done in SND_SetupChannelNoise in SND_channel.c of the Pokémon Diamond decompilation.
						this.Register.Format = 3;
						break;
				}

				// The following is from the above-used functions in SND_channel.c of the Pokémon Diamond decompilation.
				this.Register.Timer = (ushort)(0x10000 - this.timer);

				this.Register.Enable = true;
				this.SyncFlags = 0;
			}
			else
			{
				if (this.SyncFlags.HasFlag(ChannelSyncFlag.Timer))
					// Basically what is done in SND_SetChannelTimer in SND_channel.c of the Pokémon Diamond decompilation.
					this.Register.Timer = (ushort)(0x10000 - this.timer);
				if (this.SyncFlags.HasFlag(ChannelSyncFlag.Volume))
				{
					// Basically what is done in SND_SetChannelVolume in SND_channel.c of the Pokémon Diamond decompilation.
					this.Register.VolumeMultiplier = (byte)(this.volume & 0xFF);
					this.Register.VolumeDivisor = (byte)(this.volume >> 8);
				}
				if (this.SyncFlags.HasFlag(ChannelSyncFlag.Pan))
					// Basically what is done in SND_SetChannelPan in SND_channel.c of the Pokémon Diamond decompilation.
					this.Register.Panning = this.pan;
			}
		}
	}

	const int SoundVolumeDBMin = -723;

	/// <remarks>
	/// Comes from DeSmuME.
	/// </remarks>
	static readonly byte[] GetVolumeTable =
	[
		0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
		0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
		0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
		0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03,
		0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03,
		0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
		0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
		0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05,
		0x05, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06, 0x06,
		0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x08, 0x08, 0x08, 0x08,
		0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09, 0x09,
		0x09, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B, 0x0B,
		0x0B, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0C, 0x0D, 0x0D, 0x0D, 0x0D, 0x0D, 0x0D, 0x0E,
		0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0E, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x10, 0x10, 0x10, 0x10, 0x10,
		0x10, 0x11, 0x11, 0x11, 0x11, 0x11, 0x12, 0x12, 0x12, 0x12, 0x12, 0x13, 0x13, 0x13, 0x13, 0x14,
		0x14, 0x14, 0x14, 0x14, 0x15, 0x15, 0x15, 0x15, 0x16, 0x16, 0x16, 0x16, 0x17, 0x17, 0x17, 0x18,
		0x18, 0x18, 0x18, 0x19, 0x19, 0x19, 0x19, 0x1A, 0x1A, 0x1A, 0x1B, 0x1B, 0x1B, 0x1C, 0x1C, 0x1C,
		0x1D, 0x1D, 0x1D, 0x1E, 0x1E, 0x1E, 0x1F, 0x1F, 0x1F, 0x20, 0x20, 0x20, 0x21, 0x21, 0x22, 0x22,
		0x22, 0x23, 0x23, 0x24, 0x24, 0x24, 0x25, 0x25, 0x26, 0x26, 0x27, 0x27, 0x27, 0x28, 0x28, 0x29,
		0x29, 0x2A, 0x2A, 0x2B, 0x2B, 0x2C, 0x2C, 0x2D, 0x2D, 0x2E, 0x2E, 0x2F, 0x2F, 0x30, 0x31, 0x31,
		0x32, 0x32, 0x33, 0x33, 0x34, 0x35, 0x35, 0x36, 0x36, 0x37, 0x38, 0x38, 0x39, 0x3A, 0x3A, 0x3B,
		0x3C, 0x3C, 0x3D, 0x3E, 0x3F, 0x3F, 0x40, 0x41, 0x42, 0x42, 0x43, 0x44, 0x45, 0x45, 0x46, 0x47,
		0x48, 0x49, 0x4A, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x52, 0x53, 0x54, 0x55,
		0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x67,
		0x68, 0x69, 0x6A, 0x6B, 0x6D, 0x6E, 0x6F, 0x71, 0x72, 0x73, 0x75, 0x76, 0x77, 0x79, 0x7A, 0x7B,
		0x7D, 0x7E, 0x7F, 0x20, 0x21, 0x21, 0x21, 0x22, 0x22, 0x23, 0x23, 0x23, 0x24, 0x24, 0x25, 0x25,
		0x26, 0x26, 0x26, 0x27, 0x27, 0x28, 0x28, 0x29, 0x29, 0x2A, 0x2A, 0x2B, 0x2B, 0x2C, 0x2C, 0x2D,
		0x2D, 0x2E, 0x2E, 0x2F, 0x2F, 0x30, 0x30, 0x31, 0x31, 0x32, 0x33, 0x33, 0x34, 0x34, 0x35, 0x36,
		0x36, 0x37, 0x37, 0x38, 0x39, 0x39, 0x3A, 0x3B, 0x3B, 0x3C, 0x3D, 0x3E, 0x3E, 0x3F, 0x40, 0x40,
		0x41, 0x42, 0x43, 0x43, 0x44, 0x45, 0x46, 0x47, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4D,
		0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D,
		0x5E, 0x5F, 0x60, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6F, 0x70,
		0x71, 0x73, 0x74, 0x75, 0x77, 0x78, 0x79, 0x7B, 0x7C, 0x7E, 0x7E, 0x40, 0x41, 0x42, 0x43, 0x43,
		0x44, 0x45, 0x46, 0x47, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51,
		0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61,
		0x62, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6B, 0x6C, 0x6D, 0x6E, 0x70, 0x71, 0x72, 0x74, 0x75,
		0x76, 0x78, 0x79, 0x7B, 0x7C, 0x7D, 0x7E, 0x40, 0x41, 0x42, 0x42, 0x43, 0x44, 0x45, 0x46, 0x46,
		0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55,
		0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x63, 0x65, 0x66,
		0x67, 0x68, 0x69, 0x6A, 0x6C, 0x6D, 0x6E, 0x6F, 0x71, 0x72, 0x73, 0x75, 0x76, 0x77, 0x79, 0x7A,
		0x7C, 0x7D, 0x7E, 0x7F
	];

	/// <remarks>
	/// Original function: SND_CalcChannelVolume in SND_util.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="value"></param>
	/// <returns></returns>
	public static ushort CalculateChannelVolume(int value)
	{
		value = int.Clamp(value, Channel.SoundVolumeDBMin, 0);

		return (ushort)(Channel.GetVolumeTable[value - Channel.SoundVolumeDBMin] | (value switch
		{
			< -240 => 3,
			< -120 => 2,
			< -60 => 1,
			_ => 0
		} << 8));
	}

	/// <remarks>
	/// Comes from DeSmuME.
	/// </remarks>
	static readonly ushort[] GetPitchTable =
	[
		0x0000, 0x003B, 0x0076, 0x00B2, 0x00ED, 0x0128, 0x0164, 0x019F,
		0x01DB, 0x0217, 0x0252, 0x028E, 0x02CA, 0x0305, 0x0341, 0x037D,
		0x03B9, 0x03F5, 0x0431, 0x046E, 0x04AA, 0x04E6, 0x0522, 0x055F,
		0x059B, 0x05D8, 0x0614, 0x0651, 0x068D, 0x06CA, 0x0707, 0x0743,
		0x0780, 0x07BD, 0x07FA, 0x0837, 0x0874, 0x08B1, 0x08EF, 0x092C,
		0x0969, 0x09A7, 0x09E4, 0x0A21, 0x0A5F, 0x0A9C, 0x0ADA, 0x0B18,
		0x0B56, 0x0B93, 0x0BD1, 0x0C0F, 0x0C4D, 0x0C8B, 0x0CC9, 0x0D07,
		0x0D45, 0x0D84, 0x0DC2, 0x0E00, 0x0E3F, 0x0E7D, 0x0EBC, 0x0EFA,
		0x0F39, 0x0F78, 0x0FB6, 0x0FF5, 0x1034, 0x1073, 0x10B2, 0x10F1,
		0x1130, 0x116F, 0x11AE, 0x11EE, 0x122D, 0x126C, 0x12AC, 0x12EB,
		0x132B, 0x136B, 0x13AA, 0x13EA, 0x142A, 0x146A, 0x14A9, 0x14E9,
		0x1529, 0x1569, 0x15AA, 0x15EA, 0x162A, 0x166A, 0x16AB, 0x16EB,
		0x172C, 0x176C, 0x17AD, 0x17ED, 0x182E, 0x186F, 0x18B0, 0x18F0,
		0x1931, 0x1972, 0x19B3, 0x19F5, 0x1A36, 0x1A77, 0x1AB8, 0x1AFA,
		0x1B3B, 0x1B7D, 0x1BBE, 0x1C00, 0x1C41, 0x1C83, 0x1CC5, 0x1D07,
		0x1D48, 0x1D8A, 0x1DCC, 0x1E0E, 0x1E51, 0x1E93, 0x1ED5, 0x1F17,
		0x1F5A, 0x1F9C, 0x1FDF, 0x2021, 0x2064, 0x20A6, 0x20E9, 0x212C,
		0x216F, 0x21B2, 0x21F5, 0x2238, 0x227B, 0x22BE, 0x2301, 0x2344,
		0x2388, 0x23CB, 0x240E, 0x2452, 0x2496, 0x24D9, 0x251D, 0x2561,
		0x25A4, 0x25E8, 0x262C, 0x2670, 0x26B4, 0x26F8, 0x273D, 0x2781,
		0x27C5, 0x280A, 0x284E, 0x2892, 0x28D7, 0x291C, 0x2960, 0x29A5,
		0x29EA, 0x2A2F, 0x2A74, 0x2AB9, 0x2AFE, 0x2B43, 0x2B88, 0x2BCD,
		0x2C13, 0x2C58, 0x2C9D, 0x2CE3, 0x2D28, 0x2D6E, 0x2DB4, 0x2DF9,
		0x2E3F, 0x2E85, 0x2ECB, 0x2F11, 0x2F57, 0x2F9D, 0x2FE3, 0x302A,
		0x3070, 0x30B6, 0x30FD, 0x3143, 0x318A, 0x31D0, 0x3217, 0x325E,
		0x32A5, 0x32EC, 0x3332, 0x3379, 0x33C1, 0x3408, 0x344F, 0x3496,
		0x34DD, 0x3525, 0x356C, 0x35B4, 0x35FB, 0x3643, 0x368B, 0x36D3,
		0x371A, 0x3762, 0x37AA, 0x37F2, 0x383A, 0x3883, 0x38CB, 0x3913,
		0x395C, 0x39A4, 0x39ED, 0x3A35, 0x3A7E, 0x3AC6, 0x3B0F, 0x3B58,
		0x3BA1, 0x3BEA, 0x3C33, 0x3C7C, 0x3CC5, 0x3D0E, 0x3D58, 0x3DA1,
		0x3DEA, 0x3E34, 0x3E7D, 0x3EC7, 0x3F11, 0x3F5A, 0x3FA4, 0x3FEE,
		0x4038, 0x4082, 0x40CC, 0x4116, 0x4161, 0x41AB, 0x41F5, 0x4240,
		0x428A, 0x42D5, 0x431F, 0x436A, 0x43B5, 0x4400, 0x444B, 0x4495,
		0x44E1, 0x452C, 0x4577, 0x45C2, 0x460D, 0x4659, 0x46A4, 0x46F0,
		0x473B, 0x4787, 0x47D3, 0x481E, 0x486A, 0x48B6, 0x4902, 0x494E,
		0x499A, 0x49E6, 0x4A33, 0x4A7F, 0x4ACB, 0x4B18, 0x4B64, 0x4BB1,
		0x4BFE, 0x4C4A, 0x4C97, 0x4CE4, 0x4D31, 0x4D7E, 0x4DCB, 0x4E18,
		0x4E66, 0x4EB3, 0x4F00, 0x4F4E, 0x4F9B, 0x4FE9, 0x5036, 0x5084,
		0x50D2, 0x5120, 0x516E, 0x51BC, 0x520A, 0x5258, 0x52A6, 0x52F4,
		0x5343, 0x5391, 0x53E0, 0x542E, 0x547D, 0x54CC, 0x551A, 0x5569,
		0x55B8, 0x5607, 0x5656, 0x56A5, 0x56F4, 0x5744, 0x5793, 0x57E2,
		0x5832, 0x5882, 0x58D1, 0x5921, 0x5971, 0x59C1, 0x5A10, 0x5A60,
		0x5AB0, 0x5B01, 0x5B51, 0x5BA1, 0x5BF1, 0x5C42, 0x5C92, 0x5CE3,
		0x5D34, 0x5D84, 0x5DD5, 0x5E26, 0x5E77, 0x5EC8, 0x5F19, 0x5F6A,
		0x5FBB, 0x600D, 0x605E, 0x60B0, 0x6101, 0x6153, 0x61A4, 0x61F6,
		0x6248, 0x629A, 0x62EC, 0x633E, 0x6390, 0x63E2, 0x6434, 0x6487,
		0x64D9, 0x652C, 0x657E, 0x65D1, 0x6624, 0x6676, 0x66C9, 0x671C,
		0x676F, 0x67C2, 0x6815, 0x6869, 0x68BC, 0x690F, 0x6963, 0x69B6,
		0x6A0A, 0x6A5E, 0x6AB1, 0x6B05, 0x6B59, 0x6BAD, 0x6C01, 0x6C55,
		0x6CAA, 0x6CFE, 0x6D52, 0x6DA7, 0x6DFB, 0x6E50, 0x6EA4, 0x6EF9,
		0x6F4E, 0x6FA3, 0x6FF8, 0x704D, 0x70A2, 0x70F7, 0x714D, 0x71A2,
		0x71F7, 0x724D, 0x72A2, 0x72F8, 0x734E, 0x73A4, 0x73FA, 0x7450,
		0x74A6, 0x74FC, 0x7552, 0x75A8, 0x75FF, 0x7655, 0x76AC, 0x7702,
		0x7759, 0x77B0, 0x7807, 0x785E, 0x78B4, 0x790C, 0x7963, 0x79BA,
		0x7A11, 0x7A69, 0x7AC0, 0x7B18, 0x7B6F, 0x7BC7, 0x7C1F, 0x7C77,
		0x7CCF, 0x7D27, 0x7D7F, 0x7DD7, 0x7E2F, 0x7E88, 0x7EE0, 0x7F38,
		0x7F91, 0x7FEA, 0x8042, 0x809B, 0x80F4, 0x814D, 0x81A6, 0x81FF,
		0x8259, 0x82B2, 0x830B, 0x8365, 0x83BE, 0x8418, 0x8472, 0x84CB,
		0x8525, 0x857F, 0x85D9, 0x8633, 0x868E, 0x86E8, 0x8742, 0x879D,
		0x87F7, 0x8852, 0x88AC, 0x8907, 0x8962, 0x89BD, 0x8A18, 0x8A73,
		0x8ACE, 0x8B2A, 0x8B85, 0x8BE0, 0x8C3C, 0x8C97, 0x8CF3, 0x8D4F,
		0x8DAB, 0x8E07, 0x8E63, 0x8EBF, 0x8F1B, 0x8F77, 0x8FD4, 0x9030,
		0x908C, 0x90E9, 0x9146, 0x91A2, 0x91FF, 0x925C, 0x92B9, 0x9316,
		0x9373, 0x93D1, 0x942E, 0x948C, 0x94E9, 0x9547, 0x95A4, 0x9602,
		0x9660, 0x96BE, 0x971C, 0x977A, 0x97D8, 0x9836, 0x9895, 0x98F3,
		0x9952, 0x99B0, 0x9A0F, 0x9A6E, 0x9ACD, 0x9B2C, 0x9B8B, 0x9BEA,
		0x9C49, 0x9CA8, 0x9D08, 0x9D67, 0x9DC7, 0x9E26, 0x9E86, 0x9EE6,
		0x9F46, 0x9FA6, 0xA006, 0xA066, 0xA0C6, 0xA127, 0xA187, 0xA1E8,
		0xA248, 0xA2A9, 0xA30A, 0xA36B, 0xA3CC, 0xA42D, 0xA48E, 0xA4EF,
		0xA550, 0xA5B2, 0xA613, 0xA675, 0xA6D6, 0xA738, 0xA79A, 0xA7FC,
		0xA85E, 0xA8C0, 0xA922, 0xA984, 0xA9E7, 0xAA49, 0xAAAC, 0xAB0E,
		0xAB71, 0xABD4, 0xAC37, 0xAC9A, 0xACFD, 0xAD60, 0xADC3, 0xAE27,
		0xAE8A, 0xAEED, 0xAF51, 0xAFB5, 0xB019, 0xB07C, 0xB0E0, 0xB145,
		0xB1A9, 0xB20D, 0xB271, 0xB2D6, 0xB33A, 0xB39F, 0xB403, 0xB468,
		0xB4CD, 0xB532, 0xB597, 0xB5FC, 0xB662, 0xB6C7, 0xB72C, 0xB792,
		0xB7F7, 0xB85D, 0xB8C3, 0xB929, 0xB98F, 0xB9F5, 0xBA5B, 0xBAC1,
		0xBB28, 0xBB8E, 0xBBF5, 0xBC5B, 0xBCC2, 0xBD29, 0xBD90, 0xBDF7,
		0xBE5E, 0xBEC5, 0xBF2C, 0xBF94, 0xBFFB, 0xC063, 0xC0CA, 0xC132,
		0xC19A, 0xC202, 0xC26A, 0xC2D2, 0xC33A, 0xC3A2, 0xC40B, 0xC473,
		0xC4DC, 0xC544, 0xC5AD, 0xC616, 0xC67F, 0xC6E8, 0xC751, 0xC7BB,
		0xC824, 0xC88D, 0xC8F7, 0xC960, 0xC9CA, 0xCA34, 0xCA9E, 0xCB08,
		0xCB72, 0xCBDC, 0xCC47, 0xCCB1, 0xCD1B, 0xCD86, 0xCDF1, 0xCE5B,
		0xCEC6, 0xCF31, 0xCF9C, 0xD008, 0xD073, 0xD0DE, 0xD14A, 0xD1B5,
		0xD221, 0xD28D, 0xD2F8, 0xD364, 0xD3D0, 0xD43D, 0xD4A9, 0xD515,
		0xD582, 0xD5EE, 0xD65B, 0xD6C7, 0xD734, 0xD7A1, 0xD80E, 0xD87B,
		0xD8E9, 0xD956, 0xD9C3, 0xDA31, 0xDA9E, 0xDB0C, 0xDB7A, 0xDBE8,
		0xDC56, 0xDCC4, 0xDD32, 0xDDA0, 0xDE0F, 0xDE7D, 0xDEEC, 0xDF5B,
		0xDFC9, 0xE038, 0xE0A7, 0xE116, 0xE186, 0xE1F5, 0xE264, 0xE2D4,
		0xE343, 0xE3B3, 0xE423, 0xE493, 0xE503, 0xE573, 0xE5E3, 0xE654,
		0xE6C4, 0xE735, 0xE7A5, 0xE816, 0xE887, 0xE8F8, 0xE969, 0xE9DA,
		0xEA4B, 0xEABC, 0xEB2E, 0xEB9F, 0xEC11, 0xEC83, 0xECF5, 0xED66,
		0xEDD9, 0xEE4B, 0xEEBD, 0xEF2F, 0xEFA2, 0xF014, 0xF087, 0xF0FA,
		0xF16D, 0xF1E0, 0xF253, 0xF2C6, 0xF339, 0xF3AD, 0xF420, 0xF494,
		0xF507, 0xF57B, 0xF5EF, 0xF663, 0xF6D7, 0xF74C, 0xF7C0, 0xF834,
		0xF8A9, 0xF91E, 0xF992, 0xFA07, 0xFA7C, 0xFAF1, 0xFB66, 0xFBDC,
		0xFC51, 0xFCC7, 0xFD3C, 0xFDB2, 0xFE28, 0xFE9E, 0xFF14, 0xFF8A
	];

	/// <remarks>
	/// Original function: SND_CalcTimer in SND_util.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="timer"></param>
	/// <param name="pitch"></param>
	/// <returns></returns>
	static ushort CalculateTimer(int timer, int pitch)
	{
		int octave = 0;
		int pitchNormalized = -pitch;

		while (pitchNormalized < 0)
		{
			--octave;
			pitchNormalized += 768;
		}

		while (pitchNormalized >= 768)
		{
			++octave;
			pitchNormalized -= 768;
		}

		ulong result = Channel.GetPitchTable[pitchNormalized];

		result += 0x10000;
		result *= (ulong)timer;

		int shift = octave - 16;

		if (shift <= 0)
			result >>= -shift;
		else if (shift < 32)
		{
			// clamp in case timer value overflows
			if ((result & (~0UL << (32 - shift))) != 0)
				return 0xFFFF;
			result <<= shift;
		}
		else
			return 0xFFFF;

		return (ushort)ulong.Clamp(result, 0x10, 0xFFFF);
	}

	/// <remarks>
	/// Comes from SNDi_DecibelSquareTable from wram2.s of the Pokémon Diamond decompilation.
	/// </remarks>
	static readonly short[] convertSustainLookupTable =
	[
		-32768, -722, -721, -651, -601, -562, -530, -503,
		-480, -460, -442, -425, -410, -396, -383, -371,
		-360, -349, -339, -330, -321, -313, -305, -297,
		-289, -282, -276, -269, -263, -257, -251, -245,
		-239, -234, -229, -224, -219, -214, -210, -205,
		-201, -196, -192, -188, -184, -180, -176, -173,
		-169, -165, -162, -158, -155, -152, -149, -145,
		-142, -139, -136, -133, -130, -127, -125, -122,
		-119, -116, -114, -111, -109, -106, -103, -101,
		-99, -96, -94, -91, -89, -87, -85, -82,
		-80, -78, -76, -74, -72, -70, -68, -66,
		-64, -62, -60, -58, -56, -54, -52, -50,
		-49, -47, -45, -43, -42, -40, -38, -36,
		-35, -33, -31, -30, -28, -27, -25, -23,
		-22, -20, -19, -17, -16, -14, -13, -11,
		-10, -8, -7, -6, -4, -3, -1, 0
	];

	public static short ConvertSustain(int sustain)
	{
		// This check isn't in the decompilation.
		if ((sustain & 0x80) != 0) // Supposedly invalid value...
			sustain = 0x7F; // Use apparently correct default.
		return Channel.convertSustainLookupTable[sustain];
	}

	/// <remarks>
	/// Original function: SND_ExChannelMain in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Main()
	{
		if (this.IsActive())
		{
			if (this.Flags.HasFlag(ChannelFlag.Start))
			{
				this.SyncFlags |= ChannelSyncFlag.Start;
				this.Flags &= ~ChannelFlag.Start;
				// The decomp doesn't do this, but not doing it leads to my player continuing to ask the channel for samples...
				this.Register.Enable = false;
			}
			// Check is basically what is done in SND_IsChannelActive in SND_channel.c of the Pokémon Diamond decompilation.
			else if (!this.Register.Enable)
			{
				this.Kill();
				return;
			}

			int vol = Channel.ConvertSustain(this.Velocity);
			int pitch = (this.MidiKey - this.rootMidiKey) * 0x40;

			vol += this.UpdateEnvelope();
			pitch += this.UpdateSweep();

			vol += this.UserDecay;
			pitch += this.UserPitch;

			int lfo = this.UpdateLFO();

			int pan = 0;

			switch (this.LFO.Param.Target)
			{
				case LFOTarget.Volume:
					if (vol > -0x8000)
						vol += lfo;
					break;
				case LFOTarget.Pan:
					pan += lfo;
					break;
				case LFOTarget.Pitch:
					pitch += lfo;
					break;
			}

			pan += this.initialPan;
			if (this.PanRange != 127)
				pan = (pan * this.PanRange + 0x40) >> 7;
			pan += this.UserPan;

			if (this.EnvelopeStatus == ChannelState.Release && vol <= -723)
			{
				this.SyncFlags = ChannelSyncFlag.Stop;
				this.Kill();
			}
			else
			{
				vol = Channel.CalculateChannelVolume(vol);
				ushort newTimer = Channel.CalculateTimer(this.waveTimer, pitch);

				if (this.type == ChannelType.PSG)
					newTimer &= 0xFFFC;

				pan += 0x40;
				pan = int.Clamp(pan, 0, 127);

				if (vol != this.volume)
				{
					this.volume = (ushort)vol;
					this.SyncFlags |= ChannelSyncFlag.Volume;
				}
				if (newTimer != this.timer)
				{
					this.timer = newTimer;
					// This is for my code and not from the decompilation.
					this.Register.SampleIncrease = Player.ARM7Clock / (this.Player.SampleRate * 2.0) / newTimer;
					this.SyncFlags |= ChannelSyncFlag.Timer;
				}
				if (pan != this.pan)
				{
					this.pan = (byte)pan;
					this.SyncFlags |= ChannelSyncFlag.Pan;
				}
			}
		}
	}

	/// <remarks>
	/// Original function: SND_StartExChannelPcm in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="wave"></param>
	/// <param name="length"></param>
	/// <returns></returns>
	public bool StartPCM(NC.SWAV wave, int length)
	{
		this.type = ChannelType.PCM;
		// This is for my code and not from the decompilation.
		this.Register.SamplePosition = wave.WaveType == 2 ? -11 : -3;
		this.waveData = wave;
		this.waveTimer = wave.Time;
		this.Start(length);
		return true;
	}

	/// <remarks>
	/// Original function: SND_StartExChannelPsg in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="duty"></param>
	/// <param name="length"></param>
	/// <returns></returns>
	public bool StartPSG(int duty, int length)
	{
		if (this.Id is < 8 or > 13)
			return false;
		else
		{
			this.type = ChannelType.PSG;
			// This is for my code and not from the decompilation.
			this.Register.SamplePosition = -1;
			this.dutyCycle = duty;
			this.waveTimer = 8006;
			this.Start(length);
			return true;
		}
	}

	/// <remarks>
	/// Original function: SND_StartExChannelNoise in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="length"></param>
	/// <returns></returns>
	public bool StartNoise(int length)
	{
		if (this.Id is < 14 or > 15)
			return false;
		else
		{
			this.type = ChannelType.Noise;
			// These next two lines are for my code and not from the decompilation.
			this.Register.SamplePosition = -1;
			this.Register.PSGX = 0x7FFF;
			this.waveTimer = 8006;
			this.Start(length);
			return true;
		}
	}

	/// <remarks>
	/// Original function: SND_UpdateExChannelEnvelope in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int UpdateEnvelope()
	{
		switch (this.EnvelopeStatus)
		{
			case ChannelState.Attack:
				this.envelopeAttenuation = -((-this.envelopeAttenuation * this.envelopeAttack) >> 8);
				if (this.envelopeAttenuation == 0)
					this.EnvelopeStatus = ChannelState.Decay;
				break;
			case ChannelState.Decay:
			{
				int sustain = Channel.ConvertSustain(this.envelopeSustain) << 7;
				this.envelopeAttenuation -= this.envelopeDecay;
				if (this.envelopeAttenuation <= sustain)
				{
					this.envelopeAttenuation = sustain;
					this.EnvelopeStatus = ChannelState.Sustain;
				}
				break;
			}
			case ChannelState.Release:
				this.envelopeAttenuation -= this.envelopeRelease;
				break;
		}

		return this.envelopeAttenuation >> 7;
	}

	/// <remarks>
	/// Comes from sAttackCoeffTable in wram2.s of the Pokémon Diamond decompilation.
	/// </remarks>
	static readonly byte[] AttackCoefficientTable =
	[
		0, 1, 5, 14, 26, 38, 51, 63, 73, 84,
		92, 100, 109, 116, 123, 127, 132, 137, 143
	];

	/// <remarks>
	/// Original function: SND_SetExChannelAttack in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="attack"></param>
	public void SetAttack(int attack) =>
		this.envelopeAttack = attack < 109 ? (byte)(255 - attack) : Channel.AttackCoefficientTable[127 - attack];

	/// <remarks>
	/// Original function: CalcDecayCoeff in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="vol"></param>
	/// <returns></returns>
	static ushort CalculateDecayCoefficient(int vol)
	{
		// This check isn't in the decompilation.
		if ((vol & 0x80) != 0) // Supposedly invalid value...
			vol = 0; // Use apparently correct default.
		return vol switch
		{
			127 => 0xFFFF,
			126 => 0x3C00,
			< 50 => (ushort)(vol * 2 + 1),
			_ => (ushort)(0x1E00 / (126 - vol))
		};
	}

	/// <remarks>
	/// Original function: SND_SetExChannelDecay in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="decay"></param>
	public void SetDecay(int decay) => this.envelopeDecay = Channel.CalculateDecayCoefficient(decay);

	/// <remarks>
	/// Original function: SND_SetExChannelSustain in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="sustain"></param>
	public void SetSustain(int sustain) => this.envelopeSustain = (byte)sustain;

	/// <remarks>
	/// Original function: SND_SetExChannelRelease in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="release"></param>
	public void SetRelease(int release) => this.envelopeRelease = Channel.CalculateDecayCoefficient(release);

	/// <remarks>
	/// Original function: SND_ReleaseExChannel in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Release() => this.EnvelopeStatus = ChannelState.Release;

	/// <remarks>
	/// Original function: SND_IsExChannelActive in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public bool IsActive() => this.Flags.HasFlag(ChannelFlag.Active);

	/// <remarks>
	/// Original function: SND_FreeExChannel in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Free() => this.Callback = null;

	/// <remarks>
	/// Original function: ExChannelSetup in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="callback"></param>
	/// <param name="priority"></param>
	public void Setup(Action<Channel, bool> callback, int priority)
	{
		this.Callback = callback;
		this.Length = this.SweepLength = this.SweepCounter = 0;
		this.Priority = (byte)priority;
		this.volume = 127;
		this.Flags &= ~ChannelFlag.Start;
		this.Flags |= ChannelFlag.AutoSweep;
		this.MidiKey = this.rootMidiKey = 60;
		this.Velocity = this.PanRange = 127;
		this.initialPan = this.UserPan = 0;
		this.UserDecay = this.UserPitch = this.SweepPitch = 0;

		this.SetAttack(127);
		this.SetSustain(127);
		this.SetDecay(127);
		this.SetRelease(127);
		this.LFO.Param = new();
	}

	/// <remarks>
	/// Original function: ExChannelStart in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="length"></param>
	public void Start(int length)
	{
		this.envelopeAttenuation = -92544;
		this.EnvelopeStatus = ChannelState.Attack;
		this.Length = length;
		this.LFO.Start();
		this.Flags |= ChannelFlag.Start | ChannelFlag.Active;
	}

	/// <remarks>
	/// Comes from sSampleDataShiftTable in wram2.s of the Pokémon Diamond decompilation.
	/// </remarks>
	static readonly byte[] SampleDataShiftTable = [0, 1, 2, 4];

	/// <remarks>
	/// Original function: ExChannelVolumeCmp in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="other"></param>
	/// <returns></returns>
	public int VolumeCompare(Channel other)
	{
		int volA = this.volume & 0xFF;
		int volB = other.volume & 0xFF;

		volA <<= 4;
		volB <<= 4;

		volA >>= Channel.SampleDataShiftTable[this.volume >> 8];
		volB >>= Channel.SampleDataShiftTable[other.volume >> 8];

		return volA != volB ? (volA < volB ? 1 : -1) : 0;
	}

	/// <remarks>
	/// Original function: ExChannelSweepUpdate in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int UpdateSweep()
	{
		long result = 0;

		if (this.SweepPitch != 0 && this.SweepCounter < this.SweepLength)
		{
			result = (long)this.SweepPitch * (this.SweepLength - this.SweepCounter) / this.SweepLength;

			if (this.Flags.HasFlag(ChannelFlag.AutoSweep))
				++this.SweepCounter;
		}

		return (int)result;
	}

	/// <remarks>
	/// Original function: ExChannelLfoUpdate in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int UpdateLFO()
	{
		long result = this.LFO.GetValue();

		if (result != 0)
		{
			switch (this.LFO.Param.Target)
			{
				case LFOTarget.Volume:
					result *= 60;
					break;
				case LFOTarget.Pitch:
				case LFOTarget.Pan:
					result <<= 6;
					break;
			}
			result >>= 14;
		}

		this.LFO.Update();

		return (int)result;
	}

	/// <remarks>
	/// Original function: SND_NoteOn in SND_bank.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="midiKey"></param>
	/// <param name="velocity"></param>
	/// <param name="length"></param>
	/// <param name="instData"></param>
	/// <returns></returns>
	public bool NoteOn(int midiKey, int velocity, int length, NC.SBNKInstrument instData)
	{
		byte release = instData.ReleaseRate;

		if (release == 0xFF)
		{
			length = -1;
			release = 0;
		}

		bool success = instData.Record switch
		{
			// SND_INST_PCM
			1 => this.StartPCM(this.Player.SWARs[instData.SWAR].SWAVs[instData.SWAV], length),
			// SND_INST_PSG
			2 => this.StartPSG(instData.SWAV, length),
			// SND_INST_NOISE
			3 => this.StartNoise(length),
			_ => false
		};

		if (success)
		{
			this.MidiKey = (byte)midiKey;
			this.rootMidiKey = instData.NoteNumber;
			this.Velocity = (byte)velocity;
			this.SetAttack(instData.AttackRate);
			this.SetSustain(instData.SustainLevel);
			this.SetDecay(instData.DecayRate);
			this.SetRelease(release);
			this.initialPan = (sbyte)(instData.Pan - 0x40);
			return true;
		}
		else
			return false;
	}

	public void Kill()
	{
		if (this.Callback is null)
			this.Priority = 0;
		else
			this.Callback(this, true);
		this.volume = 0;
		this.Flags &= ~ChannelFlag.Active;
	}

	/// <remarks>
	/// Comes from DeSmuME.
	/// </remarks>
	protected static readonly float[][] WaveDutyTable =
	[
		[-1, -1, -1, -1, -1, -1, -1, 1],
		[-1, -1, -1, -1, -1, -1, 1, 1],
		[-1, -1, -1, -1, -1, 1, 1, 1],
		[-1, -1, -1, -1, 1, 1, 1, 1],
		[-1, -1, -1, 1, 1, 1, 1, 1],
		[-1, -1, 1, 1, 1, 1, 1, 1],
		[-1, 1, 1, 1, 1, 1, 1, 1],
		[-1, -1, -1, -1, -1, -1, -1, -1]
	];

	public abstract float GenerateSample();

	public abstract void IncrementSample();
}
