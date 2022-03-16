using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace MphRead
{
    internal static class Program
    {
        public static Version Version { get; } = new Version(0, 22, 0, 0);
        private static readonly Version _minExtractVersion = new Version(0, 19, 0, 0);

        private static void Main(string[] args)
        {
            ConsoleSetup.Run();
            if (CheckSetup(args))
            {
                return;
            }
            IReadOnlyList<Argument> arguments = ParseArguments(args);
            if (arguments.Count == 0)
            {
                if (Debugger.IsAttached)
                {
                    using var renderer = new RenderWindow();
                    renderer.AddRoom("MP3 PROVING GROUND");
                    //renderer.AddModel("Crate01");
                    renderer.Run();
                }
                else
                {
                    Menu.ShowMenuPrompts();
                }
            }
            else if (arguments.Any(a => a.Name == "setup"))
            {
                foreach (string path in Directory.EnumerateFiles(Path.Combine(Paths.FileSystem, "archives")))
                {
                    Read.ExtractArchive(Path.GetFileNameWithoutExtension(path));
                }
            }
            else if (TryGetString(arguments, "export", "e", out string? exportValue))
            {
                bool firstHunt = arguments.Any(a => a.Name == "fh");
                Read.ReadAndExport(exportValue, firstHunt);
            }
            else if (TryGetString(arguments, "extract", "x", out string? extractValue))
            {
                Read.ExtractArchive(extractValue);
            }
            else
            {
                var rooms = new List<string>();
                var models = new List<(string, int)>();
                GameMode mode = GameMode.None;
                int playerCount = 0;
                BossFlags bossFlags = BossFlags.None;
                int nodeLayerMask = 0;
                int entityLayerId = -1;
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
                if (TryGetInt(arguments, "mode", "g", out int modeValue))
                {
                    mode = (GameMode)modeValue;
                }
                if (TryGetInt(arguments, "players", "p", out int playerValue))
                {
                    playerCount = playerValue;
                }
                if (TryGetInt(arguments, "boss", "b", out int bossValue))
                {
                    bossFlags = (BossFlags)bossValue;
                }
                if (TryGetInt(arguments, "node", "n", out int nodeValue))
                {
                    nodeLayerMask = nodeValue;
                }
                if (TryGetInt(arguments, "entity", "l", out int entityValue))
                {
                    entityLayerId = entityValue;
                }
                foreach ((string, int) pair in GetPairs(arguments, "model", "m"))
                {
                    models.Add(pair);
                }
                if (rooms.Count > 1 || (rooms.Count == 0 && models.Count == 0))
                {
                    Exit();
                }
                using var renderer = new RenderWindow();
                foreach (string room in rooms)
                {
                    renderer.AddRoom(room, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
                }
                bool firstHunt = arguments.Any(a => a.Name == "fh");
                foreach ((string model, int recolor) in models)
                {
                    renderer.AddModel(model, recolor, firstHunt);
                }
                renderer.Run();
            }
        }

        private static bool CheckSetup(string[] args)
        {
            if (File.Exists("paths.txt") && !CheckVersion())
            {
                Console.WriteLine("Your paths.txt file is not compatible with this version of MphRead and needs to be recreated.");
                Console.WriteLine("It is recommended that you delete the file as well as any extracted game files, " +
                    "then perform setup again.");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return true;
            }
            if (args.Length == 1 && !args[0].StartsWith('-') && File.Exists(args[0]))
            {
                Extract.Setup(args[0]);
                return true;
            }
            if (!File.Exists("paths.txt"))
            {
                Console.WriteLine("Could not find the paths.txt file.");
                Console.WriteLine("You may need to perform first-time setup by dragging a ROM onto the MphRead executable.");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return true;
            }
            Paths.UpdatePaths();
            Paths.ChooseMphPath();
            Paths.ChooseFhPath();
            return false;
        }

        private static bool CheckVersion()
        {
            string text = File.ReadAllText("paths.txt").Split('\n')[0].Trim();
            if (Version.TryParse(text, out Version? extractVersion))
            {
                return extractVersion >= _minExtractVersion;
            }
            return false;
        }

        private readonly struct Argument
        {
            public readonly string Name;
            public readonly string? ValueOne;
            public readonly string? ValueTwo;

            public Argument(string name, string? valueOne, string? valueTwo = null)
            {
                Name = name;
                ValueOne = valueOne;
                ValueTwo = valueTwo;
            }
        }

        private static IEnumerable<(string, int)> GetPairs(IEnumerable<Argument> arguments, string fullName, string shortName)
        {
            foreach (Argument argument in arguments.Where(a => a.Name == fullName || a.Name == shortName))
            {
                if (argument.ValueOne != null)
                {
                    Int32.TryParse(argument.ValueTwo, out int valueTwo);
                    yield return (argument.ValueOne, valueTwo);
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
            if (TryGetArgument(arguments, fullName, shortName, out Argument? argument) && argument.Value.ValueOne != null)
            {
                value = argument.Value.ValueOne;
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
                    if (arg.StartsWith("-") && arg.Length > 1)
                    {
                        arg = arg[1..];
                        if (i == args.Length - 1)
                        {
                            arguments.Add(new Argument(arg, null));
                        }
                        else
                        {
                            string valueOne = args[i + 1];
                            if (valueOne.StartsWith("-"))
                            {
                                arguments.Add(new Argument(arg, null));
                            }
                            else
                            {
                                string? valueTwo = null;
                                if (i < args.Length - 2 && !args[i + 2].StartsWith("-"))
                                {
                                    valueTwo = args[i + 2];
                                    i++;
                                }
                                arguments.Add(new Argument(arg, valueOne, valueTwo));
                                i++;
                            }
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
            Console.WriteLine("    -room <room_name -or- room_id>");
            Console.WriteLine("    -model <model_name> [recolor_index]");
            Console.WriteLine("At most one room may be specified. Any number of models may be specified.");
            Console.WriteLine("To load First Hunt models, include -fh in the argument list.");
            Console.WriteLine("Available room options: -mode, -players, -boss, -node, -entity");
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
