using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ArtifactEntity : EntityBase
    {
        private readonly ArtifactEntityData _data;
        private readonly float _heightOffset;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Vector3 _invPos;
        private EntityBase? _msgTarget1 = null;
        private EntityBase? _msgTarget2 = null;
        private EntityBase? _msgTarget3 = null;

        public new bool Active { get; set; }

        public ArtifactEntity(ArtifactEntityData data, Scene scene) : base(EntityType.Artifact, scene)
        {
            // todo: load resources for simple octolith/dropped octolith when needed
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            ModelInstance inst = SetUpModel(name);
            _heightOffset = data.ModelId >= 8 ? 1.75f : inst.Model.Nodes[0].BoundingRadius;
            if (data.HasBase != 0)
            {
                SetUpModel("ArtifactBase");
            }
            // todo: room state
            Active = _data.Active != 0;
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_data.LinkedEntityId != -1)
            {
                if (_scene.TryGetEntity(_data.LinkedEntityId, out EntityBase? parent))
                {
                    _parent = parent;
                }
            }
            if (_scene.TryGetEntity(_data.Message1Target, out EntityBase? target))
            {
                _msgTarget1 = target;
            }
            if (_scene.TryGetEntity(_data.Message2Target, out target))
            {
                _msgTarget2 = target;
            }
            if (_scene.TryGetEntity(_data.Message3Target, out target))
            {
                _msgTarget3 = target;
            }
        }

        public override bool Process()
        {
            if (_parent != null)
            {
                if (!_invSetUp)
                {
                    _parent.GetDrawInfo(); // force update transforms
                    _invPos = Matrix.Vec3MultMtx4(Position, _parent.CollisionTransform.Inverted());
                    _invSetUp = true;
                }
                Position = Matrix.Vec3MultMtx4(_invPos, _parent.CollisionTransform);
            }
            if (Active)
            {
                // todo: positional audio, node ref, SFX, set scan ID
                if (CameraSequence.Current?.BlockInput == true)
                {
                    return base.Process();
                }
                // todo: move dropped octolith toward player
                PlayerEntity player = PlayerEntity.Main;
                if (player.Health == 0)
                {
                    return base.Process();
                }
                // todo: visualize pickup volumes
                Vector3 position = Position.AddY(_heightOffset);
                bool pickedUp = false;
                float radii = player.Volume.SphereRadius + (_data.ModelId >= 8 ? 1 : 0.1f);
                if (player.IsAltForm)
                {
                    Vector3 between = position - player.Volume.SpherePosition;
                    if (Vector3.Dot(between, between) < radii * radii)
                    {
                        pickedUp = true;
                    }
                }
                else
                {
                    Vector3 between = position - player.Position;
                    if (between.X * between.X + between.Z * between.Z < radii * radii)
                    {
                        float diffY = position.Y - player.Position.Y;
                        float maxY = Fixed.ToFloat(player.Values.MaxPickupHeight);
                        if (diffY <= maxY + 0.5f)
                        {
                            float minY = Fixed.ToFloat(player.Values.MinPickupHeight);
                            if (diffY >= minY - 0.5f)
                            {
                                pickedUp = true;
                            }
                        }
                    }
                }
                if (!pickedUp)
                {
                    return base.Process();
                }
                if (_data.Message1 != Message.None)
                {
                    _scene.SendMessage(_data.Message1, this, _msgTarget1, 0, 0);
                }
                if (_data.Message2 != Message.None)
                {
                    _scene.SendMessage(_data.Message2, this, _msgTarget2, 0, 0);
                }
                if (_data.Message3 != Message.None)
                {
                    _scene.SendMessage(_data.Message3, this, _msgTarget3, 0, 0);
                }
                if (_data.ModelId >= 8)
                {
                    // todo: update story save
                    if (Id == -1)
                    {
                        // todo: show reclaimed octolith dialog
                    }
                    else
                    {
                        // todo: start movie, update global state and story save
                    }
                }
                else
                {
                    // todo: play SFX, update story save, show dialog (may send dialog message)
                }
                Active = false;
                // todo: room state, stop SFX
            }
            else
            {
                // todo: set scan ID
            }
            return base.Process();
        }

        public override void GetDrawInfo()
        {
            if (Active)
            {
                base.GetDrawInfo();
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message != Message.Activate)
            {
                Active = true;
                // todo: room state
            }
            else if (info.Message != Message.SetActive)
            {
                if ((int)info.Param1 != 0)
                {
                    Active = true;
                    // todo: room state
                }
                else
                {
                    Active = false;
                    // todo: room state
                }
            }
            else if (info.Message != Message.MoveItemSpawner)
            {
                if (info.Sender.Type == EntityType.EnemySpawn)
                {
                    var spawner = (EnemySpawnEntity)info.Sender;
                    if (spawner.Data.EnemyType == EnemyType.Hunter)
                    {
                        // todo: check bot spawner slots and find player to move to
                    }
                    else
                    {
                        info.Sender.GetPosition(out Vector3 position);
                        Position = position;
                    }
                }
                else
                {
                    info.Sender.GetPosition(out Vector3 position);
                    Position = position;
                }
            }
        }

        public override void Destroy()
        {
            // todo: stop SFX
            base.Destroy();
        }

        protected override LightInfo GetLightInfo()
        {
            if (_data.ModelId >= 8)
            {
                Vector3 player = _scene.CameraMode == CameraMode.Player
                    ? PlayerEntity.Main.CameraInfo.Position
                    : _scene.CameraPosition; // skdebug
                var vector1 = new Vector3(0, 1, 0);
                Vector3 vector2 = new Vector3(player.X - Position.X, 0, player.Z - Position.Z).Normalized();
                Matrix3 lightTransform = Matrix.GetTransform3(vector2, vector1);
                return new LightInfo(
                    (Metadata.OctolithLight1Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor,
                    (Metadata.OctolithLight2Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor
                );
            }
            return base.GetLightInfo();
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 0)
            {
                transform.Row3.Y += _heightOffset;
            }
            else if (index == 1)
            {
                transform.Row3.Y -= 0.2f;
            }
            return transform;
        }
    }
}
