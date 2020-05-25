using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead
{
    public static class Sizes
    {
        public static readonly int Header = Marshal.SizeOf(typeof(Header));
        public static readonly int Bone = Marshal.SizeOf(typeof(Bone));
    }

    public class Model
    {
        public string Name { get; }
        public Header Header { get; }
        public IReadOnlyList<Bone> Bones { get; }
        public IReadOnlyList<Mesh> Meshes { get; }
        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<DisplayList> DisplayLists { get; }

        // count and order match Dlists
        public IReadOnlyList<IReadOnlyList<RenderInstruction>> RenderInstructionLists { get; }

        public IReadOnlyList<Texture> Textures => Recolors[CurrentRecolor].Textures;
        public IReadOnlyList<Palette> Palettes => Recolors[CurrentRecolor].Palettes;
        public IReadOnlyList<IReadOnlyList<TextureData>> TextureData => Recolors[CurrentRecolor].TextureData;
        public IReadOnlyList<IReadOnlyList<PaletteData>> PaletteData => Recolors[CurrentRecolor].PaletteData;

        private int _currentRecolor = 0;
        public int CurrentRecolor
        {
            get
            {
                return _currentRecolor;
            }
            set
            {
                if (_currentRecolor < 0 || _currentRecolor >= Recolors.Count)
                {
                    throw new ArgumentException(nameof(CurrentRecolor));
                }
                _currentRecolor = value;
            }
        }

        public IReadOnlyList<Recolor> Recolors { get; }

        public Model(string name, Header header, IReadOnlyList<Bone> bones, IReadOnlyList<Mesh> meshes,
            IReadOnlyList<Material> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions, IReadOnlyList<Recolor> recolors,
            int defaultRecolor)
        {
            ThrowIfInvalidEnums(materials);
            Name = name;
            Header = header;
            Bones = bones;
            Meshes = meshes;
            Materials = materials;
            DisplayLists = dlists;
            RenderInstructionLists = renderInstructions;
            Recolors = recolors;
            CurrentRecolor = defaultRecolor;
        }

        public IEnumerable<ColorRgba> GetPixels(int textureId, int paletteId)
        {
            return Recolors[CurrentRecolor].GetPixels(textureId, paletteId);
        }

        public void PrintImages(int recolor = 0)
        {
            if (recolor >= 0 && recolor < Recolors.Count)
            {
                Console.WriteLine($"-- {Recolors[recolor].Name} --");
                Console.WriteLine();
                foreach (Material material in Materials.OrderBy(m => m.TextureId).ThenBy(m => m.PaletteId))
                {
                    Console.WriteLine($"tex {material.TextureId} - pal {material.PaletteId}");
                    Console.WriteLine();
                    Texture texture = Recolors[recolor].Textures[material.TextureId];
                    IReadOnlyList<ColorRgba> pixels = Recolors[recolor].GetPixels(material.TextureId, material.PaletteId);
                    for (int i = 0; i < pixels.Count; i++)
                    {
                        ColorRgba pixel = ColorOnBlackBackground(pixels[i]);
                        for (int j = 0; j < 2; j++)
                        {
                            Console.Write($"\u001b[38;2;{pixel.Red};{pixel.Green};{pixel.Blue}m█");
                        }
                        if (i == pixels.Count - 1)
                        {
                            Console.Write("\u001b[0m");
                        }
                        if ((i + 1) % texture.Width == 0)
                        {
                            Console.WriteLine();
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        public void PrintRenderInstructions()
        {
            int i = 0;
            foreach (IReadOnlyList<RenderInstruction> list in RenderInstructionLists)
            {
                Console.WriteLine();
                Console.WriteLine($"Dlist ID {i}:");
                foreach (RenderInstruction instruction in list)
                {
                    Console.WriteLine($"{instruction.Code,-12}\t" +
                        $"{String.Join(", ", instruction.Arguments)}".Trim());
                }
                i++;
            }
        }

        private ColorRgba ColorOnBlackBackground(ColorRgba color)
        {
            float alpha = color.Alpha / 255f;
            return new ColorRgba((byte)(color.Red * alpha), (byte)(color.Green * alpha), (byte)(color.Blue * alpha), 255);
        }

        public void ExportImages()
        {
            string exportPath = Path.Combine(Paths.Export, Name);
            Directory.CreateDirectory(exportPath);
            foreach (Recolor recolor in Recolors)
            {
                string colorPath = Path.Combine(exportPath, recolor.Name);
                Directory.CreateDirectory(colorPath);
                var usedTextures = new HashSet<int>();
                foreach (Material material in Materials.OrderBy(m => m.TextureId).ThenBy(m => m.PaletteId))
                {
                    if (material.TextureId == UInt16.MaxValue)
                    {
                        continue;
                    }
                    Texture texture = recolor.Textures[material.TextureId];
                    IReadOnlyList<ColorRgba> pixels = recolor.GetPixels(material.TextureId, material.PaletteId);
                    if (texture.Width == 0 || texture.Height == 0 || pixels.Count == 0)
                    {
                        continue;
                    }
                    Debug.Assert(texture.Width * texture.Height == pixels.Count);
                    usedTextures.Add(material.TextureId);
                    string filename = $"{material.TextureId}-{material.PaletteId}";
                    SaveTexture(colorPath, filename, texture.Width, texture.Height, pixels);
                }
                if (usedTextures.Count != recolor.Textures.Count)
                {
                    string unusedPath = Path.Combine(colorPath, "unused");
                    Directory.CreateDirectory(unusedPath);
                    for (int t = 0; t < recolor.Textures.Count; t++)
                    {
                        if (usedTextures.Contains(t))
                        {
                            continue;
                        }
                        Texture texture = recolor.Textures[t];
                        for (int p = 0; p < Palettes.Count; p++)
                        {
                            IReadOnlyList<TextureData> textureData = recolor.TextureData[t];
                            IReadOnlyList<PaletteData> palette = recolor.PaletteData[p];
                            if (textureData.Any(t => t.Data >= palette.Count))
                            {
                                continue;
                            }
                            IReadOnlyList<ColorRgba> pixels = recolor.GetPixels(t, p);
                            string filename = $"{t}-{p}";
                            SaveTexture(unusedPath, filename, texture.Width, texture.Height, pixels);
                        }
                    }
                }
            }
        }

        public void ExportPalettes()
        {
            string exportPath = Path.Combine(Paths.Export, Name);
            Directory.CreateDirectory(exportPath);
            foreach (Recolor recolor in Recolors)
            {
                string colorPath = Path.Combine(exportPath, recolor.Name);
                Directory.CreateDirectory(colorPath);
                string palettePath = Path.Combine(colorPath, "palettes");
                Directory.CreateDirectory(palettePath);
                for (int p = 0; p < recolor.Palettes.Count; p++)
                {
                    IReadOnlyList<ColorRgba> pixels = recolor.GetPalettePixels(p);
                    string filename = $"p{p}";
                    SaveTexture(palettePath, filename, 16, 16, pixels);
                }
            }
        }

        private void SaveTexture(string directory, string filename, ushort width, ushort height, IReadOnlyList<ColorRgba> pixels)
        {
            string imagePath = Path.Combine(directory, $"{filename}.png");
            var bitmap = new Bitmap(width, height);
            for (int p = 0; p < pixels.Count; p++)
            {
                ColorRgba pixel = pixels[p];
                bitmap.SetPixel(p % width, p / width,
                    Color.FromArgb(pixel.Alpha, pixel.Red, pixel.Green, pixel.Blue));
            }
            bitmap.Save(imagePath);
        }

        private static void ThrowIfInvalidEnums(IEnumerable<Material> materials)
        {
            foreach (Material material in materials)
            {
                if (!Enum.IsDefined(typeof(RenderMode), material.RenderMode))
                {
                    throw new ProgramException($"Invalid render mode {material.RenderMode}.");
                }
                if (!Enum.IsDefined(typeof(RepeatMode), material.PackedRepeatMode))
                {
                    throw new ProgramException($"Invalid repeat mode {material.PackedRepeatMode}.");
                }
                if (!Enum.IsDefined(typeof(PolygonMode), material.PolygonMode))
                {
                    throw new ProgramException($"Invalid polygon mode {material.PolygonMode}.");
                }
            }
        }
    }

    public class Recolor
    {
        public string Name { get; }
        public IReadOnlyList<Texture> Textures { get; }
        public IReadOnlyList<Palette> Palettes { get; }
        public IReadOnlyList<IReadOnlyList<TextureData>> TextureData { get; }
        public IReadOnlyList<IReadOnlyList<PaletteData>> PaletteData { get; }

        public Recolor(string name, IReadOnlyList<Texture> textures, IReadOnlyList<Palette> palettes,
            IReadOnlyList<IReadOnlyList<TextureData>> textureData, IReadOnlyList<IReadOnlyList<PaletteData>> paletteData)
        {
            ThrowIfInvalidEnums(textures);
            Name = name;
            Textures = textures;
            Palettes = palettes;
            TextureData = textureData;
            PaletteData = paletteData;
            Debug.Assert(Textures.Count == TextureData.Count);
            Debug.Assert(Palettes.Count == PaletteData.Count);
        }

        public IReadOnlyList<ColorRgba> GetPixels(int textureId, int palettteId)
        {
            if (textureId < 0 || textureId >= TextureData.Count)
            {
                throw new ArgumentException(nameof(textureId));
            }
            var pixels = new List<ColorRgba>();
            TextureFormat textureFormat = Textures[textureId].Format;
            if (textureFormat == TextureFormat.DirectRgb || textureFormat == TextureFormat.DirectRgba)
            {
                for (int i = 0; i < TextureData[textureId].Count; i++)
                {
                    uint color = TextureData[textureId][i].Data;
                    byte alpha = TextureData[textureId][i].Alpha;
                    pixels.Add(ColorFromShort(color, alpha));
                }
            }
            else
            {
                if (palettteId < 0 || palettteId >= PaletteData.Count)
                {
                    throw new ArgumentException(nameof(palettteId));
                }
                for (int i = 0; i < TextureData[textureId].Count; i++)
                {
                    int index = (int)TextureData[textureId][i].Data;
                    ushort color = PaletteData[palettteId][index].Data;
                    byte alpha = TextureData[textureId][i].Alpha;
                    pixels.Add(ColorFromShort(color, alpha));
                }
            }
            return pixels;
        }

        public IReadOnlyList<ColorRgba> GetPalettePixels(int palettteId)
        {
            if (palettteId < 0 || palettteId >= PaletteData.Count)
            {
                throw new ArgumentException(nameof(palettteId));
            }
            var pixels = new List<ColorRgba>();
            foreach (PaletteData paletteData in PaletteData[palettteId])
            {
                pixels.Add(ColorFromShort(paletteData.Data, 255));
            }
            return pixels;
        }

        private ColorRgba ColorFromShort(uint value, byte alpha)
        {
            byte red = (byte)(((value >> 0) & 0x1F) << 3);
            byte green = (byte)(((value >> 5) & 0x1F) << 3);
            byte blue = (byte)(((value >> 10) & 0x1F) << 3);
            return new ColorRgba(red, green, blue, alpha);
        }

        private static void ThrowIfInvalidEnums(IEnumerable<Texture> textures)
        {
            foreach (Texture texture in textures)
            {
                if (!Enum.IsDefined(typeof(TextureFormat), texture.Format))
                {
                    throw new ProgramException($"Invalid texture format {texture.Format}.");
                }
            }
        }
    }

    public readonly struct TextureData
    {
        public readonly uint Data;
        public readonly byte Alpha;

        public TextureData(uint data, byte alpha)
        {
            Data = data;
            Alpha = alpha;
        }
    }

    public readonly struct PaletteData
    {
        public readonly ushort Data;

        public PaletteData(ushort data)
        {
            Data = data;
        }
    }

    // size: 4
    public readonly struct Mesh
    {
        public readonly ushort MaterialId;
        public readonly ushort DlistId;
    }

    // size: 32
    public readonly struct DisplayList
    {
        public readonly uint Offset;
        public readonly uint Size;
        // [Q] what are these needed for?
        public readonly int XMinimum;
        public readonly int YMinimum;
        public readonly int ZMinimum;
        public readonly int XMaximum;
        public readonly int YMaximum;
        public readonly int ZMaximum;
    }

    public enum PolygonMode : uint
    {
        Modulate = 0,
        Decal = 1,
        Toon = 2,
        Shadow = 3
    }

    public enum RepeatMode : byte
    {
        Clamp = 0,
        Repeat = 1,
        Mirror = 2
    }

    // Unknown3:
    // - JumpPad_Beam    > blinn6    (tex 0, pal 0)
    // - TeleporterSmall > lambert22 (tex 2, pal 2)
    public enum RenderMode : byte
    {
        Normal = 0,
        Decal = 1,
        Translucent = 2,
        Unknown3 = 3,
        Unknown4 = 4 // ?
    }

    public enum CullingMode : byte
    {
        Neither = 0,
        Front = 1,
        Back = 2
    }

    // size: 132
    public readonly struct Material
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public readonly string Name;
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool Light;
        public readonly CullingMode Culling;
        public readonly byte Alpha;
        public readonly byte Wireframe;
        public readonly ushort PaletteId;
        public readonly ushort TextureId;
        public readonly RepeatMode XRepeat;
        public readonly RepeatMode YRepeat;
        public readonly ColorRgb Diffuse;
        public readonly ColorRgb Ambient;
        public readonly ColorRgb Specular;
        public readonly byte Field53;
        public readonly PolygonMode PolygonMode;
        public readonly RenderMode RenderMode;
        public readonly byte AnimationFlags;
        public readonly ushort Field5A;
        public readonly uint TexcoordTransformMode;
        public readonly ushort TexcoordAnimationId;
        public readonly ushort Field62;
        public readonly uint MatrixId;
        public readonly uint ScaleS;
        public readonly uint ScaleT;
        public readonly ushort RotZ;
        public readonly ushort Field72;
        public readonly uint TranslateS;
        public readonly uint TranslateT;
        public readonly ushort MaterialAnimationId;
        public readonly ushort Field7E;
        public readonly byte PackedRepeatMode;
        public readonly byte Field81;
        public readonly ushort Field82;
    }

    public enum TextureFormat : ushort
    {
        Palette2Bit = 0, // RGB4
        Palette4Bit = 1, // RGB16
        Palette8Bit = 2, // RGB256
        DirectRgb = 3,   // RGB -- not entirely sure if this is RGB or RGBA; the alpha bit is always 1 for format 5
        PaletteA5I3 = 4, // A5I3 
        DirectRgba = 5,  // RGBA
        PaletteA3I5 = 6  // A3I5
    }

    // size: 40
    public readonly struct Texture
    {
        public readonly TextureFormat Format;
        public readonly ushort Width;
        public readonly ushort Height;
        public readonly ushort Padding;
        public readonly uint ImageOffset;
        public readonly uint ImageSize;
        public readonly uint Unknown7;
        public readonly uint Unknown8;
        public readonly uint VramOffset;
        public readonly uint Opaque;
        public readonly uint Unknown11;
        public readonly byte PackedSize;
        public readonly byte NativeTextureFormat;
        public readonly ushort TextureObjRef;
    }

    // size: 16
    public readonly struct Palette
    {
        public readonly uint Offset;
        public readonly uint Count;
        public readonly uint Unknown4;
        public readonly uint UnknownReference5;
    }

    // size: 100
    public readonly struct Header
    {
        public readonly uint ScaleFactor;
        public readonly int ScaleBase;
        public readonly uint Unknown3;
        public readonly uint Unknown4;
        public readonly uint MaterialOffset;
        public readonly uint DlistOffset;
        public readonly uint BoneOffset;
        public readonly ushort NodeAnimationCount;
        public readonly byte Flags;
        public readonly byte Field1F;
        public readonly uint UnknownNodeId;
        public readonly uint MeshOffset;
        public readonly ushort TextureCount;
        public readonly ushort Field2A;
        public readonly uint TextureOffset;
        public readonly ushort PaletteCount;
        public readonly ushort Field32;
        public readonly uint PaletteOffset;
        public readonly uint UnknownAnimationCount;
        public readonly uint Unknown8;
        public readonly uint NodeInitialPosition;
        public readonly uint NodePosition;
        public readonly ushort MaterialCount;
        public readonly ushort BoneCount;
        public readonly uint TextureMatrices;
        public readonly uint NodeAnimation;
        public readonly uint TextureCoordinateAnimations;
        public readonly uint MaterialAnimations;
        public readonly uint TextureAnimations;
        public readonly ushort MeshCount;
        public readonly ushort MatrixCount;
    }

    // size: 4
    public readonly struct Float
    {
        public Float(int intValue)
        {
            Value = intValue / (float)(1 << 12);
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

    // size: ?
    public readonly struct Bone
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public readonly string Name;
        public readonly ushort ParentId;
        public readonly ushort ChildId;
        public readonly ushort NextId;
        public readonly ushort Field46;
        public readonly uint Enabled;
        public readonly ushort MeshCount;
        public readonly ushort MeshId;
        public readonly Vector3 Scale;
        public readonly short AngleX;
        public readonly short AngleY;
        public readonly short AngleZ;
        public readonly ushort Field62;
        public readonly Vector3 Position;
        public readonly uint Field70;
        public readonly Vector3 Vector1;
        public readonly Vector3 Vector2;
        public readonly byte Type;
        public readonly byte Field8D;
        public readonly ushort Field8E;
        public readonly Vector3 NodeTransform0;
        public readonly Vector3 NodeTransform1;
        public readonly Vector3 NodeTransform2;
        public readonly Vector3 NodeTransform3;
        public readonly uint FieldC0;
        public readonly uint FieldC4;
        public readonly uint FieldC8;
        public readonly uint FieldCC;
        public readonly uint FieldD0;
        public readonly uint FieldD4;
        public readonly uint FieldD8;
        public readonly uint FieldDC;
        public readonly uint FieldE0;
        public readonly uint FieldE4;
        public readonly uint FieldE8;
        public readonly uint FieldEC;
    }

    public enum InstructionCode : uint
    {
        NOP = 0x400,
        MTX_RESTORE = 0x450,
        COLOR = 0x480,
        NORMAL = 0x484,
        TEXCOORD = 0x488,
        VTX_16 = 0x48C,
        VTX_10 = 0x490,
        VTX_XY = 0x494,
        VTX_XZ = 0x498,
        VTX_YZ = 0x49C,
        VTX_DIFF = 0x4A0,
        DIF_AMB = 0x4C0,
        BEGIN_VTXS = 0x500,
        END_VTXS = 0x504
    }

    public class RenderInstruction
    {
        public RenderInstruction(InstructionCode code, params uint[] arguments)
        {
            if (arguments.Length != GetArity(code))
            {
                throw new ProgramException($"Incorrect number of arguments for code {code}.");
            }
            Code = code;
            Arguments = arguments.ToList();
        }

        public InstructionCode Code { get; }
        public IReadOnlyList<uint> Arguments { get; }

        private static readonly IReadOnlyDictionary<InstructionCode, int> _arityMap =
            new Dictionary<InstructionCode, int>()
            {
                { InstructionCode.NOP, 0 },
                { InstructionCode.MTX_RESTORE, 1 },
                { InstructionCode.COLOR, 1 },
                { InstructionCode.NORMAL, 1 },
                { InstructionCode.TEXCOORD, 1 },
                { InstructionCode.VTX_16, 2 },
                { InstructionCode.VTX_10, 1 },
                { InstructionCode.VTX_XY, 1 },
                { InstructionCode.VTX_XZ, 1 },
                { InstructionCode.VTX_YZ, 1 },
                { InstructionCode.VTX_DIFF, 1 },
                { InstructionCode.DIF_AMB, 1 },
                { InstructionCode.BEGIN_VTXS, 1 },
                { InstructionCode.END_VTXS, 0 }
            };

        public static int GetArity(InstructionCode code)
        {
            if (!Enum.IsDefined(typeof(InstructionCode), code))
            {
                throw new ProgramException($"Invalid code arity {code}");
            }
            Debug.Assert(_arityMap.ContainsKey(code));
            return _arityMap[code];
        }
    }

    public static class Paths
    {
        public static string FileSystem => _paths.Value.FileSystem;
        public static string Export => _paths.Value.Export;

        private static readonly Lazy<(string FileSystem, string Export)> _paths
            = new Lazy<(string, string)>(() =>
        {
            if (File.Exists("paths.txt"))
            {
                string[] lines = File.ReadAllLines("paths.txt");
                return (lines[0], lines[1]);
            }
            return ("", "");
        });
    }
    
    public static class CollectionExtensions
    {
        public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            int index = 0;
            foreach (TSource item in source)
            {
                if (predicate.Invoke(item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }
    }
}
