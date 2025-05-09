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
        public float Volume { get; set; } = 1;
        public bool Self { get; set; }

        public void Update(Vector3 position, int rangeIndex)
        {
            if (rangeIndex == -1)
            {
                Position = Sfx.Instance.GetListenerPosition();
                ReferenceDistance = Single.MaxValue;
                MaxDistance = Single.MaxValue;
                Self = true;
            }
            else
            {
                Position = position;
                IReadOnlyList<Sound3dEntry> rangeData = Sfx.Instance.RangeData;
                if (rangeIndex >= 0 && rangeIndex < rangeData.Count)
                {
                    Sound3dEntry data = rangeData[rangeIndex];
                    ReferenceDistance = data.FalloffDistance / 4096f;
                    MaxDistance = data.MaxDistance / 4096f;
                }
                Self = false;
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
                    Sfx.Instance.PlayDgn(id, this, loop, noUpdate, recency, cancellable, amountA, amountB);
                }
                else if ((id & 0x4000) != 0)
                {
                    Sfx.Instance.PlayScript(id, this, noUpdate, recency, sourceOnly, cancellable);
                }
                else
                {
                    Sfx.Instance.PlaySample(id, this, loop, noUpdate, recency, sourceOnly, cancellable);
                }
            }
        }

        public int PlayFreeSfx(int id)
        {
            if (id >= 0)
            {
                Debug.Assert((id & 0x8000) == 0);
                if ((id & 0x4000) != 0)
                {
                    Sfx.Instance.PlayScript(id, source: null, noUpdate: false, recency: -1,
                        sourceOnly: false, cancellable: false);
                    return -1;
                }
                return Sfx.Instance.PlaySample(id, source: null, loop: null, noUpdate: false,
                    recency: -1, sourceOnly: false, cancellable: false);
            }
            return -1;
        }

        public void PlayEnvironmentSfx(int id)
        {
            Sfx.Instance.PlayEnvironmentSfx(id, this);
        }

        public bool CheckEnvironmentSfx(int id)
        {
            return Sfx.Instance.CheckEnvironmentSfx(id);
        }

        public void StopAllSfx(bool force = false)
        {
            Sfx.Instance.StopSoundFromSource(this, force);
        }

        public void StopSfx(SfxId id)
        {
            StopSfx((int)id);
        }

        public void StopSfx(int id)
        {
            Sfx.Instance.StopSoundFromSource(this, id);
        }

        public void StopFreeSfx(SfxId id)
        {
            StopFreeSfx((int)id);
        }

        public void StopFreeSfx(int id)
        {
            Sfx.Instance.StopSoundById(id);
        }

        public void StopSfxByHandle(int handle)
        {
            Sfx.Instance.StopSoundByHandle(handle);
        }

        public void StopFreeSfxScripts()
        {
            Sfx.Instance.StopFreeSfxScripts();
        }

        public bool IsHandlePlaying(int handle)
        {
            return Sfx.Instance.IsHandlePlaying(handle);
        }

        public int CountPlayingSfx(SfxId id)
        {
            return Sfx.Instance.CountPlayingSfx((int)id);
        }

        public int CountPlayingSfx(int id)
        {
            return Sfx.Instance.CountPlayingSfx(id);
        }

        public int CountSourcePlayingSfx(SfxId id)
        {
            return Sfx.Instance.CountSourcePlayingSfx((int)id, this);
        }

        public int CountSourcePlayingSfx(int id)
        {
            return Sfx.Instance.CountSourcePlayingSfx(id, this);
        }

        public void QueueStream(VoiceId id, float delay = 0, float expiration = 0)
        {
            Sfx.QueueStream(id, delay, expiration);
        }
    }

    public static class Sfx
    {
        public static SfxInstanceBase Instance { get; private set; } = null!;
        public static float Volume { get; set; } = 0.35f;

        public static bool SfxMute { get; set; }
        public static int ForceFieldSfxMute { get; set; }
        public static int TimedSfxMute { get; set; }
        public static int LongSfxMute { get; set; }

        public static SoundCapability CheckAudioLoad()
        {
            bool loopPointsSupported = false;
            try
            {
                ALDevice device = ALC.OpenDevice(null);
                ALContext context = ALC.CreateContext(device, new ALContextAttributes());
                ALC.MakeContextCurrent(context);
                loopPointsSupported = AL.LoopPoints.IsExtensionPresent();
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(context);
                ALC.CloseDevice(device);
            }
            catch (DllNotFoundException)
            {
                return SoundCapability.None;
            }
            return loopPointsSupported ? SoundCapability.Supported : SoundCapability.Unsupported;
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

        public static void Update(float time)
        {
            Instance?.Update(time);
        }

        public static void QueueStream(VoiceId id, float delay = 0, float expiration = 0)
        {
            Instance.QueueStream((int)id, delay, expiration);
        }

        public static void Load(Scene scene)
        {
            Instance = new SfxInstance();
            try
            {
                Instance.Load(scene);
            }
            catch (DllNotFoundException)
            {
                Instance = new SfxInstanceBase();
            }
            SfxMute = false;
            ForceFieldSfxMute = 0;
            TimedSfxMute = 0;
            LongSfxMute = 0;
        }

        public static void ShutDown()
        {
            if (Instance != null)
            {
                Instance.ShutDown();
                Instance = null!;
            }
        }
    }

    public class SfxInstance : SfxInstanceBase
    {
        private const int _maxPerInst = 12;

        public class SoundInstance
        {
            public int Count { get; set; }
            public SoundSample[] Samples { get; } = new SoundSample[_maxPerInst];
            public SoundChannel[] Channels { get; } = new SoundChannel[_maxPerInst];
            public DgnFile? DgnFile { get; set; }
            public SfxScriptFile? ScriptFile { get; set; }
            public int ScriptIndex { get; set; } = -1;
            public SoundSource? Source { get; set; }
            public float[] Volume { get; } = new float[_maxPerInst];
            public float[] Pitch { get; } = new float[_maxPerInst];
            public float PlayTime { get; set; } = -1;
            public int SfxId { get; set; } = -1;
            public bool NoUpdate { get; set; }
            public bool[] Loop { get; } = new bool[_maxPerInst];
            public bool Cancellable { get; set; }

            public int Handle { get; set; } = -1;
            public static int NextHandle { get; set; } = 0;

            public SoundInstance()
            {
                for (int i = 0; i < _maxPerInst; i++)
                {
                    Volume[i] = 1;
                    Pitch[i] = 1;
                }
            }

            public void PlayChannel(int index)
            {
                int channelId = Channels[index].Id;
                SoundSample sample = Samples[index];
                AL.Source(channelId, ALSourceb.Looping, sample.BufferCount == 1 && Loop[index]);
                AL.SourcePlay(channelId);
            }

            public bool IsLooping()
            {
                for (int i = 0; i < _maxPerInst; i++)
                {
                    if (Loop[i])
                    {
                        return true;
                    }
                }
                return false;
            }

            public void UpdatePosition()
            {
                if (Source == null || Source.Self)
                {
                    Vector3 sourcePos = Sfx.Instance.GetListenerPosition();
                    for (int i = 0; i < _maxPerInst; i++)
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
                    for (int i = 0; i < _maxPerInst; i++)
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
                for (int i = 0; i < _maxPerInst; i++)
                {
                    SoundChannel? channel = Channels[i];
                    if (channel == null)
                    {
                        continue;
                    }
                    int channelId = channel.Id;
                    float mute = Sfx.SfxMute && Source != null ? 0 : 1;
                    float sourceMute = Source?.Volume ?? 1;
                    AL.Source(channelId, ALSourcef.Gain, Sfx.Volume * Volume[i] * Samples[i].Volume * mute * sourceMute);
                    AL.Source(channelId, ALSourcef.Pitch, Pitch[i]);
                    AL.Source(channelId, ALSourceb.SourceRelative, false);
                    AL.Source(channelId, ALSourcef.RolloffFactor, 1);
                }
            }

            public void Stop()
            {
                for (int i = 0; i < _maxPerInst; i++)
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
                    Loop[i] = false;
                }
                PlayTime = -1;
                SfxId = -1;
                DgnFile = null;
                ScriptFile = null;
                ScriptIndex = -1;
                Source = null;
                NoUpdate = false;
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

        private ALDevice _device = ALDevice.Null;
        private ALContext _context = ALContext.Null;
        private bool _loopPointSupport = false;
        private readonly SoundBuffer[] _buffers = new SoundBuffer[64];
        private readonly SoundChannel[] _channels = new SoundChannel[128];
        private readonly SoundInstance[] _instances = new SoundInstance[128];

        private IReadOnlyList<SoundSample> _samples = null!;
        private IReadOnlyList<DgnFile> _dgnFiles = null!;
        private IReadOnlyList<SfxScriptFile> _sfxScripts = null!;
        private IReadOnlyList<Sound3dEntry> _rangeData = null!;
        public override IReadOnlyList<Sound3dEntry> RangeData => _rangeData;

        private Scene? _scene = null;

        public override Vector3 GetListenerPosition()
        {
            if (_scene == null)
            {
                return base.GetListenerPosition();
            }
            if (_scene.CameraMode == CameraMode.Player)
            {
                return PlayerEntity.Main.CameraInfo.Position;
            }
            return _scene.CameraPosition;
        }

        public override Vector3 GetListenerUp()
        {
            if (_scene == null)
            {
                return base.GetListenerUp();
            }
            if (_scene.CameraMode == CameraMode.Player)
            {
                return PlayerEntity.Main.CameraInfo.UpVector;
            }
            return _scene.ViewMatrix.Row1.Xyz.Normalized();
        }

        public override Vector3 GetListenerFacing()
        {
            if (_scene == null)
            {
                return base.GetListenerFacing();
            }
            if (_scene.CameraMode == CameraMode.Player)
            {
                return PlayerEntity.Main.CameraInfo.Facing;
            }
            return _scene.ViewMatrix.Row2.Xyz.Normalized();
        }

        public override int PlaySample(int id, SoundSource? source, bool? loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            SoundInstance? inst = PlaySampleGetInst(id, source, loop, noUpdate, recency, sourceOnly, cancellable);
            if (inst == null)
            {
                return -1;
            }
            return inst.Handle;
        }

        private SoundInstance? PlaySampleGetInst(int id, SoundSource? source, bool? loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            bool setUp = SetUpInstance(id, source, loop.GetValueOrDefault(),
                recency, sourceOnly, cancellable, out SoundInstance inst);
            if (!setUp)
            {
                return null;
            }
            if (!SetUpSample(id, inst, index: 0))
            {
                return null;
            }
            inst.Loop[0] = loop ?? inst.Samples[0].Loop;
            StartInstance(inst, noUpdate);
            return inst;
        }

        public override void PlayDgn(int id, SoundSource? source, bool loop, bool noUpdate,
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
                inst.Loop[i] = loop;
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

        public override void PlayScript(int id, SoundSource? source, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            Debug.Assert((id & 0x4000) != 0);
            int scriptId = id & 0x3FFF;
            Debug.Assert(scriptId >= 0 && scriptId < _sfxScripts.Count);
            SfxScriptFile script = _sfxScripts[scriptId];
            if (script.Entries.Count == 0) // e.g. TELEPORT_ACTIVATE_SCR (54)
            {
                return;
            }
            bool setUp = SetUpInstance(id, source, loop: false, recency, sourceOnly, cancellable, out SoundInstance inst);
            if (!setUp)
            {
                return;
            }
            inst.NoUpdate = noUpdate;
            inst.ScriptFile = script;
        }

        private bool SetUpInstance(int id, SoundSource? source, bool loop,
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
            inst.Cancellable = cancellable;
            inst.SfxId = id;
            inst.Handle = SoundInstance.NextHandle++;
            return true;
        }

        private bool SetUpSample(int id, SoundInstance inst, int index)
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
            else
            {
                sample.BufferCount = sample.MaxBuffers;
            }
            if (channel.BufferId != sample.BufferId)
            {
                int bufferId = sample.BufferId;
                AL.Source(channel.Id, ALSourcei.Buffer, bufferId);
                channel.BufferId = sample.BufferId;
            }
            return true;
        }

        private void UpdateInstance(SoundInstance inst, bool noUpdate)
        {
            inst.NoUpdate = false;
            inst.UpdatePosition();
            inst.NoUpdate = noUpdate;
            inst.UpdateParameters();
        }

        private void StartInstance(SoundInstance inst, bool noUpdate)
        {
            UpdateInstance(inst, noUpdate);
            for (int i = 0; i < inst.Count; i++)
            {
                inst.PlayChannel(index: i);
            }
        }

        // recency = 0 --> started playing on the current frame
        // receny = 1 --> started playing within the last second
        // recency = Single.MaxValue --> playing at all
        private SoundInstance? FindRecentSamplePlay(int id, float recency, SoundSource? source)
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

        private void UpdateDgn(SoundInstance inst, float amountA, float amountB)
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
                inst.Pitch[i] = Sfx.CalculatePitchDiv(pitchFac);
            }
        }

        private float GetDgnValue(IReadOnlyList<DgnData> data, float amount)
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

        public override void StopAllSound(bool force = true)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.SfxId != -1 && (force || inst.Source != null || inst.IsLooping()))
                {
                    inst.Stop();
                }
            }
            AL.SourceStop(_streamInstance);
        }

        public override void StopSoundFromSource(SoundSource source, bool force)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.Source == source)
                {
                    bool loop = false;
                    for (int j = 0; j < _maxPerInst; j++)
                    {
                        if (inst.Loop[j])
                        {
                            loop = true;
                            break;
                        }
                    }
                    if ((force || loop || inst.Cancellable) && (!force || !inst.NoUpdate))
                    {
                        inst.Stop();
                    }
                }
            }
        }

        public override void StopSoundFromSource(SoundSource source, int id)
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

        public override void StopSoundById(int id)
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

        public override void StopSoundByHandle(int handle)
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

        public override void StopFreeSfxScripts()
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.ScriptFile != null)
                {
                    inst.Stop();
                }
            }
        }

        public override bool IsHandlePlaying(int handle)
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

        private SoundInstance FindInstance(SoundSource? source)
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

        private SoundChannel? GetChannel(int bufferId)
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

        private void BufferData(SoundSample sample)
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
                if (_loopPointSupport)
                {
                    AL.LoopPoints.Buffer(dest.Id, BufferLoopPoint.LoopPointsSOFT, sample.LoopStart, sample.LoopStart + sample.LoopLength);
                }
                sample.BufferCount = sample.MaxBuffers = 1;
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
                for (int j = 0; j < _maxPerInst; j++)
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

        private void UpdateScript(SoundInstance inst, float time)
        {
            Debug.Assert(inst.ScriptFile != null);
            if (inst.PlayTime >= 0)
            {
                inst.PlayTime += time;
            }
            else
            {
                inst.PlayTime = 0;
            }
            int playingCount = 0;
            for (int i = 0; i < _maxPerInst; i++)
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
                        inst.Loop[i] = false;
                    }
                }
            }
            if (inst.ScriptIndex >= inst.ScriptFile.Entries.Count - 1)
            {
                if (playingCount == 0)
                {
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
                    continue;
                }
                int index = -1;
                for (int j = 0; j < _maxPerInst; j++)
                {
                    if (inst.Channels[j] == null)
                    {
                        index = j;
                        break;
                    }
                }
                if (index == -1)
                {
                    Debug.Assert(false, $"SFX script played more than {_maxPerInst} concurrent channels");
                    inst.Stop();
                    return;
                }
                inst.Volume[index] = entry.Volume;
                inst.Pitch[index] = entry.Pitch;
                if (!SetUpSample(sfxId, inst, index))
                {
                    inst.Stop();
                    return;
                }
                UpdateInstance(inst, inst.NoUpdate);
                // -1 indicates no panning, which is not the same as 0 which indicates centered panning
                // in the latter case, the center pan overrides any sourced positional audio
                if (entry.Pan > -1)
                {
                    int channelId = inst.Channels[index].Id;
                    AL.Source(channelId, ALSourceb.SourceRelative, true);
                    AL.Source(channelId, ALSourcef.RolloffFactor, 0);
                    Vector3 position = Vector3.Zero;
                    if (MathF.Abs(entry.Pan) > 1 / 128f)
                    {
                        position = new Vector3(entry.Pan, 0, -MathF.Sqrt(1 - entry.Pan * entry.Pan));
                    }
                    AL.Source(channelId, ALSource3f.Position, ref position);
                }
                inst.Loop[index] = (entry.SfxData & 0x4000) != 0;
                inst.PlayChannel(index);
            }
        }

        public override void Update(float time)
        {
            Vector3 listenerPos = GetListenerPosition();
            Vector3 listenerUp = GetListenerUp();
            Vector3 listenerFacing = GetListenerFacing();
            AL.Listener(ALListener3f.Position, ref listenerPos);
            AL.Listener(ALListenerfv.Orientation, ref listenerFacing, ref listenerUp);
            Debug.Assert(_streamInstance != -1);
            Debug.Assert(_streamBuffer != -1);
            AL.Source(_streamInstance, ALSource3f.Position, ref listenerPos);
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
                        if (inst.Source == null || !Sfx.SfxMute)
                        {
                            UpdateScript(inst, time);
                        }
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
            if (Sfx.LongSfxMute == 0)
            {
                UpdateEnvironmentSfx();
            }
            UpdateStreams(time);
        }

        public override int CountPlayingSfx(int id)
        {
            return CountPlayingSfx(id, source: null);
        }

        public override int CountSourcePlayingSfx(int id, SoundSource source)
        {
            return CountPlayingSfx(id, source);
        }

        private int CountPlayingSfx(int id, SoundSource? source)
        {
            int count = 0;
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                if (inst.Handle != -1 && inst.SfxId == id && (source == null || inst.Source == source))
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

            public void Reset()
            {
                if (Handle != -1)
                {
                    Sfx.Instance.StopSoundByHandle(Handle);
                }
                Handle = -1;
                Instances = 0;
                DistanceSquared = Single.MaxValue;
            }
        }

        private static readonly IReadOnlyList<EnvironmentItem> _environmentItems = new EnvironmentItem[10]
        {
            /* 0 */ new EnvironmentItem(SfxId.ELECTRO_WAVE2),
            /* 1 */ new EnvironmentItem(SfxId.ELECTRICITY),
            /* 2 */ new EnvironmentItem(SfxId.ELECTRIC_BARRIER),
            /* 3 */ new EnvironmentItem(SfxId.ENERGY_BALL),
            /* 4 */ new EnvironmentItem(SfxId.BLUE_FLAME),
            /* 5 */ new EnvironmentItem(SfxId.CYLINDER_BOSS_ATTACK),
            /* 6 */ new EnvironmentItem(SfxId.CYLINDER_BOSS_SPIN),
            /* 7 */ new EnvironmentItem(SfxId.BUBBLES),
            /* 8 */ new EnvironmentItem(SfxId.ELEVATOR2_START),
            /* 9 */ new EnvironmentItem(SfxId.GOREA_ATTACK3_LOOP)
        };

        public override void PlayEnvironmentSfx(int index, SoundSource source)
        {
            // in-game, environment SFX update their volume to the source's if it's greater, but that volume is already
            // the rest of the 3D sound calculation, and the pan_x is also added as a percentage of the difference based on volume
            // --> we can't really do that, so we just take the parameters of the closest source, and have no panning (for now?)
            Debug.Assert(index >= 0 && index < _environmentItems.Count);
            EnvironmentItem item = _environmentItems[index];
            Vector3 listenerPos = GetListenerPosition();
            Vector3 listenerFacing = GetListenerFacing();
            float distSqr = Vector3.DistanceSquared(source.Position, listenerPos);
            if (distSqr < item.DistanceSquared)
            {
                item.DistanceSquared = distSqr;
                float dist = MathF.Sqrt(distSqr);
                item.Source.Position = listenerPos + listenerFacing * dist;
                item.Source.ReferenceDistance = source.ReferenceDistance;
                item.Source.MaxDistance = source.MaxDistance;
                item.Source.RolloffFactor = source.RolloffFactor;
            }
            item.Instances++;
        }

        public override bool CheckEnvironmentSfx(int index)
        {
            Debug.Assert(index >= 0 && index < _environmentItems.Count);
            return _environmentItems[index].Instances > 0;
        }

        private void UpdateEnvironmentSfx()
        {
            for (int i = 0; i < _environmentItems.Count; i++)
            {
                EnvironmentItem item = _environmentItems[i];
                if (item.Instances > 0)
                {
                    SoundInstance? inst = PlaySampleGetInst(item.SfxId, item.Source, loop: true,
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

        public override void StopEnvironmentSfx()
        {
            for (int i = 0; i < _environmentItems.Count; i++)
            {
                EnvironmentItem item = _environmentItems[i];
                if (item.Instances > 0)
                {
                    item.Reset();
                }
            }
        }

        private class QueueItem
        {
            public SoundStream? Stream { get; set; }
            public float DelayTimer { get; set; }
            public float ExpirationTimer { get; set; }
            public bool Playing { get; set; }
        }

        public SoundData _soundData = null!;
        private readonly LinkedList<QueueItem> _activeQueue = new LinkedList<QueueItem>();
        private readonly Queue<QueueItem> _inactiveQueue = new Queue<QueueItem>(16);
        private int _streamInstance = -1;
        private int _streamBuffer = -1;

        public override void QueueStream(int id, float delay, float expiration)
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

        private void UpdateStreams(float time)
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
                    AL.Source(_streamInstance, ALSourceb.Looping, item.Stream.Loop);
                    AL.Source(_streamInstance, ALSourcef.Gain, Sfx.Volume * item.Stream.Volume);
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

        public override void Load(Scene scene)
        {
            _scene = scene;
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
            _loopPointSupport = AL.LoopPoints.IsExtensionPresent();
            int[] bufferIds = new int[_buffers.Length * 2];
            AL.GenBuffers(bufferIds);
            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = new SoundBuffer(bufferIds[i * 2]);
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

        public override void ShutDown()
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                SoundInstance inst = _instances[i];
                for (int j = 0; j < _maxPerInst; j++)
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
            for (int i = 0; i < _environmentItems.Count; i++)
            {
                _environmentItems[i].Reset();
            }
            AL.SourceStop(_streamInstance);
            ALC.MakeContextCurrent(ALContext.Null);
            Task.Run(() =>
            {
                ALC.DestroyContext(_context);
                ALC.CloseDevice(_device);
                _context = ALContext.Null;
                _device = ALDevice.Null;
            });
            _scene = null;
        }
    }

    public class SfxInstanceBase
    {
        public virtual IReadOnlyList<Sound3dEntry> RangeData { get; } = new List<Sound3dEntry>();

        public virtual Vector3 GetListenerPosition()
        {
            return Vector3.Zero;
        }

        public virtual Vector3 GetListenerUp()
        {
            return Vector3.UnitY;
        }

        public virtual Vector3 GetListenerFacing()
        {
            return -Vector3.UnitZ;
        }

        public virtual void QueueStream(int id, float delay, float expiration)
        {
        }

        public virtual void StopSoundByHandle(int handle)
        {
        }

        public virtual void PlayDgn(int id, SoundSource? source, bool loop, bool noUpdate,
            float recency, bool cancellable, float amountA, float amountB)
        {
        }

        public virtual void PlayScript(int id, SoundSource? source, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
        }

        public virtual int PlaySample(int id, SoundSource? source, bool? loop, bool noUpdate,
            float recency, bool sourceOnly, bool cancellable)
        {
            return -1;
        }

        public virtual void PlayEnvironmentSfx(int index, SoundSource source)
        {
        }

        public virtual bool CheckEnvironmentSfx(int index)
        {
            return false;
        }

        public virtual void StopSoundFromSource(SoundSource source, bool force)
        {
        }

        public virtual void StopSoundFromSource(SoundSource source, int id)
        {
        }

        public virtual void StopEnvironmentSfx()
        {
        }

        public virtual void StopAllSound(bool force = false)
        {
        }

        public virtual void StopSoundById(int id)
        {
        }

        public virtual void StopFreeSfxScripts()
        {
        }

        public virtual bool IsHandlePlaying(int handle)
        {
            return false;
        }

        public virtual int CountPlayingSfx(int id)
        {
            return 0;
        }

        public virtual int CountSourcePlayingSfx(int id, SoundSource source)
        {
            return 0;
        }

        public virtual void Update(float time)
        {
        }

        public virtual void Load(Scene scene)
        {
        }

        public virtual void ShutDown()
        {
        }
    }
}
