using System;
using System.Diagnostics;
using System.Threading;
using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using MphRead.Entities;
using MphRead.Mods.Render;
using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace MphRead.Droid
{
    /// <summary>
    /// The match, on a surface, with the EGL context and the thread that owns
    /// it belonging to this class rather than to <c>GLSurfaceView</c>.
    ///
    /// **That is the whole reason this is not a GLSurfaceView.** Everything the
    /// engine does with GL -- loading a room's textures, baking its geometry,
    /// compiling the shaders, drawing -- has to happen on the thread that holds
    /// the context, so the scene is *built* there, and building it takes
    /// seconds. GLSurfaceView answers a window event by handing the new size to
    /// that thread and then **waiting on the UI thread until it has been all
    /// the way round its loop**: `surfaceChanged`, `onPause` and `onResume` all
    /// do it. So any window event that landed while a room was loading froze
    /// the UI thread for the length of the load, and Android put its own "isn't
    /// responding" dialog over the loading screen -- a white box over a black
    /// one, with nothing to press, which is what starting a match from portrait
    /// did.
    ///
    /// Waiting for the window to hold still before creating the view made that
    /// rarer and could not make it impossible: a phone can resize its own
    /// window at any moment, for the system bars, for insets, for a call. Here
    /// the callbacks write a field and return, and the loop picks it up when it
    /// is next between frames. Nothing on the UI thread ever waits for a load.
    ///
    /// Two things come free from owning the context. It survives the surface
    /// going away and coming back, so a match is not lost to it (GLSurfaceView
    /// only kept it as a favour, through `PreserveEGLContextOnPause`); and
    /// pausing is a flag rather than a handshake.
    ///
    /// The loop is the desktop's, in the same order: pause, update, render.
    /// What is not the desktop's is the pacing. <c>RenderWindow</c> asks OpenTK
    /// for 60 updates a second; here the buffer swaps at the display's rate,
    /// which on a modern phone is 90 or 120, and the engine's update *is* its
    /// frame -- a render with no update in front of it draws nothing, because
    /// the render item lists are built during the update and cleared after the
    /// draw. So the thread waits for the next 60 Hz tick instead of rendering
    /// more often than the game ticks.
    /// </summary>
    internal sealed class GameView : SurfaceView, ISurfaceHolderCallback
    {
        private readonly RenderLoop _loop;

        public GameView(Context context, TouchControls controls, AndroidInput input,
            Func<AndroidInput, Vector2i, Scene> build, Action onEnd, Action onLoaded,
            Action<string> onError, Action onPauseMenu, Action<bool> onSoftKeyboard)
            : base(context)
        {
            _loop = new RenderLoop(controls, input, build, onEnd, onLoaded, onError,
                onPauseMenu, onSoftKeyboard);
            Holder?.AddCallback(this);
            // So this view can receive key events at all: from a keyboard
            // plugged into the phone, from one paired over Bluetooth, from the
            // emulator's, and from the soft keyboard the CHAT button asks for.
            // Nothing else here wants them -- the touch overlay sits on top and
            // takes every touch, and focus and touch are separate things.
            Focusable = true;
            FocusableInTouchMode = true;
            RequestFocus();
        }

        /// <summary>
        /// Yes, when chat is open -- which is what makes the system offer a
        /// soft keyboard for this view rather than refusing to show one.
        /// </summary>
        public override bool OnCheckIsTextEditor()
        {
            return MphRead.Mods.Chat.ChatBox.Composing;
        }

        /// <summary>
        /// An editor with no text in it.
        ///
        /// <see cref="InputTypes.Null"/> is doing the work: it tells the IME
        /// that this view cannot be edited through the usual commitText path,
        /// and every keyboard worth the name answers that by sending plain key
        /// events instead -- which is exactly what
        /// <see cref="OnKeyDown"/> below already handles for a real keyboard.
        /// The alternative is an <c>InputConnection</c> that maintains an
        /// editable buffer and keeps it in step with <c>ChatBox</c>'s, which is
        /// two copies of one string and a second set of rules for composing
        /// text.
        /// </summary>
        public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
        {
            if (outAttrs != null)
            {
                outAttrs.InputType = InputTypes.Null;
                // NoFullscreen and NoExtractUi together are what stop the IME
                // replacing the whole screen with its own text box in
                // landscape -- which is every phone playing this, and which
                // would put an editor over the match rather than a keyboard
                // under it.
                outAttrs.ImeOptions = (ImeFlags)((int)ImeAction.Done
                    | (int)ImeFlags.NoFullscreen | (int)ImeFlags.NoExtractUi);
            }
            return new BaseInputConnection(this, fullEditor: false);
        }

        /// <summary>
        /// The key whose press chat took, so its release can be taken too.
        ///
        /// Not a check on "is chat open": Back closes the prompt on the way
        /// down, so by the time its release arrives the prompt is shut and the
        /// release would fall through to the activity -- which is where
        /// <c>onBackPressed</c> lives, and which would leave the match. One
        /// key at a time is enough; nothing here is chorded.
        /// </summary>
        private Keycode _keyTaken = Keycode.Unknown;

        public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
        {
            // The pad first: its buttons are their own key codes and overlap
            // nothing a keyboard sends, so this only ever claims events a
            // keyboard could not have produced. A fallback in practice --
            // MainActivity.DispatchKeyEvent takes a pad's events before any
            // view sees them, so that rebinding one works on the settings
            // screen too -- and kept because it costs a comparison and this
            // class should still work if it is ever hosted somewhere else.
            if (GamepadBridge.HandleKey(keyCode, e, down: true))
            {
                return true;
            }
            if (HandleKey(keyCode, e))
            {
                _keyTaken = keyCode;
                return true;
            }
            return base.OnKeyDown(keyCode, e);
        }

        public override bool OnKeyUp(Keycode keyCode, KeyEvent? e)
        {
            if (GamepadBridge.HandleKey(keyCode, e, down: false))
            {
                return true;
            }
            if (_keyTaken == keyCode)
            {
                _keyTaken = Keycode.Unknown;
                return true;
            }
            return base.OnKeyUp(keyCode, e);
        }

        /// <summary>
        /// The sticks and triggers. Android has no way to poll a pad, so this
        /// is the only place their positions are ever reported -- see
        /// <see cref="GamepadBridge"/>.
        /// </summary>
        public override bool OnGenericMotionEvent(MotionEvent? e)
        {
            return GamepadBridge.HandleMotion(e) || base.OnGenericMotionEvent(e);
        }

        /// <summary>
        /// One key, from whatever is attached. Returns true when chat took it.
        ///
        /// Android delivers the character *with* the key event, where GLFW
        /// raises two callbacks for one press -- so this is both of the
        /// desktop's paths in one method, and the opening key does not have to
        /// be swallowed on its way back round.
        /// </summary>
        private bool HandleKey(Keycode keyCode, KeyEvent? e)
        {
            bool composing = MphRead.Mods.Chat.ChatBox.Composing;
            bool control = e?.IsCtrlPressed ?? false;
            bool alt = e?.IsAltPressed ?? false;
            if (!composing)
            {
                // The results screen's hunter picker owns the arrow keys
                // while it is up. The panel is drawn by the shared HUD, so it
                // appears here whether or not this head hooks it -- and a
                // picker that cannot be operated is worse than none. Only a
                // real keyboard or a pad's d-pad reaches this; there is no
                // touch control for it.
                if (MphRead.Mods.EndScreen.HandleKeyDown(Map(keyCode)))
                {
                    return true;
                }
                // The one key that opens it, and only where there is a match
                // to talk in. Everything else belongs to whoever asked next.
                return MphRead.Mods.Chat.ChatBox.HandleKeyDown(Map(keyCode), control, alt,
                    canOpen: Scene != null, swallowOpeningChar: false);
            }
            Keys key = Map(keyCode);
            if (key == Keys.Enter || key == Keys.Escape || key == Keys.Backspace)
            {
                MphRead.Mods.Chat.ChatBox.HandleKeyDown(key, control, alt, canOpen: false);
                return true;
            }
            int unicode = e?.GetUnicodeChar(e.MetaState) ?? 0;
            if (unicode != 0)
            {
                MphRead.Mods.Chat.ChatBox.HandleText(unicode);
            }
            // Everything while the prompt is up, character or not: a key that
            // fell through here would reach the activity, and Back would leave
            // the match in the middle of a sentence.
            return true;
        }

        /// <summary>
        /// Android key codes to the ones <c>InputSettings.ChatKey</c> is
        /// expressed in. Only what chat needs: the letters and digits any
        /// sensible chat key could be bound to, and the four keys that drive
        /// the prompt.
        /// </summary>
        private static Keys Map(Keycode code)
        {
            if (code >= Keycode.A && code <= Keycode.Z)
            {
                return Keys.A + (code - Keycode.A);
            }
            if (code >= Keycode.Num0 && code <= Keycode.Num9)
            {
                return Keys.D0 + (code - Keycode.Num0);
            }
            return code switch
            {
                Keycode.Enter or Keycode.NumpadEnter => Keys.Enter,
                Keycode.Escape or Keycode.Back => Keys.Escape,
                Keycode.Del => Keys.Backspace,
                Keycode.Space => Keys.Space,
                Keycode.Tab => Keys.Tab,
                // The arrows, which nothing here used to need: they are the
                // results screen's picker, and a pad's d-pad arrives as these
                // same codes on Android.
                Keycode.DpadLeft => Keys.Left,
                Keycode.DpadRight => Keys.Right,
                Keycode.DpadUp => Keys.Up,
                Keycode.DpadDown => Keys.Down,
                _ => Keys.Unknown
            };
        }

        public Scene? Scene => _loop.Scene;

        public void Stop()
        {
            _loop.RequestStop();
        }

        public void OnPause()
        {
            _loop.SetPaused(true);
        }

        public void OnResume()
        {
            _loop.SetPaused(false);
        }

        // The three callbacks. None of them waits for the render thread; that
        // is the point of the class.

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            // Nothing to do: surfaceChanged always follows, with the size.
        }

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            _loop.SurfaceReady(holder, width, height);
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            _loop.SurfaceGone();
        }

        /// <summary>
        /// The GL context, the thread that owns it, and the game loop that
        /// runs on it.
        /// </summary>
        private sealed class RenderLoop
        {
            /// <summary>
            /// The shortest frame this loop will pace itself to when the
            /// player has asked for the display's own rate.
            ///
            /// In that mode the pacing is <c>eglSwapBuffers</c>, which blocks
            /// until the panel is ready -- sleeping as well would be double
            /// pacing and would halve the rate on a phone whose swap already
            /// blocks. This is only a floor so that a driver which does *not*
            /// block (an emulator, a surface with no vsync) spins at 500 Hz
            /// instead of as fast as the CPU will go.
            /// </summary>
            private const double MinFrameSeconds = 1.0 / FrameTiming.MaxCap;

            /// <summary>
            /// Density-independent pixels of drag per unit of mouse movement.
            /// One means a swipe turns as far as a mouse moved the same
            /// distance would, which the player's own sensitivity setting then
            /// scales the way it does everywhere else.
            /// </summary>
            private const float AimScale = 1f;

            // EGL_OPENGL_ES3_BIT_KHR. EGL14 exposes the ES2 bit and stops
            // there, and an ES2 config will happily give an ES3 context on most
            // drivers -- but "most" is how a phone gets a context that fails
            // every call in GlEs with no message.
            private const int OpenGlEs3Bit = 0x40;

            /// <summary>
            /// How long <see cref="SurfaceGone"/> will wait for the render
            /// thread to let go. Android wants the surface unused by the time
            /// that callback returns, and this is the one place where that is
            /// worth a wait at all -- but not an unbounded one: the thread
            /// cannot answer from inside a room load, and hanging the UI thread
            /// is the thing this class exists to stop.
            /// </summary>
            private const int SurfaceReleaseMs = 2000;

            private readonly TouchControls _controls;
            private readonly AndroidInput _input;
            private readonly Func<AndroidInput, Vector2i, Scene> _build;
            private readonly Action _onEnd;
            private readonly Action _onLoaded;
            private readonly Action<string> _onError;
            private readonly Stopwatch _clock = new Stopwatch();
            private readonly object _lock = new object();
            private readonly Thread _thread;

            private ISurfaceHolder? _holder;
            private Vector2i _wanted;
            private bool _paused;
            private bool _stopping;
            private bool _holdingSurface;
            private bool _ended;
            private bool _dialogClickDown;
            private readonly Action _onPauseMenu;
            /// <summary>
            /// Show or hide the soft keyboard. An IME call belongs to the UI
            /// thread and this is the GL one, so it is asked for rather than
            /// done here -- the same arrangement <see cref="_onPauseMenu"/>
            /// has, and for the same reason.
            /// </summary>
            private readonly Action<bool> _onSoftKeyboard;
            private bool _menuWasHeld;
            private bool _spectateCycleHeld;
            private bool _spectateViewHeld;
            private bool _missileWasHeld;
            private bool _chatWasHeld;
            private bool _keyboardShown;

            private EGLDisplay? _display;
            private EGLConfig? _config;
            private EGLSurface? _eglSurface;
            private EGLContext? _context;
            private ISurfaceHolder? _boundTo;
            private Vector2i _size;
            private double _nextFrame;
            private double _lastFrameStart;
            private int _requestedFrameRate = -1;

            public Scene? Scene { get; private set; }

            public RenderLoop(TouchControls controls, AndroidInput input,
                Func<AndroidInput, Vector2i, Scene> build, Action onEnd, Action onLoaded,
                Action<string> onError, Action onPauseMenu, Action<bool> onSoftKeyboard)
            {
                _onPauseMenu = onPauseMenu;
                _onSoftKeyboard = onSoftKeyboard;
                _controls = controls;
                _input = input;
                _build = build;
                _onEnd = onEnd;
                _onLoaded = onLoaded;
                _onError = onError;
                _thread = new Thread(Run) { Name = "FruityPrime GL", IsBackground = true };
                _thread.Start();
            }

            public void RequestStop()
            {
                lock (_lock)
                {
                    _stopping = true;
                    Monitor.PulseAll(_lock);
                }
            }

            public void SetPaused(bool paused)
            {
                lock (_lock)
                {
                    _paused = paused;
                    Monitor.PulseAll(_lock);
                }
            }

            public void SurfaceReady(ISurfaceHolder holder, int width, int height)
            {
                if (width <= 0 || height <= 0)
                {
                    return;
                }
                lock (_lock)
                {
                    _holder = holder;
                    _wanted = new Vector2i(width, height);
                    Monitor.PulseAll(_lock);
                }
            }

            public void SurfaceGone()
            {
                lock (_lock)
                {
                    _holder = null;
                    Monitor.PulseAll(_lock);
                    long deadline = Environment.TickCount64 + SurfaceReleaseMs;
                    while (_holdingSurface)
                    {
                        int left = (int)(deadline - Environment.TickCount64);
                        if (left <= 0)
                        {
                            // Mid-load, almost certainly. The thread drops the
                            // surface the moment it looks up, and a swap
                            // against a surface the framework has taken back
                            // fails rather than crashing -- which is handled.
                            Console.WriteLine("[android] the surface went away while the GL thread "
                                + "was busy; carrying on without waiting for it");
                            break;
                        }
                        Monitor.Wait(_lock, left);
                    }
                }
            }

            private void Run()
            {
                try
                {
                    Loop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[android] the render thread stopped: {ex}");
                    if (!_ended)
                    {
                        _ended = true;
                        _onError(ex.Message);
                    }
                }
                finally
                {
                    ReleaseSurface();
                    DestroyContext();
                }
            }

            private void Loop()
            {
                while (true)
                {
                    ISurfaceHolder holder;
                    Vector2i wanted;
                    lock (_lock)
                    {
                        while (!_stopping && (_holder == null || _paused))
                        {
                            // Nothing to draw into, or nobody looking. Let go
                            // of the surface first if it is the former, so
                            // SurfaceGone is not left waiting on us.
                            if (_holder == null && _holdingSurface)
                            {
                                Monitor.Exit(_lock);
                                try
                                {
                                    ReleaseSurface();
                                }
                                finally
                                {
                                    Monitor.Enter(_lock);
                                }
                                Monitor.PulseAll(_lock);
                                continue;
                            }
                            Monitor.Wait(_lock);
                        }
                        if (_stopping)
                        {
                            break;
                        }
                        holder = _holder!;
                        wanted = _wanted;
                    }
                    if (!BindSurface(holder, wanted))
                    {
                        continue;
                    }
                    if (Scene == null)
                    {
                        if (_ended)
                        {
                            break;
                        }
                        BuildScene();
                        continue;
                    }
                    if (!DrawFrame())
                    {
                        break;
                    }
                }
                Scene? scene = Scene;
                if (scene != null)
                {
                    End(scene);
                }
            }

            /// <summary>
            /// Make sure there is a context, a surface for this holder, and a
            /// viewport at the size the window last reported.
            /// </summary>
            private bool BindSurface(ISurfaceHolder holder, Vector2i wanted)
            {
                if (_display == null && !CreateContext())
                {
                    return false;
                }
                if (!ReferenceEquals(_boundTo, holder) || _eglSurface == null)
                {
                    ReleaseSurface();
                    if (!CreateSurface(holder))
                    {
                        return false;
                    }
                }
                if (wanted != _size)
                {
                    _size = wanted;
                    GL.Viewport(0, 0, _size.X, _size.Y);
                    if (Scene != null)
                    {
                        // A resize is one frame's work here and nothing on the
                        // UI thread is waiting for it.
                        Scene.Size = _size;
                        Scene.OnResize();
                    }
                }
                return true;
            }

            private bool CreateContext()
            {
                _display = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
                if (_display == null || _display.Equals(EGL14.EglNoDisplay))
                {
                    return Fail("no EGL display");
                }
                int[] version = new int[2];
                if (!EGL14.EglInitialize(_display, version, 0, version, 1))
                {
                    return Fail($"eglInitialize failed (0x{EGL14.EglGetError():X})");
                }
                // 8/8/8 colour, 24-bit depth and 8 bits of stencil: the
                // renderer's translucency passes mark faces in the stencil
                // buffer, and a config without one draws the transparent
                // surfaces wrong rather than failing.
                int[] attributes =
                {
                    EGL14.EglRenderableType, OpenGlEs3Bit,
                    EGL14.EglSurfaceType, EGL14.EglWindowBit,
                    EGL14.EglRedSize, 8,
                    EGL14.EglGreenSize, 8,
                    EGL14.EglBlueSize, 8,
                    EGL14.EglAlphaSize, 0,
                    EGL14.EglDepthSize, 24,
                    EGL14.EglStencilSize, 8,
                    EGL14.EglNone
                };
                var configs = new EGLConfig[1];
                int[] found = new int[1];
                if (!EGL14.EglChooseConfig(_display, attributes, 0, configs, 0, 1, found, 0)
                    || found[0] < 1 || configs[0] == null)
                {
                    return Fail("no EGL config with a window, depth and stencil");
                }
                _config = configs[0];
                _context = EGL14.EglCreateContext(_display, _config, EGL14.EglNoContext,
                    new[] { EGL14.EglContextClientVersion, 3, EGL14.EglNone }, 0);
                if (_context == null || _context.Equals(EGL14.EglNoContext))
                {
                    return Fail($"eglCreateContext failed (0x{EGL14.EglGetError():X})");
                }
                return true;
            }

            private bool CreateSurface(ISurfaceHolder holder)
            {
                if (_display == null || _config == null || _context == null)
                {
                    return false;
                }
                Surface? window = holder.Surface;
                if (window == null || !window.IsValid)
                {
                    return false;
                }
                _eglSurface = EGL14.EglCreateWindowSurface(_display, _config, window,
                    new[] { EGL14.EglNone }, 0);
                if (_eglSurface == null || _eglSurface.Equals(EGL14.EglNoSurface))
                {
                    _eglSurface = null;
                    // Not fatal on its own: the window can be on its way out.
                    Console.WriteLine("[android] eglCreateWindowSurface failed "
                        + $"(0x{EGL14.EglGetError():X})");
                    return false;
                }
                if (!EGL14.EglMakeCurrent(_display, _eglSurface, _eglSurface, _context))
                {
                    return Fail($"eglMakeCurrent failed (0x{EGL14.EglGetError():X})");
                }
                lock (_lock)
                {
                    _boundTo = holder;
                    _holdingSurface = true;
                }
                // Function pointers are per-process; everything GlEs holds --
                // buffers, textures, uniform locations -- belongs to a context.
                // This one outlives the surface, so the reset only belongs with
                // a *new* context, which is the first surface after one.
                if (Scene == null)
                {
                    EsBindings.Load();
                    GlEs.Reset();
                    // Something rather than whatever was in the buffer, for the
                    // seconds the room takes to load.
                    GL.ClearColor(new OpenTK.Mathematics.Color4(10 / 255f, 12 / 255f, 16 / 255f, 1f));
                    GL.Clear(OpenTK.Graphics.OpenGL.ClearBufferMask.ColorBufferBit);
                    EGL14.EglSwapBuffers(_display, _eglSurface);
                }
                _size = Vector2i.Zero;
                return true;
            }

            private void ReleaseSurface()
            {
                EGLSurface? surface;
                lock (_lock)
                {
                    surface = _eglSurface;
                    _eglSurface = null;
                    _boundTo = null;
                    _holdingSurface = false;
                    Monitor.PulseAll(_lock);
                }
                if (_display == null || surface == null)
                {
                    return;
                }
                try
                {
                    EGL14.EglMakeCurrent(_display, EGL14.EglNoSurface, EGL14.EglNoSurface,
                        EGL14.EglNoContext);
                    EGL14.EglDestroySurface(_display, surface);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[android] releasing the surface failed: {ex.Message}");
                }
            }

            private void DestroyContext()
            {
                if (_display == null)
                {
                    return;
                }
                try
                {
                    EGL14.EglMakeCurrent(_display, EGL14.EglNoSurface, EGL14.EglNoSurface,
                        EGL14.EglNoContext);
                    if (_context != null)
                    {
                        EGL14.EglDestroyContext(_display, _context);
                    }
                    EGL14.EglTerminate(_display);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[android] tearing the context down failed: {ex.Message}");
                }
                _context = null;
                _config = null;
                _display = null;
            }

            private bool Fail(string message)
            {
                Console.WriteLine($"[android] {message}");
                if (!_ended)
                {
                    _ended = true;
                    _onError(message);
                }
                lock (_lock)
                {
                    _stopping = true;
                }
                return false;
            }

            /// <summary>
            /// Load the room, on this thread. It takes seconds; nothing is
            /// waiting on it, which is the difference this class makes.
            /// </summary>
            private void BuildScene()
            {
                if (_size.X <= 0 || _size.Y <= 0)
                {
                    return;
                }
                try
                {
                    Scene = _build(_input, _size);
                    Scene.OnLoad();
                }
                catch (Exception ex)
                {
                    // A missing room, a shader the driver would not take, a set
                    // of game files that is not there. Any of them ends the
                    // match with what went wrong on screen, rather than taking
                    // the process down from a thread nobody is watching.
                    Console.WriteLine($"[android] the match could not start: {ex}");
                    Scene = null;
                    _ended = true;
                    _onError(ex.Message);
                    lock (_lock)
                    {
                        _stopping = true;
                    }
                    return;
                }
                _clock.Start();
                _nextFrame = _clock.Elapsed.TotalSeconds;
                _lastFrameStart = _nextFrame;
                FrameTiming.Reset();
                _onLoaded();
            }

            /// <summary>One frame. False means the match is over.</summary>
            ///
            /// <remarks>
            /// The same split the desktop window makes (see
            /// <c>RenderWindow.OnRenderFrame</c>): the simulation runs on a
            /// fixed 60 Hz accumulator whatever the picture is doing, and the
            /// picture runs at the player's FPS limit. This used to be one
            /// <c>OnUpdateFrame</c> paced at a hard 1/60, which is why a
            /// 120 Hz phone drew 60.
            ///
            /// Input is inside the step loop rather than beside it, because
            /// that is what it is: <see cref="ApplyInput"/> works out this
            /// step's rising edges, and running it per *picture* would give a
            /// tap on FIRE two presses on a 120 Hz screen.
            /// </remarks>
            private bool DrawFrame()
            {
                Scene scene = Scene!;
                double elapsed = WaitForTick();
                GameState.ApplyPause();
                int steps = FrameTiming.Advance(elapsed);
                for (int i = 0; i < steps; i++)
                {
                    ApplyInput();
                    scene.OnSimulationFrame();
                }
                RequestFrameRate();
                scene.OnDrawFrame();
                if (!scene.OnRenderFrame())
                {
                    End(scene);
                    return false;
                }
                scene.AfterRenderFrame();
                if (_display != null && _eglSurface != null
                    && !EGL14.EglSwapBuffers(_display, _eglSurface))
                {
                    // The framework took the surface back. Let go of it and
                    // wait for the next one rather than drawing into nothing.
                    Console.WriteLine("[android] the surface stopped accepting frames; "
                        + $"waiting for another (0x{EGL14.EglGetError():X})");
                    ReleaseSurface();
                }
                return true;
            }

            /// <summary>
            /// Tell the system what rate this surface intends to draw at, so a
            /// 120 Hz panel actually runs at 120.
            ///
            /// Drawing faster than the display is otherwise wasted work: a
            /// phone that can do 120 often sits at 60 until something asks,
            /// and SurfaceFlinger picks the mode from what its surfaces
            /// declare. Zero means "no preference", which is what the display
            /// setting wants -- let the system keep whatever it chose.
            ///
            /// API 30. Below that there is no way to ask from a surface, and
            /// the panel runs at whatever the framework decided; the FPS limit
            /// still caps the loop, it just cannot raise the display.
            /// Best-effort throughout: a device that refuses is not a reason
            /// to end a match, and this is only ever an optimisation.
            /// </summary>
            private void RequestFrameRate()
            {
                int cap = FrameTiming.FrameRateCap;
                if (cap == _requestedFrameRate || !OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    return;
                }
                _requestedFrameRate = cap;
                Surface? window;
                lock (_lock)
                {
                    window = _boundTo?.Surface;
                }
                if (window == null || !window.IsValid)
                {
                    // Ask again when there is a surface to ask about.
                    _requestedFrameRate = -1;
                    return;
                }
                try
                {
                    window.SetFrameRate(
                        cap == FrameTiming.DisplayRate ? 0f : cap,
                        (int)SurfaceFrameRateCompatibility.Default);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[android] the display would not be asked for "
                        + $"{cap} fps: {ex.Message}");
                }
            }

            private void End(Scene scene)
            {
                _ended = true;
                scene.DoCleanup();
                Scene = null;
                // Whatever the session asked to have saved, before anything
                // else can run and before the front screen comes back. This is
                // the desktop's line after its render loop returns; nothing is
                // written unless the match was the story, so every other kind
                // of match passes straight through.
                try
                {
                    AndroidMatch.Finish();
                }
                catch (Exception ex)
                {
                    // A save that cannot be written is not a reason to leave
                    // the player on a dead view.
                    Console.WriteLine($"[android] the save could not be written: {ex}");
                }
                _onEnd();
            }

            /// <summary>
            /// Hold until the next picture is due, and answer how long the
            /// last one actually took -- which is what the simulation's
            /// accumulator is owed.
            /// </summary>
            private double WaitForTick()
            {
                double now = _clock.Elapsed.TotalSeconds;
                int cap = FrameTiming.FrameRateCap;
                double interval = cap == FrameTiming.DisplayRate
                    ? MinFrameSeconds
                    : Math.Max(MinFrameSeconds, 1.0 / cap);
                double wait = _nextFrame - now;
                if (wait > 0.001)
                {
                    Thread.Sleep((int)(wait * 1000));
                    now = _clock.Elapsed.TotalSeconds;
                }
                _nextFrame += interval;
                if (_nextFrame < now)
                {
                    // A stall (a load, a garbage collection, the app coming
                    // back) must not leave the game owing frames it would then
                    // run flat out to catch up on. The simulation's own debt is
                    // handled separately and properly, by FrameTiming.
                    _nextFrame = now + interval;
                }
                double elapsed = now - _lastFrameStart;
                _lastFrameStart = now;
                return elapsed;
            }

            /// <summary>
            /// One frame of touch, turned into key and button presses.
            ///
            /// The presses are collected and committed as a set rather than
            /// written one action at a time, because actions share binds --
            /// see <see cref="AndroidInput.Apply"/> for the FIRE/ALT bug that
            /// came of writing them through.
            /// </summary>
            private void ApplyInput()
            {
                // Before the check below, and before the spectator's early
                // return in CollectInput: somebody watching a match is in the
                // position where talking to the people playing is most of what
                // there is to do, and a player whose entity is not active yet
                // has still asked for the keyboard if they pressed CHAT.
                HandleChat();
                // The one thing a pad cannot do. A dialog's OK button is
                // pressed by *position* -- PlayerDialog.CheckButtonPressed
                // reads Input.ClickX/Y, because on the DS it was a touch
                // screen -- and GamepadInput deliberately drives no pointer.
                // So the touch controls stay on screen through one whether or
                // not a pad is in the player's hands, or the story stops at
                // the first scan with nothing to press.
                _controls.ForceVisible = GameState.DialogPause;
                PlayerEntity main = PlayerEntity.Main;
                if (main == null || !main.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    return;
                }
                _input.BeginFrame();
                try
                {
                    CollectInput(main);
                }
                finally
                {
                    _input.CommitFrame();
                }
            }

            /// <summary>
            /// One frame of a spectator's screen.
            ///
            /// Nothing here goes near the player: <c>PlayerEntity.ProcessInput</c>
            /// skips the local player's input entirely while spectating, and
            /// the hunter the camera is on is somebody else's. What is left is
            /// four things -- who to watch, which camera, the scoreboard, and,
            /// on the free camera, driving it.
            ///
            /// The free camera is the room viewer's Roam camera (see
            /// <c>Scene.SetFreeCamera</c>), and it is not driven through binds
            /// at all: <c>Scene.OnKeyHeld</c> reads W/A/S/D, Space and V off
            /// the keyboard and <c>Scene.OnMouseMove</c> turns it. This head
            /// already owns a keyboard nobody is holding, so the stick presses
            /// those keys and the aim drag is handed to the same method the
            /// desktop's mouse move calls. The letters are a copy of that
            /// method's own table and the one place they could go out of step
            /// with it.
            /// </summary>
            private void Spectate()
            {
                bool freeCamera = Mods.SpectatorMode.FreeCamera;
                _controls.SetSpectator(spectating: true, freeCamera);
                // NEXT: the desktop's left click.
                bool cycle = _controls.IsHeld(TouchAction.Shoot);
                if (cycle && !_spectateCycleHeld)
                {
                    Mods.SpectatorMode.CycleNext();
                }
                _spectateCycleHeld = cycle;
                // VIEW: the desktop's Space -- the map, or the player being
                // watched. Without it the free camera was a one-way trip on
                // this head: NEXT leaves it and nothing brought it back.
                bool view = _controls.IsHeld(TouchAction.ScanVisor);
                if (view && !_spectateViewHeld)
                {
                    Mods.SpectatorMode.ToggleView();
                }
                _spectateViewHeld = view;
                bool menuHeld = _controls.IsHeld(TouchAction.Pause);
                if (menuHeld && !_menuWasHeld)
                {
                    _onPauseMenu();
                }
                _menuWasHeld = menuHeld;
                // The one control a spectator keeps. Read off the keyboard
                // against the binding itself rather than through a player --
                // see PlayerInput.ProcessInput and SpectatorMode.NoteScoreboard
                // -- so the bind is what has to be pressed here, and the
                // watched player's own controls would be the wrong set.
                _input.Apply(Mods.InputSettings.Current.Pause,
                    _controls.IsHeld(TouchAction.Scoreboard));
                if (freeCamera)
                {
                    TouchControls.Dir dir = _controls.Direction;
                    _input.ApplyKey(Keys.W, (dir & TouchControls.Dir.Up) != 0);
                    _input.ApplyKey(Keys.S, (dir & TouchControls.Dir.Down) != 0);
                    _input.ApplyKey(Keys.A, (dir & TouchControls.Dir.Left) != 0);
                    _input.ApplyKey(Keys.D, (dir & TouchControls.Dir.Right) != 0);
                    _input.ApplyKey(Keys.Space, _controls.IsHeld(TouchAction.Jump));
                    _input.ApplyKey(Keys.V, _controls.IsHeld(TouchAction.Morph));
                    (float X, float Y) look = _controls.TakeAimDelta();
                    if (look.X != 0 || look.Y != 0)
                    {
                        // The same call the desktop's mouse move makes, in the
                        // same units: OnMouseMove applies the player's own
                        // sensitivity and inversion, so the free camera turns
                        // the way their game does.
                        Scene?.OnMouseMove(look.X * AimScale, look.Y * AimScale);
                    }
                }
                else
                {
                    // Riding along behind somebody's eyes: their view, not
                    // ours. Taken rather than left, so it cannot arrive as a
                    // lurch on the frame the free camera comes back.
                    _controls.TakeAimDelta();
                }
                _controls.TakeSwipeBoost();
                _controls.TakeDoubleTapJump();
                // Nothing to scan or boost from here, and FIRE has to be the
                // button that cycles players rather than a SCAN left over from
                // whatever the visor was doing.
                _controls.ScanVisorActive = false;
                _controls.SwipeBoostEnabled = false;
            }

            /// <summary>
            /// The CHAT button, and the soft keyboard that has to follow it.
            ///
            /// A phone has no T to press, so the button is the whole of how
            /// chat is opened without a keyboard attached -- and asking for
            /// the keyboard is the other half, since an open prompt with
            /// nothing to type on is worse than no prompt.
            /// </summary>
            private void HandleChat()
            {
                // Offline there is nobody to read a line, and in the story
                // chat does not exist at all; the button goes away rather than
                // sitting there doing nothing.
                _controls.ChatEnabled = MphRead.Mods.Chat.ChatBox.Available
                    && MphRead.Mods.Network.NetSession.Active;
                // Start, on a pad, is the MENU button. Same call, same
                // reason it is a request rather than a call: the menu is a
                // view swap on the UI thread and this is the GL one.
                if (MphRead.Mods.Input.GamepadInput.TakeMenuPress())
                {
                    _onPauseMenu();
                }
                bool chat = _controls.IsHeld(TouchAction.Chat);
                if (chat && !_chatWasHeld)
                {
                    // Not a key, so nothing is about to arrive as a character.
                    MphRead.Mods.Chat.ChatBox.Open(swallowOpeningChar: false);
                }
                _chatWasHeld = chat;
                bool wanted = MphRead.Mods.Chat.ChatBox.Composing;
                if (wanted != _keyboardShown)
                {
                    _keyboardShown = wanted;
                    _onSoftKeyboard(wanted);
                }
            }

            private void CollectInput(PlayerEntity main)
            {
                // The results screen owns the glass while it is up: the
                // hunter picker is the one thing on it that does anything,
                // and it was being covered by a dozen buttons that did
                // nothing. See TouchControls.SetEndScreen.
                bool endScreen = Mods.EndScreen.Available;
                _controls.SetEndScreen(endScreen);
                // And the vote's two buttons, while there are any, so a tap
                // that lands on one answers the vote instead of turning the
                // camera.
                _controls.SetTapTargets(Mods.Network.MapVote.TouchTargets());
                (bool got, float tapX, float tapY) = _controls.TakeTap();
                if (got)
                {
                    // Exactly what the desktop's left button does, in the
                    // order it does it: the pointer first, because both of
                    // these test against where it is.
                    Mods.EndScreen.NotePointer(tapX, tapY);
                    if (!Mods.Network.MapVote.HandleClick())
                    {
                        Mods.EndScreen.HandleClick();
                    }
                }
                if (Mods.SpectatorMode.IsSpectating)
                {
                    Spectate();
                    return;
                }
                _controls.SetSpectator(spectating: false, freeCamera: false);
                _spectateCycleHeld = false;
                _spectateViewHeld = false;
                PlayerControls controls = main.Controls;
                TouchControls.Dir dir = _controls.Direction;
                bool up = (dir & TouchControls.Dir.Up) != 0;
                bool down = (dir & TouchControls.Dir.Down) != 0;
                bool left = (dir & TouchControls.Dir.Left) != 0;
                bool right = (dir & TouchControls.Dir.Right) != 0;
                // Both sets: walking reads Move and the morph ball reads Roll,
                // and a player who has bound them to different keys expects
                // the stick to drive whichever form they are in.
                _input.Apply(controls.MoveUp, up);
                _input.Apply(controls.MoveDown, down);
                _input.Apply(controls.MoveLeft, left);
                _input.Apply(controls.MoveRight, right);
                _input.Apply(controls.RollUp, up);
                _input.Apply(controls.RollDown, down);
                _input.Apply(controls.RolltLeft, left);
                _input.Apply(controls.RollRight, right);

                // JUMP, or two quick taps on the aiming side, which is how the
                // DS jumped with a stylus in hand.
                //
                // Taken every frame whether it is wanted or not, so it cannot
                // go stale and jump later. It is not wanted while a dialog or
                // the wheel is up: both turn the screen into a thing to press,
                // and pressing OK twice, or two weapons in a row, is not a
                // request to jump.
                bool doubleTap = _controls.TakeDoubleTapJump();
                bool jump = _controls.IsHeld(TouchAction.Jump)
                    || (doubleTap && !GameState.DialogPause
                        && !_controls.IsHeld(TouchAction.WeaponMenu));
                // FIRE is the only attack button, and it is both attacks.
                //
                // There used to be an ALT button beside it, which is what the
                // DS did not have: one fire button served the gun on foot and
                // the alt form's attack in the ball, and the game's own
                // defaults still say so -- shoot and altAttack are both
                // MouseButton.Left. A second button for the same bind bought
                // nothing and cost the first one (see AndroidInput.Apply), so
                // it is gone and FIRE presses whichever of the two the form
                // the player is actually in will read. Both while morphing, so
                // a thumb already down on FIRE as the ball closes is not
                // dropped on the frame the form changes.
                bool fire = _controls.IsHeld(TouchAction.Shoot);
                bool altForm = main.IsAltForm || _controls.IsHeld(TouchAction.Morph);
                _input.Apply(controls.Shoot, fire && !main.IsAltForm);
                _input.Apply(controls.AltAttack, fire && altForm);
                _input.Apply(controls.Jump, jump);
                // One button on the DS, and the same key here by default:
                // jumping on foot is boosting in the ball.
                _input.Apply(controls.Boost, jump);
                // A quick flick on the aim side also boosts, the way a stylus
                // flick did on the DS -- see PlayerInput's boost handling for
                // how this one-shot is consumed. Only the ball boosts, and
                // telling the controls that is what keeps a fast turn on foot
                // from being read as a flick.
                _controls.SwipeBoostEnabled = main.IsAltForm;
                (bool Fired, float X, float Y) swipe = _controls.TakeSwipeBoost();
                if (swipe.Fired && main.IsAltForm)
                {
                    main.SwipeBoostRequested = true;
                    // Which way the thumb went, for the boost to follow. The
                    // engine turns it into a world direction; here it is still
                    // just the screen's.
                    main.SwipeBoostX = swipe.X;
                    main.SwipeBoostY = swipe.Y;
                }
                _input.Apply(controls.Morph, _controls.IsHeld(TouchAction.Morph));
                // Two binds, two buttons, exactly as the desktop has them:
                // VISOR opens and closes the scan visor (E) and SCAN reads
                // what is targeted while it is held (Q). One button trying to
                // be both could not tell "read this again" from "put the
                // visor away", and answered a press with both.
                //
                // SCAN is only there while the visor is: it takes FIRE's
                // place, which is idle in the visor anyway. See
                // TouchControls.ScanVisorActive.
                _controls.ScanVisorActive = main.ScanVisor;
                _input.Apply(controls.ScanVisor, _controls.IsHeld(TouchAction.ScanVisor));
                _input.Apply(controls.Scan, _controls.IsHeld(TouchAction.Scan));
                _input.Apply(controls.Zoom, _controls.IsHeld(TouchAction.Zoom));
                // MSSL swaps to the Missile and back to the Power Beam, since
                // neither is on the wheel and a thumb has no number row. The
                // press decides which of the two binds to hold for the frame;
                // both are read as presses (PlayerInput.ProcessTouchInput), so
                // one frame is a switch.
                bool missile = _controls.IsHeld(TouchAction.Missile);
                if (missile && !_missileWasHeld)
                {
                    _input.Apply(main.CurrentWeapon == BeamType.Missile
                        ? controls.PowerBeam : controls.Missile, true);
                }
                _missileWasHeld = missile;
                // The DS pause button -- map and status on foot, scoreboard
                // while it is held in a match -- is SCORE now. MENU is the
                // app's own menu, which is the thing a player looks for first
                // and had no way to reach at all.
                _input.Apply(controls.Pause, _controls.IsHeld(TouchAction.Scoreboard));
                bool menu = _controls.IsHeld(TouchAction.Pause);
                if (menu && !_menuWasHeld)
                {
                    // On the press, not the release, and once per press: the
                    // menu is a view swap on the UI thread and this is the GL
                    // thread, so it is asked for rather than done here.
                    _onPauseMenu();
                }
                _menuWasHeld = menu;

                // A dialog box waiting to be dismissed reads a click position
                // and nothing else: PlayerDialog.CheckButtonPressed compares
                // Input.ClickX/Y against the button rectangle, and on this
                // platform nothing ever set them. The pointer was only ever
                // *moved*, for aiming, and no touch ever pressed the left
                // mouse button -- so the OK button could not be pressed at all,
                // and a scan or a prompt could only be left by quitting.
                //
                // While one is up, the screen is the DS's touch screen: a
                // finger is a position and holding it is holding the button.
                if (GameState.DialogPause)
                {
                    _controls.PointerIsAbsolute = true;
                    (bool Down, float X, float Y) tap = _controls.AimPosition();
                    if (tap.Down)
                    {
                        _input.PlacePointer(tap.X, tap.Y);
                    }
                    _input.ApplyButton(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left, tap.Down);
                    _dialogClickDown = tap.Down;
                    // Swallowed, so the aim does not lurch by however far the
                    // finger travelled once the box is gone.
                    _controls.TakeAimDelta();
                    return;
                }
                // The weapon wheel is the other part that reads a position off
                // what used to be a touch screen, so while it is open the
                // screen is one -- the left half stops being a thumbstick.
                //
                // Without that, four of the six weapons could not be picked at
                // all. PlayerHud lays the wheel out from x = 0.30 to x = 0.785
                // of the width, so most of it is in the half where a finger
                // becomes the stick instead of the pointer, and a tap there
                // was a movement input the wheel never saw: the menu opened,
                // the icons appeared, and choosing one did nothing but play
                // the "cannot switch" sound.
                _controls.PointerIsAbsolute = _controls.IsHeld(TouchAction.WeaponMenu);
                if (_dialogClickDown)
                {
                    // Released explicitly rather than left to the next tap:
                    // the box can close on the same frame the finger is still
                    // down, and a mouse button stuck down outlives the dialog.
                    // Nothing to do but stop asking for it: CommitFrame
                    // releases every button no action asked for this frame.
                    _dialogClickDown = false;
                }
                bool weaponMenu = _controls.IsHeld(TouchAction.WeaponMenu);
                _input.Apply(controls.WeaponMenu, weaponMenu);
                if (weaponMenu)
                {
                    // The wheel is a touchscreen mechanic: it reads where the
                    // pointer is, not how far it moved. While it is open the
                    // pointer is the finger -- the one holding WEAPON, or an
                    // aiming finger if one is down. See WeaponWheelPosition.
                    (bool Down, float X, float Y) aim = _controls.WeaponWheelPosition();
                    if (aim.Down)
                    {
                        _input.PlacePointer(aim.X, aim.Y);
                    }
                    _controls.TakeAimDelta();
                }
                else
                {
                    (float X, float Y) delta = _controls.TakeAimDelta();
                    _input.MovePointer(delta.X * AimScale, delta.Y * AimScale);
                }
            }
        }
    }
}
