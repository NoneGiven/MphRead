using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy20Entity : EnemyInstanceEntity
    {
        private readonly Enemy19Entity _cretaphid;
        private ushort _field184 = 0;
        private int _field180 = 0;
        public byte Field187 { get; set; } = 2;
        public byte Field189 { get; set; } = 1;
        public byte Field18A { get; set; }
        public byte Field18C { get; set; }
        public byte Field18E { get; set; }

        private Node _attachNode = null!;
        public int EyeIndex { get; set; }
        public bool Flag { get; set; }
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
                        _field184 = _cretaphid.Values.FieldCA[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field172[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field21A[EyeIndex];
                    }
                }
                else if (newState == 1)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    Flags &= ~EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _field184 = _cretaphid.Values.FieldE2[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field18A[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field232[EyeIndex];
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
                    Flag = false;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _field184 = _cretaphid.Values.FieldFA[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field1A2[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field24A[EyeIndex];
                    }
                }
                else if (newState == 4)
                {
                    _models[0].SetAnimation(0, AnimFlags.NoLoop);
                    Flags &= ~EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _field184 = _cretaphid.Values.Field112[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field1BA[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field262[EyeIndex];
                    }
                }
                else if (newState == 5)
                {
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse | AnimFlags.Paused);
                    Flags |= EnemyFlags.Invincible;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _field184 = _cretaphid.Values.FieldCA[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field172[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field21A[EyeIndex];
                    }
                }
                else if (newState == 6)
                {
                    _soundSource.PlayEnvironmentSfx(5); // CYLINDER_BOSS_ATTACK
                    _models[0].SetAnimation(3, AnimFlags.NoLoop | AnimFlags.Reverse | AnimFlags.Paused);
                    Flags |= EnemyFlags.Invincible;
                    Flag = false;
                    if (_cretaphid.PhaseIndex == 0)
                    {
                        _field184 = _cretaphid.Values.FieldFA[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 1)
                    {
                        _field184 = _cretaphid.Values.Field1A2[EyeIndex];
                    }
                    else if (_cretaphid.PhaseIndex == 2)
                    {
                        _field184 = _cretaphid.Values.Field24A[EyeIndex];
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
    }
}
