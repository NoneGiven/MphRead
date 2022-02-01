using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MphRead.Text
{
    public static class Strings
    {
        // todo: language support
        private static readonly Dictionary<string, IReadOnlyList<StringTableEntry>> _cache
            = new Dictionary<string, IReadOnlyList<StringTableEntry>>();

        public static IReadOnlyList<StringTableEntry> ReadStringTable(string name, Language language = Language.English)
        {
            if (_cache.TryGetValue(name, out IReadOnlyList<StringTableEntry>? table))
            {
                return table;
            }
            var entries = new List<StringTableEntry>();
            string path = Path.Combine(Paths.FileSystem, GetFolder(language), name);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            // ScanLog has an 8-byte header and 8 bytes between the last entry and first string,
            // which are related to parsing max string length and not necessary for us to use
            int offset = name == StringTables.ScanLog ? 8 : 4;
            foreach (RawStringTableEntry entry in Read.DoOffsets<RawStringTableEntry>(bytes, offset, count))
            {
                // A76E has invalid offsets on some entries
                // todo?: are those not supposed to be parsed? (e.g. boost)
                if (entry.Offset < bytes.Length)
                {
                    string value = Read.ReadString(bytes, entry.Offset, entry.Length);
                    entries.Add(new StringTableEntry(entry, value));
                }
            }
            _cache.Add(name, entries);
            return entries;
        }

        public static string GetHudMessage(int id, Language language = Language.English)
        {
            return GetHudMessage((uint)id, language);
        }

        public static string GetHudMessage(uint id, Language language = Language.English)
        {
            if (id >= 1 && id <= 11)
            {
                return GetMessage('H', id, StringTables.HudMsgsCommon, language);
            }
            if (id >= 101 && id <= 122)
            {
                return GetMessage('H', id, StringTables.HudMessagesSP, language);
            }
            if (id >= 201 && id <= 257)
            {
                return GetMessage('H', id, StringTables.HudMessagesMP, language);
            }
            if (id >= 301 && id <= 305)
            {
                return GetMessage('W', id - 300, StringTables.HudMessagesMP, language);
            }
            return " ";
        }

        public static string GetMessage(char type, int id, string table, Language language = Language.English)
        {
            return GetMessage(type, (uint)id, table, language);
        }

        public static string GetMessage(char type, uint id, string table, Language language = Language.English)
        {
            string fullId = type + id.ToString().PadLeft(3, '0');
            IReadOnlyList<StringTableEntry> list = ReadStringTable(table, language);
            for (int i = 0; i < list.Count; i++)
            {
                StringTableEntry entry = list[i];
                if (entry.Id == fullId)
                {
                    return entry.Value;
                }
            }
            return " ";
        }

        private static string GetFolder(Language language)
        {
            string folder = "stringTables";
            if (language == Language.French)
            {
                folder += "_fr";
            }
            else if (language == Language.German)
            {
                folder += "_gr";
            }
            else if (language == Language.Italian)
            {
                folder += "_it";
            }
            else if (language == Language.Japanese)
            {
                folder += "_jp";
            }
            else if (language == Language.Spanish)
            {
                folder += "_sp";
            }
            return folder;
        }

        public static IReadOnlyList<string> ReadTextFile(Language language = Language.English,
            bool useEnGb = false, bool downloadPlay = false)
        {
            string suffix = useEnGb && !downloadPlay ? "en-gb" : "en";
            if (language == Language.French)
            {
                suffix = "fr";
            }
            else if (language == Language.German)
            {
                suffix = "de";
            }
            else if (language == Language.Italian)
            {
                suffix = "it";
            }
            else if (language == Language.Japanese)
            {
                suffix = "jp";
            }
            else if (language == Language.Spanish)
            {
                suffix = "es";
            }
            string prefix = downloadPlay ? "single_" : "";
            string name = $"{prefix}metroidhunters_text_{suffix}.bin";
            string path = Path.Combine(Paths.FileSystem, "frontend", name);
            if (suffix == "en-gb")
            {
                path = path.Replace("amhe0", "amhp1");
            }
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            // in practice the entries are always tightly packed in order, but we'll read them through the offsets anyway
            int offset = 0;
            var list = new List<uint>();
            while (true)
            {
                uint item = Read.SpanReadUint(bytes, ref offset);
                if (item == 0)
                {
                    break;
                }
                list.Add(item);
            }
            var strings = new List<string>();
            foreach (uint item in list)
            {
                TextFileEntry entry = Read.DoOffset<TextFileEntry>(bytes, item);
                Debug.Assert(entry.Offset1 == entry.Offset2);
                Debug.Assert(entry.Length1 == entry.Length2);
                string text = Read.ReadString(bytes, entry.Offset1, entry.Length1);
                while (text.Length != entry.Length1)
                {
                    text += '\0';
                }
                strings.Add(text);
            }
            return strings;
        }
    }

    public static class StringTables
    {
        public static readonly string GameMessages = "GameMessages.bin";
        public static readonly string HudMessagesMP = "HudMessagesMP.bin";
        public static readonly string HudMessagesSP = "HudMessagesSP.bin";
        public static readonly string HudMsgsCommon = "HudMsgsCommon.bin";
        public static readonly string LocationNames = "LocationNames.bin";
        public static readonly string MBBanner = "MBBanner.bin";
        public static readonly string ScanLog = "ScanLog.bin";
        public static readonly string ShipInSpace = "ShipInSpace.bin";
        public static readonly string ShipOnGround = "ShipOnGround.bin";
        public static readonly string WeaponNames = "WeaponNames.bin";

        public static IEnumerable<string> All { get; } = new List<string>()
        {
            GameMessages, HudMessagesMP, HudMessagesSP, HudMsgsCommon, LocationNames,
            MBBanner, ScanLog, ShipInSpace, ShipOnGround, WeaponNames
        };
    }

    public static class Font
    {
        public static IReadOnlyList<int> Widths { get; private set; } = null!;

        public static IReadOnlyList<int> Offsets { get; private set; } = null!;

        public static IReadOnlyList<byte> CharacterData { get; private set; } = null!;

        public static void SetData(byte[] widths, byte[] offsets, byte[] chars)
        {
            int[] widthData = new int[widths.Length];
            for (int i = 0; i < widths.Length; i++)
            {
                widthData[i] = widths[i];
            }
            Widths = widthData;
            int[] offsetData = new int[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsetData[i] = (sbyte)offsets[i];
            }
            Offsets = offsetData;
            Debug.Assert(chars.Length > 0 && chars.Length % 2 == 0);
            byte[] charData = new byte[chars.Length * 2];
            for (int i = 0; i < chars.Length; i++)
            {
                byte data = chars[i];
                charData[i * 2] = (byte)(data & 0xF);
                charData[i * 2 + 1] = (byte)(data >> 4);
            }
            CharacterData = charData;
        }
    }
}
