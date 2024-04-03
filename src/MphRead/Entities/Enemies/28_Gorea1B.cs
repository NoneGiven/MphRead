using System;
using System.Collections.Generic;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities.Enemies
{
    public class Enemy28Entity : GoreaEnemyEntityBase
    {
        private Enemy24Entity _gorea1A = null!;
        private Enemy29Entity _sealSphere = null!;
        public Enemy29Entity SealSphere => _sealSphere;
        private Node _spineNode = null!;
        private readonly Enemy30Entity[] _trocra = new Enemy30Entity[30];
        private CollisionVolume _volume;
        private Gorea1BFlags _goreaFlags;

        private int _phasesLeft = 0;
        public int PhasesLeft => _phasesLeft;
        private Vector3 _field1BC;
        private int _field1C8 = 0;
        private int _field1CA = 0;

        public Enemy28Entity(EnemyInstanceEntityData data, NodeRef nodeRef, Scene scene)
            : base(data, nodeRef, scene)
        {
        }

        protected override void EnemyInitialize()
        {
            if (_owner is Enemy24Entity owner)
            {
                _gorea1A = owner;
                InitializeCommon(owner.Spawner);
                Flags |= EnemyFlags.OnRadar;
                Flags |= EnemyFlags.Invincible;
                Flags &= ~EnemyFlags.CollidePlayer;
                Flags &= ~EnemyFlags.CollideBeam;
                _scanId = 0;
                _state1 = _state2 = 0;
                SetTransform(owner.FacingVector, UpVector, owner.Position);
                _prevPos = Position;
                _hurtVolumeInit = new CollisionVolume(Vector3.Zero, Fixed.ToFloat(4098));
                Scale = owner.Scale;
                _model = SetUpModel("Gorea1B_lod0");
                _model.NodeAnimIgnoreRoot = true;
                _model.Model.ComputeNodeMatrices(index: 0);
                _model.SetAnimation(3, AnimFlags.NoLoop);
                _spineNode = _model.Model.GetNodeByName("Spine_02")!;
                _volume = CollisionVolume.Move(new CollisionVolume(
                    owner.Spawner.Data.Fields.S11.Sphere2Position.ToFloatVector(),
                    owner.Spawner.Data.Fields.S11.Sphere2Radius.FloatValue), Position);
                _field1C8 = (int)(Rng.GetRandomInt2(13) + 7) * 2; // todo: FPS stuff
                _field1CA = 30 * 2; // todo: FPS stuff
                _phasesLeft = 3;
                if (Rng.GetRandomInt2(255) % 2 != 0)
                {
                    _goreaFlags |= Gorea1BFlags.Bit3;
                }
                if (EnemySpawnEntity.SpawnEnemy(this, EnemyType.GoreaSealSphere1, NodeRef, _scene) is Enemy29Entity sealSphere)
                {
                    _scene.AddEntity(sealSphere);
                    _sealSphere = sealSphere;
                }
            }
        }

        public void Activate()
        {
            _scanId = Metadata.EnemyScanIds[(int)EnemyType];
            Flags |= EnemyFlags.Visible;
            Flags |= EnemyFlags.CollidePlayer;
            Flags |= EnemyFlags.CollideBeam;
            Flags |= EnemyFlags.Invincible;
            Flags |= EnemyFlags.NoHomingNc;
            Flags |= EnemyFlags.NoHomingCo;
            Flags |= EnemyFlags.OnRadar;
            _field1BC = Vector3.Zero;
            _goreaFlags |= Gorea1BFlags.Bit4;
            _sealSphere.Activate();
            ActivateTrocraSpawns();
            UpdateMaterials();
            SetTransform(_gorea1A.FacingVector, UpVector, _gorea1A.Position);
        }

        private void ActivateTrocraSpawns()
        {
            int count = _phasesLeft == 3 ? 1 : 2;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.EnemySpawn && entity is EnemySpawnEntity spawner
                    && spawner.Data.EnemyType == EnemyType.Trocra && !spawner.Flags.TestFlag(SpawnerFlags.Active))
                {
                    _scene.SendMessage(Message.Activate, this, spawner, 0, 0);
                    if (_phasesLeft != 1 && --count == 0)
                    {
                        break;
                    }
                }
            }
        }

        private readonly IReadOnlyList<string> _bodyMatNames1 = new string[6]
        {
            "ChestMembrane", "Eye", "Head1", "Legs", "Torso", "Shoulder"
        };

        private readonly IReadOnlyList<string> _bodyMatNames2 = new string[2]
        {
            "ChestCore", "HeadFullLit"
        };

        private void UpdateMaterials()
        {
            for (int i = 0; i < 6; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames1[i])!;
                material.Ambient = _gorea1A.Colors[2];
                material.Diffuse = _gorea1A.Colors[3];
            }
            for (int i = 0; i < 2; i++)
            {
                Material material = _model.Model.GetMaterialByName(_bodyMatNames2[i])!;
                material.Ambient = _gorea1A.Colors[0];
                material.Diffuse = _gorea1A.Colors[1];
            }
            _sealSphere.Ambient = _gorea1A.Colors[0];
            _sealSphere.Diffuse = _gorea1A.Colors[1];
        }

        public void DrawSelf()
        {
            DrawGeneric();
        }

        protected override bool EnemyGetDrawInfo()
        {
            // sktodo
            return true;
        }
    }

    [Flags]
    public enum Gorea1BFlags : byte
    {
        None = 0x0,
        Bit0 = 0x1,
        Bit1 = 0x2,
        Bit2 = 0x4,
        Bit3 = 0x8,
        Bit4 = 0x10,
        Bit05 = 0x20,
        Bit06 = 0x40,
        Bit07 = 0x80
    }
}
