using System;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead.Testing
{
    public static class TestParse
    {
        public static void TestMatrix()
        {
            var field58 = new Vector3(0, 0, 1);
            var field64 = new Vector3(0, 1, 0);
            var field70 = new Vector3(-1, 0, 0);
            Matrix3 mat1 = TestVectors(field58, field64, field70);
            Nop();
            field58 = new Vector3(1, 0, 0);
            field64 = new Vector3(0, 1, 0);
            field70 = new Vector3(0, 0, 1);
            Matrix3 mat2 = TestVectors(field58, field64, field70);
            Quaternion quat1 = new Matrix4(mat1).ExtractRotation();
            Vector3 rot1 = quat1.ToEulerAngles();
            rot1 = new Vector3(MathHelper.RadiansToDegrees(rot1.X), MathHelper.RadiansToDegrees(rot1.Y), MathHelper.RadiansToDegrees(rot1.Z));
            Quaternion quat2 = new Matrix4(mat2).ExtractRotation();
            Vector3 rot2 = quat2.ToEulerAngles();
            rot2 = new Vector3(MathHelper.RadiansToDegrees(rot2.X), MathHelper.RadiansToDegrees(rot2.Y), MathHelper.RadiansToDegrees(rot2.Z));
            Nop();
        }

        public static void TestMatrices()
        {
            // 0x020DB528 (passed to draw_animated_model from CModel_draw from draw_player)
            // updated in sub_201DCE4 -- I guess it's just the model transform?
            Matrix4x3 mtx1 = ParseMatrix48("03 F0 FF FF 00 00 00 00 9C 00 00 00 F9 FF FF FF FB 0F 00 00 3E FF FF FF 64 FF FF FF 3E FF FF FF 08 F0 FF FF 22 00 00 00 86 40 00 00 F1 AD FD FF");
            // 0x220DA430 (constant?)
            Matrix4x3 mtx2 = ParseMatrix48("FD 0F 00 00 D3 FF FF FF 97 00 00 00 00 00 00 00 53 0F 00 00 9B 04 00 00 62 FF FF FF 66 FB FF FF 50 0F 00 00 F4 E8 FF FF DA 0B FF FF BF F8 01 00");
            // concatenation result
            Matrix4x3 currentTextureMatrix = ParseMatrix48("FF EF FF FF 00 00 00 00 FE FF FF FF 00 00 00 00 86 0F 00 00 DF 03 00 00 01 00 00 00 DF 03 00 00 7A F0 FF FF 00 00 00 00 7F F4 FF FF CA D2 FF FF");
            Matrix4x3 mult = Matrix.Concat43(mtx1, mtx2);

            var trans = new Matrix4(
                new Vector4(mtx1.Row0, 0.0f),
                new Vector4(mtx1.Row1, 0.0f),
                new Vector4(mtx1.Row2, 0.0f),
                new Vector4(mtx1.Row3, 1.0f)
            );
            Vector3 pos = trans.ExtractTranslation();
            Vector3 rot = trans.ExtractRotation().ToEulerAngles();
            rot = new Vector3(
                MathHelper.RadiansToDegrees(rot.X),
                MathHelper.RadiansToDegrees(rot.Y),
                MathHelper.RadiansToDegrees(rot.Z)
            );
            Vector3 scale = trans.ExtractScale();
        }

        public static Matrix3 TestVectors(Vector3 field58, Vector3 field64, Vector3 field70)
        {
            var field4F4 = new Matrix3(
                new Vector3(field58.X, 0, field58.Z),
                new Vector3(field64.X, field64.Y, field64.Z),
                new Vector3(field70.X, 0, field70.Y)
            );

            field4F4.Row2 = Vector3.Cross(field4F4.Row0, field4F4.Row1);
            field4F4.Row1 = Vector3.Cross(field4F4.Row2, field4F4.Row0);
            field4F4.Row0 = field4F4.Row0.Normalized();
            field4F4.Row1 = field4F4.Row1.Normalized();
            field4F4.Row2 = field4F4.Row2.Normalized();

            return field4F4;
        }

        public static Vector3 ParseVector3(string values)
        {
            string[] split = values.Split(' ');
            if (split.Length != 12 || split.Any(s => s.Length != 2))
            {
                throw new ArgumentException(nameof(values));
            }
            return new Vector3(
                Int32.Parse(split[3] + split[2] + split[1] + split[0], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(split[7] + split[6] + split[5] + split[4], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(split[11] + split[10] + split[9] + split[8], System.Globalization.NumberStyles.HexNumber) / 4096f
            );
        }

        public static Matrix4x3 ParseMatrix12(params string[] values)
        {
            if (values.Length != 12 || values.Any(v => v.Length != 8))
            {
                throw new ArgumentException(nameof(values));
            }
            return new Matrix4x3(
                Int32.Parse(values[0], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[1], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[2], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[3], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[4], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[5], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[6], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[7], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[8], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[9], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[10], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[11], System.Globalization.NumberStyles.HexNumber) / 4096f
            );
        }

        public static Matrix4 ParseMatrix16(params string[] values)
        {
            if (values.Length != 16 || values.Any(v => v.Length != 8))
            {
                throw new ArgumentException(nameof(values));
            }
            return new Matrix4(
                Int32.Parse(values[0], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[1], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[2], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[3], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[4], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[5], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[6], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[7], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[8], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[9], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[10], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[11], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[12], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[13], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[14], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[15], System.Globalization.NumberStyles.HexNumber) / 4096f
            );
        }

        public static Matrix4 ParseMatrix16(string value)
        {
            return ParseMatrix16(value.Split(' '));
        }

        public static Matrix4x3 ParseMatrix48(string value)
        {
            string[] values = value.Split(' ');
            if (values.Length != 48 || values.Any(v => v.Length != 2))
            {
                throw new ArgumentException(nameof(values));
            }
            return ParseMatrix12(
                values[3] + values[2] + values[1] + values[0],
                values[7] + values[6] + values[5] + values[4],
                values[11] + values[10] + values[9] + values[8],
                values[15] + values[14] + values[13] + values[12],
                values[19] + values[18] + values[17] + values[16],
                values[23] + values[22] + values[21] + values[20],
                values[27] + values[26] + values[25] + values[24],
                values[31] + values[30] + values[29] + values[28],
                values[35] + values[34] + values[33] + values[32],
                values[39] + values[38] + values[37] + values[36],
                values[43] + values[42] + values[41] + values[40],
                values[47] + values[46] + values[45] + values[44]
            );
        }

        public static Matrix4 ParseMatrix64(string value)
        {
            string[] values = value.Split(' ');
            if (values.Length != 64 || values.Any(v => v.Length != 2))
            {
                throw new ArgumentException(nameof(values));
            }
            return ParseMatrix16(
                values[3] + values[2] + values[1] + values[0],
                values[7] + values[6] + values[5] + values[4],
                values[11] + values[10] + values[9] + values[8],
                values[15] + values[14] + values[13] + values[12],
                values[19] + values[18] + values[17] + values[16],
                values[23] + values[22] + values[21] + values[20],
                values[27] + values[26] + values[25] + values[24],
                values[31] + values[30] + values[29] + values[28],
                values[35] + values[34] + values[33] + values[32],
                values[39] + values[38] + values[37] + values[36],
                values[43] + values[42] + values[41] + values[40],
                values[47] + values[46] + values[45] + values[44],
                values[51] + values[50] + values[49] + values[48],
                values[55] + values[54] + values[53] + values[52],
                values[59] + values[58] + values[57] + values[56],
                values[63] + values[62] + values[61] + values[60]
            );
        }

        private static void Nop()
        {
        }
    }
}
