using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerEntity : EntityBase
    {
        public Hunter Hunter { get; }
        public Team Team { get; set; }
        private readonly ModelInstance _bipedModel = null!;
        private readonly ModelInstance _altModel = null!;
        private readonly ModelInstance _gunModel = null!;
        private readonly ModelInstance _dblDmgModel;
        private readonly ModelInstance _altIceModel;
        private readonly ModelInstance _bipedIceModel;
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
        private bool _frozen = false;
        private bool _dead = false;
        private int _respawnTimer = 0;
        private const int _respawnCooldown = 180;

        // todo: remove testing code
        public bool Shoot { get; set; }

        public PlayerEntity(Hunter hunter, int recolor = 0, Vector3? position = null) : base(EntityType.Player)
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
                ModelInstance inst = Read.GetModelInstance(models[i]);
                _models.Add(inst);
                if (i == 0)
                {
                    _bipedModel = inst;
                }
                else if (i == 1)
                {
                    _altModel = inst;
                }
                else if (i == 2)
                {
                    _gunModel = inst;
                }
            }
            _dblDmgModel = Read.GetModelInstance("doubleDamage_img");
            _altIceModel = Read.GetModelInstance("alt_ice");
            _models.Add(_altIceModel);
            _bipedIceModel = Read.GetModelInstance(Hunter == Hunter.Noxus || Hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            _models.Add(_bipedIceModel);
            _scaleMtx = Matrix4.CreateScale(Metadata.HunterScales[Hunter]);
            // temporary
            _bipedModel.SetNodeAnim(4);
            if (Hunter == Hunter.Weavel)
            {
                _bipedModel.SetMaterialAnim(-1);
            }
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            _dblDmgBindingId = scene.BindGetTexture(_dblDmgModel.Model, 0, 0, 0);
            _light1Vector = scene.Light1Vector;
            _light1Color = scene.Light1Color;
            _light2Vector = scene.Light2Vector;
            _light2Color = scene.Light2Color;
        }

        public override bool Process(Scene scene)
        {
            UpdateLightSources(scene);
            if (_respawnTimer > 0)
            {
                _respawnTimer--;
            }
            UpdateModels();
            if (_frozen)
            {
                // skip incrementing animation frames
                return true;
            }
            if (Shoot)
            {
                TestSpawnBeam(-1, scene);
                Shoot = false;
            }
            return base.Process(scene);
        }

        private void TestSpawnBeam(int type, Scene scene)
        {
            Vector3 gunPos = Position + new Vector3(0.37203538f, 1.2936982f, -1.0930165f);
            //Vector3 direction = new Vector3(0, 2, -5).Normalized();
            Vector3 direction = -Vector3.UnitZ;
            if (type == -1)
            {
                WeaponInfo weapon = Weapons.WeaponsMP[1];
                bool charged = false;
                BeamProjectileEntity.Spawn(this, new EquipInfo(weapon) { ChargeLevel = charged ? weapon.FullCharge : (ushort)0 },
                    gunPos, direction, BeamSpawnFlags.NoMuzzle, scene);
            }
            else if (type == 0)
            {
                WeaponInfo weapon = Weapons.WeaponsMP[14];
                BeamProjectileEntity.Spawn(this, new EquipInfo(weapon) { ChargeLevel = weapon.FullCharge },
                    gunPos, -Vector3.UnitZ, BeamSpawnFlags.NoMuzzle, scene);
            }
            else if (type == 1)
            {
                Matrix4 transform = Matrix4.CreateScale(new Vector3(1, 200, 1)) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-90));
                transform.Row3.Xyz = new Vector3(0.37203538f, 1.2936982f, -1.0930165f);
                var ent = BeamEffectEntity.Create(new BeamEffectEntityData(1, false, transform), scene);
                if (ent != null)
                {
                    scene.AddEntity(ent);
                }
            }
            else if (type == 2)
            {
                Matrix4 transform = Matrix4.Identity;
                transform.Row3.Z = 2f;
                var ent = BeamEffectEntity.Create(new BeamEffectEntityData(2, false, transform), scene);
                if (ent != null)
                {
                    scene.AddEntity(ent);
                }
            }
        }

        private void UpdateModels()
        {
            for (int i = 0; i < _models.Count; i++)
            {
                ModelInstance inst = _models[i];
                _altModel.Active = !_dead && _altForm;
                _altIceModel.Active = _altModel.Active && _frozen;
                _gunModel.Active = !_dead && _mainPlayer && !_altForm;
                _bipedModel.Active = !_dead && !_mainPlayer && !_altForm;
                _bipedIceModel.Active = _bipedModel.Active && _frozen;
            }
        }

        public override void GetDrawInfo(Scene scene)
        {
            if (_dead)
            {
                if (!_altForm)
                {
                    DrawDeathParticles(scene);
                }
            }
            else
            {
                base.GetDrawInfo(scene);
            }
        }

        private void DrawDeathParticles(Scene scene)
        {
            // get current percentage through the first 1/3 of the respawn cooldown
            float timePct = 1 - ((_respawnTimer - (2 / 3f * _respawnCooldown)) / (1 / 3f * _respawnCooldown));
            if (timePct < 0 || timePct > 1)
            {
                return;
            }
            float scale = timePct / 2 + 0.1f;
            // todo: the angle stuff could be removed
            float angle = MathF.Sin(MathHelper.DegreesToRadians(270 - 90 * timePct));
            float sin270 = MathF.Sin(MathHelper.DegreesToRadians(270));
            float sin180 = MathF.Sin(MathHelper.DegreesToRadians(180));
            float offset = (angle - sin270) / (sin180 - sin270);
            for (int j = 1; j < _bipedModel.Model.Nodes.Count; j++)
            {
                Node node = _bipedModel.Model.Nodes[j];
                var nodePos = new Vector3(node.Animation.Row3);
                nodePos.Y += offset;
                if (node.ChildIndex != UInt16.MaxValue)
                {
                    Debug.Assert(node.ChildIndex > 0);
                    var childPos = new Vector3(_bipedModel.Model.Nodes[node.ChildIndex].Animation.Row3);
                    childPos.Y += offset;
                    for (int k = 1; k < 5; k++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + k * (childPos.X - nodePos.X) / 5,
                            nodePos.Y + k * (childPos.Y - nodePos.Y) / 5,
                            nodePos.Z + k * (childPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        scene.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                if (node.NextIndex != UInt16.MaxValue)
                {
                    Debug.Assert(node.NextIndex > 0);
                    var nextPos = new Vector3(_bipedModel.Model.Nodes[node.NextIndex].Animation.Row3);
                    nextPos.Y += offset;
                    for (int k = 1; k < 5; k++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + k * (nextPos.X - nodePos.X) / 5,
                            nodePos.Y + k * (nextPos.Y - nodePos.Y) / 5,
                            nodePos.Z + k * (nextPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        scene.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                nodePos += (nodePos - Position).Normalized() * offset;
                scene.AddSingleParticle(SingleType.Death, nodePos, Vector3.One, 1 - timePct, scale);
            }
        }

        protected override void UpdateTransforms(ModelInstance inst, int index, Scene scene)
        {
            if (inst == _bipedIceModel)
            {
                scene.UpdateMaterials(inst.Model, GetModelRecolor(inst, index));
            }
            else
            {
                base.UpdateTransforms(inst, index, scene);
            }
            if (_frozen && !_altForm && !_mainPlayer && inst == _bipedModel)
            {
                UpdateIceModel();
            }
        }

        private void UpdateIceModel()
        {
            for (int j = 0; j < _bipedIceModel.Model.Nodes.Count; j++)
            {
                _bipedIceModel.Model.Nodes[j].Animation = _bipedModel.Model.Nodes[j].Animation;
            }
            // identity matrices are fine since the ice model doesn't have any billboard nodes
            _bipedIceModel.Model.UpdateMatrixStack(Matrix4.Identity, Matrix4.Identity);
        }

        private const float _colorStep = 8 / 255f;

        private void UpdateLightSources(Scene scene)
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
                if (entity.Type != EntityType.LightSource)
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

        protected override LightInfo GetLightInfo(Scene scene)
        {
            return new LightInfo(_light1Vector, _light1Color, _light2Vector, _light2Color);
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            // todo: alt ice collision radius scale + height offset
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (inst == _bipedModel || inst == _bipedIceModel)
            {
                transform = _scaleMtx * transform;
            }
            return transform;
        }

        protected override int? GetBindingOverride(ModelInstance inst, Material material, int index)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(inst == _gunModel && index == 0)) && material.Lighting > 0)
            {
                return _dblDmgBindingId;
            }
            return base.GetBindingOverride(inst, material, index);
        }

        protected override Vector3 GetEmission(ModelInstance inst, Material material, int index)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(inst == _gunModel && index == 0)) && material.Lighting > 0)
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
            return base.GetEmission(inst, material, index);
        }

        protected override Matrix4 GetTexcoordMatrix(ModelInstance inst, Material material, int materialId, Node node, Scene scene)
        {
            if (_doubleDamage && (Hunter != Hunter.Spire || !(inst == _gunModel && materialId == 0))
                && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
            {
                Texture texture = _dblDmgModel.Model.Recolors[0].Textures[0];
                Matrix4 product = node.Animation.Keep3x3();
                Matrix4 texgenMatrix = Matrix4.Identity;
                // in-game, there's only one uniform scale factor for models
                if (inst.Model.Scale.X != 1 || inst.Model.Scale.Y != 1 || inst.Model.Scale.Z != 1)
                {
                    texgenMatrix = Matrix4.CreateScale(inst.Model.Scale) * texgenMatrix;
                }
                // in-game, bit 0 is set on creation if any materials have lighting enabled
                if (_anyLighting || (inst.Model.Header.Flags & 1) > 0)
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
            return base.GetTexcoordMatrix(inst, material, materialId, node, scene);
        }
    }
}
