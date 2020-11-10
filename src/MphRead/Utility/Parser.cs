using System;
using System.Collections.Generic;
using System.Globalization;

namespace MphRead.Utility
{
    public static class Parser
    {
        private enum ThingType
        {
            General,
            Boolean,
            Enum
        }

        private readonly struct Thing
        {
            public readonly string Name;
            public readonly ThingType Type;
            public readonly Type? EnumType;
            public readonly int BitFrom;
            public readonly int BitTo;

            public Thing(int bit, string name, ThingType type)
            {
                Name = name;
                Type = type;
                BitFrom = bit;
                BitTo = bit;
                EnumType = null;
            }

            public Thing(int bit, string name, Type enumType)
            {
                Name = name;
                Type = ThingType.Enum;
                BitFrom = bit;
                BitTo = bit;
                EnumType = enumType;
            }

            public Thing(int bitFrom, int bitTo, string name, ThingType type)
            {
                Name = name;
                Type = type;
                BitFrom = bitFrom;
                BitTo = bitTo;
                EnumType = null;
            }

            public Thing(int bitFrom, int bitTo, string name, Type enumType)
            {
                Name = name;
                Type = ThingType.Enum;
                BitFrom = bitFrom;
                BitTo = bitTo;
                EnumType = enumType;
            }

            public string Get(int value)
            {
                int mask = ~(~0 << (BitTo - BitFrom + 1));
                value = (value >> BitFrom) & mask;
                string output = "";
                if (Type == ThingType.Boolean)
                {
                    output = value == 0 ? "No" : "Yes";
                }
                else if (Type == ThingType.Enum)
                {
                    output = Enum.ToObject(EnumType!, value).ToString() ?? "?";
                }
                else
                {
                    output = value.ToString();
                }
                return $"{Name}: {output}";
            }
        }

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<Thing>> _things = new Dictionary<string, IReadOnlyList<Thing>>()
        {
            {
                "POLYGON_ATTR",
                new List<Thing>()
                {
                    new Thing(0, "Light 1", ThingType.Boolean),
                    new Thing(1, "Light 2", ThingType.Boolean),
                    new Thing(2, "Light 3", ThingType.Boolean),
                    new Thing(3, "Light 4", ThingType.Boolean),
                    new Thing(4, 5, "Polygon mode", typeof(PolygonMode)),
                    new Thing(6, "Back face", ThingType.Boolean),
                    new Thing(7, "Front face", ThingType.Boolean),
                    new Thing(11, "Set new depth", ThingType.Boolean),
                    new Thing(12, "Render far", ThingType.Boolean),
                    new Thing(13, "Render 1-dot", ThingType.Boolean),
                    new Thing(14, "Equal depth test", ThingType.Boolean),
                    new Thing(15, "Enable fog", ThingType.Boolean),
                    new Thing(16, 20, "Alpha", ThingType.General),
                    new Thing(24, 29, "Polygon ID", ThingType.General)
                }
            }
        };

        public static void MainLoop()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("1: POLYGON_ATTR\r\nx: quit");
                string? type = null;
                string? input = Console.ReadLine();
                if (input == "x" || input == "X")
                {
                    break;
                }
                if (input == "1")
                {
                    type = "POLYGON_ATTR";
                }
                if (type != null)
                {
                    string? output = null;
                    while (true)
                    {
                        Console.Clear();
                        if (output != null)
                        {
                            Console.WriteLine(output);
                            Console.WriteLine();
                            output = null;
                        }
                        Console.Write("Value: ");
                        string? value = Console.ReadLine();
                        if (value == "x" || value == "X")
                        {
                            break;
                        }
                        if (value != null && value.Length <= 8 && Int32.TryParse(value, NumberStyles.HexNumber, provider: null, out int result))
                        {
                            output = Convert.ToString(result, 2).PadLeft(32, '0');
                            foreach (Thing thing in _things[type])
                            {
                                output += Environment.NewLine + thing.Get(result);
                            }
                        }
                    }
                }
            }
        }
    }
}
