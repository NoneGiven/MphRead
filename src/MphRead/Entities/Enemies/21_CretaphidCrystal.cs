using System.Diagnostics;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy21Entity : EnemyInstanceEntity
    {
        private readonly Enemy19Entity _cretaphid;
        private Node _attachNode = null!;
        private EquipInfo _equipInfo = null!;
        private int _ammo = 1000;

        public Enemy21Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
            var owner = data.Spawner as Enemy19Entity;
            Debug.Assert(owner != null);
            _cretaphid = owner;
        }

        public void SetUp(Node attachNode, int scanId, uint effectiveness, ushort health, Vector3 position)
        {
            HealthbarMessageId = 1;
            _attachNode = attachNode;
            _scanId = scanId;
            Metadata.LoadEffectiveness(effectiveness, BeamEffectiveness);
            _health = _healthMax = health;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoMaxDistance;
            Matrix4 transform = GetTransformMatrix(attachNode.Transform.Row2.Xyz, attachNode.Transform.Row1.Xyz);
            transform.Row3.Xyz = attachNode.Transform.Row3.Xyz + position;
            Transform = transform;
            _hurtVolumeInit = new CollisionVolume(Vector3.Zero, 1);
            _boundingRadius = 1;
            _equipInfo = new EquipInfo(Weapons.BossWeapons[0], _beams);
            _equipInfo.GetAmmo = () => _ammo;
            _equipInfo.SetAmmo = (newAmmo) => _ammo = newAmmo;
        }

        public void SpawnBeam(ushort damage)
        {
            _equipInfo.Weapon.UnchargedDamage = damage;
            _equipInfo.Weapon.SplashDamage = damage;
            _equipInfo.Weapon.HeadshotDamage = damage;
            Vector3 spawnDir = (PlayerEntity.Main.Position.AddY(0.5f) - Position).Normalized();
            BeamProjectileEntity.Spawn(this, _equipInfo, Position, spawnDir, BeamSpawnFlags.None, _scene);
        }
    }
}
