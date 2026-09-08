using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Activates and deactivates player slots as peers come and go.
    ///
    /// Scene.AddPlayer is inert once the room has loaded
    /// (`if (!_roomLoaded)`), so every slot a session may ever need has to be
    /// built before the room does. NetLaunch builds all four and leaves the
    /// unoccupied ones inactive -- no standing bodies at spawn -- and this
    /// flips Active on the moment the server says somebody is in that slot.
    ///
    /// Keeping an inactive slot in the scene at all needs
    /// NetHooks.KeepSlotAlive: PlayerProcess returns false for an inactive
    /// multiplayer player, and Scene.UpdateScene destroys and unlists
    /// anything that returns false. Without that, the entity was gone by the
    /// end of the first frame and this class was switching flags on something
    /// nothing would ever call Process on again.
    /// </summary>
    public static class NetSlotManager
    {
        private static readonly bool[] _activated = new bool[PlayerEntity.SlotCapacity];

        public static void Reset() => Array.Clear(_activated);

        /// <summary>
        /// Bring the scene's active slots in line with the server's roster.
        /// Cheap enough to call every frame; only transitions do work.
        /// </summary>
        public static void Sync()
        {
            if (!NetSession.Active || NetSession.LocalSlot < 0)
            {
                return;
            }
            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                PlayerEntity? player = slot < PlayerEntity.Players.Count
                    ? PlayerEntity.Players[slot]
                    : null;
                if (player == null)
                {
                    continue;
                }
                bool occupied = slot == NetSession.LocalSlot
                    || (slot < NetSession.SlotOccupied.Length && NetSession.SlotOccupied[slot]);

                if (occupied && !_activated[slot])
                {
                    // Weapons.Current is populated by SceneSetup when the room
                    // loads, and Initialize() reads it. A peer can be rostered
                    // before that happens, so defer instead of dereferencing
                    // null -- Sync runs every frame and will pick this slot up
                    // as soon as the scene is ready.
                    if (Weapons.Current == null)
                    {
                        continue;
                    }
                    Activate(player, slot);
                }
                else if (occupied && slot != NetSession.LocalSlot
                    && NetSession.SlotHunter[slot] != player.Hunter)
                {
                    // The roster's first mention of a slot can precede the
                    // peer's Identify, in which case the hunter it carried was
                    // a default. Correct it whenever it is wrong rather than
                    // only before the player spawns: the alternative is
                    // wearing the wrong character for the rest of the map, and
                    // ModSetHunter makes this converge on the next frame.
                    player.ModSetHunter(NetSession.SlotHunter[slot]);
                    player.Initialize();
                    Console.WriteLine($"[net] slot {slot} is playing {player.Hunter}");
                    NetLog.Event($"slot {slot} is playing {player.Hunter}");
                }
                else if (!occupied && _activated[slot] && slot != NetSession.LocalSlot)
                {
                    Deactivate(player, slot);
                }
            }
        }

        private static void Activate(PlayerEntity player, int slot)
        {
            _activated[slot] = true;
            // Whoever is arriving is not whoever left. Every per-slot record
            // the net code keeps -- reported positions and frame numbers,
            // spawn barriers, divergence and staleness counters, the damage
            // sequence, and the score -- describes the previous occupant, and
            // inheriting it is what makes a rejoining player behave like a
            // stale one, or arrive holding somebody else's kills. See
            // NetPlayerBridge.ForgetSlot and NetScoreboard.ForgetSlot.
            NetPlayerBridge.ForgetSlot(slot);
            NetDamage.ForgetSlot(slot);
            NetSession.ForgetSlot(slot);
            NetScoreboard.ForgetSlot(slot);
            // The same flags Scene.AddPlayer sets, minus the bot marking:
            // a networked player is driven by relayed intent, not by AI.
            player.LoadFlags |= LoadFlags.SlotActive;
            player.LoadFlags |= LoadFlags.Active;
            player.LoadFlags |= LoadFlags.Initial;
            player.IsBot = false;
            player.BotLevel = 0;
            // TeamIndex defaults to -1 and Scene.AddPlayer only assigns it in
            // team modes, but GameState indexes TeamPoints/TeamKills by it
            // unconditionally -- EndIfPointGoalReached does TeamPoints[-1] and
            // throws the moment a Battle match is actually simulated. The
            // arrays hold four entries, so free-for-all can give each slot its
            // own index, exactly as PlayerEntity.Initialize does.
            //
            // Corrected against the mode, not merely filled in when missing.
            // A free-for-all in which two slots share a team index is not a
            // cosmetic fault: "same team" is what a bomb, a homing beam and
            // the Shock Coil's life drain all test before they do anything,
            // so a room where everybody is on team 0 is a room where no bomb
            // ever hurts anyone and the Shock Coil never heals its owner --
            // which is precisely the report about those weapons doing
            // nothing. It happened because the players were built from this
            // machine's own menu rather than from the server's mode (see
            // MatchStart), and a rule that only spoke up when the value was
            // out of range had nothing to say about eight players all
            // correctly holding zero.
            int wanted = GameState.Teams ? slot % 2 : slot;
            if (player.TeamIndex != wanted
                && (GameState.Teams
                    ? player.TeamIndex < 0 || player.TeamIndex > 1
                    : player.TeamIndex < 0 || player.TeamIndex >= PlayerEntity.MaxPlayers
                        || TeamIndexTaken(player.TeamIndex, slot)))
            {
                player.TeamIndex = wanted;
                player.Team = player.TeamIndex % 2 == 0 ? Team.Orange : Team.Green;
            }
            // The hunter comes from the server's roster, not from this
            // machine's menu: a client that used its own choice for every
            // slot drew the other player with the right name at the right
            // place wearing the wrong character.
            if (slot != NetSession.LocalSlot && NetSession.SlotHunter[slot] != player.Hunter)
            {
                player.ModSetHunter(NetSession.SlotHunter[slot]);
            }
            // Run the engine's own initialisation rather than reproducing
            // it. A slot switched on here never went through Initialize() or
            // Spawn(), so EquipInfo.Weapon stayed null and ProcessPlayer
            // threw the moment the match was actually simulated. Initialize()
            // rebuilds the models and equipment while preserving position,
            // facing and health.
            player.Initialize();
            PlayerEntity.PlayerCount = CountActive();
            Console.WriteLine($"[net] slot {slot} activated "
                + $"({GameState.Nicknames[slot]}) -- {PlayerEntity.PlayerCount} player(s) in scene");
            NetLog.Event($"slot {slot} activated ({GameState.Nicknames[slot]}), "
                + $"{PlayerEntity.PlayerCount} player(s) in scene");
        }

        /// <summary>
        /// Count the scene's active players rather than tracking a running
        /// total: several places adjust PlayerCount, and the increments
        /// compounded into a figure larger than the players present.
        /// </summary>
        private static int CountActive()
        {
            int count = 0;
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity? p = i < PlayerEntity.Players.Count ? PlayerEntity.Players[i] : null;
                if (p != null && p.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Empty a slot this machine has stopped being.
        ///
        /// Only one caller, and a narrow one: a client that reconnected and
        /// was given a different slot from the one it was playing. The player
        /// it left behind is nobody's now -- no intent will ever arrive for
        /// it, because the client that was sending them is this one -- and
        /// leaving it standing is the frozen twin the reconnection bug is
        /// named after. Everything else about slots is decided by whether
        /// intents keep arriving, which is right for every case but this one.
        /// </summary>
        public static void ReleaseSlot(int slot)
        {
            if (slot < 0 || slot >= PlayerEntity.SlotCapacity
                || slot >= PlayerEntity.Players.Count || !_activated[slot])
            {
                return;
            }
            Deactivate(PlayerEntity.Players[slot], slot);
        }

        /// <summary>
        /// Whether another active slot already holds this team index. Only
        /// asked in a free-for-all, where every player is their own team and
        /// two slots sharing one is the fault above.
        /// </summary>
        private static bool TeamIndexTaken(int teamIndex, int slot)
        {
            for (int i = 0; i < PlayerEntity.MaxPlayers && i < PlayerEntity.Players.Count; i++)
            {
                if (i == slot || !_activated[i])
                {
                    continue;
                }
                if (PlayerEntity.Players[i].TeamIndex == teamIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Deactivate(PlayerEntity player, int slot)
        {
            _activated[slot] = false;
            // On the way out as well as the way in: a slot can be filled again
            // before this machine has run a frame with it empty, and the
            // clearing has to happen either way round.
            NetPlayerBridge.ForgetSlot(slot);
            NetDamage.ForgetSlot(slot);
            NetSession.ForgetSlot(slot);
            // The score goes when they go, not only when somebody takes the
            // slot: a player who left is not on the board, and the board is
            // drawn from these while the slot stands empty.
            NetScoreboard.ForgetSlot(slot);
            player.LoadFlags &= ~LoadFlags.Active;
            // SlotActive is deliberately left on. It is what Scene.AddRoom and
            // Scene.OnLoad key off, and clearing it would mean this slot's
            // entity is no longer one the engine considers part of the match --
            // which is recoverable only by loading the room again.
            player.LoadFlags &= ~LoadFlags.Spawned;
            player.Health = 0;
            PlayerEntity.PlayerCount = Math.Max(CountActive(), 1);
            Console.WriteLine($"[net] slot {slot} deactivated -- player left");
            NetLog.Event($"slot {slot} deactivated");
        }
    }
}
