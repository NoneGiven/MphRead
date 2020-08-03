using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MphRead
{
    internal static class Program
    {
        public static Version Version { get; } = new Version(0, 7, 0, 0);

        private static void Main(string[] args)
        {
            ConsoleColor.Setup();
            IReadOnlyList<Argument> arguments = ParseArguments(args);
            if (arguments.Count == 0)
            {
                using var renderer = new Renderer();
                renderer.AddRoom("MP3 PROVING GROUND");
                //renderer.AddModel("Crate01");
                Nop();
                renderer.Run();
            }
            else if (TryGetString(arguments, "export", "e", out string? exportValue))
            {
                Read.ReadAndExport(exportValue);
            }
            else if (TryGetString(arguments, "extract", "x", out string? extractValue))
            {
                Read.ExtractArchive(extractValue);
            }
            else
            {
                var rooms = new List<string>();
                var models = new List<string>();
                if (TryGetInt(arguments, "room", "r", out int roomId))
                {
                    RoomMetadata? meta = Metadata.GetRoomById(roomId);
                    if (meta == null)
                    {
                        Exit();
                    }
                    rooms.Add(meta.Name);
                }
                else if (TryGetString(arguments, "room", "r", out string? roomName))
                {
                    rooms.Add(roomName);
                }
                foreach (string modelName in GetStrings(arguments, "model", "m"))
                {
                    models.Add(modelName);
                }
                if (rooms.Count > 1 || (rooms.Count == 0 && models.Count == 0))
                {
                    Exit();
                }
                using var renderer = new Renderer();
                foreach (string room in rooms)
                {
                    renderer.AddRoom(room);
                }
                foreach (string model in models)
                {
                    renderer.AddModel(model);
                }
                renderer.Run();
            }
        }

        private readonly struct Argument
        {
            public readonly string Name;
            public readonly string? StringValue;

            public Argument(string name, string? value)
            {
                Name = name;
                StringValue = value;
            }
        }

        private static IEnumerable<string> GetStrings(IEnumerable<Argument> arguments, string fullName, string shortName)
        {
            foreach (Argument argument in arguments.Where(a => a.Name == fullName || a.Name == shortName))
            {
                if (argument.StringValue != null)
                {
                    yield return argument.StringValue;
                }
            }
        }

        private static bool TryGetArgument(IEnumerable<Argument> arguments, string fullName, string shortName,
            [NotNullWhen(true)] out Argument? argument)
        {
            IEnumerable<Argument> matches = arguments.Where(a => a.Name == fullName || a.Name == shortName);
            if (matches.Any())
            {
                argument = matches.First();
                return true;
            }
            argument = null;
            return false;
        }

        private static bool TryGetString(IEnumerable<Argument> arguments, string fullName, string shortName,
            [NotNullWhen(true)] out string? value)
        {
            if (TryGetArgument(arguments, fullName, shortName, out Argument? argument) && argument.Value.StringValue != null)
            {
                value = argument.Value.StringValue;
                return true;
            }
            value = null;
            return false;
        }

        private static bool TryGetInt(IEnumerable<Argument> arguments, string fullName, string shortName,
            out int value)
        {
            if (TryGetString(arguments, fullName, shortName, out string? stringValue))
            {
                if (Int32.TryParse(stringValue, out int intValue))
                {
                    value = intValue;
                    return true;
                } 
            }
            value = 0;
            return false;
        }

        private static IReadOnlyList<Argument> ParseArguments(string[] args)
        {
            var arguments = new List<Argument>();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (i == args.Length - 1)
                    {
                        arguments.Add(new Argument(arg, null));
                    }
                    else
                    {
                        string value = args[i + 1];
                        if (value.StartsWith("-"))
                        {
                            arguments.Add(new Argument(arg, null));
                        }
                        else
                        {
                            arguments.Add(new Argument(arg, value));
                            i++;
                        }
                    }
                }
            }
            return arguments;
        }

        [DoesNotReturn]
        private static void Exit()
        {
            Console.WriteLine("MphRead usage:");
            Console.WriteLine("    -room <room_name> [layer_mask]");
            Console.WriteLine("    -model <model_name> [recolor_index]");
            Console.WriteLine("At most one room may be specified. Any number of models may be specified.");
            Console.WriteLine("- or -");
            Console.WriteLine("    -extract <archive_path>");
            Console.WriteLine("If the target archive is LZ10-compressed, it will be decompressed.");
            Console.WriteLine("- or -");
            Console.WriteLine("    -export <target_name>");
            Console.WriteLine("The export target may be a model or room name.");
            Environment.Exit(1);
        }

        private static void Nop() { }
    }

    public class ProgramException : Exception
    {
        public ProgramException(string message) : base(message) { }
    }
}
