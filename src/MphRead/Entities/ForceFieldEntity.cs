using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ForceFieldEntity : EntityBase
    {
        private readonly ForceFieldEntityData _data;
        private bool _lockSpawned = false;
        private readonly Vector3 _upVector;
        private readonly Vector3 _facingVector;
        private readonly Vector3 _rightVector;
        private readonly float _width;
        private readonly float _height;

        public ForceFieldEntityData Data => _data;
        public Vector3 UpVector => _upVector;
        public Vector3 FacingVector => _facingVector;
        public Vector3 RightVector => _rightVector;
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
            Recolor = Metadata.DoorPalettes[(int)data.Type];
            ModelInstance inst = SetUpModel("ForceField");
            Read.GetModelInstance("ForceFieldLock"); // todo: init needed effects and stuff -- other entities too
            // todo: fade in/out "animation"
            Active = data.Active != 0;
            if (!Active)
            {
                inst.Active = false;
            }
        }

        public override bool Process(Scene scene)
        {
            // todo: despawn when deactivated/destroyed
            if (Active && _data.Type != 9 && !_lockSpawned)
            {
                EnemyInstanceEntity? enemy = EnemySpawnEntity.SpawnEnemy(this, EnemyType.ForceFieldLock);
                if (enemy != null)
                {
                    scene.AddEntity(enemy);
                    _lockSpawned = true;
                }
            }
            return base.Process(scene);
        }
    }
}
