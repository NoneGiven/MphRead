using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Sound;
using MphRead.Text;

namespace MphRead
{
    public enum MatchState
    {
        InProgress = 0,
        GameOver = 1,
        Ending = 2,
        Disconnected = 3
    }

    public enum TransitionState
    {
        None = 0,
        Start = 1,
        Process = 2,
        End = 3
    }

    public enum EscapeState
    {
        None = 0,
        Event = 1,
        Escape = 2
    }

    public static class GameState
    {
        public static bool MenuPause { get; private set; }
        public static bool DialogPause { get; private set; }
        public static MatchState MatchState { get; set; } = MatchState.InProgress;
        public static TransitionState TransitionState { get; set; } = TransitionState.None;
        public static bool InRoomTransition => TransitionState != TransitionState.None;
        public static EscapeState EscapeState { get; set; } = EscapeState.None;
        public static float EscapeTimer { get; set; } = -1;
        public static bool EscapePaused { get; set; }
        public static int TransitionRoomId { get; set; } = -1;
        public static bool TransitionAltForm { get; set; }
        public static int ActivePlayers { get; set; } = 0;
        public static string[] Nicknames { get; } = new string[4] { "Player1", "Player2", "Player3", "Player4" };
        public static int[] Stars { get; } = new int[4];
        public static int[] Standings { get; } = new int[4];
        public static int[] TeamStandings { get; } = new int[4];
        public static int[] ResultSlots { get; } = new int[4]; // ordered by team rank, then by player rank
        public static int PrimeHunter { get; set; } = -1;

        public static bool Teams { get; set; } = false;
        public static bool FriendlyFire { get; set; } = false;
        public static int PointGoal { get; set; } = 0; // also used for starting extra lives
        public static float TimeGoal { get; set; } = 0; // also used for starting extra lives
        public static int DamageLevel { get; set; } = 1;
        public static bool OctolithReset { get; set; } = false;
        public static bool RadarPlayers { get; set; } = false;
        public static bool AffinityWeapons { get; set; } = false;

        public static float MatchTime { get; set; } = -1;
        public static bool ForceEndGame { get; set; } = false;

        public static int[] Points { get; } = new int[4];
        public static int[] TeamPoints { get; } = new int[4];
        public static int[] Kills { get; } = new int[4];
        public static int[] TeamKills { get; } = new int[4];
        public static int[] Deaths { get; } = new int[4];
        public static int[] TeamDeaths { get; } = new int[4];
        public static float[] Time { get; } = new float[4]; // used for prime hunter time, player survival time
        public static float[] TeamTime { get; } = new float[4]; // used for defense time, max team survival time
        public static int[] BeamDamageMax { get; } = new int[4];
        public static int[] BeamDamageDealt { get; } = new int[4];
        public static int[] DamageCount { get; } = new int[4];
        public static int[] AltDamageCount { get; } = new int[4];
        public static int[] KillStreak { get; } = new int[4];
        public static int[] Suicides { get; } = new int[4];
        public static int[] FriendlyKills { get; } = new int[4];
        public static int[] HeadshotKills { get; } = new int[4];
        public static int[,] BeamKills { get; } = new int[4, 9];

        public static int[] OctolithScores { get; } = new int[4]; // field260 in-game
        public static int[] OctolithDrops { get; } = new int[4]; // field268 in-game
        public static int[] OctolithStops { get; } = new int[4]; // field270 in-game

        public static int[] NodesCaptured { get; } = new int[4]; // field260 in-game
        public static int[] NodesLost { get; } = new int[4]; // field268 in-game

        public static int[] KillsAsPrime { get; } = new int[4]; // field260 in-game
        public static int[] PrimesKilled { get; } = new int[4]; // field268 in-game

        public static Action<Scene> ModeState { get; private set; } = ModeStateAdventure;
        private static bool _pausingDialog = false;
        private static bool _unpausingDialog = false;
        private static bool _pausingMenu = false;
        private static bool _unpausingMenu = false;

        public static void PauseMenu()
        {
            _pausingMenu = true;
        }

        public static void UnpauseMenu()
        {
            _unpausingMenu = true;
        }

        public static void PauseDialog()
        {
            _pausingDialog = true;
        }

        public static void UnpauseDialog()
        {
            _unpausingDialog = true;
        }

        public static void ApplyPause()
        {
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            if (_pausingDialog)
            {
                DialogPause = true;
            }
            if (_unpausingDialog)
            {
                DialogPause = false;
            }
            if (_pausingMenu)
            {
                MenuPause = true;
            }
            if (_unpausingMenu)
            {
                MenuPause = false;
            }
            _pausingDialog = false;
            _unpausingDialog = false;
            _pausingMenu = false;
            _unpausingMenu = false;
        }

        public static void Setup(Scene scene)
        {
            GameMode mode = scene.GameMode;
            if (mode == GameMode.BattleTeams || mode == GameMode.SurvivalTeams || mode == GameMode.Capture
                || mode == GameMode.BountyTeams || mode == GameMode.NodesTeams || mode == GameMode.DefenderTeams)
            {
                Teams = true;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = PlayerEntity.Players[i];
                    if (player.LoadFlags.TestFlag(LoadFlags.Active))
                    {
                        player.Team = player.TeamIndex == 0 ? Team.Orange : Team.Green;
                        // todo: allow other colors (and I guess disable the emission then too)
                        player.Recolor = player.TeamIndex == 0 ? 4 : 5;
                    }
                }
            }
            ModeState = ModeStateAdventure;
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
            {
                PointGoal = 7;
                MatchTime = 7 * 60;
                ModeState = ModeStateBattle;
            }
            else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                PointGoal = 2; // spare lives
                MatchTime = 15 * 60;
                ModeState = ModeStateSurvival;
            }
            else if (mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                PointGoal = 3;
                MatchTime = 15 * 60;
                ModeState = ModeStateBounty;
            }
            else if (mode == GameMode.Capture)
            {
                PointGoal = 5;
                MatchTime = 15 * 60;
                ModeState = ModeStateCapture;
            }
            else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                TimeGoal = 1.5f * 60;
                MatchTime = 15 * 60;
                ModeState = ModeStateDefender;
            }
            else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                PointGoal = 70;
                MatchTime = 15 * 60;
                ModeState = ModeStateNodes;
            }
            else if (mode == GameMode.PrimeHunter)
            {
                TimeGoal = 1.5f * 60;
                MatchTime = 15 * 60;
                ModeState = ModeStatePrimeHunter;
            }
            if (CameraSequence.Intro != null)
            {
                CameraSequence.Intro.Initialize();
                CameraSequence.Intro.SetUp(PlayerEntity.Main.CameraInfo, transitionTime: 0);
                CameraSequence.Intro.Flags |= CamSeqFlags.Loop;
                scene.SetFade(FadeType.FadeInBlack, 20 / 30f, overwrite: true);
            }
            ForceEndGame = false;
            _stateChanged = false;
            _lastAlarmTime = 0;
            _nextAlarmIndex = 0;
        }

        public static void UpdateTime(Scene scene)
        {
            // todo: update license info etc.
            if (MatchTime > 0)
            {
                MatchTime = MathF.Max(MatchTime - scene.FrameTime, 0);
            }
        }

        private static bool _stateChanged = false;
        private static float _matchEndTime = 0;
        private static float _lastAlarmTime = 0;
        private static int _nextAlarmIndex = 0;
        private static readonly IReadOnlyList<float> _alarmIntervals = new float[4]
        {
            1 / 30f, 8 / 30f, 15 / 30f, 6 / 30f
        };

        public static void ProcessFrame(Scene scene)
        {
            if (scene.Multiplayer && CameraSequence.Current?.IsIntro == true)
            {
                Debug.Assert(CameraSequence.Current.CamInfoRef == PlayerEntity.Main.CameraInfo);
                CameraSequence.Current.Process();
            }
            if (MatchState == MatchState.InProgress)
            {
                // todo: process dialogs or something for 1P
                // todo: update SFX
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.Complete && message.ExecuteFrame == scene.FrameCount)
                    {
                        MatchTime = 0;
                    }
                }
                // todo: update MP playtime to license info
                if (scene.Multiplayer && !Features.AllowInvalidTeams)
                {
                    bool invalid = PlayerEntity.MaxPlayers < 2;
                    if (!invalid && Teams)
                    {
                        bool[] teams = new bool[2];
                        for (int i = 0; i < 4; i++)
                        {
                            PlayerEntity player = PlayerEntity.Players[i];
                            if (player.LoadFlags.TestFlag(LoadFlags.Active))
                            {
                                teams[player.TeamIndex] = true;
                            }
                        }
                        invalid = !teams[0] || !teams[1];
                    }
                    if (invalid)
                    {
                        MatchTime = 0;
                        CameraSequence.Current?.End();
                        // todo: stop music/SFX, state bits/disconnect message?
                    }
                }
                ModeState(scene);
                if (!scene.Multiplayer && EscapeTimer != -1)
                {
                    // bugfix?: this fade check seems to count things like the Omega Cannon flash
                    if (!EscapePaused && !MenuPause && !DialogPause && scene.FadeType == FadeType.None
                        && CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) != true)
                    {
                        EscapeTimer -= scene.FrameTime;
                        if (EscapeState == EscapeState.Escape)
                        {
                            UpdateEscapeSounds(EscapeTimer);
                        }
                        else
                        {
                            UpdateEventSounds(EscapeTimer);
                        }
                    }
                    if (EscapeTimer <= 0)
                    {
                        if (EscapeState == EscapeState.Escape && PlayerEntity.Main.Health > 0)
                        {
                            scene.SendMessage(Message.Death, null!, PlayerEntity.Main, 0, 0);
                        }
                        EscapeTimer = -1;
                    }
                }
                if (MatchTime != 0 && !ForceEndGame)
                {
                    // mustodo: update music
                    if (scene.Multiplayer)
                    {
                        var time = TimeSpan.FromSeconds(MatchTime);
                        if (time.TotalMinutes < 1 && time.Seconds <= 9)
                        {
                            float comparison = 1;
                            if (time.Seconds <= 5)
                            {
                                if (Features.HalfSecondAlarm)
                                {
                                    comparison = 0.5f;
                                }
                                else
                                {
                                    comparison = _alarmIntervals[_nextAlarmIndex];
                                }
                            }
                            if (_lastAlarmTime == 0 || scene.ElapsedTime - _lastAlarmTime >= comparison)
                            {
                                Sfx.Instance.PlaySample((int)SfxId.ALARM, source: null, loop: false,
                                    noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                                _lastAlarmTime = scene.ElapsedTime;
                                _nextAlarmIndex++;
                                if (_nextAlarmIndex >= _alarmIntervals.Count)
                                {
                                    _nextAlarmIndex = 0;
                                }
                            }
                        }
                    }
                }
                else
                {
                    PlayerEntity.Main.HudEndDisrupted();
                    if ((scene.GameMode == GameMode.Survival || scene.GameMode == GameMode.SurvivalTeams) && !ForceEndGame)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            PlayerEntity player = PlayerEntity.Players[i];
                            // the game also checks if the player's time is greater than or equal to the time goal,
                            // which in survival is always zero, so the check isn't needed
                            if (player.LoadFlags.TestFlag(LoadFlags.Active)
                                && (player.Health > 0 || TeamDeaths[player.TeamIndex] <= PointGoal))
                            {
                                Time[i] = -1;
                                TeamTime[player.TeamIndex] = -1;
                            }
                        }
                        UpdateState(scene);
                    }
                    // todo: 1P time up? isn't that handled by death countdown etc.?
                    MatchState = MatchState.GameOver;
                    MatchTime = 90 / 30f;
                    scene.SetFade(FadeType.None, length: 0, overwrite: true);
                    _stateChanged = true;
                    _matchEndTime = scene.ElapsedTime;
                    Sfx.Instance.StopFreeSfxScripts();
                    Sfx.Instance.StopAllSound();
                    PlayerEntity.Main.StopLongSfx();
                    // sfxtodo: stop more kinds of SFX?
                    // mustodo: stop music and play timeout jingle
                }
            }
            else if (MatchState == MatchState.GameOver)
            {
                PlayerEntity winner = PlayerEntity.Players[ResultSlots[0]];
                if (winner.Health > 0 && winner.LoadFlags.TestFlag(LoadFlags.Active)
                    && winner.LoadFlags.TestFlag(LoadFlags.Spawned))
                {
                    if (_stateChanged)
                    {
                        _stateChanged = false;
                        winner.SetUpMatchEndCamera();
                    }
                    PlayerEntity.Main.UpdateMatchEndCamera(winner, scene.ElapsedTime - _matchEndTime);
                }
                else
                {
                    EnsureIntroCamSeq(scene);
                }
                if (MatchTime == 0)
                {
                    MatchState = MatchState.Ending;
                    MatchTime = 150 / 30f;
                    // todo: update license info, stop SFX
                }
            }
            else if (MatchState == MatchState.Ending)
            {
                EnsureIntroCamSeq(scene);
                // todo: more stuff?
                if (MatchTime == 0)
                {
                    MatchTime = -1;
                    scene.SetFade(FadeType.FadeOutBlack, 20 / 30f, overwrite: true, AfterFade.Exit);
                }
            }
        }

        private static void EnsureIntroCamSeq(Scene scene)
        {
            if (scene.Multiplayer && CameraSequence.Current == null && CameraSequence.Intro != null)
            {
                CameraSequence.Intro.SetUp(PlayerEntity.Main.CameraInfo, transitionTime: 0);
                PlayerEntity.Main.CameraInfo.Update();
                CameraSequence.Intro.Flags |= CamSeqFlags.Loop;
            }
        }

        public static AreaState GetAreaState(int areaId)
        {
            if (StorySave == null)
            {
                return AreaState.None;
            }
            return (AreaState)(((int)StorySave.BossFlags >> (2 * areaId)) & 3);
        }

        public static void ModeStateAdventure(Scene scene)
        {
            PlayerEntity.Main.SaveStatus();
            if ((StorySave.Areas & 0x100) == 0)
            {
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.UnlockOubliette && message.ExecuteFrame == scene.FrameCount)
                    {
                        if (StorySave.CurrentOctoliths == 0xFF)
                        {
                            // todo: play movie and defer dialog
                            StorySave.Areas |= 0x100;
                            StorySave.CurrentOctoliths = 0;
                            // GUNSHIP TRANSMISSION severe timefield disruption detected in the vicinity of the ALIMBIC CLUSTER.
                            PlayerEntity.Main.ShowDialog(DialogType.Okay, messageId: 43);
                        }
                        else
                        {
                            for (int j = 0; j < scene.Entities.Count; j++)
                            {
                                EntityBase entity = scene.Entities[j];
                                if (entity.Type == EntityType.CameraSequence)
                                {
                                    scene.SendMessage(Message.Activate, null!, entity, param1: 0, param2: 0);
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            if (PlayerEntity.Main.Health > 0)
            {
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.Checkpoint && message.ExecuteFrame == scene.FrameCount)
                    {
                        Debug.Assert(scene.Room != null);
                        scene.SendMessage(Message.SetActive, null!, message.Sender, param1: 0, param2: 0);
                        StorySave.CheckpointEntityId = message.Sender.Id;
                        StorySave.CheckpointRoomId = scene.Room.RoomId;
                        UpdateCleanSave(force: false);
                        break;
                    }
                }
            }
            // todo: game timer/boss record stuff
        }

        private static void EndIfPointGoalReached()
        {
            if (PointGoal <= 0)
            {
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.LoadFlags.TestFlag(LoadFlags.Active) && TeamPoints[player.TeamIndex] >= PointGoal)
                {
                    // deal with multiple nodes points on the same frame
                    TeamPoints[player.TeamIndex] = PointGoal;
                    MatchTime = 0;
                    break;
                }
            }
        }

        public static void ModeStateBattle(Scene scene)
        {
            EndIfPointGoalReached();
        }

        public static void ModeStateSurvival(Scene scene)
        {
            RadarPlayers = false;
            int playersAlive = 0;
            int botsAlive = 0;
            bool[] teamsAlive = new bool[2];
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.LoadFlags.TestFlag(LoadFlags.Active)
                    && (player.Health > 0 || TeamDeaths[player.TeamIndex] <= PointGoal))
                {
                    Time[i] += scene.FrameTime;
                    if (player.IsBot)
                    {
                        botsAlive++;
                    }
                    else
                    {
                        playersAlive++;
                    }
                    if (Teams)
                    {
                        Debug.Assert(player.TeamIndex == 0 || player.TeamIndex == 1);
                        teamsAlive[player.TeamIndex] = true;
                    }
                }
            }
            if (playersAlive == 0 || playersAlive + botsAlive < 2 || Teams && (!teamsAlive[0] || !teamsAlive[1]))
            {
                MatchTime = 0;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = PlayerEntity.Players[i];
                    if (player.LoadFlags.TestFlag(LoadFlags.Active)
                        && (player.Health > 0 || TeamDeaths[player.TeamIndex] <= PointGoal))
                    {
                        Time[i] = -1; // MAX
                    }
                }
            }
            else if (playersAlive + botsAlive == 2 && PlayerEntity.PlayerCount > 2)
            {
                RadarPlayers = true;
            }
        }

        public static void ModeStateCapture(Scene scene)
        {
            EndIfPointGoalReached();
        }

        public static void ModeStateBounty(Scene scene)
        {
            EndIfPointGoalReached();
        }

        public static void ModeStateDefender(Scene scene)
        {
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.LoadFlags.TestFlag(LoadFlags.Active) && TeamTime[player.TeamIndex] >= TimeGoal)
                {
                    MatchTime = 0;
                    break;
                }
            }
        }

        public static void ModeStateNodes(Scene scene)
        {
            EndIfPointGoalReached();
        }

        public static void ModeStatePrimeHunter(Scene scene)
        {
            if (PrimeHunter == -1)
            {
                return;
            }
            PlayerEntity player = PlayerEntity.Players[PrimeHunter];
            if (!player.LoadFlags.TestFlag(LoadFlags.Active))
            {
                PrimeHunter = -1;
                return;
            }
            if (scene.FrameCount % (10 * 2) == 0) // todo: FPS stuff
            {
                player.TakeDamage(1, DamageFlags.NoDmgInvuln, direction: null, source: null);
            }
            if (PrimeHunter != -1)
            {
                Time[PrimeHunter] += scene.FrameTime;
                if (Time[PrimeHunter] >= TimeGoal)
                {
                    MatchTime = 0;
                }
            }
        }

        private static bool _shownOctolithDialog = false;
        private static bool _whiteoutStarted = false;
        private static bool _gameOverShown = false;

        public static void UpdateFrame(Scene scene)
        {
            PromptType prompt = PlayerEntity.Main.DialogPromptType;
            ConfirmState confirm = PlayerEntity.Main.DialogConfirmState;

            void Quit()
            {
                scene.SetFade(FadeType.FadeOutBlack, length: 10 / 30f, overwrite: true, AfterFade.Exit);
                // mustodo: stop music
                Sfx.Instance.PlaySample((int)SfxId.QUIT_GAME, source: null, loop: false,
                    noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
            }

            if (prompt != PromptType.Any && confirm != ConfirmState.Okay)
            {
                Sfx.Instance.StopFreeSfxScripts();
                if (confirm == ConfirmState.Yes)
                {
                    if (prompt == PromptType.ShipHatch)
                    {
                        // yes to ship hatch (enter)
                        EnterShip(); // the game does this in the cockpit
                        scene.SetFade(FadeType.FadeOutWhite, length: 20 / 30f, overwrite: true, AfterFade.EnterShip);
                        // mustodo: stop music
                        // todo: fade SFX
                        Sfx.Instance.PlaySample((int)SfxId.RETURN_TO_SHIP_YES, source: null, loop: false,
                            noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                    }
                    else if (prompt == PromptType.GameOver)
                    {
                        // yes to game over (continue)
                        RestoreCleanSave();
                        Debug.Assert(scene.Room != null);
                        if (Cheats.ContinueFromCurrentRoom)
                        {
                            if (StorySave.CheckpointRoomId != scene.Room.RoomId)
                            {
                                StorySave.CheckpointEntityId = -1;
                            }
                            StorySave.CheckpointRoomId = scene.Room.RoomId;
                        }
                        else if (StorySave.CheckpointRoomId == -1)
                        {
                            StorySave.CheckpointEntityId = -1;
                            if (scene.AreaId == 0 || scene.AreaId == 1) // Alinos 1/2
                            {
                                StorySave.CheckpointRoomId = 27; // UNIT1_LAND
                            }
                            else if (scene.AreaId == 2 || scene.AreaId == 3) // CA 1/2
                            {
                                StorySave.CheckpointRoomId = 45; // UNIT2_LAND
                            }
                            else if (scene.AreaId == 4 || scene.AreaId == 5) // VDO 1/2
                            {
                                StorySave.CheckpointRoomId = 65; // UNIT3_LAND
                            }
                            else if (scene.AreaId == 6 || scene.AreaId == 7) // Arcterra 1/2
                            {
                                StorySave.CheckpointRoomId = 77; // UNIT4_LAND
                            }
                            else if (scene.AreaId == 8) // Oubliette
                            {
                                StorySave.CheckpointRoomId = 89; // Gorea_Land
                            }
                        }
                        if (StorySave.CheckpointRoomId == -1)
                        {
                            Quit();
                        }
                        // if CheckpointEntityId isn't set, we'll still spawn, just using the respawn point code path
                        TransitionRoomId = StorySave.CheckpointRoomId;
                        Sfx.Instance.PlaySample((int)SfxId.MENU_CONFIRM, source: null, loop: false,
                            noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                        scene.SetFade(FadeType.FadeOutWhite, length: 10 / 30f, overwrite: true, AfterFade.LoadRoom);
                        UnpauseDialog();
                        PlayerEntity.Main.RestartLongSfx(force: true);
                    }
                }
                else if (prompt == PromptType.GameOver)
                {
                    // no to game over (quit)
                    Quit();
                }
                else
                {
                    // no to ship hatch (resume)
                    PlayerEntity.Main.RestartLongSfx();
                    Sfx.Instance.PlaySample((int)SfxId.RETURN_TO_SHIP_NO, source: null, loop: false,
                        noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                    UnpauseDialog();
                }
                PlayerEntity.Main.DialogPromptType = PromptType.Any;
                PlayerEntity.Main.DialogConfirmState = ConfirmState.Okay;
            }
            if (!DialogPause)
            {
                if (PlayerEntity.Main.Health > 0)
                {
                    for (int i = 0; i < scene.MessageQueue.Count; i++)
                    {
                        MessageInfo message = scene.MessageQueue[i];
                        if (message.Message == Message.ShipHatch && message.ExecuteFrame == scene.FrameCount)
                        {
                            Debug.Assert(scene.Room != null);
                            ResetEscapeState(updateSounds: false); // skdebug
                            PlayerEntity.Main.DialogPromptType = PromptType.ShipHatch;
                            StorySave.CheckpointEntityId = message.Sender.Id;
                            StorySave.CheckpointRoomId = scene.Room.RoomId;
                            UpdateCleanSave(force: true);
                            // HUNTER GUNSHIP enter your ship?
                            PlayerEntity.Main.ShowDialog(DialogType.YesNo, messageId: 1);
                            Sfx.Instance.StopFreeSfxScripts();
                            Sfx.Instance.PlayScript((int)SfxId.RETURN_TO_SHIP_SCR, source: null,
                                noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                            break;
                        }
                    }
                    for (int i = 0; i < scene.MessageQueue.Count; i++)
                    {
                        MessageInfo message = scene.MessageQueue[i];
                        if (message.Message == Message.EscapeUpdate1 && message.ExecuteFrame == scene.FrameCount)
                        {
                            UpdateEscapeState((int)message.Param1 * 30, (int)message.Param2);
                        }
                    }
                    for (int i = 0; i < scene.MessageQueue.Count; i++)
                    {
                        MessageInfo message = scene.MessageQueue[i];
                        if (message.Message == Message.EscapeUpdate2 && message.ExecuteFrame == scene.FrameCount)
                        {
                            UpdateEscapeState((int)message.Param1, (int)message.Param2);
                        }
                    }
                    for (int i = 0; i < scene.MessageQueue.Count; i++)
                    {
                        MessageInfo message = scene.MessageQueue[i];
                        if (message.Message == Message.ShowPrompt && message.ExecuteFrame == scene.FrameCount)
                        {
                            int promptType = (int)message.Param2;
                            if (promptType == 0)
                            {
                                PlayerEntity.Main.ShowDialog(DialogType.Okay, messageId: (int)message.Param1);
                            }
                            else if (promptType == 1)
                            {
                                PlayerEntity.Main.ShowDialog(DialogType.YesNo, messageId: (int)message.Param1);
                            }
                        }
                    }
                }
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.ShowWarning && message.ExecuteFrame == scene.FrameCount)
                    {
                        int messageId = (int)message.Param1;
                        int duration = (int)message.Param2;
                        if (duration == 0)
                        {
                            duration = 15;
                        }
                        PlayerEntity.Main.ShowDialog(DialogType.Overlay, messageId, param1: duration, param2: 1);
                    }
                }
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.ShowOverlay && message.ExecuteFrame == scene.FrameCount)
                    {
                        int messageId = (int)message.Param1;
                        int duration = (int)message.Param2;
                        PlayerEntity.Main.ShowDialog(DialogType.Overlay, messageId, param1: duration, param2: 0);
                    }
                }
            }
            float countdown = PlayerEntity.Main.DeathCountdown;
            if (!scene.Multiplayer && PlayerEntity.Main.Health == 0 && countdown > 0)
            {
                if (countdown >= 145 / 30f)
                {
                    _shownOctolithDialog = false;
                    _whiteoutStarted = false;
                    _gameOverShown = false;
                    if (EscapeState == EscapeState.Escape)
                    {
                        // EMERGENCY security system activated.
                        PlayerEntity.Main.ShowDialog(DialogType.Hud, messageId: 120, param1: 69, param2: 1);
                    }
                    else
                    {
                        // EMERGENCY POWER SUIT energy is depleted.
                        PlayerEntity.Main.ShowDialog(DialogType.Hud, messageId: 116, param1: 45, param2: 1);
                    }
                }
                else if (countdown <= 1 / 30f && !_gameOverShown)
                {
                    PlayerEntity.Main.CameraInfo.SetShake(0);
                    //ENERGY DEPLETED continue from last checkpoint?
                    PlayerEntity.Main.DialogPromptType = PromptType.GameOver;
                    PlayerEntity.Main.ShowDialog(DialogType.YesNo, messageId: 2);
                    ResetEscapeState(updateSounds: true); // the game does when reloading the room
                    _gameOverShown = true;
                }
                else if (countdown <= 50 / 30f && !_whiteoutStarted)
                {
                    PlayerEntity.Main.BeginWhiteout();
                    _whiteoutStarted = true;
                }
                else if (countdown <= 90 / 30f && !_shownOctolithDialog)
                {
                    // todo: lost octolith
                    // HUNTER HAS TAKEN AN OCTOLITH
                    //PlayerEntity.Main.ShowDialog(DialogType.Hud, messageId: 117, param1: 90, param2: 1);
                    _shownOctolithDialog = true;
                }
            }
        }

        private static void EnterShip()
        {
            // update flags for the end of the escape sequence
            StorySave.TriggerState[2] &= 0x7F;
            if (StorySave.BossFlags.TestAny(BossFlags.Unit1B1Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit1B1Kill;
                StorySave.BossFlags |= BossFlags.Unit1B1Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit1B2Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit1B2Kill;
                StorySave.BossFlags |= BossFlags.Unit1B2Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit2B1Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit2B1Kill;
                StorySave.BossFlags |= BossFlags.Unit2B1Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit2B2Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit2B2Kill;
                StorySave.BossFlags |= BossFlags.Unit2B2Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit3B1Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit3B1Kill;
                StorySave.BossFlags |= BossFlags.Unit3B1Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit3B2Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit3B2Kill;
                StorySave.BossFlags |= BossFlags.Unit3B2Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit4B1Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit4B1Kill;
                StorySave.BossFlags |= BossFlags.Unit4B1Done;
            }
            if (StorySave.BossFlags.TestAny(BossFlags.Unit4B2Kill))
            {
                StorySave.BossFlags &= ~BossFlags.Unit4B2Kill;
                StorySave.BossFlags |= BossFlags.Unit4B2Done;
            }
        }

        public static void ResetEscapeState(bool updateSounds)
        {
            EscapeState = EscapeState.None;
            EscapeTimer = -1;
            EscapePaused = false;
            if (updateSounds)
            {
                UpdateEventSounds(timer: -1);
            }
        }

        private static void UpdateEscapeState(int frames, int stateId)
        {
            var state = (EscapeState)stateId;
            float time = frames / 30f;
            if (state == EscapeState.None)
            {
                EscapeTimer = -1;
                UpdateEventSounds(timer: -1);
            }
            else if (time == 0)
            {
                EscapePaused = !EscapePaused;
            }
            else if (EscapeState != state || EscapeTimer == -1)
            {
                EscapeTimer = time;
                EscapePaused = false;
                if (state == EscapeState.Escape)
                {
                    Sfx.QueueStream(VoiceId.VOICE_EVACUATE);
                    Sfx.QueueStream(VoiceId.VOICE_EVACUATE, delay: 3);
                    Sfx.QueueStream(VoiceId.VOICE_EVACUATE, delay: 6);
                    // mustodo: play music and update tempo
                    StorySave.TriggerState[2] |= 0x80;
                }
                else
                {
                    UpdateEventSounds(timer: -1);
                }
            }
            EscapeState = state;
        }

        private static bool _playedTimedEventSfx = false;

        private static void UpdateEventSounds(float timer)
        {
            // mustodo: update tempo
            if (timer > 165 / 30f)
            {
                _playedTimedEventSfx = false;
            }
            else if (timer >= 0 && !_playedTimedEventSfx)
            {
                PlayerEntity.Main.PlayTimedSfx(SfxId.PUZZLE_TIMER1_SCR);
                _playedTimedEventSfx = true;
            }
            else if (timer < 0)
            {
                PlayerEntity.Main.StopTimedSfx(SfxId.PUZZLE_TIMER1_SCR);
                _playedTimedEventSfx = false;
            }
        }

        private static void UpdateEscapeSounds(float timer)
        {
            // mustodo: update tempo and/or switch tracks
        }

        public static void UpdateState(Scene scene)
        {
            if (PlayerEntity.PlayerCount == 0)
            {
                return;
            }
            GameMode mode = scene.GameMode;
            IReadOnlyList<PlayerEntity> players = PlayerEntity.Players;
            int[] prevTeamPoints = new int[4];
            int[] prevTeamDeaths = new int[4];
            for (int i = 0; i < 4; i++)
            {
                prevTeamPoints[i] = TeamPoints[i];
                prevTeamDeaths[i] = TeamDeaths[i];
                TeamPoints[i] = 0;
                TeamDeaths[i] = 0;
                TeamKills[i] = 0;
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    TeamTime[i] = 0;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = players[i];
                if (!player.LoadFlags.TestFlag(LoadFlags.Initial))
                {
                    continue;
                }
                TeamPoints[player.TeamIndex] += Points[i];
                TeamDeaths[player.TeamIndex] += Deaths[i];
                TeamKills[player.TeamIndex] += Kills[i];
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    if (TeamTime[player.TeamIndex] < Time[i])
                    {
                        TeamTime[player.TeamIndex] = Time[i];
                    }
                }
                else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
                {
                    Time[i] = TeamTime[player.TeamIndex];
                }
            }
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams || mode == GameMode.Capture || mode == GameMode.Bounty
                || mode == GameMode.BountyTeams || mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                int teamPoints = TeamPoints[PlayerEntity.Main.TeamIndex];
                if (teamPoints != prevTeamPoints[PlayerEntity.Main.TeamIndex] && teamPoints == PointGoal - 1)
                {
                    Sfx.QueueStream(VoiceId.VOICE_ONE_KILL_TO_WIN, delay: 1);
                }
            }
            else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                int opponents = 0;
                int lastTeam = -1;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = players[i];
                    if (!player.LoadFlags.TestAny(LoadFlags.Active))
                    {
                        continue;
                    }
                    if (player.Health > 0 || TeamDeaths[player.TeamIndex] <= PointGoal)
                    {
                        if (player.TeamIndex != PlayerEntity.Main.TeamIndex)
                        {
                            opponents++;
                            lastTeam = player.TeamIndex;
                        }
                    }
                    if (TeamDeaths[player.TeamIndex] > PointGoal && player.RespawnTimer == PlayerEntity.RespawnTime)
                    {
                        Sfx.QueueStream(VoiceId.VOICE_ELIMINATED);
                    }
                }
                if (PlayerEntity.Main.LoadFlags.TestAny(LoadFlags.Active) && opponents == 1 && lastTeam != -1)
                {
                    int teamDeaths = TeamDeaths[lastTeam];
                    if (teamDeaths != prevTeamDeaths[lastTeam] && teamDeaths == PointGoal)
                    {
                        Sfx.QueueStream(VoiceId.VOICE_ONE_KILL_TO_WIN, delay: 1);
                    }
                }
            }
            ActivePlayers = 0;
            if (Teams)
            {
                int a = 0;
                for (int t = 0; t < 2; t++)
                {
                    for (int p = 0; p < 4; p++)
                    {
                        PlayerEntity player = players[p];
                        if (player.TeamIndex == t)
                        {
                            Standings[p] = 3;
                            if (player.LoadFlags.TestFlag(LoadFlags.Active))
                            {
                                ResultSlots[a++] = p;
                                ActivePlayers++;
                            }
                        }
                    }
                }
            }
            else
            {
                int a = 0;
                for (int p = 0; p < 4; p++)
                {
                    Standings[p] = 3;
                    if (players[p].LoadFlags.TestFlag(LoadFlags.Active))
                    {
                        ResultSlots[a++] = p;
                        ActivePlayers++;
                    }
                }
            }
            for (int index = 0; index < ActivePlayers; index++)
            {
                for (int nextIndex = index + 1; nextIndex < ActivePlayers; nextIndex++)
                {
                    int slot = ResultSlots[index];
                    int nextSlot = ResultSlots[nextIndex];
                    int teamIndex = players[slot].TeamIndex;
                    int nextTeamIndex = players[nextSlot].TeamIndex;
                    // the game passes team_ids[wslot/nslot] instead of the player fields to CompareTeams
                    if (Teams && teamIndex != nextTeamIndex && CompareTeams(teamIndex, nextTeamIndex, mode) < 0
                        || ComparePlayers(slot, nextSlot, mode) < 0)
                    {
                        ResultSlots[index] = nextSlot;
                        ResultSlots[nextIndex] = slot;
                    }
                }
            }
            if (Teams)
            {
                int v47 = 0;
                int v48 = CompareTeams(0, 1, mode);
                int[] v57 = new int[2];
                if (v48 <= 0)
                {
                    v57[0] = v48 != 0 ? 1 : 0;
                    v57[1] = 0;
                }
                else
                {
                    v57[0] = 0;
                    v57[1] = 1;
                }
                for (int i = 0; i < ActivePlayers - 1; i++)
                {
                    int slot = ResultSlots[i];
                    int nextSlot = ResultSlots[i + 1];
                    int teamIndex = players[slot].TeamIndex;
                    Standings[slot] = v57[teamIndex];
                    TeamStandings[slot] = v47;
                    if (teamIndex != players[nextSlot].TeamIndex)
                    {
                        if (ComparePlayers(slot, nextSlot, mode) != 0)
                        {
                            v47++;
                        }
                    }
                    else
                    {
                        v47 = 0;
                    }
                }
                int index = ActivePlayers - 1;
                Standings[index] = v57[players[ResultSlots[index]].TeamIndex];
                TeamStandings[index] = v47;
            }
            else
            {
                int index;
                int v47 = 0;
                for (index = 0; index < ActivePlayers - 1; index++)
                {
                    int slot = ResultSlots[index];
                    Standings[slot] = v47;
                    if (ComparePlayers(slot, ResultSlots[index + 1], mode) != 0)
                    {
                        v47 = index + 1;
                    }
                }
                Standings[ResultSlots[index]] = v47;
            }
            // todo: update license info
        }

        private static int ComparePlayers(int slot1, int slot2, GameMode mode)
        {
            int points1 = Points[slot1];
            int points2 = Points[slot2];
            float time1 = Time[slot1];
            float time2 = Time[slot2];
            if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                if (time1 == -1)
                {
                    time1 = Single.MaxValue;
                }
                if (time2 == -1)
                {
                    time2 = Single.MaxValue;
                }
            }
            int deaths1 = Deaths[slot1];
            int deaths2 = Deaths[slot2];
            int kills1 = Kills[slot1];
            int kills2 = Kills[slot2];
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                if (time1 == time2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Capture || mode == GameMode.Nodes || mode == GameMode.NodesTeams
                || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                if (points1 == points2 && kills1 == kills2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.PrimeHunter)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }

        private static int CompareTeams(int slot1, int slot2, GameMode mode)
        {
            int points1 = TeamPoints[slot1];
            int points2 = TeamPoints[slot2];
            float time1 = TeamTime[slot1];
            float time2 = TeamTime[slot2];
            if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                if (time1 == -1)
                {
                    time1 = Single.MaxValue;
                }
                if (time2 == -1)
                {
                    time2 = Single.MaxValue;
                }
            }
            int deaths1 = TeamDeaths[slot1];
            int deaths2 = TeamDeaths[slot2];
            int kills1 = TeamKills[slot1];
            int kills2 = TeamKills[slot2];
            if (mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.SurvivalTeams)
            {
                if (time1 == time2 && deaths1 == deaths2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && deaths1 > deaths2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.DefenderTeams)
            {
                if (time1 == time2 && kills1 == kills2)
                {
                    return 0;
                }
                if (time1 < time2 || time1 == time2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            if (mode == GameMode.Capture || mode == GameMode.NodesTeams || mode == GameMode.BattleTeams)
            {
                if (points1 == points2 && kills1 == kills2)
                {
                    return 0;
                }
                if (points1 < points2 || points1 == points2 && kills1 < kills2)
                {
                    return -1;
                }
                return 1;
            }
            return 0;
        }

        private static StorySave _cleanStorySave = null!;
        public static StorySave StorySave { get; private set; } = null!;

        public static void UpdateCleanSave(bool force)
        {
            if (!force && EscapeTimer != -1 && EscapeState == EscapeState.Escape)
            {
                return;
            }
            StorySave.CopyTo(_cleanStorySave);
        }

        public static void RestoreCleanSave()
        {
            // todo: save and restore more fields
            ushort prevFoundOctos = StorySave.FoundOctoliths;
            ushort prevCurOctos = StorySave.CurrentOctoliths;
            uint prevLostOctos = StorySave.LostOctoliths;
            byte[] prevAreaHunters = StorySave.AreaHunters.ToArray();
            _cleanStorySave.CopyTo(StorySave);
            int curCurCount = 0;
            int prevCurCount = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((StorySave.CurrentOctoliths & (1 << i)) != 0)
                {
                    curCurCount++;
                }
                if ((prevCurOctos & (1 << i)) != 0)
                {
                    prevCurCount++;
                }
            }
            if (curCurCount > prevCurCount)
            {
                // octolith has been lost -- need to restore values from the dirty save
                StorySave.FoundOctoliths = prevFoundOctos;
                StorySave.CurrentOctoliths = prevCurOctos;
                StorySave.LostOctoliths = prevLostOctos;
            }
            prevAreaHunters.CopyTo(StorySave.AreaHunters, index: 0);
        }

        private const string _saveFolder = "Savedata";
        private static readonly JsonSerializerOptions _jsonOpt = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true,
            Converters = { new ByteArrayConverter() }
        };

        private static string GetSavePath(byte slot)
        {
            return Paths.Combine(_saveFolder, $"save{slot:000}.json");
        }

        private static string GetSettingsPath()
        {
            return Paths.Combine(_saveFolder, $"settings.json");
        }

        public static void LoadSave()
        {
            StorySave = new StorySave();
            if (Menu.SaveSlot != 0)
            {
                string path = GetSavePath(Menu.SaveSlot);
                if (File.Exists(path))
                {
                    StorySave? save = JsonSerializer.Deserialize<StorySave>(File.ReadAllText(path), _jsonOpt);
                    if (save != null)
                    {
                        StorySave = save;
                    }
                }
            }
        }

        public static void CommitSave()
        {
            if (Menu.SaveSlot == 0)
            {
                return;
            }
            if (!Directory.Exists(_saveFolder))
            {
                Directory.CreateDirectory(_saveFolder);
            }
            File.WriteAllText(GetSavePath(Menu.SaveSlot), JsonSerializer.Serialize(StorySave, _jsonOpt));
        }

        internal sealed class ByteArrayConverter : JsonConverter<byte[]>
        {
            public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                short[]? sByteArray = JsonSerializer.Deserialize<short[]>(ref reader, options);
                if (sByteArray == null)
                {
                    return null;
                }
                byte[] value = new byte[sByteArray.Length];
                for (int i = 0; i < sByteArray.Length; i++)
                {
                    value[i] = (byte)sByteArray[i];
                }
                return value;
            }

            public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (byte val in value)
                {
                    writer.WriteNumberValue(val);
                }
                writer.WriteEndArray();
            }
        }

        private class SerializedSettings
        {
            public IReadOnlyDictionary<string, string>? Bugfixes { get; set; }
            public IReadOnlyDictionary<string, string>? Features { get; set; }
            public IReadOnlyDictionary<string, string>? Cheats { get; set; }
            public MenuSettings? MenuSettings { get; set; }
        }

        public static MenuSettings LoadSettings()
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                SerializedSettings? settings = JsonSerializer.Deserialize<SerializedSettings>(File.ReadAllText(path), _jsonOpt);
                if (settings != null)
                {
                    if (settings.Bugfixes != null)
                    {
                        Bugfixes.Load(settings.Bugfixes);
                    }
                    if (settings.Features != null)
                    {
                        Features.Load(settings.Features);
                    }
                    if (settings.Cheats != null)
                    {
                        Cheats.Load(settings.Cheats);
                    }
                    if (settings.MenuSettings != null)
                    {
                        return settings.MenuSettings;
                    }
                }
            }
            return new MenuSettings();
        }

        public static void CommitSettings(MenuSettings menuSettings)
        {
            // sktodo: commit menu options, including save slot
            if (!Directory.Exists(_saveFolder))
            {
                Directory.CreateDirectory(_saveFolder);
            }
            var settings = new SerializedSettings
            {
                Bugfixes = Bugfixes.Commit(),
                Features = Features.Commit(),
                Cheats = Cheats.Commit(),
                MenuSettings = menuSettings
            };
            File.WriteAllText(GetSettingsPath(), JsonSerializer.Serialize(settings, _jsonOpt));
        }

        public static void Reset()
        {
            _cleanStorySave = new StorySave();
            LoadSave();
            CommitSave();
            UpdateCleanSave(force: true);
            MatchState = MatchState.InProgress;
            TransitionState = TransitionState.None;
            TransitionRoomId = -1;
            TransitionAltForm = false;
            ActivePlayers = 0;
            for (int i = 0; i < 4; i++)
            {
                Standings[i] = 0;
                TeamStandings[i] = 0;
                ResultSlots[i] = 0;
                Points[i] = 0;
                TeamPoints[i] = 0;
                Kills[i] = 0;
                TeamKills[i] = 0;
                Deaths[i] = 0;
                TeamDeaths[i] = 0;
                Time[i] = 0;
                TeamTime[i] = 0;
                BeamDamageMax[i] = 0;
                BeamDamageDealt[i] = 0;
                DamageCount[i] = 0;
                AltDamageCount[i] = 0;
                Kills[i] = 0;
                Suicides[i] = 0;
                FriendlyKills[i] = 0;
                HeadshotKills[i] = 0;
                OctolithScores[i] = 0;
                OctolithDrops[i] = 0;
                OctolithStops[i] = 0;
                NodesCaptured[i] = 0;
                NodesLost[i] = 0;
                KillsAsPrime[i] = 0;
                PrimesKilled[i] = 0;
                for (int j = 0; j < 9; j++)
                {
                    BeamKills[i, j] = 0;
                }
            }
            PrimeHunter = -1;
            Teams = false;
            FriendlyFire = false;
            PointGoal = 0;
            TimeGoal = 0;
            DamageLevel = 1;
            OctolithReset = false;
            RadarPlayers = false;
            AffinityWeapons = false;
            MatchTime = -1;
            PlayerEntity.Reset();
            CameraSequence.Current = null;
            MenuPause = false;
            DialogPause = false;
            _pausingDialog = false;
            _unpausingDialog = false;
            _pausingMenu = false;
            _unpausingMenu = false;
            EscapeState = EscapeState.None;
            EscapeTimer = -1;
            EscapePaused = false;
        }
    }

    public class StorySave
    {
        public byte[][] RoomState { get; init; }
        public byte[] VisitedRooms { get; init; } = new byte[9];
        public byte[] TriggerState { get; init; } = new byte[4];
        public byte[] Logbook { get; init; } = new byte[68];
        public int ScanCount { get; set; }
        public int EquipmentCount { get; set; }
        public int CheckpointEntityId { get; set; } = -1;
        public int CheckpointRoomId { get; set; } = -1;
        public int Health { get; set; }
        public int HealthMax { get; set; }
        public int[] Ammo { get; init; } = new int[2];
        public int[] AmmoMax { get; init; } = new int[2];
        public int[] WeaponSlots { get; init; } = new int[3];
        public ushort Weapons { get; set; }
        public uint Artifacts { get; set; }
        public ushort FoundOctoliths { get; set; }
        public ushort CurrentOctoliths { get; set; }
        public uint LostOctoliths { get; set; } = UInt32.MaxValue;
        public ushort Areas { get; set; } = 0xC; // Celestial Archives 1 & 2
        public BossFlags BossFlags { get; set; } = BossFlags.None;
        public byte[] AreaHunters { get; init; } = new byte[4];
        public byte DefeatedHunters { get; set; }

        public StorySave()
        {
            RoomState = new byte[60][];
            for (int i = 0; i < RoomState.Length; i++)
            {
                RoomState[i] = new byte[66];
            }
            PlayerValues values = Metadata.PlayerValues[0];
            Health = HealthMax = values.EnergyTank - 1;
            Ammo[0] = AmmoMax[0] = 400;
            Ammo[1] = 0;
            AmmoMax[1] = 50;
            Weapons = (ushort)(WeaponUnlockBits.PowerBeam | WeaponUnlockBits.Missile);
            if (Cheats.StartWithAllUpgrades)
            {
                Health = HealthMax = 799;
                Ammo[0] = AmmoMax[0] = 4000;
                Ammo[1] = 950;
                AmmoMax[1] = 950;
                Weapons = 0xFF;
            }
            WeaponSlots[0] = (int)BeamType.PowerBeam;
            WeaponSlots[1] = (int)BeamType.Missile;
            WeaponSlots[2] = (int)BeamType.None;
            // todo: initialize more fields
            UpdateLogbook(1); // SCAN VISOR
            UpdateLogbook(2); // THERMAL POSITIONER
            UpdateLogbook(3); // ARM CANNON
            UpdateLogbook(4); // POWER BEAM
            UpdateLogbook(5); // MISSILE LAUNCHER
            UpdateLogbook(6); // MORPH BALL
            UpdateLogbook(7); // MORPH BALL BOMB
            UpdateLogbook(27); // JUMP BOOTS
            UpdateLogbook(29); // CHARGE SHOT
            if (Cheats.StartWithAllOctoliths)
            {
                FoundOctoliths = CurrentOctoliths = 0xFF;
            }
        }

        public int InitRoomState(int roomId, int entityId, bool active,
            int activeState = 3, int inactiveState = 1)
        {
            if (entityId == -1 || roomId < 27)
            {
                return 0;
            }
            if (roomId > 92) // skdebug
            {
                return 2;
            }
            roomId -= 27;
            activeState &= 3;
            inactiveState &= 3;
            (int byteIndex, int pairIndex) = Math.DivRem(entityId, 4);
            pairIndex *= 2;
            int pairMask = 3 << pairIndex;
            if ((RoomState[roomId][byteIndex] & pairMask) == 0)
            {
                RoomState[roomId][byteIndex] &= (byte)~pairMask;
                RoomState[roomId][byteIndex] |= (byte)((active ? activeState : inactiveState) << pairIndex);
            }
            return GetRoomState(roomId + 27, entityId);
        }

        public int GetRoomState(int roomId, int entityId)
        {
            if (entityId == -1 || roomId < 27 || roomId > 92)
            {
                return 0;
            }
            roomId -= 27;
            (int byteIndex, int pairIndex) = Math.DivRem(entityId, 4);
            pairIndex *= 2;
            return ((RoomState[roomId][byteIndex] >> pairIndex) & 3) - 1;
        }

        public void SetRoomState(int roomId, int entityId, int state)
        {
            if (entityId == -1 || roomId < 27 || roomId > 92)
            {
                return;
            }
            roomId -= 27;
            state &= 3;
            (int byteIndex, int pairIndex) = Math.DivRem(entityId, 4);
            pairIndex *= 2;
            int pairMask = 3 << pairIndex;
            RoomState[roomId][byteIndex] &= (byte)~pairMask;
            RoomState[roomId][byteIndex] |= (byte)(state << pairIndex);
            return;
        }

        public bool CheckVisitedRoom(int roomId)
        {
            if (roomId < 27 || roomId > 92)
            {
                return false;
            }
            roomId -= 27;
            (int byteIndex, int bitIndex) = Math.DivRem(roomId, 8);
            return (VisitedRooms[byteIndex] & (1 << bitIndex)) != 0;
        }

        public void SetVisitedRoom(int roomId)
        {
            if (roomId < 27 || roomId > 92)
            {
                return;
            }
            roomId -= 27;
            (int byteIndex, int bitIndex) = Math.DivRem(roomId, 8);
            VisitedRooms[byteIndex] |= (byte)(1 << bitIndex);
        }

        public bool CheckFoundOctolith(int areaId)
        {
            return (FoundOctoliths & (1 << areaId)) != 0;
        }

        public int CountFoundOctoliths()
        {
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if (CheckFoundOctolith(i))
                {
                    count++;
                }
            }
            return count;
        }

        public void UpdateFoundOctolith(int areaId)
        {
            FoundOctoliths |= (ushort)(1 << areaId);
            CurrentOctoliths |= (ushort)(1 << areaId);
        }

        public bool CheckFoundArtifact(int artifactId, int modelId)
        {
            return (Artifacts & (1 << (artifactId + 3 * modelId))) != 0;
        }

        public int CountFoundArtifacts(int modelId)
        {
            int count = 0;
            for (int i = 0; i < 3; i++)
            {
                if (CheckFoundArtifact(i, modelId))
                {
                    count++;
                }
            }
            return count;
        }

        public void UpdateFoundArtifact(int artifactId, int modelId)
        {
            Artifacts |= (uint)(1 << (artifactId + 3 * modelId));
        }

        public void UpdateLogbook(int scanId)
        {
            Debug.Assert(scanId >= 0 && scanId < 68 * 8);
            int index = scanId / 8;
            byte bit = (byte)(1 << (scanId % 8));
            if ((Logbook[index] & bit) == 0)
            {
                Logbook[index] |= bit;
                int category = Strings.GetScanEntryCategory(scanId);
                if (category < 3)
                {
                    ScanCount++;
                }
                else if (category == 3)
                {
                    EquipmentCount++;
                }
            }
        }

        public bool CheckLogbook(int scanId)
        {
            Debug.Assert(scanId >= 0 && scanId < 68 * 8);
            int index = scanId / 8;
            byte bit = (byte)(1 << (scanId % 8));
            return (Logbook[index] & bit) != 0;
        }

        public void CopyTo(StorySave other)
        {
            for (int i = 0; i < RoomState.Length; i++)
            {
                byte[] source = RoomState[i];
                Array.Copy(source, other.RoomState[i], source.Length);
            }
            VisitedRooms.CopyTo(other.VisitedRooms, index: 0);
            TriggerState.CopyTo(other.TriggerState, index: 0);
            Logbook.CopyTo(other.Logbook, index: 0);
            other.ScanCount = ScanCount;
            other.EquipmentCount = EquipmentCount;
            other.CheckpointEntityId = CheckpointEntityId;
            other.CheckpointRoomId = CheckpointRoomId;
            other.Health = Health;
            other.HealthMax = HealthMax;
            Ammo.CopyTo(other.Ammo, index: 0);
            AmmoMax.CopyTo(other.AmmoMax, index: 0);
            WeaponSlots.CopyTo(other.WeaponSlots, index: 0);
            other.Weapons = Weapons;
            other.Artifacts = Artifacts;
            other.Artifacts = Artifacts;
            other.FoundOctoliths = FoundOctoliths;
            other.CurrentOctoliths = CurrentOctoliths;
            other.LostOctoliths = LostOctoliths;
            other.Areas = Areas;
            other.BossFlags = BossFlags;
            AreaHunters.CopyTo(other.AreaHunters, index: 0);
            other.DefeatedHunters = DefeatedHunters;
        }
    }
}
