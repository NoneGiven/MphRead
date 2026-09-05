using System;

namespace MphRead.Mods.Render
{
    /// <summary>
    /// The clock that lets the picture run faster than the game.
    ///
    /// Every timer in this engine is counted in frames, not in seconds: the
    /// DS ran its logic at 30 Hz and upstream doubled each interval and halved
    /// each increment to reach 60, which is what the 800-odd
    /// <c>// todo: FPS stuff</c> markers are. The simulation therefore cannot
    /// be asked to run at any other rate without rewriting all of them, and
    /// rewriting them would move the wire format too, since an intent is sent
    /// per frame and a demo is a count of frames.
    ///
    /// So the simulation is not asked. It stays pinned at exactly 60 Hz here
    /// and the *drawing* is what runs at the display's rate, with
    /// <see cref="Alpha"/> saying how far between the last two simulated
    /// states the frame being drawn falls. A machine holding 144 fps runs the
    /// same 60 simulation steps a second it always did, sends the same
    /// packets on the same frames, and records a demo another build can play
    /// back.
    ///
    /// One property is worth stating plainly because it is a change, and an
    /// improvement: the game's speed no longer depends on whether the machine
    /// can keep up. Update and render used to be one call, so a box managing
    /// 40 fps played the game in slow motion. The accumulator here runs the
    /// steps that are owed regardless of how long the frame took, up to
    /// <see cref="MaxCatchUpSteps"/>.
    /// </summary>
    public static class FrameTiming
    {
        /// <summary>The rate the simulation runs at, and the only one it can.</summary>
        public const int SimulationHz = 60;

        public const double StepSeconds = 1.0 / SimulationHz;

        /// <summary>
        /// The most simulation steps one drawn frame may run.
        ///
        /// Without a ceiling, a two-second stall -- a room finishing loading,
        /// a window being dragged, a debugger stopping the process -- comes
        /// back owing 120 steps, runs them all in one frame, takes longer than
        /// a frame doing it, and owes more than it did before. That is the
        /// spiral every fixed-timestep loop has to be told not to enter. The
        /// steps past the ceiling are dropped, which is exactly what the old
        /// single-rate loop did with every frame it missed.
        /// </summary>
        public const int MaxCatchUpSteps = 5;

        /// <summary>
        /// Anything longer than this is a stall, not a slow frame: the
        /// accumulator is reset rather than paid off.
        /// </summary>
        private const double StallSeconds = 0.25;

        /// <summary>
        /// Frames per second to draw at. 0 is <see cref="DisplayRate"/>: no
        /// cap of our own, VSync on, so the rate is whatever the monitor
        /// refreshes at. Any other value caps there with VSync off.
        /// </summary>
        public static int FrameRateCap
        {
            get => _frameRateCap;
            set => _frameRateCap = value <= 0 ? DisplayRate : Math.Clamp(value, MinCap, MaxCap);
        }

        private static int _frameRateCap = DisplayRate;

        public const int DisplayRate = 0;
        public const int MinCap = 30;

        /// <summary>
        /// OpenTK clamps a render frequency above 500 to 500; past that the
        /// number is decoration anyway, since the simulation is the thing that
        /// decides what the game does and it is not moving.
        /// </summary>
        public const int MaxCap = 500;

        /// <summary>
        /// Draw entities between their last two simulated states instead of
        /// at the newest one.
        ///
        /// Off, a 144 Hz picture of a 60 Hz simulation is not smoother than
        /// the 60 Hz one -- it is the same 60 distinct positions a second,
        /// some of them shown twice, which reads as judder rather than as
        /// motion. Interpolation is what actually turns the extra frames into
        /// something the eye gets anything from. It costs one frame of
        /// latency on what is *drawn*; it costs none on what is simulated,
        /// which is what the shot you fire is resolved against.
        /// </summary>
        public static bool Interpolate { get; set; } = true;

        /// <summary>
        /// How far between the previous simulated state and the current one
        /// the frame being drawn falls, 0 to 1. Always 1 -- draw the newest
        /// state, as the engine always did -- when interpolation is off.
        /// </summary>
        public static float Alpha => !Interpolate ? 1f : (ForcedAlpha ?? _alpha);

        private static float _alpha = 1f;

        /// <summary>
        /// Drive <see cref="Alpha"/> directly instead of from the clock.
        ///
        /// For the harness only. A wall-clock accumulator on a build machine
        /// produces whatever alphas that machine's load happens to produce,
        /// which is neither reproducible nor a sweep of the range;
        /// <c>-maptest -drawrate N</c> sets this to each of 1/N .. 1 in turn
        /// so a run actually visits the whole of it and does so identically
        /// every time.
        /// </summary>
        public static float? ForcedAlpha { get; set; }

        /// <summary>
        /// True while the loop is running the picture at a rate of its own.
        /// The harness clients drive <c>Scene.OnUpdateFrame</c> one step per
        /// frame and never come through here, so their timing is untouched
        /// whatever this says.
        /// </summary>
        public static bool Active { get; private set; }

        private static double _accumulator;

        /// <summary>Steps run for the frame <see cref="Advance"/> last answered.</summary>
        public static int StepsThisFrame { get; private set; }

        #region diagnostics

        // What the debug log reports. Cheap enough to keep always on: the
        // question these answer -- "is the simulation actually still running
        // at 60 while the picture runs at 144" -- is the whole point of the
        // split, and it cannot be asked after the fact.
        public static long TotalSteps { get; private set; }
        public static long TotalFrames { get; private set; }
        public static long DroppedSteps { get; private set; }
        public static long Stalls { get; private set; }

        /// <summary>
        /// How many frames ran 0, 1, 2, 3, 4 or 5+ simulation steps. A healthy
        /// 144 Hz run is mostly 0s and 1s in roughly 84/60 proportion; a run
        /// with 2s and 3s in it is a machine that is not keeping up.
        /// </summary>
        public static readonly long[] StepHistogram = new long[MaxCatchUpSteps + 1];

        public static double MeasuredSimulationHz { get; private set; }
        public static double MeasuredFrameHz { get; private set; }

        private static double _windowSeconds;
        private static long _windowSteps;
        private static long _windowFrames;

        public static void ResetDiagnostics()
        {
            TotalSteps = 0;
            TotalFrames = 0;
            DroppedSteps = 0;
            Stalls = 0;
            Array.Clear(StepHistogram);
            MeasuredSimulationHz = 0;
            MeasuredFrameHz = 0;
            _windowSeconds = 0;
            _windowSteps = 0;
            _windowFrames = 0;
        }

        public static string Describe()
        {
            return $"sim {MeasuredSimulationHz:0.00} Hz / draw {MeasuredFrameHz:0.0} Hz, "
                + $"{TotalSteps} steps over {TotalFrames} frames, "
                + $"{DroppedSteps} dropped, {Stalls} stalls, "
                + $"steps per frame [{string.Join(", ", StepHistogram)}], "
                + $"cap {(FrameRateCap == DisplayRate ? "display" : FrameRateCap.ToString())}, "
                + $"interpolation {(Interpolate ? "on" : "off")}";
        }

        #endregion

        public static void Reset()
        {
            _accumulator = 0;
            _alpha = 1f;
            StepsThisFrame = 0;
            Active = false;
        }

        /// <summary>
        /// Take the wall-clock time one drawn frame took and answer how many
        /// simulation steps are owed before it is drawn, leaving
        /// <see cref="Alpha"/> set for the drawing.
        /// </summary>
        public static int Advance(double elapsedSeconds)
        {
            Active = true;
            TotalFrames++;
            if (elapsedSeconds > StallSeconds || elapsedSeconds < 0 || double.IsNaN(elapsedSeconds))
            {
                // A stall is not a debt. Run one step so the game does not
                // stop dead, and start the accumulator over.
                Stalls++;
                _accumulator = 0;
                _alpha = 1f;
                StepsThisFrame = 1;
                TotalSteps++;
                StepHistogram[1]++;
                Tally(StepSeconds, 1);
                return 1;
            }
            _accumulator += elapsedSeconds;
            int steps = 0;
            while (_accumulator >= StepSeconds && steps < MaxCatchUpSteps)
            {
                _accumulator -= StepSeconds;
                steps++;
            }
            if (_accumulator >= StepSeconds)
            {
                // Past the ceiling: throw the rest away rather than owe it.
                int owed = (int)(_accumulator / StepSeconds);
                DroppedSteps += owed;
                _accumulator -= owed * StepSeconds;
            }
            _alpha = (float)(_accumulator / StepSeconds);
            if (_alpha < 0)
            {
                _alpha = 0;
            }
            else if (_alpha > 1)
            {
                _alpha = 1;
            }
            StepsThisFrame = steps;
            TotalSteps += steps;
            StepHistogram[steps]++;
            Tally(elapsedSeconds, steps);
            return steps;
        }

        private static void Tally(double elapsedSeconds, int steps)
        {
            _windowSeconds += elapsedSeconds;
            _windowSteps += steps;
            _windowFrames++;
            // Two seconds, not one. The simulation is a discrete 60 Hz
            // process and the window boundary does not land on a step, so a
            // one-second window reads 60 or 61 steps and alternates between
            // 59.8 and 60.7 Hz -- 1.7% of quantisation, which is larger than
            // any drift worth reporting and made the log cry wolf every other
            // window. Two seconds halves it.
            if (_windowSeconds >= 2.0)
            {
                MeasuredSimulationHz = _windowSteps / _windowSeconds;
                MeasuredFrameHz = _windowFrames / _windowSeconds;
                _windowSeconds = 0;
                _windowSteps = 0;
                _windowFrames = 0;
                ReportWindow();
            }
        }

        private static int _windowsSinceReport;
        private static long _reportedDrops;
        private static long _reportedStalls;

        /// <summary>
        /// What the debug log gets, and when.
        ///
        /// "It crashes when the map loads" is answered by a log of everything;
        /// "the game runs slightly fast on my machine" is answered by this and
        /// nothing else -- the simulation rate cannot be measured after the
        /// fact and a player cannot see it at all, since the counter on the
        /// HUD reports the picture. A quiet run says so every five seconds; a
        /// run that dropped a step or hit a stall says so the moment it does.
        /// </summary>
        private static void ReportWindow()
        {
            bool trouble = DroppedSteps != _reportedDrops || Stalls != _reportedStalls
                || Math.Abs(MeasuredSimulationHz - SimulationHz) > SimulationHz * 0.02;
            _windowsSinceReport++;
            if (!trouble && _windowsSinceReport < 5) // ten seconds, at two each
            {
                return;
            }
            _windowsSinceReport = 0;
            _reportedDrops = DroppedSteps;
            _reportedStalls = Stalls;
            DebugLog.Line(trouble ? "frametiming!" : "frametiming", Describe());
        }

        public static int ParseCap(string? value, int fallback)
        {
            if (value == null)
            {
                return fallback;
            }
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return fallback;
            }
            if (trimmed.Equals("display", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("vsync", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || trimmed == "0")
            {
                return DisplayRate;
            }
            if (trimmed.Equals("uncapped", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
            {
                return MaxCap;
            }
            if (Int32.TryParse(trimmed, out int parsed))
            {
                return parsed <= 0 ? DisplayRate : Math.Clamp(parsed, MinCap, MaxCap);
            }
            return fallback;
        }

        public static string CapString(int cap)
        {
            return cap == DisplayRate ? "display" : cap.ToString();
        }
    }
}
