using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MphRead.Formats;
using MphRead.Formats.Collision;
using MphRead.Formats.Culling;
using MphRead.Sound;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class RoomEntity : EntityBase
    {
        private readonly List<CollisionInstance> _roomCollision = new List<CollisionInstance>();
        public IReadOnlyList<CollisionInstance> RoomCollision => _roomCollision;
        private readonly List<Portal> _portals = new List<Portal>();
        private readonly List<List<(Portal Portal, bool OtherSide)>> _portalSides = new List<List<(Portal, bool)>>();
        private readonly List<PortalNodeRef> _forceFields = new List<PortalNodeRef>();
        private IReadOnlyList<Node> Nodes => _models[0].Model.Nodes;
        private int _nextRoomPartId = 0;
        private int _doorPortalCount = 0;
        private RoomMetadata _meta = null!;
        private NodeData? _nodeData;
        public NodeData? NodeData => _nodeData;
        private readonly List<ModelInstance> _connectorModels = new List<ModelInstance>();
        private readonly float[] _emptyMatrixStack = Array.Empty<float>();

        protected override bool UseNodeTransform => false; // default -- will use transform if setting is enabled
        public int RoomId { get; private set; }
        public RoomMetadata Meta => _meta;

        // The world-space box each room part's geometry occupies, unioned
        // over the nodes that part draws. Built on demand and rebuilt when
        // the part count changes (a door adds a connector part mid-room).
        //
        // Its whole purpose is to say where a part is *not*. A part is
        // bounded by the planes of the portals opening out of it and by
        // nothing else, so a part with two portals is an unbounded wedge and
        // several parts' wedges overlap -- which is why GetNodeRefByPosition
        // could answer "part 5" for a point at the other end of the map. The
        // box is a superset of the part's geometry, so a point outside it is
        // outside the part for certain, and rejecting on it can only ever
        // remove a wrong answer.
        private readonly List<Vector3> _partBoundsMin = new List<Vector3>();
        private readonly List<Vector3> _partBoundsMax = new List<Vector3>();
        private int _partBoundsBuiltFor = -1;

        /// <summary>
        /// How far outside a part's box a position may be and still be
        /// treated as possibly inside it, in units.
        ///
        /// The box covers the part's geometry, and a player stands *on* the
        /// geometry rather than inside it -- on a part made of nothing but a
        /// floor, an exact test puts every player above the room. A few units
        /// is enough for that and still tens of units short of the wrong end
        /// of a map, which is what the wedges were answering with.
        /// </summary>
        private const float _partBoundsMargin = 4;

        private readonly Dictionary<Node, Node> _nodePairs = new Dictionary<Node, Node>();
        private readonly HashSet<Node> _excludedNodes = new HashSet<Node>();
        private readonly List<Node> _morphCameraExcludeNodes = new List<Node>();

        // 1. move some/most of the setup from the constructor to a public method
        // 2. have scenesetup.load call that method with the room entity it creates
        // 3. have the room transition function call it with this instances
        // 4. --> the things that it updates should all be skipped in processing during room transition
        public RoomEntity(Scene scene) : base(EntityType.Room, scene)
        {
            for (int i = 0; i < _roomPartMax; i++)
            {
                _partVisInfo[i] = new RoomPartVisInfo();
                _roomFrustumItems[i] = new RoomFrustumItem();
            }
        }

        public void Setup(string name, RoomMetadata meta, CollisionInstance collision, int layerMask, int roomId)
        {
            // todo: unlock the corresponding multiplayer arena when visiting a new planet
            _portals.Clear();
            _portalSides.Clear();
            _forceFields.Clear();
            _nodePairs.Clear();
            _morphCameraExcludeNodes.Clear();
            _partBoundsBuiltFor = -1;
            _nextRoomPartId = 0;
            _doorPortalCount = 0;
            ModelInstance inst = Read.GetRoomModelInstance(name);
            if (_models.Count == 0)
            {
                _models.Add(inst);
                _scene.LoadModel(inst.Model, isRoom: true);
                inst.SetAnimation(0);
            }
            else
            {
                // todo?: unload collision, etc.
                _unloadModel = _models[0].Model;
                if (_unloadModel == inst.Model)
                {
                    _unloadModel = null;
                }
                _models[0] = inst;
            }
            inst.Model.FilterNodes(layerMask);
            if (meta.Name == "UNIT2_C6") // Tetra Vista
            {
                // manually disable a decal that isn't rendered in-game because it's not on a surface
                Nodes[46].Enabled = false;
            }
            else if (meta.Name == "UNIT1_RM4" || meta.Name == "MP3 PROVING GROUND") // Combat Hall
            {
                // depending on active partial rooms, either of these may be drawn,
                // but we have a rendering issue when both are, while the game looks the same either way
                _nodePairs.Add(Nodes[16], Nodes[26]); // after drawing skyLayer0, don't draw skyLayer3
                _nodePairs.Add(Nodes[25], Nodes[17]); // ...and vice versa
                _nodePairs.Add(Nodes[17], Nodes[25]); // after drawing skyLayer01, don't draw skyLayer04
                _nodePairs.Add(Nodes[26], Nodes[16]); // ...and vice versa
            }
            else if (meta.Name == "UNIT3_C2") // Cortex CPU
            {
                // hide a wall that mysteriously isn't visible when it renders in front of the morph camera
                _morphCameraExcludeNodes.Add(Nodes[16]);
            }
            _meta = meta;
            Model model = inst.Model;
            // portals are already filtered by layer mask
            _portals.AddRange(collision.Info.Portals);
            if (_portals.Count > 0)
            {
                IEnumerable<string> parts = _portals.Select(p => p.NodeName1).Concat(_portals.Select(p => p.NodeName2)).Distinct();
                for (int i = 0; i < inst.Model.Nodes.Count; i++)
                {
                    Node node = inst.Model.Nodes[i];
                    if (parts.Contains(node.Name))
                    {
                        node.RoomPartId = _nextRoomPartId++;
                        _portalSides.Add(new List<(Portal, bool)>());
                    }
                }
                for (int i = 0; i < _portals.Count; i++)
                {
                    Portal portal = _portals[i];
                    for (int j = 0; j < model.Nodes.Count; j++)
                    {
                        Node node = model.Nodes[j];
                        if (node.Name == portal.NodeName1)
                        {
                            Debug.Assert(node.RoomPartId >= 0);
                            Debug.Assert(node.ChildIndex != -1);
                            portal.NodeRef1 = new NodeRef(meta.Name, node.RoomPartId, node.ChildIndex, modelIndex: 0);
                            _portalSides[node.RoomPartId].Add((portal, false));
                        }
                        if (node.Name == portal.NodeName2)
                        {
                            Debug.Assert(node.RoomPartId >= 0);
                            Debug.Assert(node.ChildIndex != -1);
                            portal.NodeRef2 = new NodeRef(meta.Name, node.RoomPartId, node.ChildIndex, modelIndex: 0);
                            _portalSides[node.RoomPartId].Add((portal, true));
                        }
                    }
                }
                int pmagCount = 0;
                for (int i = 0; i < _portals.Count; i++)
                {
                    Portal portal = _portals[i];
                    if (!portal.Name.StartsWith("pmag"))
                    {
                        continue;
                    }
                    pmagCount++;
                    for (int j = 0; j < model.Nodes.Count; j++)
                    {
                        if (model.Nodes[j].Name == $"geo{portal.Name[1..]}")
                        {
                            _forceFields.Add(new PortalNodeRef(portal, j));
                            break;
                        }
                    }
                }
                // biodefense chamber 04 and 07 don't have the red portal geometry nodes
                Debug.Assert(_forceFields.Count == pmagCount
                    || model.Name == "biodefense chamber 04" || model.Name == "biodefense chamber 07");
            }
            else if (meta.RoomNodeName != null
                && model.Nodes.TryFind(n => n.Name == meta.RoomNodeName && n.ChildIndex != -1, out Node? roomNode))
            {
                roomNode.RoomPartId = _nextRoomPartId;
                _nextRoomPartId++;
            }
            else
            {
                foreach (Node node in model.Nodes)
                {
                    if (node.Name.StartsWith("rm"))
                    {
                        node.RoomPartId = _nextRoomPartId;
                        _nextRoomPartId++;
                        break;
                    }
                }
            }
            Debug.Assert(model.Nodes.Any(n => n.RoomPartId >= 0));
            collision.Translation = Vector3.Zero;
            if (_roomCollision.Count == 0)
            {
                _roomCollision.Add(collision);
            }
            else
            {
                _roomCollision[0] = collision;
            }
            RoomId = roomId;
            _scene.RoomId = roomId;
        }

        public void SetNodeData(NodeData? nodeData)
        {
            _nodeData = nodeData;
            if (nodeData != null && _models.Count < 2)
            {
                // using cached instance messes with placeholders since the room entity doesn't update its instances normally
                _models.Add(Read.GetModelInstance("pick_wpn_missile", noCache: true));
            }
        }

        public Portal? GetPortalByName(string name)
        {
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (portal.Name == name)
                {
                    return portal;
                }
            }
            return null;
        }

        private NodeRef AddDoorPortal(DoorEntity door)
        {
            // workaround for unintended modes
            if (!GameState.SinglePlayer)
            {
                return NodeRef.None;
            }
            _doorPortalCount++;
            string roomNodeName = "";
            int roomPartId = -1;
            int roomNodeIndex = -1;
            IReadOnlyList<Node> roomNodes = _models[0].Model.Nodes;
            for (int i = 0; i < roomNodes.Count; i++)
            {
                Node node = roomNodes[i];
                if (node.ChildIndex == door.NodeRef.NodeIndex && node.RoomPartId == door.NodeRef.PartIndex)
                {
                    roomNodeName = node.Name;
                    roomPartId = node.RoomPartId;
                    roomNodeIndex = node.ChildIndex;
                }
            }
            if (roomPartId == -1)
            {
                throw new ProgramException("Connector did not match room part node.");
            }
            RoomMetadata? meta = Metadata.GetRoomById((int)door.Data.ConnectorId);
            Debug.Assert(meta != null);
            ModelInstance conInst = Read.GetRoomModelInstance(meta.Name); // cached
            IReadOnlyList<Node> conNodes = conInst.Model.Nodes;
            string connectorName = door.Data.RoomName.MarshalString();
            for (int i = 0; i < conNodes.Count; i++)
            {
                Node node = conNodes[i];
                if (node.Name.StartsWith("rm"))
                {
                    node.RoomPartId = _nextRoomPartId++;
                    var sides = new List<(Portal, bool)>();
                    Portal portal = door.SetUpPort(roomNodeName, node.Name);
                    portal.NodeRef1 = new NodeRef(Meta.Name, roomPartId, roomNodeIndex, modelIndex: 0);
                    portal.NodeRef2 = new NodeRef(connectorName, node.RoomPartId, node.ChildIndex, modelIndex: _doorPortalCount);
                    if (_portalSides.Count == 0)
                    {
                        Debug.Assert(roomPartId == 0);
                        _portalSides.Add(new List<(Portal, bool)>());
                    }
                    _portalSides[roomPartId].Add((portal, false));
                    sides.Add((portal, true));
                    _portalSides.Add(sides);
                    _portals.Add(portal);
                    return portal.NodeRef2;
                }
            }
            return NodeRef.None;
        }

        private static readonly ImmutableArray<Vector3> _connectorSizes =
        [
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 10),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 10),
            new Vector3(Fixed.ToFloat(0xA60F), 0, 0),
            new Vector3(Fixed.ToFloat(0xA60F), 0, 0),
            new Vector3(0, 0, Fixed.ToFloat(0xA60F)),
            new Vector3(0, 0, Fixed.ToFloat(0xA60F)),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 10),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 10),
            new Vector3(0, Fixed.ToFloat(0x24B9), Fixed.ToFloat(0x16A77)),
            new Vector3(0, Fixed.ToFloat(-7659), Fixed.ToFloat(0x16A76)),
            new Vector3(10, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 20),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, 10)
        ];

        public void AddConnector(DoorEntity door)
        {
            int connectorId = (int)door.Data.ConnectorId;
            Debug.Assert(connectorId >= 0 && connectorId < _connectorSizes.Length);
            Vector3 size = _connectorSizes[connectorId];
            Vector3 doorFacing = door.FacingVector;
            if (doorFacing.X > Fixed.ToFloat(2896) || doorFacing.Z > Fixed.ToFloat(2896))
            {
                size *= -1;
            }
            RoomMetadata? meta = Metadata.GetRoomById(connectorId);
            Debug.Assert(meta != null);
            ModelInstance conInst = Read.GetRoomModelInstance(meta.Name);
            _scene.LoadModel(conInst.Model);
            _connectorModels.Add(conInst);
            CollisionInstance collision = Collision.GetCollision(meta, roomLayerMask: -1);
            collision.ConnectorName = door.Data.RoomName.MarshalString();
            collision.Translation = door.Position + size / 2;
            _roomCollision.Add(collision);
            conInst.Active = false;
            collision.Active = false;
            if (!GameState.InRoomTransition)
            {
                // hack -- keep track of which connectors belong to the current room
                conInst.NodeAnimIgnoreRoot = true;
            }
            door.ConnectorModel = conInst;
            door.ConnectorCollision = collision;
            // todo?: update visited connectors
            var header = new EntityDataHeader((ushort)EntityType.Door, entityId: -1,
                door.Position + size, door.UpVector, -doorFacing);
            var data = new DoorEntityData(header, nodeName: null, door.Data.PaletteId, door.Data.DoorType,
                connectorId: 255, targetLayerId: 0, locked: 0, outConnectorId: 255,
                outLoaderId: door.Data.OutLoaderId, entityFilename: null, roomName: null);
            IReadOnlyList<Node> nodes = conInst.Model.Nodes;
            string nodeName = "rmMain";
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (node.Name.StartsWith("rm"))
                {
                    nodeName = node.Name;
                    break;
                }
            }
            int layerId = door.Data.TargetLayerId == 255 ? -1 : door.Data.TargetLayerId;
            var newDoor = new DoorEntity(data, nodeName, _scene, door.TargetRoomId, layerId);
            _scene.AddEntity(newDoor);
            newDoor.ConnectorInactive = true;
            door.LoaderDoor = newDoor;
            newDoor.ConnectorDoor = door;
            if (!GameState.InRoomTransition)
            {
                newDoor.NodeRef = AddDoorPortal(door);
            }
        }

        public void ActivateConnector(DoorEntity door)
        {
            Debug.Assert(door.ConnectorModel != null);
            Debug.Assert(door.ConnectorCollision != null);
            for (int i = 0; i < _connectorModels.Count; i++)
            {
                ModelInstance conInst = _connectorModels[i];
                CollisionInstance conCol = _roomCollision[i + 1];
                conInst.Active = false;
                conCol.Active = false;
            }
            door.ConnectorModel.Active = true;
            door.ConnectorCollision.Active = true;
            Debug.Assert(door.LoaderDoor != null);
            foreach (DoorEntity other in _scene.GetDoorEntities())
            {
                if (other.LoaderDoor != null)
                {
                    other.LoaderDoor.ConnectorInactive = true;
                }
            }
            door.LoaderDoor.ConnectorInactive = false;
        }

        public void UpdateTransition()
        {
            if (GameState.TransitionState == TransitionState.Start)
            {
                StartTransition(fromDoor: true);
            }
            else if (GameState.TransitionState == TransitionState.Process)
            {
                _scene.InitLoadedEntity(count: 1); // todo: revisit this count?
            }
            else if (GameState.TransitionState == TransitionState.End)
            {
                EndTransition();
            }
        }

        private static readonly ImmutableArray<bool> _keepEntities =
        [
            false, false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, true, true, false, true, true, true
        ];

        public DoorEntity? LoaderDoor { get; set; }
        public int LoadEntityId { get; set; } = -1;

        public void LoadRoom(bool resume)
        {
            PlayerEntity? player = PlayerEntity.Main;
            player.StopAllSfx();
            Hunter hunter = player.Hunter;
            int recolor = player.Recolor;
            if (GameState.TransitionRoomId == -1)
            {
                GameState.TransitionRoomId = _scene.RoomId;
            }
            _scene.ResetFrameCount();
            Rng.SetRng2(0);
            StartTransition(fromDoor: false, resume);
            _scene.ClearEffects();
            if (!resume)
            {
                PlayerEntity.Reset();
                PlayerEntity.Construct(_scene);
                if (Mods.Network.NetRoomChange.Rebuilding)
                {
                    // A networked match needs every slot rebuilt, not just
                    // this machine's: Scene.AddPlayer is inert once a room has
                    // loaded, so a slot missing here could never be filled for
                    // the rest of the map.
                    player = Mods.Network.NetRoomChange.RebuildPlayers(_scene, hunter, recolor);
                }
                else
                {
                    player = PlayerEntity.Create(hunter, recolor);
                    Debug.Assert(player != null);
                    // todo: revisit flags
                    player.LoadFlags |= LoadFlags.SlotActive;
                    player.LoadFlags |= LoadFlags.Active;
                    player.LoadFlags |= LoadFlags.Initial;
                    PlayerEntity.PlayerCount++;
                }
            }
            ProcessTransition(CancellationToken.None);
            EndTransition();
            GameState.PausePrevented = false;
            Music.TryPlayRoomMusic(_scene.RoomId, GameState.SinglePlayer && (((int)GameState.StorySave.BossFlags >> (2 * _scene.AreaId)) & 3) != 0 ? 1 : 0);
            if (!resume)
            {
                _scene.InsertEntity(player);
            }
            player.ReloadInit = resume;
            player.Initialize();
            if (!resume)
            {
                _scene.InitEntity(player);
                _scene.InitEntity(player.Halfturret);
                if (Mods.Network.NetRoomChange.Rebuilding)
                {
                    Mods.Network.NetRoomChange.AfterRebuild(_scene);
                }
            }
        }

        private void StartTransition(bool fromDoor, bool resume = false)
        {
            Debug.Assert(GameState.TransitionRoomId != -1);
            GameState.TransitionState = TransitionState.Process;
            Music.UpdateEncounterMusic(-1);
            foreach (EntityBase entity in _scene.Entities)
            {
                if (entity.Type == EntityType.Room || entity.Type == EntityType.Model
                    || entity.Type == EntityType.Player && resume)
                {
                    continue;
                }
                if (LoaderDoor != null && entity.Type == EntityType.Door)
                {
                    var door = (DoorEntity)entity;
                    if (door == LoaderDoor || door.LoaderDoor == LoaderDoor)
                    {
                        _scene.RemoveEntityFromMap(door);
                    }
                    else
                    {
                        _scene.RemoveEntity(door);
                        door.Destroy();
                    }
                }
                else if (LoaderDoor != null && _keepEntities[(int)entity.Type])
                {
                    // todo: MP1P
                    if (entity.Type == EntityType.Player && entity != PlayerEntity.Main
                        || entity.Type == EntityType.Halfturret && entity != PlayerEntity.Main.Halfturret)
                    {
                        _scene.RemoveEntity(entity);
                        entity.Destroy();
                    }
                    else if (entity.Type == EntityType.BeamProjectile)
                    {
                        var beam = (BeamProjectileEntity)entity;
                        if (beam.Owner != PlayerEntity.Main)
                        {
                            _scene.RemoveEntity(beam);
                            beam.Destroy();
                        }
                    }
                }
                else
                {
                    _scene.RemoveEntity(entity);
                    entity.Destroy();
                }
            }
            _scene.ClearNonPersistentEffects();
            // A cam sequence that was still running gets cut off here without
            // reaching the CanEnd branch that would have paired off whatever
            // mute counter it bumped -- leaving sound classes silenced for the
            // rest of the match. Sfx.Load resets these once at initial connect;
            // a mid-session room transition needs the same reset.
            Sfx.SfxMute = false;
            Sfx.ForceFieldSfxMute = 0;
            Sfx.TimedSfxMute = 0;
            Sfx.LongSfxMute = 0;
            CamSeqEntity.Current = null;
            CameraSequence.Current = null;
            _scene.ClearMessageQueue();
            // todo?: unload more stuff
            if (GameState.EscapeTimer != -1 && GameState.EscapeState != EscapeState.Escape)
            {
                GameState.ResetEscapeState(updateSounds: false);
            }
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.ResetReferences();
            }
            _scene.AreaId = Metadata.GetAreaInfo(GameState.TransitionRoomId);
            if (fromDoor)
            {
                Task.Run(() => ProcessTransition(_cts.Token), _cts.Token);
            }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void CancelTransition()
        {
            _cts.Cancel();
        }

        private void ProcessTransition(CancellationToken token)
        {
            Debug.Assert(GameState.TransitionRoomId != -1);
            RoomMetadata? roomMeta = Metadata.GetRoomById(GameState.TransitionRoomId);
            Debug.Assert(roomMeta != null);
            int entityLayer = -1;
            if (LoaderDoor != null)
            {
                // after the game creates a connector door, it overwrites the pointer to its init data struct
                // so that it points to the original door's. when door.data.targetLayerId is accessed here, it points
                // to the original door's value. we use a separate field instead, and so do not access the struct.
                entityLayer = LoaderDoor.TargetLayerId;
            }
            else
            {
                Rng.SetRng2(Rng.Rng2StartValue);
            }
            (_, IReadOnlyList<EntityBase> entities) = SceneSetup.SetUpRoom(GameState.Mode,
                Mods.Network.NetRoomChange.RoomPlayerCount,
                BossFlags.Unspecified, nodeLayerMask: 0, entityLayer, roomMeta, room: this, _scene, isRoomTransition: true);
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (GameState.SinglePlayer)
            {
                // Guarded the way the same call is at first load (SceneSetup.LoadGame).
                // It is adventure mode's hunter-encounter setup: it clears Active
                // and SlotActive on slots 1-3 and resets PlayersCreated to 1, which
                // in a multiplayer room transition silently emptied every other
                // player out of the scene for the rest of the map.
                SceneSetup.InitHunterSpawns(_scene, entities, initialize: true); // see: "probably revisit this"
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
            AiPersonality.LoadAll(GameState.Mode);
            if (token.IsCancellationRequested)
            {
                return;
            }
            SetNodeData(SceneSetup.LoadNodeData(roomMeta.NodePath, roomMeta.Id, GameState.Mode, entities, roomMeta.FirstHunt));
            PlayerEntity.PlayerAiData.InitializeGlobals();
            if (token.IsCancellationRequested)
            {
                return;
            }
            for (int i = 0; i < entities.Count; i++)
            {
                EntityBase entity = entities[i];
                entity.Initialized = false;
                _scene.InsertEntity(entity);
                _scene.LoadedEntities.Enqueue(entity);
                if (token.IsCancellationRequested)
                {
                    return;
                }
            }
            if (LoaderDoor == null)
            {
                _scene.InitLoadedEntity(count: -1);
            }
            else
            {
                while (!_scene.LoadedEntities.IsEmpty)
                {
                    Thread.Sleep(10);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
            foreach (DoorEntity door in _scene.GetDoorEntities())
            {
                if (door.Data.ConnectorId == 255 || door.Portal != null)
                {
                    continue;
                }
                Debug.Assert(door.LoaderDoor != null);
                door.LoaderDoor.NodeRef = AddDoorPortal(door);
                if (token.IsCancellationRequested)
                {
                    return;
                }
            }
            GameState.TransitionState = TransitionState.End;
        }

        private Model? _unloadModel = null;

        private void EndTransition()
        {
            RoomMetadata? roomMeta = Metadata.GetRoomById(GameState.TransitionRoomId);
            Debug.Assert(roomMeta != null);
            ModelInstance inst = _models[0];
            _scene.LoadModel(inst.Model, isRoom: true);
            inst.SetAnimation(0);
            _scene.SetRoomValues(roomMeta);
            for (int i = 0; i < _connectorModels.Count; i++)
            {
                ModelInstance conInst = _connectorModels[i];
                CollisionInstance conCol = _roomCollision[i + 1];
                if (conInst.NodeAnimIgnoreRoot)
                {
                    _connectorModels.RemoveAt(i);
                    _roomCollision.RemoveAt(i + 1);
                    i--;
                }
                else
                {
                    conInst.NodeAnimIgnoreRoot = true;
                }
            }
            if (_roomCollision.Count > 0)
            {
                _roomCollision[0].Active = true;
            }
            Vector3 offset = Vector3.Zero;
            DoorEntity? prevConnector = null;
            DoorEntity? newLoader = null;
            NodeRef nodeRef = NodeRef.None;
            foreach (EntityBase entity in _scene.Entities)
            {
                entity.Initialized = true;
                if (LoaderDoor != null && entity.Type == EntityType.Door)
                {
                    var door = (DoorEntity)entity;
                    if (door == LoaderDoor || door.LoaderDoor == LoaderDoor)
                    {
                        if (door.LoaderDoor == LoaderDoor)
                        {
                            prevConnector = door;
                        }
                        _scene.RemoveEntity(door);
                        door.Destroy();
                    }
                    else if (door.Data.OutConnectorId == LoaderDoor.Data.OutLoaderId)
                    {
                        // new connector replacing the loader
                        Debug.Assert(door.Portal != null);
                        door.Flags |= DoorFlags.ShotOpen;
                        door.Flags &= ~DoorFlags.Locked;
                        door.SetAnimationFrame(LoaderDoor.GetAnimationFrame());
                        ActivateConnector(door);
                        offset = door.Position - LoaderDoor.Position;
                        nodeRef = door.Portal.NodeRef2;
                        Debug.Assert(door.LoaderDoor != null);
                        newLoader = door.LoaderDoor;
                        prevConnector ??= LoaderDoor.ConnectorDoor;
                        Debug.Assert(prevConnector != null);
                        // new loader replacing the connector
                        newLoader.SetAnimationFrame(prevConnector.GetAnimationFrame());
                        if (prevConnector.Flags.TestFlag(DoorFlags.ShotOpen) &&
                            !newLoader.Flags.TestFlag(DoorFlags.Locked))
                        {
                            newLoader.Flags |= DoorFlags.ShotOpen;
                        }
                    }
                }
            }
            if (LoaderDoor != null)
            {
                Debug.Assert(nodeRef != NodeRef.None);
                foreach (EntityBase entity in _scene.Entities)
                {
                    if (entity.Type == EntityType.Player)
                    {
                        var player = (PlayerEntity)entity;
                        player.Reposition(offset, nodeRef);
                    }
                    else if (entity.Type == EntityType.Bomb)
                    {
                        var bomb = (BombEntity)entity;
                        bomb.Reposition(offset);
                    }
                    else if (entity.Type == EntityType.BeamEffect)
                    {
                        var beamEffect = (BeamEffectEntity)entity;
                        beamEffect.Reposition(offset);
                    }
                }
            }
            for (int i = 0; i < PlayerEntity.Players.Count; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.IsBot)
                {
                    player.AiData.InitializeAtLoad();
                }
            }
            if (newLoader?.ConnectorDoor != null)
            {
                DoorEntity targetDoor = newLoader.ConnectorDoor;
                foreach (EnemySpawnEntity spawner in _scene.GetEnemySpawnEntities())
                {
                    if (spawner.Data.EnemyType != EnemyType.Cretaphid && spawner.Data.EnemyType != EnemyType.Slench)
                    {
                        continue;
                    }
                    if (GameState.StorySave.GetRoomState(_scene.RoomId, spawner.Id) != 0)
                    {
                        Movie movieId;
                        if (spawner.Data.EnemyType == EnemyType.Cretaphid)
                        {
                            movieId = spawner.Data.Fields.S05.EnemySubtype switch
                            {
                                3 => Movie.CretaphidArcterra2Intro,
                                2 => Movie.CretaphidAlinso2Intro,
                                1 => Movie.CretaphidVDO1Intro,
                                _ => Movie.CretaphidCA1Intro // 0
                            };
                        }
                        else // if (spawner.Data.EnemyType == EnemyType.Slench)
                        {
                            movieId = _scene.RoomId switch
                            {
                                76 => Movie.SlenchVDO2Intro,
                                64 => Movie.SlenchCA2Intro,
                                82 => Movie.SlenchArcterra1Intro,
                                _ => Movie.SlenchAlinos1Intro // 35
                            };
                        }
                        Vector3 newPosition = (targetDoor.Position + targetDoor.FacingVector * 0.75f)
                            .AddY(Fixed.ToFloat(-PlayerEntity.Main.Values.MinPickupHeight));
                        // todo?: faster loading makes this transition kind of abrupt
                        GameState.PausePrevented = true;
                        _scene.StartMovie(movieId, FadeType.FadeOutInBlack, 0, FadeType.FadeOutInBlack, 5 / 30f, newPosition, targetDoor.FacingVector);
                    }
                    break;
                }
            }
            if (GameState.GetAreaState(_scene.AreaId) == AreaState.Clear && PlayerEntity.PlayerCount > 1)
            {
                foreach (DoorEntity entity in _scene.GetDoorEntities())
                {
                    if (entity.Type != EntityType.Door)
                    {
                        continue;
                    }
                    DoorEntity door = entity;
                    if (door.Id != -1 && door.Data.ConnectorId != 255)
                    {
                        if (door.LoaderDoor != null && door.LoaderDoor == newLoader)
                        {
                            door = door.LoaderDoor;
                        }
                        door.Lock(updateState: false);
                        door.Flags |= DoorFlags.ShowLock;
                    }
                }
            }
            GameState.StorySave.SetVisitedRoom(RoomId);
            if (_unloadModel != null)
            {
                _scene.UnloadModel(_unloadModel);
            }
            _unloadModel = null;
            LoaderDoor = null;
            GC.Collect(generation: 2, GCCollectionMode.Forced, blocking: false, compacting: true);
            GameState.TransitionState = TransitionState.None;
            GameState.TransitionRoomId = -1;
        }

        protected override void GetCollisionDrawInfo()
        {
            for (int i = 0; i < _roomCollision.Count; i++)
            {
                CollisionInstance inst = _roomCollision[i];
                CollisionInfo info = inst.Info;
                info.GetDrawInfo(info.Points, inst.Translation, Type, _scene);
            }
        }

        private const int _roomPartMax = 32;

        private readonly bool[] _activeRoomParts = new bool[_roomPartMax];
        private readonly bool[] _audibleRoomParts = new bool[_roomPartMax];

        private int _visNodeRefRecursionDepth = 0;
        private int _audNodeRefRecursionDepth = 0;

        private RoomPartVisInfo? _partVisInfoHead = null;
        private readonly RoomPartVisInfo[] _partVisInfo = new RoomPartVisInfo[_roomPartMax];

        private RoomPartVisInfo GetPartVisInfo(NodeRef nodeRef)
        {
            RoomPartVisInfo visInfo = _partVisInfo[nodeRef.PartIndex];
            if (!_activeRoomParts[nodeRef.PartIndex])
            {
                visInfo.NodeRef = nodeRef;
                visInfo.ViewMinX = 1;
                visInfo.ViewMaxX = 0;
                visInfo.ViewMinY = 1;
                visInfo.ViewMaxY = 0;
                visInfo.Next = _partVisInfoHead;
                _partVisInfoHead = visInfo;
            }
            return visInfo;
        }

        private int _roomFrustumIndex = 0;
        private readonly RoomFrustumItem[] _roomFrustumItems = new RoomFrustumItem[_roomPartMax];
        private readonly RoomFrustumItem?[] _roomFrustumLinks = new RoomFrustumItem[_roomPartMax];

        private RoomFrustumItem GetRoomFrustumItem()
        {
            Debug.Assert(_roomFrustumIndex != _roomPartMax);
            return _roomFrustumItems[_roomFrustumIndex];
        }

        private void ClearRoomPartState()
        {
            for (int i = 0; i < _roomPartMax; i++)
            {
                _activeRoomParts[i] = false;
                _roomFrustumLinks[i] = null;
                _audibleRoomParts[i] = false;
            }
            _visNodeRefRecursionDepth = 0;
            _audNodeRefRecursionDepth = 0;
            _roomFrustumIndex = 0;
            _partVisInfoHead = null;
        }

        private void UpdateRoomParts()
        {
            NodeRef curNodeRef = PlayerEntity.Main.CameraInfo.NodeRef;
            if (_scene.CameraMode != CameraMode.Player || curNodeRef.PartIndex == -1)
            {
                return;
            }
            // The camera has to actually be in the part it says it is in.
            //
            // Everything below walks the portal graph outwards from that part
            // and draws what it reaches, so a camera holding somebody else's
            // part describes a view from somewhere else in the map and the
            // room comes out black with the gun and a few pickups floating in
            // it. That is not hypothetical: a spectated player and every
            // player in a demo replay is a puppet whose node ref was looked
            // up from its position rather than walked, and a demo of a match
            // on MP1 SANCTORUS played back as an unlit void.
            //
            // Returning here leaves _partVisInfoHead null, which GetDrawInfo
            // already reads as "draw every part". The test is one-sided --
            // outside the part's own bounding box is outside the part -- so a
            // correct node ref never reaches it and the culling upstream does
            // is untouched.
            if (!PartCouldContain(curNodeRef.PartIndex, _scene.CameraPosition, _partBoundsMargin))
            {
                return;
            }
            Debug.Assert(curNodeRef.NodeIndex != -1);
            RoomPartVisInfo curVisInfo = GetPartVisInfo(curNodeRef);
            curVisInfo.ViewMinX = 0;
            curVisInfo.ViewMaxX = 1;
            curVisInfo.ViewMinY = 0;
            curVisInfo.ViewMaxY = 1;
            _activeRoomParts[curNodeRef.PartIndex] = true;
            RoomFrustumItem curRoomFrustum = GetRoomFrustumItem();
            _roomFrustumIndex++;
            curRoomFrustum.Info.Count = _scene.FrustumInfo.Count; // always 5
            curRoomFrustum.Info.Index = _scene.FrustumInfo.Index; // always 1
            for (int i = 0; i < curRoomFrustum.Info.Planes.Length; i++)
            {
                curRoomFrustum.Info.Planes[i] = _scene.FrustumInfo.Planes[i];
            }
            curRoomFrustum.NodeRef = curNodeRef;
            RoomFrustumItem? link = _roomFrustumLinks[curNodeRef.PartIndex];
            curRoomFrustum.Next = link;
            _roomFrustumLinks[curNodeRef.PartIndex] = curRoomFrustum;
            FindVisibleRoomParts(curRoomFrustum, curNodeRef);
            FindAudibleRoomParts(curNodeRef, curNodeRef);
        }

        private static readonly Vector3[] _startPointList = new Vector3[14];
        private static readonly Vector3[] _destPointList = new Vector3[14];

        private void FindVisibleRoomParts(RoomFrustumItem frustumItem, NodeRef mainNodeRef)
        {
            bool otherSide = false;
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (!portal.Active)
                {
                    continue;
                }
                if (portal.NodeRef1 == frustumItem.NodeRef)
                {
                    otherSide = false;
                }
                else if (portal.NodeRef2 == frustumItem.NodeRef)
                {
                    otherSide = true;
                }
                else
                {
                    continue;
                }
                Debug.Assert(portal.NodeRef1 != NodeRef.None);
                Debug.Assert(portal.NodeRef2 != NodeRef.None);
                Debug.Assert(portal.NodeRef1 != portal.NodeRef2);
                float minX = 1;
                float maxX = 0;
                float minY = 1;
                float maxY = 0;
                float dist = GetDistanceToPortal(_scene.CameraPosition, portal.Plane, otherSide);
                if (dist < 0)
                {
                    continue;
                }
                // todo?: link this portal into the draw list
                if (portal.IsForceField && GetPortalAlpha(portal.Position, _scene.CameraPosition) == 1)
                {
                    continue;
                }
                bool adjacent = false;
                if (dist < 0.5f)
                {
                    adjacent = true;
                    for (int j = 0; j < portal.Planes.Count; j++)
                    {
                        Vector4 plane = portal.Planes[j];
                        if (Vector3.Dot(_scene.CameraPosition, plane.Xyz) - plane.W < Fixed.ToFloat(-4224))
                        {
                            adjacent = false;
                            break;
                        }
                    }
                }
                int v28;
                RoomFrustumItem nextFrustumItem = GetRoomFrustumItem();
                if (adjacent)
                {
                    // even if facing away, we're close enough to the portal that should consider its part visible
                    minX = 0;
                    maxX = 1;
                    minY = 0;
                    maxY = 1;
                    v28 = 4;
                    nextFrustumItem.Info.Index = frustumItem.Info.Index;
                    nextFrustumItem.Info.Count = frustumItem.Info.Count;
                    for (int j = 0; j < frustumItem.Info.Count; j++)
                    {
                        nextFrustumItem.Info.Planes[j] = frustumItem.Info.Planes[j];
                    }
                }
                else
                {
                    for (int j = 0; j < portal.Points.Count; j++)
                    {
                        _startPointList[j] = portal.Points[j];
                    }
                    v28 = Func21180A8(frustumItem.Info, _startPointList, portal.Points.Count, _destPointList);
                    if (v28 >= 3)
                    {
                        Debug.Assert(frustumItem.Info.Index + v28 <= 10);
                        // not entirely sure what it means for the index to be 0 or not
                        // --> basically we've got an "current count" (index) and a "new count" after adding v28?
                        // --> so not really the same usage as with the main frustum?
                        int index = frustumItem.Info.Index;
                        nextFrustumItem.Info.Index = index;
                        for (int j = 0; j < frustumItem.Info.Index; j++)
                        {
                            nextFrustumItem.Info.Planes[j] = frustumItem.Info.Planes[j];
                        }
                        // todo: names/purposes
                        nextFrustumItem.Info.Count = index;
                        for (int j = 0; j < v28; j++)
                        {
                            Vector3 point1 = _destPointList[j];
                            Vector3 point2 = _destPointList[j == v28 - 1 ? 0 : j + 1];
                            if (MathF.Abs(point1.X - point2.X) >= 1 / 4096f || MathF.Abs(point1.Y - point2.Y) >= 1 / 4096f
                                || MathF.Abs(point1.Z - point2.Z) >= 1 / 4096f)
                            {
                                Vector3 normal;
                                Vector3 vec1 = point1 - _scene.CameraPosition;
                                Vector3 vec2 = point2 - _scene.CameraPosition;
                                if (otherSide)
                                {
                                    normal = Vector3.Cross(vec1, vec2).Normalized();
                                }
                                else
                                {
                                    normal = Vector3.Cross(vec2, vec1).Normalized();
                                }
                                var plane = new Vector4(normal, Vector3.Dot(normal, _scene.CameraPosition));
                                nextFrustumItem.Info.Planes[index + j] = Scene.SetBoundsIndices(plane);
                                Vector3 destPoint = _startPointList[j];
                                if (Func2117F84(point1, ref destPoint) >= 0)
                                {
                                    minX = MathF.Min(minX, destPoint.X);
                                    maxX = MathF.Max(maxX, destPoint.X);
                                    minY = MathF.Min(minY, destPoint.Y);
                                    maxY = MathF.Max(maxY, destPoint.Y);
                                }
                                nextFrustumItem.Info.Count++;
                            }
                        }
                    }
                }
                if (v28 >= 3)
                {
                    minX = MathF.Max(minX, 0);
                    maxX = MathF.Min(maxX, 1);
                    minY = MathF.Max(minY, 0);
                    maxY = MathF.Min(maxY, 1);
                    // todo?: base these values on the aspect ratio or something?
                    if (minX < maxX - 1 / 800f && minY < maxY - 1 / 600f)
                    {
                        // todo?: link this portal into the draw list
                        NodeRef nextNodeRef = otherSide ? portal.NodeRef1 : portal.NodeRef2;
                        // todo: couldn't we bail a lot earlier for either condition?
                        if (nextNodeRef.PartIndex == mainNodeRef.PartIndex || _visNodeRefRecursionDepth < 6)
                        {
                            _visNodeRefRecursionDepth++;
                            RoomPartVisInfo nextVisInfo = GetPartVisInfo(nextNodeRef);
                            nextVisInfo.ViewMinX = MathF.Min(nextVisInfo.ViewMinX, minX);
                            nextVisInfo.ViewMaxX = MathF.Max(nextVisInfo.ViewMaxX, maxX);
                            nextVisInfo.ViewMinY = MathF.Min(nextVisInfo.ViewMinY, minY);
                            nextVisInfo.ViewMaxY = MathF.Max(nextVisInfo.ViewMaxY, maxY);
                            _activeRoomParts[nextNodeRef.PartIndex] = true;
                            _roomFrustumIndex++;
                            nextFrustumItem.NodeRef = nextNodeRef;
                            RoomFrustumItem? link = _roomFrustumLinks[nextNodeRef.PartIndex];
                            nextFrustumItem.Next = link;
                            _roomFrustumLinks[nextNodeRef.PartIndex] = nextFrustumItem;
                            FindVisibleRoomParts(nextFrustumItem, mainNodeRef);
                            _visNodeRefRecursionDepth--;
                        }
                    }
                }
            }
        }

        // todo: name/purpose
        private float Func2117F84(Vector3 point, ref Vector3 dest)
        {
            Matrix4 matrix = _scene.ViewMatrix * _scene.PerspectiveMatrix; // todo: no need to do this multiple times
            float v4 = point.X * matrix.Row0.W + point.Y * matrix.Row1.W + point.Z * matrix.Row2.W + matrix.Row3.W;
            if (v4 <= 0)
            {
                return v4;
            }
            dest = Matrix.Vec3MultMtx4(point, matrix);
            dest.X = (dest.X * 400 / v4 + 400) / 800;
            dest.Y = (dest.Y * 300 / v4 + 300) / 600;
            dest.Z /= v4;
            return 1 / v4;
        }

        // todo: name/purpose
        private int Func21180A8(FrustumInfo frustumInfo, Vector3[] pointList, int pointCount, Vector3[] destList)
        {
            Debug.Assert(frustumInfo.Count > 0);
            var temp1 = new Vector3[14];
            var temp2 = new Vector3[14];
            for (int i = 0; i < frustumInfo.Count; i++)
            {
                // first iteration: pointList is the list of points from the portal
                // next iterations: pointList is the list we built in temp1/temp2 in the last iteration
                int newPointCount = 0;
                Vector3[] newList;
                if (i == frustumInfo.Count - 1)
                {
                    newList = destList;
                }
                else
                {
                    newList = i % 2 == 0 ? temp1 : temp2;
                }
                Vector4 plane = frustumInfo.Planes[i].Plane;
                float dist1 = Vector3.Dot(pointList[0], plane.Xyz) - plane.W;
                bool v5 = dist1 >= 0;
                Debug.Assert(pointCount > 0);
                for (int j = 0; j < pointCount; j++)
                {
                    // each iteration, dist1 is the distance from point1 to the frustum plane,
                    // and dist2 is the distance from point2 to the portal, as we test successive edges
                    Vector3 point1 = pointList[j];
                    Vector3 point2 = pointList[j == pointCount - 1 ? 0 : j + 1];
                    if (v5)
                    {
                        newList[newPointCount++] = point1;
                    }
                    float dist2 = Vector3.Dot(point2, plane.Xyz) - plane.W;
                    bool v6 = dist2 >= 0;
                    if (v5 != v6)
                    {
                        float div = -dist1 / (dist2 - dist1);
                        newList[newPointCount++] = point1 + (point2 - point1) * div;
                    }
                    dist1 = dist2;
                    v5 = v6;
                }
                if (newPointCount == 0)
                {
                    return 0;
                }
                pointList = newList;
                pointCount = newPointCount;
            }
            return pointCount;
        }

        private float GetDistanceToPortal(Vector3 pos, Vector4 plane, bool otherSide)
        {
            float dist = Vector3.Dot(pos, plane.Xyz) - plane.W;
            if (otherSide)
            {
                dist *= -1;
            }
            return dist;
        }

        private void FindAudibleRoomParts(NodeRef nodeRef, NodeRef mainNodeRef)
        {
            _audibleRoomParts[nodeRef.PartIndex] = true;
            bool otherSide = false;
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (!portal.Active)
                {
                    continue;
                }
                if (portal.NodeRef1 == nodeRef)
                {
                    otherSide = false;
                }
                else if (portal.NodeRef2 == nodeRef)
                {
                    otherSide = true;
                }
                else
                {
                    continue;
                }
                if (portal.IsForceField && GetPortalAlpha(portal.Position, _scene.CameraPosition) == 1)
                {
                    continue;
                }
                Debug.Assert(portal.NodeRef1 != NodeRef.None);
                Debug.Assert(portal.NodeRef2 != NodeRef.None);
                Debug.Assert(portal.NodeRef1 != portal.NodeRef2);
                float dist = GetDistanceToPortal(_scene.CameraPosition, portal.Plane, otherSide);
                if (dist > Fixed.ToFloat(100000) || dist < Fixed.ToFloat(-100000))
                {
                    continue;
                }
                NodeRef nextNodeRef = otherSide ? portal.NodeRef1 : portal.NodeRef2;
                if (nextNodeRef.PartIndex == mainNodeRef.PartIndex || _audNodeRefRecursionDepth < 2)
                {
                    _audNodeRefRecursionDepth++;
                    FindAudibleRoomParts(nextNodeRef, mainNodeRef);
                    _audNodeRefRecursionDepth--;
                }
            }
        }

        public NodeRef GetNodeRefByName(string nodeName)
        {
            Model model = _models[0].Model;
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                if (node.Name == nodeName)
                {
                    Debug.Assert(node.RoomPartId >= 0);
                    Debug.Assert(node.ChildIndex != -1);
                    // for the purposes this node ref is returned for, we can assume it belongs to the actual room and not a connector
                    return new NodeRef(Meta.Name, node.RoomPartId, node.ChildIndex, modelIndex: 0);
                }
            }
            return NodeRef.None;
        }

        private void EnsurePartBounds()
        {
            if (_partBoundsBuiltFor == _nextRoomPartId)
            {
                return;
            }
            _partBoundsBuiltFor = _nextRoomPartId;
            _partBoundsMin.Clear();
            _partBoundsMax.Clear();
            for (int i = 0; i < _nextRoomPartId; i++)
            {
                _partBoundsMin.Add(new Vector3(Single.MaxValue));
                _partBoundsMax.Add(new Vector3(Single.MinValue));
            }
            if (_models.Count > 0)
            {
                AddPartBounds(_models[0], Vector3.Zero);
            }
            for (int i = 0; i < _connectorModels.Count; i++)
            {
                Vector3 offset = i + 1 < _roomCollision.Count
                    ? _roomCollision[i + 1].Translation
                    : Vector3.Zero;
                AddPartBounds(_connectorModels[i], offset);
            }
        }

        private void AddPartBounds(ModelInstance inst, Vector3 offset)
        {
            // The same walk DrawAllNodes does: a part is a parent node, and
            // what it draws is the chain of children hanging off it.
            IReadOnlyList<Node> nodes = inst.Model.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node pnode = nodes[i];
                int part = pnode.RoomPartId;
                if (part < 0 || part >= _partBoundsMin.Count)
                {
                    continue;
                }
                int nodeIndex = pnode.ChildIndex;
                while (nodeIndex != -1)
                {
                    Node node = nodes[nodeIndex];
                    // Nodes with no mesh have no bounds worth the name, and a
                    // degenerate one would stretch the box to the origin.
                    // Nothing is filtered on Enabled: a node switched off by
                    // the layer mask still says where the part is, and the box
                    // is only ever used to rule a part out.
                    if (node.MeshCount > 0)
                    {
                        var min = new Vector3(node.Bounds[0], node.Bounds[1], node.Bounds[2]) + offset;
                        var max = new Vector3(node.Bounds[3], node.Bounds[4], node.Bounds[5]) + offset;
                        _partBoundsMin[part] = Vector3.ComponentMin(_partBoundsMin[part], min);
                        _partBoundsMax[part] = Vector3.ComponentMax(_partBoundsMax[part], max);
                    }
                    nodeIndex = node.NextIndex;
                }
            }
        }

        /// <summary>
        /// How far inside this part's geometry box the position is, in units.
        /// Zero or less means outside, and <see cref="Single.MaxValue"/> means
        /// there is no box to judge by, which is not evidence either way.
        /// </summary>
        private float PartBoundsDepth(int partIndex, Vector3 position)
        {
            EnsurePartBounds();
            if (partIndex < 0 || partIndex >= _partBoundsMin.Count)
            {
                return Single.MaxValue;
            }
            Vector3 min = _partBoundsMin[partIndex];
            Vector3 max = _partBoundsMax[partIndex];
            if (min.X > max.X)
            {
                return Single.MaxValue;
            }
            float depth = MathF.Min(position.X - min.X, max.X - position.X);
            depth = MathF.Min(depth, MathF.Min(position.Y - min.Y, max.Y - position.Y));
            depth = MathF.Min(depth, MathF.Min(position.Z - min.Z, max.Z - position.Z));
            return depth;
        }

        /// <summary>
        /// Whether the part could hold this position at all. One-sided on
        /// purpose: false means "certainly not this part", true means only
        /// that the box does not rule it out.
        /// </summary>
        public bool PartCouldContain(int partIndex, Vector3 position, float margin)
        {
            float depth = PartBoundsDepth(partIndex, position);
            return depth == Single.MaxValue || depth > -margin;
        }

        /// <summary>
        /// Which room part a position is in, for the callers that cannot walk
        /// there -- a remote player whose position arrives over the wire, a
        /// projectile placed where it struck.
        ///
        /// The portal half-spaces are a necessary condition and not a
        /// sufficient one (see <see cref="_partBoundsMin"/>), so a candidate
        /// that passes them must also be somewhere near the part's own
        /// geometry, and of those the part the position sits deepest inside
        /// wins rather than the lowest-numbered one. When nothing passes both,
        /// the answer is none: a caller that knows it has no node ref can draw
        /// the player anyway, while a confidently wrong one culls them out of
        /// the room.
        /// </summary>
        public NodeRef GetNodeRefByPosition(Vector3 position)
        {
            NodeRef best = NodeRef.None;
            float bestDepth = 0;
            for (int i = 0; i < _portalSides.Count; i++)
            {
                NodeRef result = NodeRef.None;
                bool allInside = true;
                List<(Portal Portal, bool OtherSide)> partSides = _portalSides[i];
                for (int j = 0; j < partSides.Count; j++)
                {
                    (Portal portal, bool otherSide) = partSides[j];
                    float dist = Vector3.Dot(position, portal.Plane.Xyz) - portal.Plane.W;
                    if (otherSide)
                    {
                        dist *= -1;
                    }
                    if (dist < 0)
                    {
                        allInside = false;
                        break;
                    }
                    result = otherSide ? portal.NodeRef2 : portal.NodeRef1;
                }
                if (!allInside || result == NodeRef.None)
                {
                    continue;
                }
                float depth = PartBoundsDepth(result.PartIndex, position);
                if (depth == Single.MaxValue)
                {
                    // A part with no geometry to judge by: take it only if
                    // nothing better has been found, which is what this used
                    // to do for every part.
                    if (best == NodeRef.None)
                    {
                        best = result;
                    }
                    continue;
                }
                if (depth <= -_partBoundsMargin)
                {
                    continue;
                }
                if (best == NodeRef.None || depth > bestDepth)
                {
                    best = result;
                    bestDepth = depth;
                }
            }
            return best;
        }

        public NodeRef UpdateNodeRef(NodeRef current, Vector3 prevPos, Vector3 curPos)
        {
            Debug.Assert(current.PartIndex != -1);
            for (int i = 0; i < _portals.Count; i++)
            {
                Portal portal = _portals[i];
                if (!portal.Active)
                {
                    continue;
                }
                if (portal.NodeRef1.PartIndex == current.PartIndex)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: false))
                    {
                        return portal.NodeRef2;
                    }
                }
                if (portal.NodeRef2.PartIndex == current.PartIndex)
                {
                    if (CollisionDetection.CheckPortBetweenPoints(portal, prevPos, curPos, otherSide: true))
                    {
                        return portal.NodeRef1;
                    }
                }
            }
            return current;
        }

        public bool IsNodeRefAudible(NodeRef nodeRef)
        {
            if (nodeRef.PartIndex == -1)
            {
                return true;
            }
            return _audibleRoomParts[nodeRef.PartIndex];
        }

        public bool IsNodeRefVisible(NodeRef nodeRef)
        {
            // Nothing was culled at all this frame, so nothing may be culled
            // against it. This is the same test GetDrawInfo makes to decide
            // it must draw every part of the room -- the camera had no part
            // to walk the portal graph from -- and while it holds, the active
            // set is empty because it was never filled in, not because
            // everything is out of view.
            //
            // Read the other way round it hid every other player in the
            // match. A client joining one in progress spawns from a snapshot,
            // whose node ref is looked up from the spawn position and can
            // come back as none; nothing ever recovers it, because the
            // one-hop walk needs a part to start from. Its owner then played
            // a whole match in a room that drew perfectly and contained
            // nobody else -- opponents who could be shot and could not be
            // seen, with their shadows still moving about on the floor.
            // Straight out of a real match's log: MP6 HEADSHOT, slot 1
            // (local) nodeRef=none for two solid minutes while slot 0 held
            // a part the whole time.
            if (_partVisInfoHead == null || _scene.ShowAllNodes)
            {
                return true;
            }
            // workaround for unintended modes
            if (nodeRef.PartIndex == -1)
            {
                return false;
            }
            return _activeRoomParts[nodeRef.PartIndex];
        }

        private readonly HashSet<NodeData3> _drawnNodeData = [];

        public override void GetDrawInfo()
        {
            if (!Hidden)
            {
                for (int i = 0; i < _connectorModels.Count; i++)
                {
                    ModelInstance conInst = _connectorModels[i];
                    if (!conInst.Active)
                    {
                        continue;
                    }
                    _scene.UpdateMaterials(conInst.Model, recolorId: 0);
                    if (GameState.InRoomTransition || _partVisInfoHead == null || _scene.ShowAllNodes)
                    {
                        var transform = Matrix4.CreateScale(conInst.Model.Scale);
                        transform.Row3.Xyz = _roomCollision[i + 1].Translation;
                        IReadOnlyList<Node> nodes = conInst.Model.Nodes;
                        for (int j = 0; j < nodes.Count; j++)
                        {
                            nodes[j].Animation = transform;
                        }
                        DrawAllNodes(conInst, connector: true);
                    }
                }
                if (!GameState.InRoomTransition)
                {
                    ModelInstance inst = _models[0];
                    UpdateTransforms(inst, 0);
                    if (_scene.ProcessFrame)
                    {
                        ClearRoomPartState();
                        UpdateRoomParts();
                    }
                    if (_partVisInfoHead == null || _scene.ShowAllNodes)
                    {
                        DrawAllNodes(inst);
                    }
                    else
                    {
                        DrawRoomParts(inst);
                    }
                }
            }
            else if (_scene.ProcessFrame) // skdebug
            {
                ClearRoomPartState();
                UpdateRoomParts();
            }
            if (_scene.ShowCollision && (_scene.ColEntDisplay == EntityType.All || _scene.ColEntDisplay == Type))
            {
                GetCollisionDrawInfo();
            }
            if (_nodeData != null && (_scene.ShowNodeData || _scene.ShowVolumes == VolumeDisplay.NodeData))
            {
                _drawnNodeData.Clear();
                Debug.Assert(_models.Count == 2);
                ModelInstance inst = _models[1];
                int polygonId = _scene.GetNextPolygonId();
                for (int i = 0; i < _nodeData.Data.Count; i++)
                {
                    IReadOnlyList<IReadOnlyList<NodeData3>> str1 = _nodeData.Data[i];
                    for (int j = 0; j < str1.Count; j++)
                    {
                        IReadOnlyList<NodeData3> str2 = str1[j];
                        for (int k = 0; k < str2.Count; k++)
                        {
                            NodeData3 str3 = str2[k];
                            if (!_drawnNodeData.Contains(str3))
                            {
                                GetNodeDataItem(inst, str3.Transform, str3.Color, polygonId);
                                _drawnNodeData.Add(str3);
                                if (_scene.ShowVolumes == VolumeDisplay.NodeData)
                                {
                                    var sphere = new CollisionVolume(str3.Position, str3.MaxDistance);
                                    AddVolumeItem(sphere, Vector3.UnitX);
                                }
                            }
                        }
                    }
                }
            }

            void GetNodeDataItem(ModelInstance inst, Matrix4 transform, Vector4 color, int polygonId)
            {
                Model model = inst.Model;
                Node node = model.Nodes[3];
                if (node.Enabled)
                {
                    int start = node.MeshId / 2;
                    for (int k = 0; k < node.MeshCount; k++)
                    {
                        Mesh mesh = model.Meshes[start + k];
                        if (!mesh.Visible)
                        {
                            continue;
                        }
                        Material material = model.Materials[mesh.MaterialId];
                        _scene.AddRenderItem(material, polygonId, 1, Vector3.Zero, GetLightInfo(), Matrix4.Identity,
                            transform, mesh.ListId, 0, _emptyMatrixStack, color, null, SelectionType.None, node.BillboardMode);
                    }
                }
            }
        }

        private bool IsNodeVisible(FrustumInfo frustumInfo, Node node, int mask, Vector3 offset)
        {
            float[] bounds = node.Bounds;
            for (int i = 0; i < frustumInfo.Count; i++)
            {
                Debug.Assert((mask & (1 << i)) != 0); // todo: if this never happens, we can just get rid of mask
                FrustumPlane frustumPlane = frustumInfo.Planes[i];
                // frustum planes have outward facing normals -- near plane = false if min bounds are on outer side,
                // right plane = false if min bounds are on outer side, left plane = false if max bounds are on outer side,
                // bottom plane = false if max bounds are on outer side, top plane = false if min bounds are on outer side
                Vector4 plane = frustumPlane.Plane;
                if (plane.X * (bounds[frustumPlane.XIndex2] + offset.X)
                    + plane.Y * (bounds[frustumPlane.YIndex2] + offset.Y)
                    + plane.Z * (bounds[frustumPlane.ZIndex2] + offset.Z) - plane.W < 0)
                {
                    return false;
                }
                if (plane.X * (bounds[frustumPlane.XIndex1] + offset.X)
                    + plane.Y * (bounds[frustumPlane.YIndex1] + offset.Y)
                    + plane.Z * (bounds[frustumPlane.ZIndex1] + offset.Z) - plane.W >= 0)
                {
                    mask &= ~(1 << i);
                }
            }
            return true;
        }

        private void DrawRoomParts(ModelInstance roomInst)
        {
            _excludedNodes.Clear();
            if (PlayerEntity.Main.MorphCamera != null)
            {
                for (int i = 0; i < _morphCameraExcludeNodes.Count; i++)
                {
                    _excludedNodes.Add(_morphCameraExcludeNodes[i]);
                }
            }
            RoomPartVisInfo? roomPart = _partVisInfoHead;
            while (roomPart != null)
            {
                RoomFrustumItem? frustumItem = _roomFrustumLinks[roomPart.NodeRef.PartIndex];
                int nodeIndex = roomPart.NodeRef.NodeIndex;
                int modelIndex = roomPart.NodeRef.ModelIndex;
                Debug.Assert(frustumItem != null);
                Debug.Assert(nodeIndex != -1);
                Debug.Assert(modelIndex != -1);
                Vector3 offset = Vector3.Zero;
                ModelInstance partInst;
                Matrix4 transform = Matrix4.Identity;
                if (modelIndex == 0)
                {
                    partInst = _models[0];
                }
                else
                {
                    partInst = _connectorModels[modelIndex - 1];
                    offset = _roomCollision[modelIndex].Translation;
                    transform = Matrix4.CreateScale(partInst.Model.Scale);
                    transform.Row3.Xyz = offset;
                }
                if (!partInst.Active)
                {
                    roomPart = roomPart.Next;
                    continue;
                }
                while (nodeIndex != -1)
                {
                    Node? node = partInst.Model.Nodes[nodeIndex];
                    Debug.Assert(node.ChildIndex == -1);
                    if (!node.Enabled || node.MeshCount == 0 || _excludedNodes.Contains(node))
                    {
                        nodeIndex = node.NextIndex;
                        continue;
                    }
                    RoomFrustumItem? frustumLink = frustumItem;
                    while (frustumLink != null)
                    {
                        if (IsNodeVisible(frustumLink.Info, node, 0x8FFF, offset))
                        {
                            if (offset != Vector3.Zero)
                            {
                                node.Animation = transform;
                            }
                            GetItems(partInst, node);
                            if (_nodePairs.TryGetValue(node, out Node? exclude))
                            {
                                _excludedNodes.Add(exclude);
                            }
                            break;
                        }
                        frustumLink = frustumLink.Next;
                    }
                    nodeIndex = node.NextIndex;
                }
                roomPart = roomPart.Next;
            }
            // todo: use visibility list for portals too
            if (_scene.ShowForceFields)
            {
                for (int i = 0; i < _forceFields.Count; i++)
                {
                    PortalNodeRef forceField = _forceFields[i];
                    Node pnode = Nodes[forceField.NodeIndex];
                    if (pnode.ChildIndex != -1)
                    {
                        Node node = Nodes[pnode.ChildIndex];
                        GetItems(roomInst, node, forceField.Portal);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != -1)
                        {
                            node = Nodes[nextIndex];
                            GetItems(roomInst, node, forceField.Portal);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }
        }

        private void DrawAllNodes(ModelInstance inst, bool connector = false)
        {
            _excludedNodes.Clear();
            IReadOnlyList<Node> nodes = inst.Model.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node pnode = nodes[i];
                if (!pnode.Enabled)
                {
                    continue;
                }
                if (_scene.ShowAllNodes || connector)
                {
                    GetItems(inst, pnode);
                }
                else if (pnode.RoomPartId >= 0)
                {
                    int nodeIndex = pnode.ChildIndex;
                    while (nodeIndex != -1)
                    {
                        Node node = nodes[nodeIndex];
                        if (!_excludedNodes.Contains(node))
                        {
                            GetItems(inst, node);
                            if (_nodePairs.TryGetValue(node, out Node? exclude))
                            {
                                _excludedNodes.Add(exclude);
                            }
                        }
                        nodeIndex = node.NextIndex;
                    }
                }
            }
            if (_scene.ShowForceFields && !connector)
            {
                for (int i = 0; i < _forceFields.Count; i++)
                {
                    PortalNodeRef forceField = _forceFields[i];
                    Node pnode = Nodes[forceField.NodeIndex];
                    if (pnode.ChildIndex != -1)
                    {
                        Node node = Nodes[pnode.ChildIndex];
                        GetItems(inst, node, forceField.Portal);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != -1)
                        {
                            node = Nodes[nextIndex];
                            GetItems(inst, node, forceField.Portal);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }
        }

        private void GetItems(ModelInstance inst, Node node, Portal? portal = null)
        {
            if (!node.Enabled)
            {
                return;
            }
            Model model = inst.Model;
            int start = node.MeshId / 2;
            for (int k = 0; k < node.MeshCount; k++)
            {
                int polygonId = 0;
                Mesh mesh = model.Meshes[start + k];
                if (!mesh.Visible)
                {
                    continue;
                }
                Material material = model.Materials[mesh.MaterialId];
                float alpha = 1.0f;
                if (portal != null)
                {
                    polygonId = _scene.GetNextPolygonId();
                    alpha = GetPortalAlpha(portal.Position, _scene.CameraPosition);
                }
                else if (material.RenderMode == RenderMode.Translucent)
                {
                    polygonId = _scene.GetNextPolygonId();
                }
                Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node);
                SelectionType selectionType = Selection.CheckSelection(this, inst, node, mesh);
                _scene.AddRenderItem(material, polygonId, alpha, emission: Vector3.Zero, GetLightInfo(),
                    texcoordMatrix, node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues,
                    overrideColor: null, paletteOverride: null, selectionType, node.BillboardMode);
            }
        }

        private float GetPortalAlpha(Vector3 portalPosition, Vector3 cameraPosition)
        {
            float between = (portalPosition - cameraPosition).Length;
            between /= 8;
            if (between < 1 / 4096f)
            {
                between = 0;
            }
            return MathF.Min(between, 1);
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.NodeBounds)
            {
                if (Selection.Node != null && Nodes.Contains(Selection.Node))
                {
                    Node node = Selection.Node;
                    float width = node.MaxBounds.X - node.MinBounds.X;
                    float height = node.MaxBounds.Y - node.MinBounds.Y;
                    float depth = node.MaxBounds.Z - node.MinBounds.Z;
                    var box = new CollisionVolume(Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ,
                        node.MinBounds.WithZ(node.MaxBounds.Z), width, height, depth);
                    AddVolumeItem(box, Vector3.UnitX);
                }
            }
            else if (_scene.ShowVolumes == VolumeDisplay.Portal)
            {
                for (int i = 0; i < _portals.Count; i++)
                {
                    Portal portal = _portals[i];
                    if (!portal.Active)
                    {
                        continue;
                    }
                    int count = portal.Points.Count;
                    Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(count);
                    for (int j = 0; j < count; j++)
                    {
                        verts[j] = portal.Points[j];
                    }
                    float alpha = GetPortalAlpha(portal.Position, _scene.CameraPosition);
                    Vector4 color = portal.IsForceField
                        ? new Vector4(16 / 31f, 16 / 31f, 1f, alpha)
                        : new Vector4(16 / 31f, 1f, 16 / 31f, alpha);
                    _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Ngon, verts, count, noLines: true);
                }
            }
            else if (_scene.ShowVolumes == VolumeDisplay.KillPlane && !_meta.FirstHunt)
            {
                Vector3[] verts = ArrayPool<Vector3>.Shared.Rent(4);
                verts[0] = new Vector3(10000f, _scene.KillHeight, 10000f);
                verts[1] = new Vector3(10000f, _scene.KillHeight, -10000f);
                verts[2] = new Vector3(-10000f, _scene.KillHeight, -10000f);
                verts[3] = new Vector3(-10000f, _scene.KillHeight, 10000f);
                var color = new Vector4(1f, 0f, 1f, 0.5f);
                _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Quad, verts, noLines: true);
            }
            else if ((_scene.ShowVolumes == VolumeDisplay.CameraLimit || _scene.ShowVolumes == VolumeDisplay.PlayerLimit) && _meta.HasLimits)
            {
                Vector3 minLimit = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? _meta.CameraMin : _meta.PlayerMin;
                Vector3 maxLimit = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? _meta.CameraMax : _meta.PlayerMax;
                Vector3[] bverts = ArrayPool<Vector3>.Shared.Rent(8);
                Vector3 point0 = minLimit;
                var sideX = new Vector3(maxLimit.X - minLimit.X, 0, 0);
                var sideY = new Vector3(0, maxLimit.Y - minLimit.Y, 0);
                var sideZ = new Vector3(0, 0, maxLimit.Z - minLimit.Z);
                bverts[0] = point0;
                bverts[1] = point0 + sideZ;
                bverts[2] = point0 + sideX;
                bverts[3] = point0 + sideX + sideZ;
                bverts[4] = point0 + sideY;
                bverts[5] = point0 + sideY + sideZ;
                bverts[6] = point0 + sideX + sideY;
                bverts[7] = point0 + sideX + sideY + sideZ;
                Vector4 color = _scene.ShowVolumes == VolumeDisplay.CameraLimit ? new Vector4(1, 0, 0.69f, 0.5f) : new Vector4(1, 0, 0, 0.5f);
                _scene.AddRenderItem(CullingMode.Neither, _scene.GetNextPolygonId(), color, RenderItemType.Box, bverts, 8);
            }
        }

        private readonly struct PortalNodeRef
        {
            public readonly Portal Portal;
            public readonly int NodeIndex;

            public PortalNodeRef(Portal portal, int nodeIndex)
            {
                Portal = portal;
                NodeIndex = nodeIndex;
            }
        }
    }
}
