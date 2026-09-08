using System;
using System.Collections.Generic;
using MphRead.Entities;

namespace MphRead.Mods
{
    /// <summary>
    /// Watching the match instead of playing it -- reached from the pause
    /// menu, multiplayer only. It opens on the map, on a free camera of its
    /// own (<see cref="FreeCamera"/>), and a left click moves into the
    /// players, one per click, in first person as if playing as them.
    ///
    /// The whole thing is one pointer swap: <see cref="PlayerEntity.Main"/>
    /// is already what the camera, the HUD and the weapon viewmodel all key
    /// off (see <c>Mods.Network.NetPlayerSetup</c>), so pointing
    /// <see cref="PlayerEntity.MainPlayerIndex"/> at somebody else's slot
    /// makes every one of those follow them for free. The one thing that
    /// pointer does not touch is whose slot real hardware input reaches --
    /// that is <c>Network.NetHooks.LocalSlot</c>, unchanged here -- so
    /// <c>Entities.PlayerEntity.ProcessInput</c> checks <see cref="IsSpectating"/>
    /// itself to stop applying input to the real local player while this is
    /// active, rather than this class reaching in to silence it.
    /// </summary>
    public static class SpectatorMode
    {
        public static bool IsSpectating { get; private set; }

        /// <summary>
        /// Looking around the map on the spectator's own camera, rather than
        /// out of some player's eyes. Where spectating starts.
        ///
        /// Owned by the scene, which is what actually holds the camera:
        /// <c>Scene.SetFreeCamera</c> reports the change here through
        /// <see cref="NoteFreeCamera"/> rather than this class trying to keep
        /// a second copy of the truth. Space toggles it (see the render
        /// window's key handling) and the first left click leaves it.
        /// </summary>
        public static bool FreeCamera { get; private set; }

        /// <summary>
        /// The camera this mode wants, for the render loop to act on: true
        /// for the free one, false for a player's, null for nothing pending.
        ///
        /// Both callers -- the pause menu's Spectate and Rejoin entries --
        /// run on the game's own thread, but neither has the scene to hand,
        /// and the camera is the scene's. So they leave the decision here and
        /// <c>Scene.OnRenderFrame</c> takes it between frames, the same shape
        /// <see cref="PauseMenu"/> uses for the window work it cannot do
        /// from a click handler either.
        /// </summary>
        private static bool? _cameraRequest;

        /// <summary>Hidden in the adventure/single-player pause menu -- there is nobody else to watch.</summary>
        public static bool CanSpectate => GameState.Multiplayer;

        /// <param name="watchSomeone">
        /// Skip the overview and go straight to a player, for demo playback,
        /// which has no view of its own to have just left.
        /// </param>
        public static void Start(bool watchSomeone = false)
        {
            if (IsSpectating || !CanSpectate)
            {
                return;
            }
            int next = FindNextActiveSlot(PlayerEntity.MainPlayerIndex);
            if (watchSomeone && next == -1)
            {
                // Nobody to watch yet: the demo path calls this every frame
                // until there is somebody, so this is "not yet", not "no".
                return;
            }
            IsSpectating = true;
            // Hidden and non-solid on every client, like Quake 3's
            // spectator -- not just a body left standing still. Set on the
            // real local entity (not whoever Main points at); NetSession
            // reads this flag into the outgoing snapshot for everyone else.
            int localSlot = Network.NetHooks.LocalSlot;
            if (localSlot >= 0 && localSlot < PlayerEntity.Players.Count)
            {
                PlayerEntity.Players[localSlot].ModSetSpectating(true);
            }
            if (watchSomeone)
            {
                Switch(next);
                return;
            }
            // The overview first, whether or not there is anybody to watch.
            // Spectating is "I am out of the match and looking at it", and
            // being dropped into a stranger's first-person view the instant
            // you ask for it is a jump cut that also loses the thing worth
            // having -- the map itself. A click moves on to the players; the
            // camera starts where the player's own view was, so this reads as
            // stepping out of your body rather than as a cut somewhere else.
            _cameraRequest = true;
        }

        /// <summary>
        /// Left click, while spectating: into the players, and then on to the
        /// next one each click after that.
        /// </summary>
        public static void CycleNext()
        {
            if (!IsSpectating)
            {
                return;
            }
            int next = FindNextActiveSlot(PlayerEntity.MainPlayerIndex);
            if (next == -1)
            {
                // Nobody to switch to. In the overview that means the click
                // does nothing, which is better than dropping the free camera
                // to follow a player who is not there.
                return;
            }
            if (FreeCamera)
            {
                _cameraRequest = false;
            }
            Switch(next);
        }

        /// <summary>
        /// Space, while spectating: the map, or the player you were watching.
        ///
        /// Never your own body, which is what toggling the camera directly
        /// did -- a spectator's own view is of a hidden, frozen hunter taking
        /// no input, and it came with their HUD back on. Leaving the overview
        /// therefore means picking somebody, exactly as a click does, and
        /// where there is nobody to pick it means staying where you are.
        /// </summary>
        public static void ToggleView()
        {
            if (!IsSpectating)
            {
                return;
            }
            if (FreeCamera)
            {
                CycleNext();
                return;
            }
            _cameraRequest = true;
        }

        /// <summary>
        /// The spectator holding the show-score button, which is the one
        /// control they keep.
        ///
        /// A scoreboard is the match's and not a player's, so it is the piece
        /// of HUD that still means something to somebody who is only
        /// watching -- and on the free camera it is the only thing on screen
        /// at all. It cannot come from the usual place: the keybind states
        /// every other control reads are updated in the loop that spectating
        /// steps out of, so <c>PlayerEntity.ProcessInput</c> reads this
        /// one off the keyboard itself and leaves it here.
        /// </summary>
        public static bool ShowScoreboard { get; private set; }

        /// <summary>The input pass reporting the show-score button. See <see cref="ShowScoreboard"/>.</summary>
        internal static void NoteScoreboard(bool down)
        {
            ShowScoreboard = down && IsSpectating;
        }

        /// <summary>The scene reporting what it did with the camera. See <see cref="FreeCamera"/>.</summary>
        internal static void NoteFreeCamera(bool on)
        {
            FreeCamera = on;
        }

        /// <summary>The render loop taking the pending camera change, if there is one.</summary>
        internal static bool? TakeCameraRequest()
        {
            bool? request = _cameraRequest;
            _cameraRequest = null;
            return request;
        }

        /// <summary>
        /// SetUpHud only ever ran, at spawn, for whoever was Main at the
        /// time -- every bot and every other connected player's HUD fields
        /// are still null. Build them here, once, the first time anyone
        /// points the camera at that player, rather than trying to build
        /// a HUD for all eight slots up front for players nobody may ever
        /// spectate.
        /// </summary>
        private static void Switch(int slot)
        {
            PlayerEntity target = PlayerEntity.Players[slot];
            if (!target.HudReady)
            {
                target.SetUpHud();
            }
            PlayerEntity.MainPlayerIndex = slot;
            // RoomEntity.UpdateRoomParts walks the portal graph outward from
            // Main.CameraInfo.NodeRef to decide which room geometry is
            // active this frame -- not from the player's own NodeRef, which
            // PlayerProcess already keeps current. CameraInfo.NodeRef only
            // gets the same treatment while its owner *is* Main (see
            // PlayerProcess.cs's node-ref sync block), so a player who has
            // never been Main before starts this walk from whatever stale
            // value the field was left at, not from wherever they actually
            // are -- which is where the room started loading only the
            // geometry near their spawn point instead of their real spot.
            target.CameraInfo.NodeRef = target.NodeRef;
        }

        /// <summary>
        /// Back into the match. The score resets because time spent
        /// spectating was time not playing -- picking the match back up with
        /// whatever score was left standing would credit or fault a period
        /// nothing was actually being played.
        ///
        /// **It never resets the score upwards.** A score below zero is a
        /// penalty -- this game subtracts a point for killing yourself -- and
        /// clearing it by spectating for a second and rejoining would make
        /// the pause menu the cheapest way out of one. So a negative score is
        /// left exactly where it was, and only a positive one goes back to
        /// zero.
        /// </summary>
        public static void Rejoin()
        {
            if (!IsSpectating)
            {
                return;
            }
            int localSlot = Network.NetHooks.LocalSlot;
            PlayerEntity.MainPlayerIndex = localSlot;
            IsSpectating = false;
            ShowScoreboard = false;
            // Back behind your own eyes, whichever of the two spectator
            // cameras was up.
            _cameraRequest = false;
            if (localSlot >= 0 && localSlot < GameState.Points.Length)
            {
                PlayerEntity.Players[localSlot].ModSetSpectating(false);
                GameState.Points[localSlot] = Math.Min(0, GameState.Points[localSlot]);
                GameState.Kills[localSlot] = 0;
                GameState.Deaths[localSlot] = 0;
            }
        }

        /// <summary>Forget spectating without the rejoin bookkeeping -- the match itself is ending.</summary>
        public static void Reset()
        {
            IsSpectating = false;
            FreeCamera = false;
            ShowScoreboard = false;
            _cameraRequest = null;
        }

        private static int FindNextActiveSlot(int fromSlot)
        {
            int localSlot = Network.NetHooks.LocalSlot;
            IReadOnlyList<PlayerEntity> players = PlayerEntity.Players;
            for (int offset = 1; offset <= players.Count; offset++)
            {
                int index = (fromSlot + offset) % players.Count;
                if (index == localSlot)
                {
                    continue;
                }
                PlayerEntity candidate = players[index];
                // Spawned and alive, not just Active: Active alone can be
                // true for a slot that exists but has not actually been
                // placed in the map yet (see BuildPlayers/NetSlotManager),
                // which during demo playback showed up as a body with no
                // model and no textures for a frame or more -- Main pointed
                // at a player camera code did not yet consider ready to draw.
                if (candidate.LoadFlags.TestFlag(LoadFlags.Active)
                    && candidate.LoadFlags.TestFlag(LoadFlags.Spawned) && candidate.Health > 0)
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
