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

        public static Vector3 operator/(ColorRgb left, float right)
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
