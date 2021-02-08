using OpenTK.Mathematics;

namespace MphRead.Entities
{
    // ntodo: enemy entities
    public class ForceFieldEntity : VisibleEntityBase
    {
        private readonly ForceFieldEntityData _data;
        private readonly NewModel _enemyModel;

        public ForceFieldEntity(ForceFieldEntityData data) : base(NewEntityType.ForceField)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            Scale = new Vector3(data.Width.FloatValue, data.Height.FloatValue, 1.0f);
            Recolor = Metadata.DoorPalettes[(int)data.Type];
            NewModel model = Read.GetNewModel("ForceField");
            model.Active = data.Active != 0;
            _models.Add(model);
            // ntodo: enemy entities
            _enemyModel = Read.GetNewModel("ForceFieldLock");
            //Vector3 position = model.Position;
            //position.X += Fixed.ToFloat(409) * vec2.X;
            //position.Y += Fixed.ToFloat(409) * vec2.Y;
            //position.Z += Fixed.ToFloat(409) * vec2.Z;
            //enemy.Position = enemy.InitialPosition = position;
            //enemy.Vector1 = vec1;
            //enemy.Vector2 = vec2;
            //ComputeModelMatrices(enemy, vec2, vec1);
            //ComputeNodeMatrices(enemy, index: 0);
            //if (data.Active == 0 || data.Type == 9)
            //{
            //    enemy.Active = false;
            //}
            //_models.Add(enemy);
        }
    }
}
