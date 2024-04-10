using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy29Entity : GoreaEnemyEntityBase
    {
        private Enemy28Entity _gorea1B = null!;
        private Node _attachNode = null!;

        private int _damage = 0;
        public int Damage => _damage;
        private int _damageTimer = 0;
        public ColorRgb Ambient { get; set; }
        public ColorRgb Diffuse { get; set; }

        public Enemy29Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy28Entity owner)
            {
                _gorea1B = owner;
                Flags &= ~EnemyFlags.Visible;
                Flags |= EnemyFlags.NoHomingNc;
                Flags |= EnemyFlags.NoHomingCo;
                Flags |= EnemyFlags.Invincible;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                Flags |= EnemyFlags.NoMaxDistance;
                _scanId = 0;
                _state1 = _state2 = 255;
                HealthbarMessageId = 7;
                _attachNode = owner.GetModels()[0].Model.GetNodeByName("ChestBall1")!;
                SetTransform(owner.FacingVector, owner.UpVector, owner.Position);
                Position += _attachNode.Position;
                _prevPos = Position;
                _boundingRadius = 1;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, _owner.Scale.X);
                _health = 65535;
                _healthMax = 3000;
            }
        }

        public void Activate()
        {
            _scanId = Metadata.EnemyScanIds[(int)EnemyType];
            Position = _gorea1B.Position;
            Flags &= ~EnemyFlags.Visible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.NoHomingNc;
            Flags &= ~EnemyFlags.NoHomingCo;
        }

        protected override void EnemyProcess()
        {
            if (_gorea1B.Flags.TestFlag(EnemyFlags.Visible))
            {
                Matrix4 transform = GetNodeTransform(_gorea1B, _attachNode);
                Position = transform.Row3.Xyz;
            }
            if (_damageTimer > 0)
            {
                _damageTimer--;
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            int change = 65535 - _health;
            if (source is BeamProjectileEntity beam && beam.BeamKind == BeamType.ShockCoil)
            {
                // sktodo
            }
            _damage += change;
            if (_damage > _healthMax)
            {
                _damage = _healthMax;
            }
            int prevDamage = _damage - change;
            _health = 65535;
            if (!Flags.TestFlag(EnemyFlags.Invincible))
            {
                // do SFX and effect only if the damage total has reached a new multiple of 10,
                // excluding 1000, from a base of 0/1000/2000 depending on the phase
                int damage = _damage;
                if (damage / 1000 == prevDamage / 1000)
                {
                    damage %= 1000;
                    prevDamage %= 1000;
                    if (damage / 10 > prevDamage / 10)
                    {
                        _soundSource.PlaySfx(SfxId.GOREA_1B_DAMAGE);
                        SpawnEffect(44, Position); // goreaShoulderHits
                    }
                }
                _damageTimer = 10 * 2; // todo: FPS stuff
                Material material = _gorea1B.GetModels()[0].Model.GetMaterialByName("ChestCore")!;
                material.Ambient = Ambient;
                material.Diffuse = new ColorRgb(31, 0, 0);
            }
            return false;
        }
    }
}
