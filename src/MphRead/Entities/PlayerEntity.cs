using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerEntity : VisibleEntityBase
    {
        public Hunter Hunter { get; }
        private readonly NewModel _bipedModel = null!;
        private readonly NewModel _altModel = null!;
        private readonly NewModel _gunModel = null!;
        private readonly NewModel _dblDmgModel;
        private int _dblDmgBindingId;
        // todo: does this affect collision and stuff?
        private readonly Matrix4 _scaleMtx;

        // todo: main player, player slots, etc.
        private readonly bool _mainPlayer = false;
        private bool _altForm = false;
        private bool _doubleDamage = false;

        public PlayerEntity(Hunter hunter, int recolor = 0, Vector3? position = null) : base(NewEntityType.Player)
        {
            Hunter = hunter;
            if (position.HasValue)
            {
                Position = position.Value;
            }
            Recolor = recolor;
            // todo: lod1
            IReadOnlyList<string> models = Metadata.HunterModels[Hunter];
            Debug.Assert(models.Count == 3);
            for (int i = 0; i < models.Count; i++)
            {
                NewModel model = Read.GetNewModel(models[i]);
                _models.Add(model);
                if (i == 0)
                {
                    _bipedModel = model;
                }
                else if (i == 1)
                {
                    _altModel = model;
                }
                else if (i == 2)
                {
                    _gunModel = model;
                }
            }
            _dblDmgModel = Read.GetNewModel("doubleDamage_img");
            _scaleMtx = Matrix4.CreateScale(Metadata.HunterScales[Hunter]);
            // temporary
            _bipedModel.Animations.NodeGroupId = 4;
            _doubleDamage = true;
        }

        public override void Init(NewScene scene)
        {
            base.Init(scene);
            _dblDmgBindingId = scene.BindGetTexture(_dblDmgModel, 0, 0, 0);
        }

        protected override bool GetModelActive(NewModel model, int index)
        {
            return (_altForm && model == _altModel) || (_mainPlayer && model == _gunModel) || (!_mainPlayer && model == _bipedModel);
        }

        protected override Matrix4 GetModelTransform(NewModel model, int index)
        {
            Matrix4 transform = base.GetModelTransform(model, index);
            if (model == _bipedModel)
            {
                transform = _scaleMtx * transform;
            }
            return transform;
        }

        protected override int? GetBindingOverride(NewModel model, Material material, int index)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(model == _gunModel && index == 0)) && material.Lighting > 0)
            {
                return _dblDmgBindingId;
            }
            return base.GetBindingOverride(model, material, index);
        }

        protected override Vector3 GetEmission(NewModel model, Material material, int index)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(model == _gunModel && index == 0)) && material.Lighting > 0)
            {
                return Metadata.EmissionGray;
            }
            return base.GetEmission(model, material, index);
        }

        protected override Matrix4 GetTexcoordMatrix(NewModel model, Material material, int materialId, Node node, NewScene scene)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(model == _gunModel && materialId == 0))
                && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
            {
                Texture texture = _dblDmgModel.Recolors[0].Textures[0];
                Matrix4 product = node.Animation.Keep3x3();
                Matrix4 texgenMatrix = Matrix4.Identity;
                // in-game, there's only one uniform scale factor for models
                if (model.Scale.X != 1 || model.Scale.Y != 1 || model.Scale.Z != 1)
                {
                    texgenMatrix = Matrix4.CreateScale(model.Scale) * texgenMatrix;
                }
                // in-game, bit 0 is set on creation if any materials have lighting enabled
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
                long frame = scene.FrameCount / 2;
                float rotZ = ((int)(16 * ((781874935307L * (ulong)(53248 * frame) >> 32) + 2048)) >> 20) * (360 / 4096f);
                float rotY = ((int)(16 * ((781874935307L * (ulong)(26624 * frame) + 0x80000000000) >> 32)) >> 20) * (360 / 4096f);
                var rot = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotZ));
                rot *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotY));
                product = rot * product;
                product *= (1.0f / (texture.Width / 2));
                product = new Matrix4(
                    product.Row0 * 16.0f,
                    product.Row1 * 16.0f,
                    product.Row2 * 16.0f,
                    product.Row3
                );
                product.Transpose();
                return product;
            }
            return base.GetTexcoordMatrix(model, material, materialId, node, scene);
        }
    }
}
