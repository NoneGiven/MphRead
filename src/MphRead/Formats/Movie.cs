using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MphRead.Formats;
using MphRead.Formats.Sound;
using MphRead.Sound;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MphRead
{
    public enum AfterMovie
    {
        StartGame,
        LoadRoom,
        EndGame
    }

    public partial class Scene
    {
        private const int _frameWidth = 256;
        private const int _frameHeight = 192;
        private int _topMovieBinding = -1;
        private int _botMovieBinding = -1;
        private int _movieFrameCount = 0;
        private int _movieFrameIndex = -1;
        private int _lastRenderedMovieFrameIndex = 0;
        private int _movieFrameTotal = 0;
        public bool MoviePlaying => _movieFrameIndex != -1;
        private bool _skipMovie = false;
        // hack to avoid things (like spawn SFX) happening on frame 0 of processing before we can start playing the landing movie
        private bool _playingLandingMovie = false;

        private bool _dualScreenMovie = true;
        private readonly byte[] _topImageBuffer = new byte[_frameWidth * _frameHeight * 3];
        private readonly byte[] _botImageBuffer = new byte[_frameWidth * _frameHeight * 3];
        private CancellationTokenSource _decoderCts = null!;

        public void StartMovies(Movie movieId, Movie afterMovieId, FadeType fadeToMovieType, float fadeToMovieLength,
            FadeType fadeFromMovieType, float fadeFromMovieLength, AfterMovie afterMovieAction = AfterMovie.LoadRoom)
        {
            StartMovie(movieId, fadeToMovieType, fadeToMovieLength, fadeFromMovieType, fadeFromMovieLength,
                afterMovieId: afterMovieId, afterMovieAction: afterMovieAction);
        }

        public void StartMovie(Movie movieId, FadeType fadeToMovieType, float fadeToMovieLength, FadeType fadeFromMovieType,
            float fadeFromMovieLength, Vector3? afterPosition = null, Vector3? afterFacing = null, Movie? afterMovieId = null,
            AfterMovie afterMovieAction = AfterMovie.LoadRoom)
        {
            // on the frame when the whiteout completes (middle frame if the type is out+in, last frame if out only), the scene needs to pause
            // it can start playing the movie then or delay a couple frames to match the game, but the movie plays during the end of the fade
            // at the end of the movie (or when skipped), the last frame of the movie needs to freeze on-screen while after-fade occurs
            // then at the complete (middle) of the after-fade, the movie should stop being dislayed and the scene should unpause
            // --> what exactly happens depends on the after-movie action
            // - resume as-is
            // - resume with updated position/facing/node ref
            // - do room transition and then resume
            // - end game/credits/menu/etc.
            _movieSettings.MovieId = movieId;
            _movieSettings.AfterMovieId = afterMovieId;
            _movieSettings.AfterFadeType = fadeFromMovieType;
            _movieSettings.AfterFadeLength = fadeFromMovieLength;
            _movieSettings.AfterPosition = afterPosition;
            _movieSettings.AfterFacing = afterFacing;
            _movieSettings.AfterMovieAction = afterMovieAction;
            SetFade(fadeToMovieType, fadeToMovieLength, overwrite: true, AfterFade.PlayMovie);
        }

        private void PlayMovie(Movie movieId)
        {
            // - start decoding and wait for first frame
            // - proceed with scene processing to draw first frame
            // - after enough time has elapsed, request the next frame's color buffer from the decoder
            // - decoder can decode up to 4 frames, then needs to wait until its oldest frame is requested by the renderer,
            //   then it can drop that frame and decode another one
            Music.Stop();
            GameState.PauseDialog();
            Sfx.SfxMute = true;
            Sfx.LongSfxMute++;
            Sfx.TimedSfxMute++;
            Sfx.ForceFieldSfxMute++;
            VxDecoder.Instance1.Reset();
            VxDecoder.Instance2.Reset();
            _decoderCts = new CancellationTokenSource();
            if (_audioHandle != -1)
            {
                AL.SourceStop(_audioHandle);
                AL.DeleteSource(_audioHandle);
                AL.DeleteBuffers(_audioBufferIds);
            }
            _audioHandle = AL.GenSource();
            AL.GenBuffers(_audioBufferIds);
            AL.Source(_audioHandle, ALSourcef.Gain, Music.UserVolume * 0.5f);
            Array.Fill(_audioBuffersAvailable, true);
            _audioBufferIndex = 0;
            Metadata.MovieInfo info = Metadata.MovieFiles[(int)movieId];
            _dualScreenMovie = info.BottomScreenPath != null;
            string topPath = Paths.Combine(Paths.FileSystem, info.TopScreenPath);
            Task.Run(async () => await VxDecoder.Instance1.Decode(topPath, token: _decoderCts.Token), _decoderCts.Token);
            if (_dualScreenMovie)
            {
                string botPath = Paths.Combine(Paths.FileSystem, info.BottomScreenPath!);
                Task.Run(async () => await VxDecoder.Instance2.Decode(botPath, token: _decoderCts.Token), _decoderCts.Token);
            }
            else
            {
                Array.Fill<byte>(_botImageBuffer, 0);
            }
            // start with a few frames to make sure audio doesn't run out
            // (note that even if we don't sleep, we'll hit the max of 4 anyway)
            while (VxDecoder.Instance1.FramesQueued < 4)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(10));
            }
            VxDecoder.Instance1.GetImage(frameIndex: 0, _topImageBuffer);
            if (_dualScreenMovie)
            {
                while (VxDecoder.Instance2.FramesQueued < 4)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                }
                VxDecoder.Instance2.GetImage(frameIndex: 0, _botImageBuffer);
            }
            if (_topMovieBinding == -1)
            {
                _topMovieBinding = ++_textureCount;
                _botMovieBinding = ++_textureCount;
            }
            GL.BindTexture(TextureTarget.Texture2D, _topMovieBinding);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, _frameWidth, _frameHeight, 0,
                PixelFormat.Rgb, PixelType.UnsignedByte, _topImageBuffer);
            GL.BindTexture(TextureTarget.Texture2D, _botMovieBinding);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, _frameWidth, _frameHeight, 0,
                PixelFormat.Rgb, PixelType.UnsignedByte, _botImageBuffer);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            Debug.Assert(!_dualScreenMovie || VxDecoder.Instance1.FrameCount == VxDecoder.Instance2.FrameCount);
            _movieFrameTotal = VxDecoder.Instance1.FrameCount;
            _lastRenderedMovieFrameIndex = 0;
            _movieFrameIndex = 0;
            _movieFrameCount = 0;
            _skipMovie = false;
            Task.Run(async () =>
            {
                // while loop outside the method so we can use stackalloc without limitation (possibly different sizes each iteration)
                while (!_decoderCts.Token.IsCancellationRequested && _movieFrameCount != Int32.MaxValue)
                {
                    await UpdateMovieAudio(_decoderCts.Token);
                }
            }, _decoderCts.Token);
            Task.Run(async () => await UpdateMovieImage(_decoderCts.Token), _decoderCts.Token);
        }

        private int _audioHandle = -1;
        public int MovieAudioHandle => _audioHandle;
        private const int _audioBufferCount = 16;
        private int _audioBufferIndex = 0;
        private readonly int[] _audioBufferIds = new int[_audioBufferCount];
        private readonly bool[] _audioBuffersAvailable = new bool[_audioBufferCount];

        private async Task UpdateMovieAudio(CancellationToken token)
        {
            Span<short> stereoBuffer = stackalloc short[128 * 2];
            bool initial = _audioBufferIndex == 0;
            int framesAvailable = VxDecoder.Instance1.AudioFrameTotal - _audioBufferIndex;
            if (framesAvailable > 0)
            {
                int buffersAvailable = 0;
                if (_audioBufferIndex == 0)
                {
                    buffersAvailable = _audioBufferCount;
                }
                else
                {
                    int buffersProcessed = AL.GetSource(_audioHandle, ALGetSourcei.BuffersProcessed);
                    if (buffersProcessed > 0)
                    {
                        Span<int> processedIds = stackalloc int[Math.Min(framesAvailable, buffersProcessed)];
                        AL.SourceUnqueueBuffers(_audioHandle, processedIds);
                        for (int i = 0; i < _audioBufferCount; i++)
                        {
                            _audioBuffersAvailable[i] = processedIds.Contains(_audioBufferIds[i]);
                        }
                    }
                    for (int i = 0; i < _audioBufferCount; i++)
                    {
                        if (_audioBuffersAvailable[i])
                        {
                            buffersAvailable++;
                        }
                    }
                }
                if (buffersAvailable > 0)
                {
                    Span<int> queueBuffers = stackalloc int[Math.Min(framesAvailable, buffersAvailable)];
                    int bufferIndex = 0;
                    for (int i = 0; i < queueBuffers.Length; i++)
                    {
                        for (; bufferIndex < _audioBufferCount; bufferIndex++)
                        {
                            if (_audioBuffersAvailable[bufferIndex])
                            {
                                queueBuffers[i] = _audioBufferIds[bufferIndex];
                                _audioBuffersAvailable[bufferIndex] = false;
                                break;
                            }
                        }
                        Debug.Assert(bufferIndex < _audioBufferCount);
                        if (_dualScreenMovie)
                        {
                            ReadOnlySpan<short> buffer1 = VxDecoder.Instance1.GetAudioBuffer(_audioBufferIndex);
                            ReadOnlySpan<short> buffer2 = VxDecoder.Instance2.GetAudioBuffer(_audioBufferIndex++);
                            for (int j = 0; j < 128; j++)
                            {
                                stereoBuffer[j * 2] = buffer1[j];
                                stereoBuffer[j * 2 + 1] = buffer2[j];
                            }
                            AL.BufferData<short>(queueBuffers[i], ALFormat.Stereo16, stereoBuffer, VxDecoder.Instance1.AudioSampleRate);
                        }
                        else
                        {
                            ReadOnlySpan<short> buffer = VxDecoder.Instance1.GetAudioBuffer(_audioBufferIndex++);
                            AL.BufferData<short>(queueBuffers[i], ALFormat.Mono16, buffer, VxDecoder.Instance1.AudioSampleRate);
                        }
                    }
                    AL.SourceQueueBuffers(_audioHandle, queueBuffers);
                    var state = (ALSourceState)AL.GetSource(_audioHandle, ALGetSourcei.SourceState);
                    if (state != ALSourceState.Playing)
                    {
                        // this should only occur once unless things are lagging behind
                        AL.SourcePlay(_audioHandle);
                    }
                }
            }
            try
            {
                await Task.Delay(1);
            }
            catch (TaskCanceledException) { }
        }

        private void StopMovie()
        {
            if (_audioHandle != -1)
            {
                AL.SourceStop(_audioHandle);
                AL.DeleteSource(_audioHandle);
                AL.DeleteBuffers(_audioBufferIds);
                _audioHandle = -1;
            }
            if (_movieSettings.AfterMovieId.HasValue)
            {
                Sfx.LongSfxMute--;
                Sfx.TimedSfxMute--;
                Sfx.ForceFieldSfxMute--;
                _decoderCts?.Cancel();
                _movieSettings.MovieId = _movieSettings.AfterMovieId.Value;
                _movieSettings.AfterMovieId = null;
                PlayMovie(_movieSettings.MovieId);
                _fadeType = FadeType.None;
            }
            else if (_movieSettings.AfterMovieAction == AfterMovie.EndGame)
            {
                QuitGame(enteringShip: true);
            }
            else
            {
                if (_movieSettings.AfterMovieAction != AfterMovie.StartGame)
                {
                    Debug.Assert(_room != null);
                    _room.LoadRoom(resume: GameState.TransitionRoomId == -1);
                }
                else
                {
                    // hold on white to give the landing cam seq a chance to fade in, overwriting this, if any.
                    // otherwise this fade will fade out to gameplay (no cam seq for an existing save).
                    SetFade(FadeType.FadeInWhite, 5 / 30f, overwrite: true, delay: 5 / 30f);
                    Music.PlayPausedMusic();
                    _playingLandingMovie = false;
                }
                Sfx.SfxMute = false;
                Sfx.LongSfxMute--;
                Sfx.TimedSfxMute--;
                Sfx.ForceFieldSfxMute--;
                GameState.UnpauseDialog();
                _decoderCts?.Cancel();
                _movieFrameIndex = -1;
            }
        }

        public void SkipMovie()
        {
            if (_movieFrameIndex != -1 && _movieFrameCount != Int32.MaxValue)
            {
                _skipMovie = true;
            }
        }

        private void UpdateMovie()
        {
            if (_skipMovie)
            {
                if (_movieFrameCount != Int32.MaxValue)
                {
                    _movieFrameCount = Int32.MaxValue;
                    SetFade(_movieSettings.AfterFadeType, _movieSettings.AfterFadeLength, overwrite: true, AfterFade.StopMovie);
                }
                return;
            }
        }

        private async Task UpdateMovieImage(CancellationToken token)
        {
            TimeSpan frameTime = TimeSpan.FromSeconds(1 / 15.0);
            TimeSpan nextFrameElapsed = frameTime;
            // tolerance is used mainly so the audio doesn't run out if we end up sleeping a bit past the next 15 fps mark
            // there are either 11 or 12 audio frames per video frame, which balance each other out, but will drift unless the video waits are balanced
            TimeSpan tolerance = TimeSpan.FromMilliseconds(15);
            var sw = new Stopwatch();
            sw.Start();
            while (!token.IsCancellationRequested && _movieFrameCount != Int32.MaxValue)
            {
                TimeSpan elapsed = sw.Elapsed;
                if (elapsed >= nextFrameElapsed && nextFrameElapsed - elapsed < tolerance)
                {
                    _movieFrameCount++;
                    nextFrameElapsed += frameTime;
                }
                int frameIndex = _movieFrameCount;
                if (_movieFrameCount != _movieFrameIndex)
                {
                    if (frameIndex == _movieFrameTotal)
                    {
                        _movieFrameCount = Int32.MaxValue;
                        SetFade(_movieSettings.AfterFadeType, _movieSettings.AfterFadeLength, overwrite: true, AfterFade.StopMovie);
                        return;
                    }
                    if (frameIndex < _movieFrameTotal)
                    {
                        if (VxDecoder.Instance1.GetImage(frameIndex, _topImageBuffer)
                            && !_dualScreenMovie || VxDecoder.Instance2.GetImage(frameIndex, _botImageBuffer))
                        {
                            _movieFrameIndex = frameIndex;
                        }
                    }
                }
                try
                {
                    await Task.Delay(1);
                }
                catch (TaskCanceledException) { }
            }
        }

        private void DrawMovieFrame()
        {
            GL.Uniform1(_shaderLocations.LayerAlpha, 1);
            bool newFrame = false;

            void DrawScreen(int movieBinding, byte[] imageBuffer, float y)
            {
                GL.BindTexture(TextureTarget.Texture2D, movieBinding);
                int minParameter = (int)TextureMinFilter.Nearest;
                int magParameter = (int)TextureMagFilter.Nearest;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                GL.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D,
                    TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                if (_movieFrameIndex != _lastRenderedMovieFrameIndex || newFrame && _dualScreenMovie)
                {
                    newFrame = true;
                    _lastRenderedMovieFrameIndex = _movieFrameIndex;
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _frameWidth, _frameHeight, PixelFormat.Rgb, PixelType.UnsignedByte, imageBuffer);
                }
                GL.Begin(PrimitiveType.TriangleStrip);
                // top right
                GL.TexCoord3(1f, 0f, 0f);
                GL.Vertex3(0.5f, y, 0f);
                // top left
                GL.TexCoord3(0f, 0f, 0f);
                GL.Vertex3(-0.5f, y, 0f);
                // bottom right
                GL.TexCoord3(1f, 1f, 0f);
                GL.Vertex3(0.5f, y - 1, 0f);
                // bottom left
                GL.TexCoord3(0f, 1f, 0f);
                GL.Vertex3(-0.5f, y - 1, 0f);
                GL.End();
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }

            GL.Uniform4(_shaderLocations.FadeColor, 0, 0, 0, 1);
            GL.Begin(PrimitiveType.TriangleStrip);
            // top right
            GL.TexCoord3(1f, 1f, 0f);
            GL.Vertex3(1f, 1f, 0f);
            // top left
            GL.TexCoord3(0f, 1f, 0f);
            GL.Vertex3(-1f, 1f, 0f);
            // bottom right
            GL.TexCoord3(1f, 0f, 0f);
            GL.Vertex3(1f, -1f, 0f);
            // bottom left
            GL.TexCoord3(0f, 0f, 0f);
            GL.Vertex3(-1f, -1f, 0f);
            GL.End();
            DrawScreen(_topMovieBinding, _topImageBuffer, y: 1);
            DrawScreen(_botMovieBinding, _botImageBuffer, y: 0);
        }
    }
}

namespace MphRead.Formats
{
    public readonly struct SeekTableEntry
    {
        public readonly int FrameId;
        public readonly int FrameOffset;

        public SeekTableEntry(int frameId, int frameOffset)
        {
            FrameId = frameId;
            FrameOffset = frameOffset;
        }
    }

    public class AudioExtradata
    {
        public int[,,] LpcCodebooks { get; } = new int[3, 64, 8]; // 1536
        public int[] ScaleModifiers { get; } = new int[8];
        public int[] LpcBase { get; } = new int[8];
        public int ScaleInitial { get; set; }
    }

    public class VxDecoder
    {
        public static VxDecoder Instance1 { get; } = new VxDecoder();
        public static VxDecoder Instance2 { get; } = new VxDecoder();

        public char[] Magic { get; } = new char[4];
        public int FrameCount { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public decimal FrameRate { get; set; }
        public int Quantizer { get; set; }
        public int AudioSampleRate { get; set; }
        public int AudioStreamCount { get; set; }
        public int MaxDataSize { get; set; }
        public int ExtradataOffset { get; set; }
        public int SeekTableOffset { get; set; }
        public int SeekTableCount { get; set; }
        public AudioExtradata Extradata { get; } = new AudioExtradata();
        public SeekTableEntry[] SeekTable { get; } = new SeekTableEntry[1];
        public int[] QuantizerTable { get; } = new int[3];
        private readonly VideoFrame?[] _prevVideoFrames = new VideoFrame?[3];
        private int _framesQueued = 0;
        public int FramesQueued => _framesQueued;
        private int _audioFrameTotal = 0;
        private List<VxFrame> _vxFrames = null!;

        private static readonly ImmutableArray<ImmutableArray<int>> _quantizer4x4Table =
        [
            [ 0x0A, 0x0D, 0x10 ],
            [ 0x0B, 0x0E, 0x12 ],
            [ 0x0D, 0x10, 0x14 ],
            [ 0x0E, 0x12, 0x17 ],
            [ 0x10, 0x14, 0x19 ],
            [ 0x12, 0x17, 0x1D ]
        ];

        public void Reset()
        {
            _vxFrames?.Clear();
            UseStaticBuffers = true;
            Array.Clear(_sampleBuffer);
        }

        public async Task ExportAll()
        {
            Reset();
            UseStaticBuffers = false;
            int i = 0;
            var files = Directory.EnumerateFiles(Paths.Combine(Paths.FileSystem, "movies")).ToList();
            foreach (string path in files)
            {
                if (Path.GetExtension(path) == ".vx")
                {
                    Console.WriteLine($"Exporting {++i} of {files.Count}: {Path.GetFileName(path)}");
                    await Decode(path, writeFiles: true);
                }
            }
            Console.WriteLine("Done.");
        }

        public async Task Export(string filePath)
        {
            Reset();
            string path = Paths.Combine(Paths.FileSystem, "movies", filePath);
            if (!File.Exists(path))
            {
                path = Paths.Combine(Paths.FileSystem, filePath);
                if (!File.Exists(path))
                {
                    path = filePath;
                }
            }
            Console.WriteLine("Exporting...");
            UseStaticBuffers = false;
            await Decode(path, writeFiles: true);
            Console.WriteLine("Done.");
        }

        public async Task Decode(string filePath, bool writeFiles = false, CancellationToken token = default)
        {
            using FileStream fs = File.OpenRead(filePath);
            await Decode(fs, Path.GetFileName(filePath), writeFiles, token);
        }

        public async Task Decode(byte[] data, string filename, bool writeFiles = false, CancellationToken token = default)
        {
            using var ms = new MemoryStream(data);
            await Decode(ms, filename, writeFiles, token);
        }

        public async Task Decode(Stream stream, string filename, bool writeFiles = false, CancellationToken token = default)
        {
            // current max error code: 022
            string folder = Paths.Combine(Paths.Export, Path.GetFileNameWithoutExtension(filename));
            if (writeFiles)
            {
                Directory.CreateDirectory(folder);
            }
            using var reader = new BinaryReader(stream);
            _nextPlaneBufferIndex = 0;
            _nextSampleBufferIndex = 0;
            _framesQueued = 0;

            Magic[0] = reader.ReadChar(); // V (86)
            Magic[1] = reader.ReadChar(); // X (88)
            Magic[2] = reader.ReadChar(); // D (68)
            Magic[3] = reader.ReadChar(); // S (83)
            FrameCount = reader.ReadInt32();
            FrameWidth = reader.ReadInt32();
            FrameHeight = reader.ReadInt32();
            FrameRate = reader.ReadInt32() / 65536m;
            Quantizer = reader.ReadInt32();
            AudioSampleRate = reader.ReadInt32();
            AudioStreamCount = reader.ReadInt32();
            if (AudioStreamCount > 1)
            {
                throw new ProgramException($"VX decoding error 022: {AudioStreamCount}");
            }
            MaxDataSize = reader.ReadInt32();
            MaxDataSize -= 2; // subtract 2 for audioFrameCount
            Debug.Assert(MaxDataSize % 2 == 0);
            ExtradataOffset = reader.ReadInt32();
            SeekTableOffset = reader.ReadInt32();
            SeekTableCount = reader.ReadInt32();

            if (FrameWidth % 16 != 0 || FrameHeight % 16 != 0)
            {
                throw new ProgramException($"VX decoding error 001: {FrameWidth} x {FrameHeight}");
            }

            long prevPosition = reader.BaseStream.Position;
            reader.BaseStream.Position = ExtradataOffset;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 64; j++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        Extradata.LpcCodebooks[i, j, k] = reader.ReadInt16();
                    }
                }
            }

            for (int i = 0; i < 8; i++)
            {
                Extradata.ScaleModifiers[i] = reader.ReadUInt16();
            }

            for (int i = 0; i < 8; i++)
            {
                Extradata.LpcBase[i] = reader.ReadInt32();
            }

            Extradata.ScaleInitial = reader.ReadInt32();

            reader.BaseStream.Position = SeekTableOffset;
            for (int i = 0; i < SeekTableCount; i++)
            {
                // SeekTable[i]: MPH only has one, and we don't use this
                SeekTable[0] = new SeekTableEntry(frameId: reader.ReadInt32(), frameOffset: reader.ReadInt32());
            }

            if (Quantizer < 12 || Quantizer > 161)
            {
                throw new ProgramException($"VX decoding error 002: {Quantizer}");
            }
            (int qy, int qx) = Math.DivRem(Quantizer, 6);
            ImmutableArray<int> table = _quantizer4x4Table[qx];
            for (int i = 0; i < table.Length; i++)
            {
                QuantizerTable[i] = table[i] << qy;
            }

            reader.BaseStream.Position = prevPosition;

            // static init
            _ = VLC.Temp;

            byte[] buffer = GetDataBuffer();
            Array.Fill<byte>(buffer, 0);
            byte[,] coeffBufferY;
            byte[,] coeffBufferUV;
            Vector2ir[,] vectors;
            if (UseStaticBuffers)
            {
                coeffBufferY = _coeffBufferY;
                coeffBufferUV = _coeffBufferUV;
                vectors = _vectors;
            }
            else
            {
                coeffBufferY = new byte[FrameHeight / 4 + 1, FrameWidth / 4 + 1];
                coeffBufferUV = new byte[FrameHeight / 8 + 1, FrameWidth / 8 + 1];
                vectors = new Vector2ir[FrameHeight / 16 + 1, FrameWidth / 16 + 2];
            }
            // todo: avoid list allocation? buffers are recycled anyway, but when decoding in realtime, we only need 4 frames
            _vxFrames = new List<VxFrame>(FrameCount);
            _prevVideoFrames[0] = _prevVideoFrames[1] = _prevVideoFrames[2] = null;
            Array.Clear(_prevSampleBuffer);
            Array.Clear(_prevPulseBuffer);
            Array.Clear(_lpcFilterBuffer);
            Array.Clear(_influenceBuffer);
            AudioFrame? prevAudioFrame = null;
            _audioFrameTotal = 0;
            using Stream file = writeFiles && AudioStreamCount >= 1 ? File.OpenWrite(Paths.Combine(folder, "audio.wav")) : Stream.Null;
            using BinaryWriter writer = new BinaryWriter(file);
            for (int i = 0; i < 11; i++)
            {
                // dummy space for 44-byte WAV header will be overwritten later (contains sizes)
                writer.Write(0);
            }
            for (int i = 0; i < FrameCount; i++)
            {
                while (!writeFiles && _framesQueued >= 4 && !token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(1);
                    }
                    catch (TaskCanceledException) { }
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
                int dataSize = reader.ReadUInt16();
                dataSize -= 2; // subtract 2 for audioFrameCount
                Debug.Assert(dataSize % 2 == 0); // for byte swapping
                Debug.Assert(dataSize <= MaxDataSize);
                int audioFrameCount = reader.ReadUInt16();
                byte[,]? planeBufferY = null;
                byte[,]? planeBufferU = null;
                byte[,]? planeBufferV = null;
                if (UseStaticBuffers)
                {
                    (planeBufferY, planeBufferU, planeBufferV) = GetPlaneBuffers();
                }
                ClearArray(coeffBufferY);
                ClearArray(coeffBufferUV);
                ClearArray(vectors);
                VxBuffers buffers = new VxBuffers(_prevVideoFrames, QuantizerTable, planeBufferY, planeBufferU, planeBufferV, coeffBufferY,
                    coeffBufferUV, vectors, _prevSampleBuffer, _prevPulseBuffer, _lpcFilterBuffer, _influenceBuffer, _sampleBuffer);
                var vxFrame = new VxFrame(FrameWidth, FrameHeight, audioFrameCount, Extradata, prevAudioFrame, buffers, _nextSampleBufferIndex);
                vxFrame.Decode(reader, buffer, dataSize);
                if (audioFrameCount > 0)
                {
                    prevAudioFrame = vxFrame.AudioFrames[^1];
                    _nextSampleBufferIndex += audioFrameCount;
                    _nextSampleBufferIndex %= SampleBufferCount;
                }
                _audioFrameTotal += audioFrameCount;
                _vxFrames.Add(vxFrame);
                _framesQueued++;
                _prevVideoFrames[2] = _prevVideoFrames[1];
                _prevVideoFrames[1] = _prevVideoFrames[0];
                _prevVideoFrames[0] = vxFrame.VideoFrame;
                if (writeFiles && UseStaticBuffers)
                {
                    WriteFile(vxFrame, folder, i);
                }
                if (writeFiles && audioFrameCount > 0)
                {
                    for (int j = 0; j < audioFrameCount; j++)
                    {
                        AudioFrame audioFrame = vxFrame.AudioFrames[j];
                        for (int k = 0; k < 128; k++)
                        {
                            ushort sample = (ushort)audioFrame.SampleBuffer[k];
                            writer.Write((byte)(sample & 0xFF));
                            writer.Write((byte)((sample >> 8) & 0xFF));
                        }
                    }
                }
            }
            if (writeFiles && _audioFrameTotal > 0)
            {
                writer.Seek(0, SeekOrigin.Begin);
                SoundRead.WriteWavHeader(writer, (uint)_audioFrameTotal * 128, (ushort)AudioSampleRate, WaveFormat.PCM16);
            }

            void WriteFile(VxFrame vxFrame, string folder, int f)
            {
                VideoFrame videoFrame = vxFrame.VideoFrame;
                using var image = new Image<Rgb24>(FrameWidth, FrameHeight);
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < FrameHeight; y++)
                    {
                        Span<Rgb24> row = accessor.GetRowSpan(y);
                        for (int x = 0; x < FrameWidth; x++)
                        {
                            int cy = videoFrame.PlaneBufferY[y, x];
                            int cu = videoFrame.PlaneBufferU[y / 2, x / 2];
                            int cv = videoFrame.PlaneBufferV[y / 2, x / 2];
                            row[x] = YuvToRgb(cy, cu, cv);
                        }
                    }
                });
                image.SaveAsPng(Paths.Combine(folder, $"{f.ToString().PadLeft(4, '0')}.png"));
            }

            if (writeFiles && !UseStaticBuffers)
            {
                int f = 0;
                foreach (VxFrame vxFrame in _vxFrames)
                {
                    WriteFile(vxFrame, folder, f++);
                }
            }
        }

        // with a realtime approach, we only need:
        // plane buffers for frame n being decoded
        // coeff buffers for frame being decoded
        // vector buffer for frame being decoded
        // plane buffers for frame n - 1
        // plane buffers for frame n - 2
        // plane buffers for frame n - 3
        // color buffer for frame being displayed
        public bool GetImage(int frameIndex, byte[] texture)
        {
            if (_vxFrames == null || frameIndex >= _vxFrames.Count)
            {
                return false;
            }
            VxFrame vxFrame = _vxFrames[frameIndex];
            for (int y = 0; y < FrameHeight; y++)
            {
                for (int x = 0; x < FrameWidth; x++)
                {
                    int cy = vxFrame.VideoFrame.PlaneBufferY[y, x];
                    int cu = vxFrame.VideoFrame.PlaneBufferU[y / 2, x / 2];
                    int cv = vxFrame.VideoFrame.PlaneBufferV[y / 2, x / 2];
                    Rgb24 rgb = YuvToRgb(cy, cu, cv);
                    texture[y * 256 * 3 + x * 3] = rgb.R;
                    texture[y * 256 * 3 + x * 3 + 1] = rgb.G;
                    texture[y * 256 * 3 + x * 3 + 2] = rgb.B;
                }
            }
            _framesQueued--;
            return true;
        }

        public int AudioFrameTotal => _audioFrameTotal;

        public ReadOnlySpan<short> GetAudioBuffer(int index)
        {
            index %= SampleBufferCount;
            return new Span<short>(_sampleBuffer, 128 * index, 128);
        }

        private readonly short[] _prevSampleBuffer = new short[8];
        private readonly int[] _prevPulseBuffer = new int[256];
        private readonly int[] _lpcFilterBuffer = new int[8];
        private readonly int[] _influenceBuffer = new int[8];
        private int _nextSampleBufferIndex = 0;
        public static int SampleBufferCount => 5 * 12;

        // because we don't know how fast decoding will be, we have to assume that the maximum of 5 video frames x 12 audio frames will be
        // sitting around waiting while the previous frame is displaying and previous audio buffers are not yet played. so we'll have to
        // have 48 buffers for audio frame samples. if video and audio decoding were decoupled, then instead of the 4 video frame decoding limit
        // applying to both, audio frames could have their own decoding limit based on fast they get played, reducing the number of buffers
        // needed to just a few. this 12 KB static allocation (x2 decoders) could be reduced to 1-2 KB.
        // note: 128-byte spans of this buffer are used by each audio frame. because the audio frame needs to be hold those samples until export
        // or playback, the frame must store a reference to this entire array, but it's only accessed through the span per _nextSampleBufferIndex.
        private readonly short[] _sampleBuffer = new short[SampleBufferCount * 128];

        // because we know the dimensions and maximum data size of MPH's videos, we can decode successive videos with reusable, permanent static allocations.
        // in order to keep this code flexible to allow decoding other videos with unknown sizes, this behavior can be disabled. when enabled, the code will:
        // - use one buffer for the file data of the frame currently being decoded. this buffer could be removed if the bit reader operated on a stream.
        // - use three plane buffers for YUV of the frame being decoded and the three previous frames. the plane buffer collection is used circularly.
        // when disabled, the data buffer will be allocated once for each video file, and the plane buffers will be newly allocated within each video frame.
        public static bool UseStaticBuffers { get; set; } = true;

        private const int _mphMaxDataSize = 7602;
        private readonly byte[] _dataBuffer = new byte[_mphMaxDataSize];

        private byte[] GetDataBuffer()
        {
            return UseStaticBuffers || MaxDataSize <= _mphMaxDataSize ? _dataBuffer : new byte[MaxDataSize];
        }

        private const int _mphFrameW = 256;
        private const int _mphFrameH = 192;
        private int _nextPlaneBufferIndex = 0;

        // if spans were supported for 2D arrays, these could also use the single allocation, multiple span approach used by _sampleBuffer
        private readonly ImmutableArray<byte[,]> _planeBuffers =
        [
            //                  Y                                 U                                         V
            /* frame n   */ new byte[_mphFrameH, _mphFrameW], new byte[_mphFrameH / 2, _mphFrameW / 2], new byte[_mphFrameH / 2, _mphFrameW / 2],
            /* frame n-1 */ new byte[_mphFrameH, _mphFrameW], new byte[_mphFrameH / 2, _mphFrameW / 2], new byte[_mphFrameH / 2, _mphFrameW / 2],
            /* frame n-2 */ new byte[_mphFrameH, _mphFrameW], new byte[_mphFrameH / 2, _mphFrameW / 2], new byte[_mphFrameH / 2, _mphFrameW / 2],
            /* frame n-3 */ new byte[_mphFrameH, _mphFrameW], new byte[_mphFrameH / 2, _mphFrameW / 2], new byte[_mphFrameH / 2, _mphFrameW / 2]
        ];

        private (byte[,], byte[,], byte[,]) GetPlaneBuffers()
        {
            byte[,] bufferY = _planeBuffers[_nextPlaneBufferIndex++];
            byte[,] bufferU = _planeBuffers[_nextPlaneBufferIndex++];
            byte[,] bufferV = _planeBuffers[_nextPlaneBufferIndex++];
            _nextPlaneBufferIndex %= _planeBuffers.Length;
            ClearArray(bufferV);
            ClearArray(bufferU);
            ClearArray(bufferV);
            return (bufferY, bufferU, bufferV);
        }

        // unlike the plane buffers, these are reusable for each new frame. the static buffers setting only
        // determines whether they're also reused across different movie files decoded by the same decoder instance.
        private readonly byte[,] _coeffBufferY = new byte[_mphFrameH / 4 + 1, _mphFrameW / 4 + 1];
        private readonly byte[,] _coeffBufferUV = new byte[_mphFrameH / 8 + 1, _mphFrameW / 8 + 1];
        private readonly Vector2ir[,] _vectors = new Vector2ir[_mphFrameH / 16 + 1, _mphFrameW / 16 + 2];

        private static Rgb24 YuvToRgb(int y, int u, int v)
        {
            u -= 128;
            v -= 128;
            int r = y + 2 * v;
            int g = y - u / 2 - v;
            int b = y + 2 * u;
            return new Rgb24((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        }

        private static void ClearArray<T>(T[,] array) where T : struct
        {
            MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(array)), array.Length).Fill(default);
        }
    }

    public readonly struct VxBuffers
    {
        public readonly VideoFrame?[] PrevVideoFrames;
        public readonly int[] QuantizerTable;
        public readonly byte[,]? PlaneBufferY;
        public readonly byte[,]? PlaneBufferU;
        public readonly byte[,]? PlaneBufferV;
        public readonly byte[,] CoeffBufferY;
        public readonly byte[,] CoeffBufferUV;
        public readonly Vector2ir[,] Vectors;
        public readonly short[] PrevSampleBuffer;
        public readonly int[] PrevPulseBuffer;
        public readonly int[] LpcFilterBuffer;
        public readonly int[] InfluenceBuffer;
        public readonly short[] SampleBuffer;

        public VxBuffers(VideoFrame?[] prevVideoFrames, int[] quantizerTable, byte[,]? planeBufferY, byte[,]? planeBufferU, byte[,]? planeBufferV,
            byte[,] coeffBufferY, byte[,] coeffBufferUV, Vector2ir[,] vectors, short[] prevSampleBuffer, int[] prevPulseBuffer, int[] lpcFilterBuffer,
            int[] influenceBuffer, short[] sampleBuffer)
        {
            PrevVideoFrames = prevVideoFrames;
            QuantizerTable = quantizerTable;
            PlaneBufferY = planeBufferY;
            PlaneBufferU = planeBufferU;
            PlaneBufferV = planeBufferV;
            CoeffBufferY = coeffBufferY;
            CoeffBufferUV = coeffBufferUV;
            Vectors = vectors;
            PrevSampleBuffer = prevSampleBuffer;
            PrevPulseBuffer = prevPulseBuffer;
            LpcFilterBuffer = lpcFilterBuffer;
            InfluenceBuffer = influenceBuffer;
            SampleBuffer = sampleBuffer;
        }
    }

    public class VxFrame
    {
        public VideoFrame VideoFrame { get; }
        public int AudioFrameCount { get; }
        public AudioFrame[] AudioFrames { get; }

        public VxFrame(int frameWidth, int frameHeight, int audioFrameCount, AudioExtradata extradata,
            AudioFrame? prevAudioFrame, VxBuffers buffers, int sampleBufferIndex)
        {
            VideoFrame = new VideoFrame(frameWidth, frameHeight, buffers);
            AudioFrameCount = audioFrameCount;
            AudioFrames = new AudioFrame[audioFrameCount];
            for (int i = 0; i < audioFrameCount; i++)
            {
                var audioFrame = new AudioFrame(extradata, prevAudioFrame, buffers, sampleBufferIndex++);
                AudioFrames[i] = audioFrame;
                prevAudioFrame = audioFrame;
                sampleBufferIndex %= VxDecoder.SampleBufferCount;
            }
        }

        public void Decode(BinaryReader reader, byte[] buffer, int length)
        {
            for (int i = 0; i < length; i += 2)
            {
                buffer[i + 1] = reader.ReadByte();
                buffer[i] = reader.ReadByte();
            }
            var bitReader = new BitStreamReader(buffer, length); // todo: this doesn't need to be allocated repeatedly
            VideoFrame.Decode(bitReader);
            for (int i = 0; i < AudioFrameCount; i++)
            {
                AudioFrames[i].Decode(bitReader);
            }
        }
    }

    public readonly struct Vector2ir
    {
        public readonly int X;
        public readonly int Y;

        public Vector2ir()
        {
        }

        public Vector2ir(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public class BitStreamReader
    {
        private readonly byte[] _buffer;
        private readonly int _length;
        // using an int indexer means we can read 2,147,483,647 / 8 = 268,435,455 bytes (256 MB minus 1 b) at once
        private int _bitPosition = 0;

        public BitStreamReader(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
        }

        public int ReadBit()
        {
            // instead of flipping the bits in the buffer, we can do the 7 - pos shift here instead of shifting by pos
            // debatable which way makes more sense -- read the bits left-to-right (human) or right-to-left (little endian LSB -> MSB)
            // we're going with left-to-right
            (int bytePosition, int bitPosition) = Math.DivRem(_bitPosition++, 8);
            return (_buffer[bytePosition] >> (7 - bitPosition)) & 1;
        }

        public int ConsumeUntilNotZero()
        {
            // this will run off the end of the buffer, but is not intended to be used there
            int count = 0;
            while (ReadBit() == 0)
            {
                count++;
            }
            return count;
        }

        public int ReadUnsignedExpGolomb()
        {
            int zeroCount = ConsumeUntilNotZero();
            Debug.Assert(zeroCount <= 30);
            int value = 1 << zeroCount; // this 1 is the last thing ConsumeUntilNotZero() consumed
            for (int i = 0; i < zeroCount; i++)
            {
                value |= ReadBit() << (zeroCount - i - 1);
            }
            return value - 1;
        }

        public int ReadSignedExpGolomb()
        {
            int value = ReadUnsignedExpGolomb() + 1;
            // (value / 2) * ((value & 1) == 0 ? 1 : -1)
            return (value >> 1) * ((value & 1) * -2 + 1);
        }

        public int ReadInt(int bitCount)
        {
            Debug.Assert(bitCount >= 0 && bitCount <= 32);
            int value = 0;
            for (int i = 0; i < bitCount; i++)
            {
                value |= ReadBit() << (bitCount - i - 1);
            }
            return value;
        }

        public int ReadVLC2(VLCData vlc)
        {
            int bitCount = 1;
            int hashCode = HashCode.Combine(0, ReadBit());
            int index = vlc.FindBitPattern(hashCode);
            while (index == -1)
            {
                Debug.Assert(bitCount < vlc.MaxBitCount);
                bitCount++;
                hashCode = HashCode.Combine(hashCode, ReadBit());
                index = vlc.FindBitPattern(hashCode);
            }
            return index;
        }

        public void EnsureWordAlignment()
        {
            while (_bitPosition % 16 != 0)
            {
                _bitPosition++;
            }
        }
    }

    public readonly struct Block
    {
        public readonly int X;
        public readonly int Y;
        public readonly int W;
        public readonly int H;

        public Block(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public Block HalfLeft()
        {
            return new Block(X, Y, W / 2, H);
        }

        public Block HalfRight()
        {
            return new Block(X + W / 2, Y, W / 2, H);
        }

        public Block HalfUp()
        {
            return new Block(X, Y, W, H / 2);
        }

        public Block HalfDown()
        {
            return new Block(X, Y + H / 2, W, H / 2);
        }
    }

    public class VideoFrame
    {
        public int FrameWidth { get; }
        public int FrameHeight { get; }
        private byte[,] _planeBufferY = null!;
        private byte[,] _planeBufferU = null!;
        private byte[,] _planeBufferV = null!;
        public byte[,] PlaneBufferY => _planeBufferY;
        public byte[,] PlaneBufferU => _planeBufferU;
        public byte[,] PlaneBufferV => _planeBufferV;
        private byte[,] _coeffBufferY;
        private byte[,] _coeffBufferUV;
        private Vector2ir[,] _vectors;
        private readonly VideoFrame?[] _prevVideoFrames;
        private readonly int[] _quantizerTable;

        private BitStreamReader _reader = null!;

        public VideoFrame(int frameWidth, int frameHeight, VxBuffers buffers)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            _prevVideoFrames = buffers.PrevVideoFrames;
            _quantizerTable = buffers.QuantizerTable;
            _planeBufferY = buffers.PlaneBufferY!;
            _planeBufferU = buffers.PlaneBufferU!;
            _planeBufferV = buffers.PlaneBufferV!;
            _coeffBufferY = buffers.CoeffBufferY;
            _coeffBufferUV = buffers.CoeffBufferUV;
            _vectors = buffers.Vectors;
        }

        public void Decode(BitStreamReader reader)
        {
            _reader = reader;

            if (_planeBufferY == null)
            {
                _planeBufferY = new byte[FrameHeight, FrameWidth];
                _planeBufferU = new byte[FrameHeight / 2, FrameWidth / 2];
                _planeBufferV = new byte[FrameHeight / 2, FrameWidth / 2];
            }

            for (int y = 0; y < FrameHeight; y += 16)
            {
                for (int x = 0; x < FrameWidth; x += 16)
                {
                    Vector2ir predictionVector = new Vector2ir(
                        GetMiddleValue(
                            _vectors[(y / 16) + 1, (x / 16) + 0].X,
                            _vectors[(y / 16) + 0, (x / 16) + 1].X,
                            _vectors[(y / 16) + 0, (x / 16) + 2].X
                        ),
                        GetMiddleValue(
                            _vectors[(y / 16) + 1, (x / 16) + 0].Y,
                            _vectors[(y / 16) + 0, (x / 16) + 1].Y,
                            _vectors[(y / 16) + 0, (x / 16) + 2].Y
                        )
                    );
                    DecodeBlock(new Block(x, y, w: 16, h: 16), predictionVector);
                }
            }

            // align before audio bit stream reading begins
            _reader.EnsureWordAlignment();
            _reader = null!;
        }

        private static int GetMiddleValue(int a, int b, int c)
        {
            Span<int> array = stackalloc int[3] { a, b, c };
            array.Sort();
            return array[1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte PlaneBufferGetter(byte[,] planeBuffer, int step, int x, int y)
        {
            return planeBuffer[y / step, x / step];
        }

        private void DecodeBlock(Block block, Vector2ir predictionVector)
        {
            int mode = _reader.ReadUnsignedExpGolomb();
            if (mode == 0) // v-split, no residue
            {
                if (block.W == 2)
                {
                    throw new ProgramException("VX decoding error 003");
                }
                DecodeBlock(block.HalfLeft(), predictionVector);
                DecodeBlock(block.HalfRight(), predictionVector);
                if (block.W == 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 1) // no delta, no residue, ref 0
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[0]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 2) // h-split, no residue
            {
                if (block.H == 2)
                {
                    throw new ProgramException("VX decoding error 004");
                }
                DecodeBlock(block.HalfUp(), predictionVector);
                DecodeBlock(block.HalfDown(), predictionVector);
                if (block.W >= 8 && block.H == 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 3) // unpredicted delta ref0 + dc offset, no residue
            {
                PredictInterDC(block);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 4) // delta, no residue, ref 0
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[0]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 5) // delta, no residue, ref 1
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[1]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 6) // delta, no residue, ref 2
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[2]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 7) // plane, no residue
            {
                PredictMBPlane(block);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 8) // v-split, residue
            {
                if (block.W == 2)
                {
                    throw new ProgramException("VX decoding error 005");
                }
                DecodeBlock(block.HalfLeft(), predictionVector);
                DecodeBlock(block.HalfRight(), predictionVector);
                DecodeResidueBlocks(block);
            }
            else if (mode == 9) // no delta, no residue, ref 1
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[1]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 10) // unpredicted delta ref0 + dc offset, residue
            {
                PredictInterDC(block);
                DecodeResidueBlocks(block);
            }
            else if (mode == 11) // predict notile, no residue
            {
                PredictNoTile(block);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 12) // no delta, residue, ref 0
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[0]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 13) // h-split, residue
            {
                if (block.H == 2)
                {
                    throw new ProgramException("VX decoding error 006");
                }
                DecodeBlock(block.HalfUp(), predictionVector);
                DecodeBlock(block.HalfDown(), predictionVector);
                DecodeResidueBlocks(block);
            }
            else if (mode == 14) // no delta, no residue, ref 2
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[2]);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 15) // predict4, no residue
            {
                Predict4(block);
                if (block.W >= 8 && block.H >= 8)
                {
                    ClearTotalCoeff(block);
                }
            }
            else if (mode == 16) // delta, residue, ref 0
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[0]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 17) // delta, residue, ref 1
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[1]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 18) // delta, residue, ref 2
            {
                PredictInter(block, predictionVector, true, _prevVideoFrames[2]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 19) // predict4, residue
            {
                Predict4(block);
                DecodeResidueBlocks(block);
            }
            else if (mode == 20) // no delta, residue, ref 1
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[1]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 21) // no delta, residue, ref 2
            {
                PredictInter(block, predictionVector, false, _prevVideoFrames[2]);
                DecodeResidueBlocks(block);
            }
            else if (mode == 22) // predict notile, residue
            {
                PredictNoTile(block);
                DecodeResidueBlocks(block);
            }
            else if (mode == 23) // plane, residue
            {
                PredictMBPlane(block);
                DecodeResidueBlocks(block);
            }
            else
            {
                throw new ProgramException($"VX decoding error 007: {mode}");
            }
        }

        private void PredictInter(Block block, Vector2ir predictionVector, bool hasDelta, VideoFrame? prevVideoFrame)
        {
            Debug.Assert(prevVideoFrame != null);

            if (hasDelta)
            {
                predictionVector = new Vector2ir(predictionVector.X + _reader.ReadSignedExpGolomb(), predictionVector.Y + _reader.ReadSignedExpGolomb());
            }

            _vectors[(block.Y / 16) + 1, (block.X / 16) + 1] = predictionVector;

            for (int y = block.Y; y < block.Y + block.H; y++)
            {
                for (int x = block.X; x < block.X + block.W; x++)
                {
                    _planeBufferY[y, x] = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferY, 1, x + predictionVector.X, y + predictionVector.Y);
                }
            }
            for (int y = block.Y; y < block.Y + block.H; y += 2)
            {
                for (int x = block.X; x < block.X + block.W; x += 2)
                {
                    _planeBufferU[y / 2, x / 2] = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferU, 2, x + predictionVector.X, y + predictionVector.Y);
                }
            }
            for (int y = block.Y; y < block.Y + block.H; y += 2)
            {
                for (int x = block.X; x < block.X + block.W; x += 2)
                {
                    _planeBufferV[y / 2, x / 2] = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferV, 2, x + predictionVector.X, y + predictionVector.Y);
                }
            }
        }

        private void PredictInterDC(Block block)
        {
            var vec = new Vector2ir(_reader.ReadSignedExpGolomb(), _reader.ReadSignedExpGolomb());

            if (block.X + vec.X < 0 || block.X + vec.X + block.W > FrameWidth ||
                block.Y + vec.Y < 0 || block.Y + vec.Y + block.H > FrameHeight)
            {
                throw new ProgramException("VX decoding error 008");
            }

            int dcY = _reader.ReadSignedExpGolomb();
            if (dcY < -(1 << 16) || dcY >= (1 << 16))
            {
                throw new ProgramException("VX decoding error 009");
            }
            dcY *= 2;

            int dcU = _reader.ReadSignedExpGolomb();
            if (dcU < -(1 << 16) || dcU >= (1 << 16))
            {
                throw new ProgramException("VX decoding error 010");
            }
            dcU *= 2;

            int dcV = _reader.ReadSignedExpGolomb();
            if (dcV < -(1 << 16) || dcV >= (1 << 16))
            {
                throw new ProgramException("VX decoding error 011");
            }
            dcV *= 2;

            VideoFrame? prevVideoFrame = _prevVideoFrames[0];
            Debug.Assert(prevVideoFrame != null);
            for (int y = block.Y; y < block.Y + block.H; y++)
            {
                for (int x = block.X; x < block.X + block.W; x++)
                {
                    int pixel = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferY, 1, x + vec.X, y + vec.Y) + dcY;
                    _planeBufferY[y, x] = (byte)Math.Clamp(pixel, 0, 255);
                }
            }
            for (int y = block.Y; y < block.Y + block.H; y += 2)
            {
                for (int x = block.X; x < block.X + block.W; x += 2)
                {
                    int pixel = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferU, 2, x + vec.X, y + vec.Y) + dcU;
                    _planeBufferU[y / 2, x / 2] = (byte)Math.Clamp(pixel, 0, 255);
                }
            }
            for (int y = block.Y; y < block.Y + block.H; y += 2)
            {
                for (int x = block.X; x < block.X + block.W; x += 2)
                {
                    int pixel = prevVideoFrame.PlaneBufferGetter(prevVideoFrame._planeBufferV, 2, x + vec.X, y + vec.Y) + dcV;
                    _planeBufferV[y / 2, x / 2] = (byte)Math.Clamp(pixel, 0, 255);
                }
            }
        }

        private void PredictMBPlane(Block block)
        {
            int value = _reader.ReadSignedExpGolomb();
            if (value < -(1 << 16) || value >= (1 << 16))
            {
                throw new ProgramException($"VX decoding error 012: {value}");
            }
            PredictPlane(block, _planeBufferY, 1, value * 2);

            value = _reader.ReadSignedExpGolomb();
            if (value < -(1 << 16) || value >= (1 << 16))
            {
                throw new ProgramException($"VX decoding error 013: {value}");
            }
            PredictPlane(block, _planeBufferU, 2, value * 2);

            value = _reader.ReadSignedExpGolomb();
            if (value < -(1 << 16) || value >= (1 << 16))
            {
                throw new ProgramException($"VX decoding error 014: {value}");
            }
            PredictPlane(block, _planeBufferV, 2, value * 2);
        }

        private static readonly ImmutableArray<int> _residueMaskTable =
        [
            0x00, 0x08, 0x04, 0x02, 0x01, 0x1F, 0x0F, 0x0A,
            0x05, 0x0C, 0x03, 0x10, 0x0E, 0x0D, 0x0B, 0x07,
            0x09, 0x06, 0x1E, 0x1B, 0x1A, 0x1D, 0x17, 0x15,
            0x18, 0x12, 0x11, 0x1C, 0x14, 0x13, 0x16, 0x19
        ];

        private static readonly ImmutableArray<int> _tokenIndexTable =
        [
            0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3
        ];

        private static readonly ImmutableArray<int> _suffixLimits =
        [
            0, 3, 6, 12, 24, 48, 0x8000
        ];

        private static readonly ImmutableArray<int> _zigzagScanTable =
        [
            0 * 4 + 0,  1 * 4 + 0,  0 * 4 + 1,  0 * 4 + 2,
            1 * 4 + 1,  2 * 4 + 0,  3 * 4 + 0,  2 * 4 + 1,
            1 * 4 + 2,  0 * 4 + 3,  1 * 4 + 3,  2 * 4 + 2,
            3 * 4 + 1,  3 * 4 + 2,  2 * 4 + 3,  3 * 4 + 3
        ];

        private void DecodeResidueBlocks(Block block)
        {
            for (int y = 0; y < block.H; y += 8)
            {
                for (int x = 0; x < block.W; x += 8)
                {
                    int index = _reader.ReadUnsignedExpGolomb();
                    if (index > 31)
                    {
                        throw new ProgramException($"VX decoding error 015: {index}");
                    }
                    int residueMask = _residueMaskTable[index];

                    if ((residueMask & 1) != 0)
                    {
                        int coeffLeft = GetCoeffBuffer(_coeffBufferY, 1, block.X + x - 1, block.Y + y);
                        int coeffTop = GetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y - 1);
                        int nc = (coeffLeft + coeffTop + 1) / 2;
                        int outTotalCoeff = DecodeResidueCAVLC(block.X + x, block.Y + y, nc, _planeBufferY, 1);
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y, (byte)outTotalCoeff);
                    }
                    else
                    {
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y, 0);
                    }

                    if ((residueMask & 2) != 0)
                    {
                        int coeffLeft = GetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4 - 1, block.Y + y);
                        int coeffTop = GetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y - 1);
                        int nc = (coeffLeft + coeffTop + 1) / 2;
                        int outTotalCoeff = DecodeResidueCAVLC(block.X + x + 4, block.Y + y, nc, _planeBufferY, 1);
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y, (byte)outTotalCoeff);
                    }
                    else
                    {
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y, 0);
                    }

                    if ((residueMask & 4) != 0)
                    {
                        int coeffLeft = GetCoeffBuffer(_coeffBufferY, 1, block.X + x - 1, block.Y + y + 4);
                        int coeffTop = GetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y + 4 - 1);
                        int nc = (coeffLeft + coeffTop + 1) / 2;
                        int outTotalCoeff = DecodeResidueCAVLC(block.X + x, block.Y + y + 4, nc, _planeBufferY, 1);
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y + 4, (byte)outTotalCoeff);
                    }
                    else
                    {
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y + 4, 0);
                    }

                    if ((residueMask & 8) != 0)
                    {
                        int coeffLeft = GetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4 - 1, block.Y + y + 4);
                        int coeffTop = GetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y + 4 - 1);
                        int nc = (coeffLeft + coeffTop + 1) / 2;
                        int outTotalCoeff = DecodeResidueCAVLC(block.X + x + 4, block.Y + y + 4, nc, _planeBufferY, 1);
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y + 4, (byte)outTotalCoeff);
                    }
                    else
                    {
                        SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y + 4, 0);
                    }

                    if ((residueMask & 16) != 0)
                    {
                        int coeffLeft = GetCoeffBuffer(_coeffBufferUV, 2, block.X + x - 1, block.Y + y);
                        int coeffTop = GetCoeffBuffer(_coeffBufferUV, 2, block.X + x, block.Y + y - 1);
                        int nc = (coeffLeft + coeffTop + 1) / 2;
                        int totalCoeffU = DecodeResidueCAVLC(block.X + x, block.Y + y, nc, _planeBufferU, 2);
                        int totalCoeffV = DecodeResidueCAVLC(block.X + x, block.Y + y, nc, _planeBufferV, 2);
                        int outTotalCoeff = (totalCoeffU + totalCoeffV + 1) / 2;
                        SetCoeffBuffer(_coeffBufferUV, 2, block.X + x, block.Y + y, (byte)outTotalCoeff);
                    }
                    else
                    {
                        SetCoeffBuffer(_coeffBufferUV, 2, block.X + x, block.Y + y, 0);
                    }
                }
            }
        }

        private int DecodeResidueCAVLC(int x, int y, int nc, byte[,] planeBuffer, int step)
        {
            int coeffToken = _reader.ReadVLC2(VLC.CoeffTokenVlc[_tokenIndexTable[nc]]);
            if (coeffToken == -1)
            {
                throw new ProgramException("VX decoding error 016");
            }

            int trailingOnes = coeffToken & 3;
            int totalCoeff = coeffToken >> 2;
            int outTotalCoeff = totalCoeff;
            if (totalCoeff == 0)
            {
                return outTotalCoeff;
            }

            // not sure if this could need the variable/non-16 length, but for MPH, it's always 16
            Span<int> level = stackalloc int[16]; // no init needed since every element will be written to
            int levelPos = 0;
            int zeroesRemaining;
            if (totalCoeff == 16)
            {
                zeroesRemaining = 0;
            }
            else
            {
                zeroesRemaining = _reader.ReadVLC2(VLC.TotalZeroesVlc[totalCoeff]);
                for (int i = 0; i < 16 - (totalCoeff + zeroesRemaining); i++)
                {
                    level[levelPos++] = 0;
                }
            }

            int suffixLength = 0;
            while (true)
            {
                if (trailingOnes > 0)
                {
                    trailingOnes -= 1;
                    level[levelPos++] = _reader.ReadBit() == 0 ? 1 : -1;
                }
                else
                {
                    int levelPrefix = 0;
                    while (_reader.ReadBit() == 0)
                    {
                        levelPrefix += 1;
                    }

                    int levelSuffix;
                    if (levelPrefix == 15)
                    {
                        levelSuffix = _reader.ReadInt(11);
                    }
                    else
                    {
                        levelSuffix = _reader.ReadInt(suffixLength);
                    }

                    int levelCode = (levelPrefix << suffixLength) + levelSuffix + 1;

                    if (levelCode > _suffixLimits[suffixLength + 1])
                    {
                        suffixLength += 1;
                    }

                    if (_reader.ReadBit() == 1)
                    {
                        levelCode = -levelCode;
                    }
                    level[levelPos++] = levelCode;
                }

                totalCoeff -= 1;
                if (totalCoeff == 0)
                {
                    break;
                }

                if (zeroesRemaining == 0)
                {
                    continue;
                }

                int runBefore;
                if (zeroesRemaining < 7)
                {
                    runBefore = _reader.ReadVLC2(VLC.RunVlc[zeroesRemaining]);
                }
                else
                {
                    runBefore = _reader.ReadVLC2(VLC.Run7Vlc);
                }

                zeroesRemaining -= runBefore;
                for (int i = 0; i < runBefore; i++)
                {
                    level[levelPos++] = 0;
                }
            }

            for (int i = 0; i < zeroesRemaining; i++)
            {
                level[levelPos++] = 0;
            }

            Debug.Assert(levelPos == 16);
            DecodeDct(x, y, planeBuffer, step, level);
            return outTotalCoeff;
        }

        private void DecodeDct(int x, int y, byte[,] planeBuffer, int step, ReadOnlySpan<int> level)
        {
            Span<int> dct = stackalloc int[_zigzagScanTable.Length]; // no init needed since every element will be written to

            for (int i = 0; i < _zigzagScanTable.Length; i++)
            {
                int z = _zigzagScanTable[i];
                dct[z] = level[15 - i] * _quantizerTable[(z & 1) + ((z >> 2) & 1)];
            }

            dct[0] += 1 << 5;

            for (int i = 0; i < 4; i++)
            {
                int z0 = dct[i + 4 * 0] + dct[i + 4 * 2];
                int z1 = dct[i + 4 * 0] - dct[i + 4 * 2];
                int z2 = (dct[i + 4 * 1] / 2) - dct[i + 4 * 3];
                int z3 = dct[i + 4 * 1] + (dct[i + 4 * 3] / 2);

                dct[i + 4 * 0] = z0 + z3;
                dct[i + 4 * 1] = z1 + z2;
                dct[i + 4 * 2] = z1 - z2;
                dct[i + 4 * 3] = z0 - z3;
            }

            for (int i = 0; i < 4; i++)
            {
                int z0 = dct[0 + 4 * i] + dct[2 + 4 * i];
                int z1 = dct[0 + 4 * i] - dct[2 + 4 * i];
                int z2 = (dct[1 + 4 * i] / 2) - dct[3 + 4 * i];
                int z3 = dct[1 + 4 * i] + (dct[3 + 4 * i] / 2);

                int bx = x + step * i;
                int by = y + step * 0;
                int pixel = PlaneBufferGetter(planeBuffer, step, bx, by) + ((z0 + z3) >> 6);
                planeBuffer[by / step, bx / step] = (byte)Math.Clamp(pixel, 0, 255);

                by = y + step * 1;
                pixel = PlaneBufferGetter(planeBuffer, step, bx, by) + ((z1 + z2) >> 6);
                planeBuffer[by / step, bx / step] = (byte)Math.Clamp(pixel, 0, 255);

                by = y + step * 2;
                pixel = PlaneBufferGetter(planeBuffer, step, bx, by) + ((z1 - z2) >> 6);
                planeBuffer[by / step, bx / step] = (byte)Math.Clamp(pixel, 0, 255);

                by = y + step * 3;
                pixel = PlaneBufferGetter(planeBuffer, step, bx, by) + ((z0 - z3) >> 6);
                planeBuffer[by / step, bx / step] = (byte)Math.Clamp(pixel, 0, 255);
            }
        }

        private void PredictNoTile(Block block)
        {
            int mode = _reader.ReadUnsignedExpGolomb();
            if (mode == 0)
            {
                PredictVertical(block, _planeBufferY, 1);
            }
            else if (mode == 1)
            {
                PredictHorizontal(block, _planeBufferY, 1);
            }
            else if (mode == 2)
            {
                PredictDC(block, _planeBufferY, 1);
            }
            else if (mode == 3)
            {
                PredictPlane(block, _planeBufferY, 1, 0);
            }
            else
            {
                throw new ProgramException($"VX decoding error 017: {mode}");
            }
            PredictNoTileUV(block);
        }

        private void PredictVertical(Block block, byte[,] planeBuffer, int step)
        {
            for (int y = block.Y; y < block.Y + block.H; y += step)
            {
                for (int x = block.X; x < block.X + block.W; x += step)
                {
                    planeBuffer[y / step, x / step] = PlaneBufferGetter(planeBuffer, step, x, block.Y - 1);
                }
            }
        }

        private void PredictHorizontal(Block block, byte[,] planeBuffer, int step)
        {
            for (int y = block.Y; y < block.Y + block.H; y += step)
            {
                for (int x = block.X; x < block.X + block.W; x += step)
                {
                    planeBuffer[y / step, x / step] = PlaneBufferGetter(planeBuffer, step, block.X - 1, y);
                }
            }
        }

        private void PredictDC(Block block, byte[,] planeBuffer, int step)
        {
            byte dc = 128;
            if (block.X != 0 && block.Y != 0)
            {
                int sumX = block.W / 2;
                for (int x = 0; x < block.W; x++)
                {
                    sumX += PlaneBufferGetter(planeBuffer, step, block.X + x, block.Y - 1);
                }
                int sumY = block.H / 2;
                for (int y = 0; y < block.H; y++)
                {
                    sumY += PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y + y);
                }
                dc = (byte)(((sumX / block.W) + (sumY / block.H) + 1) / 2);
            }
            else if (block.X == 0 && block.Y != 0)
            {
                int sumX = block.W / 2;
                for (int x = 0; x < block.W; x++)
                {
                    sumX += PlaneBufferGetter(planeBuffer, step, block.X + x, block.Y - 1);
                }
                dc = (byte)(sumX / block.W);
            }
            else if (block.X != 0 && block.Y == 0)
            {
                int sumY = block.H / 2;
                for (int y = 0; y < block.H; y++)
                {
                    sumY += PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y + y);
                }
                dc = (byte)(sumY / block.H);
            }
            for (int y = block.Y; y < block.Y + block.H; y += step)
            {
                for (int x = block.X; x < block.X + block.W; x += step)
                {
                    planeBuffer[y / step, x / step] = dc;
                }
            }
        }

        private void PredictPlane(Block block, byte[,] planeBuffer, int step, int value)
        {
            int bottomLeft = PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y + block.H - 1);
            int topRight = PlaneBufferGetter(planeBuffer, step, block.X + block.W - 1, block.Y - 1);
            int pixel = (bottomLeft + topRight + 1) / 2 + value;
            int x = block.X + block.W - 1;
            int y = block.Y + block.H - 1;
            planeBuffer[y / step, x / step] = (byte)pixel;
            PredictPlaneRecursive(block, planeBuffer, step);
        }

        private void PredictPlaneRecursive(Block block, byte[,] planeBuffer, int step)
        {
            if (block.W == step && block.H == step)
            {
                return;
            }
            if (block.W == step && block.H > step)
            {
                int top = PlaneBufferGetter(planeBuffer, step, block.X, block.Y - 1);
                int bottom = PlaneBufferGetter(planeBuffer, step, block.X, block.Y + block.H - 1);
                int pixel = (top + bottom) / 2;
                int x = block.X;
                int y = block.Y + (block.H / 2) - 1;
                planeBuffer[y / step, x / step] = (byte)pixel;
                PredictPlaneRecursive(block.HalfUp(), planeBuffer, step);
                PredictPlaneRecursive(block.HalfDown(), planeBuffer, step);
            }
            else if (block.W > step && block.H == step)
            {
                int left = PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y);
                int right = PlaneBufferGetter(planeBuffer, step, block.X + block.W - 1, block.Y);
                int pixel = (left + right) / 2;
                int x = block.X + (block.W / 2) - 1;
                int y = block.Y;
                planeBuffer[y / step, x / step] = (byte)pixel;
                PredictPlaneRecursive(block.HalfLeft(), planeBuffer, step);
                PredictPlaneRecursive(block.HalfRight(), planeBuffer, step);
            }
            else
            {
                int bottomLeft = PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y + block.H - 1);
                int topRight = PlaneBufferGetter(planeBuffer, step, block.X + block.W - 1, block.Y - 1);
                int bottomRight = PlaneBufferGetter(planeBuffer, step, block.X + block.W - 1, block.Y + block.H - 1);
                int bottomCenter = (bottomLeft + bottomRight) / 2;
                int centerRight = (topRight + bottomRight) / 2;
                int pixel;
                int x = block.X + (block.W / 2) - 1;
                int y = block.Y + block.H - 1;
                planeBuffer[y / step, x / step] = (byte)bottomCenter;
                x = block.X + block.W - 1;
                y = block.Y + (block.H / 2) - 1;
                planeBuffer[y / step, x / step] = (byte)centerRight;
                if ((block.W == 4 * step || block.W == 16 * step) != (block.H == 4 * step || block.H == 16 * step))
                {
                    int centerLeft = PlaneBufferGetter(planeBuffer, step, block.X - 1, block.Y + (block.H / 2) - 1);
                    pixel = (centerLeft + centerRight) / 2;
                }
                else
                {
                    int topCenter = PlaneBufferGetter(planeBuffer, step, block.X + (block.W / 2) - 1, block.Y - 1);
                    pixel = (topCenter + bottomCenter) / 2;
                }
                x = block.X + (block.W / 2) - 1;
                y = block.Y + (block.H / 2) - 1;
                planeBuffer[y / step, x / step] = (byte)pixel;
                PredictPlaneRecursive(block.HalfLeft().HalfUp(), planeBuffer, step);
                PredictPlaneRecursive(block.HalfRight().HalfUp(), planeBuffer, step);
                PredictPlaneRecursive(block.HalfLeft().HalfDown(), planeBuffer, step);
                PredictPlaneRecursive(block.HalfRight().HalfDown(), planeBuffer, step);
            }
        }

        private void PredictNoTileUV(Block block)
        {
            int mode = _reader.ReadUnsignedExpGolomb();
            if (mode == 0)
            {
                PredictDC(block, _planeBufferU, 2);
                PredictDC(block, _planeBufferV, 2);
            }
            else if (mode == 1)
            {
                PredictHorizontal(block, _planeBufferU, 2);
                PredictHorizontal(block, _planeBufferV, 2);
            }
            else if (mode == 2)
            {
                PredictVertical(block, _planeBufferU, 2);
                PredictVertical(block, _planeBufferV, 2);
            }
            else if (mode == 3)
            {
                PredictPlane(block, _planeBufferU, 2, 0);
                PredictPlane(block, _planeBufferV, 2, 0);
            }
            else
            {
                throw new ProgramException($"VX decoding error 018: {mode}");
            }
        }

        private void Predict4(Block block)
        {
            Span<int> cache = stackalloc int[5 * 5];
            cache.Fill(9);

            for (int y2 = 0; y2 < block.H / 4; y2++)
            {
                for (int x2 = 0; x2 < block.W / 4; x2++)
                {
                    int mode = Math.Min(cache[(1 + y2 - 1) * 5 + 1 + x2], cache[(1 + y2) * 5 + 1 + x2 - 1]);
                    if (mode == 9)
                    {
                        mode = 2;
                    }

                    if (_reader.ReadBit() == 0)
                    {
                        int val = _reader.ReadInt(3);
                        mode = val + (val >= mode ? 1 : 0);
                    }

                    cache[(1 + y2) * 5 + 1 + x2] = mode;

                    var vec = new Vector2ir(block.X + x2 * 4, block.Y + y2 * 4);

                    if (mode == 0)
                    {
                        Predict4x4Vertical(_planeBufferY, vec);
                    }
                    else if (mode == 1)
                    {
                        Predict4x4Horizontal(_planeBufferY, vec);
                    }
                    else if (mode == 2)
                    {
                        if (vec.X != 0 && vec.Y != 0)
                        {
                            Predict4x4Dc(_planeBufferY, vec);
                        }
                        else if (vec.X != 0)
                        {
                            Predict4x4LeftDc(_planeBufferY, vec);
                        }
                        else if (vec.Y != 0)
                        {
                            Predict4x4TopDc(_planeBufferY, vec);
                        }
                        else
                        {
                            Predict4x4Dc128(_planeBufferY, vec);
                        }
                    }
                    else if (mode == 3)
                    {
                        Predict4x4DownLeft(_planeBufferY, vec);
                    }
                    else if (mode == 4)
                    {
                        Predict4x4DownRight(_planeBufferY, vec);
                    }
                    else if (mode == 5)
                    {
                        Predict4x4VerticalRight(_planeBufferY, vec);
                    }
                    else if (mode == 6)
                    {
                        Predict4x4HorizontalDown(_planeBufferY, vec);
                    }
                    else if (mode == 7)
                    {
                        Predict4x4VerticalLeft(_planeBufferY, vec);
                    }
                    else if (mode == 8)
                    {
                        Predict4x4HorizontalUp(_planeBufferY, vec);
                    }
                    else
                    {
                        throw new ProgramException($"VX decoding error 019: {mode}");
                    }
                }
            }

            PredictNoTileUV(block);
        }

        private void Predict4x4Vertical(byte[,] planeBuffer, Vector2ir vec)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = planeBuffer[vec.Y - 1, vec.X + x];
                }
            }
        }

        private void Predict4x4Horizontal(byte[,] planeBuffer, Vector2ir vec)
        {
            for (int y = 0; y < 4; y++)
            {
                byte value = planeBuffer[vec.Y + y, vec.X - 1];
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = value;
                }
            }
        }

        private void Predict4x4Dc(byte[,] planeBuffer, Vector2ir vec)
        {
            byte value = (byte)((planeBuffer[vec.Y - 1, vec.X + 0] + planeBuffer[vec.Y - 1, vec.X + 1] +
                planeBuffer[vec.Y - 1, vec.X + 2] + planeBuffer[vec.Y - 1, vec.X + 3] +
                planeBuffer[vec.Y + 0, vec.X - 1] + planeBuffer[vec.Y + 1, vec.X - 1] +
                planeBuffer[vec.Y + 2, vec.X - 1] + planeBuffer[vec.Y + 3, vec.X - 1] + 4) / 8);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = value;
                }
            }
        }

        private void Predict4x4LeftDc(byte[,] planeBuffer, Vector2ir vec)
        {
            byte value = (byte)((planeBuffer[vec.Y + 0, vec.X - 1] + planeBuffer[vec.Y + 1, vec.X - 1] +
                planeBuffer[vec.Y + 2, vec.X - 1] + planeBuffer[vec.Y + 3, vec.X - 1] + 2) / 4);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = value;
                }
            }
        }

        private void Predict4x4TopDc(byte[,] planeBuffer, Vector2ir vec)
        {
            byte value = (byte)((planeBuffer[vec.Y - 1, vec.X + 0] + planeBuffer[vec.Y - 1, vec.X + 1] +
                planeBuffer[vec.Y - 1, vec.X + 2] + planeBuffer[vec.Y - 1, vec.X + 3] + 2) / 4);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = value;
                }
            }
        }

        private void Predict4x4Dc128(byte[,] planeBuffer, Vector2ir vec)
        {
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = 128;
                }
            }
        }

        private void Predict4x4DownLeft(byte[,] planeBuffer, Vector2ir vec)
        {
            int t0 = planeBuffer[vec.Y - 1, vec.X + 0];
            int t1 = planeBuffer[vec.Y - 1, vec.X + 1];
            int t2 = planeBuffer[vec.Y - 1, vec.X + 2];
            int t3 = planeBuffer[vec.Y - 1, vec.X + 3];

            int t4 = planeBuffer[vec.Y - 1, vec.X + 4];
            int t5 = planeBuffer[vec.Y - 1, vec.X + 5];
            int t6 = planeBuffer[vec.Y - 1, vec.X + 6];
            int t7 = planeBuffer[vec.Y - 1, vec.X + 7];

            int[] pixels =
            [
                (t0 + 2 * t1 + t2 + 2) / 4, // (0,0)
                (t1 + 2 * t2 + t3 + 2) / 4, // (1,0) (0,1)
                (t2 + 2 * t3 + t4 + 2) / 4, // (2,0) (1,1) (0,2)
                (t3 + 2 * t4 + t5 + 2) / 4, // (3,0) (2,1) (1,2) (0,3)
                (t4 + 2 * t5 + t6 + 2) / 4, // (3,1) (2,2) (1,3)
                (t5 + 2 * t6 + t7 + 2) / 4, // (3,2) (2,3)
                (t6 + 3 * t7 + 2)      / 4  // (3,3)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[x + y];
                }
            }
        }

        private void Predict4x4DownRight(byte[,] planeBuffer, Vector2ir vec)
        {
            int lt = planeBuffer[vec.Y - 1, vec.X - 1];

            int t0 = planeBuffer[vec.Y - 1, vec.X + 0];
            int t1 = planeBuffer[vec.Y - 1, vec.X + 1];
            int t2 = planeBuffer[vec.Y - 1, vec.X + 2];
            int t3 = planeBuffer[vec.Y - 1, vec.X + 3];

            int l0 = planeBuffer[vec.Y + 0, vec.X - 1];
            int l1 = planeBuffer[vec.Y + 1, vec.X - 1];
            int l2 = planeBuffer[vec.Y + 2, vec.X - 1];
            int l3 = planeBuffer[vec.Y + 3, vec.X - 1];

            int[] pixels =
            [
                (l3 + 2 * l2 + l1 + 2) / 4, // (0,3)
                (l2 + 2 * l1 + l0 + 2) / 4, // (0,2) (1,3)
                (l1 + 2 * l0 + lt + 2) / 4, // (0,1) (1,2) (2,3)
                (l0 + 2 * lt + t0 + 2) / 4, // (0,0) (1,1) (2,2) (3,3)
                (lt + 2 * t0 + t1 + 2) / 4, // (1,0) (2,1) (3,2)
                (t0 + 2 * t1 + t2 + 2) / 4, // (2,0) (3,1)
                (t1 + 2 * t2 + t3 + 2) / 4  // (3,0)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[3 + x - y];
                }
            }
        }

        private void Predict4x4VerticalRight(byte[,] planeBuffer, Vector2ir vec)
        {
            int lt = planeBuffer[vec.Y - 1, vec.X - 1];

            int t0 = planeBuffer[vec.Y - 1, vec.X + 0];
            int t1 = planeBuffer[vec.Y - 1, vec.X + 1];
            int t2 = planeBuffer[vec.Y - 1, vec.X + 2];
            int t3 = planeBuffer[vec.Y - 1, vec.X + 3];

            int l0 = planeBuffer[vec.Y + 0, vec.X - 1];
            int l1 = planeBuffer[vec.Y + 1, vec.X - 1];
            int l2 = planeBuffer[vec.Y + 2, vec.X - 1];
            //int l3 = int(planeBuffer[vec.Y + 3, vec.X - 1]);

            int[] pixels =
            [
                (l0 + 2 * l1 + l2 + 2) / 4, // (0,3)
                (lt + 2 * l0 + l1 + 2) / 4, // (0,2)
                (l0 + 2 * lt + t0 + 2) / 4, // (0,1) (1,3)
                (lt + t0 + 1)          / 2, // (0,0) (1,2)
                (lt + 2 * t0 + t1 + 2) / 4, // (1,1) (2,3)
                (t0 + t1 + 1)          / 2, // (1,0) (2,2)
                (t0 + 2 * t1 + t2 + 2) / 4, // (2,1) (3,3)
                (t1 + t2 + 1)          / 2, // (2,0) (3,2)
                (t1 + 2 * t2 + t3 + 2) / 4, // (3,1)
                (t2 + t3 + 1)          / 2  // (3,0)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[3 + 2 * x - y];
                }
            }
        }

        private void Predict4x4HorizontalDown(byte[,] planeBuffer, Vector2ir vec)
        {
            int lt = planeBuffer[vec.Y - 1, vec.X - 1];

            int t0 = planeBuffer[vec.Y - 1, vec.X + 0];
            int t1 = planeBuffer[vec.Y - 1, vec.X + 1];
            int t2 = planeBuffer[vec.Y - 1, vec.X + 2];
            //int t3 = int(planeBuffer[vec.Y - 1, vec.X + 3]);

            int l0 = planeBuffer[vec.Y + 0, vec.X - 1];
            int l1 = planeBuffer[vec.Y + 1, vec.X - 1];
            int l2 = planeBuffer[vec.Y + 2, vec.X - 1];
            int l3 = planeBuffer[vec.Y + 3, vec.X - 1];

            int[] pixels =
            [
                (t0 + 2 * t1 + t2 + 2) / 4, // (3,0)
                (lt + 2 * t0 + t1 + 2) / 4, // (2,0)
                (l0 + 2 * lt + t0 + 2) / 4, // (1,0) (3,1)
                (lt + l0 + 1)          / 2, // (0,0) (2,1)
                (lt + 2 * l0 + l1 + 2) / 4, // (1,1) (3,2)
                (l0 + l1 + 1)          / 2, // (0,1) (2,2)
                (l0 + 2 * l1 + l2 + 2) / 4, // (1,2) (3,3)
                (l1 + l2 + 1)          / 2, // (0,2) (2,3)
                (l1 + 2 * l2 + l3 + 2) / 4, // (1,3)
                (l2 + l3 + 1)          / 2  // (0,3)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[3 - x + 2 * y];
                }
            }
        }

        private void Predict4x4VerticalLeft(byte[,] planeBuffer, Vector2ir vec)
        {
            int t0 = planeBuffer[vec.Y - 1, vec.X + 0];
            int t1 = planeBuffer[vec.Y - 1, vec.X + 1];
            int t2 = planeBuffer[vec.Y - 1, vec.X + 2];
            int t3 = planeBuffer[vec.Y - 1, vec.X + 3];

            int t4 = planeBuffer[vec.Y - 1, vec.X + 4];
            int t5 = planeBuffer[vec.Y - 1, vec.X + 5];
            int t6 = planeBuffer[vec.Y - 1, vec.X + 6];
            //int t7 = int(planeBuffer[vec.Y - 1, vec.X + 7]);

            int[] pixels =
            [
                (t0 + t1 + 1)          / 2, // (0,0)
                (t0 + 2 * t1 + t2 + 2) / 4, // (0,1)
                (t1 + t2 + 1)          / 2, // (1,0) (0,2)
                (t1 + 2 * t2 + t3 + 2) / 4, // (1,1) (0,3)
                (t2 + t3 + 1)          / 2, // (2,0) (1,2)
                (t2 + 2 * t3 + t4 + 2) / 4, // (2,1) (1,3)
                (t3 + t4 + 1)          / 2, // (3,0) (2,2)
                (t3 + 2 * t4 + t5 + 2) / 4, // (3,1) (2,3)
                (t4 + t5 + 1)          / 2, // (3,2)
                (t4 + 2 * t5 + t6 + 2) / 4  // (3,3)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[2 * x + y];
                }
            }
        }

        private void Predict4x4HorizontalUp(byte[,] planeBuffer, Vector2ir vec)
        {
            int l0 = planeBuffer[vec.Y + 0, vec.X - 1];
            int l1 = planeBuffer[vec.Y + 1, vec.X - 1];
            int l2 = planeBuffer[vec.Y + 2, vec.X - 1];
            int l3 = planeBuffer[vec.Y + 3, vec.X - 1];

            int[] pixels =
            [
                (l0 + l1 + 1)          / 2, // (0,0)
                (l0 + 2 * l1 + l2 + 2) / 4, // (1,0)
                (l1 + l2 + 1)          / 2, // (2,0) (0,1)
                (l1 + 2 * l2 + l3 + 2) / 4, // (3,0) (1,1)
                (l2 + l3 + 1)          / 2, // (2,1) (0,2)
                (l2 + 2 * l3 + l3 + 2) / 4, // (3,1) (1,2)
                l3                          // (2,2) (0,3) (3,2) (1,3) (2,3) (3,3)
            ];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    planeBuffer[vec.Y + y, vec.X + x] = (byte)pixels[Math.Min(x + 2 * y, 6)];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetCoeffBuffer(byte[,] buffer, int step, int x, int y)
        {
            return buffer[y / (step * 4) + 1, x / (step * 4) + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetCoeffBuffer(byte[,] buffer, int step, int x, int y, byte value)
        {
            buffer[y / (step * 4) + 1, x / (step * 4) + 1] = value;
        }

        private void ClearTotalCoeff(Block block)
        {
            for (int y = 0; y < block.H; y += 8)
            {
                for (int x = 0; x < block.W; x += 8)
                {
                    // step: y = 1, u/v/uv = 2
                    SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y, 0);
                    SetCoeffBuffer(_coeffBufferY, 1, block.X + x, block.Y + y + 4, 0);
                    SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y, 0);
                    SetCoeffBuffer(_coeffBufferY, 1, block.X + x + 4, block.Y + y + 4, 0);
                    SetCoeffBuffer(_coeffBufferUV, 2, block.X + x, block.Y + y, 0);
                }
            }
        }
    }

    public class AudioFrame
    {
        private readonly AudioExtradata _extradata;
        private readonly AudioFrame? _prevAudioFrame;
        private readonly short[] _prevSampleBuffer;
        private readonly int[] _prevPulseBuffer;
        private readonly int[] _lpcFilterBuffer;
        private readonly int[] _influenceBuffer;
        private readonly short[] _sampleBuffer;
        private readonly int _sampleBufferIndex;
        public Span<short> SampleBuffer => new Span<short>(_sampleBuffer, 128 * _sampleBufferIndex, 128);

        private int _scale;
        public int Scale => _scale;

        private BitStreamReader _reader = null!;

        public AudioFrame(AudioExtradata extradata, AudioFrame? prevAudioFrame, VxBuffers buffers, int sampleBufferIndex)
        {
            _extradata = extradata;
            _prevAudioFrame = prevAudioFrame;
            _prevSampleBuffer = buffers.PrevSampleBuffer;
            _prevPulseBuffer = buffers.PrevPulseBuffer;
            _lpcFilterBuffer = buffers.LpcFilterBuffer;
            _influenceBuffer = buffers.InfluenceBuffer;
            _sampleBuffer = buffers.SampleBuffer;
            _sampleBufferIndex = sampleBufferIndex;
        }

        private static readonly ImmutableArray<int> _pulseDataLengths = [8, 5, 4, 3];
        private static readonly ImmutableArray<int> _pulseDistances = [3, 3, 4, 5];

        public void Decode(BitStreamReader reader)
        {
            _reader = reader;
            Span<int> lpcCodebookIndices = stackalloc int[3];
            int header1 = reader.ReadInt(16);
            int header2 = reader.ReadInt(16);
            // bits 0-5
            lpcCodebookIndices[0] = header1 & 0x3F;
            // bits 6-8
            int scaleModifierIndex = (header1 >> 6) & 7;
            // bits 9-15
            int prevFrameOffset = (header1 >> 9) & 0x7F;
            // bits 0-5
            lpcCodebookIndices[2] = header2 & 0x3F;
            // bits 6-11
            lpcCodebookIndices[1] = (header2 >> 6) & 0x3F;
            // bits 12-13
            int pulsePackingMode = (header2 >> 12) & 3;
            // bits 14-15
            int pulseStartPosition = (header2 >> 14) & 3;
            if (pulsePackingMode >= _pulseDataLengths.Length)
            {
                throw new ProgramException($"VX decoding error 020: {pulsePackingMode}");
            }
            if (scaleModifierIndex >= _extradata.ScaleModifiers.Length)
            {
                throw new ProgramException($"VX decoding error 021: {scaleModifierIndex}, {_extradata.ScaleModifiers.Length}");
            }
            int pulseDataLength = _pulseDataLengths[pulsePackingMode];
            Span<ushort> pulseData = stackalloc ushort[pulseDataLength];
            for (int i = 0; i < pulseDataLength; i++)
            {
                pulseData[i] = (ushort)reader.ReadInt(16);
            }
            // mode 0: pulseData length 8, 5 loop iterations, plus 2 more values = 42 output values
            // others: pulseData length 3/4/5, 8 loop iterations = 24/32/40 output values
            Span<int> pulseValues = stackalloc int[pulsePackingMode == 0 ? 42 : pulseDataLength * 8];
            if (pulsePackingMode == 0)
            {
                int v = 0;
                for (int i = 0; i < pulseDataLength; i++)
                {
                    for (int j = 13; j >= 0; j -= 3)
                    {
                        pulseValues[v++] = ((pulseData[i] >> j) & 7) * 2 - 7;
                    }
                }
                pulseValues[v++] = ((pulseData[0] & 1) * 4 + (pulseData[1] & 1) * 2 + (pulseData[2] & 1) * 1) * 2 - 7;
                pulseValues[v++] = ((pulseData[3] & 1) * 4 + (pulseData[4] & 1) * 2 + (pulseData[5] & 1) * 1) * 2 - 7;
            }
            else
            {
                int v = 0;
                for (int i = 0; i < pulseDataLength; i++)
                {
                    for (int j = 14; j >= 0; j -= 2)
                    {
                        pulseValues[v++] = ((pulseData[i] >> j) & 3) * 2 - 3;
                    }
                }
            }
            _scale = _extradata.ScaleInitial;
            if (prevFrameOffset != 127)
            {
                Debug.Assert(_prevAudioFrame != null);
                _scale = _prevAudioFrame.Scale;
            }
            _scale = _scale * _extradata.ScaleModifiers[scaleModifierIndex] / 8192;
            int pulseDistance = _pulseDistances[pulsePackingMode];
            Span<int> pulseBuffer = stackalloc int[128];
            if (prevFrameOffset < 126)
            {
                for (int i = 0; i < 128; i++)
                {
                    int volume = Math.Min(8, Math.Min(i + 1, 128 - i));
                    pulseBuffer[i] = _prevPulseBuffer[i + 127 - prevFrameOffset] * volume / 16;
                }
            }
            for (int i = 0; i < 128; i++)
            {
                (int index, int remainder) = Math.DivRem(i - pulseStartPosition, pulseDistance);
                if (remainder == 0 && index >= 0 && index < pulseValues.Length)
                {
                    pulseBuffer[i] += pulseValues[index] * _scale;
                }
            }
            if (prevFrameOffset == 127)
            {
                // if not 127, keep using the values from the previous frame
                Array.Copy(_extradata.LpcBase, _lpcFilterBuffer, 8);
            }
            for (int i = 0; i < 8; i++)
            {
                int coeffSum = 0;
                for (int j = 0; j < 3; j++)
                {
                    int index = lpcCodebookIndices[j];
                    coeffSum += _extradata.LpcCodebooks[j, index, i];
                }
                _lpcFilterBuffer[i] += coeffSum;
            }
            Span<int> influenceValues = stackalloc int[8];
            Span<int> influenceTemp = stackalloc int[8];
            for (int i = 0; i < 8; i++)
            {
                influenceValues.CopyTo(influenceTemp);
                int coeff = _lpcFilterBuffer[i];
                for (int j = 0; j < i; j++)
                {
                    influenceValues[j] += influenceTemp[i - j - 1] * coeff / 32768;
                }
                influenceValues[i] = coeff;
            }
            for (int i = 0; i < 8; i++)
            {
                influenceValues[i] = influenceValues[i] / -2;
            }
            Span<int> influenceQuarters = stackalloc int[4 * 8];
            if (prevFrameOffset != 127)
            {
                Debug.Assert(_prevAudioFrame != null);
                influenceValues.CopyTo(influenceQuarters[24..32]);
                for (int i = 0; i < 8; i++)
                {
                    influenceQuarters[8 + i] = (_influenceBuffer[i] + influenceQuarters[3 * 8 + i]) / 2;
                }
                for (int i = 0; i < 8; i++)
                {
                    influenceQuarters[i] = (_influenceBuffer[i] + influenceQuarters[8 + i]) / 2;
                }
                for (int i = 0; i < 8; i++)
                {
                    influenceQuarters[2 * 8 + i] = (influenceQuarters[8 + i] + influenceQuarters[3 * 8 + i]) / 2;
                }
            }
            else
            {
                influenceValues.CopyTo(influenceQuarters);
                influenceValues.CopyTo(influenceQuarters[8..16]);
                influenceValues.CopyTo(influenceQuarters[16..24]);
                influenceValues.CopyTo(influenceQuarters[24..32]);
            }
            Span<short> sampleBuffer = SampleBuffer;
            sampleBuffer.Clear();
            for (int i = 0; i < 128; i++)
            {
                int quarterStart = i * 4 / 128 * 8;
                int quarterEnd = quarterStart + 8;
                Span<int> quarterSpan = influenceQuarters[quarterStart..quarterEnd];
                int sample = pulseBuffer[i] * 16384;
                for (int j = 0; j < 8; j++)
                {
                    int sampleIndex = i - j - 1;
                    int prevSample = sampleIndex >= 0 ? sampleBuffer[sampleIndex] : _prevSampleBuffer[sampleIndex + 8];
                    sample += prevSample * quarterSpan[j];
                }
                sample = sample / 16384;
                sampleBuffer[i] = (short)Math.Clamp(sample, Int16.MinValue, Int16.MaxValue);
            }
            // copy the last 128 elements of the prev pulse array (n-1) into that array's first 128 elements (n-2)
            Array.Copy(_prevPulseBuffer, 128, _prevPulseBuffer, 0, 128);
            // copy our 128 pulse values (n) into the last 128 elements of the prev pulse array (n-1)
            pulseBuffer.CopyTo(new Span<int>(_prevPulseBuffer, 128, 128));
            // copy the last 8 of our 128 sample values (n) into the prev sample array's 8 elements (n-1)
            sampleBuffer[120..128].CopyTo(_prevSampleBuffer);
            // copy our 8 influence values (n) into the prev influence array's 8 elements (n-1)
            influenceValues.CopyTo(_influenceBuffer);
            _reader = null!;
        }
    }

    public class VLCData
    {
        private readonly FrozenDictionary<int, int> _bitDict;

        public int MaxBitCount { get; }

        public VLCData(ImmutableArray<int> lengthList, ImmutableArray<int> bitList)
        {
            var bitDict = new Dictionary<int, int>(lengthList.Length);
            for (int i = 0; i < lengthList.Length; i++)
            {
                int length = lengthList[i];
                int value = bitList[i];
                string bitString = "";
                int hashCode = 0;
                if (length != 0)
                {
                    bitString = value.ToString("b").PadLeft(length, '0');
                    foreach (char c in bitString)
                    {
                        hashCode = HashCode.Combine(hashCode, (int)c - 48);
                    }
                    bitDict.Add(hashCode, i);
                    MaxBitCount = Math.Max(MaxBitCount, bitString.Length);
                }
            }
            _bitDict = bitDict.ToFrozenDictionary();
        }

        public int FindBitPattern(int hashCode)
        {
            return _bitDict.GetValueOrDefault(hashCode, -1);
        }
    }

    public static class VLC
    {
        public static int Temp { get; }
        public static ImmutableArray<VLCData> CoeffTokenVlc { get; }
        public static ImmutableArray<VLCData> TotalZeroesVlc { get; }
        public static ImmutableArray<VLCData> RunVlc { get; }
        public static VLCData Run7Vlc { get; }

        static VLC()
        {
            var coeffTokenVlcs = new VLCData[_coeffTokenLengths.Length];
            for (int i = 0; i < _coeffTokenLengths.Length; i++)
            {
                coeffTokenVlcs[i] = new VLCData(_coeffTokenLengths[i], _coeffTokenBits[i]);
            }
            CoeffTokenVlc = ImmutableCollectionsMarshal.AsImmutableArray(coeffTokenVlcs);
            var totalZeroesVlcs = new VLCData[_totalZeroesLengths.Length];
            for (int i = 0; i < _totalZeroesLengths.Length; i++)
            {
                totalZeroesVlcs[i] = new VLCData(_totalZeroesLengths[i], _totalZeroesBits[i]);
            }
            TotalZeroesVlc = ImmutableCollectionsMarshal.AsImmutableArray(totalZeroesVlcs);
            var runVlcs = new VLCData[_runLengths.Length];
            for (int i = 0; i < _runLengths.Length; i++)
            {
                runVlcs[i] = new VLCData(_runLengths[i], _runBits[i]);
            }
            RunVlc = ImmutableCollectionsMarshal.AsImmutableArray(runVlcs);
            Run7Vlc = new VLCData(_run7Lengths, _run7Bits);
            Temp = 1;
        }

        private static readonly ImmutableArray<ImmutableArray<int>> _coeffTokenLengths =
        [
            [
                 1,  0,  0,  0,
                 6,  2,  0,  0,   8,  6,  3,  0,   9,  8,  7,  5,  10,  9,  8,  6,
                11, 10,  9,  7,  13, 11, 10,  8,  13, 13, 11,  9,  13, 13, 13, 10,
                14, 14, 13, 11,  14, 14, 14, 13,  15, 15, 14, 14,  15, 15, 15, 14,
                16, 15, 15, 15,  16, 16, 16, 15,  16, 16, 16, 16,  16, 16, 16, 16
            ],
            [
                 2,  0,  0,  0,
                 6,  2,  0,  0,   6,  5,  3,  0,   7,  6,  6,  4,   8,  6,  6,  4,
                 8,  7,  7,  5,   9,  8,  8,  6,  11,  9,  9,  6,  11, 11, 11,  7,
                12, 11, 11,  9,  12, 12, 12, 11,  12, 12, 12, 11,  13, 13, 13, 12,
                13, 13, 13, 13,  13, 14, 13, 13,  14, 14, 14, 13,  14, 14, 14, 14
            ],
            [
                 4,  0,  0,  0,
                 6,  4,  0,  0,   6,  5,  4,  0,   6,  5,  5,  4,   7,  5,  5,  4,
                 7,  5,  5,  4,   7,  6,  6,  4,   7,  6,  6,  4,   8,  7,  7,  5,
                 8,  8,  7,  6,   9,  8,  8,  7,   9,  9,  8,  8,   9,  9,  9,  8,
                10,  9,  9,  9,  10, 10, 10, 10,  10, 10, 10, 10,  10, 10, 10, 10
            ],
            [
                 6,  0,  0,  0,
                 6,  6,  0,  0,   6,  6,  6,  0,   6,  6,  6,  6,   6,  6,  6,  6,
                 6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6,
                 6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6,
                 6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6,   6,  6,  6,  6
            ]
        ];

        private static readonly ImmutableArray<ImmutableArray<int>> _coeffTokenBits =
        [
            [
                 1,  0,  0,  0,
                 5,  1,  0,  0,   7,  4,  1,  0,   7,  6,  5,  3,   7,  6,  5,  3,
                 7,  6,  5,  4,  15,  6,  5,  4,  11, 14,  5,  4,   8, 10, 13,  4,
                15, 14,  9,  4,  11, 10, 13, 12,  15, 14,  9, 12,  11, 10, 13,  8,
                15,  1,  9, 12,  11, 14, 13,  8,   7, 10,  9, 12,   4,  6,  5,  8
            ],
            [
                 3,  0,  0,  0,
                11,  2,  0,  0,   7,  7,  3,  0,   7, 10,  9,  5,   7,  6,  5,  4,
                 4,  6,  5,  6,   7,  6,  5,  8,  15,  6,  5,  4,  11, 14, 13,  4,
                15, 10,  9,  4,  11, 14, 13, 12,   8, 10,  9,  8,  15, 14, 13, 12,
                11, 10,  9, 12,   7, 11,  6,  8,   9,  8, 10,  1,   7,  6,  5,  4
            ],
            [
                15,  0,  0,  0,
                15, 14,  0,  0,  11, 15, 13,  0,   8, 12, 14, 12,  15, 10, 11, 11,
                11,  8,  9, 10,   9, 14, 13,  9,   8, 10,  9,  8,  15, 14, 13, 13,
                11, 14, 10, 12,  15, 10, 13, 12,  11, 14,  9, 12,   8, 10, 13,  8,
                13,  7,  9, 12,   9, 12, 11, 10,   5,  8,  7,  6,   1,  4,  3,  2
            ],
            [
                 3,  0,  0,  0,
                 0,  1,  0,  0,   4,  5,  6,  0,   8,  9, 10, 11,  12, 13, 14, 15,
                16, 17, 18, 19,  20, 21, 22, 23,  24, 25, 26, 27,  28, 29, 30, 31,
                32, 33, 34, 35,  36, 37, 38, 39,  40, 41, 42, 43,  44, 45, 46, 47,
                48, 49, 50, 51,  52, 53, 54, 55,  56, 57, 58, 59,  60, 61, 62, 63
            ]
        ];

        private static readonly ImmutableArray<ImmutableArray<int>> _totalZeroesLengths =
        [
            [],
            [1, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 9],
            [3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 6, 6, 6, 6],
            [4, 3, 3, 3, 4, 4, 3, 3, 4, 5, 5, 6, 5, 6],
            [5, 3, 4, 4, 3, 3, 3, 4, 3, 4, 5, 5, 5],
            [4, 4, 4, 3, 3, 3, 3, 3, 4, 5, 4, 5],
            [6, 5, 3, 3, 3, 3, 3, 3, 4, 3, 6],
            [6, 5, 3, 3, 3, 2, 3, 4, 3, 6],
            [6, 4, 5, 3, 2, 2, 3, 3, 6],
            [6, 6, 4, 2, 2, 3, 2, 5],
            [5, 5, 3, 2, 2, 2, 4],
            [4, 4, 3, 3, 1, 3],
            [4, 4, 2, 1, 3],
            [3, 3, 1, 2],
            [2, 2, 1],
            [1, 1]
        ];

        private static readonly ImmutableArray<ImmutableArray<int>> _totalZeroesBits =
        [
            [],
            [1, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 1],
            [7, 6, 5, 4, 3, 5, 4, 3, 2, 3, 2, 3, 2, 1, 0],
            [5, 7, 6, 5, 4, 3, 4, 3, 2, 3, 2, 1, 1, 0],
            [3, 7, 5, 4, 6, 5, 4, 3, 3, 2, 2, 1, 0],
            [5, 4, 3, 7, 6, 5, 4, 3, 2, 1, 1, 0],
            [1, 1, 7, 6, 5, 4, 3, 2, 1, 1, 0],
            [1, 1, 5, 4, 3, 3, 2, 1, 1, 0],
            [1, 1, 1, 3, 3, 2, 2, 1, 0],
            [1, 0, 1, 3, 2, 1, 1, 1],
            [1, 0, 1, 3, 2, 1, 1],
            [0, 1, 1, 2, 1, 3],
            [0, 1, 1, 1, 1],
            [0, 1, 1, 1],
            [0, 1, 1],
            [0, 1]
        ];

        private static readonly ImmutableArray<ImmutableArray<int>> _runLengths =
        [
            [],
            [1, 1],
            [1, 2, 2],
            [2, 2, 2, 2],
            [2, 2, 2, 3, 3],
            [2, 2, 3, 3, 3, 3],
            [2, 3, 3, 3, 3, 3, 3]
        ];

        private static readonly ImmutableArray<ImmutableArray<int>> _runBits =
        [
            [],
            [1, 0],
            [1, 1, 0],
            [3, 2, 1, 0],
            [3, 2, 1, 1, 0],
            [3, 2, 3, 2, 1, 0],
            [3, 0, 1, 3, 2, 5, 4]
        ];

        private static readonly ImmutableArray<int> _run7Lengths = [3, 3, 3, 3, 3, 3, 3, 4, 5, 6, 7, 8, 9, 10, 11];

        private static readonly ImmutableArray<int> _run7Bits = [7, 6, 5, 4, 3, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1];
    }
}
