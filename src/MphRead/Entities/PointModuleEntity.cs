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
        public static PointModuleEntity? Current => _current;

        public const int StartId = 50;

        public PointModuleEntity(PointModuleEntityData data) : base(NewEntityType.PointModule)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = Read.GetFhNewModel("pick_morphball");
            _models.Add(inst);
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
        }

        public override void Process(NewScene scene)
        {
            base.Process(scene);
            if (_current == null && Id == StartId)
            {
                SetCurrent();
            }
        }

        public void SetCurrent()
        {
            if (_current != this)
            {
                UpdateChain(_current, false);
                _current = this;
                UpdateChain(_current, true);
            }
        }

        private void UpdateChain(PointModuleEntity? entity, bool state)
        {
            int i = 0;
            while (entity != null && i < 5)
            {
                entity.SetActive(state);
                entity = entity.Next;
                i++;
            }
        }

        protected override bool GetModelActive(ModelInstance inst, int index)
        {
            return Active;
        }
    }
}
