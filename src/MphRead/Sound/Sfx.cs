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

        // sktodo: should we be updating rolloff factor? if so, how?
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
            float recency = -1, bool sourceOnly = false, bool cancellable = false)
        {
            PlaySfx((int)id, loop, noUpdate, recency, sourceOnly, cancellable);
        }

        public int PlayFreeSfx(SfxId id)
        {
            return PlayFreeSfx((int)id);
        }

        public void PlaySfx(int id, bool loop = false, bool noUpdate = false,
            float recency = -1, bool sourceOnly = false, bool cancellable = false)
        {
            // sktodo: support DGN and scripts
            if (id >= 0 && (id & 0xC000) == 0)
            {
                Sfx.PlaySample(id, this, loop, noUpdate, recency, sourceOnly, cancellable);
            }
        }

        public int PlayFreeSfx(int id)
        {
            if (id >= 0)
            {
                Debug.Assert((id & 0xC000) == 0);
                return Sfx.PlaySample(id, null, loop: false, noUpdate: false,
                    recency: -1, sourceOnly: false, cancellable: false);
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

    // sktodo: pause all sounds when debugger breaks, frame advance is on, etc.
    public static class Sfx
    {
        private class SoundChannel
        {
            public int ChannelId { get; set; }
            public int BufferId { get; set; }
            public SoundSource? Source { get; set; }
            public float SampleVolume { get; set; } = 1;
            public float PlayTime { get; set; } = -1;
            public int SfxId { get; set; } = -1;
            public bool NoUpdate { get; set; }
            public bool Loop { get; set; }
            public bool Cancellable { get; set; }

            public int Handle { get; set; } = -1;
            public static int NextHandle { get; set; } = 0;

            public SoundChannel(int channelId)
            {
                ChannelId = channelId;
            }

            public void Update()
            {
                if (Source == null)
                {
                    Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
                    AL.Source(ChannelId, ALSource3f.Position, ref sourcePos);
                    AL.Source(ChannelId, ALSourcef.ReferenceDistance, Single.MaxValue);
                    AL.Source(ChannelId, ALSourcef.MaxDistance, Single.MaxValue);
                    AL.Source(ChannelId, ALSourcef.RolloffFactor, 1);
                }
                else if (!NoUpdate)
                {
                    // sktodo: velocity, maybe also direction?
                    Vector3 sourcePos = Source.Position;
                    AL.Source(ChannelId, ALSource3f.Position, ref sourcePos);
                    AL.Source(ChannelId, ALSourcef.ReferenceDistance, Source.ReferenceDistance);
                    AL.Source(ChannelId, ALSourcef.MaxDistance, Source.MaxDistance);
                    AL.Source(ChannelId, ALSourcef.RolloffFactor, Source.RolloffFactor);
                }
                // sktodo: this volume multiplication isn't really right
                AL.Source(ChannelId, ALSourcef.Gain, Volume * SampleVolume);
            }

            public void Stop()
            {
                AL.SourceStop(ChannelId);
                AL.Source(ChannelId, ALSourcei.Buffer, 0);
                PlayTime = -1;
                BufferId = 0;
                SfxId = -1;
                Source = null;
                SampleVolume = 1;
                NoUpdate = false;
                Loop = false;
                Cancellable = false;
                Handle = -1;
            }
        }

        // sktodo: clear all of this stuff on shutdown
        private static ALDevice _device = ALDevice.Null;
        private static ALContext _context = ALContext.Null;
        private static readonly int[] _bufferIds = new int[64];
        private static readonly int[] _channelIds = new int[128];
        private static readonly SoundChannel[] _channels = new SoundChannel[128];

        private static IReadOnlyList<SoundSample> _samples = null!;
        private static IReadOnlyList<Sound3dEntry> _rangeData = null!;
        public static IReadOnlyList<Sound3dEntry> RangeData => _rangeData;

        public static float Volume { get; set; } = 0.35f;

        public static int PlaySample(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            Debug.Assert(id >= 0 && id < _samples.Count);
            if (loop)
            {
                // sfxtodo: this is a different code path, and it includes requests flagged with SFX_SINGLE
                // --> this "is this playing" path updates DGN parameters wen true, while the "recency" path doesn't
                recency = Single.MaxValue;
                sourceOnly = true;
            }
            if (recency >= 0 && CheckRecentSamplePlay(id, recency, sourceOnly ? source : null))
            {
                return -1;
            }
            SoundSample sample = _samples[id];
            int bufferId = sample.BufferId;
            if (bufferId == 0)
            {
                bufferId = BufferData(sample);
            }
            SoundChannel channel = FindChannel(source);
            channel.Source = source;
            channel.PlayTime = 0;
            channel.NoUpdate = false;
            channel.Update();
            channel.NoUpdate = noUpdate;
            channel.SampleVolume = sample.Volume;
            if (channel.BufferId != bufferId)
            {
                AL.Source(channel.ChannelId, ALSourcei.Buffer, bufferId);
                channel.BufferId = bufferId;
            }
            // sktodo: this volume multiplication isn't really right
            AL.Source(channel.ChannelId, ALSourcef.Gain, Volume * channel.SampleVolume);
            // sktodo: loop points (needs opentk update)
            AL.Source(channel.ChannelId, ALSourceb.Looping, loop);
            channel.Loop = loop;
            channel.Cancellable = cancellable;
            channel.SfxId = id;
            channel.Handle = SoundChannel.NextHandle++;
            AL.SourcePlay(channel.ChannelId);
            return channel.Handle;
        }

        // recency = 0 --> started playing on the current frame
        // receny = 1 --> started playing within the last second
        // recency = Single.MaxValue --> playing at all
        private static bool CheckRecentSamplePlay(int id, float recency, SoundSource? source)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if ((source == null || channel.Source == source)
                    && channel.SfxId == id && channel.PlayTime <= recency)
                {
                    return true;
                }
            }
            return false;
        }

        public static void StopAllSound()
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.BufferId != -1)
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
            // sktodo: check what flags the game really uses, and implement "don't play if existing" etc.
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
                _usedBufferIds.Add(_channels[i].BufferId);
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
            // sktodo: if we don't find an unused buffer, we should stop SFX currently playing with the one we overwrite
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
                    AL.GetSource(channel.ChannelId, ALGetSourcei.SourceState, out int value);
                    var state = (ALSourceState)value;
                    if (state == ALSourceState.Playing)
                    {
                        channel.PlayTime += time;
                        channel.Update();
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
                    // sktodo: loop points
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
                // todo: priority? other fields?
                sample.Volume = entry.InitialVolume / 127f;
                sample.Name = entry.Name;
                _ = sample.WaveData.Value;
            }
            _rangeData = SoundRead.ReadSound3dList();
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
            for (int i = 0; i < _channelIds.Length; i++)
            {
                _channels[i] = new SoundChannel(_channelIds[i]);
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
            for (int i = 0; i < _channelIds.Length; i++)
            {
                SoundChannel channel = _channels[i];
                AL.GetSource(channel.ChannelId, ALGetSourcei.SourceState, out int value);
                var state = (ALSourceState)value;
                if (state == ALSourceState.Playing)
                {
                    channel.Stop();
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
