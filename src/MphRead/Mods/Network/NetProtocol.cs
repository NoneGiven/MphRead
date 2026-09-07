using System;
using System.Buffers.Binary;
using System.Text;
using MphRead.Entities;
using OpenTK.Mathematics;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Wire format for MphRead's own LAN play. This is NOT the DS Wi-Fi
    /// protocol: it cannot talk to real hardware, melonDS, or Wiimmfi. It
    /// only connects MphRead instances to each other, which is why it can
    /// send gameplay intent instead of emulated 802.11 frames.
    ///
    /// The simulation runs in float (see Fixed.ToFloat -- the 20.12 values
    /// from the ROM are converted on load, not kept as integers), so two
    /// machines cannot be trusted to stay bit-identical from inputs alone.
    /// That rules out lockstep and makes the host authoritative: clients
    /// send intent, the host simulates, the host broadcasts resulting state.
    /// </summary>
    public enum PacketType : byte
    {
        Hello = 1,          // client -> host, join request
        Welcome = 2,        // host -> client, assigns a slot
        Intent = 3,         // client -> host, one frame of input
        Snapshot = 4,       // host -> clients, authoritative state
        Bye = 5,            // either direction, clean disconnect
        Ping = 6,
        Pong = 7,
        MatchState = 8,     // server -> clients, current map/mode/clock
        MapChange = 9,      // server -> clients, rotation advanced
        Roster = 10,        // server -> clients, who is in which slot
        Identify = 11,      // client -> server, my display name and hunter
        Authority = 12,     // server -> client, you are the simulation authority
        SlotIntent = 13,    // server -> authority, one peer's input, tagged with its slot
        StatusQuery = 14,   // anyone -> server, "what is running?" -- claims no slot
        StatusReply = 15,   // server -> asker, the running match plus the player cap
        MatchEnd = 16,      // authority -> server, somebody won or the clock ran out
        MasterHeartbeat = 17, // dedicated server -> master, "I am up, here is what I run"
        MasterQuery = 18,   // launcher -> master, "who is up?"
        MasterList = 19,    // master -> launcher, one page of the answer
        HostRequest = 20,   // launcher -> master, "run a game for me"
        HostReply = 21,     // master -> launcher, the port it is on, or why not
        Refused = 22,       // server -> client, "not you, and here is why"
        Chat = 23           // client -> server -> everyone else, one line of text
        // 24 and 25 are left free for a voice channel. Speech is a stream of
        // frames rather than a line of text -- it wants its own type, its own
        // cadence and its own "who is talking" packet, and squeezing it into
        // Chat's Kind byte would put an audio codec inside the packet the
        // scoreboard reads. Nothing here needs to change when it arrives: the
        // server relays what it recognises and drops what it does not, so a
        // build that speaks voice and a build that does not can share a match.
    }

    /// <summary>
    /// Why a server would not admit a client.
    ///
    /// Until this existed a refusal was a silence: the server logged its
    /// reason and sent nothing, so a full server, a server running a
    /// different build and a server that was switched off were the same eight
    /// seconds of waiting followed by a guess -- every screen in the program
    /// said "it may be off, full, or UDP may be blocked", because that is
    /// genuinely all the client knew.
    ///
    /// Additive and ignorable in both directions, so it needs no protocol
    /// bump: a client built before this drops an unknown packet type on the
    /// floor exactly as it always did, and a server built before it simply
    /// never sends one -- which is why the client also keeps the StatusQuery
    /// fallback (see <c>NetLaunch.DescribeJoinFailure</c>) for the servers
    /// already deployed.
    /// </summary>
    public struct RefusedPacket
    {
        public const int Size = 3;

        public const byte ReasonFull = 1;
        public const byte ReasonProtocol = 2;

        public byte Reason;
        public byte Players;
        public byte MaxPlayers;

        public void Write(Span<byte> dest)
        {
            dest[0] = Reason;
            dest[1] = Players;
            dest[2] = MaxPlayers;
        }

        public static RefusedPacket Read(ReadOnlySpan<byte> src)
        {
            return new RefusedPacket
            {
                Reason = src[0],
                Players = src.Length > 1 ? src[1] : (byte)0,
                MaxPlayers = src.Length > 2 ? src[2] : (byte)0
            };
        }

        public readonly string Describe(string where)
        {
            return Reason switch
            {
                ReasonFull => $"{where} is full ({Players}/{MaxPlayers} players). "
                    + "Try again when somebody leaves.",
                ReasonProtocol => $"{where} is running a different version of the game. "
                    + "One of you needs updating.",
                _ => $"{where} would not admit this client."
            };
        }
    }

    /// <summary>
    /// "Start a server for me, on your machine."
    ///
    /// This is how a game gets hosted without anybody opening a port. The
    /// player's own router is the problem -- a server on a home machine is
    /// unreachable from outside unless UDP is forwarded to it, which most
    /// people cannot or will not do -- and the fix that needs no cooperation
    /// from it is not to put the server there. The directory already runs on a
    /// machine with a reachable port; it starts the match there instead, and
    /// the host joins it by connecting *out*, exactly like every other player.
    ///
    /// Punching a hole through the NAT was the other candidate and is what a
    /// peer-to-peer game would have to do. It is not worth it here: this
    /// engine's netcode is already "everyone connects to one relay", so
    /// putting the relay somewhere reachable is the whole of the work, and it
    /// has no failure mode -- hole punching has one for every symmetric NAT.
    /// </summary>
    public struct HostRequestPacket
    {
        public const int MaxRoomBytes = 40;
        public const int MaxNameBytes = 32;
        public const int Size = 1 + 1 + 1 + 2 + 2 + MaxRoomBytes + MaxNameBytes;

        public byte Protocol;
        public byte MaxPlayers;
        public byte Mode;
        /// <summary>Match length in seconds. Zero means no limit.</summary>
        public ushort TimeLimit;
        public ushort PointGoal;
        public string RoomKey;
        public string ServerName;

        public void Write(Span<byte> dest)
        {
            dest[0] = Protocol;
            dest[1] = MaxPlayers;
            dest[2] = Mode;
            BinaryPrimitives.WriteUInt16LittleEndian(dest[3..], TimeLimit);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[5..], PointGoal);
            NetText.Write(dest.Slice(7, MaxRoomBytes), RoomKey);
            NetText.Write(dest.Slice(7 + MaxRoomBytes, MaxNameBytes), ServerName);
        }

        public static HostRequestPacket Read(ReadOnlySpan<byte> src)
        {
            return new HostRequestPacket
            {
                Protocol = src[0],
                MaxPlayers = src[1],
                Mode = src[2],
                TimeLimit = BinaryPrimitives.ReadUInt16LittleEndian(src[3..]),
                PointGoal = BinaryPrimitives.ReadUInt16LittleEndian(src[5..]),
                RoomKey = NetText.Read(src.Slice(7, MaxRoomBytes)),
                ServerName = NetText.Read(src.Slice(7 + MaxRoomBytes, MaxNameBytes))
            };
        }
    }

    /// <summary>Where the game the directory just started is listening, or why it did not.</summary>
    public struct HostReplyPacket
    {
        public const int MaxReasonBytes = 96;
        public const int Size = 1 + 2 + MaxReasonBytes;

        public bool Started;
        public ushort Port;
        public string Reason;

        public void Write(Span<byte> dest)
        {
            dest[0] = (byte)(Started ? 1 : 0);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[1..], Port);
            NetText.Write(dest.Slice(3, MaxReasonBytes), Reason);
        }

        public static HostReplyPacket Read(ReadOnlySpan<byte> src)
        {
            return new HostReplyPacket
            {
                Started = src[0] != 0,
                Port = BinaryPrimitives.ReadUInt16LittleEndian(src[1..]),
                Reason = NetText.Read(src.Slice(3, MaxReasonBytes))
            };
        }
    }

    /// <summary>
    /// What a launcher needs to show a server on a list, answered without
    /// joining.
    ///
    /// A Hello would answer the same questions, but it takes a slot to do it:
    /// polling with Hello churns the roster, can be refused outright when the
    /// server is full -- reporting a busy server as a dead one -- and on an
    /// empty server briefly makes the poller the simulation authority. This
    /// asks and leaves nothing behind.
    ///
    /// A server built before this packet existed ignores it, so the caller
    /// falls back to the Hello probe rather than reporting the server down.
    /// </summary>
    public struct ServerStatusPacket
    {
        /// <summary>What the server calls itself on a browser's list.</summary>
        public const int MaxNameBytes = 32;
        public const int Size = MatchStatePacket.Size + 2 + MaxNameBytes;

        public MatchStatePacket Match;
        public byte MaxPlayers;
        public byte Protocol;
        /// <summary>
        /// The name an admin gave this server, or an empty string. A list of
        /// addresses is not a list of servers -- people pick the one they
        /// recognise, and a numeric address is recognisable to nobody.
        /// </summary>
        public string ServerName;

        public void Write(Span<byte> dest)
        {
            Match.Write(dest);
            dest[MatchStatePacket.Size] = MaxPlayers;
            dest[MatchStatePacket.Size + 1] = Protocol;
            NetText.Write(dest.Slice(MatchStatePacket.Size + 2, MaxNameBytes), ServerName);
        }

        public static ServerStatusPacket Read(ReadOnlySpan<byte> src)
        {
            return new ServerStatusPacket
            {
                Match = MatchStatePacket.Read(src),
                MaxPlayers = src[MatchStatePacket.Size],
                Protocol = src[MatchStatePacket.Size + 1],
                ServerName = src.Length >= Size
                    ? NetText.Read(src.Slice(MatchStatePacket.Size + 2, MaxNameBytes))
                    : ""
            };
        }
    }

    /// <summary>
    /// Fixed-width ASCII in a packet, written and read the same way
    /// everywhere.
    ///
    /// Every name on the wire had its own private copy of this and they had
    /// started to disagree about what to do with a byte the in-game font
    /// cannot draw. One copy, one answer.
    /// </summary>
    public static class NetText
    {
        public static void Write(Span<byte> dest, string? value)
        {
            dest.Clear();
            if (String.IsNullOrEmpty(value))
            {
                return;
            }
            int count = Math.Min(value.Length, dest.Length);
            for (int i = 0; i < count; i++)
            {
                char c = value[i];
                dest[i] = (byte)(c < 32 || c > 126 ? '?' : c);
            }
        }

        public static string Read(ReadOnlySpan<byte> src)
        {
            int length = 0;
            while (length < src.Length && src[length] != 0)
            {
                length++;
            }
            return length == 0 ? String.Empty : Encoding.ASCII.GetString(src[..length]);
        }
    }

    /// <summary>
    /// One dedicated server, as the master list knows it.
    ///
    /// The master is a directory and nothing else: servers announce
    /// themselves to it every few seconds, it forgets the ones that stop, and
    /// a launcher asking for the list gets back address, port and whatever
    /// each server last said about itself. It never relays gameplay, so it
    /// costs a Raspberry Pi nothing to run beside the server it is listed in.
    ///
    /// Latency is deliberately absent: the master could only report its own
    /// round trip to each server, which is not the number a player wants.
    /// The launcher measures its own, by asking each server directly, which
    /// is also what proves the entry is still real.
    /// </summary>
    public struct MasterEntryPacket
    {
        public const int MaxNameBytes = 32;
        public const int MaxRoomBytes = 40;
        // address, port, players, max, mode, protocol, name, room
        public const int Size = 4 + 2 + 1 + 1 + 1 + 1 + MaxNameBytes + MaxRoomBytes;

        /// <summary>IPv4, network order, as the master saw the heartbeat arrive.</summary>
        public uint Address;
        public ushort Port;
        public byte Players;
        public byte MaxPlayers;
        public byte Mode;
        public byte Protocol;
        public string ServerName;
        public string RoomKey;

        public void Write(Span<byte> dest)
        {
            BinaryPrimitives.WriteUInt32BigEndian(dest, Address);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[4..], Port);
            dest[6] = Players;
            dest[7] = MaxPlayers;
            dest[8] = Mode;
            dest[9] = Protocol;
            NetText.Write(dest.Slice(10, MaxNameBytes), ServerName);
            NetText.Write(dest.Slice(10 + MaxNameBytes, MaxRoomBytes), RoomKey);
        }

        public static MasterEntryPacket Read(ReadOnlySpan<byte> src)
        {
            return new MasterEntryPacket
            {
                Address = BinaryPrimitives.ReadUInt32BigEndian(src),
                Port = BinaryPrimitives.ReadUInt16LittleEndian(src[4..]),
                Players = src[6],
                MaxPlayers = src[7],
                Mode = src[8],
                Protocol = src[9],
                ServerName = NetText.Read(src.Slice(10, MaxNameBytes)),
                RoomKey = NetText.Read(src.Slice(10 + MaxNameBytes, MaxRoomBytes))
            };
        }
    }

    /// <summary>
    /// What a dedicated server tells the master about itself.
    ///
    /// The address is not in it: the master takes that from the datagram it
    /// arrived in, so a server behind a router announces the address people
    /// can actually reach rather than the one it sees on its own interface.
    /// The port is, because that one the server does know and the source port
    /// of a heartbeat is not necessarily the one it listens on.
    /// </summary>
    public struct MasterHeartbeatPacket
    {
        public const int Size = 1 + 2 + 1 + 1 + 1 + MasterEntryPacket.MaxNameBytes
            + MasterEntryPacket.MaxRoomBytes;

        public byte Protocol;
        public ushort Port;
        public byte Players;
        public byte MaxPlayers;
        public byte Mode;
        public string ServerName;
        public string RoomKey;

        public void Write(Span<byte> dest)
        {
            dest[0] = Protocol;
            BinaryPrimitives.WriteUInt16LittleEndian(dest[1..], Port);
            dest[3] = Players;
            dest[4] = MaxPlayers;
            dest[5] = Mode;
            NetText.Write(dest.Slice(6, MasterEntryPacket.MaxNameBytes), ServerName);
            NetText.Write(dest.Slice(6 + MasterEntryPacket.MaxNameBytes,
                MasterEntryPacket.MaxRoomBytes), RoomKey);
        }

        public static MasterHeartbeatPacket Read(ReadOnlySpan<byte> src)
        {
            return new MasterHeartbeatPacket
            {
                Protocol = src[0],
                Port = BinaryPrimitives.ReadUInt16LittleEndian(src[1..]),
                Players = src[3],
                MaxPlayers = src[4],
                Mode = src[5],
                ServerName = NetText.Read(src.Slice(6, MasterEntryPacket.MaxNameBytes)),
                RoomKey = NetText.Read(src.Slice(6 + MasterEntryPacket.MaxNameBytes,
                    MasterEntryPacket.MaxRoomBytes))
            };
        }
    }

    /// <summary>
    /// What map is running, in what mode, and how much of it is left.
    ///
    /// Sent to a client the moment it connects and repeated periodically, so
    /// arriving mid-match is the normal case rather than a special one: the
    /// joiner loads the running map and adopts the server's clock instead of
    /// starting its own. Also carries the rotation's next map so clients can
    /// preload and switch without a gap.
    /// </summary>
    public struct MatchStatePacket
    {
        public const int MaxNameBytes = 40;
        public const int Size = 1 + 4 + 4 + 1 + 1 + 2 + 2 + MaxNameBytes + MaxNameBytes;

        public byte Mode;              // GameMode
        public float TimeRemaining;    // seconds left in this match
        public float TimeElapsed;      // seconds since the match started
        public byte PlayerCount;
        public byte Flags;             // bit 0 = match in progress, bit 1 = ending
        /// <summary>
        /// The score that wins this match, from the server's rotation file.
        ///
        /// Sent because it decides when the match ends, and a client that
        /// used its own would stop playing at a different moment from
        /// everybody else -- which is the same class of bug the match clock
        /// had before the server started publishing that.
        /// </summary>
        public ushort PointGoal;
        /// <summary>
        /// Which match this is, counting from the server's start.
        ///
        /// The room key alone cannot answer "is this a new match": a server
        /// hosting one map -- which is what the launcher's "Host a game" sets
        /// up -- plays the same room over and over, so a client watching only
        /// the name saw nothing change and sat on its results screen for the
        /// rest of the session. This changes every time the server starts a
        /// round, whatever it is being played on.
        /// </summary>
        public ushort MatchId;
        public string RoomKey;
        public string NextRoomKey;

        public const byte FlagInProgress = 1 << 0;
        /// <summary>
        /// The match is over and the server is running out the results
        /// sequence before it rotates. Clients show the winner, and -- this
        /// is the part that matters -- stop adopting the match clock, which
        /// would otherwise overwrite the countdown the results screen runs
        /// on.
        /// </summary>
        public const byte FlagEnding = 1 << 1;
        /// <summary>
        /// Same-team damage counts. Server-decided and broadcast rather than
        /// left to each client's own local setting -- see
        /// <see cref="DedicatedServer.FriendlyFire"/>.
        /// </summary>
        public const byte FlagFriendlyFire = 1 << 2;
        /// <summary>
        /// The shadow freeze glitch is switched **off** on this server, so an
        /// affinity Judicator's ice wave freezes what is in front of it rather
        /// than everything within 60 degrees at any height. Server-decided for
        /// the same reason friendly fire is: the machine resolving a shot
        /// decides who it hit.
        ///
        /// Stated as the negative deliberately. Zero is the cartridge's own
        /// behaviour, so a packet from anything that does not know about this
        /// rule -- a demo recorded before it, a server built before it --
        /// plays exactly as it always did.
        /// </summary>
        public const byte FlagNoShadowFreeze = 1 << 3;

        public readonly bool Ending => (Flags & FlagEnding) != 0;
        public readonly bool FriendlyFire => (Flags & FlagFriendlyFire) != 0;
        public readonly bool ShadowFreeze => (Flags & FlagNoShadowFreeze) == 0;

        public void Write(Span<byte> dest)
        {
            dest[0] = Mode;
            BinaryPrimitives.WriteSingleLittleEndian(dest[1..], TimeRemaining);
            BinaryPrimitives.WriteSingleLittleEndian(dest[5..], TimeElapsed);
            dest[9] = PlayerCount;
            dest[10] = Flags;
            BinaryPrimitives.WriteUInt16LittleEndian(dest[11..], PointGoal);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[13..], MatchId);
            WriteName(dest[15..], RoomKey);
            WriteName(dest[(15 + MaxNameBytes)..], NextRoomKey);
        }

        public static MatchStatePacket Read(ReadOnlySpan<byte> src)
        {
            return new MatchStatePacket
            {
                Mode = src[0],
                TimeRemaining = BinaryPrimitives.ReadSingleLittleEndian(src[1..]),
                TimeElapsed = BinaryPrimitives.ReadSingleLittleEndian(src[5..]),
                PlayerCount = src[9],
                Flags = src[10],
                PointGoal = BinaryPrimitives.ReadUInt16LittleEndian(src[11..]),
                MatchId = BinaryPrimitives.ReadUInt16LittleEndian(src[13..]),
                RoomKey = ReadName(src[15..]),
                NextRoomKey = ReadName(src[(15 + MaxNameBytes)..])
            };
        }

        private static void WriteName(Span<byte> dest, string? value)
        {
            dest[..MaxNameBytes].Clear();
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            // Room keys are ASCII in the metadata; truncate rather than throw
            // so an unexpected long name degrades instead of dropping the packet.
            int count = Math.Min(value.Length, MaxNameBytes);
            for (int i = 0; i < count; i++)
            {
                dest[i] = (byte)value[i];
            }
        }

        private static string ReadName(ReadOnlySpan<byte> src)
        {
            int length = 0;
            while (length < MaxNameBytes && src[length] != 0)
            {
                length++;
            }
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(src[..length]);
        }
    }

    /// <summary>
    /// One frame of player intent, device-independent. Deliberately not
    /// KeyboardState/MouseState: those are host-machine concepts. This is
    /// the same abstraction PlayerAi already writes into Controls, which is
    /// why a remote player can reuse the bot injection path verbatim.
    /// </summary>
    [Flags]
    public enum IntentButtons : uint
    {
        None = 0,
        MoveLeft = 1u << 0,
        MoveRight = 1u << 1,
        MoveUp = 1u << 2,
        MoveDown = 1u << 3,
        Shoot = 1u << 4,
        Zoom = 1u << 5,
        Jump = 1u << 6,
        Morph = 1u << 7,
        Boost = 1u << 8,
        AltAttack = 1u << 9,
        ScanVisor = 1u << 10,
        NextWeapon = 1u << 11,
        PrevWeapon = 1u << 12,
        RollLeft = 1u << 13,
        RollRight = 1u << 14,
        RollUp = 1u << 15,
        RollDown = 1u << 16,
        /// <summary>
        /// Not a button: whether the sender is *currently* zoomed.
        ///
        /// Zoom was the last thing in this packet still being reconstructed on
        /// the receiver from a rising edge, and reconstruction is exactly what
        /// the ammo and the weapon are here to avoid. UpdateZoom is a toggle,
        /// and it is ignored unless the player already holds a weapon that can
        /// zoom -- so at 250 ms, where a puppet's weapon runs a quarter of a
        /// second behind its owner's, the press arrives before the Imperialist
        /// does, the toggle is skipped, the press is spent, and the owner
        /// never presses again because on its own screen it is already zoomed.
        /// Measured against the Pi: 485 frames zoomed on the owner and zero on
        /// all five machines watching.
        ///
        /// A state rather than an edge cannot be missed twice. It costs
        /// nothing -- the mask had fifteen bits spare.
        /// </summary>
        ZoomedState = 1u << 17,
        /// <summary>
        /// Not a button either: which form the sender was in when it measured
        /// the position in this packet.
        ///
        /// Position means two different things depending on form. UpdateForm
        /// shifts it by the distance between the two collision volumes'
        /// centres on the way into alt and back again on the way out, so the
        /// same standing spot is a different number in each. A puppet whose
        /// form has not caught up with its owner's is therefore placed in the
        /// wrong reference frame, and its hitbox sits that far off the body
        /// everyone can see -- vertically, on a biped cylinder only 1.6 units
        /// tall. Reported from play as a player who could not be hurt in
        /// biped form while alt form worked perfectly.
        ///
        /// Sending the form is what lets the receiver convert instead of
        /// guess. Free: another spare bit.
        /// </summary>
        AltFormState = 1u << 18,
        /// <summary>
        /// Whether the sender considers itself alive and on the map.
        ///
        /// Without it the authority cannot tell "here is where I am" from
        /// "here is where my body is lying". A dead player keeps sending
        /// intents, and they keep carrying the spot it died on -- so the
        /// authority puts the puppet on a spawn point and the very next
        /// packet drags it back onto the corpse, which is then published as
        /// the position it respawned at.
        ///
        /// The frame number cannot answer this. It was tried: ignore intents
        /// composed before the spawn. But the owner's counter keeps rising
        /// while it is still dead, so the barrier is cleared within two
        /// frames and the corpse position wins anyway. Measured from a real
        /// session, seven respawns out of seven landed exactly on the spot of
        /// death, to two decimal places, on both machines.
        /// </summary>
        InPlayState = 1u << 19,
        /// <summary>
        /// Not a button either: the sender has stopped playing and is
        /// watching (<see cref="Mods.SpectatorMode"/>).
        ///
        /// Here rather than only in the snapshot because the snapshot travels
        /// the wrong way for this. A spectator sets the flag on its own
        /// machine; the flag reaches everyone else through the authority's
        /// snapshot; and the authority learns nothing about a client except
        /// through this packet. So a client that spectated was hidden and
        /// non-solid on its own screen alone -- on every other machine, the
        /// authority's included, it was still a solid, shootable player
        /// standing exactly where it had stopped, and could be killed for
        /// points while its owner was watching from the ceiling. Measured
        /// against the Pi: 1501 frames spectating on the owner, 0 on the
        /// observer.
        ///
        /// A spare bit, deliberately: an older build ignores it and behaves
        /// exactly as it did before, so this needs no protocol bump.
        /// </summary>
        SpectatingState = 1u << 20
    }

    /// <summary>
    /// Who is in which slot.
    ///
    /// Names are the check that matters for "are we in the same match":
    /// positions can look plausible while two clients are actually alone in
    /// their own scenes, but a name can only appear on your scoreboard if it
    /// travelled from the other machine.
    /// </summary>
    public struct RosterPacket
    {
        public const int MaxNameBytes = 16;
        public const int MaxSlots = PlayerEntity.SlotCapacity;
        // Slot, hunter, suit colour, round trip time and name per entry. The
        // hunter travels with the name because both answer the same question
        // -- who is in this slot -- and because a client that never learns it
        // draws every other player as whichever hunter this machine happens to
        // have picked. The colour is the same fact one step further: without
        // it every client picked its own, so two people on the same hunter
        // were the same figure in the same suit on every screen. The ping
        // rides along for the same reason: it is a property of who is in the
        // slot, the server is the only party that can measure it for
        // everybody, and it already sends this packet every second.
        public const int EntrySize = 1 + 1 + 1 + 2 + MaxNameBytes;
        public const int Size = 1 + MaxSlots * EntrySize;

        public byte Count;
        public byte[] Slots;      // slot index per entry
        public byte[] Hunters;    // Hunter enum value per entry
        public byte[] Colors;     // suit palette asked for, 0-3
        public ushort[] Pings;    // round trip to the server, milliseconds
        public string[] Names;

        public static RosterPacket Create()
        {
            return new RosterPacket
            {
                Count = 0,
                Slots = new byte[MaxSlots],
                Hunters = new byte[MaxSlots],
                Colors = new byte[MaxSlots],
                Pings = new ushort[MaxSlots],
                Names = new string[MaxSlots]
            };
        }

        public void Write(Span<byte> dest)
        {
            dest[..Size].Clear();
            dest[0] = Count;
            int offset = 1;
            for (int i = 0; i < Count && i < MaxSlots; i++)
            {
                dest[offset] = Slots[i];
                dest[offset + 1] = Hunters[i];
                dest[offset + 2] = Colors[i];
                BinaryPrimitives.WriteUInt16LittleEndian(dest[(offset + 3)..], Pings[i]);
                WriteName(dest.Slice(offset + 5, MaxNameBytes), Names[i]);
                offset += EntrySize;
            }
        }

        public static RosterPacket Read(ReadOnlySpan<byte> src)
        {
            RosterPacket roster = Create();
            roster.Count = Math.Min(src[0], (byte)MaxSlots);
            int offset = 1;
            for (int i = 0; i < roster.Count; i++)
            {
                roster.Slots[i] = src[offset];
                roster.Hunters[i] = src[offset + 1];
                roster.Colors[i] = src[offset + 2];
                roster.Pings[i] = BinaryPrimitives.ReadUInt16LittleEndian(src[(offset + 3)..]);
                roster.Names[i] = ReadName(src.Slice(offset + 5, MaxNameBytes));
                offset += EntrySize;
            }
            return roster;
        }

        private static void WriteName(Span<byte> dest, string? value)
        {
            dest.Clear();
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            int count = Math.Min(value.Length, MaxNameBytes);
            for (int i = 0; i < count; i++)
            {
                char c = value[i];
                // The in-game font is ASCII; substitute rather than emit
                // bytes the HUD cannot draw.
                dest[i] = (byte)(c < 32 || c > 126 ? '?' : c);
            }
        }

        private static string ReadName(ReadOnlySpan<byte> src)
        {
            int length = 0;
            while (length < src.Length && src[length] != 0)
            {
                length++;
            }
            return length == 0 ? string.Empty : Encoding.ASCII.GetString(src[..length]);
        }
    }

    /// <summary>
    /// One line somebody typed.
    ///
    /// Additive and ignorable in both directions, exactly like
    /// <see cref="RefusedPacket"/>, so it needs no protocol bump: a server
    /// built before this drops the type on the floor as it always did -- the
    /// sender still sees its own line, nobody else does -- and a client built
    /// before it is never sent one. What that buys is that chat can be
    /// deployed without taking every match offline; what it costs is that
    /// "nobody answered me" on an old server looks exactly like "nobody
    /// answered me", which is why <c>ChatBox</c> says so once per session
    /// when the server never echoes anything back.
    ///
    /// The slot and the name are written by the *server*, not trusted from
    /// the sender: a client can put anything in these fields, and a line
    /// attributed to somebody else is the whole of what a chat exploit is.
    /// The client fills them in anyway, so a demo recorded against a server
    /// that predates chat still replays with a name attached.
    ///
    /// Fixed size, like every other packet here. 114 bytes for something sent
    /// a handful of times a match is not worth a length prefix, and a fixed
    /// layout is the one that cannot be read short.
    /// </summary>
    public struct ChatPacket
    {
        public const int MaxNameBytes = 16;
        /// <summary>
        /// Room for a sentence and no more. The HUD draws these in the DS's
        /// 256-unit space at half size, which is about 90 characters across
        /// once a name and a colon are in front of them, so a longer message
        /// could not be read even if it were carried.
        /// </summary>
        public const int MaxTextBytes = 96;
        public const int Size = 1 + 1 + MaxNameBytes + MaxTextBytes;

        /// <summary>Everyone in the match.</summary>
        public const byte KindSay = 0;
        /// <summary>
        /// One team. Reserved rather than implemented: the modes that have
        /// teams are here, but nothing yet asks the server which side a slot
        /// is on, so the server relays this as <see cref="KindSay"/> instead
        /// of quietly delivering a team line to the other team.
        /// </summary>
        public const byte KindTeam = 1;
        /// <summary>The server itself talking -- joins, leaves, refusals.</summary>
        public const byte KindSystem = 2;

        public byte Slot;
        public byte Kind;
        public string Name;
        public string Text;

        public void Write(Span<byte> dest)
        {
            dest[..Size].Clear();
            dest[0] = Slot;
            dest[1] = Kind;
            WriteAscii(dest.Slice(2, MaxNameBytes), Name);
            WriteAscii(dest.Slice(2 + MaxNameBytes, MaxTextBytes), Text);
        }

        public static ChatPacket Read(ReadOnlySpan<byte> src)
        {
            return new ChatPacket
            {
                Slot = src[0],
                Kind = src[1],
                Name = ReadAscii(src.Slice(2, MaxNameBytes)),
                Text = ReadAscii(src.Slice(2 + MaxNameBytes, MaxTextBytes))
            };
        }

        /// <summary>
        /// The same substitution <see cref="RosterPacket"/> makes, and for the
        /// same reason: the in-game font is ASCII, so a byte it cannot draw
        /// has to be replaced here rather than discovered by the HUD. It also
        /// means a hostile client cannot put a control character on anybody
        /// else's screen -- everything below 32 becomes a question mark on the
        /// way in as well as on the way out.
        /// </summary>
        internal static void WriteAscii(Span<byte> dest, string? value)
        {
            dest.Clear();
            if (String.IsNullOrEmpty(value))
            {
                return;
            }
            int count = Math.Min(value.Length, dest.Length);
            for (int i = 0; i < count; i++)
            {
                char c = value[i];
                dest[i] = (byte)(c < 32 || c > 126 ? '?' : c);
            }
        }

        internal static string ReadAscii(ReadOnlySpan<byte> src)
        {
            int length = 0;
            while (length < src.Length && src[length] != 0)
            {
                length++;
            }
            if (length == 0)
            {
                return String.Empty;
            }
            Span<char> chars = stackalloc char[length];
            for (int i = 0; i < length; i++)
            {
                byte b = src[i];
                chars[i] = b < 32 || b > 126 ? '?' : (char)b;
            }
            return new string(chars);
        }
    }

    public struct IntentPacket
    {
        /// <summary>
        /// How many frames of rising edges each packet carries. A button held
        /// for one frame -- morph, weapon switch, alt attack -- exists in
        /// exactly one packet, and UDP loses packets: half of them never
        /// arrived, so a player morphed on their own screen and stayed a
        /// biped on everyone else's. Repeating the last few frames of presses
        /// means an action survives three consecutive drops, and the frame
        /// number each one belongs to lets the receiver take each press once.
        /// </summary>
        public const int PressHistory = 8;
        public const int Size = 4 + 4 + 12 + 1 + 4 * PressHistory + 12 + 2 + 2 + 4;

        public uint Frame;          // client's frame counter, for ordering
        public IntentButtons Buttons;
        /// <summary>Rising edges for Frame, Frame-1, ... Frame-(PressHistory-1).</summary>
        public uint[] Presses;
        /// <summary>
        /// Where the sender's gun points, as a direction rather than as this
        /// frame's mouse movement.
        ///
        /// Deltas were the obvious encoding and the wrong one. Aim is applied
        /// by rotating the receiver's copy, so a single dropped datagram --
        /// UDP, so routine -- left the two machines holding permanently
        /// different aim for the same player, with no mechanism that could
        /// ever bring them back together. The shooter saw its crosshair on an
        /// opponent while the authority, which decides what is hit, had the
        /// gun pointing somewhere else, so its shots simply never connected.
        /// An absolute direction re-agrees on every packet that does arrive.
        /// </summary>
        public Vector3 Aim;
        /// <summary>
        /// Where the sender actually is.
        ///
        /// Sent rather than re-derived, because deriving it meant simulating
        /// the same player twice -- once on their own machine from their
        /// keyboard, once on the authority from these buttons -- and two
        /// simulations of one player drift apart the moment a packet is lost.
        /// They then disagree about collision, and the correction yanks the
        /// player back and forth several times a second: a 10-unit jump, then
        /// the local collision pushing it straight back, forever. Whoever is
        /// playing a character is the one who knows where it is.
        /// </summary>
        public Vector3 Position;
        public byte WeaponSelect;   // 0xFF = no direct weapon switch this frame
        /// <summary>
        /// Universal ammo and missiles, as the owner counts them.
        ///
        /// Sent for the same reason the position is: everyone simulates this
        /// player's shots, only the owner collects this player's pickups, and
        /// the two answers part company within a round. A beam whose cost
        /// exceeds the shooter's ammo is not spawned at all, so a puppet that
        /// has run dry on the authority's machine makes its owner's shots
        /// vanish on the one machine that decides what they hit -- which
        /// looks, from every screen, like a player who cannot be damaged.
        /// </summary>
        public ushort AmmoUa;
        public ushort AmmoMissiles;

        /// <summary>
        /// The newest snapshot frame this client had applied when it composed
        /// this packet -- which is to say, the moment in the authority's
        /// simulation that its screen was showing.
        ///
        /// The one number lag compensation needs. Everything else in this
        /// packet says what the player did; this says what they were looking
        /// at while they did it, and without it the authority can only guess
        /// -- from a smoothed ping, which is an average of a quantity that is
        /// not smooth, and which is measured over a path the intent did not
        /// necessarily take.
        ///
        /// Zero from a client that has not received a snapshot yet, and from
        /// the authority itself, which is never behind its own simulation.
        /// Both mean "do not rewind": see
        /// <see cref="Mods.Network.NetUnlagged.RewindFor"/>, which refuses an
        /// ack it cannot serve rather than serving it approximately.
        /// </summary>
        public uint AckFrame;

        public void Write(Span<byte> dest)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dest[0..], Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(dest[4..], (uint)Buttons);
            BinaryPrimitives.WriteSingleLittleEndian(dest[8..], Aim.X);
            BinaryPrimitives.WriteSingleLittleEndian(dest[12..], Aim.Y);
            BinaryPrimitives.WriteSingleLittleEndian(dest[16..], Aim.Z);
            dest[20] = WeaponSelect;
            for (int i = 0; i < PressHistory; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(dest[(21 + i * 4)..],
                    Presses != null && i < Presses.Length ? Presses[i] : 0);
            }
            int at = 21 + PressHistory * 4;
            BinaryPrimitives.WriteSingleLittleEndian(dest[at..], Position.X);
            BinaryPrimitives.WriteSingleLittleEndian(dest[(at + 4)..], Position.Y);
            BinaryPrimitives.WriteSingleLittleEndian(dest[(at + 8)..], Position.Z);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[(at + 12)..], AmmoUa);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[(at + 14)..], AmmoMissiles);
            BinaryPrimitives.WriteUInt32LittleEndian(dest[(at + 16)..], AckFrame);
        }

        public static IntentPacket Read(ReadOnlySpan<byte> src)
        {
            var presses = new uint[PressHistory];
            for (int i = 0; i < PressHistory; i++)
            {
                presses[i] = BinaryPrimitives.ReadUInt32LittleEndian(src[(21 + i * 4)..]);
            }
            return new IntentPacket
            {
                Frame = BinaryPrimitives.ReadUInt32LittleEndian(src[0..]),
                Buttons = (IntentButtons)BinaryPrimitives.ReadUInt32LittleEndian(src[4..]),
                Aim = new Vector3(
                    BinaryPrimitives.ReadSingleLittleEndian(src[8..]),
                    BinaryPrimitives.ReadSingleLittleEndian(src[12..]),
                    BinaryPrimitives.ReadSingleLittleEndian(src[16..])),
                WeaponSelect = src[20],
                Presses = presses,
                Position = new Vector3(
                    BinaryPrimitives.ReadSingleLittleEndian(src[(21 + PressHistory * 4)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(src[(25 + PressHistory * 4)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(src[(29 + PressHistory * 4)..])),
                AmmoUa = BinaryPrimitives.ReadUInt16LittleEndian(src[(33 + PressHistory * 4)..]),
                AmmoMissiles = BinaryPrimitives.ReadUInt16LittleEndian(src[(35 + PressHistory * 4)..]),
                AckFrame = BinaryPrimitives.ReadUInt32LittleEndian(src[(37 + PressHistory * 4)..])
            };
        }
    }

    /// <summary>
    /// Authoritative per-player state. Position/Speed/facing are what a
    /// remote client cannot derive on its own once float drift is possible;
    /// health/weapon/team are cheap enough to resend every snapshot rather
    /// than tracking deltas at this stage.
    /// </summary>
    public struct PlayerState
    {
        public const int Size = 1 + 1 + 12 + 12 + 12 + 2 + 1 + 1 + 1 + 1 + 1 + 1 + 12 + 2 + 2 + 2;

        public byte SlotIndex;
        public byte Flags;          // bit 0 = active, bit 1 = alt form, bit 2 = spawned
        public Vector3 Position;
        public Vector3 Speed;
        public Vector3 Facing;
        public ushort Health;
        public byte CurrentWeapon;
        public byte Team;
        /// <summary>
        /// Counts hits the authority has resolved against this player, so a
        /// receiver can tell a new one from a snapshot it has already seen.
        /// Comparing health instead would replay a repeated snapshot as a
        /// fresh hit and miss two that cancelled out.
        /// </summary>
        public byte DamageSeq;
        public byte AttackerSlot;   // 0xFF = nobody
        public byte DamageBeam;     // BeamType, 0xFF = not a beam
        public byte DamageFlags;    // headshot / deathalt / burn
        public Vector3 HitDirection;
        /// <summary>
        /// The score, from the machine that keeps it.
        ///
        /// Each client used to count only the deaths it had witnessed, so a
        /// player joining a running match started everyone at zero and its
        /// scoreboard never agreed with anybody else's again. The authority
        /// resolves every kill, so its tally is the one worth sending.
        /// </summary>
        public short Points;
        public ushort Kills;
        public ushort Deaths;

        public const byte FlagActive = 1 << 0;
        public const byte FlagAltForm = 1 << 1;
        /// <summary>
        /// The authority has placed this player at a spawn point. Receivers
        /// use it to tell "standing in the map" from "waiting at the origin
        /// with no health": both look identical in position and health
        /// alone, and treating the second as the first put motionless bodies
        /// at (0,0,0) on every other client.
        /// </summary>
        public const byte FlagSpawned = 1 << 2;
        /// <summary>
        /// Aiming down the Imperialist's sight. Visible to everyone else as
        /// the laser, so it has to travel; it was measured at 2488 frames on
        /// the player holding it and 92 on everyone watching.
        /// </summary>
        public const byte FlagZoomed = 1 << 3;
        /// <summary>
        /// Spectating: hidden and non-solid on every client, not just the
        /// one whose local input is frozen. See <see cref="Mods.SpectatorMode"/>.
        /// </summary>
        public const byte FlagSpectating = 1 << 4;
        /// <summary>
        /// Frozen solid by an affinity Judicator.
        ///
        /// The freeze is produced inside <c>TakeDamage</c>, from the beam
        /// entity that landed the hit -- and a beam entity only ever exists on
        /// the machine that resolved it. <c>NetDamage.Replay</c> has no beam
        /// to give, so every machine but the authority replayed the damage
        /// without the affliction: the victim went on walking about on their
        /// own screen while the authority held them still, and the authority,
        /// which pins a puppet to the position its owner last reported, drew a
        /// player encased in ice sliding around the room. Reported from play
        /// as "frozen players who keep moving", from the person hosting.
        ///
        /// So the state travels rather than the cause. Additive: the byte and
        /// the packet are the size they were, an older build ignores the bit,
        /// and an older authority simply never sets it.
        /// </summary>
        public const byte FlagFrozen = 1 << 5;
        /// <summary>
        /// Disrupted by an affinity Volt Driver's charged shot.
        ///
        /// The same fault as the freeze, one weapon along, and the last bit of
        /// it that was still showing: the disruption is applied inside
        /// <c>TakeDamage</c> from the beam that landed the hit, so on every
        /// machine but the authority's the victim was hit by a charged Volt
        /// Driver and nothing happened at all -- no aim disruption and, above
        /// all, none of the screen distortion the weapon is *known* by. The
        /// shooter watched their charge land and the target play on, and the
        /// target had no idea what had hit them. Reported as "the Volt
        /// Driver's charged shot is missing the screen-distortion effect".
        ///
        /// The distortion itself was never missing: the shader, the shift
        /// table and the four-state machine that drives it are all there in
        /// <c>PlayerHud</c> and <c>Renderer</c>, and they work perfectly for
        /// whoever happens to be the authority. Nothing was ever setting them
        /// off for anybody else.
        /// </summary>
        public const byte FlagDisrupted = 1 << 6;
        /// <summary>
        /// Burning, from an affinity Magmaul's charged shot.
        ///
        /// Third of the same three, and the same story: the flames are spawned
        /// in <c>TakeDamage</c> from the beam, so a victim on any machine but
        /// the authority's took the damage over time -- which is relayed like
        /// any other hit -- while standing there not on fire. "Hunters taking
        /// burn damage do not display the burning visual effect".
        ///
        /// Cosmetic on arrival, and deliberately so: the burn's own tick calls
        /// TakeDamage, and <see cref="NetDamage.Suppress"/> drops that on
        /// every machine that is not resolving the match. What travels is the
        /// fire; the damage keeps coming the way all damage does.
        /// </summary>
        public const byte FlagBurning = 1 << 7;

        public void Write(Span<byte> dest)
        {
            dest[0] = SlotIndex;
            dest[1] = Flags;
            WriteVec(dest[2..], Position);
            WriteVec(dest[14..], Speed);
            WriteVec(dest[26..], Facing);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[38..], Health);
            dest[40] = CurrentWeapon;
            dest[41] = Team;
            dest[42] = DamageSeq;
            dest[43] = AttackerSlot;
            dest[44] = DamageBeam;
            dest[45] = DamageFlags;
            WriteVec(dest[46..], HitDirection);
            BinaryPrimitives.WriteInt16LittleEndian(dest[58..], Points);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[60..], Kills);
            BinaryPrimitives.WriteUInt16LittleEndian(dest[62..], Deaths);
        }

        public static PlayerState Read(ReadOnlySpan<byte> src)
        {
            return new PlayerState
            {
                SlotIndex = src[0],
                Flags = src[1],
                Position = ReadVec(src[2..]),
                Speed = ReadVec(src[14..]),
                Facing = ReadVec(src[26..]),
                Health = BinaryPrimitives.ReadUInt16LittleEndian(src[38..]),
                CurrentWeapon = src[40],
                Team = src[41],
                DamageSeq = src[42],
                AttackerSlot = src[43],
                DamageBeam = src[44],
                DamageFlags = src[45],
                HitDirection = ReadVec(src[46..]),
                Points = BinaryPrimitives.ReadInt16LittleEndian(src[58..]),
                Kills = BinaryPrimitives.ReadUInt16LittleEndian(src[60..]),
                Deaths = BinaryPrimitives.ReadUInt16LittleEndian(src[62..])
            };
        }

        private static void WriteVec(Span<byte> dest, Vector3 v)
        {
            BinaryPrimitives.WriteSingleLittleEndian(dest[0..], v.X);
            BinaryPrimitives.WriteSingleLittleEndian(dest[4..], v.Y);
            BinaryPrimitives.WriteSingleLittleEndian(dest[8..], v.Z);
        }

        private static Vector3 ReadVec(ReadOnlySpan<byte> src)
        {
            return new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(src[0..]),
                BinaryPrimitives.ReadSingleLittleEndian(src[4..]),
                BinaryPrimitives.ReadSingleLittleEndian(src[8..]));
        }
    }

    /// <summary>
    /// Host -> clients. Carries both RNG words: Rng.cs reproduces the game's
    /// original LCG exactly and its state is global, so resyncing it keeps
    /// host-side and client-side effects (damage rolls, AI jitter) agreeing
    /// without replicating every consumer of randomness.
    /// </summary>
    public struct SnapshotHeader
    {
        public const int Size = 4 + 4 + 4 + 1;

        public uint Frame;
        public uint Rng1;
        public uint Rng2;
        public byte PlayerCount;

        public void Write(Span<byte> dest)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dest[0..], Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(dest[4..], Rng1);
            BinaryPrimitives.WriteUInt32LittleEndian(dest[8..], Rng2);
            dest[12] = PlayerCount;
        }

        public static SnapshotHeader Read(ReadOnlySpan<byte> src)
        {
            return new SnapshotHeader
            {
                Frame = BinaryPrimitives.ReadUInt32LittleEndian(src[0..]),
                Rng1 = BinaryPrimitives.ReadUInt32LittleEndian(src[4..]),
                Rng2 = BinaryPrimitives.ReadUInt32LittleEndian(src[8..]),
                PlayerCount = src[12]
            };
        }
    }

    public static class NetConfig
    {
        public const ushort DefaultPort = 27888;
        public const int MaxPacketSize = 1024;
        /// <summary>
        /// Bumped when the wire format changes in a way an older build would
        /// misread rather than notice. Version 2 added the ping to the roster:
        /// its entries grew from 18 bytes to 20, and a version 1 client would
        /// have accepted the longer packet and read every name at the wrong
        /// offset. Version 3 added the shooter's ammo to the intent, the
        /// end-of-match handshake, and a name to the status reply. A mismatch
        /// is refused at Hello, with a line in the server log, which is a far
        /// better failure than garbled names.
        ///
        /// Version 4 is the odd one: nothing in the layout moved. It is a
        /// refusal on *behaviour*, because a version 3 build reads every byte
        /// correctly and then plays a different game -- its own player frozen
        /// where it stands, its shots leaving from its ankles, its respawns
        /// putting it back inside whatever it died in. Two of those are worse
        /// coming from the authority than from anyone else, and the authority
        /// is simply the first client to connect, so one stale copy joining
        /// first hands every one of those faults to everybody in the match.
        /// Nothing in the wire would have noticed; this is what makes the
        /// server say no.
        ///
        /// Version 5 grows the intent by four bytes for
        /// <see cref="IntentPacket.AckFrame"/>, which lag compensation reads
        /// to decide how far back a client's shot belongs. It is appended
        /// rather than inserted, so nothing before it moved -- but the packet
        /// is longer, and a version 4 authority handed one would read the
        /// whole thing correctly and then resolve every remote shot against
        /// the present, which is the fault this exists to fix. The layout
        /// change is what forces the refusal; the behaviour is why it is
        /// worth forcing.
        ///
        /// Version 6 puts a suit colour beside the hunter, in Identify and in
        /// the roster, so that two people playing the same hunter are two
        /// different figures on every screen (see
        /// <see cref="Mods.Network.PlayerColors"/>). Both packets are
        /// *inserted* into rather than appended to: the roster's entries grow
        /// from 20 bytes to 21 and every name after the first moves, which a
        /// version 5 client would read as garbage rather than notice. This is
        /// the same shape of change version 2 was, and it is refused the same
        /// way.
        ///
        /// Version 6 also spends the last two bits of the player state's flag
        /// byte on <see cref="PlayerState.FlagDisrupted"/> and
        /// <see cref="PlayerState.FlagBurning"/>, which cost no space and
        /// would not have needed a bump of their own -- an older build ignores
        /// a bit it does not know. They are mentioned here because the byte is
        /// now full: the next flag needs somewhere to live.
        /// </summary>
        public const int ProtocolVersion = 6;
        /// <summary>
        /// Frames between intent packets. One, so every frame.
        ///
        /// This is the rate at which a remote player exists, not just the rate
        /// it is corrected at: a puppet is pinned to the position its owner
        /// reported (see NetPlayerBridge.RestoreReportedPosition, which runs
        /// after the engine's own movement step), so whatever the engine
        /// simulates in between is thrown away. At 2 that made every player
        /// but your own move in 30 Hz steps -- on a 60 Hz screen, in a 60 Hz
        /// simulation -- and a recorded demo, where every player is a puppet,
        /// stepped from end to end.
        ///
        /// It was 2 because the server relays N*(N-1) intents per frame and at
        /// six players that was losing enough of them to leave gaps. What made
        /// that true was a transport whose send queue dropped the *newest*
        /// packets when it filled, which is the opposite of what a position
        /// stream wants and was fixed since (see NETWORK-DIAGNOSTICS). Doubled
        /// traffic is the cost: about 100 bytes on the wire per player per
        /// frame, so 42 KB/s into each client of an eight-player match.
        ///
        /// The feature check samples both sides of a comparison on this
        /// cadence, which is now every frame.
        /// </summary>
        public const int IntentSendInterval = 1;
        // A client that has sent nothing for this long is dropped. Generous
        // on purpose: loading a room is synchronous and sends nothing while
        // it runs, and a client dropped mid-load used to be gone for good --
        // it had a slot, so it never said hello again, and every packet it
        // sent afterwards was from an endpoint the server no longer knew.
        public const double TimeoutSeconds = 30.0;
    }
}
