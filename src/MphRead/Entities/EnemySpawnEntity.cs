using System;
using System.Diagnostics;
using MphRead.Entities.Enemies;
using MphRead.Formats.Collision;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    [Flags]
    public enum SpawnerFlags : byte
    {
        Suspended = 1,
        Active = 2,
        HasModel = 4,
        PlayAnimation = 8,
        CounterBit0 = 0x10, // we don't use these
        CounterBit1 = 0x20,
        CounterBit2 = 0x40,
        CounterBit3 = 0x80
    }

    public class EnemySpawnEntity : EntityBase
    {
        private readonly EnemySpawnEntityData _data;
        private int _activeCount = 0; // todo: names are backwards?
        private int _spawnedCount = 0;
        private int _cooldownTimer = 0;
        private EntityBase? _entity1;
        private EntityBase? _entity2;
        private EntityBase? _entity3;
        private EntityBase? _parent;
        public EntityCollision? ParentEntCol { get; private set; } = null;
        private Matrix4 _invTransform = Matrix4.Identity;
        private NodeRef _rangeNodeRef = NodeRef.None;

        public SpawnerFlags Flags { get; set; }
        public EnemySpawnEntityData Data => _data;

        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        public EnemySpawnEntity(EnemySpawnEntityData data, string nodeName, Scene scene)
            : base(EntityType.EnemySpawn, nodeName, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            _rangeNodeRef = scene.GetNodeRefByName(data.NodeName.MarshalString());
            _cooldownTimer = _data.InitialCooldown * 2; // todo: FPS stuff
            Debug.Assert(scene.GameMode == GameMode.SinglePlayer);
            bool active = false;
            int state = GameState.StorySave.InitRoomState(_scene.RoomId, Id, active: data.Active != 0);
            if (data.AlwaysActive != 0)
            {
                active = data.Active != 0;
            }
            else
            {
                active = state != 0;
            }
            if (active)
            {
                Flags |= SpawnerFlags.Active;
            }
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            Flags |= SpawnerFlags.Suspended;
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_data.EntityId1 != -1)
            {
                _scene.TryGetEntity(_data.EntityId1, out _entity1);
            }
            if (_data.EntityId2 != -1)
            {
                _scene.TryGetEntity(_data.EntityId2, out _entity2);
            }
            if (_data.EntityId3 != -1)
            {
                _scene.TryGetEntity(_data.EntityId3, out _entity3);
            }
            if (_data.LinkedEntityId != -1)
            {
                _scene.TryGetEntity(_data.LinkedEntityId, out _parent);
                if (_parent != null)
                {
                    ParentEntCol = _parent.EntityCollision[0];
                    if (ParentEntCol != null)
                    {
                        _invTransform = _transform * ParentEntCol.Inverse2;
                    }
                }
            }
            if (_data.SpawnerHealth > 0)
            {
                EnemyInstanceEntity? enemy = SpawnEnemy(this, EnemyType.Spawner, NodeRef, _scene);
                if (enemy != null)
                {
                    _scene.AddEntity(enemy);
                }
            }
        }

        public override void SetActive(bool active)
        {
            base.SetActive(active);
            Flags |= SpawnerFlags.Active;
        }

        public override bool Process()
        {
            if (!Flags.TestFlag(SpawnerFlags.Active))
            {
                return base.Process();
            }
            if (ParentEntCol != null)
            {
                Transform = _invTransform * ParentEntCol.Transform;
            }
            if (_cooldownTimer > 0)
            {
                _cooldownTimer--;
            }
            if (Flags.TestFlag(SpawnerFlags.Suspended) && _cooldownTimer == 0)
            {
                if (_rangeNodeRef != NodeRef.None && _scene.CameraMode == CameraMode.Player) // skdebug
                {
                    for (int i = 0; i < _scene.Entities.Count; i++)
                    {
                        EntityBase entity = _scene.Entities[i];
                        if (entity.Type != EntityType.Player)
                        {
                            continue;
                        }
                        var player = (PlayerEntity)entity;
                        if (player.Health > 0 && player.NodeRef == _rangeNodeRef)
                        {
                            Flags &= ~SpawnerFlags.Suspended;
                        }
                    }
                }
                else
                {
                    Flags &= ~SpawnerFlags.Suspended;
                }
            }
            if (Flags.TestFlag(SpawnerFlags.Suspended))
            {
                return base.Process();
            }
            float distSqr = _data.ActiveDistance.FloatValue;
            distSqr *= distSqr;
            bool inRange = false;
            if (_data.EnemyType != EnemyType.CarnivorousPlant // the game doesn't have this condition
                && _scene.CameraMode == CameraMode.Player) // skdebug
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    if (Vector3.DistanceSquared(Position, entity.Position) < distSqr)
                    {
                        inRange = true;
                        break;
                    }
                }
            }
            else
            {
                inRange = true;
            }
            if (!inRange)
            {
                Flags |= SpawnerFlags.Suspended;
            }
            else if (_spawnedCount < _data.SpawnTotal && _cooldownTimer == 0 && _data.SpawnCount > 0
                && (_data.SpawnLimit == 0 || _activeCount < _data.SpawnLimit))
            {
                for (int i = 0; i < _data.SpawnCount; i++)
                {
                    EntityBase? spawned;
                    if (_data.EnemyType == EnemyType.Hunter)
                    {
                        spawned = SpawnHunter();
                    }
                    else
                    {
                        spawned = SpawnEnemy(this, _data.EnemyType, NodeRef, _scene);
                    }
                    if (spawned == null)
                    {
                        break;
                    }
                    if (_data.EnemyType != EnemyType.Hunter)
                    {
                        _scene.AddEntity(spawned);
                    }
                    if (Flags.TestFlag(SpawnerFlags.HasModel))
                    {
                        Flags |= SpawnerFlags.PlayAnimation;
                    }
                    _spawnedCount++;
                    _activeCount++;
                    _cooldownTimer = _data.CooldownTime * 2; // todo: FPS stuff
                    if (_spawnedCount >= _data.SpawnTotal || _data.SpawnLimit > 0 && _activeCount >= _data.SpawnLimit)
                    {
                        break;
                    }
                }
            }
            if (_data.SpawnLimit > 0 && _activeCount >= _data.SpawnLimit && _spawnedCount == 0)
            {
                DeactivateAndSendMessages();
            }
            return base.Process();
        }

        private PlayerEntity? SpawnHunter()
        {
            PlayerEntity? player = null;
            for (int i = 0; i < 4; i++)
            {
                player = PlayerEntity.Players[i];
                if (player.Health == 0 && player.EnemySpawner == this)
                {
                    player.Spawn(Position, FacingVector, UpVector, NodeRef, respawn: true);
                    player.InitEnemyHunter();
                    break;
                }
            }
            return player;
        }

        private void DeactivateAndSendMessages()
        {
            bool updateSave = false;

            void UpdateBossFlags()
            {
                uint flags = (uint)GameState.StorySave.BossFlags;
                flags &= (uint)~(3 << (2 * _scene.AreaId));
                flags |= (uint)(1 << (2 * _scene.AreaId));
                GameState.StorySave.BossFlags = (BossFlags)flags;
                updateSave = true;
            }

            Flags &= ~SpawnerFlags.Active;
            GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
            if (_data.EnemyType != EnemyType.Hunter || _data.Fields.S09.EncounterType == 1)
            {
                // todo: update completed encounters in story save (unused?)
            }
            if (_data.EnemyType == EnemyType.Cretaphid)
            {
                GameState.StorySave.Areas |= 3; // Alinos 1 & 2
                UpdateBossFlags();
            }
            else if (_data.EnemyType == EnemyType.Slench)
            {
                GameState.StorySave.Areas |= 0xF0; // VDO 1 & 2, Arcterra 1 & 2
                UpdateBossFlags();
            }
            else if (_data.EnemyType == EnemyType.Gorea1A)
            {
                UpdateBossFlags();
            }
            if (_entity1 != null)
            {
                _scene.SendMessage(_data.Message1, this, _entity1, -1, 0);
            }
            if (_entity2 != null)
            {
                _scene.SendMessage(_data.Message2, this, _entity2, -1, 0);
            }
            if (_entity3 != null)
            {
                _scene.SendMessage(_data.Message3, this, _entity3, -1, 0);
            }
            if (updateSave)
            {
                GameState.UpdateCleanSave(force: false);
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Destroyed)
            {
                if ((int)info.Param1 != 0)
                {
                    // enemy was out of range
                    --_spawnedCount;
                    --_activeCount;
                    _cooldownTimer = 0;
                }
                else
                {
                    if (info.Sender.Type == EntityType.EnemyInstance
                        && ((EnemyInstanceEntity)info.Sender).EnemyType == EnemyType.Spawner)
                    {
                        Flags &= ~SpawnerFlags.Active;
                        GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
                    }
                    else
                    {
                        --_spawnedCount;
                        _cooldownTimer = _data.CooldownTime * 2; // todo: FPS stuff
                    }
                }
                if (!Flags.TestFlag(SpawnerFlags.Active) && _spawnedCount == 0)
                {
                    DeactivateAndSendMessages();
                }
            }
            else if (info.Message == Message.Activate)
            {
                Flags |= SpawnerFlags.Active;
                GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
            }
            else if (info.Message == Message.SetActive)
            {
                if ((int)info.Param1 != 0)
                {
                    Flags |= SpawnerFlags.Active;
                    GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 3);
                }
                else
                {
                    Flags &= ~SpawnerFlags.Active;
                    GameState.StorySave.SetRoomState(_scene.RoomId, Id, state: 1);
                }
            }
            else if (info.Message == Message.Unknown36)
            {
                // todo-gorea: pass to Gorea2
            }
        }

        public static EnemyInstanceEntity? SpawnEnemy(EntityBase spawner, EnemyType type, NodeRef nodeRef, Scene scene)
        {
            if (type == EnemyType.WarWasp)
            {
                return new Enemy00Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Zoomer)
            {
                return new Enemy01Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Temroid)
            {
                return new Enemy02Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Petrasyl1)
            {
                return new Enemy03Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Petrasyl2)
            {
                return new Enemy04Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Petrasyl3)
            {
                return new Enemy05Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Petrasyl4)
            {
                return new Enemy06Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.BarbedWarWasp)
            {
                return new Enemy10Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Shriekbat)
            {
                return new Enemy11Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Geemer)
            {
                return new Enemy12Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Blastcap)
            {
                return new Enemy16Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.AlimbicTurret)
            {
                return new Enemy18Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Cretaphid)
            {
                return new Enemy19Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.CretaphidEye)
            {
                return new Enemy20Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.CretaphidCrystal)
            {
                return new Enemy21Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Slench)
            {
                return new Enemy41Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.SlenchShield)
            {
                return new Enemy42Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.SlenchNest)
            {
                return new Enemy43Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.SlenchSynapse)
            {
                return new Enemy44Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.SlenchTurret)
            {
                return new Enemy45Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.PsychoBit1)
            {
                return new Enemy23Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Voldrum2)
            {
                return new Enemy35Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Voldrum1)
            {
                return new Enemy36Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Quadtroid)
            {
                return new Enemy37Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.CrashPillar)
            {
                return new Enemy38Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.FireSpawn)
            {
                return new Enemy39Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.Spawner)
            {
                return new Enemy40Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.LesserIthrak)
            {
                return new Enemy46Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.GreaterIthrak)
            {
                return new Enemy47Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.ForceFieldLock)
            {
                return new Enemy49Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.HitZone)
            {
                return new Enemy50Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            if (type == EnemyType.CarnivorousPlant)
            {
                return new Enemy51Entity(new EnemyInstanceEntityData(type, spawner), nodeRef, scene);
            }
            //throw new ProgramException("Invalid enemy type."); // also make non-nullable
            return null;
        }
    }

    public class FhEnemySpawnEntity : EntityBase
    {
        private readonly FhEnemySpawnEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0x00, 0x00, 0x8B).AsVector4();

        // todo: FH enemy spawning
        public FhEnemySpawnEntity(FhEnemySpawnEntityData data, Scene scene) : base(EntityType.EnemySpawn, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
        }
    }
}
