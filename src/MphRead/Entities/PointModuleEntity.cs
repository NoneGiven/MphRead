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

        public PointModuleEntity(PointModuleEntityData data, Scene scene) : base(EntityType.PointModule, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            ModelInstance inst = SetUpModel("pick_morphball", firstHunt: true);
            Active = false;
            inst.Active = false;
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_data.NextId != 0 && _scene.TryGetEntity(_data.NextId, out EntityBase? entity))
            {
                Next = (PointModuleEntity)entity;
            }
            if (_data.PrevId != 0 && _scene.TryGetEntity(_data.PrevId, out entity))
            {
                Prev = (PointModuleEntity)entity;
            }
        }

        public override bool Process()
        {
            if (_current == null && Id == StartId)
            {
                SetCurrent();
            }
            return base.Process();
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
