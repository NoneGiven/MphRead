using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public abstract class EntityBase
    {
        public int Id { get; protected set; } = -1; // todo: use init for Id
        public virtual int Recolor { get; set; }
        public EntityType Type { get; }
        public bool ShouldDraw { get; protected set; } = true;
        public bool Active { get; protected set; } = true;
        public bool Hidden { get; set; }
        public float Alpha { get; set; } = 1.0f;

        protected Scene _scene;
        private readonly string? _nodeName;
        public int NodeRef { get; protected set; }

        protected float _drawScale = 1;
        protected Matrix4 _transform = Matrix4.Identity;
        protected Vector3 _scale = new Vector3(1, 1, 1);
        protected Vector3 _rotation = Vector3.Zero;
        protected Vector3 _position = Vector3.Zero;

        protected Node? _colAttachNode = null;
        private bool _drawColUpdated = true;
        public EntityCollision?[] EntityCollision { get; } = new EntityCollision?[2];
        // todo: look into getting rid of this in favor of EntityCollision
        public Matrix4 CollisionTransform => _colAttachNode == null ? _transform : _colAttachNode.Animation;

        public Matrix4 Transform
        {
            get
            {
                return _transform;
            }
            set
            {
                if (_transform != value)
                {
                    _scale = value.ExtractScale();
                    value.ExtractRotation().ToEulerAngles(out _rotation);
                    _position = value.Row3.Xyz;
                    _transform = value;
                    _drawColUpdated = false;
                }
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
                if (_scale != value)
                {
                    _transform = Matrix4.CreateScale(value) * Matrix4.CreateRotationZ(Rotation.Z)
                        * Matrix4.CreateRotationY(Rotation.Y) * Matrix4.CreateRotationX(Rotation.X);
                    _transform.Row3.Xyz = Position;
                    _scale = value;
                    _drawColUpdated = false;
                }
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
                if (_rotation != value)
                {
                    _transform = Matrix4.CreateScale(Scale) * Matrix4.CreateRotationZ(value.Z)
                        * Matrix4.CreateRotationY(value.Y) * Matrix4.CreateRotationX(value.X);
                    _transform.Row3.Xyz = Position;
                    _rotation = value;
                    _drawColUpdated = false;
                }
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
                if (_position != value)
                {
                    _transform.Row3.Xyz = value;
                    _position = value;
                    _drawColUpdated = false;
                }
            }
        }

        public Vector3 RightVector => Transform.Row0.Xyz.Normalized();
        public Vector3 UpVector => Transform.Row1.Xyz.Normalized();
        public Vector3 FacingVector => Transform.Row2.Xyz.Normalized();

        protected bool _anyLighting = false;
        protected readonly List<ModelInstance> _models = new List<ModelInstance>();

        protected virtual bool UseNodeTransform => true;
        protected virtual Vector4? OverrideColor { get; } = null;
        protected virtual Vector4? PaletteOverride { get; set; } = null;

        protected EntityBase(EntityType type, Scene scene)
        {
            Type = type;
            _scene = scene;
        }

        protected EntityBase(EntityType type, string nodeName, Scene scene)
        {
            Type = type;
            _scene = scene;
            _nodeName = nodeName;
        }

        public virtual void Initialize()
        {
            _anyLighting |= _models.Any(n => n.Model.Materials.Any(m => m.Lighting != 0));
            if (_nodeName != null)
            {
                NodeRef = _scene.Room?.GetNodeRefByName(_nodeName) ?? -1;
            }
        }

        protected ModelInstance SetUpModel(string name, int animIndex = 0, AnimFlags animFlags = AnimFlags.None, bool firstHunt = false)
        {
            ModelInstance inst = Read.GetModelInstance(name, firstHunt);
            inst.SetAnimation(animIndex, animFlags);
            _models.Add(inst);
            return inst;
        }

        protected void SetCollision(CollisionInstance collision, int slot = 0, ModelInstance? attach = null)
        {
            var entCol = new EntityCollision(collision, this);
            SetCollisionMaxAvg(entCol);
            EntityCollision[slot] = entCol;
            _drawColUpdated = false;
            UpdateCollisionTransform(slot, Transform.ClearScale());
            UpdateLinkedInverse(slot);
            if (entCol.Collision != null)
            {
                for (int i = 0; i < entCol.Collision.Info.Points.Count; i++)
                {
                    entCol.DrawPoints.Add(entCol.Collision.Info.Points[i]);
                }
            }
            if (attach != null)
            {
                _colAttachNode = attach.Model.GetNodeByName("attach");
            }
        }

        private void SetCollisionMaxAvg(EntityCollision entCol)
        {
            if (entCol.Collision == null)
            {
                return;
            }
            int count = entCol.Collision.Info.Points.Count;
            Vector3 avg = Vector3.Zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = entCol.Collision.Info.Points[i];
                avg.X += point.X;
                avg.Y += point.Y;
                avg.Z += point.Z;
            }
            avg /= count;
            entCol.InitialCenter = avg; // centroid
            float maxDist = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = entCol.Collision.Info.Points[i];
                float dist = Vector3.Distance(avg, point);
                if (dist > maxDist)
                {
                    maxDist = dist;
                }
            }
            entCol.MaxDistance = maxDist;
        }

        protected void UpdateCollisionTransform(int slot, Matrix4 transform)
        {
            EntityCollision? entCol = EntityCollision[slot];
            if (entCol != null)
            {
                entCol.Transform = transform;
                entCol.Inverse1 = transform.Inverted();
                entCol.CurrentCenter = Matrix.Vec3MultMtx4(entCol.InitialCenter, transform);
            }
        }

        protected void UpdateLinkedInverse(int slot)
        {
            EntityCollision? entCol = EntityCollision[slot];
            if (entCol != null)
            {
                entCol.Inverse2 = entCol.Transform.Inverted();
            }
        }

        private void UpdateDrawCollision()
        {
            if (!_drawColUpdated || _colAttachNode != null)
            {
                Matrix4 transform = CollisionTransform;
                for (int i = 0; i < 2; i++)
                {
                    EntityCollision? entCol = EntityCollision[i];
                    if (entCol?.Collision != null)
                    {
                        CollisionInfo collision = entCol.Collision.Info;
                        for (int j = 0; j < collision.Points.Count; j++)
                        {
                            entCol.DrawPoints[j] = Matrix.Vec3MultMtx4(collision.Points[j], transform);
                        }
                    }
                }
                _drawColUpdated = true;
            }
        }

        public virtual void Destroy()
        {
        }

        protected virtual Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            return Matrix4.CreateScale(inst.Model.Scale) * _transform;
        }

        public virtual void GetPosition(out Vector3 position)
        {
            position = Position;
        }

        public virtual void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = Position;
            up = UpVector;
            facing = FacingVector;
        }

        public virtual bool GetTargetable()
        {
            return true;
        }

        public virtual bool Process()
        {
            if (Active)
            {
                for (int i = 0; i < _models.Count; i++)
                {
                    UpdateAnimFrames(_models[i]);
                }
            }
            return true;
        }

        protected void UpdateAnimFrames(ModelInstance inst)
        {
            if (_scene.FrameCount != 0 && _scene.FrameCount % 2 == 0)
            {
                inst.UpdateAnimFrames();
            }
        }

        protected virtual int GetModelRecolor(ModelInstance inst, int index)
        {
            return Recolor;
        }

        public IReadOnlyList<ModelInstance> GetModels()
        {
            return _models;
        }

        protected void AddPlaceholderModel()
        {
            ModelInstance inst = Read.GetModelInstance("pick_wpn_missile");
            inst.IsPlaceholder = true;
            _models.Add(inst);
        }

        protected virtual Vector4? GetOverrideColor(ModelInstance inst, int index)
        {
            return OverrideColor;
        }

        protected virtual LightInfo GetLightInfo()
        {
            return new LightInfo(_scene.Light1Vector, _scene.Light1Color, _scene.Light2Vector, _scene.Light2Color);
        }

        protected virtual int? GetBindingOverride(ModelInstance inst, Material material, int index)
        {
            return null;
        }

        protected virtual void UpdateTransforms(ModelInstance inst, int index)
        {
            Model model = inst.Model;
            model.AnimateMaterials(inst.AnimInfo);
            model.AnimateTextures(inst.AnimInfo);
            model.ComputeNodeMatrices(index: 0);
            Matrix4 transform = GetModelTransform(inst, index);
            model.AnimateNodes(index: 0, UseNodeTransform || _scene.TransformRoomNodes, transform, model.Scale, inst.AnimInfo);
            model.UpdateMatrixStack();
            // todo: could skip this unless a relevant material property changed this update (and we're going to draw this entity)
            _scene.UpdateMaterials(model, GetModelRecolor(inst, index));
            if (_scene.ShowCollision)
            {
                // if collision is not shown, the "needs update" state will persist until it is
                // --> this is fine since _colPoints are only for display, not detection
                UpdateDrawCollision();
            }
        }

        protected void UpdateTransforms(ModelInstance inst, Matrix4 transform, int recolor)
        {
            Model model = inst.Model;
            model.AnimateMaterials(inst.AnimInfo);
            model.AnimateTextures(inst.AnimInfo);
            model.ComputeNodeMatrices(index: 0);
            model.AnimateNodes(index: 0, UseNodeTransform, transform, model.Scale, inst.AnimInfo);
            model.UpdateMatrixStack();
            _scene.UpdateMaterials(model, recolor);
        }

        protected void UpdateMaterials(ModelInstance inst, int recolor)
        {
            Model model = inst.Model;
            model.AnimateMaterials(inst.AnimInfo);
            model.AnimateTextures(inst.AnimInfo);
            _scene.UpdateMaterials(model, recolor);
        }

        protected void GetDrawItems(ModelInstance inst, int i)
        {
            int polygonId = _scene.GetNextPolygonId();
            GetItems(inst, i, inst.Model.Nodes[0], polygonId);

            void GetItems(ModelInstance inst, int index, Node node, int polygonId)
            {
                Model model = inst.Model;
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
                        Vector3 emission = GetEmission(inst, material, mesh.MaterialId);
                        Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node);
                        Vector4? color = inst.IsPlaceholder ? GetOverrideColor(inst, index) : null;
                        SelectionType selectionType = Selection.CheckSelection(this, inst, node, mesh);
                        int? bindingOverride = GetBindingOverride(inst, material, mesh.MaterialId);
                        _scene.AddRenderItem(material, polygonId, Alpha, emission, GetLightInfo(), texcoordMatrix,
                            node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues, color,
                            PaletteOverride, selectionType, node.BillboardMode, _drawScale, bindingOverride);
                    }
                    if (node.ChildIndex != -1)
                    {
                        GetItems(inst, index, model.Nodes[node.ChildIndex], polygonId);
                    }
                }
                if (node.NextIndex != -1)
                {
                    GetItems(inst, index, model.Nodes[node.NextIndex], polygonId);
                }
            }
        }

        public virtual void GetDrawInfo()
        {
            for (int i = 0; i < _models.Count; i++)
            {
                ModelInstance inst = _models[i];
                if ((!inst.Active && !_scene.ShowAllEntities) || (inst.IsPlaceholder && !_scene.ShowInvisibleEntities && !_scene.ShowAllEntities))
                {
                    continue;
                }
                UpdateTransforms(inst, i);
                if (!Hidden)
                {
                    // todo: hide attached effects
                    GetDrawItems(inst, i);
                }
            }
            if (_scene.ShowCollision && (_scene.ColEntDisplay == EntityType.All || _scene.ColEntDisplay == Type))
            {
                GetCollisionDrawInfo();
            }
        }

        protected virtual void GetCollisionDrawInfo()
        {
            for (int i = 0; i < 2; i++)
            {
                EntityCollision? entCol = EntityCollision[i];
                if (entCol?.Collision != null && entCol.Collision.Active)
                {
                    entCol.Collision.Info.GetDrawInfo(entCol.DrawPoints, Type, _scene);
                }
            }
        }

        protected virtual Vector3 GetEmission(ModelInstance inst, Material material, int index)
        {
            return Vector3.Zero;
        }

        protected virtual Matrix4 GetTexcoordMatrix(ModelInstance inst, Material material, int materialId, Node node, int recolor = -1)
        {
            Model model = inst.Model;
            Matrix4 texcoordMatrix = Matrix4.Identity;
            TexcoordAnimationGroup? group = inst.AnimInfo.Texcoord.Group;
            TexcoordAnimation? animation = null;
            if (group != null && group.Animations.TryGetValue(material.Name, out TexcoordAnimation result))
            {
                animation = result;
            }
            if (group != null && animation != null && (!inst.Model.FirstHunt || material.TexgenMode != TexgenMode.None))
            {
                // MPH overwrites a material's None texgen with Texcoord when parsing a texcoord animation; FH does not
                texcoordMatrix = model.AnimateTexcoords(group, animation.Value, inst.AnimInfo.TexcoordFrame);
            }
            if (material.TexgenMode != TexgenMode.None)
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
                    Texture texture = model.Recolors[recolor == -1 ? Recolor : recolor].Textures[material.TextureId];
                    // product should start with the upper 3x3 of the node animation result,
                    // texgenMatrix should be multiplied with the view matrix if lighting is enabled,
                    // and the result should be transposed.
                    // these steps are done in the shader so the view matrix can be updated when frame advance is on.
                    // strictly speaking, the use_light check in the shader is not the same as what the game does,
                    // since the game checks if *any* material in the model uses lighting, but the result is the same.
                    Matrix4 texgenMatrix = Matrix4.Identity;
                    // in-game, there's only one uniform scale factor for models
                    if (model.Scale.X != 1 || model.Scale.Y != 1 || model.Scale.Z != 1)
                    {
                        texgenMatrix = Matrix4.CreateScale(model.Scale);
                    }
                    Matrix4 product = texgenMatrix;
                    product.M12 *= -1;
                    product.M13 *= -1;
                    product.M22 *= -1;
                    product.M23 *= -1;
                    product.M32 *= -1;
                    product.M33 *= -1;
                    product *= materialMatrix;
                    product *= texcoordMatrix;
                    product *= 1.0f / (texture.Width / 2);
                    texcoordMatrix = new Matrix4(
                        product.Row0 * 16.0f,
                        product.Row1 * 16.0f,
                        product.Row2 * 16.0f,
                        product.Row3
                    );
                }
            }
            return texcoordMatrix;
        }

        protected void SetTransform(Vector3Fx facing, Vector3Fx up, Vector3Fx position)
        {
            SetTransform(facing.ToFloatVector(), up.ToFloatVector(), position.ToFloatVector());
        }

        protected void SetTransform(Vector3 facing, Vector3 up, Vector3 position)
        {
            Matrix4 transform = GetTransformMatrix(facing, up);
            //transform.ExtractRotation().ToEulerAngles(out Vector3 rotation);
            //Rotation = rotation;
            //Position = position;
            transform.Row3.Xyz = position;
            Transform = transform;
        }

        public static Matrix4 GetTransformMatrix(Vector3 facing, Vector3 up)
        {
            Vector3 right = Vector3.Cross(up, facing).Normalized();
            up = Vector3.Cross(facing, right);
            Matrix4 transform = default;
            transform.M11 = right.X;
            transform.M12 = right.Y;
            transform.M13 = right.Z;
            transform.M14 = 0;
            transform.M21 = up.X;
            transform.M22 = up.Y;
            transform.M23 = up.Z;
            transform.M24 = 0;
            transform.M31 = facing.X;
            transform.M32 = facing.Y;
            transform.M33 = facing.Z;
            transform.M34 = 0;
            transform.M41 = 0;
            transform.M42 = 0;
            transform.M43 = 0;
            transform.M44 = 1;
            return transform;
        }

        public static Matrix4 GetTransformMatrix(Vector3 facing, Vector3 up, Vector3 position)
        {
            Vector3 right = Vector3.Cross(up, facing).Normalized();
            up = Vector3.Cross(facing, right);
            Matrix4 transform = default;
            transform.M11 = right.X;
            transform.M12 = right.Y;
            transform.M13 = right.Z;
            transform.M14 = 0;
            transform.M21 = up.X;
            transform.M22 = up.Y;
            transform.M23 = up.Z;
            transform.M24 = 0;
            transform.M31 = facing.X;
            transform.M32 = facing.Y;
            transform.M33 = facing.Z;
            transform.M34 = 0;
            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;
            transform.M44 = 1;
            return transform;
        }

        protected void AddVolumeItem(CollisionVolume volume, Vector3 color)
        {
            if (!Selection.CheckVolume(this))
            {
                return;
            }
            Vector3[] verts = Array.Empty<Vector3>();
            if (volume.Type == VolumeType.Box)
            {
                verts = ArrayPool<Vector3>.Shared.Rent(8);
                Vector3 point0 = volume.BoxPosition;
                Vector3 sideX = volume.BoxVector1 * volume.BoxDot1;
                Vector3 sideY = volume.BoxVector2 * volume.BoxDot2;
                Vector3 sideZ = volume.BoxVector3 * volume.BoxDot3;
                verts[0] = point0;
                verts[1] = point0 + sideZ;
                verts[2] = point0 + sideX;
                verts[3] = point0 + sideX + sideZ;
                verts[4] = point0 + sideY;
                verts[5] = point0 + sideY + sideZ;
                verts[6] = point0 + sideX + sideY;
                verts[7] = point0 + sideX + sideY + sideZ;
            }
            else if (volume.Type == VolumeType.Cylinder)
            {
                verts = ArrayPool<Vector3>.Shared.Rent(34);
                Vector3 vector = volume.CylinderVector.Normalized();
                float radius = volume.CylinderRadius;
                Matrix3 rotation = Matrix.RotateAlign(Vector3.UnitY, vector);
                Vector3 start;
                Vector3 end;
                // cylinder volumes are always axis-aligned, so we can use this hack to avoid normal issues
                if (vector == Vector3.UnitX || vector == Vector3.UnitY || vector == Vector3.UnitZ)
                {
                    start = volume.CylinderPosition;
                    end = volume.CylinderPosition + vector * volume.CylinderDot;
                }
                else
                {
                    start = volume.CylinderPosition + vector * volume.CylinderDot;
                    end = volume.CylinderPosition;
                }
                for (int i = 0; i < 16; i++)
                {
                    verts[i] = GetDiscVertices(radius, i) * rotation + start;
                }
                for (int i = 0; i < 16; i++)
                {
                    verts[i + 16] = GetDiscVertices(radius, i) * rotation + end;
                }
                verts[32] = start;
                verts[33] = end;
            }
            else if (volume.Type == VolumeType.Sphere)
            {
                int stackCount = Scene.DisplaySphereStacks;
                int sectorCount = Scene.DisplaySphereSectors;
                verts = ArrayPool<Vector3>.Shared.Rent(stackCount * sectorCount);
                float radius = volume.SphereRadius;
                float sectorStep = 2 * MathF.PI / sectorCount;
                float stackStep = MathF.PI / stackCount;
                float sectorAngle, stackAngle, x, y, z, xy;
                for (int i = 0; i <= stackCount; i++)
                {
                    stackAngle = MathF.PI / 2 - i * stackStep;
                    xy = radius * MathF.Cos(stackAngle);
                    z = radius * MathF.Sin(stackAngle);
                    for (int j = 0; j <= sectorCount; j++)
                    {
                        sectorAngle = j * sectorStep;
                        x = xy * MathF.Cos(sectorAngle);
                        y = xy * MathF.Sin(sectorAngle);
                        verts[i * (sectorCount + 1) + j] = new Vector3(x, z, y) + volume.SpherePosition;
                    }
                }
            }
            CullingMode cullingMode = volume.TestPoint(_scene.CameraPosition) ? CullingMode.Front : CullingMode.Back;
            _scene.AddRenderItem(cullingMode, _scene.GetNextPolygonId(), new Vector4(color, 0.5f), (RenderItemType)(volume.Type + 1), verts);
        }

        private Vector3 GetDiscVertices(float radius, int index)
        {
            return new Vector3(
                radius * MathF.Cos(2f * MathF.PI * index / 16f),
                0.0f,
                radius * MathF.Sin(2f * MathF.PI * index / 16f)
            );
        }

        public virtual void GetDisplayVolumes()
        {
        }

        public virtual void SetActive(bool active)
        {
            Active = active;
        }

        // todo: item and enemy spawners
        public virtual EntityBase? GetParent()
        {
            return null;
        }

        public virtual EntityBase? GetChild()
        {
            return null;
        }

        public virtual void HandleMessage(MessageInfo info)
        {
        }

        public virtual void CheckContactDamage(ref DamageResult result)
        {
        }

        public virtual void CheckBeamReflection(ref bool result)
        {
        }

        protected bool IsVisible()
        {
            // sktodo: this
            return true;
        }
    }

    public class ModelEntity : EntityBase
    {
        public ModelEntity(ModelInstance model, Scene scene, int recolor = 0) : base(EntityType.Model, scene)
        {
            Recolor = recolor;
            _models.Add(model);
            model.SetAnimation(0);
        }
    }
}
