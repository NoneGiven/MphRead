using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy20Entity : EnemyInstanceEntity
    {
        private readonly Enemy19Entity _cretaphid;
        private int _stateTimer = 0;
        private float _beamLength = 0;
        public ushort BeamType { get; set; } = 2;
        public bool EyeActive { get; set; } = true;
        public ushort BeamSpawnCount { get; set; }
        public int BeamSpawnCooldown { get; set; }
        public int BeamSpawnTimer { get; set; }

        private Node _attachNode = null!;
        private Matrix4 _beamTransform = Matrix4.Identity;
        public int EyeIndex { get; set; }
        public bool BeamActive { get; set; }
        public int SegmentIndex { get; private set; }

        public Enemy20Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var owner = data.Spawner as Enemy19Entity;
            Debug.Assert(owner != null);
            _cretaphid = owner;
        }

        public void SetUp(Node attachNode, int scanId, uint effectiveness,
            ushort health, Vector3 position, float radius)
        {
            HealthbarMessageId = 1;
            if (EyeIndex > 6)
            {
                SegmentIndex = 2;
            }
            else if (EyeIndex > 2)
            {
                SegmentIndex = 1;
            }
            else
            {
                SegmentIndex = 0;
            }
            _beamLength = 0;
            _stateTimer = 0;
            BeamType = 2;
            EyeActive = true;
            _attachNode = attachNode;
            _scanId = scanId;
            Metadata.LoadEffectiveness(effectiveness, BeamEffectiveness);
            _state1 = _state2 = 255;
            _health = _healthMax = health;
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoMaxDistance;
            Matrix4 transform = GetTransformMatrix(attachNode.Transform.Row2.Xyz, attachNode.Transform.Row1.Xyz);
            transform.Row3.Xyz = attachNode.Transform.Row3.Xyz + position;
            Transform = transform;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 0.5f);
            _boundingRadius = radius;
            SetUpModel("CylinderBossEye");
        }

        public void UpdateState(byte newState)
        {
            if (_state1 == 7)
            {
                return;
            }
            if (newState != _state1)
            {
                if (newState == 0)
                {
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse);
                    Flags |= EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {

                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 1)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    Flags &= ~EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer1[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer1[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer1[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 2)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    Flags &= ~EnemyFlags.Invincible;
                }
                else if (newState == 3)
                {
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse | AnimFlags.Paused);
                    Flags |= EnemyFlags.Invincible;
                    BeamActive = false;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 4)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    Flags &= ~EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer3[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer3[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer3[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 5)
                {
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse | AnimFlags.Paused);
                    Flags |= EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer0[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 6)
                {
                    _soundSource.PlayEnvironmentSfx(5); // CYLINDER_BOSS_ATTACK
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse | AnimFlags.Paused);
                    Flags |= EnemyFlags.Invincible;
                    BeamActive = false;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _stateTimer = _cretaphid.Values.Phase0EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _stateTimer = _cretaphid.Values.Phase1EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _stateTimer = _cretaphid.Values.Phase2EyeStateTimer2[EyeIndex] * 2; // todo: FPS stuff
                    }
                }
                else if (newState == 9)
                {
                    _models[0].SetAnimation(5, AnimFlags.NoLoop);
                    Flags |= EnemyFlags.Invincible;
                }
            }
            _state1 = _state2 = newState;
        }

        protected override void EnemyProcess()
        {
            if (!EyeActive)
            {
                return;
            }

            void ProgressState(byte state)
            {
                if (_stateTimer > 0)
                {
                    _stateTimer--;
                }
                else
                {
                    UpdateState(state);
                }
            }

            if (_state1 == 0)
            {
                ProgressState(1);
            }
            else if (_state1 == 1)
            {
                ProgressState(3);
            }
            else if (_state1 == 3)
            {
                if (BeamType == 2)
                {
                    _cretaphid.SounceSource.PlayEnvironmentSfx(5); // CYLINDER_BOSS_ATTACK
                }
                else if (BeamType <= 1)
                {
                    if (BeamSpawnTimer > 0)
                    {
                        BeamSpawnTimer--;
                    }
                    else if (BeamSpawnCount > 0)
                    {
                        BeamSpawnCount--;
                        BeamSpawnTimer = BeamSpawnCooldown;
                        SpawnBeam();
                    }
                }
                if (_stateTimer > 0)
                {
                    _stateTimer--;
                }
                else
                {
                    BeamSpawnTimer = BeamSpawnCooldown;
                    _cretaphid.Sub213619C(this);
                    UpdateState(4);
                }
            }
            else if (_state1 == 4)
            {
                ProgressState(0);
            }
            else if (_state1 == 5)
            {
                ProgressState(6);
            }
            else if (_state1 == 6)
            {
                if (BeamType <= 1)
                {
                    if (BeamSpawnTimer > 0)
                    {
                        BeamSpawnTimer--;
                    }
                    else if (BeamSpawnCount > 0)
                    {
                        BeamSpawnCount--;
                        BeamSpawnTimer = BeamSpawnCooldown;
                        SpawnBeam();
                    }
                }
                if (_stateTimer > 0)
                {
                    _stateTimer--;
                    _cretaphid.SounceSource.PlayEnvironmentSfx(5); // CYLINDER_BOSS_ATTACK
                }
                else
                {
                    BeamSpawnTimer = BeamSpawnCooldown;
                    _cretaphid.Sub213619C(this);
                    UpdateState(5);
                }
            }
            else if (_state1 == 7)
            {
                if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _health = 0;
                }
            }
            else if (_state1 == 9)
            {
                if (_models[0].AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _cretaphid.Sub2135F54();
                }
            }
            UpdateBeamTransform();
            CheckBeamCollision();
        }

        private void SpawnBeam()
        {
            Debug.Assert(BeamType == 0 || BeamType == 1);
            ushort damage = _cretaphid.Values.EyeBeamDamage[SegmentIndex];
            EquipInfo equipInfo = _cretaphid.EquipInfo[BeamType];
            equipInfo.Weapon.UnchargedDamage = damage;
            equipInfo.Weapon.SplashDamage = damage;
            equipInfo.Weapon.HeadshotDamage = damage;
            Vector3 facing = FacingVector;
            Vector3 spawnDir = (PlayerEntity.Main.Position.AddY(0.5f) - Position).Normalized();
            if (Vector3.Dot(facing, spawnDir) < -1)
            {
                spawnDir = facing;
            }
            BeamProjectileEntity.Spawn(this, equipInfo, Position, spawnDir, BeamSpawnFlags.None, _scene);
        }

        private void CheckBeamCollision()
        {
            PlayerEntity player = PlayerEntity.Main;
            Vector3 beamCylTop = Position + _beamTransform.Row2.Xyz;
            float radii = player.Volume.SphereRadius + Fixed.ToFloat(_cretaphid.Values.CollisionRadius);
            CollisionResult discard = default;
            bool collided = false;
            if (player.IsAltForm)
            {
                collided = CollisionDetection.CheckCylinderOverlapSphere(Position, beamCylTop,
                    player.Volume.SpherePosition, radii, ref discard);
            }
            else
            {
                Vector3 twoBottom = player.Volume.SpherePosition.AddY(-0.5f);
                collided = CollisionDetection.CheckCylindersOverlap(Position, beamCylTop, twoBottom,
                    Vector3.UnitY, twoDot: 2, radii, ref discard);
            }
            if (collided)
            {
                ushort damage = _cretaphid.Values.EyeContactDamage[SegmentIndex];
                player.TakeDamage(damage, DamageFlags.None, Vector3.Zero, this);
            }
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            if (_health == 0)
            {
                _health = 1;
                _state1 = _state2 = 7;
                Flags |= EnemyFlags.Invincible;
                _models[0].SetAnimation(2, AnimFlags.NoLoop);
                ItemType itemType = ItemType.None;
                Enemy19Values values = _cretaphid.Values;
                int chanceTotal = values.ItemChanceHealth + values.ItemChanceMissile
                    + values.ItemChanceUa + values.ItemChanceNone;
                uint rand = Rng.GetRandomInt2(chanceTotal);
                if (rand < values.ItemChanceHealth)
                {
                    itemType = ItemType.HealthMedium;
                }
                else
                {
                    rand -= values.ItemChanceHealth;
                    if (rand < values.ItemChanceMissile)
                    {
                        itemType = ItemType.MissileSmall;
                    }
                    else
                    {
                        rand -= values.ItemChanceMissile;
                        if (rand < values.ItemChanceUa)
                        {
                            itemType = ItemType.UASmall;
                        }
                    }
                }
                if (itemType != ItemType.None)
                {
                    ItemSpawnEntity.SpawnItem(itemType, Position, NodeRef,
                        despawnTime: 300 * 2, _scene); // todo: FPS stuff
                }
            }
            return false;
        }

        private void UpdateBeamTransform()
        {
            // sktodo
        }

        protected override bool EnemyGetDrawInfo()
        {
            // sktodo
            return true;
        }
    }
}
