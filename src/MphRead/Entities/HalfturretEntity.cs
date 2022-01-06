using System;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class HalfturretEntity : DynamicLightEntityBase
    {
        public PlayerEntity Owner { get; }
        private EntityBase? _target = null;

        private int _health = 0;
        private ushort _timeSinceDamage = UInt16.MaxValue;
        private ushort _timeSinceFrozen = 0;
        private ushort _freezeTimer = 0;
        private ushort _burnTimer = 0;
        private EffectEntry? _burnEffect = null;

        private float _ySpeed = 0;
        private bool _grounded = false;
        private Vector3 _field24;
        private ushort _targetTimer = 0;
        private ushort _fieldDB = 0;
        private float _fieldCC = 1.5f;

        public EquipInfo EquipInfo { get; } = new EquipInfo();

        public HalfturretEntity(PlayerEntity owner, Scene scene) : base(EntityType.Halfturret, scene)
        {
            Owner = owner;
        }

        public override void Initialize()
        {
            base.Initialize();
            // todo: scan ID
            float minY = Fixed.ToFloat(Owner.Values.MinPickupHeight);
            Vector3 position = Owner.Position.AddY(minY + 0.45f);
            var facing = new Vector3(Owner.Field70, 0, Owner.Field74);
            Transform = GetTransformMatrix(facing, Vector3.UnitY, position);
            _field24 = facing;
            // todo: node ref
            int health = Owner.Health;
            if (health > 1)
            {
                _health = health / 2;
                Owner.Health -= _health;
            }
            else
            {
                _health = 1;
            }
            _grounded = Owner.Flags1.TestFlag(PlayerFlags1.Standing);
            EquipInfo.Beams = Owner.EquipInfo.Beams;
            EquipInfo.Weapon = Weapons.Current[3]; // non-affinity Battlehammer
            ModelInstance inst = Read.GetModelInstance("");
            inst.SetAnimation(1, AnimFlags.NoLoop);
            _light1Vector = Owner.Light1Vector;
            _light1Color = Owner.Light1Color;
            _light2Vector = Owner.Light2Vector;
            _light2Color = Owner.Light2Color;
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = Position;
            up = Vector3.UnitY;
            facing = FacingVector;
        }

        public override bool Process()
        {
            if (_health == 0 || !Owner.Flags2.TestFlag(PlayerFlags2.Halfturret))
            {
                return false;
            }
            if (_burnTimer > 0)
            {
                _burnTimer--;
                if (_burnTimer % (8 * 2) == 0) // todo:FPS stuff
                {
                    Owner.TakeDamage(1, DamageFlags.NoSfx | DamageFlags.Burn | DamageFlags.NoDmgInvuln | DamageFlags.Halfturret,
                        direction: null, Owner.BurnedBy);
                }
                if (_burnEffect != null)
                {
                    Vector3 facing = FacingVector;
                    facing = new Vector3(facing.X, 0, facing.Z);
                    _burnEffect.Transform(facing, Vector3.UnitY, Position);
                }
            }
            else if (_burnEffect != null)
            {
                _scene.UnlinkEffectEntry(_burnEffect);
                _burnEffect = null;
            }
            if (_freezeTimer == 0)
            {
                base.Process();
                if (_targetTimer > 0)
                {
                    _targetTimer--;
                }
                else
                {
                    _target = null;
                }
                if (_fieldCC < 1.5f)
                {
                    _fieldCC = Math.Min(_fieldCC + 0.015f / 2, 1.5f); // todo: FPS stuff
                }
                else if (_fieldCC > 1.5f)
                {
                    _fieldCC = Math.Max(_fieldCC - 0.015f / 2, 1.5f); // todo: FPS stuff
                }
                if (_target == null)
                {
                    float minDistSqr = 15 * 15;
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Player || entity == Owner)
                        {
                            continue;
                        }
                        var player = (PlayerEntity)entity;
                        if (player.Health == 0 || player.TeamIndex == Owner.TeamIndex || player.CurAlpha < 6 / 31f)
                        {
                            continue;
                        }
                        Vector3 between = player.Position - Position;
                        float distSqr = between.LengthSquared;
                        if (distSqr < minDistSqr)
                        {
                            minDistSqr = distSqr;
                            _target = player;
                        }
                    }
                }
                if (_target != null)
                {
                    // todo: if 1P bot and encounter state, do something
                    // else...

                    _fieldDB = 1;
                    // sktodo: this
                }
            }
            else
            {
                _freezeTimer--;
                _timeSinceFrozen = 0;
            }
            if (_timeSinceFrozen != UInt16.MaxValue)
            {
                _timeSinceFrozen++;
            }
            if (_timeSinceDamage != UInt16.MaxValue)
            {
                _timeSinceDamage++;
            }
            // todo: show HUD message
            if (!_grounded)
            {
                // future: it would be cool to have the halfturret move with platforms, etc.
                Vector3 prevPos = Position;
                _ySpeed -= 0.02f / 2; // todo: FPS stuff
                Position = Position.AddY(_ySpeed / 2); // todo: FPS stuff
                var results = new CollisionResult[1];
                if (CollisionDetection.CheckSphereBetweenPoints(prevPos, Position, 0.45f, limit: 1,
                    includeOffset: false, TestFlags.None, _scene, results) > 0)
                {
                    CollisionResult result = results[0];
                    float dot = Vector3.Dot(Position, result.Plane.Xyz) - result.Plane.W;
                    dot = 0.45f - dot;
                    Position = result.Position + result.Plane.Xyz * dot;
                    _ySpeed = 0;
                    _grounded = true;
                    // todo?: wifi stuff
                }
                UpdateLightSources(Position);
                // todo: update node ref
            }
            // todo?: wifi stuff
            Debug.Assert(_scene.Room != null);
            if (Position.Y < _scene.Room.Metadata.KillHeight)
            {
                Die();
            }
            return true;
        }

        public void Die()
        {
            Owner.OnHalfturretDied();
            if (_health > 0)
            {
                _health = 0;
                _scene.SpawnEffect(216, Vector3.UnitX, Vector3.UnitY, Position); // deathAlt
            }
        }

        public override void Destroy()
        {
            if (_burnEffect != null)
            {
                _scene.UnlinkEffectEntry(_burnEffect);
                _burnEffect = null;
            }
        }
    }
}
