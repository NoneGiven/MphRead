namespace MphRead.Entities
{
    public class PointModuleEntity : VisibleEntityBase
    {
        private readonly PointModuleEntityData _data;
        private PointModuleEntity? _next;
        public PointModuleEntity? Next => _next;
        private PointModuleEntity? _prev;
        public PointModuleEntity? Prev => _prev;

        private static PointModuleEntity? _current;

        public PointModuleEntity(PointModuleEntityData data) : base(NewEntityType.PointModule)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            NewModel model = Read.GetFhNewModel("pick_morphball");
            _models.Add(model);
            Active = false;
        }

        public override void Init(NewScene scene)
        {
            base.Init(scene);
            if (_data.NextId != 0 && scene.TryGetEntity(_data.NextId, out EntityBase? entity))
            {
                _next = (PointModuleEntity)entity;
            }
            if (_data.PrevId != 0 && scene.TryGetEntity(_data.PrevId, out entity))
            {
                _prev = (PointModuleEntity)entity;
            }
            if (Id == 50)
            {
                SetCurrent();
            }
        }

        public void SetCurrent()
        {
            if (_current != this)
            {
                int i = 0;
                PointModuleEntity? entity = _current;
                while (entity != null && i < 5)
                {
                    entity.SetActive(false);
                    entity = entity.Next;
                    i++;
                }
                i = 0;
                entity = _current = this;
                while (entity != null && i < 5)
                {
                    entity.SetActive(true);
                    entity = entity.Next;
                    i++;
                }
            }
        }

        protected override bool GetModelActive(NewModel model, int index)
        {
            return Active;
        }
    }
}
