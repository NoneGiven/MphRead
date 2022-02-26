using System;
using System.Collections.Generic;
using MphRead.Effects;
using MphRead.Formats.Culling;
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
        public int ParentId { get; set; } = -1;
        private EntityBase? _parent = null;
        private Vector3 _invPos;

        public int DespawnTimer { get; set; } = -1;
        public ItemSpawnEntity? Owner { get; set; }

        private static readonly IReadOnlyList<int> _scanIds = new int[22]
        {
            9, 11, 12, 0, 10, 21, 13, 23, 22, 18, 20, 19, 28, 14, 15, 16, 17, 0, 24, 463, 0, 0
        };

        public ItemInstanceEntity(ItemInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(0.35f, Vector3.UnitY, 0, 0, EntityType.ItemInstance, nodeRef, scene)
        {
            Position = data.Position;
            ItemType = data.ItemType;
            _scanId = _scanIds[(int)data.ItemType];
            if (scene.Multiplayer && GameState.AffinityWeapons && (ItemType == ItemType.VoltDriver
                || ItemType == ItemType.Battlehammer || ItemType == ItemType.Imperialist
                || ItemType == ItemType.Judicator || ItemType == ItemType.Magmaul || ItemType == ItemType.ShockCoil))
            {
                ItemType = ItemType.AffinityWeapon;
            }
            // todo: node ref
            SetUpModel(Metadata.Items[(int)ItemType]);
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
                _effectEntry?.SetElementExtension(true);
            }
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = Position;
            up = Vector3.UnitY;
            facing = Vector3.UnitZ;
        }

        private static readonly IReadOnlyList<int> _sfxIds = new int[22]
        {
            33, 33, 33, -1, 34, -1, 34, -1, -1, -1, -1, -1, -1, 33, 33, 33, 33, -1, -1, -1, -1, -1
        };

        public override bool Process()
        {
            if (!_linkDone && ParentId != -1)
            {
                if (_scene.TryGetEntity(ParentId, out EntityBase? parent))
                {
                    _parent = parent;
                }
                if (_parent != null)
                {
                    _invPos = Matrix.Vec3MultMtx4(Position, _parent.CollisionTransform.Inverted());
                }
                _linkDone = true;
            }
            if (_linkDone && _parent != null)
            {
                Position = Matrix.Vec3MultMtx4(_invPos, _parent.CollisionTransform);
            }
            _soundSource.Update(Position, rangeIndex: 7);
            // sfxtodo: if node ref is not active, set sound volume override to 0
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
            int sfx = _sfxIds[(int)ItemType];
            if (sfx != -1)
            {
                _soundSource.PlaySfx(sfx, loop: true);
            }
            if (Owner == null && !_scene.Multiplayer && PlayerEntity.Main.EquipInfo.Weapon != null)
            {
                EquipInfo equip = PlayerEntity.Main.EquipInfo;
                if (equip.ChargeLevel >= equip.Weapon.MinCharge * 2) // todo: FPS stuff
                {
                    // todo: visualize
                    Vector3 between = PlayerEntity.Main.Position - Position;
                    float distSqr = between.LengthSquared;
                    if (distSqr > 0 && distSqr < 20 * 20)
                    {
                        // hyperbolic function -- (20 - x) / (80 * x)
                        float distance = MathF.Sqrt(distSqr);
                        float div = distance / 20;
                        float pct = (1 - div) / distance;
                        Position += between * (pct / (4 * 2)); // todo: FPS stuff
                    }
                }
            }
            return base.Process();
        }

        public void OnPickedUp()
        {
            DespawnTimer = 0;
            Owner?.OnItemPickedUp();
            if (!_scene.Multiplayer)
            {
                int scanId = GetScanId();
                GameState.StorySave.UpdateLogbook(scanId);
            }
        }

        public override void GetDrawInfo()
        {
            if (IsVisible(NodeRef))
            {
                base.GetDrawInfo();
            }
        }

        public override void Destroy()
        {
            if (_effectEntry != null)
            {
                _scene.UnlinkEffectEntry(_effectEntry);
            }
            _soundSource.StopAllSfx(force: true);
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

        public SpinningEntityBase(float spinSpeed, Vector3 spinAxis, int spinModelIndex, int floatModelIndex,
            EntityType type, NodeRef nodeRef, Scene scene) : base(type, nodeRef, scene)
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
