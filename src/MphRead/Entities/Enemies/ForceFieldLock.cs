using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy49Entity : EnemyEntity
    {
        private Vector3 _vec1;
        private Vector3 _vec2;

        public Enemy49Entity(EnemyInstanceEntityData data) : base(data)
        {
            var spawner = (ForceFieldEntity)data.Spawner;
            Vector3 position = data.Spawner.Position;
            _vec1 = spawner.Data.Header.UpVector.ToFloatVector();
            _vec2 = spawner.Data.Header.RightVector.ToFloatVector();
            position += _vec2 * Fixed.ToFloat(409);
            ComputeTransform(_vec2, _vec1, position);
            _initialPosition = Position;
            ModelInstance inst = Read.GetNewModel("ForceFieldLock");
            _models.Add(inst);
            Recolor = data.Spawner.Recolor;
        }

        public override void Process(NewScene scene)
        {
            if (Vector3.Dot(scene.CameraPosition - _initialPosition, _vec2) < 0)
            {
                _vec2 *= -1;
                Vector3 position = _initialPosition + _vec2 * Fixed.ToFloat(409);
                ComputeTransform(_vec2, _vec1, position);
            }
            base.Process(scene);
        }
    }
}
