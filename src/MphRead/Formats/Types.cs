namespace MphRead
{
    // size: 4
    public readonly struct Float
    {
        public static float FromFixed(int intValue)
        {
            return intValue / (float)(1 << 12);
        }

        public Float(int intValue)
        {
            Value = FromFixed(intValue);
        }

        public Float(double floatValue)
        {
            Value = (float)(floatValue > 0
                ? (floatValue) * (1 << 12) + 0.5f
                : (floatValue) * (1 << 12) - 0.5f);
        }

        public Float(float floatValue)
        {
            Value = floatValue > 0
                ? (floatValue) * (1 << 12) + 0.5f
                : (floatValue) * (1 << 12) - 0.5f;
        }

        public float Value { get; }
    }

    // size: 12
    public readonly struct Vector3
    {
        public Vector3(Float x, Float y, Float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3(int x, int y, int z)
        {
            X = new Float(x);
            Y = new Float(y);
            Z = new Float(z);
        }

        public Vector3(double x, double y, double z)
        {
            X = new Float(x);
            Y = new Float(y);
            Z = new Float(z);
        }

        public Vector3(float x, float y, float z)
        {
            X = new Float(x);
            Y = new Float(y);
            Z = new Float(z);
        }

        public readonly Float X;
        public readonly Float Y;
        public readonly Float Z;
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
