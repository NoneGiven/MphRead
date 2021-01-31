using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTK.Mathematics;

namespace MphRead.Effects
{
    public readonly struct TimeValues
    {
        // global elapsed time
        public readonly float Global;
        // time since the current EffectElementEntry or EffectParticle was created
        public readonly float Elapsed;
        // lifespan of the current EffectElementEntry or EffectParticle
        public readonly float Lifespan;

        public TimeValues(float global, float elapsed, float lifespan)
        {
            Global = global;
            Elapsed = elapsed;
            Lifespan = lifespan;
        }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public abstract class EffectFuncBase
    {
        [NotNull, DisallowNull]
        public virtual IReadOnlyDictionary<FuncAction, FxFuncInfo>? Actions { get; set; }
        [NotNull, DisallowNull]
        public virtual IReadOnlyDictionary<uint, FxFuncInfo>? Funcs { get; set; }

        protected abstract void FxFunc01(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec);

        protected abstract void FxFunc03(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec);

        protected void FxFunc04(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(param[0]);
            vec.Y = Fixed.ToFloat(param[1]);
            vec.Z = Fixed.ToFloat(param[2]);
        }

        protected void FxFunc05(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        protected void FxFunc06(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        protected void FxFunc07(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = 1;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        protected void FxFunc08(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        protected void FxFunc09(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        protected void FxFunc10(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = 1;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        protected abstract void FxFunc11(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec);

        protected void FxFunc13(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Debug.Assert(false, "FxFunc13 was called");
            // note: some weirdness with the param list -- params[1] instead of *(*params + 4) -- but this function is unused
            FxFuncInfo funcInfo = Funcs[(uint)param[0]];
            FxFuncInfo paramInfo = Funcs[(uint)param[1]];
            float value = InvokeFloatFunc(funcInfo.FuncId, paramInfo.Parameters, times);
            float percent = times.Elapsed / value;
            if (value < 0)
            {
                percent *= -1;
            }
            float angle = MathHelper.DegreesToRadians(360 * percent);
            vec.X = MathF.Sin(angle);
            vec.Y = 0;
            vec.Z = MathF.Cos(angle);
        }

        protected void FxFunc14(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp);
            float value = InvokeFloatFunc(Funcs[(uint)param[1]], times);
            float div = times.Elapsed / value;
            if (value < 0)
            {
                div *= -1;
            }
            vec.X = temp.X * div;
            vec.Y = temp.Y * div;
            vec.Z = temp.Z * div;
        }

        protected void FxFunc15(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float value1 = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            float value2 = InvokeFloatFunc(Funcs[(uint)param[1]], times);
            float angle = MathHelper.DegreesToRadians((Test.GetRandomInt1(0xFFFF) >> 4) * (360 / 4096f));
            vec.X = MathF.Sin(angle) * value1;
            vec.Y = value2;
            vec.Z = MathF.Cos(angle) * value1;
        }

        protected void FxFunc16(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float value1 = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            float value2 = InvokeFloatFunc(Funcs[(uint)param[1]], times);
            vec.X = (Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f) * value1;
            vec.Y = 0;
            vec.Z = (Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f) * value2;
        }

        protected void FxFunc17(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X + temp2.X;
            vec.Y = temp1.Y + temp2.Y;
            vec.Z = temp1.Z + temp2.Z;
        }

        protected void FxFunc18(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X - temp2.X;
            vec.Y = temp1.Y - temp2.Y;
            vec.Z = temp1.Z - temp2.Z;
        }

        protected void FxFunc19(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X / temp2.X;
            vec.Y = temp1.Y / temp2.Y;
            vec.Z = temp1.Z / temp2.Z;
        }

        protected void FxFunc20(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp = Vector3.Zero;
            float value = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp);
            vec.X = temp.X * value;
            vec.Y = temp.Y * value;
            vec.Z = temp.Z * value;
        }

        protected float FxFunc21(IReadOnlyList<int> param, TimeValues times)
        {
            return 0;
        }

        protected abstract float FxFunc22(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc23(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc24(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc25(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc26(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc27(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc29(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc30(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc31(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc32(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc33(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc34(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc35(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc36(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc37(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc38(IReadOnlyList<int> param, TimeValues times);

        protected abstract float FxFunc39(IReadOnlyList<int> param, TimeValues times);

        protected float FxFunc40(IReadOnlyList<int> param, TimeValues times)
        {
            if (times.Elapsed / times.Lifespan <= Fixed.ToFloat(param[0]))
            {
                return Fixed.ToFloat(param[1]);
            }
            return Fixed.ToFloat(param[2]);
        }

        protected float FxFunc41(IReadOnlyList<int> param, TimeValues times)
        {
            float percent = times.Elapsed / times.Lifespan;
            if (percent < Fixed.ToFloat(param[0]))
            {
                return Fixed.ToFloat(param[1]);
            }
            bool none = true;
            int i = 0;
            do
            {
                if (Fixed.ToFloat(param[i]) > percent)
                {
                    break;
                }
                none = false;
                i += 2;
            }
            while (param[i] != Int32.MinValue);
            if (none)
            {
                return 0;
            }
            i -= 2;
            if (param[i + 2] == Int32.MinValue)
            {
                return Fixed.ToFloat(param[i + 1]);
            }
            return Fixed.ToFloat(param[i + 1]) + (Fixed.ToFloat(param[i + 3]) - Fixed.ToFloat(param[i + 1]))
                * ((percent - Fixed.ToFloat(param[i])) / (Fixed.ToFloat(param[i + 2]) - Fixed.ToFloat(param[i])));
        }

        protected float FxFunc42(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(param[0]);
        }

        protected float FxFunc43(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        protected float FxFunc44(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        // get random angle [0-360) in fx32
        protected float FxFunc45(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(0x168000));
        }

        protected float FxFunc46(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) + InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        protected float FxFunc47(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) - InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        protected float FxFunc48(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) * InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        protected float FxFunc49(IReadOnlyList<int> param, TimeValues times)
        {
            if (InvokeFloatFunc(Funcs[(uint)param[0]], times) >= InvokeFloatFunc(Funcs[(uint)param[1]], times))
            {
                return InvokeFloatFunc(Funcs[(uint)param[2]], times);
            }
            return InvokeFloatFunc(Funcs[(uint)param[3]], times);
        }

        public void InvokeVecFunc(FxFuncInfo info, TimeValues times, ref Vector3 vec)
        {
            switch (info.FuncId)
            {
            case 1:
            case 2:
                FxFunc01(info.Parameters, times, ref vec);
                break;
            case 3:
                FxFunc03(info.Parameters, times, ref vec);
                break;
            case 4:
                FxFunc04(info.Parameters, times, ref vec);
                break;
            case 5:
                FxFunc05(info.Parameters, times, ref vec);
                break;
            case 6:
                FxFunc06(info.Parameters, times, ref vec);
                break;
            case 7:
                FxFunc07(info.Parameters, times, ref vec);
                break;
            case 8:
                FxFunc08(info.Parameters, times, ref vec);
                break;
            case 9:
                FxFunc09(info.Parameters, times, ref vec);
                break;
            case 10:
                FxFunc10(info.Parameters, times, ref vec);
                break;
            case 11:
                FxFunc11(info.Parameters, times, ref vec);
                break;
            case 13:
                FxFunc13(info.Parameters, times, ref vec);
                break;
            case 14:
                FxFunc14(info.Parameters, times, ref vec);
                break;
            case 15:
                FxFunc15(info.Parameters, times, ref vec);
                break;
            case 16:
                FxFunc16(info.Parameters, times, ref vec);
                break;
            case 17:
                FxFunc17(info.Parameters, times, ref vec);
                break;
            case 18:
                FxFunc18(info.Parameters, times, ref vec);
                break;
            case 19:
                FxFunc19(info.Parameters, times, ref vec);
                break;
            case 20:
                FxFunc20(info.Parameters, times, ref vec);
                break;
            default:
                throw new ProgramException("Invalid effect func.");
            }
        }

        public float InvokeFloatFunc(FxFuncInfo info, TimeValues times)
        {
            return InvokeFloatFunc(info.FuncId, info.Parameters, times);
        }

        private float InvokeFloatFunc(uint funcId, IReadOnlyList<int> parameters, TimeValues times)
        {
            return funcId switch
            {
                28 => FxFunc21(parameters, times),
                21 => FxFunc21(parameters, times),
                22 => FxFunc22(parameters, times),
                23 => FxFunc23(parameters, times),
                24 => FxFunc24(parameters, times),
                25 => FxFunc25(parameters, times),
                26 => FxFunc26(parameters, times),
                27 => FxFunc27(parameters, times),
                29 => FxFunc29(parameters, times),
                30 => FxFunc30(parameters, times),
                31 => FxFunc31(parameters, times),
                32 => FxFunc32(parameters, times),
                33 => FxFunc33(parameters, times),
                34 => FxFunc34(parameters, times),
                35 => FxFunc35(parameters, times),
                36 => FxFunc36(parameters, times),
                37 => FxFunc37(parameters, times),
                38 => FxFunc38(parameters, times),
                39 => FxFunc39(parameters, times),
                40 => FxFunc40(parameters, times),
                41 => FxFunc41(parameters, times),
                42 => FxFunc42(parameters, times),
                43 => FxFunc43(parameters, times),
                44 => FxFunc44(parameters, times),
                45 => FxFunc45(parameters, times),
                46 => FxFunc46(parameters, times),
                47 => FxFunc47(parameters, times),
                48 => FxFunc48(parameters, times),
                49 => FxFunc49(parameters, times),
                _ => throw new ProgramException("Invalid effect func.")
            };
        }
    }

    public class EffectElementEntry : EffectFuncBase
    {
        public float CreationTime { get; set; }
        public float ExpirationTime { get; set; }
        public float DrainTime { get; set; }
        public float BufferTime { get; set; }
        public float Lifespan { get; set; }
        public uint Flags { get; set; }
        public int DrawType { get; set; }
        public Vector3 Position { get; set; }
        public Matrix4 Transform { get; set; }
        public Vector3 Acceleration { get; set; }
        public bool Func39Called { get; set; }
        public float ParticleAmount { get; set; }

        public List<Particle> ParticleDefinitions { get; } = new List<Particle>();
        public List<int> TextureBindingIds { get; } = new List<int>();

        [NotNull, DisallowNull]
        public Model? Model { get; set; }

        protected override void FxFunc01(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Position.X;
            vec.Y = Position.Y;
            vec.Z = Position.Z;
        }

        protected override void FxFunc03(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            // element doesn't have the Speed property
            throw new NotImplementedException();
        }

        protected override void FxFunc11(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            // element doesn't have the PortionTotal property
            throw new NotImplementedException();
        }

        protected override float FxFunc22(IReadOnlyList<int> param, TimeValues times)
        {
            return Lifespan;
        }

        protected override float FxFunc23(IReadOnlyList<int> param, TimeValues times)
        {
            return times.Global - CreationTime;
        }

        protected override float FxFunc24(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Alpha property
            throw new NotImplementedException();
        }

        protected override float FxFunc25(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Red property
            throw new NotImplementedException();
        }

        protected override float FxFunc26(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Green property
            throw new NotImplementedException();
        }

        protected override float FxFunc27(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Blue property
            throw new NotImplementedException();
        }

        protected override float FxFunc29(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Scale property
            throw new NotImplementedException();
        }

        protected override float FxFunc30(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the Rotation property
            throw new NotImplementedException();
        }

        protected override float FxFunc31(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RoField1 property
            throw new NotImplementedException();
        }

        protected override float FxFunc32(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RoField2 property
            throw new NotImplementedException();
        }

        protected override float FxFunc33(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RoField3 property
            throw new NotImplementedException();
        }

        protected override float FxFunc34(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RoField4 property
            throw new NotImplementedException();
        }

        protected override float FxFunc35(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RwField1 property
            throw new NotImplementedException();
        }

        protected override float FxFunc36(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RwField2 property
            throw new NotImplementedException();
        }

        protected override float FxFunc37(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RwField3 property
            throw new NotImplementedException();
        }

        protected override float FxFunc38(IReadOnlyList<int> param, TimeValues times)
        {
            // element doesn't have the RwField4 property
            throw new NotImplementedException();
        }

        protected override float FxFunc39(IReadOnlyList<int> param, TimeValues times)
        {
            if (Func39Called)
            {
                return 0;
            }
            Func39Called = true;
            return Fixed.ToFloat(param[0]);
        }
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public class EffectParticle : EffectFuncBase
    {
        public float CreationTime { get; set; }
        public float ExpirationTime { get; set; }
        public float Lifespan { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Speed { get; set; }
        public float Scale { get; set; }
        public float Rotation { get; set; }
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public float Alpha { get; set; }
        public int ParticleId { get; set; }
        public float PortionTotal { get; set; }
        public float RoField1 { get; set; } // 4 general-purpose fields set once upon particle creation
        public float RoField2 { get; set; }
        public float RoField3 { get; set; }
        public float RoField4 { get; set; }
        public float RwField1 { get; set; } // 4 general-purpose fields updated every frame
        public float RwField2 { get; set; }
        public float RwField3 { get; set; }
        public float RwField4 { get; set; }

        [NotNull, DisallowNull]
        public EffectElementEntry? Owner { get; set; }
        public int MaterialId { get; set; }
        public int SetVecsId { get; set; }
        public int DrawId { get; set; }
        public Vector3 EffectVec1 { get; private set; }
        public Vector3 EffectVec2 { get; private set; }
        public Vector3 EffectVec3 { get; private set; }

        public bool ShouldDraw { get; private set; }
        public Vector3 Color { get; private set; }
        public Vector2 Texcoord0 { get; private set; }
        public Vector3 Vertex0 { get; private set; }
        public Vector2 Texcoord1 { get; private set; }
        public Vector3 Vertex1 { get; private set; }
        public Vector2 Texcoord2 { get; private set; }
        public Vector3 Vertex2 { get; private set; }
        public Vector2 Texcoord3 { get; private set; }
        public Vector3 Vertex3 { get; private set; }

        [NotNull, DisallowNull]
        public override IReadOnlyDictionary<uint, FxFuncInfo>? Funcs
        {
            get => Owner.Funcs;
            set => Owner.Funcs = value;
        }

        [NotNull, DisallowNull]
        public override IReadOnlyDictionary<FuncAction, FxFuncInfo>? Actions
        {
            get => Owner.Actions;
            set => Owner.Actions = value;
        }

        protected override void FxFunc01(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Position.X;
            vec.Y = Position.Y;
            vec.Z = Position.Z;
        }

        protected override void FxFunc03(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Speed.X;
            vec.Y = Speed.Y;
            vec.Z = Speed.Z;
        }

        protected override void FxFunc11(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float angle = MathHelper.DegreesToRadians(360 * PortionTotal);
            vec.X = MathF.Sin(angle);
            vec.Y = 0;
            vec.Z = MathF.Cos(angle);
        }

        protected override float FxFunc22(IReadOnlyList<int> param, TimeValues times)
        {
            return Owner.Lifespan;
        }

        protected override float FxFunc23(IReadOnlyList<int> param, TimeValues times)
        {
            return times.Global - Owner.CreationTime;
        }

        protected override float FxFunc24(IReadOnlyList<int> param, TimeValues times)
        {
            return Alpha;
        }

        protected override float FxFunc25(IReadOnlyList<int> param, TimeValues times)
        {
            return Red;
        }

        protected override float FxFunc26(IReadOnlyList<int> param, TimeValues times)
        {
            return Green;
        }

        protected override float FxFunc27(IReadOnlyList<int> param, TimeValues times)
        {
            return Blue;
        }

        protected override float FxFunc29(IReadOnlyList<int> param, TimeValues times)
        {
            return Scale;
        }

        protected override float FxFunc30(IReadOnlyList<int> param, TimeValues times)
        {
            return Rotation;
        }

        protected override float FxFunc31(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField1;
        }

        protected override float FxFunc32(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField2;
        }

        protected override float FxFunc33(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField3;
        }

        protected override float FxFunc34(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField4;
        }

        protected override float FxFunc35(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField1;
        }

        protected override float FxFunc36(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField2;
        }

        protected override float FxFunc37(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField3;
        }

        protected override float FxFunc38(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField4;
        }

        protected override float FxFunc39(IReadOnlyList<int> param, TimeValues times)
        {
            if (Owner.Func39Called)
            {
                return 0;
            }
            Owner.Func39Called = true;
            return Fixed.ToFloat(param[0]);
        }

        private void SetVecsB0(Matrix4 viewMatrix)
        {
            var vec1 = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);
            var vec2 = new Vector3(-viewMatrix.M12, -viewMatrix.M22, -viewMatrix.M32);
            EffectVec1 = vec1;
            EffectVec2 = vec2;
        }

        private void SetVecsBC(Matrix4 viewMatrix)
        {
            Matrix3 identity = Matrix3.Identity;
            var vec1 = new Vector3(identity.M11, identity.M21, identity.M31);
            var vec2 = new Vector3(identity.M13, identity.M23, identity.M33);
            EffectVec1 = vec1;
            EffectVec2 = vec2;
        }

        private void SetVecsC0(Matrix4 viewMatrix)
        {
            Matrix3 identity = Matrix3.Identity;
            var vec1 = new Vector3(-identity.M12, -identity.M22, -identity.M32);
            var vec2 = new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33);
            EffectVec1 = vec1;
            EffectVec2 = vec2;
        }

        private void SetVecsD4(Matrix4 viewMatrix)
        {
            var vec1 = Vector3.Normalize(Speed);
            var vec2 = new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33);
            var vec3 = Vector3.Cross(vec2, vec1);
            if (vec3.LengthSquared < Fixed.ToFloat(64))
            {
                vec2 = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);
                vec3 = Vector3.Cross(vec2, vec1);
            }
            vec3 = Vector3.Normalize(vec3);
            vec1 = Vector3.Cross(vec3, vec2);
            EffectVec1 = vec1;
            EffectVec2 = vec2;
            EffectVec3 = vec3;
        }

        private void SetVecsD8(Matrix4 viewMatrix)
        {
            Vector3 vec3;
            if ((Owner.Flags & 1) != 0)
            {
                vec3 = new Vector3(
                    viewMatrix.Row3.X - (Position.X + Owner.Position.X),
                    viewMatrix.Row3.Y - (Position.Y + Owner.Position.Y),
                    viewMatrix.Row3.Z - (Position.Z + Owner.Position.Z)
                );
            }
            else
            {
                vec3 = new Vector3(
                    viewMatrix.Row3.X - Position.X,
                    viewMatrix.Row3.Y - Position.Y,
                    viewMatrix.Row3.Z - Position.Z
                );
            }
            vec3 = Vector3.Normalize(vec3);
            var vec2 = new Vector3(viewMatrix.Row1);
            var vec1 = Vector3.Cross(vec3, vec2);
            if (vec1.LengthSquared < Fixed.ToFloat(64))
            {
                vec2 = new Vector3(viewMatrix.Row2);
                vec1 = Vector3.Cross(vec3, vec2);
            }
            vec1 = Vector3.Normalize(vec1);
            vec2 = Vector3.Cross(vec3, vec1);
            EffectVec1 = vec1;
            EffectVec2 = vec2;
            EffectVec3 = vec3;
        }

        public void InvokeSetVecsFunc(Matrix4 viewMatrix)
        {
            switch (SetVecsId)
            {
            case 1:
                SetVecsB0(viewMatrix);
                break;
            case 2:
                SetVecsBC(viewMatrix);
                break;
            case 3:
                SetVecsC0(viewMatrix);
                break;
            case 4:
                SetVecsD4(viewMatrix);
                break;
            case 5:
                SetVecsD8(viewMatrix);
                break;
            default:
                throw new ProgramException("Invalid set vecs func.");
            }
        }

        private void DrawB8(float scaleFactor)
        {
            // todo
        }

        private void DrawC4(float scaleFactor)
        {
            ShouldDraw = false;
            if (Speed.LengthSquared > Fixed.ToFloat(128))
            {
                ShouldDraw = true;
                EffectVec1 = Vector3.Normalize(Speed);
                DrawShared(scaleFactor, skipIfZeroSpeed: true);
            }
        }

        private void DrawCC(float scaleFactor)
        {
            ShouldDraw = false;
            if (Alpha > 0)
            {
                ShouldDraw = true;
                Color = new Vector3(Red, Green, Blue);
                float angle1 = MathHelper.DegreesToRadians(Rotation);
                float angle2 = MathHelper.DegreesToRadians(Rotation + 90);
                float sin1 = MathF.Sin(angle1);
                float cos1 = MathF.Cos(angle1);
                float sin2 = MathF.Sin(angle2);
                float cos2 = MathF.Cos(angle2);

                float v20 = (EffectVec1.X * sin2 + EffectVec2.X * cos2) * Scale;
                float v24 = (EffectVec1.Y * sin2 + EffectVec2.Y * cos2) * Scale;
                float v25 = (EffectVec1.Z * sin2 + EffectVec2.Z * cos2) * Scale;

                float v26 = (EffectVec1.X * sin1 + EffectVec2.X * cos1) * Scale;
                float v28 = (EffectVec1.Y * sin1 + EffectVec2.Y * cos1) * Scale;
                float v29 = (EffectVec1.Z * sin1 + EffectVec2.Z * cos1) * Scale;

                float v27 = Position.X + (-v20 / 2) + (v26 / 2);
                float v30 = Position.Y + (-v24 / 2) + (v28 / 2);
                float v31 = Position.Z + (-v25 / 2) + (v29 / 2);

                // top left
                float x = v27 / scaleFactor;
                float y = v30 / scaleFactor;
                float z = v31 / scaleFactor;
                Vertex0 = new Vector3(x, y, z);
                Texcoord0 = new Vector2(0, 1);

                // top right
                float v39 = v27 + v20;
                float v38 = v30 + v24;
                float v33 = v31 + v25;
                x = v39 / scaleFactor;
                y = v38 / scaleFactor;
                z = v33 / scaleFactor;
                Vertex1 = new Vector3(x, y, z);
                Texcoord1 = new Vector2(1, 1);

                // bottom right
                float v40 = v27 + v20 - v26;
                float v41 = v38 - v28;
                float v35 = v33 - v29;
                x = v40 / scaleFactor;
                y = v41 / scaleFactor;
                z = v35 / scaleFactor;
                Vertex2 = new Vector3(x, y, z);
                Texcoord2 = new Vector2(1, 0);

                // bottom left
                float v42 = v40 - v20;
                float v43 = v41 - v24;
                float v36 = v33 - v29 - v25;
                x = v42 / scaleFactor;
                y = v43 / scaleFactor;
                z = v36 / scaleFactor;
                Vertex3 = new Vector3(x, y, z);
                Texcoord3 = new Vector2(0, 0);
            }
        }

        private void DrawD0(float scaleFactor)
        {
            DrawShared(scaleFactor, skipIfZeroSpeed: false);
        }

        private void DrawDC(float scaleFactor)
        {
            // todo: draw_single_node for geo1 effects -- need model/node references
        }

        private void DrawShared(float scaleFactor, bool skipIfZeroSpeed)
        {
            // todo
        }

        public void InvokeDrawFunc(float scaleFactor)
        {
            switch (DrawId)
            {
            case 1:
                DrawB8(scaleFactor: 3);
                break;
            case 2:
                DrawB8(scaleFactor);
                break;
            case 3:
                DrawC4(scaleFactor);
                break;
            case 4:
                DrawCC(scaleFactor: 3);
                break;
            case 5:
                DrawCC(scaleFactor);
                break;
            case 6:
                DrawD0(scaleFactor);
                break;
            case 7:
                DrawDC(scaleFactor);
                break;
            default:
                throw new ProgramException("Invalid draw func.");
            }
        }

        public void SetFuncIds()
        {
            if ((Owner.Flags & 4) != 0)
            {
                DrawId = 7;
                SetVecsId = (Owner.DrawType == 3 ? 4 : 5);
            }
            else
            {
                switch (Owner.DrawType)
                {
                case 1:
                    SetVecsId = 1;
                    if ((Owner.Flags & 1) != 0)
                    {
                        DrawId = 1;
                    }
                    else
                    {
                        DrawId = 2;
                    }
                    break;
                case 2:
                    SetVecsId = 2;
                    if ((Owner.Flags & 1) != 0)
                    {
                        DrawId = 1;
                    }
                    else
                    {
                        DrawId = 2;
                    }
                    break;
                case 3:
                    SetVecsId = 3;
                    DrawId = 3;
                    break;
                case 4:
                    SetVecsId = 1;
                    if ((Owner.Flags & 1) != 0)
                    {
                        DrawId = 4;
                    }
                    else
                    {
                        DrawId = 5;
                    }
                    break;
                case 5:
                    SetVecsId = 2;
                    if ((Owner.Flags & 1) != 0)
                    {
                        DrawId = 4;
                    }
                    else
                    {
                        DrawId = 5;
                    }
                    break;
                case 6:
                    SetVecsId = 3;
                    DrawId = 6;
                    break;
                default:
                    throw new ProgramException("Invalid draw type.");
                }
            }
        }
    }
}
