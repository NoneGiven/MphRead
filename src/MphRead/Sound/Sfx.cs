using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MphRead.Entities;
using MphRead.Formats.Sound;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;

namespace MphRead.Sound
{
    public class SoundSource
    {
        public Vector3 Position { get; set; }
        public float ReferenceDistance { get; set; } = 1;
        public float MaxDistance { get; set; } = Single.MaxValue;
        public float RolloffFactor { get; set; } = 1;

        public void Update(Vector3 position, int rangeIndex)
        {
            if (rangeIndex == -1)
            {
                Position = PlayerEntity.Main.CameraInfo.Position + PlayerEntity.Main.CameraInfo.Facing;
                ReferenceDistance = Single.MaxValue;
                MaxDistance = Single.MaxValue;
            }
            else
            {
                Position = position;
                Debug.Assert(rangeIndex >= 0 && rangeIndex < Sfx.RangeData.Count);
                Sound3dEntry rangeData = Sfx.RangeData[rangeIndex];
                ReferenceDistance = rangeData.FalloffDistance / 4096f;
                MaxDistance = rangeData.MaxDistance / 4096f;
            }
        }

        public void PlaySfx(SfxId id, bool loop = false, bool noUpdate = false,
            float recency = -1, bool sourceOnly = false, bool cancellable = false,
            float amountA = 0, float amountB = 0)
        {
            PlaySfx((int)id, loop, noUpdate, recency, sourceOnly, cancellable, amountA, amountB);
        }

        public int PlayFreeSfx(SfxId id)
        {
            return PlayFreeSfx((int)id);
        }

        public void PlaySfx(int id, bool loop = false, bool noUpdate = false,
            float recency = -1, bool sourceOnly = false, bool cancellable = false,
            float amountA = 0, float amountB = 0)
        {
            if (id >= 0)
            {
                if ((id & 0x8000) != 0)
                {
                    Sfx.PlayDgn(id, this, loop, noUpdate, recency, cancellable, amountA, amountB);
                }
                else if ((id & 0x4000) != 0)
                {
                    Sfx.PlayScript(id, this, noUpdate, recency, sourceOnly, cancellable);
                }
                else
                {
                    Sfx.PlaySample(id, this, loop, noUpdate, recency, sourceOnly, cancellable);
                }
            }
        }

        public int PlayFreeSfx(int id)
        {
            if (id >= 0)
            {
                Debug.Assert((id & 0xC000) == 0);
                return Sfx.PlaySample(id, null, loop: false, noUpdate: false,
                    recency: -1, sourceOnly: false, cancellable: false)?.Handle ?? -1;
            }
            return -1;
        }

        public void PlayEnvironmentSfx(int id)
        {
            Sfx.PlayEnvironmentSfx(id, this);
        }

        public void StopAllSfx(bool force = false)
        {
            Sfx.StopSoundFromSource(this, force);
        }

        public void StopSfx(SfxId id)
        {
            StopSfx((int)id);
        }

        public void StopSfx(int id)
        {
            Sfx.StopSoundFromSource(this, id);
        }

        public void StopSfxByHandle(int handle)
        {
            Sfx.StopSoundByHandle(handle);
        }

        public bool IsHandlePlaying(int handle)
        {
            return Sfx.IsHandlePlaying(handle);
        }

        public int CountPlayingSfx(SfxId id)
        {
            return Sfx.CountPlayingSfx((int)id);
        }

        public int CountPlayingSfx(int id)
        {
            return Sfx.CountPlayingSfx(id);
        }

        public void QueueStream(VoiceId id, float delay = 0, float expiration = 0)
        {
            Sfx.QueueStream((int)id, delay, expiration);
        }

        public void QueueStream(int id, float delay = 0, float expiration = 0)
        {
            Sfx.QueueStream(id, delay, expiration);
        }
    }

    // sfxtodo: pause all sounds when debugger breaks, frame advance is on, etc.
    public static class Sfx
    {
        public class SoundInstance
        {
            public int Count { get; set; }
            public SoundSample[] Samples { get; } = new SoundSample[8];
            public SoundChannel[] Channels { get; } = new SoundChannel[8];
            public DgnFile? DgnFile { get; set; }
            public SfxScriptFile? ScriptFile { get; set; }
            public int ScriptIndex { get; set; } = -1;
            public SoundSource? Source { get; set; }
            public float[] Volume { get; } = new float[8];
            public float[] Pitch { get; } = new float[8];
            public float PlayTime { get; set; } = -1;
            public int SfxId { get; set; } = -1;
            public bool NoUpdate { get; set; }
            public bool Loop { get; set; }
            public bool Cancellable { get; set; }

            public int Handle { get; set; } = -1;
            public static int NextHandle { get; set; } = 0;

            public SoundInstance()
            {
                for (int i = 0; i < 8; i++)
                {
                    Volume[i] = 1;
                    Pitch[i] = 1;
                }
            }

            public void PlayChannel(int index, bool loop)
            {
                int channelId = Channels[index].Id;
                // sfxtodo: loop points (needs opentk update)
                AL.Source(channelId, ALSourceb.Looping, loop);
                AL.SourcePlay(channelId);
            }

            public void UpdatePosition()
            {
                if (Source == null)
                {
                    Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
                    for (int i = 0; i < Channels.Length; i++)
                    {
                        SoundChannel? channel = Channels[i];
                        if (channel == null)
                        {
                            continue;
                        }
                        int channelId = channel.Id;
                        AL.Source(channelId, ALSource3f.Position, ref sourcePos);
                        AL.Source(channelId, ALSourcef.ReferenceDistance, Single.MaxValue);
                        AL.Source(channelId, ALSourcef.MaxDistance, Single.MaxValue);
                        AL.Source(channelId, ALSourcef.RolloffFactor, 1);
                    }
                }
                else if (!NoUpdate)
                {
                    Vector3 sourcePos = Source.Position;
                    for (int i = 0; i < Channels.Length; i++)
                    {
                        SoundChannel? channel = Channels[i];
                        if (channel == null)
                        {
                            continue;
                        }
                        int channelId = channel.Id;
                        AL.Source(channelId, ALSource3f.Position, ref sourcePos);
                        AL.Source(channelId, ALSourcef.ReferenceDistance, Source.ReferenceDistance);
                        AL.Source(channelId, ALSourcef.MaxDistance, Source.MaxDistance);
                        AL.Source(channelId, ALSourcef.RolloffFactor, Source.RolloffFactor);
                    }
                }
            }

            public void UpdateParameters()
            {
                for (int i = 0; i < Channels.Length; i++)
                {
                    SoundChannel? channel = Channels[i];
                    if (channel == null)
                    {
                        continue;
                    }
                    int channelId = channel.Id;
                    // sfxtodo: this volume multiplication isn't really right
                    AL.Source(channelId, ALSourcef.Gain, Sfx.Volume * Volume[i] * Samples[i].Volume);
                    AL.Source(channelId, ALSourcef.Pitch, Pitch[i]);
                }
            }

            public void Stop()
            {
                for (int i = 0; i < 8; i++)
                {
                    SoundChannel channel = Channels[i];
                    if (channel != null)
                    {
                        channel.Stop();
                        Channels[i] = null!;
                    }
                    SoundSample sample = Samples[i];
                    if (sample != null)
                    {
                        sample.References--;
                        Samples[i] = null!;
                    }
                    Volume[i] = 1;
                    Pitch[i] = 1;
                }
                PlayTime = -1;
                SfxId = -1;
                DgnFile = null;
                ScriptFile = null;
                ScriptIndex = -1;
                Source = null;
                NoUpdate = false;
                Loop = false;
                Cancellable = false;
                Handle = -1;
                Count = 0;
            }
        }

        public class SoundChannel
        {
            public int Id { get; }
            public bool InUse { get; set; }
            public int BufferId { get; set; }

            public SoundChannel(int id)
            {
                Id = id;
            }

            public void Stop()
            {
                AL.SourceStop(Id);
                // buffer must be disassociated from sources in order to buffer new data
                AL.Source(Id, ALSourcei.Buffer, 0);
                InUse = false;
                BufferId = 0;
            }
        }

        public class SoundBuffer
        {
            public int Id { get; }
            public SoundSample? Sample { get; set; }

            public SoundBuffer(int id)
            {
                Id = id;
            }
        }

        // sktodo: clear all of this stuff on shutdown
        private static ALDevice _device = ALDevice.Null;
        private static ALContext _context = ALContext.Null;
        private static readonly SoundBuffer[] _buffers = new SoundBuffer[64];
        private static readonly SoundChannel[] _channels = new SoundChannel[128];
        private static readonly SoundInstance[] _instances = new SoundInstance[128];

        private static IReadOnlyList<SoundSample> _samples = null!;
        private static IReadOnlyList<DgnFile> _dgnFiles = null!;
        private static IReadOnlyList<SfxScriptFile> _sfxScripts = null!;
        public static IReadOnlyList<SfxScriptFile> SfxScripts => _sfxScripts; // skdebug
        private static IReadOnlyList<Sound3dEntry> _rangeData = null!;
        public static IReadOnlyList<Sound3dEntry> RangeData => _rangeData;

        public static float Volume { get; set; } = 0.35f;

        public static SoundInstance? PlaySample(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            bool setUp = SetUpInstance(id, source, loop, recency, sourceOnly, cancellable, out SoundInstance inst);
            if (!setUp)
            {
                return null;
            }
            if (!SetUpSample(id, inst, index: 0))
            {
                return null;
            }
            StartInstance(inst, noUpdate);
            return inst;
        }

        public static void PlayDgn(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool cancellable, float amountA, float amountB)
        {
            Debug.Assert((id & 0x8000) != 0);
            int dgnId = id & 0x3FFF;
            Debug.Assert(dgnId >= 0 && dgnId < _dgnFiles.Count);
            bool setUp = SetUpInstance(id, source, loop, recency, sourceOnly: true, cancellable, out SoundInstance inst);
            if (!setUp)
            {
                UpdateDgn(inst, amountA, amountB);
                return;
            }
            DgnFile dgnFile = _dgnFiles[dgnId];
            Debug.Assert(dgnFile.Entries.Count > 0 && dgnFile.Entries.Count <= 3);
            inst.DgnFile = dgnFile;
            for (int i = 0; i < dgnFile.Entries.Count; i++)
            {
                DgnFileEntry entry = dgnFile.Entries[i];
                if (!SetUpSample((int)entry.SfxId, inst, index: i))
                {
                    inst.Stop();
                    return;
                }
            }
            UpdateDgn(inst, amountA, amountB);
            for (int i = 0; i < inst.Count; i++)
            {
                if (inst.Volume[i] > 0)
                {
                    StartInstance(inst, noUpdate);
                    return;
                }
            }
            // no volume was above 0
            inst.Stop();
        }

        public static void PlayScript(int id, SoundSource? source, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            Debug.Assert((id & 0x4000) != 0);
            int scriptId = id & 0x3FFF;
            Debug.Assert(scriptId >= 0 && scriptId < _sfxScripts.Count);
            bool setUp = SetUpInstance(id, source, loop: false, recency, sourceOnly, cancellable, out SoundInstance inst);
            if (!setUp)
            {
                return;
            }
            inst.NoUpdate = noUpdate;
            SfxScriptFile script = _sfxScripts[scriptId];
            Debug.Assert(script.Entries.Count > 0 && script.Entries.Count <= 22);
            inst.ScriptFile = script;
        }

        public static bool SetUpInstance(int id, SoundSource? source, bool loop,
            float recency, bool sourceOnly, bool cancellable, out SoundInstance inst)
        {
            if (loop)
            {
                recency = Single.MaxValue;
                sourceOnly = true;
            }
            if (recency >= 0)
            {
                SoundInstance? recent = FindRecentSamplePlay(id, recency, sourceOnly ? source : null);
                if (recent != null)
                {
                    inst = recent;
                    return false;
                }
            }
            inst = FindInstance(source);
            inst.Source = source;
            inst.PlayTime = 0;
            inst.Loop = loop;
            inst.Cancellable = cancellable;
            inst.SfxId = id;
            inst.Handle = SoundInstance.NextHandle++;
            return true;
        }

        private static bool SetUpSample(int id, SoundInstance inst, int index)
        {
            Debug.Assert(id >= 0 && id < _samples.Count);
            SoundSample sample = _samples[id];
            SoundSample? prevSample = inst.Samples[index];
            if (prevSample != sample)
            {
                if (prevSample != null)
                {
                    prevSample.References--;
                }
                inst.Samples[index] = sample;
                sample.References++;
            }
            SoundChannel? channel = inst.Channels[index];
            if (channel == null)
            {
                channel = GetChannel(sample.BufferId);
                if (channel == null)
                {
                    return false;
                }
                inst.Channels[index] = channel;
                inst.Count++;
            }
            channel.InUse = true;
            if (sample.BufferId == 0)
            {
                BufferData(sample);
            }
            if (channel.BufferId != sample.BufferId)
            {
                AL.Source(channel.Id, ALSourcei.Buffer, sample.BufferId);
                channel.BufferId = sample.BufferId;
            }
            return true;
        }

        private static void UpdateInstance(SoundInstance inst, bool noUpdate)
        {
            inst.NoUpdate = false;
            inst.UpdatePosition();
            inst.NoUpdate = noUpdate;
            inst.UpdateParameters();
        }

        private static void StartInstance(SoundInstance inst, bool noUpdate)
        {
            UpdateInstance(inst, noUpdate);
            for (int i = 0; i < inst.Count; i++)
            {
                inst.PlayChannel(index: i, inst.Loop);
            }
        }

        // recency = 0 --> started playing on the current frame
        // receny = 1 --> started playing within the last second
        // recency = Single.MaxValue --> playing at all
        private static SoundInstance? FindRecentSamplePlay(int id, float recency, SoundSource? source)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if ((source == null || inst.Source == source)
                    && inst.SfxId == id && inst.PlayTime <= recency)
                {
                    return inst;
                }
            }
            return null;
        }

        public static float CalculatePitchDiv(float pitchFac)
        {
            if (pitchFac == 0)
            {
                pitchFac = 1;
            }
            int pitchInt = (int)pitchFac;
            if (pitchFac <= 0xFFF)
            {
                pitchInt = -((0x600000 / pitchInt) >> 1);
            }
            else if (pitchFac <= 0x1FFF)
            {
                pitchInt = (768 * (pitchInt - 0x2000)) >> 12;
            }
            else
            {
                pitchInt = (768 * (pitchInt - 0x2000)) >> 13;
            }
            float semitones = pitchInt / 64f;
            float octaves = MathF.Abs(semitones / 12f);
            if (semitones >= 0)
            {
                return MathF.Pow(2, octaves);
            }
            return MathF.Pow(0.5f, octaves);
        }

        private static void UpdateDgn(SoundInstance inst, float amountA, float amountB)
        {
            Debug.Assert(inst.DgnFile != null);
            for (int i = 0; i < inst.Count; i++)
            {
                DgnFileEntry entry = inst.DgnFile.Entries[i];
                float volumeA = GetDgnValue(entry.Data1, amountA);
                float volumeB = GetDgnValue(entry.Data2, amountB);
                float pitchA = GetDgnValue(entry.Data3, amountA);
                float pitchB = GetDgnValue(entry.Data4, amountB);
                float volumeFac = volumeA / 127f * volumeB;
                volumeFac = volumeFac / 127f * inst.DgnFile.Header.InitialVolume / 127f;
                if (volumeFac < 1 / 130f)
                {
                    volumeFac = 0;
                }
                inst.Volume[i] = volumeFac;
                float pitchFac = pitchA / 0x2000 * pitchB;
                Debug.Assert(pitchFac >= 0);
                if (pitchFac >= 0x4000)
                {
                    pitchFac = 0x3FFF;
                }
                inst.Pitch[i] = CalculatePitchDiv(pitchFac);
            }
        }

        private static float GetDgnValue(IReadOnlyList<DgnData> data, float amount)
        {
            DgnData first = data[0];
            if (amount <= first.Amount)
            {
                return first.Value & 0x3FFF;
            }
            DgnData last = data[^1];
            if (amount >= last.Amount)
            {
                return last.Value & 0x3FFF;
            }
            if (data.Count == 1)
            {
                return 0;
            }
            DgnData data1;
            DgnData data2;
            int i = 0;
            while (true)
            {
                data1 = data[i];
                data2 = data[i + 1];
                if (amount < data2.Amount)
                {
                    break;
                }
                if (++i >= data.Count - 1)
                {
                    return 0;
                }
            }
            float diff = amount - data1.Amount;
            float ratio = diff / (data2.Amount - data1.Amount);
            float value1 = data1.Value & 0x3FFF;
            float value2 = data2.Value & 0x3FFF;
            float flags2 = data2.Value & 0xC000;
            if (flags2 == 0x4000)
            {
                // sin
                return value1 + (value2 - value1) * MathF.Sin(MathF.PI / 2 * ratio);
            }
            if (flags2 == 0x8000)
            {
                Debug.Assert(false);
                return 0;
            }
            // lerp
            return value1 + (value2 - value1) * ratio;
        }

        public static void StopAllSound()
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.SfxId != -1)
                {
                    inst.Stop();
                }
            }
            AL.SourceStop(_streamInstance);
        }

        public static void StopSoundFromSource(SoundSource source, bool force)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.Source == source)
                {
                    if ((force || inst.Loop || inst.Cancellable) && (!force || !inst.NoUpdate))
                    {
                        inst.Stop();
                    }
                }
            }
        }

        public static void StopSoundFromSource(SoundSource source, int id)
        {
            Debug.Assert(id >= 0);
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.Source == source && inst.SfxId == id)
                {
                    inst.Stop();
                }
            }
        }

        public static void StopSoundById(int id)
        {
            Debug.Assert(id >= 0);
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.SfxId == id)
                {
                    inst.Stop();
                }
            }
        }

        public static void StopSoundByHandle(int handle)
        {
            if (handle >= 0)
            {
                for (int i = 0; i < _instances.Length; i++)
                {
                    SoundInstance inst = _instances[i];
                    if (inst.Handle == handle)
                    {
                        inst.Stop();
                    }
                }
            }
        }

        public static bool IsHandlePlaying(int handle)
        {
            if (handle >= 0)
            {
                for (int i = 0; i < _instances.Length; i++)
                {
                    SoundInstance inst = _instances[i];
                    if (inst.Handle == handle)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static SoundInstance FindInstance(SoundSource? source)
        {
            float maxTime = 0;
            SoundInstance? resultBySource = null;
            SoundInstance? anyResult = null;
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.PlayTime < 0)
                {
                    return inst;
                }
                if (inst.PlayTime >= maxTime)
                {
                    if (source != null && inst.Source == source)
                    {
                        resultBySource = inst;
                    }
                    anyResult = inst;
                    maxTime = inst.PlayTime;
                }
            }
            if (resultBySource != null)
            {
                return resultBySource;
            }
            Debug.Assert(anyResult != null);
            return anyResult;
        }

        private static SoundChannel? GetChannel(int bufferId)
        {
            if (bufferId > 0)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    SoundChannel channel = _channels[i];
                    if (channel.BufferId == bufferId && !channel.InUse)
                    {
                        return channel;
                    }
                }
            }
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (!channel.InUse)
                {
                    return channel;
                }
            }
            // not expected to happen
            Debugger.Break();
            return null;
        }

        private static void BufferData(SoundSample sample)
        {
            SoundBuffer? dest = null;

            void DoBuffer()
            {
                if (dest.Sample != null)
                {
                    dest.Sample.BufferId = 0;
                }
                dest.Sample = sample;
                ALFormat format = sample.Format == WaveFormat.ADPCM ? ALFormat.Mono16 : ALFormat.Mono8;
                AL.BufferData(dest.Id, format, sample.WaveData.Value, sample.SampleRate);
                sample.BufferId = dest.Id;
            }

            for (int i = 0; i < _buffers.Length; i++)
            {
                SoundBuffer buffer = _buffers[i];
                if (buffer.Sample == null)
                {
                    dest = buffer;
                    break;
                }
            }
            if (dest != null)
            {
                DoBuffer();
                return;
            }

            for (int i = 0; i < _buffers.Length; i++)
            {
                SoundBuffer buffer = _buffers[i];
                Debug.Assert(buffer.Sample != null);
                if (buffer.Sample.References == 0)
                {
                    dest = buffer;
                    break;
                }
            }
            if (dest != null)
            {
                DoBuffer();
                return;
            }

            // not expected to happen
            Debugger.Break();
            dest = _buffers[0];
            Debug.Assert(dest.Sample != null);
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                for (int j = 0; j < inst.Samples.Length; j++)
                {
                    if (inst.Samples[j] == dest.Sample)
                    {
                        inst.Stop();
                        break;
                    }
                }
            }
            DoBuffer();
        }

        private static void UpdateScript(SoundInstance inst, float time)
        {
            Debug.Assert(inst.ScriptFile != null);
            if (inst.ScriptIndex != -1)
            {
                inst.PlayTime += time;
            }
            int playingCount = 0;
            for (int i = 0; i < inst.Channels.Length; i++)
            {
                SoundChannel? channel = inst.Channels[i];
                if (channel != null)
                {
                    AL.GetSource(channel.Id, ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state == ALSourceState.Playing)
                    {
                        playingCount++;
                    }
                    else
                    {
                        channel.Stop();
                        inst.Channels[i] = null!;
                        SoundSample sample = inst.Samples[i];
                        sample.References--;
                        inst.Samples[i] = null!;
                    }
                }
            }
            if (inst.ScriptIndex + 1 >= inst.ScriptFile.Entries.Count)
            {
                if (playingCount == 0)
                {
                    Debug.WriteLine($"stopped {inst.ScriptFile.Name}");
                    inst.Stop();
                    return;
                }
            }
            for (int i = inst.ScriptIndex + 1; i < inst.ScriptFile.Entries.Count; i++)
            {
                SfxScriptEntry entry = inst.ScriptFile.Entries[i];
                if (entry.Delay > inst.PlayTime)
                {
                    break;
                }
                inst.ScriptIndex = i;
                int sfxId = entry.SfxData & 0x3FFF;
                if ((entry.SfxData & 0x8000) != 0)
                {
                    if (inst.Source == null)
                    {
                        StopSoundById(sfxId);
                    }
                    else
                    {
                        StopSoundFromSource(inst.Source, sfxId);
                    }
                }
                if (playingCount >= 8)
                {
                    Debugger.Break();
                }
                int index = -1;
                for (int j = 0; j < inst.Channels.Length; j++)
                {
                    if (inst.Channels[j] == null)
                    {
                        index = j;
                        break;
                    }
                }
                if (index == -1)
                {
                    Debug.Assert(false, "SFX script tried to play more than 8 concurrent channels");
                    inst.Stop();
                    return;
                }
                // sktodo: handle pan by forcing relative position and overriding it
                inst.Volume[index] = entry.Volume;
                inst.Pitch[index] = entry.Pitch;
                if (!SetUpSample(sfxId, inst, index))
                {
                    inst.Stop();
                    return;
                }
                UpdateInstance(inst, inst.NoUpdate);
                inst.PlayChannel(index, loop: (entry.SfxData & 0x4000) != 0);
            }
        }

        public static void Update(float time)
        {
            Vector3 listenerPos = PlayerEntity.Main.CameraInfo.Position;
            Vector3 listenerUp = PlayerEntity.Main.CameraInfo.UpVector;
            Vector3 listenerFacing = PlayerEntity.Main.CameraInfo.Facing;
            AL.Listener(ALListener3f.Position, ref listenerPos);
            AL.Listener(ALListenerfv.Orientation, ref listenerFacing, ref listenerUp);
            Debug.Assert(_streamInstance != -1);
            Debug.Assert(_streamBuffer != -1);
            Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
            AL.Source(_streamInstance, ALSource3f.Position, ref sourcePos);
            AL.Source(_streamInstance, ALSourcef.ReferenceDistance, Single.MaxValue);
            AL.Source(_streamInstance, ALSourcef.MaxDistance, Single.MaxValue);
            AL.Source(_streamInstance, ALSourcef.RolloffFactor, 1);
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.PlayTime >= 0)
                {
                    if (inst.ScriptFile != null)
                    {
                        UpdateScript(inst, time);
                        continue;
                    }
                    bool playing = false;
                    for (int j = 0; j < inst.Count; j++)
                    {
                        int channelId = inst.Channels[j].Id;
                        AL.GetSource(channelId, ALGetSourcei.SourceState, out int value);
                        var state = (ALSourceState)value;
                        if (state == ALSourceState.Playing)
                        {
                            if (inst.Volume[j] == 0)
                            {
                                // DGN reduced to zero volume
                                AL.SourceStop(channelId);
                                continue;
                            }
                            playing = true;
                            break;
                        }
                    }
                    if (playing)
                    {
                        inst.PlayTime += time;
                        inst.UpdatePosition();
                        inst.UpdateParameters();
                    }
                    else
                    {
                        inst.Stop();
                    }
                }
            }
            UpdateEnvironmentSfx();
            UpdateStreams(time);
        }

        public static int CountPlayingSfx(int id)
        {
            int count = 0;
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.Handle != -1 && inst.SfxId == id)
                {
                    count++;
                }
            }
            return count;
        }

        private class EnvironmentItem
        {
            public int SfxId { get; }
            public int Instances { get; set; }
            public int Handle { get; set; } = -1;
            public SoundSource Source { get; }
            public float DistanceSquared { get; set; } = Single.MaxValue;

            public EnvironmentItem(SfxId sfxId)
            {
                SfxId = (int)sfxId;
                Source = new SoundSource();
            }
        }

        private static readonly IReadOnlyList<EnvironmentItem> _environmentItems = new EnvironmentItem[10]
        {
            new EnvironmentItem(SfxId.ELECTRO_WAVE2),
            new EnvironmentItem(SfxId.ELECTRICITY),
            new EnvironmentItem(SfxId.ELECTRIC_BARRIER),
            new EnvironmentItem(SfxId.ENERGY_BALL),
            new EnvironmentItem(SfxId.BLUE_FLAME),
            new EnvironmentItem(SfxId.CYLINDER_BOSS_ATTACK),
            new EnvironmentItem(SfxId.CYLINDER_BOSS_SPIN),
            new EnvironmentItem(SfxId.BUBBLES),
            new EnvironmentItem(SfxId.ELEVATOR2_START),
            new EnvironmentItem(SfxId.GOREA_ATTACK3_LOOP)
        };

        public static void PlayEnvironmentSfx(int index, SoundSource source)
        {
            // in-game, environment SFX update their volume to the source's if it's greater, but that volume is already
            // the rest of the 3D sound calculation, and the pan_x is also added as a percentage of the difference based on volume
            // --> we can't really do that, so we just take the parameters of the closest source, and have no panning (for now?)
            Debug.Assert(index >= 0 && index < _environmentItems.Count);
            EnvironmentItem item = _environmentItems[index];
            CameraInfo? camInfo = PlayerEntity.Main.CameraInfo;
            float distSqr = Vector3.DistanceSquared(source.Position, camInfo.Position);
            if (distSqr < item.DistanceSquared)
            {
                item.DistanceSquared = distSqr;
                float dist = MathF.Sqrt(distSqr);
                item.Source.Position = camInfo.Position + camInfo.Facing * dist;
                item.Source.ReferenceDistance = source.ReferenceDistance;
                item.Source.MaxDistance = source.MaxDistance;
                item.Source.RolloffFactor = source.RolloffFactor;
            }
            item.Instances++;
        }

        public static void UpdateEnvironmentSfx()
        {
            foreach (EnvironmentItem item in _environmentItems)
            {
                if (item.Instances > 0)
                {
                    SoundInstance? inst = PlaySample(item.SfxId, item.Source, loop: true,
                        noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                    if (inst != null)
                    {
                        item.Handle = inst.Handle;
                    }
                }
                else if (item.Handle != -1)
                {
                    StopSoundByHandle(item.Handle);
                    item.Handle = -1;
                }
                item.Instances = 0;
                item.DistanceSquared = Single.MaxValue;
            }
        }

        private class QueueItem
        {
            public SoundStream? Stream { get; set; }
            public float DelayTimer { get; set; }
            public float ExpirationTimer { get; set; }
            public bool Playing { get; set; }
        }

        public static SoundData _soundData = null!;
        private static readonly LinkedList<QueueItem> _activeQueue = new LinkedList<QueueItem>();
        private static readonly Queue<QueueItem> _inactiveQueue = new Queue<QueueItem>(16);
        private static int _streamInstance = -1;
        private static int _streamBuffer = -1;

        public static void QueueStream(int id, float delay, float expiration)
        {
            if (_inactiveQueue.Count == 0)
            {
                return;
            }
            Debug.Assert(id >= 0 && id < _soundData.Streams.Count);
            QueueItem item = _inactiveQueue.Dequeue();
            item.Stream = _soundData.Streams[id];
            item.DelayTimer = delay;
            item.ExpirationTimer = expiration;
            item.Playing = false;
            _activeQueue.AddLast(item);
        }

        private static void UpdateStreams(float time)
        {
            int index = 0;
            LinkedListNode<QueueItem>? node = _activeQueue.First;
            while (node != null)
            {
                LinkedListNode<QueueItem>? next = node.Next;
                QueueItem item = node.Value;
                Debug.Assert(item.Stream != null);
                if (index == 0 && item.Playing)
                {
                    AL.GetSource(_streamInstance, ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state != ALSourceState.Initial && state != ALSourceState.Playing)
                    {
                        AL.SourceStop(_streamInstance);
                        AL.Source(_streamInstance, ALSourcei.Buffer, 0);
                        item.Stream = null;
                        _activeQueue.Remove(node);
                        node = next;
                        index++;
                        continue;
                    }
                }
                else if (item.DelayTimer > 0)
                {
                    item.DelayTimer -= time;
                    if (item.DelayTimer <= 0)
                    {
                        item.DelayTimer = 0;
                    }
                }
                if (item.DelayTimer == 0 && !item.Playing && index == 0)
                {
                    ALFormat format;
                    if (item.Stream.Format == WaveFormat.ADPCM)
                    {
                        format = item.Stream.Channels.Count == 2 ? ALFormat.Stereo16 : ALFormat.Mono16;
                    }
                    else // if (item.Stream.Format == WaveFormat.PCM8)
                    {
                        format = item.Stream.Channels.Count == 2 ? ALFormat.Stereo8 : ALFormat.Mono8;
                    }
                    AL.BufferData(_streamBuffer, format, item.Stream.BufferData.Value, item.Stream.SampleRate);
                    AL.Source(_streamInstance, ALSourcei.Buffer, _streamBuffer);
                    // sfxtodo: loop points
                    AL.Source(_streamInstance, ALSourceb.Looping, item.Stream.Loop);
                    AL.Source(_streamInstance, ALSourcef.Gain, Volume * item.Stream.Volume);
                    AL.SourcePlay(_streamInstance);
                    item.Playing = true;
                    node = next;
                    index++;
                    continue;
                }
                if (index > 0 && item.ExpirationTimer > 0)
                {
                    item.ExpirationTimer -= time;
                    if (item.ExpirationTimer <= 0)
                    {
                        item.ExpirationTimer = 0;
                        _activeQueue.Remove(node);
                    }
                }
                node = next;
                index++;
            }
        }

        public static void Load()
        {
            _samples = SoundRead.ReadSoundSamples();
            SoundTable table = SoundRead.ReadSoundTables();
            Debug.Assert(_samples.Count == table.Entries.Count);
            for (int i = 0; i < _samples.Count; i++)
            {
                SoundSample sample = _samples[i];
                SoundTableEntry entry = table.Entries[i];
                sample.Volume = entry.InitialVolume / 127f;
                sample.Name = entry.Name;
                _ = sample.WaveData.Value;
            }
            _rangeData = SoundRead.ReadSound3dList();
            _dgnFiles = SoundRead.ReadDgnFiles();
            _sfxScripts = SoundRead.ReadSfxScriptFiles();
            _soundData = SoundRead.ReadSdat();
            foreach (SoundStream stream in _soundData.Streams)
            {
                _ = stream.BufferData.Value;
            }
            for (int i = 0; i < 16; i++)
            {
                _inactiveQueue.Enqueue(new QueueItem());
            }
            _device = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, new ALContextAttributes());
            ALC.MakeContextCurrent(_context);
            int[] bufferIds = new int[_buffers.Length];
            AL.GenBuffers(bufferIds);
            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = new SoundBuffer(bufferIds[i]);
            }
            int[] channelIds = new int[_channels.Length];
            AL.GenSources(channelIds);
            for (int i = 0; i < _channels.Length; i++)
            {
                _channels[i] = new SoundChannel(channelIds[i]);
            }
            for (int i = 0; i < _instances.Length; i++)
            {
                _instances[i] = new SoundInstance();
            }
            _streamBuffer = AL.GenBuffer();
            _streamInstance = AL.GenSource();
            if (!Features.LogSpatialAudio)
            {
                AL.DistanceModel(ALDistanceModel.LinearDistanceClamped);
            }
        }

        public static void ShutDown()
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                for (int j = 0; j < inst.Channels.Length; j++)
                {
                    SoundChannel? channel = inst.Channels[j];
                    if (channel == null)
                    {
                        continue;
                    }
                    AL.GetSource(channel.Id, ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state == ALSourceState.Playing)
                    {
                        inst.Stop();
                        break;
                    }
                }
            }
            ALC.MakeContextCurrent(ALContext.Null);
            Task.Run(() =>
            {
                ALC.DestroyContext(_context);
                ALC.CloseDevice(_device);
                _context = ALContext.Null;
                _device = ALDevice.Null;
            });
        }

        public static void Test()
        {
            IReadOnlyList<SoundSample> samples = SoundRead.ReadSoundSamples();
            SoundSample sample = samples[0];
            byte[] data = SoundRead.GetWaveData(sample);
            ALDevice device = ALC.OpenDevice(null);
            ALContext context = ALC.CreateContext(device, new ALContextAttributes());
            ALC.MakeContextCurrent(context);
            int buffer = AL.GenBuffer();
            int source = AL.GenSource();
            ALFormat format = sample.Format == WaveFormat.ADPCM ? ALFormat.Mono16 : ALFormat.Mono8;
            AL.BufferData(buffer, format, data, sample.SampleRate);
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.SourcePlay(source);
        }
    }
}
