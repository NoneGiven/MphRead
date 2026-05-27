using System.Buffers.Binary;

namespace NCSFCommon;

[Flags]
public enum TrackFlag : byte
{
	Active = 1 << 0,
	NoteWait = 1 << 1,
	Tie = 1 << 2,
	NoteFinishWait = 1 << 3,
	Portamento = 1 << 4,
	Compare = 1 << 5
}

public enum ValueType : byte
{
	U8,
	U16,
	VLV,
	Variable,
	Random
}

public abstract class Track
{
	protected const int MaxCall = 3;

	public TrackFlag Flags { get; set; }

	protected byte panRange;
	protected ushort program;

	protected byte volume;
	protected byte expression;
	protected sbyte pitchBend;
	protected byte bendRange;

	protected sbyte pan;
	protected byte envelopeAttack;
	protected byte envelopeDecay;
	protected byte envelopeSustain;
	protected byte envelopeRelease;
	protected byte priority;
	protected sbyte transpose;

	protected byte portamentoKey;
	protected byte portamentoTime;
	protected short sweepPitch;

	protected LFOParam modulation = new();

	protected int wait;

	protected ReadOnlyMemory<byte> @base;
	public int CurrentPos { get; set; }
	protected readonly int[] positionCallStack = new int[Track.MaxCall];
	protected readonly byte[] loopCount = new byte[Track.MaxCall];
	protected byte callStackDepth;

	public Player Player { get; set; } = null!;
	public byte Id { get; set; }
	public bool Mute { get; set; }
	protected readonly List<int> channels = [];

	/// <remarks>
	/// Original function: TrackReadU8 in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public byte ReadU8() => this.@base.Span[this.CurrentPos++];

	/// <remarks>
	/// Original function: TrackReadU16 in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public ushort ReadU16()
	{
		ushort result = BinaryPrimitives.ReadUInt16LittleEndian(this.@base[this.CurrentPos..].Span);
		this.CurrentPos += 2;
		return result;
	}

	/// <remarks>
	/// Original function: TrackReadU24 in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public uint ReadU24()
	{
		// Making a separate storage area for the bytes instead of using the bytes directly is to allow us to copy only 3 bytes and still
		// use the conversion for 32-bit integers.
		Span<byte> bytes = stackalloc byte[4];
		this.@base[this.CurrentPos..].Span[..3].CopyTo(bytes);
		bytes[3] = 0;
		uint result = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
		this.CurrentPos += 3;
		return result;
	}

	/// <remarks>
	/// Original function: TrackReadVLV in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int ReadVLV()
	{
		var span = this.@base[this.CurrentPos..].Span;
		int pos = 0;
		int retval = 0;
		int b;

		do
		{
			b = span[pos++];
			retval = (retval << 7) | (b & 0x7F);
		} while ((b & 0x80) != 0);

		this.CurrentPos += pos;
		return retval;
	}

	static uint RandomU = 0x12345678;

	/// <remarks>
	/// Original function: SND_CalcRandom in SND_util.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	protected static ushort CalculateRandom()
	{
		Track.RandomU = Track.RandomU * 1664525 + 1013904223;
		return (ushort)(Track.RandomU >> 16);
	}

	/// <remarks>
	/// Original function: TrackParseValue in SND_seq.c of the Pokeémon Diamond decompilation.
	/// </remarks>
	/// <param name="valueType"></param>
	/// <returns></returns>
	public int ParseValue(ValueType valueType)
	{
		// BUG: undefined behavior if invalid valueType is passed (uninitialized return value).

		switch (valueType)
		{
			case ValueType.U8:
				return this.ReadU8();
			case ValueType.U16:
				return this.ReadU16();
			case ValueType.VLV:
				return this.ReadVLV();
			case ValueType.Variable:
				return this.Player.Variables[this.ReadU8()];
			case ValueType.Random:
			{
				int lo = this.ReadU16() << 16;
				int hi = (short)this.ReadU16();
				int ran = Track.CalculateRandom();
				int retval = hi - (lo >> 16);
				++retval;
				retval = (ran * retval) >> 16;
				retval += lo >> 16;
				return retval;
			}
		}

		return 0;
	}

	/// <remarks>
	/// Original function: TrackInit in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public virtual void Init()
	{
		this.@base = null;
		this.CurrentPos = -1;

		this.Flags |= TrackFlag.NoteWait | TrackFlag.Compare;
		this.Flags &= ~TrackFlag.Tie & ~TrackFlag.NoteFinishWait & ~TrackFlag.Portamento;

		this.callStackDepth = this.portamentoTime = 0;
		this.program = 0;
		this.priority = 64;
		this.volume = this.expression = this.panRange = 127;
		this.pan = this.pitchBend = this.transpose = 0;
		this.envelopeAttack = this.envelopeDecay = this.envelopeSustain = this.envelopeRelease = 255;
		this.bendRange = 2;
		this.portamentoKey = 60;
		this.sweepPitch = 0;
		this.modulation = new();
		this.wait = 0;
		this.channels.Clear();
	}

	/// <remarks>
	/// Original function: TrackStart in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="data"></param>
	/// <param name="offset"></param>
	public void Start(ReadOnlyMemory<byte> data, int offset)
	{
		this.@base = data;
		this.CurrentPos = offset;
	}

	/// <remarks>
	/// Original function: TrackReleaseChannels in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="release"></param>
	public void ReleaseChannels(int release)
	{
		this.UpdateChannel(false);

		var playerChannels = this.Player.Channels;
		foreach (int channelId in this.channels)
		{
			var channel = playerChannels[channelId];

			if (channel.IsActive())
			{
				if (release >= 0)
					channel.SetRelease(release & 0xFF);
				channel.Priority = 1;
				channel.Release();
			}
		}
	}

	/// <remarks>
	/// Original function: TrackFreeChannels in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void FreeChannels()
	{
		var playerChannels = this.Player.Channels;
		foreach (int channelId in this.channels)
			playerChannels[channelId].Free();
		this.channels.Clear();
	}

	/// <remarks>
	/// Original function: TrackStop in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Stop()
	{
		this.CurrentPos = -1;
		this.ReleaseChannels(-1);
		this.FreeChannels();
	}

	/// <remarks>
	/// Original function: ChannelCallback in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="channel"></param>
	/// <param name="free"></param>
	void ChannelCallback(Channel channel, bool free)
	{
		if (free)
		{
			channel.Priority = 0;
			channel.Free();
		}

		_ = this.channels.Remove(channel.Id);
	}

	/// <remarks>
	/// Original function: TrackUpdateChannel in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="release"></param>
	public void UpdateChannel(bool release)
	{
		// The sseqVol portion I added in based on what was in FSS.
		int vol = this.Mute ? -0x8000 : Channel.ConvertSustain(this.volume) + Channel.ConvertSustain(this.expression) +
			Channel.ConvertSustain(this.Player.Volume) + this.Player.SSEQVolume;

		int pitch = this.pitchBend;
		pitch *= this.bendRange << 6;
		pitch >>= 7;

		int pan = this.pan;

		if (this.panRange != 127)
			pan = (pan * this.panRange + 0x40) >> 7;

		if (vol < -0x8000)
			vol = -0x8000;

		pan = int.Clamp(pan, -128, 127);

		var playerChannels = this.Player.Channels;
		foreach (int channelId in this.channels)
		{
			var channel = playerChannels[channelId];

			if (channel.EnvelopeStatus != ChannelState.Release)
			{
				channel.UserDecay = (short)vol;
				channel.UserPitch = (short)pitch;
				channel.UserPan = (sbyte)pan;
				channel.PanRange = this.panRange;
				this.modulation.CopyTo(channel.LFO.Param);

				if (channel.Length == 0 && release)
				{
					channel.Priority = 1;
					channel.Release();
				}
			}
		}
	}

	/// <remarks>
	/// Original function: SND_ReadInstData in SND_bank.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="program"></param>
	/// <param name="midiKey"></param>
	/// <returns></returns>
	public NC.SBNKInstrument? ReadInstrumentData(int program, int midiKey)
	{
		var sbnk = this.Player.SBNK;

		var sbnkEntries = sbnk.Entries;
		if (program < 0 || program >= sbnkEntries.Length)
			return null;

		var entry = sbnkEntries[program];

		var instruments = entry.Instruments;
		switch (entry.Record)
		{
			case 1: // SND_INST_PCM
			case 2: // SND_INST_PSG
			case 3: // SND_INST_NOISE
			case 5: // SND_INST_DUMMY
				return instruments[0];
			case 16: // SND_INST_DRUM_TABLE
				return midiKey < instruments[0].LowNote || midiKey > instruments[^1].HighNote ? null :
					instruments[midiKey - instruments[0].LowNote];
			case 17: // SND_INST_KEY_SPLIT
			{
				int reg, entries;
				for (reg = 0, entries = instruments.Length; reg < entries; ++reg)
					if (midiKey <= instruments[reg].HighNote)
						break;
				return reg == entries ? null : instruments[reg];
			}
			default:
				return null;
		}
	}

	/// <remarks>
	/// Original function: TrackPlayNote in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="midiKey"></param>
	/// <param name="velocity"></param>
	/// <param name="length"></param>
	public void PlayNote(int midiKey, int velocity, int length)
	{
		Channel? channel = null;

		if (this.Flags.HasFlag(TrackFlag.Tie) && this.channels.Count != 0)
		{
			channel = this.Player.Channels[this.channels[0]];
			channel.MidiKey = (byte)midiKey;
			channel.Velocity = (byte)velocity;
		}

		if (channel is null)
		{
			var noteDef = this.ReadInstrumentData(this.program, midiKey);
			if (noteDef is null)
				return;

			uint allowedChannels;

			// Get bitmask with allocatable channels based on channel type.
			switch (noteDef.Record)
			{
				case 1: // SND_INST_PCM
					// All channels support PCM.
					allowedChannels = 0xFFFF;
					break;
				case 2: // SND_INST_PSG
					// Only channels 8, 9, 10, 11, 12, 13 support PSG.
					allowedChannels = 0x3F00;
					break;
				case 3: // SND_INST_NOISE
					// Only channels 14 and 15 support noise.
					allowedChannels = 0xC000;
					break;
				default:
					return;
			}

			allowedChannels &= this.Player.ChannelMask;

			channel = this.Player.AllocateChannel(allowedChannels, this.Player.Priority + this.priority, this.ChannelCallback);
			if (channel is null)
				return;

			if (!channel.NoteOn(midiKey, velocity, this.Flags.HasFlag(TrackFlag.Tie) ? -1 : length, noteDef))
			{
				channel.Priority = 0;
				channel.Free();
				return;
			}

			this.channels.Insert(0, channel.Id);
		}

		if (this.envelopeAttack != 0xFF)
			channel.SetAttack(this.envelopeAttack);

		if (this.envelopeDecay != 0xFF)
			channel.SetDecay(this.envelopeDecay);

		if (this.envelopeSustain != 0xFF)
			channel.SetSustain(this.envelopeSustain);

		if (this.envelopeRelease != 0xFF)
			channel.SetRelease(this.envelopeRelease);

		channel.SweepPitch = this.sweepPitch;
		if (this.Flags.HasFlag(TrackFlag.Portamento))
			channel.SweepPitch += (short)((this.portamentoKey - midiKey) << 6);

		if (this.portamentoTime != 0)
		{
			int swp = this.portamentoTime * this.portamentoTime;
			swp *= channel.SweepPitch < 0 ? -channel.SweepPitch : channel.SweepPitch;
			swp >>= 11;
			channel.SweepLength = swp;
		}
		else
		{
			channel.SweepLength = length;
			channel.Flags &= ~ChannelFlag.AutoSweep;
		}

		channel.SweepCounter = 0;
	}

	protected internal enum SSEQCommand : byte
	{
		AllocateTrack = 0xFE, // Silently ignored
		OpenTrack = 0x93,

		Rest = 0x80,
		Patch = 0x81,
		Pan = 0xC0,
		Volume = 0xC1,
		MasterVolume = 0xC2,
		Priority = 0xC6,
		NoteWait = 0xC7,
		Tie = 0xC8,
		Expression = 0xD5,
		Tempo = 0xE1,
		End = 0xFF,

		Goto = 0x94,
		Call = 0x95,
		Return = 0xFD,
		LoopStart = 0xD4,
		LoopEnd = 0xFC,

		Transpose = 0xC3,
		PitchBend = 0xC4,
		PitchBendRange = 0xC5,

		Attack = 0xD0,
		Decay = 0xD1,
		Sustain = 0xD2,
		Release = 0xD3,

		PortamentoKey = 0xC9,
		PortamentoFlag = 0xCE,
		PortamentoTime = 0xCF,
		SweepPitch = 0xE3,

		ModulationDepth = 0xCA,
		ModulationSpeed = 0xCB,
		ModulationType = 0xCC,
		ModulationRange = 0xCD,
		ModulationDelay = 0xE0,

		Random = 0xA0,
		PrintVariable = 0xD6, // Silently ignored
		If = 0xA2,
		FromVariable = 0xA1,
		SetVariable = 0xB0,
		AddVariable = 0xB1,
		SubtractVariable = 0xB2,
		MultiplyVariable = 0xB3,
		DivideVariable = 0xB4,
		ShiftVariable = 0xB5,
		RandomizeVariable = 0xB6,
		CompareEquals = 0xB8,
		CompareGreaterThanOrEquals = 0xB9,
		CompareGreaterThan = 0xBA,
		CompareLessThanOrEquals = 0xBB,
		CompareLessThan = 0xBC,
		CompareNotEquals = 0xBD,

		Mute = 0xD7 // Unsupported
	}

	/// <remarks>
	/// Original function: TrackStepTicks in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public abstract bool StepTicks();
}
