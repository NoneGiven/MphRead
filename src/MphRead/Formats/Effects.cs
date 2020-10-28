using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTK.Mathematics;

namespace MphRead.Formats
{
    public static class Effects
    {
        [NotNull]
        [DisallowNull]
        public static EffectElement? CurrentElement { get; set; }

        private static void FxFunc4(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            vec.X = param[0];
            vec.Y = param[1];
            vec.Z = param[2];
        }

        private static void FxFunc8(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
        }

        private static void FxFunc9(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
        }

        private static void FxFunc11(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: sin/cos stuff with dword_2123DE8
        }

        private static void FxFunc14(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            Vector3 temp = Vector3.Zero;
            InvokeVecFunc(funcs[(uint)param[0]], a2, ref temp, funcs);
            float value = InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
            float div = Fixed.ToFloat(a2) / value;
            if (value < 0)
            {
                div *= -1;
            }
            vec.X = temp.X * div;
            vec.Y = temp.Y * div;
            vec.Z = temp.Z * div;
        }

        private static void FxFunc15(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            float value1 = InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs);
            float value2 = InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
            float angle = 2 * (Test.GetRandomInt1(0xFFFF) >> 4) * (360 / 4096f);
            vec.X = MathF.Sin(angle) * value1;
            vec.Y = value2;
            vec.Z = MathF.Cos(angle) * value1;
        }

        private static void FxFunc16(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            float value1 = InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs);
            float value2 = InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
            value1 = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048) * value1;
            value2 = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048) * value2;
            vec.X = value1 / 4f;
            vec.Y = 0;
            vec.X = value2 / 4f;
        }

        private static void FxFunc17(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(funcs[(uint)param[0]], a2, ref temp1, funcs);
            InvokeVecFunc(funcs[(uint)param[1]], a2, ref temp2, funcs);
            vec.X = temp1.X + temp2.X;
            vec.Y = temp1.Y + temp2.Y;
            vec.Z = temp1.Z + temp2.Z;
        }

        private static void FxFunc19(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            Vector3 temp1 = Vector3.Zero;
            Vector3 temp2 = Vector3.Zero;
            InvokeVecFunc(funcs[(uint)param[0]], a2, ref temp1, funcs);
            InvokeVecFunc(funcs[(uint)param[1]], a2, ref temp2, funcs);
            vec.X = temp1.X * temp2.X / 4096f;
            vec.Y = temp1.Y * temp2.Y / 4096f;
            vec.Z = temp1.Z * temp2.Z / 4096f;
        }

        private static void FxFunc20(IReadOnlyList<int> param, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            Vector3 temp = Vector3.Zero;
            float value = InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs);
            InvokeVecFunc(funcs[(uint)param[1]], a2, ref temp, funcs);
            vec.X = temp.X * value;
            vec.Y = temp.Y * value;
            vec.Z = temp.Z * value;
        }

        private static float FxFunc22(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->field_74;
            return 0;
        }

        private static float FxFunc24(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->alpha;
            return 0;
        }

        private static float FxFunc25(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->red;
            return 0;
        }

        private static float FxFunc26(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->green;
            return 0;
        }

        private static float FxFunc29(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->blue;
            return 0;
        }

        private static float FxFunc31(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->field_44;
            return 0;
        }

        private static float FxFunc32(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->field_48;
            return 0;
        }

        private static float FxFunc35(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: return cur_effect_elem->field_54;
            return 0;
        }

        private static float FxFunc40(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: eff_div_something
            float effDivSomething = Fixed.ToFloat(0x2000);
            if (Fixed.ToFloat(a2) / effDivSomething <= Fixed.ToFloat(param[0]))
            {
                return Fixed.ToFloat(param[1]);
            }
            return Fixed.ToFloat(param[2]);
        }

        private static float FxFunc41(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: eff_div_something
            float effDivSomething = Fixed.ToFloat(0x2000);
            float value = Fixed.ToFloat(a2) / effDivSomething;
            Debug.Assert(param.Count % 2 == 0);
            if (value < Fixed.ToFloat(param[0]))
            {
                return Fixed.ToFloat(param[1]);
            }
            //for (int i = 0; ; i += 2)
            //{
            //    float cur1 = Fixed.ToFloat(param[i]);
            //    float cur2 = Fixed.ToFloat(param[i + 1]);
            //    float next1 = Fixed.ToFloat(param[i + 2]);
            //    float next2 = Fixed.ToFloat(param[i + 3]);
            //}
            //return (C2 - B2) * ((v4 - B1) / (C1 - B1)) + B2
            return 0;
        }

        private static float FxFunc42(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return Fixed.ToFloat(param[0]);
        }

        private static float FxFunc43(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        private static float FxFunc45(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(0x168000));
        }

        private static float FxFunc46(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs) + InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
        }

        private static float FxFunc47(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs) - InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
        }

        private static float FxFunc48(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs) * InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs);
        }

        private static float FxFunc49(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            if (InvokeFloatFunc(funcs[(uint)param[0]], a2, funcs) >= InvokeFloatFunc(funcs[(uint)param[1]], a2, funcs))
            {
                return InvokeFloatFunc(funcs[(uint)param[2]], a2, funcs);
            }
            return InvokeFloatFunc(funcs[(uint)param[3]], a2, funcs);
        }

        public static void InvokeVecFunc(FxFuncInfo info, int a2, ref Vector3 vec, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            switch (info.FuncId)
            {
            case 4:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 8:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 9:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 11:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 14:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 15:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 16:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 17:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 19:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            case 20:
                FxFunc4(info.Parameters, a2, ref vec, funcs);
                break;
            default:
                throw new ProgramException("Invalid effect func.");
            }
        }

        public static float InvokeFloatFunc(FxFuncInfo info, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return info.FuncId switch
            {
                22 => FxFunc22(info.Parameters, a2, funcs),
                24 => FxFunc24(info.Parameters, a2, funcs),
                25 => FxFunc25(info.Parameters, a2, funcs),
                26 => FxFunc26(info.Parameters, a2, funcs),
                29 => FxFunc29(info.Parameters, a2, funcs),
                31 => FxFunc31(info.Parameters, a2, funcs),
                32 => FxFunc32(info.Parameters, a2, funcs),
                35 => FxFunc35(info.Parameters, a2, funcs),
                40 => FxFunc40(info.Parameters, a2, funcs),
                41 => FxFunc41(info.Parameters, a2, funcs),
                42 => FxFunc42(info.Parameters, a2, funcs),
                43 => FxFunc43(info.Parameters, a2, funcs),
                45 => FxFunc45(info.Parameters, a2, funcs),
                46 => FxFunc46(info.Parameters, a2, funcs),
                47 => FxFunc47(info.Parameters, a2, funcs),
                48 => FxFunc48(info.Parameters, a2, funcs),
                49 => FxFunc49(info.Parameters, a2, funcs),
                _ => throw new ProgramException("Invalid effect func.")
            };
        }
    }
}
