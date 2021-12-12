using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ForceFieldEntity : EntityBase
    {
        private readonly ForceFieldEntityData _data;
        private EnemyInstanceEntity? _lock;
        private readonly Vector3 _upVector;
        private readonly Vector3 _facingVector;
        private readonly Vector3 _rightVector;
        private readonly Vector4 _plane;
        private readonly float _width;
        private readonly float _height;
        private bool _active = false; // todo: start replace/removing the Active property on EntityBase

        public ForceFieldEntityData Data => _data;
        public Vector3 UpVector => _upVector;
        public Vector3 FacingVector => _facingVector;
        public Vector3 RightVector => _rightVector;
        public Vector4 Plane => _plane;
        public float Width => _width;
        public float Height => _height;

        public ForceFieldEntity(ForceFieldEntityData data) : base(EntityType.ForceField)
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

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            if (_active && _data.Type != 9)
            {
                _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock);
                if (_lock != null)
                {
                    scene.AddEntity(_lock);
                }
            }
            scene.LoadEffect(77);
        }

        public override bool Process(Scene scene)
        {
            base.Process(scene);
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

        public override void HandleMessage(MessageInfo info, Scene scene)
        {
            if (info.Message == Message.Unlock)
            {
                if (_lock != null)
                {
                    _lock.SetHealth(0);
                    // todo: 1P stuff
                    _active = false;
                    // todo: scan ID
                } 
            }
            else if (info.Message == Message.Lock)
            {
                // todo: SFX
                _active = true;
                // todo: scan ID, room state
                if (_lock == null && _data.Type != 9)
                {
                    _lock = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock);
                    if (_lock != null)
                    {
                        scene.AddEntity(_lock);
                    }
                }
            }
        }
    }
}
