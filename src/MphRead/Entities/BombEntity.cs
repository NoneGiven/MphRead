using System;
using System.Buffers;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class BombEntity : EntityBase
    {
        public BombFlags Flags { get; private set; }
        public PlayerEntity? Owner { get; private set; }
        public BombType BombType { get; private set; }
        public int BombIndex { get; private set; }

        public int Countdown { get; set; }

        public EffectEntry? Effect { get; private set; }
        private ModelInstance? _trailModel = null;
        private int _bindingId = 0;

        public BombEntity() : base(EntityType.Bomb)
        {
        }

        public override void Initialize(Scene scene)
        {
            Debug.Assert(Owner != null);
            base.Initialize(scene);
            int effectId = 0;
            if (BombType == BombType.Stinglarva)
            {
                _models.Add(Read.GetModelInstance("KandenAlt_TailBomb"));
                Flags |= BombFlags.HasModel;
                Countdown = 43 * 2; // todo: FPS stuff
            }
            else if (BombType == BombType.Lockjaw)
            {
                if (Recolor == 0)
                {
                    _trailModel = Read.GetModelInstance("arcWelder");
                }
                else
                {
                    _trailModel = Read.GetModelInstance("arcWelder1");
                }
                Countdown = 900 * 2;
                effectId = Metadata.SyluxBombEffects[Recolor];
                if (Owner.BombCount == 2)
                {
                    BombEntity firstBomb = Owner.Bombs[0];
                    Vector3 between = firstBomb.Position - Position;
                    // todo: also check for collision blocker
                    if (between.LengthSquared >= 100)
                    {
                        Countdown = 1;
                        firstBomb.Countdown = 1;
                    }
                }
            }
            else if (BombType == BombType.MorphBall)
            {
                Countdown = 43 * 2;
                effectId = 9; // todo: use 119 if MP w/ 3+ players
            }
            if (effectId != 0)
            {
                Effect = scene.SpawnEffectGetEntry(effectId, Transform);
                for (int i = 0; i < Effect.Elements.Count; i++)
                {
                    EffectElementEntry element = Effect.Elements[i];
                    element.Flags |= EffElemFlags.ElementExtension;
                }
            }
            if (_trailModel != null)
            {
                int recolor = Recolor;
                if (Recolor > 0)
                {
                    recolor--;
                }
                Material material = _trailModel.Model.Materials[0];
                _bindingId = scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, recolor);
            }
        }

        public override bool Process(Scene scene)
        {
            Debug.Assert(Owner != null);
            if (!Flags.HasFlag(BombFlags.Exploded))
            {
                Countdown--;
                if (Countdown <= 0)
                {
                    Flags |= BombFlags.Exploding;
                }
                // todo: collision and damage check stuff
                if (BombType == BombType.Lockjaw && Owner.BombCount == 3)
                {
                    // todo: if there's a target, detonation doesn't happen immediately
                    for (int i = 0; i < 3; i++)
                    {
                        Owner.Bombs[i].Countdown = 1;
                    }
                }
            }
            if (Flags.HasFlag(BombFlags.Exploding))
            {
                if (Flags.HasFlag(BombFlags.Exploded))
                {
                    return false;
                }
                Flags |= BombFlags.Exploded;
                _models.Clear();
                if (Effect != null)
                {
                    scene.UnlinkEffectEntry(Effect);
                }
                if (BombType == BombType.Stinglarva)
                {
                    scene.SpawnEffect(128, Transform);
                }
                else if (BombType == BombType.Lockjaw)
                {
                    scene.SpawnEffect(146, Transform);
                }
                else if (BombType == BombType.MorphBall)
                {
                    scene.SpawnEffect(145, Transform);
                }
            }
            else if (Effect != null)
            {
                for (int i = 0; i < Effect.Elements.Count; i++)
                {
                    EffectElementEntry element = Effect.Elements[i];
                    element.Transform = Transform;
                }
            }
            return base.Process(scene);
        }

        public override void GetDrawInfo(Scene scene)
        {
            Debug.Assert(Owner != null);
            if (BombType == BombType.Lockjaw)
            {
                if (BombIndex == 1)
                {
                    DrawLockjawTrail(Position, Owner.Bombs[0].Position, Fixed.ToFloat(614), 10, scene);
                }
                else if (BombIndex == 2)
                {
                    DrawLockjawTrail(Position, Owner.Bombs[1].Position, Fixed.ToFloat(614), 10, scene);
                    DrawLockjawTrail(Position, Owner.Bombs[0].Position, Fixed.ToFloat(614), 10, scene);
                }
            }
            base.GetDrawInfo(scene);
        }

        private void DrawLockjawTrail(Vector3 point1, Vector3 point2, float height, int segments, Scene scene)
        {
            Debug.Assert(_trailModel != null);
            if (segments < 2)
            {
                return;
            }
            int count = 4 * segments;
            int recolor = Recolor;
            if (Recolor > 0)
            {
                recolor--;
            }
            Vector3 vec = point2 - point1;
            Texture texture = _trailModel.Model.Recolors[recolor].Textures[0];
            float uvT = (texture.Height - (1 / 16f)) / texture.Height;
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(count);
            for (int i = 0; i < segments; i++)
            {
                float uvS = 0;
                if (i > 0)
                {
                    uvS = (texture.Width / (float)(segments - 1) * i - (1 / 16f)) / texture.Width;
                }
                float pct = i * (1f / (segments - 1));
                float x = vec.X * pct;
                float y = vec.Y * pct;
                float z = vec.Z * pct;
                if (i > 0 && i < segments - 1)
                {
                    x += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                    y += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                    z += Rng.GetRandomInt1(0x800) / 4096f - 0.25f;
                }
                uvsAndVerts[4 * i] = new Vector3(uvS, 0, 0);
                uvsAndVerts[4 * i + 1] = new Vector3(x, y - height, z);
                uvsAndVerts[4 * i + 2] = new Vector3(uvS, uvT, 0);
                uvsAndVerts[4 * i + 3] = new Vector3(x, y + height, z);
            }
            Material material = _trailModel.Model.Materials[0];
            scene.AddRenderItem(RenderItemType.TrailMulti, alpha: 1, scene.GetNextPolygonId(), Vector3.One, material.XRepeat, material.YRepeat,
                material.ScaleS, material.ScaleT, Matrix4.CreateTranslation(point1), uvsAndVerts, _bindingId, count);
        }

        public override void Destroy(Scene scene)
        {
            Debug.Assert(Owner != null);
            for (int i = BombIndex; i < Owner.BombMax - 1; i++)
            {
                Owner.Bombs[i] = Owner.Bombs[i + 1];
            }
            Owner.BombCount--;
            _models.Clear();
            _trailModel = null;
            if (Effect != null)
            {
                scene.UnlinkEffectEntry(Effect);
            }
            Effect = null;
            Owner = null;
        }

        public static void Spawn(PlayerEntity owner, Matrix4 transform, Scene scene)
        {
            BombType type = BombType.MorphBall;
            if (owner.Hunter == Hunter.Kanden)
            {
                type = BombType.Stinglarva;
            }
            else if (owner.Hunter == Hunter.Sylux)
            {
                type = BombType.Lockjaw;
            }
            BombEntity? bomb = scene.InitBomb();
            if (bomb == null)
            {
                Debug.Assert(false, "Failed to spawn bomb");
                return;
            }
            owner.Bombs[owner.BombCount] = bomb;
            bomb.Owner = owner;
            bomb.BombType = type;
            bomb.BombIndex = owner.BombCount++;
            bomb.Transform = transform;
            bomb.Recolor = owner.Recolor;
            bomb.Flags = BombFlags.None;
            scene.AddEntity(bomb);
        }
    }

    [Flags]
    public enum BombFlags : byte
    {
        None = 0x0,
        Exploding = 0x1,
        Exploded = 0x2,
        HasModel = 0x4,
        Bit03 = 0x8,
        Bit04 = 0x10,
        Bit05 = 0x20,
        Bit06 = 0x40,
        Bit07 = 0x80
    }
}
