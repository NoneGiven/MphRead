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

        public void PlaySfx(SfxId id, bool loop = false, bool ignoreParams = false, bool single = false)
        {
            PlaySfx((int)id, loop, ignoreParams, single);
        }

        public void PlayFreeSfx(SfxId id)
        {
            PlayFreeSfx((int)id);
        }

        public void PlaySfx(int id, bool loop = false, bool ignoreParams = false, bool single = false)
        {
            // sktodo: support DGN and scripts
            if (id >= 0 && (id & 0xC000) == 0)
            {
                Sfx.PlaySample(id, this, loop, ignoreParams, single);
            }
        }

        public void PlayFreeSfx(int id)
        {
            if (id >= 0)
            {
                Debug.Assert((id & 0xC000) == 0);
                Sfx.PlaySample(id, null, loop: false, ignoreParams: false, single: false);
            }
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
    }

    // sktodo: pause all sounds when debugger breaks, frame advance is on, etc.
    public static class Sfx
    {
        private class SoundChannel
        {
            public int Handle { get; set; }
            public int BufferId { get; set; }
            public SoundSource? Source { get; set; }
            public float SampleVolume { get; set; } = 1;
            public float PlayTime { get; set; } = -1;
            public int SfxId { get; set; } = -1;
            public bool IgnoreParams { get; set; }
            public bool Loop { get; set; }

            public SoundChannel(int handle)
            {
                Handle = handle;
            }

            public void Update()
            {
                if (Source != null && !IgnoreParams)
                {
                    // sktodo: velocity, maybe also direction?
                    Vector3 sourcePos = Source.Position;
                    AL.Source(Handle, ALSource3f.Position, ref sourcePos);
                    AL.Source(Handle, ALSourcef.ReferenceDistance, Source.ReferenceDistance);
                    AL.Source(Handle, ALSourcef.MaxDistance, Source.MaxDistance);
                    AL.Source(Handle, ALSourcef.RolloffFactor, Source.RolloffFactor);
                }
                else
                {
                    Vector3 sourcePos = PlayerEntity.Main.CameraInfo.Position;
                    AL.Source(Handle, ALSource3f.Position, ref sourcePos);
                    AL.Source(Handle, ALSourcef.ReferenceDistance, Single.MaxValue);
                    AL.Source(Handle, ALSourcef.MaxDistance, Single.MaxValue);
                    AL.Source(Handle, ALSourcef.RolloffFactor, 1);
                }
                // sktodo: this volume multiplication isn't really right
                AL.Source(Handle, ALSourcef.Gain, Volume * SampleVolume);
            }

            public void Stop()
            {
                AL.SourceStop(Handle);
                AL.Source(Handle, ALSourcei.Buffer, 0);
                PlayTime = -1;
                BufferId = 0;
                SfxId = -1;
                Source = null;
                SampleVolume = 1;
                IgnoreParams = false;
                Loop = false;
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

        public static float Volume { get; set; } = 0.5f;

        public static void PlaySample(int id, SoundSource? source, bool loop, bool ignoreParams, bool single)
        {
            Debug.Assert(id >= 0 && id < _samples.Count);
            if (single && source != null && IsSourcePlayingSample(source, id))
            {
                return;
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
            channel.IgnoreParams = false;
            channel.Update();
            channel.IgnoreParams = ignoreParams;
            channel.SampleVolume = sample.Volume;
            if (channel.BufferId != bufferId)
            {
                AL.Source(channel.Handle, ALSourcei.Buffer, bufferId);
                channel.BufferId = bufferId;
            }
            // sktodo: loop points (needs opentk update)
            AL.Source(channel.Handle, ALSourceb.Looping, loop);
            channel.Loop = loop;
            channel.SfxId = id;
            AL.SourcePlay(channel.Handle);
        }

        private static bool IsSourcePlayingSample(SoundSource source, int id)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.Source == source && channel.SfxId == id)
                {
                    return true;
                }
            }
            return false;
        }

        public static void StopSoundFromSource(SoundSource source, bool force)
        {
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.Source == source)
                {
                    if ((force || channel.Loop) && (!force || !channel.IgnoreParams))
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
            for (int i = 0; i < _channels.Length; i++)
            {
                SoundChannel channel = _channels[i];
                if (channel.PlayTime >= 0)
                {
                    AL.GetSource(channel.Handle, ALGetSourcei.SourceState, out int value);
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
            _device = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, new ALContextAttributes());
            ALC.MakeContextCurrent(_context);
            AL.GenBuffers(_bufferIds);
            AL.GenSources(_channelIds);
            for (int i = 0; i < _channelIds.Length; i++)
            {
                _channels[i] = new SoundChannel(_channelIds[i]);
            }
            // sktodo: control this with a setting
            // sktodo: either way, the panning from left to right is way too harsh
            // sktodo: low quality sound effect interpolation should be better
            AL.DistanceModel(ALDistanceModel.LinearDistanceClamped);
        }

        public static void ShutDown()
        {
            for (int i = 0; i < _channelIds.Length; i++)
            {
                SoundChannel channel = _channels[i];
                AL.GetSource(channel.Handle, ALGetSourcei.SourceState, out int value);
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
