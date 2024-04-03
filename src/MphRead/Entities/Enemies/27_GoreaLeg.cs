using System;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy27Entity : GoreaEnemyEntityBase
    {
        private Enemy24Entity _gorea1A = null!;
        private Node _kneeNode = null!;
        public int Index { get; set; }

        public Enemy27Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy24Entity owner)
            {
                _gorea1A = owner;
                InitializeCommon(owner.Spawner);
                Flags &= ~EnemyFlags.Visible;
                Flags |= EnemyFlags.Invincible;
                _state1 = _state2 = 255;
                _prevPos = Position;
                SetTransform(owner.FacingVector, owner.UpVector, Position);
                _hurtVolumeInit = new CollisionVolume(Vector3.UnitY, Vector3.Zero, Fixed.ToFloat(736), Fixed.ToFloat(9700));
                _health = 65535;
                _healthMax = 120;
                SetKneeNode(owner);
            }
        }

        public void SetKneeNode(EnemyInstanceEntity parent)
        {
            string nodeName;
            if (Index == 2)
            {
                nodeName = "BK_Knee";
            }
            else if (Index == 1)
            {
                nodeName = "R_Knee";
            }
            else
            {
                nodeName = "L_Knee";
            }
            ModelInstance ownerModel = parent.GetModels()[0];
            _kneeNode = ownerModel.Model.GetNodeByName(nodeName)!;
            Matrix4 transform = GetNodeTransform(_kneeNode, _gorea1A, _gorea1A.Scale);
            Position = transform.Row3.Xyz;
        }

        protected override void EnemyProcess()
        {
            Matrix4 transform = GetNodeTransform(_kneeNode, _gorea1A, _gorea1A.Scale);
            Position = transform.Row3.Xyz;
            Vector3 cylinderVec = transform.Row0.Xyz.Normalized();
            if (Index != 1)
            {
                cylinderVec *= -1;
            }
            Vector3 cylinderPos = cylinderVec * Fixed.ToFloat(-9700);
            _hurtVolumeInit = new CollisionVolume(cylinderVec, cylinderPos, _hurtVolumeInit.CylinderRadius, _hurtVolumeInit.CylinderDot);
            CheckPlayerCollision(factor: 0.25f, damage: 10); // 1024
        }

        private void CheckPlayerCollision(float factor, int damage)
        {
            if (!HitPlayers[PlayerEntity.Main.SlotIndex])
            {
                return;
            }
            Vector3 between = (PlayerEntity.Main.Position - Position).WithY(0);
            between = between.LengthSquared > 1 / 128f
                ? between.Normalized()
                : FacingVector;
            between *= factor;
            PlayerEntity.Main.Speed += between / 2; // todo: FPS stuff
            PlayerEntity.Main.TakeDamage(damage, DamageFlags.None, null, this);
        }

        protected override bool EnemyTakeDamage(EntityBase? source)
        {
            _health = 65535;
            return false;
        }
    }
}
