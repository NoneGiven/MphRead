using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MphRead
{
    public enum SoundCapability
    {
        None = 0,
        Unsupported = 1,
        Supported = 2
    }

    public static class Menu
    {
        private static string _mode = "auto-select";
        private static Language _language = Language.English;

        public static void ShowMenuPrompts()
        {
            SoundCapability soundCapability = Sound.Sfx.CheckAudioLoad();
            int prompt = 0;
            int selection = 11;
            int roomId = -1;
            string room = "";
            string roomKey = "";
            // set default room with either roomId or roomKey
            roomKey = "MP3 PROVING GROUND";
            if (roomId >= 0)
            {
                RoomMetadata? init = Metadata.GetRoomById(roomId);
                Debug.Assert(init != null);
                room = init.InGameName ?? init.Name;
                roomKey = init.Name;
            }
            else if (roomKey != "")
            {
                (RoomMetadata? init, int id) = Metadata.GetRoomByName(roomKey);
                Debug.Assert(init != null);
                roomId = id;
                room = init.InGameName ?? init.Name;
            }
            bool fhRoom = false;
            var players = new List<(string Hunter, string Team, string Recolor)>()
            {
                ("Samus", "orange", "0"), ("none", "green", "0"), ("none", "orange", "0"), ("none", "green", "0")
            };
            var playerIds = new List<int>() { 0, -1, -1, -1 };
            var hunters = new List<string>()
            {
                "Samus", "Kanden", "Spire", "Trace", "Noxus", "Sylux", "Weavel", "Guardian"
            };
            int modeId = 0;
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
            var teamsModes = new List<string>()
            {
                "Battle Teams", "Survival Teams", "Capture", "Bounty Teams", "Nodes Teams", "Defender Teams"
            };
            var models = new List<(string Name, string Recolor)>();
            var mphVersions = new List<string>() { Ver.A76E0, Ver.AMHE0, Ver.AMHE1,
                Ver.AMHP0, Ver.AMHP1, Ver.AMHJ0, Ver.AMHJ1, Ver.AMHK0 };
            var fhVersions = new List<string>() { Ver.AMFE0, Ver.AMFP0 };
            var mphInfo = new Dictionary<string, string>()
            {
                { Ver.A76E0, "Kiosk demo" },
                { Ver.AMHE0, "USA rev 0" },
                { Ver.AMHE1, "USA rev 1" },
                { Ver.AMHP0, "EUR rev 0" },
                { Ver.AMHP1, "EUR rev 1" },
                { Ver.AMHJ0, "JPN rev 0" },
                { Ver.AMHJ1, "JPN rev 1" },
                { Ver.AMHK0, "KOR rev 0" }
            };
            var fhInfo = new Dictionary<string, string>()
            {
                { Ver.AMFE0, "USA rev 0" },
                { Ver.AMFP0, "EUR rev 0" }
            };
            UpdateSettings();

            string PrintPlayer(int index)
            {
                (string hunter, string team, string recolor) = players[index];
                if (hunter == "none")
                {
                    return "none";
                }
                if (_teams)
                {
                    return $"{hunter}, {team} team";
                }
                return $"{hunter}, suit color {recolor}";
            }

            string PrintModels()
            {
                if (models.Count == 0)
                {
                    return "none";
                }
                return String.Join(", ", models.Select(c => $"{c.Name} {c.Recolor}"));
            }

            void SetDefaultLanguage()
            {
                _language = Paths.IsMphJapan || Paths.IsMphKorea ? Language.Japanese : Language.English;
            }

            string X(int index)
            {
                return $"[{(selection == index ? "x" : " ")}]";
            }

            while (true)
            {
                while (true)
                {
                    int s = 0;
                    if (prompt == -1)
                    {
                        if (!ShowSettingsPrompts())
                        {
                            return;
                        }
                        prompt = 0;
                    }
                    string lastMode = _mode;
                    string mphKey = Paths.MphKey;
                    string fhKey = Paths.FhKey;
                    string roomString = roomKey == "AD1 TRANSFER LOCK BT" ? "Transfer Lock (Expanded)" : room;
                    roomString = $"{roomString} [{roomKey}] - {roomId}";
                    string modeString = _mode == "auto-select" ? "auto-select (Adventure or Battle)" : _mode;
                    string languageString = Paths.MphKey == Ver.AMHK0 ? "Korean" : _language.ToString();
                    Console.Clear();
                    Console.WriteLine($"MphRead Version {Program.Version}");
                    Console.WriteLine();
                    Console.WriteLine("Choose an option using up/down or with the key indicated.");
                    Console.WriteLine("Press Space to specify, Backspace to clear, or left/right to advance the option.");
                    Console.WriteLine("When finished, press Enter or use the last option to launch. Press Escape to exit.");
                    Console.WriteLine();
                    Console.WriteLine($"{X(s++)} (R) Room: {roomString}");
                    Console.WriteLine($"{X(s++)} (G) Game mode: {modeString}");
                    Console.WriteLine($"{X(s++)} (1) Player 1: {PrintPlayer(0)}");
                    Console.WriteLine($"{X(s++)} (2) Player 2: {PrintPlayer(1)}");
                    Console.WriteLine($"{X(s++)} (3) Player 3: {PrintPlayer(2)}");
                    Console.WriteLine($"{X(s++)} (4) Player 4: {PrintPlayer(3)}");
                    Console.WriteLine($"{X(s++)} (M) Models: {PrintModels()}");
                    Console.WriteLine($"{X(s++)} (V) MPH Version: {mphKey} ({mphInfo[mphKey]})");
                    Console.WriteLine($"{X(s++)} (F) FH Version: {fhKey} ({fhInfo[fhKey]})");
                    Console.WriteLine($"{X(s++)} (I) Language: {languageString}");
                    Console.WriteLine($"{X(s++)} (S) Match Settings...");
                    Console.WriteLine($"{X(s++)} (L) Launch");
                    s--;
                    if (prompt == 0)
                    {
                        if (soundCapability == SoundCapability.None)
                        {
                            Console.WriteLine();
                            Console.WriteLine("WARNING: Audio system could not be loaded. " +
                                "Sound effects will not be played.");
                            Console.WriteLine("You may need to install OpenAL Soft on your system.");
                        }
                        else if (soundCapability == SoundCapability.Unsupported)
                        {
                            Console.WriteLine();
                            Console.WriteLine("WARNING: Audio system was loaded, " +
                                "but an unsupported version of OpenAL was used.");
                            Console.WriteLine("You may need to install OpenAL Soft on your system for sounds to play correctly.");
                        }
                        ConsoleKeyInfo keyInfo = Console.ReadKey();
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                            return;
                        }
                        if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.L
                            || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                        {
                            Console.Clear();
                            Console.WriteLine($"MphRead Version {Program.Version}");
                            Console.WriteLine();
                            Console.WriteLine("Loading...");
                            break;
                        }
                        if (keyInfo.Key == ConsoleKey.Spacebar)
                        {
                            if (selection == s - 1)
                            {
                                prompt = -1;
                                continue;
                            }
                            prompt = selection + 1;
                        }
                        else if (keyInfo.Key == ConsoleKey.R)
                        {
                            selection = 0;
                        }
                        else if (keyInfo.Key == ConsoleKey.G)
                        {
                            selection = 1;
                        }
                        else if (keyInfo.Key == ConsoleKey.P
                            || keyInfo.Key == ConsoleKey.D1 || keyInfo.Key == ConsoleKey.NumPad1)
                        {
                            selection = 2;
                        }
                        else if (keyInfo.Key == ConsoleKey.D2 || keyInfo.Key == ConsoleKey.NumPad2)
                        {
                            selection = 3;
                        }
                        else if (keyInfo.Key == ConsoleKey.D3 || keyInfo.Key == ConsoleKey.NumPad3)
                        {
                            selection = 4;
                        }
                        else if (keyInfo.Key == ConsoleKey.D4 || keyInfo.Key == ConsoleKey.NumPad4)
                        {
                            selection = 5;
                        }
                        else if (keyInfo.Key == ConsoleKey.M)
                        {
                            selection = 6;
                            prompt = selection + 1;
                        }
                        else if (keyInfo.Key == ConsoleKey.V)
                        {
                            selection = 7;
                        }
                        else if (keyInfo.Key == ConsoleKey.F)
                        {
                            selection = 8;
                        }
                        else if (keyInfo.Key == ConsoleKey.I)
                        {
                            selection = 9;
                        }
                        else if (keyInfo.Key == ConsoleKey.S)
                        {
                            selection = 10;
                            prompt = -1;
                            continue;
                        }
                        else if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                        {
                            selection--;
                            if (selection < 0)
                            {
                                selection = s;
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S)
                        {
                            selection++;
                            if (selection > s)
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
                                _mode = "auto-select";
                            }
                            else if (selection >= 2 && selection <= 5)
                            {
                                int index = selection - 2;
                                string team = index == 0 || index == 2 ? "orange" : "green";
                                players[index] = ("none", team, "0");
                            }
                            else if (selection == 6)
                            {
                                models.Clear();
                            }
                            else if (selection == 7)
                            {
                                Paths.ChooseMphPath();
                            }
                            else if (selection == 8)
                            {
                                Paths.ChooseFhPath();
                            }
                            else if (selection == 9)
                            {
                                SetDefaultLanguage();
                            }
                        }
                        else if (keyInfo.Key == ConsoleKey.Add || keyInfo.Key == ConsoleKey.OemPlus
                            || keyInfo.Key == ConsoleKey.RightArrow)
                        {
                            if (selection == 0)
                            {
                                roomId++;
                                if (roomId > 137)
                                {
                                    roomId = -1;
                                    room = "none";
                                    roomKey = "none";
                                }
                                else
                                {
                                    RoomMetadata? meta = Metadata.GetRoomById(roomId);
                                    if (meta != null)
                                    {
                                        room = meta.InGameName ?? meta.Name;
                                        roomKey = meta.Name;
                                        fhRoom = meta.FirstHunt;
                                    }
                                    else
                                    {
                                        roomId = -1;
                                        room = "none";
                                        roomKey = "none";
                                    }
                                }
                            }
                            else if (selection == 1)
                            {
                                modeId++;
                                if (modeId >= modes.Count)
                                {
                                    modeId = 0;
                                }
                                _mode = modes[modeId];
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
                                players[index] = (id == -1 ? "none" : hunters[id], players[index].Team, players[index].Recolor);
                            }
                            else if (selection == 6)
                            {
                                if (models.Count == 0)
                                {
                                    models.Add(("Crate01", "0"));
                                }
                                else
                                {
                                    string model = models[0].Name;
                                    int index = Metadata.ModelMetadata.Keys.IndexOf(k => k == model);
                                    index++;
                                    if (index >= Metadata.ModelMetadata.Keys.Count())
                                    {
                                        index = 0;
                                    }
                                    model = Metadata.ModelMetadata[Metadata.ModelMetadata.Keys.ElementAt(index)].Name;
                                    models[0] = (model, models[0].Recolor);
                                }
                            }
                            else if (selection == 7)
                            {
                                string current = Paths.MphKey;
                                string next = Paths.MphKey;
                                do
                                {
                                    int index = mphVersions.IndexOf(next);
                                    index++;
                                    if (index >= mphVersions.Count)
                                    {
                                        index = 0;
                                    }
                                    next = mphVersions[index];
                                    if (Paths.AllPaths[next] != "")
                                    {
                                        current = next;
                                    }
                                }
                                while (current != next);
                                Paths.MphKey = current;
                                SetDefaultLanguage();
                            }
                            else if (selection == 8)
                            {
                                string current = Paths.FhKey;
                                string next = Paths.FhKey;
                                do
                                {
                                    int index = fhVersions.IndexOf(next);
                                    index++;
                                    if (index >= fhVersions.Count)
                                    {
                                        index = 0;
                                    }
                                    next = fhVersions[index];
                                    if (Paths.AllPaths[next] != "")
                                    {
                                        current = next;
                                    }
                                }
                                while (current != next);
                                Paths.FhKey = current;
                                SetDefaultLanguage();
                            }
                            else if (selection == 9)
                            {
                                int language = (int)_language + 1;
                                if (language > 5)
                                {
                                    language = 0;
                                }
                                _language = (Language)language;
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
                                    roomId = 137;
                                }
                                if (roomId == -1)
                                {
                                    room = "none";
                                    roomKey = "none";
                                }
                                else
                                {
                                    RoomMetadata? meta = Metadata.GetRoomById(roomId);
                                    if (meta != null)
                                    {
                                        room = meta.InGameName ?? meta.Name;
                                        roomKey = meta.Name;
                                        fhRoom = meta.FirstHunt;
                                    }
                                    else
                                    {
                                        roomId = -1;
                                        room = "none";
                                        roomKey = "none";
                                    }
                                }
                            }
                            else if (selection == 1)
                            {
                                modeId--;
                                if (modeId < 0)
                                {
                                    modeId = modes.Count - 1;
                                }
                                _mode = modes[modeId];
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
                                players[index] = (id == -1 ? "none" : hunters[id], players[index].Team, players[index].Recolor);
                            }
                            else if (selection == 6)
                            {
                                if (models.Count == 0)
                                {
                                    models.Add(("Crate01", "0"));
                                }
                                else
                                {
                                    string model = models[0].Name;
                                    int index = Metadata.ModelMetadata.Keys.IndexOf(k => k == model);
                                    index--;
                                    if (index < 0)
                                    {
                                        index = Metadata.ModelMetadata.Keys.Count() - 1;
                                    }
                                    model = Metadata.ModelMetadata[Metadata.ModelMetadata.Keys.ElementAt(index)].Name;
                                    models[0] = (model, models[0].Recolor);
                                }
                            }
                            else if (selection == 7)
                            {
                                string current = Paths.MphKey;
                                string next = Paths.MphKey;
                                do
                                {
                                    int index = mphVersions.IndexOf(next);
                                    index--;
                                    if (index < 0)
                                    {
                                        index = mphVersions.Count - 1;
                                    }
                                    next = mphVersions[index];
                                    if (Paths.AllPaths[next] != "")
                                    {
                                        current = next;
                                    }
                                }
                                while (current != next);
                                Paths.MphKey = current;
                                SetDefaultLanguage();
                            }
                            else if (selection == 8)
                            {
                                string current = Paths.FhKey;
                                string next = Paths.FhKey;
                                do
                                {
                                    int index = fhVersions.IndexOf(next);
                                    index--;
                                    if (index < 0)
                                    {
                                        index = fhVersions.Count - 1;
                                    }
                                    next = fhVersions[index];
                                    if (Paths.AllPaths[next] != "")
                                    {
                                        current = next;
                                    }
                                }
                                while (current != next);
                                Paths.FhKey = current;
                                SetDefaultLanguage();
                            }
                            else if (selection == 9)
                            {
                                int language = (int)_language - 1;
                                if (language < 0)
                                {
                                    language = 5;
                                }
                                _language = (Language)language;
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
                                        fhRoom = meta.FirstHunt;
                                    }
                                }
                                else
                                {
                                    IReadOnlyList<RoomMetadata> rooms = Metadata.RoomList;
                                    RoomMetadata? meta = rooms.FirstOrDefault(r => r.Name.ToLower() == input);
                                    if (meta == null)
                                    {
                                        bool multi = _mode != "Adventure";
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
                                        fhRoom = meta.FirstHunt;
                                    }
                                }
                            }
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
                                    _mode = result;
                                    modeId = modes.IndexOf(_mode);
                                }
                                else
                                {
                                    _mode = "auto-select";
                                    modeId = 0;
                                }
                            }
                        }
                        else if (prompt >= 3 && prompt <= 6)
                        {
                            if (_teams)
                            {
                                Console.WriteLine("Enter hunter and (optionally) team.");
                                Console.WriteLine("Examples: Samus, Trace 0, Sylux 1, Guardian");
                            }
                            else
                            {
                                Console.WriteLine("Enter hunter and (optionally) recolor.");
                                Console.WriteLine("Examples: Samus, Trace 2, Sylux 5, Guardian");
                            }
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
                                if (Enum.TryParse(name, out Hunter hunter)
                                    && Enum.IsDefined(hunter) && hunter != Hunter.Random)
                                {
                                    player = hunter.ToString();
                                }
                                string team = players[index].Team;
                                string recolor = players[index].Recolor;
                                if (split.Length > 1)
                                {
                                    if (_teams)
                                    {
                                        string value = split[1].ToLower();
                                        if (value == "orange" || value == "red")
                                        {
                                            team = "orange";
                                        }
                                        else if (value == "green")
                                        {
                                            team = "green";
                                        }
                                        else if (Int32.TryParse(split[1], out int result))
                                        {
                                            team = Math.Clamp(result, 0, 1) == 0 ? "orange" : "green";
                                        }
                                    }
                                    else if (Int32.TryParse(split[1], out int result))
                                    {
                                        recolor = Math.Clamp(result, 0, 5).ToString();
                                    }
                                }
                                players[index] = (player, team, recolor);
                            }
                        }
                        else if (prompt == 7)
                        {
                            Console.WriteLine("Enter comma-separated list of models and (optionally) recolors.");
                            Console.WriteLine("Examples: Crate01, blastcap, LavaDemon 1, KandenGun 4");
                            string? input = Console.ReadLine();
                            if (!String.IsNullOrWhiteSpace(input))
                            {
                                models.Clear();
                                string[] split = input.Split(',');
                                for (int i = 0; i < split.Length; i++)
                                {
                                    string[] pair = split[i].Trim().Split(' ');
                                    if (Metadata.ModelMetadata.TryGetValue(pair[0], out ModelMetadata? meta))
                                    {
                                        string recolor = "0";
                                        if (pair.Length > 1 && Int32.TryParse(pair[1], out int result))
                                        {
                                            recolor = Math.Clamp(result, 0, meta.Recolors.Count - 1).ToString();
                                        }
                                        models.Add((meta.Name, recolor));
                                    }
                                }
                            }
                        }
                        else if (prompt == 8)
                        {
                            Console.WriteLine("Enter MPH version.");
                            Console.WriteLine("Examples: AMHE0, AMHP1, A76E0");
                            string? input = Console.ReadLine();
                            if (!String.IsNullOrWhiteSpace(input))
                            {
                                input = input.Trim().ToUpper();
                                if (mphVersions.Contains(input) && Paths.AllPaths[input] != "")
                                {
                                    Paths.MphKey = input;
                                    SetDefaultLanguage();
                                }
                            }
                        }
                        else if (prompt == 9)
                        {
                            Console.WriteLine("Enter FH version.");
                            Console.WriteLine("Examples: AMFE0, AMFP0");
                            string? input = Console.ReadLine();
                            if (!String.IsNullOrWhiteSpace(input))
                            {
                                input = input.Trim().ToUpper();
                                if (fhVersions.Contains(input) && Paths.AllPaths[input] != "")
                                {
                                    Paths.FhKey = input;
                                    SetDefaultLanguage();
                                }
                            }
                        }
                        prompt = 0;
                    }
                    if (_mode != lastMode)
                    {
                        UpdateSettings();
                    }
                }
                _applySettings = true;
                using var renderer = new RenderWindow();
                if (room != "none")
                {
                    if (!fhRoom)
                    {
                        for (int i = 0; i < players.Count; i++)
                        {
                            (string hunter, string team, string recolor) = players[i];
                            if (hunter != "none")
                            {
                                int teamId = -1;
                                if (_teams)
                                {
                                    teamId = team == "orange" ? 0 : 1;
                                }
                                renderer.AddPlayer(Enum.Parse<Hunter>(hunter), Int32.Parse(recolor), teamId);
                            }
                        }
                    }
                    Scene.Language = Paths.MphKey == "AMHK0" ? Language.Japanese : _language;
                    GameMode gameMode = GameMode.None;
                    if (_mode == "Adventure")
                    {
                        gameMode = GameMode.SinglePlayer;
                    }
                    else if (_mode != "auto-select")
                    {
                        gameMode = Enum.Parse<GameMode>(_mode.Replace(" ", ""));
                    }
                    renderer.AddRoom(roomKey, gameMode);
                }
                for (int i = 0; i < models.Count; i++)
                {
                    (string model, string recolor) = models[i];
                    renderer.AddModel(model, Int32.Parse(recolor));
                }
                renderer.Run();
            }
        }

        private static bool _applySettings = false;
        private static bool _teams = false;
        // point goal is a decimal just to share code for advancing the value
        private static decimal _pointGoal = 0;
        private static decimal _timeGoal = 0;
        private static decimal _timeLimit = 0;
        private static bool _octolithReset = true;
        private static bool _radarPlayers = false;
        private static int _damageLevel = 1;
        private static bool _friendlyFire = false;
        private static bool _affinityWeapons = false;

        private static string _goalType = "";

        private static bool ShowSettingsPrompts()
        {
            int prompt = 0;
            int selection = 0;
            IReadOnlyList<string> damageLevels = new string[]
            {
                "Low", "Medium", "High"
            };

            string X(int index)
            {
                return $"[{(selection == index ? "x" : " ")}]";
            }

            static string FormatTime(decimal value)
            {
                var time = TimeSpan.FromSeconds((float)value);
                if (time.Hours > 0)
                {
                    return $"{time:h\\:mm\\:ss}";
                }
                if (time.Minutes > 0)
                {
                    return $"{time:m\\:ss}";
                }
                return $"0:{time:ss}";
            }

            static string OnOff(bool value)
            {
                return value ? "On" : "Off";
            }

            static decimal Advance(decimal current, IReadOnlyList<decimal> values, int direction)
            {
                decimal update;
                if (direction == 1)
                {
                    update = Decimal.MaxValue;
                    for (int i = 0; i < values.Count; i++)
                    {
                        decimal value = values[i];
                        if (value < update && value > current)
                        {
                            update = value;
                        }
                    }
                    return update == Decimal.MaxValue ? values[0] : update;
                }
                update = Decimal.MinValue;
                for (int i = values.Count - 1; i >= 0; i--)
                {
                    decimal value = values[i];
                    if (value > update && value < current)
                    {
                        update = value;
                    }
                }
                return update == Decimal.MinValue ? values[^1] : update;
            }

            while (true)
            {
                int s = 0;
                string modeString;
                if (_mode == "auto-select")
                {
                    modeString = "Battle";
                }
                else
                {
                    modeString = _mode.Replace(" Teams", "");
                }
                modeString += " Mode Settings";
                string goalString;
                if (_mode.StartsWith("Defender") || _mode == "Prime Hunter")
                {
                    goalString = FormatTime(_timeGoal);
                }
                else
                {
                    goalString = _pointGoal.ToString();
                }
                string timeString = FormatTime(_timeLimit);
                string resetString = "N/A";
                if (_mode == "Capture" || _mode.StartsWith("Bounty"))
                {
                    resetString = OnOff(_octolithReset);
                }
                string weaponsString = _affinityWeapons ? "Affinity Weapons" : "Default Weapons";
                Console.Clear();
                Console.WriteLine($"MphRead Version {Program.Version}");
                Console.WriteLine();
                Console.WriteLine("Choose a setting using up/down or with the key indicated.");
                Console.WriteLine("Press Space to specify, Backspace to clear, or left/right to advance the setting.");
                Console.WriteLine("When finished, press Enter or use the last option to return. Press Escape to exit.");
                Console.WriteLine();
                Console.WriteLine(modeString);
                Console.WriteLine();
                Console.WriteLine($"{X(s++)} (P) {_goalType}: {goalString}");
                Console.WriteLine($"{X(s++)} (L) Time Limit: {timeString}");
                Console.WriteLine($"{X(s++)} (A) Auto Reset: {resetString}");
                Console.WriteLine($"{X(s++)} (T) Team Play: {OnOff(_teams)}");
                Console.WriteLine($"{X(s++)} (S) Show Hunters On Radar: {OnOff(_radarPlayers)}");
                Console.WriteLine($"{X(s++)} (D) Damage Level: {damageLevels[_damageLevel]}");
                Console.WriteLine($"{X(s++)} (F) Friendly Fire: {OnOff(_friendlyFire)}");
                Console.WriteLine($"{X(s++)} (W) Available Weapons: {weaponsString}");
                Console.WriteLine($"{X(s++)} (R) Return");
                s--;
                if (prompt == 0)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        return false;
                    }
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.R
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.Spacebar)
                    {
                        prompt = selection + 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.P)
                    {
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.L)
                    {
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.A)
                    {
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.T)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.S)
                    {
                        selection = 4;
                    }
                    else if (keyInfo.Key == ConsoleKey.D)
                    {
                        selection = 5;
                    }
                    else if (keyInfo.Key == ConsoleKey.F)
                    {
                        selection = 6;
                    }
                    else if (keyInfo.Key == ConsoleKey.W)
                    {
                        selection = 7;
                    }
                    else if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                    {
                        selection--;
                        if (selection < 0)
                        {
                            selection = s;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S)
                    {
                        selection++;
                        if (selection > s)
                        {
                            selection = 0;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace || keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (selection == 0)
                        {
                            ResetGoal();
                        }
                        else if (selection == 1)
                        {
                            ResetTimeLimit();
                        }
                        else if (selection == 2)
                        {
                            _octolithReset = true;
                        }
                        else if (selection == 3)
                        {
                            _teams = _mode == "Capture";
                        }
                        else if (selection == 4)
                        {
                            _radarPlayers = false;
                        }
                        else if (selection == 5)
                        {
                            _damageLevel = 1;
                        }
                        else if (selection == 6)
                        {
                            _friendlyFire = false;
                        }
                        else if (selection == 7)
                        {
                            _affinityWeapons = false;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.Add || keyInfo.Key == ConsoleKey.OemPlus
                        || keyInfo.Key == ConsoleKey.RightArrow || keyInfo.Key == ConsoleKey.Subtract
                        || keyInfo.Key == ConsoleKey.OemMinus || keyInfo.Key == ConsoleKey.LeftArrow)
                    {
                        int direction = keyInfo.Key == ConsoleKey.Add || keyInfo.Key == ConsoleKey.OemPlus
                            || keyInfo.Key == ConsoleKey.RightArrow ? 1 : -1;
                        if (selection == 0)
                        {
                            if (_mode == "auto-select" || _mode.StartsWith("Battle"))
                            {
                                _pointGoal = Advance(_pointGoal, _battlePoints, direction);
                            }
                            else if (_mode.StartsWith("Survival"))
                            {
                                _pointGoal = Advance(_pointGoal, _extraLives, direction);
                            }
                            else if (_mode == "Capture" || _mode.StartsWith("Bounty"))
                            {
                                _pointGoal = Advance(_pointGoal, _octolithPoints, direction);
                            }
                            else if (_mode.StartsWith("Nodes"))
                            {
                                _pointGoal = Advance(_pointGoal, _nodePoints, direction);
                            }
                            else if (_mode.StartsWith("Defender") || _mode == "Prime Hunter")
                            {
                                _timeGoal = Advance(_timeGoal, _timeGoals, direction);
                            }
                        }
                        else if (selection == 1)
                        {
                            _timeLimit = Advance(_timeLimit, _timeLimits, direction);
                        }
                        else if (selection == 2)
                        {
                            _octolithReset = !_octolithReset;
                        }
                        else if (selection == 3)
                        {
                            if (_mode == "Capture")
                            {
                                _teams = true;
                            }
                            else if (_mode == "Prime Hunter")
                            {
                                _teams = false;
                            }
                            else
                            {
                                _teams = !_teams;
                            }
                        }
                        else if (selection == 4)
                        {
                            _radarPlayers = !_radarPlayers;
                        }
                        else if (selection == 5)
                        {
                            _damageLevel += direction;
                            if (_damageLevel >= damageLevels.Count)
                            {
                                _damageLevel = 0;
                            }
                            else if (_damageLevel < 0)
                            {
                                _damageLevel = damageLevels.Count - 1;
                            }
                        }
                        else if (selection == 6)
                        {
                            _friendlyFire = !_friendlyFire;
                        }
                        else if (selection == 7)
                        {
                            _affinityWeapons = !_affinityWeapons;
                        }
                        if (_teams && _mode != "Capture" && !_mode.EndsWith("Teams"))
                        {
                            if (_mode == "auto-select")
                            {
                                _mode = "Battle";
                            }
                            _mode += " Teams";
                        }
                        else if (!_teams && _mode.EndsWith("Teams"))
                        {
                            _mode = _mode.Replace(" Teams", "");
                        }
                    }
                }
                else
                {
                    static bool GetTime(string input, out decimal result)
                    {
                        result = 0;
                        string[] split = input.Split(':');
                        if (split.Length == 1)
                        {
                            if (Int32.TryParse(split[0], out int minutes))
                            {
                                result = minutes * 60;
                                return true;
                            }
                        }
                        else if (split.Length == 2)
                        {
                            if (Int32.TryParse(split[0], out int minutes)
                                && Int32.TryParse(split[1], out int seconds))
                            {
                                result = minutes * 60 + seconds;
                                return true;
                            }
                        }
                        else if (split.Length == 3)
                        {
                            if (Int32.TryParse(split[0], out int hours)
                                && Int32.TryParse(split[1], out int minutes)
                                && Int32.TryParse(split[2], out int seconds))
                            {
                                result = hours * 60 * 60 + minutes * 60 + seconds;
                                return true;
                            }
                        }
                        return false;
                    }

                    if (prompt == 1)
                    {
                        Console.WriteLine($"Enter {_goalType.ToLower()}.");
                        if (_goalType == "Time Goal")
                        {
                            Console.WriteLine("Examples: 7, 2:30, 0:45");
                        }
                        else
                        {
                            Console.WriteLine("Examples: 5, 66, 100");
                        }
                        string? input = Console.ReadLine();
                        if (!String.IsNullOrWhiteSpace(input))
                        {
                            input = input.Trim().ToUpper();
                            if (_goalType == "Time Goal")
                            {
                                if (GetTime(input, out decimal result))
                                {
                                    _timeGoal = result;
                                }
                            }
                            else if (Decimal.TryParse(input, out decimal result))
                            {
                                _pointGoal = Math.Clamp((int)result, _goalType == "Extra Lives" ? 0 : 1, 99999);
                            }
                        }
                    }
                    else if (prompt == 2)
                    {
                        Console.WriteLine("Enter time limit.");
                        Console.WriteLine("Examples: 7, 2:30, 0:45");
                        string? input = Console.ReadLine();
                        if (!String.IsNullOrWhiteSpace(input))
                        {
                            if (GetTime(input.Trim().ToUpper(), out decimal result))
                            {
                                _timeLimit = result;
                            }
                        }
                    }
                    prompt = 0;
                }
            }
            return true;
        }

        private static readonly IReadOnlyList<decimal> _battlePoints = new decimal[]
        {
            1, 5, 7, 10, 15, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100
        };

        private static readonly IReadOnlyList<decimal> _octolithPoints = new decimal[]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25
        };

        private static readonly IReadOnlyList<decimal> _nodePoints = new decimal[]
        {
            40, 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 190, 200, 250
        };

        private static readonly IReadOnlyList<decimal> _extraLives = new decimal[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
        };

        private static readonly IReadOnlyList<decimal> _timeGoals = new decimal[]
        {
            1 * 60, 1.5m * 60, 2 * 60, 2.5m * 60, 3 * 60, 3.5m * 60, 4 * 60,
            4.5m * 60, 5 * 60, 6 * 60, 7 * 60, 8 * 60, 9 * 60, 10 * 60
        };

        private static readonly IReadOnlyList<decimal> _timeLimits = new decimal[]
        {
            3 * 60, 5 * 60, 7 * 60, 9 * 60, 10 * 60, 15 * 60, 20 * 60, 25 * 60,
            30 * 60, 35 * 60, 40 * 60, 45 * 60, 50 * 60, 55 * 60, 60 * 60
        };

        private static void ResetGoal()
        {
            if (_mode == "auto-select" || _mode.StartsWith("Battle"))
            {
                _pointGoal = 7;
            }
            else if (_mode.StartsWith("Survival"))
            {
                _pointGoal = 2;
            }
            else if (_mode == "Capture")
            {
                _pointGoal = 5;
            }
            else if (_mode.StartsWith("Bounty"))
            {
                _pointGoal = 3;
            }
            else if (_mode.StartsWith("Nodes"))
            {
                _pointGoal = 70;
            }
            else if (_mode.StartsWith("Defender") || _mode == "Prime Hunter")
            {
                _timeGoal = 1.5m * 60;
            }
        }

        private static void ResetTimeLimit()
        {
            if (_mode == "auto-select" || _mode.StartsWith("Battle"))
            {
                _timeLimit = 7 * 60;
            }
            else
            {
                _timeLimit = 15 * 60;
            }
        }

        private static void UpdateSettings()
        {
            _teams = false;
            _pointGoal = 0;
            _timeGoal = 0;
            _timeLimit = 0;
            _octolithReset = true;
            ResetGoal();
            ResetTimeLimit();
            if (_mode == "auto-select" || _mode.StartsWith("Battle") || _mode.StartsWith("Nodes"))
            {
                _goalType = "Point Goal";
            }
            else if (_mode.StartsWith("Survival"))
            {
                _goalType = "Extra Lives";
            }
            else if (_mode == "Capture" || _mode.StartsWith("Bounty"))
            {
                _goalType = "Octolith Goal";
            }
            else if (_mode.StartsWith("Defender") || _mode == "Prime Hunter")
            {
                _goalType = "Time Goal";
            }
            if (_mode == "Capture" || _mode.EndsWith("Teams"))
            {
                _teams = true;
            }
        }

        public static void ApplySettings()
        {
            if (_applySettings)
            {
                GameState.Teams = _teams;
                GameState.PointGoal = (int)_pointGoal;
                GameState.TimeGoal = (float)_timeGoal;
                GameState.MatchTime = (float)_timeLimit;
                GameState.OctolithReset = _octolithReset;
                GameState.RadarPlayers = _radarPlayers;
                GameState.DamageLevel = _damageLevel;
                GameState.FriendlyFire = _friendlyFire;
                GameState.AffinityWeapons = _affinityWeapons;
            }
        }
    }
}
