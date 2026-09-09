namespace MphRead.Mods
{
    /// <summary>
    /// "There is no picture, no sound and nobody at the keyboard."
    ///
    /// A global switch, in the shape <see cref="ThumbnailMode"/> already
    /// established, for the one caller that runs the engine's simulation with
    /// no window at all: the dedicated server, when it is the match's
    /// simulation authority (<c>Mods/Network/ServerSim.cs</c>).
    ///
    /// Every other headless thing in this program -- the map audit, the net
    /// check client, the thumbnail capture -- is headless only in the sense
    /// that its window is never shown. They all still create a GL context and
    /// draw, because they are measuring or photographing what was drawn. This
    /// is the first caller that genuinely never draws, and the difference is
    /// worth a switch rather than a hidden window: a GL context is the single
    /// largest thing a process of this program allocates, and the machine this
    /// runs on is a Pi with under 320 MB free.
    ///
    /// What it turns off is only ever work whose output is a picture, a sound
    /// or an answer to a person. Nothing here may change what the simulation
    /// does -- the whole value of running the engine on the server is that it
    /// is the same engine, so a switch that made it a different one would give
    /// the server a world that disagreed with every client's.
    /// </summary>
    public static class Headless
    {
        /// <summary>
        /// True while the engine is running with no GL context and no audio
        /// device. Set once, before a scene is built, and never cleared: a
        /// process either is a headless simulation or is not.
        /// </summary>
        public static bool Active { get; private set; }

        public static void Enter()
        {
            Active = true;
        }
    }
}
