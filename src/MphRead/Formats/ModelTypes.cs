using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats.Collision;

namespace MphRead.Models
{
    public class RoomModel : Model
    {
        private readonly IReadOnlyList<CollisionPortal> _portals = new List<CollisionPortal>();
        private readonly IReadOnlyList<ForceFieldNodeRef> _forceFields = new List<ForceFieldNodeRef>();

        public RoomModel(Model model) : base(model)
        {
            Type = ModelType.Room;
        }

        public RoomModel(Model model, RoomMetadata meta, CollisionInfo collision, int layerMask) : base(model)
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
                foreach (CollisionPortal portal in portals.Where(p => p.Name.StartsWith("pmag")))
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
            _portals = portals;
            _forceFields = forceFields;
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
}
