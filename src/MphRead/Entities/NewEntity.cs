using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    // ntodo: merge this with the other enum
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
        public bool ShouldDraw { get; protected set; } = true;

        protected Matrix4 _transform = Matrix4.Identity;
        protected Vector3 _scale = new Vector3(1, 1, 1);
        protected Vector3 _rotation = Vector3.Zero;
        protected Vector3 _position = Vector3.Zero;

        public Matrix4 Transform
        {
            get
            {
                return _transform;
            }
            set
            {
                _scale = value.ExtractScale();
                value.ExtractRotation().ToEulerAngles(out _rotation);
                _position = value.Row3.Xyz;
                _transform = value;
            }
        }

        public Vector3 Scale
        {
            get
            {
                return _scale;
            }
            set
            {
                _transform = Matrix4.CreateScale(value) * Matrix4.CreateRotationZ(Rotation.Z)
                    * Matrix4.CreateRotationY(Rotation.Y) * Matrix4.CreateRotationX(Rotation.X);
                _transform.Row3.Xyz = Position;
                _scale = value;
            }
        }

        public Vector3 Rotation
        {
            get
            {
                return _rotation;
            }
            set
            {
                _transform = Matrix4.CreateScale(Scale) * Matrix4.CreateRotationZ(value.Z)
                    * Matrix4.CreateRotationY(value.Y) * Matrix4.CreateRotationX(value.X);
                _transform.Row3.Xyz = Position;
                _rotation = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                return _position;
            }
            set
            {
                _transform.Row3.Xyz = value;
                _position = value;
            }
        }

        protected EntityBase(NewEntityType type)
        {
            Type = type;
        }

        public virtual void Init(NewScene scene)
        {
        }

        public virtual void Process(NewScene scene)
        {
        }

        public virtual void UpdateTransforms(NewScene scene)
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

        protected bool _anyLighting = false;
        protected readonly List<NewModel> _models = new List<NewModel>();

        protected virtual bool UseNodeTransform => true;

        public VisibleEntityBase(NewEntityType type) : base(type)
        {
        }

        public override void Init(NewScene scene)
        {
            _anyLighting = _models.Any(n => n.Materials.Any(m => m.Lighting != 0));
            for (int i = 0; i < _models.Count; i++)
            {
                NewModel model = _models[i];
                _materialAnimFrames.Add(model, 0);
                _texcoordAnimFrames.Add(model, 0);
                _textureAnimFrames.Add(model, 0);
                _nodeAnimFrames.Add(model, 0);
            }
        }

        protected virtual Matrix4 GetModelTransform(NewModel model, int index)
        {
            return Matrix4.CreateScale(model.Scale) * _transform;
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
                }
            }
        }

        protected virtual int GetModelRecolor(NewModel model, int index)
        {
            return Recolor;
        }

        public override void UpdateTransforms(NewScene scene)
        {
            if (ShouldDraw && Alpha > 0)
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    NewModel model = _models[i];
                    if (model.Active)
                    {
                        model.AnimateMaterials(_materialAnimFrames[model]);
                        model.AnimateTextures(_textureAnimFrames[model]);
                        model.ComputeNodeMatrices(index: 0);
                        Matrix4 transform = GetModelTransform(model, i);
                        model.AnimateNodes(index: 0, UseNodeTransform || scene.TransformRoomNodes, transform, model.Scale, _nodeAnimFrames[model]);
                        model.UpdateMatrixStack(scene.ViewInvRotMatrix, scene.ViewInvRotYMatrix);
                        // todo: could skip this unless a relevant material property changed this update (and we're going to draw this entity)
                        scene.UpdateMaterials(model, GetModelRecolor(model, i));
                    }
                }
            }
        }

        public override IEnumerable<NewModel> GetModels()
        {
            return _models;
        }

        public virtual LightInfo GetLightInfo(NewModel model, NewScene scene)
        {
            return new LightInfo(scene.Light1Vector, scene.Light1Color, scene.Light2Vector, scene.Light2Color);
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
                        scene.AddRenderItem(material, polygonId, Alpha, emission: Vector3.Zero, GetLightInfo(model, scene),
                            texcoordMatrix, node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues);
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
                texcoordMatrix = model.AnimateTexcoords(group, animation.Value, _texcoordAnimFrames[model]);
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

        private readonly Dictionary<NewModel, int> _materialAnimFrames = new Dictionary<NewModel, int>();
        private readonly Dictionary<NewModel, int> _texcoordAnimFrames = new Dictionary<NewModel, int>();
        private readonly Dictionary<NewModel, int> _textureAnimFrames = new Dictionary<NewModel, int>();
        private readonly Dictionary<NewModel, int> _nodeAnimFrames = new Dictionary<NewModel, int>();

        // ntodo: maybe remove CurrentFrame from these classes
        private void UpdateAnimationFrames(NewModel model)
        {
            if (model.Animations.MaterialGroup != null)
            {
                int frame = _materialAnimFrames[model];
                frame++;
                frame %= model.Animations.MaterialGroup.FrameCount;
                _materialAnimFrames[model] = frame;
            }
            if (model.Animations.TexcoordGroup != null)
            {
                int frame = _texcoordAnimFrames[model];
                frame++;
                frame %= model.Animations.TexcoordGroup.FrameCount;
                _texcoordAnimFrames[model] = frame;
            }
            if (model.Animations.TextureGroup != null)
            {
                int frame = _textureAnimFrames[model];
                frame++;
                frame %= model.Animations.TextureGroup.FrameCount;
                _textureAnimFrames[model] = frame;
            }
            if (model.Animations.NodeGroup != null)
            {
                int frame = _nodeAnimFrames[model];
                frame++;
                frame %= model.Animations.NodeGroup.FrameCount;
                _nodeAnimFrames[model] = frame;
            }
        }

        protected void ComputeTransform(Vector3Fx vector2, Vector3Fx vector1, Vector3Fx position)
        {
            ComputeTransform(vector2.ToFloatVector(), vector1.ToFloatVector(), position.ToFloatVector());
        }

        private void ComputeTransform(Vector3 vector2, Vector3 vector1, Vector3 position)
        {
            Matrix4 transform = GetTransformMatrix(vector2, vector1);
            transform.ExtractRotation().ToEulerAngles(out Vector3 rotation);
            Rotation = rotation;
            Position = position;
        }

        protected Matrix4 GetTransformMatrix(Vector3 vector2, Vector3 vector1)
        {
            Vector3 up = Vector3.Cross(vector1, vector2).Normalized();
            var direction = Vector3.Cross(vector2, up);
            Matrix4 transform = default;
            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;
            transform.M14 = 0;
            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;
            transform.M24 = 0;
            transform.M31 = vector2.X;
            transform.M32 = vector2.Y;
            transform.M33 = vector2.Z;
            transform.M34 = 0;
            transform.M41 = 0;
            transform.M42 = 0;
            transform.M43 = 0;
            transform.M44 = 1;
            return transform;
        }
    }

    public class ModelEntity : VisibleEntityBase
    {
        public ModelEntity(NewModel model, int recolor = 0) : base(NewEntityType.Model)
        {
            Recolor = recolor;
            _models.Add(model);
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
