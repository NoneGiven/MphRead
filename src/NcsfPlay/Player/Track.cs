using NCSFCommon;
using ValueType = NCSFCommon.ValueType;

namespace NCSFPlayer;

public class Track : NCSFCommon.Track
{
	public override bool StepTicks()
	{
		var playerChannels = this.Player.Channels;
		foreach (int channelId in this.channels)
		{
			var channel = playerChannels[channelId];

			if (channel.Length > 0)
				--channel.Length;

			if (!channel.Flags.HasFlag(ChannelFlag.AutoSweep) && channel.SweepCounter < channel.SweepLength)
				++channel.SweepCounter;
		}

		if (this.Flags.HasFlag(TrackFlag.NoteFinishWait))
		{
			if (this.channels.Count != 0)
				return true;
			this.Flags &= ~TrackFlag.NoteFinishWait;
		}

		if (this.wait > 0 && --this.wait > 0)
			return true;

		while (this.wait == 0 && !this.Flags.HasFlag(TrackFlag.NoteFinishWait))
		{
			bool runCmd = true;
			ValueType? valueType = null;

			byte cmd = this.ReadU8();

			if (cmd == SSEQCommand.If.ToByte())
			{
				cmd = this.ReadU8();
				runCmd = this.Flags.HasFlag(TrackFlag.Compare);
			}

			if (cmd == SSEQCommand.Random.ToByte())
			{
				cmd = this.ReadU8();
				valueType = ValueType.Random;
			}

			if (cmd == SSEQCommand.FromVariable.ToByte())
			{
				cmd = this.ReadU8();
				valueType = ValueType.Variable;
			}

			if ((cmd & 0x80) == 0)
			{
				int par = this.ReadU8();

				int length = this.ParseValue(valueType ?? ValueType.VLV);

				if (runCmd)
				{
					int midiKey = int.Clamp(cmd + this.transpose, 0, 127);

					this.PlayNote(midiKey, par, length > 0 ? length : -1);

					this.portamentoKey = (byte)midiKey;

					if (this.Flags.HasFlag(TrackFlag.NoteWait))
					{
						this.wait = length;
						if (length == 0)
							this.Flags |= TrackFlag.NoteFinishWait;
					}
				}

				continue;
			}

			switch (cmd & 0xF0)
			{
				case 0x80:
				{
					int par = this.ParseValue(valueType ?? ValueType.VLV);
					if (runCmd)
						switch (cmd.ToEnum<SSEQCommand>())
						{
							case SSEQCommand.Rest:
								this.wait = par;
								break;
							case SSEQCommand.Patch:
								if (par < 0x10000)
									this.program = (ushort)par;
								break;
						}
					break;
				}
				case 0x90:
					switch (cmd.ToEnum<SSEQCommand>())
					{
						case SSEQCommand.OpenTrack:
						{
							int par = this.ReadU8();
							uint off = this.ReadU24();
							if (runCmd)
							{
								var newTrack = this.Player.GetTrack(par);
								if (newTrack is not null && newTrack != this)
								{
									newTrack.Stop();
									newTrack.Start(this.@base, (int)off);
								}
							}
							break;
						}
						case SSEQCommand.Goto:
						{
							uint off = this.ReadU24();
							if (runCmd)
								this.CurrentPos = (int)off;
							break;
						}
						case SSEQCommand.Call:
						{
							uint off = this.ReadU24();
							if (runCmd && this.callStackDepth < Track.MaxCall)
							{
								this.positionCallStack[this.callStackDepth++] = this.CurrentPos;
								this.CurrentPos = (int)off;
							}
							break;
						}
					}
					break;
				case 0xC0:
				case 0xD0:
				{
					byte par = (byte)this.ParseValue(valueType ?? ValueType.U8);
					if (runCmd)
						switch (cmd.ToEnum<SSEQCommand>())
						{
							case SSEQCommand.Volume:
								this.volume = par;
								break;
							case SSEQCommand.Expression:
								this.expression = par;
								break;
							case SSEQCommand.MasterVolume:
								this.Player.Volume = par;
								break;
							case SSEQCommand.PitchBendRange:
								this.bendRange = par;
								break;
							case SSEQCommand.Priority:
								this.priority = par;
								break;
							case SSEQCommand.NoteWait:
								if (par != 0)
									this.Flags |= TrackFlag.NoteWait;
								else
									this.Flags &= ~TrackFlag.NoteWait;
								break;
							case SSEQCommand.PortamentoTime:
								this.portamentoTime = par;
								break;
							case SSEQCommand.ModulationDepth:
								this.modulation.Depth = par;
								break;
							case SSEQCommand.ModulationSpeed:
								this.modulation.Speed = par;
								break;
							case SSEQCommand.ModulationType:
								this.modulation.Target = par.ToEnum<LFOTarget>();
								break;
							case SSEQCommand.ModulationRange:
								this.modulation.Range = par;
								break;
							case SSEQCommand.Attack:
								this.envelopeAttack = par;
								break;
							case SSEQCommand.Decay:
								this.envelopeDecay = par;
								break;
							case SSEQCommand.Sustain:
								this.envelopeSustain = par;
								break;
							case SSEQCommand.Release:
								this.envelopeRelease = par;
								break;
							case SSEQCommand.LoopStart:
								if (this.callStackDepth < Track.MaxCall)
								{
									this.positionCallStack[this.callStackDepth] = this.CurrentPos;
									this.loopCount[this.callStackDepth++] = par;
								}
								break;
							case SSEQCommand.Tie:
								if (par != 0)
									this.Flags |= TrackFlag.Tie;
								else
									this.Flags &= ~TrackFlag.Tie;
								this.ReleaseChannels(-1);
								this.FreeChannels();
								break;
							case SSEQCommand.PortamentoKey:
								this.portamentoKey = (byte)(par + this.transpose);
								this.Flags |= TrackFlag.Portamento;
								break;
							case SSEQCommand.PortamentoFlag:
								if (par != 0)
									this.Flags |= TrackFlag.Portamento;
								else
									this.Flags &= ~TrackFlag.Portamento;
								break;
							case SSEQCommand.Transpose:
								this.transpose = (sbyte)par;
								break;
							case SSEQCommand.PitchBend:
								this.pitchBend = (sbyte)par;
								break;
							case SSEQCommand.Pan:
								this.pan = (sbyte)(par - 0x40);
								break;
						}
					break;
				}
				case 0xE0:
				{
					short par = (short)this.ParseValue(valueType ?? ValueType.U16);
					if (runCmd)
						switch (cmd.ToEnum<SSEQCommand>())
						{
							case SSEQCommand.SweepPitch:
								this.sweepPitch = par;
								break;
							case SSEQCommand.Tempo:
								this.Player.Tempo = (ushort)par;
								break;
							case SSEQCommand.ModulationDelay:
								this.modulation.Delay = (ushort)par;
								break;
						}
					break;
				}
				case 0xB0:
				{
					int varNum = this.ReadU8();

					short par = (short)this.ParseValue(valueType ?? ValueType.U16);
					if (runCmd)
					{
						short var = this.Player.Variables[varNum];
						switch (cmd.ToEnum<SSEQCommand>())
						{
							case SSEQCommand.SetVariable:
								var = par;
								break;
							case SSEQCommand.AddVariable:
								var += par;
								break;
							case SSEQCommand.SubtractVariable:
								var -= par;
								break;
							case SSEQCommand.MultiplyVariable:
								var *= par;
								break;
							case SSEQCommand.DivideVariable:
								if (par != 0)
									var /= par;
								break;
							case SSEQCommand.ShiftVariable:
								if (par >= 0)
									var <<= par;
								else
									var >>= -par;
								break;
							case SSEQCommand.RandomizeVariable:
							{
								bool neg = false;
								if (par < 0)
								{
									neg = true;
									par = (short)-par;
								}
								int random = Track.CalculateRandom();
								random = (random * (par + 1)) >> 16;
								if (neg)
									random = -random;
								var = (short)random;
								break;
							}
							case SSEQCommand.CompareEquals:
								if (var == par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
							case SSEQCommand.CompareGreaterThanOrEquals:
								if (var >= par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
							case SSEQCommand.CompareGreaterThan:
								if (var > par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
							case SSEQCommand.CompareLessThanOrEquals:
								if (var <= par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
							case SSEQCommand.CompareLessThan:
								if (var < par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
							case SSEQCommand.CompareNotEquals:
								if (var != par)
									this.Flags |= TrackFlag.Compare;
								else
									this.Flags &= ~TrackFlag.Compare;
								break;
						}
						this.Player.SetVariable((sbyte)varNum, var);
					}
					break;
				}
				case 0xF0:
					if (runCmd)
						switch (cmd.ToEnum<SSEQCommand>())
						{
							case SSEQCommand.Return:
								if (this.callStackDepth != 0)
									this.CurrentPos = this.positionCallStack[--this.callStackDepth];
								break;
							case SSEQCommand.LoopEnd:
								if (this.callStackDepth != 0)
								{
									// gosh, this was nasty to figure out
									byte count = this.loopCount[this.callStackDepth - 1];
									if (count != 0 && --count == 0)
									{
										--this.callStackDepth;
										break;
									}
									this.loopCount[this.callStackDepth - 1] = count;
									this.CurrentPos = this.positionCallStack[this.callStackDepth - 1];
								}
								break;
							case SSEQCommand.End:
								return false;
						}
					break;
			}
		}

		return true;
	}
}
