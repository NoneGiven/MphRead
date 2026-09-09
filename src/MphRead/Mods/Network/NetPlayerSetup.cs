using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// Turns freshly created player slots into network-driven players.
    ///
    /// Scene.AddPlayer marks every player after the first as a bot
    /// (`IsBot = PlayerCount >= 1`), which is right for a local match and
    /// wrong for a networked one: PlayerAi.ProcessInput then overwrites the
    /// Controls that remote intent had just filled in, so remote players
    /// behave like AI opponents instead of like the person driving them.
    /// That is what produced bots in the scene, and -- because an AI player
    /// runs damage and spawn logic the networked slot never initialised --
    /// the instant kills alongside them.
    ///
    /// Called after the room has loaded, because room setup is what assigns
    /// spawn points and can itself re-flag slots.
    /// </summary>
    public static class NetPlayerSetup
    {
        private static bool _applied;

        public static void Reset() => _applied = false;

        /// <summary>
        /// Mark every slot other than the local one as a remote player:
        /// present and active, but not AI-driven.
        /// </summary>
        public static void ApplyOnce()
        {
            if (_applied || !NetSession.Active)
            {
                return;
            }
            // The local slot is only known once the server's Welcome has
            // arrived; applying before that would label the wrong player.
            // Except on a server that simulates the match, which has no local
            // slot to wait for -- every slot there is somebody else's, which
            // is what the loop below does with local = -1.
            if (NetSession.LocalSlot < 0 && !NetSession.IsServer)
            {
                return;
            }
            _applied = true;

            int local = NetSession.LocalSlot;
            // The camera, the HUD and the intro-end check all key off
            // PlayerEntity.Main, which is Players[MainPlayerIndex] and
            // defaults to 0. A client on slot 1 was therefore never its own
            // main player: its intro sequence never ended, so it kept the
            // spectator camera and HUD and never spawned. Point Main at the
            // slot this machine actually drives.
            if (local >= 0 && local < PlayerEntity.MaxPlayers)
            {
                PlayerEntity.MainPlayerIndex = local;
            }
            for (int slot = 0; slot < PlayerEntity.MaxPlayers; slot++)
            {
                PlayerEntity? player = PlayerEntity.Players[slot];
                if (player == null)
                {
                    continue;
                }
                if (slot == local)
                {
                    player.IsBot = false;
                    continue;
                }
                // Remote: no AI, and no bot level to drive one.
                player.IsBot = false;
                player.BotLevel = 0;
            }
            Console.WriteLine($"[net] player slots prepared -- local slot {local}, "
                + $"{CountActive()} active, AI disabled on remote slots");
        }

        private static int CountActive()
        {
            int count = 0;
            for (int i = 0; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity? player = PlayerEntity.Players[i];
                if (player != null && player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
