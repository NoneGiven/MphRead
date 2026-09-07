using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Headless relay server. No window, no GL, no game files -- it can run
    /// on a small ARM64 box from the command line.
    ///
    /// It is a relay, not a simulator. MphRead's simulation runs in float
    /// (Fixed only converts the ROM's 20.12 values on load), so a server
    /// cannot be trusted to reproduce a client's physics bit-for-bit; and
    /// reproducing it at all would mean running the whole engine, which
    /// needs the game files this machine deliberately does not have. So the
    /// first client to connect is the simulation authority: the server
    /// assigns slots, forwards intents to that authority, and fans its
    /// snapshots back out to everyone else.
    ///
    /// What that buys over peer-to-peer hosting: players connect to one
    /// stable address instead of to whoever happens to be hosting, the
    /// authority can leave and be replaced without the session dying, and
    /// only the server needs a reachable port.
    /// </summary>
    public sealed class DedicatedServer
    {
        private sealed class Peer
        {
            public IPEndPoint EndPoint = null!;
            public int SlotIndex = -1;
            public double LastSeen;
            public uint LastIntentFrame;
            public string Name = "";
            public byte Hunter;
            /// <summary>The suit this player asked for, 0-3. See PlayerColors.</summary>
            public byte Color;
            /// <summary>Round trip in milliseconds, smoothed. 0 = not measured yet.</summary>
            public int Ping;
            public double PingSentAt;
            public byte PingId;
            public bool PingPending;
            /// <summary>
            /// Chat allowance left, in messages. See <see cref="HandleChat"/>.
            /// Starts full so a player can say hello the moment they arrive.
            /// </summary>
            public double ChatCredit = ChatBurst;
            public double ChatCreditAt;
            /// <summary>
            /// Lines dropped since the last one that got through. Only there
            /// so the log says "this peer is flooding" once rather than once
            /// per dropped packet, which would make the flood the log's
            /// problem as well as the relay's.
            /// </summary>
            public int ChatDropped;
        }

        private readonly List<Peer> _peers = new();
        private readonly byte[] _scratch = new byte[NetConfig.MaxPacketSize];
        private readonly int _port;
        private readonly int _maxPlayers;
        private readonly MapRotation _rotation;
        private NetTransport? _transport;
        private Peer? _authority;
        private byte[]? _lastSnapshot;
        private volatile bool _running;
        private double _matchStarted;
        /// <summary>
        /// When the match ended, or -1 while one is being played.
        ///
        /// A match ends on this server for two reasons and they used to be
        /// handled as one: the clock running out, which the server saw for
        /// itself, and somebody reaching the score, which only the machine
        /// simulating the match can know. The second case rotated nothing at
        /// all -- the winner was announced, every client faded to black, and
        /// each one dropped back to its own launcher. Both now enter the same
        /// short intermission, which is what gives the results screen time to
        /// play before everyone is moved together.
        /// </summary>
        private double _matchEndedAt = -1;

        /// <summary>
        /// Counts the matches this server has started. Published so a client
        /// can tell a new round from the one it is already playing, which the
        /// map name cannot do when the rotation is one map long.
        /// </summary>
        private ushort _matchId = 1;

        /// <summary>
        /// How long the results are left on screen before the map changes.
        ///
        /// The client's own end-of-match sequence is three seconds of the
        /// winner's camera and <see cref="GameState.MatchEndingSeconds"/> of
        /// the results; a second on top of that means the fade to black
        /// belongs to the rotation rather than cutting the results short.
        ///
        /// It has to cover the whole sequence, because the results screen is
        /// where the next hunter and the next suit are chosen now (see
        /// Mods.EndScreen) -- a server that rotated early would take the
        /// question away mid-answer.
        /// </summary>
        private const double EndSequenceSeconds = 3.0 + GameState.MatchEndingSeconds + 1.0;

        /// <summary>
        /// What this server calls itself on a browser's list. Defaults to the
        /// machine name, because an unnamed row in a list of servers is worse
        /// than a dull one.
        /// </summary>
        public string ServerName { get; set; } = Environment.MachineName;

        /// <summary>
        /// How many peers are connected, for a pool deciding whether a game it
        /// started is still being played.
        ///
        /// Read from another thread on purpose. It is one int, it is only ever
        /// used to answer "has this been empty for minutes", and taking a lock
        /// on the relay's hot path to make a housekeeping check exact would be
        /// the wrong trade.
        /// </summary>
        public int PeerCount => _peers.Count;

        /// <summary>
        /// Whether anybody has ever been in this match.
        ///
        /// The difference between "started a moment ago and the host is still
        /// loading the map" and "was being played and everyone has left" --
        /// which want completely different amounts of patience from whatever
        /// is deciding when to shut it down.
        /// </summary>
        public bool EverOccupied { get; private set; }

        /// <summary>Whether the listener is up, so a pool can wait for it.</summary>
        public bool Listening => _transport != null;

        /// <summary>The port actually bound, which is not the requested one when that was zero.</summary>
        public int BoundPort => _transport?.LocalPort ?? _port;

        /// <summary>
        /// Where to announce this server, or null to stay unlisted. See
        /// <see cref="MasterReporter"/>.
        /// </summary>
        public MasterReporter? Reporter { get; set; }

        /// <summary>
        /// Whether same-team damage counts, for the whole session. This is
        /// the server's call, broadcast in every <see cref="MatchStatePacket"/>
        /// so every client applies the same rule -- each client's own local
        /// setting used to be what decided this, which meant the host turning
        /// it on in Match rules never reached anyone else.
        /// </summary>
        public bool FriendlyFire { get; set; }

        /// <summary>
        /// Whether the shadow freeze glitch is allowed here. On by default,
        /// because it is what the cartridge does and a server that quietly
        /// changed the game would be a surprising one; <c>-noshadowfreeze</c>
        /// turns it off for the room. See GameState.ShadowFreeze.
        /// </summary>
        public bool ShadowFreeze { get; set; } = true;

        /// <summary>
        /// Whether this server keeps itself on the newest release.
        ///
        /// Opt-in, and set by exactly one caller: the standalone
        /// <c>-server</c> path, which is the process's whole reason for
        /// existing. The other two <see cref="DedicatedServer"/>s in the
        /// program must not -- a listen host is a server inside somebody's
        /// game, and a hosted match is one of several inside the directory's
        /// process, so a swap decided here would take down a program that was
        /// doing something else as well, and several of them would race to
        /// decide it.
        /// </summary>
        public bool AutoUpdate { get; set; }

        public DedicatedServer(int port = NetConfig.DefaultPort, int maxPlayers = 4,
                               MapRotation? rotation = null)
        {
            _port = port;
            _maxPlayers = Math.Clamp(maxPlayers, 2, MphRead.Entities.PlayerEntity.SlotCapacity);
            _rotation = rotation ?? new MapRotation();
        }

        public void Run(CancellationToken cancel = default)
        {
            _transport = new NetTransport(_port);
            _running = true;
            Log($"listening on UDP {_transport.LocalPort}, up to {_maxPlayers} players");
            Log("relay mode: the first client to connect is the simulation authority");
            Log($"rotation: {_rotation.Entries.Count} map(s), starting on {_rotation.Current}");

            // The bound port, taken once: the heartbeat has to advertise the
            // port players dial, which is not the requested one when the
            // requested one was zero.
            var listenPort = (ushort)_transport.LocalPort;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double lastReport = 0;
            double lastStateBroadcast = 0;
            _matchStarted = 0;
            try
            {
                while (_running && !cancel.IsCancellationRequested)
                {
                    double now = clock.Elapsed.TotalSeconds;
                    foreach (ReceivedPacket packet in _transport.Drain())
                    {
                        Handle(packet, now);
                    }
                    DropTimedOut(now);

                    // The server owns the match clock, not the authority client:
                    // that is what lets a joiner adopt a running match's timer
                    // instead of starting its own, and what keeps the rotation
                    // advancing even while players come and go.
                    float limit = _rotation.Current.TimeLimit;
                    if (_matchEndedAt < 0 && limit > 0 && _peers.Count > 0
                        && now - _matchStarted >= limit)
                    {
                        EndMatch(now, "time limit");
                    }
                    else if (_matchEndedAt >= 0 && now - _matchEndedAt >= EndSequenceSeconds)
                    {
                        AdvanceMap(now);
                    }
                    // Repeated rather than sent once: UDP drops, and a client that
                    // missed the state packet would otherwise sit on a stale map.
                    if (now - lastStateBroadcast >= 1.0)
                    {
                        lastStateBroadcast = now;
                        // Order matters only in that the roster carries the last
                        // measurement: ping first, publish second.
                        PingPeers(now);
                        BroadcastMatchState(now);
                        BroadcastRoster();
                        if (_authority != null)
                        {
                            NotifyAuthority(_authority);
                        }
                        Reporter?.Beat(now, ServerName, listenPort,
                            (byte)_peers.Count, (byte)_maxPlayers,
                            (byte)_rotation.Current.Mode, _rotation.Current.RoomKey);
                    }
                    // Newest release, checked on a timer and applied the
                    // moment there is nobody to interrupt. It says yes at most
                    // once, and only with an empty server, so a busy one keeps
                    // playing and swaps when the last person leaves.
                    if (AutoUpdate && Update.ServerUpdate.ShouldRestart(_peers.Count))
                    {
                        Log("shutting down to come back on the new build");
                        _running = false;
                        break;
                    }
                    if (now - lastReport >= 30)
                    {
                        lastReport = now;
                        Log($"{_peers.Count} peer(s) connected"
                            + (_authority != null ? $", authority = slot {_authority.SlotIndex}" : ", no authority")
                            + $", map {_rotation.Current.RoomKey}"
                            + (limit > 0 ? $", {Math.Max(0, limit - (now - _matchStarted)):0} s left" : "")
                            + (_transport is { PacketsDropped: > 0 }
                                ? $", {_transport.PacketsDropped} packet(s) dropped" : ""));
                    }
                    // A millisecond between passes while anyone is connected --
                    // well under any sane packet interval -- and twenty while
                    // nobody is. An empty server spinning at 1 kHz cost 5-7% of a
                    // core on the Pi around the clock for nothing; the only thing
                    // waiting on this loop then is the next Hello, and twenty
                    // milliseconds is not a join anybody can feel.
                    Thread.Sleep(_peers.Count == 0 ? 20 : 1);
                }
            }
            finally
            {
                Shutdown(listenPort);
            }
        }

        /// <summary>
        /// Come off the list and give the socket back.
        ///
        /// In a finally, not after the loop: this used to be plain statements
        /// at the end of Run, so anything thrown inside the loop skipped the
        /// farewell entirely and left the game listed -- offered to players,
        /// answering nothing -- until the directory timed it out. A hosted
        /// game runs inside the player's own process, where an exception is
        /// swallowed by the thread wrapper and nothing says so.
        /// </summary>
        private void Shutdown(ushort listenPort)
        {
            Log("shutting down");
            _running = false;
            try
            {
                // Say so, rather than letting the directory work it out from
                // fifty seconds of silence. A server that has just been
                // stopped is a server nobody should still be offered.
                Reporter?.Farewell(listenPort);
            }
            catch (Exception)
            {
                // Nothing left to tell, and nothing left to do about it.
            }
            Reporter?.Dispose();
            Reporter = null;
            _transport?.Dispose();
            _transport = null;
        }

        /// <summary>
        /// Begin the intermission. Announced immediately rather than waiting
        /// for the next periodic state, because the clients are about to show
        /// their results screens and the flag is what stops them adopting the
        /// match clock over the countdown that screen runs on.
        /// </summary>
        private void EndMatch(double now, string reason)
        {
            if (_matchEndedAt >= 0)
            {
                return;
            }
            _matchEndedAt = now;
            Log($"match over on {_rotation.Current.RoomKey} ({reason}); "
                + $"{_rotation.Next.RoomKey} in {EndSequenceSeconds:0} s");
            BroadcastMatchState(now);
        }

        private void AdvanceMap(double now)
        {
            RotationEntry entry = _rotation.Advance();
            _matchStarted = now;
            _matchEndedAt = -1;
            _matchId++;
            Log($"rotating to {entry}");
            MatchStatePacket state = BuildState(now);
            state.Write(_scratch);
            for (int i = 0; i < _peers.Count; i++)
            {
                _transport?.Send(_peers[i].EndPoint, PacketType.MapChange,
                    _scratch.AsSpan(0, MatchStatePacket.Size));
            }
        }

        private MatchStatePacket BuildState(double now)
        {
            RotationEntry entry = _rotation.Current;
            float elapsed = (float)(now - _matchStarted);
            bool ending = _matchEndedAt >= 0;
            return new MatchStatePacket
            {
                Mode = (byte)entry.Mode,
                TimeRemaining = ending || entry.TimeLimit <= 0
                    ? 0
                    : Math.Max(0, entry.TimeLimit - elapsed),
                TimeElapsed = elapsed,
                PlayerCount = (byte)_peers.Count,
                Flags = (byte)((ending ? MatchStatePacket.FlagEnding : MatchStatePacket.FlagInProgress)
                    | (FriendlyFire ? MatchStatePacket.FlagFriendlyFire : 0)
                    | (ShadowFreeze ? 0 : MatchStatePacket.FlagNoShadowFreeze)),
                PointGoal = (ushort)Math.Clamp(entry.PointGoal, 0, UInt16.MaxValue),
                MatchId = _matchId,
                RoomKey = entry.RoomKey,
                NextRoomKey = _rotation.Next.RoomKey
            };
        }

        private void BroadcastMatchState(double now)
        {
            if (_peers.Count == 0)
            {
                return;
            }
            MatchStatePacket state = BuildState(now);
            state.Write(_scratch);
            for (int i = 0; i < _peers.Count; i++)
            {
                _transport?.Send(_peers[i].EndPoint, PacketType.MatchState,
                    _scratch.AsSpan(0, MatchStatePacket.Size));
            }
        }

        public void Stop() => _running = false;

        private void Handle(ReceivedPacket packet, double now)
        {
            switch (packet.Type)
            {
                case PacketType.Hello:
                    HandleHello(packet, now);
                    break;
                case PacketType.Intent:
                    HandleIntent(packet, now);
                    break;
                case PacketType.Snapshot:
                    HandleSnapshot(packet, now);
                    break;
                case PacketType.Bye:
                    HandleBye(packet);
                    break;
                case PacketType.Identify:
                    HandleIdentify(packet, now);
                    break;
                case PacketType.Ping:
                    _transport?.Send(packet.Sender, PacketType.Pong, ReadOnlySpan<byte>.Empty);
                    break;
                case PacketType.Pong:
                    HandlePong(packet, now);
                    break;
                case PacketType.StatusQuery:
                    SendStatus(packet.Sender, now);
                    break;
                case PacketType.MatchEnd:
                    HandleMatchEnd(packet, now);
                    break;
                case PacketType.Chat:
                    HandleChat(packet, now);
                    break;
            }
        }

        /// <summary>
        /// Sustained chat rate, in messages a second, and how many may be
        /// sent back to back before that rate starts to bite.
        ///
        /// A relay with no limit here is a relay that will multiply one
        /// client's flood by the number of people in the match and send it to
        /// all of them -- the one packet type in this protocol whose contents
        /// a player chooses freely and whose cost is paid by everybody else.
        /// Three in a row then one every two seconds is faster than anybody
        /// types and slower than anything worth calling a flood.
        /// </summary>
        private const double ChatRatePerSecond = 0.5;
        private const double ChatBurst = 3;

        /// <summary>
        /// Pass one player's line on to everybody else.
        ///
        /// The slot and the name are overwritten with what this server knows
        /// about the sender rather than taken from the packet. A client can
        /// put any name it likes in those fields, and a line that appears to
        /// come from somebody else is the entire attack: the endpoint a
        /// datagram arrived from is the only thing here that cannot be typed
        /// into a text box.
        ///
        /// Not sent back to the sender, which echoes its own line locally --
        /// see <c>ChatBox.Submit</c> for why that is the better half of the
        /// round trip to skip.
        /// </summary>
        private void HandleChat(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null || peer.SlotIndex < 0 || packet.Payload.Length < ChatPacket.Size)
            {
                return;
            }
            peer.LastSeen = now;
            ChatPacket chat = ChatPacket.Read(packet.Payload);
            if (chat.Text.Length == 0)
            {
                return;
            }
            // A leaky bucket rather than a minimum interval: an interval
            // punishes two quick lines of the same thought exactly as hard as
            // a hundred, and the thing worth stopping is the hundred.
            peer.ChatCredit = Math.Min(ChatBurst,
                peer.ChatCredit + (now - peer.ChatCreditAt) * ChatRatePerSecond);
            peer.ChatCreditAt = now;
            if (peer.ChatCredit < 1)
            {
                if (peer.ChatDropped++ == 0)
                {
                    Log($"chat from slot {peer.SlotIndex} ({peer.EndPoint}) dropped: too fast");
                }
                return;
            }
            peer.ChatCredit -= 1;
            peer.ChatDropped = 0;
            chat.Slot = (byte)peer.SlotIndex;
            chat.Name = peer.Name.Length > 0 ? peer.Name : $"Player{peer.SlotIndex}";
            // Team chat has no teams behind it yet: relaying it as a team line
            // would deliver it to everybody while telling the reader it went
            // to one side, which is worse than not having the channel.
            chat.Kind = ChatPacket.KindSay;
            chat.Write(_scratch);
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i] != peer)
                {
                    _transport?.Send(_peers[i].EndPoint, PacketType.Chat,
                        _scratch.AsSpan(0, ChatPacket.Size));
                }
            }
            Log($"chat {chat.Name}: {chat.Text}");
        }

        /// <summary>
        /// The server itself saying something to everybody.
        ///
        /// No slot and no name -- <see cref="ChatPacket.KindSystem"/> is drawn
        /// in its own colour with nobody's name in front of it, because a
        /// notice attributed to a player reads as that player having typed it.
        /// Not rate limited: nothing a client sends can cause one.
        /// </summary>
        private void Announce(string text)
        {
            if (_peers.Count == 0)
            {
                return;
            }
            var chat = new ChatPacket
            {
                Slot = 0xFF,
                Kind = ChatPacket.KindSystem,
                Name = String.Empty,
                Text = text
            };
            chat.Write(_scratch);
            for (int i = 0; i < _peers.Count; i++)
            {
                _transport?.Send(_peers[i].EndPoint, PacketType.Chat,
                    _scratch.AsSpan(0, ChatPacket.Size));
            }
        }

        /// <summary>
        /// Answer "what is running here?" without touching the roster.
        ///
        /// The asker is a launcher deciding whether to show this server as
        /// worth joining, not a player: it gets no slot, no peer entry and no
        /// effect on the match clock, so it can be asked every few seconds
        /// while somebody reads the screen.
        /// </summary>
        private void SendStatus(IPEndPoint sender, double now)
        {
            var status = new ServerStatusPacket
            {
                Match = BuildState(now),
                MaxPlayers = (byte)_maxPlayers,
                Protocol = NetConfig.ProtocolVersion,
                ServerName = ServerName
            };
            status.Write(_scratch);
            _transport?.Send(sender, PacketType.StatusReply,
                _scratch.AsSpan(0, ServerStatusPacket.Size));
        }

        private void HandleHello(ReceivedPacket packet, double now)
        {
            if (packet.Payload.Length < 1 || packet.Payload[0] != NetConfig.ProtocolVersion)
            {
                Log($"rejected {packet.Sender}: protocol mismatch");
                SendRefusal(packet.Sender, RefusedPacket.ReasonProtocol);
                return;
            }
            Peer? peer = Find(packet.Sender);
            if (peer == null)
            {
                // Honour the slot the client asks for when it is free. A
                // client that says hello again is usually one this server
                // dropped while it was loading a room, and handing it a
                // different slot swaps two players' identities mid-match.
                int slot = -1;
                if (packet.Payload.Length >= 2 && packet.Payload[1] != 0xFF
                    && packet.Payload[1] < _maxPlayers && SlotFree(packet.Payload[1]))
                {
                    slot = packet.Payload[1];
                }
                if (slot < 0)
                {
                    slot = NextFreeSlot();
                }
                if (slot < 0)
                {
                    Log($"rejected {packet.Sender}: session full");
                    SendRefusal(packet.Sender, RefusedPacket.ReasonFull);
                    return;
                }
                if (_peers.Count == 0)
                {
                    // Restart the match clock for the first arrival. The clock
                    // runs whether or not anybody is connected, so a server
                    // left alone overnight greets its next player with a round
                    // that has no time left on it -- which the client adopts,
                    // ending the match before it has drawn a frame.
                    _matchStarted = now;
                    // And it is not mid-results either: an intermission that
                    // was running when the last player left has nobody to show
                    // it to.
                    _matchEndedAt = -1;
                    _matchId++;
                }
                peer = new Peer { EndPoint = packet.Sender, SlotIndex = slot };
                _peers.Add(peer);
                EverOccupied = true;
                // Slot 0 is the authority's slot, matching what a listen host
                // would occupy, so clients need no special case for either.
                if (_authority == null)
                {
                    _authority = peer;
                    Log($"{packet.Sender} joined as slot {slot} (authority)");
                    NotifyAuthority(peer);
                }
                else
                {
                    Log($"{packet.Sender} joined as slot {slot}");
                }
            }
            peer.LastSeen = now;
            // Re-answered on every Hello; the first Welcome may have been lost.
            _scratch[0] = (byte)peer.SlotIndex;
            _transport?.Send(peer.EndPoint, PacketType.Welcome, _scratch.AsSpan(0, 1));
            // Immediately follow with the running match, so a client that
            // arrives mid-round loads the right map and adopts the server's
            // clock rather than starting a fresh one of its own.
            MatchStatePacket state = BuildState(now);
            state.Write(_scratch);
            _transport?.Send(peer.EndPoint, PacketType.MatchState,
                _scratch.AsSpan(0, MatchStatePacket.Size));
            BroadcastRoster();
        }

        /// <summary>
        /// The authority saying the match it is simulating is over.
        ///
        /// Only the authority, and only once: everyone else's copy of the
        /// scoreboard comes from the authority's snapshot, so a client
        /// reporting the end is reporting the authority's own conclusion back
        /// to it -- and a peer that could end matches on its own would be a
        /// peer that could rotate the server whenever it liked.
        ///
        /// Repeated by the sender until the state comes back with the flag
        /// set, so losing this datagram costs a second rather than the
        /// rotation.
        /// </summary>
        private void HandleMatchEnd(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null)
            {
                return;
            }
            peer.LastSeen = now;
            if (peer != _authority)
            {
                return;
            }
            EndMatch(now, "a player reached the goal");
        }

        /// <summary>
        /// Say no out loud. See <see cref="RefusedPacket"/> for why this is
        /// worth a packet: the alternative, and what this used to be, is a
        /// silence the client cannot tell from a server that is switched off.
        ///
        /// One datagram per refused Hello, and a refused client retries about
        /// twice a second for eight seconds, so this is not a reflection risk
        /// worth rate-limiting: the reply is three bytes to an address that
        /// just sent a Hello of its own.
        /// </summary>
        private void SendRefusal(IPEndPoint to, byte reason)
        {
            var refusal = new RefusedPacket
            {
                Reason = reason,
                Players = (byte)_peers.Count,
                MaxPlayers = (byte)_maxPlayers
            };
            refusal.Write(_scratch);
            _transport?.Send(to, PacketType.Refused, _scratch.AsSpan(0, RefusedPacket.Size));
        }

        private void HandleIdentify(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null)
            {
                return;
            }
            peer.LastSeen = now;
            if (packet.Payload.Length < 1)
            {
                return;
            }
            if (packet.Payload.Length < 2)
            {
                return;
            }
            byte hunter = packet.Payload[0];
            byte color = packet.Payload[1];
            string name = System.Text.Encoding.ASCII
                .GetString(packet.Payload[2..])
                .TrimEnd('\0')
                .Trim();
            if (name.Length == 0)
            {
                return;
            }
            if (name.Length > RosterPacket.MaxNameBytes)
            {
                name = name[..RosterPacket.MaxNameBytes];
            }
            if (peer.Name == name && peer.Hunter == hunter && peer.Color == color)
            {
                return;
            }
            bool firstName = peer.Name.Length == 0;
            peer.Name = name;
            peer.Hunter = hunter;
            peer.Color = color;
            Log($"slot {peer.SlotIndex} is \"{name}\" playing {(Hunter)hunter} "
                + $"in suit {color + 1}");
            if (firstName)
            {
                // The first line anybody sees in a match, and the only one
                // that is worth sending. It is also the answer to "is chat
                // working at all here": a player who joins and sees nothing
                // when the next person arrives is on a server that predates
                // chat, which otherwise looks exactly like nobody talking.
                Announce($"{name} joined");
            }
            // Membership changed in a way clients care about, so tell
            // everyone rather than waiting for the periodic broadcast.
            BroadcastRoster();
        }

        /// <summary>
        /// Send every client the full slot->name mapping. This is what lets
        /// each player see who else is actually in the match, which is the
        /// check that distinguishes "connected" from "in the same game".
        /// </summary>
        /// <summary>
        /// Tell a peer it owns the simulation. Without this a client on a
        /// dedicated server is only ever NetRole.Client, so nothing ever
        /// broadcasts snapshots and no player sees another move.
        /// </summary>
        private void NotifyAuthority(Peer peer)
        {
            if (_lastSnapshot != null)
            {
                _transport?.Send(peer.EndPoint, PacketType.Snapshot, _lastSnapshot);
            }
            _scratch[0] = 1;
            _transport?.Send(peer.EndPoint, PacketType.Authority, _scratch.AsSpan(0, 1));
        }

        /// <summary>
        /// Ask every peer how far away it is.
        ///
        /// The server is the only party that can answer that for everybody:
        /// clients never exchange packets with each other, so a client can
        /// time its own round trip and nobody else's. One ping per peer per
        /// second is enough for a number on a scoreboard and is lost in the
        /// noise next to sixty snapshots a second.
        /// </summary>
        private void PingPeers(double now)
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                Peer peer = _peers[i];
                if (peer.PingPending && now - peer.PingSentAt < 5)
                {
                    // Still waiting on the last one. Leave the previous value
                    // standing rather than reporting a peer as unreachable for
                    // one dropped datagram.
                    continue;
                }
                peer.PingId++;
                peer.PingSentAt = now;
                peer.PingPending = true;
                _scratch[0] = peer.PingId;
                _transport?.Send(peer.EndPoint, PacketType.Ping, _scratch.AsSpan(0, 1));
            }
        }

        private void HandlePong(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null || !peer.PingPending)
            {
                return;
            }
            // The id makes a late reply to an earlier ping unusable rather than
            // wrong: without it a Pong that took three seconds to arrive would
            // be measured against the ping sent one second ago.
            if (packet.Payload.Length < 1 || packet.Payload[0] != peer.PingId)
            {
                return;
            }
            peer.PingPending = false;
            peer.LastSeen = now;
            int rtt = (int)Math.Round((now - peer.PingSentAt) * 1000);
            rtt = Math.Clamp(rtt, 0, 9999);
            // Smoothed, because one late datagram is not a worse connection.
            peer.Ping = peer.Ping == 0 ? rtt : (peer.Ping * 2 + rtt) / 3;
        }

        private void BroadcastRoster()
        {
            if (_peers.Count == 0)
            {
                return;
            }
            RosterPacket roster = RosterPacket.Create();
            for (int i = 0; i < _peers.Count && i < RosterPacket.MaxSlots; i++)
            {
                roster.Slots[roster.Count] = (byte)_peers[i].SlotIndex;
                roster.Hunters[roster.Count] = _peers[i].Hunter;
                roster.Colors[roster.Count] = _peers[i].Color;
                roster.Pings[roster.Count] = (ushort)Math.Clamp(_peers[i].Ping, 0, 9999);
                roster.Names[roster.Count] = _peers[i].Name.Length > 0
                    ? _peers[i].Name
                    : $"Player{_peers[i].SlotIndex + 1}";
                roster.Count++;
            }
            roster.Write(_scratch);
            for (int i = 0; i < _peers.Count; i++)
            {
                _transport?.Send(_peers[i].EndPoint, PacketType.Roster,
                    _scratch.AsSpan(0, RosterPacket.Size));
            }
        }

        /// <summary>
        /// How far behind a peer's newest intent frame a packet may be and
        /// still be a reordered straggler rather than a restarted counter.
        /// </summary>
        private const uint IntentResetGap = 600;

        private void HandleIntent(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null || _authority == null)
            {
                return;
            }
            peer.LastSeen = now;
            if (packet.Payload.Length >= IntentPacket.Size)
            {
                IntentPacket intent = IntentPacket.Read(packet.Payload);
                // UDP reorders; an older frame must not replace a newer one.
                //
                // Unless it is far enough behind to be a different session
                // rather than a straggler: a client's counter restarts at
                // zero when it joins, and a Peer that survived a reconnect --
                // one that came back to the same endpoint before this server
                // noticed it had gone -- would otherwise have every packet of
                // its new session refused until the counter climbed back past
                // the old one. Same ten seconds NetSession allows.
                if (peer.LastIntentFrame != 0 && intent.Frame <= peer.LastIntentFrame
                    && peer.LastIntentFrame - intent.Frame < IntentResetGap)
                {
                    return;
                }
                peer.LastIntentFrame = intent.Frame;
            }
            // Tag with the sender's slot. A receiver is a client with no peer
            // list, so it cannot work out who an endpoint belongs to; without
            // this the authority dropped every relayed intent and simulated
            // nobody.
            _scratch[0] = (byte)peer.SlotIndex;
            packet.Payload.CopyTo(_scratch.AsSpan(1));
            for (int i = 0; i < _peers.Count; i++)
            {
                // To everyone, not just the authority. Input is what makes a
                // player do anything visible -- fire, morph, lay a bomb, swing
                // an alt attack -- and a client that only ever received
                // positions drew opponents that slid around the level in
                // silence: no beams, no morph animation, no bombs. Position
                // still comes from the authority's snapshot; this is what
                // fills in everything a position cannot express.
                if (_peers[i] != peer)
                {
                    _transport?.Send(_peers[i].EndPoint, PacketType.SlotIntent,
                        _scratch.AsSpan(0, packet.Payload.Length + 1));
                }
            }
        }

        private void HandleSnapshot(ReceivedPacket packet, double now)
        {
            Peer? peer = Find(packet.Sender);
            if (peer == null)
            {
                return;
            }
            peer.LastSeen = now;
            // Only the authority's view of the world is forwarded; anything
            // else would let a client overwrite everyone's state.
            if (peer != _authority)
            {
                return;
            }
            _lastSnapshot = packet.Payload.ToArray();
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i] != peer)
                {
                    _transport?.Send(_peers[i].EndPoint, PacketType.Snapshot, packet.Payload);
                }
            }
        }

        private void HandleBye(ReceivedPacket packet)
        {
            Peer? peer = Find(packet.Sender);
            if (peer != null)
            {
                Remove(peer, "left");
            }
        }

        private void DropTimedOut(double now)
        {
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                if (now - _peers[i].LastSeen > NetConfig.TimeoutSeconds)
                {
                    Remove(_peers[i], "timed out");
                }
            }
        }

        private void Remove(Peer peer, string reason)
        {
            _peers.Remove(peer);
            BroadcastRoster();
            Log($"{peer.EndPoint} {reason} (slot {peer.SlotIndex})");
            if (peer.Name.Length > 0)
            {
                Announce($"{peer.Name} {reason}");
            }
            if (_authority != peer)
            {
                return;
            }
            // Promote rather than end the session: the remaining players keep
            // playing, and the new authority's snapshots simply take over.
            _authority = _peers.Count > 0 ? _peers[0] : null;
            Log(_authority != null
                ? $"authority moved to slot {_authority.SlotIndex}"
                : "no peers left; waiting for a new authority");
            if (_authority != null)
            {
                NotifyAuthority(_authority);
            }
        }

        private Peer? Find(IPEndPoint endPoint)
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i].EndPoint.Equals(endPoint))
                {
                    return _peers[i];
                }
            }
            return null;
        }

        private bool SlotFree(int slot)
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i].SlotIndex == slot)
                {
                    return false;
                }
            }
            return true;
        }

        private int NextFreeSlot()
        {
            for (int slot = 0; slot < _maxPlayers; slot++)
            {
                bool taken = false;
                for (int i = 0; i < _peers.Count; i++)
                {
                    if (_peers[i].SlotIndex == slot)
                    {
                        taken = true;
                        break;
                    }
                }
                if (!taken)
                {
                    return slot;
                }
            }
            return -1;
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [server] {message}");
        }
    }
}
