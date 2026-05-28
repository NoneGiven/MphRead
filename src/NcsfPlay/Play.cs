using NCSF123;
using NCSFPlayer;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace NcsfPlay
{
    public static class Player
    {
        // sktodo:
        // - perform conversion during setup to an "underscore" folder
        // - create enumeration of tracks that map to the output files
        // - accept that index when playing a new file here
        // sktodo: return controller object
        // sktodo: volume input + update, track mask input + update, tempo update
        public static void Play()
        {
            string path = @"C:\Users\auser\Home\MPH\Sound\NCSF\NDStoNCSF\bin\Debug\net9.0\output\002B - SEQ_CHUTNEY.minincsf";
            PeakType clipProtect = PeakType.ReplayGainTrack;
            int defaultFadeInMS = 5000;
            Interpolation interpolation = Interpolation.None;
            int defaultLengthInMS = 115000;
            ushort channelMutes = 0;
            ushort trackMutes = 0;
            VolumeType replayGain = VolumeType.ReplayGainAlbum;
            int sampleRate = 32728; // sktodo: is this right?
            uint skipSilence = 5; // sktodo: is this important?
            float volume = 1;
            var format = new AudioFormat()
            {
                SampleRate = sampleRate,
                Channels = 2,
                Format = SampleFormat.F32
            };
            using var audioEngine = new MiniAudioEngine();
            using var playbackDevice = audioEngine.InitializePlaybackDevice(deviceInfo: null, format);
            using var stream = new NCSFPlayerStream(path, (uint)sampleRate, interpolation, skipSilence, defaultLengthInMS,
                defaultFadeInMS, replayGain, clipProtect, playForever: true, volume, channelMutes, trackMutes, ignoreVolume: false);
            using var provider = new RawDataProvider(stream, SampleFormat.F32, sampleRate);
            var player = new SoundPlayer(audioEngine, format, provider);
            playbackDevice.MasterMixer.AddComponent(player);
            playbackDevice.Start();
            player.Play();
            while (player.State != PlaybackState.Stopped)
            {
                Thread.Sleep(10);
            }
        }
    }
}
