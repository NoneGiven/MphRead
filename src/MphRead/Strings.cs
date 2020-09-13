using System;
using System.Collections.Generic;
using System.IO;

namespace MphRead
{
    public static class Strings
    {
        public static IReadOnlyList<StringTableEntry> ReadStringTable(string name, Language language = Language.English)
        {
            var entries = new List<StringTableEntry>();
            string path = Path.Combine(Paths.FileSystem, GetFolder(language), name);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            uint count = Read.SpanReadUint(bytes, 0);
            // ScanLog has an 8-byte header and 8 bytes between the last entry and first string,
            // which are related to parsing max string length and not necessary for us to use
            int offset = name == StringTables.ScanLog ? 8 : 4;
            foreach (RawStringTableEntry entry in Read.DoOffsets<RawStringTableEntry>(bytes, offset, count))
            {
                string value = Read.ReadString(bytes, entry.Offset, entry.Length);
                entries.Add(new StringTableEntry(entry, value));
            }
            return entries;
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

        public static string GetMessage(char type, uint id, string table, Language language = Language.English)
        {
            string fullId = type + id.ToString().PadLeft(3, '0');
            foreach (StringTableEntry entry in ReadStringTable(table, language))
            {
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
}
