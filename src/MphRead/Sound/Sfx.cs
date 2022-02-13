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
            // sktodo: support scripts
            if (id >= 0 && (id & 0x4000) == 0)
            {
                if ((id & 0x8000) != 0)
                {
                    Sfx.PlayDgn(id, this, loop, noUpdate, recency, cancellable, amountA, amountB);
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
        public class SoundChannel
        {
            public int Count { get; set; }
            private readonly int[] _channelIds = new int[3];
            public IReadOnlyList<int> ChannelIds => _channelIds;
            public int[] BufferIds { get; } = new int[3];
            public SoundSample[] Samples { get; } = new SoundSample[3];
            public DgnFile? DgnFile { get; set; }
            public SoundSource? Source { get; set; }
            public float[] Volume { get; } = new float[3] { 1, 1, 1 };
            public float[] Pitch { get; } = new float[3] { 1, 1, 1 };
            public float PlayTime { get; set; } = -1;
            public int SfxId { get; set; } = -1;
            public bool NoUpdate { get; set; }
            public bool Loop { get; set; }
            public bool Cancellable { get; set; }

            public int Handle { get; set; } = -1;
            public static int NextHandle { get; set; } = 0;

            public SoundChannel(int channel0, int channel1, int channel2)
            {
                _channelIds[0] = channel0;
                _channelIds[1] = channel1;
                _channelIds[2] = channel2;
            }

            public void PlayChannel(int index, bool loop)
            {
                int channelId = _channelIds[index];
                int bufferId = Samples[index].BufferId;
                if (BufferIds[index] != bufferId)
                {
                    AL.Source(channelId, ALSourcei.Buffer, bufferId);
                    BufferIds[index] = bufferId;
                }
                // sfxtodo: loop points (needs opentk update)
                AL.Source(channelId, ALSourceb.Looping, loop);
                AL.SourcePlay(channelId);
            }

            public void UpdatePosition()
            {
                if (Source == null)
                {
                    Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
                    for (int i = 0; i < Count; i++)
                    {
                        int channelId = ChannelIds[i];
                        AL.Source(channelId, ALSource3f.Position, ref sourcePos);
                        AL.Source(channelId, ALSourcef.ReferenceDistance, Single.MaxValue);
                        AL.Source(channelId, ALSourcef.MaxDistance, Single.MaxValue);
                        AL.Source(channelId, ALSourcef.RolloffFactor, 1);
                    }
                }
                else if (!NoUpdate)
                {
                    Vector3 sourcePos = Source.Position;
                    for (int i = 0; i < Count; i++)
                    {
                        int channelId = ChannelIds[i];
                        AL.Source(channelId, ALSource3f.Position, ref sourcePos);
                        AL.Source(channelId, ALSourcef.ReferenceDistance, Source.ReferenceDistance);
                        AL.Source(channelId, ALSourcef.MaxDistance, Source.MaxDistance);
                        AL.Source(channelId, ALSourcef.RolloffFactor, Source.RolloffFactor);
                    }
                }
            }

            public void UpdateParameters()
            {
                for (int i = 0; i < Count; i++)
                {
                    int channelId = ChannelIds[i];
                    // sfxtodo: this volume multiplication isn't really right
                    AL.Source(channelId, ALSourcef.Gain, Sfx.Volume * Volume[i] * Samples[i].Volume);
                    AL.Source(channelId, ALSourcef.Pitch, Pitch[i]);
                }
            }

            public void Stop()
            {
                for (int i = 0; i < Count; i++)
                {
                    int channelId = ChannelIds[i];
                    AL.SourceStop(channelId);
                    AL.Source(channelId, ALSourcei.Buffer, 0);
                    BufferIds[i] = 0;
                    SoundSample sample = Samples[i];
                    sample.References--;
                    if (sample.References == 0)
                    {
                        sample.BufferId = 0;
                    }
                    Samples[i] = null!;
                }
                PlayTime = -1;
                SfxId = -1;
                DgnFile = null;
                Source = null;
                Volume[0] = 1;
                Volume[1] = 1;
                Volume[2] = 1;
                Pitch[0] = 1;
                Pitch[1] = 1;
                Pitch[2] = 1;
                NoUpdate = false;
                Loop = false;
                Cancellable = false;
                Handle = -1;
                Count = 0;
            }
        }

        // sfxtodo: clear all of this stuff on shutdown
        private static ALDevice _device = ALDevice.Null;
        private static ALContext _context = ALContext.Null;
        private static readonly int[] _bufferIds = new int[15];
        private static readonly int[] _channelIds = new int[85 * 3];
        private static readonly SoundChannel[] _channels = new SoundChannel[85];

        private static IReadOnlyList<SoundSample> _samples = null!;
        private static IReadOnlyList<DgnFile> _dgnFiles = null!;
        private static IReadOnlyList<Sound3dEntry> _rangeData = null!;
        public static IReadOnlyList<Sound3dEntry> RangeData => _rangeData;

        public static float Volume { get; set; } = 0.35f;

        public static SoundChannel? PlaySample(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            bool setUp = SetUpChannel(id, source, loop, recency, sourceOnly, cancellable, out SoundChannel channel);
            if (!setUp)
            {
                return null;
            }
            SetUpSample(id, channel, index: 0);
            StartChannel(channel, noUpdate);
            return channel;
        }

        public static void PlayDgn(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool cancellable, float amountA, float amountB)
        {
            Debug.Assert((id & 0x8000) != 0);
            int dgnId = id & 0x3FFF;
            Debug.Assert(dgnId >= 0 && dgnId <= _dgnFiles.Count);
            bool setUp = SetUpChannel(id, source, loop, recency, sourceOnly: true, cancellable, out SoundChannel channel);
            if (!setUp)
            {
                UpdateDgn(channel, amountA, amountB);
                return;
            }
            DgnFile dgnFile = _dgnFiles[dgnId];
            Debug.Assert(dgnFile.Entries.Count > 0 && dgnFile.Entries.Count <= 3);
            channel.DgnFile = dgnFile;
            for (int i = 0; i < dgnFile.Entries.Count; i++)
            {
                DgnFileEntry entry = dgnFile.Entries[i];
                SetUpSample((int)entry.SfxId, channel, index: i);
            }
            UpdateDgn(channel, amountA, amountB);
            StartChannel(channel, noUpdate);
            return;
        }

        public static bool SetUpChannel(int id, SoundSource? source, bool loop,
            float recency, bool sourceOnly, bool cancellable, out SoundChannel channel)
        {
            if (loop)
            {
                recency = Single.MaxValue;
                sourceOnly = true;
            }
            if (recency >= 0)
            {
                SoundChannel? recent = FindRecentSamplePlay(id, recency, sourceOnly ? source : null);
                if (recent != null)
                {
                    channel = recent;
                    return false;
                }
            }
            channel = FindChannel(source);
            channel.Source = source;
            channel.PlayTime = 0;
            channel.Loop = loop;
            channel.Cancellable = cancellable;
            channel.SfxId = id;
            channel.Handle = SoundChannel.NextHandle++;
            return true;
        }

        private static void SetUpSample(int id, SoundChannel channel, int index)
        {
            Debug.Assert(id >= 0 && id < _samples.Count);
            SoundSample sample = _samples[id];
            channel.Count++;
            channel.Samples[index] = sample;
            sample.References++;
            if (sample.BufferId == 0)
            {
                sample.BufferId = BufferData(sample);
            }
        }

        private static void StartChannel(SoundChannel channel, bool noUpdate)
        {
            channel.NoUpdate = false;
            channel.UpdatePosition();
            channel.NoUpdate = noUpdate;
            channel.UpdateParameters();
            for (int i = 0; i < channel.Count; i++)
            {
                channel.PlayChannel(index: i, channel.Loop);
            }
        }

        // recency = 0 --> started playing on the current frame
        // receny = 1 --> started playing within the last second
        // recency = Single.MaxValue --> playing at all
        private static SoundChannel? FindRecentSamplePlay(int id, float recency, SoundSource? source)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if ((source == null || channel.Source == source)
                    && channel.SfxId == id && channel.PlayTime <= recency)
                {
                    return channel;
                }
            }
            return null;
        }

        private static void UpdateDgn(SoundChannel channel, float amountA, float amountB)
        {
            Debug.Assert(channel.DgnFile != null);
            for (int i = 0; i < channel.Count; i++)
            {
                DgnFileEntry entry = channel.DgnFile.Entries[i];
                float volumeA = GetDgnValue(entry.Data1, amountA);
                float volumeB = GetDgnValue(entry.Data2, amountB);
                float pitchA = GetDgnValue(entry.Data3, amountA);
                float pitchB = GetDgnValue(entry.Data4, amountB);
                float volumeFac = volumeA / 127f * volumeB;
                volumeFac = volumeFac / 127f * channel.DgnFile.Header.InitialVolume;
                channel.Volume[i] = volumeFac / 127f;
                float pitchFac = pitchA / 0x2000 * pitchB;
                Debug.Assert(pitchFac >= 0);
                if (pitchFac >= 0x4000)
                {
                    pitchFac = 0x3FFF;
                }
                else if (pitchFac == 0)
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
                    channel.Pitch[i] = MathF.Pow(2, octaves);
                }
                else
                {
                    channel.Pitch[i] = MathF.Pow(0.5f, octaves);
                }
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
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.SfxId != -1)
                {
                    channel.Stop();
                }
            }
            AL.SourceStop(_streamChannel);
        }

        public static void StopSoundFromSource(SoundSource source, bool force)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.Source == source)
                {
                    if ((force || channel.Loop || channel.Cancellable) && (!force || !channel.NoUpdate))
                    {
                        channel.Stop();
                    }
                }
            }
        }

        public static void StopSoundFromSource(SoundSource source, int id)
        {
            Debug.Assert(id >= 0);
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.Source == source && channel.SfxId == id)
                {
                    channel.Stop();
                }
            }
        }

        public static void StopSoundByHandle(int handle)
        {
            if (handle >= 0)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    SoundChannel channel = _channels[i];
                    if (channel.Handle == handle)
                    {
                        channel.Stop();
                    }
                }
            }
        }

        public static bool IsHandlePlaying(int handle)
        {
            if (handle >= 0)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    SoundChannel channel = _channels[i];
                    if (channel.Handle == handle)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static SoundChannel FindChannel(SoundSource? source)
        {
            float maxTime = 0;
            SoundChannel? resultBySource = null;
            SoundChannel? anyResult = null;
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.PlayTime < 0)
                {
                    return channel;
                }
                if (channel.PlayTime >= maxTime)
                {
                    if (source != null && channel.Source == source)
                    {
                        resultBySource = channel;
                    }
                    anyResult = channel;
                    maxTime = channel.PlayTime;
                }
            }
            if (resultBySource != null)
            {
                return resultBySource;
            }
            Debug.Assert(anyResult != null);
            return anyResult;
        }

        private static readonly HashSet<int> _usedBufferIds = new HashSet<int>(64);

        private static int BufferData(SoundSample sample)
        {
            int bufferId = _bufferIds[0];
            _usedBufferIds.Clear();
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                for (int j = 0; j < channel.Count; j++)
                {
                    _usedBufferIds.Add(channel.Samples[j].BufferId);
                }
            }
            for (int i = 0; i < _bufferIds.Length; i++)
            {
                int id = _bufferIds[i];
                if (!_usedBufferIds.Contains(id))
                {
                    bufferId = id;
                    break;
                }
            }
            // sfxtodo: if we don't find an unused buffer, we should stop SFX currently playing with the one we overwrite
            ALFormat format = sample.Format == WaveFormat.ADPCM ? ALFormat.Mono16 : ALFormat.Mono8;
            AL.BufferData(bufferId, format, sample.WaveData.Value, sample.SampleRate);
            return bufferId;
        }

        public static void Update(float time)
        {
            Vector3 listenerPos = PlayerEntity.Main.CameraInfo.Position;
            Vector3 listenerUp = PlayerEntity.Main.CameraInfo.UpVector;
            Vector3 listenerFacing = PlayerEntity.Main.CameraInfo.Facing;
            AL.Listener(ALListener3f.Position, ref listenerPos);
            AL.Listener(ALListenerfv.Orientation, ref listenerFacing, ref listenerUp);
            Debug.Assert(_streamChannel != -1);
            Debug.Assert(_streamBuffer != -1);
            Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
            AL.Source(_streamChannel, ALSource3f.Position, ref sourcePos);
            AL.Source(_streamChannel, ALSourcef.ReferenceDistance, Single.MaxValue);
            AL.Source(_streamChannel, ALSourcef.MaxDistance, Single.MaxValue);
            AL.Source(_streamChannel, ALSourcef.RolloffFactor, 1);
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.PlayTime >= 0)
                {
                    // sktodo: DGN need to stop and start based on volume,
                    // and unlink on the next frame if all their entries have stopped
                    bool playing = false;
                    for (int j = 0; j < channel.Count; j++)
                    {
                        AL.GetSource(channel.ChannelIds[j], ALGetSourcei.SourceState, out int value);
                        var state = (ALSourceState)value;
                        if (state == ALSourceState.Playing)
                        {
                            if (channel.Volume[j] == 0)
                            {
                                // DGN reduced to zero volume
                                AL.SourceStop(channel.ChannelIds[j]);
                                continue;
                            }
                            playing = true;
                            break;
                        }
                    }
                    if (playing)
                    {
                        channel.PlayTime += time;
                        channel.UpdatePosition();
                        channel.UpdateParameters();
                    }
                    else
                    {
                        channel.Stop();
                    }
                }
            }
            UpdateStreams(time);
        }

        public static int CountPlayingSfx(int id)
        {
            int count = 0;
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.Handle != -1 && channel.SfxId == id)
                {
                    count++;
                }
            }
            return count;
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
        private static int _streamChannel = -1;
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
                    AL.GetSource(_streamChannel, ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state != ALSourceState.Initial && state != ALSourceState.Playing)
                    {
                        AL.SourceStop(_streamChannel);
                        AL.Source(_streamChannel, ALSourcei.Buffer, 0);
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
                    AL.Source(_streamChannel, ALSourcei.Buffer, _streamBuffer);
                    // sfxtodo: loop points
                    AL.Source(_streamChannel, ALSourceb.Looping, item.Stream.Loop);
                    AL.Source(_streamChannel, ALSourcef.Gain, Volume * item.Stream.Volume);
                    AL.SourcePlay(_streamChannel);
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
            AL.GenBuffers(_bufferIds);
            AL.GenSources(_channelIds);
            for (int i = 0; i < _channelIds.Length; i += 3)
            {
                _channels[i / 3] = new SoundChannel(_channelIds[i], _channelIds[i + 1], _channelIds[i + 2]);
            }
            _streamBuffer = AL.GenBuffer();
            _streamChannel = AL.GenSource();
            if (!Features.LogSpatialAudio)
            {
                AL.DistanceModel(ALDistanceModel.LinearDistanceClamped);
            }
        }

        public static void ShutDown()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                for (int j = 0; j < channel.Count; j++)
                {
                    AL.GetSource(channel.ChannelIds[j], ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state == ALSourceState.Playing)
                    {
                        channel.Stop();
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
