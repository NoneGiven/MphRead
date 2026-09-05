using System;

namespace MphRead.Mods.Render
{
    /// <summary>
    /// What <c>-frametimingcheck</c> runs: the accumulator on its own, against
    /// frame times chosen rather than measured.
    ///
    /// The half of the decoupling that can silently be wrong is arithmetic --
    /// a game that runs at 60.4 Hz instead of 60 loses a second every two and
    /// a half minutes, which is not visible in a screenshot and is fatal to a
    /// match. It needs no room, no window and no display, so it is checked
    /// here; <c>-maptest -drawrate N</c> checks the other half, that drawing
    /// more often does not change what the world does.
    /// </summary>
    public static class FrameTimingCheck
    {
        private sealed class Case
        {
            public string Name = "";
            public double Seconds;
            public Func<int, double> FrameTime = null!;
            public double ExpectedStepsPerSecond = FrameTiming.SimulationHz;
            public double TolerancePercent = 0.5;
            public int MaxStepsInOneFrame = FrameTiming.MaxCatchUpSteps;
        }

        public static int Run()
        {
            var rng = new Random(20260905);
            Case[] cases = new[]
            {
                new Case
                {
                    // The old behaviour, and the one that must not move.
                    Name = "60 Hz display",
                    Seconds = 120,
                    FrameTime = _ => 1 / 60.0,
                    MaxStepsInOneFrame = 1
                },
                new Case
                {
                    Name = "144 Hz display",
                    Seconds = 120,
                    FrameTime = _ => 1 / 144.0,
                    MaxStepsInOneFrame = 1
                },
                new Case
                {
                    Name = "240 Hz display",
                    Seconds = 120,
                    FrameTime = _ => 1 / 240.0,
                    MaxStepsInOneFrame = 1
                },
                new Case
                {
                    Name = "165 Hz display (not a multiple of 60)",
                    Seconds = 300,
                    FrameTime = _ => 1 / 165.0,
                    MaxStepsInOneFrame = 1
                },
                new Case
                {
                    // A machine that cannot keep 60. The old loop ran the game
                    // in slow motion here; this must still deliver 60 steps a
                    // second of game for every second of wall clock.
                    Name = "40 Hz, a machine that cannot keep up",
                    Seconds = 120,
                    FrameTime = _ => 1 / 40.0,
                    MaxStepsInOneFrame = 2
                },
                new Case
                {
                    Name = "jittery 144 Hz",
                    Seconds = 300,
                    FrameTime = _ => 1 / 144.0 * (0.4 + rng.NextDouble() * 1.2),
                    // Jitter is symmetric, so the mean holds; allow a little
                    // more for the tail of one 300-second sample.
                    TolerancePercent = 1.0,
                    MaxStepsInOneFrame = 3
                },
                new Case
                {
                    Name = "vsync flipping between 144 and 72",
                    Seconds = 300,
                    FrameTime = i => (i % 7 == 0 ? 2 : 1) / 144.0,
                    MaxStepsInOneFrame = 2
                }
            };

            int failures = 0;
            foreach (Case test in cases)
            {
                failures += RunCase(test) ? 0 : 1;
            }
            failures += RunStallCase() ? 0 : 1;
            failures += RunAlphaCase() ? 0 : 1;
            Console.WriteLine(failures == 0
                ? "FRAMETIMING all cases pass"
                : $"FRAMETIMING {failures} case(s) FAILED");
            return failures;
        }

        private static bool RunCase(Case test)
        {
            FrameTiming.Reset();
            FrameTiming.ResetDiagnostics();
            double elapsed = 0;
            long steps = 0;
            int worstFrame = 0;
            int frame = 0;
            bool alphaOk = true;
            while (elapsed < test.Seconds)
            {
                double dt = test.FrameTime(frame++);
                elapsed += dt;
                int taken = FrameTiming.Advance(dt);
                steps += taken;
                if (taken > worstFrame)
                {
                    worstFrame = taken;
                }
                float alpha = FrameTiming.Alpha;
                if (alpha < 0 || alpha > 1 || Single.IsNaN(alpha))
                {
                    alphaOk = false;
                }
            }
            double rate = steps / elapsed;
            double drift = Math.Abs(rate - test.ExpectedStepsPerSecond)
                / test.ExpectedStepsPerSecond * 100;
            bool ok = alphaOk
                && drift <= test.TolerancePercent
                && worstFrame <= test.MaxStepsInOneFrame
                && FrameTiming.DroppedSteps == 0;
            // Seconds of game per second of wall clock, which is the number a
            // player would feel: 1.00 is right, 0.97 is a match clock that
            // loses two minutes an hour.
            double gameSeconds = steps * FrameTiming.StepSeconds;
            Console.WriteLine($"FRAMETIMING {(ok ? "ok  " : "FAIL")} {test.Name}"
                + $" | {frame} frames over {elapsed:0.0} s"
                + $" | {steps} steps = {rate:0.000} Hz (drift {drift:0.000}%)"
                + $" | game ran {gameSeconds / elapsed:0.0000}x real time"
                + $" | worst frame {worstFrame} step(s)"
                + $" | dropped {FrameTiming.DroppedSteps}"
                + (alphaOk ? "" : " | ALPHA OUT OF RANGE"));
            return ok;
        }

        /// <summary>
        /// A two-second stall -- a room finishing loading, a window being
        /// dragged, a debugger -- must come back with one step, not a hundred
        /// and twenty crammed into one frame.
        /// </summary>
        private static bool RunStallCase()
        {
            FrameTiming.Reset();
            FrameTiming.ResetDiagnostics();
            int worst = 0;
            for (int i = 0; i < 600; i++)
            {
                worst = Math.Max(worst, FrameTiming.Advance(1 / 144.0));
            }
            int afterStall = FrameTiming.Advance(2.0);
            for (int i = 0; i < 600; i++)
            {
                worst = Math.Max(worst, FrameTiming.Advance(1 / 144.0));
            }
            bool ok = afterStall == 1 && worst <= 1 && FrameTiming.Stalls == 1;
            Console.WriteLine($"FRAMETIMING {(ok ? "ok  " : "FAIL")} 2 s stall"
                + $" | {afterStall} step(s) on the stalled frame"
                + $" | {FrameTiming.Stalls} stall(s) seen"
                + $" | worst ordinary frame {worst} step(s)");
            return ok;
        }

        /// <summary>
        /// Alpha has to sweep the range on a display that is not a multiple of
        /// 60, and has to sit at a repeating pattern on one that is. Both are
        /// correct; a value stuck at 0 or 1 is not, and would mean the extra
        /// frames are duplicates.
        /// </summary>
        private static bool RunAlphaCase()
        {
            FrameTiming.Reset();
            FrameTiming.ResetDiagnostics();
            var buckets = new int[10];
            for (int i = 0; i < 144 * 20; i++)
            {
                FrameTiming.Advance(1 / 144.0);
                int bucket = Math.Clamp((int)(FrameTiming.Alpha * 10), 0, 9);
                buckets[bucket]++;
            }
            int occupied = 0;
            foreach (int count in buckets)
            {
                if (count > 0)
                {
                    occupied++;
                }
            }
            bool ok = occupied >= 8;
            Console.WriteLine($"FRAMETIMING {(ok ? "ok  " : "FAIL")} alpha coverage at 144 Hz"
                + $" | {occupied}/10 tenths visited"
                + $" | [{String.Join(", ", buckets)}]");
            return ok;
        }
    }
}
