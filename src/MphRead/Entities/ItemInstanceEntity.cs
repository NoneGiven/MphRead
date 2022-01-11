using System;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public readonly struct ItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly ItemType ItemType;
        public readonly int DespawnTimer;

        public ItemInstanceEntityData(Vector3 position, ItemType type, int despawnTimer)
        {
            Position = position;
            ItemType = type;
            DespawnTimer = despawnTimer;
        }
    }

    // todo: preallocation
    public class ItemInstanceEntity : SpinningEntityBase
    {
        public ItemType ItemType { get; }
        private EffectEntry? _effectEntry = null;
        private bool _linkDone = false;

        public int DespawnTimer { get; set; } = -1;
        public ItemSpawnEntity? Owner { get; set; }

        public ItemInstanceEntity(ItemInstanceEntityData data, Scene scene)
            : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance, scene)
        {
            Position = data.Position;
            // todo: scan ID
            // todo: replace with affinity weapon based on game state
            ItemType = data.ItemType;
            // todo: node ref
            SetUpModel(Metadata.Items[(int)data.ItemType]);
            if (data.DespawnTimer > 0)
            {
                DespawnTimer = data.DespawnTimer;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (ItemType == ItemType.ArtifactKey)
            {
                Matrix4 transform = Matrix.GetTransform4(Vector3.UnitX, Vector3.UnitY, Position);
                _effectEntry = _scene.SpawnEffectGetEntry(144, transform); // artifactKeyEffect
                _effectEntry.SetElementExtension(true);
            }
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = Position;
            up = Vector3.UnitY;
            facing = Vector3.UnitZ;
        }

        public override bool Process()
        {
            if (!_linkDone)
            {
                // todo: linked entity (position only)
                _linkDone = true;
            }
            // todo: inv pos
            // todo: position audio, node ref
            if (_effectEntry != null)
            {
                Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY, Position);
                _effectEntry.Transform(Position, transform);
            }
            if (DespawnTimer > 0)
            {
                DespawnTimer--;
            }
            if (DespawnTimer == 0)
            {
                if (Owner != null)
                {
                    Owner.Item = null;
                    if (!_scene.Multiplayer)
                    {
                        // todo: room state
                        if (!Owner.AlwaysActive)
                        {
                            Owner.Active = false;
                        }
                    }
                }
                if (_effectEntry != null)
                {
                    _scene.DetachEffectEntry(_effectEntry, setExpired: false);
                    _effectEntry = null;
                }
                return false;
            }
            // todo: play SFX
            if (Owner == null && !_scene.Multiplayer && PlayerEntity.Main.EquipInfo.Weapon != null)
            {
                EquipInfo equip = PlayerEntity.Main.EquipInfo;
                if (equip.ChargeLevel >= equip.Weapon.MinCharge * 2) // todo: FPS stuff
                {
                    // todo: visualize
                    Vector3 between = PlayerEntity.Main.Position - Position;
                    float distance = between.Length;
                    if (distance < 20 && distance != 0)
                    {
                        float factor = (20 - distance) / (80 * distance); // hyperbolic function
                        Position += between * factor / 2; // todo: FPS stuff
                    }
                }
            }
            return base.Process();
        }

        public void OnPickedUp()
        {
            DespawnTimer = 0;
            Owner?.OnItemPickedUp();
            // todo: update logbook
        }

        public override void Destroy()
        {
            if (_effectEntry != null)
            {
                _scene.UnlinkEffectEntry(_effectEntry);
            }
        }
    }

    public readonly struct FhItemInstanceEntityData
    {
        public readonly Vector3 Position;
        public readonly FhItemType ItemType;

        public FhItemInstanceEntityData(Vector3 position, FhItemType type)
        {
            Position = position;
            ItemType = type;
        }
    }

    public class FhItemEntity : SpinningEntityBase
    {
        public FhItemEntity(FhItemInstanceEntityData data, Scene scene)
            : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance, scene)
        {
            // note: the actual height at creation is 1.0f greater than the spawner's,
            // but 0.5f is subtracted when drawing (after the floating calculation)
            Position = data.Position.AddY(0.5f);
            SetUpModel(Metadata.FhItems[(int)data.ItemType], firstHunt: true);
        }
    }

    public abstract class SpinningEntityBase : EntityBase
    {
        private float _spin;
        private readonly float _spinSpeed;
        private readonly Vector3 _spinAxis;
        protected int _spinModelIndex;
        protected int _floatModelIndex;

        private static ushort _nextItemRotation = 0;

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis,
            EntityType type, Scene scene) : base(type, scene)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = -1;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex,
            EntityType type, Scene scene) : base(type, scene)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = -1;
        }

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, int floatModelIndex,
            EntityType type, Scene scene) : base(type, scene)
        {
            _spin = GetItemRotation();
            _spinSpeed = spinSpeed;
            _spinAxis = spinAxis;
            _spinModelIndex = spinModelIndex;
            _floatModelIndex = floatModelIndex;
        }

        public override bool Process()
        {
            _spin = (float)(_spin + _scene.FrameTime * 360 * _spinSpeed) % 360;
            return base.Process();
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            var transform = Matrix4.CreateScale(inst.Model.Scale);
            if (index == _spinModelIndex)
            {
                transform *= Matrix.GetTransformSRT(Vector3.One, new Vector3(
                    MathHelper.DegreesToRadians(_spinAxis.X * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Y * _spin),
                    MathHelper.DegreesToRadians(_spinAxis.Z * _spin)),
                    Vector3.Zero);
            }
            transform *= _transform;
            if (index == _floatModelIndex)
            {
                transform.M42 += (MathF.Sin(_spin / 180 * MathF.PI) + 1) / 8f;
            }
            return transform;
        }

        private static float GetItemRotation()
        {
            float rotation = _nextItemRotation / (float)0x10000 * 360f;
            _nextItemRotation += 0x2000;
            return rotation;
        }
    }
}
