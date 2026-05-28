using System;
using System.Diagnostics;
using NCSF123;
using NCSFPlayer;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace MphRead
{
    public static class Music
    {
        // sktodo: need seek/resume for suspended music
        private static MiniAudioEngine _audioEngine;
        private static AudioPlaybackDevice _playbackDevice;
        private static RawDataProvider? _provider = null;
        private static NCSFPlayerStream? _stream = null;
        private static SoundPlayer? _player = null;
        private static readonly AudioFormat _format;

        private const int _sampleRate = 32728;

        static Music()
        {
            _format = new AudioFormat()
            {
                SampleRate = _sampleRate,
                Channels = 2,
                Format = SampleFormat.F32
            };
            _audioEngine = new MiniAudioEngine();
            _playbackDevice = _audioEngine.InitializePlaybackDevice(deviceInfo: null, _format);
        }

        public static void Play(SeqId seqId, ushort tracks = UInt16.MaxValue, float volume = 1)
        {
            // todo?: non-looping seqs (jingles, credits) will apparently stay in playing state indefinitely when playForever is on
            // --> should get overwritten with another seq before long
            Stop();
            if (seqId == SeqId.None)
            {
                return;
            }
            string path = Paths.Combine(Paths.FileSystem, "_seq", Metadata.SequenceFiles[(int)seqId]);
            // todo: need to look at allocations (including recreating these objects, but especially the byte and float lists internal to NCSF)
            _stream = new NCSFPlayerStream(path, (uint)_sampleRate, Interpolation.None, skipSilenceOnStartSec: 5,
                defaultLengthInMS: 115000, defaultFadeInMS: 5000, NCSF123.VolumeType.ReplayGainAlbum, PeakType.ReplayGainTrack,
                playForever: true, volume, channelMutes: 0, (ushort)(tracks ^ UInt16.MaxValue), ignoreVolume: false);
            _provider = new RawDataProvider(_stream, SampleFormat.F32, _sampleRate);
            _player = new SoundPlayer(_audioEngine, _format, _provider);
            _playbackDevice.MasterMixer.AddComponent(_player);
            _playbackDevice.Start();
            _player.Play();
        }

        public static void Pause()
        {
            _player?.Pause();
        }

        public static void Resume()
        {
            _player?.Play();
        }

        public static float Volume
        {
            get
            {
                if (_stream != null)
                {
                    return _stream.VolumeModification;
                }
                return 0;
            }
            set
            {
                if (_stream != null)
                {
                    _stream.VolumeModification = Math.Clamp(value, 0, 1);
                }
            }
        }

        public static ushort Tracks
        {
            get
            {
                if (_stream != null)
                {
                    return (ushort)(_stream.Player.TrackMutes ^ UInt16.MaxValue);
                }
                return 0;
            }
            set
            {
                if (_stream != null)
                {
                    _stream.Player.TrackMutes = (ushort)(value ^ UInt16.MaxValue);
                }
            }
        }

        public static ushort Tempo
        {
            get
            {
                if (_stream != null)
                {
                    return _stream.Player.Tempo;
                }
                return 0;
            }
            set
            {
                if (_stream != null)
                {
                    _stream.Player.Tempo = value;
                }
            }
        }

        public static void Stop()
        {
            if (_player != null)
            {
                Debug.Assert(_provider != null);
                Debug.Assert(_stream != null);
                _player.Stop();
                _playbackDevice.Stop();
                _playbackDevice.MasterMixer.RemoveComponent(_player);
                _provider.Dispose();
                _stream.Dispose();
                _player.Dispose();
                _provider = null;
                _stream = null;
                _player = null;
            }
        }
    }
}
