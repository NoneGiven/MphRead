using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Effects
{
    // todo: set_vecs and draw functions
    public static class Effects
    {
        // todo: create "eff4" class
        // todo: pass "cur_*" stuff as params and remove globals
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

        // todo: use Fixed everywhere instead of int
        public static float FxFunc41(IReadOnlyList<Fixed> param, Fixed elapsed, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            // todo: pass the lifespan
            float lifespan = 15;
            float percent = elapsed.FloatValue / lifespan;
            if (percent < param[0].FloatValue)
            {
                return param[1].FloatValue;
            }
            bool none = true;
            int i = 0;
            do
            {
                if (param[i].FloatValue > percent)
                {
                    break;
                }
                none = false;
                i += 2;
            }
            while (param[i].Value != Int32.MinValue);
            if (none)
            {
                return 0;
            }
            i -= 2;
            if (param[i + 2].Value == Int32.MinValue)
            {
                return param[i + 1].FloatValue;
            }
            return param[i + 1].FloatValue + (param[i + 3].FloatValue - param[i + 1].FloatValue)
                * ((percent - param[i].FloatValue) / (param[i + 2].FloatValue - param[i].FloatValue));
        }

        private static float FxFunc42(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return Fixed.ToFloat(param[0]);
        }

        private static float FxFunc43(IReadOnlyList<int> param, int a2, IReadOnlyDictionary<uint, FxFuncInfo> funcs)
        {
            return Fixed.ToFloat(Test.GetRandomInt1(4096));
        }

        // get random angle [0-360) in fx32
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
                FxFunc8(info.Parameters, a2, ref vec, funcs);
                break;
            case 9:
                FxFunc9(info.Parameters, a2, ref vec, funcs);
                break;
            case 11:
                FxFunc11(info.Parameters, a2, ref vec, funcs);
                break;
            case 14:
                FxFunc14(info.Parameters, a2, ref vec, funcs);
                break;
            case 15:
                FxFunc15(info.Parameters, a2, ref vec, funcs);
                break;
            case 16:
                FxFunc16(info.Parameters, a2, ref vec, funcs);
                break;
            case 17:
                FxFunc17(info.Parameters, a2, ref vec, funcs);
                break;
            case 19:
                FxFunc19(info.Parameters, a2, ref vec, funcs);
                break;
            case 20:
                FxFunc20(info.Parameters, a2, ref vec, funcs);
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
                41 => FxFunc41(info.Parameters.Select(s => new Fixed(s)).ToList(), new Fixed(a2), funcs),
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
