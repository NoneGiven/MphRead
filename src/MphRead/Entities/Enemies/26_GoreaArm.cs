using System;
using MphRead.Effects;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy26Entity : GoreaEnemyEntityBase
    {
        private Enemy24Entity _gorea1A = null!;
        private Node _shoulderNode = null!;
        private Node _elbowNode = null!;
        private Node _upperArmNode = null!;
        public int Index { get; set; }
        public int ScanId { get => _scanId; set => _scanId = value; }

        public GoreaArmFlags ArmFlags { get; set; }
        private EquipInfo _equipInfo = null!;
        public EquipInfo EquipInfo => _equipInfo;
        public int Ammo { get; set; } = 65535;
        public int Damage { get; set; }
        public int Cooldown { get; set; }
        public int RegenTimer { get; set; }
        private int _colorTimer = 0;

        private EffectEntry? _shotEffect = null;
        private EffectEntry? _damageEffect = null;

        public Enemy26Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy24Entity owner)
            {
                _gorea1A = owner;
                InitializeCommon(owner.Spawner);
                Flags &= ~EnemyFlags.NoHomingCo;
                Flags &= ~EnemyFlags.Visible;
                _state1 = _state2 = 255;
                ModelInstance ownerModel = _owner.GetModels()[0];
                _shoulderNode = ownerModel.Model.GetNodeByName(Index == 0 ? "L_Shoulder" : "R_Shoulder")!;
                _elbowNode = ownerModel.Model.GetNodeByName(Index == 0 ? "L_Elbow" : "R_Elbow")!;
                _upperArmNode = ownerModel.Model.GetNodeByName(Index == 0 ? "L_UpperArm" : "R_UpperArm")!;
                Position += _shoulderNode.Position;
                _prevPos = Position;
                SetTransform(owner.FacingVector, owner.UpVector, Position);
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, Fixed.ToFloat(1732));
                _health = 65535;
                _healthMax = 120;
                _equipInfo = new EquipInfo(Weapons.GoreaWeapons[0], _beams);
                _equipInfo.GetAmmo = () => Ammo;
                _equipInfo.SetAmmo = (newAmmo) => Ammo = newAmmo;
                ArmFlags |= GoreaArmFlags.Bit1;
            }
        }

        public void UpdateWeapon(WeaponInfo weapon)
        {
            _equipInfo.Weapon = weapon;
            Cooldown = (Index == 0 ? weapon.ShotCooldown : weapon.AutofireCooldown) * 2; // todo: FPS stuff
        }

        public void GetNodeVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            Matrix4 transform = GetNodeTransform(_elbowNode, _gorea1A, _gorea1A.Scale);
            position = transform.Row3.Xyz;
            up = Index == 0 ? transform.Row0.Xyz : transform.Row2.Xyz;
            facing = transform.Row1.Xyz;
        }

        protected override void EnemyProcess()
        {
            if (ArmFlags.TestFlag(GoreaArmFlags.Bit0))
            {
                return;
            }
            Matrix4 transform = GetNodeTransform(_shoulderNode, _gorea1A, _gorea1A.Scale);
            Position = transform.Row3.Xyz;
            if (_damageEffect != null)
            {
                _damageEffect.Transform(FacingVector, UpVector, Position);
            }
            if (_shotEffect != null)
            {
                GetNodeVectors(out Vector3 position, out Vector3 up, out Vector3 facing);
                up = up.Normalized();
                facing = facing.Normalized();
                position += up * Fixed.ToFloat(8343);
                _shotEffect.Transform(facing, up, position);
            }
            if (Damage <= 60)
            {
                if (_damageEffect != null)
                {
                    _scene.UnlinkEffectEntry(_damageEffect);
                    _damageEffect = null;
                }
            }
            else if (_damageEffect == null)
            {
                _damageEffect = SpawnEffectGetEntry(43, Position, extensionFlag: true); // goreaShoulderDamageLoop
            }
            if (RegenTimer > 0)
            {
                RegenTimer--;
            }
            if (_colorTimer > 0)
            {
                _colorTimer--;
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            int prevDamage = Damage;
            Damage += 65535 - _health;
            if (RegenTimer > 0 || _gorea1A.WeaponIndex == 5 // Shock Coil
                && source is BeamProjectileEntity beam && beam.BeamKind == BeamType.Imperialist)
            {
                Damage = 121;
            }
            if (Damage <= 120)
            {
                // do SFX and effect only if the damage total has reached a new multiple of 10, excluding 120
                int damage = Damage;
                if (damage == 120)
                {
                    damage--;
                }
                if (damage / 10 > prevDamage / 10)
                {
                    _soundSource.PlaySfx(SfxId.GOREA_SHOULDER_DAMAGE2);
                    SpawnEffect(44, Position); // goreaShoulderHits
                }
            }
            else
            {
                RegenTimer = 0;
                Damage = 120;
                _scanId = 0;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.Invincible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.NoHomingCo;
                ArmFlags |= GoreaArmFlags.Bit0;
                _soundSource.PlaySfx(SfxId.GOREA_SHOULDER_DIE);
                if (_damageEffect != null)
                {
                    _scene.UnlinkEffectEntry(_damageEffect);
                    _damageEffect = null;
                }
            }
            _health = 65535;
            if (!Flags.TestFlag(EnemyFlags.Invincible))
            {
                _colorTimer = 10 * 2; // todo: FPS stuff
                string matName = Index == 0 ? "L_ShoulderTarget" : "R_ShoulderTarget";
                Material material = _gorea1A.GetModels()[0].Model.GetMaterialByName(matName)!;
                material.Diffuse = new ColorRgb(29, 9, 0);
            }
            return true;
        }

        public void SpawnShotEffect(int effectId)
        {
            _shotEffect = SpawnEffectGetEntry(effectId, Position, extensionFlag: false);
        }

        public void StopShotEffect(bool deatch)
        {
            if (_shotEffect != null)
            {
                if (deatch)
                {
                    _scene.DetachEffectEntry(_shotEffect, setExpired: false);
                }
                else
                {
                    _scene.UnlinkEffectEntry(_shotEffect);
                }
                _shotEffect = null;
            }
        }
    }

    [Flags]
    public enum GoreaArmFlags : byte
    {
        None = 0x0,
        Bit0 = 0x1,
        Bit1 = 0x2,
        Bit2 = 0x4,
        Unused3 = 0x8,
        Unused4 = 0x10,
        Unused5 = 0x20,
        Unused6 = 0x40,
        Unused7 = 0x80
    }
}
