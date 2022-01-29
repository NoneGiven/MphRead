using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Hud
{
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
        public int TextType; // sktodo: enum
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

        public HudObject(int width, int height, IReadOnlyList<byte> characterData, IReadOnlyList<ColorRgba> paletteData)
        {
            Width = width;
            Height = height;
            CharacterData = characterData;
            PaletteData = paletteData;
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
        public IReadOnlyList<byte>? CharacterData;
        public int PaletteIndex = -1;
        public IReadOnlyList<ColorRgba>? PaletteData;
        public readonly ColorRgba[] Texture;
        public float Alpha = 1;
        public int BindingId = -1;
        // ltodo?: if we find a "non-linear" example, then this will need to be adjusted
        // --> also going to need a loop flag and control over animation speed (e.g. blinking Octolith icon)
        public int CurrentFrame;
        public int StartFrame;
        public int TargetFrame;
        public float Timer = -1;
        public float Time = -1;

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
            Timer = 0;
            CharacterData = charData;
            CurrentFrame = charFrame;
            PaletteData = palData;
            PaletteIndex = palIndex;
            DoTexture(scene);
        }

        public void SetData(int charFrame, int palIndex, Scene scene)
        {
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
                            Texture[index] = palIndex == 0 ? new ColorRgba() : PaletteData[palOffset + palIndex];
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

        public void SetAnimation(int start, int target, int frames)
        {
            if (start == target)
            {
                CurrentFrame = start;
            }
            else
            {
                CurrentFrame = start;
                StartFrame = start;
                TargetFrame = target;
                Timer = Time = frames * (1 / 30f);
            }
            // no need to update textures since ProcessAnimation will do it
        }

        public void ProcessAnimation(Scene scene)
        {
            if (Timer > 0)
            {
                int prev = CurrentFrame;
                Timer -= scene.FrameTime;
                if (Timer <= 0)
                {
                    CurrentFrame = TargetFrame;
                }
                else
                {
                    CurrentFrame = StartFrame + (int)MathF.Round((TargetFrame - StartFrame) * (1 - Timer / Time));
                }
                if (CurrentFrame != prev)
                {
                    DoTexture(scene);
                }
            }
        }
    }

    public class HudInfo
    {
        private readonly struct UiPartHeader
        {
            public readonly int Magic; // always zero
            public readonly int CharDataSize;
            public readonly int PalDataSize;
        }

        private readonly struct ScrDatInfo
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

        public static int CharMapToTexture(string path, Scene scene)
        {
            // todo: does this file define the palette (16/256 colors, etc.) or screen size (256x256, 512x512, etc.)?
            // --> if not, where does the game get that info when setting it on the hardware?
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
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
                        character.Add(new ColorRgba(paletteData[index1]));
                        int index2 = (data & 0xF0) >> 4;
                        character.Add(new ColorRgba(paletteData[index2]));
                    }
                }
                characters.Add(character);
            }

            var texture = new ColorRgba[256 * 256];
            for (int cy = 0; cy < 32; cy++)
            {
                for (int cx = 0; cx < 32; cx++) // skip last 16 of 64
                {
                    int icx = cx + 16; // skip first 16 of 64
                    int idx = cy * 32 + ((icx / 32) == 1 ? 0x400 + (icx - 32) : icx); // deal with the "split" addresses
                    ScreenData screen = screenData[idx];
                    Debug.Assert(screen.PaletteId == 0);
                    List<ColorRgba> character = characters[screen.CharacterId];
                    int start = cy * 32 * 8 * 8 + cx * 8;
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
                            int index = start + py * 32 * 8 + px;
                            texture[index] = pixel;
                        }
                    }
                }
            }
            return scene.BindGetTexture(texture, 256, 256);
        }

        private static readonly (int Width, int Height)[,] _objectDimensions = new (int, int)[3, 4]
        {
            // tiny    small  medium   large
            { (1, 1), (2, 2), (4, 4), (8, 8) }, // square
            { (2, 1), (4, 1), (4, 2), (8, 4) }, // wide
            { (1, 2), (1, 4), (2, 4), (4, 8) }  // tall
        };

        private readonly struct UiObjectHeader
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

        private readonly struct UiAnimParams
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
            // skodo: parse characters for each palette that's actually specified by the attr, I guess?
            // --> some just have 1 in all the attrs instead of 0 in all the attrs, so just use that?
            (int width, int height) = _objectDimensions[(int)attrs.Shape, (int)attrs.Size];
            return new HudObject(width * 8, height * 8, palIndexData, palColorData);
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
                            character.Add(new ColorRgba(paletteData[index1]));
                            int index2 = (data & 0xF0) >> 4;
                            character.Add(new ColorRgba(paletteData[index2]));
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
                //Export.Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), name, width, height, texture);

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

    public static class HudElements
    {
        public static readonly string IceLayer = @"_archives\common\bg_ice.bin";

        public static IEnumerable<string> AllLayers { get; } = new List<string>()
        {
            IceLayer
        };

        public static IReadOnlyList<HudObjects> HunterObjects = new HudObjects[8]
        {
            // Samus
            new HudObjects(
                helmet: @"_archives\localSamus\bg_top.bin",
                helmetDrop: @"_archives\localSamus\bg_top_drop.bin",
                visor: @"_archives\localSamus\bg_top_ovl.bin",
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
                scanBox: @"_archives\spSamus\hud_scanbox.bin",
                messageBox: @"_archives\spSamus\hud_msgBox.bin",
                weaponSelect: @"_archives\localSamus\rad_wepsel.bin",
                selectIcon: @"_archives\localSamus\wepsel_icon.bin",
                selectBox: @"_archives\localSamus\wepsel_box.bin",
                healthMainPosX: 93,
                healthMainPosY: -5,
                healthSubPosX: 93,
                healthSubPosY: 1,
                healthOffsetY: 32,
                healthOffsetYAlt: -10
            ),
            // Kanden
            new HudObjects(
                helmet: @"_archives\localKanden\bg_top.bin",
                helmetDrop: @"_archives\localKanden\bg_top_drop.bin",
                visor: @"_archives\localKanden\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null, // todo: SP stuff for other hunters
                weaponSelect: @"_archives\localKanden\rad_wepsel.bin",
                selectIcon: @"_archives\localKanden\wepsel_icon.bin",
                selectBox: @"_archives\localKanden\wepsel_box.bin",
                healthMainPosX: 13,
                healthMainPosY: 0,
                healthSubPosX: 20,
                healthSubPosY: 0,
                healthOffsetY: 128,
                healthOffsetYAlt: 0
            ),
            // Trace
            new HudObjects(
                helmet: @"_archives\localTrace\bg_top.bin",
                helmetDrop: @"_archives\localTrace\bg_top_drop.bin",
                visor: @"_archives\localTrace\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localTrace\rad_wepsel.bin",
                selectIcon: @"_archives\localTrace\wepsel_icon.bin",
                selectBox: @"_archives\localTrace\wepsel_box.bin",
                healthMainPosX: 24,
                healthMainPosY: 135,
                healthSubPosX: 29,
                healthSubPosY: 135,
                healthOffsetY: 0,
                healthOffsetYAlt: 0
            ),
            // Sylux
            new HudObjects(
                helmet: @"_archives\localSylux\bg_top.bin",
                helmetDrop: @"_archives\localSylux\bg_top_drop.bin",
                visor: @"_archives\localSylux\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localSylux\rad_wepsel.bin",
                selectIcon: @"_archives\localSylux\wepsel_icon.bin",
                selectBox: @"_archives\localSylux\wepsel_box.bin",
                healthMainPosX: 47,
                healthMainPosY: 165,
                healthSubPosX: 51,
                healthSubPosY: 165,
                healthOffsetY: 0,
                healthOffsetYAlt: 0
            ),
            // Noxus
            new HudObjects(
                helmet: @"_archives\localNox\bg_top.bin",
                helmetDrop: @"_archives\localNox\bg_top_drop.bin",
                visor: @"_archives\localNox\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localNox\rad_wepsel.bin",
                selectIcon: @"_archives\localNox\wepsel_icon.bin",
                selectBox: @"_archives\localNox\wepsel_box.bin",
                healthMainPosX: 29,
                healthMainPosY: 0,
                healthSubPosX: 34,
                healthSubPosY: 0,
                healthOffsetY: 117,
                healthOffsetYAlt: 0
            ),
            // Spire
            new HudObjects(
                helmet: @"_archives\localSpire\bg_top.bin",
                helmetDrop: @"_archives\localSpire\bg_top_drop.bin",
                visor: @"_archives\localSpire\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localSpire\rad_wepsel.bin",
                selectIcon: @"_archives\localSpire\wepsel_icon.bin",
                selectBox: @"_archives\localSpire\wepsel_box.bin",
                healthMainPosX: 12,
                healthMainPosY: 0,
                healthSubPosX: 21,
                healthSubPosY: 0,
                healthOffsetY: 128,
                healthOffsetYAlt: 0
            ),
            // Weavel
            new HudObjects(
                helmet: @"_archives\localWeavel\bg_top.bin",
                helmetDrop: @"_archives\localWeavel\bg_top_drop.bin",
                visor: @"_archives\localWeavel\bg_top_ovl.bin",
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
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localWeavel\rad_wepsel.bin",
                selectIcon: @"_archives\localWeavel\wepsel_icon.bin",
                selectBox: @"_archives\localWeavel\wepsel_box.bin",
                healthMainPosX: 22,
                healthMainPosY: 118,
                healthSubPosX: 30,
                healthSubPosY: 118,
                healthOffsetY: 0,
                healthOffsetYAlt: 0
            ),
            // Guardian
            new HudObjects(
                helmet: "", // todo: all HUD stuff for guardians
                helmetDrop: "",
                visor: "",
                healthBarA: @"_archives\localSamus\hud_energybar.bin",
                healthBarB: @"_archives\localSamus\hud_energybar2.bin",
                energyTanks: @"_archives\spSamus\hud_etank.bin",
                weaponIcon: "",
                doubleDamage: "",
                cloaking: "",
                primeHunter: "",
                ammoBar: "",
                reticle: @"_archives\localSamus\hud_targetcircle.bin",
                sniperReticle: @"_archives\localSamus\hud_snipercircle.bin",
                scanBox: null,
                messageBox: null,
                weaponSelect: @"_archives\localSamus\rad_wepsel.bin",
                selectIcon: @"_archives\localSamus\wepsel_icon.bin",
                selectBox: @"_archives\localSamus\wepsel_box.bin",
                healthMainPosX: 93,
                healthMainPosY: -5,
                healthSubPosX: 93,
                healthSubPosY: 1,
                healthOffsetY: 32,
                healthOffsetYAlt: -10
            )
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
                TextType = 0,
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
                TextType = 1,
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
                TankOffsetX = 8, // game has 0
                TankOffsetY = 3, // game has 0
                BarOffsetX = 8,
                BarOffsetY = 0,
                TextType = 0,
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
                TextType = 1,
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
                TextType = 1,
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
                TextType = 2,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
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
                TextType = 0,
                TextOffsetX = 30,
                TextOffsetY = 7,
                MessageId = 0
            }
        };
    }

    public class HudObjects
    {
        public readonly string Helmet;
        public readonly string HelmetDrop;
        public readonly string Visor;
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
        public readonly string? ScanBox;
        public readonly string? MessageBox;
        public readonly string WeaponSelect;
        public readonly string SelectIcon;
        public readonly string SelectBox;
        public readonly int HealthMainPosX;
        public readonly int HealthMainPosY;
        public readonly int HealthSubPosX;
        public readonly int HealthSubPosY;
        public readonly int HealthOffsetY;
        public readonly int HealthOffsetYAlt;

        public HudObjects(string helmet, string helmetDrop, string visor, string healthBarA, string healthBarB, string? energyTanks,
            string weaponIcon, string doubleDamage, string cloaking, string primeHunter, string ammoBar, string reticle,
            string sniperReticle, string? scanBox, string? messageBox, string weaponSelect, string selectIcon, string selectBox,
            int healthMainPosX, int healthMainPosY, int healthSubPosX, int healthSubPosY, int healthOffsetY, int healthOffsetYAlt)
        {
            Helmet = helmet;
            HelmetDrop = helmetDrop;
            Visor = visor;
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
            ScanBox = scanBox;
            MessageBox = messageBox;
            WeaponSelect = weaponSelect;
            SelectIcon = selectIcon;
            SelectBox = selectBox;
            HealthMainPosX = healthMainPosX;
            HealthMainPosY = healthMainPosY;
            HealthSubPosX = healthSubPosX;
            HealthSubPosY = healthSubPosY;
            HealthOffsetY = healthOffsetY;
            HealthOffsetYAlt = healthOffsetYAlt;
        }
    }
}
