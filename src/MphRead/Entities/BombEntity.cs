using System;
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
                Flags |= BombFlags.HasModel;
                _models.Add(Read.GetModelInstance("KandenAlt_TailBomb"));
                Countdown = 43 * 2; // todo: FPS stuff
            }
            else if (BombType == BombType.Lockjaw)
            {
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
                    element.Flags |= 0x80000; // set bit 19 (lifetime extension)
                }
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
                    Owner.BombCount--;
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

        public override void Destroy(Scene scene)
        {
            _models.Clear();
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
