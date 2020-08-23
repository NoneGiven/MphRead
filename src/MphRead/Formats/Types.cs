using System;
using OpenToolkit.Mathematics;

namespace MphRead
{
    // size: 4
    public readonly struct Fixed
    {
        public readonly int Value;

        public float FloatValue => ToFloat(Value);

        public static float ToFloat(long value)
        {
            return value / (float)(1 << 12);
        }

        public static float ToFloat(int value)
        {
            return value / (float)(1 << 12);
        }

        public static float ToFloat(string value)
        {
            return ToFloat(Int32.Parse(value, System.Globalization.NumberStyles.HexNumber));
        }

        public override string? ToString()
        {
            return Value.ToString();
        }
    }

    // size: 12
    public readonly struct Vector3Fx
    {
        public readonly Fixed X;
        public readonly Fixed Y;
        public readonly Fixed Z;

        public Vector3 ToFloatVector()
        {
            return new Vector3(X.FloatValue, Y.FloatValue, Z.FloatValue);
        }
    }

    // size: 16
    public readonly struct Vector4Fx
    {
        public readonly Fixed X;
        public readonly Fixed Y;
        public readonly Fixed Z;
        public readonly Fixed W;

        public Vector4 ToFloatVector()
        {
            return new Vector4(X.FloatValue, Y.FloatValue, Z.FloatValue, W.FloatValue);
        }
    }

    // size: 48
    public readonly struct Matrix43Fx
    {
        public readonly Vector3Fx One;
        public readonly Vector3Fx Two;
        public readonly Vector3Fx Three;
        public readonly Vector3Fx Four;
    }

    // size: 64
    public readonly struct Matrix44Fx
    {
        public readonly Vector4Fx One;
        public readonly Vector4Fx Two;
        public readonly Vector4Fx Three;
        public readonly Vector4Fx Four;

        public Matrix4 ToFloatMatrix()
        {
            return new Matrix4(One.ToFloatVector(), Two.ToFloatVector(), Three.ToFloatVector(), Four.ToFloatVector());
        }
    }

    public static class Matrix
    {
        public static Matrix4x3 Concat43(Matrix4x3 first, Matrix4x3 second)
        {
            Matrix4x3 output = Matrix4x3.Zero;
            output.M11 = first.M13 * second.M31 + first.M11 * second.M11 + first.M12 * second.M21;
            output.M12 = first.M13 * second.M32 + first.M11 * second.M12 + first.M12 * second.M22;
            output.M13 = first.M13 * second.M33 + first.M11 * second.M13 + first.M12 * second.M23;
            output.M21 = first.M23 * second.M31 + first.M21 * second.M11 + first.M22 * second.M21;
            output.M22 = first.M23 * second.M32 + first.M21 * second.M12 + first.M22 * second.M22;
            output.M23 = first.M23 * second.M33 + first.M21 * second.M13 + first.M22 * second.M23;
            output.M31 = first.M33 * second.M31 + first.M31 * second.M11 + first.M32 * second.M21;
            output.M32 = first.M33 * second.M32 + first.M31 * second.M12 + first.M32 * second.M22;
            output.M33 = first.M33 * second.M33 + first.M31 * second.M13 + first.M32 * second.M23;
            output.M41 = second.M41 + first.M43 * second.M31 + first.M41 * second.M11 + first.M42 * second.M21;
            output.M42 = second.M42 + first.M43 * second.M32 + first.M41 * second.M12 + first.M42 * second.M22;
            output.M43 = second.M43 + first.M43 * second.M33 + first.M41 * second.M13 + first.M42 * second.M23;
            return output;
        }

        // todo: could replace this with "keep 3x2"
        public static Matrix4 Multiply44(Matrix4 first, Matrix4 second)
        {
            Matrix4 output = Matrix4.Zero;
            output.M11 = first.M13 * second.M31 + first.M11 * second.M11 + first.M12 * second.M21;
            output.M12 = first.M13 * second.M32 + first.M11 * second.M12 + first.M12 * second.M22;
            output.M21 = first.M23 * second.M31 + first.M21 * second.M11 + first.M22 * second.M21;
            output.M22 = first.M23 * second.M32 + first.M21 * second.M12 + first.M22 * second.M22;
            output.M31 = first.M33 * second.M31 + first.M31 * second.M11 + first.M32 * second.M21;
            output.M32 = first.M33 * second.M32 + first.M31 * second.M12 + first.M32 * second.M22;
            return output;
        }

        public static Matrix3 RotateAlign(Vector3 from, Vector3 to)
        {
            var axis = Vector3.Cross(from, to);
            float cos = Vector3.Dot(from, to);
            float k = 1.0f / (1.0f + cos);
            return new Matrix3(
                (axis.X * axis.X * k) + cos,
                (axis.Z * axis.X * k) - axis.Z,
                (axis.Z * axis.X * k) + axis.Y,
                (axis.X * axis.Y * k) + axis.Z,
                (axis.Y * axis.Y * k) + cos,
                (axis.Z * axis.Y * k) - axis.X,
                (axis.X * axis.Z * k) - axis.Y,
                (axis.Y * axis.Z * k) + axis.X,
                (axis.Z * axis.Z * k) + cos
            );
        }
    }

    // size: 3
    public readonly struct ColorRgb
    {
        public readonly byte Red;
        public readonly byte Green;
        public readonly byte Blue;

        public ColorRgb(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public Vector3 AsVector3()
        {
            return new Vector3(Red / 255f, Green / 255f, Blue / 255f);
        }

        public Vector4 AsVector4(float alpha = 1f)
        {
            return new Vector4(Red / 255f, Green / 255f, Blue / 255f, alpha);
        }

        public static Vector3 operator /(ColorRgb left, float right)
        {
            return new Vector3(left.Red / right, left.Green / right, left.Blue / right);
        }
    }

    // size: 4
    public readonly struct ColorRgba
    {
        public readonly byte Red;
        public readonly byte Green;
        public readonly byte Blue;
        public readonly byte Alpha;

        public ColorRgba(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public ColorRgba WithAlpha(byte alpha)
        {
            return new ColorRgba(Red, Green, Blue, alpha);
        }
    }

    public static class TypeExtensions
    {
        public static Vector3 WithX(this Vector3 vector, float x)
        {
            return new Vector3(x, vector.Y, vector.Z);
        }

        public static Vector3 WithY(this Vector3 vector, float y)
        {
            return new Vector3(vector.X, y, vector.Z);
        }

        public static Vector3 WithZ(this Vector3 vector, float z)
        {
            return new Vector3(vector.X, vector.Y, z);
        }

        public static Matrix3 AsMatrix3(this Matrix4x3 matrix)
        {
            return new Matrix3(matrix.Row0, matrix.Row1, matrix.Row2);
        }

        public static Matrix4 AsMatrix4(this Matrix4x3 matrix)
        {
            return new Matrix4(
                new Vector4(matrix.Row0),
                new Vector4(matrix.Row1),
                new Vector4(matrix.Row2),
                new Vector4(matrix.Row3)
            );
        }

        public static Matrix4 Keep3x3(this Matrix4x3 matrix)
        {
            return new Matrix4(
                new Vector4(matrix.Row0, 0.0f),
                new Vector4(matrix.Row1, 0.0f),
                new Vector4(matrix.Row2, 0.0f),
                Vector4.Zero
            );
        }

        public static Matrix4 Keep3x3(this Matrix4 matrix)
        {
            return new Matrix4(
                new Vector4(matrix.Row0.Xyz, 0.0f),
                new Vector4(matrix.Row1.Xyz, 0.0f),
                new Vector4(matrix.Row2.Xyz, 0.0f),
                Vector4.Zero
            );
        }
    }
}
