using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public PlayerAiData AiData { get; init; }
        public NodeData3? FieldF20 { get; set; } = null;
        public int BotLevel { get; set; } = 0;

        public class PlayerAiData
        {
            private PlayerEntity _player;
            private Scene _scene;
            private NodeData _nodeData = null!;

            public PlayerAiData(PlayerEntity player)
            {
                _player = player;
                _scene = player._scene;
            }

            public AiPersonalityData1 Personality { get; set; } = null!;
            public bool Flags1 { get; set; }
            public AiFlags2 Flags2 { get; set; }
            public AiFlags3 Flags3 { get; set; }
            public AiFlags4 Flags4 { get; set; }
            public ushort HealthThreshold { get; set; }
            public uint DamageFromHalfturret { get; set; }
            // todo: member name -- set by teleporter
            public int Field118 { get; set; }

            private readonly int[] _slotHits = new int[4];
            private readonly int[] _slotDamage = new int[4];

            private ItemInstanceEntity? _itemC8 = null;
            private OctolithFlagEntity? _octolithFlagCC = null;
            private FlagBaseEntity? _flagBaseD0 = null;
            private OctolithFlagEntity? _octolithFlagD4 = null;
            private FlagBaseEntity? _flagBaseD8 = null;
            private OctolithFlagEntity? _octolithFlagDC = null;
            private FlagBaseEntity? _flagBaseE0 = null;

            private int _field22 = 0;
            private IReadOnlyList<NodeData3> _nodeList = null!;
            private readonly int[] _nodeTypeIndex = new int[6];

            // todo: member names
            private uint _field102C = 0; // random value bounds based on bot level
            private uint _field1030 = 0;
            private int _field116 = 0; // timer?
            private int _field1020 = 0; // timer?
            private NodeData3? _field44 = null;

            public void Reset()
            {
                Debugger.Break();
                _player = null!;
                _scene = null!;
                _nodeData = null!;
                Flags1 = false;
                Flags2 = AiFlags2.None;
                Flags3 = AiFlags3.None;
                HealthThreshold = 0;
                DamageFromHalfturret = 0;
                Field118 = 0;
                Array.Fill(_slotHits, 0);
                Array.Fill(_slotDamage, 0);
                _itemC8 = null;
                _octolithFlagCC = _octolithFlagD4 = _octolithFlagDC = null;
                _flagBaseD0 = _flagBaseD8 = _flagBaseE0 = null;
                _field22 = 0;
                _nodeList = null!;
                Array.Fill(_nodeTypeIndex, 0);
                _field102C = 0;
                _field1030 = 0;
                _field116 = 0;
                for (int i = 0; i < _executionTree.Length; i++)
                {
                    _executionTree[i].Clear();
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

            // todo: member names
            private static int _globalField0 = 0;
            private static int _globalField2 = 0;
            private static readonly AiGlobals[] _globalObjs =
            [
                new AiGlobals(), new AiGlobals(), new AiGlobals(), new AiGlobals()
            ];
            private static readonly byte[,] _globalByteArray = new byte[4, 4];
            private static byte _globaByteF4 = 0;
            private static byte _globalByteF5 = 0;
            private static int _globalIntF8 = 0;

            public class AiGlobals
            {
                public PlayerEntity Player { get; set; } = null!;
                public int Field4 { get; set; }
                public NodeData3 NodeData { get; set; } = null!;
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
                    globals.NodeData = null!;
                }
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        _globalByteArray[i, j] = 0;
                    }
                }
                _globaByteF4 = 1;
                _globalByteF5 = 0;
                _globalIntF8 = 0;
            }

            private void InitializeMain()
            {
                if (_scene.Room?.NodeData == null)
                {
                    return;
                }
                _nodeData = _scene.Room.NodeData;
                SetClosestNodeList(_player.Position);
                if (_scene.GameMode == GameMode.Capture)
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
                else if (_scene.GameMode == GameMode.Bounty || _scene.GameMode == GameMode.BountyTeams)
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
            // todo: member names
            private byte _field2F4 = 0;
            private byte _field2F5 = 0;

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
                _field2F4 = 0;
                _field2F5 = 0;
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
                if (!_scene.Multiplayer && GameState.EncounterState[_player.SlotIndex] != 0)
                {
                    _field102C = 0;
                    _field1030 = 0;
                }
                else
                {
                    // note: the game uses index 1, not index 2, for out-of-range bot levels
                    int index = Math.Clamp(_player.BotLevel, 0, 2);
                    _field102C = _botLevelRandomValues1[index][(int)_player.Hunter];
                    _field1030 = _botLevelRandomValues2[index];
                }
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
                // skhere
            }

            // todo: member name
            private void Func2148ABC()
            {
                // skhere
            }

            // INFO
            // data1 is a tree/graph of AI execution. in steady state, one path is taken through the tree. the tree is initially set up pointing only down
            // the left side (terminating wherever the first node doesn't have any children).
            // execution consists of visiting each node and calling funcs_1[node->data3b], then funcs_2[node->field_0].
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
            // the switch is accompanied by calls to funcs_1[node->data3a] and funcs_4[node->field_0] for the new child and its left-side children.
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
                public int Field28 { get; set; }
                public int Field2C { get; set; }
                public int Field30 { get; set; }
                public Vector3 Field34 { get; set; }
                public int Field40 { get; set; }
                public int Field44 { get; set; }
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
                    Field28 = 0;
                    Field2C = 0;
                    Field30 = 0;
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
            private static readonly HashSet<int> _func4Ids = [ 1, 2, 3 ];

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
                _field44 = null;
                Flags4 &= ~AiFlags4.Bit3;
                Flags2 &= ~AiFlags2.Bit10;
                RemovePlayerFromGlobals(_player);
                if (_func4Ids.Contains(context.Func24Id) && _player.Flags1.TestFlag(PlayerFlags1.Grounded))
                {
                    Flags2 &= ~AiFlags2.Bit7;
                }
                ExecuteFuncs4(context.Func24Id);
                if (data1.Data1.Count > 0 && depth < _maxContextDepth - 1)
                {
                    UpdateExecutionPath(data1.Data1[0], depth + 1);
                }
            }

            private void Execute(AiContext context)
            {
                ExecuteFuncs1(context.Data1.Data3b);
                ExecuteFuncs2(context.Func24Id);
                if (context.Func24Id != 0 && _player.EquipInfo.Weapon.Flags.TestFlag(WeaponFlags.CanZoom)
                    && _buttons.Select.FramesUp > 5 * 2 // todo: FPS stuff
                    && (!_player.EquipInfo.Zoomed && Flags4.TestFlag(AiFlags4.Bit2)
                    || _player.EquipInfo.Zoomed && !Flags4.TestFlag(AiFlags4.Bit2)))
                {
                    _buttons.Select.IsDown = true;
                }
                if (_player.Hunter == Hunter.Spire && !_player.Flags1.TestFlag(PlayerFlags1.AltForm))
                {
                    _field116 = 0;
                }
                if (context.CallCount < 18000)
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
                    context.Weights[weightIndex] += ExecuteFuncs3(context, data2.Func3Id, data2.Parameters);
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
                        Func1_2148C98();
                        break;
                    default:
                        throw new ProgramException("Invalid AI func 1.");
                    }
                }
            }

            // todo: member name
            private void ExecuteFuncs2(int funcId)
            {
                switch (funcId)
                {
                case 0:
                case 125:
                    break;
                case 1:
                    Func2_213EA10();
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
                    Func2_213EA48();
                    break;
                case 45:
                    Func2_213DDCC();
                    break;
                case 47:
                    Func2_213DA88();
                    break;
                case 49:
                    Func2_213E148();
                    break;
                case 79:
                    Func2_213E9C8();
                    break;
                case 80:
                    Func2_213E984();
                    break;
                case 81:
                    Func2_213E934();
                    break;
                case 99:
                    Func2_213E904();
                    break;
                case 100:
                    Func2_213E684();
                    break;
                case 102:
                    Func2_213E3C4();
                    break;
                case 104:
                    Func2_213E31C();
                    break;
                case 105:
                    Func2_213E274();
                    break;
                case 114:
                    Func2_213E1CC();
                    break;
                case 123:
                    Func2_213D9B8();
                    break;
                case 124:
                    Func2_213D96C();
                    break;
                default:
                    throw new ProgramException("Invalid AI func 2.");
                }
            }

            // todo: member name
            private int ExecuteFuncs3(AiContext context, int fundId, AiPersonalityData5 param)
            {
                return fundId switch
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
            private void ExecuteFuncs4(int funcId)
            {
                switch (funcId)
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
                    Func4_21462DC();
                    break;
                case 45:
                    Func4_2145EB0();
                    break;
                case 79:
                    Func4_21462AC();
                    break;
                case 80:
                    Func4_2146284();
                    break;
                case 81:
                    Func4_21461EC();
                    break;
                case 100:
                    Func4_214612C();
                    break;
                case 102:
                    Func4_2145F78();
                    break;
                case 104:
                    Func4_2145F50();
                    break;
                case 105:
                    Func4_2145F28();
                    break;
                case 114:
                    Func4_2145F00();
                    break;
                case 123:
                    Func4_2145E54();
                    break;
                case 124:
                    Func4_2145E40();
                    break;
                case 125:
                    Func4_2145E2C();
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
                // skhere
            }

            private void Func1_2149C50()
            {
                // skhere
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

            private void Func1_2149AD8()
            {
                // skhere
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
                // skhere
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
                // skhere
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
                // skhere
            }

            private void Func1_21491E4()
            {
                // skhere
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
                // skhere
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
                // skhere
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
                // skhere
            }

            private void Func1_2148E88()
            {
                // skhere
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
                // skhere
            }

            private void Func1_2148C98()
            {
                // skhere
            }

            #endregion

            // skhere
            // todo: member names
            #region Funcs2

            private void Func2_213EA10()
            {
                // skhere
            }

            private void Func2_213EA48()
            {
                // skhere
            }

            private void Func2_213DDCC()
            {
                // skhere
            }

            private void Func2_213DA88()
            {
                // skhere
            }

            private void Func2_213E148()
            {
                // skhere
            }

            private void Func2_213E9C8()
            {
                // skhere
            }

            private void Func2_213E984()
            {
                // skhere
            }

            private void Func2_213E934()
            {
                // skhere
            }

            private void Func2_213E904()
            {
                // skhere
            }

            private void Func2_213E684()
            {
                // skhere
            }

            private void Func2_213E3C4()
            {
                // skhere
            }

            private void Func2_213E31C()
            {
                // skhere
            }

            private void Func2_213E274()
            {
                // skhere
            }

            private void Func2_213E1CC()
            {
                // skhere
            }

            private void Func2_213D9B8()
            {
                // skhere
            }

            private void Func2_213D96C()
            {
                // skhere
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
                // skhere
                return 0;
            }

            private int Func3_213D814(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D7F0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D800(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
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
                // skhere
                return 0;
            }

            private int Func3_213D608(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D564(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D540(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D530(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213D514(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
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
                // skhere
                return 0;
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
                // skhere
                return 0;
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
                // skhere
                return 0;
            }

            private int Func3_213CA58(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CA2C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213CA00(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C9D4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213C9C4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
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
                // skhere
                return 0;
            }

            private int Func3_213BCC4(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
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
                // skhere
                return 0;
            }

            private int Func3_213ACA8(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AC8C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AC70(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AC54(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AC38(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AC04(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213ABC0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AB8C(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AB58(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AB24(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213AAF0(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
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
                // skhere
                return 0;
            }

            private int Func3_213A660(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            private int Func3_213A650(AiContext context, AiPersonalityData5 param)
            {
                // skhere
                return 0;
            }

            #endregion

            // skhere
            // todo: member names
            #region Funcs4

            private void Func4_21462DC()
            {
                // skhere
            }

            private void Func4_2145EB0()
            {
                // skhere
            }

            private void Func4_21462AC()
            {
                // skhere
            }

            private void Func4_2146284()
            {
                // skhere
            }

            private void Func4_21461EC()
            {
                // skhere
            }

            private void Func4_214612C()
            {
                // skhere
            }

            private void Func4_2145F78()
            {
                // skhere
            }

            private void Func4_2145F50()
            {
                // skhere
            }

            private void Func4_2145F28()
            {
                // skhere
            }

            private void Func4_2145F00()
            {
                // skhere
            }

            private void Func4_2145E54()
            {
                // skhere
            }

            private void Func4_2145E40()
            {
                // skhere
            }

            private void Func4_2145E2C()
            {
                // skhere
            }

            #endregion

            private void SetClosestNodeList(Vector3 position)
            {
                Flags2 &= ~AiFlags2.Bit7;
                IReadOnlyList<IReadOnlyList<NodeData3>> data1 = _nodeData.Data[_field22];
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
                    ushort type = _nodeList[i].NodeType;
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

            private void RemovePlayerFromGlobals(PlayerEntity player)
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
                    current.NodeData = next.NodeData;
                }
                _globalField2--;
            }
        }
    }

    [Flags]
    public enum AiFlags2 : uint
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
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
        Bit3 = 8,
        Bit4 = 0x10,
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

    public static class ReadBotAi
    {
        // copied Kanden 0 for Samus and Guardian 0 for Guardian, but they're unused anyway
        private static readonly IReadOnlyList<IReadOnlyList<int>> _encounterAiOffsets =
        [
            //                  Sam    Kan    Tra    Syl    Nox    Spi    Wea    Gua
            /* encounter 0 */ [ 33152, 33152, 33696, 33836, 33556, 33372, 33976, 13480 ],
            /* encounter 1 */ [ 33152, 33196, 37576, 41948, 35428, 33416, 41492, 13480 ],
            /* encounter 3 */ [ 33152, 33152, 39420, 42772, 33556, 40312, 33976, 13480 ],
            /* encounter 4 */ [ 33152, 33152, 33696, 45176, 33556, 40556, 33976, 13480 ]
        ];

        public static void LoadAll(GameMode mode)
        {
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.AiData.Reset();
                if (!player.IsBot)
                {
                    continue;
                }
                int aiOffset = 32896; // default, Battle, BattleTeams
                if (mode == GameMode.SinglePlayer)
                {
                    int encounterState = GameState.EncounterState[i];
                    if (player.Hunter == Hunter.Guardian)
                    {
                        aiOffset = encounterState == 2 ? 32932 : 13480;
                    }
                    else
                    {
                        if (encounterState == 2)
                        {
                            aiOffset = 33232;
                        }
                        else
                        {
                            int index = encounterState switch
                            {
                                1 => 1,
                                3 => 2,
                                4 => 3,
                                _ => 0
                            };
                            // todo?: if replacing enemy hunters, consider loading the offset belonging to the one replaced
                            aiOffset = _encounterAiOffsets[index][(int)player.Hunter];
                        }
                        player.AiData.Flags1 = true;
                    }
                }
                else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    aiOffset = 45696;
                }
                else if (mode == GameMode.Capture
                    || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
                {
                    aiOffset = 32968;
                }
                else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams
                    || mode == GameMode.Defender || mode == GameMode.DefenderTeams)
                {
                    aiOffset = 33012;
                }
                else if (mode == GameMode.PrimeHunter)
                {
                    aiOffset = 45220;
                }
                player.AiData.Personality = LoadData(aiOffset);
            }
        }

        private static string _cachedVersion = "";
        private static byte[]? _aiPersonalityData = null;

        private static AiPersonalityData1 LoadData(int offset)
        {
            if (Paths.MphKey != _cachedVersion)
            {
                _aiPersonalityData = null;
                _data1Cache.Clear();
                _data2Cache.Clear();
                _cachedVersion = Paths.MphKey;
            }
            if (_aiPersonalityData == null)
            {
                _aiPersonalityData = File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin"));
            }
            return ParseData1(offset, count: 1)[0];
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData1>> _data1Cache = [];
        private static readonly Dictionary<int, IReadOnlyList<int>> _data3Cache = [];

        private static IReadOnlyList<AiPersonalityData1> ParseData1(int offset, int count)
        {
            if (_data1Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData1>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData1>(count);
            IReadOnlyList<AiData1> data1s = Read.DoOffsets<AiData1>(bytes, offset, count);
            for (int i = 0; i < data1s.Count; i++)
            {
                AiData1 data1 = data1s[i];
                IReadOnlyList<AiPersonalityData1> data1Children = [];
                if (data1.Data1Count > 0 && data1.Data1Offset != offset)
                {
                    data1Children = ParseData1(data1.Data1Offset, data1.Data1Count);
                }
                IReadOnlyList<AiPersonalityData2> data2 = [];
                if (data1.Data2Count > 0)
                {
                    data2 = ParseData2(data1.Data2Offset, data1.Data2Count);
                }
                IReadOnlyList<int> data3a = [];
                if (data1.Data3aCount > 0)
                {
                    if (_data3Cache.TryGetValue(data1.Data3aOffset, out IReadOnlyList<int>? cached3a))
                    {
                        data3a = cached3a;
                    }
                    else
                    {
                        data3a = Read.DoOffsets<int>(bytes, data1.Data3aOffset, data1.Data3aCount);
                        _data3Cache.Add(data1.Data3aOffset, data3a);
                    }
                }
                IReadOnlyList<int> data3b = [];
                if (data1.Data3bCount > 0)
                {
                    if (_data3Cache.TryGetValue(data1.Data3bOffset, out IReadOnlyList<int>? cached3b))
                    {
                        data3b = cached3b;
                    }
                    else
                    {
                        data3b = Read.DoOffsets<int>(bytes, data1.Data3bOffset, data1.Data3bCount);
                        _data3Cache.Add(data1.Data3bOffset, data3b);
                    }
                }
                results.Add(new AiPersonalityData1(data1.Field0, data1Children, data2, data3a, data3b));
            }
            _data1Cache.Add(offset, results);
            return results;
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData2>> _data2Cache = [];

        private static IReadOnlyList<AiPersonalityData2> ParseData2(int offset, int count)
        {
            if (_data2Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData2>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData2>(count);
            IReadOnlyList<AiData2> data2s = Read.DoOffsets<AiData2>(bytes, offset, count);
            for (int i = 0; i < data2s.Count; i++)
            {
                AiData2 data2 = data2s[i];
                IReadOnlyList<AiPersonalityData4> data4 = [];
                if (data2.Data4Count > 0)
                {
                    data4 = ParseData4(data2.Data4Offset, data2.Data4Count);
                }
                AiPersonalityData5 data5 = _emptyParams;
                if (data2.Data5Offset != 0)
                {
                    data5 = ParseData5(data2.Data5Type, data2.Data5Offset);
                }
                results.Add(new AiPersonalityData2(data2.FieldC, data2.Field10, data4, data2.Data5Type, data5));
            }
            _data2Cache.Add(offset, results);
            return results;
        }

        private static readonly Dictionary<int, IReadOnlyList<AiPersonalityData4>> _data4Cache = [];

        private static IReadOnlyList<AiPersonalityData4> ParseData4(int offset, int count)
        {
            if (_data4Cache.TryGetValue(offset, out IReadOnlyList<AiPersonalityData4>? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            var results = new List<AiPersonalityData4>(count);
            IReadOnlyList<AiData4> data4s = Read.DoOffsets<AiData4>(bytes, offset, count);
            for (int i = 0; i < data4s.Count; i++)
            {
                AiData4 data4 = data4s[i];
                AiPersonalityData5 data5 = _emptyParams;
                if (data4.Data5Offset != 0)
                {
                    data5 = ParseData5(data4.Data5Type, data4.Data5Offset);
                }
                results.Add(new AiPersonalityData4(data4.Data5Type, data5));
            }
            _data4Cache.Add(offset, results);
            return results;
        }

        private static readonly AiPersonalityData5 _emptyParams = new AiPersonalityData5();
        private static readonly Dictionary<int, AiPersonalityData5> _data5Cache = [];

        private static AiPersonalityData5 ParseData5(int type, int offset)
        {
            if (_data5Cache.TryGetValue(offset, out AiPersonalityData5? cached))
            {
                return cached;
            }
            var bytes = new ReadOnlySpan<byte>(_aiPersonalityData);
            uint param1 = Read.SpanReadUint(bytes, offset);
            uint param2 = type == 210 ? Read.SpanReadUint(bytes, offset + 4) : 0;
            return new AiPersonalityData5(param1, param2);
        }

        // skdebug
        public static void TestRead()
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Paths.Combine(Paths.FileSystem, @"aiPersonalityData\aiPersonalityData.bin")));
            var offsets = new List<int>()
            {
                13480, 32896, 32932, 32968, 33012, 33152, 33196, 33232, 33372, 33416, 33556, 33696, 33836,
                33976, 35428, 37576, 39420, 40312, 40556, 41492, 41948, 42772, 45176, 45220, 45696
            };
            var results = new List<AiPersonalityData1>();
            foreach (int offset in offsets)
            {
                results.Add(LoadData(offset));
            }
            _ = 5;
            _ = 5;
        }

        // size: 36
        public readonly struct AiData1
        {
            public readonly int Field0;
            public readonly int Data1Count;
            public readonly int Data1Offset;
            public readonly int Data2Count;
            public readonly int Data2Offset;
            public readonly int Data3aCount;
            public readonly int Data3aOffset;
            public readonly int Data3bCount;
            public readonly int Data3bOffset;
        }

        // size: 24
        public readonly struct AiData2
        {
            public readonly int Data5Type;
            public readonly int Data4Count;
            public readonly int Data4Offset;
            public readonly int FieldC;
            public readonly int Field10;
            public readonly int Data5Offset;
        }

        // size: 8
        public readonly struct AiData4
        {
            public readonly int Data5Type;
            public readonly int Data5Offset;
        }
    }

    public class AiPersonalityData1
    {
        public int Func24Id { get; init; }
        public IReadOnlyList<AiPersonalityData1> Data1 { get; init; }
        public IReadOnlyList<AiPersonalityData2> Data2 { get; init; }
        public IReadOnlyList<int> Data3a { get; init; }
        public IReadOnlyList<int> Data3b { get; init; }

        public AiPersonalityData1(int field0, IReadOnlyList<AiPersonalityData1> data1,
            IReadOnlyList<AiPersonalityData2> data2, IReadOnlyList<int> data3a, IReadOnlyList<int> data3b)
        {
            Func24Id = field0;
            Data1 = data1;
            Data2 = data2;
            Data3a = data3a;
            Data3b = data3b;
        }
    }

    public class AiPersonalityData2
    {
        public int Data1SelectIndex { get; init; }
        public int Field10 { get; init; }
        public IReadOnlyList<AiPersonalityData4> Data4 { get; init; }
        public int Func3Id { get; init; }
        public AiPersonalityData5 Parameters { get; init; }

        public AiPersonalityData2(int fieldC, int field10, IReadOnlyList<AiPersonalityData4> data4,
            int data5Type, AiPersonalityData5 data5)
        {
            Data1SelectIndex = fieldC;
            Field10 = field10;
            Data4 = data4;
            Func3Id = data5Type;
            Parameters = data5;
        }
    }

    public class AiPersonalityData4
    {
        public int Func3Id { get; init; }
        public AiPersonalityData5 Parameters { get; init; }

        public AiPersonalityData4(int data5Type, AiPersonalityData5 data5)
        {
            Func3Id = data5Type;
            Parameters = data5;
        }
    }

    public class AiPersonalityData5
    {
        public uint Param1 { get; init; }
        public uint Param2 { get; init; }
        // skdebug: see if an empty one ever gets used for parameters, since it would be a null ref in-game
        public bool IsEmpty { get; init; }

        public AiPersonalityData5()
        {
            IsEmpty = true;
        }

        public AiPersonalityData5(uint param1, uint param2)
        {
            Param1 = param1;
            Param2 = param2;
        }
    }
}
