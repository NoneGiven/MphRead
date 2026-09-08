using System;

namespace MphRead.Mods.Input
{
    /// <summary>
    /// Aiming with something that is not a mouse: a pen tablet, a stylus, a
    /// drawing display, a trackpad in absolute mode.
    ///
    /// The game turns the view by integrating pointer movement, which is what
    /// a mouse produces: a stream of small deltas, none of them large,
    /// because a mouse cannot leave the desk and reappear somewhere else. A
    /// pen can, and does, constantly -- it is lifted at the edge of the tablet
    /// and put back down in the middle, which the operating system reports as
    /// one enormous movement. Integrated, that is the view spinning
    /// completely round every time the hand is repositioned, which is why a
    /// tablet is unusable here rather than merely different.
    ///
    /// Nothing in this needs to know what device it is talking to, and that
    /// is deliberate. GLFW reports a pen as a mouse on all three desktop
    /// platforms, because the operating system synthesises mouse input from
    /// it -- so a pen already arrives, correctly, as pointer positions. What
    /// does not arrive is the distinction, and asking for it means a native
    /// tablet backend per platform. The jump is the part that actually breaks
    /// aiming, and a jump can be recognised without knowing what made it.
    ///
    /// What this does not do: pressure, tilt, barrel buttons, or eraser --
    /// none of which is reachable without those native backends
    /// (WM_POINTER/PT_PEN on Windows, NSEvent tablet events on macOS,
    /// zwp_tablet_v2 or XInput2 on Linux), and none of which an FPS has a use
    /// for beyond aiming.
    /// </summary>
    public static class PointerInput
    {
        /// <summary>
        /// How far the pointer may move in one frame and still be treated as
        /// aiming, in pixels.
        ///
        /// A mouse at the highest sensible sensitivity, whipped, produces a
        /// few hundred pixels in a frame; this is well above that and well
        /// below the width of a screen, which is the distance a pen covers
        /// when it is lifted and set down. Set to zero to switch the guard
        /// off entirely, which is what the settings' "off" does.
        /// </summary>
        public static float JumpPixels { get; set; } = 600;

        /// <summary>
        /// Whether the guard is applied. On by default: it costs a mouse
        /// nothing -- no mouse movement reaches the threshold -- and it is
        /// the whole difference between a tablet working and not.
        /// </summary>
        public static bool GuardJumps { get; set; } = true;

        /// <summary>
        /// Pointer jumps ignored so far. Zero for anybody using a mouse,
        /// which is also how a report of "my aim snaps" can be told apart
        /// from a report of "my tablet does nothing".
        /// </summary>
        public static int JumpsIgnored { get; private set; }

        /// <summary>
        /// Whether something that jumps has ever been seen, which in practice
        /// means a pen or a touchscreen. Only ever used to say so in a log --
        /// nothing behaves differently because of it, since the guard is the
        /// behaviour and it is always on.
        /// </summary>
        public static bool JumpingPointerSeen { get; private set; }

        /// <summary>
        /// One frame of pointer movement, with a jump turned into no movement
        /// at all rather than into a smaller one.
        ///
        /// Zero, not a clamp: a clamped jump is still a turn nobody asked
        /// for, just a slower one, and the frame a pen is set down in
        /// contains no aiming intent to preserve.
        /// </summary>
        public static float Filter(float delta)
        {
            if (!GuardJumps || JumpPixels <= 0)
            {
                return delta;
            }
            if (MathF.Abs(delta) < JumpPixels)
            {
                return delta;
            }
            JumpsIgnored++;
            if (!JumpingPointerSeen)
            {
                JumpingPointerSeen = true;
                DebugLog.Line("input", $"pointer jumped {delta:0} px in a frame and was "
                    + "ignored -- a pen, a touchscreen, or a cursor warp");
            }
            return 0;
        }

        public static void Reset()
        {
            JumpsIgnored = 0;
            JumpingPointerSeen = false;
        }
    }
}
