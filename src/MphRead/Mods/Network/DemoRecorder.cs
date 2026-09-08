using System;
using System.IO;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Records every packet this client receives to a demo file, from
    /// whenever the player asks (the pause menu's "Record demo", online
    /// matches only) to whenever they ask again or the match ends.
    ///
    /// Fed from <see cref="NetSession.Update"/>'s own drain loop -- it sees
    /// exactly what the session sees, in the same order, on the same frame,
    /// so a replay through <see cref="DemoPlayback"/> reproduces exactly what
    /// this client saw.
    ///
    /// Two things this client never receives are synthesized instead, because
    /// a demo made of arrivals alone is missing whatever this machine already
    /// knew: its own intent (<see cref="RecordOwnIntent"/>) and, when it is
    /// the authority, its own snapshot (<see cref="RecordOwnSnapshot"/>).
    /// </summary>
    internal static class DemoRecorder
    {
        private static DemoWriter? _writer;
        private static uint _startFrame;

        public static bool IsRecording => _writer != null;

        /// <summary>Where the file being written now lives, for a "saved to..." message.</summary>
        public static string? CurrentPath { get; private set; }

        public static bool Start()
        {
            if (IsRecording || !NetSession.Active || DemoPlayback.IsActive)
            {
                return false;
            }
            string room = SanitizeFileName(NetSession.ServerMatch?.RoomKey ?? "match");
            string fileName = $"{room}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{DemoFile.Extension}";
            string path = Paths.Combine(Paths.Export, "_demos", fileName);
            try
            {
                _writer = new DemoWriter(path);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[demo] could not start recording: {ex.Message}");
                _writer = null;
                return false;
            }
            CurrentPath = path;
            _startFrame = NetSession.NetFrame;
            return true;
        }

        public static void Stop()
        {
            _writer?.Dispose();
            _writer = null;
            CurrentPath = null;
        }

        internal static void Record(ReceivedPacket packet)
        {
            // The clip buffer first, and whether or not a full recording is
            // running: the two are independent, and a player recording the
            // whole match can still cut a short out of it.
            DemoClip.Add(packet.Data.AsSpan(0, packet.Length));
            if (_writer == null)
            {
                return;
            }
            _writer.WriteRecord(Frame(), packet.Data.AsSpan(0, packet.Length));
        }

        /// <summary>
        /// Synthesizes a SlotIntent record for this client's own outgoing
        /// Intent, exactly the shape <see cref="NetSession"/> would have
        /// received one in from the server for anybody else's input
        /// (<c>[PacketType.SlotIntent][slot][IntentPacket bytes]</c>) -- see
        /// the call site in <see cref="NetSession.SendIntent"/> for why this
        /// is the only way the recording player's own shooting/morphing/
        /// alt-attack animations end up in the file at all.
        /// </summary>
        internal static void RecordOwnIntent(int slot, ReadOnlySpan<byte> intentBytes)
        {
            if (_writer == null && !DemoClip.Active)
            {
                return;
            }
            Span<byte> buffer = stackalloc byte[2 + intentBytes.Length];
            buffer[0] = (byte)PacketType.SlotIntent;
            buffer[1] = (byte)slot;
            intentBytes.CopyTo(buffer[2..]);
            DemoClip.Add(buffer);
            _writer?.WriteRecord(Frame(), buffer);
        }

        /// <summary>
        /// Synthesizes a Snapshot record for the one this machine is about to
        /// publish, when this machine is the one publishing them.
        ///
        /// Without it, a demo recorded by the authority contains no snapshots
        /// at all -- the server forwards them to every peer *except* the one
        /// that sent them, which is right on the wire and leaves a hole in the
        /// file. And the snapshot is not one stream among several: it is the
        /// only carrier of health, score, the damage sequence and the spawn
        /// flag, and <see cref="NetPlayerBridge.ApplyState"/> is the only
        /// thing during playback that ever calls <c>ModNetSpawn</c>. So an
        /// authority's demo did not merely look thin, it opened on an empty
        /// room: nobody was ever placed, nothing was ever hit, and no score
        /// ever moved.
        ///
        /// The authority is whichever client connected first, which is
        /// normally whoever set the match up -- so this was the common case,
        /// not the corner one.
        /// </summary>
        internal static void RecordOwnSnapshot(ReadOnlySpan<byte> payload)
        {
            if (_writer == null && !DemoClip.Active)
            {
                return;
            }
            Span<byte> buffer = stackalloc byte[1 + payload.Length];
            buffer[0] = (byte)PacketType.Snapshot;
            payload.CopyTo(buffer[1..]);
            DemoClip.Add(buffer);
            _writer?.WriteRecord(Frame(), buffer);
        }

        /// <summary>
        /// Simulation frames since recording started.
        ///
        /// The frame counter and not the clock: the whole point is that the
        /// player releases these on the frame they were recorded on, whatever
        /// either machine's frame rate is doing. See <see cref="DemoFile"/>.
        /// </summary>
        private static uint Frame()
        {
            uint now = NetSession.NetFrame;
            return now > _startFrame ? now - _startFrame : 0;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
