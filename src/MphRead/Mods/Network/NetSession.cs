using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    public enum NetRole
    {
        Offline,
        Host,
        Client
    }

    internal sealed class RemotePeer
    {
        public IPEndPoint EndPoint = null!;
        public int SlotIndex = -1;
        public IntentPacket LatestIntent;
        public uint LastIntentFrame;
        public double LastSeenTime;
        /// <summary>Who this peer says it is, across address changes. See
        /// <see cref="NetSession.ClientId"/>. Zero from an older client.</summary>
        public uint ClientId;
    }

    /// <summary>
    /// Session state and per-frame network step.
    ///
    /// Host authority, not lockstep: the simulation runs in float (Fixed
    /// only converts the ROM's 20.12 values on load), so two machines
    /// stepping the same inputs are not guaranteed to stay bit-identical.
    /// The host therefore owns the simulation and clients apply what it
    /// sends. Divergence becomes a correction rather than a desync.
    /// </summary>
    public static class NetSession
    {
        private static NetTransport? _transport;
        private static readonly List<RemotePeer> _peers = new();
        private static IPEndPoint? _hostEndPoint;
        private static readonly byte[] _scratch = new byte[NetConfig.MaxPacketSize];

        public static NetRole Role { get; private set; } = NetRole.Offline;
        public static bool Active => Role != NetRole.Offline;
        public static bool IsHost => Role == NetRole.Host;
        public static bool IsClient => Role == NetRole.Client;
        public static int LocalSlot { get; private set; } = 0;
        public static uint NetFrame { get; private set; }
        public static uint LastSnapshotFrame => _lastSnapshotFrame;
        public static string? LastError { get; private set; }

        /// <summary>Latest authoritative state per slot, applied by clients.</summary>
        public static readonly PlayerState[] RemoteStates = new PlayerState[PlayerEntity.SlotCapacity];
        public static readonly bool[] RemoteStateValid = new bool[PlayerEntity.SlotCapacity];

        /// <summary>Latest intent per slot, consumed by the host's input step.</summary>
        public static readonly IntentPacket[] RemoteIntents = new IntentPacket[PlayerEntity.SlotCapacity];
        public static readonly bool[] RemoteIntentValid = new bool[PlayerEntity.SlotCapacity];

        /// <summary>
        /// The local frame each slot's intent last arrived on, so a receiver
        /// can tell a current one from one that stopped coming.
        ///
        /// The intent carries where its owner says they are, and every client
        /// pins that slot's puppet there (ApplyReportedPosition). When a
        /// player's line goes away the intents stop, the last one stays in
        /// the array, and the pin keeps pulling the puppet back to where they
        /// were while the authority's snapshots -- which do not stop -- keep
        /// moving it. On everyone's screen the missing player strobes between
        /// two places at 60 Hz for as long as the outage lasts: 1600 to 2176
        /// position snaps per client in a 200 s run with 52 s of cuts in it,
        /// against zero in the same run with no cuts.
        /// </summary>
        public static readonly uint[] RemoteIntentArrived = new uint[PlayerEntity.SlotCapacity];

        /// <summary>
        /// How long ago, in frames, this slot's owner last said anything.
        /// <see cref="UInt32.MaxValue"/> when they never have.
        /// </summary>
        public static uint RemoteIntentAge(int slot)
        {
            if (slot < 0 || slot >= RemoteIntentArrived.Length || RemoteIntentArrived[slot] == 0)
            {
                return UInt32.MaxValue;
            }
            return NetFrame >= RemoteIntentArrived[slot]
                ? NetFrame - RemoteIntentArrived[slot]
                : 0;
        }

        /// <summary>
        /// Counters the log reads to tell "nothing arrived" from "it arrived
        /// and was ignored". Two clients that are demonstrably exchanging
        /// packets can still each hold a scene containing only themselves,
        /// and only the difference between these two numbers says which half
        /// of the path is at fault.
        /// </summary>
        public static long SnapshotsReceived { get; private set; }
        public static long SnapshotsSent { get; private set; }
        public static long StatesApplied { get; private set; }
        public static long IntentsReceived { get; private set; }

        public static void NoteStatesApplied() => StatesApplied++;

        public static void StartHost(int port = NetConfig.DefaultPort)
        {
            Stop();
            try
            {
                _transport = new NetTransport(port);
                Role = NetRole.Host;
                LocalSlot = 0;
                NetFrame = 0;
                LastError = null;
                Console.WriteLine($"[net] hosting on UDP {_transport.LocalPort}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[net] host failed: {ex.Message}");
                Role = NetRole.Offline;
            }
        }

        public static void StartClient(string address, int port = NetConfig.DefaultPort)
        {
            Stop();
            try
            {
                _transport = new NetTransport(0);
                // The server measures everyone's round trip by pinging them,
                // so the reply must not wait for a frame boundary: see
                // NetTransport.AnswerPingsImmediately.
                _transport.AnswerPingsImmediately();
                // Resolve rather than Parse: IPAddress.Parse only accepts a
                // literal, so a hostname threw here and the join silently
                // failed -- the session stayed offline while the launcher
                // reported nothing wrong.
                _hostEndPoint = new IPEndPoint(ResolveIPv4(address), port);
                Role = NetRole.Client;
                LocalSlot = -1; // assigned by the host's Welcome
                NetFrame = 0;
                LastError = null;
                NetLog.Open(PlayerName);
                NetLog.Event($"joining {address}:{port} as \"{PlayerName}\"");
                SendHello();
                SendIdentify();
                Console.WriteLine($"[net] joining {address}:{port} as \"{PlayerName}\"");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[net] join failed: {ex.Message}");
                Role = NetRole.Offline;
            }
        }

        /// <summary>
        /// A session that never actually talks to anyone: fed only from a
        /// recorded demo file, played back through <see cref="DemoPlayback"/>.
        ///
        /// Deliberately not <see cref="StartClient"/> minus the address --
        /// <see cref="_hostEndPoint"/> stays null for the whole session,
        /// which is what keeps every send in this class a no-op (each one
        /// already guards on it), and <see cref="LocalSlot"/> stays -1
        /// forever instead of resolving from a Welcome packet, since a demo
        /// only ever starts recording after the real join already happened.
        /// </summary>
        public static void StartPlayback()
        {
            Stop();
            _transport = new NetTransport(0);
            Role = NetRole.Client;
            LocalSlot = -1;
            NetFrame = 0;
            LastError = null;
        }

        /// <summary>
        /// Forget what has already been seen, because the demo is about to be
        /// played again from its first frame.
        ///
        /// <see cref="DemoPlayback.Join"/> reads the opening of the file to
        /// find out what room to load, and then rewinds so that opening is
        /// actually watched rather than spent. Everything learned along the
        /// way is kept -- the match state, the roster, who is in which slot
        /// and as which hunter, all of which the scene is about to be built
        /// from. What has to go is the bookkeeping that says "I have seen
        /// newer than this": the ordering guards on the snapshot and intent
        /// streams would otherwise refuse every rewound packet as stale,
        /// which is a worse version of the problem the rewind is fixing.
        /// </summary>
        public static void RewindPlayback()
        {
            NetUnlagged.Reset();
            _lastSnapshotFrame = 0;
            Array.Clear(_lastSlotIntentFrame);
            Array.Clear(RemoteStateValid);
            Array.Clear(RemoteIntentValid);
            SnapshotsReceived = 0;
            SnapshotsSent = 0;
            SnapshotsOutOfOrder = 0;
            StatesApplied = 0;
            IntentsReceived = 0;
            IntentsOutOfOrder = 0;
            NetPlayerBridge.Reset();
            NetDamage.Reset();
        }

        /// <summary>Hands a packet read back from a demo file to this session as if it had just arrived.</summary>
        public static void InjectPlaybackPacket(byte[] data, int length)
        {
            _transport?.EnqueueForPlayback(data, length);
        }

        /// <summary>
        /// Turn a hostname or literal address into an IPv4 endpoint address.
        /// IPv4 specifically: the transport binds an InterNetwork socket, so
        /// handing it a v6 address would fail at send time instead of here.
        /// </summary>
        private static IPAddress ResolveIPv4(string address)
        {
            if (IPAddress.TryParse(address, out IPAddress? literal)
                && literal.AddressFamily == AddressFamily.InterNetwork)
            {
                return literal;
            }
            IPAddress[] resolved = Dns.GetHostAddresses(address);
            foreach (IPAddress candidate in resolved)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException($"{address} has no IPv4 address");
        }

        public static void Stop()
        {
            NetPlayerSetup.Reset();
            SpectatorMode.Reset();
            DemoRecorder.Stop();
            NetMatchSync.Reset();
            NetSlotManager.Reset();
            NetDamage.Reset();
            NetRoomChange.Reset();
            NetMatchEnd.Reset();
            NetPlayerBridge.Reset();
            Chat.ChatBox.Clear();
            IsAuthority = false;
            _authorityNeedsStateApply = false;
            if (_transport != null)
            {
                if (Role == NetRole.Client && _hostEndPoint != null)
                {
                    _transport.Send(_hostEndPoint, PacketType.Bye, ReadOnlySpan<byte>.Empty);
                }
                _transport.Dispose();
                _transport = null;
            }
            _peers.Clear();
            _hostEndPoint = null;
            Role = NetRole.Offline;
            // Whatever the last server was being asked, it was not this one's
            // question: a vote left standing here would draw a prompt over
            // the next match.
            MapVote.Reset();
            ConnectionLost = false;
            LocalSlot = 0;
            Array.Clear(RemoteStateValid);
            Array.Clear(RemoteIntentValid);
            Array.Clear(RemoteIntentArrived);
            Array.Clear(SlotPing);
            Array.Clear(_lastSlotIntentFrame);
            _lastServerPacket = 0;
            ReAnnouncements = 0;
            LongestServerSilence = 0;
            AuthorityStandDowns = 0;
            AuthorityFrames = 0;
            Refused = false;
            SnapshotStreamResets = 0;
            _lateSnapshotRun = 0;
            _reAnnounced = false;
            Array.Clear(SlotOccupied);
            SnapshotsReceived = 0;
            SnapshotsSent = 0;
            SnapshotsOutOfOrder = 0;
            IntentsOutOfOrder = 0;
            _lastSnapshotFrame = 0;
            StatesApplied = 0;
            IntentsReceived = 0;
            ServerMatch = null;
            // The position history is indexed by frame, and the frame counter
            // restarts here. Keeping it would let a rewind land on a cell
            // stamped with the same number from the previous match, which is
            // a shot resolved against a room nobody is standing in.
            NetUnlagged.Reset();
        }

        /// <summary>
        /// Tell the server who we are: display name, hunter and suit colour.
        /// The two bytes lead so the name stays a plain trailing string, which
        /// is what the server reads it as.
        ///
        /// Sent again whenever any of the three changes, not only at join: a
        /// player who picks a different hunter to respawn as has changed the
        /// answer to "who is in this slot", and everybody else learns it from
        /// the roster this produces.
        /// </summary>
        public static void SendIdentify()
        {
            if (_transport == null || _hostEndPoint == null)
            {
                return;
            }
            byte[] name = System.Text.Encoding.ASCII.GetBytes(PlayerName);
            int count = Math.Min(name.Length, RosterPacket.MaxNameBytes);
            _scratch[0] = (byte)LocalHunter;
            _scratch[1] = (byte)PlayerColors.Clamp(LocalColor);
            name.AsSpan(0, count).CopyTo(_scratch.AsSpan(2));
            _transport.Send(_hostEndPoint, PacketType.Identify, _scratch.AsSpan(0, count + 2));
        }

        /// <summary>The hunter this machine plays, announced in Identify.</summary>
        public static Hunter LocalHunter { get; set; } = Hunter.Samus;

        /// <summary>
        /// The suit this machine asked for, 0-3, announced with the hunter.
        /// What is actually drawn is <see cref="PlayerColors.Resolve"/>'s
        /// answer, which may move it to keep two players of one hunter apart.
        /// </summary>
        public static int LocalColor { get; set; }

        /// <summary>
        /// Which hunter each slot is playing, per the server's roster. A
        /// client that assumed its own choice for everybody drew the other
        /// player as the wrong character -- correct position, correct name,
        /// wrong model.
        /// </summary>
        public static readonly Hunter[] SlotHunter = new Hunter[PlayerEntity.SlotCapacity];

        /// <summary>
        /// Round trip to the server per slot, in milliseconds, as the server
        /// measured it. Zero means "not measured yet", which the scoreboard
        /// draws as a dash rather than as a suspiciously perfect connection.
        ///
        /// Measured by the server rather than by each client because clients
        /// never exchange packets with each other: a client can time its own
        /// round trip and nobody else's, and a scoreboard that showed one real
        /// number and five zeroes would be worse than none.
        /// </summary>
        public static readonly int[] SlotPing = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// Who this client is, for as long as the program runs.
        ///
        /// A server tells its peers apart by the address a datagram came
        /// from, and that is not stable across a dropped connection: a line
        /// that comes back comes back through a new NAT binding, so the same
        /// player says hello from a source port the server has never seen.
        /// Every one of those looked like somebody new arriving -- a second
        /// slot, a second hunter -- while the slot the player actually had
        /// sat there receiving nothing until it timed out thirty seconds
        /// later. That is the frozen twin standing in the room.
        ///
        /// So the client says who it is as well as where it is, and the
        /// server matches on this first. Random per process rather than
        /// derived from anything: it has to survive a reconnection and must
        /// not survive the program, or two people sharing a settings file
        /// would be one player.
        /// </summary>
        public static readonly uint ClientId = NewClientId();

        private static uint NewClientId()
        {
            Span<byte> bytes = stackalloc byte[4];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            // Zero means "did not say", which is what an older client sends.
            return id == 0 ? 1u : id;
        }

        private static void SendHello()
        {
            if (_transport == null || _hostEndPoint == null)
            {
                return;
            }
            _scratch[0] = NetConfig.ProtocolVersion;
            // Ask for the slot we already hold. A reconnection is normally a
            // client the server forgot while it was loading, and coming back
            // as a different player would swap two people's scores, names and
            // hunters mid-match.
            _scratch[1] = LocalSlot >= 0 && LocalSlot < 0xFF ? (byte)LocalSlot : (byte)0xFF;
            // Appended rather than inserted: a server built before this
            // reads the first two bytes and ignores the rest, so a new
            // client still joins an old server -- it simply gets the old
            // behaviour when its connection drops.
            BinaryPrimitives.WriteUInt32LittleEndian(_scratch.AsSpan(2, 4), ClientId);
            _transport.Send(_hostEndPoint, PacketType.Hello, _scratch.AsSpan(0, 6));
        }

        /// <summary>
        /// Throw this client's socket away and open another, keeping
        /// everything else -- the slot, the identity, the match.
        ///
        /// What a dropped connection does to a client that comes back: the
        /// address the server knows it by is gone and the packets now arrive
        /// from a port nobody has seen. It is the whole reason
        /// <see cref="ClientId"/> exists, and it cannot be provoked from a
        /// test machine any other way, so the harness can ask for it
        /// (MPHREAD_NET_REBIND=seconds).
        /// </summary>
        public static void RebindSocket()
        {
            if (Role != NetRole.Client || _transport == null)
            {
                return;
            }
            int wasPort = _transport.LocalPort;
            _transport.Dispose();
            _transport = new NetTransport(0);
            Console.WriteLine($"[net] rebound the socket: {wasPort} -> {_transport.LocalPort}");
            SendHello();
            SendIdentify();
        }

        /// <summary>
        /// Pump the network once per simulation frame. Call before input is
        /// sampled so a remote intent that arrived this frame is visible to
        /// the input step that follows.
        /// </summary>
        public static void Update(double time)
        {
            if (_transport == null)
            {
                return;
            }
            NetFrame++;
            if (IsAuthority)
            {
                AuthorityFrames++;
            }
            foreach (ReceivedPacket packet in _transport.Drain())
            {
                DemoRecorder.Record(packet);
                Handle(packet, time);
            }
            if (Role == NetRole.Host)
            {
                DropTimedOutPeers(time);
            }
            else if (Role == NetRole.Client && LocalSlot < 0 && NetFrame % 60 == 0)
            {
                SendHello(); // still waiting to be admitted
            }
            else if (Role == NetRole.Client && NetFrame % 60 == 0
                && time - _lastServerPacket > SilenceBeforeRejoin)
            {
                // The server has not said anything for a long time, which
                // means it has forgotten us -- dropped while a room was
                // loading, or restarted. Saying hello again re-registers this
                // endpoint, and since the slot we held is free by then, we
                // normally get it straight back. Without this a client that
                // was dropped once kept playing alone forever, sending
                // packets to a server that ignored every one of them.
                ReAnnouncements++;
                _reAnnounced = true;
                // Say so on screen, once per outage rather than once per
                // attempt. A player whose line has gone sees a match that has
                // stopped moving and no reason for it -- and, before the peer
                // identity fix above, a second hunter walking out of their own
                // frozen body a moment later. Saying "this is the network, we
                // are still trying" is the difference between a bug and a
                // wait.
                if (!ConnectionLost)
                {
                    ConnectionLost = true;
                    Chat.ChatBox.System("Connection lost, retrying...");
                }
                Console.WriteLine("[net] no word from the server; re-announcing "
                    + $"(#{ReAnnouncements}, silent for {time - _lastServerPacket:0.0} s)");
                NetLog.Event("server silent, re-announcing");
                SendHello();
                SendIdentify();
            }
            else if (Role == NetRole.Client && NetFrame % 120 == 0
                && LocalSlot >= 0 && LocalSlot < GameState.Nicknames.Length
                && GameState.Nicknames[LocalSlot] != PlayerName)
            {
                // The roster still has a placeholder for this slot, so the
                // Identify that went out with the join was lost. Nothing else
                // resends it, and a client whose name never landed shows up as
                // "PlayerN" on every other scoreboard for the whole match.
                SendIdentify();
            }
        }

        /// <summary>
        /// The server has stopped answering and this client is still trying.
        ///
        /// Read by the HUD, and cleared by the first packet that arrives from
        /// the server again -- whatever it is, since anything arriving means
        /// the line is back.
        /// </summary>
        public static bool ConnectionLost { get; private set; }

        /// <summary>Seconds of silence from the server before saying hello again.</summary>
        private const double SilenceBeforeRejoin = 5.0;

        private static double _lastServerPacket;

        /// <summary>
        /// How many times this client found the server silent long enough to
        /// re-announce itself. Zero on a healthy connection; one per outage
        /// on a line that comes and goes, which is the number a report about
        /// a dropped connection should carry instead of a grep for a console
        /// line.
        /// </summary>
        public static int ReAnnouncements { get; private set; }

        /// <summary>
        /// The longest the server went without a word, in seconds. The
        /// measurement a player's "it froze for a moment" is actually about.
        /// </summary>
        public static double LongestServerSilence { get; private set; }

        /// <summary>
        /// How many times this client gave the simulation back on being
        /// re-admitted. Non-zero means it was out of touch long enough for the
        /// server to have moved the authority.
        /// </summary>
        public static int AuthorityStandDowns { get; private set; }

        /// <summary>Set when a Hello goes out because the server had gone quiet.</summary>
        private static bool _reAnnounced;

        /// <summary>
        /// Frames this client has spent believing it runs the simulation.
        /// Printed beside the snap count because the two only make sense
        /// together: snaps are counted on the authority and nowhere else, so
        /// a client reporting a thousand of them while never having been the
        /// authority is a contradiction worth seeing rather than reasoning
        /// about.
        /// </summary>
        public static long AuthorityFrames { get; private set; }

        /// <summary>The server said no, in as many words. See <see cref="RefusedPacket"/>.</summary>
        public static bool Refused { get; private set; }

        /// <summary>What it said. Only meaningful while <see cref="Refused"/>.</summary>
        public static RefusedPacket RefusedReason { get; private set; }

        private static void Handle(ReceivedPacket packet, double time)
        {
            if (Role == NetRole.Client)
            {
                if (_lastServerPacket > 0 && time > _lastServerPacket)
                {
                    LongestServerSilence = Math.Max(LongestServerSilence,
                        time - _lastServerPacket);
                }
                _lastServerPacket = time;
                if (ConnectionLost)
                {
                    ConnectionLost = false;
                    Chat.ChatBox.System("Reconnected.");
                }
            }
            switch (packet.Type)
            {
                case PacketType.Hello when Role == NetRole.Host:
                    HandleHello(packet, time);
                    break;
                case PacketType.Welcome when Role == NetRole.Client:
                    if (_reAnnounced)
                    {
                        // Re-admitted after the server had stopped talking to
                        // us. While we were away it may have given the
                        // simulation to somebody else -- it promotes the next
                        // peer the moment it drops one (DedicatedServer.Remove)
                        // -- and it has no way to say so: PacketType.Authority
                        // only ever promotes. So stand down here and wait to be
                        // told again; the server re-sends Authority to whoever
                        // holds it once a second, so a client that really is
                        // still the authority has it back within one.
                        //
                        // Without this, an authority whose line dropped for
                        // longer than the server's timeout came back believing
                        // it still ran the match: it ignored every snapshot it
                        // received, its own were dropped by the server as
                        // coming from a non-authority, and it played on in a
                        // private copy of the match that looked entirely
                        // healthy from inside. Found by cutting the
                        // authority's line for 40 s against the Pi.
                        _reAnnounced = false;
                        if (IsAuthority)
                        {
                            IsAuthority = false;
                            AuthorityStandDowns++;
                            Console.WriteLine("[net] re-admitted; standing down as the "
                                + "simulation authority until the server says otherwise");
                            NetLog.Event("re-admitted, authority relinquished");
                        }
                    }
                    if (packet.Payload.Length >= 1)
                    {
                        int assigned = packet.Payload[0];
                        // A different slot from the one we were playing is
                        // the server having failed to recognise us -- an
                        // older server, which cannot match a reconnection to
                        // the peer that made it. The player we were is still
                        // standing in the room from everybody's point of
                        // view, and the one thing that must not happen is
                        // this machine driving a second one beside it. So the
                        // slot we left is emptied here rather than waited
                        // out: NetSlotManager builds the player for whatever
                        // LocalSlot says, and two live players from one
                        // client is the frozen twin.
                        if (LocalSlot >= 0 && assigned != LocalSlot)
                        {
                            Console.WriteLine($"[net] came back as slot {assigned}, "
                                + $"was slot {LocalSlot}; releasing the old one");
                            NetLog.Event($"reconnected into slot {assigned}, was {LocalSlot}");
                            NetSlotManager.ReleaseSlot(LocalSlot);
                        }
                        LocalSlot = assigned;
                        Console.WriteLine($"[net] joined as slot {LocalSlot}");
                        NetLog.Event($"server assigned slot {LocalSlot}");
                    }
                    break;
                case PacketType.Intent when Role == NetRole.Host:
                    HandleIntent(packet, time);
                    break;
                case PacketType.SlotIntent when Role == NetRole.Client:
                    HandleSlotIntent(packet);
                    break;
                case PacketType.Snapshot when Role == NetRole.Client:
                    HandleSnapshot(packet);
                    break;
                case PacketType.Authority when Role == NetRole.Client:
                    if (!IsAuthority)
                    {
                        IsAuthority = true;
                        _authorityNeedsStateApply = true;
                        Console.WriteLine("[net] this client is now the simulation authority");
                        NetLog.Event("became the simulation authority");
                    }
                    break;
                case PacketType.Refused when Role == NetRole.Client:
                    if (packet.Payload.Length >= 1 && LocalSlot < 0)
                    {
                        // Only while still waiting to be let in. A refusal
                        // arriving mid-match would be a stale datagram from
                        // the join, and acting on one of those would throw a
                        // player out of a match they are already in.
                        RefusedReason = RefusedPacket.Read(packet.Payload);
                        Refused = true;
                    }
                    break;
                case PacketType.Roster when Role == NetRole.Client:
                    HandleRoster(packet);
                    break;
                case PacketType.Ping when Role == NetRole.Client:
                    // Normally answered on the transport's own thread before
                    // it ever reaches here, which is what keeps the reported
                    // ping a measurement of the network rather than of this
                    // machine's frame rate. Kept as the fallback for a
                    // transport that was not asked to.
                    if (_hostEndPoint != null)
                    {
                        _transport?.Send(_hostEndPoint, PacketType.Pong, packet.Payload);
                    }
                    break;
                case PacketType.MatchState when Role == NetRole.Client:
                case PacketType.MapChange when Role == NetRole.Client:
                    HandleMatchState(packet, packet.Type == PacketType.MapChange);
                    break;
                case PacketType.Chat:
                    HandleChat(packet, time);
                    break;
                case PacketType.VoteState when Role == NetRole.Client:
                    if (packet.Payload.Length >= VoteStatePacket.Size)
                    {
                        MapVote.Apply(VoteStatePacket.Read(packet.Payload));
                    }
                    break;
                case PacketType.Bye:
                    HandleBye(packet);
                    break;
            }
        }

        /// <summary>
        /// One line somebody typed, as the server passed it on.
        ///
        /// Not guarded by role, unlike almost everything above it: a listen
        /// host receives these from its own peers and has to hand them round,
        /// and a client receives them from the server and only has to read
        /// them. The server is the one that decides whose name goes on a line
        /// (see <see cref="ChatPacket"/>), so nothing here re-checks it --
        /// but a *host* is the server for its peers, so it does.
        /// </summary>
        private static void HandleChat(ReceivedPacket packet, double time)
        {
            if (packet.Payload.Length < ChatPacket.Size)
            {
                return;
            }
            ChatPacket chat = ChatPacket.Read(packet.Payload);
            if (chat.Text.Length == 0)
            {
                return;
            }
            if (Role == NetRole.Host)
            {
                RemotePeer? peer = FindPeer(packet.Sender);
                if (peer == null || peer.SlotIndex < 0)
                {
                    return;
                }
                peer.LastSeenTime = time;
                // The sender's own claim about who it is, replaced with what
                // this host knows. Same rule the dedicated server applies.
                chat.Slot = (byte)peer.SlotIndex;
                if (peer.SlotIndex < GameState.Nicknames.Length
                    && !String.IsNullOrEmpty(GameState.Nicknames[peer.SlotIndex]))
                {
                    chat.Name = GameState.Nicknames[peer.SlotIndex];
                }
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
            }
            Chat.ChatBox.Receive(chat);
        }

        /// <summary>
        /// Put a line on the wire. The server stamps it with this client's
        /// real slot and name before anyone else sees it, so what goes in
        /// these two fields only matters to a demo recorded here.
        /// </summary>
        public static void SendChat(string text)
        {
            if (_transport == null || String.IsNullOrWhiteSpace(text))
            {
                return;
            }
            var chat = new ChatPacket
            {
                Slot = (byte)Math.Max(LocalSlot, 0),
                Kind = ChatPacket.KindSay,
                Name = PlayerName,
                Text = text
            };
            chat.Write(_scratch);
            if (Role == NetRole.Host)
            {
                // Nobody upstream to send to: a host is the server. Straight
                // out to the peers, exactly as the relay above would.
                for (int i = 0; i < _peers.Count; i++)
                {
                    _transport.Send(_peers[i].EndPoint, PacketType.Chat,
                        _scratch.AsSpan(0, ChatPacket.Size));
                }
                return;
            }
            if (_hostEndPoint != null)
            {
                _transport.Send(_hostEndPoint, PacketType.Chat,
                    _scratch.AsSpan(0, ChatPacket.Size));
            }
        }

        /// <summary>
        /// A proposal or a ballot, upstream.
        ///
        /// Client only. A listen host is its own server and its
        /// <see cref="DedicatedServer"/> is in the same process, but nothing
        /// routes a vote to it yet -- and a vote of one player, held by that
        /// player, is not a vote. So this sends where there is somebody to
        /// send to and does nothing otherwise, rather than pretending.
        /// </summary>
        public static void SendVote(byte kind, string roomKey)
        {
            if (_transport == null || _hostEndPoint == null || Role != NetRole.Client)
            {
                return;
            }
            var vote = new VotePacket { Kind = kind, RoomKey = roomKey ?? "" };
            vote.Write(_scratch);
            _transport.Send(_hostEndPoint, PacketType.Vote,
                _scratch.AsSpan(0, VotePacket.Size));
        }

        private static void HandleHello(ReceivedPacket packet, double time)
        {
            if (packet.Payload.Length < 1 || packet.Payload[0] != NetConfig.ProtocolVersion)
            {
                return;
            }
            uint clientId = packet.Payload.Length >= 6
                ? BinaryPrimitives.ReadUInt32LittleEndian(packet.Payload.Slice(2, 4))
                : 0;
            RemotePeer? peer = FindPeer(packet.Sender);
            if (peer == null && clientId != 0)
            {
                // The same player from a new address. See NetSession.ClientId.
                for (int i = 0; i < _peers.Count; i++)
                {
                    if (_peers[i].ClientId == clientId)
                    {
                        peer = _peers[i];
                        Console.WriteLine($"[net] slot {peer.SlotIndex} came back on "
                            + $"{packet.Sender} (was {peer.EndPoint})");
                        peer.EndPoint = packet.Sender;
                        break;
                    }
                }
            }
            if (peer == null)
            {
                int slot = NextFreeSlot();
                if (slot < 0)
                {
                    return; // session full
                }
                peer = new RemotePeer
                {
                    EndPoint = packet.Sender,
                    SlotIndex = slot
                };
                _peers.Add(peer);
                Console.WriteLine($"[net] peer {packet.Sender} -> slot {slot}");
            }
            peer.ClientId = clientId;
            peer.LastSeenTime = time;
            // Re-answered on every Hello: the first Welcome may have been lost.
            _scratch[0] = (byte)peer.SlotIndex;
            _transport!.Send(peer.EndPoint, PacketType.Welcome, _scratch.AsSpan(0, 1));
        }

        private static void HandleIntent(ReceivedPacket packet, double time)
        {
            if (packet.Payload.Length < IntentPacket.Size)
            {
                return;
            }
            RemotePeer? peer = FindPeer(packet.Sender);
            if (peer == null || peer.SlotIndex < 0)
            {
                return;
            }
            IntentPacket intent = IntentPacket.Read(packet.Payload);
            // UDP reorders; an older frame must not overwrite a newer one --
            // unless it is so much older that the peer restarted its counter.
            // See HandleSlotIntent.
            if (peer.LastIntentFrame != 0 && intent.Frame <= peer.LastIntentFrame
                && peer.LastIntentFrame - intent.Frame < IntentResetGap)
            {
                return;
            }
            peer.LastIntentFrame = intent.Frame;
            peer.LatestIntent = intent;
            peer.LastSeenTime = time;
            RemoteIntents[peer.SlotIndex] = intent;
            RemoteIntentValid[peer.SlotIndex] = true;
        }

        /// <summary>
        /// A peer's input, relayed by the server and tagged with its slot.
        ///
        /// Every client gets these, not only the authority. The authority
        /// needs them to simulate the match; everyone else needs them because
        /// a player's input is what makes it do anything a position cannot
        /// express -- firing, morphing, laying a bomb, an alt attack. Clients
        /// that had only positions drew opponents gliding around in silence.
        /// The authority still owns where everyone ends up: its snapshot
        /// corrects whatever the local simulation of that input produced.
        /// </summary>
        private static void HandleSlotIntent(ReceivedPacket packet)
        {
            if (packet.Payload.Length < 1 + IntentPacket.Size)
            {
                return;
            }
            int slot = packet.Payload[0];
            if (slot < 0 || slot >= RemoteIntents.Length || slot == LocalSlot)
            {
                return;
            }
            IntentPacket intent = IntentPacket.Read(packet.Payload[1..]);
            // UDP reorders; an older frame must not overwrite a newer one.
            //
            // "Older", though, means older than what this peer was sending a
            // moment ago -- not older than what the peer who held this slot
            // before them was sending. A client's frame counter starts at
            // zero on NetSession.StartClient, so somebody rejoining a match
            // they had been playing for five minutes comes back numbering
            // from 1 while this array still holds 18000, and every intent
            // they send is refused for the next five minutes: they are drawn
            // wherever they were standing when they left, their aim and their
            // trigger never arrive, and on the authority -- which is the only
            // machine whose shots count -- they can neither hit nor be hit
            // where anyone can see them. The same gap the snapshot stream
            // has: below it this is a reordered straggler, above it the
            // counter has restarted and the newcomer is who to believe.
            if (_lastSlotIntentFrame[slot] != 0 && intent.Frame <= _lastSlotIntentFrame[slot]
                && _lastSlotIntentFrame[slot] - intent.Frame < IntentResetGap)
            {
                IntentsOutOfOrder++;
                return;
            }
            _lastSlotIntentFrame[slot] = intent.Frame;
            RemoteIntents[slot] = intent;
            RemoteIntentValid[slot] = true;
            RemoteIntentArrived[slot] = Math.Max(NetFrame, 1);
            IntentsReceived++;
        }

        private static readonly uint[] _lastSlotIntentFrame = new uint[PlayerEntity.SlotCapacity];

        /// <summary>
        /// How far behind the newest intent a packet may be and still be
        /// treated as a reordered straggler rather than a peer whose counter
        /// has restarted. The same ten seconds at sixty frames the snapshot
        /// stream allows: reordering is a matter of milliseconds, so anything
        /// this far back is a different session.
        /// </summary>
        private const uint IntentResetGap = 600;

        /// <summary>Relayed intents thrown away as out of order, for the report.</summary>
        public static long IntentsOutOfOrder { get; private set; }

        /// <summary>
        /// Forget everything this session keeps for one slot, because
        /// somebody else is in it now.
        ///
        /// The per-slot state <see cref="NetPlayerBridge.ForgetSlot"/> clears
        /// is the simulation's; this is the wire's, and it was not cleared
        /// anywhere. A vacated slot kept its last occupant's intent flagged
        /// valid, so the authority went on placing, aiming and firing a
        /// player who had left -- and kept their frame counter, which is what
        /// refused the next occupant's packets.
        /// </summary>
        public static void ForgetSlot(int slot)
        {
            if (slot < 0 || slot >= PlayerEntity.SlotCapacity)
            {
                return;
            }
            _lastSlotIntentFrame[slot] = 0;
            RemoteIntentValid[slot] = false;
            RemoteIntents[slot] = default;
            RemoteStateValid[slot] = false;
            RemoteStates[slot] = default;
            for (int i = 0; i < _peers.Count; i++)
            {
                if (_peers[i].SlotIndex == slot)
                {
                    _peers[i].LastIntentFrame = 0;
                }
            }
        }

        /// <summary>
        /// The running match, as last reported by a dedicated server. Null
        /// when hosting or offline, where there is no server clock to follow.
        /// </summary>
        public static MatchStatePacket? ServerMatch { get; private set; }

        /// <summary>
        /// True when a dedicated server has designated this client as the
        /// simulation authority. On a dedicated server every peer is
        /// NetRole.Client, so without this nothing would ever broadcast
        /// snapshots and no player would see another move.
        /// </summary>
        public static bool IsAuthority { get; private set; }
        private static bool _authorityNeedsStateApply;

        public static bool ConsumeAuthorityStateSync()
        {
            if (!_authorityNeedsStateApply)
            {
                return false;
            }
            _authorityNeedsStateApply = false;
            return true;
        }

        /// <summary>How many peers the server last reported, including us.</summary>
        public static int ServerPlayerCount => ServerMatch?.PlayerCount ?? 0;

        /// <summary>Display name sent to the server on join.</summary>
        public static string PlayerName { get; set; } = "Player";

        /// <summary>Raised when the server rotates to a different map.</summary>
        public static event Action<MatchStatePacket>? MapChanged;

        /// <summary>Slots currently occupied by a real peer, per the server.</summary>
        public static readonly bool[] SlotOccupied = new bool[PlayerEntity.SlotCapacity];

        private static void HandleRoster(ReceivedPacket packet)
        {
            if (packet.Payload.Length < RosterPacket.Size)
            {
                return;
            }
            RosterPacket roster = RosterPacket.Read(packet.Payload);
            Array.Clear(SlotOccupied);
            for (int i = 0; i < roster.Count; i++)
            {
                int slot = roster.Slots[i];
                if (slot < 0 || slot >= SlotOccupied.Length)
                {
                    continue;
                }
                SlotOccupied[slot] = true;
                // Nicknames is what the scoreboard draws, so writing here is
                // what makes the other player's name appear on Tab.
                GameState.Nicknames[slot] = roster.Names[i];
                if (Enum.IsDefined(typeof(Hunter), roster.Hunters[i]))
                {
                    SlotHunter[slot] = (Hunter)roster.Hunters[i];
                }
                // What they asked for. PlayerColors decides what they get,
                // every frame, from every slot's answer at once.
                PlayerColors.Choice[slot] = PlayerColors.Clamp(roster.Colors[i]);
                SlotPing[slot] = roster.Pings[i];
            }
        }

        private static void HandleMatchState(ReceivedPacket packet, bool rotated)
        {
            if (packet.Payload.Length < MatchStatePacket.Size)
            {
                return;
            }
            MatchStatePacket state = MatchStatePacket.Read(packet.Payload);
            string? previous = ServerMatch?.RoomKey;
            ServerMatch = state;
            // Fire on an actual map change, whether the server announced it
            // as a rotation or the periodic state simply differs -- a joiner
            // arriving mid-match learns the map this same way.
            if (rotated || previous == null || previous != state.RoomKey)
            {
                Console.WriteLine($"[net] server map: {state.RoomKey} "
                    + $"({(GameMode)state.Mode}, {state.TimeRemaining:0} s left)");
                MapChanged?.Invoke(state);
            }
        }

        /// <summary>
        /// The newest snapshot frame this client has accepted. Snapshots are
        /// the one stream that was not ordered.
        /// </summary>
        private static uint _lastSnapshotFrame;

        /// <summary>
        /// How far behind the newest snapshot a packet may be and still be
        /// treated as a reordered straggler rather than a fresh start. Ten
        /// seconds at sixty frames: a client that has been away longer than
        /// that has been away long enough for the authority to have changed
        /// or the room to have reloaded.
        /// </summary>
        private const uint SnapshotResetGap = 600;

        /// <summary>
        /// How many consecutive "older than what I have" snapshots it takes
        /// before the stream is treated as a new source rather than as
        /// stragglers. A fifth of a second: longer than any reordering seen
        /// on a real path, shorter than a player would notice.
        /// </summary>
        private const int LateSnapshotsBeforeReset = 12;

        private static int _lateSnapshotRun;

        /// <summary>
        /// How many times this client re-based its snapshot ordering on a new
        /// source. One per authority handover is expected; a stream of them
        /// means two machines are publishing.
        /// </summary>
        public static int SnapshotStreamResets { get; private set; }

        /// <summary>Reordered snapshots thrown away, for the report.</summary>
        public static long SnapshotsOutOfOrder { get; private set; }

        private static void HandleSnapshot(ReceivedPacket packet)
        {
            ReadOnlySpan<byte> payload = packet.Payload;
            if (payload.Length < SnapshotHeader.Size)
            {
                return;
            }
            SnapshotHeader header = SnapshotHeader.Read(payload);
            // Both the intent streams already refuse an older frame; this one
            // did not, and it is the stream that carries health, score and the
            // damage counter. A datagram overtaken in flight therefore put a
            // player back where they had been, undid a kill on the scoreboard,
            // and -- worst of it -- ran the damage counter backwards, which the
            // replay reads as two hundred and fifty-odd new hits because the
            // counter is a byte. UDP reorders as a matter of course; a
            // snapshot arrives sixty times a second, so throwing away a late
            // one costs nothing at all.
            if (_lastSnapshotFrame != 0 && header.Frame <= _lastSnapshotFrame
                && _lastSnapshotFrame - header.Frame < SnapshotResetGap)
            {
                SnapshotsOutOfOrder++;
                // A straggler is a packet; this is a stream. When the server
                // moves the authority to another client, the snapshots start
                // coming from a machine whose own frame counter is its own --
                // typically a few seconds behind, because it joined a few
                // seconds later -- and every one of them looks late. Refusing
                // the lot freezes every puppet on every screen until the new
                // authority's counter climbs past the old one's: 159 and 169
                // consecutive refusals, about 2.7 s, measured on two clients
                // in one churn run against the Pi.
                //
                // Genuine reordering never lasts: a late datagram arrives
                // among packets that are not late, and each of those resets
                // this. A fifth of a second of nothing but "older" is a new
                // source, so take it and re-base on it.
                if (++_lateSnapshotRun < LateSnapshotsBeforeReset)
                {
                    return;
                }
                NetLog.Event($"snapshot stream re-based: {_lateSnapshotRun} in a row "
                    + $"older than {_lastSnapshotFrame} (now {header.Frame})");
                SnapshotStreamResets++;
            }
            _lateSnapshotRun = 0;
            _lastSnapshotFrame = header.Frame;
            SnapshotsReceived++;
            // Rng.cs reproduces the game's original LCG and its state is
            // global, so adopting the host's words keeps every random
            // consumer agreeing without replicating each one individually.
            Rng.SetRng1(header.Rng1);
            Rng.SetRng2(header.Rng2);
            int offset = SnapshotHeader.Size;
            Array.Clear(RemoteStateValid);
            for (int i = 0; i < header.PlayerCount; i++)
            {
                if (offset + PlayerState.Size > payload.Length)
                {
                    break;
                }
                PlayerState state = PlayerState.Read(payload[offset..]);
                offset += PlayerState.Size;
                if (state.SlotIndex < RemoteStates.Length)
                {
                    RemoteStates[state.SlotIndex] = state;
                    RemoteStateValid[state.SlotIndex] = true;
                }
            }
        }

        private static void HandleBye(ReceivedPacket packet)
        {
            if (Role == NetRole.Host)
            {
                RemotePeer? peer = FindPeer(packet.Sender);
                if (peer != null)
                {
                    Console.WriteLine($"[net] peer {peer.EndPoint} left (slot {peer.SlotIndex})");
                    RemoteIntentValid[peer.SlotIndex] = false;
                    _peers.Remove(peer);
                }
            }
            else
            {
                Console.WriteLine("[net] host closed the session");
                Stop();
            }
        }

        private static void DropTimedOutPeers(double time)
        {
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                RemotePeer peer = _peers[i];
                if (time - peer.LastSeenTime > NetConfig.TimeoutSeconds)
                {
                    Console.WriteLine($"[net] peer {peer.EndPoint} timed out (slot {peer.SlotIndex})");
                    RemoteIntentValid[peer.SlotIndex] = false;
                    _peers.RemoveAt(i);
                }
            }
        }

        private static RemotePeer? FindPeer(IPEndPoint endPoint)
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

        private static int NextFreeSlot()
        {
            for (int slot = 1; slot < PlayerEntity.MaxPlayers; slot++)
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

        /// <summary>Client -> host: this frame's intent for the local player.</summary>
        public static void SendIntent(IntentPacket intent)
        {
            if (_transport == null || Role != NetRole.Client || _hostEndPoint == null)
            {
                return;
            }
            intent.Frame = NetFrame;
            intent.Write(_scratch);
            _transport.Send(_hostEndPoint, PacketType.Intent, _scratch.AsSpan(0, IntentPacket.Size));
            // A demo only ever contains what this client *received* -- and
            // this client never receives its own SlotIntent back, since it
            // already knows what it pressed. Without this, playback shows
            // every remote player's shooting/morphing/alt-attack animation
            // correctly (their SlotIntent really was received and recorded)
            // and never this player's own, because nothing ever told it to.
            if (LocalSlot >= 0)
            {
                DemoRecorder.RecordOwnIntent(LocalSlot, _scratch.AsSpan(0, IntentPacket.Size));
            }
        }

        /// <summary>
        /// Authority -> server: the match this client is simulating is over.
        ///
        /// The server keeps the rotation but has no scoreboard, so a match
        /// won on points ends on the authority's machine and nowhere else.
        /// Sent repeatedly by NetMatchEnd until the server's state comes back
        /// saying it heard.
        /// </summary>
        public static void SendMatchEnd()
        {
            if (_transport == null || Role != NetRole.Client || _hostEndPoint == null)
            {
                return;
            }
            _transport.Send(_hostEndPoint, PacketType.MatchEnd, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>Host -> clients: authoritative state for every active player.</summary>
        public static void BroadcastSnapshot()
        {
            if (_transport == null)
            {
                return;
            }
            bool asHost = Role == NetRole.Host && _peers.Count > 0;
            bool asAuthority = Role == NetRole.Client && IsAuthority && _hostEndPoint != null;
            if (!asHost && !asAuthority)
            {
                return;
            }
            int count = 0;
            int offset = SnapshotHeader.Size;
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                if (offset + PlayerState.Size > NetConfig.MaxPacketSize - 1)
                {
                    break;
                }
                if (!Single.IsFinite(player.Position.X) || !Single.IsFinite(player.Position.Y)
                    || !Single.IsFinite(player.Position.Z))
                {
                    // Publishing this would hand the corruption to everyone
                    // else, and they would hand it back as an authoritative
                    // correction. Skip the slot until it makes sense again.
                    NetLog.Event($"slot {i} not published: position is {player.Position}");
                    continue;
                }
                var state = new PlayerState
                {
                    SlotIndex = (byte)i,
                    Flags = (byte)(PlayerState.FlagActive
                        | (player.IsAltForm ? PlayerState.FlagAltForm : 0)
                        | (player.ModIsInPlay ? PlayerState.FlagSpawned : 0)
                        | (player.EquipInfo.Zoomed ? PlayerState.FlagZoomed : 0)
                        | (player.Flags2.TestFlag(PlayerFlags2.Spectating) ? PlayerState.FlagSpectating : 0)
                        | (player.ModFrozen ? PlayerState.FlagFrozen : 0)
                        | (player.ModDisrupted ? PlayerState.FlagDisrupted : 0)
                        | (player.ModBurning ? PlayerState.FlagBurning : 0)),
                    Position = player.Position,
                    Speed = player.Speed,
                    Facing = player.FacingVector,
                    Health = (ushort)Math.Clamp(player.Health, 0, ushort.MaxValue),
                    CurrentWeapon = (byte)player.CurrentWeapon,
                    Team = (byte)player.Team
                };
                state.Points = (short)Math.Clamp(GameState.Points[i], Int16.MinValue, Int16.MaxValue);
                state.Kills = (ushort)Math.Clamp(GameState.Kills[i], 0, UInt16.MaxValue);
                state.Deaths = (ushort)Math.Clamp(GameState.Deaths[i], 0, UInt16.MaxValue);
                NetDamage.Write(i, ref state);
                state.Write(_scratch.AsSpan(offset));
                offset += PlayerState.Size;
                count++;
            }
            var header = new SnapshotHeader
            {
                Frame = NetFrame,
                Rng1 = Rng.Rng1,
                Rng2 = Rng.Rng2,
                PlayerCount = (byte)count
            };
            header.Write(_scratch);
            SnapshotsSent++;
            // Where everybody was, filed under the frame number this snapshot
            // carries. It has to be recorded here rather than anywhere else in
            // the frame: a client's ack names a snapshot, and the rewind is
            // the claim that cell `Frame` holds the picture that client saw.
            NetUnlagged.Record(header.Frame);
            // A demo only ever contains what this client *received*, and the
            // server forwards a snapshot to every peer except the one that
            // sent it -- so the authority's own demo had no snapshots in it
            // at all, which is every spawn, every hit and the whole
            // scoreboard. Same trick as the intent below.
            DemoRecorder.RecordOwnSnapshot(_scratch.AsSpan(0, offset));
            if (asAuthority)
            {
                // One send to the server, which relays to every other peer.
                _transport.Send(_hostEndPoint!, PacketType.Snapshot, _scratch.AsSpan(0, offset));
                return;
            }
            for (int i = 0; i < _peers.Count; i++)
            {
                _transport.Send(_peers[i].EndPoint, PacketType.Snapshot, _scratch.AsSpan(0, offset));
            }
        }
    }
}
