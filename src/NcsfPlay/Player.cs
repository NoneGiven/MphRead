using CommunityToolkit.Diagnostics;

namespace NCSFCommon;

/// <summary>
/// SDAT - Player. Base version common to both timing and playing.
/// </summary>
/// <remarks>
/// By Naram Qashat (CyberBotX) [cyberbotx@cyberbotx.com]
/// <para>
/// Adapted from source code of the <see href="https://github.com/pret/pokediamond">Pokémon Diamond decompilation by pret</see>.
/// </para>
/// </remarks>
public abstract class Player
{
	protected const int TrackCount = 16;
	const byte InvalidTrackIndex = 0xFF;
	protected const ushort TimerRate = 240;
	public const int ChannelCount = 16;

	public const uint ARM7Clock = 33514000;
	public const float SecondsPerClockCycle = 64 * 2728.0f / Player.ARM7Clock;

	public byte Priority { get; set; } = 64;
	public byte Volume { get; set; } = 0x7F;

	readonly byte[] trackIds = [.. Enumerable.Repeat(Player.InvalidTrackIndex, Player.TrackCount)];

	public ushort Tempo { get; set; } = 120;
	protected ushort tempoRatio = 256;
	protected ushort tempoCounter = Player.TimerRate;

	#region Originally part of SNDWork and SNDSharedWork

	readonly short[] variables = [.. Enumerable.Repeat((short)-1, 32)];
	public ReadOnlySpan<short> Variables => this.variables.AsSpan();
	protected abstract Track[] tracks { get; }

	public abstract uint SampleRate { get; set; }

	#endregion

	/// <summary>
	/// The <see cref="NC.SBNK" /> being used by this player.
	/// </summary>
	public NC.SBNK SBNK { get; set; } = null!;
	readonly NC.SWAR[] swars = [.. Enumerable.Range(0, 4).Select(static _ => (null as NC.SWAR)!)];
	/// <summary>
	/// The <see cref="NC.SWAR" />s being used by this player.
	/// </summary>
	public ReadOnlySpan<NC.SWAR> SWARs => this.swars.AsSpan();
	/// <summary>
	/// The volume associated with the SSEQ specifically.
	/// </summary>
	public short SSEQVolume { get; set; }

	protected abstract Channel[] channels { get; }
	public ReadOnlySpan<Channel> Channels => this.channels.AsSpan();
	public ushort ChannelMask { get; set; }

	/// <remarks>
	/// Comes from sChannelAllocationOrder in wram2.s of the Pokémon Diamond decompilation.
	/// </remarks>
	static readonly byte[] ChannelAllocationOrder = [4, 5, 6, 7, 2, 0, 3, 1, 8, 9, 10, 11, 14, 12, 15, 13];

	/// <remarks>
	/// Original function: SND_AllocExChannel in SND_exChannel.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="channelMask"></param>
	/// <param name="priority"></param>
	/// <param name="callback"></param>
	/// <returns></returns>
	public Channel? AllocateChannel(uint channelMask, int priority, Action<Channel, bool> callback)
	{
		Channel? chnPrev = null;

		foreach (byte channelCandidate in Player.ChannelAllocationOrder)
			if ((channelMask & (1 << channelCandidate)) != 0)
			{
				var chn = this.channels[channelCandidate];

				if (chnPrev is null ||
					(chn.Priority <= chnPrev.Priority && (chn.Priority != chnPrev.Priority || chnPrev.VolumeCompare(chn) < 0)))
					chnPrev = chn;
			}

		if (chnPrev is null || priority < chnPrev.Priority)
			return null;

		chnPrev.Callback?.Invoke(chnPrev, false);

		chnPrev.SyncFlags = ChannelSyncFlag.Stop;
		chnPrev.Flags &= ~ChannelFlag.Active;
		chnPrev.Setup(callback, priority);
		return chnPrev;
	}

	/// <remarks>
	/// Original function: SND_SeqMain in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public abstract void SequenceMain();

	/// <remarks>
	/// Original function: SND_PrepareSeq in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="sseq"></param>
	/// <param name="offset"></param>
	/// <param name="sseqVol"></param>
	public void PrepareSequence(NC.SSEQ sseq, int offset, short sseqVol)
	{
		this.Stop();

		this.Init(sseqVol);

		int allocateTrackIndex = this.AllocateTrack();

		if (allocateTrackIndex >= 0)
		{
			var trk = this.tracks[allocateTrackIndex];
			trk.Init();
			trk.Start(sseq.Data, offset);
			this.trackIds[0] = (byte)allocateTrackIndex;

			byte cmd = trk.ReadU8();

			if (cmd == Track.SSEQCommand.AllocateTrack.ToByte())
			{
				int track;
				ushort trackMask;

				for (trackMask = (ushort)(trk.ReadU16() >> 1), track = 1; trackMask != 0; ++track, trackMask >>= 1)
					if ((trackMask & 1) != 0)
					{
						allocateTrackIndex = this.AllocateTrack();
						if (allocateTrackIndex < 0)
							break;
						this.tracks[allocateTrackIndex].Init();
						this.trackIds[track] = (byte)allocateTrackIndex;
					}
			}
			else
				--trk.CurrentPos;
		}
	}

	/// <remarks>
	/// Original function: PlayerInit in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="sseqVol"></param>
	public void Init(short sseqVol)
	{
		this.Tempo = 120;
		this.tempoRatio = 256;
		this.tempoCounter = Player.TimerRate;
		this.Volume = 0x7F;
		this.Priority = 64;

		this.trackIds.AsSpan().Fill(Player.InvalidTrackIndex);

		this.variables.AsSpan().Fill(-1);
		// Aside from the comment inside this loop, everything from this point on is done for my player specifically
		// and is not from the Pokémon Diamond decompilation.
		for (int i = 0; i < Player.TrackCount; ++i)
		{
			this.tracks[i].Id = (byte)i;
			this.tracks[i].Player = this;
			// This is done in SND_SeqInit in SND_seq.c of the Pokémon Diamond decompilation.
			this.tracks[i].Flags &= ~TrackFlag.Active;
		}

		this.SSEQVolume = sseqVol;

		for (int i = 0; i < Player.ChannelCount; ++i)
		{
			this.channels[i].Player = this;
			this.channels[i].Init(i);
		}
	}

	/// <remarks>
	/// Original function: PlayerSeqMain in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public virtual void Main()
	{
		int ticks = 0;

		while (this.tempoCounter >= Player.TimerRate)
		{
			this.tempoCounter -= Player.TimerRate;
			++ticks;
		}

		for (int i = 0; i < ticks; ++i)
			this.StepTicks();

		int tempoIncrease = this.Tempo;
		tempoIncrease *= this.tempoRatio;
		tempoIncrease >>= 8;

		this.tempoCounter += (ushort)tempoIncrease;
	}

	/// <remarks>
	/// Original function: PlayerGetTrack in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="track"></param>
	/// <returns></returns>
	public Track? GetTrack(int track) =>
		track > Player.TrackCount - 1 || this.trackIds[track] == Player.InvalidTrackIndex ? null : this.tracks[this.trackIds[track]];

	/// <remarks>
	/// Original function: PlayerStopTrack in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <param name="trackIndex"></param>
	public void StopTrack(int trackIndex)
	{
		var track = this.GetTrack(trackIndex);

		if (track is not null)
		{
			track.Stop();
			track.Flags &= ~TrackFlag.Active;
			this.trackIds[trackIndex] = Player.InvalidTrackIndex;
		}
	}

	/// <remarks>
	/// Original function: PlayerStop in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void Stop()
	{
		for (int i = 0; i < Player.TrackCount; ++i)
			this.StopTrack(i);
	}

	/// <remarks>
	/// Original function: PlayerUpdateChannel in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public void UpdateChannel()
	{
		for (int i = 0; i < Player.TrackCount; ++i)
			this.GetTrack(i)?.UpdateChannel(true);
	}

	/// <remarks>
	/// Original function: PlayerStepTicks in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	public abstract void StepTicks();

	/// <remarks>
	/// Original function: AllocateTrack in SND_seq.c of the Pokémon Diamond decompilation.
	/// </remarks>
	/// <returns></returns>
	public int AllocateTrack()
	{
		for (int i = 0; i < Player.TrackCount; ++i)
			if (!this.tracks[i].Flags.HasFlag(TrackFlag.Active))
			{
				this.tracks[i].Flags |= TrackFlag.Active;
				return i;
			}
		return -1;
	}

	/// <summary>
	/// Sets the variable for the given index.
	/// </summary>
	/// <param name="variableNumber">The index of the variable to set.</param>
	/// <param name="value">The value for the variable.</param>
	public void SetVariable(sbyte variableNumber, short value)
	{
		Guard.IsBetweenOrEqualTo(variableNumber, (sbyte)0, (sbyte)31);

		this.variables[variableNumber] = value;
	}

	/// <summary>
	/// Sets the SWAR for the given index.
	/// </summary>
	/// <param name="index">The index of the SWAR to set.</param>
	/// <param name="swar">The <see cref="NC.SWAR" /> to set.</param>
	public void SetSWAR(int index, NC.SWAR swar)
	{
		Guard.IsBetweenOrEqualTo(index, 0, 3);

		this.swars[index] = swar;
	}

	/// <remarks>
	/// Comes from DeSmuME.
	/// </remarks>
	/// <param name="val"></param>
	/// <param name="mul"></param>
	/// <returns></returns>
	public static float MulDiv7(float val, byte mul) => mul == 127 ? val : val * mul * 0.0078125f;
}
