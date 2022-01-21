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
        public static Version Version { get; } = new Version(0, 17, 1, 0);

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
                    ShowMenuPrompts();
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

        private static void ShowMenuPrompts()
        {
            int prompt = 0;
            int selection = 6;
            int roomId = 95;
            string room = "Combat Hall";
            string roomKey = "MP3 PROVING GROUND";
            var players = new List<(string Hunter, string Recolor)>()
            {
                ("Samus", "0"), ("none", "0"), ("none", "0"), ("none", "0")
            };
            var playerIds = new List<int>() { 0, -1, -1, -1 };
            var hunters = new List<string>()
            {
                "Samus", "Kanden", "Trace", "Sylux", "Noxus", "Spire", "Weavel", "Guardian"
            };
            string mode = "auto-select";
            int modeId = 0;
            var models = new List<(string, string)>();
            var modeOpts = new Dictionary<string, string>()
            {
                { "adventure", "Adventure" },
                { "story", "Adventure" },
                { "1p", "Adventure" },
                { "battle", "Battle" },
                { "battleteams", "Battle Teams" },
                { "survival", "Survival" },
                { "survivalteams", "Survival Teams" },
                { "capture", "Capture" },
                { "bounty", "Bounty" },
                { "bountyteams", "Bounty Teams" },
                { "nodes", "Nodes" },
                { "nodesteams", "Nodes Teams" },
                { "defender", "Defender" },
                { "defenderteams", "Defender Teams" },
                { "primehunter", "Prime Hunter" },
            };
            var modes = new List<string>()
            {
                "auto-select", "Adventure", "Battle", "Battle Teams", "Survival", "Survival Teams", "Capture",
                "Bounty", "Bounty Teams", "Nodes", "Nodes Teams", "Defender", "Defender Teams", "Prime Hunter"
            };

            string PrintPlayer(int index)
            {
                (string hunter, string recolor) = players[index];
                if (hunter == "none")
                {
                    return "none";
                }
                return $"{hunter}, suit color {recolor}";
            }

            // sktodo:
            // - model entry, inc/dec, clear, add to renderer
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"MphRead Version {Version}");
                Console.WriteLine();
                Console.WriteLine("Choose an option with up/down.");
                Console.WriteLine("Press Enter to specify, Backspace to clear, or left/right to advance the option.");
                Console.WriteLine("When finished, press Enter on the last option to launch.");
                Console.WriteLine();
                Console.WriteLine($"[{(selection == 0 ? "x" : " ")}] Room: {room}");
                Console.WriteLine($"[{(selection == 1 ? "x" : " ")}] Game mode: {mode}");
                Console.WriteLine($"[{(selection == 2 ? "x" : " ")}] Player 1: {PrintPlayer(0)}");
                Console.WriteLine($"[{(selection == 3 ? "x" : " ")}] Player 2: {PrintPlayer(1)}");
                Console.WriteLine($"[{(selection == 4 ? "x" : " ")}] Player 3: {PrintPlayer(2)}");
                Console.WriteLine($"[{(selection == 5 ? "x" : " ")}] Player 4: {PrintPlayer(3)}");
                Console.WriteLine($"[{(selection == 6 ? "x" : " ")}] Launch");
                if (prompt == 0)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        return;
                    }
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift)
                            || keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) || selection == 6)
                        {
                            Console.Clear();
                            Console.WriteLine($"MphRead Version {Version}");
                            Console.WriteLine();
                            Console.WriteLine("Loading...");
                            break;
                        }
                        prompt = selection + 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                    {
                        selection--;
                        if (selection < 0)
                        {
                            selection = 6;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S)
                    {
                        selection++;
                        if (selection > 6)
                        {
                            selection = 0;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace || keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (selection == 0)
                        {
                            roomId = -1;
                            room = "none";
                            roomKey = "none";
                        }
                        else if (selection == 1)
                        {
                            mode = "auto-select";
                        }
                        else if (selection >= 2 && selection <= 5)
                        {
                            players[selection - 2] = ("none", "0");
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Add || keyInfo.Key == ConsoleKey.OemPlus
                        || keyInfo.Key == ConsoleKey.RightArrow)
                    {
                        if (selection == 0)
                        {
                            roomId++;
                            if (roomId > 130)
                            {
                                roomId = -1;
                            }
                            RoomMetadata? meta = Metadata.GetRoomById(roomId);
                            if (meta != null)
                            {
                                room = meta.InGameName ?? meta.Name;
                                roomKey = meta.Name;
                            }
                            else
                            {
                                roomId = -1;
                                room = "none";
                                roomKey = "none";
                            }
                        }
                        else if (selection == 1)
                        {
                            modeId++;
                            if (modeId >= modes.Count)
                            {
                                modeId = 0;
                            }
                            mode = modes[modeId];
                        }
                        else if (selection >= 2 && selection <= 5)
                        {
                            int index = selection - 2;
                            int id = playerIds[index];
                            id++;
                            if (id > 7)
                            {
                                id = -1;
                            }
                            playerIds[index] = id;
                            players[index] = (id == -1 ? "none" : hunters[id], players[index].Recolor);
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Subtract || keyInfo.Key == ConsoleKey.OemMinus
                        || keyInfo.Key == ConsoleKey.LeftArrow)
                    {
                        if (selection == 0)
                        {
                            roomId--;
                            if (roomId < -1)
                            {
                                roomId = 130;
                            }
                            RoomMetadata? meta = Metadata.GetRoomById(roomId);
                            if (meta != null)
                            {
                                room = meta.InGameName ?? meta.Name;
                                roomKey = meta.Name;
                            }
                            else
                            {
                                roomId = -1;
                                room = "none";
                                roomKey = "none";
                            }
                        }
                        else if (selection == 1)
                        {
                            modeId--;
                            if (modeId < 0)
                            {
                                modeId = modes.Count - 1;
                            }
                            mode = modes[modeId];
                        }
                        else if (selection >= 2 && selection <= 5)
                        {
                            int index = selection - 2;
                            int id = playerIds[index];
                            id--;
                            if (id < -1)
                            {
                                id = 7;
                            }
                            playerIds[index] = id;
                            players[index] = (id == -1 ? "none" : hunters[id], players[index].Recolor);
                        }
                    }
                }
                else
                {
                    Console.WriteLine();
                    if (prompt == 1)
                    {
                        Console.WriteLine("Enter room ID, internal name, or in-game name.");
                        Console.WriteLine("Examples: 95, MP3 PROVING GROUND, Combat Hall");
                        string? input = Console.ReadLine();
                        if (!String.IsNullOrWhiteSpace(input))
                        {
                            input = input.Trim().ToLower();
                            if (Int32.TryParse(input, out int id))
                            {
                                RoomMetadata? meta = Metadata.GetRoomById(id);
                                if (meta != null)
                                {
                                    roomId = id;
                                    room = meta.InGameName ?? meta.Name;
                                    roomKey = meta.Name;
                                }
                            }
                            else
                            {
                                IEnumerable<RoomMetadata> rooms = Metadata.RoomMetadata.Values;
                                RoomMetadata? meta = rooms.FirstOrDefault(r => r.Name.ToLower() == input);
                                if (meta == null)
                                {
                                    bool multi = mode != "Adventure";
                                    meta = rooms.FirstOrDefault(r => r.InGameName?.ToLower() == input && r.Multiplayer == multi);
                                    if (meta == null)
                                    {
                                        meta = rooms.FirstOrDefault(r => r.InGameName?.ToLower() == input);
                                    }
                                }
                                if (meta != null)
                                {
                                    roomId = meta.Id;
                                    room = meta.InGameName ?? meta.Name;
                                    roomKey = meta.Name;
                                }
                            }
                        }
                        prompt = 0;
                    }
                    else if (prompt == 2)
                    {
                        Console.WriteLine("Enter game mode.");
                        Console.WriteLine("Examples: Adventure, Battle, Survival Teams");
                        string? input = Console.ReadLine();
                        if (!String.IsNullOrWhiteSpace(input))
                        {
                            input = input.Trim().ToLower().Replace(" ", "");
                            if (modeOpts.TryGetValue(input, out string? result))
                            {
                                mode = result;
                                modeId = modes.IndexOf(mode);
                            }
                            else
                            {
                                mode = "auto-select";
                                modeId = 0;
                            }
                        }
                        prompt = 0;
                    }
                    else if (prompt >= 3 && prompt <= 6)
                    {
                        Console.WriteLine("Enter hunter and (optionally) recolor.");
                        Console.WriteLine("Examples: Samus, Trace 2, Sylux 5, Guardian");
                        string? input = Console.ReadLine();
                        if (!String.IsNullOrWhiteSpace(input))
                        {
                            input = input.Trim().ToLower();
                            int index = prompt - 3;
                            string[] split = input.Split(' ');
                            string player = "none";
                            string name = split[0];
                            if (name.Length > 0)
                            {
                                name = name[..1].ToUpper() + name[1..];
                            }
                            if (Enum.TryParse(name, out Hunter hunter) && hunter != Hunter.Random)
                            {
                                player = hunter.ToString();
                            }
                            string recolor = players[index].Recolor;
                            if (split.Length > 1 && Int32.TryParse(split[1], out int result))
                            {
                                recolor = result.ToString();
                            }
                            players[index] = (player, recolor);
                        }
                        prompt = 0;
                    }
                }
            }
            using var renderer = new RenderWindow();
            if (room != "none")
            {
                for (int i = 0; i < players.Count; i++)
                {
                    (string hunter, string recolor) = players[i];
                    Int32.TryParse(recolor, out int result);
                    result = Math.Clamp(result, 0, 5);
                    renderer.AddPlayer(Enum.Parse<Hunter>(hunter), result);
                }
                GameMode gameMode = GameMode.None;
                if (mode == "Adventure")
                {
                    gameMode = GameMode.SinglePlayer;
                }
                else if (mode != "auto-select")
                {
                    gameMode = Enum.Parse<GameMode>(mode.Replace(" ", ""));
                }
                renderer.AddRoom(roomKey, gameMode);
            }
            renderer.Run();
        }

        private static bool CheckSetup(string[] args)
        {
            if (args.Length == 1 && !args[0].StartsWith('-') && File.Exists(args[0]))
            {
                Extract.Setup(args[0]);
                return true;
            }
            if (!File.Exists("paths.txt"))
            {
                Console.WriteLine("Could not find paths.txt file.");
                return true;
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
