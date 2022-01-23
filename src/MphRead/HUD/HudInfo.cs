using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MphRead.Hud
{
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
            public readonly int Field0;
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

        public static int CharMapToTexture(string path, Scene scene)
        {
            // todo: does this file define the palette (16/256 colors, etc.) or screen size (256x256, 512x512, etc.)?
            // --> if not, where does the game get that info when setting it on the hardware?
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, path)));
            UiPartHeader header = Read.ReadStruct<UiPartHeader>(bytes);
            Debug.Assert(header.Magic == 0);
            int offset = Marshal.SizeOf<UiPartHeader>();
            IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
            offset += header.CharDataSize;
            Debug.Assert(header.PalDataSize % 2 == 0);
            IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
            offset += header.PalDataSize;
            ScrDatInfo info = Read.DoOffset<ScrDatInfo>(bytes, offset);
            offset += Marshal.SizeOf<ScrDatInfo>();
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

        public static void Test(Scene? scene)
        {
            var files = new List<string>()
            {
                //"hud/samus/bg_bottom.bin",
                //"hud/samus/bg_bottomL.bin",
                //"_archives/localSamus/map_grid.bin",
                //"hud/samus/bg_altform.bin",
                //"hud/samus/bg_altformL.bin",
                //"hud/samus/bg_wepsel.bin",
                //"hud/samus/bg_wepselL.bin",
                //"_archives/spSamus/bg_scanjewel.bin",
                //"_archives/spSamus/map_scan.bin",
                //"_archives/localSamus/bg_top_ovl.bin",
                //"_archives/localSamus/bg_top.bin",
                //"_archives/localSamus/bg_top_drop.bin",
                "_archives/common/bg_ice.bin"
            };
            // todo: does this file define the palette (16/256 colors, etc.) or screen size (256x256, 512x512, etc.)?
            // --> if not, where does the game get that info when setting it on the hardware?
            foreach (string file in files)
            {
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(Paths.FileSystem, file)));
                UiPartHeader header = Read.ReadStruct<UiPartHeader>(bytes);
                Debug.Assert(header.Magic == 0);
                int offset = Marshal.SizeOf<UiPartHeader>();
                IReadOnlyList<byte> characterData = Read.DoOffsets<byte>(bytes, offset, header.CharDataSize);
                offset += header.CharDataSize;
                Debug.Assert(header.PalDataSize % 2 == 0);
                IReadOnlyList<ushort> paletteData = Read.DoOffsets<ushort>(bytes, offset, header.PalDataSize / 2);
                offset += header.PalDataSize;
                ScrDatInfo info = Read.DoOffset<ScrDatInfo>(bytes, offset);
                offset += Marshal.SizeOf<ScrDatInfo>();
                Debug.Assert(info.ScrDataSize % 2 == 0);
                IReadOnlyList<ushort> screenDataValues = Read.DoOffsets<ushort>(bytes, offset, info.ScrDataSize / 2);

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
                //Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), $"_test", 256, 256, texture);
                if (scene != null)
                {
                    scene.IceLayerBindingId = scene.BindGetTexture(texture, 256, 256);
                }

                // for getting the entire screen:
                //var texture = new ColorRgba[512 * 256];
                //for (int cy = 0; cy < 32; cy++)
                //{
                //    for (int cx = 0; cx < 64; cx++)
                //    {
                //        int idx = cy * 32 + ((cx / 32) == 1 ? 0x400 + (cx - 32) : cx);
                //        ScreenData screen = screenData[idx];
                //        //Console.Write($"{screen.CharacterId} ");
                //        Debug.Assert(screen.PaletteId == 0);
                //        List<ColorRgba> character = characters[screen.CharacterId];
                //        int start = cy * 64 * 8 * 8 + cx * 8;
                //        for (int py = 0; py < 8; py++)
                //        {
                //            int iy = py;
                //            if (screen.FlipVertical)
                //            {
                //                iy = 7 - py;
                //            }
                //            for (int px = 0; px < 8; px++)
                //            {
                //                int ix = px;
                //                if (screen.FlipHorizontal)
                //                {
                //                    ix = 7 - px;
                //                }
                //                ColorRgba pixel = character[iy * 8 + ix];
                //                int index = start + py * 64 * 8 + px;
                //                texture[index] = pixel;
                //            }
                //        }
                //    }
                //}
                //Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), $"_test_full", 512, 256, texture);

                //for (int i = 0; i < characters.Count; i++)
                //{
                //    List<ColorRgba> character = characters[i];
                //    Images.SaveTexture(Path.Combine(Paths.Export, "_ice"), $"{i.ToString().PadLeft(3, '0')}", 8, 8, character);
                //}

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

        public static IEnumerable<string> All { get; } = new List<string>()
        {
            IceLayer
        };
    }
}
