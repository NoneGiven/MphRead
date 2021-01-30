using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public class ObjectModel : Model
    {
        public CollisionVolume EffectVolume { get; set; }

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

        public override void Process(double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
            // ptodo: effect SFX stuff
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

        public override void Process(double elapsedTime, long frameCount, Vector3 cameraPosition,
            Matrix4 viewInvRot, Matrix4 viewInvRotY, bool useTransform)
        {
            base.Process(elapsedTime, frameCount, cameraPosition, viewInvRot, viewInvRotY, useTransform);
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
}
