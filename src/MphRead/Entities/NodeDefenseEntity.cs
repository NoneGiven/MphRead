using System.Diagnostics;
using System.Linq;
using MphRead.Hud;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class NodeDefenseEntity : EntityBase
    {
        private readonly NodeDefenseEntityData _data;
        private readonly Matrix4 _circleScale;
        private readonly CollisionVolume _volume;
        private readonly bool _defender = false;

        private int _currentTeam = 4;
        private int _occupyingTeam = 4;
        private readonly bool[] _occupiedBy = new bool[4];
        private float _blinkTimer = 0;
        private PlayerEntity? _capturedPlayer = null;
        private float _progress = 0;
        private float _scoreTimer = 0;
        private float _curRotation = 0;
        private float _spinSpeed = 0;
        private bool _contested = false;
        private bool _bit1 = false;

        private readonly Material _terminalMat = null!;
        private readonly Material _ringMat = null!;

        public NodeDefenseEntity(NodeDefenseEntityData data, Scene scene) : base(EntityType.NodeDefense, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            _volume = CollisionVolume.Move(_data.Volume, Position);
            GameMode mode = scene.GameMode;
            if (mode == GameMode.Defender || mode == GameMode.DefenderTeams
                || mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                // yes, these names are correct
                ModelInstance terminalInst = SetUpModel("koth_data_flow");
                ModelInstance ringInst = SetUpModel("koth_terminal");
                float scale = data.Volume.CylinderRadius.FloatValue;
                _circleScale = Matrix4.CreateScale(scale);
                _terminalMat = terminalInst.Model.Materials.First(m => m.Name == "lambert4");
                _ringMat = ringInst.Model.Materials.First(m => m.Name == "lambert2");
            }
            if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                _defender = true;
            }
        }

        public override bool Process()
        {
            if (_defender)
            {
                ProcessDefender();
            }
            else
            {
                ProcessNodes();
            }
            return true;
        }

        private void ProcessDefender()
        {
            int team = 4;
            _contested = false;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health > 0 && _volume.TestPoint(player.Volume.SpherePosition))
                {
                    if (team == 4)
                    {
                        team = player.TeamIndex;
                    }
                    else if (team != player.TeamIndex)
                    {
                        _contested = true;
                    }
                }
            }
            if (_contested)
            {
                team = 4;
            }
            float speed;
            float rotation;
            if (team == 4)
            {
                (speed, rotation) = ConstantAcceleration(-0.25f, _spinSpeed, minVelocity: 0);
            }
            else
            {
                (speed, rotation) = ConstantAcceleration(0.25f, _spinSpeed, maxVelocity: 8 * 30f);
                GameState.DefenseTime[team] += _scene.FrameTime;
            }
            _spinSpeed = speed;
            _curRotation += rotation;
            if (_curRotation >= 360)
            {
                _curRotation -= 360;
            }
            _currentTeam = team;
        }

        private void ProcessNodes()
        {
            int value1 = 0;
            int value2 = 0;
            bool[] prevOccupiedBy = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                prevOccupiedBy[i] = _occupiedBy[i];
                _occupiedBy[i] = false;
            }
            _contested = false;
            int slot = 0;
            bool occupiedByAny = false;
            // todo: update positional audio
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health > 0 && _volume.TestPoint(player.Volume.SpherePosition))
                {
                    if (_occupyingTeam == player.TeamIndex)
                    {
                        _occupiedBy[player.SlotIndex] = true;
                        occupiedByAny = true;
                        slot = player.SlotIndex;
                    }
                    if (_occupyingTeam == 4 && _currentTeam != player.TeamIndex)
                    {
                        _occupiedBy[player.SlotIndex] = true;
                        occupiedByAny = true;
                        _occupyingTeam = player.TeamIndex;
                        _progress = 0;
                        _bit1 = false;
                        slot = player.SlotIndex;
                    }
                    else if (_occupyingTeam != player.TeamIndex)
                    {
                        _contested = true;
                    }
                }
            }
            float rotation = 0;
            if (occupiedByAny)
            {
                if (_contested)
                {
                    if (_occupiedBy[PlayerEntity.Main.SlotIndex])
                    {
                        // todo: update music
                    }
                }
                else if (_currentTeam != _occupyingTeam)
                {
                    if (_occupiedBy[PlayerEntity.Main.SlotIndex])
                    {
                        if (!_bit1 && _progress >= 10 / 30f)
                        {
                            // todo: update music
                            value1 = 1;
                            _bit1 = true;
                        }
                        // todo: update music
                    }
                    _progress += _scene.FrameTime;
                    float spinSpeed = _progress / (300 / 30f) * (15 * 30f);
                    rotation = _spinSpeed * _scene.FrameTime + (spinSpeed - _spinSpeed) / 2 * _scene.FrameTime;
                    _spinSpeed = spinSpeed;
                    if (_progress >= 300 / 30f)
                    {
                        Complete(ref value1, ref value2);
                        occupiedByAny = false;
                    }
                }
            }
            else
            {
                if (_occupiedBy[PlayerEntity.Main.SlotIndex])
                {
                    // todo: update music
                    if (value1 != 2)
                    {
                        value1 = 3;
                    }
                    _currentTeam = 4;
                    _progress = 0;
                    _bit1 = false;
                    (_spinSpeed, rotation) = ConstantAcceleration(-0.15f, _spinSpeed, minVelocity: 0);
                }
            }
            int nodeCount = 0;
            int team = _currentTeam;
            float scoreThreshold = 150 / 30f;
            if (team == 4)
            {
                team = _occupyingTeam;
            }
            if (team != 4)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.NodeDefense)
                    {
                        continue;
                    }
                    var node = (NodeDefenseEntity)entity;
                    if (node._currentTeam == team && node._occupyingTeam == 4)
                    {
                        nodeCount++;
                        if (nodeCount > 1)
                        {
                            scoreThreshold -= 45 / 30f;
                        }
                    }
                }
            }
            if (_currentTeam != 4 && !occupiedByAny)
            {
                _scoreTimer += _scene.FrameTime;
                if (_scoreTimer >= scoreThreshold)
                {
                    Debug.Assert(_capturedPlayer != null);
                    GameState.Points[_capturedPlayer.SlotIndex]++;
                    _scoreTimer = 0;
                }
                if (nodeCount == 1)
                {
                    // todo: play SFX
                }
                else if (nodeCount > 1)
                {
                    // todo: play SFX
                }
            }
            if (value2 != 0 && nodeCount >= 2)
            {
                value1 = 5;
            }
            if (value1 != 0)
            {
                // todo: update SFX
            }
            float prevRotation = _curRotation;
            _curRotation += rotation;
            if (_curRotation >= 360)
            {
                _curRotation -= 360;
            }
            if (_blinkTimer > 0)
            {
                _blinkTimer -= _scene.FrameTime;
            }
            if (occupiedByAny && Fixed.ToInt(_curRotation) / 61440 != Fixed.ToInt(prevRotation) / 61440)
            {
                _blinkTimer = 1 / 30f;
            }
        }

        private void Complete(ref int dest1, ref int dest2)
        {
            if (_currentTeam == PlayerEntity.Main.TeamIndex)
            {
                dest1 = 4;
                string msg = Text.Strings.GetHudMessage(211); // node stolen
                PlayerEntity.Main.QueueHudMessage(128, 133, TextType.Centered, 256, 8, new ColorRgba(31), 1, 90 / 30f, 17, msg);
            }
            for (int i = 0; i < 4; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (_occupiedBy[i])
                {
                    GameState.NodesCaptured[i]++;
                    if (player.LoadFlags.TestFlag(LoadFlags.Active))
                    {
                        _capturedPlayer = player;
                    }
                }
                else if (_currentTeam == player.TeamIndex)
                {
                    GameState.NodesLost[i]++;
                }
                _occupiedBy[i] = false;
            }
            if (_capturedPlayer == PlayerEntity.Main)
            {
                PlayerEntity.Main.QueueHudMessage(128, 133, 90 / 30f, 1, 206); // complete
            }
            _currentTeam = _occupyingTeam;
            _progress = 0;
            _bit1 = false;
            _occupyingTeam = 4;
            _scoreTimer = 150 / 30f;
            if (_currentTeam == PlayerEntity.Main.TeamIndex)
            {
                // todo: update music
                dest1 = 2;
            }
            else
            {
                dest2 = 1;
            }
            _blinkTimer = 0;
        }

        private static readonly ColorRgb _neutralColor = new ColorRgb(31, 31, 31);
        private static readonly ColorRgb _selfColor = new ColorRgb(15, 15, 31);
        private static readonly ColorRgb _enemyColor = new ColorRgb(31, 0, 0);

        // todo: is_visible
        public override void GetDrawInfo()
        {
            bool blinking = _blinkTimer > 0;
            ColorRgb color = _neutralColor;
            if (_currentTeam == 4)
            {
                if (blinking)
                {
                    if (GameState.Teams)
                    {
                        color = Metadata.TeamColors[_occupyingTeam];
                    }
                    else if (_occupyingTeam == PlayerEntity.Main.TeamIndex)
                    {
                        color = _selfColor;
                    }
                    else
                    {
                        color = _enemyColor;
                    }
                }
            }
            else if (GameState.Teams)
            {
                if (blinking)
                {
                    color = Metadata.TeamColors[_occupyingTeam];
                }
                else
                {
                    color = Metadata.TeamColors[_currentTeam];
                }
            }
            else
            {
                if (_currentTeam == PlayerEntity.Main.TeamIndex)
                {
                    if (!blinking || _occupyingTeam == PlayerEntity.Main.TeamIndex)
                    {
                        color = _selfColor;
                    }
                    else
                    {
                        color = _enemyColor;
                    }
                }
                else if (blinking && _occupyingTeam == PlayerEntity.Main.TeamIndex)
                {
                    color = _selfColor;
                }
                else
                {
                    color = _enemyColor;
                }
            }
            _terminalMat.Diffuse = color;
            _ringMat.Diffuse = color;
            base.GetDrawInfo();
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 1)
            {
                var rotY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_curRotation));
                transform = _circleScale * rotY * transform;
                transform.Row3.Xyz = Position.AddY(0.7f);
            }
            return transform;
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.DefenseNode)
            {
                AddVolumeItem(_volume, Vector3.One);
            }
        }
    }
}
