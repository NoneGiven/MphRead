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

        public ForceFieldEntity(ForceFieldEntityData data, Scene scene) : base(EntityType.ForceField, scene)
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
            // todo: node ref
            // todo: room state
            _active = data.Active != 0;
            if (_active)
            {
                // todo: scan ID
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
                _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock, _scene) as Enemy49Entity;
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
                if (!_scene.Multiplayer)
                {
                    if (CameraSequence.Current == null)
                    {
                        PlayerEntity.Main.ForceFieldSfxTimer = 2 / 30f;
                    }
                    else if (Sfx.ForceFieldSfxMute == 0 && _soundSource.CountPlayingSfx(SfxId.GEN_OFF) == 0)
                    {
                        _soundSource.PlayFreeSfx(SfxId.GEN_OFF);
                    }
                    // todo: room state
                }
                _active = false;
                // todo: scan ID
            }
            else if (info.Message == Message.Lock)
            {
                if (!_active && !_scene.Multiplayer && CameraSequence.Current != null
                    && _soundSource.CountPlayingSfx(SfxId.FORCEFIELD_APPEAR) == 0)
                {
                    _soundSource.PlayFreeSfx(SfxId.FORCEFIELD_APPEAR);
                }
                _active = true;
                // todo: scan ID, room state
                if (_lock == null && _data.Type != 9)
                {
                    _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock, _scene) as Enemy49Entity;
                    if (_lock != null)
                    {
                        _scene.AddEntity(_lock);
                    }
                }
            }
        }
    }
}
