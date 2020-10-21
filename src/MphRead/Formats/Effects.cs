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

        private static void FxFunc4(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
            vec.X = param[0];
            vec.Y = param[1];
            vec.Z = param[2];
        }

        private static void FxFunc8(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Y = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
        }

        private static void FxFunc9(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
            vec.X = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
            vec.Y = 0;
            vec.Z = Fixed.ToFloat(Test.GetRandomInt1(4096) - 2048);
        }

        private static void FxFunc11(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
            // todo: sin/cos stuff with dword_2123DE8
        }

        private static void FxFunc14(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static void FxFunc15(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static void FxFunc16(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static void FxFunc17(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static void FxFunc19(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static void FxFunc20(IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
        }

        private static int FxFunc22(IReadOnlyList<int> param, int a2)
        {
            // todo: return cur_effect_elem->field_74;
            return 0;
        }

        public static void InvokeVecFunc(int fxFunc, IReadOnlyList<int> param, int a2, ref Vector3 vec)
        {
            switch (fxFunc)
            {
            case 4:
                FxFunc4(param, a2, ref vec);
                break;
            case 8:
                FxFunc4(param, a2, ref vec);
                break;
            case 9:
                FxFunc4(param, a2, ref vec);
                break;
            case 11:
                FxFunc4(param, a2, ref vec);
                break;
            case 14:
                FxFunc4(param, a2, ref vec);
                break;
            case 15:
                FxFunc4(param, a2, ref vec);
                break;
            case 16:
                FxFunc4(param, a2, ref vec);
                break;
            case 17:
                FxFunc4(param, a2, ref vec);
                break;
            case 19:
                FxFunc4(param, a2, ref vec);
                break;
            case 20:
                FxFunc4(param, a2, ref vec);
                break;
            default:
                throw new ProgramException("Invalid effect func.");
            }
        }

        public static int InvokeIntFunc(int fxFunc, IReadOnlyList<int> param, int a2)
        {
            return fxFunc switch
            {
                22 => FxFunc22(param, a2),
                24 => FxFunc22(param, a2),
                25 => FxFunc22(param, a2),
                26 => FxFunc22(param, a2),
                29 => FxFunc22(param, a2),
                31 => FxFunc22(param, a2),
                32 => FxFunc22(param, a2),
                35 => FxFunc22(param, a2),
                40 => FxFunc22(param, a2),
                41 => FxFunc22(param, a2),
                42 => FxFunc22(param, a2),
                43 => FxFunc22(param, a2),
                45 => FxFunc22(param, a2),
                46 => FxFunc22(param, a2),
                47 => FxFunc22(param, a2),
                48 => FxFunc22(param, a2),
                49 => FxFunc22(param, a2),
                _ => throw new ProgramException("Invalid effect func.")
            };
        }
    }
}
