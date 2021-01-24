using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace MphRead.Effects
{
    public struct TimeValues
    {
        // global elapsed time
        public float Global;
        // time since the current EffectElementEntry or EffectParticle was created
        public float Elapsed;
        // lifespan of the current EffectElementEntry or EffectParticle
        public float Lifespan;
    }

    public class EffectElementEntry
    {
        public float CreationTime { get; set; }
        public float ExpirationTime { get; set; }
        public float DrainTime { get; set; }
        public float BufferTime { get; set; }
        public bool Func39Called { get; set; }
        public uint Flags { get; set; }
        public float Lifespan { get; set; }
        public int DrawType { get; set; }
        public Vector3 Position { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public class EffectParticle
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

        public EffectElementEntry Owner { get; }
        public IReadOnlyDictionary<uint, FxFuncInfo> Funcs { get; }
        public int SetVecsId { get; set; }
        public int DrawId { get; set; }
        public Vector3 EffectVec1 { get; set; }
        public Vector3 EffectVec2 { get; set; }
        public Vector3 EffectVec3 { get; set; }

        public EffectParticle(EffectElementEntry owner, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            Owner = owner;
            Funcs = funcs;
        }

        private void FxFunc01(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Position.X;
            vec.Y = Position.Y;
            vec.Z = Position.Z;
        }

        private void FxFunc03(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Speed.X;
            vec.Y = Speed.Y;
            vec.Z = Speed.Z;
        }

        private void FxFunc04(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = param[0];
            vec.Y = param[1];
            vec.Z = param[2];
        }

        private void FxFunc05(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        private void FxFunc06(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        private void FxFunc07(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096));
            vec.Y = 1;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        private void FxFunc08(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        private void FxFunc09(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        private void FxFunc10(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
            vec.Y = 1;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        private void FxFunc11(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float angle = MathHelper.DegreesToRadians(360 * PortionTotal);
            vec.X = MathF.Cos(angle);
            vec.Y = 0;
            vec.Z = MathF.Sin(angle);
        }

        private void FxFunc13(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
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
            vec.X = MathF.Cos(angle);
            vec.Y = 0;
            vec.Z = MathF.Sin(angle);
        }

        private void FxFunc14(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
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

        private void FxFunc15(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float value1 = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            float value2 = InvokeFloatFunc(Funcs[(uint)param[1]], times);
            float angle = MathHelper.DegreesToRadians(2 * (Test.GetRandomInt1(0xFFFF) >> 4) * (360 / 4096f));
            vec.X = MathF.Cos(angle) * value1;
            vec.Y = value2;
            vec.Z = MathF.Sin(angle) * value1;
        }

        private void FxFunc16(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            float value1 = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            float value2 = InvokeFloatFunc(Funcs[(uint)param[1]], times);
            vec.X = (Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f) * value1;
            vec.Y = 0;
            vec.Z = (Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f) * value2;
        }

        private void FxFunc17(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X + temp2.X;
            vec.Y = temp1.Y + temp2.Y;
            vec.Z = temp1.Z + temp2.Z;
        }

        private void FxFunc18(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X - temp2.X;
            vec.Y = temp1.Y - temp2.Y;
            vec.Z = temp1.Z - temp2.Z;
        }

        private void FxFunc19(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(Funcs[(uint)param[0]], times, ref temp1);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp2);
            vec.X = temp1.X / temp2.X;
            vec.Y = temp1.Y / temp2.Y;
            vec.Z = temp1.Z / temp2.Z;
        }

        private void FxFunc20(IReadOnlyList<int> param, TimeValues times, ref Vector3 vec)
        {
            Vector3 temp = Vector3.Zero;
            float value = InvokeFloatFunc(Funcs[(uint)param[0]], times);
            InvokeVecFunc(Funcs[(uint)param[1]], times, ref temp);
            vec.X = temp.X * value;
            vec.Y = temp.Y * value;
            vec.Z = temp.Z * value;
        }

        private float FxFunc21(IReadOnlyList<int> param, TimeValues times)
        {
            return 0;
        }

        private float FxFunc22(IReadOnlyList<int> param, TimeValues times)
        {
            return Owner.Lifespan;
        }

        private float FxFunc23(IReadOnlyList<int> param, TimeValues times)
        {
            return times.Global - Owner.CreationTime;
        }

        private float FxFunc24(IReadOnlyList<int> param, TimeValues times)
        {
            return Alpha;
        }

        private float FxFunc25(IReadOnlyList<int> param, TimeValues times)
        {
            return Red;
        }

        private float FxFunc26(IReadOnlyList<int> param, TimeValues times)
        {
            return Green;
        }

        private float FxFunc27(IReadOnlyList<int> param, TimeValues times)
        {
            return Blue;
        }

        private float FxFunc29(IReadOnlyList<int> param, TimeValues times)
        {
            return Scale;
        }

        private float FxFunc30(IReadOnlyList<int> param, TimeValues times)
        {
            return Rotation;
        }

        private float FxFunc31(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField1;
        }

        private float FxFunc32(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField2;
        }

        private float FxFunc33(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField3;
        }

        private float FxFunc34(IReadOnlyList<int> param, TimeValues times)
        {
            return RoField4;
        }

        private float FxFunc35(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField1;
        }

        private float FxFunc36(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField2;
        }

        private float FxFunc37(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField3;
        }

        private float FxFunc38(IReadOnlyList<int> param, TimeValues times)
        {
            return RwField4;
        }

        private float FxFunc39(IReadOnlyList<int> param, TimeValues times)
        {
            if (Owner.Func39Called)
            {
                return 0;
            }
            Owner.Func39Called = true;
            return Fixed.ToFloat(param[0]);
        }

        private float FxFunc40(IReadOnlyList<int> param, TimeValues times)
        {
            if (times.Elapsed / times.Lifespan <= Fixed.ToFloat(param[0]))
            {
                return Fixed.ToFloat(param[1]);
            }
            return Fixed.ToFloat(param[2]);
        }

        // todo: private
        public float FxFunc41(IReadOnlyList<int> param, TimeValues times)
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

        private float FxFunc42(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(param[0]);
        }

        private float FxFunc43(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        private float FxFunc44(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096)) - 0.5f;
        }

        // get random angle [0-360) in fx32
        private float FxFunc45(IReadOnlyList<int> param, TimeValues times)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(0x168000));
        }

        private float FxFunc46(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) + InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        private float FxFunc47(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) - InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        private float FxFunc48(IReadOnlyList<int> param, TimeValues times)
        {
            return InvokeFloatFunc(Funcs[(uint)param[0]], times) * InvokeFloatFunc(Funcs[(uint)param[1]], times);
        }

        private float FxFunc49(IReadOnlyList<int> param, TimeValues times)
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

        // sktodo: confirm the first matrix is always the identity matrix,
        // and that we can use the bottom row of the view matrix instead of cam info,
        // and that we can use our view matrix the same way as the in-game one is used in these functions
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
            if ((Owner.Flags & 1) != 0 )
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

        private void DrawB4()
        {
            // todo
        }

        private void DrawB8()
        {
            // todo
        }

        private void DrawC4()
        {
            // todo
        }

        private void DrawC8()
        {
            // todo
        }

        private void DrawCC()
        {
            if (Alpha > 0)
            {
                float angle1 = MathHelper.DegreesToRadians(Rotation);
                float angle2 = MathHelper.DegreesToRadians(Rotation + 90);
                float cos1 = MathF.Cos(angle1);
                float sin1 = MathF.Sin(angle1);
                float cos2 = MathF.Cos(angle2);
                float sin2 = MathF.Sin(angle2);
                // sktodo: polygon ID (RenderScene), double-sided (RenderMesh), alpha + modulate (DoMaterial)
                // -- can probably insert particle "meshes" as mesh info in RenderScene, but they need to take different paths to set uniforms etc.
                // (and obviously they need to have their vertex and other data directly in them instead of references to dlists/materials/etc.)
                // --> need to make sure effect base textures are set up and we can reference them by index (also eff_tex_width/height)
                var color = new Vector3(Red, Green, Blue);
            }
        }
        
        private void DrawD0()
        {
            // todo
        }

        private void DrawDC()
        {
            // todo
        }

        private void DrawShared(bool skipIfZeroSpeed)
        {
            // todo
        }

        public void InvokeDrawFunc()
        {
            switch (SetVecsId)
            {
            case 1:
                DrawB4();
                break;
            case 2:
                DrawB8();
                break;
            case 3:
                DrawC4();
                break;
            case 4:
                DrawC8();
                break;
            case 5:
                DrawCC();
                break;
            case 6:
                DrawD0();
                break;
            case 7:
                DrawDC();
                break;
            default:
                throw new ProgramException("Invalid draw func.");
            }
        }
    }
}
