using System;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// The map vote, as this client sees it.
    ///
    /// A mirror and nothing more. Every rule -- who may call a vote, how long
    /// the room has to answer, what counts as passing, how long before another
    /// may be called -- is the server's, and lives in
    /// <see cref="DedicatedServer"/>. This holds the last picture the server
    /// sent so the HUD can draw it and the keys can answer it, which is the
    /// only part of a vote a client has any business deciding.
    ///
    /// Kept deliberately ignorant of whether the answer it sends arrives. A
    /// ballot is a datagram like any other and can be lost; the server's next
    /// broadcast is what says whether it was counted, so the prompt reads off
    /// <see cref="Yes"/> and <see cref="No"/> rather than off what this
    /// machine believes it pressed. <see cref="Answered"/> exists only to stop
    /// the key repeating.
    /// </summary>
    public static class MapVote
    {
        /// <summary>A vote is on the table right now.</summary>
        public static bool Active { get; private set; }

        /// <summary>The map being proposed.</summary>
        public static string RoomKey { get; private set; } = "";

        /// <summary>Who proposed it.</summary>
        public static string Proposer { get; private set; } = "";

        public static int Yes { get; private set; }
        public static int No { get; private set; }
        public static int Eligible { get; private set; }
        public static int Needed { get; private set; }

        /// <summary>
        /// Seconds left to answer while a vote runs; seconds until the room
        /// may call another while none does.
        /// </summary>
        public static int Seconds { get; private set; }

        /// <summary>
        /// Whether this client has already answered the vote on the table.
        /// Local, and only so F1 held down does not send thirty ballots.
        /// </summary>
        public static bool Answered { get; private set; }

        /// <summary>
        /// Whether the server has ever said anything about voting.
        ///
        /// A server built before this existed relays what it knows and drops
        /// what it does not, so a vote packet sent at one is not refused --
        /// it simply disappears. Nothing here can tell that from a server
        /// that is thinking about it, so the menu says "the server has not
        /// answered about voting" rather than offering a button that does
        /// nothing.
        /// </summary>
        public static bool Supported { get; private set; }

        /// <summary>
        /// Voting is switched off on this server, rather than merely on
        /// cooldown. The server says so by sending a wait nothing will ever
        /// count down to.
        /// </summary>
        public static bool Disabled { get; private set; }

        /// <summary>
        /// True while a vote may be called: the server does voting, none is
        /// running, and the room's cooldown has run out.
        /// </summary>
        public static bool CanPropose => Supported && !Disabled && !Active && Seconds == 0;

        /// <summary>Why not, in a sentence for the menu to show.</summary>
        public static string WhyNotProposing()
        {
            if (!NetSession.Active)
            {
                return "You are not in an online match.";
            }
            if (!Supported)
            {
                return "This server has not answered about voting; it may be an older build.";
            }
            if (Disabled)
            {
                return "Voting is switched off on this server.";
            }
            if (Active)
            {
                return $"A vote is already running: {RoomKey}.";
            }
            if (Seconds > 0)
            {
                return $"Another vote may be called in {Seconds} s.";
            }
            return "";
        }

        // Where the touch buttons were drawn, published by the draw rather
        // than worked out twice -- EndScreen's rule, and for its reason: two
        // computations of one layout drift, and the one that drifts is the
        // invisible one.
        private static EndScreen.Hit _hitAccept;
        private static EndScreen.Hit _hitDeny;

        public static void NoteLayout(EndScreen.Hit accept, EndScreen.Hit deny)
        {
            _hitAccept = accept;
            _hitDeny = deny;
        }

        /// <summary>
        /// A click or a tap, in the window fractions
        /// <see cref="EndScreen.PointerX"/> is kept in. True when it landed on
        /// one of the vote buttons, which means nothing else should see it.
        /// </summary>
        public static bool HandleClick()
        {
            if (!Active || Answered)
            {
                return false;
            }
            float x = EndScreen.PointerX;
            float y = EndScreen.PointerY;
            if (_hitAccept.Contains(x, y))
            {
                Cast(yes: true);
                return true;
            }
            if (_hitDeny.Contains(x, y))
            {
                Cast(yes: false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The two buttons, as window fractions, for a platform whose input
        /// is a finger rather than a cursor. Eight floats -- accept then deny,
        /// each left, top, right, bottom -- or empty when there is nothing to
        /// press. Flat rather than a shape, because it crosses into the
        /// Android head and one array is one thing for it to know about.
        /// </summary>
        public static float[] TouchTargets()
        {
            if (!Active || Answered || _hitAccept.Right <= _hitAccept.Left)
            {
                return Array.Empty<float>();
            }
            return new[]
            {
                _hitAccept.Left, _hitAccept.Top, _hitAccept.Right, _hitAccept.Bottom,
                _hitDeny.Left, _hitDeny.Top, _hitDeny.Right, _hitDeny.Bottom
            };
        }

        public static void Reset()
        {
            _hitAccept = default;
            _hitDeny = default;
            Active = false;
            RoomKey = "";
            Proposer = "";
            Yes = No = Eligible = Needed = Seconds = 0;
            Answered = false;
            Supported = false;
            Disabled = false;
        }

        /// <summary>Take the server's picture of the vote.</summary>
        public static void Apply(VoteStatePacket state)
        {
            Supported = true;
            Disabled = state.State != VoteStatePacket.StateRunning
                && state.Seconds == UInt16.MaxValue;
            bool wasActive = Active;
            string wasRoom = RoomKey;
            Active = state.State == VoteStatePacket.StateRunning;
            RoomKey = state.RoomKey;
            Proposer = state.Proposer;
            Yes = state.Yes;
            No = state.No;
            Eligible = state.Eligible;
            Needed = state.Needed;
            Seconds = Disabled ? 0 : state.Seconds;
            // A different vote is a different question, so the answer this
            // machine gave to the last one does not carry over. Keyed off the
            // map rather than off the transition, because a client that
            // joined mid-vote never saw one.
            if (!Active || !wasActive || wasRoom != RoomKey)
            {
                if (!Active || wasRoom != RoomKey)
                {
                    Answered = false;
                }
            }
        }

        /// <summary>
        /// Put a map to the room. Whether this client is allowed to is the
        /// server's decision -- it answers a refusal privately, as a system
        /// line -- so this only declines to send what plainly cannot be one.
        /// </summary>
        public static void Propose(string roomKey)
        {
            if (!NetSession.Active || String.IsNullOrWhiteSpace(roomKey))
            {
                return;
            }
            NetSession.SendVote(VotePacket.KindPropose, roomKey);
        }

        /// <summary>Answer the vote on the table.</summary>
        public static void Cast(bool yes)
        {
            if (!Active || Answered || !NetSession.Active)
            {
                return;
            }
            Answered = true;
            NetSession.SendVote(yes ? VotePacket.KindYes : VotePacket.KindNo, "");
        }

        /// <summary>
        /// The line the prompt draws, or empty when there is nothing to ask.
        /// </summary>
        public static string PromptLine()
        {
            if (!Active)
            {
                return "";
            }
            return $"{Proposer} PROPOSES {RoomKey.ToUpperInvariant()}";
        }

        /// <summary>The tally under it.</summary>
        public static string TallyLine()
        {
            if (!Active)
            {
                return "";
            }
            string answer = Answered ? "" : "  F1 YES / F2 NO";
            return $"{Yes}/{Needed} OF {Eligible}   {Seconds}s{answer}";
        }
    }
}
