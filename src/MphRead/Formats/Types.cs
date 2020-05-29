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
    }

    // size: 48
    public readonly struct Matrix43Fx
    {
        public readonly Vector3Fx One;
        public readonly Vector3Fx Two;
        public readonly Vector3Fx Three;
        public readonly Vector3Fx Four;
    }

    // todo: replace with built-in types
    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(double x, double y, double z)
        {
            X = (float)x;
            Y = (float)y;
            Z = (float)z;
        }

        public Vector3(Vector3Fx vector)
        {
            X = vector.X.FloatValue;
            Y = vector.Y.FloatValue;
            Z = vector.Z.FloatValue;
        }
    }

    public struct Vector4
    {
        public float A;
        public float B;
        public float C;
        public float D;
    }

    public struct Matrix43
    {
        public Vector3 One;
        public Vector3 Two;
        public Vector3 Three;
        public Vector3 Four;

        public Matrix43(Matrix43Fx matrix)
        {
            One = new Vector3(matrix.One);
            Two = new Vector3(matrix.Two);
            Three = new Vector3(matrix.Three);
            Four = new Vector3(matrix.Four);
        }
    }

    // size: 3
    public readonly struct ColorRgb
    {
        public ColorRgb(byte red, byte green, byte blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public readonly byte Red;
        public readonly byte Green;
        public readonly byte Blue;
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
