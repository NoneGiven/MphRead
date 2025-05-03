using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy32Entity : GoreaEnemyEntityBase
    {
        private Enemy31Entity _gorea2 = null!;
        private Node _attachNode = null!;
        public Node AttachNode => _attachNode;

        private int _damage = 0;
        public int Damage { get => _damage; set => _damage = value; }
        private int _damageTimer = 0;
        public int DamageTimer => _damageTimer;
        private bool _visible = false;
        private bool _targetable = false;
        public bool Visible => _visible;
        public bool Targetable => _targetable;

        public Enemy32Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy31Entity owner)
            {
                _gorea2 = owner;
                Flags &= ~EnemyFlags.Visible;
                Flags &= ~EnemyFlags.NoHomingNc;
                Flags &= ~EnemyFlags.NoHomingCo;
                Flags |= EnemyFlags.Invincible;
                Flags |= EnemyFlags.CollidePlayer;
                Flags |= EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.NoMaxDistance;
                _state1 = _state2 = 255;
                HealthbarMessageId = 3;
                _attachNode = owner.GetModels()[0].Model.GetNodeByName("ChestBall1")!;
                SetTransform(owner.FacingVector, owner.UpVector, owner.Position);
                Position += _attachNode.Position;
                _prevPos = Position;
                _boundingRadius = 1;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, _owner.Scale.X);
                _health = 65535;
                _healthMax = 840;
                Metadata.LoadEffectiveness(EnemyType.GoreaSealSphere2, BeamEffectiveness);
            }
        }

        protected override void EnemyProcess()
        {
            if (_gorea2.Flags.TestFlag(EnemyFlags.Visible))
            {
                Matrix4 transform = GetNodeTransform(_gorea2, _attachNode);
                Position = transform.Row3.Xyz;
            }
            if (_damageTimer > 0)
            {
                _damageTimer--;
            }
            // note: the game updates the effectiveness here when damage taken is 720 or greater,
            // but the value doesn't seem to change (might have been intended to halve Omega Cannon?)
            _targetable = _visible && IsVisible(NodeRef);
        }

        public void UpdateVisibility()
        {
            CollisionResult discard = default;
            _visible = !CollisionDetection.CheckBetweenPoints(Position, PlayerEntity.Main.CameraInfo.Position, TestFlags.None, _scene, ref discard);
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            bool ignoreDamage = false;
            bool isOmegaCannon = false;
            if (source?.Type == EntityType.BeamProjectile)
            {
                var beamSource = (BeamProjectileEntity)source;
                isOmegaCannon = beamSource.BeamKind == BeamType.OmegaCannon;
            }
            int damage = 65535 - _health;
            if (_damage >= 720 && (!isOmegaCannon || !_gorea2.GoreaFlags.TestFlag(Gorea2Flags.Bit9))
                || source?.Type == EntityType.Bomb)
            {
                damage = 0;
                ignoreDamage = true;
            }
            if (isOmegaCannon)
            {
                _gorea2.GoreaFlags |= Gorea2Flags.Bit9;
            }
            if (Flags.TestFlag(EnemyFlags.Invincible) || _gorea2.Func214080C())
            {
                damage = 0;
                ignoreDamage = true;
            }
            else
            {
                _damage += damage;
                _gorea2.UpdatePhase();
            }
            _health = 65535;
            _gorea2.GoreaFlags &= ~Gorea2Flags.LaserActive;
            _soundSource.StopSfx(SfxId.GOREA2_ATTACK1B);
            if (_damage >= 840)
            {
                _gorea2.GoreaFlags |= Gorea2Flags.Bit11;
            }
            else if (damage != 0 && !Flags.TestFlag(EnemyFlags.Invincible))
            {
                bool spawnEffect = false;
                if (damage >= 120)
                {
                    spawnEffect = true;
                }
                else
                {
                    // this returns 32, instead of 8, for 0 (which the game might also do?), but we clamp to 6 anyway
                    int index = System.Numerics.BitOperations.TrailingZeroCount(_gorea2.Field244);
                    if (index > 6)
                    {
                        index = 6;
                    }
                    int diff = _damage - 120 * index;
                    for (int i = 1; i < 3; i++)
                    {
                        if (diff - damage < i * 40 && i * 40 <= diff)
                        {
                            spawnEffect = true;
                            break;
                        }
                    }
                }
                if (spawnEffect)
                {
                    _gorea2.GoreaFlags |= Gorea2Flags.Bit16;
                    SpawnEffect(44, Position); // goreaShoulderHits
                    _gorea2.GoreaFlags |= Gorea2Flags.Bit13;
                    Flags |= EnemyFlags.Invincible;
                }
                _soundSource.PlaySfx(SfxId.GOREA2_DAMAGE1);
                _damageTimer = 10 * 2; // todo: FPS stuff
                Material material = _gorea2.GetModels()[0].Model.GetMaterialByName("ChestCore")!;
                material.Diffuse = new ColorRgb(31, 0, 0);
            }
            return ignoreDamage;
        }

        public void SetDead()
        {
            _scanId = 0;
            Flags &= ~EnemyFlags.CollidePlayer;
            Flags &= ~EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
        }
    }
}
