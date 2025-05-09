using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities.Enemies;
using MphRead.Formats;
using MphRead.Sound;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ForceFieldEntity : EntityBase
    {
        private readonly ForceFieldEntityData _data;
        private Enemy49Entity? _lock;
        private readonly Vector3 _upVector;
        private readonly Vector3 _facingVector;
        private readonly Vector3 _rightVector;
        private readonly Vector4 _plane;
        private readonly float _width;
        private readonly float _height;
        private bool _active = false; // todo: start replace/removing the Active property on EntityBase

        public ForceFieldEntityData Data => _data;
        public Vector3 FieldUpVector => _upVector;
        public Vector3 FieldFacingVector => _facingVector;
        public Vector3 FieldRightVector => _rightVector;
        public Vector4 Plane => _plane;
        public float Width => _width;
        public float Height => _height;

        public new bool Active => _active;
        public Enemy49Entity? Lock => _lock;

        private static readonly IReadOnlyList<int> _scanIds = new int[10]
        {
            0, 294, 295, 291, 290, 292, 293, 296, 0, 267
        };

        public ForceFieldEntity(ForceFieldEntityData data, string nodeName, Scene scene)
            : base(EntityType.ForceField, nodeName, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _upVector = data.Header.UpVector.ToFloatVector();
            _facingVector = data.Header.FacingVector.ToFloatVector();
            _rightVector = Vector3.Cross(_upVector, _facingVector).Normalized();
            _width = data.Width.FloatValue;
            _height = data.Height.FloatValue;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            Scale = new Vector3(_width, _height, 1.0f);
            Debug.Assert(GameState.Mode == GameMode.SinglePlayer);
            int state = GameState.StorySave.InitRoomState(_scene.RoomId, Id, active: _data.Active != 0);
            _active = state != 0;
            if (_active)
            {
                _scanId = _scanIds[(int)data.Type];
            }
            else
            {
                Alpha = 0;
            }
            _plane = new Vector4(_facingVector, Vector3.Dot(_facingVector, Position));
            Recolor = Metadata.DoorPalettes[(int)data.Type];
            ModelInstance inst = SetUpModel("ForceField");
            Read.GetModelInstance("ForceFieldLock");
            inst.SetAnimation(0);
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_active && _data.Type != 9)
            {
                _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock, NodeRef, _scene) as Enemy49Entity;
                if (_lock != null)
                {
                    _scene.AddEntity(_lock);
                }
            }
            _scene.LoadEffect(77); // deathMech1
        }

        public override bool Process()
        {
            base.Process();
            if (_active)
            {
                if (Alpha < 1)
                {
                    Alpha += 1f / 31f / 2f;
                    if (Alpha > 1)
                    {
                        Alpha = 1;
                    }
                }
            }
            else if (Alpha > 0)
            {
                Alpha -= 1f / 31f / 2f;
                if (Alpha < 0)
                {
                    Alpha = 0;
                }
            }
            return true;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Unlock)
            {
                if (_lock != null)
                {
                    _lock.SetHealth(0);
                }
                if (GameState.SinglePlayer)
                {
                    if (CameraSequence.Current == null)
                    {
                        PlayerEntity.Main.ForceFieldSfxTimer = 2 / 30f;
                    }
                    else if (Sfx.ForceFieldSfxMute == 0 && _soundSource.CountPlayingSfx(SfxId.GEN_OFF) == 0)
                    {
                        _soundSource.PlayFreeSfx(SfxId.GEN_OFF);
                    }
                    GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
                }
                _active = false;
                _scanId = 0;
            }
            else if (info.Message == Message.Lock)
            {
                if (!_active && GameState.SinglePlayer && CameraSequence.Current != null
                    && _soundSource.CountPlayingSfx(SfxId.FORCEFIELD_APPEAR) == 0)
                {
                    _soundSource.PlayFreeSfx(SfxId.FORCEFIELD_APPEAR);
                }
                _active = true;
                if (_data.Type == 9)
                {
                    // bugfix?: this is a different result than when first created
                    _scanId = 0;
                }
                else
                {
                    _scanId = _scanIds[(int)_data.Type];
                }
                GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
                if (_lock == null && _data.Type != 9)
                {
                    _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock, NodeRef, _scene) as Enemy49Entity;
                    if (_lock != null)
                    {
                        _scene.AddEntity(_lock);
                    }
                }
            }
        }

        public override void GetDrawInfo()
        {
            if (Alpha > 0 && IsVisible(NodeRef))
            {
                base.GetDrawInfo();
            }
        }
    }
}
