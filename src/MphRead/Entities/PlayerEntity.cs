using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class PlayerEntity : EntityBase
    {
        public Hunter Hunter { get; private set; }
        public Team Team { get; set; }
        private ModelInstance _bipedModel = null!;
        private ModelInstance _altModel = null!;
        private ModelInstance _gunModel = null!;
        private readonly ModelInstance _dblDmgModel;
        private readonly ModelInstance _altIceModel;
        private ModelInstance _bipedIceModel = null!;
        private int _dblDmgBindingId;
        // todo: does this affect collision and stuff?
        private Matrix4 _scaleMtx;

        private Vector3 _light1Vector;
        private Vector3 _light1Color;
        private Vector3 _light2Vector;
        private Vector3 _light2Color;

        // todo: player position in biped is 0.5 above the ground, so the game only adds 0.5 -- handle this for biped and alt
        public override Vector3 TargetPosition => _altForm ? Position : Position.AddY(1f);

        // todo: main player, player slots, etc.
        private readonly bool _mainPlayer = false;
        private bool _altForm = false;
        private bool _doubleDamage = false;
        private bool _frozen = false;
        private bool _dead = false;
        private int _respawnTimer = 0;
        private const int _respawnCooldown = 180;

        private readonly BeamProjectileEntity[] _beams = SceneSetup.CreateBeamList(16); // in-game: 5
        public BombEntity[] Bombs { get; } = new BombEntity[3];
        public int BombMax { get; private set; }
        public int BombCount { get; set; }

        private bool _respawning = false;
        // todo: remove testing code
        public bool Shoot { get; set; }
        public bool Bomb { get; set; }

        public static int PlayerCount { get; private set; }
        public int Slot { get; private set; }
        public const int MaxPlayers = 4;
        public static readonly int MainPlayer = 0; // todo
        private const int _mbTrailSegments = 9;
        private static readonly Matrix4[,] _mbTrailMatrices = new Matrix4[MaxPlayers, _mbTrailSegments];
        private static readonly int[,] _mbTrailAlphas = new int[MaxPlayers, _mbTrailSegments];
        private static readonly int[] _mbTrailIndices = new int[MaxPlayers];
        private ModelInstance? _trailModel = null;
        private int _bindingId1 = 0;
        private int _bindingId2 = 0;

        public Vector3 PrevPosition1 { get; private set; }
        public Vector3 PrevPosition2 { get; private set; }

        public static readonly PlayerEntity[] Players = new PlayerEntity[4]
        {
            new PlayerEntity(), new PlayerEntity(), new PlayerEntity(), new PlayerEntity()
        };

        private PlayerEntity() : base(EntityType.Player)
        {
            _dblDmgModel = Read.GetModelInstance("doubleDamage_img");
            _altIceModel = Read.GetModelInstance("alt_ice");
            _models.Add(_altIceModel);
        }

        public static PlayerEntity? Spawn(Hunter hunter, int recolor = 0, Vector3? position = null, Vector3? facing = null, bool respawn = false)
        {
            int slot = PlayerCount++;
            if (slot >= MaxPlayers)
            {
                return null;
            }
            PlayerEntity player = Players[slot];
            player.Slot = slot;
            player.Setup(hunter, recolor, position, facing, respawn);
            return player;
        }

        private void Setup(Hunter hunter, int recolor, Vector3? position, Vector3? facing, bool respawn)
        {
            Hunter = hunter;
            BombMax = 0;
            if (Hunter == Hunter.Samus || Hunter == Hunter.Sylux)
            {
                BombMax = 3;
            }
            else if (Hunter == Hunter.Kanden)
            {
                BombMax = 1;
            }
            // todo: player transform does something weird with the spine rotation (instead of this negation)
            SetTransform(facing.HasValue ? -facing.Value : Vector3.UnitZ, Vector3.UnitY, position ?? Vector3.Zero);
            PrevPosition2 = PrevPosition2 = Position;
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
            _bipedIceModel = Read.GetModelInstance(Hunter == Hunter.Noxus || Hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            _models.Add(_bipedIceModel);
            // todo: scale is applied so as not to mess up the min collision height/y offset
            _scaleMtx = Matrix4.CreateScale(Metadata.HunterScales[Hunter]);
            _respawning = respawn;
            // temporary
            _bipedModel.SetNodeAnim(8);
            if (Hunter == Hunter.Weavel)
            {
                _bipedModel.SetMaterialAnim(-1);
            }
        }

        private CollisionVolume _sphere; // bounding sphere for capsule check
        private CollisionVolume _cylinder; // collision cylinder/capsule

        public override void Initialize(Scene scene)
        {
            _sphere = new CollisionVolume(new Vector3(0, 0.8f, 0), 0.8f);
            _cylinder = new CollisionVolume(Vector3.UnitY, Vector3.Zero, 0.5f + 0.45f, 1.6f);
            base.Initialize(scene);
            _dblDmgBindingId = scene.BindGetTexture(_dblDmgModel.Model, 0, 0, 0);
            _light1Vector = scene.Light1Vector;
            _light1Color = scene.Light1Color;
            _light2Vector = scene.Light2Vector;
            _light2Color = scene.Light2Color;
            _trailModel = Read.GetModelInstance("trail");
            Material material = _trailModel.Model.Materials[0];
            _bindingId1 = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            material = _trailModel.Model.Materials[1];
            _bindingId2 = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            ResetMorphBallTrail();
            if (_respawning)
            {
                Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY);
                transform.Row3.Xyz = _altForm ? Position : Position.AddY(0.5f);
                scene.SpawnEffect(scene.Multiplayer ? 33 : 31, transform);
            }
        }

        public override bool Process(Scene scene)
        {
            PrevPosition2 = PrevPosition1;
            PrevPosition1 = Position;
            if (Hunter == Hunter.Spire)
            {
                Rotation = Rotation.AddY(0.01f);
            }
            UpdateLightSources(scene);
            if (_respawnTimer > 0)
            {
                _respawnTimer--;
            }
            UpdateModels();
            if (Hunter == Hunter.Samus && scene.FrameCount % 2 == 0)
            {
                UpdateMorphBallTrail();
            }
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
            if (Bomb)
            {
                SpawnBomb(scene);
                Bomb = false;
            }
            return base.Process(scene);
        }

        private void TestSpawnBeam(int type, Scene scene)
        {
            Vector3 gunPos = (new Vector4(0.37203538f, 1.2936982f, -1.0930165f, 1) * Transform).Xyz;
            var spawnTransform = new Matrix3(Transform.Row0.Xyz, Transform.Row1.Xyz, Transform.Row2.Xyz);
            Vector3 direction = (-Vector3.UnitZ * spawnTransform).Normalized();
            if (type == -1)
            {
                WeaponInfo weapon = Weapons.Weapons1P[17];
                bool charged = true;
                BeamProjectileEntity.Spawn(this, new EquipInfo(weapon, _beams) { ChargeLevel = charged ? weapon.FullCharge : (ushort)0 },
                    gunPos, direction, BeamSpawnFlags.NoMuzzle, scene);
            }
            else if (type == 0)
            {
                WeaponInfo weapon = Weapons.WeaponsMP[14];
                BeamProjectileEntity.Spawn(this, new EquipInfo(weapon, _beams) { ChargeLevel = weapon.FullCharge },
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

        private void SpawnBomb(Scene scene)
        {
            if (BombCount >= BombMax)
            {
                if (Hunter == Hunter.Sylux)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Bombs[i].Countdown = 0;
                    }
                }
                return;
            }
            // todo: bomb spawn position/transform
            Matrix4 transform;
            if (Hunter == Hunter.Kanden)
            {
                transform = Transform.ClearScale();
                transform.Row3.Xyz = Position.AddY(0.25f);
            }
            else
            {
                transform = Matrix4.CreateTranslation(Position.AddY(Fixed.ToFloat(1000)));
            }
            BombEntity.Spawn(this, transform, scene);
            // todo: bomb cooldown/refill stuff
        }

        public override void Destroy(Scene scene)
        {
            _trailModel = null;
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

        // todo: player Y position is 0.5 when standing on ground at 0.0
        public override void GetDrawInfo(Scene scene)
        {
            DrawShadow(scene);
            if (_dead)
            {
                Alpha -= 2 / 31f;
                if (Alpha < 2 / 31f)
                {
                    Alpha = 0;
                }
                if (!_altForm)
                {
                    DrawDeathParticles(scene);
                }
            }
            else
            {
                // todo: cloaking and other stuff
                Alpha = 1;
                base.GetDrawInfo(scene);
                if (_altForm && Hunter == Hunter.Samus)
                {
                    DrawMorphBallTrail(scene);
                }
                //AddVolumeItem(_cylinder, Vector3.UnitX, scene);
                //AddVolumeItem(_sphere, Vector3.UnitZ, scene);
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
                if (node.ChildIndex != -1)
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
                if (node.NextIndex != -1)
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
                light1Vector.X += (scene.Light1Vector.X - light1Vector.X) / 8f * frames;
                light1Vector.Y += (scene.Light1Vector.Y - light1Vector.Y) / 8f * frames;
                light1Vector.Z += (scene.Light1Vector.Z - light1Vector.Z) / 8f * frames;
                light1Color.X = UpdateChannel(light1Color.X, scene.Light1Color.X, frames);
                light1Color.Y = UpdateChannel(light1Color.Y, scene.Light1Color.Y, frames);
                light1Color.Z = UpdateChannel(light1Color.Z, scene.Light1Color.Z, frames);
            }
            if (!hasLight2)
            {
                light2Vector.X += (scene.Light2Vector.X - light2Vector.X) / 8f * frames;
                light2Vector.Y += (scene.Light2Vector.Y - light2Vector.Y) / 8f * frames;
                light2Vector.Z += (scene.Light2Vector.Z - light2Vector.Z) / 8f * frames;
                light2Color.X = UpdateChannel(light2Color.X, scene.Light2Color.X, frames);
                light2Color.Y = UpdateChannel(light2Color.Y, scene.Light2Color.Y, frames);
                light2Color.Z = UpdateChannel(light2Color.Z, scene.Light2Color.Z, frames);
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

        private void ResetMorphBallTrail()
        {
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                _mbTrailAlphas[Slot, i] = 0;
            }
            _mbTrailIndices[Slot] = 0;
        }

        private void UpdateMorphBallTrail()
        {
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                int alpha = _mbTrailAlphas[Slot, i] - 3;
                if (alpha < 0)
                {
                    alpha = 0;
                }
                _mbTrailAlphas[Slot, i] = alpha;
            }
            if (_altForm)
            {
                // todo: use actual speed value and remove previous positions (and the fx32 value should be 1269)
                Vector3 row0 = Transform.Row0.Xyz;
                float hSpeed = (PrevPosition2.WithY(0) - Position.WithY(0)).Length;
                if (hSpeed >= Fixed.ToFloat(600) && Vector3.Dot(Vector3.UnitY, row0) < 0.5f)
                {
                    Vector3 cross = Vector3.Cross(row0, Vector3.UnitY).Normalized();
                    var cross2 = Vector3.Cross(cross, row0);
                    int index = _mbTrailIndices[Slot];
                    _mbTrailAlphas[Slot, index] = 25;
                    _mbTrailMatrices[Slot, index] = new Matrix4(
                        row0.X, row0.Y, row0.Z, 0,
                        cross2.X, cross2.Y, cross2.Z, 0,
                        cross.X, cross.Y, cross.Z, 0,
                        Position.X, Position.Y, Position.Z, 1
                    );
                    _mbTrailIndices[Slot] = (index + 1) % _mbTrailSegments;
                }
            }
        }

        private void DrawShadow(Scene scene)
        {
            Debug.Assert(_trailModel != null);
            Material material = _trailModel.Model.Materials[1];
            // todo: use collision sphere
            Vector3 point1 = Position.AddY(0.5f);
            Vector3 point2 = point1.AddY(-10);
            // todo: don't draw if main player in first person
            CollisionResult colRes = default;
            if (CollisionDetection.CheckBetweenPoints(point1, point2, TestFlags.None, scene, ref colRes))
            {
                float height = point1.Y - colRes.Position.Y;
                if (height < 10)
                {
                    float pct = 1 - height / 10;
                    float alpha = Alpha * pct;
                    if (_dead)
                    {
                        // sktodo: extra alpha factor
                    }
                    if (alpha > 0)
                    {
                        Vector3 row1 = Vector3.Cross(colRes.Plane.Xyz, Vector3.UnitZ).Normalized();
                        Vector3 row2 = colRes.Plane.Xyz;
                        var row3 = Vector3.Cross(row1, colRes.Plane.Xyz);
                        row1 *= pct;
                        row2 *= pct;
                        row3 *= pct;
                        float factor = Fixed.ToFloat(100);
                        var row4 = new Vector3(
                            colRes.Position.X + colRes.Plane.X * factor,
                            colRes.Position.Y + colRes.Plane.Y * factor,
                            colRes.Position.Z + colRes.Plane.Z * factor
                        );
                        var transform = new Matrix4(
                            row1.X, row1.Y, row1.Z, 0,
                            row2.X, row2.Y, row2.Z, 0,
                            row3.X, row3.Y, row3.Z, 0,
                            row4.X, row4.Y, row4.Z, 1
                        );
                        Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8);
                        uvsAndVerts[0] = new Vector3(0, 0, 0);
                        uvsAndVerts[1] = new Vector3(-0.75f, 0.03125f, -0.75f);
                        uvsAndVerts[2] = new Vector3(0, 1, 0);
                        uvsAndVerts[3] = new Vector3(-0.75f, 0.03125f, 0.75f);
                        uvsAndVerts[4] = new Vector3(1, 1, 0);
                        uvsAndVerts[5] = new Vector3(0.75f, 0.03125f, 0.75f);
                        uvsAndVerts[6] = new Vector3(1, 0, 0);
                        uvsAndVerts[7] = new Vector3(0.75f, 0.03125f, -0.75f);
                        int polygonId = scene.GetNextPolygonId();
                        var color = new Vector3(0, 0, 0);
                        scene.AddRenderItem(RenderItemType.Particle, alpha, polygonId, color, material.XRepeat, material.YRepeat,
                            material.ScaleS, material.ScaleT, transform, uvsAndVerts, _bindingId2);
                    }
                }
            }
        }

        private void DrawMorphBallTrail(Scene scene)
        {
            Debug.Assert(_trailModel != null);
            Material material = _trailModel.Model.Materials[0];
            Debug.Assert(_trailModel.Model.Recolors[0].Textures[material.TextureId].Width == 32);
            float[] matrixStack = ArrayPool<float>.Shared.Rent(16 * _mbTrailSegments);
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                Matrix4 matrix = _mbTrailMatrices[Slot, i];
                matrixStack[i * 16] = matrix.Row0.X;
                matrixStack[i * 16 + 1] = matrix.Row0.Y;
                matrixStack[i * 16 + 2] = matrix.Row0.Z;
                matrixStack[i * 16 + 3] = matrix.Row0.W;
                matrixStack[i * 16 + 4] = matrix.Row1.X;
                matrixStack[i * 16 + 5] = matrix.Row1.Y;
                matrixStack[i * 16 + 6] = matrix.Row1.Z;
                matrixStack[i * 16 + 7] = matrix.Row1.W;
                matrixStack[i * 16 + 8] = matrix.Row2.X;
                matrixStack[i * 16 + 9] = matrix.Row2.Y;
                matrixStack[i * 16 + 10] = matrix.Row2.Z;
                matrixStack[i * 16 + 11] = matrix.Row2.W;
                matrixStack[i * 16 + 12] = matrix.Row3.X;
                matrixStack[i * 16 + 13] = matrix.Row3.Y;
                matrixStack[i * 16 + 14] = matrix.Row3.Z;
                matrixStack[i * 16 + 15] = matrix.Row3.W;
            }
            int count = 0;
            int index = _mbTrailIndices[Slot];
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8 * _mbTrailSegments);
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                // going backwards with wrap-around
                int mtxId1 = index - 1 - i + (index - 1 - i < 0 ? 9 : 0);
                int mtxId2 = mtxId1 - 1 + (mtxId1 - 1 < 0 ? 9 : 0);
                int alpha1 = _mbTrailAlphas[Slot, mtxId1];
                int alpha2 = _mbTrailAlphas[Slot, mtxId2];
                if (alpha1 > 0 && alpha2 > 0)
                {
                    float uvS1 = (31 - alpha1) / 32f;
                    float uvS2 = (31 - alpha2) / 32f;
                    uvsAndVerts[i * 8] = new Vector3(uvS1, 0, mtxId1);
                    uvsAndVerts[i * 8 + 1] = new Vector3(0, 0.375f, 0);
                    uvsAndVerts[i * 8 + 2] = new Vector3(uvS1, 1, mtxId1);
                    uvsAndVerts[i * 8 + 3] = new Vector3(0, -0.375f, 0);
                    uvsAndVerts[i * 8 + 4] = new Vector3(uvS2, 1, mtxId2);
                    uvsAndVerts[i * 8 + 5] = new Vector3(0, -0.375f, 0);
                    uvsAndVerts[i * 8 + 6] = new Vector3(uvS2, 0, mtxId2);
                    uvsAndVerts[i * 8 + 7] = new Vector3(0, 0.375f, 0);
                    count++;
                }
            }
            if (count > 0)
            {
                var color = new Vector3(1, 27 / 31f, 11 / 31f);
                scene.AddRenderItem(RenderItemType.TrailStack, scene.GetNextPolygonId(), color, material.XRepeat, material.YRepeat,
                    material.ScaleS, material.ScaleT, _mbTrailSegments, matrixStack, uvsAndVerts, count, _bindingId1);
            }
            ArrayPool<float>.Shared.Return(matrixStack);
        }
    }
}
