using System;
using System.Threading;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Hosting, done by running the dedicated server in this process and
    /// joining it over the loopback.
    ///
    /// The alternative -- the peer-to-peer host role in NetSession -- answers
    /// Hello and relays intent and nothing else: no roster, so no names, no
    /// hunters and no pings; no match clock, so no rotation. Everything that
    /// makes a match legible was written once, in the server, and a listen
    /// host that runs it gets all of it for the cost of one thread. The local
    /// player is the first to connect, so it is the simulation authority,
    /// which is what a listen host is anyway.
    /// </summary>
    public static class NetHostSession
    {
        private static DedicatedServer? _server;
        private static CancellationTokenSource? _cancel;
        private static Thread? _thread;

        public static bool Running => _server != null;

        /// <summary>Why the server would not start, for the screen to show.</summary>
        public static string? LastError { get; private set; }

        /// <param name="listing">
        /// Where to announce this game, or null to keep it off every list.
        ///
        /// A hosted game is a dedicated server that happens to be running
        /// inside somebody's client, so there is no reason it cannot be found
        /// the same way -- but it is also somebody's home machine, and being
        /// listed publishes the address of it. That is a decision for the
        /// person hosting, made on the card that starts the game, not a
        /// default buried here.
        /// </param>
        public static bool StartAndJoin(int port, string playerName, Hunter hunter,
            string roomKey, GameMode mode, float timeLimit, int pointGoal,
            int maxPlayers = PlayerEntity.SlotCapacity,
            (string Host, int Port, string Name)? listing = null)
        {
            Stop();
            LastError = null;
            MapRotation rotation = MapRotation.SingleMatch(roomKey, mode, timeLimit, pointGoal);
            // GameState.FriendlyFire is whatever the host chose in Match
            // rules -- previously that only ever applied on their own
            // machine; broadcasting it here is what makes everyone else's
            // TakeDamage agree with it too.
            var server = new DedicatedServer(port, maxPlayers, rotation)
            {
                FriendlyFire = GameState.FriendlyFire,
                ShadowFreeze = GameState.ShadowFreeze
            };
            if (listing != null)
            {
                server.ServerName = listing.Value.Name;
                server.Reporter = new MasterReporter(listing.Value.Host, listing.Value.Port);
                Console.WriteLine($"[net] listing this game on {listing.Value.Host}:"
                    + $"{listing.Value.Port} as \"{listing.Value.Name}\"");
            }
            var cancel = new CancellationTokenSource();
            _server = server;
            _cancel = cancel;
            _thread = new Thread(() =>
            {
                try
                {
                    server.Run(cancel.Token);
                }
                catch (Exception ex)
                {
                    // Almost always the port: binding happens inside Run, on
                    // this thread, and an unhandled exception here would take
                    // the whole process down instead of showing a message.
                    LastError = ex.Message;
                }
            })
            {
                IsBackground = true,
                Name = "MphRead host server"
            };
            _thread.Start();
            // The socket is bound a few milliseconds in; the join below retries
            // for several seconds anyway, so this only avoids the first Hello
            // being sent into nothing.
            Thread.Sleep(250);
            if (LastError != null)
            {
                Stop();
                return false;
            }
            if (!NetLaunch.Join("127.0.0.1", port, playerName, hunter))
            {
                Stop();
                return false;
            }
            return true;
        }

        public static void Stop()
        {
            _cancel?.Cancel();
            _server?.Stop();
            // Not joined: the run loop sleeps a millisecond at a time and will
            // notice, and a background thread cannot hold the process open.
            _server = null;
            _cancel?.Dispose();
            _cancel = null;
            _thread = null;
        }
    }
}
