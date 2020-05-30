using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public class Model
    {
        public ModelType Type { get; set; }

        public string Name { get; }
        public Header Header { get; }
        public IReadOnlyList<Node> Nodes { get; }
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

        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public bool Animate { get; set; }

        public IReadOnlyList<Recolor> Recolors { get; }

        public Model(string name, Header header, IReadOnlyList<RawNode> nodes, IReadOnlyList<Mesh> meshes,
            IReadOnlyList<RawMaterial> materials, IReadOnlyList<DisplayList> dlists,
            IReadOnlyList<IReadOnlyList<RenderInstruction>> renderInstructions, IReadOnlyList<Recolor> recolors,
            int defaultRecolor)
        {
            ThrowIfInvalidEnums(materials);
            Name = name;
            Header = header;
            Nodes = nodes.Select(n => new Node(n)).ToList();
            Meshes = meshes;
            Materials = materials.Select(m => new Material(m)).ToList();
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

        private static void ThrowIfInvalidEnums(IEnumerable<RawMaterial> materials)
        {
            foreach (RawMaterial material in materials)
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

    // todo: look at and use more fields from the raw struct (same for Material)
    public class Node
    {
        public string Name { get; }
        public int ParentIndex { get; }
        public int ChildIndex { get; }
        public int NextIndex { get; }
        public bool Enabled { get; set; }
        public int MeshCount { get; }
        public int MeshId { get; }
        public Vector3 Scale { get; set; }
        public Vector3 Angle { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Vector1 { get; }
        public Vector3 Vector2 { get; }
        public byte Type { get; }
        public Matrix4 Transform { get; set; }

        public Node(RawNode raw)
        {
            Name = raw.Name;
            ParentIndex = raw.ParentId;
            ChildIndex = raw.ChildId;
            NextIndex = raw.NextId;
            Enabled = raw.Enabled != 0;
            MeshCount = raw.MeshCount;
            MeshId = raw.MeshId;
            Scale = raw.Scale.ToFloatVector();
            Angle = new Vector3(
                raw.AngleX / 65536.0f * 2.0f * MathF.PI,
                raw.AngleY / 65536.0f * 2.0f * MathF.PI,
                raw.AngleZ / 65536.0f * 2.0f * MathF.PI
            );
            Position = raw.Position.ToFloatVector();
            Vector1 = raw.Vector1.ToFloatVector();
            Vector2 = raw.Vector2.ToFloatVector();
            Type = raw.Type;
        }
    }

    public class Material
    {
        public string Name { get; }
        public CullingMode Culling { get; }
        public byte Alpha { get; }
        public int PaletteId { get; }
        public int TextureId { get; }
        public RepeatMode XRepeat { get; }
        public RepeatMode YRepeat { get; }
        public RenderMode RenderMode { get; set; }
        public float ScaleS { get; }
        public float ScaleT { get; }
        public float TranslateS { get; }
        public float TranslateT { get; }

        public Material(RawMaterial raw)
        {
            Name = raw.Name;
            Culling = raw.Culling;
            Alpha = raw.Alpha;
            PaletteId = raw.PaletteId;
            TextureId = raw.TextureId;
            XRepeat = raw.XRepeat;
            YRepeat = raw.YRepeat;
            RenderMode = raw.RenderMode;
            ScaleS = raw.ScaleS.FloatValue;
            ScaleT = raw.ScaleT.FloatValue;
            TranslateS = raw.TranslateS.FloatValue;
            TranslateT = raw.TranslateT.FloatValue;
        }
    }

    public class Entity
    {
        public string NodeName { get; }
        public short LayerMask { get; }
        public ushort Length { get; }
        public EntityType Type { get; }
        public ushort SomeId { get; }

        public Entity(EntityEntry entry, EntityType type, ushort someId)
        {
            NodeName = entry.NodeName;
            LayerMask = entry.LayerMask;
            Length = entry.Length;
            // todo: once all of these are accounted for, throw if not defined
            Type = type;
            SomeId = someId;
        }
    }

    public class Entity<T> : Entity where T : struct
    {
        public T Data { get; }

        public Entity(EntityEntry entry, EntityType type, ushort someId, T data)
            : base(entry, type, someId)
        {
            Data = data;
        }
    }

    public enum EntityType : ushort
    {
        Platform = 0x0,
        Object = 0x1,
        AlimbicDoor = 0x3,
        Item = 0x4,
        Pickup = 0x6,
        JumpPad = 0x9,
        Teleporter = 0xE,
        Artifact = 0x11,
        CameraSeq = 0x12,
        ForceField = 0x13,
        EnergyBeam = 0x1A,
    }

    public enum NodeLayer
    {
        Multiplayer0 = 0x0008,
        Multiplayer1 = 0x0010,
        MultiplayerU = 0x0020,
        CaptureTheFlag = 0x4000
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
