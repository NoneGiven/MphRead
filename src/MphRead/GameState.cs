using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public static class GameState
    {
        public static bool MenuPause { get; private set; }
        public static bool DialogPause { get; private set; }
        public static int EscapeState { get; set; }
        public static MatchState MatchState { get; set; } = MatchState.InProgress;
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
                // todo: escape sequence stuff
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
                    scene.SetFade(FadeType.FadeOutBlack, 20 / 30f, overwrite: true, exitAfterFade: true);
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

        private static bool _oublietteUnlocked = false; // skdebug
        private static bool _hasAllOctoliths = false; // skdebug

        public static void ModeStateAdventure(Scene scene)
        {
            // todo: update save
            if (!_oublietteUnlocked)
            {
                for (int i = 0; i < scene.MessageQueue.Count; i++)
                {
                    MessageInfo message = scene.MessageQueue[i];
                    if (message.Message == Message.UnlockOubliette && message.ExecuteFrame == scene.FrameCount)
                    {
                        if (_hasAllOctoliths)
                        {
                            // todo: play movie and defer dialog
                            _oublietteUnlocked = true;
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
                                    _oublietteUnlocked = true; // skdebug
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }
            }
            for (int i = 0; i < scene.MessageQueue.Count; i++)
            {
                MessageInfo message = scene.MessageQueue[i];
                if (message.Message == Message.Checkpoint && message.ExecuteFrame == scene.FrameCount)
                {
                    // todo: update checkpoint
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
            if (prompt != PromptType.Any && confirm != ConfirmState.Okay)
            {
                Sfx.Instance.StopFreeSfxScripts();
                if (confirm == ConfirmState.Yes)
                {
                    if (prompt == PromptType.ShipHatch)
                    {
                        // yes to ship hatch (enter)
                        // todo: update story save
                        scene.SetFade(FadeType.FadeOutWhite, length: 20 / 30f, overwrite: true, exitAfterFade: true);
                        // mustodo: stop music
                        // todo: fade SFX
                        Sfx.Instance.PlaySample((int)SfxId.RETURN_TO_SHIP_YES, source: null, loop: false,
                            noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                    }
                    else if (prompt == PromptType.GameOver)
                    {
                        // yes to game over (continue)
                        PlayerEntity.Main.RestartLongSfx(); // skdebug
                        scene.SetFade(FadeType.FadeOutInWhite, length: 10 / 30f, overwrite: true); // skdebug
                        PlayerEntity.Main.RespawnTimer = 10 * 2; // skdebug
                        // todo: fade out and reload room
                        Sfx.Instance.PlaySample((int)SfxId.MENU_CONFIRM, source: null, loop: false,
                            noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                        UnpauseDialog();
                    }
                }
                else if (prompt == PromptType.GameOver)
                {
                    // no to game over (quit)
                    scene.SetFade(FadeType.FadeOutBlack, length: 10 / 30f, overwrite: true, exitAfterFade: true);
                    // mustodo: stop music
                    Sfx.Instance.PlaySample((int)SfxId.QUIT_GAME, source: null, loop: false,
                        noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
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
                            PlayerEntity.Main.DialogPromptType = PromptType.ShipHatch;
                            // todo: set checkpoint and update story save
                            // HUNTER GUNSHIP enter your ship?
                            PlayerEntity.Main.ShowDialog(DialogType.YesNo, messageId: 1);
                            Sfx.Instance.StopFreeSfxScripts();
                            Sfx.Instance.PlayScript((int)SfxId.RETURN_TO_SHIP_SCR, source: null,
                                noUpdate: false, recency: -1, sourceOnly: false, cancellable: false);
                        }
                    }
                    // diagtodo: escape start, escape cancel
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
                    if (EscapeState == 2)
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

        public static StorySave StorySave { get; private set; } = null!;

        public static void Reset()
        {
            // todo: persist story saves
            StorySave = new StorySave();
            MatchState = MatchState.InProgress;
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
            EscapeState = 0;
            _oublietteUnlocked = false;
        }
    }

    public class StorySave
    {
        public byte[] Logbook { get; } = new byte[68];
        public int ScanCount { get; set; }
        public int EquipmentCount { get; set; }

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
    }
}
