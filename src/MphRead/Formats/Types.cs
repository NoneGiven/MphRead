using System;
using OpenTK.Mathematics;

namespace MphRead
{
    public readonly struct LightInfo
    {
        public readonly Vector3 Light1Vector;
        public readonly Vector3 Light1Color;
        public readonly Vector3 Light2Vector;
        public readonly Vector3 Light2Color;

        public static readonly LightInfo Zero = new LightInfo(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero);

        public LightInfo(Vector3 light1Vector, Vector3 light1Color, Vector3 light2Vector, Vector3 light2Color)
        {
            Light1Vector = light1Vector;
            Light1Color = light1Color;
            Light2Vector = light2Vector;
            Light2Color = light2Color;
        }
    }

    public enum RenderItemType
    {
        // box/cylinder/sphere must be 1/2/3
        Mesh = 0,
        Box = 1,
        Cylinder = 2,
        Sphere = 3,
        Triangle = 4,
        Quad = 5,
        Ngon = 6,
        Particle = 7,
        TrailSingle = 8,
        TrailMulti = 9,
        TrailStack = 10
    }

    public class RenderItem
    {
        public RenderItemType Type { get; set; }
        public int PolygonId { get; set; }
        public float Alpha { get; set; }
        public PolygonMode PolygonMode { get; set; }
        public RenderMode RenderMode { get; set; }
        public CullingMode CullingMode { get; set; }
        public bool Wireframe { get; set; }
        public bool Lighting { get; set; }
        public Vector3 Diffuse { get; set; }
        public Vector3 Ambient { get; set; }
        public Vector3 Specular { get; set; }
        public Vector3 Emission { get; set; }
        public LightInfo LightInfo { get; set; }
        public TexgenMode TexgenMode { get; set; }
        public RepeatMode XRepeat { get; set; }
        public RepeatMode YRepeat { get; set; }
        public bool HasTexture { get; set; }
        public int TextureBindingId { get; set; }
        public Matrix4 TexcoordMatrix { get; set; }
        public Matrix4 Transform { get; set; }
        public int ListId { get; set; }
        public int MatrixStackCount { get; set; }
        public float[] MatrixStack { get; }
        public Vector4? OverrideColor { get; set; }
        public Vector4? PaletteOverride { get; set; }
        public Vector3[] Points { get; set; }
        // number of segments for morph ball trail, or total for other multi-segment trails
        public int ItemCount { get; set; }
        public float ScaleS { get; set; }
        public float ScaleT { get; set; }

        public RenderItem()
        {
            // todo: consider using ArrayPool
            MatrixStack = new float[16 * 31];
            Points = Array.Empty<Vector3>();
        }
    }

    // size: 4
    public readonly struct Fixed
    {
        public readonly int Value;

        public float FloatValue => ToFloat(Value);

        public Fixed(int value)
        {
            Value = value;
        }

        public static float ToFloat(long value)
        {
            return value / (float)(1 << 12);
        }

        public static float ToFloat(uint value)
        {
            return (int)value / (float)(1 << 12);
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

        public Vector3Fx(int x, int y, int z)
        {
            X = new Fixed(x);
            Y = new Fixed(y);
            Z = new Fixed(z);
        }

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
        public static Matrix3 GetTransform3(Vector3 vector1, Vector3 vector2)
        {
            Vector3 up = Vector3.Cross(vector2, vector1).Normalized();
            var direction = Vector3.Cross(vector1, up);

            Matrix3 transform = default;

            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;

            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;

            transform.M31 = vector1.X;
            transform.M32 = vector1.Y;
            transform.M33 = vector1.Z;

            return transform;
        }

        public static Matrix4 GetTransform4(Vector3 vector1, Vector3 vector2, Vector3 position)
        {
            Vector3 up = Vector3.Cross(vector2, vector1).Normalized();
            var direction = Vector3.Cross(vector1, up);

            Matrix4 transform = default;

            transform.M11 = up.X;
            transform.M12 = up.Y;
            transform.M13 = up.Z;

            transform.M21 = direction.X;
            transform.M22 = direction.Y;
            transform.M23 = direction.Z;

            transform.M31 = vector1.X;
            transform.M32 = vector1.Y;
            transform.M33 = vector1.Z;

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;
            transform.M44 = 1;

            return transform;
        }

        public static Matrix4 GetTransformSRT(Vector3 scale, Vector3 angle, Vector3 position)
        {
            float sinAx = MathF.Sin(angle.X);
            float sinAy = MathF.Sin(angle.Y);
            float sinAz = MathF.Sin(angle.Z);
            float cosAx = MathF.Cos(angle.X);
            float cosAy = MathF.Cos(angle.Y);
            float cosAz = MathF.Cos(angle.Z);

            float v18 = cosAx * cosAz;
            float v19 = cosAx * sinAz;
            float v20 = cosAx * cosAy;

            float v22 = sinAx * sinAy;

            float v17 = v19 * sinAy;

            Matrix4 transform = default;

            transform.M11 = scale.X * cosAy * cosAz;
            transform.M12 = scale.X * cosAy * sinAz;
            transform.M13 = scale.X * -sinAy;

            transform.M21 = scale.Y * ((v22 * cosAz) - v19);
            transform.M22 = scale.Y * ((v22 * sinAz) + v18);
            transform.M23 = scale.Y * sinAx * cosAy;

            transform.M31 = scale.Z * (v18 * sinAy + sinAx * sinAz);
            transform.M32 = scale.Z * (v17 + (v19 * sinAy) - (sinAx * cosAz));
            transform.M33 = scale.Z * v20;

            transform.M41 = position.X;
            transform.M42 = position.Y;
            transform.M43 = position.Z;

            transform.M14 = 0;
            transform.M24 = 0;
            transform.M34 = 0;
            transform.M44 = 1;

            return transform;
        }

        public static Vector3 Vec3MultMtx4(Vector3 vec, Matrix4 mat)
        {
            Vector3 result = Vector3.Zero;
            result.X = (vec.X * mat.Row0.X) + (vec.Y * mat.Row1.X) + (vec.Z * mat.Row2.X) + mat.Row3.X;
            result.Y = (vec.X * mat.Row0.Y) + (vec.Y * mat.Row1.Y) + (vec.Z * mat.Row2.Y) + mat.Row3.Y;
            result.Z = (vec.X * mat.Row0.Z) + (vec.Y * mat.Row1.Z) + (vec.Z * mat.Row2.Z) + mat.Row3.Z;
            return result;
        }

        public static Vector3 Vec3MultMtx3(Vector3 vec, Matrix4 mat)
        {
            Vector3 result = Vector3.Zero;
            result.X = (vec.X * mat.Row0.X) + (vec.Y * mat.Row1.X) + (vec.Z * mat.Row2.X);
            result.Y = (vec.X * mat.Row0.Y) + (vec.Y * mat.Row1.Y) + (vec.Z * mat.Row2.Y);
            result.Z = (vec.X * mat.Row0.Z) + (vec.Y * mat.Row1.Z) + (vec.Z * mat.Row2.Z);
            return result;
        }

        public static Vector3 Vec4MultMtx4x3(Vector4 vec, Matrix4x3 mat)
        {
            Vector3 result = Vector3.Zero;
            result.X = vec.W * mat.M41 + vec.Z * mat.M31 + vec.X * mat.M11 + vec.Y * mat.M21;
            result.Y = vec.W * mat.M42 + vec.Z * mat.M32 + vec.X * mat.M12 + vec.Y * mat.M22;
            result.Z = vec.W * mat.M43 + vec.Z * mat.M33 + vec.X * mat.M13 + vec.Y * mat.M23;
            return result;
        }

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

        public ColorRgba(uint value, byte alpha = 255)
        {
            Red = (byte)MathF.Round(((value >> 0) & 0x1F) / 31f * 255f);
            Green = (byte)MathF.Round(((value >> 5) & 0x1F) / 31f * 255f);
            Blue = (byte)MathF.Round(((value >> 10) & 0x1F) / 31f * 255f);
            Alpha = alpha;
        }

        public ColorRgba WithAlpha(byte alpha)
        {
            return new ColorRgba(Red, Green, Blue, alpha);
        }

        public uint ToUint()
        {
            return (uint)((Red << 0) | (Green << 8) | (Blue << 16) | (Alpha << 24));
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

        public static Vector3 AddX(this Vector3 vector, float x)
        {
            return new Vector3(vector.X + x, vector.Y, vector.Z);
        }

        public static Vector3 AddY(this Vector3 vector, float y)
        {
            return new Vector3(vector.X, vector.Y + y, vector.Z);
        }

        public static Vector3 AddZ(this Vector3 vector, float z)
        {
            return new Vector3(vector.X, vector.Y, vector.Z + z);
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

    public static class MarshalExtensions
    {
        public static string MarshalString(this char[] array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            string result = new string(array);
            int index = result.IndexOf('\0');
            if (index != -1)
            {
                return result.Substring(0, index);
            }
            return result;
        }
    }
}
