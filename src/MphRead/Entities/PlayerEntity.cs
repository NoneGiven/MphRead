using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerEntity : VisibleEntityBase
    {
        public Hunter Hunter { get; }
        public Team Team { get; set; }
        private readonly NewModel _bipedModel = null!;
        private readonly NewModel _altModel = null!;
        private readonly NewModel _gunModel = null!;
        private readonly NewModel _dblDmgModel;
        private int _dblDmgBindingId;
        // todo: does this affect collision and stuff?
        private readonly Matrix4 _scaleMtx;

        private Vector3 _light1Vector;
        private Vector3 _light1Color;
        private Vector3 _light2Vector;
        private Vector3 _light2Color;

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
        }

        public override void Init(NewScene scene)
        {
            base.Init(scene);
            _dblDmgBindingId = scene.BindGetTexture(_dblDmgModel, 0, 0, 0);
            _light1Vector = scene.Light1Vector;
            _light1Color = scene.Light1Color;
            _light2Vector = scene.Light2Vector;
            _light2Color = scene.Light2Color;
        }

        public override void Process(NewScene scene)
        {
            UpdateLightSources(scene);
            base.Process(scene);
        }

        private const float _colorStep = 8 / 255f;

        private void UpdateLightSources(NewScene scene)
        {
            static float UpdateChannel(float current, float source, float frames)
            {
                float diff = source - current;
                if (MathF.Abs(diff) < _colorStep)
                {
                    return source;
                }
                int factor;
                if (current > source)
                {
                    factor = (int)MathF.Truncate((diff + _colorStep) / (8 * _colorStep));
                    if (factor <= -1)
                    {
                        return current + (factor - 1) * _colorStep * frames;
                    }
                    return current - _colorStep * frames;
                }
                factor = (int)MathF.Truncate(diff / (8 * _colorStep));
                if (factor >= 1)
                {
                    return current + factor * _colorStep * frames;
                }
                return current + _colorStep * frames;
            }
            bool hasLight1 = false;
            bool hasLight2 = false;
            Vector3 light1Color = _light1Color;
            Vector3 light1Vector = _light1Vector;
            Vector3 light2Color = _light2Color;
            Vector3 light2Vector = _light2Vector;
            float frames = scene.FrameTime * 30;
            for (int i = 0; i < scene.Entities.Count; i++)
            {
                EntityBase entity = scene.Entities[i];
                if (entity.Type != NewEntityType.LightSource)
                {
                    continue;
                }
                var lightSource = (LightSourceEntity)entity;
                // todo: position to use differs depending on player form
                if (lightSource.Volume.TestPoint(Position))
                {
                    if (lightSource.Light1Enabled)
                    {
                        hasLight1 = true;
                        light1Vector.X += (lightSource.Light1Vector.X - light1Vector.X) / 8f * frames;
                        light1Vector.Y += (lightSource.Light1Vector.Y - light1Vector.Y) / 8f * frames;
                        light1Vector.Z += (lightSource.Light1Vector.Z - light1Vector.Z) / 8f * frames;
                        light1Color.X = UpdateChannel(light1Color.X, lightSource.Light1Color.X, frames);
                        light1Color.Y = UpdateChannel(light1Color.Y, lightSource.Light1Color.Y, frames);
                        light1Color.Z = UpdateChannel(light1Color.Z, lightSource.Light1Color.Z, frames);
                    }
                    if (lightSource.Light2Enabled)
                    {
                        hasLight2 = true;
                        light2Vector.X += (lightSource.Light2Vector.X - light2Vector.X) / 8f * frames;
                        light2Vector.Y += (lightSource.Light2Vector.Y - light2Vector.Y) / 8f * frames;
                        light2Vector.Z += (lightSource.Light2Vector.Z - light2Vector.Z) / 8f * frames;
                        light2Color.X = UpdateChannel(light2Color.X, lightSource.Light2Color.X, frames);
                        light2Color.Y = UpdateChannel(light2Color.Y, lightSource.Light2Color.Y, frames);
                        light2Color.Z = UpdateChannel(light2Color.Z, lightSource.Light2Color.Z, frames);
                    }
                }
            }
            if (!hasLight1)
            {
                light1Vector.X += (_light1Vector.X - light1Vector.X) / 8f * frames;
                light1Vector.Y += (_light1Vector.Y - light1Vector.Y) / 8f * frames;
                light1Vector.Z += (_light1Vector.Z - light1Vector.Z) / 8f * frames;
                light1Color.X = UpdateChannel(light1Color.X, _light1Color.X, frames);
                light1Color.Y = UpdateChannel(light1Color.Y, _light1Color.Y, frames);
                light1Color.Z = UpdateChannel(light1Color.Z, _light1Color.Z, frames);
            }
            if (!hasLight2)
            {
                light2Vector.X += (_light2Vector.X - light2Vector.X) / 8f * frames;
                light2Vector.Y += (_light2Vector.Y - light2Vector.Y) / 8f * frames;
                light2Vector.Z += (_light2Vector.Z - light2Vector.Z) / 8f * frames;
                light2Color.X = UpdateChannel(light2Color.X, _light2Color.X, frames);
                light2Color.Y = UpdateChannel(light2Color.Y, _light2Color.Y, frames);
                light2Color.Z = UpdateChannel(light2Color.Z, _light2Color.Z, frames);
            }
            _light1Color = light1Color;
            _light1Vector = light1Vector.Normalized();
            _light2Color = light2Color;
            _light2Vector = light2Vector.Normalized();
        }

        protected override LightInfo GetLightInfo(NewModel model, NewScene scene)
        {
            return new LightInfo(_light1Vector, _light1Color, _light2Vector, _light2Color);
        }

        protected override bool GetModelActive(NewModel model, int index)
        {
            if (_altForm)
            {
                return model == _altModel;
            }
            if (_mainPlayer)
            {
                return model == _gunModel;
            }
            return model == _bipedModel;
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
            if (Team == Team.Orange)
            {
                return Metadata.EmissionOrange;
            }
            if (Team == Team.Green)
            {
                return Metadata.EmissionGreen;
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
