using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Which of a hunter's suits each player is wearing.
    ///
    /// Every hunter model carries six palettes -- <c>pal_01</c> to
    /// <c>pal_04</c>, then the two team suits -- and the game had never used
    /// more than the first: a match was built with <c>recolor: 0</c> for
    /// everybody, so two people who picked Samus were the same Samus, in the
    /// same colours, at the same size. In a fast shooter that is not a
    /// cosmetic complaint; it is not knowing who you are looking at. Both
    /// halves of it were reported -- "let me choose my colour", and "when two
    /// of us pick the same hunter we cannot tell each other apart".
    ///
    /// So a colour is a choice (kept in <c>launcher.txt</c>, announced in
    /// Identify, carried in the roster like the hunter and the name), and a
    /// collision is resolved rather than allowed: <see cref="Resolve"/> walks
    /// the slots in order and moves anybody who would have come out identical
    /// to somebody earlier onto the next palette along.
    ///
    /// Two properties matter more than which colour anybody ends up in.
    /// **Every machine reaches the same answer**, because every machine runs
    /// this over the same roster in the same slot order -- a client that
    /// picked colours by arrival time would show a different match from its
    /// neighbour. And **your own choice is never the one that moves** unless
    /// somebody in a lower slot took it first, which is the fairest rule
    /// available and the only one that does not need a negotiation.
    ///
    /// Team modes are untouched. <c>GameState</c> assigns the two team
    /// palettes there, and what team you are on is the thing the colour is
    /// for.
    /// </summary>
    public static class PlayerColors
    {
        /// <summary>
        /// How many suits a player may choose between. Four, not six: the
        /// last two palettes are the team suits, and wearing one of those in a
        /// free-for-all reads as being on a team that does not exist.
        /// </summary>
        public const int Count = 4;

        /// <summary>
        /// What each slot asked for, by slot index. Filled from the server's
        /// roster in a networked match and from this machine's own preference
        /// otherwise; what is actually drawn is <see cref="Resolve"/>'s answer.
        /// </summary>
        public static readonly int[] Choice = new int[PlayerEntity.SlotCapacity];

        /// <summary>
        /// What each slot was last given, so a change can be said once rather
        /// than sixty times a second. -1 is "nothing yet".
        /// </summary>
        private static readonly int[] _applied = CreateApplied();

        private static int[] CreateApplied()
        {
            var applied = new int[PlayerEntity.SlotCapacity];
            Array.Fill(applied, -1);
            return applied;
        }

        public static void Reset()
        {
            Array.Clear(Choice);
            Array.Fill(_applied, -1);
        }

        public static int Clamp(int color)
        {
            return color < 0 || color >= Count ? 0 : color;
        }

        /// <summary>
        /// Give every active player a suit, and no two players of the same
        /// hunter the same one.
        ///
        /// Cheap enough to call every frame, and called that way for the same
        /// reason <see cref="NetSlotManager.Sync"/> is: the roster this reads
        /// arrives asynchronously, a player can change hunter between lives,
        /// and a rule that only ran at spawn would leave the wrong answer on
        /// screen until somebody died.
        /// </summary>
        public static void Resolve()
        {
            if (GameState.Teams)
            {
                return;
            }
            for (int slot = 0; slot < PlayerEntity.MaxPlayers
                && slot < PlayerEntity.Players.Count; slot++)
            {
                PlayerEntity player = PlayerEntity.Players[slot];
                if (player == null || !player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                int want = Clamp(Choice[slot]);
                int color = want;
                for (int step = 0; step < Count; step++)
                {
                    color = (want + step) % Count;
                    if (!TakenBefore(slot, player.Hunter, color))
                    {
                        break;
                    }
                }
                // With five players on one hunter the fifth wears a suit
                // somebody else already has -- there are four. Better than
                // refusing them a colour, and rare enough that spending a
                // fifth palette nobody drew on it would be worse.
                player.Recolor = color;
                if (_applied[slot] != color)
                {
                    _applied[slot] = color;
                    // Once per change, because "why is that Samus green" is a
                    // question a log should be able to answer, and because
                    // this is the one place the answer exists.
                    Console.WriteLine($"[net] slot {slot} ({player.Hunter}) wears suit "
                        + $"{color + 1}{(color == want ? "" : $" -- asked for {want + 1}")}");
                    NetLog.Event($"slot {slot} {player.Hunter} suit {color + 1} "
                        + $"(asked {want + 1})");
                }
                // The halfturret is Weavel's lower half and is drawn from its
                // own entity, which copied the owner's palette once, when it
                // was created. Left alone it keeps whatever colour that player
                // had at the start of the match.
                if (player.Halfturret != null)
                {
                    player.Halfturret.Recolor = color;
                }
            }
        }

        /// <summary>
        /// Whether an earlier slot already wears this suit on this hunter.
        ///
        /// Earlier only, and against what those slots have already been
        /// *given* rather than what they asked for: the pass runs in slot
        /// order, so by the time a slot is looked at every slot before it is
        /// settled. That is what makes one machine's answer the same as
        /// another's.
        /// </summary>
        private static bool TakenBefore(int slot, Hunter hunter, int color)
        {
            for (int i = 0; i < slot; i++)
            {
                PlayerEntity other = PlayerEntity.Players[i];
                if (other != null && other.LoadFlags.TestFlag(LoadFlags.Active)
                    && other.Hunter == hunter && other.Recolor == color)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
