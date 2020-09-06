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
            // todo: what are the extra bytes after the length in ScanLog.bin?
            // seems to be an offset to 8 bytes before the first string data
            int offset = name == StringTables.ScanLog ? 8 : 4;
            foreach (RawStringTableEntry entry in Read.DoOffsets<RawStringTableEntry>(bytes, offset, count))
            {
                string value = Read.ReadString(bytes, entry.Offset, entry.Length);
                entries.Add(new StringTableEntry(entry, value));
            }
            return entries;
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
