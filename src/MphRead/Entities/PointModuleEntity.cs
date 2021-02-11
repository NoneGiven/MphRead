namespace MphRead.Entities
{
    public class PointModuleEntity : EntityBase
    {
        private readonly PointModuleEntityData _data;
        public PointModuleEntity? Next { get; private set; }
        public PointModuleEntity? Prev { get; private set; }

        private static PointModuleEntity? _current;
        public static PointModuleEntity? Current => _current;

        public const int StartId = 50;

        public PointModuleEntity(PointModuleEntityData data) : base(EntityType.PointModule)
        {
            _data = data;
            Id = data.Header.EntityId;
            ComputeTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = Read.GetModelInstance("pick_morphball", firstHunt: true);
            _models.Add(inst);
            Active = false;
            _models[0].Active = false;
        }

        public override void Init(Scene scene)
        {
            base.Init(scene);
            if (_data.NextId != 0 && scene.TryGetEntity(_data.NextId, out EntityBase? entity))
            {
                Next = (PointModuleEntity)entity;
            }
            if (_data.PrevId != 0 && scene.TryGetEntity(_data.PrevId, out entity))
            {
                Prev = (PointModuleEntity)entity;
            }
        }

        public override void Process(Scene scene)
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

        public override void SetActive(bool active)
        {
            base.SetActive(active);
            _models[0].Active = Active;
        }

        public override EntityBase? GetParent()
        {
            return Prev;
        }

        public override EntityBase? GetChild()
        {
            return Next;
        }
    }
}
