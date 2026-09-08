using System;
using System.Collections.Generic;
using System.IO;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// The last few seconds of the match, kept in memory so they can be saved
    /// after the fact.
    ///
    /// A demo has to be started before the thing worth watching happens, which
    /// is the one moment nobody knows it is about to. So this records all the
    /// time and keeps only a window: press the button and the window becomes a
    /// file, press nothing and it is thrown away.
    ///
    /// It holds the same three streams <see cref="DemoRecorder"/> writes --
    /// what arrived from the server, this client's own intents, and, when this
    /// client is the authority, the snapshots it published -- because a clip
    /// missing any of them opens on an empty room for the reasons that class
    /// documents at length.
    ///
    /// Kept apart from DemoRecorder rather than folded into it: a full
    /// recording started from the menu is a deliberate act with a file
    /// attached, and this is a rolling buffer with none. They run at the same
    /// time and neither interferes with the other -- a player recording the
    /// whole match can still cut a short out of it, which is the case that
    /// made them separate classes instead of one with a mode.
    /// </summary>
    internal static class DemoClip
    {
        /// <summary>The lengths the settings screen offers, in seconds.</summary>
        public static readonly int[] Lengths = { 5, 10, 15 };

        /// <summary>
        /// How much to keep. Zero switches the buffer off entirely, which is
        /// what an unbound button means: nothing can ask for a clip, so
        /// nothing is worth keeping.
        /// </summary>
        public static int Seconds { get; set; } = 10;

        /// <summary>
        /// Frames of slack over the asked-for window.
        ///
        /// A clip trimmed exactly to the second starts on whatever packet
        /// happened to be first, which is usually mid-snapshot; a little extra
        /// gives the reader a complete one to open on. Cheap: this is a few
        /// hundred kilobytes at most.
        /// </summary>
        private const uint Slack = 30;

        private static readonly Queue<DemoRecord> _records = new();

        /// <summary>
        /// Bytes held. Tracked rather than measured so a long stall -- a match
        /// paused on a menu, a client hitching -- cannot grow this without
        /// bound while the frame counter stands still.
        /// </summary>
        private static long _bytes;

        private const long MaxBytes = 24 * 1024 * 1024;

        public static bool Active => Seconds > 0 && NetSession.Active && !DemoPlayback.IsActive;

        /// <summary>Seconds actually held right now, for the HUD to say so.</summary>
        public static double Held
        {
            get
            {
                if (_records.Count == 0)
                {
                    return 0;
                }
                uint first = 0;
                foreach (DemoRecord record in _records)
                {
                    first = record.Frame;
                    break;
                }
                return Math.Max(0, (NetSession.NetFrame - first) / 60.0);
            }
        }

        public static void Add(ReadOnlySpan<byte> data)
        {
            if (!Active || data.Length == 0 || data.Length > UInt16.MaxValue)
            {
                return;
            }
            _records.Enqueue(new DemoRecord(NetSession.NetFrame, data.ToArray()));
            _bytes += data.Length;
            Trim();
        }

        private static void Trim()
        {
            uint window = (uint)(Seconds * 60) + Slack;
            uint now = NetSession.NetFrame;
            while (_records.Count > 0)
            {
                DemoRecord oldest = _records.Peek();
                // Unsigned, so the subtraction has to be guarded rather than
                // compared: a counter that restarted at a room change would
                // otherwise wrap into an enormous age and empty the buffer.
                bool tooOld = now >= oldest.Frame && now - oldest.Frame > window;
                if (!tooOld && _bytes <= MaxBytes)
                {
                    break;
                }
                _bytes -= oldest.Data.Length;
                _records.Dequeue();
            }
        }

        /// <summary>
        /// Throw the window away.
        ///
        /// Called at the end of a match, when nobody asked for a clip, and
        /// when the button is rebound -- binding it is the moment a player
        /// starts meaning to use it, and inheriting whatever was in memory
        /// from before then would hand them somebody else's few seconds.
        /// </summary>
        public static void Purge()
        {
            _records.Clear();
            _bytes = 0;
        }

        /// <summary>
        /// Write what is held to a demo file. Returns the path, or null if
        /// there was nothing to write or the write failed.
        ///
        /// Frames are rebased so the clip starts at zero: the reader clocks
        /// records off the first one, and a file whose first record is at
        /// frame ninety thousand would sit still for twenty-five minutes
        /// before showing anything.
        /// </summary>
        public static string? Save()
        {
            if (_records.Count == 0)
            {
                return null;
            }
            string room = NetSession.ServerMatch?.RoomKey ?? "match";
            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                room = room.Replace(bad, '_');
            }
            room = room.Replace(' ', '_');
            string fileName = $"{room}_clip_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{DemoFile.Extension}";
            string path = Paths.Combine(Paths.Export, "_demos", fileName);
            try
            {
                using var writer = new DemoWriter(path);
                uint start = 0;
                bool first = true;
                foreach (DemoRecord record in _records)
                {
                    if (first)
                    {
                        start = record.Frame;
                        first = false;
                    }
                    writer.WriteRecord(record.Frame >= start ? record.Frame - start : 0,
                        record.Data);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[demo] could not save the clip: {ex.Message}");
                return null;
            }
            return path;
        }
    }
}
