using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy32Entity : GoreaEnemyEntityBase
    {
        private Enemy31Entity _gorea2 = null!;
        private Node _attachNode = null!;

        private int _damage = 0;
        public int Damage => _damage;
        private int _field17C = 0; // skhere: field name (timer)
        private bool _visible = false;
        private bool _targetable = false;
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
                HealthbarMessageId = 7;
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
            if (_field17C > 0)
            {
                _field17C--;
            }
            // note: the game updates the effectiveness here when damage taken is 720 or greater,
            // but the value doesn't seem to change (might have been intended to halve Omega Cannon?)
            bool flagUpdate = _visible;
            if (flagUpdate)
            {
                flagUpdate = IsVisible(NodeRef);
            }
            _targetable = flagUpdate;
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            // skhere
            return false;
        }
    }
}
