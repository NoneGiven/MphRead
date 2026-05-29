using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MphRead.Formats.Sound;
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
        private static IReadOnlyList<MusicTrack> _musicInfo = null!;
        private static IReadOnlyList<RoomMusic> _roomMusic = null!;

        public static void Init()
        {
            _musicInfo = SoundRead.ReadInterMusicInfo();
            _roomMusic = SoundRead.ReadAssignMusic();
            _pendingTracks = 0;
            _currentMusicId = MusicId.None;
            _musicEncounterSuspension = 0;
            MusicToResume = MusicId.None;
            _playing = false;
            _paused = false;
            _musicQueued = false;
            _currentMusicSeq = SeqId.None;
            _nextMusicSeq = SeqId.None;
            _isReady = true;
            _nextTrackNotReady = false;
            _nextFadeInFrames = 0;
            _nextTracks = 0;
            _activeTracks = 0;
            _mutedTracks = 0;
            _negatedTracks = 0;
            // play empty seq to ensure initialization
            MusicPlayer.Load(SeqId.WIN);
            MusicPlayer.WaitForLoad();
            MusicPlayer.Play();
            MusicPlayer.Stop();
        }

        private static ushort _pendingTracks = 0;
        private static MusicId _currentMusicId = MusicId.None;

        public static void PlayMusic(MusicId musicId, ushort? tracks = null, bool toggleOnTracks = false, bool toggleOffTracks = false)
        {
            int index = (int)musicId;
            if (index < 0 || index >= _musicInfo.Count)
            {
                return;
            }
            MusicTrack info = _musicInfo[index];
            if (info.SeqId == SeqId.None)
            {
                return;
            }
            if (!tracks.HasValue)
            {
                tracks = info.Tracks;
            }
            if (toggleOnTracks)
            {
                _pendingTracks |= tracks.Value;
            }
            else if (toggleOffTracks)
            {
                _pendingTracks &= (ushort)(~tracks.Value);
            }
            else
            {
                _pendingTracks = tracks.Value;
            }
            _currentMusicId = musicId;
            _paused = false;
            PlaySeq(info.SeqId, _pendingTracks, queue: true, notReady: true, info.FadeOutFrames, info.FadeInFrames);
        }

        public static void TryPlayRoomMusic(int roomId, int track)
        {
            if ((GameState.EscapeTimer == -1 || GameState.EscapeState != EscapeState.Escape) && _musicEncounterSuspension == 0)
            {
                PlayRoomMusic(roomId, track);
            }
        }

        public static void PlayRoomMusic(int roomId, int track)
        {
            track = Math.Clamp(track, 0, 2);
            for (int i = 0; i < _roomMusic.Count; i++)
            {
                RoomMusic room = _roomMusic[i];
                if (room.RoomId == roomId)
                {
                    PlayMusic((MusicId)room.TrackIds[track]);
                    return;
                }
            }
        }

        public static void Pause()
        {
            if (!_paused)
            {
                Stop();
                _paused = true;
            }
        }

        public static void PlayPausedMusic()
        {
            if (!_paused)
            {
                return;
            }
            int index = (int)_currentMusicId;
            if (index < 0 || index >= _musicInfo.Count)
            {
                return;
            }
            MusicTrack info = _musicInfo[index];
            if (info.SeqId == SeqId.None)
            {
                return;
            }
            _paused = false;
            PlaySeq(info.SeqId, _pendingTracks, queue: true, notReady: true);
        }

        public static void UpdateMusic()
        {
            if (!_isReady && !MusicPlayer.Loading)
            {
                _isReady = true;
                if (_playing)
                {
                    MusicPlayer.Play();
                    // skhere : set tempo
                    // the game applies the likely frontend pause flag to the player, as well as lid mute/unmute
                    // skhere: update negated tracks
                }
            }
            // the game checks the likely frontend pause flag before proceeding
            if (!_isReady || MusicPlayer.State != PlaybackState.Stopped)
            {
                // if not ready, there's no player to update, but we can still update our own fields
                // skhere: update tempo and tracks
            }
            else if (_musicQueued)
            {
                // if ready and stopped, we can proceed to queued music
                PlaySeq(_nextMusicSeq, _nextTracks, queue: false, _nextTrackNotReady, fadeOutFrames: 0, _nextFadeInFrames);
            }
        }

        private static int _musicEncounterSuspension = 0;
        public static int MusicEncounterSuspension => _musicEncounterSuspension;
        public static MusicId MusicToResume { get; set; } = MusicId.None;

        public static void UpdateMusicIdIfPaused(MusicId musicId)
        {
            if (IsPaused)
            {
                _currentMusicId = musicId;
            }
        }

        public static void UpdateEscapeMusic()
        {
            if (_currentMusicSeq != SeqId.OREGANO)
            {
                return;
            }
            if (GameState.EscapeTimer >= 5400 / 30f) // 3 min+
            {
                // sktodo: update tempo
            }
            else if (GameState.EscapeTimer >= 5100 / 30f) // 3 min - 2 min 50 sec
            {
                // sktodo: update tempo
            }
            else if (GameState.EscapeTimer >= 3600 / 30f) // 2 min 50 sec - 2 min
            {
                // sktodo: update tempo
            }
            else if (GameState.EscapeTimer >= 3400 / 30f) // 2 min - 1 min 53 sec
            {
                // sktodo: update tempo
            }
            else if (GameState.EscapeTimer >= 1800 / 30f) // 1 min 53 sec - 1 min
            {
                if (GameState.EscapeTimer == 1800)
                {
                    SwitchEscapeMusicIfNeeded();
                }
                // sktodo: update tempo
            }
            else if (GameState.EscapeTimer >= 1700 / 30f) // 1 min - 57 sec
            {
                SwitchEscapeMusicIfNeeded();
                // sktodo: update tempo
            }
            else // 57 sec - 0 sec
            {
                SwitchEscapeMusicIfNeeded();
                // sktodo: update tempo
            }
        }

        private static void SwitchEscapeMusicIfNeeded()
        {
            // switch ASAP at or after 1 min remaining
            // the game does this on the exact frame the timer hits 1800 (1 min), but we may miss that mark
            // escape timer (actual escape) is never set to 1 minute or lower, so no issue of a behavior difference w/ at vs. at-or-after
            if (_currentMusicId != MusicId.SEQ_OREGANO_M56)
            {
                PlayMusic(MusicId.SEQ_OREGANO_M56);
            }
        }

        private static readonly ImmutableArray<int> _hunterMusicTracks = [16, 4, 1, 0, 2, 5, 3, 16];

        public static void PlayEncounterMusic(Hunter hunter)
        {
            if (_musicEncounterSuspension == 0)
            {
                MusicToResume = _currentMusicId;
                // only played if no non-Guardian hunters are in the random encounter, otherwise some suspension bits are already set
                if (hunter == Hunter.Guardian)
                {
                    PlayMusic(MusicId.SEQ_GUARDIAN_M18);
                }
            }
            int bitIndex;
            if (hunter == Hunter.Guardian)
            {
                int i;
                for (i = 7; i <= 9; i++)
                {
                    if ((_musicEncounterSuspension & (1 << i)) == 0)
                    {
                        break;
                    }
                }
                bitIndex = i; // always 7/8/9, can't reach 10+ in practice since enemy hunter/Guardian max is 3
            }
            else
            {
                bitIndex = (int)hunter;
                if (_currentMusicId != MusicId.SEQ_GUMBO_M3)
                {
                    PlayMusic(MusicId.SEQ_GUMBO_M3);
                    // track 4 is on by default in the mask, but it's only for Kanden, so it's disabled here
                    PlayMusic(MusicId.SEQ_GUMBO_M3, tracks: 1 << 4, toggleOffTracks: true);
                }
                // enable the track specific to this hunter
                PlayMusic(MusicId.SEQ_GUMBO_M3, tracks: (ushort)(1 << _hunterMusicTracks[bitIndex]), toggleOnTracks: true);
                if (hunter == Hunter.Spire)
                {
                    // disable an addition track that Spire doesn't use
                    PlayMusic(MusicId.SEQ_GUMBO_M3, tracks: 1 << 9, toggleOffTracks: true);
                }
            }
            _musicEncounterSuspension |= (1 << bitIndex);
        }

        public static void UpdateEncounterMusic(int clearId)
        {
            if (_musicEncounterSuspension == 0)
            {
                return;
            }
            if (clearId < 0)
            {
                _musicEncounterSuspension = 0;
                if (clearId != -2)
                {
                    PlayMusic(MusicToResume);
                }
            }
            else
            {
                int bitIndex = clearId;
                Hunter hunter = (Hunter)clearId;
                if (hunter == Hunter.Guardian)
                {
                    int i;
                    for (i = 9; i >= 7; i--)
                    {
                        if ((_musicEncounterSuspension & (1 << i)) != 0)
                        {
                            break;
                        }
                    }
                    bitIndex = i; // always 9/8/7, can't reach 6- in practice since enemy hunter/Guardian max is 3
                }
                if ((_musicEncounterSuspension & (1 << bitIndex)) != 0)
                {
                    _musicEncounterSuspension &= ~(1 << bitIndex);
                    if (_musicEncounterSuspension == 0)
                    {
                        if (MusicToResume == MusicId.None)
                        {
                            // the game doesn't do this, but we don't want eternal encounter music after loading directly into a room
                            Stop(fadeOutFrames: 30);
                        }
                        else
                        {
                            PlayMusic(MusicToResume);
                        }
                    }
                    else if (hunter != Hunter.Guardian)
                    {
                        // when clearing a Guardian bit, no update is needed
                        if ((_musicEncounterSuspension & 0x7F) != 0)
                        {
                            // hunter defeated, still other hunters remaining
                            PlayMusic(MusicId.SEQ_GUMBO_M3, tracks: (ushort)(1 << _hunterMusicTracks[bitIndex]), toggleOffTracks: true);
                            if (hunter == Hunter.Spire)
                            {
                                PlayMusic(MusicId.SEQ_GUMBO_M3, tracks: 1 << 9, toggleOnTracks: true);
                            }
                        }
                        else
                        {
                            // hunter defeated, only Guardians remaining
                            PlayMusic(MusicId.SEQ_GUARDIAN_M18);
                        }
                    }
                }
            }
        }

        public static void PlaySeq(SeqId seqId, bool notReady = true)
        {
            PlaySeq(seqId, UInt16.MaxValue, notReady: notReady);
        }

        public static void PlaySeq(SeqId seqId, ushort tracks, bool queue = false, bool notReady = false,
            ushort fadeOutFrames = 0, ushort fadeInFrames = 0)
        {
            // the game may update the seq ID first using download play values
            if (!queue)
            {
                _nextTrackNotReady = false;
                Stop();
                _isReady = !notReady;
                _activeTracks = tracks;
                // todo?: stop music if needed (finished playing)
                // the game checks whether the music is loaded and/or available before proceeding
                _currentMusicSeq = seqId;
                _mutedTracks = 0;
                _negatedTracks = (ushort)(~_activeTracks);
                // skhere: init track faders
                _playing = true;
                MusicPlayer.Load(seqId, tracks);
                if (_isReady)
                {
                    MusicPlayer.WaitForLoad(); // sktodo: need to ensure this works okay waiting-wise (cam seq direct seq ID play only)
                    MusicPlayer.Play();
                    _negatedTracks = 0;
                }
                // skhere: set tempo
                _musicQueued = false;
                _nextMusicSeq = seqId;
            }
            else
            {
                if (!_musicQueued)
                {
                    if (_nextMusicSeq == seqId)
                    {
                        if (_isReady)
                        {
                            // skhere: set up track faders instead of setting immediately
                            MusicPlayer.Tracks = tracks;
                        }
                        else
                        {
                            _activeTracks = tracks;
                        }
                        return;
                    }
                    // the game sets the fade out frames in an unused music state field
                    // the game checks a pause flag we don't have before calling stop
                    Stop(fadeOutFrames);
                    _musicQueued = true;
                }
                _nextTrackNotReady = notReady;
                _nextMusicSeq = seqId;
                _nextTracks = tracks;
                _nextFadeInFrames = fadeInFrames;
            }
        }

        private static bool _playing = false;
        private static bool _paused = false;
        public static bool IsPaused => _paused;
        private static bool _musicQueued = false;
        private static SeqId _currentMusicSeq = SeqId.None;
        private static SeqId _nextMusicSeq = SeqId.None;
        private static bool _isReady = true;
        private static bool _nextTrackNotReady = false;
        private static ushort _nextFadeInFrames = 0;
        private static ushort _nextTracks = 0;
        private static ushort _activeTracks = 0;
        private static ushort _mutedTracks = 0;
        private static ushort _negatedTracks = 0;

        // sktodo: implement fade out with additional volume factor
        public static void Stop(int fadeOutFrames = 0)
        {
            _playing = false;
            _musicQueued = false;
            _nextMusicSeq = SeqId.None;
            _isReady = true;
            MusicPlayer.Stop();
        }
    }

    public static class MusicPlayer
    {
        // sktodo: need counters like the game's for track and tempo changes over time (depending on how/when the tracks and tempo are changed, anyway)
        // sktodo: need the ability to update track volume internally from 0-127, not just on/off
        private static MiniAudioEngine _audioEngine;
        private static AudioPlaybackDevice _playbackDevice;
        private static RawDataProvider? _provider = null;
        private static NCSFPlayerStream? _stream = null;
        private static SoundPlayer? _player = null;
        private static readonly AudioFormat _format;

        private const int _sampleRate = 32728;

        static MusicPlayer()
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

        public static bool Loading { get; private set; }

        public static void Load(SeqId seqId, ushort tracks = UInt16.MaxValue, float volume = 1)
        {
            // todo?: non-looping seqs (jingles, credits) will apparently stay in playing state indefinitely when playForever is on
            // --> should get overwritten with another seq before long
            Loading = true;
            Stop();
            if (seqId == SeqId.None)
            {
                Loading = false;
                return;
            }
            // sktodo: add CTS
            Task.Run(() =>
            {
                try
                {
                    Remove();
                    string path = Paths.Combine(Paths.FileSystem, "_seq", Metadata.SequenceFiles[(int)seqId]);
                    // todo: need to look at allocations (including recreating these objects, but especially the byte and float lists internal to NCSF)
                    _stream = new NCSFPlayerStream(path, (uint)_sampleRate, Interpolation.None, skipSilenceOnStartSec: 5,
                        defaultLengthInMS: 115000, defaultFadeInMS: 5000, NCSF123.VolumeType.ReplayGainAlbum, PeakType.ReplayGainTrack,
                        playForever: true, volume, channelMutes: 0, (ushort)(tracks ^ UInt16.MaxValue), ignoreVolume: false);
                    _provider = new RawDataProvider(_stream, SampleFormat.F32, _sampleRate);
                    _player = new SoundPlayer(_audioEngine, _format, _provider);
                    _playbackDevice.MasterMixer.AddComponent(_player);
                    _playbackDevice.Start();
                }
                finally
                {
                    _break++;
                    Loading = false;
                }
            });
        }

        public static void WaitForLoad(int sleepMs = 100)
        {
            while (MusicPlayer.Loading)
            {
                Thread.Sleep(sleepMs);
            }
        }

        public static void Play()
        {
            _player?.Play();
        }

        public static void Pause()
        {
            _player?.Pause();
        }

        public static void Resume()
        {
            _player?.Play();
        }

        public static PlaybackState State
        {
            get
            {
                if (_player != null)
                {
                    return _player.State;
                }
                return PlaybackState.Stopped;
            }
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

        private static Lock _lock = new Lock();

        public static void Stop()
        {
            if (_player != null)
            {
                _player.Stop();
            }
        }

        private static int _break = 0;

        public static void Remove(bool shutdown = false)
        {
            if (_player != null)
            {
                Debug.Assert(_provider != null);
                Debug.Assert(_stream != null);
                _player.Stop();
                if (shutdown)
                {
                    _playbackDevice.Stop();
                }
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
