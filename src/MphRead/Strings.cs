using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MphRead.Text
{
    public static class Strings
    {
        // todo: language support
        private static readonly Dictionary<string, IReadOnlyList<StringTableEntry>> _cache
            = new Dictionary<string, IReadOnlyList<StringTableEntry>>();

        public static void ClearCache()
        {
            _cache.Clear();
        }

        public static IReadOnlyList<StringTableEntry> ReadStringTable(string name, Language language = Language.English)
        {
            if (_cache.TryGetValue(name, out IReadOnlyList<StringTableEntry>? table))
            {
                return table;
            }
            var entries = new List<StringTableEntry>();
            string path = Paths.Combine(Paths.FileSystem, GetFolder(language), name);
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
                    string value = Read.ReadStringTable(bytes, entry.Offset, entry.Length);
                    value = value.Replace("$", "");
                    char prefix = '\0';
                    if (name == StringTables.GameMessages)
                    {
                        prefix = value[0];
                        value = value[1..];
                    }
                    string value1 = value;
                    string value2 = "";
                    int slashCount = value.Count(c => c == '\\');
                    if (slashCount == 1)
                    {
                        string[] split = value.Split('\\');
                        value1 = split[0];
                        value2 = split[1];
                    }
                    entries.Add(new StringTableEntry(entry, prefix, value1, value2));
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
            StringTableEntry? entry = GetEntry(type, id, table, language);
            return entry?.Value1 ?? " ";
        }

        public static StringTableEntry? GetEntry(char type, int id, string table, Language language = Language.English)
        {
            return GetEntry(type, (uint)id, table, language);
        }

        public static StringTableEntry? GetEntry(char type, uint id, string table, Language language = Language.English)
        {
            string fullId = $"{type}{id:000}";
            IReadOnlyList<StringTableEntry> list = ReadStringTable(table, language);
            for (int i = 0; i < list.Count; i++)
            {
                StringTableEntry entry = list[i];
                if (entry.Id == fullId)
                {
                    return entry;
                }
            }
            return null;
        }

        private static readonly IReadOnlyDictionary<char, int> _categoryMap = new Dictionary<char, int>()
        {
            ['L'] = 0,
            ['l'] = 0,
            ['B'] = 1,
            ['b'] = 1,
            ['O'] = 2,
            ['o'] = 2,
            ['E'] = 3,
            ['e'] = 3,
            ['X'] = 4,
            ['x'] = 4
        };

        public static readonly StringTableEntry EmptyScanEntry = new StringTableEntry(
            id: "000",
            prefix: '\0',
            value1: "INVALID LOG ENTRY",
            value2: "This object has no entry in the log book.",
            speed: 0,
            category: 'S'
        );

        public static StringTableEntry? GetScanEntry(int scanId)
        {
            return GetEntry('L', (uint)scanId, StringTables.ScanLog);
        }

        public static int GetScanEntryCategory(int scanId)
        {
            // todo: languagel
            StringTableEntry? entry = GetEntry('L', (uint)scanId, StringTables.ScanLog);
            if (entry == null)
            {
                return 0;
            }
            if (_categoryMap.TryGetValue(entry.Category, out int result))
            {
                return result;
            }
            return 5;
        }

        public static float GetScanEntryTime(int scanId)
        {
            StringTableEntry? entry = GetEntry('L', (uint)scanId, StringTables.ScanLog);
            if (entry == null)
            {
                return 60 / 30f;
            }
            return 10 * (entry.Speed & 7) / 30f;
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
            else if (language == Language.Japanese || Paths.MphKey == "AMHK0")
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
            string path = Paths.Combine(Paths.FileSystem, "frontend", name);
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

        private static readonly IReadOnlyList<string> _nonAscii = new string[]
        {
            "€", " ", "‚", "ƒ", "„", "…", "†", "‡", "ˆ", "‰", "Š", "‹", "Œ", " ", "Ž", " ", " ", "‘", "’", "“", "”", "•", "–", "—", "˜", "™",
            "š", "›", "œ", " ", "ž", "Ÿ", " ", "¡", "¢", "£", "¤", "¥", "¦", "§", "¨", "©", "ª", "«", "¬", "–", "®", "¯", "°", "±", "²", "³",
            "´", "µ", "¶", "·", "¸", "¹", "º", "»", "¼", "½", "¾", "¿", "À", "Á", "Â", "Ã", "Ä", "Å", "Æ", "Ç", "È", "É", "Ê", "Ë", "Ì", "Í",
            "Î", "Ï", "Ð", "Ñ", "Ò", "Ó", "Ô", "Õ", "Ö", "×", "Ø", "Ù", "Ú", "Û", "Ü", "Ý", "Þ", "ß", "à", "á", "â", "ã", "ä", "å", "æ", "ç",
            "è", "é", "ê", "ë", "ì", "í", "î", "ï", "ð", "ñ", "ò", "ó", "ô", "õ", "ö", "÷", "ø", "ù", "ú", "û", "ü", "ý", "þ", "ÿ", " ",
            "ぁ", "あ", "ぃ", "い", "ぅ", "う", "ぇ", "え", "ぉ", "お", "か", "が", "き", "ぎ", "く", "ぐ", "け", "げ", "こ", "ご", "さ", "ざ",
            "し", "じ", "す", "ず", "せ", "ぜ", "そ", "ぞ", "た", "だ", "ち", "ぢ", "っ", "つ", "づ", "て", "で", "と", "ど", "な", "に", "ぬ",
            "ね", "の", "は", "ば", "ぱ", "ひ", "び", "ぴ", "ふ", "ぶ", "ぷ", "へ", "べ", "ぺ", "ほ", "ぼ", "ぽ", "ま", "み", "む", "め", "も",
            "ゃ", "や", "ゅ", "ゆ", "ょ", "よ", "ら", "り", "る", "れ", "ろ", "ゎ", "わ", "ゐ", "ゑ", "を", "ん", "ァ", "ア", "ィ", "イ", "ゥ",
            "ウ", "ェ", "エ", "ォ", "オ", "カ", "ガ", "キ", "ギ", "ク", "グ", "ケ", "ゲ", "コ", "ゴ", "サ", "ザ", "シ", "ジ", "ス", "ズ", "セ",
            "ゼ", "ソ", "ゾ", "タ", "ダ", "チ", "ヂ", "ッ", "ツ", "ヅ", "テ", "デ", "ト", "ド", "ナ", "ニ", "ヌ", "ネ", "ノ", "ハ", "バ", "パ",
            "ヒ", "ビ", "ピ", "フ", "ブ", "プ", "ヘ", "ベ", "ペ", "ホ", "ボ", "ポ", "マ", "ミ", "ム", "メ", "モ", "ャ", "ヤ", "ュ", "ユ", "ョ",
            "ヨ", "ラ", "リ", "ル", "レ", "ロ", "ヮ", "ワ", "ヰ", "ヱ", "ヲ", "ン", "ヴ", "ヵ", "ㇰ", " ", " ", " ", " ", " ", " ", " ",
            "、", "。", "'", "・", "・", ":", ";", "?", "!", "゛", "゜", "´", "`", "¨", "^", "‾", "_", " ", " ", "ゝ", "ゞ", " ", " ",
            "々", " ", " ", "–", "—", "−", "／", "＼", "˜", " ", "|", "…", " ", "'", "'", "\"", "\"", "(", ")", "(", ")", "[", "]", "{", "}",
            "<", ">", " ", " ", "「", "」", " ", " ", " ", " ", "+", "-", "±", "×", "÷", "=", " ", " ", " ", " ", " ", "∞", "∴", " ", " ",
            "°", "ᐟ", "ᐥ ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ",
            " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " "
        };

        public static string ReplaceNonAscii(string value)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c & 0xA0) == 0xA0)
                {
                    return "<kanji>";
                }
                if ((c & 0x80) == 0)
                {
                    sb.Append(c);
                }
                else
                {
                    int index = value[++i] & 0x3F | ((c & 0x1F) << 6);
                    sb.Append(_nonAscii[index - 128]);
                }
            }
            return sb.ToString();
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
