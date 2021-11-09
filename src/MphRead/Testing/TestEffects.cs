using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Testing
{
    public static class TestEffects
    {
        private static readonly Random _random = new Random();

        public static void TestEffectMath()
        {
            while (true)
            {
                uint v5 = Rng.GetRandomInt1(0x168000);
                int index1 = (int)(2 * ((16 * (((0xB60B60B60B * v5) >> 32) + 2048)) >> 20));
                int index2 = (int)(2 * ((16 * (((0xB60B60B60B * v5) >> 32) + 2048)) >> 20) + 1);
                int index3 = (int)(2 * ((((((0xB60B60B60B * v5) >> 32) + 2048) >> 12) + 0x4000) >> 4));
                int index4 = (int)(2 * ((((((0xB60B60B60B * v5) >> 32) + 2048) >> 12) + 0x4000) >> 4) + 1);
                float angle1 = index1 / 2 * (360 / 4096f);
                float angle2 = index3 / 2 * (360 / 4096f);
                Debug.Assert(index1 % 2 == 0);
                Debug.Assert(index3 % 2 == 0);
                Debug.Assert(index1 + 1 == index2);
                Debug.Assert(index3 + 1 == index4);
                Debug.Assert(index1 + 2048 == index3);
                Debug.Assert(angle1 >= 0 && angle1 < 360);
                Debug.Assert(angle2 >= 0 && angle2 < 360);
                float test = angle1 + 90;
                if (test >= 360)
                {
                    test -= 360;
                }
                Debug.Assert(test == angle2);
                //Console.WriteLine($"v5: {v5} ({v5 / 4096f} deg)");
                //Console.WriteLine($"i1: {index1} ({angle1} deg)");
                //Console.WriteLine($"i3: {index3} ({angle2} deg)");
                Nop();
            }
        }

        public static void TestEffectMathFloat()
        {
            // random angle value comes from rot_z (originally rw_field_1)
            // note: in the original fixed math, neither angle will be >= 360 because of the way the division(?) works
            // -- but also, in this case, we don't need to worry about it since we're just doing trig
            float angle = _random.Next(0x168000) / 4096f;
            float angle1 = MathHelper.DegreesToRadians(angle);
            float angle2 = MathHelper.DegreesToRadians(angle + 90);
            float cos1 = MathF.Cos(angle1);
            float sin1 = MathF.Sin(angle1);
            float cos2 = MathF.Cos(angle2);
            float sin2 = MathF.Sin(angle2);
        }

        public static void TestAllEffects()
        {
            foreach ((string name, string? archive) in Metadata.Effects)
            {
                if (name != "" && name != "sparksFall" && name != "mortarSecondary" && name != "powerBeamChargeNoSplatMP")
                {
                    Effect effect = Read.LoadEffect(name, archive);
                    foreach (EffectElement element in effect.Elements)
                    {
                    }
                }
            }
            Nop();
        }

        public static void TestEffectBases()
        {
            var names = new List<string>() { "deathParticle", "geo1", "particles", "particles2" };
            foreach (string name in names)
            {
                Model model = Read.GetModelInstance(name).Model;
                foreach (Material material in model.Materials)
                {
                    if (material.XRepeat == RepeatMode.Mirror || material.YRepeat == RepeatMode.Mirror)
                    {
                        Console.WriteLine($"{name} - {material.Name} ({material.TextureId}, {material.PaletteId})");
                    }
                    if (material.XRepeat == RepeatMode.Mirror)
                    {
                        Console.WriteLine($"S: {material.ScaleS}");
                    }
                    if (material.YRepeat == RepeatMode.Mirror)
                    {
                        Console.WriteLine($"T: {material.ScaleT}");
                    }
                    if (material.XRepeat == RepeatMode.Mirror || material.YRepeat == RepeatMode.Mirror)
                    {
                        Console.WriteLine();
                    }
                }
            }
            Nop();
        }

        public static int FxDiv(int a, int b)
        {
            return (int)(((long)a << 12) / b);
        }

        public static int TestFx41(IReadOnlyList<int> parameters, int percent)
        {
            int result;
            int next;
            int index1 = -1;
            int index2 = 0;
            //int percent = FxDiv(elapsed, lifespan);
            if (percent < parameters[index2 + 0])
            {
                return parameters[index2 + 1];
            }
            if (parameters[index2 + 0] != Int32.MinValue)
            {
                do
                {
                    if (parameters[index2 + 0] > percent)
                    {
                        break;
                    }
                    index1 = index2;
                    next = parameters[index2 + 2];
                    index2 += 2;
                }
                while (next != Int32.MinValue);
            }
            if (index1 == -1)
            {
                return 0;
            }
            int v7 = parameters[index1 + 2];
            if (v7 == Int32.MinValue)
            {
                result = parameters[index1 + 1];
            }
            else
            {
                result = parameters[index1 + 1] + (int)(((parameters[index1 + 3] - parameters[index1 + 1])
                    * (long)FxDiv(percent - parameters[index1 + 0], v7 - parameters[index1 + 0]) + 2048) >> 12);
                int left = (parameters[index1 + 3] - parameters[index1 + 1]);
                int right = FxDiv(percent - parameters[index1 + 0], v7 - parameters[index1 + 0]);
                int prod = (left * right + 2048) >> 12;
                int parm = parameters[index1 + 1];
                int final = parm + prod;
                Nop();
            }
            return result;
        }

        public static void TestEntityEffects()
        {
            var effects = new Dictionary<int, Effect>();
            for (int i = 0; i < Metadata.Effects.Count; i++)
            {
                (string name, string? archive) = Metadata.Effects[i];
                if (name != "" && name != "sparksFall" && name != "mortarSecondary"
                    && name != "powerBeamChargeNoSplatMP")
                {
                    effects.Add(i, Read.LoadEffect(name, archive));
                }
            }
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                bool printed = false;
                if (meta.Value.EntityPath != null)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.Object)
                        {
                            ObjectEntityData data = ((Entity<ObjectEntityData>)entity).Data;
                            if (data.EffectId > 0)
                            {
                                if (!printed)
                                {
                                    Console.WriteLine("--------------------------------------------------------------------------------------");
                                    Console.WriteLine();
                                    Console.WriteLine($"{meta.Key} ({meta.Value.InGameName})");
                                    Console.WriteLine();
                                    printed = true;
                                }
                                Effect effect = effects[data.EffectId];
                                Console.WriteLine($"[ ] Entity {entity.EntityId}, Effect {data.EffectId} ({effect.Name})");
                                var elems = new List<string>();
                                foreach (EffectElement element in effect.Elements)
                                {
                                    (int setVecsId, int drawId) = EffectFuncBase.GetFuncIds(element.Flags, element.DrawType);
                                    string vecs = setVecsId switch
                                    {
                                        1 => "B0",
                                        2 => "BC",
                                        3 => "C0",
                                        4 => "D4",
                                        5 => "D8",
                                        _ => "XX"
                                    };
                                    string draw = drawId switch
                                    {
                                        1 => "B4",
                                        2 => "B8",
                                        3 => "C4",
                                        4 => "C8",
                                        5 => "CC",
                                        6 => "D0",
                                        7 => "DC",
                                        _ => "XX"
                                    };
                                    elems.Add($"{element.Name} v:{vecs} d:{draw}");
                                }
                                Console.WriteLine(String.Join(", ", elems));
                                Console.Write("Spawns: ");
                                if (data.EffectFlags.TestFlag(Entities.ObjEffFlags.AlwaysUpdateEffect))
                                {
                                    Console.WriteLine("Always");
                                }
                                else if (data.EffectFlags.TestFlag(Entities.ObjEffFlags.UseEffectVolume))
                                {
                                    Console.WriteLine("Volume");
                                }
                                else
                                {
                                    Console.WriteLine("Anim ID");
                                }
                                Console.WriteLine($"Attach: {(data.EffectFlags.TestFlag(Entities.ObjEffFlags.AttachEffect) ? "Yes" : "No")}");
                                Console.WriteLine($"Linked: {((data.LinkedEntity != -1) ? data.LinkedEntity.ToString() : "No")}");
                                Console.WriteLine();
                            }
                        }
                    }
                }
            }
            Nop();
        }

        private static void Nop()
        {
        }
    }
}
