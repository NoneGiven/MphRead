using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Hud
{
    public enum Align
    {
        Left = 0,
        Right = 1,
        Center = 2,
        PadCenter = 3
    }

    public readonly struct UiAnimParams
    {
        public readonly byte ImageIndex;
        public readonly byte Delay; // sort of
        public readonly ushort Field2; // unused?
        public readonly int Field4; // unused?
        public readonly ushort ParamPa;
        public readonly ushort ParamPb;
        public readonly ushort ParamPc;
        public readonly ushort ParamPd;
    }

    public class HudMeter
    {
        public bool Horizontal;
        public int Length;
        public int TankSpacing;
        public int TankOffsetX;
        public int TankOffsetY;
        public int BarOffsetX;
        public int BarOffsetY;
        public int TextOffsetX;
        public int TextOffsetY;
        public Align Align;
        public int MessageId;

        public int TankAmount;
        public int TankCount;
        public HudObjectInstance BarInst = null!;
        public HudObjectInstance? TankInst;
    }

    public class HudObject
    {
        public readonly int Width;
        public readonly int Height;
        public readonly IReadOnlyList<byte> CharacterData;
        public readonly IReadOnlyList<ColorRgba> PaletteData;
        public readonly IReadOnlyList<UiAnimParams> AnimParams;

        public HudObject(int width, int height, IReadOnlyList<byte> characterData, IReadOnlyList<ColorRgba> paletteData,
            IReadOnlyList<UiAnimParams> animParams)
        {
            Width = width;
            Height = height;
            CharacterData = characterData;
            PaletteData = paletteData;
            AnimParams = animParams;
        }
    }

    public class HudObjectInstance
    {
        public bool Enabled;
        public bool Center;
        public float PositionX;
        public float PositionY;
        public int Width;
        public int Height;
        public bool FlipHorizontal;
        public bool FlipVertical;
        public bool UseMask;
        public IReadOnlyList<byte>? CharacterData;
        public int PaletteIndex = -1;
        public IReadOnlyList<ColorRgba>? PaletteData;
        public IReadOnlyList<int>? AnimFrames;
        public ColorRgba? Color;
        public readonly ColorRgba[] Texture;
        public float Alpha = 1;
        public int BindingId = -1;
        public int CurrentFrame;
        public int StartFrame;
        public int TargetFrame;
        public float Timer = -1;
        public float Time = -1;
        public bool Loop = false;
        public int AfterAnimFrame = -1;

        public HudObjectInstance(int width, int height)
        {
            Width = width;
            Height = height;
            Texture = new ColorRgba[Width * Height];
        }

        public HudObjectInstance(int width, int height, int maxWidth, int maxHeight)
        {
            Width = width;
            Height = height;
            Texture = new ColorRgba[maxWidth * maxHeight];
        }

        public void SetAnimationFrames(IReadOnlyList<UiAnimParams> frames)
        {
            var list = new List<int>();
            for (int i = 0; i < frames.Count; i++)
            {
                UiAnimParams frame = frames[i];
                for (int j = 0; j < frame.Delay; j++)
                {
                    list.Add(frame.ImageIndex);
                }
            }
            AnimFrames = list;
        }

        public void SetCharacterData(IReadOnlyList<byte> data, int width, int height, Scene scene)
        {
            Debug.Assert(Width * Height <= Texture.Length);
            Width = width;
            Height = height;
            CharacterData = null; // ensure DoTexture is called
            SetCharacterData(data, scene);
        }

        public void SetCharacterData(IReadOnlyList<byte> data, Scene scene)
        {
            SetCharacterData(data, frame: 0, scene);
        }

        public void SetCharacterData(IReadOnlyList<byte> data, int frame, Scene scene)
        {
            if (CharacterData != data)
            {
                CharacterData = data;
                CurrentFrame = frame;
                Timer = 0;
                if (PaletteData != null)
                {
                    DoTexture(scene);
                }
            }
        }

        public void SetPaletteData(IReadOnlyList<ColorRgba> data, Scene scene)
        {
            Color = null;
            if (PaletteData != data)
            {
                PaletteData = data;
                PaletteIndex = 0;
                if (CharacterData != null)
                {
                    DoTexture(scene);
                }
            }
        }

        public void SetPalette(int index, Scene scene)
        {
            Color = null;
            int prev = PaletteIndex;
            PaletteIndex = index;
            if (CharacterData != null && index != prev)
            {
                DoTexture(scene);
            }
        }

        public void SetData(IReadOnlyList<byte> charData, int charFrame,
            IReadOnlyList<ColorRgba> palData, int palIndex, Scene scene)
        {
            Color = null;
            Timer = 0;
            CharacterData = charData;
            CurrentFrame = charFrame;
            PaletteData = palData;
            PaletteIndex = palIndex;
            DoTexture(scene);
        }

        public void SetData(int charFrame, int palIndex, Scene scene)
        {
            Color = null;
            Timer = 0;
            int prevChar = CurrentFrame;
            int prevPal = PaletteIndex;
            CurrentFrame = charFrame;
            PaletteIndex = palIndex;
            if (CharacterData != null && PaletteData != null && (charFrame != prevChar || palIndex != prevPal))
            {
                DoTexture(scene);
            }
        }

        public void SetData(int charFrame, ColorRgba color, Scene scene)
        {
            PaletteIndex = -1;
            Timer = 0;
            int prevChar = CurrentFrame;
            ColorRgba? prevColor = Color;
            CurrentFrame = charFrame;
            Color = color;
            if (CharacterData != null && PaletteData != null && (charFrame != prevChar || prevColor != color))
            {
                DoTexture(scene);
            }
        }

        private void DoTexture(Scene scene)
        {
            Debug.Assert(CharacterData != null);
            Debug.Assert(PaletteData != null);
            int palOffset = PaletteIndex * 16;
            int width = Width / 8;
            int height = Height / 8;
            int image = CurrentFrame * Width * Height;
            for (int ty = 0; ty < height; ty++)
            {
                for (int tx = 0; tx < width; tx++)
                {
                    int start = ty * width * 8 * 8 + tx * 8;
                    for (int py = 0; py < 8; py++)
                    {
                        for (int px = 0; px < 8; px++)
                        {
                            byte palIndex = CharacterData[image + ty * width * 8 * 8 + tx * 8 * 8 + py * 8 + px];
                            int index = start + py * width * 8 + px;
                            if (palIndex == 0)
                            {
                                Texture[index] = new ColorRgba();
                            }
                            else if (Color != null)
                            {
                                Texture[index] = Color.Value;
                            }
                            else
                            {
                                Texture[index] = PaletteData[palOffset + palIndex];
                            }
                        }
                    }
                }
            }
            if (BindingId == -1)
            {
                BindingId = scene.BindGetTexture(Texture, Width, Height);
            }
            else
            {
                scene.BindTexture(Texture, Width, Height, BindingId);
            }
        }

        public void SetIndex(int frame, Scene scene)
        {
            int prev = CurrentFrame;
            CurrentFrame = frame;
            Timer = 0;
            if (frame != prev)
            {
                DoTexture(scene);
            }
        }

        public void SetAnimation(int start, int target, int frames, bool loop = false)
        {
            if (start == target)
            {
                CurrentFrame = start;
            }
            else
            {
                // no need to update textures since ProcessAnimation will do it
                CurrentFrame = start;
                StartFrame = start;
                TargetFrame = target;
                Timer = Time = frames * (1 / 30f);
                Loop = loop;
            }
        }

        public void SetAnimation(int start, int target, int frames, int afterAnim, bool loop = false)
        {
            Debug.Assert(AnimFrames != null);
            if (start == target)
            {
                CurrentFrame = start;
            }
            else
            {
                // don't set the start frame as current, since it's not always where we currently are
                StartFrame = start;
                TargetFrame = target;
                Timer = Time = frames * (1 / 30f);
                AfterAnimFrame = AnimFrames[afterAnim];
                Loop = loop;
            }
        }

        public void ProcessAnimation(Scene scene)
        {
            if (Timer > 0)
            {
                int prev = CurrentFrame;
                Timer -= scene.FrameTime;
                if (Timer <= 0)
                {
                    if (Loop)
                    {
                        CurrentFrame = StartFrame;
                        Timer = Time;
                    }
                    else
                    {
                        CurrentFrame = AnimFrames == null ? TargetFrame : AfterAnimFrame;
                    }
                }
                else
                {
                    int frame = StartFrame + (int)MathF.Round((TargetFrame - StartFrame) * (1 - Timer / Time));
                    if (AnimFrames == null)
                    {
                        CurrentFrame = frame;
                    }
                    else
                    {
                        CurrentFrame = AnimFrames[frame];
                    }
                }
                if (CurrentFrame != prev)
                {
                    DoTexture(scene);
                }
            }
        }
    }

    public class LayerInfo
    {
        public int BindingId { get; set; } = -1;
        public int MaskId { get; set; } = -1;
        public float Alpha { get; set; } = 1;
        public float ScaleX { get; set; } = -1;
        public float ScaleY { get; set; } = -1;
        public float ShiftX { get; set; } = 0;
        public float ShiftY { get; set; } = 0;
    }

    public class HudInfo
    {
        public readonly struct UiPartHeader
        {
            public readonly int Magic; // always zero
            public readonly int CharDataSize;
            public readonly int PalDataSize;
        }

        public readonly struct ScrDatInfo
        {
            public readonly ushort CharsX;
            public readonly ushort CharsY;
            public readonly int ScrDataSize;
        }

        private struct ScreenData
        {
            public int CharacterId;
            public bool FlipHorizontal;
            public bool FlipVertical;
            public int PaletteId;

            public ScreenData(ushort data)
            {
                CharacterId = data & 0x3FF;
                FlipHorizontal = (data & 0x400) != 0;
                FlipVertical = (data & 0x800) != 0;
                PaletteId = (data & 0xF000) >> 12;
            }
        }

        private static readonly int _layerHeaderSize = Marshal.SizeOf<UiPartHeader>();
        private static readonly int _scrDatInfoSize = Marshal.SizeOf<ScrDatInfo>();

        public static (int, IReadOnlyList<ushort>) CharMapToTexture(string path, Scene scene,
            IReadOnlyList<ushort>? paletteOverride = null, int paletteId = -1)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            return CharMapToTexture(bytes, startX: 0, startY: 0, tilesX: 0, tilesY: 0, scene, paletteOverride, paletteId);
        }

        public static (int, IReadOnlyList<ushort>) CharMapToTexture(string path, int startX, int startY,
            int tilesX, int tilesY, Scene scene, IReadOnlyList<ushort>? paletteOverride = null, int paletteId = -1)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            return CharMapToTexture(bytes, startX, startY, tilesX, tilesY, scene, paletteOverride, paletteId);
        }

        private static (int, IReadOnlyList<ushort>) CharMapToTexture(ReadOnlySpan<byte> bytes, int startX, int startY,
            int tilesX, int tilesY, Scene scene, IReadOnlyList<ushort>? paletteOverride = null, int paletteId = -1)
        {
            UiPartHeader header = Read.ReadStruct<UiPartHeader>(bytes);
            Debug.Assert(header.Magic == 0);
            int offset = _layerHeaderSize;
            IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
            offset += header.CharDataSize;
            Debug.Assert(header.PalDataSize % 2 == 0);
            IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
            offset += header.PalDataSize;
            ScrDatInfo info = Read.DoOffset<ScrDatInfo>(bytes, offset);
            offset += _scrDatInfoSize;
            Debug.Assert(info.ScrDataSize % 2 == 0);
            IReadOnlyList<ScreenData> screenData = Read.DoOffsets<ushort>(bytes, offset, info.ScrDataSize / 2)
                .Select(v => new ScreenData(v)).ToList();
            offset += info.ScrDataSize;
            Debug.Assert(Read.DoOffsets<byte>(bytes, offset, bytes.Length - offset).All(x => x == 0));

            if (paletteOverride != null)
            {
                var newPalette = new List<ushort>();
                newPalette.Add(paletteData[0]);
                newPalette.Add(paletteData[1]);
                for (int i = 1; i < paletteOverride.Count; i++)
                {
                    newPalette.Add(paletteOverride[i]);
                }
                paletteData = newPalette;
            }

            int paletteOffset = 0;
            if (paletteId != -1)
            {
                paletteOffset = paletteId * 16;
            }

            var characters = new List<List<ColorRgba>>();
            Debug.Assert(characterData.Count % 32 == 0);
            for (int i = 0; i < characterData.Count / 32; i++)
            {
                var character = new List<ColorRgba>();
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        byte data = characterData[i * 32 + y * 4 + x];
                        int index1 = data & 0xF;
                        int index2 = (data & 0xF0) >> 4;

                        void AddColor(int index)
                        {
                            var color = new ColorRgba();
                            if (index != 0 && (paletteId == -1 || index != 6))
                            {
                                color = new ColorRgba(paletteData[index + paletteOffset]);
                            }
                            character.Add(color);
                        }

                        AddColor(index1);
                        AddColor(index2);
                    }
                }
                characters.Add(character);
            }

            if (paletteId != -1)
            {
                var newScreenData = new List<ScreenData>(screenData.Count + 32 * 10);
                for (int i = 0; i < 32 * 10; i++)
                {
                    newScreenData.Add(new ScreenData(data: 0));
                }
                newScreenData.AddRange(screenData);
                if (paletteId == 4)
                {
                    for (int i = 10 * 32; i < 17 * 32; i++)
                    {
                        newScreenData[i] = new ScreenData(data: 0);
                    }
                }
                else
                {
                    for (int i = 10 * 32; i < 13 * 32; i++)
                    {
                        int charId = newScreenData[i].CharacterId;
                        if (charId == 1 || charId == 2 || charId == 5)
                        {
                            newScreenData[i] = new ScreenData(data: 0);
                        }
                    }
                }
                for (int i = 16 * 32; i < 17 * 32; i++)
                {
                    newScreenData[i] = new ScreenData(data: 13);
                }
                for (int i = 17 * 32; i < 21 * 32; i++)
                {
                    newScreenData[i] = new ScreenData(data: 15);
                }
                for (int i = 21 * 32; i < 21 * 32 + 4; i++)
                {
                    newScreenData[i] = new ScreenData(data: 21);
                }
                for (int i = 21 * 32 + 28; i < 22 * 32; i++)
                {
                    newScreenData[i] = new ScreenData(data: 21);
                }
                screenData = newScreenData;
            }

            if (tilesX == 0)
            {
                tilesX = info.CharsX;
            }
            if (tilesY == 0)
            {
                tilesY = info.CharsY;
            }
            ushort width = (ushort)(tilesX * 8);
            ushort height = (ushort)(tilesY * 8);
            var texture = new ColorRgba[width * height];
            for (int cy = 0; cy < tilesY; cy++)
            {
                int icy = cy + startY;
                for (int cx = 0; cx < tilesX; cx++)
                {
                    int icx = cx + startX;
                    int idx = icy * (info.CharsX > 32 ? info.CharsX / 2 : info.CharsX)
                        + ((icx / 32) == 1 ? 0x400 + (icx - 32) : icx); // deal with the "split" addresses
                    ScreenData screen = screenData[idx];
                    Debug.Assert(screen.PaletteId == 0);
                    List<ColorRgba> character = characters[screen.CharacterId];
                    int start = cy * tilesX * 8 * 8 + cx * 8;
                    for (int py = 0; py < 8; py++)
                    {
                        int iy = py;
                        if (screen.FlipVertical)
                        {
                            iy = 7 - py;
                        }
                        for (int px = 0; px < 8; px++)
                        {
                            int ix = px;
                            if (screen.FlipHorizontal)
                            {
                                ix = 7 - px;
                            }
                            ColorRgba pixel = character[iy * 8 + ix];
                            int index = start + py * tilesX * 8 + px;
                            texture[index] = pixel;
                        }
                    }
                }
            }
            return (scene.BindGetTexture(texture, width, height), paletteData);
        }

        private static readonly (int Width, int Height)[,] _objectDimensions = new (int, int)[3, 4]
        {
            // tiny    small  medium   large
            { (1, 1), (2, 2), (4, 4), (8, 8) }, // square
            { (2, 1), (4, 1), (4, 2), (8, 4) }, // wide
            { (1, 2), (1, 4), (2, 4), (4, 8) }  // tall
        };

        public readonly struct UiObjectHeader
        {
            public readonly ushort FrameCount;
            public readonly ushort ImageCount;
            public readonly ushort Width;
            public readonly ushort Height;
            public readonly int ParamDataSize;
            public readonly int AttrDataSize;
            public readonly int CharDataSize;
            public readonly int PalDataSize;
        }

        public readonly struct RawUiOamAttrs
        {
            public readonly ushort Attr0;
            public readonly ushort Attr1;
            public readonly ushort Attr2;
            public readonly ushort Padding6; // not used to write affine params
        }

        public enum ObjType
        {
            Normal = 0,
            Transparent = 1,
            Window = 2,
            Bitmap = 3
        }

        public enum ObjColors
        {
            Color16 = 0,
            Color256 = 1
        }

        public enum ObjShape
        {
            Square = 0,
            Wide = 1,
            Tall = 2,
            Unused = 3
        }

        public enum ObjSize
        {
            Tiny = 0,
            Small = 1,
            Medium = 2,
            Large = 3
        }

        public struct UiOamAttrs
        {
            public ushort XPos;
            public ushort YPos;
            public ObjType Type;
            public ObjSize Size;
            public ObjShape Shape;
            public ObjColors Colors;
            public bool AffineEnable;
            public bool DoubleSize;
            public int AffineIndex;
            public bool FlipHorizontal;
            public bool FlipVertical;
            public bool Mosaic;
            public int CharacterId;
            public int PaletteId;
            public byte Alpha;
            public byte Priority;

            public UiOamAttrs(RawUiOamAttrs raw)
            {
                Debug.Assert(raw.Padding6 == 0);
                YPos = (ushort)(raw.Attr0 & 0xFF);
                AffineEnable = (raw.Attr0 & (1 << 8)) != 0;
                DoubleSize = (raw.Attr0 & (1 << 9)) != 0;
                Type = (ObjType)((raw.Attr0 & (3 << 10)) >> 10);
                Mosaic = (raw.Attr0 & (1 << 12)) != 0;
                Colors = (raw.Attr0 & (1 << 13)) == 0 ? ObjColors.Color16 : ObjColors.Color256;
                Shape = (ObjShape)(raw.Attr0 >> 14);
                Debug.Assert(Shape != ObjShape.Unused);
                XPos = (ushort)(raw.Attr1 & 0x1FF);
                if (AffineEnable)
                {
                    AffineIndex = (raw.Attr1 & (0x1F << 9)) >> 9;
                    FlipHorizontal = false;
                    FlipVertical = false;
                }
                else
                {
                    AffineIndex = -1;
                    FlipHorizontal = (raw.Attr1 & (1 << 12)) != 0;
                    FlipVertical = (raw.Attr1 & (1 << 13)) != 0;
                }
                Size = (ObjSize)(raw.Attr1 >> 14);
                CharacterId = raw.Attr2 & 0x3FF;
                Priority = (byte)((raw.Attr2 & (3 << 10)) >> 10);
                if (Type == ObjType.Bitmap)
                {
                    PaletteId = -1;
                    Alpha = (byte)(raw.Attr2 >> 12);
                }
                else
                {
                    PaletteId = raw.Attr2 >> 12;
                    Alpha = 0;
                }
                Debug.Assert(!AffineEnable);
                Debug.Assert(Type == ObjType.Normal);
                Debug.Assert(Colors == ObjColors.Color16);
                Debug.Assert((raw.Attr0 & 0x3FFF) == 0); // zeroes except shape
                Debug.Assert((raw.Attr1 & 0x3FFF) == 0); // zeroes except size
                Debug.Assert((raw.Attr2 & 0xFFF) == 0); // zeroes except palette ID
            }
        }

        private static readonly int _objHeaderSize = Marshal.SizeOf<UiObjectHeader>();
        private static readonly int _animParamSize = Marshal.SizeOf<UiAnimParams>();
        private static readonly int _oamAttrSize = Marshal.SizeOf<RawUiOamAttrs>();

        public static HudObject GetHudObject(string file)
        {
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, file)));
            UiObjectHeader header = Read.ReadStruct<UiObjectHeader>(bytes);
            int offset = _objHeaderSize;
            Debug.Assert(header.ParamDataSize % _animParamSize == 0);
            int count = header.ParamDataSize / _animParamSize;
            IReadOnlyList<UiAnimParams> animParams = Read.DoOffsets<UiAnimParams>(bytes, offset, count);
            offset += header.ParamDataSize;
            Debug.Assert(header.AttrDataSize % _oamAttrSize == 0);
            count = header.AttrDataSize / _oamAttrSize;
            IReadOnlyList<RawUiOamAttrs> rawOamAttrs = Read.DoOffsets<RawUiOamAttrs>(bytes, offset, count);
            offset += header.AttrDataSize;
            IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
            offset += header.CharDataSize;
            Debug.Assert(header.PalDataSize % 2 == 0);
            IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
            Debug.Assert(offset + header.PalDataSize == bytes.Length);
            var palIndexData = new List<byte>();
            for (int i = 0; i < characterData.Count; i++)
            {
                byte data = characterData[i];
                palIndexData.Add((byte)(data & 0xF));
                palIndexData.Add((byte)((data & 0xF0) >> 4));
            }
            var palColorData = new List<ColorRgba>();
            for (int i = 0; i < paletteData.Count; i++)
            {
                palColorData.Add(new ColorRgba(paletteData[i]));
            }
            IReadOnlyList<UiOamAttrs> oamAttrs = rawOamAttrs.Select(r => new UiOamAttrs(r)).ToList();
            UiOamAttrs attrs = oamAttrs[0];
            Debug.Assert(!attrs.FlipHorizontal);
            Debug.Assert(!attrs.FlipVertical);
            (int width, int height) = _objectDimensions[(int)attrs.Shape, (int)attrs.Size];
            return new HudObject(width * 8, height * 8, palIndexData, palColorData, animParams);
        }

        private static void TestAnimation(IReadOnlyList<UiAnimParams> animParams, int pInitial, int pStart, int pTimer, int pTarget)
        {
            int frame = pInitial;
            int timer = 0;
            int target = 0;
            bool updating = false;

            void SetAnim()
            {
                frame = pStart - 1;
                timer = pTimer - 1;
                target = pTarget;
                updating = true;
            }

            void ProcessAnim()
            {
                if (updating)
                {
                    frame++;
                    if (frame == timer)
                    {
                        if (target >= 0)
                        {
                            updating = false;
                            frame = target - 1;
                        }
                        else
                        {
                            // loop
                            frame = -1 - target;
                        }
                    }
                }
            }

            (int, int) CheckThing(int pFrame)
            {
                for (int i = 0; i < animParams.Count; i++)
                {
                    int value = animParams[i].Delay;
                    if (pFrame < value)
                    {
                        return (i, animParams[i].ImageIndex);
                    }
                    pFrame -= value;
                }
                return (0, animParams[0].ImageIndex);
            }

            (int sn, int sidx) = CheckThing(frame);
            Console.WriteLine($"start: {sn} --> {sidx}");
            SetAnim();

            int frameCount = -1;
            while (updating)
            {
                frameCount++;
                ProcessAnim();
                (int n, int idx) = CheckThing(frame);
                Console.WriteLine($"f{frameCount}: {n} --> {idx}");
                Nop();
            }
            Console.WriteLine();
            Nop();
        }

        public static void TestObjects(string? filename, int pInitial, int pStart, int pTimer, int pTarget)
        {
            var files = new List<string>()
            {
                "_archives/spSamus/message_spacer.bin",
                "_archives/spSamus/hud_msgBox.bin",
                "_archives/spSamus/message_pickupframe.bin",
                "_archives/spSamus/message_pickups.bin",
                "_archives/spSamus/message_crystalpickup.bin",
                "_archives/spSamus/map_quit.bin",
                "_archives/spSamus/scan_lore.bin",
                "_archives/spSamus/scan_lore_dim.bin",
                "_archives/spSamus/scan_enemy.bin",
                "_archives/spSamus/scan_enemy_dim.bin",
                "_archives/spSamus/scan_object.bin",
                "_archives/spSamus/scan_object_dim.bin",
                "_archives/spSamus/scan_equipment.bin",
                "_archives/spSamus/scan_equipment_dim.bin",
                "_archives/spSamus/scan_red.bin",
                "_archives/spSamus/scan_red_dim.bin",
                "_archives/spSamus/scan_arrow.bin",
                "_archives/spSamus/scan_ok.bin",
                "_archives/spSamus/scan_select.bin",
                "_archives/spSamus/scan_corner.bin",
                "_archives/spSamus/scan_cornerSm.bin",
                "_archives/spSamus/scan_horizline.bin",
                "_archives/spSamus/scan_vertline.bin",
                "_archives/spSamus/obj_wnd.bin",
                "_archives/spSamus/hud_etank.bin",
                "_archives/spSamus/map_portal.bin",
                "_archives/spSamus/map_crystalbig.bin",
                "_archives/spSamus/map_art_1.bin",
                "_archives/spSamus/map_art_2.bin",
                "_archives/spSamus/map_art_3.bin",
                "_archives/spSamus/map_art_4.bin",
                "_archives/spSamus/map_art_5.bin",
                "_archives/spSamus/map_art_6.bin",
                "_archives/spSamus/map_art_7.bin",
                "_archives/spSamus/map_art_8.bin",
                "_archives/spSamus/map_crystalred.bin",
                "_archives/spSamus/map_legendOthers.bin",
                "_archives/spSamus/map_legendDoors.bin",
                "hud/rad_NodesOG.bin",
                "hud/rad_NodesRB.bin",
                "_archives/commonMP/radar_octolithLARGE.bin",
                "_archives/commonMP/radar_octolithSMALL.bin",
                "_archives/commonMP/hud_systemload.bin",
                "_archives/commonMP/wifi_strength.bin",
                "_archives/commonMP/stars.bin",
                "_archives/common/rad_radplyred.bin",
                "_archives/common/rad_key.bin",
                "_archives/common/hud_boost.bin",
                "_archives/common/hud_bombs.bin",
                "_archives/common/enemy_samus.bin",
                "_archives/common/enemy_kanden.bin",
                "_archives/common/enemy_noxus.bin",
                "_archives/common/enemy_spyre.bin",
                "_archives/common/enemy_sylux.bin",
                "_archives/common/enemy_trace.bin",
                "_archives/common/enemy_weavel.bin",
                "_archives/localSamus/rad_wepsel.bin",
                "_archives/localSamus/wepsel_icon.bin",
                "_archives/localSamus/wepsel_extra.bin",
                "_archives/localSamus/wepsel_box.bin",
                "_archives/localSamus/wepsel_hotdot.bin",
                "_archives/localSamus/hud_targetcircle.bin",
                "_archives/localSamus/hud_snipercircle.bin",
                "_archives/localSamus/hud_primehunter.bin",
                "_archives/localSamus/cloaking.bin",
                "_archives/localSamus/hud_damage.bin",
                "_archives/localSamus/hud_weaponicon.bin",
                "_archives/localSamus/hud_ammobar.bin",
                "_archives/localSamus/hud_energybar.bin",
                "_archives/localSamus/hud_energybar2.bin",
                "_archives/localSamus/rad_lights.bin",
                "_archives/localSamus/rad_ammobar.bin",
                "_archives/localKanden/rad_wepsel.bin",
                "_archives/localKanden/wepsel_icon.bin",
                "_archives/localKanden/wepsel_extra.bin",
                "_archives/localKanden/wepsel_box.bin",
                "_archives/localKanden/wepsel_hotdot.bin",
                "_archives/localKanden/hud_targetcircle.bin",
                "_archives/localKanden/hud_snipercircle.bin",
                "_archives/localKanden/hud_primehunter.bin",
                "_archives/localKanden/cloaking.bin",
                "_archives/localKanden/hud_damage.bin",
                "_archives/localKanden/hud_weaponicon.bin",
                "_archives/localKanden/hud_ammobar.bin",
                "_archives/localKanden/hud_energybar.bin",
                "_archives/localKanden/hud_energybar2.bin",
                "_archives/localKanden/rad_lights.bin",
                "_archives/localKanden/rad_ammobar.bin",
                "_archives/localNox/rad_wepsel.bin",
                "_archives/localNox/wepsel_icon.bin",
                "_archives/localNox/wepsel_extra.bin",
                "_archives/localNox/wepsel_box.bin",
                "_archives/localNox/wepsel_hotdot.bin",
                "_archives/localNox/hud_targetcircle.bin",
                "_archives/localNox/hud_snipercircle.bin",
                "_archives/localNox/hud_primehunter.bin",
                "_archives/localNox/cloaking.bin",
                "_archives/localNox/hud_damage.bin",
                "_archives/localNox/hud_weaponicon.bin",
                "_archives/localNox/hud_ammobar.bin",
                "_archives/localNox/hud_energybar.bin",
                "_archives/localNox/hud_energybar2.bin",
                "_archives/localNox/rad_lights.bin",
                "_archives/localNox/rad_ammobar.bin",
                "_archives/localSpire/rad_wepsel.bin",
                "_archives/localSpire/wepsel_icon.bin",
                "_archives/localSpire/wepsel_extra.bin",
                "_archives/localSpire/wepsel_box.bin",
                "_archives/localSpire/wepsel_hotdot.bin",
                "_archives/localSpire/hud_targetcircle.bin",
                "_archives/localSpire/hud_snipercircle.bin",
                "_archives/localSpire/hud_primehunter.bin",
                "_archives/localSpire/cloaking.bin",
                "_archives/localSpire/hud_damage.bin",
                "_archives/localSpire/hud_weaponicon.bin",
                "_archives/localSpire/hud_ammobar.bin",
                "_archives/localSpire/hud_energybar.bin",
                "_archives/localSpire/hud_energybar2.bin",
                "_archives/localSpire/rad_lights.bin",
                "_archives/localSpire/rad_ammobar.bin",
                "_archives/localSylux/rad_wepsel.bin",
                "_archives/localSylux/wepsel_icon.bin",
                "_archives/localSylux/wepsel_extra.bin",
                "_archives/localSylux/wepsel_box.bin",
                "_archives/localSylux/wepsel_hotdot.bin",
                "_archives/localSylux/hud_targetcircle.bin",
                "_archives/localSylux/hud_snipercircle.bin",
                "_archives/localSylux/hud_primehunter.bin",
                "_archives/localSylux/cloaking.bin",
                "_archives/localSylux/hud_damage.bin",
                "_archives/localSylux/hud_weaponicon.bin",
                "_archives/localSylux/hud_ammobar.bin",
                "_archives/localSylux/hud_energybar.bin",
                "_archives/localSylux/hud_energybar2.bin",
                "_archives/localSylux/rad_lights.bin",
                "_archives/localSylux/rad_ammobar.bin",
                "_archives/localTrace/rad_wepsel.bin",
                "_archives/localTrace/wepsel_icon.bin",
                "_archives/localTrace/wepsel_extra.bin",
                "_archives/localTrace/wepsel_box.bin",
                "_archives/localTrace/wepsel_hotdot.bin",
                "_archives/localTrace/hud_targetcircle.bin",
                "_archives/localTrace/hud_snipercircle.bin",
                "_archives/localTrace/hud_primehunter.bin",
                "_archives/localTrace/cloaking.bin",
                "_archives/localTrace/hud_damage.bin",
                "_archives/localTrace/hud_weaponicon.bin",
                "_archives/localTrace/hud_ammobar.bin",
                "_archives/localTrace/hud_energybar.bin",
                "_archives/localTrace/hud_energybar2.bin",
                "_archives/localTrace/rad_lights.bin",
                "_archives/localTrace/rad_ammobar.bin",
                "_archives/localWeavel/rad_wepsel.bin",
                "_archives/localWeavel/wepsel_icon.bin",
                "_archives/localWeavel/wepsel_extra.bin",
                "_archives/localWeavel/wepsel_box.bin",
                "_archives/localWeavel/wepsel_hotdot.bin",
                "_archives/localWeavel/hud_targetcircle.bin",
                "_archives/localWeavel/hud_snipercircle.bin",
                "_archives/localWeavel/hud_primehunter.bin",
                "_archives/localWeavel/cloaking.bin",
                "_archives/localWeavel/hud_damage.bin",
                "_archives/localWeavel/hud_weaponicon.bin",
                "_archives/localWeavel/hud_ammobar.bin",
                "_archives/localWeavel/hud_energybar.bin",
                "_archives/localWeavel/hud_energybar2.bin",
                "_archives/localWeavel/rad_lights.bin",
                "_archives/localWeavel/rad_ammobar.bin"
            };
            if (filename != null)
            {
                files.Clear();
                files.Add(filename);
            }
            foreach (string file in files)
            {
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, file)));
                UiObjectHeader header = Read.ReadStruct<UiObjectHeader>(bytes);
                int offset = _objHeaderSize;
                Debug.Assert(header.ParamDataSize % _animParamSize == 0);
                int count = header.ParamDataSize / _animParamSize;
                Debug.Assert(count == header.FrameCount);
                IReadOnlyList<UiAnimParams> animParams = Read.DoOffsets<UiAnimParams>(bytes, offset, count);
                offset += header.ParamDataSize;
                Debug.Assert(header.AttrDataSize % _oamAttrSize == 0);
                count = header.AttrDataSize / _oamAttrSize;
                IReadOnlyList<RawUiOamAttrs> rawOamAttrs = Read.DoOffsets<RawUiOamAttrs>(bytes, offset, count);
                IReadOnlyList<UiOamAttrs> oamAttrs = rawOamAttrs.Select(r => new UiOamAttrs(r)).ToList();
                offset += header.AttrDataSize;
                IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
                offset += header.CharDataSize;
                Debug.Assert(header.PalDataSize % 2 == 0);
                IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
                offset += header.PalDataSize;
                Debug.Assert(offset == bytes.Length);

                if (filename != null)
                {
                    TestAnimation(animParams, pInitial, pStart, pTimer, pTarget);
                }

                var palIndexData = new List<byte>();
                for (int i = 0; i < characterData.Count; i++)
                {
                    byte data = characterData[i];
                    palIndexData.Add((byte)(data & 0xF));
                    palIndexData.Add((byte)((data & 0xF0) >> 4));
                }

                var palColorData = new List<ColorRgba>();
                for (int i = 0; i < paletteData.Count; i++)
                {
                    palColorData.Add(new ColorRgba(paletteData[i]));
                }

                // todo: share this
                var characters = new List<List<List<ColorRgba>>>();
                Debug.Assert(characterData.Count % 32 == 0);
                Debug.Assert(paletteData.Count % 16 == 0);
                for (int p = 0; p < paletteData.Count / 16; p++)
                {
                    var palChars = new List<List<ColorRgba>>();
                    for (int i = 0; i < characterData.Count / 32; i++)
                    {
                        // todo?: if 256 colors were used, these would need to be parsed differently
                        var character = new List<ColorRgba>();
                        for (int y = 0; y < 8; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                byte data = characterData[i * 32 + y * 4 + x];
                                int index1 = data & 0xF;
                                character.Add(index1 == 0 ? new ColorRgba() : new ColorRgba(paletteData[p * 16 + index1]));
                                int index2 = (data & 0xF0) >> 4;
                                character.Add(index2 == 0 ? new ColorRgba() : new ColorRgba(paletteData[p * 16 + index2]));
                            }
                        }
                        palChars.Add(character);
                    }
                    characters.Add(palChars);
                }
                //for (int i = 0; i < characters.Count; i++)
                //{
                //    List<ColorRgba> character = characters[i];
                //    string filename = $"{i.ToString().PadLeft(3, '0')}";
                //    Export.Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), filename, 8, 8, character);
                //}
                UiOamAttrs attrs = oamAttrs[0];
                Debug.Assert(attrs.CharacterId == 0);
                if (attrs.PaletteId != 0)
                {
                    Console.WriteLine(file);
                }
                Debug.Assert(!attrs.FlipHorizontal);
                Debug.Assert(!attrs.FlipVertical);
                // ltodo: parse characters for each palette that's actually specified by the attr, I guess?
                // --> some just have 1 in all the attrs instead of 0 in all the attrs, so just use that?
                (int width, int height) = _objectDimensions[(int)attrs.Shape, (int)attrs.Size];
                int tiles = width * height;
                ushort texWidth = (ushort)(width * 8);
                ushort texHeight = (ushort)(height * 8);
                int size = texWidth * texHeight;
                for (int i = 0; i < header.ImageCount; i++)
                {
                    var distinctPalettes = new List<List<ushort>>();
                    for (int p = 0; p < paletteData.Count / 16; p++)
                    {
                        var palette = new List<ushort>();
                        for (int j = 0; j < 16; j++)
                        {
                            palette.Add(paletteData[p * 16 + j]);
                        }
                        if (distinctPalettes.Any(d => d.SequenceEqual(palette)))
                        {
                            continue;
                        }
                        distinctPalettes.Add(palette);
                        var texture = new ColorRgba[size];
                        for (int ty = 0; ty < height; ty++)
                        {
                            for (int tx = 0; tx < width; tx++)
                            {
                                List<ColorRgba> character = characters[p][i * tiles + ty * width + tx];
                                int start = ty * width * 8 * 8 + tx * 8;
                                for (int py = 0; py < 8; py++)
                                {
                                    for (int px = 0; px < 8; px++)
                                    {
                                        ColorRgba pixel = character[py * 8 + px];
                                        int index = start + py * width * 8 + px;
                                        texture[index] = pixel;
                                    }
                                }
                            }
                        }
                        string dir = file.Replace("_archives/", "").Replace(".bin", "");
                        dir = Path.Combine(Paths.Export, "_2D/Objects", dir, $"pal_{p.ToString().PadLeft(2, '0')}");
                        //Directory.CreateDirectory(dir);
                        string name = i.ToString().PadLeft(3, '0');
                        //Export.Images.SaveTexture(dir, name, texWidth, texHeight, texture);
                    }
                }
                Nop();
            }
            Nop();
        }

        public static void TestLayers()
        {
            var files = new List<string>()
            {
                "hud/samus/bg_bottom.bin",
                "hud/samus/bg_bottomL.bin",
                "hud/samus/bg_altform.bin",
                "hud/samus/bg_altformL.bin",
                "hud/samus/bg_wepsel.bin",
                "hud/samus/bg_wepselL.bin",
                "_archives/localSamus/map_grid.bin",
                "_archives/localSamus/bg_top_ovl.bin",
                "_archives/localSamus/bg_top.bin",
                "_archives/localSamus/bg_top_drop.bin",
                "hud/kanden/bg_bottom.bin",
                "hud/kanden/bg_bottomL.bin",
                "hud/kanden/bg_altform.bin",
                "hud/kanden/bg_altformL.bin",
                "hud/kanden/bg_wepsel.bin",
                "hud/kanden/bg_wepselL.bin",
                "_archives/localKanden/map_grid.bin",
                "_archives/localKanden/bg_top_ovl.bin",
                "_archives/localKanden/bg_top.bin",
                "_archives/localKanden/bg_top_drop.bin",
                "hud/nox/bg_bottom.bin",
                "hud/nox/bg_bottomL.bin",
                "hud/nox/bg_altform.bin",
                "hud/nox/bg_altformL.bin",
                "hud/nox/bg_wepsel.bin",
                "hud/nox/bg_wepselL.bin",
                "_archives/localNox/map_grid.bin",
                "_archives/localNox/bg_top_ovl.bin",
                "_archives/localNox/bg_top.bin",
                "_archives/localNox/bg_top_drop.bin",
                "hud/spire/bg_bottom.bin",
                "hud/spire/bg_bottomL.bin",
                "hud/spire/bg_altform.bin",
                "hud/spire/bg_altformL.bin",
                "hud/spire/bg_wepsel.bin",
                "hud/spire/bg_wepselL.bin",
                "_archives/localSpire/map_grid.bin",
                "_archives/localSpire/bg_top_ovl.bin",
                "_archives/localSpire/bg_top.bin",
                "_archives/localSpire/bg_top_drop.bin",
                "hud/sylux/bg_bottom.bin",
                "hud/sylux/bg_bottomL.bin",
                "hud/sylux/bg_altform.bin",
                "hud/sylux/bg_altformL.bin",
                "hud/sylux/bg_wepsel.bin",
                "hud/sylux/bg_wepselL.bin",
                "_archives/localSylux/map_grid.bin",
                "_archives/localSylux/bg_top_ovl.bin",
                "_archives/localSylux/bg_top.bin",
                "_archives/localSylux/bg_top_drop.bin",
                "hud/trace/bg_bottom.bin",
                "hud/trace/bg_bottomL.bin",
                "hud/trace/bg_altform.bin",
                "hud/trace/bg_altformL.bin",
                "hud/trace/bg_wepsel.bin",
                "hud/trace/bg_wepselL.bin",
                "_archives/localTrace/map_grid.bin",
                "_archives/localTrace/bg_top_ovl.bin",
                "_archives/localTrace/bg_top.bin",
                "_archives/localTrace/bg_top_drop.bin",
                "hud/weavel/bg_bottom.bin",
                "hud/weavel/bg_bottomL.bin",
                "hud/weavel/bg_altform.bin",
                "hud/weavel/bg_altformL.bin",
                "hud/weavel/bg_wepsel.bin",
                "hud/weavel/bg_wepselL.bin",
                "_archives/localWeavel/map_grid.bin",
                "_archives/localWeavel/bg_top_ovl.bin",
                "_archives/localWeavel/bg_top.bin",
                "_archives/localWeavel/bg_top_drop.bin",
                "_archives/spSamus/bg_scanjewel.bin",
                "_archives/spSamus/map_scan.bin",
                "_archives/common/bg_ice.bin"
            };
            foreach (string file in files)
            {
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, file)));
                UiPartHeader header = Read.ReadStruct<UiPartHeader>(bytes);
                Debug.Assert(header.Magic == 0);
                int offset = _layerHeaderSize;
                IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
                offset += header.CharDataSize;
                Debug.Assert(header.PalDataSize % 2 == 0);
                IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
                offset += header.PalDataSize;
                ScrDatInfo info = Read.DoOffset<ScrDatInfo>(bytes, offset);
                offset += _scrDatInfoSize;
                Debug.Assert(info.ScrDataSize % 2 == 0);
                IReadOnlyList<ushort> screenDataValues = Read.DoOffsets<ushort>(bytes, offset, info.ScrDataSize / 2);
                Debug.Assert(info.CharsX * info.CharsY == screenDataValues.Count);

                var vramStuff = new List<ushort>();
                int charSlot = (header.CharDataSize + 32) / 32;
                int palSlot = 1;
                int size = info.ScrDataSize;

                int count = size / 2;
                uint minCharId = 1024;
                uint minPalId = 16;
                for (int i = 0; i < count; ++i)
                {
                    uint data = screenDataValues[i];
                    uint charId = data & 0x3FF;
                    if (charId < minCharId)
                    {
                        minCharId = charId;
                    }
                    uint palId = (data & 0xF000) >> 12;
                    if (palId < minPalId)
                    {
                        minPalId = palId;
                    }
                }
                for (int j = 0; j < count;)
                {
                    uint val = screenDataValues[j++];
                    uint curFlip = val & 0xC00;
                    uint curPalId = (val & 0xF000) >> 12;
                    uint curCharId = val & 0x3FF;
                    long newPalId = palSlot + curPalId - minPalId;
                    long newCharId = charSlot + curCharId - minCharId;
                    vramStuff.Add((ushort)(curCharId | curFlip | (curPalId << 12)));
                }

                IReadOnlyList<ScreenData> screenData = screenDataValues.Select(v => new ScreenData(v)).ToList();

                // every character's data is 32 bytes big -- each byte contains palette index data for two pixels
                // (going left to right, top to bottom) -- each row is 4 bytes (4 * 2 = 8 pixels)

                var characters = new List<List<ColorRgba>>();
                Debug.Assert(characterData.Count % 32 == 0);
                for (int i = 0; i < characterData.Count / 32; i++)
                {
                    var character = new List<ColorRgba>();
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            byte data = characterData[i * 32 + y * 4 + x];
                            int index1 = data & 0xF;
                            character.Add(index1 == 0 ? new ColorRgba() : new ColorRgba(paletteData[index1]));
                            int index2 = (data & 0xF0) >> 4;
                            character.Add(index2 == 0 ? new ColorRgba() : new ColorRgba(paletteData[index2]));
                        }
                    }
                    characters.Add(character);
                }

                ushort width = (ushort)(info.CharsX * 8);
                ushort height = (ushort)(info.CharsY * 8);
                var texture = new ColorRgba[width * height];
                for (int cy = 0; cy < info.CharsY; cy++)
                {
                    for (int cx = 0; cx < info.CharsX; cx++)
                    {
                        int idx = cy * (info.CharsX > 32 ? info.CharsX / 2 : info.CharsX)
                            + ((cx / 32) == 1 ? 0x400 + (cx - 32) : cx);
                        ScreenData screen = screenData[idx];
                        Debug.Assert(screen.PaletteId == 0);
                        List<ColorRgba> character = characters[screen.CharacterId];
                        int start = cy * info.CharsX * 8 * 8 + cx * 8;
                        for (int py = 0; py < 8; py++)
                        {
                            int iy = py;
                            if (screen.FlipVertical)
                            {
                                iy = 7 - py;
                            }
                            for (int px = 0; px < 8; px++)
                            {
                                int ix = px;
                                if (screen.FlipHorizontal)
                                {
                                    ix = 7 - px;
                                }
                                ColorRgba pixel = character[iy * 8 + ix];
                                int index = start + py * info.CharsX * 8 + px;
                                texture[index] = pixel;
                            }
                        }
                    }
                }
                string name = file.Replace("/", "--");
                //Export.Images.SaveTexture(Path.Combine(Paths.Export, @"_2D\layertest"), name, width, height, texture);

                for (int i = 0; i < characters.Count; i++)
                {
                    List<ColorRgba> character = characters[i];
                    string filename = $"{i.ToString().PadLeft(3, '0')}";
                    //Export.Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), filename, 8, 8, character);
                }

                offset += info.ScrDataSize;
                Debug.Assert(Read.DoOffsets<byte>(bytes, offset, bytes.Length - offset).All(x => x == 0));
                Nop();
            }
            Nop();
        }

        public static void Nop()
        {
        }
    }

    public class RulesInfo
    {
        public int Count { get; }
        public int[] MessageIds { get; }
        public int[] Offsets { get; }

        public RulesInfo(int count, int[] messageIds, int[] offsets)
        {
            Count = count;
            MessageIds = messageIds;
            Offsets = offsets;
        }
    }

    public static class HudElements
    {
        public static readonly string IceLayer = @"_archives\common\bg_ice.bin";
        public static readonly string Boost = @"_archives\common\hud_boost.bin";
        public static readonly string Bombs = @"_archives\common\hud_bombs.bin";
        public static readonly string Stars = @"_archives\commonMP\stars.bin";
        public static readonly string Octolith = @"_archives\commonMP\radar_octolithLARGE.bin";
        public static readonly string NodesOG = @"hud\rad_NodesOG.bin";
        public static readonly string NodesRB = @"hud\rad_NodesRB.bin";
        public static readonly string SystemLoad = @"_archives\commonMP\hud_systemload.bin";
        public static readonly string MessageBox = @"_archives\spSamus\hud_msgBox.bin";
        public static readonly string MessageSpacer = @"_archives\spSamus\message_spacer.bin";
        public static readonly string MapScan = @"_archives\spSamus\map_scan.bin";
        public static readonly string DialogButton = @"_archives\spSamus\scan_ok.bin";
        public static readonly string DialogArrow = @"_archives\spSamus\scan_arrow.bin";

        public static readonly IReadOnlyList<string> Hunters = new string[8]
        {
            @"_archives\common\enemy_samus.bin",
            @"_archives\common\enemy_kanden.bin",
            @"_archives\common\enemy_trace.bin",
            @"_archives\common\enemy_sylux.bin",
            @"_archives\common\enemy_noxus.bin",
            @"_archives\common\enemy_spyre.bin",
            @"_archives\common\enemy_weavel.bin",
            @"_archives\common\enemy_samus.bin" // todo: Guardian portrait
        };

        public static IReadOnlyList<RulesInfo> RulesInfo = new RulesInfo[7]
        {
            // Battle
            new RulesInfo(
                count: 4,
                messageIds: new int[8] { 1, 2, 3, 4, 0, 0, 0, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }
            ),
            // Survival
            new RulesInfo
            (
                count: 4,
                messageIds: new int[8] { 11, 12, 13, 14, 0, 0, 0, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }
            ),
            // Prime Hunter
            new RulesInfo
            (
                count: 7,
                messageIds: new int[8] { 21, 22, 23, 24, 25, 26, 27, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 12, 12, 12, 0 }
            ),
            // Bounty
            new RulesInfo
            (
                count: 5,
                messageIds: new int[8] { 31, 32, 33, 34, 35, 0, 0, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }
            ),
            // Capture
            new RulesInfo
            (
                count: 6,
                messageIds: new int[8] { 41, 42, 43, 44, 45, 46, 0, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }
            ),
            // Defender
            new RulesInfo
            (
                count: 4,
                messageIds: new int[8] { 51, 52, 53, 54, 0, 0, 0, 0 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 }
            ),
            // Nodes
            new RulesInfo
            (
                count: 8,
                messageIds: new int[8] { 61, 62, 63, 64, 65, 66, 67, 68 },
                offsets: new int[8] { 0, 0, 0, 0, 0, 12, 12, 12 }
            )
        };

        public static readonly string ScanCorner = @"_archives\spSamus\scan_corner.bin";
        public static readonly string ScanCornerSmall = @"_archives\spSamus\scan_cornerSm.bin";
        public static readonly string ScanLineHoriz = @"_archives\spSamus\scan_horizline.bin";
        public static readonly string ScanLineVert = @"_archives\spSamus\scan_vertline.bin";

        public static IReadOnlyList<string> ScanIcons = new string[10]
        {
            @"_archives\spSamus\scan_lore.bin",
            @"_archives\spSamus\scan_lore_dim.bin",
            @"_archives\spSamus\scan_enemy.bin",
            @"_archives\spSamus\scan_enemy_dim.bin",
            @"_archives\spSamus\scan_object.bin",
            @"_archives\spSamus\scan_object_dim.bin",
            @"_archives\spSamus\scan_equipment.bin",
            @"_archives\spSamus\scan_equipment_dim.bin",
            @"_archives\spSamus\scan_red.bin",
            @"_archives\spSamus\scan_red_dim.bin"
        };

        public static IReadOnlyList<HudObjects> HunterObjects = new HudObjects[8]
        {
            // Samus
            new HudObjects(
                helmet: @"_archives\localSamus\bg_top.bin",
                helmetDrop: @"_archives\localSamus\bg_top_drop.bin",
                visor: @"_archives\localSamus\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localSamus\hud_energybar.bin",
                healthBarB: @"_archives\localSamus\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localSamus\hud_weaponicon.bin",
                doubleDamage: @"_archives\localSamus\hud_damage.bin",
                cloaking: @"_archives\localSamus\cloaking.bin",
                primeHunter: @"_archives\localSamus\hud_primehunter.bin",
                ammoBar: @"_archives\localSamus\hud_ammobar.bin",
                reticle: @"_archives\localSamus\hud_targetcircle.bin",
                sniperReticle: @"_archives\localSamus\hud_snipercircle.bin",
                weaponSelect: @"_archives\localSamus\rad_wepsel.bin",
                selectIcon: @"_archives\localSamus\wepsel_icon.bin",
                selectBox: @"_archives\localSamus\wepsel_box.bin",
                damageBar: @"_archives\localSamus\rad_ammobar.bin",
                healthMainPosX: 93,
                healthMainPosY: -5,
                healthSubPosX: 93,
                healthSubPosY: 1,
                healthOffsetY: 32,
                healthOffsetYAlt: -10,
                ammoBarPosX: 236,
                ammoBarPosY: 137,
                weaponIconPosX: 214,
                weaponIconPosY: 150,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 12,
                scorePosY: 30,
                scoreAlign: Align.Left,
                octolithPosX: 228,
                octolithPosY: 28,
                primePosX: 232,
                primePosY: 42,
                primeTextPosX: -16,
                primeTextPosY: -4,
                primeAlign: Align.Right,
                nodeBonusPosX: 22,
                nodeBonusPosY: 56,
                enemyBonusPosX: 22,
                enemyBonusPosY: 80,
                nodeIconPosX: 220,
                nodeIconPosY: 41,
                nodeTextPosX: 220,
                nodeTextPosY: 45,
                dblDmgPosX: 64,
                dblDmgPosY: 174,
                dblDmgTextPosX: 16,
                dblDmgTextPosY: -8,
                dblDmgAlign: Align.Left,
                cloakPosX: 192,
                cloakPosY: 174,
                cloakTextPosX: -16,
                cloakTextPosY: 2,
                cloakAlign: Align.Right
            ),
            // Kanden
            new HudObjects(
                helmet: @"_archives\localKanden\bg_top.bin",
                helmetDrop: @"_archives\localKanden\bg_top_drop.bin",
                visor: @"_archives\localKanden\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localKanden\hud_energybar.bin",
                healthBarB: @"_archives\localKanden\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localKanden\hud_weaponicon.bin",
                doubleDamage: @"_archives\localKanden\hud_damage.bin",
                cloaking: @"_archives\localKanden\cloaking.bin",
                primeHunter: @"_archives\localKanden\hud_primehunter.bin",
                ammoBar: @"_archives\localKanden\hud_ammobar.bin",
                reticle: @"_archives\localKanden\hud_targetcircle.bin",
                sniperReticle: @"_archives\localKanden\hud_snipercircle.bin",
                weaponSelect: @"_archives\localKanden\rad_wepsel.bin",
                selectIcon: @"_archives\localKanden\wepsel_icon.bin",
                selectBox: @"_archives\localKanden\wepsel_box.bin",
                damageBar: @"_archives\localKanden\rad_ammobar.bin",
                healthMainPosX: 13,
                healthMainPosY: 0,
                healthSubPosX: 20,
                healthSubPosY: 0,
                healthOffsetY: 128,
                healthOffsetYAlt: 0,
                ammoBarPosX: 238,
                ammoBarPosY: 128,
                weaponIconPosX: 230,
                weaponIconPosY: 138,
                enemyHealthPosX: 93, // hunters besides Samus have zeroes for these fields in the game
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 20,
                scorePosY: 4,
                scoreAlign: Align.Left,
                octolithPosX: 212,
                octolithPosY: 4,
                primePosX: 222,
                primePosY: 17,
                primeTextPosX: -16,
                primeTextPosY: -10,
                primeAlign: Align.Right,
                nodeBonusPosX: 96,
                nodeBonusPosY: 4,
                enemyBonusPosX: 136,
                enemyBonusPosY: 4,
                nodeIconPosX: 210,
                nodeIconPosY: 12,
                nodeTextPosX: 210,
                nodeTextPosY: 14,
                dblDmgPosX: 22,
                dblDmgPosY: 156,
                dblDmgTextPosX: 6,
                dblDmgTextPosY: 14,
                dblDmgAlign: Align.Left,
                cloakPosX: 224,
                cloakPosY: 176,
                cloakTextPosX: -16,
                cloakTextPosY: 3,
                cloakAlign: Align.Right
            ),
            // Trace
            new HudObjects(
                helmet: @"_archives\localTrace\bg_top.bin",
                helmetDrop: @"_archives\localTrace\bg_top_drop.bin",
                visor: @"_archives\localTrace\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localTrace\hud_energybar.bin",
                healthBarB: @"_archives\localTrace\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localTrace\hud_weaponicon.bin",
                doubleDamage: @"_archives\localTrace\hud_damage.bin",
                cloaking: @"_archives\localTrace\cloaking.bin",
                primeHunter: @"_archives\localTrace\hud_primehunter.bin",
                ammoBar: @"_archives\localTrace\hud_ammobar.bin",
                reticle: @"_archives\localTrace\hud_targetcircle.bin",
                sniperReticle: @"_archives\localTrace\hud_snipercircle.bin",
                weaponSelect: @"_archives\localTrace\rad_wepsel.bin",
                selectIcon: @"_archives\localTrace\wepsel_icon.bin",
                selectBox: @"_archives\localTrace\wepsel_box.bin",
                damageBar: @"_archives\localTrace\rad_ammobar.bin",
                healthMainPosX: 24,
                healthMainPosY: 135,
                healthSubPosX: 29,
                healthSubPosY: 135,
                healthOffsetY: 0,
                healthOffsetYAlt: 0,
                ammoBarPosX: 232,
                ammoBarPosY: 135,
                weaponIconPosX: 225,
                weaponIconPosY: 148,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 128,
                scorePosY: 12,
                scoreAlign: Align.Center,
                octolithPosX: 176,
                octolithPosY: 12,
                primePosX: 226,
                primePosY: 56,
                primeTextPosX: -16,
                primeTextPosY: -10,
                primeAlign: Align.Right,
                nodeBonusPosX: 60,
                nodeBonusPosY: 24,
                enemyBonusPosX: 24,
                enemyBonusPosY: 38,
                nodeIconPosX: 202,
                nodeIconPosY: 32,
                nodeTextPosX: 202,
                nodeTextPosY: 36,
                dblDmgPosX: 48,
                dblDmgPosY: 172,
                dblDmgTextPosX: 16,
                dblDmgTextPosY: -7,
                dblDmgAlign: Align.Left,
                cloakPosX: 208,
                cloakPosY: 172,
                cloakTextPosX: -16,
                cloakTextPosY: 3,
                cloakAlign: Align.Right
            ),
            // Sylux
            new HudObjects(
                helmet: @"_archives\localSylux\bg_top.bin",
                helmetDrop: @"_archives\localSylux\bg_top_drop.bin",
                visor: @"_archives\localSylux\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localSylux\hud_energybar.bin",
                healthBarB: @"_archives\localSylux\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localSylux\hud_weaponicon.bin",
                doubleDamage: @"_archives\localSylux\hud_damage.bin",
                cloaking: @"_archives\localSylux\cloaking.bin",
                primeHunter: @"_archives\localSylux\hud_primehunter.bin",
                ammoBar: @"_archives\localSylux\hud_ammobar.bin",
                reticle: @"_archives\localSylux\hud_targetcircle.bin",
                sniperReticle: @"_archives\localSylux\hud_snipercircle.bin",
                weaponSelect: @"_archives\localSylux\rad_wepsel.bin",
                selectIcon: @"_archives\localSylux\wepsel_icon.bin",
                selectBox: @"_archives\localSylux\wepsel_box.bin",
                damageBar: @"_archives\localSylux\rad_ammobar.bin",
                healthMainPosX: 47,
                healthMainPosY: 165,
                healthSubPosX: 51,
                healthSubPosY: 165,
                healthOffsetY: 0,
                healthOffsetYAlt: 0,
                ammoBarPosX: 206,
                ammoBarPosY: 165,
                weaponIconPosX: 214,
                weaponIconPosY: 131,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 56,
                scorePosY: 8,
                scoreAlign: Align.Left,
                octolithPosX: 186,
                octolithPosY: 4,
                primePosX: 190,
                primePosY: 16,
                primeTextPosX: 14,
                primeTextPosY: 17,
                primeAlign: Align.Right,
                nodeBonusPosX: 212,
                nodeBonusPosY: 38,
                enemyBonusPosX: 212,
                enemyBonusPosY: 62,
                nodeIconPosX: 180,
                nodeIconPosY: 15,
                nodeTextPosX: 180,
                nodeTextPosY: 17,
                dblDmgPosX: 32,
                dblDmgPosY: 164,
                dblDmgTextPosX: 20,
                dblDmgTextPosY: -3,
                dblDmgAlign: Align.Left,
                cloakPosX: 186,
                cloakPosY: 162,
                cloakTextPosX: 16,
                cloakTextPosY: 12,
                cloakAlign: Align.Right
            ),
            // Noxus
            new HudObjects(
                helmet: @"_archives\localNox\bg_top.bin",
                helmetDrop: @"_archives\localNox\bg_top_drop.bin",
                visor: @"_archives\localNox\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localNox\hud_energybar.bin",
                healthBarB: @"_archives\localNox\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localNox\hud_weaponicon.bin",
                doubleDamage: @"_archives\localNox\hud_damage.bin",
                cloaking: @"_archives\localNox\cloaking.bin",
                primeHunter: @"_archives\localNox\hud_primehunter.bin",
                ammoBar: @"_archives\localNox\hud_ammobar.bin",
                reticle: @"_archives\localNox\hud_targetcircle.bin",
                sniperReticle: @"_archives\localNox\hud_snipercircle.bin",
                weaponSelect: @"_archives\localNox\rad_wepsel.bin",
                selectIcon: @"_archives\localNox\wepsel_icon.bin",
                selectBox: @"_archives\localNox\wepsel_box.bin",
                damageBar: @"_archives\localNox\rad_ammobar.bin",
                healthMainPosX: 29,
                healthMainPosY: 0,
                healthSubPosX: 34,
                healthSubPosY: 0,
                healthOffsetY: 117,
                healthOffsetYAlt: 0,
                ammoBarPosX: 221,
                ammoBarPosY: 117,
                weaponIconPosX: 196,
                weaponIconPosY: 138,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 36,
                scorePosY: 12,
                scoreAlign: Align.Left,
                octolithPosX: 200,
                octolithPosY: 8,
                primePosX: 204,
                primePosY: 16,
                primeTextPosX: 14,
                primeTextPosY: 17,
                primeAlign: Align.Right,
                nodeBonusPosX: 40,
                nodeBonusPosY: 32,
                enemyBonusPosX: 190,
                enemyBonusPosY: 32,
                nodeIconPosX: 200,
                nodeIconPosY: 18,
                nodeTextPosX: 200,
                nodeTextPosY: 20,
                dblDmgPosX: 56,
                dblDmgPosY: 173,
                dblDmgTextPosX: 16,
                dblDmgTextPosY: -8,
                dblDmgAlign: Align.Left,
                cloakPosX: 200,
                cloakPosY: 173,
                cloakTextPosX: -16,
                cloakTextPosY: 2,
                cloakAlign: Align.Right
            ),
            // Spire
            new HudObjects(
                helmet: @"_archives\localSpire\bg_top.bin",
                helmetDrop: @"_archives\localSpire\bg_top_drop.bin",
                visor: @"_archives\localSpire\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localSpire\hud_energybar.bin",
                healthBarB: @"_archives\localSpire\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localSpire\hud_weaponicon.bin",
                doubleDamage: @"_archives\localSpire\hud_damage.bin",
                cloaking: @"_archives\localSpire\cloaking.bin",
                primeHunter: @"_archives\localSpire\hud_primehunter.bin",
                ammoBar: @"_archives\localSpire\hud_ammobar.bin",
                reticle: @"_archives\localSpire\hud_targetcircle.bin",
                sniperReticle: @"_archives\localSpire\hud_snipercircle.bin",
                weaponSelect: @"_archives\localSpire\rad_wepsel.bin",
                selectIcon: @"_archives\localSpire\wepsel_icon.bin",
                selectBox: @"_archives\localSpire\wepsel_box.bin",
                damageBar: @"_archives\localSpire\rad_ammobar.bin",
                healthMainPosX: 12,
                healthMainPosY: 0,
                healthSubPosX: 21,
                healthSubPosY: 0,
                healthOffsetY: 128,
                healthOffsetYAlt: 0,
                ammoBarPosX: 233,
                ammoBarPosY: 128,
                weaponIconPosX: 227,
                weaponIconPosY: 20,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 10,
                scorePosY: 16,
                scoreAlign: Align.Left,
                octolithPosX: 208,
                octolithPosY: 13,
                primePosX: 210,
                primePosY: 20,
                primeTextPosX: 14,
                primeTextPosY: 17,
                primeAlign: Align.Right,
                nodeBonusPosX: 37,
                nodeBonusPosY: 35,
                enemyBonusPosX: 193,
                enemyBonusPosY: 35,
                nodeIconPosX: 196,
                nodeIconPosY: 16,
                nodeTextPosX: 196,
                nodeTextPosY: 20,
                dblDmgPosX: 68,
                dblDmgPosY: 164,
                dblDmgTextPosX: 16,
                dblDmgTextPosY: -8,
                dblDmgAlign: Align.Left,
                cloakPosX: 188,
                cloakPosY: 164,
                cloakTextPosX: -16,
                cloakTextPosY: 2,
                cloakAlign: Align.Right
            ),
            // Weavel
            new HudObjects(
                helmet: @"_archives\localWeavel\bg_top.bin",
                helmetDrop: @"_archives\localWeavel\bg_top_drop.bin",
                visor: @"_archives\localWeavel\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localWeavel\hud_energybar.bin",
                healthBarB: @"_archives\localWeavel\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localWeavel\hud_weaponicon.bin",
                doubleDamage: @"_archives\localWeavel\hud_damage.bin",
                cloaking: @"_archives\localWeavel\cloaking.bin",
                primeHunter: @"_archives\localWeavel\hud_primehunter.bin",
                ammoBar: @"_archives\localWeavel\hud_ammobar.bin",
                reticle: @"_archives\localWeavel\hud_targetcircle.bin",
                sniperReticle: @"_archives\localWeavel\hud_snipercircle.bin",
                weaponSelect: @"_archives\localWeavel\rad_wepsel.bin",
                selectIcon: @"_archives\localWeavel\wepsel_icon.bin",
                selectBox: @"_archives\localWeavel\wepsel_box.bin",
                damageBar: @"_archives\localWeavel\rad_ammobar.bin",
                healthMainPosX: 22,
                healthMainPosY: 118,
                healthSubPosX: 30,
                healthSubPosY: 118,
                healthOffsetY: 0,
                healthOffsetYAlt: 0,
                ammoBarPosX: 229,
                ammoBarPosY: 118,
                weaponIconPosX: 206,
                weaponIconPosY: 104,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 128,
                scorePosY: 18,
                scoreAlign: Align.Center,
                octolithPosX: 216,
                octolithPosY: 4,
                primePosX: 214,
                primePosY: 78,
                primeTextPosX: -16,
                primeTextPosY: -10,
                primeAlign: Align.Right,
                nodeBonusPosX: 36,
                nodeBonusPosY: 4,
                enemyBonusPosX: 196,
                enemyBonusPosY: 4,
                nodeIconPosX: 128,
                nodeIconPosY: 9,
                nodeTextPosX: 128,
                nodeTextPosY: 11,
                dblDmgPosX: 88,
                dblDmgPosY: 178,
                dblDmgTextPosX: 13,
                dblDmgTextPosY: -8,
                dblDmgAlign: Align.Left,
                cloakPosX: 168,
                cloakPosY: 178,
                cloakTextPosX: 0,
                cloakTextPosY: -18,
                cloakAlign: Align.Center
            ),
            // Guardian
            new HudObjects(
                helmet: @"_archives\localWeavel\bg_top.bin", // todo: modify HUD graphics/palettes for Guardians
                helmetDrop: @"_archives\localWeavel\bg_top_drop.bin",
                visor: @"_archives\localKanden\bg_top_ovl.bin",
                scanVisor: @"_archives\localSamus\bg_top_ovl.bin",
                healthBarA: @"_archives\localSamus\hud_energybar.bin",
                healthBarB: @"_archives\localSamus\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: @"_archives\localSamus\hud_weaponicon.bin",
                doubleDamage: @"_archives\localSamus\hud_damage.bin",
                cloaking: @"_archives\localSamus\cloaking.bin",
                primeHunter: @"_archives\localSamus\hud_primehunter.bin",
                ammoBar: @"_archives\localSamus\hud_ammobar.bin",
                reticle: @"_archives\localSamus\hud_targetcircle.bin",
                sniperReticle: @"_archives\localSamus\hud_snipercircle.bin",
                weaponSelect: @"_archives\localSamus\rad_wepsel.bin",
                selectIcon: @"_archives\localSamus\wepsel_icon.bin",
                selectBox: @"_archives\localSamus\wepsel_box.bin",
                damageBar: @"_archives\localSamus\rad_ammobar.bin",
                healthMainPosX: 93,
                healthMainPosY: -5,
                healthSubPosX: 93,
                healthSubPosY: 1,
                healthOffsetY: 32,
                healthOffsetYAlt: -10,
                ammoBarPosX: 236,
                ammoBarPosY: 137,
                weaponIconPosX: 214,
                weaponIconPosY: 150,
                enemyHealthPosX: 93,
                enemyHealthPosY: 164,
                enemyHealthTextPosX: 128,
                enemyHealthTextPosY: 168,
                scorePosX: 12,
                scorePosY: 30,
                scoreAlign: Align.Left,
                octolithPosX: 228,
                octolithPosY: 28,
                primePosX: 232,
                primePosY: 42,
                primeTextPosX: -16,
                primeTextPosY: -4,
                primeAlign: Align.Right,
                nodeBonusPosX: 22,
                nodeBonusPosY: 56,
                enemyBonusPosX: 22,
                enemyBonusPosY: 80,
                nodeIconPosX: 220,
                nodeIconPosY: 41,
                nodeTextPosX: 220,
                nodeTextPosY: 45,
                dblDmgPosX: 64,
                dblDmgPosY: 174,
                dblDmgTextPosX: 16,
                dblDmgTextPosY: -8,
                dblDmgAlign: Align.Left,
                cloakPosX: 192,
                cloakPosY: 174,
                cloakTextPosX: -16,
                cloakTextPosY: 2,
                cloakAlign: Align.Right
            )
        };

        public static readonly HudMeter EnemyHealthbar = new HudMeter()
        {
            Horizontal = true,
            TankAmount = 0,
            TankCount = 0,
            Length = 0,
            TankSpacing = 0,
            TankOffsetX = 0,
            TankOffsetY = 0,
            BarOffsetX = 15,
            BarOffsetY = 6,
            Align = Align.Left,
            TextOffsetX = 30,
            TextOffsetY = 7,
            MessageId = 0
        };

        public static readonly HudMeter NodeProgressBar = new HudMeter()
        {
            Horizontal = true,
            TankAmount = 100,
            TankCount = 5,
            Length = 40,
            TankSpacing = 8,
            TankOffsetX = 1,
            TankOffsetY = -8,
            BarOffsetX = 15,
            BarOffsetY = 6,
            Align = Align.Center,
            TextOffsetX = 0,
            TextOffsetY = -7,
            MessageId = 0
        };

        public static readonly IReadOnlyList<HudMeter> MainHealthbars = new HudMeter[8]
        {
            // Samus
            new HudMeter()
            {
                Horizontal = true,
                TankAmount = 100,
                TankCount = 0,
                Length = 72,
                TankSpacing = 6,
                TankOffsetX = 1,
                TankOffsetY = 8,
                BarOffsetX = 0,
                BarOffsetY = -8,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = -8,
                MessageId = 6
            },
            // Kanden
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 8, // game has 1
                TankOffsetY = 3, // game has -8
                BarOffsetX = 32,
                BarOffsetY = -35,
                Align = Align.Right,
                TextOffsetX = 30,
                TextOffsetY = -7,
                MessageId = 0
            },
            // Trace
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 0,
                Length = 64,
                TankSpacing = 8, // game has 0
                TankOffsetX = -8, // game has 0
                TankOffsetY = 3, // game has 0
                BarOffsetX = 8,
                BarOffsetY = 0,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Sylux
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 152,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = -4,
                BarOffsetY = -66,
                Align = Align.Right,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Noxus
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 8, // game has 1
                TankOffsetY = 3, // game has -8
                BarOffsetX = -3,
                BarOffsetY = 1,
                Align = Align.Right,
                TextOffsetX = 30,
                TextOffsetY = -7,
                MessageId = 0
            },
            // Spire
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 10, // game has 1
                TankOffsetY = 3, // game has -8
                BarOffsetX = 5,
                BarOffsetY = -82,
                Align = Align.Center,
                TextOffsetX = 30,
                TextOffsetY = -7,
                MessageId = 0
            },
            // Weavel
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 64,
                TankSpacing = 8,
                TankOffsetX = 8, // game has 1
                TankOffsetY = 3, // game has -8
                BarOffsetX = 10,
                BarOffsetY = -68,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Guardian
            new HudMeter()
            {
                Horizontal = true,
                TankAmount = 100,
                TankCount = 0,
                Length = 72,
                TankSpacing = 6,
                TankOffsetX = 1,
                TankOffsetY = 8,
                BarOffsetX = 0,
                BarOffsetY = -8,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = -8,
                MessageId = 6
            }
        };

        public static readonly IReadOnlyList<HudMeter> SubHealthbars = new HudMeter[8]
        {
            // Samus
            new HudMeter()
            {
                Horizontal = true,
                TankAmount = 100,
                TankCount = 5,
                Length = 72,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 15,
                BarOffsetY = 6,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            },
            // Kanden
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 15,
                BarOffsetY = 6,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            },
            // Trace
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 0,
                Length = 64,
                TankSpacing = 0,
                TankOffsetX = 0,
                TankOffsetY = 0,
                BarOffsetX = 0,
                BarOffsetY = 0,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Sylux
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 152,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 0,
                BarOffsetY = 0,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Noxus
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 15,
                BarOffsetY = 6,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            },
            // Spire
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 15,
                BarOffsetY = 6,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            },
            // Weavel
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 64,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 0,
                BarOffsetY = 0,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Guardian
            new HudMeter()
            {
                Horizontal = true,
                TankAmount = 100,
                TankCount = 5,
                Length = 72,
                TankSpacing = 8,
                TankOffsetX = 1,
                TankOffsetY = -8,
                BarOffsetX = 15,
                BarOffsetY = 6,
                Align = Align.Left,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            }
        };

        public static readonly IReadOnlyList<HudMeter> AmmoBars = new HudMeter[8]
        {
            // Samus
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 72,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = -3,
                BarOffsetY = 0,
                Align = Align.Right,
                TextOffsetX = -2,
                TextOffsetY = -1,
                MessageId = 0
            },
            // Kanden
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = -22,
                BarOffsetY = -35,
                Align = Align.Left,
                TextOffsetX = -2,
                TextOffsetY = -1,
                MessageId = 0
            },
            // Trace
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 0,
                Length = 64,
                TankSpacing = 0,
                TankOffsetX = 0,
                TankOffsetY = 0,
                BarOffsetX = -2,
                BarOffsetY = 0,
                Align = Align.Right,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Sylux
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 152,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = 6,
                BarOffsetY = -66,
                Align = Align.Left,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Noxus
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = 9,
                BarOffsetY = 1,
                Align = Align.Left,
                TextOffsetX = -2,
                TextOffsetY = -1,
                MessageId = 0
            },
            // Spire
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 80,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = 3,
                BarOffsetY = -82,
                Align = Align.Center,
                TextOffsetX = -2,
                TextOffsetY = -1,
                MessageId = 0
            },
            // Weavel
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 64,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = -10,
                BarOffsetY = -68,
                Align = Align.Center,
                TextOffsetX = 0,
                TextOffsetY = 0,
                MessageId = 0
            },
            // Guardian
            new HudMeter()
            {
                Horizontal = false,
                TankAmount = 100,
                TankCount = 5,
                Length = 72,
                TankSpacing = 8,
                TankOffsetX = 8,
                TankOffsetY = 1,
                BarOffsetX = -3,
                BarOffsetY = 0,
                Align = Align.Right,
                TextOffsetX = -2,
                TextOffsetY = -1,
                MessageId = 0
            }
        };
    }

    public class HudObjects
    {
        public readonly string Helmet;
        public readonly string HelmetDrop;
        public readonly string Visor;
        public readonly string ScanVisor;
        public readonly string HealthBarA;
        public readonly string HealthBarB;
        public readonly string? EnergyTanks;
        public readonly string WeaponIcon;
        public readonly string DoubleDamage;
        public readonly string Cloaking;
        public readonly string PrimeHunter;
        public readonly string AmmoBar;
        public readonly string Reticle;
        public readonly string SniperReticle;
        public readonly string WeaponSelect;
        public readonly string SelectIcon;
        public readonly string SelectBox;
        public readonly string DamageBar;
        public readonly int HealthMainPosX;
        public readonly int HealthSubPosX;
        public readonly int HealthSubPosY;
        public readonly int HealthOffsetY;
        public readonly int HealthOffsetYAlt;
        public readonly int HealthMainPosY;
        public readonly int AmmoBarPosX;
        public readonly int AmmoBarPosY;
        public readonly int WeaponIconPosX;
        public readonly int WeaponIconPosY;
        public readonly int EnemyHealthPosX;
        public readonly int EnemyHealthPosY;
        public readonly int EnemyHealthTextPosX;
        public readonly int EnemyHealthTextPosY;
        public readonly int ScorePosX;
        public readonly int ScorePosY;
        public readonly Align ScoreAlign;
        public readonly int OctolithPosX;
        public readonly int OctolithPosY;
        public readonly int PrimePosX;
        public readonly int PrimePosY;
        public readonly int PrimeTextPosX;
        public readonly int PrimeTextPosY;
        public readonly Align PrimeAlign;
        public readonly int NodeBonusPosX;
        public readonly int NodeBonusPosY;
        public readonly int EnemyBonusPosX;
        public readonly int EnemyBonusPosY;
        public readonly int NodeIconPosX;
        public readonly int NodeIconPosY;
        public readonly int NodeTextPosX;
        public readonly int NodeTextPosY;
        public readonly int DblDmgPosX;
        public readonly int DblDmgPosY;
        public readonly int DblDmgTextPosX;
        public readonly int DblDmgTextPosY;
        public readonly Align DblDmgAlign;
        public readonly int CloakPosX;
        public readonly int CloakPosY;
        public readonly int CloakTextPosX;
        public readonly int CloakTextPosY;
        public readonly Align CloakAlign;

        public HudObjects(string helmet, string helmetDrop, string visor, string scanVisor, string healthBarA, string healthBarB,
            string? energyTanks, string weaponIcon, string doubleDamage, string cloaking, string primeHunter, string ammoBar,
            string reticle, string sniperReticle, string weaponSelect, string selectIcon,
            string selectBox, string damageBar, int healthMainPosX, int healthMainPosY, int healthSubPosX, int healthSubPosY,
            int healthOffsetY, int healthOffsetYAlt, int ammoBarPosX, int ammoBarPosY, int weaponIconPosX, int weaponIconPosY,
            int enemyHealthPosX, int enemyHealthPosY, int enemyHealthTextPosX, int enemyHealthTextPosY, int scorePosX,
            int scorePosY, Align scoreAlign, int octolithPosX, int octolithPosY, int primePosX, int primePosY, int primeTextPosX,
            int primeTextPosY, Align primeAlign, int nodeBonusPosX, int nodeBonusPosY, int enemyBonusPosX, int enemyBonusPosY,
            int nodeIconPosX, int nodeIconPosY, int nodeTextPosX, int nodeTextPosY, int dblDmgPosX, int dblDmgPosY,
            int dblDmgTextPosX, int dblDmgTextPosY, Align dblDmgAlign, int cloakPosX, int cloakPosY, int cloakTextPosX,
            int cloakTextPosY, Align cloakAlign)
        {
            Helmet = helmet;
            HelmetDrop = helmetDrop;
            Visor = visor;
            ScanVisor = scanVisor;
            HealthBarA = healthBarA;
            HealthBarB = healthBarB;
            EnergyTanks = energyTanks;
            WeaponIcon = weaponIcon;
            DoubleDamage = doubleDamage;
            Cloaking = cloaking;
            PrimeHunter = primeHunter;
            AmmoBar = ammoBar;
            Reticle = reticle;
            SniperReticle = sniperReticle;
            WeaponSelect = weaponSelect;
            SelectIcon = selectIcon;
            SelectBox = selectBox;
            DamageBar = damageBar;
            HealthMainPosX = healthMainPosX;
            HealthMainPosY = healthMainPosY;
            HealthSubPosX = healthSubPosX;
            HealthSubPosY = healthSubPosY;
            HealthOffsetY = healthOffsetY;
            HealthOffsetYAlt = healthOffsetYAlt;
            AmmoBarPosX = ammoBarPosX;
            AmmoBarPosY = ammoBarPosY;
            WeaponIconPosX = weaponIconPosX;
            WeaponIconPosY = weaponIconPosY;
            EnemyHealthPosX = enemyHealthPosX;
            EnemyHealthPosY = enemyHealthPosY;
            EnemyHealthTextPosX = enemyHealthTextPosX;
            EnemyHealthTextPosY = enemyHealthTextPosY;
            ScorePosX = scorePosX;
            ScorePosY = scorePosY;
            ScoreAlign = scoreAlign;
            OctolithPosX = octolithPosX;
            OctolithPosY = octolithPosY;
            PrimePosX = primePosX;
            PrimePosY = primePosY;
            PrimeTextPosX = primeTextPosX;
            PrimeTextPosY = primeTextPosY;
            PrimeAlign = primeAlign;
            NodeBonusPosX = nodeBonusPosX;
            NodeBonusPosY = nodeBonusPosY;
            EnemyBonusPosX = enemyBonusPosX;
            EnemyBonusPosY = enemyBonusPosY;
            NodeIconPosX = nodeIconPosX;
            NodeIconPosY = nodeIconPosY;
            NodeTextPosX = nodeTextPosX;
            NodeTextPosY = nodeTextPosY;
            DblDmgPosX = dblDmgPosX;
            DblDmgPosY = dblDmgPosY;
            DblDmgTextPosX = dblDmgTextPosX;
            DblDmgTextPosY = dblDmgTextPosY;
            DblDmgAlign = dblDmgAlign;
            CloakPosX = cloakPosX;
            CloakPosY = cloakPosY;
            CloakTextPosX = cloakTextPosX;
            CloakTextPosY = cloakTextPosY;
            CloakAlign = cloakAlign;
        }
    }
}
