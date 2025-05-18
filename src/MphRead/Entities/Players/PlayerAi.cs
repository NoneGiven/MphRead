using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public PlayerAiData AiData { get; init; }
        public NodeData3? ClosestNode { get; set; } = null;
        public int BotLevel { get; set; } = 0;

        public class PlayerAiData
        {
            private readonly PlayerEntity _player;
            private readonly Scene _scene;
            private NodeData _nodeData = null!;

            public PlayerAiData(PlayerEntity player)
            {
                _player = player;
                _scene = player._scene;
            }

            public AiPersonalityData1 Personality { get; set; } = null!;
            public bool Flags1 { get; set; }
            public AiFlags2 Flags2 { get; set; }
            public AiFlags3 Flags3 { get; set; } // frame flags -- cleared every frame and need to be reset by funcs
            public AiFlags4 Flags4 { get; set; }
            public ushort HealthThreshold { get; set; }
            public uint DamageFromHalfturret { get; set; }
            // todo: member name -- set by teleporter
            public int Field118 { get; set; } // sktodo-ai: FPS stuff? not sure if this is a timer/counter

            private readonly int[] _slotHits = new int[4];
            private readonly int[] _slotDamage = new int[4];

            // todo: member names
            private NodeData3? _node3C = null;
            private NodeData3? _node40 = null;
            private NodeData3? _node44 = null;
            private NodeData3? _node48 = null;
            private readonly NodeData3?[] _field4C = new NodeData3?[11];
            private ushort _field78 = 0;
            private readonly ushort[] _field7A = new ushort[10]; // sktodo-ai: confirm this should be 10 and not 11 like field4C

            // todo: member names
            private PlayerEntity? _targetPlayer = null;
            private HalfturretEntity? _halfturret1C = null;
            private ItemSpawnEntity? _itemSpawnC4 = null;
            private ItemInstanceEntity? _itemC8 = null;
            private OctolithFlagEntity? _octolithFlagCC = null;
            private FlagBaseEntity? _flagBaseD0 = null;
            private OctolithFlagEntity? _octolithFlagD4 = null;
            private FlagBaseEntity? _flagBaseD8 = null;
            private OctolithFlagEntity? _octolithFlagDC = null;
            private FlagBaseEntity? _flagBaseE0 = null;
            private NodeDefenseEntity? _defenseE4 = null;
            private DoorEntity? _doorE8 = null;

            private int _nodeDataSetIndex = 0;
            private byte _nodeDataSelOff = 0;
            private byte _nodeDataSelOn = 0;
            private IReadOnlyList<NodeData3> _nodeList = null!;
            private readonly int[] _nodeTypeIndex = new int[6];

            // todo: member names
            private uint _field102C = 0; // random value bounds based on bot level
            private uint _field102E = 0; // timer set from field102C -- bomb delay
            private uint _field1030 = 0;
            private int _field116 = 0; // timer?
            private int _field1020 = 0; // timer?
            private int _field1032 = 0; // timer/counter
            private Vector3 _fieldAC = Vector3.Zero;
            private Vector3 _fieldB8 = Vector3.Zero;
            private Vector3 _field1038 = Vector3.Zero;
            private Vector3 _field1048 = Vector3.Zero;
            private Vector3 _field1054 = Vector3.Zero;
            private int _field30 = 0; // don't think this is a timer, matched to ND3 field4
            private Vector3 _field90 = Vector3.Zero;
            private float _field9C = 0;

            private int _weapon1 = 0;
            private int _shotDelay = 0;

            public void Reset()
            {
                _nodeData = null!;
                Flags1 = false;
                Flags2 = AiFlags2.None;
                Flags3 = AiFlags3.None;
                HealthThreshold = 0;
                DamageFromHalfturret = 0;
                Field118 = 0;
                Array.Fill(_slotHits, 0);
                Array.Fill(_slotDamage, 0);
                _node3C = null;
                _node40 = null;
                _node44 = null;
                _node48 = null;
                Array.Fill(_field4C, null);
                _field78 = 0;
                Array.Fill(_field7A, (ushort)0);
                _targetPlayer = null;
                _halfturret1C = null;
                _itemSpawnC4 = null;
                _itemC8 = null;
                _octolithFlagCC = _octolithFlagD4 = _octolithFlagDC = null;
                _flagBaseD0 = _flagBaseD8 = _flagBaseE0 = null;
                _defenseE4 = null;
                _doorE8 = null;
                _nodeDataSetIndex = 0;
                _nodeList = null!;
                Array.Fill(_nodeTypeIndex, 0);
                _field102C = 0;
                _field102E = 0;
                _field1030 = 0;
                _field116 = 0;
                _field1020 = 0;
                _field1032 = 0;
                _fieldAC = Vector3.Zero;
                _fieldB8 = Vector3.Zero;
                _field1038 = Vector3.Zero;
                _field1048 = Vector3.Zero;
                _field1054 = Vector3.Zero;
                _field30 = 0;
                Field118 = 0;
                _field90 = Vector3.Zero;
                _field9C = 0;
                _queuedFindEntityAction = AiQueuedEnt.None;
                _weapon1 = 0;
                _shotDelay = 0;
                for (int i = 0; i < _executionTree.Length; i++)
                {
                    if (_executionTree[i] == null)
                    {
                        _executionTree[i] = new AiContext();
                    }
                    else
                    {
                        _executionTree[i].Clear();
                    }
                }
                _playerAggroCount = 0;
                for (int i = 0; i < _playerAggro.Length; i++)
                {
                    _playerAggro[i].Clear();
                }
            }

            public void InitializeAtLoad()
            {
                InitializeMain();
                ClearInput();
                InitializeSub();
                UpdateExecutionPath(Personality, depth: 0);
            }

            public void InitializeAtSpawn()
            {
                InitializeMain();
                ClearInput();
                UpdateExecutionPath(Personality, depth: 0);
            }

            // todo: member names -- and is the first set even used?
            private static int _globalField0 = 0;
            private static int _globalField2 = 0;
            private static readonly AiGlobals[] _globalObjs =
            [
                new AiGlobals(), new AiGlobals(), new AiGlobals(), new AiGlobals()
            ];
            private static readonly bool[,] _playerVisibility = new bool[4, 4];
            private static byte _visIndex1 = 0;
            private static byte _visIndex2 = 0;

            public class AiGlobals
            {
                public PlayerEntity Player { get; set; } = null!;
                public int Field4 { get; set; }
                public int NodeDataIndex { get; set; }
                public IReadOnlyList<NodeData3> NodeData { get; set; } = null!;
            }

            public static void InitializeGlobals()
            {
                _globalField0 = 0;
                _globalField2 = 0;
                for (int i = 0; i < _globalObjs.Length; i++)
                {
                    AiGlobals globals = _globalObjs[i];
                    globals.Player = null!;
                    globals.Field4 = 0;
                    globals.NodeDataIndex = 0;
                    globals.NodeData = null!;
                }
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        _playerVisibility[i, j] = false;
                    }
                }
                _visIndex1 = 1;
                _visIndex2 = 0;
            }

            public static void UpdateVisibilityAndGlobals(Scene scene)
            {
                UpdateVisibility(scene);
                UpdateGlobals(scene);
            }

            private static void UpdateVisibility(Scene scene)
            {
                // sktodo-ai: presumably these are done one player per frame for efficiency,
                // but we don't really need to worry about that, and we're not being 100% accurate
                // to the game by doing this once per 60 fps frame anyway, so yeah.
                // the game's unused counter that disables these updates for a certain number of frames
                // was probably also added to make them even less frequent, but was ultimately not needed.
                _playerVisibility[_visIndex1, _visIndex2] = false;
                _playerVisibility[_visIndex2, _visIndex1] = false;
                PlayerEntity player1 = Players[_visIndex1];
                PlayerEntity player2 = Players[_visIndex2];
                if (player1.Health != 0 && player1.LoadFlags.TestFlag(LoadFlags.Active)
                    && player2.Health != 0 && player2.LoadFlags.TestFlag(LoadFlags.Active)
                    && (player1.IsBot || player2.IsBot))
                {
                    Vector3 pos1 = player1.CameraInfo.Position;
                    Vector3 pos2 = player2.CameraInfo.Position;
                    if (pos1 == pos2)
                    {
                        pos1 = player1.Position;
                        pos2 = player2.Position;
                    }
                    CollisionResult discard = default;
                    if (!CollisionDetection.CheckBetweenPoints(pos1, pos2, TestFlags.None, scene, ref discard))
                    {
                        _playerVisibility[_visIndex1, _visIndex2] = true;
                        _playerVisibility[_visIndex2, _visIndex1] = true;
                    }
                }
                if (++_visIndex1 >= 4)
                {
                    if (++_visIndex2 >= 3)
                    {
                        _visIndex2 = 0;
                    }
                    _visIndex1 = (byte)(_visIndex2 + 1);
                }
            }

            private static void UpdateGlobals(Scene scene)
            {
                if (_globalField2 == 0)
                {
                    return;
                }
                if (_globalField0 >= _globalField2)
                {
                    _globalField0 = 0;
                }
                AiGlobals global = _globalObjs[_globalField0];
                PlayerEntity player = global.Player;
                NodeData3 node = global.NodeData[global.NodeDataIndex];
                Vector3 pos1 = player.Position.AddY(player.IsAltForm ? 0.5f : 1);
                Vector3 pos2 = node.Position.AddY(0.5f);
                CollisionResult discard = default;
                if (CollisionDetection.CheckBetweenPoints(pos1, pos2, TestFlags.None, scene, ref discard))
                {
                    global.NodeDataIndex++;
                    global.Field4--;
                    if (global.Field4 == 0)
                    {
                        player.AiData.Flags2 &= ~AiFlags2.Bit10;
                        RemovePlayerFromGlobals(player);
                    }
                }
                else
                {
                    player.AiData._node40 = node;
                    player.AiData._entityRefs.Field1 = node;
                    player.AiData.Flags2 &= ~AiFlags2.Bit10;
                    RemovePlayerFromGlobals(player);
                }
                _globalField0++;
            }

            private void InitializeMain()
            {
                if (_scene.Room?.NodeData == null)
                {
                    return;
                }
                _nodeData = _scene.Room.NodeData;
                SetClosestNodeList(_player.Position);
                if (GameState.Mode == GameMode.Capture)
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type == EntityType.OctolithFlag)
                        {
                            var octoFlag = (OctolithFlagEntity)entity;
                            if (octoFlag.Data.TeamId == _player.TeamIndex)
                            {
                                _octolithFlagCC = _octolithFlagD4 = octoFlag;
                            }
                            else
                            {
                                _octolithFlagDC = octoFlag;
                            }
                        }
                        else if (entity.Type == EntityType.FlagBase)
                        {
                            var flagBase = (FlagBaseEntity)entity;
                            if (flagBase.Data.TeamId == _player.TeamIndex)
                            {
                                _flagBaseD0 = _flagBaseD8 = flagBase;
                            }
                            else
                            {
                                _flagBaseE0 = flagBase;
                            }
                        }
                    }
                }
                else if (GameState.Mode == GameMode.Bounty || GameState.Mode == GameMode.BountyTeams)
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type == EntityType.OctolithFlag && _octolithFlagCC == null)
                        {
                            _octolithFlagCC = _octolithFlagD4 = _octolithFlagDC = (OctolithFlagEntity)entity;
                        }
                        else if (entity.Type == EntityType.FlagBase && _flagBaseD0 == null)
                        {
                            _flagBaseD0 = _flagBaseD8 = _flagBaseE0 = (FlagBaseEntity)entity;
                        }
                    }
                }
            }

            private class AiButton
            {
                public bool IsDown { get; set; }
                public int FramesDown { get; set; }
                public int FramesUp { get; set; }

                public void Clear()
                {
                    IsDown = false;
                    FramesDown = 0;
                    FramesUp = 0;
                }
            }

            private class AiButtons
            {
                public AiButton Up { get; } = new AiButton();
                public AiButton Down { get; } = new AiButton();
                public AiButton Left { get; } = new AiButton();
                public AiButton Right { get; } = new AiButton();
                public AiButton A { get; } = new AiButton();
                public AiButton B { get; } = new AiButton();
                public AiButton X { get; } = new AiButton();
                public AiButton Y { get; } = new AiButton();
                public AiButton L { get; } = new AiButton();
                public AiButton R { get; } = new AiButton();
                public AiButton Start { get; } = new AiButton();
                public AiButton Select { get; } = new AiButton();

                public IReadOnlyList<AiButton> AllButtons { get; }

                public AiButtons()
                {
                    AllButtons =
                    [
                        Up, Down, Left, Right, A, B, X, Y, L, R, Start, Select
                    ];
                }
            }

            private class AiTouchButtons
            {
                public AiButton Morph { get; } = new AiButton();
                public AiButton Unmorph { get; } = new AiButton();
                public AiButton PowerBeam { get; } = new AiButton();
                public AiButton Missile { get; } = new AiButton();
                public AiButton VoltDriver { get; } = new AiButton();
                public AiButton Battlehammer { get; } = new AiButton();
                public AiButton Imperialist { get; } = new AiButton();
                public AiButton Judicator { get; } = new AiButton();
                public AiButton Magmaul { get; } = new AiButton();
                public AiButton ShockCoil { get; } = new AiButton();
                public AiButton OmegaCannon { get; } = new AiButton();

                public IReadOnlyList<AiButton> AllButtons { get; }

                public AiTouchButtons()
                {
                    AllButtons =
                    [
                        Morph, Unmorph, PowerBeam, Missile, VoltDriver, Battlehammer,
                        Imperialist, Judicator, Magmaul, ShockCoil, OmegaCannon
                    ];
                }
            }

            private readonly AiButtons _buttons = new AiButtons();
            private readonly AiTouchButtons _touchButtons = new AiTouchButtons();
            private ushort _touchAimX = 0;
            private ushort _touchAimY = 0;
            private bool _hasTouch = false;
            private ushort _framesWithTouch = 0;
            private ushort _framesWithoutTouch = 0;
            private float _buttonAimX = 0;
            private float _buttonAimY = 0;

            private void ClearInput()
            {
                _buttons.Up.Clear();
                _buttons.Down.Clear();
                _buttons.Left.Clear();
                _buttons.Right.Clear();
                _buttons.A.Clear();
                _buttons.B.Clear();
                _buttons.X.Clear();
                _buttons.Y.Clear();
                _buttons.L.Clear();
                _buttons.R.Clear();
                _buttons.Start.Clear();
                _buttons.Select.Clear();
                _touchAimX = 0;
                _touchAimY = 0;
                _hasTouch = false;
                _framesWithTouch = 0;
                _framesWithoutTouch = 0;
                _buttonAimX = 0;
                _buttonAimY = 0;
                _touchButtons.Morph.Clear();
                _touchButtons.Unmorph.Clear();
                _touchButtons.PowerBeam.Clear();
                _touchButtons.Missile.Clear();
                _touchButtons.VoltDriver.Clear();
                _touchButtons.Battlehammer.Clear();
                _touchButtons.Imperialist.Clear();
                _touchButtons.Judicator.Clear();
                _touchButtons.Magmaul.Clear();
                _touchButtons.ShockCoil.Clear();
                _touchButtons.OmegaCannon.Clear();
                _nodeDataSelOff = 0;
                _nodeDataSelOn = 0;
            }

            // duplicated the last value for Guardian
            private static readonly IReadOnlyList<IReadOnlyList<uint>> _botLevelRandomValues1
                = [
                    [ 45, 45, 90, 45, 60, 45, 45, 45 ],
                    [ 15, 15, 10, 10, 10, 10, 10, 10 ],
                    [ 7, 7, 2, 2, 2, 2, 2, 2 ]
                ];

            private static readonly IReadOnlyList<uint> _botLevelRandomValues2 = [150, 45, 10];

            private void InitializeSub()
            {
                if (GameState.SinglePlayer && GameState.EncounterState[_player.SlotIndex] != 0)
                {
                    _field102C = 0;
                    _field1030 = 0;
                }
                else
                {
                    // note: the game uses index 1, not index 2, for out-of-range bot levels
                    int index = Math.Clamp(_player.BotLevel, 0, 2);
                    _field102C = _botLevelRandomValues1[index][(int)_player.Hunter] * 2; // todo: FPS stuff
                    _field1030 = _botLevelRandomValues2[index] * 2; // todo: FPS stuff
                }
            }

            public void ProcessInput()
            {
                Flags3 |= AiFlags3.NoInput;
                for (int i = 0; i < _buttons.AllButtons.Count; i++)
                {
                    AiButton button = _buttons.AllButtons[i];
                    Keybind control;
                    if (button == _buttons.Up)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.MoveUp
                            : _player.Controls.AimUp;
                    }
                    else if (button == _buttons.Down)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.MoveDown
                            : _player.Controls.AimDown;
                    }
                    else if (button == _buttons.Left)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.MoveLeft
                            : _player.Controls.AimLeft;
                    }
                    else if (button == _buttons.Right)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.MoveRight
                            : _player.Controls.AimRight;
                    }
                    else if (button == _buttons.A)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.AimRight
                            : _player.Controls.MoveRight;
                    }
                    else if (button == _buttons.B)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.AimDown
                            : _player.Controls.MoveDown;
                    }
                    else if (button == _buttons.X)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.AimUp
                            : _player.Controls.MoveUp;
                    }
                    else if (button == _buttons.Y)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.AimLeft
                            : _player.Controls.MoveLeft;
                    }
                    else if (button == _buttons.L)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.AltAttack
                            : _player.Controls.Jump;
                    }
                    else if (button == _buttons.R)
                    {
                        control = _player.IsAltForm
                            ? _player.Controls.Boost
                            : _player.Controls.Shoot;
                    }
                    else if (button == _buttons.Start)
                    {
                        control = _player.Controls.Pause;
                    }
                    else if (button == _buttons.Select)
                    {
                        control = _player.Controls.Zoom;
                    }
                    else
                    {
                        throw new ProgramException("Unreachable AI button.");
                    }
                    bool prevDown = control.IsDown;
                    if (button.IsDown)
                    {
                        Flags3 &= ~AiFlags3.NoInput;
                        button.FramesUp = 0;
                        if (button.FramesDown < 6000)
                        {
                            button.FramesDown++;
                        }
                        button.IsDown = false;
                        control.IsDown = true;
                        control.IsPressed = !prevDown;
                        control.IsReleased = false;
                    }
                    else
                    {
                        button.FramesDown = 0;
                        if (button.FramesUp < 6000)
                        {
                            button.FramesUp++;
                        }
                        control.IsDown = false;
                        control.IsPressed = false;
                        control.IsReleased = prevDown;
                    }
                }
                if (_hasTouch)
                {
                    // sktodo-ai: this would set mouse aim values, but it's only used for Morph Ball,
                    // which we haven't implemented touch controls for (see Func2141A0C)
                    Flags3 &= ~AiFlags3.NoInput;
                    _framesWithoutTouch = 0;
                    if (_framesWithTouch < 6000)
                    {
                        _framesWithTouch++;
                    }
                    _hasTouch = false;
                }
                else
                {
                    _framesWithTouch = 0;
                    if (_framesWithoutTouch < 6000)
                    {
                        _framesWithoutTouch++;
                    }
                }
                for (int i = 0; i < _touchButtons.AllButtons.Count; i++)
                {
                    AiButton button = _touchButtons.AllButtons[i];
                    if (button.IsDown)
                    {
                        Flags3 &= ~AiFlags3.NoInput;
                        if (button == _touchButtons.Morph || button == _touchButtons.Unmorph)
                        {
                            _player.TrySwitchForms();
                        }
                        else if (button == _touchButtons.PowerBeam)
                        {
                            _player.TryEquipWeapon(BeamType.PowerBeam);
                        }
                        else if (button == _touchButtons.Missile)
                        {
                            _player.TryEquipWeapon(BeamType.Missile);
                        }
                        else if (button == _touchButtons.VoltDriver)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.VoltDriver);
                            _player.TryEquipWeapon(BeamType.VoltDriver);
                        }
                        else if (button == _touchButtons.Battlehammer)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.Battlehammer);
                            _player.TryEquipWeapon(BeamType.Battlehammer);
                        }
                        else if (button == _touchButtons.Imperialist)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.Imperialist);
                            _player.TryEquipWeapon(BeamType.Imperialist);
                        }
                        else if (button == _touchButtons.Judicator)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.Judicator);
                            _player.TryEquipWeapon(BeamType.Judicator);
                        }
                        else if (button == _touchButtons.Magmaul)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.Magmaul);
                            _player.TryEquipWeapon(BeamType.Magmaul);
                        }
                        else if (button == _touchButtons.ShockCoil)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.ShockCoil);
                            _player.TryEquipWeapon(BeamType.ShockCoil);
                        }
                        else if (button == _touchButtons.OmegaCannon)
                        {
                            _player.UpdateAffinityWeaponSlot(BeamType.OmegaCannon);
                            _player.TryEquipWeapon(BeamType.OmegaCannon);
                        }
                        button.FramesUp = 0;
                        if (button.FramesDown < 6000)
                        {
                            button.FramesDown++;
                        }
                        button.IsDown = false;
                    }
                    else
                    {
                        button.FramesDown = 0;
                        if (button.FramesUp < 6000)
                        {
                            button.FramesUp++;
                        }
                    }
                }
                _player._buttonAimX = 0;
                _player._buttonAimY = 0;
                if (_buttonAimX != 0)
                {
                    Flags3 &= ~AiFlags3.NoInput;
                    _player._buttonAimX = _buttonAimX;
                }
                if (_buttonAimY != 0)
                {
                    Flags3 &= ~AiFlags3.NoInput;
                    _player._buttonAimY = _buttonAimY;
                }
                _buttonAimX = 0;
                _buttonAimY = 0;
                UpdateNodeDataSetSelection();
                _nodeDataSelOff = 0;
                _nodeDataSelOn = 0;
            }

            public void Process()
            {
                if (_nodeData == null)
                {
                    return;
                }
                if (Flags2.TestFlag(AiFlags2.SeekItem) && _itemC8?.DespawnTimer == 0)
                {
                    Flags2 &= ~AiFlags2.SeekItem;
                }
                Flags2 &= ~AiFlags2.Bit18;
                Flags2 &= ~AiFlags2.Bit19;
                Flags2 &= ~AiFlags2.Bit20;
                Flags4 &= ~AiFlags4.Bit2;
                Func2134594();
                Func2148ABC();
                Execute(_executionTree[0]);
                Array.Fill(_slotHits, 0);
                Array.Fill(_slotDamage, 0);
                DamageFromHalfturret = 0;
                Flags2 &= ~AiFlags2.Bit14;
                Flags2 &= ~AiFlags2.Bit16;
                Flags2 &= ~AiFlags2.Bit17;
                Flags2 &= ~AiFlags2.Bit21;
            }

            // todo: member name
            private void Func2134594()
            {
                _entityRefs.Clear();
                UpdateAggro();
                if (GameState.Mode == GameMode.PrimeHunter && GameState.PrimeHunter == _player.SlotIndex
                    && Flags2.TestFlag(AiFlags2.SeekItem) && _itemC8 != null && (_itemC8.ItemType == ItemType.HealthSmall
                    || _itemC8.ItemType == ItemType.HealthMedium || _itemC8.ItemType == ItemType.HealthBig))
                {
                    Flags2 &= ~AiFlags2.SeekItem;
                }
            }

            private void UpdateAggro()
            {
                float fov = MathHelper.DegreesToRadians(_player.CameraInfo.Fov > 0 ? _player.CameraInfo.Fov : 78);
                Matrix4 perspectiveMatrix = _scene.GetPerspectiveMatrix(fov);
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var other = (PlayerEntity)entity;
                    if (other == _player || other.Health == 0 || !IsPlayerVisible(_player, other))
                    {
                        continue;
                    }
                    float w = Matrix.ProjectPosition(other.Position, Matrix4.Identity, perspectiveMatrix, out Vector2 proj);
                    if (w < 0)
                    {
                        // sktodo-ai: bug? should this be a continue? or is the byte array check only expected to pass for one player?
                        // but then why do we continue if the screen coordinates are out of range?
                        //Debugger.Break();
                        return;
                    }
                    if (proj.X >= 1 || proj.Y >= 1)
                    {
                        continue;
                    }
                    if (other.CurAlpha >= 1 || other.Flags2.TestFlag(PlayerFlags2.RadarReveal) || GameState.RadarPlayers
                        || other.OctolithFlag != null || GameState.PrimeHunter == other.SlotIndex)
                    {
                        AggroFunc214864C(6, 1, 2, null, other, 0, 30, 10, 3);
                    }
                    else
                    {
                        Vector3 between = other.Position - _player.CameraInfo.Position;
                        between = between.AddY(other.IsAltForm ? Fixed.ToFloat(other.Values.AltColYPos) : 0.5f);
                        int rand = (int)(between.LengthSquared * 4096);
                        int alpha = (int)(other.CurAlpha * 31);
                        if (alpha > 2)
                        {
                            // dividing the fx32 by 1.87-200.13 and truncating to int
                            rand /= alpha * alpha * 853;
                        }
                        else
                        {
                            // dividing the fx32 by 0.8333 (multiplying by 1.2) and truncating to int
                            rand /= 2 * 2 * 853;
                        }
                        int div = 1;
                        if (AggroFunc214857C(4, 2, 1, other, null))
                        {
                            div = 4;
                        }
                        if (Rng.GetRandomInt2((31 - alpha + rand) / div) != 0)
                        {
                            // sktodo-ai: same as above
                            return;
                        }
                        alpha = alpha <= 2 ? 1 : (alpha - 2);
                        AggroFunc214864C(6, 1, 2, null, other, 0, alpha, 10, 3);
                    }
                    float otherFov = MathHelper.DegreesToRadians(other.CameraInfo.Fov > 0 ? other.CameraInfo.Fov : 78);
                    Matrix4 otherPerspective = _scene.GetPerspectiveMatrix(otherFov);
                    w = Matrix.ProjectPosition(_player.Position, Matrix4.Identity, otherPerspective, out proj);
                    if (w < 0)
                    {
                        // sktodo-ai: same as above
                        //Debugger.Break();
                        return;
                    }
                    if (proj.X < 1 && proj.Y < 1)
                    {
                        AggroFunc214864C(6, 2, 1, other, null, 0, 30, 10, 3);
                    }
                }
            }

            private bool IsPlayerVisible(PlayerEntity player, PlayerEntity other)
            {
                return _playerVisibility[other.SlotIndex, player.SlotIndex];
            }

            private class AiPlayerAggro
            {
                public byte Field0A { get; set; }
                public byte Field0B { get; set; }
                public byte Field0C { get; set; }
                public byte Field0D { get; set; }
                public byte Field2A { get; set; }
                public ushort Field2B { get; set; }
                public ushort Staleness { get; set; }
                public ushort Expiration { get; set; }
                public PlayerEntity? Player1 { get; set; }
                public PlayerEntity? Player2 { get; set; }

                public void Clear()
                {
                    Field0A = 0;
                    Field0B = 0;
                    Field0C = 0;
                    Field0D = 0;
                    Field2A = 0;
                    Field2B = 0;
                    Staleness = 0;
                    Expiration = 0;
                    Player1 = null;
                    Player2 = null;
                }
            }

            private int _playerAggroCount = 0;
            private readonly AiPlayerAggro[] _playerAggro =
            [
                new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(),
                new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(),
                new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(),
                new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(),
                new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro(), new AiPlayerAggro()
            ];

            // todo: member name
            private int AggroFunc2148394(int a2, int a3, int a4, PlayerEntity? player1, PlayerEntity? player2)
            {
                // returns "priority" value for finding certain entities
                int result = 0;
                for (int i = 0; i < _playerAggroCount; i++)
                {
                    AiPlayerAggro aggro = _playerAggro[i];
                    if ((a2 == aggro.Field0A || a2 == 7)
                        && (a3 == aggro.Field0C || a3 == 7)
                        && (a4 == aggro.Field0D || a4 == 7)
                        && (player1 == aggro.Player1 || a3 != 2)
                        && (player2 == aggro.Player2 || a4 != 2))
                    {
                        result += aggro.Field2B;
                    }
                }
                return result;
            }

            // todo: member name
            private AiPlayerAggro? AggroFunc214847C(int a2, int a3, int a4, PlayerEntity? player1, PlayerEntity? player2)
            {
                AiPlayerAggro? result = null;
                int maxField4 = -1;
                for (int i = 0; i < _playerAggroCount; i++)
                {
                    AiPlayerAggro aggro = _playerAggro[i];
                    if ((a2 == aggro.Field0A || a2 == 7)
                        && (a3 == aggro.Field0C || a3 == 7)
                        && (a4 == aggro.Field0D || a4 == 7)
                        && (player1 == aggro.Player1 || a3 != 2)
                        && (player2 == aggro.Player2 || a4 != 2))
                    {
                        if (aggro.Staleness > maxField4)
                        {
                            result = aggro;
                            maxField4 = aggro.Staleness;
                        }
                    }
                }
                return result;
            }

            // todo: member name
            private bool AggroFunc214857C(int a2, int a3, int a4, PlayerEntity? player1, PlayerEntity? player2)
            {
                // note: in-game, this function returns the list item if it finds one,
                // but its usage is boolean (checking whether there is a return value)
                for (int i = 0; i < _playerAggroCount; i++)
                {
                    AiPlayerAggro aggro = _playerAggro[i];
                    if ((a2 == aggro.Field0A || a2 == 7)
                        && (a3 == aggro.Field0C || a3 == 7)
                        && (a4 == aggro.Field0D || a4 == 7)
                        && (player1 == aggro.Player1 || a3 != 2)
                        && (player2 == aggro.Player2 || a4 != 2))
                    {
                        return true;
                    }
                }
                return false;
            }

            // todo: member name
            private void AggroFunc214864C(int a2, int a3, int a4, PlayerEntity? player1,
                PlayerEntity? player2, int a7, int a8, int a9, int a10)
            {
                if (a10 == 2)
                {
                    AiPlayerAggro? aggro = AggroFunc21489B4(a2, a3, a4, player1, player2);
                    if (aggro != null)
                    {
                        aggro.Expiration += (ushort)a8; // sktodo-ai: FPS stuff for these fields?
                        if (aggro.Expiration > 54000)
                        {
                            aggro.Expiration = 54000;
                        }
                        if (a9 > aggro.Field0B)
                        {
                            aggro.Field0B = (byte)(a9 & 0xF);
                        }
                        aggro.Field2B += (ushort)a7; // sktodo-ai: also maybe FPS stuff for this field?
                        if (aggro.Field2B > 4000)
                        {
                            aggro.Field2B = 4000;
                        }
                        return;
                    }
                }
                else if (a10 == 3)
                {
                    AiPlayerAggro? aggro = AggroFunc21489B4(a2, a3, a4, player1, player2);
                    if (aggro != null)
                    {
                        aggro.Field0A = (byte)(a2 & 0xF);
                        aggro.Field0B = (byte)(a9 & 0xF);
                        aggro.Field0C = (byte)(a3 & 0xF);
                        aggro.Field0D = (byte)(a4 & 0xF);
                        aggro.Field2A = 3;
                        aggro.Field2B = (byte)(a7 & 0xF);
                        aggro.Staleness = 0;
                        aggro.Expiration = (ushort)a8;
                        aggro.Player1 = player1;
                        aggro.Player2 = player2;
                        return;
                    }
                }
                int index = _playerAggroCount;
                if (index >= _playerAggro.Length)
                {
                    int min = a9;
                    for (int i = 0; i < _playerAggro.Length; i++)
                    {
                        AiPlayerAggro aggro = _playerAggro[i];
                        if (aggro.Field0B < min)
                        {
                            index = i;
                            min = aggro.Field0B;
                        }
                    }
                }
                else
                {
                    _playerAggroCount++;
                }
                if (index < _playerAggro.Length)
                {
                    AiPlayerAggro aggro = _playerAggro[index];
                    aggro.Field0A = (byte)(a2 & 0xF);
                    aggro.Field0B = (byte)(a9 & 0xF);
                    aggro.Field0C = (byte)(a3 & 0xF);
                    aggro.Field0D = (byte)(a4 & 0xF);
                    aggro.Field2A = (byte)(a10 & 0xF);
                    aggro.Field2B = (byte)(a7 & 0xF);
                    aggro.Staleness = 0;
                    aggro.Expiration = (ushort)a8;
                    aggro.Player1 = player1;
                    aggro.Player2 = player2;
                }
            }

            // todo: member name
            private AiPlayerAggro? AggroFunc21489B4(int a2, int a3, int a4, PlayerEntity? player1, PlayerEntity? player2)
            {
                for (int i = 0; i < _playerAggroCount; i++)
                {
                    AiPlayerAggro aggro = _playerAggro[i];
                    if (aggro.Field2A == 1
                        || a2 != aggro.Field0A && a2 != 7
                        || a3 != aggro.Field0C && a3 != 7
                        || a4 != aggro.Field0D && a4 != 7
                        || player1 != aggro.Player1 && a3 == 2
                        || player2 != aggro.Player2 && a4 == 2)
                    {
                        return aggro;
                    }
                }
                return null;
            }

            private void UpdateAggroExpiration()
            {
                int i = 0;
                while (i < _playerAggroCount)
                {
                    AiPlayerAggro aggro = _playerAggro[i];
                    aggro.Staleness++;
                    if (aggro.Staleness > aggro.Expiration * 2) // sktodo-ai: FPS stuff? review Field6 (expiration) changes
                    {
                        AiPlayerAggro last = _playerAggro[_playerAggroCount - 1];
                        aggro.Field0A = last.Field0A;
                        aggro.Field0B = last.Field0B;
                        aggro.Field0C = last.Field0C;
                        aggro.Field0D = last.Field0D;
                        aggro.Staleness = last.Staleness;
                        aggro.Expiration = last.Expiration;
                        aggro.Player1 = last.Player1;
                        aggro.Player2 = last.Player2;
                        _playerAggroCount--;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            // todo: member name
            private void Func2148ABC()
            {
                UpdateAggroExpiration();
                Flags4 &= ~AiFlags4.Bit0;
                if (Vector3.Dot(_field1038, _player.CameraInfo.Facing) >= 255 / 256f)
                {
                    Flags4 |= AiFlags4.Bit0;
                }
                if (_field1020 > 0)
                {
                    _field1020--;
                }
            }

            // INFO
            // data1 is a tree/graph of AI execution. in steady state, one path is taken through the tree. the tree is initially set up pointing only down
            // the left side (terminating wherever the first node doesn't have any children).
            // execution consists of visiting each node and calling funcs_1[node->data3b], then funcs_2[node->field0].
            // execution reaches the end of the path after calling the above for a node with no children. it then returns to the previous recursion level and
            // does a check to determine if the path should be updated from that level forward, so that instead of the node that was just called, a different
            // child node would be called next time.
            // EX: after calling I in level 3, the recursion for E in level 2 determines whether E should be followed in level 3 by J next time.
            // this determination is made using data from both E (current node) and I (current node's currently selected child).
            // the current node has a list of weights with possible indices matching its number of children.
            // each item of the child node's data2 list has an index for which child node to switch to if that data2 is selected via the weight process.
            // first, in order to update the weights, funcs_3 invocations are made using the data5 parameters referenced by the data2's data4s.
            // if all of those invocations return true (or there are no data4s), then the weights are updated by a second round of funcs_3 invocations,
            // this time using the data5 parameters referenced by the data2 itself. these invocations return numeric values (mostly but not all booleans),
            // which are then multiplied by a weight value on the data2, and then added to the weight total for the corresponding index.
            // if that weight total accumulates to 100,000 or greater, that data2 is selected and its index is returned to switch the next level node to.
            // when the switch occurs, the tree is updated to point to the left side of any newly accessible child nodes below that point. weights are reset.
            // the switch is accompanied by calls to funcs_1[node->data3a] and funcs_4[node->field0] for the new child and its left-side children.
            // a special index of 100 (or technically any value 20 or greater) indicates that weights should reset and a child node switch should not occur.
            // the weights returned by funcs_3 calls for this purpose accumulate in an index one past the max count of data1 options that could be switched to,
            // and when that index reaches 100,000, the reset occurs.
            // execution then return to all previous recursion levels and performs the same checks to determine if the tree should change at each higher point.
            // if it does, the the change to the lower level is potentially dropped, since the tree will start off down the left side from the higher level.

            private class AiContext
            {
                public int Func24Id { get; set; }
                public byte Field4 { get; set; }
                public byte Field5 { get; set; }
                public byte Field6 { get; set; }
                public byte Field7 { get; set; }
                public byte Field8 { get; set; }
                public byte Field9 { get; set; }
                public byte FieldA { get; set; }
                public byte FieldB { get; set; }
                public byte FieldC { get; set; }
                public byte FieldD { get; set; }
                public byte FieldE { get; set; }
                public byte FieldF { get; set; }
                public byte Field10 { get; set; }
                public int Field14 { get; set; }
                public int Field18 { get; set; }
                public int Field1C { get; set; }
                public int Field20 { get; set; }
                public int Field24 { get; set; }
                public bool Field28 { get; set; }
                public int Field2C { get; set; }
                public bool Field30 { get; set; }
                public Vector3 Field34 { get; set; } // stored player position
                public int Field40 { get; set; } // timer/counter
                public int Field44 { get; set; } // timer
                public AiPersonalityData1 Data1 { get; set; } = null!;
                public int CallCount;
                public int Depth;
                public int[] Weights { get; } = new int[21];

                public void Clear()
                {
                    Func24Id = 0;
                    Field4 = 0;
                    Field5 = 0;
                    Field6 = 0;
                    Field7 = 0;
                    Field8 = 0;
                    Field9 = 0;
                    FieldA = 0;
                    FieldB = 0;
                    FieldC = 0;
                    FieldD = 0;
                    FieldE = 0;
                    FieldF = 0;
                    Field10 = 0;
                    Field14 = 0;
                    Field18 = 0;
                    Field1C = 0;
                    Field20 = 0;
                    Field24 = 0;
                    Field28 = false;
                    Field2C = 0;
                    Field30 = false;
                    Field34 = Vector3.Zero;
                    Field40 = 0;
                    Field44 = 0;
                    Data1 = null!;
                    CallCount = 0;
                    Depth = 0;
                    Array.Fill(Weights, 0);
                }
            }

            private const int _maxContextDepth = 20;
            private readonly AiContext[] _executionTree = new AiContext[_maxContextDepth];
            // IDs that map to something other than Func4_21462DC()
            private static readonly HashSet<int> _func4Ids = [1, 2, 3];

            private void UpdateExecutionPath(AiPersonalityData1 data1, int depth)
            {
                Debug.Assert(depth < _maxContextDepth);
                AiContext context = _executionTree[depth];
                context.Data1 = data1;
                context.Depth = depth;
                context.CallCount = 0;
                Array.Fill(context.Weights, 0, 0, data1.Data1.Count);
                ExecuteFuncs1(context.Data1.Data3a);
                context.Func24Id = context.Data1.Func24Id;
                context.Field10 = 0;
                _field1020 = 0;
                _node44 = null;
                Flags4 &= ~AiFlags4.Bit3;
                Flags2 &= ~AiFlags2.Bit10;
                RemovePlayerFromGlobals(_player);
                if (_func4Ids.Contains(context.Func24Id) && _player.Flags1.TestFlag(PlayerFlags1.Grounded))
                {
                    Flags2 &= ~AiFlags2.Bit7;
                }
                ExecuteFuncs4(context);
                if (data1.Data1.Count > 0 && depth < _maxContextDepth - 1)
                {
                    UpdateExecutionPath(data1.Data1[0], depth + 1);
                }
            }

            private void Execute(AiContext context)
            {
                ExecuteFuncs1(context.Data1.Data3b);
                ExecuteFuncs2(context);
                if (context.Func24Id != 0 && _player.EquipWeapon.Flags.TestFlag(WeaponFlags.CanZoom)
                    && _buttons.Select.FramesUp > 5 * 2 // todo: FPS stuff
                    && (!_player.EquipInfo.Zoomed && Flags4.TestFlag(AiFlags4.Bit2)
                    || _player.EquipInfo.Zoomed && !Flags4.TestFlag(AiFlags4.Bit2)))
                {
                    _buttons.Select.IsDown = true;
                }
                if (_player.Hunter == Hunter.Spire && !_player.IsAltForm)
                {
                    _field116 = 0;
                }
                if (context.CallCount < Int32.MaxValue)
                {
                    context.CallCount++;
                }
                if (context.Data1.Data1.Count > 0 && context.Depth < _maxContextDepth - 1)
                {
                    Execute(_executionTree[context.Depth + 1]);
                    int newChildIndex = UpdatePathWeights(context);
                    if (newChildIndex != -1)
                    {
                        Debug.Assert(newChildIndex < context.Data1.Data1.Count);
                        UpdateExecutionPath(context.Data1.Data1[newChildIndex], context.Depth + 1);
                    }
                }
            }

            private int UpdatePathWeights(AiContext context)
            {
                AiPersonalityData1 nextData1 = _executionTree[context.Depth + 1].Data1;
                if (nextData1.Data2.Count == 0)
                {
                    return -1;
                }
                int result = -1;
                for (int i = 0; i < nextData1.Data2.Count; i++)
                {
                    AiPersonalityData2 data2 = nextData1.Data2[i];
                    bool noUpdate = false;
                    for (int j = 0; j < data2.Data4.Count; j++)
                    {
                        AiPersonalityData4 data4 = data2.Data4[j];
                        if (ExecuteFuncs3(context, data4.Func3Id, data4.Parameters) == 0)
                        {
                            noUpdate = true;
                            break;
                        }
                    }
                    if (noUpdate)
                    {
                        continue;
                    }
                    int weightIndex = data2.Data1SelectIndex;
                    if (weightIndex >= 20)
                    {
                        weightIndex = context.Data1.Data1.Count;
                    }
                    // sktodo-ai: we need to determine which funcs3 functions are frame/time-based and which are occurrence-based.
                    // occurrence-based should take the same number of repeats to hit 100,000, but frame-based will need to accumulate at half speed.
                    context.Weights[weightIndex] += ExecuteFuncs3(context, data2.Func3Id, data2.Parameters) * data2.Weight;
                    if (context.Weights[weightIndex] >= 100000)
                    {
                        result = data2.Data1SelectIndex;
                        context.Weights[weightIndex] = 0;
                        break;
                    }
                }
                if (result < 20)
                {
                    // succeded with valid index (return that index), or didn't find a new index (return -1)
                    return result;
                }
                // succeeded with the "reset" index (reset weights and return -1)
                Array.Fill(context.Weights, 0, 0, context.Data1.Data1.Count);
                return -1;
            }

            // todo: member name (and delete the following)
            /*
                 0: index 0, clearY  no, normalize  no  |  000 0 0 000 *
                 1: index 1, clearY  no, normalize  no  |  000 0 0 001 *
                 2: index 2, clearY  no, normalize  no  |  000 0 0 010
                 3: index 3, clearY  no, normalize  no  |  000 0 0 011
                 4: index 4, clearY  no, normalize  no  |  000 0 0 100
                 5: index 5, clearY  no, normalize  no  |  000 0 0 101
                 6: index 6, clearY  no, normalize  no  |  000 0 0 110
                 7: index 7, clearY  no, normalize  no  |  000 0 0 111 *
                 8: index 0, clearY yes, normalize  no  |  000 0 1 000 *
                 9: index 1, clearY yes, normalize  no  |  000 0 1 001
                10: index 2, clearY yes, normalize  no  |  000 0 1 010
                11: index 3, clearY yes, normalize  no  |  000 0 1 011
                12: index 4, clearY yes, normalize  no  |  000 0 1 100
                13: index 5, clearY yes, normalize  no  |  000 0 1 101
                14: index 6, clearY yes, normalize  no  |  000 0 1 110
                15: index 7, clearY yes, normalize  no  |  000 0 1 111
                16: index 0, clearY  no, normalize yes  |  000 1 0 000
                17: index 1, clearY  no, normalize yes  |  000 1 0 001
                18: index 2, clearY  no, normalize yes  |  000 1 0 010
                19: index 3, clearY  no, normalize yes  |  000 1 0 011
                20: index 4, clearY  no, normalize yes  |  000 1 0 100
                21: index 5, clearY  no, normalize yes  |  000 1 0 101
                22: index 6, clearY  no, normalize yes  |  000 1 0 110
                23: index 7, clearY  no, normalize yes  |  000 1 0 111
                24: index 0, clearY yes, normalize yes  |  000 1 1 000
                25: index 1, clearY yes, normalize yes  |  000 1 1 001
                26: index 2, clearY yes, normalize yes  |  000 1 1 010
                27: index 3, clearY yes, normalize yes  |  000 1 1 011
                28: index 4, clearY yes, normalize yes  |  000 1 1 100
                29: index 5, clearY yes, normalize yes  |  000 1 1 101
                30: index 6, clearY yes, normalize yes  |  000 1 1 110
                31: index 7, clearY yes, normalize yes  |  000 1 1 111
            */
            private Vector3 ExecuteVectorFunc(int index, bool clearY, bool normalize)
            {
                // the game encodes all three parameters in one value, which would be useful if it came from metadata,
                // but it never does. this is only called with a small handful of constant values, so I'm not bothering.
                Vector3 result = index switch
                {
                    0 => Func213A470(),
                    1 => Func213A458(),
                    2 => Func213A3DC(),
                    3 => Func213A3C0(),
                    4 => Func213A3A8(),
                    5 => Func213A37C(),
                    6 => Func213A35C(),
                    7 => Func213A31C(),
                    _ => throw new ProgramException("Invalid AI vector func.")
                };
                if (clearY)
                {
                    result = result.WithY(0);
                }
                if (normalize)
                {
                    result = result != Vector3.Zero ? result.Normalized() : Vector3.UnitX;
                }
                return result;
            }

            // todo: member name
            private Vector3 Func213A470()
            {
                Debug.Assert(_targetPlayer != null);
                return _targetPlayer.Position - _player.Position;
            }

            // todo: member name
            private Vector3 Func213A458()
            {
                return _player._facingVector;
            }

            // todo: member name
            private Vector3 Func213A3DC()
            {
                Debug.Assert(_targetPlayer != null);
                _targetPlayer.GetPosition(out Vector3 targetPos);
                targetPos = targetPos.AddY(_targetPlayer.IsAltForm
                    ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos)
                    : 0.5f);
                return targetPos - _player._muzzlePos;
            }

            // todo: member name
            private Vector3 Func213A3C0()
            {
                return _player._aimPosition - _player._muzzlePos;
            }

            // todo: member name
            private Vector3 Func213A3A8()
            {
                Debug.Assert(_targetPlayer != null);
                return _targetPlayer._facingVector;
            }

            // todo: member name
            private Vector3 Func213A37C()
            {
                Debug.Assert(_node40 != null);
                return _node40.Position - _player.Position;
            }

            // todo: member name
            private Vector3 Func213A35C()
            {
                return _player.CameraInfo.Facing;
            }

            // todo: member name
            private Vector3 Func213A31C()
            {
                FindEntityRef(AiEntRefType.Type26);
                Debug.Assert(_entityRefs.Field26 != null);
                return _entityRefs.Field26.Position - _player.Position;
            }

            // todo: member name
            private void ExecuteFuncs1(IReadOnlyList<int> funcsIds)
            {
                for (int i = 0; i < funcsIds.Count; i++)
                {
                    switch (funcsIds[i])
                    {
                    case 0:
                        Func1_214A39C();
                        break;
                    case 1:
                        Func1_214A098();
                        break;
                    case 2:
                        Func1_2149D3C();
                        break;
                    case 3:
                        Func1_2149C98();
                        break;
                    case 4:
                        Func1_2149C80();
                        break;
                    case 5:
                        Func1_2149C68();
                        break;
                    case 6:
                        Func1_2149C50();
                        break;
                    case 7:
                        Func1_2149C38();
                        break;
                    case 8:
                        Func1_2149C20();
                        break;
                    case 9:
                        Func1_2149C08();
                        break;
                    case 10:
                        Func1_2149BF0();
                        break;
                    case 11:
                        Func1_2149BD8();
                        break;
                    case 12:
                        Func1_2149BC0();
                        break;
                    case 13:
                        Func1_2149BA8();
                        break;
                    case 14:
                        Func1_2149B98();
                        break;
                    case 15:
                        Func1_2149AD8();
                        break;
                    case 16:
                        Func1_2149AC8();
                        break;
                    case 17:
                        Func1_2149ABC();
                        break;
                    case 18:
                        Func1_2149AB0();
                        break;
                    case 19:
                        Func1_2149AA4();
                        break;
                    case 20:
                        Func1_2149A98();
                        break;
                    case 21:
                        Func1_2149A64();
                        break;
                    case 22:
                        Func1_2149824();
                        break;
                    case 23:
                        Func1_21497F0();
                        break;
                    case 24:
                        Func1_2149570();
                        break;
                    case 25:
                        Func1_21494FC();
                        break;
                    case 26:
                        Func1_2149488();
                        break;
                    case 27:
                        Func1_2149414();
                        break;
                    case 28:
                        Func1_21493A0();
                        break;
                    case 29:
                        Func1_214932C();
                        break;
                    case 30:
                        Func1_21495A4();
                        break;
                    case 31:
                        Func1_2149530();
                        break;
                    case 32:
                        Func1_21494BC();
                        break;
                    case 33:
                        Func1_2149448();
                        break;
                    case 34:
                        Func1_21493D4();
                        break;
                    case 35:
                        Func1_2149360();
                        break;
                    case 36:
                        Func1_21492EC();
                        break;
                    case 37:
                        Func1_21492DC();
                        break;
                    case 38:
                        Func1_21492CC();
                        break;
                    case 39:
                        Func1_21492BC();
                        break;
                    case 40:
                        Func1_21492AC();
                        break;
                    case 41:
                        Func1_214929C();
                        break;
                    case 42:
                        Func1_214928C();
                        break;
                    case 43:
                        Func1_214927C();
                        break;
                    case 44:
                        Func1_214926C();
                        break;
                    case 45:
                        Func1_214925C();
                        break;
                    case 46:
                        Func1_214924C();
                        break;
                    case 47:
                        Func1_214923C();
                        break;
                    case 48:
                        Func1_214922C();
                        break;
                    case 49:
                        Func1_214921C();
                        break;
                    case 50:
                        Func1_214920C();
                        break;
                    case 51:
                        Func1_21491FC();
                        break;
                    case 52:
                        Func1_21491E4();
                        break;
                    case 53:
                        Func1_21491CC();
                        break;
                    case 54:
                        Func1_21491B4();
                        break;
                    case 55:
                        Func1_214919C();
                        break;
                    case 56:
                        Func1_2149184();
                        break;
                    case 57:
                        Func1_214916C();
                        break;
                    case 58:
                        Func1_2149154();
                        break;
                    case 59:
                        Func1_214913C();
                        break;
                    case 60:
                        Func1_2149124();
                        break;
                    case 61:
                        Func1_214910C();
                        break;
                    case 62:
                        Func1_21490F4();
                        break;
                    case 63:
                        Func1_21490DC();
                        break;
                    case 64:
                        Func1_21490C4();
                        break;
                    case 65:
                        Func1_21490AC();
                        break;
                    case 66:
                        Func1_2149094();
                        break;
                    case 67:
                        Func1_2149088();
                        break;
                    case 68:
                        Func1_2149034();
                        break;
                    case 69:
                        Func1_2148F10();
                        break;
                    case 70:
                        Func1_2148EDC();
                        break;
                    case 71:
                        Func1_2148ECC();
                        break;
                    case 72:
                        Func1_2148EB8();
                        break;
                    case 73:
                        Func1_2148EA8();
                        break;
                    case 74:
                        Func1_2148E98();
                        break;
                    case 75:
                        Func1_2148E88();
                        break;
                    case 76:
                        Func1_2148E74();
                        break;
                    case 77:
                        Func1_2148E64();
                        break;
                    case 78:
                        Func1_2148E54();
                        break;
                    case 79:
                        Func1_2148DF8();
                        break;
                    case 80:
                        Func1_2148DE8();
                        break;
                    case 81:
                        Func1_2148D50();
                        break;
                    case 82:
                        Func1_UnlockEchoHallForceField();
                        break;
                    case 83:
                        Func1_SetInvulnerable();
                        break;
                    default:
                        throw new ProgramException("Invalid AI func 1.");
                    }
                }
            }

            // todo: member name
            private void ExecuteFuncs2(AiContext context)
            {
                switch (context.Func24Id)
                {
                case 0:
                case 125:
                    break;
                case 1:
                    Func2_213EA10(context);
                    break;
                case >= 2 and <= 44:
                case 46:
                case 48:
                case >= 50 and <= 78:
                case >= 82 and <= 98:
                case 101:
                case 103:
                case >= 106 and <= 113:
                case >= 115 and <= 122:
                    Func2_213EA48(context);
                    break;
                case 45:
                    Func2_213DDCC(context);
                    break;
                case 47:
                    Func2_213DA88(context);
                    break;
                case 49:
                    Func2_213E148(context);
                    break;
                case 79:
                    Func2_213E9C8(context);
                    break;
                case 80:
                    Func2_213E984(context);
                    break;
                case 81:
                    Func2_213E934(context);
                    break;
                case 99:
                    Func2_213E904(context);
                    break;
                case 100:
                    Func2_213E684(context);
                    break;
                case 102:
                    Func2_213E3C4(context);
                    break;
                case 104:
                    Func2_213E31C(context);
                    break;
                case 105:
                    Func2_213E274(context);
                    break;
                case 114:
                    Func2_213E1CC(context);
                    break;
                case 123:
                    Func2_213D9B8(context);
                    break;
                case 124:
                    Func2_213D96C(context);
                    break;
                default:
                    throw new ProgramException("Invalid AI func 2.");
                }
            }

            // todo: member name
            private int ExecuteFuncs3(AiContext context, int funcId, AiPersonalityData5 param)
            {
                return funcId switch
                {
                    0 => Func3_213D87C(context, param),
                    1 => Func3_213D83C(context, param),
                    2 => Func3_213D814(context, param),
                    3 => Func3_213D7F0(context, param),
                    4 => Func3_213D800(context, param),
                    5 => Func3_213D7E8(context, param),
                    6 => Func3_213D7D0(context, param),
                    7 => Func3_213D7B8(context, param),
                    8 => Func3_213D7A0(context, param),
                    9 => Func3_213D77C(context, param),
                    10 => Func3_213D758(context, param),
                    11 => Func3_213D734(context, param),
                    12 => Func3_213D710(context, param),
                    13 => Func3_213D6D0(context, param),
                    14 => Func3_213D6AC(context, param),
                    15 => Func3_213D624(context, param),
                    16 => Func3_213D608(context, param),
                    17 => Func3_213D564(context, param),
                    18 => Func3_213D540(context, param),
                    19 => Func3_213D530(context, param),
                    20 => Func3_213D514(context, param),
                    21 => Func3_213D4C0(context, param),
                    22 => Func3_213D49C(context, param),
                    23 => Func3_213D43C(context, param),
                    24 => Func3_213D418(context, param),
                    25 => Func3_213D388(context, param),
                    26 => Func3_213D36C(context, param),
                    27 => Func3_213D2C0(context, param),
                    28 => Func3_213D2A4(context, param),
                    29 => Func3_213D234(context, param),
                    30 => Func3_213D218(context, param),
                    31 => Func3_213D178(context, param),
                    32 => Func3_213D15C(context, param),
                    33 => Func3_213D128(context, param),
                    34 => Func3_213D0F4(context, param),
                    35 => Func3_213D0C4(context, param),
                    36 => Func3_213D0A8(context, param),
                    37 => Func3_213D078(context, param),
                    38 => Func3_213D05C(context, param),
                    39 => Func3_213D044(context, param),
                    40 => Func3_213D028(context, param),
                    41 => Func3_213D010(context, param),
                    42 => Func3_213CFF4(context, param),
                    43 => Func3_213CFDC(context, param),
                    44 => Func3_213CFC0(context, param),
                    45 => Func3_213CFA4(context, param),
                    46 => Func3_213CF0C(context, param),
                    47 => Func3_213CEE8(context, param),
                    48 => Func3_213CDB8(context, param),
                    49 => Func3_213CF94(context, param),
                    50 => Func3_213CF7C(context, param),
                    51 => Func3_213CDA4(context, param),
                    52 => Func3_213CD74(context, param),
                    53 => Func3_213CD58(context, param),
                    54 => Func3_213CD34(context, param),
                    55 => Func3_213CD18(context, param),
                    56 => Func3_213CCF4(context, param),
                    57 => Func3_213CCD8(context, param),
                    58 => Func3_213CCBC(context, param),
                    59 => Func3_213CCB0(context, param),
                    60 => Func3_213CC94(context, param),
                    61 => Func3_213CBE4(context, param),
                    62 => Func3_213CBC0(context, param),
                    63 => Func3_213CBB0(context, param),
                    64 => Func3_213CB8C(context, param),
                    65 => Func3_213CADC(context, param),
                    66 => Func3_213CAA8(context, param),
                    67 => Func3_213CA84(context, param),
                    68 => Func3_213CA70(context, param),
                    69 => Func3_213CA58(context, param),
                    70 => Func3_213CA2C(context, param),
                    71 => Func3_213CA00(context, param),
                    72 => Func3_213C9D4(context, param),
                    73 => Func3_213C9C4(context, param),
                    74 => Func3_213C89C(context, param),
                    75 => Func3_213C88C(context, param),
                    76 => Func3_213C764(context, param),
                    77 => Func3_213C75C(context, param),
                    78 => Func3_213C698(context, param),
                    79 => Func3_213C64C(context, param),
                    80 => Func3_213C600(context, param),
                    81 => Func3_213C52C(context, param),
                    82 => Func3_213C48C(context, param),
                    83 => Func3_213C470(context, param),
                    84 => Func3_213C334(context, param),
                    85 => Func3_213C310(context, param),
                    86 => Func3_213C0D0(context, param),
                    87 => Func3_213C078(context, param),
                    88 => Func3_213C054(context, param),
                    89 => Func3_213BFFC(context, param),
                    90 => Func3_213BFD8(context, param),
                    91 => Func3_213BED8(context, param),
                    92 => Func3_213BEBC(context, param),
                    93 => Func3_213BEA0(context, param),
                    94 => Func3_213BE48(context, param),
                    95 => Func3_213BE10(context, param),
                    96 => Func3_213BDF4(context, param),
                    97 => Func3_213BD7C(context, param),
                    98 => Func3_213BCE8(context, param),
                    99 => Func3_213BCC4(context, param),
                    100 => Func3_213BCB0(context, param),
                    101 => Func3_213BC8C(context, param),
                    102 => Func3_213BC70(context, param),
                    103 => Func3_213BC4C(context, param),
                    104 => Func3_213BC0C(context, param),
                    105 => Func3_213BBE8(context, param),
                    106 => Func3_213BBA0(context, param),
                    107 => Func3_213BB7C(context, param),
                    108 => Func3_213BAF4(context, param),
                    109 => Func3_213BAD0(context, param),
                    110 => Func3_213BA68(context, param),
                    111 => Func3_213BA44(context, param),
                    112 => Func3_213BA28(context, param),
                    113 => Func3_213BA04(context, param),
                    114 => Func3_213B99C(context, param),
                    115 => Func3_213B978(context, param),
                    116 => Func3_213B8B0(context, param),
                    117 => Func3_213B88C(context, param),
                    118 => Func3_213B7A0(context, param),
                    119 => Func3_213B77C(context, param),
                    120 => Func3_213B690(context, param),
                    121 => Func3_213B5DC(context, param),
                    122 => Func3_213B528(context, param),
                    123 => Func3_213B4E4(context, param),
                    124 => Func3_213B4A0(context, param),
                    125 => Func3_213B45C(context, param),
                    126 => Func3_213B3F0(context, param),
                    127 => Func3_213B3A0(context, param),
                    128 => Func3_213B37C(context, param),
                    129 => Func3_213B34C(context, param),
                    130 => Func3_213B328(context, param),
                    131 => Func3_213B284(context, param),
                    132 => Func3_213B260(context, param),
                    133 => Func3_213B1F0(context, param),
                    134 => Func3_213B1D8(context, param),
                    135 => Func3_213B1C0(context, param),
                    136 => Func3_213B1A8(context, param),
                    137 => Func3_213B190(context, param),
                    138 => Func3_213B178(context, param),
                    139 => Func3_213B160(context, param),
                    140 => Func3_213B148(context, param),
                    141 => Func3_213B130(context, param),
                    142 => Func3_213B118(context, param),
                    143 => Func3_213B100(context, param),
                    144 => Func3_213B0E8(context, param),
                    145 => Func3_213B0D0(context, param),
                    146 => Func3_213B0B8(context, param),
                    147 => Func3_213B0A0(context, param),
                    148 => Func3_213B088(context, param),
                    149 => Func3_213B070(context, param),
                    150 => Func3_213B058(context, param),
                    151 => Func3_213B040(context, param),
                    152 => Func3_213B020(context, param),
                    153 => Func3_213B000(context, param),
                    154 => Func3_213AFE0(context, param),
                    155 => Func3_213AFC0(context, param),
                    156 => Func3_213AFA0(context, param),
                    157 => Func3_213AF80(context, param),
                    158 => Func3_213AF68(context, param),
                    159 => Func3_213AF50(context, param),
                    160 => Func3_213AF38(context, param),
                    161 => Func3_213AF20(context, param),
                    162 => Func3_213AF08(context, param),
                    163 => Func3_213AEF0(context, param),
                    164 => Func3_213AED8(context, param),
                    165 => Func3_213AEC0(context, param),
                    166 => Func3_213AEA8(context, param),
                    167 => Func3_213AE90(context, param),
                    168 => Func3_213AE78(context, param),
                    169 => Func3_213AE60(context, param),
                    170 => Func3_213AE48(context, param),
                    171 => Func3_213AE30(context, param),
                    172 => Func3_213AE14(context, param),
                    173 => Func3_213ADF8(context, param),
                    174 => Func3_213ADC4(context, param),
                    175 => Func3_213ADA0(context, param),
                    176 => Func3_213AD88(context, param),
                    177 => Func3_213AD64(context, param),
                    178 => Func3_213ACE8(context, param),
                    179 => Func3_213ACCC(context, param),
                    180 => Func3_213ACA8(context, param),
                    181 => Func3_213AC8C(context, param),
                    182 => Func3_213AC70(context, param),
                    183 => Func3_213AC54(context, param),
                    184 => Func3_213AC38(context, param),
                    185 => Func3_213AC04(context, param),
                    186 => Func3_213ABC0(context, param),
                    187 => Func3_213AB8C(context, param),
                    188 => Func3_213AB58(context, param),
                    189 => Func3_213AB24(context, param),
                    190 => Func3_213AAF0(context, param),
                    191 => Func3_213AA64(context, param),
                    192 => Func3_213AA20(context, param),
                    193 => Func3_213A9B8(context, param),
                    194 => Func3_213A94C(context, param),
                    195 => Func3_213A938(context, param),
                    196 => Func3_213A91C(context, param),
                    197 => Func3_213A900(context, param),
                    198 => Func3_213A8DC(context, param),
                    199 => Func3_213A8A8(context, param),
                    200 => Func3_213A884(context, param),
                    201 => Func3_213A868(context, param),
                    202 => Func3_213A844(context, param),
                    203 => Func3_213A828(context, param),
                    204 => Func3_213A804(context, param),
                    205 => Func3_213A798(context, param),
                    206 => Func3_213A72C(context, param),
                    207 => Func3_213A714(context, param),
                    208 => Func3_213A698(context, param),
                    209 => Func3_213A688(context, param),
                    210 => Func3_213A660(context, param),
                    211 => Func3_213A650(context, param),
                    _ => throw new ProgramException("Invalid AI func 3.")
                };
            }

            // todo: member name
            private void ExecuteFuncs4(AiContext context)
            {
                switch (context.Func24Id)
                {
                case 0:
                case 1:
                case 47:
                case 49:
                case 99:
                    break;
                case >= 2 and <= 44:
                case 46:
                case 48:
                case >= 50 and <= 78:
                case >= 82 and <= 98:
                case 101:
                case 103:
                case >= 106 and <= 113:
                case >= 115 and <= 122:
                    Func4_21462DC(context);
                    break;
                case 45:
                    Func4_2145EB0(context);
                    break;
                case 79:
                    Func4_21462AC(context);
                    break;
                case 80:
                    Func4_2146284(context);
                    break;
                case 81:
                    Func4_21461EC(context);
                    break;
                case 100:
                    Func4_214612C(context);
                    break;
                case 102:
                    Func4_2145F78(context);
                    break;
                case 104:
                    Func4_2145F50(context);
                    break;
                case 105:
                    Func4_2145F28(context);
                    break;
                case 114:
                    Func4_2145F00(context);
                    break;
                case 123:
                    Func4_2145E54(context);
                    break;
                case 124:
                    Func4_2145E40(context);
                    break;
                case 125:
                    Func4_SetDespawned(context);
                    break;
                default:
                    throw new ProgramException("Invalid AI func 4.");
                }
            }

            // skhere
            // todo: member names
            #region Funcs1

            private void Func1_214A39C()
            {
                // skhere
            }

            private void Func1_214A098()
            {
                // skhere
            }

            private void Func1_2149D3C()
            {
                // skhere
            }

            private void Func1_2149C98()
            {
                // skhere
            }

            private void Func1_2149C80()
            {
                // skhere
            }

            private void Func1_2149C68()
            {
                _weapon1 = 2;
                Flags4 &= ~AiFlags4.Bit1;
            }

            private void Func1_2149C50()
            {
                _weapon1 = 2; // sktodo-ai: enum/mapping for these
                Flags4 |= AiFlags4.Bit1;
            }

            private void Func1_2149C38()
            {
                // skhere
            }

            private void Func1_2149C20()
            {
                // skhere
            }

            private void Func1_2149C08()
            {
                // skhere
            }

            private void Func1_2149BF0()
            {
                // skhere
            }

            private void Func1_2149BD8()
            {
                // skhere
            }

            private void Func1_2149BC0()
            {
                // skhere
            }

            private void Func1_2149BA8()
            {
                // skhere
            }

            private void Func1_2149B98()
            {
                // skhere
            }

            private bool CanChargeWeapon()
            {
                WeaponInfo weapon = _player.EquipWeapon;
                return weapon.Flags.TestFlag(WeaponFlags.CanCharge) && _player._availableCharges[_player.CurrentWeapon]
                    && (_player._ammo[weapon.AmmoType] >= weapon.ChargeCost || _player._ammo[weapon.AmmoType] == -1);
            }

            private void Func1_2149AD8()
            {
                // keep holding shoot to charge weapon
                if (_buttons.R.FramesDown == 0 || _player.IsAltForm)
                {
                    return;
                }
                EquipInfo equip = _player.EquipInfo;
                WeaponInfo weapon = _player.EquipWeapon;
                if (CanChargeWeapon())
                {
                    _buttons.R.IsDown = true;
                }
            }

            private void Func1_2149AC8()
            {
                // skhere
            }

            private void Func1_2149ABC()
            {
                // skhere
            }

            private void Func1_2149AB0()
            {
                // skhere
            }

            private void Func1_2149AA4()
            {
                // skhere
            }

            private void Func1_2149A98()
            {
                // skhere
            }

            private void Func1_2149A64()
            {
                // skhere
            }

            private void Func1_2149824()
            {
                // skhere
            }

            private void Func1_21497F0()
            {
                // skhere
            }

            private void Func1_2149570()
            {
                // skhere
            }

            private void Func1_21494FC()
            {
                // skhere
            }

            private void Func1_2149488()
            {
                // skhere
            }

            private void Func1_2149414()
            {
                // skhere
            }

            private void Func1_21493A0()
            {
                // skhere
            }

            private void Func1_214932C()
            {
                // skhere
            }

            private void Func1_21495A4()
            {
                // skhere
            }

            private void Func1_2149530()
            {
                // skhere
            }

            private void Func1_21494BC()
            {
                // skhere
            }

            private void Func1_2149448()
            {
                // skhere
            }

            private void Func1_21493D4()
            {
                // skhere
            }

            private void Func1_2149360()
            {
                // skhere
            }

            private void Func1_21492EC()
            {
                // skhere
            }

            private void Func1_21492DC()
            {
                // note: for MP, the game finds the first player object, rather than the main player.
                if (PlayerEntity.Main == null)
                {
                    Flags2 |= AiFlags2.Bit9;
                }
                else
                {
                    Flags2 &= ~AiFlags2.Bit9;
                    Func21356C0(PlayerEntity.Main);
                }
            }

            private void Func1_21492CC()
            {
                // skhere
            }

            private void Func1_21492BC()
            {
                // skhere
            }

            private void Func1_21492AC()
            {
                // skhere
            }

            private void Func1_214929C()
            {
                // skhere
            }

            private void Func1_214928C()
            {
                Func21354B0();
            }

            private void Func1_214927C()
            {
                // skhere
            }

            private void Func1_214926C()
            {
                // skhere
            }

            private void Func1_214925C()
            {
                // skhere
            }

            private void Func1_214924C()
            {
                // skhere
            }

            private void Func1_214923C()
            {
                // skhere
            }

            private void Func1_214922C()
            {
                // skhere
            }

            private void Func1_214921C()
            {
                // skhere
            }

            private void Func1_214920C()
            {
                // skhere
            }

            private void Func1_21491FC()
            {
                Func21355D8();
            }

            private void Func1_21491E4()
            {
                _queuedFindEntityAction = AiQueuedEnt.Type0;
                Flags2 |= AiFlags2.Bit1;
            }

            private void Func1_21491CC()
            {
                // skhere
            }

            private void Func1_21491B4()
            {
                // skhere
            }

            private void Func1_214919C()
            {
                // skhere
            }

            private void Func1_2149184()
            {
                // skhere
            }

            private void Func1_214916C()
            {
                // skhere
            }

            private void Func1_2149154()
            {
                // skhere
            }

            private void Func1_214913C()
            {
                // skhere
            }

            private void Func1_2149124()
            {
                // skhere
            }

            private void Func1_214910C()
            {
                // skhere
            }

            private void Func1_21490F4()
            {
                // skhere
            }

            private void Func1_21490DC()
            {
                // skhere
            }

            private void Func1_21490C4()
            {
                // skhere
            }

            private void Func1_21490AC()
            {
                // skhere
            }

            private void Func1_2149094()
            {
                // skhere
            }

            private void Func1_2149088()
            {
                _field30 = 1;
            }

            private void Func1_2149034()
            {
                // skhere
            }

            private void Func1_2148F10()
            {
                // skhere
            }

            private void Func1_2148EDC()
            {
                // skhere
            }

            private void Func1_2148ECC()
            {
                _field30++;
            }

            private void Func1_2148EB8()
            {
                // skhere
            }

            private void Func1_2148EA8()
            {
                // skhere
            }

            private void Func1_2148E98()
            {
                _nodeDataSelOn = 255;
            }

            private void Func1_2148E88()
            {
                _nodeDataSelOff = 255;
            }

            private void Func1_2148E74()
            {
                // skhere
            }

            private void Func1_2148E64()
            {
                // skhere
            }

            private void Func1_2148E54()
            {
                // skhere
            }

            private void Func1_2148DF8()
            {
                // skhere
            }

            private void Func1_2148DE8()
            {
                // skhere
            }

            private void Func1_2148D50()
            {
                // skhere
            }

            private void Func1_UnlockEchoHallForceField()
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ForceField)
                    {
                        continue;
                    }
                    var forceField = (ForceFieldEntity)entity;
                    if (forceField.Id == 19)
                    {
                        _scene.SendMessage(Message.Unlock, _player, forceField, 0, 0);
                        break;
                    }
                }
            }

            private void Func1_SetInvulnerable()
            {
                Flags3 |= AiFlags3.Invulnerable;
            }

            #endregion

            // skhere
            // todo: member names
            #region Funcs2

            private void Func2_213EA10(AiContext context)
            {
                // skhere
            }

            // process counterpart to Func4_21462DC
            private void Func2_213EA48(AiContext context)
            {
                if (context.FieldD == 28 && _player.IsAltForm && context.Field4 != 37)
                {
                    CheckUnmorph();
                    if (context.Field4 == 33)
                    {
                        Field118++;
                    }
                }
                else if (context.FieldD == 29 && !_player.IsAltForm && context.Field4 != 37)
                {
                    if (_touchButtons.Morph.FramesUp > 10 * 2) // todo: FPS stuff
                    {
                        _touchButtons.Morph.IsDown = true;
                    }
                }
                Vector3 targetPos = Vector3.Zero;
                if (_player.IsAltForm || context.FieldA == 31)
                {
                    if (_player.Values.AltFormStrafe != 0 && context.FieldA == 31)
                    {
                        if (context.FieldB == 4 && Flags2.TestFlag(AiFlags2.Bit2))
                        {
                            Debug.Assert(_targetPlayer != null);
                            _targetPlayer.GetPosition(out targetPos);
                            targetPos = targetPos.AddY(_targetPlayer.IsAltForm
                                ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos)
                                : 0.5f);
                            Func2145C14(targetPos);
                        }
                        else if (context.FieldB == 5 && Flags2.TestFlag(AiFlags2.Bit3))
                        {
                            Debug.Assert(_halfturret1C != null);
                            _halfturret1C.GetPosition(out targetPos);
                            Func2145C14(targetPos);
                        }
                    }
                }
                else if (context.FieldA == 32)
                {
                    if (_player.Values.AltFormStrafe != 0 && context.FieldA == 31)
                    {
                        if (context.FieldB == 4 && Flags2.TestFlag(AiFlags2.Bit2))
                        {
                            Debug.Assert(_targetPlayer != null);
                            _targetPlayer.GetPosition(out targetPos);
                            targetPos = targetPos.AddY(_targetPlayer.IsAltForm
                                ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos)
                                : 0.5f);
                            _field1038 = targetPos - _player.CameraInfo.Position;
                            _field1038 = _field1038 != Vector3.Zero ? _field1038.Normalized() : _player.CameraInfo.Facing;
                        }
                        else if (context.FieldB == 5 && Flags2.TestFlag(AiFlags2.Bit3))
                        {
                            Debug.Assert(_halfturret1C != null);
                            _halfturret1C.GetPosition(out targetPos);
                            _field1038 = targetPos - _player.CameraInfo.Position;
                            _field1038 = _field1038 != Vector3.Zero ? _field1038.Normalized() : _player.CameraInfo.Facing;
                        }
                        else if (context.FieldB == 27)
                        {
                            _field1038 = _fieldB8 - _player.CameraInfo.Position;
                            _field1038 = _field1038 != Vector3.Zero ? _field1038.Normalized() : _player.CameraInfo.Facing;
                        }
                        Func21447E8();
                    }
                }
                else if (context.FieldC == 55)
                {
                    Func21433A0(_fieldB8);
                }
                else if (context.FieldC == 56)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        Func21436D8();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1))
                    {
                        Func214380C();
                    }
                }
                else if (context.FieldC == 57)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        Func2143658();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1))
                    {
                        Func214380C();
                    }
                }
                else if (context.FieldC == 59)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        Func21433E4();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1))
                    {
                        Func214380C();
                    }
                }
                else if (context.FieldC == 60)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit3))
                    {
                        Func2143470();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1))
                    {
                        Func214380C();
                    }
                }
                else if (context.FieldC == 61)
                {
                    Debug.Assert(_doorE8 != null);
                    _doorE8.GetPosition(out targetPos);
                    Func21433A0(targetPos);
                }
                else
                {
                    Func2145BA0();
                }
                if (context.Field4 == 37)
                {
                    if (context.FieldD == 29)
                    {
                        Func2140094(context);
                        if (_player.Values.AltFormStrafe != 0 && _buttonAimX == 0 && _buttonAimY == 0)
                        {
                            if (Flags2.TestFlag(AiFlags2.Bit2) && context.Field9 == 4)
                            {
                                // sktodo-ai: this is being repeated a lot, including the call to Func2145C14() in some cases
                                Debug.Assert(_targetPlayer != null);
                                _targetPlayer.GetPosition(out targetPos);
                                targetPos = targetPos.AddY(_targetPlayer.IsAltForm
                                    ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos)
                                    : 0.5f);
                                Func2145C14(targetPos);
                            }
                            else
                            {
                                Debug.Assert(_node40 != null);
                                Func2145C14(_node40.Position);
                            }
                        }
                    }
                    else if (context.Field5 != 58 || _player.IsAltForm)
                    {
                        Func2140094(context);
                    }
                    else
                    {
                        Func214003C(context);
                    }
                    if (context.Field6 == 52 && !_player.IsAltForm && !_player.IsMorphing
                        && !_player.Flags1.TestFlag(PlayerFlags1.UsedJump) && _buttons.L.FramesUp > 5 * 2) // todo: FPS stuff
                    {
                        AiPlayerAggro? aggro = AggroFunc214847C(4, 7, 1, null, null);
                        if (aggro != null && aggro.Staleness < 30 * 2) // sktodo-ai: FPS stuff? review this and Field6
                        {
                            Debug.Assert(_node40 != null);
                            Vector3 toNode = _node40.Position - _player.Position;
                            if (toNode.LengthSquared > 3 * 3)
                            {
                                _buttons.L.IsDown = true;
                            }
                        }
                    }
                }
                else if (context.Field4 == 33
                    && (context.Field9 != 4 || Flags2.TestFlag(AiFlags2.Bit2))
                    && (context.Field9 != 5 || Flags2.TestFlag(AiFlags2.Bit3)))
                {
                    Vector3? position = null;
                    if (context.Field9 == 4)
                    {
                        Debug.Assert(_targetPlayer != null);
                        position = _targetPlayer.Position;
                    }
                    else if (context.Field9 == 5)
                    {
                        Debug.Assert(_halfturret1C != null);
                        position = _halfturret1C.Position;
                    }
                    else if (context.Field9 == 6 && Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        Debug.Assert(_itemC8 != null);
                        position = targetPos = _itemC8.Position.AddY(-0.5f);
                    }
                    else if (context.Field9 == 12 || context.Field9 == 13 || context.Field9 == 39)
                    {
                        Debug.Assert(_node40 != null);
                        position = _node40.Position;
                    }
                    else if (context.Field9 == 14)
                    {
                        Debug.Assert(_octolithFlagCC != null);
                        position = _octolithFlagCC.Position;
                    }
                    else if (context.Field9 == 15)
                    {
                        Debug.Assert(_octolithFlagCC != null);
                        position = _octolithFlagCC.BasePosition;
                    }
                    else if (context.Field9 == 16)
                    {
                        Debug.Assert(_flagBaseD0 != null);
                        position = _flagBaseD0.Position;
                    }
                    else if (context.Field9 == 17)
                    {
                        Debug.Assert(_octolithFlagD4 != null);
                        position = _octolithFlagD4.Position;
                    }
                    else if (context.Field9 == 18)
                    {
                        Debug.Assert(_octolithFlagD4 != null);
                        position = _octolithFlagD4.BasePosition;
                    }
                    else if (context.Field9 == 19)
                    {
                        Debug.Assert(_flagBaseD8 != null);
                        position = _flagBaseD8.Position;
                    }
                    else if (context.Field9 == 20)
                    {
                        Debug.Assert(_octolithFlagDC != null);
                        position = _octolithFlagDC.Position;
                    }
                    else if (context.Field9 == 21)
                    {
                        Debug.Assert(_octolithFlagDC != null);
                        position = _octolithFlagDC.BasePosition;
                    }
                    else if (context.Field9 == 22)
                    {
                        Debug.Assert(_flagBaseE0 != null);
                        position = _flagBaseE0.Position;
                    }
                    else if (context.Field9 == 23 && Flags2.TestFlag(AiFlags2.Bit5))
                    {
                        Debug.Assert(_defenseE4 != null);
                        position = _defenseE4.Position;
                    }
                    else if (context.Field9 == 35)
                    {
                        position = targetPos = _player.Position.AddZ(1);
                    }
                    else if (context.Field9 == 36)
                    {
                        position = targetPos = _player.Position.AddZ(-1);
                    }
                    if (position.HasValue)
                    {
                        if (_player.IsAltForm)
                        {
                            if (_player.Values.AltFormStrafe != 0)
                            {
                                Func2145C14(targetPos);
                                Func2142AE8(position.Value);
                            }
                            else if (context.Field5 != 48 || _player.Hunter != Hunter.Samus)
                            {
                                Func21418D8(position.Value);
                            }
                            else
                            {
                                Func2141840(position.Value);
                            }
                        }
                        else
                        {
                            Func2140B18(context, position.Value);
                            if (context.FieldA != 0 || context.FieldC == 56 || context.FieldC == 57 || context.FieldC == 59
                                || context.FieldC == 60 || context.FieldC == 61 || context.FieldC == 55)
                            {
                                Func2142AE8(position.Value);
                            }
                            else
                            {
                                Func2142ABC(position.Value);
                            }
                            if (!_player.IsMorphing && !_player.IsUnmorphing)
                            {
                                if (_player._horizColTimer > 10 * 2 && _player.Flags1.TestFlag(PlayerFlags1.Grounded)) // todo: FPS stuff
                                {
                                    PressL();
                                }
                                if (context.Field9 == 6 || context.Field9 == 14 || context.Field9 == 17 || context.Field9 == 20)
                                {
                                    Vector3 toPos = position.Value - _player.Position;
                                    toPos = toPos.AddY(-Fixed.ToFloat(_player.Values.MaxPickupHeight));
                                    if (context.Field9 != 6)
                                    {
                                        toPos = toPos.AddY(-1.25f);
                                    }
                                    if (toPos.Y > -0.5f && toPos.WithY(0).LengthSquared < 0.5f * 0.5f)
                                    {
                                        PressL();
                                    }
                                }
                            }
                        }
                    }
                }
                else if (context.Field4 == 34 && context.FieldD == 29)
                {
                    if (!_player.IsAltForm)
                    {
                        PressButton(_touchButtons.Morph, 10);
                    }
                    else if (_fieldAC.X != 0 || _fieldAC.Z != 0)
                    {
                        Func2141CD4(_fieldAC);
                    }
                }
                if (context.FieldE == 38 && !_player.IsAltForm)
                {
                    Func214380C();
                }
                if (context.FieldC == 62 && _player.IsAltForm)
                {
                    void ShootAndSetDelay()
                    {
                        if (_buttons.L.FramesUp > _field102E)
                        {
                            _buttons.L.IsDown = true;
                            _field102E = _field102C + Rng.GetRandomInt2(_field102C / 2); // note: FPS stuff when _field101C is set
                        }
                    }

                    if (context.FieldF == 64)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.Bombs) && _player._bombAmmo > 0 && _player._bombCooldown == 0)
                        {
                            ShootAndSetDelay();
                        }
                    }
                    else if (context.FieldF == 65)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.Bombs) && _player._bombCooldown == 0)
                        {
                            ShootAndSetDelay();
                        }
                    }
                    else if (context.FieldF == 66)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.SpireAltAttack)
                            && !_player.Flags2.TestFlag(PlayerFlags2.AltAttack))
                        {
                            ShootAndSetDelay();
                        }
                    }
                    else if (context.FieldF == 67)
                    {
                        if (_player._altAttackTime > 0 || _buttons.L.FramesUp > _field102E)
                        {
                            _buttons.L.IsDown = true;
                            _field102E = _field102C + Rng.GetRandomInt2(_field102C / 2); // note: FPS stuff when _field101C is set
                        }
                    }
                    else if (context.FieldF == 68)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.TraceAltAttack)
                            && _player._altAttackCooldown == 0 && Flags2.TestFlag(AiFlags2.Bit0))
                        {
                            ShootAndSetDelay();
                        }
                    }
                    else if (context.FieldF == 69)
                    {
                        if (Rng.GetRandomInt2(15 * _player.SyluxBombCount + 10) == 0 // sktodo-ai: FPS stuff if this is called repeatedly
                            && _player._abilities.TestFlag(AbilityFlags.Bombs)
                            && _player._bombAmmo > 0 && _player._bombCooldown == 0)
                        {
                            ShootAndSetDelay();
                        }
                    }
                    else if (context.FieldF == 70)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.WeavelAltAttack)
                            && _player._altAttackCooldown == 0 && Flags2.TestFlag(AiFlags2.Bit0))
                        {
                            ShootAndSetDelay();
                        }
                    }
                }
                else if (context.FieldC == 63)
                {
                    if (context.FieldF == 65)
                    {
                        if (_player._abilities.TestFlag(AbilityFlags.Bombs)
                            && _player._bombCooldown == 0 && _buttons.L.FramesUp > 60 * 2) // todo: FPS stuff
                        {
                            _buttons.L.IsDown = true;
                        }
                    }
                    else if (context.FieldF == 69 && _player.SyluxBombCount < 2)
                    {
                        Debug.Assert(_node3C != null);
                        if (IsNodeInRange(_node3C) && _player._abilities.TestFlag(AbilityFlags.Bombs)
                            && _player._bombAmmo > 0 && _player._bombCooldown == 0 && _buttons.L.FramesUp > 0)
                        {
                            _buttons.L.IsDown = true;
                        }
                    }
                }
                if (!_player.IsAltForm && !_player.IsMorphing && Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Debug.Assert(_targetPlayer != null);
                    if (_targetPlayer.Hunter == Hunter.Sylux && _targetPlayer.IsAltForm && Func2139C60(_targetPlayer))
                    {
                        PressL();
                    }
                }
                if (context.Field6 == 51 && !_player.IsAltForm && !_player.IsMorphing
                    && !_player.Flags1.TestFlag(PlayerFlags1.UsedJump))
                {
                    PressButton(_buttons.L, 5);
                }
                if (Flags2.TestFlag(AiFlags2.Bit21) && Rng.GetRandomInt2(10) == 0 // sktodo-ai: FPS stuff if this is called repeatedly
                    && !_player.IsAltForm && !_player.IsMorphing && !_player.Flags1.TestFlag(PlayerFlags1.UsedJump))
                {
                    PressButton(_buttons.L, 5);
                }
                if (context.Func24Id == 95 // maps to the "default" 2/4 function, but evidently a specific case
                    && !_player.IsAltForm && !_player.IsMorphing && !_player.Flags1.TestFlag(PlayerFlags1.UsedJump)
                    && Metadata.SlipSpeedFactors[_player._slipperiness] > 0 && _player._hSpeedMag > 0)
                {
                    PressButton(_buttons.L, 5);
                }
            }

            private void Func2_213DDCC(AiContext context)
            {
                // skhere
            }

            private void Func2_213DA88(AiContext context)
            {
                // skhere
            }

            private void Func2_213E148(AiContext context)
            {
                // skhere
            }

            private void Func2_213E9C8(AiContext context)
            {
                // skhere
            }

            private void Func2_213E984(AiContext context)
            {
                // skhere
            }

            private void Func2_213E934(AiContext context)
            {
                // skhere
            }

            private void Func2_213E904(AiContext context)
            {
                // skhere
            }

            private void Func2_213E684(AiContext context)
            {
                // skhere
            }

            private void Func2_213E3C4(AiContext context)
            {
                // skhere
            }

            private void Func2_213E31C(AiContext context)
            {
                // skhere
            }

            private void Func2_213E274(AiContext context)
            {
                // skhere
            }

            private void Func2_213E1CC(AiContext context)
            {
                // skhere
            }

            private void Func2_213D9B8(AiContext context)
            {
                // skhere
            }

            private void Func2_213D96C(AiContext context)
            {
                CheckUnmorph();
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Debug.Assert(_targetPlayer != null);
                    Func2145C14(_targetPlayer.Position);
                }
            }

            #endregion

            // skhere
            // todo: member names
            #region Funcs3

            private int Func3_213D87C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D83C(AiContext context, AiPersonalityData5 param)
            {
                Debug.Assert(_node3C != null);
                return IsNodeInRange(_node3C) && _node40 == _node3C ? 1 : 0;
            }

            private int Func3_213D814(AiContext context, AiPersonalityData5 param)
            {
                Debug.Assert(_node40 != null);
                return _node40.Position.Y - _player.Position.Y > param.Param1 ? 1 : 0;
            }

            private int Func3_213D7F0(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit0) ? 1 : 0;
            }

            private int Func3_213D800(AiContext context, AiPersonalityData5 param)
            {
                return Flags4.TestFlag(AiFlags4.Bit0) ? 1 : 0;
            }

            private int Func3_213D7E8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D7D0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D7B8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D7A0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D77C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D758(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D734(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D710(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D6D0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D6AC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D624(AiContext context, AiPersonalityData5 param)
            {
                if (!Flags2.TestFlag(AiFlags2.Bit2))
                {
                    return 0;
                }
                FindEntityRef(AiEntRefType.Type0);
                NodeData3? node0 = _entityRefs.Field0;
                Debug.Assert(node0 != null);
                FindEntityRef(AiEntRefType.Type2);
                NodeData3? node2 = _entityRefs.Field2;
                Debug.Assert(node2 != null);
                return node0 == node2 || IsNodeInRange(node2) ? 1 : 0;
            }

            private int Func3_213D608(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213D624(context, param) == 0 ? 1 : 0;
            }

            private int Func3_213D564(AiContext context, AiPersonalityData5 param)
            {
                if (!Flags2.TestFlag(AiFlags2.Bit2))
                {
                    return 0;
                }
                Debug.Assert(_targetPlayer != null);
                return Vector3.DistanceSquared(_targetPlayer.Position, _player.Position) < param.Param1 * param.Param1 ? 1 : 0;
            }

            private int Func3_213D540(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213D564(context, param) == 0 ? 1 : 0;
            }

            private int Func3_213D530(AiContext context, AiPersonalityData5 param)
            {
                return Func213842C() ? 1 : 0;
            }

            private int Func3_213D514(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213D530(context, param) == 0 ? 1 : 0;
            }

            private int Func3_213D4C0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D49C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D43C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D418(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D388(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D36C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D2C0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D2A4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D234(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D218(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D178(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D15C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D128(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D0F4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D0C4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D0A8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D078(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D05C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D044(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D028(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D010(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CFF4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CFDC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CFC0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CFA4(AiContext context, AiPersonalityData5 param)
            {
                return _slotHits[_player.SlotIndex] != 0 ? 1 : 0;
            }

            private int Func3_213CF0C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CEE8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CDB8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CF94(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CF7C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CDA4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CD74(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CD58(AiContext context, AiPersonalityData5 param)
            {
                return _player._health < _player._healthMax / 4 ? 1 : 0;
            }

            private int Func3_213CD34(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CD18(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CCF4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CCD8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CCBC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CCB0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CC94(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CBE4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CBC0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CBB0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CB8C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CADC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CAA8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CA84(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CA70(AiContext context, AiPersonalityData5 param)
            {
                return Field118 >= 151 ? 1 : 0; // sktodo-ai: FPS stuff?
            }

            private int Func3_213CA58(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CA2C(AiContext context, AiPersonalityData5 param)
            {
                return _executionTree[context.Depth + 1].CallCount > (param.Param1 * 2) ? 1 : 0; // todo: FPS stuff
            }

            private int Func3_213CA00(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213CA2C(context, param); // identical
            }

            private int Func3_213C9D4(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213CA2C(context, param); // identical
            }

            private int Func3_213C9C4(AiContext context, AiPersonalityData5 param)
            {
                return _slotHits[_player.SlotIndex];
            }

            private int Func3_213C89C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C88C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C764(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C75C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C698(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C64C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C600(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C52C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C48C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C470(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C334(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C310(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C0D0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C078(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C054(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BFFC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BFD8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BED8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BEBC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BEA0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BE48(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BE10(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BDF4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BD7C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BCE8(AiContext context, AiPersonalityData5 param)
            {
                return CanChargeWeapon() ? 1 : 0;
            }

            private int Func3_213BCC4(AiContext context, AiPersonalityData5 param)
            {
                return Func3_213BCE8(context, param) == 0 ? 1 : 0;
            }

            private int Func3_213BCB0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BC8C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BC70(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BC4C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BC0C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BBE8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BBA0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BB7C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BAF4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BAD0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BA68(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BA44(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BA28(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213BA04(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B99C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B978(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B8B0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B88C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B7A0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B77C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B690(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B5DC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B528(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B4E4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B4A0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B45C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B3F0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B3A0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B37C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B34C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B328(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B284(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B260(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B1F0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B1D8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B1C0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B1A8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B190(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B178(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B160(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B148(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B130(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B118(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B100(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B0E8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B0D0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B0B8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B0A0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B088(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B070(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B058(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B040(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B020(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213B000(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AFE0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AFC0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AFA0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF80(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF68(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF50(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF38(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF20(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AF08(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AEF0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AED8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AEC0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AEA8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE90(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE78(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE60(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE48(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE30(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AE14(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ADF8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ADC4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ADA0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AD88(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AD64(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ACE8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ACCC(AiContext context, AiPersonalityData5 param)
            {
                return _player.Position.Y < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213ACA8(AiContext context, AiPersonalityData5 param)
            {
                // instead of checking the Y pos here, the game calls Func3_213ACA8() and tests for a failure
                return _player.Position.Y >= param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AC8C(AiContext context, AiPersonalityData5 param)
            {
                return _player.Position.X > param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AC70(AiContext context, AiPersonalityData5 param)
            {
                return _player.Position.X < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AC54(AiContext context, AiPersonalityData5 param)
            {
                return _player.Position.Z > param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AC38(AiContext context, AiPersonalityData5 param)
            {
                return _player.Position.Z < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AC04(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.Y < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213ABC0(AiContext context, AiPersonalityData5 param)
            {
                // instead of checking the Y pos here, the game calls Func3_213AC04() and tests for a failure
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.Y >= param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AB8C(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.X > param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AB58(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.X < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AB24(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.Z > param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AAF0(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                    && _targetPlayer.Position.Z < param.Param1 / 4096f ? 1 : 0;
            }

            private int Func3_213AA64(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AA20(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A9B8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A94C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A938(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A91C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A900(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A8DC(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A8A8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A884(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A868(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A844(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A828(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A804(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A798(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A72C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A714(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A698(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A688(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit14) ? 1 : 0;
            }

            private int Func3_213A660(AiContext context, AiPersonalityData5 param)
            {
                // todo: FPS stuff
                if (_scene.FrameCount > 0 && _scene.FrameCount % 2 == 0)
                {
                    return param.Param1 + (int)Rng.GetRandomInt2(param.Param2 - param.Param1);
                }
                return 0;
            }

            private int Func3_213A650(AiContext context, AiPersonalityData5 param)
            {
                return Flags2.TestFlag(AiFlags2.Bit13) ? 1 : 0;
            }

            #endregion

            // skhere
            // todo: member names
            #region Funcs4

            // init counterpart to Func2_213EA48
            private void Func4_21462DC(AiContext context)
            {
                Func214715C(context);
                if (context.FieldA == 31)
                {
                    Flags2 &= ~AiFlags2.Bit0;
                }
                Vector3 targetPos = Vector3.Zero;
                Vector3 halfturretPos = Vector3.Zero;
                if (context.Field9 == 4 || context.FieldB == 4 || context.Field9 == 41 || context.FieldC == 56
                    || context.FieldC == 57 || context.FieldC == 59 || context.Field5 == 58)
                {
                    // todo: field7 doesn't appear to be updated to anything other than 0
                    if (context.Field7 == 1)
                    {
                        Func2135510();
                    }
                    if (context.Field7 == 2)
                    {
                        Func21354B0();
                    }
                    if (Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null)
                    {
                        _targetPlayer.GetPosition(out targetPos);
                        targetPos = targetPos
                            .AddY(_targetPlayer.IsAltForm ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos) : 0.5f);
                    }
                }
                if (Flags2.TestFlag(AiFlags2.Bit3) && _halfturret1C != null
                    && (context.Field9 == 5 || context.FieldB == 5 || context.FieldC == 60))
                {
                    _halfturret1C.GetPosition(out halfturretPos);
                }
                if (context.Field9 == 6)
                {
                    if (context.Field8 == 7)
                    {
                        FindEntityRef(AiEntRefType.Type55);
                        UpdateSeekItem(_entityRefs.Field55);
                    }
                    else if (context.Field8 == 8)
                    {
                        FindEntityRef(AiEntRefType.Type56);
                        UpdateSeekItem(_entityRefs.Field56);
                    }
                    else if (context.Field8 == 9)
                    {
                        FindEntityRef(AiEntRefType.Type57);
                        UpdateSeekItem(_entityRefs.Field57);
                    }
                    else if (context.Field8 == 10)
                    {
                        FindEntityRef(AiEntRefType.Type58);
                        UpdateSeekItem(_entityRefs.Field58);
                    }
                    else if (context.Field8 == 11)
                    {
                        FindEntityRef(AiEntRefType.Type59);
                        UpdateSeekItem(_entityRefs.Field59);
                    }
                }
                if (context.Field9 == 23 && !Flags2.TestFlag(AiFlags2.Bit5))
                {
                    Func2135320();
                }
                if (context.FieldC == 61 && !Flags2.TestFlag(AiFlags2.Bit6))
                {
                    Func21355D8();
                }
                if (context.FieldA == 32)
                {
                    if (context.FieldB == 4 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        _field1038 = targetPos - _player.CameraInfo.Position;
                    }
                    else if (context.FieldB == 3 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        AiPlayerAggro? aggro = AggroFunc214847C(4, 7, 1, null, null);
                        if (aggro?.Player1 != null)
                        {
                            _field1038 = aggro.Player1.Position - _player.CameraInfo.Position;
                        }
                        else
                        {
                            _field1038 = _player.CameraInfo.Facing;
                        }
                    }
                    else if (context.FieldB == 5 && Flags2.TestFlag(AiFlags2.Bit3))
                    {
                        _field1038 = halfturretPos - _player.CameraInfo.Position;
                    }
                    else if (context.FieldB == 25)
                    {
                        float x = Rng.GetRandomInt2(4096) / 4096f - 0.5f;
                        float y = Rng.GetRandomInt2(4096) / 4096f - 0.5f;
                        float z = Rng.GetRandomInt2(4096) / 4096f - 0.5f;
                        _field1038 = new Vector3(x, y, z);
                    }
                    else if (context.FieldB == 26)
                    {
                        float x = Rng.GetRandomInt2(4096) / 4096f - 0.5f;
                        float z = Rng.GetRandomInt2(4096) / 4096f - 0.5f;
                        float length = MathF.Sqrt(x * x + z * z);
                        float y = Rng.GetRandomInt2(Fixed.ToInt(length)) / 4096f - length / 2;
                        _field1038 = new Vector3(x, y, z);
                    }
                    else if (context.FieldB == 27)
                    {
                        _field1038 = _fieldB8 - _player.CameraInfo.Position;
                    }
                    if (_field1038 != Vector3.Zero)
                    {
                        _field1038 = _field1038.Normalized();
                    }
                    else
                    {
                        _field1038 = _player.CameraInfo.Facing;
                    }
                }
                if (context.Field4 != 0)
                {
                    Field118 = 0;
                    _field78 = 0;
                    Flags2 &= ~AiFlags2.Bit15;
                    context.Field40 = 0;
                    context.Field44 = 0;
                    context.Field34 = _player.Position;
                }
                NodeData3? v25 = null;
                if (context.Field4 == 37 || context.Field9 == 39)
                {
                    bool node40Set = false;
                    if (Flags2.TestFlag(AiFlags2.Bit7) && context.Field4 == 37)
                    {
                        if (_node40 == _node48)
                        {
                            v25 = Func213A1A8();
                            if (v25 == _field4C[0])
                            {
                                _node44 = _node40;
                                _node40 = v25;
                                node40Set = true;
                            }
                        }
                        else if (!_player.Flags1.TestFlag(PlayerFlags1.Grounded))
                        {
                            _node40 = _field4C[0];
                            node40Set = true;
                        }
                    }
                    if (!node40Set)
                    {
                        FindEntityRef(AiEntRefType.Type1);
                        _node40 = _entityRefs.Field1;
                    }
                    Debug.Assert(_node40 != null);
                    if (_node40.NodeType != NodeType.AltForm || _player.Hunter == Hunter.Guardian)
                    {
                        context.Field18 = 1;
                    }
                    else
                    {
                        context.Field18 = 2;
                    }
                }
                if (context.Field4 == 33)
                {
                    if (context.Field9 == 12)
                    {
                        FindEntityRef(AiEntRefType.Type25);
                        _node3C = _node40 = _entityRefs.Field25;
                    }
                    else if (context.Field9 == 13)
                    {
                        FindEntityRef(AiEntRefType.Type24);
                        _node3C = _node40 = _entityRefs.Field25;
                    }
                    else if (context.Field9 == 39)
                    {
                        _node3C = _node40;
                    }
                    else if (context.Field9 != 4)
                    {
                        Vector3? position = null;
                        if (context.Field9 == 5)
                        {
                            Debug.Assert(_halfturret1C != null);
                            position = _halfturret1C.Position;
                        }
                        else if (context.Field9 == 6)
                        {
                            Debug.Assert(_itemC8 != null);
                            position = _itemC8.Position.AddY(-0.5f);
                        }
                        else if (context.Field9 == 14)
                        {
                            Debug.Assert(_octolithFlagCC != null);
                            position = _octolithFlagCC.Position;
                        }
                        else if (context.Field9 == 15)
                        {
                            Debug.Assert(_octolithFlagCC != null);
                            position = _octolithFlagCC.BasePosition;
                        }
                        else if (context.Field9 == 16)
                        {
                            Debug.Assert(_flagBaseD0 != null);
                            position = _flagBaseD0.Position;
                        }
                        else if (context.Field9 == 17)
                        {
                            Debug.Assert(_octolithFlagD4 != null);
                            position = _octolithFlagD4.Position;
                        }
                        else if (context.Field9 == 18)
                        {
                            Debug.Assert(_octolithFlagD4 != null);
                            position = _octolithFlagD4.BasePosition;
                        }
                        else if (context.Field9 == 19)
                        {
                            Debug.Assert(_flagBaseD8 != null);
                            position = _flagBaseD8.Position;
                        }
                        else if (context.Field9 == 20)
                        {
                            Debug.Assert(_octolithFlagDC != null);
                            position = _octolithFlagDC.Position;
                        }
                        else if (context.Field9 == 21)
                        {
                            Debug.Assert(_octolithFlagDC != null);
                            position = _octolithFlagDC.BasePosition;
                        }
                        else if (context.Field9 == 22)
                        {
                            Debug.Assert(_flagBaseE0 != null);
                            position = _flagBaseE0.Position;
                        }
                        else if (context.Field9 == 23 && Flags2.TestFlag(AiFlags2.Bit5))
                        {
                            Debug.Assert(_defenseE4 != null);
                            position = _defenseE4.Position;
                        }
                        if (position.HasValue)
                        {
                            NodeData3 node = FindClosestNonHazardNodeToPosition(position.Value);
                            if (node.NodeType == NodeType.AltForm && _player.Hunter != Hunter.Guardian
                                && context.FieldD != 29)
                            {
                                context.FieldD = 29;
                                context.FieldE = 0;
                            }
                        }
                    }
                }
                else if (context.Field4 == 34)
                {
                    if (context.Field9 == 4 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        Debug.Assert(_targetPlayer != null);
                        _fieldAC = (_targetPlayer.Position - _player.Position).WithY(0);
                    }
                    else if (context.Field9 == 5 && Flags2.TestFlag(AiFlags2.Bit3))
                    {
                        Debug.Assert(_halfturret1C != null);
                        _fieldAC = (_halfturret1C.Position - _player.Position).WithY(0);
                    }
                    if (_fieldAC.X != 0 || _fieldAC.Z != 0)
                    {
                        _fieldAC = _fieldAC.Normalized();
                    }
                }
                else if (context.Field4 == 37)
                {
                    context.Field20 = 0;
                    context.Field24 = 1;
                    context.Field14 = 0;
                    context.Field18 = 0;
                    context.Field1C = 0;
                    if (context.Field5 == 47)
                    {
                        context.Field2C = 1;
                    }
                    else if (context.Field5 == 48)
                    {
                        context.Field2C = 2;
                    }
                    else
                    {
                        context.Field2C = 0;
                    }
                    context.Field28 = context.FieldD == 29;
                    context.Field30 = context.FieldA == 0
                        && context.FieldC != 55 && context.FieldC != 56 && context.FieldC != 57
                        && context.FieldC != 59 && context.FieldC != 60 && context.FieldC != 61;
                    if (context.Field9 == 4 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type2);
                        _node3C = _entityRefs.Field2;
                        _queuedFindEntityAction = AiQueuedEnt.Type0;
                    }
                    else if (context.Field9 == 5 && Flags2.TestFlag(AiFlags2.Bit3))
                    {
                        FindEntityRef(AiEntRefType.Type3);
                        _node3C = _entityRefs.Field3;
                        _queuedFindEntityAction = AiQueuedEnt.Type8;
                    }
                    else if (context.Field9 == 6 && Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                        _queuedFindEntityAction = context.Field8 switch
                        {
                            7 => AiQueuedEnt.Type12,
                            8 => AiQueuedEnt.Type13,
                            9 => AiQueuedEnt.Type14,
                            10 => AiQueuedEnt.Type15,
                            11 => AiQueuedEnt.Type16,
                            _ => AiQueuedEnt.None
                        };
                    }
                    else if (context.Field9 == 12)
                    {
                        FindEntityRef(AiEntRefType.Type25);
                        _node3C = _entityRefs.Field25;
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else if (context.Field9 == 13)
                    {
                        FindEntityRef(AiEntRefType.Type24);
                        _node3C = _entityRefs.Field24;
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else if (context.Field9 == 14)
                    {
                        FindEntityRef(AiEntRefType.Type6);
                        _node3C = _entityRefs.Field6;
                        _queuedFindEntityAction = AiQueuedEnt.Type27;
                    }
                    else if (context.Field9 == 15)
                    {
                        FindEntityRef(AiEntRefType.Type7);
                        _node3C = _entityRefs.Field7;
                        _queuedFindEntityAction = AiQueuedEnt.Type28;
                    }
                    else if (context.Field9 == 16)
                    {
                        FindEntityRef(AiEntRefType.Type8);
                        _node3C = _entityRefs.Field8;
                        _queuedFindEntityAction = AiQueuedEnt.Type29;
                    }
                    else if (context.Field9 == 17)
                    {
                        FindEntityRef(AiEntRefType.Type9);
                        _node3C = _entityRefs.Field9;
                        _queuedFindEntityAction = AiQueuedEnt.Type30;
                    }
                    else if (context.Field9 == 18)
                    {
                        FindEntityRef(AiEntRefType.Type10);
                        _node3C = _entityRefs.Field10;
                        _queuedFindEntityAction = AiQueuedEnt.Type31;
                    }
                    else if (context.Field9 == 19)
                    {
                        FindEntityRef(AiEntRefType.Type11);
                        _node3C = _entityRefs.Field11;
                        _queuedFindEntityAction = AiQueuedEnt.Type32;
                    }
                    else if (context.Field9 == 20)
                    {
                        FindEntityRef(AiEntRefType.Type12);
                        _node3C = _entityRefs.Field12;
                        _queuedFindEntityAction = AiQueuedEnt.Type33;
                    }
                    else if (context.Field9 == 21)
                    {
                        FindEntityRef(AiEntRefType.Type13);
                        _node3C = _entityRefs.Field13;
                        _queuedFindEntityAction = AiQueuedEnt.Type34;
                    }
                    else if (context.Field9 == 22)
                    {
                        FindEntityRef(AiEntRefType.Type14);
                        _node3C = _entityRefs.Field14;
                        _queuedFindEntityAction = AiQueuedEnt.Type35;
                    }
                    else if (context.Field9 == 23)
                    {
                        FindEntityRef(AiEntRefType.Type15);
                        _node3C = _entityRefs.Field15;
                        _queuedFindEntityAction = AiQueuedEnt.Type36;
                    }
                    else if (context.Field9 == 24)
                    {
                        _node3C = GetRandomNavigationNode();
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else if (context.Field9 == 40)
                    {
                        _node3C = FindHighestNode();
                        _queuedFindEntityAction = AiQueuedEnt.Type9;
                    }
                    else if (context.Field9 == 41 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type17);
                        _node3C = _entityRefs.Field17;
                        _queuedFindEntityAction = AiQueuedEnt.Type3;
                    }
                    else if (context.Field9 == 42 && Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type18);
                        _node3C = _entityRefs.Field18;
                        _queuedFindEntityAction = AiQueuedEnt.Type4;
                    }
                    else if (context.Field9 == 44)
                    {
                        FindEntityRef(AiEntRefType.Type23);
                        _node3C = _entityRefs.Field23;
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else if (context.Field9 == 45)
                    {
                        FindEntityRef(AiEntRefType.Type22);
                        _node3C = _entityRefs.Field22;
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else if (context.Field9 == 46)
                    {
                        FindEntityRef(AiEntRefType.Type21);
                        _node3C = _entityRefs.Field21;
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    else
                    {
                        if (Flags2.TestFlag(AiFlags2.Bit1))
                        {
                            FindQueuedEntityRef();
                        }
                        else
                        {
                            _node3C = _node40;
                        }
                        _queuedFindEntityAction = AiQueuedEnt.None;
                    }
                    if (_node40 != null && IsNodeInRange(_node40))
                    {
                        if (v25 == null)
                        {
                            v25 = Func213A1A8();
                        }
                        bool v33 = false;
                        for (int i = 0; i < _node40.Count2; i++)
                        {
                            if (_node40.Values[_node40.Index2 + i] == v25.Id)
                            {
                                v33 = true;
                                break;
                            }
                        }
                        if (!v33)
                        {
                            _node48 = _node44 = _node40;
                            _field4C[0] = _node40 = v25;
                            if (_node40.NodeType != NodeType.AltForm || _player.Hunter == Hunter.Guardian)
                            {
                                context.Field18 = 1;
                            }
                            else
                            {
                                context.Field18 = 2;
                            }
                            Flags2 |= AiFlags2.Bit7;
                        }
                    }
                }
                if (context.Field5 == 47)
                {
                    _field90 = _player.Position;
                    _field9C = 0.5f;
                }
                if (context.Field6 == 51 && !_player.IsAltForm && !_player.IsMorphing
                    && !_player.Flags1.TestFlag(PlayerFlags1.UsedJump) && _buttons.L.FramesUp > 5 * 2) // todo: FPS stuff
                {
                    _buttons.L.IsDown = true;
                }
            }

            private void Func4_2145EB0(AiContext context)
            {
                // skhere
            }

            private void Func4_21462AC(AiContext context)
            {
                // skhere
            }

            private void Func4_2146284(AiContext context)
            {
                // skhere
            }

            private void Func4_21461EC(AiContext context)
            {
                // skhere
            }

            private void Func4_214612C(AiContext context)
            {
                // skhere
            }

            private void Func4_2145F78(AiContext context)
            {
                // skhere
            }

            private void Func4_2145F50(AiContext context)
            {
                // skhere
            }

            private void Func4_2145F28(AiContext context)
            {
                // skhere
            }

            private void Func4_2145F00(AiContext context)
            {
                // skhere
            }

            private void Func4_2145E54(AiContext context)
            {
                // skhere
            }

            private void Func4_2145E40(AiContext context)
            {
                Flags3 |= AiFlags3.Bit1;
            }

            private void Func4_SetDespawned(AiContext context)
            {
                Flags3 |= AiFlags3.Despawned;
            }

            #endregion

            // todo: member name
            private void Func21356C0(PlayerEntity? player)
            {
                if (Flags2.TestFlag(AiFlags2.Bit9))
                {
                    Flags2 &= ~AiFlags2.Bit2;
                }
                else
                {
                    Flags2 |= AiFlags2.Bit2;
                }
                if (player != _targetPlayer)
                {
                    _targetPlayer = player;
                    _entityRefs.Field2 = null;
                    _entityRefs.Field17 = null;
                    _entityRefs.Field18 = null;
                    _entityRefs.Field29 = null;
                }
            }

            // todo: member name
            private void Func2135624(NodeDefenseEntity? defense)
            {
                if (defense != null)
                {
                    Flags2 |= AiFlags2.Bit5;
                    if (_defenseE4 != defense)
                    {
                        _defenseE4 = defense;
                        _entityRefs.Field15 = null;
                    }
                }
            }

            // todo: member name
            private void Func2135608(DoorEntity? door)
            {
                Flags2 |= AiFlags2.Bit6;
                if (_doorE8 != door)
                {
                    _doorE8 = door;
                }
            }

            private void CheckUnmorph()
            {
                if (_player.IsAltForm && _touchButtons.Unmorph.FramesUp > 10 * 2) // todo: FPS stuff
                {
                    _touchButtons.Unmorph.IsDown = true;
                }
            }

            // todo: member name -- dword_214C75C, dword_214C750
            private static readonly IReadOnlyList<float> _dotValues = [255 / 256f, 3956 / 4096f, 3849 / 4096f];
            private static readonly IReadOnlyList<float> _aimValues = [5, 15, 20];

            // todo: member name -- Func2145C14() updates X, Func21447E8() updates X and Y
            private void Func2145C14(Vector3 position)
            {
                Vector3 vec1 = _player.IsAltForm
                    ? new Vector3(_player._field80, 0, _player._field84)
                    : new Vector3(_player._field70, 0, _player._field74);
                Vector3 vec2 = (position - _player.Position).WithY(0);
                if (vec2.X != 0 || vec2.Z != 0)
                {
                    vec2 = vec2.Normalized();
                }
                else
                {
                    vec2 = vec1;
                }
                float dot = Vector3.Dot(vec1, vec2);
                if (dot > 255 / 256f)
                {
                    Flags2 |= AiFlags2.Bit0;
                }
                else
                {
                    Flags2 &= ~AiFlags2.Bit0;
                }
                if (dot < 1)
                {
                    if (dot > _dotValues[_player.BotLevel])
                    {
                        _buttonAimX = MathHelper.RadiansToDegrees(MathF.Acos(dot));
                    }
                    else
                    {
                        _buttonAimX = _aimValues[_player.BotLevel];
                    }
                    if (Vector3.Cross(vec1, vec2).Y < 0)
                    {
                        _buttonAimX *= -1;
                    }
                }
            }

            // todo: member name -- Func2145C14() updates X, Func21447E8() updates X and Y
            private void Func21447E8()
            {
                var vec1 = new Vector3(_player.CameraInfo.Field48, 0, _player.CameraInfo.Field4C);
                Vector3 vec2 = _field1038.X != 0 || _field1038.Z != 0 ? _field1038.WithY(0).Normalized() : vec1;
                float dot = Vector3.Dot(vec1, vec2);
                float value = _aimValues[_player.BotLevel];
                if (dot < 1)
                {
                    if (dot > _dotValues[_player.BotLevel])
                    {
                        _buttonAimY = MathHelper.RadiansToDegrees(MathF.Acos(dot));
                    }
                    else
                    {
                        _buttonAimX = value;
                    }
                    if (Vector3.Cross(vec1, vec2).Y < 0)
                    {
                        _buttonAimX *= -1;
                    }
                }
                float angle1 = 90 - MathHelper.RadiansToDegrees(MathF.Acos(_player.CameraInfo.Facing.Y));
                float angle2 = 90 - MathHelper.RadiansToDegrees(MathF.Acos(_field1038.Y));
                float angleDiff = angle2 - angle1;
                _buttonAimY = Math.Clamp(angleDiff, -value, value);
            }

            // todo: member name
            private void Func21436D8()
            {
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Debug.Assert(_targetPlayer != null);
                    Func2144B88();
                    Vector3 vec = ExecuteVectorFunc(index: 0, clearY: false, normalize: false);
                    if (Flags2.TestFlag(AiFlags2.Bit8)
                        && Func213842C() && (IsPlayerVisible(_player, _targetPlayer) || _weapon1 == 7)
                        || vec.LengthSquared < 10)
                    {
                        Func2143A40();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) || _weapon1 <= 1)
                    {
                        Func214380C();
                    }
                }
                else if (Flags4.TestFlag(AiFlags4.Bit1))
                {
                    Func214380C();
                }
            }

            // todo: member name
            private void Func2144B88()
            {
                // update aim, accounting for distance, beam travel, accuracy, etc.
                if (!Flags2.TestFlag(AiFlags2.Bit2))
                {
                    return;
                }
                Debug.Assert(_targetPlayer != null);
                Vector3 toTarget = _targetPlayer.Position - _player.Position;
                float targetDist = toTarget.Length;
                // field1020 -- deviation
                // if we haven't set the deviation yet, or we have but the bot is facing away by more than 90 degrees:
                if (_field1020 == 0 || Vector3.Dot(toTarget, _player._facingVector) < 0)
                {
                    _targetPlayer.GetPosition(out Vector3 targetPos);
                    int prevField1020 = _field1020;
                    if (Flags2.TestFlag(AiFlags2.Bit21))
                    {
                        _field1020 = 0;
                    }
                    if (_player.BotLevel == 0)
                    {
                        _field1020 = 15;
                    }
                    else if (_player.BotLevel == 1)
                    {
                        _field1020 = 7;
                    }
                    else
                    {
                        _field1020 = 3;
                    }
                    // sktodo-ai: FPS stuff for field1020, based on usage
                    ushort disruptedTimer = (ushort)(_player._disruptedTimer / 2);
                    if (_field1020 < disruptedTimer)
                    {
                        _field1020 += (int)Rng.GetRandomInt2(disruptedTimer - _field1020);
                    }
                    int field1020Diff = _field1020 - prevField1020;
                    if (Flags4.TestFlag(AiFlags4.Bit3) && field1020Diff > 0 && _player.BotLevel > 0)
                    {
                        EquipInfo equip = _player.EquipInfo;
                        WeaponInfo weapon = equip.Weapon;
                        bool isCharged = false;
                        float chargePct = 0;
                        if (weapon.Flags.TestFlag(WeaponFlags.PartialCharge))
                        {
                            if (weapon.Flags.TestFlag(WeaponFlags.CanCharge)
                                && equip.ChargeLevel >= weapon.MinCharge * 2) // todo: FPS stuff
                            {
                                isCharged = true;
                                // todo: FPS stuff
                                chargePct = (equip.ChargeLevel - weapon.MinCharge * 2) / (float)(weapon.FullCharge * 2 - weapon.MinCharge * 2);
                            }
                        }
                        else if (equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            isCharged = true;
                            chargePct = 1;
                        }
                        Vector3 vec = (targetPos - _field1054) / field1020Diff;
                        float homing;
                        float speed;
                        if (isCharged)
                        {
                            homing = (weapon.MinChargeHoming
                                + ((weapon.ChargedHoming - weapon.MinChargeHoming) * chargePct)) / 4096f / 2; // todo: FPS stuff
                            speed = (weapon.MinChargeSpeed
                                + ((weapon.ChargedSpeed - weapon.MinChargeSpeed) * chargePct)) / 4096f / 2; // todo: FPS stuff
                        }
                        else
                        {
                            homing = weapon.UnchargedHoming / 4096f / 2; // todo: FPS stuff
                            speed = weapon.UnchargedSpeed / 4096f / 2; // todo: FPS stuff
                        }
                        if (homing > 0 || speed <= 0)
                        {
                            vec *= _field1020 / 2f;
                        }
                        else
                        {
                            Vector3 muzzleTarget = targetPos - _player._muzzlePos;
                            float muzzleDist = muzzleTarget.Length;
                            vec *= muzzleDist;
                            // the game checks the third speed decay value, but the result is the same as the second
                            ushort decay = weapon.SpeedDecayTimes[isCharged ? 1 : 0];
                            float finalSpeed;
                            if (decay == 0)
                            {
                                finalSpeed = speed;
                            }
                            else if (isCharged)
                            {
                                finalSpeed = (weapon.MinChargeFinalSpeed
                                    + ((weapon.ChargedFinalSpeed - weapon.MinChargeFinalSpeed) * chargePct)) / 4096f / 2; // todo: FPS stuff
                            }
                            else
                            {
                                finalSpeed = weapon.UnchargedFinalSpeed / 4096f / 2; // todo: FPS stuff
                            }
                            vec /= finalSpeed; // sktodo-ai: FPS stuff, by usage --> leading shots
                        }
                        _field1048 = targetPos + vec;
                    }
                    else
                    {
                        _field1048 = targetPos;
                    }
                    _field1054 = targetPos;
                    Flags4 |= AiFlags4.Bit3;
                    _field1048 = _field1048.AddY(_targetPlayer.IsAltForm ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos) : 0.5f);
                    // sktodo-ai: FPS stuff, by usage --> speed affecting camera
                    Vector3 speedDiff = _player.Speed - _targetPlayer.Speed;
                    var camVec = new Vector3(_player.CameraInfo.Field50, 0, _player.CameraInfo.Field54);
                    float dot1 = MathF.Abs(Vector3.Dot(speedDiff, camVec));
                    float dot2 = MathF.Abs(Vector3.Dot(speedDiff, _player.CameraInfo.UpVector));
                    float v52;
                    float v66;
                    if (_player.BotLevel == 0)
                    {
                        v52 = (dot1 * 5) + 0.25f;
                        v66 = (dot2 * 5) + 0.25f;
                        if (Flags4.TestFlag(AiFlags4.Bit2))
                        {
                            v52 += targetDist / 2;
                            v66 += targetDist / 2;
                        }
                    }
                    else if (_player.BotLevel == 1)
                    {
                        v52 = (dot1 * 2) + 0.1f;
                        v66 = (dot2 * 2) + 0.1f;
                        if (Flags4.TestFlag(AiFlags4.Bit2))
                        {
                            v52 += targetDist / 9;
                            v66 += targetDist / 9;
                        }
                    }
                    else
                    {
                        v52 = (dot1 * 0.2f) + 0.01f;
                        v66 = (dot2 * 0.2f) + 0.01f;
                        if (Flags4.TestFlag(AiFlags4.Bit2))
                        {
                            v52 += targetDist / 50;
                            v66 += targetDist / 50;
                        }
                    }
                    if (_player._disruptedTimer > 0)
                    {
                        v52 *= 2;
                        v66 *= 2;
                    }
                    if (Flags2.TestFlag(AiFlags2.Bit21))
                    {
                        v52 /= 2;
                        v66 /= 2;
                    }
                    if (_player.ShockCoilTimer > 10 * 2) // todo: FPS stuff
                    {
                        v52 /= 2;
                        v66 /= 2;
                    }
                    int v61 = (int)(v52 * 4096);
                    int v62 = (int)(v66 * 4096);
                    float rand1 = (Rng.GetRandomInt2(v61 * 2) - v61) / 4096f;
                    float rand2 = (Rng.GetRandomInt2(v62 * 2) - v62) / 4096f;
                    if (_player._disruptedTimer == 0)
                    {
                        if (rand1 > 6)
                        {
                            rand1 = Rng.GetRandomInt2(8192) / 4096f + 4;
                        }
                        else if (rand1 < -6)
                        {
                            rand1 = -4 - Rng.GetRandomInt2(9182) / 4096f;
                        }
                        if (rand2 > 6)
                        {
                            rand2 = Rng.GetRandomInt2(8192) / 4096f + 4;
                        }
                        else if (rand2 < -6)
                        {
                            rand2 = -4 - Rng.GetRandomInt2(9182) / 4096f;
                        }
                    }
                    _field1048 += camVec * rand1 + _player.CameraInfo.UpVector * rand2;
                }
                Func2145738(_field1048);
                if (Flags4.TestFlag(AiFlags4.Bit2))
                {
                    float aimValue = _aimValues[_player.BotLevel] / 2;
                    _buttonAimX = Math.Clamp(_buttonAimX, -aimValue, aimValue);
                    _buttonAimY = Math.Clamp(_buttonAimY, -aimValue, aimValue);
                }
            }

            // todo: member name
            private void Func2145738(Vector3 position)
            {
                // update aim
                Vector3 toTarget = _player._aimPosition - _player._muzzlePos;
                float toTargetX = toTarget.X;
                float toTargetY = toTarget.Y;
                float toTargetZ = toTarget.Z;
                toTarget = toTarget.Normalized();
                float toTargetYNrm = toTarget.Y;
                toTarget = toTarget.WithY(0);
                toTarget = toTarget != Vector3.Zero ? toTarget.Normalized() : Vector3.UnitX;
                Vector3 toPos = position - _player._muzzlePos;
                float distToPosH = toPos.WithY(0).Length;
                float posY = toPos.Y;
                toPos = toPos.Normalized();
                float posYNrm = toPos.Y;
                toPos = toPos.WithY(0);
                toPos = toPos != Vector3.Zero ? toPos.Normalized() : toTarget;
                float dot = Vector3.Dot(toTarget, toPos);
                float value = _aimValues[_player.BotLevel];
                if (dot < 1)
                {
                    // sktodo-ai: add a common function for this
                    if (dot > _dotValues[_player.BotLevel])
                    {
                        _buttonAimX = MathHelper.RadiansToDegrees(MathF.Acos(dot));
                    }
                    else
                    {
                        _buttonAimX = value;
                    }
                    if (Vector3.Cross(toTarget, toPos).Y < 0)
                    {
                        _buttonAimX *= -1;
                    }
                }
                EquipInfo equip = _player.EquipInfo;
                WeaponInfo weapon = equip.Weapon;
                // sktodo-ai: add a common function for the beam stuff
                float chargePct = 0;
                if (weapon.Flags.TestFlag(WeaponFlags.CanCharge)
                    && equip.ChargeLevel >= weapon.MinCharge * 2) // todo: FPS stuff
                {
                    // todo: FPS stuff
                    chargePct = (equip.ChargeLevel - weapon.MinCharge * 2) / (float)(weapon.FullCharge * 2 - weapon.MinCharge * 2);
                }
                // todo: bugfix: the game's math is wrong here, using charge pct as a factor between uncharged and min charge values.
                // it should be a factor between min and max charge values (or uncharged should used directly for 0% charge).
                // going between uncharged and min charge will make the calculation wrong for any weapon where uncahrged, min, and max
                // aren't all identical.
                float speed = (weapon.UnchargedSpeed
                    + ((weapon.MinChargeSpeed - weapon.UnchargedSpeed) * chargePct)) / 4096f / 2; // todo: FPS stuff
                float gravity = (weapon.UnchargedGravity
                    + ((weapon.MinChargeGravity - weapon.UnchargedGravity) * chargePct)) / 4096f / 2; // todo: FPS stuff
                float div = distToPosH * distToPosH * gravity / (speed * speed);
                float angle1;
                float angle2;
                if (div != 0)
                {
                    float div2 = 0;
                    if (toTargetX != 0 || toTargetZ != 0)
                    {
                        float toTargetDistH = MathF.Sqrt(toTargetX * toTargetX + toTargetZ * toTargetZ);
                        div2 = toTargetY / toTargetDistH;
                    }
                    float v28 = distToPosH * distToPosH - 4 * (div / 2 * (div / 2) - posY);
                    if (v28 < 0)
                    {
                        return;
                    }
                    float sqrt = MathF.Sqrt(v28);
                    // note: the game chops the last bit off div, basically rounding it down to an even number of 1/4096ths
                    float div3 = (sqrt - distToPosH) / div;
                    angle1 = MathHelper.RadiansToDegrees(MathF.Atan(div2));
                    angle2 = MathHelper.RadiansToDegrees(MathF.Atan(div3));
                }
                else
                {
                    angle1 = 90 - MathHelper.RadiansToDegrees(MathF.Acos(toTargetYNrm));
                    angle2 = 90 - MathHelper.RadiansToDegrees(MathF.Acos(posYNrm));
                }
                float angleDiff = angle2 - angle1;
                _buttonAimY = Math.Clamp(angleDiff, -value, value);
                Flags2 &= ~AiFlags2.Bit8;
                if (dot >= 255 / 256f && angleDiff > -5 && angleDiff < 5)
                {
                    Flags2 |= AiFlags2.Bit8;
                }
            }

            // todo: member name
            private bool Func213842C()
            {
                if (!Flags2.TestFlag(AiFlags2.Bit16))
                {
                    Flags2 |= AiFlags2.Bit16;
                    Flags2 &= ~AiFlags2.Bit17;
                    if (Flags2.TestFlag(AiFlags2.Bit2) && AggroFunc214857C(6, 1, 2, null, _targetPlayer))
                    {
                        Flags2 |= AiFlags2.Bit17;
                    }
                }
                return Flags2.TestFlag(AiFlags2.Bit17);
            }

            // todo: member name
            private void Func2143A40()
            {
                EquipInfo equip = _player.EquipInfo;
                WeaponInfo weapon = _player.EquipWeapon;
                int shotDelay;
                if (_player.BotLevel == 0)
                {
                    shotDelay = 60;
                }
                else if (_player.BotLevel == 1)
                {
                    shotDelay = 15;
                }
                else
                {
                    shotDelay = 5;
                }
                if (Flags2.TestFlag(AiFlags2.Bit21))
                {
                    shotDelay /= 2;
                }
                shotDelay *= 2; // todo: FPS stuff
                BeamType beam = _weapon1 switch
                {
                    1 => BeamType.Missile,
                    2 => BeamType.VoltDriver,
                    _ => (BeamType)_weapon1
                };
                if (beam != BeamType.ShockCoil && !_player.AvailableWeapons[beam])
                {
                    return;
                }

                void SetRandomDelay()
                {
                    _shotDelay = weapon.ShotCooldown * 2 + (int)Rng.GetRandomInt2(shotDelay); // todo: FPS stuff
                }

                if (beam == BeamType.PowerBeam)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.PowerBeam.IsDown = true;
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) && CanChargeWeapon())
                    {
                        if (!_player.Flags2.TestFlag(PlayerFlags2.Shooting) && _buttons.R.FramesUp == 0
                            || equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            SetRandomDelay();
                        }
                        else
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_buttons.R.FramesDown > 0 && _buttons.R.FramesDown < _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                    }
                    else if (_buttons.R.FramesUp <= _shotDelay)
                    {
                        SetRandomDelay();
                    }
                    else
                    {
                        _buttons.R.IsDown = true;
                        _shotDelay = (int)Rng.GetRandomInt2(weapon.FullCharge * 2); // todo: FPS stuff
                    }
                }
                else if (beam == BeamType.Missile)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.Missile.IsDown = true;
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) && CanChargeWeapon())
                    {
                        if (!_player.Flags2.TestFlag(PlayerFlags2.Shooting) && _buttons.R.FramesUp <= _shotDelay
                            || equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            SetRandomDelay();
                        }
                        else
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_player.Flags1.TestFlag(PlayerFlags1.GunOpenAnimation) && _buttons.R.FramesUp > _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                        SetRandomDelay();
                    }
                }
                else if (beam == BeamType.VoltDriver)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.VoltDriver.IsDown = true;
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) && CanChargeWeapon())
                    {
                        if (!_player.Flags2.TestFlag(PlayerFlags2.Shooting) && _buttons.R.FramesUp <= _shotDelay
                            || equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            SetRandomDelay();
                        }
                        else
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_buttons.R.FramesUp > _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                        SetRandomDelay();
                    }
                }
                else if (beam == BeamType.Battlehammer)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.Battlehammer.IsDown = true;
                    }
                    else if (_player.Flags1.TestFlag(PlayerFlags1.ShotUncharged) || _buttons.R.FramesUp > 0)
                    {
                        _buttons.R.IsDown = true;
                    }
                }
                else if (beam == BeamType.Imperialist)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.Imperialist.IsDown = true;
                    }
                    else if (_buttons.R.FramesUp > _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                    }
                }
                else if (beam == BeamType.Judicator)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.Judicator.IsDown = true;
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) && CanChargeWeapon())
                    {
                        if (!_player.Flags2.TestFlag(PlayerFlags2.Shooting) && _buttons.R.FramesUp <= _shotDelay
                            || equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            if (Flags2.TestFlag(AiFlags2.Bit2))
                            {
                                Debug.Assert(_targetPlayer != null);
                                Vector3 toTarget = _targetPlayer.Position - _player.Position;
                                float distSqr = toTarget.LengthSquared;
                                if (distSqr > 3 * 3 && _player.BotLevel == 0
                                    || distSqr > 11 && _player.BotLevel == 1
                                    || distSqr > 13)
                                {
                                    SetRandomDelay();
                                }
                                else
                                {
                                    _buttons.R.IsDown = true;
                                }
                            }
                            else
                            {
                                SetRandomDelay();
                            }
                        }
                        else
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_buttons.R.FramesUp > _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                        SetRandomDelay();
                    }
                }
                else if (beam == BeamType.Magmaul)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.Magmaul.IsDown = true;
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) && CanChargeWeapon())
                    {
                        if (!_player.Flags2.TestFlag(PlayerFlags2.Shooting) && _buttons.R.FramesUp <= _shotDelay
                            || equip.ChargeLevel >= weapon.FullCharge * 2) // todo: FPS stuff
                        {
                            SetRandomDelay();
                        }
                        else
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_buttons.R.FramesUp > _shotDelay)
                    {
                        SetRandomDelay();
                        _buttons.R.IsDown = true;
                    }
                }
                else if (beam == BeamType.ShockCoil)
                {
                    Debug.Assert(_targetPlayer != null);
                    Vector3 toTarget = _targetPlayer.Position - _player.Position;
                    float distSqr = toTarget.LengthSquared;
                    if (GameState.SinglePlayer || distSqr <= 15 * 15 || !_player.AvailableWeapons[BeamType.PowerBeam])
                    {
                        if (!_player.AvailableWeapons[beam])
                        {
                            return;
                        }
                        if (_player.CurrentWeapon != beam)
                        {
                            _touchButtons.ShockCoil.IsDown = true;
                        }
                        else if ((_player.Flags1.TestFlag(PlayerFlags1.ShotUncharged) || _buttons.R.FramesUp > 0)
                            && (distSqr < 15 * 15 || distSqr < 16 * 16 && _player.Flags1.TestFlag(PlayerFlags1.ShotUncharged)))
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                    else if (_player.CurrentWeapon != BeamType.PowerBeam)
                    {
                        _touchButtons.PowerBeam.IsDown = true;
                    }
                    else if (_buttons.R.FramesDown > 0 && _buttons.R.FramesDown < _shotDelay)
                    {
                        _buttons.R.IsDown = true;
                    }
                    else if (_buttons.R.FramesUp <= _shotDelay)
                    {
                        SetRandomDelay();
                    }
                    else
                    {
                        _buttons.R.IsDown = true;
                        _shotDelay = (int)Rng.GetRandomInt2(weapon.FullCharge * 2); // todo: FPS stuff
                    }
                }
                else if (beam == BeamType.OmegaCannon)
                {
                    if (_player.CurrentWeapon != beam)
                    {
                        _touchButtons.OmegaCannon.IsDown = true;
                    }
                    else if (Flags2.TestFlag(AiFlags2.Bit2) && _buttons.R.FramesUp > 0)
                    {
                        Debug.Assert(_targetPlayer != null);
                        Vector3 toTarget = _targetPlayer.Position - _player.Position;
                        if (toTarget.LengthSquared > 10 * 10)
                        {
                            _buttons.R.IsDown = true;
                        }
                    }
                }
            }

            // todo: member name
            private void Func214380C()
            {
                // switch to _weapon1 if possible. if not possible or it's already equipped,
                // hold the fire button charge whatever we have equipped (provided it can charge).
                EquipInfo equip = _player.EquipInfo;
                WeaponInfo weapon = _player.EquipWeapon;
                if (!weapon.Flags.TestFlag(WeaponFlags.CanCharge))
                {
                    return;
                }
                BeamType beam = _weapon1 switch
                {
                    1 => BeamType.Missile,
                    2 => BeamType.VoltDriver,
                    _ => (BeamType)_weapon1
                };
                if (_player.CurrentWeapon != beam && _player._availableWeapons[beam])
                {
                    if (beam == BeamType.PowerBeam)
                    {
                        _touchButtons.PowerBeam.IsDown = true;
                    }
                    else if (beam == BeamType.Missile)
                    {
                        _touchButtons.Missile.IsDown = true;
                    }
                    else if (beam == BeamType.VoltDriver)
                    {
                        _touchButtons.VoltDriver.IsDown = true;
                    }
                    else if (beam == BeamType.Judicator)
                    {
                        _touchButtons.Judicator.IsDown = true;
                    }
                    else if (beam == BeamType.Magmaul)
                    {
                        _touchButtons.Magmaul.IsDown = true;
                    }
                    return;
                }
                // note: the game checks the potentially overriden bot weapon's charge cost (0 for 1P) here, but it normally
                // checks the original weapon metadata. 1P bots don't consume ammo, so the check will always succeed for them either way.
                if (CanChargeWeapon() && (_buttons.R.FramesDown == 0 || equip.ChargeLevel > 0))
                {
                    _buttons.R.IsDown = true;
                }
            }

            // todo: member name
            private void Func2143658()
            {
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Func2144AE4();
                    if (Flags2.TestFlag(AiFlags2.Bit8))
                    {
                        Func2143A40();
                    }
                }
                else if (Flags4.TestFlag(AiFlags4.Bit1))
                {
                    Func214380C();
                }
            }

            // todo: member name
            private void Func2144AE4()
            {
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Debug.Assert(_targetPlayer != null);
                    _targetPlayer.GetPosition(out Vector3 targetPos);
                    _field1048 = targetPos.AddY(_targetPlayer.IsAltForm
                        ? Fixed.ToFloat(_targetPlayer.Values.AltColYPos)
                        : 0.5f);
                    Func2145738(_field1048);
                }
            }

            // todo: member name
            private void Func21433E4()
            {
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Func2144B88();
                    Vector3 vec = ExecuteVectorFunc(index: 0, clearY: false, normalize: false);
                    if (Vector3.Dot(_player._facingVector, vec) > 0.866f)
                    {
                        Func2143A40();
                    }
                }
            }

            // todo: member name
            private void Func2143470()
            {
                if (Flags2.TestFlag(AiFlags2.Bit3))
                {
                    Debug.Assert(_halfturret1C != null);
                    Func2144964();
                    Vector3 toHalfturret = _halfturret1C.Position - _player.Position;
                    if (Flags2.TestFlag(AiFlags2.Bit8) || toHalfturret.LengthSquared < 10)
                    {
                        Func2143A40();
                    }
                    else if (Flags4.TestFlag(AiFlags4.Bit1) || _weapon1 == 0 || _weapon1 == 1)
                    {
                        Func214380C();
                    }
                }
                else if (Flags4.TestFlag(AiFlags4.Bit1))
                {
                    Func214380C();
                }
            }

            // todo: member name
            private void Func2144964()
            {
                if (Flags2.TestFlag(AiFlags2.Bit3))
                {
                    Debug.Assert(_halfturret1C != null);
                    _halfturret1C.GetPosition(out Vector3 halfturretPos);
                    _field1048 = halfturretPos.AddY(0.5f);
                    Func2145738(_field1048);
                }
            }

            // todo: member name
            private void Func21433A0(Vector3 position)
            {
                Func2145738(position);
                if (Flags2.TestFlag(AiFlags2.Bit8))
                {
                    Func2143A40();
                }
            }

            // todo: member name
            private void Func2145BA0()
            {
                float facingY = _player._facingVector.Y;
                if (facingY != 0)
                {
                    float aimY = MathHelper.RadiansToDegrees(MathF.Acos(facingY)) - 90;
                    float value = _aimValues[_player.BotLevel];
                    _buttonAimY = Math.Clamp(aimY, -value, value);
                }
            }

            private void PressButton(AiButton button, int frames = 0)
            {
                if (button.FramesUp > frames * 2) // todo: FPS stuff
                {
                    button.IsDown = true;
                }
            }

            private void PressL(int frames = 0)
            {
                // todo?: could add bugfix for checking L instead of Magmaul -- not sure how big the impact is (might lead
                // to cases where L is treated as held instead of pressed, but only if Magmaul is also down that frame)
                if (_touchButtons.Magmaul.FramesUp > frames * 2) // todo: FPS stuff
                {
                    _buttons.L.IsDown = true;
                }
            }

            // todo: member name
            private void Func2140094(AiContext context)
            {
                void SetField118()
                {
                    if (_scene.RoomId == 106 // MP13 ACCELERATOR (Fuel Stack)
                        && _player.Position.X > -2 && _player.Position.X < 2
                        && _player.Position.Z > -2 && _player.Position.Z < 2
                        && _player.Speed.Y > 0 && _player.Position.Y - _node40.Position.Y > 1)
                    {
                        Field118 = 151; // sktodo-ai: FPS stuff?
                    }
                    else
                    {
                        Field118++; // sktodo-ai: FPS stuff?
                    }
                }

                while (Func2140584(context))
                {
                }
                Debug.Assert(_node40 != null);
                context.Field18 = _node40.NodeType == NodeType.AltForm && _player.Hunter != Hunter.Guardian ? 2 : 0;
                if (_player.IsAltForm)
                {
                    if (_player._deathaltTimer == 0 && (context.Field1C == 1 || !context.Field28 && context.Field18 != 2))
                    {
                        CheckUnmorph();
                    }
                    if (context.Field20 != 0 && (context.Field1C != 1 || _player._deathaltTimer != 0))
                    {
                        if (_player.Values.AltFormStrafe != 0)
                        {
                            if (context.Field30 && context.Field2C == 0)
                            {
                                Func2142A80();
                            }
                            else if (!context.Field30 && context.Field2C == 0)
                            {
                                Func2142AE8(_node40.Position);
                            }
                            else if (context.Field30 && context.Field2C == 1)
                            {
                                Func2141EA8();
                            }
                            else if (!context.Field30 && context.Field2C == 1)
                            {
                                Func214201C();
                            }
                        }
                        else if (context.Field2C == 1)
                        {
                            Func2140D5C();
                        }
                        else if (context.Field2C != 2 || Field118 != 0)
                        {
                            Func21418D8(_node40.Position);
                        }
                        else
                        {
                            Func214182C();
                        }
                        SetField118();
                    }
                }
                else
                {
                    if (context.Field1C != 1 && (context.Field28 || context.Field18 == 2))
                    {
                        PressButton(_touchButtons.Morph, 10);
                    }
                    if (context.Field20 == 2)
                    {
                        PressL();
                    }
                    if (context.Field20 != 0)
                    {
                        if (Flags2.TestFlag(AiFlags2.Bit21) || _player.Flags1.TestFlag(PlayerFlags1.Grounded)
                            || (_player.Position - _node40.Position).WithY(0).LengthSquared > _node40.MaxDistance * _node40.MaxDistance)
                        {
                            if (context.Field30 && ((context.Field2C = context.Field2C) == 0 || context.Field2C == 2 || context.Field20 == 2))
                            {
                                Func2142A80();
                            }
                            else if (!context.Field30 && ((context.Field2C = context.Field2C) == 0 || context.Field2C == 2 || context.Field20 == 2))
                            {
                                Func2142AE8(_node40.Position);
                            }
                            else if (context.Field30 && context.Field2C == 1)
                            {
                                Func2141EA8();
                            }
                            else if (!context.Field30 && context.Field2C == 1)
                            {
                                Func214201C();
                            }
                        }
                        SetField118();
                        Func2140B18(context, _node40.Position);
                    }
                }
            }

            // todo: member name
            private bool Func2140584(AiContext context)
            {
                Debug.Assert(_node40 != null);
                if (context.Field24 == 0)
                {
                    if (IsNodeInRange(_node40))
                    {
                        Field118 = 0;
                        _field78 = 0;
                        Flags2 &= ~AiFlags2.Bit15;
                        context.Field40 = 0;
                        context.Field44 = 0;
                        context.Field34 = _player.Position;
                        FindQueuedEntityRef();
                        if (_node40 == _node3C)
                        {
                            context.Field20 = 0;
                            context.Field24 = 1;
                            Flags2 &= ~AiFlags2.Bit7;
                            return true;
                        }
                        NodeData3? node2 = null;
                        NodeData3 node1 = Func213A1A8();
                        (bool result, Vector3 bomb0Position, Vector3 bomb1Position) = Func2139E34(_node40, node1);
                        if (result)
                        {
                            node2 = Func2139F84(_node40, node1, bomb0Position, bomb1Position);
                            if (node2 != node1)
                            {
                                node1 = node2;
                            }
                        }
                        context.Field14 = 0;
                        context.Field1C = 0;
                        if (node1 == node2)
                        {
                            context.Field14 = 1;
                            context.Field1C = 1;
                            context.Field20 = 2;
                            context.Field24 = 2;
                        }
                        else
                        {
                            for (int i = 0; i < _node40.Count2; i++)
                            {
                                if (_node40.Values[_node40.Index2 + i] == node1.Id)
                                {
                                    context.Field14 = 1;
                                    context.Field1C = 1;
                                    context.Field20 = 2;
                                    context.Field24 = 2;
                                    break;
                                }
                            }
                        }
                        _node48 = _node44 = _node40;
                        _field4C[0] = _node40 = node1;
                        context.Field18 = _node40.NodeType == NodeType.AltForm && _player.Hunter != Hunter.Guardian ? 2 : 0;
                        Flags2 |= AiFlags2.Bit7;
                        return context.Field24 != 0;
                    }
                    if (_player._horizColTimer <= 10 * 2 // todo: FPS stuff
                        || _player.Flags1.TestFlag(PlayerFlags1.Grounded) && _player._horizColTimer <= 60 * 2 // todo: FPS stuff
                        || _player.IsAltForm || _player.IsMorphing)
                    {
                        if (_player._horizColTimer > 30 * 2 // todo: FPS stuff
                            && _player.Flags1.TestFlag(PlayerFlags1.Grounded) && _player.IsAltForm && !_player.IsUnmorphing
                            && Field118 > 30) // sktodo-AI: FPS stuff?
                        {
                            _field7A[_field78] = _node40.Id;
                            if (_field78 < 9)
                            {
                                _field78++;
                            }
                            FindEntityRef(AiEntRefType.Type1);
                            _node40 = _entityRefs.Field1;
                            Debug.Assert(_node40 != null);
                            context.Field18 = _node40.NodeType == NodeType.AltForm && _player.Hunter != Hunter.Guardian ? 2 : 0;
                        }
                        else if (Flags2.TestFlag(AiFlags2.Bit2) && _targetPlayer != null
                            && _targetPlayer.Hunter == Hunter.Sylux && _targetPlayer.IsAltForm)
                        {
                            if (Func2139C60(_targetPlayer))
                            {
                                context.Field14 = 1;
                                context.Field1C = 1;
                                context.Field20 = 2;
                                context.Field24 = 2;
                                Flags2 |= AiFlags2.Bit15;
                            }
                        }
                        else if (_node44 == null || !IsNodeInRange(_node44))
                        {
                            Vector3 toNode = _node40.Position - _player.Position;
                            if (toNode.LengthSquared < 0.5f * 0.5f
                                && _player.Flags1.TestFlag(PlayerFlags1.Grounded)
                                && Field118 > 30) // sktodo-AI: FPS stuff?
                            {
                                Field118 = 151; // sktodo-AI: FPS stuff?
                            }
                        }
                    }
                    else if (Flags2.TestFlag(AiFlags2.Bit15))
                    {
                        _field7A[_field78] = _node40.Id;
                        if (_field78 < 9)
                        {
                            _field78++;
                        }
                        FindEntityRef(AiEntRefType.Type1);
                        _node40 = _entityRefs.Field1;
                        Debug.Assert(_node40 != null);
                        context.Field18 = _node40.NodeType == NodeType.AltForm && _player.Hunter != Hunter.Guardian ? 2 : 0;
                    }
                    else
                    {
                        context.Field14 = 1;
                        context.Field1C = 1;
                        context.Field20 = 2;
                        context.Field24 = 2;
                        Flags2 |= AiFlags2.Bit15;
                    }
                    return false;
                }
                if (context.Field24 == 1)
                {
                    Field118 = 0;
                    _field78 = 0;
                    Flags2 &= ~AiFlags2.Bit15;
                    context.Field40 = 0;
                    context.Field44 = 0;
                    context.Field34 = _player.Position;
                    FindQueuedEntityRef();
                    Debug.Assert(_node3C != null);
                    if (IsNodeInRange(_node3C) && _node40 == _node3C)
                    {
                        return false;
                    }
                    context.Field14 = 0;
                    context.Field18 = 0;
                    context.Field1C = 0;
                    context.Field20 = 1;
                    context.Field24 = 0;
                    if (IsNodeInRange(_node40) || Flags2.TestFlag(AiFlags2.Bit7))
                    {
                        return true;
                    }
                    FindEntityRef(AiEntRefType.Type1);
                    _node40 = _entityRefs.Field1;
                    return false;
                }
                if (context.Field24 == 2 && _player.Flags1.TestFlag(PlayerFlags1.UsedJump))
                {
                    context.Field20 = 1;
                    context.Field24 = 0;
                    return true;
                }
                return false;
            }

            // todo: member name
            private (bool Result, Vector3 Bomb0Position, Vector3 Bomb1Position) Func2139E34(NodeData3 node1, NodeData3 node2)
            {
                // note: the game returns up to 10 sets of results, but only the first one is used by Func2139F84()
                float nodeMidpointY = (node1.Position.Y + node2.Position.Y) / 2;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var other = (PlayerEntity)entity;
                    if (other.Hunter != Hunter.Sylux || other.SyluxBombCount != 2)
                    {
                        continue;
                    }
                    BombEntity? bomb0 = other.SyluxBombs[0];
                    BombEntity? bomb1 = other.SyluxBombs[1];
                    Debug.Assert(bomb0 != null);
                    Debug.Assert(bomb1 != null);
                    float bombMidpointY = (bomb0.Position.Y + bomb1.Position.Y) / 2;
                    float yDiff = bombMidpointY - nodeMidpointY;
                    if (yDiff >= -3 && yDiff <= 3 && Func204CF74(node1.Position, node2.Position, bomb0.Position, bomb1.Position))
                    {
                        return (true, bomb0.Position, bomb1.Position);
                    }
                }
                return (false, Vector3.Zero, Vector3.Zero);
            }

            // todo: member name
            private bool Func204CF74(Vector3 a1, Vector3 a2, Vector3 a3, Vector3 a4)
            {
                float v18 = (a1.X - a3.X) * (a4.X - a3.X) + (a1.Z - a3.Z) * (a4.Z - a3.Z);
                float v19 = (a1.X - a4.X) * (a4.X - a3.X) + (a1.Z - a4.Z) * (a4.Z - a3.Z);
                float v22 = (a1.X - a3.X) * (a2.X - a1.X) + (a1.Z - a3.Z) * (a2.Z - a1.Z);
                float v23 = (a2.X - a3.X) * (a2.X - a1.X) + (a2.Z - a3.Z) * (a2.Z - a1.Z);
                return (v18 <= 0 && v19 >= 0 || v18 >= 0 && v19 <= 0) && (v22 <= 0 && v23 >= 0 || v22 >= 0 && v23 <= 0);
            }

            // todo: member name
            private NodeData3 Func2139F84(NodeData3 node1, NodeData3 node2, Vector3 bomb0Position, Vector3 bomb1Position)
            {
                var nodeList1 = new NodeData3[20];
                var nodeList2 = new NodeData3[20];
                int nodeCount1 = Func213A0A4(node1, nodeList1);
                int nodeCount2 = Func213A0A4(node2, nodeList2);
                var nodeList3 = new NodeData3[20];
                int nodeCount3 = 0;
                for (int i = 0; i < nodeCount1; i++)
                {
                    NodeData3 list1Node = nodeList1[i];
                    for (int j = 0; j < nodeCount2; j++)
                    {
                        NodeData3 list2Node = nodeList2[j];
                        if (list1Node == list2Node)
                        {
                            nodeList3[nodeCount3++] = list1Node;
                        }
                    }
                }
                for (int i = 0; i < nodeCount3; i++)
                {
                    NodeData3 list3Node = nodeList3[i];
                    if (!Func204CF74(node1.Position, list3Node.Position, bomb0Position, bomb1Position)
                        && !Func204CF74(list3Node.Position, node2.Position, bomb0Position, bomb1Position))
                    {
                        return list3Node;
                    }
                }
                return node2;
            }

            // todo: member name
            private bool Func2139C60(PlayerEntity target)
            {
                Debug.Assert(target.Hunter == Hunter.Sylux);
                if (target.SyluxBombCount != 2 && target.SyluxBombCount != 3)
                {
                    return false;
                }
                Vector3 bomb2Position;
                if (target.SyluxBombCount == 2)
                {
                    bomb2Position = target.Position;
                }
                else // if (target.SyluxBombCount == 3)
                {
                    BombEntity? bomb2 = target.SyluxBombs[2];
                    Debug.Assert(bomb2 != null);
                    bomb2Position = bomb2.Position;
                }
                BombEntity? bomb0 = target.SyluxBombs[0];
                BombEntity? bomb1 = target.SyluxBombs[1];
                Debug.Assert(bomb0 != null);
                Debug.Assert(bomb1 != null);
                Vector3 bomb01 = bomb1.Position - bomb0.Position;
                Vector3 bomb12 = bomb2Position - bomb1.Position;
                Vector3 bomb20 = bomb0.Position - bomb2Position;
                Vector3 cross = Vector3.Cross(bomb12, bomb01).Normalized();
                Vector3 bomb0Player = _player.Position - bomb0.Position;
                Vector3 bomb1Player = _player.Position - bomb1.Position;
                Vector3 bomb2Player = _player.Position - bomb2Position;
                float dot = Vector3.Dot(cross, bomb0Player);
                if (dot > -0.75f && dot < 0.75f
                    && Vector3.Dot(Vector3.Cross(bomb0Player, bomb01), cross) > 0
                    && Vector3.Dot(Vector3.Cross(bomb1Player, bomb12), cross) > 0
                    && Vector3.Dot(Vector3.Cross(bomb2Player, bomb20), cross) > 0)
                {
                    return true;
                }
                return false;
            }

            // todo: member name
            private void Func2142A80()
            {
                Debug.Assert(_node40 != null);
                Func2142AE8(_node40.Position);
                Func2145C14(_node40.Position);
            }

            // todo: member name
            private void Func2141EA8()
            {
                Debug.Assert(_node40 != null);
                Vector3 altVec = _player.IsAltForm
                    ? new Vector3(_player._field80, 0, _player._field84)
                    : new Vector3(_player._field70, 0, _player._field74);
                Vector3 toNode = _node40.Position - (_node44?.Position ?? _field90);
                toNode = toNode.X != 0 || toNode.Z != 0 ? toNode.WithY(0).Normalized() : altVec;
                float dot = Vector3.Dot(altVec, toNode);
                // sktodo-ai: common
                if (dot < 1)
                {
                    if (dot > _dotValues[_player.BotLevel])
                    {
                        _buttonAimX = MathHelper.RadiansToDegrees(MathF.Acos(dot));
                    }
                    else
                    {
                        _buttonAimX = _aimValues[_player.BotLevel];
                    }
                    if (Vector3.Cross(altVec, toNode).Y < 0)
                    {
                        _buttonAimX *= -1;
                    }
                }
                Func214201C();
            }

            // todo: member name
            private void Func2142AE8(Vector3 position)
            {
                Vector3 altVec = _player.IsAltForm
                    ? new Vector3(_player._field80, 0, _player._field84)
                    : new Vector3(_player._field70, 0, _player._field74);
                Vector3 toPos = position - _player.Position;
                toPos = toPos.X != 0 || toPos.Z != 0 ? toPos.WithY(0).Normalized() : altVec;
                Helper01(altVec, toPos, _buttons.X, _buttons.B, _buttons.Y, _buttons.A);
            }

            // todo: member name
            private void Func2142ABC(Vector3 position)
            {
                Func2142AE8(position);
                Func2145C14(position);
            }

            // todo: member name
            private void Func21418D8(Vector3 position)
            {
                if (CheckSpireClimbInput())
                {
                    return;
                }
                Vector3 toPos = position - _player.Position;
                toPos = toPos.X != 0 || toPos.Z != 0
                    ? toPos.WithY(0).Normalized()
                    : new Vector3(_player._altRollFbX, 0, _player._altRollFbZ);
                Func2141CD4(toPos);
            }

            // todo: member name
            private void Func2141CD4(Vector3 toPos)
            {
                var altVec = new Vector3(_player._altRollFbX, 0, _player._altRollFbZ);
                Helper01(altVec, toPos, _buttons.Up, _buttons.Down, _buttons.Left, _buttons.Right);
            }

            // todo: member name
            private void Func214201C()
            {
                Vector3 altVec = _player.IsAltForm
                    ? new Vector3(_player._field80, 0, _player._field84)
                    : new Vector3(_player._field70, 0, _player._field74);
                Helper02(altVec, _buttons.X, _buttons.B, _buttons.Y, _buttons.A);
            }

            // todo: member name
            private void Func2140D5C()
            {
                if (CheckSpireClimbInput())
                {
                    return;
                }
                var altVec = new Vector3(_player._altRollFbX, 0, _player._altRollFbZ);
                Helper02(altVec, _buttons.Up, _buttons.Down, _buttons.Left, _buttons.Right);
            }

            private bool CheckSpireClimbInput()
            {
                if (_player.Hunter == Hunter.Spire)
                {
                    if (_field116 > 0)
                    {
                        _field116--;
                    }
                    else if (_player.Flags2.TestFlag(PlayerFlags2.SpireClimbing))
                    {
                        // todo: FPS stuff
                        if (_buttons.Up.FramesUp < 10 * 2 || _buttons.Down.FramesUp < 10 * 2
                            || _buttons.Left.FramesUp < 10 * 2 || _buttons.Right.FramesUp < 10 * 2)
                        {
                            return true;
                        }
                        _field116 = 10 * 2; // todo: FPS stuff
                    }
                }
                return false;
            }

            // todo: member name
            private void Helper01(Vector3 altVec, Vector3 toPos,
                AiButton upButton, AiButton downButton, AiButton leftButton, AiButton rightButton)
            {
                float dot = Vector3.Dot(altVec, toPos);
                var cross = Vector3.Cross(altVec, toPos);
                if (dot > 0.866f)
                {
                    upButton.IsDown = true;
                }
                else if (dot > 0.5f)
                {
                    upButton.IsDown = true;
                    if (cross.Y >= 0)
                    {
                        leftButton.IsDown = true;
                    }
                    else
                    {
                        rightButton.IsDown = true;
                    }
                }
                else if (dot > -0.5f)
                {
                    if (cross.Y >= 0)
                    {
                        leftButton.IsDown = true;
                    }
                    else
                    {
                        rightButton.IsDown = true;
                    }
                }
                else if (dot >= -0.866f)
                {
                    downButton.IsDown = true;
                }
                else
                {
                    downButton.IsDown = true;
                    if (cross.Y >= 0)
                    {
                        leftButton.IsDown = true;
                    }
                    else
                    {
                        rightButton.IsDown = true;
                    }
                }
            }

            // todo: member name
            private void Helper02(Vector3 altVec, AiButton upButton, AiButton downButton, AiButton leftButton, AiButton rightButton)
            {
                Debug.Assert(_node40 != null);
                Vector3 toNode = _node40.Position - (_node44?.Position ?? _field90);
                toNode = toNode.X != 0 || toNode.Z != 0 ? toNode.WithY(0).Normalized() : altVec;
                Vector3 playerToNode = _node40.Position - _player.Position;
                playerToNode = playerToNode.X != 0 || playerToNode.Z != 0 ? toNode.WithY(0).Normalized() : Vector3.UnitX;
                if (Vector3.Dot(playerToNode, toNode) < 0)
                {
                    _node44 = null;
                    _field90 = _player.Position;
                    // same as playerToNode calc, but fall back to altVec instead of unit X
                    toNode = _node40.Position - _field90;
                    toNode = toNode.X != 0 || toNode.Z != 0 ? toNode.WithY(0).Normalized() : altVec;
                }
                float dot = Vector3.Dot(altVec, toNode);
                var cross1 = Vector3.Cross(altVec, toNode);
                var cross2 = Vector3.Cross(toNode, Vector3.UnitY);
                cross2 = cross2.X != 0 || cross2.Z != 0 ? cross2.WithY(0).Normalized() : toNode.WithY(0);
                Vector3 pos1Add = _node40.Position + cross2 * _node40.MaxDistance;
                Vector3 pos2Add;
                if (_node44 != null)
                {
                    pos2Add = _node44.Position + cross2 * _node44.MaxDistance;
                }
                else
                {
                    pos2Add = _field90 + cross2 * _field9C;
                }
                Vector3 pos3Add = _player.Position + cross2 / 2;
                Vector3 cross3 = Vector3.UnitY;
                Vector3 pos21 = (pos1Add - pos2Add).WithY(0);
                Vector3 pos23 = (pos3Add - pos2Add).WithY(0);
                if ((pos21.X != 0 || pos21.Z != 0) && (pos23.X != 0 || pos23.Z != 0))
                {
                    cross3 = Vector3.Cross(pos21, pos23);
                }
                pos1Add = _node40.Position + cross2 * -_node40.MaxDistance;
                if (_node44 != null)
                {
                    pos2Add = _node44.Position + cross2 * -_node44.MaxDistance;
                }
                else
                {
                    pos2Add = _field90 + cross2 * -_field9C;
                }
                pos3Add = _player.Position + cross2 / -2;
                Vector3 cross4 = Vector3.UnitY;
                pos21 = (pos1Add - pos2Add).WithY(0);
                pos23 = (pos3Add - pos2Add).WithY(0);
                if ((pos21.X != 0 || pos21.Z != 0) && (pos23.X != 0 || pos23.Z != 0))
                {
                    cross4 = Vector3.Cross(pos21, pos23);
                }
                if (cross4.Y > 0)
                {
                    Flags2 |= AiFlags2.Bit11;
                }
                else if (cross3.Y < 0)
                {
                    Flags2 &= ~AiFlags2.Bit11;
                }
                // this part is similar to Helper01(), but the flags checks lead to different buttons being pressed
                if (dot > 0.866f)
                {
                    upButton.IsDown = true;
                    if (Flags2.TestFlag(AiFlags2.Bit11))
                    {
                        rightButton.IsDown = true;
                    }
                    else
                    {
                        leftButton.IsDown = true;
                    }
                }
                else if (dot > 0.5f)
                {
                    if (cross1.Y >= 0)
                    {
                        if (Flags2.TestFlag(AiFlags2.Bit11))
                        {
                            upButton.IsDown = true;
                        }
                        else
                        {
                            leftButton.IsDown = true;
                        }
                    }
                    else if (Flags2.TestFlag(AiFlags2.Bit11))
                    {
                        rightButton.IsDown = true;
                    }
                    else
                    {
                        upButton.IsDown = true;
                    }
                }
                else if (dot > -0.5f)
                {
                    if (cross1.Y >= 0)
                    {
                        leftButton.IsDown = true;
                        if (Flags2.TestFlag(AiFlags2.Bit11))
                        {
                            upButton.IsDown = true;
                        }
                        else
                        {
                            downButton.IsDown = true;
                        }
                    }
                    else
                    {
                        rightButton.IsDown = true;
                        if (Flags2.TestFlag(AiFlags2.Bit11))
                        {
                            downButton.IsDown = true;
                        }
                        else
                        {
                            upButton.IsDown = true;
                        }
                    }
                }
                else if (dot >= -0.866f)
                {
                    downButton.IsDown = true;
                    if (Flags2.TestFlag(AiFlags2.Bit11))
                    {
                        leftButton.IsDown = true;
                    }
                    else
                    {
                        rightButton.IsDown = true;
                    }
                }
                else if (cross1.Y >= 0)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit11))
                    {
                        leftButton.IsDown = true;
                    }
                    else
                    {
                        downButton.IsDown = true;
                    }
                }
                else if (Flags2.TestFlag(AiFlags2.Bit11))
                {
                    downButton.IsDown = true;
                }
                else
                {
                    rightButton.IsDown = true;
                }
            }

            // todo: member name
            private void Func214182C()
            {
                Debug.Assert(_node40 != null);
                Func2141840(_node40.Position);
            }

            // todo: member name
            private void Func2141840(Vector3 position)
            {
                Debug.Assert(_node40 != null);
                Vector3 toPos = position - _player.Position;
                toPos = toPos.X != 0 || toPos.Z != 0
                    ? toPos.WithY(0).Normalized()
                    : new Vector3(_player._altRollFbX, 0, _player._altRollFbZ);
                Func2141A0C(toPos);
            }

            // todo: member name
            private void Func2141A0C(Vector3 toPos)
            {
                Debug.Assert(_node40 != null);
                var altVec = new Vector3(_player._altRollFbX, 0, _player._altRollFbZ);
                float dot = Vector3.Dot(altVec, toPos);
                var cross = Vector3.Cross(altVec, toPos);
                if (_player._abilities.TestFlag(AbilityFlags.Boost) && !_player.Flags1.TestFlag(PlayerFlags1.Boosting))
                {
                    if (_framesWithTouch == 0 && _framesWithoutTouch > _field1032)
                    {
                        _field1032 = (int)(_field1030 + Rng.GetRandomInt2(_field1030 / 2)); // note: FPS stuff when _field1030 is set
                        _hasTouch = true;
                        _touchAimX = (ushort)((int)(50 * cross.Y) + 128);
                        _touchAimY = (ushort)((int)(50 * dot) + 100);
                    }
                    else if (_framesWithTouch == 1)
                    {
                        _hasTouch = true;
                        _touchAimX = (ushort)(256 - _touchAimX);
                        _touchAimY = (ushort)(200 - _touchAimY);
                    }
                }
                Helper01(altVec, toPos, _buttons.Up, _buttons.Down, _buttons.Left, _buttons.Right);
            }

            // todo: member name
            private void Func2140B18(AiContext context, Vector3 position)
            {
                if (context.Field44 > 0)
                {
                    context.Field44--;
                    PressL();
                    if (!_player.IsAltForm || _player.Values.AltFormStrafe != 0)
                    {
                        _buttons.X.IsDown = false;
                        _buttons.B.IsDown = false;
                        _buttons.Y.IsDown = false;
                        _buttons.A.IsDown = false;
                    }
                    else
                    {
                        _buttons.Up.IsDown = false;
                        _buttons.Down.IsDown = false;
                        _buttons.Left.IsDown = false;
                        _buttons.Right.IsDown = false;
                    }
                }
                else
                {
                    if (_player.Speed.Y >= 0.0625f / 2 || _player.Speed.Y < -0.0625f / 2 // todo: FPS stuff
                        || _player.IsMorphing || _player.IsUnmorphing)
                    {
                        context.Field40 = 0;
                        context.Field34 = _player.Position;
                    }
                    else if (_player.Flags1.TestFlag(PlayerFlags1.CollidingLateral)
                        && !_player.Flags1.TestFlag(PlayerFlags1.Grounded))
                    {
                        context.Field40++;
                    }
                    if (context.Field40 >= 12 * 2) // todo: FPS stuff
                    {
                        Vector3 toPlayer = (_player.Position - context.Field34).WithY(0);
                        Vector3 toPos = (position - _player.Position).WithY(0).Normalized();
                        if (Vector3.Dot(toPlayer, toPos) < 1 / 256f)
                        {
                            context.Field44 = 15 * 2; // todo: FPS stuff
                        }
                        context.Field40 = 0;
                        context.Field34 = _player.Position;
                    }
                }
            }

            // todo: member name
            private void Func214003C(AiContext context)
            {
                if (Flags2.TestFlag(AiFlags2.Bit2))
                {
                    Func21436D8();
                }
                else if (Flags4.TestFlag(AiFlags4.Bit1))
                {
                    Func214380C();
                }
                Func2140094(context);
            }

            // todo: member name
            private void Func214715C(AiContext context)
            {
                context.Field4 = 0;
                context.Field5 = 0;
                context.Field6 = 0;
                context.Field7 = 0;
                context.Field8 = 0;
                context.Field9 = 0;
                context.FieldA = 0;
                context.FieldB = 0;
                context.FieldC = 0;
                context.FieldD = 28;
                context.FieldE = 0;
                context.FieldF = 0;
                switch (context.Func24Id)
                {
                case 2:
                    context.FieldA = 31;
                    context.FieldB = 4;
                    break;
                case 3:
                    context.FieldA = 31;
                    context.FieldB = 4;
                    context.FieldD = 29;
                    break;
                case 4:
                    context.FieldA = 32;
                    context.FieldB = 4;
                    break;
                case 5:
                    context.FieldA = 32;
                    context.FieldB = 4;
                    context.FieldE = 38;
                    break;
                case 6:
                    context.FieldA = 32;
                    context.FieldB = 3;
                    break;
                case 7:
                    context.FieldA = 32;
                    context.FieldB = 25;
                    break;
                case 8:
                    context.FieldA = 32;
                    context.FieldB = 26;
                    break;
                case 9:
                    context.Field4 = 37;
                    break;
                case 10:
                    context.Field4 = 37;
                    context.FieldE = 38;
                    break;
                case 11:
                    context.Field4 = 33;
                    context.Field9 = 39;
                    break;
                case 12:
                    context.Field4 = 33;
                    context.Field9 = 39;
                    context.FieldD = 29;
                    break;
                case 13:
                    context.Field4 = 33;
                    context.Field9 = 39;
                    context.Field6 = 51;
                    break;
                case 14:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    break;
                case 15:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    break;
                case 16:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    break;
                case 17:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldE = 38;
                    context.FieldA = 32;
                    context.FieldB = 4;
                    break;
                case 18:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldE = 38;
                    break;
                case 19:
                    context.FieldA = 32;
                    context.FieldB = 4;
                    context.Field4 = 37;
                    context.Field9 = 4;
                    break;
                case 20:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.Field5 = 47;
                    break;
                case 21:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    break;
                case 22:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    if (_player.BotLevel > 0)
                    {
                        context.Field5 = 48;
                    }
                    break;
                case 23:
                    context.Field4 = 37;
                    context.Field9 = 24;
                    context.FieldD = 29;
                    break;
                case 24:
                    context.Field4 = 37;
                    context.Field9 = 24;
                    context.FieldD = 29;
                    context.FieldC = 63;
                    context.FieldF = 69;
                    break;
                case 25:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.FieldD = 29;
                    break;
                case 26:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.Field5 = 47;
                    context.FieldD = 29;
                    break;
                case 27:
                    context.Field4 = 37;
                    context.Field9 = 13;
                    context.FieldD = 29;
                    break;
                case 28:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 66;
                    break;
                case 29:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.Field5 = 47;
                    context.FieldD = 29;
                    break;
                case 30:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 64;
                    break;
                case 31:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 64;
                    break;
                case 32:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    if (_player.BotLevel > 0)
                    {
                        context.Field5 = 48;
                    }
                    context.FieldF = 64;
                    break;
                case 33:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 34:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 63;
                    context.FieldF = 65;
                    break;
                case 35:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 36:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 63;
                    context.FieldF = 65;
                    break;
                case 37:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 66;
                    break;
                case 38:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 66;
                    break;
                case 39:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 67;
                    break;
                case 40:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 67;
                    break;
                case 41:
                    context.Field4 = 34;
                    context.Field9 = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 67;
                    break;
                case 42:
                    context.Field4 = 37;
                    context.Field5 = 47;
                    context.Field9 = 24;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 43:
                    context.Field4 = 37;
                    context.Field5 = 47;
                    context.Field9 = 24;
                    context.FieldD = 29;
                    context.FieldC = 63;
                    context.FieldF = 65;
                    break;
                case 44:
                    context.FieldA = 31;
                    context.FieldB = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 68;
                    break;
                case 45:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldA = 31;
                    context.FieldB = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 69;
                    break;
                case 48:
                    context.FieldA = 31;
                    context.FieldB = 4;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 70;
                    break;
                case 50:
                    context.Field4 = 37;
                    context.Field9 = 40;
                    break;
                case 51:
                    context.Field4 = 37;
                    context.Field9 = 24;
                    break;
                case 52:
                    context.Field4 = 37;
                    context.Field9 = 24;
                    context.FieldC = 56;
                    break;
                case 53:
                    context.Field4 = 37;
                    context.Field9 = 44;
                    break;
                case 54:
                    context.Field4 = 37;
                    context.Field9 = 44;
                    context.FieldC = 56;
                    break;
                case 55:
                    context.FieldA = 32;
                    context.FieldB = 27;
                    context.Field4 = 37;
                    context.Field9 = 44;
                    _fieldB8 = new Vector3(29, 15, 0);
                    break;
                case 56:
                    context.Field4 = 37;
                    context.Field9 = 45;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 57:
                    context.FieldA = 32;
                    context.FieldB = 4;
                    context.Field4 = 37;
                    context.Field9 = 46;
                    break;
                case 59:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    break;
                case 60:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.FieldC = 56;
                    break;
                case 61:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 7;
                    break;
                case 62:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.FieldC = 56;
                    context.Field8 = 7;
                    break;
                case 63:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 7;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 64:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 8;
                    break;
                case 65:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 9;
                    break;
                case 66:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 10;
                    break;
                case 67:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.Field8 = 11;
                    break;
                case 68:
                    context.Field4 = 37;
                    context.Field9 = 14;
                    break;
                case 69:
                    context.Field4 = 37;
                    context.Field9 = 14;
                    context.FieldC = 56;
                    break;
                case 70:
                    context.Field4 = 37;
                    context.Field9 = 15;
                    break;
                case 71:
                    context.Field4 = 37;
                    context.Field9 = 15;
                    context.FieldC = 56;
                    break;
                case 72:
                    context.Field4 = 37;
                    context.Field9 = 16;
                    break;
                case 73:
                    context.Field4 = 37;
                    context.Field9 = 16;
                    context.FieldC = 56;
                    break;
                case 74:
                    context.Field4 = 37;
                    context.Field9 = 16;
                    context.FieldC = 56;
                    context.Field6 = 52;
                    break;
                case 75:
                    context.Field4 = 37;
                    context.Field9 = 16;
                    context.FieldA = 32;
                    context.FieldB = 4;
                    break;
                case 76:
                    context.Field4 = 37;
                    context.Field9 = 23;
                    break;
                case 77:
                    context.Field4 = 37;
                    context.Field9 = 23;
                    context.FieldC = 56;
                    break;
                case 78:
                    context.Field4 = 33;
                    context.Field9 = 23;
                    break;
                case 82:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    break;
                case 83:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.FieldC = 56;
                    break;
                case 84:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 7;
                    break;
                case 85:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 7;
                    context.FieldD = 29;
                    context.FieldC = 62;
                    context.FieldF = 65;
                    break;
                case 86:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 8;
                    break;
                case 87:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 9;
                    break;
                case 88:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 10;
                    break;
                case 89:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.Field8 = 11;
                    break;
                case 90:
                    context.Field4 = 33;
                    context.Field9 = 20;
                    break;
                case 91:
                    context.Field4 = 33;
                    context.Field9 = 17;
                    break;
                case 92:
                    context.Field4 = 33;
                    context.Field9 = 15;
                    break;
                case 93:
                    context.Field4 = 33;
                    context.Field9 = 15;
                    context.FieldC = 56;
                    break;
                case 94:
                    context.Field4 = 33;
                    context.Field9 = 19;
                    break;
                case 95:
                    context.FieldA = 32;
                    context.FieldB = 26;
                    break;
                case 96:
                    context.FieldC = 55;
                    _fieldB8 = new Vector3(29, 15, 0);
                    break;
                case 97:
                    context.FieldC = 56;
                    break;
                case 98:
                    context.FieldC = 57;
                    break;
                case 101:
                    context.FieldC = 59;
                    break;
                case 103:
                    context.FieldC = 61;
                    break;
                case 106:
                    context.Field4 = 33;
                    context.Field9 = 4;
                    context.FieldC = 56;
                    break;
                case 108:
                    context.Field4 = 37;
                    context.Field9 = 4;
                    context.FieldC = 56;
                    break;
                case 109:
                    context.Field4 = 37;
                    context.Field9 = 42;
                    context.FieldC = 56;
                    break;
                case 110:
                    context.Field4 = 37;
                    context.Field9 = 42;
                    context.FieldC = 56;
                    context.Field5 = 47;
                    context.Field6 = 51;
                    break;
                case 112:
                    context.Field4 = 37;
                    context.Field5 = 47;
                    context.Field9 = 4;
                    context.FieldC = 56;
                    break;
                case 113:
                    context.Field4 = 37;
                    context.Field5 = 47;
                    context.Field9 = 5;
                    context.FieldC = 60;
                    break;
                case 115:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    break;
                case 116:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.FieldA = 32;
                    context.FieldB = 4;
                    break;
                case 117:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.FieldC = 56;
                    break;
                case 118:
                    context.Field4 = 37;
                    context.Field9 = 12;
                    context.FieldC = 56;
                    context.Field6 = 52;
                    break;
                case 119:
                    context.Field4 = 37;
                    context.Field9 = 6;
                    context.FieldD = 29;
                    break;
                case 120:
                    context.Field4 = 33;
                    context.Field9 = 6;
                    context.FieldD = 29;
                    break;
                case 121:
                    context.Field4 = 33;
                    context.Field9 = 35;
                    break;
                case 122:
                    context.Field4 = 33;
                    context.Field9 = 36;
                    break;
                default:
                    break;
                }
                if (_player.BotLevel == 0 && context.Field5 == 47)
                {
                    context.Field5 = 0;
                }
                if (context.Field4 != 37)
                {
                    Flags2 &= ~AiFlags2.Bit7;
                }
            }

            // todo: member name
            private void Func2135510()
            {
                FindEntityRef(AiEntRefType.Type26);
                Func21356C0(_entityRefs.Field26);
            }

            // todo: member name
            private void Func21354E0()
            {
                FindEntityRef(AiEntRefType.Type28);
                Func21356C0(_entityRefs.Field28);
            }

            // todo: member name
            private void Func2135540()
            {
                FindEntityRef(AiEntRefType.Type27);
                Func21356C0(_entityRefs.Field27);
            }

            // todo: member name
            private void Func21354B0()
            {
                FindEntityRef(AiEntRefType.Type30);
                Func21356C0(_entityRefs.Field30);
            }

            // todo: member name
            private void Func2135380()
            {
                FindEntityRef(AiEntRefType.Type31);
                Func21356C0(_entityRefs.Field31);
            }

            // todo: member name
            private void Func2135480()
            {
                FindEntityRef(AiEntRefType.Type32);
                Func21356C0(_entityRefs.Field32);
            }

            // todo: member name
            private void Func2135320()
            {
                FindEntityRef(AiEntRefType.Type74);
                Func2135624(_entityRefs.Field74);
            }

            // todo: member name
            private void Func21355D8()
            {
                FindEntityRef(AiEntRefType.Type77);
                Func2135608(_entityRefs.Field77);
            }

            private void UpdateSeekItem(ItemInstanceEntity? item)
            {
                if (item != null && item.DespawnTimer > 0)
                {
                    Flags2 |= AiFlags2.SeekItem;
                }
                else
                {
                    Flags2 &= ~AiFlags2.SeekItem;
                }
                if (item != _itemC8)
                {
                    _itemC8 = item;
                }
            }

            private void FindEntityRef(AiEntRefType type)
            {
                if (_entityRefs.IsPopulated((int)type))
                {
                    return;
                }
                // todo?: add behavior for seeking deathalt item/spawn?
                if (type == AiEntRefType.Type0)
                {
                    if (_player.ClosestNode != null)
                    {
                        _entityRefs.Field0 = _player.ClosestNode;
                    }
                    else
                    {
                        Vector3 position = _player.Position;
                        position = position.AddY(_player.IsAltForm
                            ? -(Fixed.ToFloat(_player.Values.AltColRadius) - Fixed.ToFloat(_player.Values.AltColYPos))
                            : -0.5f);
                        _entityRefs.Field0 = FindClosestNodeToPosition(position);
                        if (_nodeData.Simple)
                        {
                            _player.ClosestNode = _entityRefs.Field0;
                        }
                    }
                }
                else if (type == AiEntRefType.Type1)
                {
                    if (Flags2.TestFlag(AiFlags2.Bit10))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field1 = _entityRefs.Field0;
                        return;
                    }
                    Vector3 position = _player.Position;
                    position = position.AddY(_player.IsAltForm
                        ? -(Fixed.ToFloat(_player.Values.AltColRadius) - Fixed.ToFloat(_player.Values.AltColYPos))
                        : -0.5f);
                    _entityRefs.Field1 = Func2138D28(position);
                }
                else if (type == AiEntRefType.Type2)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field2 = _entityRefs.Field0;
                        return;
                    }
                    Debug.Assert(_targetPlayer != null);
                    if (_targetPlayer.ClosestNode != null)
                    {
                        _entityRefs.Field2 = _targetPlayer.ClosestNode;
                    }
                    else
                    {
                        Vector3 position = _targetPlayer.Position;
                        position = position.AddY(_targetPlayer.IsAltForm
                            ? -(Fixed.ToFloat(_targetPlayer.Values.AltColRadius) - Fixed.ToFloat(_targetPlayer.Values.AltColYPos))
                            : -0.5f);
                        _entityRefs.Field2 = FindClosestNonHazardNodeToPosition(position);
                        if (_nodeData.Simple)
                        {
                            _targetPlayer.ClosestNode = _entityRefs.Field2;
                        }
                    }
                }
                else if (type == AiEntRefType.Type3)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit3))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field3 = _entityRefs.Field0;
                        return;
                    }
                    Debug.Assert(_halfturret1C != null);
                    if (_halfturret1C.ClosestNode != null)
                    {
                        _entityRefs.Field3 = _halfturret1C.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field3 = FindClosestNonHazardNodeToPosition(_halfturret1C.Position);
                        if (_nodeData.Simple)
                        {
                            _halfturret1C.ClosestNode = _entityRefs.Field3;
                        }
                    }
                }
                else if (type == AiEntRefType.Type4)
                {
                    if (_itemSpawnC4 == null)
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field4 = _entityRefs.Field0;
                        return;
                    }
                    // todo?: bugfix? this pattern suggests that item spawns are supposed to have their node data
                    // set in scene setup, but they don't -- only jump pads, octoliths, bases, and defense nodes do
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field4 = _itemSpawnC4.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field4 = FindClosestNonHazardNodeToPosition(_itemSpawnC4.Position);
                    }
                }
                else if (type == AiEntRefType.Type5)
                {
                    if (!Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field5 = _entityRefs.Field0;
                        return;
                    }
                    Debug.Assert(_itemC8 != null);
                    if (_itemC8.ClosestNode != null)
                    {
                        _entityRefs.Field5 = _itemC8.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field5 = FindClosestNonHazardNodeToPosition(_itemC8.Position);
                        if (_nodeData.Simple)
                        {
                            _itemC8.ClosestNode = _entityRefs.Field5;
                        }
                    }
                }
                else if (type == AiEntRefType.Type6)
                {
                    if (_octolithFlagCC == _octolithFlagD4)
                    {
                        FindEntityRef(AiEntRefType.Type9);
                        _entityRefs.Field6 = _entityRefs.Field9;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type12);
                        _entityRefs.Field6 = _entityRefs.Field12;
                    }
                }
                else if (type == AiEntRefType.Type7)
                {
                    if (_octolithFlagCC == _octolithFlagD4)
                    {
                        FindEntityRef(AiEntRefType.Type10);
                        _entityRefs.Field7 = _entityRefs.Field10;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type13);
                        _entityRefs.Field7 = _entityRefs.Field13;
                    }
                }
                else if (type == AiEntRefType.Type8)
                {
                    if (_flagBaseD0 == _flagBaseD8)
                    {
                        FindEntityRef(AiEntRefType.Type11);
                        _entityRefs.Field8 = _entityRefs.Field11;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type14);
                        _entityRefs.Field8 = _entityRefs.Field14;
                    }
                }
                else if (type == AiEntRefType.Type9)
                {
                    Debug.Assert(_octolithFlagD4 != null);
                    if (_octolithFlagD4.ClosestNode != null)
                    {
                        _entityRefs.Field9 = _octolithFlagD4.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field9 = FindClosestNonHazardNodeToPosition(_octolithFlagD4.Position);
                        if (_nodeData.Simple)
                        {
                            _octolithFlagD4.ClosestNode = _entityRefs.Field9;
                        }
                    }
                }
                else if (type == AiEntRefType.Type10)
                {
                    Debug.Assert(_octolithFlagD4 != null);
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field10 = _octolithFlagD4.BaseClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field10 = FindClosestNonHazardNodeToPosition(_octolithFlagD4.BasePosition);
                    }
                }
                else if (type == AiEntRefType.Type11)
                {
                    Debug.Assert(_flagBaseD8 != null);
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field11 = _flagBaseD8.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field11 = FindClosestNonHazardNodeToPosition(_flagBaseD8.Position);
                    }
                }
                else if (type == AiEntRefType.Type12)
                {
                    Debug.Assert(_octolithFlagDC != null);
                    if (_octolithFlagDC.ClosestNode != null)
                    {
                        _entityRefs.Field12 = _octolithFlagDC.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field12 = FindClosestNonHazardNodeToPosition(_octolithFlagDC.Position);
                        if (_nodeData.Simple)
                        {
                            _octolithFlagDC.ClosestNode = _entityRefs.Field12;
                        }
                    }
                }
                else if (type == AiEntRefType.Type13)
                {
                    Debug.Assert(_octolithFlagDC != null);
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field13 = _octolithFlagDC.BaseClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field13 = FindClosestNonHazardNodeToPosition(_octolithFlagDC.BasePosition);
                    }
                }
                else if (type == AiEntRefType.Type14)
                {
                    Debug.Assert(_flagBaseE0 != null);
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field14 = _flagBaseE0.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field14 = FindClosestNonHazardNodeToPosition(_flagBaseE0.Position);
                    }
                }
                else if (type == AiEntRefType.Type15)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit5))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _entityRefs.Field15 = _entityRefs.Field0;
                        return;
                    }
                    Debug.Assert(_defenseE4 != null);
                    if (_nodeData.Simple)
                    {
                        _entityRefs.Field15 = _defenseE4.ClosestNode;
                    }
                    else
                    {
                        _entityRefs.Field15 = FindClosestNonHazardNodeToPosition(_defenseE4.Position);
                    }
                }
                else if (type == AiEntRefType.Type16)
                {
                    _entityRefs.Field16 = FindFarthestNodeFromPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type17)
                {
                    Debug.Assert(_targetPlayer != null);
                    _entityRefs.Field17 = FindFarthestNodeFromPosition(_targetPlayer.Position);
                }
                else if (type == AiEntRefType.Type18)
                {
                    Debug.Assert(_targetPlayer != null);
                    _entityRefs.Field18 = Func21396A0(_targetPlayer.Position);
                }
                else if (type == AiEntRefType.Type19)
                {
                    _entityRefs.Field19 = FindHighestNode();
                }
                else if (type == AiEntRefType.Type20)
                {
                    _entityRefs.Field20 = FindClosestVantageNodeToPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type21)
                {
                    _entityRefs.Field21 = FindClosestVantageNodeToPositionWithRange(_player.Position);
                }
                else if (type == AiEntRefType.Type22)
                {
                    _entityRefs.Field22 = FindFarthestVantageNodeFromPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type23)
                {
                    _entityRefs.Field23 = GetRandomAerialNode();
                }
                else if (type == AiEntRefType.Type24)
                {
                    _entityRefs.Field24 = GetRandomNavigationNodeByField4();
                }
                else if (type == AiEntRefType.Type25)
                {
                    _entityRefs.Field25 = FindClosestNavigationNodeByField4(_player.Position);
                }
                else if (type == AiEntRefType.Type26)
                {
                    _entityRefs.Field26 = FindClosestOpponentToPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type27)
                {
                    _entityRefs.Field27 = FindClosestOpponentToPosition(_player.Position, botsOnly: true);
                }
                else if (type == AiEntRefType.Type28)
                {
                    _entityRefs.Field28 = Func2138038(_player.Position);
                }
                else if (type == AiEntRefType.Type29)
                {
                    Debug.Assert(_targetPlayer != null);
                    // should be called when the target is dead, or it would find the same player
                    _entityRefs.Field29 = FindClosestOpponentToPosition(_targetPlayer.Position);
                }
                else if (type == AiEntRefType.Type30)
                {
                    _entityRefs.Field30 = Func2137E8C(_player.Position);
                }
                else if (type == AiEntRefType.Type31)
                {
                    _entityRefs.Field31 = Func21378C0(_player.Position);
                }
                else if (type == AiEntRefType.Type32)
                {
                    // this function doesn't have a default/fallback return, so null means nothing was found
                    PlayerEntity? result = Func2137AA4(_player.Position);
                    if (result != null)
                    {
                        _entityRefs.Field32 = result;
                    }
                }
                else if (type == AiEntRefType.Type33)
                {
                    _entityRefs.Field33 = Func2137D08(_player.Position);
                }
                else if (type == AiEntRefType.Type34)
                {
                    _entityRefs.Field34 = FindClosestPopulatedItemSpawnToPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type35)
                {
                    _entityRefs.Field35 = FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position,
                        ItemType.HealthMedium, ItemType.HealthSmall, ItemType.HealthBig);
                }
                else if (type == AiEntRefType.Type36)
                {
                    _entityRefs.Field36 = FindItemSpawnForMissiles();
                }
                else if (type == AiEntRefType.Type37)
                {
                    _entityRefs.Field37 = FindItemSpawnForMissiles();
                }
                else if (type == AiEntRefType.Type38)
                {
                    _entityRefs.Field38 = FindItemSpawnForWeapon(ItemType.VoltDriver, BeamType.VoltDriver);
                }
                else if (type == AiEntRefType.Type39)
                {
                    _entityRefs.Field39 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type40)
                {
                    _entityRefs.Field40 = FindItemSpawnForWeapon(ItemType.Battlehammer, BeamType.Battlehammer);
                }
                else if (type == AiEntRefType.Type41)
                {
                    _entityRefs.Field41 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type42)
                {
                    _entityRefs.Field42 = FindItemSpawnForWeapon(ItemType.Imperialist, BeamType.Imperialist);
                }
                else if (type == AiEntRefType.Type43)
                {
                    _entityRefs.Field43 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type44)
                {
                    _entityRefs.Field44 = FindItemSpawnForWeapon(ItemType.Judicator, BeamType.Judicator);
                }
                else if (type == AiEntRefType.Type45)
                {
                    _entityRefs.Field45 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type46)
                {
                    _entityRefs.Field46 = FindItemSpawnForWeapon(ItemType.Magmaul, BeamType.Magmaul);
                }
                else if (type == AiEntRefType.Type47)
                {
                    _entityRefs.Field47 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type48)
                {
                    _entityRefs.Field48 = FindItemSpawnForWeapon(ItemType.ShockCoil, BeamType.ShockCoil);
                }
                else if (type == AiEntRefType.Type49)
                {
                    _entityRefs.Field49 = FindItemSpawnForUa();
                }
                else if (type == AiEntRefType.Type50)
                {
                    _entityRefs.Field50 = FindItemSpawnForWeapon(ItemType.OmegaCannon, BeamType.OmegaCannon);
                }
                else if (type == AiEntRefType.Type51)
                {
                    // not implemented in-game, but this makes sense looking at 70/71
                    _entityRefs.Field51 = FindItemSpawnForWeapon(ItemType.OmegaCannon, BeamType.OmegaCannon);
                }
                else if (type == AiEntRefType.Type52)
                {
                    _entityRefs.Field52 = FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position, ItemType.DoubleDamage);
                }
                else if (type == AiEntRefType.Type53)
                {
                    _entityRefs.Field53 = FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position, ItemType.Cloak);
                }
                else if (type == AiEntRefType.Type54)
                {
                    _entityRefs.Field54 = FindClosestItemToPosition(_player.Position);
                }
                else if (type == AiEntRefType.Type55)
                {
                    _entityRefs.Field55 = FindClosestItemOfTypeToPosition(_player.Position,
                        ItemType.HealthMedium, ItemType.HealthSmall, ItemType.HealthBig);
                }
                else if (type == AiEntRefType.Type56)
                {
                    _entityRefs.Field56 = FindItemForMissiles();
                }
                else if (type == AiEntRefType.Type57)
                {
                    _entityRefs.Field57 = FindItemForMissiles();
                }
                else if (type == AiEntRefType.Type58)
                {
                    _entityRefs.Field58 = FindItemForWeapon(ItemType.VoltDriver, BeamType.VoltDriver);
                }
                else if (type == AiEntRefType.Type59)
                {
                    _entityRefs.Field59 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type60)
                {
                    _entityRefs.Field60 = FindItemForWeapon(ItemType.Battlehammer, BeamType.Battlehammer);
                }
                else if (type == AiEntRefType.Type61)
                {
                    _entityRefs.Field61 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type62)
                {
                    _entityRefs.Field62 = FindItemForWeapon(ItemType.Imperialist, BeamType.Imperialist);
                }
                else if (type == AiEntRefType.Type63)
                {
                    _entityRefs.Field63 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type64)
                {
                    _entityRefs.Field64 = FindItemForWeapon(ItemType.Judicator, BeamType.Judicator);
                }
                else if (type == AiEntRefType.Type65)
                {
                    _entityRefs.Field65 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type66)
                {
                    _entityRefs.Field66 = FindItemForWeapon(ItemType.Magmaul, BeamType.Magmaul);
                }
                else if (type == AiEntRefType.Type67)
                {
                    _entityRefs.Field67 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type68)
                {
                    _entityRefs.Field68 = FindItemForWeapon(ItemType.ShockCoil, BeamType.ShockCoil);
                }
                else if (type == AiEntRefType.Type69)
                {
                    _entityRefs.Field69 = FindItemForUa();
                }
                else if (type == AiEntRefType.Type70)
                {
                    _entityRefs.Field70 = FindItemForWeapon(ItemType.OmegaCannon, BeamType.OmegaCannon);
                }
                else if (type == AiEntRefType.Type71)
                {
                    _entityRefs.Field71 = FindItemForWeapon(ItemType.OmegaCannon, BeamType.OmegaCannon);
                }
                else if (type == AiEntRefType.Type72)
                {
                    _entityRefs.Field72 = FindClosestItemOfTypeToPosition(_player.Position, ItemType.DoubleDamage);
                }
                else if (type == AiEntRefType.Type73)
                {
                    _entityRefs.Field73 = FindClosestItemOfTypeToPosition(_player.Position, ItemType.Cloak);
                }
                else if (type == AiEntRefType.Type74)
                {
                    _entityRefs.Field74 = FindClosestNodeDefense(_player.Position);
                }
                else if (type == AiEntRefType.Type75)
                {
                    _entityRefs.Field75 = ChooseNodeDefenseToRetake();
                }
                else if (type == AiEntRefType.Type76)
                {
                    _entityRefs.Field76 = FindClosestFriendlyNodeDefense();
                }
                else if (type == AiEntRefType.Type77)
                {
                    _entityRefs.Field77 = FindClosestDoor(_player.Position);
                }
            }

            private void FindQueuedEntityRef()
            {
                if (_queuedFindEntityAction == AiQueuedEnt.Type0)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type2);
                        _node3C = _entityRefs.Field2;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type1)
                {
                    Func2135510();
                    FindEntityRef(AiEntRefType.Type2);
                    _node3C = _entityRefs.Field2;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type2)
                {
                    Func21354E0();
                    FindEntityRef(AiEntRefType.Type2);
                    _node3C = _entityRefs.Field2;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type3)
                {
                    Func2135510();
                    FindEntityRef(AiEntRefType.Type17);
                    _node3C = _entityRefs.Field17;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type4)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type18);
                        _node3C = _entityRefs.Field18;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type5)
                {
                    Func21354B0();
                    FindEntityRef(AiEntRefType.Type2);
                    _node3C = _entityRefs.Field2;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type6)
                {
                    Func2135380();
                    FindEntityRef(AiEntRefType.Type2);
                    _node3C = _entityRefs.Field2;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type7)
                {
                    Func21354B0();
                    FindEntityRef(AiEntRefType.Type17);
                    _node3C = _entityRefs.Field17;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type8)
                {
                    if (!Flags2.TestFlag(AiFlags2.Bit2))
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type3);
                        _node3C = _entityRefs.Field3;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type9)
                {
                    FindEntityRef(AiEntRefType.Type19);
                    _node3C = _entityRefs.Field19;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type10)
                {
                    FindEntityRef(AiEntRefType.Type54);
                    UpdateSeekItem(_entityRefs.Field54);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type11)
                {
                    FindEntityRef(AiEntRefType.Type34);
                    if (_itemSpawnC4 != _entityRefs.Field34)
                    {
                        _itemSpawnC4 = _entityRefs.Field34;
                        if (_itemSpawnC4?.Item != null)
                        {
                            FindEntityRef(AiEntRefType.Type4);
                            _node3C = _entityRefs.Field4;
                        }
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type12)
                {
                    FindEntityRef(AiEntRefType.Type55);
                    UpdateSeekItem(_entityRefs.Field55);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type13)
                {
                    FindEntityRef(AiEntRefType.Type56);
                    UpdateSeekItem(_entityRefs.Field56);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type14)
                {
                    FindEntityRef(AiEntRefType.Type57);
                    UpdateSeekItem(_entityRefs.Field57);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type15)
                {
                    FindEntityRef(AiEntRefType.Type58);
                    UpdateSeekItem(_entityRefs.Field58);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type16)
                {
                    FindEntityRef(AiEntRefType.Type59);
                    UpdateSeekItem(_entityRefs.Field59);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type17)
                {
                    FindEntityRef(AiEntRefType.Type60);
                    UpdateSeekItem(_entityRefs.Field60);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type18)
                {
                    FindEntityRef(AiEntRefType.Type61);
                    UpdateSeekItem(_entityRefs.Field61);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type19)
                {
                    // note: types 19 through 26 are never called in-game (among others), which is just as well since they
                    // incorrect pass an item spawn instead to UpdateSeekItem(). we just pass the spawner's item if there is one.
                    FindEntityRef(AiEntRefType.Type42);
                    UpdateSeekItem(_entityRefs.Field42?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type20)
                {
                    FindEntityRef(AiEntRefType.Type43);
                    UpdateSeekItem(_entityRefs.Field43?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type21)
                {
                    FindEntityRef(AiEntRefType.Type44);
                    UpdateSeekItem(_entityRefs.Field44?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type22)
                {
                    FindEntityRef(AiEntRefType.Type45);
                    UpdateSeekItem(_entityRefs.Field45?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type23)
                {
                    FindEntityRef(AiEntRefType.Type46);
                    UpdateSeekItem(_entityRefs.Field46?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type24)
                {
                    FindEntityRef(AiEntRefType.Type47);
                    UpdateSeekItem(_entityRefs.Field47?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type25)
                {
                    FindEntityRef(AiEntRefType.Type48);
                    UpdateSeekItem(_entityRefs.Field48?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type26)
                {
                    FindEntityRef(AiEntRefType.Type49);
                    UpdateSeekItem(_entityRefs.Field49?.Item);
                    if (Flags2.TestFlag(AiFlags2.SeekItem))
                    {
                        FindEntityRef(AiEntRefType.Type5);
                        _node3C = _entityRefs.Field5;
                    }
                    else
                    {
                        FindEntityRef(AiEntRefType.Type0);
                        _node3C = _entityRefs.Field0;
                    }
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type27 || _queuedFindEntityAction == AiQueuedEnt.Type28)
                {
                    FindEntityRef(AiEntRefType.Type6);
                    _node3C = _entityRefs.Field6;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type29)
                {
                    FindEntityRef(AiEntRefType.Type8);
                    _node3C = _entityRefs.Field8;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type30 || _queuedFindEntityAction == AiQueuedEnt.Type31)
                {
                    FindEntityRef(AiEntRefType.Type9);
                    _node3C = _entityRefs.Field9;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type32)
                {
                    FindEntityRef(AiEntRefType.Type11);
                    _node3C = _entityRefs.Field11;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type33 || _queuedFindEntityAction == AiQueuedEnt.Type34)
                {
                    FindEntityRef(AiEntRefType.Type12);
                    _node3C = _entityRefs.Field12;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type35)
                {
                    FindEntityRef(AiEntRefType.Type14);
                    _node3C = _entityRefs.Field14;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type36)
                {
                    FindEntityRef(AiEntRefType.Type15);
                    _node3C = _entityRefs.Field15;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type37)
                {
                    FindEntityRef(AiEntRefType.Type20);
                    _node3C = _entityRefs.Field20;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type38)
                {
                    FindEntityRef(AiEntRefType.Type21);
                    _node3C = _entityRefs.Field21;
                }
                else if (_queuedFindEntityAction == AiQueuedEnt.Type39)
                {
                    FindEntityRef(AiEntRefType.Type22);
                    _node3C = _entityRefs.Field22;
                }
            }

            private ItemSpawnEntity? FindItemSpawnForWeapon(ItemType itemType, BeamType beamType)
            {
                ItemType type2 = ItemType.None;
                if (Weapons.AffinityWeapons[(int)_player.Hunter] == beamType)
                {
                    type2 = ItemType.AffinityWeapon;
                }
                return FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position, itemType, type2);
            }

            private ItemInstanceEntity? FindItemForWeapon(ItemType itemType, BeamType beamType)
            {
                ItemType type2 = ItemType.None;
                if (Weapons.AffinityWeapons[(int)_player.Hunter] == beamType)
                {
                    type2 = ItemType.AffinityWeapon;
                }
                return FindClosestItemOfTypeToPosition(_player.Position, itemType, type2);
            }

            private ItemSpawnEntity? FindItemSpawnForUa()
            {
                return FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position, ItemType.UASmall, ItemType.UABig);
            }

            private ItemInstanceEntity? FindItemForUa()
            {
                return FindClosestItemOfTypeToPosition(_player.Position, ItemType.UASmall, ItemType.UABig);
            }

            private ItemSpawnEntity? FindItemSpawnForMissiles()
            {
                ItemType type3 = ItemType.None;
                if (Weapons.AffinityWeapons[(int)_player.Hunter] == BeamType.Missile)
                {
                    type3 = ItemType.AffinityWeapon;
                }
                return FindClosestPopulatedItemSpawnOfTypeToPosition(_player.Position,
                    ItemType.MissileSmall, ItemType.MissileBig, type3);
            }

            private ItemInstanceEntity? FindItemForMissiles()
            {
                ItemType type3 = ItemType.None;
                if (Weapons.AffinityWeapons[(int)_player.Hunter] == BeamType.Missile)
                {
                    type3 = ItemType.AffinityWeapon;
                }
                return FindClosestItemOfTypeToPosition(_player.Position,
                    ItemType.MissileSmall, ItemType.MissileBig, type3);
            }

            private NodeData3 FindClosestNodeToPosition(Vector3 position)
            {
                Debug.Assert(_nodeList.Count > 0);
                NodeData3 result = _nodeList[0];
                float minDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    if (dist < minDist)
                    {
                        result = node;
                        minDist = dist;
                    }
                }
                return result;
            }

            private NodeData3 FindFarthestNodeFromPosition(Vector3 position)
            {
                Debug.Assert(_nodeList.Count > 0);
                NodeData3 result = _nodeList[0];
                float maxDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    if (dist > maxDist)
                    {
                        result = node;
                        maxDist = dist;
                    }
                }
                return result;
            }

            // todo: member name
            private NodeData3 Func2138D28(Vector3 position)
            {
                _field4C[1] = null;
                int v4 = 0;
                float minDist = 100000;
                float[] distList = new float[10];
                for (int i = 0; i < _nodeList.Count; i++)
                {
                    int j = 0;
                    for (; j < _field78; j++)
                    {
                        if (_field7A[j] == i)
                        {
                            break;
                        }
                    }
                    if (j >= _field78)
                    {
                        NodeData3 node = _nodeList[i];
                        float dist = Vector3.DistanceSquared(node.Position, position);
                        if (dist < minDist)
                        {
                            int k = 0;
                            for (; k < v4; k++)
                            {
                                if (dist < distList[k])
                                {
                                    break;
                                }
                            }
                            for (int l = v4; l > k; l--)
                            {
                                _field4C[l + 1] = _field4C[l];
                                distList[l] = distList[l - 1];
                            }
                            if (k < 10)
                            {
                                _field4C[k + 1] = node;
                                distList[k] = dist;
                            }
                            minDist = distList[v4];
                            if (v4 < 9)
                            {
                                v4++;
                            }
                        }
                    }
                }
                NodeData3? result = _field4C[1];
                if (result != null)
                {
                    Flags2 |= AiFlags2.Bit10;
                    Func214B810(v4);
                    return result;
                }
                return FindClosestNonHazardNodeToPosition(position);
            }

            // todo: member name
            private void Func214B810(int v4)
            {
                Debug.Assert(_field4C != null);
                for (int i = 0; i < _globalField2; i++)
                {
                    AiGlobals obj = _globalObjs[i];
                    if (obj.Player == _player)
                    {
                        obj.Field4 = v4;
                        obj.NodeData = _field4C!;
                        obj.NodeDataIndex = 1;
                        return;
                    }
                }
                Debug.Assert(_globalField2 < _globalObjs.Length);
                AiGlobals nextObj = _globalObjs[_globalField2];
                nextObj.Player = _player;
                nextObj.Field4 = v4;
                nextObj.NodeData = _field4C!;
                nextObj.NodeDataIndex = 1;
                _globalField2++;
            }

            private NodeData3 FindClosestNonHazardNodeToPosition(Vector3 position)
            {
                Debug.Assert(_nodeList.Count > 0);
                NodeData3 result = _nodeList[0];
                float minDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    if (node.NodeType != NodeType.Hazard)
                    {
                        float dist = Vector3.DistanceSquared(node.Position, position);
                        if (dist < minDist || result.NodeType == NodeType.Hazard)
                        {
                            result = node;
                            minDist = dist;
                        }
                    }
                }
                return result;
            }

            // todo: member name
            private int Func213A0A4(NodeData3? head, NodeData3?[] nodeList)
            {
                if (_nodeList.Count == 0 || head == null)
                {
                    return 0;
                }
                int count = 0;
                int i = 0;
                int index = 0;
                while (i < _nodeList.Count)
                {
                    int j = 0;
                    for (; j < count; j++)
                    {
                        // the ushort list contains pairs of values, at least when pointed to by ptr1
                        // the second is an index used here, the first is an advancing/offset value used below
                        if (nodeList[j] == _nodeList[head.Values[head.Index1 + index + 1]])
                        {
                            break;
                        }
                    }
                    if (j == count)
                    {
                        NodeData3 node = _nodeList[head.Values[head.Index1 + index + 1]];
                        if (node != head)
                        {
                            nodeList[count] = node;
                            count++;
                            if (count >= 19)
                            {
                                break;
                            }
                        }
                    }
                    i += head.Values[head.Index1 + index];
                    index += 2;
                }
                return count;
            }

            // todo: member name
            private NodeData3 Func213A1A8()
            {
                Debug.Assert(_node3C != null);
                Debug.Assert(_node40 != null);
                int index = 0;
                int maxId = _node3C.Id;
                int valueTotal = 0;
                if (_node40.Values[_node40.Index1] <= maxId)
                {
                    int value2;
                    do
                    {
                        int value = _node40.Values[_node40.Index1 + index];
                        valueTotal += value;
                        index += 2;
                        value2 = valueTotal + _node40.Values[_node40.Index1 + index];
                    }
                    while (value2 <= maxId);
                }
                return _nodeList[_node40.Values[_node40.Index1 + index + 1]];
            }

            // todo: member name
            private NodeData3? Func21396A0(Vector3 position)
            {
                var nodeList = new NodeData3[20];
                int nodeCount = Func213A0A4(_node40, nodeList);
                if (nodeCount == 0)
                {
                    return _node40;
                }
                NodeData3 result = nodeList[0];
                float maxDist = (position - result.Position).LengthSquared
                    - (_player.Position - result.Position).LengthSquared;
                for (int i = 1; i < nodeCount; i++)
                {
                    NodeData3 node = nodeList[i];
                    float dist = (position - node.Position).LengthSquared
                        - (_player.Position - node.Position).LengthSquared;
                    if (dist > maxDist)
                    {
                        result = node;
                        maxDist = dist;
                    }
                }
                return result;
            }

            private NodeData3 FindHighestNode()
            {
                // todo?: bugfixs? the game has a loop that looks like it should find the node with the highest Y pos,
                // but the first item in the node list is compared in each iteration, so the comparison never succeeds
                // --> the correct item for the iteration is returned if the comparison could succeed, which makes it look even more like a bug
                return _nodeList[0];
            }

            private NodeData3 FindClosestVantageNodeToPosition(Vector3 position)
            {
                NodeData3 result = _nodeList[0];
                float minDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    if (node.NodeType != NodeType.Vantage && result.NodeType == NodeType.Vantage)
                    {
                        continue;
                    }
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    if (dist < minDist)
                    {
                        result = node;
                        minDist = dist;
                    }
                }
                return result;
            }

            private NodeData3 FindClosestVantageNodeToPositionWithRange(Vector3 position)
            {
                NodeData3 result = _nodeList[0];
                float minDist = Vector3.DistanceSquared(result.Position, position);
                bool resultInRange = IsNodeInRange(result);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    // do not consider non-vantage nodes unless the current node is also non-vantage
                    if (node.NodeType != NodeType.Vantage && result.NodeType == NodeType.Vantage)
                    {
                        continue;
                    }
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    bool nodeInRange = IsNodeInRange(node);
                    // take the new node if any of the following:
                    // - new node is vantage and current node is non-vantage
                    // - new node is closer, and either both nodes are non-vantage or position is out of the new node's range
                    // - both nodes are vantage, position is in the current node's range, and position is out of the new node's range
                    if (result.NodeType != NodeType.Vantage && node.NodeType == NodeType.Vantage
                        || dist < minDist && (!nodeInRange || result.NodeType != NodeType.Vantage && node.NodeType != NodeType.Vantage)
                        || result.NodeType == NodeType.Vantage && node.NodeType == NodeType.Vantage && resultInRange && !nodeInRange)
                    {
                        result = node;
                        resultInRange = nodeInRange;
                        minDist = dist;
                    }
                }
                return result;
            }

            private NodeData3 FindFarthestVantageNodeFromPosition(Vector3 position)
            {
                NodeData3 result = _nodeList[0];
                float maxDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = 1; i < _nodeList.Count; i++)
                {
                    NodeData3 node = _nodeList[i];
                    if (node.NodeType != NodeType.Vantage && result.NodeType == NodeType.Vantage)
                    {
                        continue;
                    }
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    if (dist > maxDist)
                    {
                        result = node;
                        maxDist = dist;
                    }
                }
                return result;
            }

            private bool IsNodeInRange(NodeData3 node)
            {
                if (_player._timeSinceJumpPad > 5 * 2 && IsJumpPadNode(node)) // todo: FPS stuff
                {
                    return false;
                }
                if (!_player.Flags1.TestFlag(PlayerFlags1.Grounded)
                    && (_node40 == null || _node40.NodeType != NodeType.Aerial))
                {
                    return false;
                }
                Vector3 between = node.Position - _player.Position;
                between = between.AddY(_player.IsAltForm
                    ? Fixed.ToFloat(_player.Values.AltColRadius) - Fixed.ToFloat(_player.Values.AltColYPos)
                    : 0.5f);
                return between.LengthSquared < node.MaxDistance * node.MaxDistance;
            }

            private bool IsJumpPadNode(NodeData3 node)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.JumpPad)
                    {
                        var jumpPad = (JumpPadEntity)entity;
                        if (jumpPad.ClosestNode == node)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private NodeData3 GetRandomAerialNode()
            {
                int aerialIndex = _nodeTypeIndex[(int)NodeType.Aerial];
                int vantageIndex = _nodeTypeIndex[(int)NodeType.Vantage];
                if (aerialIndex == vantageIndex)
                {
                    // later index (vantage) is the same as earlier (aerial), so there are no aerial nodes
                    return GetRandomNavigationNode();
                }
                int index = aerialIndex + (int)Rng.GetRandomInt2(vantageIndex - aerialIndex);
                return _nodeList[index];
            }

            private NodeData3 GetRandomNavigationNode()
            {
                int index = (int)Rng.GetRandomInt2(_nodeTypeIndex[(int)NodeType.Navigation]);
                return _nodeList[index];
            }

            // todo: member name
            private NodeData3 GetRandomNavigationNodeByField4()
            {
                int navIndex = _nodeTypeIndex[(int)NodeType.Navigation];
                int specIndex = _nodeTypeIndex[(int)NodeType.Special];
                while (navIndex < specIndex)
                {
                    NodeData3 node = _nodeList[navIndex];
                    if (node.Field4 == _field30)
                    {
                        break;
                    }
                    navIndex++;
                }
                int endIndex = navIndex;
                while (navIndex < specIndex)
                {
                    NodeData3 node = _nodeList[endIndex];
                    if (node.Field4 != _field30)
                    {
                        break;
                    }
                    endIndex++;
                }
                if (endIndex == navIndex)
                {
                    return GetRandomNavigationNode();
                }
                int index = navIndex + (int)Rng.GetRandomInt2(endIndex - navIndex);
                return _nodeList[index];
            }

            // todo: member name
            private NodeData3 FindClosestNavigationNodeByField4(Vector3 position)
            {
                int navIndex = _nodeTypeIndex[(int)NodeType.Navigation];
                int specIndex = _nodeTypeIndex[(int)NodeType.Special];
                if (navIndex == specIndex)
                {
                    return FindClosestNonHazardNodeToPosition(position);
                }
                NodeData3 result = _nodeList[navIndex];
                float minDist = Vector3.DistanceSquared(result.Position, position);
                for (int i = navIndex + 1; i < specIndex; i++)
                {
                    NodeData3 node = _nodeList[i];
                    if (node.Field4 != _field30 && result.Field4 == _field30)
                    {
                        break;
                    }
                    float dist = Vector3.DistanceSquared(node.Position, position);
                    if (dist < minDist
                        || node.Field4 == _field30 && result.Field4 != _field30)
                    {
                        result = node;
                        minDist = dist;
                    }
                }
                return result;
            }

            private PlayerEntity FindClosestOpponentToPosition(Vector3 position, bool botsOnly = false)
            {
                PlayerEntity? result = null;
                float minDist = Single.MaxValue;
                Flags2 |= AiFlags2.Bit9;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (result == null)
                    {
                        result = player;
                        minDist = Vector3.DistanceSquared(player.Position, position);
                    }
                    if (player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0
                        && (!botsOnly || player.IsBot))
                    {
                        float dist = Vector3.DistanceSquared(player.Position, position);
                        if (dist <= minDist || result.Health == 0 || botsOnly && !result.IsBot)
                        {
                            result = player;
                            minDist = dist;
                            Flags2 &= ~AiFlags2.Bit9;
                        }
                    }
                }
                Debug.Assert(result != null);
                return result;
            }

            // todo: member name
            private PlayerEntity Func2138038(Vector3 position)
            {
                // same as FindClosestOpponentToPosition(), but only testing those for whom Func214857C() returns true
                PlayerEntity? result = null;
                float minDist = Single.MaxValue;
                Flags2 |= AiFlags2.Bit9;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (result == null)
                    {
                        result = player;
                        minDist = Vector3.DistanceSquared(player.Position, position);
                    }
                    if (AggroFunc214857C(6, 1, 2, null, player)
                        && player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0)
                    {
                        float dist = Vector3.DistanceSquared(player.Position, position);
                        if (dist <= minDist || result.Health == 0)
                        {
                            result = player;
                            minDist = dist;
                            Flags2 &= ~AiFlags2.Bit9;
                        }
                    }
                }
                Debug.Assert(result != null);
                return result;
            }

            // todo: member name
            private PlayerEntity Func2137E8C(Vector3 position)
            {
                // get opponent with primary criteria being max value from Func2148394(), then min distance as tiebreaker
                PlayerEntity? result = null;
                float minDist = Single.MaxValue;
                float dist = 0;
                int maxValue = 0;
                Flags2 |= AiFlags2.Bit9;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (result == null)
                    {
                        result = player;
                        minDist = Vector3.DistanceSquared(player.Position, position);
                    }
                    if (player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0)
                    {
                        int value = AggroFunc2148394(7, 2, 1, player, null);
                        if (value > maxValue)
                        {
                            result = player;
                            minDist = dist; // the game might use an undefined value here
                            maxValue = value;
                            Flags2 &= ~AiFlags2.Bit9;
                        }
                        else if (value == maxValue)
                        {
                            dist = Vector3.DistanceSquared(player.Position, position);
                            if (dist <= minDist || result.Health == 0)
                            {
                                result = player;
                                minDist = dist;
                                Flags2 &= ~AiFlags2.Bit9;
                            }
                        }
                    }
                }
                Debug.Assert(result != null);
                return result;
            }

            // todo: member name
            private PlayerEntity Func21378C0(Vector3 position)
            {
                // same as FuncFunc2137E8C(), but only testing those for whom Func214857C() returns true
                PlayerEntity? result = null;
                float minDist = Single.MaxValue;
                float dist = minDist;
                int maxValue = 0;
                Flags2 |= AiFlags2.Bit9;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (result == null)
                    {
                        result = player;
                        minDist = Vector3.DistanceSquared(player.Position, position);
                    }
                    if (AggroFunc214857C(6, 1, 2, null, player)
                        && player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0)
                    {
                        int value = AggroFunc2148394(7, 2, 1, player, null);
                        if (value > maxValue)
                        {
                            result = player;
                            minDist = dist; // the game might use an undefined value here
                            maxValue = value;
                            Flags2 &= ~AiFlags2.Bit9;
                        }
                        else if (value == maxValue)
                        {
                            dist = Vector3.DistanceSquared(player.Position, position);
                            if (dist <= minDist || result.Health == 0)
                            {
                                result = player;
                                minDist = dist;
                                Flags2 &= ~AiFlags2.Bit9;
                            }
                        }
                    }
                }
                Debug.Assert(result != null);
                return result;
            }

            // todo: member name
            private PlayerEntity? Func2137AA4(Vector3 position)
            {
                // similar to Func21378C0(), but with additional criteria including another call to Func2148394()
                // for teammates only, and something to do with the target's freeze timer apparently, and no default
                PlayerEntity? result = null;
                bool v4 = false;
                int maxValue = -50000;
                Flags2 |= AiFlags2.Bit9;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    bool v14 = AggroFunc214857C(6, 1, 2, null, player);
                    if ((v14 || !v4) && player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0)
                    {
                        int value = AggroFunc2148394(7, 2, 1, player, null);
                        for (int j = 0; j < _scene.Entities.Count; j++)
                        {
                            entity = _scene.Entities[j];
                            if (entity.Type != EntityType.Player)
                            {
                                continue;
                            }
                            var other = (PlayerEntity)entity;
                            if (other != _player && other.TeamIndex == _player.TeamIndex && other.Health > 0)
                            {
                                value += AggroFunc2148394(7, 2, 2, player, other);
                            }
                        }
                        int dist = (int)Vector3.DistanceSquared(player.Position, position);
                        if (dist < 400)
                        {
                            value += (400 - dist) / 4;
                        }
                        value += 2 * player._frozenTimer / 2; // todo: FPS stuff
                        if (value > maxValue || !v4 && v14)
                        {
                            if (v14)
                            {
                                v4 = true;
                            }
                            result = player;
                            maxValue = value;
                            Flags2 &= ~AiFlags2.Bit9;
                        }
                    }
                }
                return result;
            }

            // todo: member name
            private HalfturretEntity? Func2137D08(Vector3 position)
            {
                // get opponent with primary criteria being max value from Func2148394(), then min distance as tiebreaker,
                // with a "priority" value of 0 causing one to be discarded unless it's targeting us, in which case priority is 1
                HalfturretEntity? result = null;
                float minDist = 10000;
                float dist = minDist;
                int maxValue = 0;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (player != _player && player.TeamIndex != _player.TeamIndex && player.Health > 0
                        && player.Hunter == Hunter.Weavel && player.Halfturret.Health > 0)
                    {
                        int value = AggroFunc2148394(5, 2, 1, player, null);
                        if (value == 0)
                        {
                            if (player.Halfturret.Target != _player)
                            {
                                continue;
                            }
                            value = 1;
                        }
                        if (value > maxValue)
                        {
                            result = player.Halfturret;
                            minDist = dist; // the game might use an undefined value here
                            maxValue = value;
                        }
                        else if (value == maxValue)
                        {
                            dist = Vector3.DistanceSquared(player.Position, position);
                            if (dist <= minDist)
                            {
                                result = player.Halfturret;
                                minDist = dist;
                            }
                        }
                    }
                }
                return result;
            }

            private ItemSpawnEntity? FindClosestPopulatedItemSpawnToPosition(Vector3 position, bool checkNeeded = true)
            {
                // get item spawn, where priority is given to those whose items have spawned, then min distance is checked.
                // if true is passed, IsItemNotNeeded() must return false, or the candidate is discarded.
                // there are also specific checks in Func2137860() that may cause us to just return the first entity in the list,
                // and specific checks in Func21377FC() that also cause us to discard the candidate.
                ItemSpawnEntity? result = null;
                float minDist = Single.MaxValue; // uninitialized in-game, but not used until after the first entity is found
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ItemSpawn)
                    {
                        continue;
                    }
                    var itemSpawn = (ItemSpawnEntity)entity;
                    if (result == null)
                    {
                        result = itemSpawn;
                        if (Func2137860())
                        {
                            return result;
                        }
                        continue;
                    }
                    if ((!checkNeeded || !IsItemNotNeeded(itemSpawn.Data.ItemType))
                        && (GameState.Mode != GameMode.PrimeHunter || GameState.PrimeHunter != _player.SlotIndex || !IsHealth(itemSpawn))
                        && (result == null || result.Item == null || itemSpawn.Item != null)
                        && (itemSpawn.Item == null || !Func21377FC(itemSpawn.Item)))
                    {
                        float dist = Vector3.DistanceSquared(itemSpawn.Position, position);
                        if (result == null || result.Item == null && itemSpawn.Item != null || dist < minDist)
                        {
                            result = itemSpawn;
                            minDist = dist;
                        }
                    }
                }
                return result;
            }

            private ItemSpawnEntity? FindClosestPopulatedItemSpawnOfTypeToPosition(Vector3 position,
                ItemType type1, ItemType type2 = ItemType.None, ItemType type3 = ItemType.None)
            {
                bool IsType(ItemSpawnEntity candidate)
                {
                    return candidate.Data.ItemType == type1
                        || type2 != ItemType.None && candidate.Data.ItemType == type2
                        || type3 != ItemType.None && candidate.Data.ItemType == type3;
                }

                ItemSpawnEntity? result = null;
                float minDist = Single.MaxValue; // uninitialized in-game, but not used until after the first entity is found
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ItemSpawn)
                    {
                        continue;
                    }
                    var itemSpawn = (ItemSpawnEntity)entity;
                    if (result == null)
                    {
                        result = itemSpawn;
                        if (Func2137860())
                        {
                            return result;
                        }
                        continue;
                    }
                    if (!IsType(itemSpawn)
                        || itemSpawn.Item != null && Func21377FC(itemSpawn.Item))
                    {
                        continue;
                    }
                    float dist = Vector3.DistanceSquared(itemSpawn.Position, position);
                    if (result.Item == null && itemSpawn.Item != null
                        || !IsType(result) || dist < minDist)
                    {
                        result = itemSpawn;
                        minDist = dist;
                    }
                }
                return result;
            }

            private ItemInstanceEntity? FindClosestItemToPosition(Vector3 position, bool checkNeeded = true)
            {
                ItemInstanceEntity? result = null;
                float minDist = Single.MaxValue;
                if (Func2137860())
                {
                    return result;
                }
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ItemInstance)
                    {
                        continue;
                    }
                    var item = (ItemInstanceEntity)entity;
                    if ((!checkNeeded || !IsItemNotNeeded(item.ItemType))
                        && (GameState.Mode != GameMode.PrimeHunter || GameState.PrimeHunter != _player.SlotIndex || !IsHealth(item))
                        && item.DespawnTimer > 0
                        && !Func21377FC(item))
                    {
                        float dist = Vector3.DistanceSquared(item.Position, position);
                        if (dist < minDist)
                        {
                            result = item;
                            minDist = dist;
                        }
                    }
                }
                return result;
            }

            private ItemInstanceEntity? FindClosestItemOfTypeToPosition(Vector3 position,
                ItemType type1, ItemType type2 = ItemType.None, ItemType type3 = ItemType.None)
            {
                bool IsType(ItemInstanceEntity candidate)
                {
                    return candidate.ItemType == type1
                        || type2 != ItemType.None && candidate.ItemType == type2
                        || type3 != ItemType.None && candidate.ItemType == type3;
                }

                ItemInstanceEntity? result = null;
                if (Func2137860())
                {
                    return result;
                }
                float minDist = Single.MaxValue; // uninitialized in-game, but not used until after the first entity is found
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.ItemInstance)
                    {
                        continue;
                    }
                    var item = (ItemInstanceEntity)entity;
                    if (IsType(item) && item.DespawnTimer > 0 && !Func21377FC(item))
                    {
                        float dist = Vector3.DistanceSquared(item.Position, position);
                        if (dist < minDist)
                        {
                            result = item;
                            minDist = dist;
                        }
                    }
                }
                return result;
            }

            // todo: member name
            private bool Func2137860()
            {
                // MP1 SANCTORUS (Data Shrine), or
                // MP6 HEADSHOT (Head Shot) and currently carrying flag DC
                return GameState.IsOctolithMode && (_scene.RoomId == 93 || _scene.RoomId == 99 && _octolithFlagDC?.Carrier == _player);
            }

            private bool IsItemNotNeeded(ItemType itemType)
            {
                if (itemType == ItemType.HealthMedium || itemType == ItemType.HealthSmall || itemType == ItemType.HealthBig)
                {
                    return _player.Health == _player.HealthMax;
                }
                if (itemType == ItemType.UASmall || itemType == ItemType.UABig)
                {
                    return _player._ammo[0] == _player._ammoMax[0];
                }
                if (itemType == ItemType.MissileSmall || itemType == ItemType.MissileBig
                    || itemType == ItemType.AffinityWeapon && _player.Hunter == Hunter.Samus)
                {
                    return _player._ammo[1] == _player._ammoMax[1];
                }
                if (itemType == ItemType.VoltDriver || itemType == ItemType.Battlehammer || itemType == ItemType.Imperialist
                    || itemType == ItemType.Judicator || itemType == ItemType.Magmaul || itemType == ItemType.ShockCoil
                    || itemType == ItemType.OmegaCannon || itemType == ItemType.AffinityWeapon)
                {
                    int weapon = (int)itemType - 4;
                    if (itemType == ItemType.AffinityWeapon)
                    {
                        weapon = (int)Weapons.AffinityWeapons[(int)_player.Hunter];
                    }
                    return _player.AvailableWeapons[weapon];
                }
                return false;
            }

            private bool IsHealth(ItemSpawnEntity itemSpawn)
            {
                return itemSpawn.Data.ItemType == ItemType.HealthMedium
                    || itemSpawn.Data.ItemType == ItemType.HealthSmall
                    || itemSpawn.Data.ItemType == ItemType.HealthBig;
            }

            private bool IsHealth(ItemInstanceEntity item)
            {
                return item.ItemType == ItemType.HealthMedium
                    || item.ItemType == ItemType.HealthSmall
                    || item.ItemType == ItemType.HealthBig;
            }

            // todo: member name
            private bool Func21377FC(ItemInstanceEntity item)
            {
                // UNIT 4 ARCTERRA BASE (Arcterra Gateway)
                return GameState.IsOctolithMode && _scene.RoomId == 117
                    && _octolithFlagDC?.Carrier == _player && item.Owner?.Id == 53;
            }

            private NodeDefenseEntity? FindClosestNodeDefense(Vector3 position)
            {
                NodeDefenseEntity? result = null;
                float minDist = Single.MaxValue;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.NodeDefense)
                    {
                        continue;
                    }
                    var defense = (NodeDefenseEntity)entity;
                    float dist = Vector3.DistanceSquared(defense.Position, position);
                    if (dist < minDist)
                    {
                        result = defense;
                        minDist = dist;
                    }
                }
                return result;
            }

            private NodeDefenseEntity? ChooseNodeDefenseToRetake()
            {
                // if all nodes are held by our team, return the first node in the list.
                // otherwise, find the opponent who has captured the most nodes, choosing randomly
                // among any tied opponents, and randomly return one of that opponent's nodes.
                // if no nodes are captured by opponents, randomly return a non-captured node.
                NodeDefenseEntity? firstResult = null;
                int[] captureList = new int[4];
                int maxCaptureCount = 0;
                int maxCaptureSlotIndex = 0;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.NodeDefense)
                    {
                        continue;
                    }
                    var defense = (NodeDefenseEntity)entity;
                    if (firstResult == null)
                    {
                        firstResult = defense;
                    }
                    if (defense.CapturedPlayer != null && defense.CapturedPlayer.TeamIndex != _player.TeamIndex)
                    {
                        int captureCount = ++captureList[defense.CapturedPlayer.SlotIndex];
                        if (captureCount > maxCaptureCount)
                        {
                            maxCaptureSlotIndex = defense.CapturedPlayer.SlotIndex;
                            maxCaptureCount = captureCount;
                        }
                    }
                }
                int resultCount = 0;
                var resultList = new NodeDefenseEntity?[10];
                if (maxCaptureCount > 0)
                {
                    int playerCount = 0;
                    var playerList = new PlayerEntity?[4];
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Player)
                        {
                            continue;
                        }
                        var player = (PlayerEntity)entity;
                        if (player.TeamIndex != _player.TeamIndex && player.SlotIndex == maxCaptureSlotIndex)
                        {
                            playerList[playerCount++] = player;
                        }
                    }
                    PlayerEntity? chosenPlayer = playerList[Rng.GetRandomInt2(playerCount)];
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.NodeDefense)
                        {
                            continue;
                        }
                        var defense = (NodeDefenseEntity)entity;
                        if (resultCount < 10 && defense.CapturedPlayer == chosenPlayer)
                        {
                            resultList[resultCount++] = defense;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.NodeDefense)
                        {
                            continue;
                        }
                        var defense = (NodeDefenseEntity)entity;
                        if (resultCount < 10 && defense.CapturedPlayer == null)
                        {
                            resultList[resultCount++] = defense;
                        }
                    }
                }
                if (resultCount > 0)
                {
                    return resultList[Rng.GetRandomInt2(resultCount)];
                }
                return firstResult;
            }

            private NodeDefenseEntity? FindClosestFriendlyNodeDefense()
            {
                NodeDefenseEntity? result = null;
                float minDist = Single.MaxValue;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.NodeDefense)
                    {
                        continue;
                    }
                    var defense = (NodeDefenseEntity)entity;
                    if (defense.CapturedPlayer != null && defense.CapturedPlayer.TeamIndex == _player.TeamIndex && defense.IsOccupied)
                    {
                        float dist = Vector3.DistanceSquared(defense.Position, _player.Position);
                        if (dist < minDist)
                        {
                            result = defense;
                            minDist = dist;
                        }
                    }
                }
                return result;
            }

            private DoorEntity? FindClosestDoor(Vector3 position)
            {
                DoorEntity? result = null;
                float minDist = Single.MaxValue;
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Door)
                    {
                        continue;
                    }
                    var defense = (DoorEntity)entity;
                    float dist = Vector3.DistanceSquared(defense.Position, position);
                    if (dist < minDist)
                    {
                        result = defense;
                        minDist = dist;
                    }
                }
                return result;
            }

            private void UpdateNodeDataSetSelection()
            {
                // todo?: simplify the bit shifting addition nonsense?
                for (int i = 0; i < _nodeData.SetIndices.Count; i++)
                {
                    if (_nodeData.SetSelector[i] && (_nodeDataSelOff & (1 << i)) != 0)
                    {
                        _nodeData.SetSelector[i] = false;
                    }
                    else if (!_nodeData.SetSelector[i] && (_nodeDataSelOn & (1 << i)) != 0)
                    {
                        _nodeData.SetSelector[i] = true;
                    }
                }
                int newIndex = 0;
                for (int i = 0; i < _nodeData.SetIndices.Count; i++)
                {
                    newIndex += (_nodeData.SetSelector[i] ? 1 : 0) << i;
                }
                // todo?: it's kind of jank how this is called for each bot, but updates all bots
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.Player)
                    {
                        var player = (PlayerEntity)entity;
                        if (player.IsBot && player.AiData._nodeDataSetIndex != newIndex)
                        {
                            player.AiData._nodeDataSetIndex = newIndex;
                            player.AiData.SetClosestNodeList(player.Position);
                        }
                    }
                }
            }

            private void SetClosestNodeList(Vector3 position)
            {
                Flags2 &= ~AiFlags2.Bit7;
                IReadOnlyList<IReadOnlyList<NodeData3>> data1 = _nodeData.Data[_nodeDataSetIndex];
                _nodeList = data1[0];
                if (data1.Count > 1)
                {
                    float minDist = GetClosestNodeInList(position, data1[0]);
                    for (int i = 1; i < data1.Count; i++)
                    {
                        float dist = GetClosestNodeInList(position, data1[i]);
                        if (dist < minDist)
                        {
                            _nodeList = data1[i];
                            minDist = dist;
                        }
                    }
                }
                SetNodeTypeFirstIndices();
            }

            private float GetClosestNodeInList(Vector3 position, IReadOnlyList<NodeData3> data2)
            {
                // the game returns the closest ND3 and has the caller compute the distance again
                float minDist = Vector3.DistanceSquared(data2[0].Position, position);
                for (int i = 1; i < data2.Count; i++)
                {
                    float dist = Vector3.DistanceSquared(data2[i].Position, position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                    }
                }
                return minDist;
            }

            private void SetNodeTypeFirstIndices()
            {
                // nodes are always arranged from lower types to higher types, so if we haven't seen a type by the time
                // we first see a higher one, the lower one isn't in the list and we just assign them both the same index
                int curType = 0;
                for (int i = 0; i < _nodeList.Count; i++)
                {
                    ushort type = (ushort)_nodeList[i].NodeType;
                    while (curType < type)
                    {
                        _nodeTypeIndex[curType] = i;
                        curType++;
                    }
                }
                for (int i = curType; i < 6; i++)
                {
                    _nodeTypeIndex[i] = _nodeList.Count;
                }
            }

            private static void RemovePlayerFromGlobals(PlayerEntity player)
            {
                if (_globalField2 == 0)
                {
                    return;
                }
                int i;
                for (i = 0; i < _globalField2; i++)
                {
                    if (_globalObjs[i].Player == player)
                    {
                        break;
                    }
                }
                if (i == _globalField2)
                {
                    return;
                }
                for (; i < _globalField2 - 1; i++)
                {
                    AiGlobals current = _globalObjs[i];
                    AiGlobals next = _globalObjs[i + 1];
                    current.Player = next.Player;
                    current.Field4 = next.Field4;
                    current.NodeDataIndex = next.NodeDataIndex;
                    current.NodeData = next.NodeData;
                }
                _globalField2--;
            }

            // todo: member names
            private class AiEntityRefs
            {
                public NodeData3? Field0 { get; set; }
                public NodeData3? Field1 { get; set; }
                public NodeData3? Field2 { get; set; }
                public NodeData3? Field3 { get; set; }
                public NodeData3? Field4 { get; set; }
                public NodeData3? Field5 { get; set; }
                public NodeData3? Field6 { get; set; }
                public NodeData3? Field7 { get; set; }
                public NodeData3? Field8 { get; set; }
                public NodeData3? Field9 { get; set; }
                public NodeData3? Field10 { get; set; }
                public NodeData3? Field11 { get; set; }
                public NodeData3? Field12 { get; set; }
                public NodeData3? Field13 { get; set; }
                public NodeData3? Field14 { get; set; }
                public NodeData3? Field15 { get; set; }
                public NodeData3? Field16 { get; set; }
                public NodeData3? Field17 { get; set; }
                public NodeData3? Field18 { get; set; }
                public NodeData3? Field19 { get; set; }
                public NodeData3? Field20 { get; set; }
                public NodeData3? Field21 { get; set; }
                public NodeData3? Field22 { get; set; }
                public NodeData3? Field23 { get; set; }
                public NodeData3? Field24 { get; set; }
                public NodeData3? Field25 { get; set; }
                public PlayerEntity? Field26 { get; set; }
                public PlayerEntity? Field27 { get; set; }
                public PlayerEntity? Field28 { get; set; }
                public PlayerEntity? Field29 { get; set; }
                public PlayerEntity? Field30 { get; set; }
                public PlayerEntity? Field31 { get; set; }
                public PlayerEntity? Field32 { get; set; }
                public HalfturretEntity? Field33 { get; set; }
                public ItemSpawnEntity? Field34 { get; set; }
                public ItemSpawnEntity? Field35 { get; set; }
                public ItemSpawnEntity? Field36 { get; set; }
                public ItemSpawnEntity? Field37 { get; set; }
                public ItemSpawnEntity? Field38 { get; set; }
                public ItemSpawnEntity? Field39 { get; set; }
                public ItemSpawnEntity? Field40 { get; set; }
                public ItemSpawnEntity? Field41 { get; set; }
                public ItemSpawnEntity? Field42 { get; set; }
                public ItemSpawnEntity? Field43 { get; set; }
                public ItemSpawnEntity? Field44 { get; set; }
                public ItemSpawnEntity? Field45 { get; set; }
                public ItemSpawnEntity? Field46 { get; set; }
                public ItemSpawnEntity? Field47 { get; set; }
                public ItemSpawnEntity? Field48 { get; set; }
                public ItemSpawnEntity? Field49 { get; set; }
                public ItemSpawnEntity? Field50 { get; set; }
                public ItemSpawnEntity? Field51 { get; set; }
                public ItemSpawnEntity? Field52 { get; set; }
                public ItemSpawnEntity? Field53 { get; set; }
                public ItemInstanceEntity? Field54 { get; set; }
                public ItemInstanceEntity? Field55 { get; set; }
                public ItemInstanceEntity? Field56 { get; set; }
                public ItemInstanceEntity? Field57 { get; set; }
                public ItemInstanceEntity? Field58 { get; set; }
                public ItemInstanceEntity? Field59 { get; set; }
                public ItemInstanceEntity? Field60 { get; set; }
                public ItemInstanceEntity? Field61 { get; set; }
                public ItemInstanceEntity? Field62 { get; set; }
                public ItemInstanceEntity? Field63 { get; set; }
                public ItemInstanceEntity? Field64 { get; set; }
                public ItemInstanceEntity? Field65 { get; set; }
                public ItemInstanceEntity? Field66 { get; set; }
                public ItemInstanceEntity? Field67 { get; set; }
                public ItemInstanceEntity? Field68 { get; set; }
                public ItemInstanceEntity? Field69 { get; set; }
                public ItemInstanceEntity? Field70 { get; set; }
                public ItemInstanceEntity? Field71 { get; set; }
                public ItemInstanceEntity? Field72 { get; set; }
                public ItemInstanceEntity? Field73 { get; set; }
                public NodeDefenseEntity? Field74 { get; set; }
                public NodeDefenseEntity? Field75 { get; set; }
                public NodeDefenseEntity? Field76 { get; set; }
                public DoorEntity? Field77 { get; set; }

                public bool IsPopulated(int index)
                {
                    return index switch
                    {
                        0 => Field0 != null,
                        1 => Field1 != null,
                        2 => Field2 != null,
                        3 => Field3 != null,
                        4 => Field4 != null,
                        5 => Field5 != null,
                        6 => Field6 != null,
                        7 => Field7 != null,
                        8 => Field8 != null,
                        9 => Field9 != null,
                        10 => Field10 != null,
                        11 => Field11 != null,
                        12 => Field12 != null,
                        13 => Field13 != null,
                        14 => Field14 != null,
                        15 => Field15 != null,
                        16 => Field16 != null,
                        17 => Field17 != null,
                        18 => Field18 != null,
                        19 => Field19 != null,
                        20 => Field20 != null,
                        21 => Field21 != null,
                        22 => Field22 != null,
                        23 => Field23 != null,
                        24 => Field24 != null,
                        25 => Field25 != null,
                        26 => Field26 != null,
                        27 => Field27 != null,
                        28 => Field28 != null,
                        29 => Field29 != null,
                        30 => Field30 != null,
                        31 => Field31 != null,
                        32 => Field32 != null,
                        33 => Field33 != null,
                        34 => Field34 != null,
                        35 => Field35 != null,
                        36 => Field36 != null,
                        37 => Field37 != null,
                        38 => Field38 != null,
                        39 => Field39 != null,
                        40 => Field40 != null,
                        41 => Field41 != null,
                        42 => Field42 != null,
                        43 => Field43 != null,
                        44 => Field44 != null,
                        45 => Field45 != null,
                        46 => Field46 != null,
                        47 => Field47 != null,
                        48 => Field48 != null,
                        49 => Field49 != null,
                        50 => Field50 != null,
                        52 => Field52 != null,
                        53 => Field53 != null,
                        54 => Field54 != null,
                        55 => Field55 != null,
                        56 => Field56 != null,
                        57 => Field57 != null,
                        58 => Field58 != null,
                        59 => Field59 != null,
                        60 => Field60 != null,
                        61 => Field61 != null,
                        62 => Field62 != null,
                        63 => Field63 != null,
                        64 => Field64 != null,
                        65 => Field65 != null,
                        66 => Field66 != null,
                        67 => Field67 != null,
                        68 => Field68 != null,
                        69 => Field69 != null,
                        70 => Field70 != null,
                        71 => Field71 != null,
                        72 => Field72 != null,
                        73 => Field73 != null,
                        74 => Field74 != null,
                        75 => Field75 != null,
                        76 => Field76 != null,
                        77 => Field77 != null,
                        _ => throw new ProgramException("Invalid AI entity index.")
                    };
                }

                public void Clear()
                {
                    Field0 = null;
                    Field1 = null;
                    Field2 = null;
                    Field3 = null;
                    Field4 = null;
                    Field5 = null;
                    Field6 = null;
                    Field7 = null;
                    Field8 = null;
                    Field9 = null;
                    Field10 = null;
                    Field11 = null;
                    Field12 = null;
                    Field13 = null;
                    Field14 = null;
                    Field15 = null;
                    Field16 = null;
                    Field17 = null;
                    Field18 = null;
                    Field19 = null;
                    Field20 = null;
                    Field21 = null;
                    Field22 = null;
                    Field23 = null;
                    Field24 = null;
                    Field25 = null;
                    Field26 = null;
                    Field27 = null;
                    Field28 = null;
                    Field29 = null;
                    Field30 = null;
                    Field31 = null;
                    Field32 = null;
                    Field33 = null;
                    Field34 = null;
                    Field35 = null;
                    Field36 = null;
                    Field37 = null;
                    Field38 = null;
                    Field39 = null;
                    Field40 = null;
                    Field41 = null;
                    Field42 = null;
                    Field43 = null;
                    Field44 = null;
                    Field45 = null;
                    Field46 = null;
                    Field47 = null;
                    Field48 = null;
                    Field49 = null;
                    Field50 = null;
                    Field52 = null;
                    Field53 = null;
                    Field54 = null;
                    Field55 = null;
                    Field56 = null;
                    Field57 = null;
                    Field58 = null;
                    Field59 = null;
                    Field60 = null;
                    Field61 = null;
                    Field62 = null;
                    Field63 = null;
                    Field64 = null;
                    Field65 = null;
                    Field66 = null;
                    Field67 = null;
                    Field68 = null;
                    Field69 = null;
                    Field70 = null;
                    Field71 = null;
                    Field72 = null;
                    Field73 = null;
                    Field74 = null;
                    Field75 = null;
                    Field76 = null;
                    Field77 = null;
                }
            }

            private AiQueuedEnt _queuedFindEntityAction = AiQueuedEnt.None; // todo: member name
            private readonly AiEntityRefs _entityRefs = new AiEntityRefs();

            public void OnTakeDamage(int damage, EntityBase source, PlayerEntity? attacker)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (!player.IsBot)
                    {
                        return;
                    }
                    player.AiData._slotHits[_player.SlotIndex]++;
                    player.AiData._slotDamage[_player.SlotIndex] += damage;
                    if (attacker != null)
                    {
                        if (player == _player)
                        {
                            if (source.Type == EntityType.BeamProjectile
                                && attacker.Hunter == Hunter.Weavel && attacker.IsAltForm)
                            {
                                AggroFunc214864C(5, 2, 1, attacker, null, damage, damage, 2, 2);
                            }
                            else
                            {
                                AggroFunc214864C(4, 2, 1, attacker, null, damage, damage, 2, 2);
                                if (source.Type == EntityType.BeamProjectile
                                    && ((BeamProjectileEntity)entity).Beam == BeamType.ShockCoil
                                    && attacker.ShockCoilTimer > 10 * 2) // todo: FPS stuff
                                {
                                    player.AiData.Flags2 |= AiFlags2.Bit21;
                                }
                            }
                        }
                        else if (source.Type == EntityType.BeamProjectile
                            && attacker.Hunter == Hunter.Weavel && attacker.IsAltForm)
                        {
                            AggroFunc214864C(5, 2, 2, attacker, _player, damage, damage, 2, 2);
                        }
                        else
                        {
                            AggroFunc214864C(4, 2, 2, attacker, _player, damage, damage, 2, 2);
                        }
                    }
                }
            }

            public enum AiEntRefType
            {
                Type0 = 0,
                Type1 = 1,
                Type2 = 2,
                Type3 = 3,
                Type4 = 4,
                Type5 = 5,
                Type6 = 6,
                Type7 = 7,
                Type8 = 8,
                Type9 = 9,
                Type10 = 10,
                Type11 = 11,
                Type12 = 12,
                Type13 = 13,
                Type14 = 14,
                Type15 = 15,
                Type16 = 16,
                Type17 = 17,
                Type18 = 18,
                Type19 = 19,
                Type20 = 20,
                Type21 = 21,
                Type22 = 22,
                Type23 = 23,
                Type24 = 24,
                Type25 = 25,
                Type26 = 26,
                Type27 = 27,
                Type28 = 28,
                Type29 = 29,
                Type30 = 30,
                Type31 = 31,
                Type32 = 32,
                Type33 = 33,
                Type34 = 34,
                Type35 = 35,
                Type36 = 36,
                Type37 = 37,
                Type38 = 38,
                Type39 = 39,
                Type40 = 40,
                Type41 = 41,
                Type42 = 42,
                Type43 = 43,
                Type44 = 44,
                Type45 = 45,
                Type46 = 46,
                Type47 = 47,
                Type48 = 48,
                Type49 = 49,
                Type50 = 50,
                Type51 = 51,
                Type52 = 52,
                Type53 = 53,
                Type54 = 54,
                Type55 = 55,
                Type56 = 56,
                Type57 = 57,
                Type58 = 58,
                Type59 = 59,
                Type60 = 60,
                Type61 = 61,
                Type62 = 62,
                Type63 = 63,
                Type64 = 64,
                Type65 = 65,
                Type66 = 66,
                Type67 = 67,
                Type68 = 68,
                Type69 = 69,
                Type70 = 70,
                Type71 = 71,
                Type72 = 72,
                Type73 = 73,
                Type74 = 74,
                Type75 = 75,
                Type76 = 76,
                Type77 = 77
            }

            public enum AiQueuedEnt
            {
                Type0 = 0,
                Type1 = 1,
                Type2 = 2,
                Type3 = 3,
                Type4 = 4,
                Type5 = 5,
                Type6 = 6,
                Type7 = 7,
                Type8 = 8,
                Type9 = 9,
                Type10 = 10,
                Type11 = 11,
                Type12 = 12,
                Type13 = 13,
                Type14 = 14,
                Type15 = 15,
                Type16 = 16,
                Type17 = 17,
                Type18 = 18,
                Type19 = 19,
                Type20 = 20,
                Type21 = 21,
                Type22 = 22,
                Type23 = 23,
                Type24 = 24,
                Type25 = 25,
                Type26 = 26,
                Type27 = 27,
                Type28 = 28,
                Type29 = 29,
                Type30 = 30,
                Type31 = 31,
                Type32 = 32,
                Type33 = 33,
                Type34 = 34,
                Type35 = 35,
                Type36 = 36,
                Type37 = 37,
                Type38 = 38,
                Type39 = 39,
                None = 40
            }

            #region Debug

            public void GetOuptut(System.Text.StringBuilder sb)
            {
                for (int i = 0; i < _executionTree.Length; i++)
                {
                    AiContext item = _executionTree[i];
                    //AiPersonalityData1.PrintNode(item.Data1, sb);
                    if (i == 0)
                    {
                        sb.AppendLine();
                        for (int j = 0; j < _executionTree.Length; j++)
                        {
                            AiContext node = _executionTree[j];
                            if (j != 0)
                            {
                                sb.Append(" -> ");
                            }
                            sb.Append(node.Data1.Label);
                            if (node.Data1.Data1.Count == 0)
                            {
                                break;
                            }
                        }
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine(item.Data1.Label);
                        Debug.Assert(item.Data1.Parent != null);
                        IReadOnlyList<AiPersonalityData1> level = item.Data1.Parent.Data1;
                        if (level.Count > 1)
                        {
                            for (int j = 0; j < item.Data1.Data2.Count; j++)
                            {
                                int index = item.Data1.Data2[j].Data1SelectIndex;
                                string target;
                                if (index >= 20)
                                {
                                    index = level.Count;
                                    target = "reset";
                                }
                                else
                                {
                                    target = level[index].Label;
                                }
                                int weight = _executionTree[i - 1].Weights[index];
                                float pct = weight / 100000f * 100;
                                sb.AppendLine($"w: {weight,6} / 100000 ({pct,5:f1}%) -> {target}");
                            }
                        }
                    }
                    if (item.Data1.Data1.Count == 0)
                    {
                        break;
                    }
                    sb.AppendLine();
                }
            }

            public static IEnumerable<string> GetFuncs1Names(IEnumerable<int> ids)
            {
                return ids.Select(i => GetFuncs1Name(i));
            }

            private static string GetFuncs1Name(int id)
            {
                // init (d3a) and process (d3b)
                return id switch
                {
                    0 => nameof(Func1_214A39C),
                    1 => nameof(Func1_214A098),
                    2 => nameof(Func1_2149D3C),
                    3 => nameof(Func1_2149C98),
                    4 => nameof(Func1_2149C80),
                    5 => nameof(Func1_2149C68),
                    6 => nameof(Func1_2149C50),
                    7 => nameof(Func1_2149C38),
                    8 => nameof(Func1_2149C20),
                    9 => nameof(Func1_2149C08),
                    10 => nameof(Func1_2149BF0),
                    11 => nameof(Func1_2149BD8),
                    12 => nameof(Func1_2149BC0),
                    13 => nameof(Func1_2149BA8),
                    14 => nameof(Func1_2149B98),
                    15 => nameof(Func1_2149AD8),
                    16 => nameof(Func1_2149AC8),
                    17 => nameof(Func1_2149ABC),
                    18 => nameof(Func1_2149AB0),
                    19 => nameof(Func1_2149AA4),
                    20 => nameof(Func1_2149A98),
                    21 => nameof(Func1_2149A64),
                    22 => nameof(Func1_2149824),
                    23 => nameof(Func1_21497F0),
                    24 => nameof(Func1_2149570),
                    25 => nameof(Func1_21494FC),
                    26 => nameof(Func1_2149488),
                    27 => nameof(Func1_2149414),
                    28 => nameof(Func1_21493A0),
                    29 => nameof(Func1_214932C),
                    30 => nameof(Func1_21495A4),
                    31 => nameof(Func1_2149530),
                    32 => nameof(Func1_21494BC),
                    33 => nameof(Func1_2149448),
                    34 => nameof(Func1_21493D4),
                    35 => nameof(Func1_2149360),
                    36 => nameof(Func1_21492EC),
                    37 => nameof(Func1_21492DC),
                    38 => nameof(Func1_21492CC),
                    39 => nameof(Func1_21492BC),
                    40 => nameof(Func1_21492AC),
                    41 => nameof(Func1_214929C),
                    42 => nameof(Func1_214928C),
                    43 => nameof(Func1_214927C),
                    44 => nameof(Func1_214926C),
                    45 => nameof(Func1_214925C),
                    46 => nameof(Func1_214924C),
                    47 => nameof(Func1_214923C),
                    48 => nameof(Func1_214922C),
                    49 => nameof(Func1_214921C),
                    50 => nameof(Func1_214920C),
                    51 => nameof(Func1_21491FC),
                    52 => nameof(Func1_21491E4),
                    53 => nameof(Func1_21491CC),
                    54 => nameof(Func1_21491B4),
                    55 => nameof(Func1_214919C),
                    56 => nameof(Func1_2149184),
                    57 => nameof(Func1_214916C),
                    58 => nameof(Func1_2149154),
                    59 => nameof(Func1_214913C),
                    60 => nameof(Func1_2149124),
                    61 => nameof(Func1_214910C),
                    62 => nameof(Func1_21490F4),
                    63 => nameof(Func1_21490DC),
                    64 => nameof(Func1_21490C4),
                    65 => nameof(Func1_21490AC),
                    66 => nameof(Func1_2149094),
                    67 => nameof(Func1_2149088),
                    68 => nameof(Func1_2149034),
                    69 => nameof(Func1_2148F10),
                    70 => nameof(Func1_2148EDC),
                    71 => nameof(Func1_2148ECC),
                    72 => nameof(Func1_2148EB8),
                    73 => nameof(Func1_2148EA8),
                    74 => nameof(Func1_2148E98),
                    75 => nameof(Func1_2148E88),
                    76 => nameof(Func1_2148E74),
                    77 => nameof(Func1_2148E64),
                    78 => nameof(Func1_2148E54),
                    79 => nameof(Func1_2148DF8),
                    80 => nameof(Func1_2148DE8),
                    81 => nameof(Func1_2148D50),
                    82 => nameof(Func1_UnlockEchoHallForceField),
                    83 => nameof(Func1_SetInvulnerable),
                    _ => throw new ProgramException("Invalid AI func 1.")
                };
            }

            public static IEnumerable<string> GetFuncs3Names(IEnumerable<int> ids)
            {
                return ids.Select(i => GetFuncs3Name(i));
            }

            public static string GetFuncs3Name(int id)
            {
                // preconditions (d2->d4->d5) and path updates (d2->d5)
                return id switch
                {
                    0 => nameof(Func3_213D87C),
                    1 => nameof(Func3_213D83C),
                    2 => nameof(Func3_213D814),
                    3 => nameof(Func3_213D7F0),
                    4 => nameof(Func3_213D800),
                    5 => nameof(Func3_213D7E8),
                    6 => nameof(Func3_213D7D0),
                    7 => nameof(Func3_213D7B8),
                    8 => nameof(Func3_213D7A0),
                    9 => nameof(Func3_213D77C),
                    10 => nameof(Func3_213D758),
                    11 => nameof(Func3_213D734),
                    12 => nameof(Func3_213D710),
                    13 => nameof(Func3_213D6D0),
                    14 => nameof(Func3_213D6AC),
                    15 => nameof(Func3_213D624),
                    16 => nameof(Func3_213D608),
                    17 => nameof(Func3_213D564),
                    18 => nameof(Func3_213D540),
                    19 => nameof(Func3_213D530),
                    20 => nameof(Func3_213D514),
                    21 => nameof(Func3_213D4C0),
                    22 => nameof(Func3_213D49C),
                    23 => nameof(Func3_213D43C),
                    24 => nameof(Func3_213D418),
                    25 => nameof(Func3_213D388),
                    26 => nameof(Func3_213D36C),
                    27 => nameof(Func3_213D2C0),
                    28 => nameof(Func3_213D2A4),
                    29 => nameof(Func3_213D234),
                    30 => nameof(Func3_213D218),
                    31 => nameof(Func3_213D178),
                    32 => nameof(Func3_213D15C),
                    33 => nameof(Func3_213D128),
                    34 => nameof(Func3_213D0F4),
                    35 => nameof(Func3_213D0C4),
                    36 => nameof(Func3_213D0A8),
                    37 => nameof(Func3_213D078),
                    38 => nameof(Func3_213D05C),
                    39 => nameof(Func3_213D044),
                    40 => nameof(Func3_213D028),
                    41 => nameof(Func3_213D010),
                    42 => nameof(Func3_213CFF4),
                    43 => nameof(Func3_213CFDC),
                    44 => nameof(Func3_213CFC0),
                    45 => nameof(Func3_213CFA4),
                    46 => nameof(Func3_213CF0C),
                    47 => nameof(Func3_213CEE8),
                    48 => nameof(Func3_213CDB8),
                    49 => nameof(Func3_213CF94),
                    50 => nameof(Func3_213CF7C),
                    51 => nameof(Func3_213CDA4),
                    52 => nameof(Func3_213CD74),
                    53 => nameof(Func3_213CD58),
                    54 => nameof(Func3_213CD34),
                    55 => nameof(Func3_213CD18),
                    56 => nameof(Func3_213CCF4),
                    57 => nameof(Func3_213CCD8),
                    58 => nameof(Func3_213CCBC),
                    59 => nameof(Func3_213CCB0),
                    60 => nameof(Func3_213CC94),
                    61 => nameof(Func3_213CBE4),
                    62 => nameof(Func3_213CBC0),
                    63 => nameof(Func3_213CBB0),
                    64 => nameof(Func3_213CB8C),
                    65 => nameof(Func3_213CADC),
                    66 => nameof(Func3_213CAA8),
                    67 => nameof(Func3_213CA84),
                    68 => nameof(Func3_213CA70),
                    69 => nameof(Func3_213CA58),
                    70 => nameof(Func3_213CA2C),
                    71 => nameof(Func3_213CA00),
                    72 => nameof(Func3_213C9D4),
                    73 => nameof(Func3_213C9C4),
                    74 => nameof(Func3_213C89C),
                    75 => nameof(Func3_213C88C),
                    76 => nameof(Func3_213C764),
                    77 => nameof(Func3_213C75C),
                    78 => nameof(Func3_213C698),
                    79 => nameof(Func3_213C64C),
                    80 => nameof(Func3_213C600),
                    81 => nameof(Func3_213C52C),
                    82 => nameof(Func3_213C48C),
                    83 => nameof(Func3_213C470),
                    84 => nameof(Func3_213C334),
                    85 => nameof(Func3_213C310),
                    86 => nameof(Func3_213C0D0),
                    87 => nameof(Func3_213C078),
                    88 => nameof(Func3_213C054),
                    89 => nameof(Func3_213BFFC),
                    90 => nameof(Func3_213BFD8),
                    91 => nameof(Func3_213BED8),
                    92 => nameof(Func3_213BEBC),
                    93 => nameof(Func3_213BEA0),
                    94 => nameof(Func3_213BE48),
                    95 => nameof(Func3_213BE10),
                    96 => nameof(Func3_213BDF4),
                    97 => nameof(Func3_213BD7C),
                    98 => nameof(Func3_213BCE8),
                    99 => nameof(Func3_213BCC4),
                    100 => nameof(Func3_213BCB0),
                    101 => nameof(Func3_213BC8C),
                    102 => nameof(Func3_213BC70),
                    103 => nameof(Func3_213BC4C),
                    104 => nameof(Func3_213BC0C),
                    105 => nameof(Func3_213BBE8),
                    106 => nameof(Func3_213BBA0),
                    107 => nameof(Func3_213BB7C),
                    108 => nameof(Func3_213BAF4),
                    109 => nameof(Func3_213BAD0),
                    110 => nameof(Func3_213BA68),
                    111 => nameof(Func3_213BA44),
                    112 => nameof(Func3_213BA28),
                    113 => nameof(Func3_213BA04),
                    114 => nameof(Func3_213B99C),
                    115 => nameof(Func3_213B978),
                    116 => nameof(Func3_213B8B0),
                    117 => nameof(Func3_213B88C),
                    118 => nameof(Func3_213B7A0),
                    119 => nameof(Func3_213B77C),
                    120 => nameof(Func3_213B690),
                    121 => nameof(Func3_213B5DC),
                    122 => nameof(Func3_213B528),
                    123 => nameof(Func3_213B4E4),
                    124 => nameof(Func3_213B4A0),
                    125 => nameof(Func3_213B45C),
                    126 => nameof(Func3_213B3F0),
                    127 => nameof(Func3_213B3A0),
                    128 => nameof(Func3_213B37C),
                    129 => nameof(Func3_213B34C),
                    130 => nameof(Func3_213B328),
                    131 => nameof(Func3_213B284),
                    132 => nameof(Func3_213B260),
                    133 => nameof(Func3_213B1F0),
                    134 => nameof(Func3_213B1D8),
                    135 => nameof(Func3_213B1C0),
                    136 => nameof(Func3_213B1A8),
                    137 => nameof(Func3_213B190),
                    138 => nameof(Func3_213B178),
                    139 => nameof(Func3_213B160),
                    140 => nameof(Func3_213B148),
                    141 => nameof(Func3_213B130),
                    142 => nameof(Func3_213B118),
                    143 => nameof(Func3_213B100),
                    144 => nameof(Func3_213B0E8),
                    145 => nameof(Func3_213B0D0),
                    146 => nameof(Func3_213B0B8),
                    147 => nameof(Func3_213B0A0),
                    148 => nameof(Func3_213B088),
                    149 => nameof(Func3_213B070),
                    150 => nameof(Func3_213B058),
                    151 => nameof(Func3_213B040),
                    152 => nameof(Func3_213B020),
                    153 => nameof(Func3_213B000),
                    154 => nameof(Func3_213AFE0),
                    155 => nameof(Func3_213AFC0),
                    156 => nameof(Func3_213AFA0),
                    157 => nameof(Func3_213AF80),
                    158 => nameof(Func3_213AF68),
                    159 => nameof(Func3_213AF50),
                    160 => nameof(Func3_213AF38),
                    161 => nameof(Func3_213AF20),
                    162 => nameof(Func3_213AF08),
                    163 => nameof(Func3_213AEF0),
                    164 => nameof(Func3_213AED8),
                    165 => nameof(Func3_213AEC0),
                    166 => nameof(Func3_213AEA8),
                    167 => nameof(Func3_213AE90),
                    168 => nameof(Func3_213AE78),
                    169 => nameof(Func3_213AE60),
                    170 => nameof(Func3_213AE48),
                    171 => nameof(Func3_213AE30),
                    172 => nameof(Func3_213AE14),
                    173 => nameof(Func3_213ADF8),
                    174 => nameof(Func3_213ADC4),
                    175 => nameof(Func3_213ADA0),
                    176 => nameof(Func3_213AD88),
                    177 => nameof(Func3_213AD64),
                    178 => nameof(Func3_213ACE8),
                    179 => nameof(Func3_213ACCC),
                    180 => nameof(Func3_213ACA8),
                    181 => nameof(Func3_213AC8C),
                    182 => nameof(Func3_213AC70),
                    183 => nameof(Func3_213AC54),
                    184 => nameof(Func3_213AC38),
                    185 => nameof(Func3_213AC04),
                    186 => nameof(Func3_213ABC0),
                    187 => nameof(Func3_213AB8C),
                    188 => nameof(Func3_213AB58),
                    189 => nameof(Func3_213AB24),
                    190 => nameof(Func3_213AAF0),
                    191 => nameof(Func3_213AA64),
                    192 => nameof(Func3_213AA20),
                    193 => nameof(Func3_213A9B8),
                    194 => nameof(Func3_213A94C),
                    195 => nameof(Func3_213A938),
                    196 => nameof(Func3_213A91C),
                    197 => nameof(Func3_213A900),
                    198 => nameof(Func3_213A8DC),
                    199 => nameof(Func3_213A8A8),
                    200 => nameof(Func3_213A884),
                    201 => nameof(Func3_213A868),
                    202 => nameof(Func3_213A844),
                    203 => nameof(Func3_213A828),
                    204 => nameof(Func3_213A804),
                    205 => nameof(Func3_213A798),
                    206 => nameof(Func3_213A72C),
                    207 => nameof(Func3_213A714),
                    208 => nameof(Func3_213A698),
                    209 => nameof(Func3_213A688),
                    210 => nameof(Func3_213A660),
                    211 => nameof(Func3_213A650),
                    _ => throw new ProgramException("Invalid AI func 3.")
                };
            }

            public static string GetFuncs4Name(int id)
            {
                // init (f2*4*)
                return id switch
                {
                    0 or 1 or 47 or 49 or 99 => "empty",
                    (>= 2 and <= 44) or 46 or 48 or (>= 50 and <= 78) or (>= 82 and <= 98) or 101 or 103
                        or (>= 106 and <= 113) or (>= 115 and <= 122) => nameof(Func4_21462DC) + "*",
                    45 => nameof(Func4_2145EB0),
                    79 => nameof(Func4_21462AC),
                    80 => nameof(Func4_2146284),
                    81 => nameof(Func4_21461EC),
                    100 => nameof(Func4_214612C),
                    102 => nameof(Func4_2145F78),
                    104 => nameof(Func4_2145F50),
                    105 => nameof(Func4_2145F28),
                    114 => nameof(Func4_2145F00),
                    123 => nameof(Func4_2145E54),
                    124 => nameof(Func4_2145E40),
                    125 => nameof(Func4_SetDespawned),
                    _ => throw new ProgramException("Invalid AI func 4.")
                };
            }

            public static string GetFuncs2Name(int id)
            {
                // proc (f*2*4)
                return id switch
                {
                    0 or 125 => "empty",
                    1 => nameof(Func2_213EA10),
                    (>= 2 and <= 44) or 46 or 48 or (>= 50 and <= 78) or (>= 82 and <= 98) or 101 or 103
                        or (>= 106 and <= 113) or (>= 115 and <= 122) => nameof(Func2_213EA48) + "*",
                    45 => nameof(Func2_213DDCC),
                    47 => nameof(Func2_213DA88),
                    49 => nameof(Func2_213E148),
                    79 => nameof(Func2_213E9C8),
                    80 => nameof(Func2_213E984),
                    81 => nameof(Func2_213E934),
                    99 => nameof(Func2_213E904),
                    100 => nameof(Func2_213E684),
                    102 => nameof(Func2_213E3C4),
                    104 => nameof(Func2_213E31C),
                    105 => nameof(Func2_213E274),
                    114 => nameof(Func2_213E1CC),
                    123 => nameof(Func2_213D9B8),
                    124 => nameof(Func2_213D96C),
                    _ => throw new ProgramException("Invalid AI func 2.")
                };
            }

            #endregion
        }
    }

    [Flags]
    public enum AiFlags2 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4, // has target player?
        Bit3 = 8, // has target halfturret?
        SeekItem = 0x10,
        Bit5 = 0x20,
        Bit6 = 0x40,
        Bit7 = 0x80,
        Bit8 = 0x100,
        Bit9 = 0x200,
        Bit10 = 0x400,
        Bit11 = 0x800,
        Bit12 = 0x1000,
        Bit13 = 0x2000,
        Bit14 = 0x4000,
        Bit15 = 0x8000,
        Bit16 = 0x10000,
        Bit17 = 0x20000,
        Bit18 = 0x40000,
        Bit19 = 0x80000,
        Bit20 = 0x100000,
        Bit21 = 0x200000,
        Unused22 = 0x400000,
        Unused23 = 0x800000,
        Unused24 = 0x1000000,
        Unused25 = 0x2000000,
        Unused26 = 0x4000000,
        Unused27 = 0x8000000,
        Unused28 = 0x10000000,
        Unused29 = 0x20000000,
        Unused30 = 0x40000000,
        Unused31 = 0x80000000
    }

    [Flags]
    public enum AiFlags3 : uint
    {
        None = 0,
        NoInput = 1,
        Bit1 = 2,
        Bit2 = 4,
        Despawned = 8,
        Invulnerable = 0x10,
        Bit5 = 0x20
    }

    [Flags]
    public enum AiFlags4 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8
    }
}
