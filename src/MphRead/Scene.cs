using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MphRead.Entities;

namespace MphRead
{
    public partial class Scene
    {
        private readonly LinkedList<EntityBase> _entities = new LinkedList<EntityBase>();
        public LinkedListIterator<EntityBase> Entities => new LinkedListIterator<EntityBase>(_entities);

        private readonly Dictionary<int, EntityBase> _entityMap = new Dictionary<int, EntityBase>();

        // called after load -- entity needs init
        public void AddEntity(EntityBase entity)
        {
            InsertEntity(entity);
            InitializeEntity(entity);
        }

        public void InsertEntity(EntityBase entity)
        {
            InsertEntityByType(entity);
            if (entity.Id != -1)
            {
                _entityMap.Add(entity.Id, entity);
            }
        }

        private void InsertEntityByType(EntityBase entity)
        {
            bool isFirstOfType = true;
            for (LinkedListNode<EntityBase>? item = _entities.First; item != null; item = item.Next)
            {
                EntityBase existing = item.Value;
                if (existing.Type == entity.Type)
                {
                    isFirstOfType = false;
                }
                if (existing.Type != EntityType.Room && existing.Type > entity.Type)
                {
                    LinkedListNode<EntityBase> newNode = _entities.AddBefore(item, entity);
                    if (isFirstOfType)
                    {
                        _entityNodesByType[entity.Type] = newNode;
                    }
                    return;
                }
            }
            LinkedListNode<EntityBase> lastNode = _entities.AddLast(entity);
            if (isFirstOfType)
            {
                _entityNodesByType[entity.Type] = lastNode;
            }
        }

        public void InitializeEntity(EntityBase entity)
        {
            // important to call in this order because the entity may add models (at least in development)
            entity.Initialize();
            InitEntity(entity);
        }

        public bool TryGetEntity(int id, [NotNullWhen(true)] out EntityBase? entity)
        {
            return _entityMap.TryGetValue(id, out entity);
        }

        public void RemoveEntity(EntityBase entity)
        {
            _entityMap.Remove(entity.Id);
            // this is slightly less efficient than if we already had the node (like in-game, where the entity class *is* the node),
            // but since we can use the map by type, we at least don't need to iterate the entire list, just the entities of this type
            if (_entityNodesByType.TryGetValue(entity.Type, out LinkedListNode<EntityBase>? node))
            {
                while (node != null && node.Value != entity)
                {
                    node = node.Next;
                }
            }
            else
            {
                // not expected to occur
                node = _entities.Find(entity);
            }
            if (node != null)
            {
                if (_entityNodesByType[entity.Type] == node)
                {
                    _entityNodesByType[entity.Type] = node.Next?.Value.Type == entity.Type ? node.Next : null;
                }
                _entities.Remove(node);
            }
        }

        public void RemoveEntityFromMap(EntityBase entity)
        {
            _entityMap.Remove(entity.Id);
        }

        #region Iterator Methods

        public LinkedListIteratorSpecialized<PlatformEntity> GetPlatformEntities()
        {
            return new LinkedListIteratorSpecialized<PlatformEntity>(_entityNodesByType[EntityType.Platform]);
        }

        public LinkedListIteratorSpecialized<ObjectEntity> GetObjectEntities()
        {
            return new LinkedListIteratorSpecialized<ObjectEntity>(_entityNodesByType[EntityType.Object]);
        }

        public LinkedListIteratorSpecialized<PlayerSpawnEntity> GetPlayerSpawnEntities()
        {
            return new LinkedListIteratorSpecialized<PlayerSpawnEntity>(_entityNodesByType[EntityType.PlayerSpawn]);
        }

        public LinkedListIteratorSpecialized<DoorEntity> GetDoorEntities()
        {
            return new LinkedListIteratorSpecialized<DoorEntity>(_entityNodesByType[EntityType.Door]);
        }

        public LinkedListIteratorSpecialized<ItemSpawnEntity> GetItemSpawnEntities()
        {
            return new LinkedListIteratorSpecialized<ItemSpawnEntity>(_entityNodesByType[EntityType.ItemSpawn]);
        }

        public LinkedListIteratorSpecialized<ItemInstanceEntity> GetItemInstanceEntities()
        {
            return new LinkedListIteratorSpecialized<ItemInstanceEntity>(_entityNodesByType[EntityType.ItemInstance]);
        }

        public LinkedListIteratorSpecialized<EnemySpawnEntity> GetEnemySpawnEntities()
        {
            return new LinkedListIteratorSpecialized<EnemySpawnEntity>(_entityNodesByType[EntityType.EnemySpawn]);
        }

        public LinkedListIteratorSpecialized<TriggerVolumeEntity> GetTriggerVolumeEntities()
        {
            return new LinkedListIteratorSpecialized<TriggerVolumeEntity>(_entityNodesByType[EntityType.TriggerVolume]);
        }

        public LinkedListIteratorSpecialized<AreaVolumeEntity> GetAreaVolumeEntities()
        {
            return new LinkedListIteratorSpecialized<AreaVolumeEntity>(_entityNodesByType[EntityType.AreaVolume]);
        }

        public LinkedListIteratorSpecialized<JumpPadEntity> GetJumpPadEntities()
        {
            return new LinkedListIteratorSpecialized<JumpPadEntity>(_entityNodesByType[EntityType.JumpPad]);
        }

        public LinkedListIteratorSpecialized<PointModuleEntity> GetPointModuleEntities()
        {
            return new LinkedListIteratorSpecialized<PointModuleEntity>(_entityNodesByType[EntityType.PointModule]);
        }

        public LinkedListIteratorSpecialized<MorphCameraEntity> GetMorphCameraEntities()
        {
            return new LinkedListIteratorSpecialized<MorphCameraEntity>(_entityNodesByType[EntityType.MorphCamera]);
        }

        public LinkedListIteratorSpecialized<OctolithFlagEntity> GetOctolithFlagEntities()
        {
            return new LinkedListIteratorSpecialized<OctolithFlagEntity>(_entityNodesByType[EntityType.OctolithFlag]);
        }

        public LinkedListIteratorSpecialized<FlagBaseEntity> GetFlagBaseEntities()
        {
            return new LinkedListIteratorSpecialized<FlagBaseEntity>(_entityNodesByType[EntityType.FhBomb]);
        }

        public LinkedListIteratorSpecialized<TeleporterEntity> GetTeleporterEntities()
        {
            return new LinkedListIteratorSpecialized<TeleporterEntity>(_entityNodesByType[EntityType.Teleporter]);
        }

        public LinkedListIteratorSpecialized<NodeDefenseEntity> GetNodeDefenseEntities()
        {
            return new LinkedListIteratorSpecialized<NodeDefenseEntity>(_entityNodesByType[EntityType.NodeDefense]);
        }

        public LinkedListIteratorSpecialized<LightSourceEntity> GetLightSourceEntities()
        {
            return new LinkedListIteratorSpecialized<LightSourceEntity>(_entityNodesByType[EntityType.LightSource]);
        }

        public LinkedListIteratorSpecialized<ArtifactEntity> GetArtifactEntities()
        {
            return new LinkedListIteratorSpecialized<ArtifactEntity>(_entityNodesByType[EntityType.Artifact]);
        }

        public LinkedListIteratorSpecialized<CamSeqEntity> GetCamSeqEntities()
        {
            return new LinkedListIteratorSpecialized<CamSeqEntity>(_entityNodesByType[EntityType.CameraSequence]);
        }

        public LinkedListIteratorSpecialized<ForceFieldEntity> GetForceFieldEntities()
        {
            return new LinkedListIteratorSpecialized<ForceFieldEntity>(_entityNodesByType[EntityType.ForceField]);
        }

        public LinkedListIteratorSpecialized<BeamEffectEntity> GetBeamEffectEntities()
        {
            return new LinkedListIteratorSpecialized<BeamEffectEntity>(_entityNodesByType[EntityType.BeamEffect]);
        }

        public LinkedListIteratorSpecialized<BombEntity> GetBombEntities()
        {
            return new LinkedListIteratorSpecialized<BombEntity>(_entityNodesByType[EntityType.Bomb]);
        }

        public LinkedListIteratorSpecialized<EnemyInstanceEntity> GetEnemyInstanceEntities()
        {
            return new LinkedListIteratorSpecialized<EnemyInstanceEntity>(_entityNodesByType[EntityType.EnemyInstance]);
        }

        public LinkedListIteratorSpecialized<HalfturretEntity> GetHalfturretEntities()
        {
            return new LinkedListIteratorSpecialized<HalfturretEntity>(_entityNodesByType[EntityType.Halfturret]);
        }

        public LinkedListIteratorSpecialized<PlayerEntity> GetPlayerEntities()
        {
            return new LinkedListIteratorSpecialized<PlayerEntity>(_entityNodesByType[EntityType.Player]);
        }

        public LinkedListIteratorSpecialized<BeamProjectileEntity> GetBeamProjectileEntities()
        {
            return new LinkedListIteratorSpecialized<BeamProjectileEntity>(_entityNodesByType[EntityType.BeamProjectile]);
        }

        public LinkedListIteratorSpecialized<FhDoorEntity> GetFhDoorEntities()
        {
            return new LinkedListIteratorSpecialized<FhDoorEntity>(_entityNodesByType[EntityType.FhDoor]);
        }

        public LinkedListIteratorSpecialized<FhItemSpawnEntity> GetFhItemSpawnEntities()
        {
            return new LinkedListIteratorSpecialized<FhItemSpawnEntity>(_entityNodesByType[EntityType.FhItemSpawn]);
        }

        public LinkedListIteratorSpecialized<FhEnemySpawnEntity> GetFhEnemySpawnEntities()
        {
            return new LinkedListIteratorSpecialized<FhEnemySpawnEntity>(_entityNodesByType[EntityType.FhEnemySpawn]);
        }

        public LinkedListIteratorSpecialized<FhTriggerVolumeEntity> GetFhTriggerVolumeEntities()
        {
            return new LinkedListIteratorSpecialized<FhTriggerVolumeEntity>(_entityNodesByType[EntityType.FhTriggerVolume]);
        }

        public LinkedListIteratorSpecialized<FhAreaVolumeEntity> GetFhAreaVolumeEntities()
        {
            return new LinkedListIteratorSpecialized<FhAreaVolumeEntity>(_entityNodesByType[EntityType.FhAreaVolume]);
        }

        public LinkedListIteratorSpecialized<FhPlatformEntity> GetFhPlatformEntities()
        {
            return new LinkedListIteratorSpecialized<FhPlatformEntity>(_entityNodesByType[EntityType.FhPlatform]);
        }

        public LinkedListIteratorSpecialized<FhJumpPadEntity> GetFhJumpPadEntities()
        {
            return new LinkedListIteratorSpecialized<FhJumpPadEntity>(_entityNodesByType[EntityType.FhJumpPad]);
        }

        public LinkedListIteratorSpecialized<FhMorphCameraEntity> GetFhMorphCameraEntities()
        {
            return new LinkedListIteratorSpecialized<FhMorphCameraEntity>(_entityNodesByType[EntityType.FhMorphCamera]);
        }

        #endregion

        private readonly Dictionary<EntityType, LinkedListNode<EntityBase>?> _entityNodesByType
            = new Dictionary<EntityType, LinkedListNode<EntityBase>?>()
            {
                { EntityType.Platform, null },
                { EntityType.Object, null },
                { EntityType.PlayerSpawn, null },
                { EntityType.Door, null },
                { EntityType.ItemSpawn, null },
                { EntityType.ItemInstance, null },
                { EntityType.EnemySpawn, null },
                { EntityType.TriggerVolume, null },
                { EntityType.AreaVolume, null },
                { EntityType.JumpPad, null },
                { EntityType.PointModule, null },
                { EntityType.MorphCamera, null },
                { EntityType.OctolithFlag, null },
                { EntityType.FlagBase, null },
                { EntityType.Teleporter, null },
                { EntityType.NodeDefense, null },
                { EntityType.LightSource, null },
                { EntityType.Artifact, null },
                { EntityType.CameraSequence, null },
                { EntityType.ForceField, null },
                { EntityType.BeamEffect, null },
                { EntityType.Bomb, null },
                { EntityType.EnemyInstance, null },
                { EntityType.Halfturret, null },
                { EntityType.Player, null },
                { EntityType.BeamProjectile, null },
                { EntityType.FhUnknown0, null },
                { EntityType.FhPlayerSpawn, null },
                { EntityType.FhUnknown2, null },
                { EntityType.FhDoor, null },
                { EntityType.FhItemSpawn, null },
                { EntityType.FhItemInstance, null },
                { EntityType.FhEnemySpawn, null },
                { EntityType.FhEffectInstance, null },
                { EntityType.FhBomb, null },
                { EntityType.FhTriggerVolume, null },
                { EntityType.FhAreaVolume, null },
                { EntityType.FhPlatform, null },
                { EntityType.FhJumpPad, null },
                { EntityType.FhPointModule, null },
                { EntityType.FhMorphCamera, null },
                { EntityType.FhEnemyInstance, null },
                { EntityType.FhPlayer, null },
                { EntityType.FhBeamProjectile, null }
            };
    }

    // this class supports allocation-free foreach while also handling entities being added or removed during enumeration.
    // access to the head/tail nodes is provided for custom iteration in a couple places (where adds/removes do not occur).
    public readonly struct LinkedListIterator<T> where T : class
    {
        private readonly LinkedList<T> _list;
        public int Count => _list.Count;

        public LinkedListNode<T>? FirstNode => _list.First;
        public LinkedListNode<T>? LastNode => _list.Last;

        public LinkedListIterator(LinkedList<T> list)
        {
            _list = list;
        }

        public LinkedListEnumerator<T> GetEnumerator()
        {
            return new LinkedListEnumerator<T>(_list.First);
        }
    }

    public struct LinkedListEnumerator<T>
    {
        private bool first = true;
        private LinkedListNode<T>? _node;
        private LinkedListNode<T>? _next;

        public T Current => _node == null ? throw new InvalidOperationException() : _node.Value;

        public LinkedListEnumerator(LinkedListNode<T>? first)
        {
            _node = first;
            _next = first?.Next;
        }

        public bool MoveNext()
        {
            if (first)
            {
                first = false;
                return _node != null;
            }
            Debug.Assert(_node != null);
            LinkedListNode<T>? next = _node.Next ?? _next;
            if (next == null)
            {
                return false;
            }
            _node = next;
            _next = next.Next;
            return true;
        }
    }

    // similar to the above, but returning only a specific Entity eype, starting iteration using the predetermined
    // first node of its type, and ending when another entity type is encountered (or at the end of the list).
    // this leverages the fact that entities of each type are contiguous in the list and in ascending type ID order.
    public readonly struct LinkedListIteratorSpecialized<T> where T : EntityBase
    {
        private readonly LinkedListNode<EntityBase>? _node;

        public LinkedListIteratorSpecialized(LinkedListNode<EntityBase>? node)
        {
            _node = node;
        }

        public LinkedListEnumeratorSpecialized<T> GetEnumerator()
        {
            return new LinkedListEnumeratorSpecialized<T>(_node);
        }
    }

    public struct LinkedListEnumeratorSpecialized<T> where T : EntityBase
    {
        private bool first = true;
        private LinkedListNode<EntityBase>? _node;
        private LinkedListNode<EntityBase>? _next;
        private readonly EntityType _entityType;

        public T Current => _node == null ? throw new InvalidOperationException() : (T)_node.Value;

        private static readonly FrozenDictionary<Type, EntityType> _typeMap = Frozen.Create<Type, EntityType>(
        [
            new(typeof(PlatformEntity), EntityType.Platform),
            new(typeof(ObjectEntity), EntityType.Object),
            new(typeof(PlayerSpawnEntity), EntityType.PlayerSpawn),
            new(typeof(DoorEntity), EntityType.Door),
            new(typeof(ItemSpawnEntity), EntityType.ItemSpawn),
            new(typeof(ItemInstanceEntity), EntityType.ItemInstance),
            new(typeof(EnemySpawnEntity), EntityType.EnemySpawn),
            new(typeof(TriggerVolumeEntity), EntityType.TriggerVolume),
            new(typeof(AreaVolumeEntity), EntityType.AreaVolume),
            new(typeof(JumpPadEntity), EntityType.JumpPad),
            new(typeof(PointModuleEntity), EntityType.PointModule),
            new(typeof(MorphCameraEntity), EntityType.MorphCamera),
            new(typeof(OctolithFlagEntity), EntityType.OctolithFlag),
            new(typeof(FlagBaseEntity), EntityType.FlagBase),
            new(typeof(TeleporterEntity), EntityType.Teleporter),
            new(typeof(NodeDefenseEntity), EntityType.NodeDefense),
            new(typeof(LightSourceEntity), EntityType.LightSource),
            new(typeof(ArtifactEntity), EntityType.Artifact),
            new(typeof(CamSeqEntity), EntityType.CameraSequence),
            new(typeof(ForceFieldEntity), EntityType.ForceField),
            new(typeof(BeamEffectEntity), EntityType.BeamEffect),
            new(typeof(BombEntity), EntityType.Bomb),
            new(typeof(EnemyInstanceEntity), EntityType.EnemyInstance),
            new(typeof(HalfturretEntity), EntityType.Halfturret),
            new(typeof(PlayerEntity), EntityType.Player),
            new(typeof(BeamProjectileEntity), EntityType.BeamProjectile),
            // todo: revisit for First Hunt entity types
            //new(typeof(), EntityType.FhUnknown0),
            //new(typeof(), EntityType.FhPlayerSpawn),
            //new(typeof(), EntityType.FhUnknown2),
            new(typeof(FhDoorEntity), EntityType.FhDoor),
            new(typeof(FhItemSpawnEntity), EntityType.FhItemSpawn),
            //new(typeof(), EntityType.FhItemInstance),
            new(typeof(FhEnemySpawnEntity), EntityType.FhEnemySpawn),
            //new(typeof(), EntityType.FhEffectInstance),
            //new(typeof(), EntityType.FhBomb),
            new(typeof(FhTriggerVolumeEntity), EntityType.FhTriggerVolume),
            new(typeof(FhAreaVolumeEntity), EntityType.FhAreaVolume),
            new(typeof(FhPlatformEntity), EntityType.FhPlatform),
            new(typeof(FhJumpPadEntity), EntityType.FhJumpPad),
            //new(typeof(), EntityType.FhPointModule),
            new(typeof(FhMorphCameraEntity), EntityType.FhMorphCamera)
            //new(typeof(), EntityType.FhEnemyInstance),
            //new(typeof(), EntityType.FhPlayer),
            //new(typeof(), EntityType.FhBeamProjectile)
        ]);

        public LinkedListEnumeratorSpecialized(LinkedListNode<EntityBase>? first)
        {
            _node = first;
            _next = first?.Next;
            _entityType = _typeMap[typeof(T)];
        }

        public bool MoveNext()
        {
            if (first)
            {
                first = false;
                return _node != null;
            }
            Debug.Assert(_node != null);
            LinkedListNode<EntityBase>? next = _node.Next ?? _next;
            if (next == null || next.Value.Type > _entityType)
            {
                return false;
            }
            _node = next;
            _next = next.Next;
            return true;
        }
    }
}
