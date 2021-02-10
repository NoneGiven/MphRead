using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Effects;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Models
{
    public class RoomModel : Model
    {
        private readonly List<CollisionPortal> _portals = new List<CollisionPortal>();
        private readonly List<ForceFieldNodeRef> _forceFields = new List<ForceFieldNodeRef>();

        public RoomModel(string name, Header header, IReadOnlyList<RawNode> nodes,
            IReadOnlyList<RawMesh> meshes, IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeWeights) : base(name, header, nodes, meshes, materials, dlists, renderInstructions, nodeGroups,
                materialGroups, texcoordGroups, textureGroups, textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights)
        {
            Type = ModelType.Room;
        }

        public void Setup(RoomMetadata meta, CollisionInfo collision, int layerMask)
        {
            Type = ModelType.Room;
            var portals = new List<CollisionPortal>();
            var forceFields = new List<ForceFieldNodeRef>();
            portals.AddRange(collision.Portals.Where(p => (p.LayerMask & 4) != 0 || (p.LayerMask & layerMask) != 0));
            if (portals.Count > 0)
            {
                IEnumerable<string> parts = portals.Select(p => p.NodeName1).Concat(portals.Select(p => p.NodeName2)).Distinct();
                foreach (Node node in Nodes)
                {
                    if (parts.Contains(node.Name))
                    {
                        node.IsRoomPartNode = true;
                    }
                }
                IEnumerable<CollisionPortal> pmags = portals.Where(p => p.Name.StartsWith("pmag"));
                foreach (CollisionPortal portal in pmags)
                {
                    for (int i = 0; i < Nodes.Count; i++)
                    {
                        if (Nodes[i].Name == $"geo{portal.Name[1..]}")
                        {
                            forceFields.Add(new ForceFieldNodeRef(portal, i));
                            break;
                        }
                    }
                }
                // biodefense chamber 04 and 07 don't have the red portal geometry nodes
                Debug.Assert(forceFields.Count == pmags.Count()
                    || Name == "biodefense chamber 04" || Name == "biodefense chamber 07");
            }
            else if (meta.RoomNodeName != null
                && Nodes.TryFind(n => n.Name == meta.RoomNodeName && n.ChildIndex != UInt16.MaxValue, out Node? roomNode))
            {
                roomNode.IsRoomPartNode = true;
            }
            else
            {
                foreach (Node node in Nodes)
                {
                    if (node.Name.StartsWith("rm"))
                    {
                        node.IsRoomPartNode = true;
                        break;
                    }
                }
            }
            Debug.Assert(Nodes.Any(n => n.IsRoomPartNode));
            _portals.AddRange(portals);
            _forceFields.AddRange(forceFields);
        }

        // todo: partial room rendering with toggle
        // --> should also have a toggle to show etags, gray triangle, etc.
        public override IEnumerable<NodeInfo> GetDrawNodes(bool includeForceFields)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                Node node = Nodes[i];
                if (node.IsRoomPartNode)
                {
                    foreach (Node leaf in GetNodeTree(node))
                    {
                        yield return new NodeInfo(leaf);
                    }
                }
            }
            if (includeForceFields)
            {
                for (int i = 0; i < _forceFields.Count; i++)
                {
                    ForceFieldNodeRef forceField = _forceFields[i];
                    foreach (Node node in GetNodeTree(Nodes[forceField.NodeIndex]))
                    {
                        yield return new NodeInfo(node, forceField.Portal);
                    }
                }
            }
        }

        private IEnumerable<Node> GetNodeTree(Node node)
        {
            int childIndex = node.ChildIndex;
            if (childIndex != UInt16.MaxValue)
            {
                node = Nodes[childIndex];
                yield return node;
                int nextIndex = node.NextIndex;
                while (nextIndex != UInt16.MaxValue)
                {
                    node = Nodes[nextIndex];
                    yield return node;
                    nextIndex = node.NextIndex;
                }
            }
        }

        private readonly struct ForceFieldNodeRef
        {
            public readonly CollisionPortal Portal;
            public readonly int NodeIndex;

            public ForceFieldNodeRef(CollisionPortal portal, int nodeIndex)
            {
                Portal = portal;
                NodeIndex = nodeIndex;
            }
        }
    }

    public class PlatformModel : Model
    {
        private Entity<PlatformEntityData>? _entity = null;
        private uint _flags = 0;
        private readonly List<int> _effectNodeIds = new List<int>() { -1, -1, -1, -1 };
        private readonly List<EffectEntry?> _effects = new List<EffectEntry?>() { null, null, null, null };

        private const int _effectId = 182; // nozzleJet

        public PlatformModel(string name, Header header, IReadOnlyList<RawNode> nodes,
            IReadOnlyList<RawMesh> meshes, IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeWeights) : base(name, header, nodes, meshes, materials, dlists, renderInstructions, nodeGroups,
                materialGroups, texcoordGroups, textureGroups, textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights)
        {
            Type = ModelType.Platform;
        }

        public override void Initialize(RenderWindow renderer)
        {
            base.Initialize(renderer);
            _entity = (Entity<PlatformEntityData>)Entity!;
            _flags = _entity.Data.Flags;
            if ((_flags & 0x80000) != 0)
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Node node = Nodes[i];
                    if (node.Name == "R_Turret")
                    {
                        _effectNodeIds[0] = i;
                    }
                    else if (node.Name == "R_Turret1")
                    {
                        _effectNodeIds[1] = i;
                    }
                    else if (node.Name == "R_Turret2")
                    {
                        _effectNodeIds[2] = i;
                    }
                    else if (node.Name == "R_Turret3")
                    {
                        _effectNodeIds[3] = i;
                    }
                }
                if (_effectNodeIds[0] != -1 || _effectNodeIds[1] != -1 || _effectNodeIds[2] != -1 || _effectNodeIds[3] != -1)
                {
                    renderer.LoadEffect(_effectId);
                }
            }
        }

        public override void Process(RenderWindow renderer, double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(renderer, elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
            // todo: if "is_visible" returns false (and other conditions), don't draw the effects
            for (int i = 0; i < 4; i++)
            {
                if (_effectNodeIds[i] >= 0 && _effects[i] == null)
                {
                    var transform = new Matrix4(SceneSetup.GetTransformMatrix(Vector3.UnitX, Vector3.UnitY));
                    transform.M32 = 2;
                    transform.M34 = 1;
                    _effects[i] = renderer.SpawnEffectGetEntry(_effectId, transform);
                    foreach (EffectElementEntry element in _effects[i]!.Elements)
                    {
                        element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                    }
                }
                if (_effects[i] != null)
                {
                    Matrix4 transform = Nodes[_effectNodeIds[i]].Animation;
                    var position = new Vector3(
                        transform.M31 * 1.5f + transform.M41,
                        transform.M32 * 1.5f + transform.M42,
                        transform.M33 * 1.5f + transform.M43
                    );
                    transform = new Matrix4(SceneSetup.GetTransformMatrix(new Vector3(transform.Row1), new Vector3(transform.Row2)));
                    transform.Row3 = new Vector4(position, 1);
                    foreach (EffectElementEntry element in _effects[i]!.Elements)
                    {
                        element.Position = position;
                        element.Transform = transform;
                    }
                }
            }
        }
    }

    public class ObjectModel : Model
    {
        public CollisionVolume EffectVolume { get; set; }
        private Entity<ObjectEntityData>? _entity = null;
        private uint _flags = 0;
        private int _effectIntervalTimer = 0;
        private int _effectIntervalIndex = 0;
        private bool _effectProcessing = false;
        private EffectEntry? _effectEntry = null;
        public bool _effectActive = false;

        public bool ForceSpawnEffect { get; set; }

        public ObjectModel(string name, Header header, IReadOnlyList<RawNode> nodes,
            IReadOnlyList<RawMesh> meshes, IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeWeights) : base(name, header, nodes, meshes, materials, dlists, renderInstructions, nodeGroups,
                materialGroups, texcoordGroups, textureGroups, textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights)
        {
            Type = ModelType.Object;
        }

        public override void Initialize(RenderWindow renderer)
        {
            base.Initialize(renderer);
            _entity = (Entity<ObjectEntityData>)Entity!;
            _flags = _entity.Data.Flags;
            // todo: bits 0 and 1 should be cleared if entity ID is -1 (and they should also be affected by room state otherwise)
            _flags &= 0xFB;
            _flags &= 0xF7;
            _flags &= 0xEF;
            if (_entity.Data.ModelId == UInt32.MaxValue)
            {
                // todo: this also applies for other models depending on the anim ID
                _flags |= 4;
                // todo: this should get cleared if there's an effect ID and "is_visible" returns false
                _flags |= 0x10;
            }
            if (_entity.Data.EffectId != 0)
            {
                renderer.LoadEffect((int)_entity.Data.EffectId);
            }
        }

        public override void Process(RenderWindow renderer, double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(renderer, elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
            // todo: FPS stuff
            if (_entity!.Data.EffectId != 0 && frameCount % 2 == 0)
            {
                bool processEffect = false;
                if ((_entity.Data.EffectFlags & 0x40) != 0)
                {
                    processEffect = true;
                }
                else if ((_flags & 0x10) != 0)
                {
                    if ((_entity.Data.EffectFlags & 1) != 0)
                    {
                        // todo: add an option to disable this check
                        processEffect = EffectVolume.TestPoint(cameraPosition);
                    }
                    else
                    {
                        processEffect = (_flags & 3) != 0;
                    }
                }
                if (ForceSpawnEffect)
                {
                    if (_effectEntry == null)
                    {
                        processEffect = true;
                    }
                    ForceSpawnEffect = false;
                }
                if (processEffect)
                {
                    if (!_effectProcessing)
                    {
                        _effectIntervalTimer = 0;
                        _effectIntervalIndex = 15;
                    }
                    if (--_effectIntervalTimer > 0)
                    {
                        // todo: lots of SFX stuff
                    }
                    else
                    {
                        _effectIntervalIndex++;
                        _effectIntervalIndex %= 16;
                        if ((_entity.Data.EffectFlags & 0x10) != 0)
                        {
                            bool previouslyActive = _effectActive;
                            _effectActive = (_entity.Data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0;
                            if (_effectActive != previouslyActive)
                            {
                                if (!_effectActive)
                                {
                                    RemoveEffect(renderer);
                                }
                                else
                                {
                                    _effectEntry = renderer.SpawnEffectGetEntry((int)_entity.Data.EffectId, Transform);
                                    foreach (EffectElementEntry element in _effectEntry.Elements)
                                    {
                                        element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                                    }
                                }
                            }
                        }
                        else if ((_entity.Data.EffectOnIntervals & (1 << _effectIntervalIndex)) != 0)
                        {
                            // ptodo: mtxptr stuff
                            Matrix4 spawnTransform = Transform;
                            if ((_entity.Data.EffectFlags & 2) != 0)
                            {
                                Vector3 offset = _entity.Data.EffectPositionOffset.ToFloatVector();
                                offset.X *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset.Y *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset.Z *= Fixed.ToFloat(2 * (Test.GetRandomInt1(0x1000u) - 2048));
                                offset = Matrix.Vec3MultMtx3(offset, Transform.ClearScale());
                                spawnTransform = new Matrix4(
                                    spawnTransform.Row0,
                                    spawnTransform.Row1,
                                    spawnTransform.Row2,
                                    new Vector4(offset) + spawnTransform.Row3
                                );
                            }
                            renderer.SpawnEffect((int)_entity.Data.EffectId, spawnTransform);
                        }
                        _effectIntervalTimer = (int)_entity.Data.EffectInterval;
                    }
                }
                _effectProcessing = processEffect;
            }
            if (_effectEntry != null)
            {
                foreach (EffectElementEntry element in _effectEntry.Elements)
                {
                    element.Position = Position;
                    element.Transform = Transform;
                }
            }
        }

        private void RemoveEffect(RenderWindow renderer)
        {
            if (_effectEntry != null)
            {
                if ((_entity!.Data.EffectFlags & 0x20) != 0)
                {
                    renderer.UnlinkEffectEntry(_effectEntry);
                }
                else
                {
                    renderer.DetachEffectEntry(_effectEntry, setExpired: false);
                }
            }
        }
    }

    public class ForceFieldLockModel : Model
    {
        public ForceFieldLockModel(string name, Header header, IReadOnlyList<RawNode> nodes,
            IReadOnlyList<RawMesh> meshes, IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeWeights) : base(name, header, nodes, meshes, materials, dlists, renderInstructions, nodeGroups,
                materialGroups, texcoordGroups, textureGroups, textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights)
        {
            Type = ModelType.Enemy;
        }

        public override void Process(RenderWindow renderer, double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(renderer, elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
            if (Vector3.Dot(cameraPosition - InitialPosition, Vector2) < 0)
            {
                Vector2 *= -1;
                Vector3 position = InitialPosition;
                position.X += Fixed.ToFloat(409) * Vector2.X;
                position.Y += Fixed.ToFloat(409) * Vector2.Y;
                position.Z += Fixed.ToFloat(409) * Vector2.Z;
                Position = position;
                SceneSetup.ComputeModelMatrices(this, Vector2, Vector1);
            }
        }
    }

    public class PlayerModel : Model
    {
        public Hunter Hunter { get; set; }
        public bool AltForm { get; set; }
        public bool Frozen { get; set; }
        // todo: load/init/cache/etc.
        private static Model? _altIceModel;
        private static Model? _samusIceModel;
        private static Model? _noxusIceModel;
        private Model? _bipedIceModel;

        public PlayerModel(string name, Header header, IReadOnlyList<RawNode> nodes,
            IReadOnlyList<RawMesh> meshes, IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions,
            IReadOnlyList<NodeAnimationGroup> nodeGroups, IReadOnlyList<MaterialAnimationGroup> materialGroups,
            IReadOnlyList<TexcoordAnimationGroup> texcoordGroups, IReadOnlyList<TextureAnimationGroup> textureGroups,
            IReadOnlyList<Matrix4> textureMatrices, IReadOnlyList<Recolor> recolors, int defaultRecolor, bool useLightSources,
            IReadOnlyList<int> nodeWeights) : base(name, header, nodes, meshes, materials, dlists, renderInstructions, nodeGroups,
                materialGroups, texcoordGroups, textureGroups, textureMatrices, recolors, defaultRecolor, useLightSources, nodeWeights)
        {
            Type = ModelType.Player;
        }

        public override void Initialize(RenderWindow renderer)
        {
            // todo: hunter scale
            base.Initialize(renderer);
            if (_altIceModel == null)
            {
                _altIceModel = renderer.AddModel("alt_ice", 0, firstHunt: false);
                renderer.InitModel(_altIceModel);
            }
            if (Hunter == Hunter.Noxus || Hunter == Hunter.Trace)
            {
                if (_noxusIceModel == null)
                {
                    _noxusIceModel = renderer.AddModel("nox_ice", 0, firstHunt: false);
                    renderer.InitModel(_noxusIceModel);
                }
            }
            else if (_samusIceModel == null)
            {
                _samusIceModel = renderer.AddModel("samus_ice", 0, firstHunt: false);
                renderer.InitModel(_samusIceModel);
            }
            _bipedIceModel = Hunter == Hunter.Noxus || Hunter == Hunter.Trace ? _noxusIceModel! : _samusIceModel!;
        }

        private void GetFrozenDrawItems()
        {
            if (Frozen)
            {
                if (AltForm)
                {
                    // todo: collision radius scale, height offset
                    Node node = _altIceModel!.Nodes[0];
                    Mesh mesh = _altIceModel.Meshes[node.MeshId / 2];
                    Material material = _altIceModel.Materials[mesh.MaterialId];
                    //var meshInfo = new MeshInfo(_altIceModel, node, mesh, material, polygonId++, 1, Transform);
                    //_nonDecalMeshes.Add(meshInfo);
                    //_translucentMeshes.Add(meshInfo);
                }
                else
                {
                    for (int j = 0; j < _bipedIceModel!.Nodes.Count; j++)
                    {
                        _bipedIceModel.Nodes[j].Animation = Nodes[j].Animation;
                    }
                    // identity matrices are fine since the ice model doesn't have any billboard nodes
                    _bipedIceModel.UpdateMatrixStack(Matrix4.Identity, Matrix4.Identity);
                    float[] stack = _bipedIceModel.MatrixStackValues.ToArray(); // todo: allocations?
                    for (int j = 0; j < _bipedIceModel.Nodes.Count; j++)
                    {
                        Node node = _bipedIceModel.Nodes[j];
                        foreach (Mesh mesh in _bipedIceModel.GetNodeMeshes(node))
                        {
                            Material material = _bipedIceModel.Materials[mesh.MaterialId];
                            //var meshInfo = new MeshInfo(_bipedIceModel, node, mesh, material, polygonId++, 1, stack);
                            //_nonDecalMeshes.Add(meshInfo);
                            //_translucentMeshes.Add(meshInfo);
                        }
                    }
                }
            }
        }

        public override void Process(RenderWindow renderer, double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(renderer, elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
            // todo: test if dead, process respawn cooldown
            float timePct = 0.5f; // todo: time percent (0 --> 1)
            float scale = timePct / 2 + 0.1f;
            // todo: the angle stuff could be removed
            float angle = MathF.Sin(MathHelper.DegreesToRadians(270 - 90 * timePct));
            float sin270 = MathF.Sin(MathHelper.DegreesToRadians(270));
            float sin180 = MathF.Sin(MathHelper.DegreesToRadians(180));
            float offset = (angle - sin270) / (sin180 - sin270);
            for (int j = 1; j < Nodes.Count; j++)
            {
                Node node = Nodes[j];
                var nodePos = new Vector3(node.Animation.Row3);
                nodePos.Y += offset;
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    Debug.Assert(node.ChildIndex > 0);
                    var childPos = new Vector3(Nodes[node.ChildIndex].Animation.Row3);
                    childPos.Y += offset;
                    for (int k = 1; k < 5; k++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + k * (childPos.X - nodePos.X) / 5,
                            nodePos.Y + k * (childPos.Y - nodePos.Y) / 5,
                            nodePos.Z + k * (childPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        renderer.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                if (node.NextIndex != UInt16.MaxValue)
                {
                    Debug.Assert(node.NextIndex > 0);
                    var nextPos = new Vector3(Nodes[node.NextIndex].Animation.Row3);
                    nextPos.Y += offset;
                    for (int k = 1; k < 5; k++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + k * (nextPos.X - nodePos.X) / 5,
                            nodePos.Y + k * (nextPos.Y - nodePos.Y) / 5,
                            nodePos.Z + k * (nextPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        renderer.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                nodePos += (nodePos - Position).Normalized() * offset;
                renderer.AddSingleParticle(SingleType.Death, nodePos, Vector3.One, 1 - timePct, scale);
            }
        }
    }
}
