using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public enum NewEntityType
    {
        Platform = 0,
        Object = 1,
        PlayerSpawn = 2,
        Door = 3,
        Item = 4,
        ItemInstance = 5,
        Enemy = 6,
        TriggerVolume = 7,
        AreaVolume = 8,
        JumpPad = 9,
        PointModule = 10,
        MorphCamera = 11,
        OctolithFlag = 12,
        FlagBase = 13,
        Teleporter = 14,
        NodeDefense = 15,
        LightSource = 16,
        Artifact = 17,
        CameraSequence = 18,
        ForceField = 19,
        EffectInstance = 21,
        Bomb = 22,
        EnemyInstance = 23,
        Halfturret = 24,
        Player = 25,
        BeamProjectile = 26,
        Room = 100,
        Effect = 101,
        Particle = 102,
        Model = 103
    }

    public interface IRenderable
    {
        NewEntityType Type { get; }
        int Recolor { get; }
        IEnumerable<NewModel> GetModels();
        void GetDrawInfo(NewScene scene);
    }

    public abstract class EntityBase : IRenderable
    {
        public int Id { get; protected set; } = -1; // todo: use init
        public int Recolor { get; }
        public NewEntityType Type { get; }
        public bool ShouldDraw { get; set; } = true;

        public Vector3 Position { get; set; }

        protected EntityBase(NewEntityType type)
        {
            Type = type;
        }

        public virtual void Process(NewScene scene)
        {
        }

        public virtual IEnumerable<NewModel> GetModels()
        {
            return Enumerable.Empty<NewModel>();
        }

        public virtual void GetDrawInfo(NewScene scene)
        {
        }
    }

    public abstract class InvisibleEntityBase : EntityBase
    {
        public Vector3 PlaceholderColor { get; }

        public InvisibleEntityBase(NewEntityType type) : base(type)
        {
        }

        // ntodo: objects need to return false if scanvisor etc.
        public override void Process(NewScene scene)
        {
            ShouldDraw = scene.ShowInvisible;
        }
    }

    public abstract class VisibleEntityBase : EntityBase
    {
        public float Alpha { get; set; } = 1.0f;
        public new int Recolor { get; set; }

        protected readonly List<NewModel> _models = new List<NewModel>();

        protected virtual bool UseNodeTransform => true;

        protected bool _anyLighting = false;

        public VisibleEntityBase(NewEntityType type) : base(type)
        {
        }

        public override void Process(NewScene scene)
        {
            ShouldDraw = false;
            if (Alpha > 0)
            {
                ShouldDraw = true;
                for (int i = 0; i < _models.Count; i++)
                {
                    NewModel model = _models[i];
                    if (model.Active)
                    {
                        if (scene.FrameCount != 0 && scene.FrameCount % 2 == 0)
                        {
                            UpdateAnimationFrames(model);
                        }
                        model.AnimateMaterials(_materialAnimCurFrame);
                        model.AnimateTextures(_textureAnimCurFrame);
                        model.ComputeNodeMatrices(index: 0);
                        // mtodo: parent transform (QUATERNIONS)
                        var transform = Matrix4.CreateScale(model.Scale);
                        model.AnimateNodes(index: 0, UseNodeTransform || scene.TransformRoomNodes, transform, model.Scale, _nodeAnimCurFrame);
                        model.UpdateMatrixStack(scene.ViewInvRotMatrix, scene.ViewInvRotYMatrix);
                        // todo: could skip this unless a relevant material property changed this update (and we're going to draw this entity)
                        scene.UpdateMaterials(model, Recolor);
                    }
                }
            }
        }

        public override IEnumerable<NewModel> GetModels()
        {
            return _models;
        }

        public override void GetDrawInfo(NewScene scene)
        {
            for (int i = 0; i < _models.Count; i++)
            {
                NewModel model = _models[i];
                if (!model.Active)
                {
                    continue;
                }
                int polygonId = scene.GetNextPolygonId();
                GetItems(model, model.Nodes[0], polygonId);
            }

            void GetItems(NewModel model, Node node, int polygonId)
            {
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
                        Matrix4 texcoordMatrix = GetTexcoordMatrix(model, material, node, scene);
                        scene.AddRenderItem(material, polygonId, Alpha, emission: Vector3.Zero, texcoordMatrix,
                            node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues);
                    }
                    if (node.ChildIndex != UInt16.MaxValue)
                    {
                        GetItems(model, model.Nodes[node.ChildIndex], polygonId);
                    }
                }
                if (node.NextIndex != UInt16.MaxValue)
                {
                    GetItems(model, model.Nodes[node.NextIndex], polygonId);
                }
            }
        }

        protected Matrix4 GetTexcoordMatrix(NewModel model, Material material, Node node, NewScene scene)
        {
            Matrix4 texcoordMatrix = Matrix4.Identity;
            TexcoordAnimationGroup? group = model.Animations.TexcoordGroup;
            TexcoordAnimation? animation = null;
            if (group != null && group.Animations.TryGetValue(material.Name, out TexcoordAnimation result))
            {
                animation = result;
            }
            if (group != null && animation != null)
            {
                texcoordMatrix = model.AnimateTexcoords(group, animation.Value, _texcoordAnimCurFrame);
            }
            if (material.TexgenMode != TexgenMode.None) // ntodo: texgen mode (among other things) can be overriden by double damage
            {
                Matrix4 materialMatrix;
                // in-game, this is a list of precomputed matrices that we compute on the fly in the next block;
                // however, we only use it for the one hard-coded matrix in AlimbicCapsule
                if (model.TextureMatrices.Count > 0)
                {
                    materialMatrix = model.TextureMatrices[material.MatrixId];
                }
                else
                {
                    materialMatrix = Matrix4.CreateTranslation(material.ScaleS * material.TranslateS,
                        material.ScaleT * material.TranslateT, 0.0f);
                    materialMatrix = Matrix4.CreateScale(material.ScaleS, material.ScaleT, 1.0f) * materialMatrix;
                    materialMatrix = Matrix4.CreateRotationZ(material.RotateZ) * materialMatrix;
                }
                // for texcoord texgen, the animation result is used if any, otherwise the material matrix is used.
                // for normal texgen, two matrices are multiplied. the first is always the material matrix.
                // the second is the animation result if any, otherwise it's the material matrix again.
                if (group == null || animation == null)
                {
                    texcoordMatrix = materialMatrix;
                }
                if (material.TexgenMode == TexgenMode.Normal)
                {
                    Texture texture = model.Recolors[Recolor].Textures[material.TextureId];
                    Matrix4 product = node.Animation.Keep3x3();
                    Matrix4 texgenMatrix = Matrix4.Identity;
                    // in-game, there's only one uniform scale factor for models
                    if (model.Scale.X != 1 || model.Scale.Y != 1 || model.Scale.Z != 1)
                    {
                        texgenMatrix = Matrix4.CreateScale(model.Scale) * texgenMatrix;
                    }
                    // in-game, big 0 is set on creation if any materials have lighting enabled
                    if (_anyLighting || (model.Header.Flags & 1) > 0)
                    {
                        texgenMatrix = scene.ViewMatrix * texgenMatrix;
                    }
                    product *= texgenMatrix;
                    product.M12 *= -1;
                    product.M13 *= -1;
                    product.M22 *= -1;
                    product.M23 *= -1;
                    product.M32 *= -1;
                    product.M33 *= -1;
                    //if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material)
                    //    && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
                    //{
                    //    long frame = _frameCount / 2;
                    //    float rotZ = ((int)(16 * ((781874935307L * (ulong)(53248 * frame) >> 32) + 2048)) >> 20) * (360 / 4096f);
                    //    float rotY = ((int)(16 * ((781874935307L * (ulong)(26624 * frame) + 0x80000000000) >> 32)) >> 20) * (360 / 4096f);
                    //    var rot = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotZ));
                    //    rot *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotY));
                    //    product = rot * product;
                    //}
                    product *= materialMatrix;
                    product *= texcoordMatrix;
                    product *= (1.0f / (texture.Width / 2));
                    texcoordMatrix = new Matrix4(
                        product.Row0 * 16.0f,
                        product.Row1 * 16.0f,
                        product.Row2 * 16.0f,
                        product.Row3
                    );
                    texcoordMatrix.Transpose();
                }
            }
            return texcoordMatrix;
        }

        private int _materialAnimCurFrame = 0;
        private int _texcoordAnimCurFrame = 0;
        private int _textureAnimCurFrame = 0;
        private int _nodeAnimCurFrame = 0;

        // ntodo: maybe remove CurrentFrame from these classes
        private void UpdateAnimationFrames(NewModel model)
        {
            if (model.Animations.MaterialGroup != null)
            {
                _materialAnimCurFrame++;
                _materialAnimCurFrame %= model.Animations.MaterialGroup.FrameCount;
            }
            if (model.Animations.TexcoordGroup != null)
            {
                _texcoordAnimCurFrame++;
                _texcoordAnimCurFrame %= model.Animations.TexcoordGroup.FrameCount;
            }
            if (model.Animations.TextureGroup != null)
            {
                _textureAnimCurFrame++;
                _textureAnimCurFrame %= model.Animations.TextureGroup.FrameCount;
            }
            if (model.Animations.NodeGroup != null)
            {
                _nodeAnimCurFrame++;
                _nodeAnimCurFrame %= model.Animations.NodeGroup.FrameCount;
            }
        }
    }

    public class ModelEntity : VisibleEntityBase
    {
        public ModelEntity(NewModel model, int recolor = 0) : base(NewEntityType.Model)
        {
            Recolor = recolor;
            _models.Add(model);
            _anyLighting = model.Materials.Any(m => m.Lighting != 0);
        }
    }

    public class RoomEntity : VisibleEntityBase
    {
        private readonly IReadOnlyList<CollisionPortal> _portals = new List<CollisionPortal>();
        private readonly IReadOnlyList<ForceFieldNodeRef> _forceFields = new List<ForceFieldNodeRef>();
        private IReadOnlyList<Node> Nodes => _models[0].Nodes;

        protected override bool UseNodeTransform => false; // default -- will use transform if setting is enabled

        public RoomEntity(NewModel model, RoomMetadata meta, CollisionInfo collision, int layerMask) : base(NewEntityType.Room)
        {
            _models.Add(model);
            _anyLighting = model.Materials.Any(m => m.Lighting != 0);
            FilterNodes(layerMask);
            var portals = new List<CollisionPortal>();
            var forceFields = new List<ForceFieldNodeRef>();
            portals.AddRange(collision.Portals.Where(p => (p.LayerMask & 4) != 0 || (p.LayerMask & layerMask) != 0));
            if (portals.Count > 0)
            {
                IEnumerable<string> parts = portals.Select(p => p.NodeName1).Concat(portals.Select(p => p.NodeName2)).Distinct();
                foreach (Node node in model.Nodes)
                {
                    if (parts.Contains(node.Name))
                    {
                        node.IsRoomPartNode = true;
                    }
                }
                IEnumerable<CollisionPortal> pmags = portals.Where(p => p.Name.StartsWith("pmag"));
                foreach (CollisionPortal portal in pmags)
                {
                    for (int i = 0; i < model.Nodes.Count; i++)
                    {
                        if (model.Nodes[i].Name == $"geo{portal.Name[1..]}")
                        {
                            forceFields.Add(new ForceFieldNodeRef(portal, i));
                            break;
                        }
                    }
                }
                // biodefense chamber 04 and 07 don't have the red portal geometry nodes
                Debug.Assert(forceFields.Count == pmags.Count()
                    || model.Name == "biodefense chamber 04" || model.Name == "biodefense chamber 07");
            }
            else if (meta.RoomNodeName != null
                && model.Nodes.TryFind(n => n.Name == meta.RoomNodeName && n.ChildIndex != UInt16.MaxValue, out Node? roomNode))
            {
                roomNode.IsRoomPartNode = true;
            }
            else
            {
                foreach (Node node in model.Nodes)
                {
                    if (node.Name.StartsWith("rm"))
                    {
                        node.IsRoomPartNode = true;
                        break;
                    }
                }
            }
            Debug.Assert(model.Nodes.Any(n => n.IsRoomPartNode));
            _portals = portals;
            _forceFields = forceFields;
        }

        private void FilterNodes(int layerMask)
        {
            foreach (Node node in Nodes)
            {
                if (!node.Name.StartsWith("_"))
                {
                    continue;
                }
                // todo: refactor this
                int flags = 0;
                // we actually have to step through 4 characters at a time rather than using Contains,
                // based on the game's behavior with e.g. "_ml_s010blocks", which is not visible in SP or MP;
                // while it presumably would be in SP since it contains "_s01", that isn't picked up
                for (int i = 0; node.Name.Length - i >= 4; i += 4)
                {
                    string chunk = node.Name.Substring(i, 4);
                    if (chunk.StartsWith("_s") && Int32.TryParse(chunk[2..], out int id))
                    {
                        flags = (int)((uint)flags & 0xC03F | (((uint)flags << 18 >> 24) | (uint)(1 << id)) << 6);
                    }
                    else if (chunk == "_ml0")
                    {
                        flags |= (int)NodeLayer.MultiplayerLod0;
                    }
                    else if (chunk == "_ml1")
                    {
                        flags |= (int)NodeLayer.MultiplayerLod1;
                    }
                    else if (chunk == "_mpu")
                    {
                        flags |= (int)NodeLayer.MultiplayerU;
                    }
                    else if (chunk == "_ctf")
                    {
                        flags |= (int)NodeLayer.CaptureTheFlag;
                    }
                }
                if ((flags & layerMask) == 0)
                {
                    node.Enabled = false;
                }
            }
        }

        public override void GetDrawInfo(NewScene scene)
        {
            NewModel model = _models[0];
            for (int i = 0; i < Nodes.Count; i++)
            {
                Node pnode = Nodes[i];
                if (pnode.IsRoomPartNode && pnode.Enabled)
                {
                    int childIndex = pnode.ChildIndex;
                    if (childIndex != UInt16.MaxValue)
                    {
                        Node node = Nodes[childIndex];
                        Debug.Assert(node.ChildIndex == UInt16.MaxValue);
                        GetItems(model, node);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != UInt16.MaxValue)
                        {
                            node = Nodes[nextIndex];
                            GetItems(model, node);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }
            if (scene.ShowForceFields)
            {
                for (int i = 0; i < _forceFields.Count; i++)
                {
                    ForceFieldNodeRef forceField = _forceFields[i];
                    Node pnode = Nodes[forceField.NodeIndex];
                    if (pnode.ChildIndex != UInt16.MaxValue)
                    {
                        Node node = Nodes[pnode.ChildIndex];
                        GetItems(model, node, forceField.Portal);
                        int nextIndex = node.NextIndex;
                        while (nextIndex != UInt16.MaxValue)
                        {
                            node = Nodes[nextIndex];
                            GetItems(model, node, forceField.Portal);
                            nextIndex = node.NextIndex;
                        }
                    }
                }
            }

            void GetItems(NewModel model, Node node, CollisionPortal? portal = null)
            {
                if (!node.Enabled)
                {
                    return;
                }
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
                        polygonId = UInt16.MaxValue;
                        alpha = GetPortalAlpha(portal.Position, scene.CameraPosition);
                    }
                    else if (material.RenderMode == RenderMode.Translucent)
                    {
                        polygonId = scene.GetNextPolygonId();
                    }
                    Matrix4 texcoordMatrix = GetTexcoordMatrix(model, material, node, scene);
                    scene.AddRenderItem(material, polygonId, alpha, emission: Vector3.Zero, texcoordMatrix,
                        node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues);
                }
            }
        }

        private float GetPortalAlpha(Vector3 portalPosition, Vector3 cameraPosition)
        {
            float between = (portalPosition - cameraPosition * -1).Length;
            return MathF.Min(between / 8, 1);
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

    // todo: for player
    //Vector3 emission = Vector3.Zero;
    //if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material))
    //{
    //    if (material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
    //    {
    //        emission = Metadata.EmissionGray;
    //    }
    //}
    //else if (model.Team == Team.Orange)
    //{
    //    emission = Metadata.EmissionOrange;
    //}
    //else if (model.Team == Team.Green)
    //{
    //    emission = Metadata.EmissionGreen;
    //}

    //if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material)
    //    && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
    //{
    //    texgenMode = TexgenMode.Normal;
    //    xRepeat = RepeatMode.Mirror;
    //    yRepeat = RepeatMode.Mirror;
    //    bindingId = _dblDmgBindingId;
    //}
}
