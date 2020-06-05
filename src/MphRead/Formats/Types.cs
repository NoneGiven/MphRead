using OpenToolkit.Mathematics;

namespace MphRead
{
    // size: 4
    public readonly struct Fixed
    {
        public int Value { get; }

        public float FloatValue => ToFloat(Value);

        public static float ToFloat(int value)
        {
            return value / (float)(1 << 12);
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
    }

    // size: 4
    public readonly struct ColorRgba
    {
        public ColorRgba(byte red, byte green, byte blue, byte alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        public readonly byte Red;
        public readonly byte Green;
        public readonly byte Blue;
        public readonly byte Alpha;
    }
}
