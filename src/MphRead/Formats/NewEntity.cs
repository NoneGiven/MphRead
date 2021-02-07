using System;
using System.Collections.Generic;
using System.Linq;
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

        public EntityBase(NewEntityType type)
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

        // mtodo: objects need to return false if scanvisor etc.
        public override void Process(NewScene scene)
        {
            ShouldDraw = scene.ShowInvisible;
        }
    }

    public abstract class VisibleEntityBase : EntityBase
    {
        public float Alpha { get; set; } = 1.0f;
        public new int Recolor { get; set; }
        public Vector3 Scale { get; set; } = Vector3.One;

        protected readonly List<NewModel> _models = new List<NewModel>();

        protected virtual bool UseNodeTransform => true;

        protected bool _anyLighting = false;

        public VisibleEntityBase(NewEntityType type) : base(type)
        {
        }

        public override void Process(NewScene scene)
        {
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
                    // mtodo: parent transform (do we really need scale separately?)
                    model.AnimateNodes(0, UseNodeTransform || scene.TransformRoomNodes, Matrix4.Identity, Scale, _nodeAnimCurFrame);
                    model.UpdateMatrixStack(scene.ViewInvRotMatrix, scene.ViewInvRotYMatrix);
                    // todo: could skip this unless a relevant material property changed this update (and we're going to draw this entity)
                    scene.UpdateMaterials(model, Recolor);
                }
            }
            ShouldDraw = Alpha > 0;
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
                // mtodo: follow the child/next stuff and avoid needing NodeParentsEnabled
                for (int j = 0; j < model.Nodes.Count; j++)
                {
                    Node node = model.Nodes[j];
                    if (node.MeshCount == 0 || !node.Enabled/* || !model.NodeParentsEnabled(node)*/)
                    {
                        continue;
                    }
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
                        // mtodo: main transform
                        var item = new RenderItem(polygonId, Alpha * material.CurrentAlpha, material.PolygonMode, material.RenderMode,
                            material.Culling, material.Wireframe != 0, material.Lighting != 0, material.CurrentDiffuse, material.CurrentAmbient,
                            material.CurrentSpecular, emission: Vector3.Zero, material.TexgenMode, material.XRepeat, material.YRepeat,
                            material.TextureId != UInt16.MaxValue, material.TextureBindingId, texcoordMatrix, Matrix4.Identity, mesh.ListId);
                        scene.AddRenderItem(item);
                    }
                }
            }
        }

        private Matrix4 GetTexcoordMatrix(NewModel model, Material material, Node node, NewScene scene)
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
            if (material.TexgenMode != TexgenMode.None) // mtodo: texgen mode (among other things) can be overriden by double damage
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
                    if (Scale.X != 1 || Scale.Y != 1 || Scale.Z != 1)
                    {
                        texgenMatrix = Matrix4.CreateScale(Scale) * texgenMatrix;
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

        // mtodo: maybe remove CurrentFrame from these classes
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
        public ModelEntity(NewModel model) : base(NewEntityType.Model)
        {
            _models.Add(model);
            _anyLighting = model.Materials.Any(m => m.Lighting != 0);
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
}
