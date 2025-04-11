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

    public class MenuSettings()
    {
        public string RoomKey { get; set; } = "MP3 PROVING GROUND";
        public string Mode { get; set; } = "auto-select";
        public string Player1 { get; set; } = "Samus 0";
        public string Player2 { get; set; } = "none 0";
        public string Player3 { get; set; } = "none 0";
        public string Player4 { get; set; } = "none 0";
        public string Models { get; set; } = "none";
        public string MphVersion { get; set; } = "AMHE1";
        public string FhVersion { get; set; } = "AMFE0";
        public string Language { get; set; } = "English";
        public string PointGoal { get; set; } = "7";
        public string TimeLimit { get; set; } = "7:00";
        public string TimeGoal { get; set; } = "1:30";
        public string AutoReset { get; set; } = "on";
        public string TeamPlay { get; set; } = "off";
        public string HunterRadar { get; set; } = "off";
        public string DamageLevel { get; set; } = "medium";
        public string FriendlyFire { get; set; } = "off";
        public string AffinityWeapons { get; set; } = "off";
        public string SaveSlot { get; set; } = "none";
        public string SaveFromExit { get; set; } = "never";
        public string SaveFromShip { get; set; } = "prompt";
        public string Planets { get; set; } = "CA";
        public string Alinos1State { get; set; } = "none";
        public string Alinos2State { get; set; } = "none";
        public string Ca1State { get; set; } = "none";
        public string Ca2State { get; set; } = "none";
        public string Vdo1State { get; set; } = "none";
        public string Vdo2State { get; set; } = "none";
        public string Arcterra1State { get; set; } = "none";
        public string Arcterra2State { get; set; } = "none";
        public string CheckpointId { get; set; } = "none";
        public string HealthMax { get; set; } = "99";
        public string MissileMax { get; set; } = "50";
        public string UaMax { get; set; } = "400";
        public string Weapons { get; set; } = "PB, MS";
        public string Octoliths { get; set; } = "none";
    }

    public static class Menu
    {
        private static string _mode = "auto-select";
        private static Language _language = Language.English;

        public static void ShowMenuPrompts()
        {
            SoundCapability soundCapability = Sound.Sfx.CheckAudioLoad();
            int prompt = 0;
            int selection = 14;
            int roomId = -1;
            string room = "";
            string roomKey = "";
            // set default room with either roomId or roomKey
            // (although this will all get overwritten by the persisted/default settings now)
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
            MenuSettings menuSettings = GameState.LoadSettings();
            LoadSettings(menuSettings);
            UpdateSettings();

            void LoadSettings(MenuSettings settings)
            {
                static int GetState(string state)
                {
                    return state switch
                    {
                        "escape" => 1,
                        "done" => 2,
                        _ => 0
                    };
                }

                static SaveWhen ParseSaveWhen(string value, SaveWhen fallback)
                {
                    if (value.Length >= 2)
                    {
                        value = value[0].ToString().ToUpper() + value[1..].ToLower();
                        if (Enum.TryParse(value, out SaveWhen result))
                        {
                            return result;
                        }
                    }
                    return fallback;
                }

                ReadRoom(settings.RoomKey);
                ReadMode(settings.Mode);
                ReadPlayer(settings.Player1, 0);
                ReadPlayer(settings.Player2, 1);
                ReadPlayer(settings.Player3, 2);
                ReadPlayer(settings.Player4, 3);
                ReadModels(settings.Models);
                ReadMphVersion(settings.MphVersion);
                ReadFhVersion(settings.MphVersion);
                ReadTimeLimit(settings.TimeLimit);
                ReadTimeGoal(settings.TimeGoal);
                if (Enum.TryParse(settings.Language, out Language result))
                {
                    _language = result;
                }
                if (Int32.TryParse(settings.PointGoal, out int pointGoal))
                {
                    _pointGoal = pointGoal;
                }
                _octolithReset = settings.PointGoal != "off";
                _teams = settings.TeamPlay != "off";
                _radarPlayers = settings.HunterRadar != "off";
                _damageLevel = settings.DamageLevel switch
                {
                    "low" => 0,
                    "medium" => 1,
                    "high" => 2,
                    _ => _damageLevel
                };
                _friendlyFire = settings.FriendlyFire != "off";
                _affinityWeapons = settings.AffinityWeapons != "off";
                if (settings.SaveSlot == "none")
                {
                    SaveSlot = 0;
                }
                else if (Byte.TryParse(settings.SaveSlot, out byte saveSlot))
                {
                    SaveSlot = saveSlot;
                }
                SaveFromExit = ParseSaveWhen(settings.SaveFromExit, SaveWhen.Never);
                SaveFromShip = ParseSaveWhen(settings.SaveFromShip, SaveWhen.Prompt);
                string[] planets = settings.Planets.ToUpper().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (planets.Length > 0)
                {
                    _planets[0] = planets.Contains("CA") ? 1 : 0;
                    _planets[1] = planets.Contains("Alinos") ? 1 : 0;
                    _planets[2] = planets.Contains("VDO") ? 1 : 0;
                    _planets[3] = planets.Contains("Arcterra") ? 1 : 0;
                    _planets[4] = planets.Contains("Oubliette") ? 1 : 0;
                }
                _ca1State = GetState(settings.Ca1State);
                _ca1State = GetState(settings.Ca2State);
                _alinos1State = GetState(settings.Alinos1State);
                _alinos2State = GetState(settings.Alinos2State);
                _vdo1State = GetState(settings.Vdo1State);
                _vdo2State = GetState(settings.Vdo2State);
                _arcterra1State = GetState(settings.Arcterra1State);
                _arcterra2State = GetState(settings.Arcterra2State);
                if (settings.CheckpointId == "none")
                {
                    _checkpointId = -1;
                }
                else if (Int32.TryParse(settings.CheckpointId, out int checkpointId) && checkpointId >= 0)
                {
                    _checkpointId = checkpointId;
                }
                if (Int32.TryParse(settings.HealthMax, out int healthMax))
                {
                    _healthMax = Math.Max(healthMax, 1);
                }
                if (Int32.TryParse(settings.MissileMax, out int missileMax))
                {
                    _missileMax = Math.Max(missileMax, 0);
                }
                if (Int32.TryParse(settings.UaMax, out int uaMax))
                {
                    _uaMax = Math.Max(uaMax, 0);
                }
                string[] weapons = settings.Weapons.ToLower().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (weapons.Length > 0)
                {
                    _weapons[0] = weapons.Contains("power beam") ? 1 : 0;
                    _weapons[2] = weapons.Contains("missiles") ? 1 : 0;
                    _weapons[1] = weapons.Contains("volt driver") ? 1 : 0;
                    _weapons[3] = weapons.Contains("battlehammer") ? 1 : 0;
                    _weapons[4] = weapons.Contains("imperialist") ? 1 : 0;
                    _weapons[5] = weapons.Contains("judicator") ? 1 : 0;
                    _weapons[6] = weapons.Contains("magmaul") ? 1 : 0;
                    _weapons[7] = weapons.Contains("shock coil") ? 1 : 0;
                    _weapons[8] = weapons.Contains("omega cannon") ? 1 : 0;
                }
                string[] octoliths = settings.Octoliths.ToUpper().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (octoliths.Length > 0)
                {
                    _ca1State = octoliths.Contains("CA1") ? 1 : 0;
                    _ca2State = octoliths.Contains("CA2") ? 1 : 0;
                    _alinos1State = octoliths.Contains("Alinos1") ? 1 : 0;
                    _alinos2State = octoliths.Contains("Alinos2") ? 1 : 0;
                    _vdo1State = octoliths.Contains("VDO1") ? 1 : 0;
                    _vdo2State = octoliths.Contains("VDO2") ? 1 : 0;
                    _arcterra1State = octoliths.Contains("Arcterra1") ? 1 : 0;
                    _arcterra2State = octoliths.Contains("Arcterra2") ? 1 : 0;
                }
            }

            void ReadRoom(string? input)
            {
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

            void ReadMode(string? input)
            {
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

            void ReadPlayer(string? input, int index)
            {
                if (!String.IsNullOrWhiteSpace(input))
                {
                    input = input.Trim().ToLower();
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
                    playerIds[index] = player == "none" ? -1 : hunters.IndexOf(player);
                }
            }

            void ReadModels(string? input)
            {
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

            void ReadMphVersion(string? input)
            {
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

            void ReadFhVersion(string? input)
            {
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

            void CommitSettings()
            {
                string FormatHunter(int index)
                {
                    int id = playerIds[index];
                    if (id < 0)
                    {
                        return "none";
                    }
                    return $"{hunters[id]} {(_teams ? players[index].Team : players[index].Recolor)}";
                }

                string FormatState(int state)
                {
                    return state switch
                    {
                        1 => "escape",
                        2 => "done",
                        _ => "none"
                    };
                }

                var planets = new List<string>()
                {
                    _planets[0] != 0 ? "CA" : "",
                    _planets[1] != 0 ? "Alinos" : "",
                    _planets[2] != 0 ? "VDO" : "",
                    _planets[3] != 0 ? "Arcterra" : "",
                    _planets[4] != 0 ? "Oubliette" : ""
                };
                var weapons = new List<string>()
                {
                    _weapons[0] != 0 ? "Power Beam" : "",
                    _weapons[2] != 0 ? "Missiles" : "",
                    _weapons[1] != 0 ? "Volt Driver" : "",
                    _weapons[3] != 0 ? "Battlehammer" : "",
                    _weapons[4] != 0 ? "Imperialist" : "",
                    _weapons[5] != 0 ? "Judicator" : "",
                    _weapons[6] != 0 ? "Magmaul" : "",
                    _weapons[7] != 0 ? "Shock Coil" : "",
                    _weapons[8] != 0 ? "Omega Cannon" : ""
                };
                var octoliths = new List<string>()
                {
                    _ca1State != 0 ? "CA1" : "",
                    _ca2State != 0 ? "CA2" : "",
                    _alinos1State != 0 ? "Alinos1" : "",
                    _alinos2State != 0 ? "Alinos2" : "",
                    _vdo1State != 0 ? "VDO1" : "",
                    _vdo2State != 0 ? "VDO2" : "",
                    _arcterra1State != 0 ? "Arcterra1" : "",
                    _arcterra2State != 0 ? "Arcterra2" : ""
                };
                GameState.CommitSettings(new MenuSettings()
                {
                    RoomKey = roomKey,
                    Mode = _mode,
                    Player1 = FormatHunter(0),
                    Player2 = FormatHunter(1),
                    Player3 = FormatHunter(2),
                    Player4 = FormatHunter(3),
                    Models = String.Join(", ", models.Select(m => $"{m.Name} {m.Recolor}")),
                    MphVersion = Paths.MphKey,
                    FhVersion = Paths.FhKey,
                    Language = _language.ToString(),
                    PointGoal = _pointGoal.ToString(),
                    TimeLimit = FormatTime(_timeLimit),
                    TimeGoal = FormatTime(_timeGoal),
                    AutoReset = _octolithReset ? "on" : "off",
                    TeamPlay = _teams ? "on" : "off",
                    HunterRadar = _radarPlayers ? "on" : "off",
                    DamageLevel = _damageLevel switch
                    {
                        0 => "low",
                        1 => "medium",
                        2 => "high",
                        _ => "medium",
                    },
                    FriendlyFire = _friendlyFire ? "on" : "off",
                    AffinityWeapons = _affinityWeapons ? "on" : "off",
                    SaveSlot = SaveSlot == 0 ? "none" : SaveSlot.ToString(),
                    SaveFromExit = SaveFromExit.ToString().ToLower(),
                    SaveFromShip = SaveFromShip.ToString().ToLower(),
                    Planets = String.Join(',', planets.Where(p => p != "")),
                    Alinos1State = FormatState(_ca1State),
                    Alinos2State = FormatState(_ca2State),
                    Ca1State = FormatState(_alinos1State),
                    Ca2State = FormatState(_alinos2State),
                    Vdo1State = FormatState(_vdo1State),
                    Vdo2State = FormatState(_vdo2State),
                    Arcterra1State = FormatState(_arcterra1State),
                    Arcterra2State = FormatState(_arcterra2State),
                    CheckpointId = _checkpointId == -1 ? "none" : _checkpointId.ToString(),
                    HealthMax = _healthMax.ToString(),
                    MissileMax = _missileMax.ToString(),
                    UaMax = _uaMax.ToString(),
                    Weapons = String.Join(',', weapons.Where(p => p != "")),
                    Octoliths = String.Join(',', octoliths.Where(p => p != ""))
                });
            }

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
                // saving code placed here instead of afer Run() so that the game window will close
                // todo: handle saving for multiplayer
                if (NeededSave != SaveWhen.Never && SaveSlot != 0)
                {
                    if (NeededSave == SaveWhen.Always)
                    {
                        GameState.CommitSave();
                    }
                    else if (NeededSave == SaveWhen.Prompt)
                    {
                        Console.Clear();
                        Console.WriteLine($"MphRead Version {Program.Version}");
                        Console.WriteLine();
                        Console.WriteLine($"Save game to slot {SaveSlot}? (y/n)");
                        ConsoleKey input = ConsoleKey.None;
                        while (input != ConsoleKey.Y && input != ConsoleKey.N && input != ConsoleKey.Escape)
                        {
                            input = Console.ReadKey().Key;
                            if (input == ConsoleKey.Y)
                            {
                                GameState.CommitSave();
                            }
                        }
                    }
                }
                NeededSave = SaveWhen.Never;
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
                    else if (prompt == -2)
                    {
                        if (!ShowStoryModePrompts())
                        {
                            return;
                        }
                        prompt = 0;
                    }
                    else if (prompt == -3)
                    {
                        if (!ShowFeaturePrompts())
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
                    Console.WriteLine($"{X(s++)} (A) Adventure Mode Settings...");
                    Console.WriteLine($"{X(s++)} (S) Match Settings...");
                    Console.WriteLine($"{X(s++)} (C) Features...");
                    Console.WriteLine($"{X(s++)} (X) Reset All");
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
                            CommitSettings();
                            break;
                        }
                        if (keyInfo.Key == ConsoleKey.Spacebar)
                        {
                            if (selection == s - 4)
                            {
                                // story settings
                                prompt = -2;
                                continue;
                            }
                            if (selection == s - 3)
                            {
                                // match settings
                                prompt = -1;
                                continue;
                            }
                            if (selection == s - 2)
                            {
                                // feature settings
                                prompt = -3;
                                continue;
                            }
                            if (selection == s - 1)
                            {
                                // reset
                                ResetFeatures();
                                LoadSettings(new MenuSettings());
                                UpdateSettings();
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
                        else if (keyInfo.Key == ConsoleKey.A)
                        {
                            selection = 10;
                            prompt = -2;
                            continue;
                        }
                        else if (keyInfo.Key == ConsoleKey.S)
                        {
                            selection = 11;
                            prompt = -1;
                            continue;
                        }
                        else if (keyInfo.Key == ConsoleKey.C)
                        {
                            selection = 12;
                            prompt = -3;
                            continue;
                        }
                        else if (keyInfo.Key == ConsoleKey.X)
                        {
                            selection = 13;
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
                            ReadRoom(Console.ReadLine());
                        }
                        else if (prompt == 2)
                        {
                            Console.WriteLine("Enter game mode.");
                            Console.WriteLine("Examples: Adventure, Battle, Survival Teams");
                            ReadMode(Console.ReadLine());
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
                            ReadPlayer(Console.ReadLine(), prompt - 3);
                        }
                        else if (prompt == 7)
                        {
                            Console.WriteLine("Enter comma-separated list of models and (optionally) recolors.");
                            Console.WriteLine("Examples: Crate01, blastcap, LavaDemon 1, KandenGun 4");
                            ReadModels(Console.ReadLine());
                        }
                        else if (prompt == 8)
                        {
                            Console.WriteLine("Enter MPH version.");
                            Console.WriteLine("Examples: AMHE0, AMHP1, A76E0");
                            ReadMphVersion(Console.ReadLine());
                        }
                        else if (prompt == 9)
                        {
                            Console.WriteLine("Enter FH version.");
                            Console.WriteLine("Examples: AMFE0, AMFP0");
                            ReadFhVersion(Console.ReadLine());
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

        private static void ReadTimeGoal(string? input)
        {
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

        private static void ReadTimeLimit(string? input)
        {
            if (!String.IsNullOrWhiteSpace(input))
            {
                if (GetTime(input.Trim().ToUpper(), out decimal result))
                {
                    _timeLimit = result;
                }
            }
        }

        private static bool GetTime(string input, out decimal result)
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

        private static string FormatTime(decimal value)
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
                Console.WriteLine($"{X(s++)} (X) Reset Match Settings");
                Console.WriteLine($"{X(s++)} (B) Go Back");
                s--;
                if (prompt == 0)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        return false;
                    }
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.Spacebar)
                    {
                        if (selection == s - 1)
                        {
                            _radarPlayers = false;
                            _damageLevel = 1;
                            _friendlyFire = false;
                            _affinityWeapons = false;
                            UpdateSettings();
                            continue;
                        }
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
                    else if (keyInfo.Key == ConsoleKey.X)
                    {
                        selection = 8;
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
                        ReadTimeGoal(Console.ReadLine());
                    }
                    else if (prompt == 2)
                    {
                        Console.WriteLine("Enter time limit.");
                        Console.WriteLine("Examples: 7, 2:30, 0:45");
                        ReadTimeLimit(Console.ReadLine());
                    }
                    prompt = 0;
                }
            }
            return true;
        }

        public static byte SaveSlot { get; set; } = 0;
        public static SaveWhen SaveFromExit { get; set; } = SaveWhen.Never;
        public static SaveWhen SaveFromShip { get; set; } = SaveWhen.Prompt;
        public static SaveWhen NeededSave { get; set; } = SaveWhen.Never;

        private static readonly int[] _planets = [1, 0, 0, 0, 0];
        private static int _alinos1State = 0;
        private static int _alinos2State = 0;
        private static int _ca1State = 0;
        private static int _ca2State = 0;
        private static int _vdo1State = 0;
        private static int _vdo2State = 0;
        private static int _arcterra1State = 0;
        private static int _arcterra2State = 0;
        private static int _checkpointId = -1;
        private static int _healthMax = 99;
        private static int _missileMax = 50;
        private static int _uaMax = 400;
        private static readonly int[] _weapons = [1, 0, 1, 0, 0, 0, 0, 0, 0];
        private static readonly int[] _octoliths = new int[8];

        private static bool ShowFeaturePrompts()
        {
            int screen = 0;
            int selection = 0;

            string X(int index)
            {
                return $"[{(selection == index ? "x" : " ")}]";
            }

            static string OnOff(bool value)
            {
                return value ? "On" : "Off";
            }

            static string PrintOpacity(float value)
            {
                return value switch
                {
                    0 => "zero",
                    >= 1 => "full",
                    _ => "partial"
                };
            }

            while (true)
            {
                int s = 0;
                Console.Clear();
                Console.WriteLine($"MphRead Version {Program.Version}");
                Console.WriteLine();
                Console.WriteLine("Choose a setting using up/down or with the key indicated.");
                Console.WriteLine("Press Space to specify, Backspace to clear, or left/right to advance the setting.");
                Console.WriteLine("When finished, press Enter or use the last option to return. Press Escape to exit.");
                Console.WriteLine();
                if (screen == 0)
                {
                    Console.WriteLine("Features, Cheats, and Bugfixes");
                    Console.WriteLine();
                    Console.WriteLine($"{X(s++)} (F) Features...");
                    Console.WriteLine($"{X(s++)} (C) Cheats...");
                    Console.WriteLine($"{X(s++)} (G) Bugfixes...");
                    Console.WriteLine($"{X(s++)} (X) Reset Features, Cheats, and Bugfixes");
                }
                else if (screen == 1)
                {
                    Console.WriteLine("Features");
                    Console.WriteLine();
                    Console.WriteLine($"{X(s++)} (T) Allow Invalid Teams: {OnOff(Features.AllowInvalidTeams)}");
                    Console.WriteLine($"{X(s++)} (I) Target Info On Top Screen: {OnOff(Features.TopScreenTargetInfo)}");
                    Console.WriteLine($"{X(s++)} (H) Helmet Opacity: {PrintOpacity(Features.HelmetOpacity)}");
                    Console.WriteLine($"{X(s++)} (V) Visor Opacity: {PrintOpacity(Features.VisorOpacity)}");
                    Console.WriteLine($"{X(s++)} (S) HUD Sway: {OnOff(Features.HudSway)}");
                    Console.WriteLine($"{X(s++)} (F) Target Info Sway: {OnOff(Features.TargetInfoSway)}");
                    Console.WriteLine($"{X(s++)} (R) Maximum Room Detail: {OnOff(Features.MaxRoomDetail)}");
                    Console.WriteLine($"{X(s++)} (P) Maximum Player Detail: {OnOff(Features.MaxPlayerDetail)}");
                    Console.WriteLine($"{X(s++)} (L) Logarithmic Spatial Audio: {OnOff(Features.LogSpatialAudio)}");
                    Console.WriteLine($"{X(s++)} (A) Consistent Alarm Interval: {OnOff(Features.HalfSecondAlarm)}");
                }
                else if (screen == 2)
                {
                    Console.WriteLine("Cheats");
                    Console.WriteLine();
                    Console.WriteLine($"{X(s++)} (W) Free Weapon Selection: {OnOff(Cheats.FreeWeaponSelect)}");
                    Console.WriteLine($"{X(s++)} (J) Unlimited Jumps: {OnOff(Cheats.UnlimitedJumps)}");
                    Console.WriteLine($"{X(s++)} (D) All Doors Unlocked: {OnOff(Cheats.UnlockAllDoors)}");
                    Console.WriteLine($"{X(s++)} (U) Start With All Upgrades: {OnOff(Cheats.StartWithAllUpgrades)}");
                    Console.WriteLine($"{X(s++)} (O) Start With All Octoliths: {OnOff(Cheats.StartWithAllOctoliths)}");
                    Console.WriteLine($"{X(s++)} (G) Walk Through Walls: {OnOff(Cheats.WalkThroughWalls)}");
                }
                else if (screen == 3)
                {
                    Console.WriteLine("Bugfixes");
                    Console.WriteLine();
                    Console.WriteLine($"{X(s++)} (C) Smooth Camera Sequence Handoff: {OnOff(Bugfixes.SmoothCamSeqHandoff)}");
                    Console.WriteLine($"{X(s++)} (N) Better Camera Sequence Node Refs: {OnOff(Bugfixes.BetterCamSeqNodeRef)}");
                    Console.WriteLine($"{X(s++)} (R) No Stray Respawn Text: {OnOff(Bugfixes.NoStrayRespawnText)}");
                    Console.WriteLine($"{X(s++)} (S) Correct Bounty SFX: {OnOff(Bugfixes.CorrectBountySfx)}");
                    Console.WriteLine($"{X(s++)} (E) Fix Double Enemy Death: {OnOff(Bugfixes.NoDoubleEnemyDeath)}");
                    Console.WriteLine($"{X(s++)} (T) Fix Slench Roll Timer Underflow: {OnOff(Bugfixes.NoSlenchRollTimerUnderflow)}");
                }
                Console.WriteLine($"{X(s++)} (B) Go Back");
                s--;
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    selection--;
                    if (selection < 0)
                    {
                        selection = s;
                    }
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    selection++;
                    if (selection > s)
                    {
                        selection = 0;
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Escape)
                {
                    return false;
                }
                else if (screen == 0)
                {
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.F
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == 0)
                    {
                        screen = 1;
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.C
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == 1)
                    {
                        screen = 2;
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.G
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == 2)
                    {
                        screen = 3;
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.X)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.Spacebar && selection == 3)
                    {
                        ResetFeatures();
                    }
                }
                else if (screen == 1)
                {
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        screen = 0;
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.T)
                    {
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.I)
                    {
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.H)
                    {
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.V)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.S)
                    {
                        selection = 4;
                    }
                    else if (keyInfo.Key == ConsoleKey.F)
                    {
                        selection = 5;
                    }
                    else if (keyInfo.Key == ConsoleKey.R)
                    {
                        selection = 6;
                    }
                    else if (keyInfo.Key == ConsoleKey.P)
                    {
                        selection = 7;
                    }
                    else if (keyInfo.Key == ConsoleKey.L)
                    {
                        selection = 8;
                    }
                    else if (keyInfo.Key == ConsoleKey.A)
                    {
                        selection = 9;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace || keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (selection == 0)
                        {
                            Features.AllowInvalidTeams = true;
                        }
                        else if (selection == 1)
                        {
                            Features.TopScreenTargetInfo = true;
                        }
                        else if (selection == 2)
                        {
                            Features.HelmetOpacity = 1;
                        }
                        else if (selection == 3)
                        {
                            Features.VisorOpacity = 0.5f;
                        }
                        else if (selection == 4)
                        {
                            Features.HudSway = true;
                        }
                        else if (selection == 5)
                        {
                            Features.TargetInfoSway = false;
                        }
                        else if (selection == 6)
                        {
                            Features.MaxRoomDetail = false;
                        }
                        else if (selection == 7)
                        {
                            Features.MaxPlayerDetail = true;
                        }
                        else if (selection == 8)
                        {
                            Features.LogSpatialAudio = false;
                        }
                        else if (selection == 9)
                        {
                            Features.HalfSecondAlarm = false;
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
                            Features.AllowInvalidTeams = !Features.AllowInvalidTeams;
                        }
                        else if (selection == 1)
                        {
                            Features.TopScreenTargetInfo = !Features.TopScreenTargetInfo;
                        }
                        else if (selection == 2)
                        {
                            if (direction == 1)
                            {
                                if (Features.HelmetOpacity <= 0)
                                {
                                    Features.HelmetOpacity = 0.5f;
                                }
                                else if (Features.HelmetOpacity >= 1)
                                {
                                    Features.HelmetOpacity = 0;
                                }
                                else
                                {
                                    Features.HelmetOpacity = 1;
                                }
                            }
                            else
                            {
                                if (Features.HelmetOpacity <= 0)
                                {
                                    Features.HelmetOpacity = 1;
                                }
                                else if (Features.HelmetOpacity >= 1)
                                {
                                    Features.HelmetOpacity = 0.5f;
                                }
                                else
                                {
                                    Features.HelmetOpacity = 0;
                                }
                            }
                        }
                        else if (selection == 3)
                        {
                            if (direction == 1)
                            {
                                if (Features.VisorOpacity <= 0)
                                {
                                    Features.VisorOpacity = 0.5f;
                                }
                                else if (Features.VisorOpacity >= 1)
                                {
                                    Features.VisorOpacity = 0;
                                }
                                else
                                {
                                    Features.VisorOpacity = 1;
                                }
                            }
                            else
                            {
                                if (Features.VisorOpacity <= 0)
                                {
                                    Features.VisorOpacity = 1;
                                }
                                else if (Features.VisorOpacity >= 1)
                                {
                                    Features.VisorOpacity = 0.5f;
                                }
                                else
                                {
                                    Features.VisorOpacity = 0;
                                }
                            }
                        }
                        else if (selection == 4)
                        {
                            Features.HudSway = !Features.HudSway;
                        }
                        else if (selection == 5)
                        {
                            Features.TargetInfoSway = !Features.TargetInfoSway;
                        }
                        else if (selection == 6)
                        {
                            Features.MaxRoomDetail = !Features.MaxRoomDetail;
                        }
                        else if (selection == 7)
                        {
                            Features.MaxPlayerDetail = !Features.MaxPlayerDetail;
                        }
                        else if (selection == 8)
                        {
                            Features.LogSpatialAudio = !Features.LogSpatialAudio;
                        }
                        else if (selection == 9)
                        {
                            Features.HalfSecondAlarm = !Features.HalfSecondAlarm;
                        }
                    }
                }
                else if (screen == 2)
                {
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        screen = 0;
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.W)
                    {
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.J)
                    {
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.D)
                    {
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.U)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.O)
                    {
                        selection = 4;
                    }
                    else if (keyInfo.Key == ConsoleKey.G)
                    {
                        selection = 5;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace || keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (selection == 0)
                        {
                            Cheats.FreeWeaponSelect = false;
                        }
                        else if (selection == 1)
                        {
                            Cheats.UnlimitedJumps = false;
                        }
                        else if (selection == 2)
                        {
                            Cheats.UnlockAllDoors = false;
                        }
                        else if (selection == 3)
                        {
                            Cheats.StartWithAllUpgrades = false;
                        }
                        else if (selection == 4)
                        {
                            Cheats.StartWithAllOctoliths = false;
                        }
                        else if (selection == 5)
                        {
                            Cheats.WalkThroughWalls = false;
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
                            Cheats.FreeWeaponSelect = !Cheats.FreeWeaponSelect;
                        }
                        else if (selection == 1)
                        {
                            Cheats.UnlimitedJumps = !Cheats.UnlimitedJumps;
                        }
                        else if (selection == 2)
                        {
                            Cheats.UnlockAllDoors = !Cheats.UnlockAllDoors;
                        }
                        else if (selection == 3)
                        {
                            Cheats.StartWithAllUpgrades = !Cheats.StartWithAllUpgrades;
                        }
                        else if (selection == 4)
                        {
                            Cheats.StartWithAllOctoliths = !Cheats.StartWithAllOctoliths;
                        }
                        else if (selection == 5)
                        {
                            Cheats.WalkThroughWalls = !Cheats.WalkThroughWalls;
                        }
                    }
                }
                else if (screen == 3)
                {
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        screen = 0;
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.C)
                    {
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.N)
                    {
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.R)
                    {
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.S)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.E)
                    {
                        selection = 4;
                    }
                    else if (keyInfo.Key == ConsoleKey.T)
                    {
                        selection = 5;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace || keyInfo.Key == ConsoleKey.Delete)
                    {
                        if (selection == 0)
                        {
                            Bugfixes.SmoothCamSeqHandoff = false;
                        }
                        else if (selection == 1)
                        {
                            Bugfixes.BetterCamSeqNodeRef = true;
                        }
                        else if (selection == 2)
                        {
                            Bugfixes.NoStrayRespawnText = false;
                        }
                        else if (selection == 3)
                        {
                            Bugfixes.CorrectBountySfx = true;
                        }
                        else if (selection == 4)
                        {
                            Bugfixes.NoDoubleEnemyDeath = true;
                        }
                        else if (selection == 5)
                        {
                            Bugfixes.NoSlenchRollTimerUnderflow = true;
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
                            Bugfixes.SmoothCamSeqHandoff = !Bugfixes.SmoothCamSeqHandoff;
                        }
                        else if (selection == 1)
                        {
                            Bugfixes.BetterCamSeqNodeRef = !Bugfixes.BetterCamSeqNodeRef;
                        }
                        else if (selection == 2)
                        {
                            Bugfixes.NoStrayRespawnText = !Bugfixes.NoStrayRespawnText;
                        }
                        else if (selection == 3)
                        {
                            Bugfixes.CorrectBountySfx = !Bugfixes.CorrectBountySfx;
                        }
                        else if (selection == 4)
                        {
                            Bugfixes.NoDoubleEnemyDeath = !Bugfixes.NoDoubleEnemyDeath;
                        }
                        else if (selection == 5)
                        {
                            Bugfixes.NoSlenchRollTimerUnderflow = !Bugfixes.NoSlenchRollTimerUnderflow;
                        }
                    }
                }
            }
            return true;
        }

        private static void ResetFeatures()
        {
            Features.AllowInvalidTeams = true;
            Features.TopScreenTargetInfo = true;
            Features.HelmetOpacity = 1;
            Features.VisorOpacity = 0.5f;
            Features.HudSway = true;
            Features.TargetInfoSway = false;
            Features.MaxRoomDetail = false;
            Features.MaxPlayerDetail = true;
            Features.LogSpatialAudio = false;
            Features.HalfSecondAlarm = false;
            Cheats.FreeWeaponSelect = false;
            Cheats.UnlimitedJumps = false;
            Cheats.UnlockAllDoors = false;
            Cheats.StartWithAllUpgrades = false;
            Cheats.StartWithAllOctoliths = false;
            Cheats.WalkThroughWalls = false;
            Bugfixes.SmoothCamSeqHandoff = false;
            Bugfixes.BetterCamSeqNodeRef = true;
            Bugfixes.NoStrayRespawnText = false;
            Bugfixes.CorrectBountySfx = true;
            Bugfixes.NoDoubleEnemyDeath = true;
            Bugfixes.NoSlenchRollTimerUnderflow = true;
        }

        private static bool ShowStoryModePrompts()
        {
            int prompt = 0;
            int selection = 0;
            int planet = 0;
            int weapon = 0;
            int octolith = 0;

            string X(int index)
            {
                return $"[{(selection == index ? "x" : " ")}]";
            }

            static string LayerName(int value)
            {
                return value switch
                {
                    0 => "Before Boss",
                    1 => "Leaving Boss",
                    2 => "After Boss",
                    _ => "?"
                };
            }

            static int Advance(int current, int direction, int maxValue)
            {
                current += direction;
                if (current < 0)
                {
                    current = maxValue;
                }
                else if (current > maxValue)
                {
                    current = 0;
                }
                return current;
            }

            string Planet(int index)
            {
                if (selection == 3 && planet == index)
                {
                    return $"*{_planets[index]}*";
                }
                return $" {_planets[index]} ";
            }

            string Weapon(int index)
            {
                int highlight = index;
                if (highlight == 1)
                {
                    highlight = 2;
                }
                else if (highlight == 2)
                {
                    highlight = 1;
                }
                if (selection == 16 && weapon == highlight)
                {
                    return $"*{_weapons[index]}*";
                }
                return $" {_weapons[index]} ";
            }

            string Octolith(int index)
            {
                if (selection == 17 && octolith == index)
                {
                    return $"*{_octoliths[index]}*";
                }
                return $" {_octoliths[index]} ";
            }

            while (true)
            {
                string saveSlot = SaveSlot == 0 ? "none" : SaveSlot.ToString();
                string planets = $"CA:{Planet(0)}," +
                    $" Alinos:{Planet(1)}," +
                    $" VDO:{Planet(2)}," +
                    $" Arcterra:{Planet(3)}," +
                    $" Oubliette:{Planet(4)}";
                string weapons = $"PB:{Weapon(0)}, MI:{Weapon(2)}, VD:{Weapon(1)}, BH:{Weapon(3)}," +
                    $" IM:{Weapon(4)}, JD:{Weapon(5)}, MG:{Weapon(6)}, SC:{Weapon(7)}, OC:{Weapon(8)}";
                string octoliths = $"CA:{Octolith(0)}{Octolith(1)}," +
                    $" Alinos:{Octolith(2)}{Octolith(3)}," +
                    $" VDO:{Octolith(4)}{Octolith(5)}," +
                    $" Arcterra:{Octolith(6)}{Octolith(7)}";
                int s = 0;
                Console.Clear();
                Console.WriteLine($"MphRead Version {Program.Version}");
                Console.WriteLine();
                Console.WriteLine("Choose a setting using up/down or with the key indicated.");
                Console.WriteLine("Press Space to specify, Backspace to clear, or left/right to advance the setting.");
                Console.WriteLine("When finished, press Enter or use the last option to return. Press Escape to exit.");
                Console.WriteLine();
                Console.WriteLine("Adventure Mode Settings");
                Console.WriteLine();
                Console.WriteLine($"{X(s++)} (S) Save Slot: {saveSlot}");
                Console.WriteLine($"{X(s++)} (E) Save From Exit: {SaveFromExit.ToString().ToLower()}");
                Console.WriteLine($"{X(s++)} (G) Save From Ship: {SaveFromShip.ToString().ToLower()}");
                if (SaveSlot == 0)
                {
                    Console.WriteLine($"{X(s++)} (A) Areas: {planets}");
                    Console.WriteLine($"{X(s++)} (1) CA 1 State: {LayerName(_ca1State)}");
                    Console.WriteLine($"{X(s++)} (2) CA 2 State: {LayerName(_ca2State)}");
                    Console.WriteLine($"{X(s++)} (3) Alinos 1 State: {LayerName(_alinos1State)}");
                    Console.WriteLine($"{X(s++)} (4) Alinos 2 State: {LayerName(_alinos2State)}");
                    Console.WriteLine($"{X(s++)} (5) VDO 1 State: {LayerName(_vdo1State)}");
                    Console.WriteLine($"{X(s++)} (6) VDO 2 State: {LayerName(_vdo2State)}");
                    Console.WriteLine($"{X(s++)} (7) Arcterra 1 State: {LayerName(_arcterra1State)}");
                    Console.WriteLine($"{X(s++)} (8) Arcterra 2 State: {LayerName(_arcterra2State)}");
                    Console.WriteLine($"{X(s++)} (C) Checkpoint ID: {_checkpointId}");
                    Console.WriteLine($"{X(s++)} (H) Health Max: {_healthMax}");
                    Console.WriteLine($"{X(s++)} (M) Missile Max: {_missileMax}");
                    Console.WriteLine($"{X(s++)} (U) UA Max: {_uaMax}");
                    Console.WriteLine($"{X(s++)} (W) Weapons: {weapons}");
                    Console.WriteLine($"{X(s++)} (O) Octoliths: {octoliths}");
                    Console.WriteLine($"{X(s++)} (X) Reset Adventure Settings");
                }
                Console.WriteLine($"{X(s++)} (B) Go Back");
                s--;
                if (prompt == 0)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey();
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        return false;
                    }
                    if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.B
                        || keyInfo.Key == ConsoleKey.Spacebar && selection == s)
                    {
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.S)
                    {
                        selection = 0;
                    }
                    else if (keyInfo.Key == ConsoleKey.E)
                    {
                        selection = 1;
                    }
                    else if (keyInfo.Key == ConsoleKey.G)
                    {
                        selection = 2;
                    }
                    else if (keyInfo.Key == ConsoleKey.UpArrow)
                    {
                        selection--;
                        if (selection < 0)
                        {
                            selection = s;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.DownArrow)
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
                            SaveSlot = 0;
                        }
                        else if (selection == 1)
                        {
                            SaveFromExit = SaveWhen.Never;
                        }
                        else if (selection == 2)
                        {
                            SaveFromShip = SaveWhen.Prompt;
                        }
                        if (SaveSlot != 0)
                        {
                            continue;
                        }
                        if (selection == 3)
                        {
                            Array.Fill(_planets, 0);
                            _planets[0] = 1;
                        }
                        else if (selection == 12)
                        {
                            _checkpointId = -1;
                        }
                        else if (selection == 13)
                        {
                            _healthMax = 99;
                        }
                        else if (selection == 14)
                        {
                            _missileMax = 50;
                        }
                        else if (selection == 15)
                        {
                            _uaMax = 400;
                        }
                        else if (selection == 16)
                        {
                            Array.Fill(_weapons, 0);
                            _weapons[(int)BeamType.PowerBeam] = 1;
                            _weapons[(int)BeamType.Missile] = 1;
                        }
                        else if (selection == 17)
                        {
                            Array.Fill(_octoliths, 0);
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
                            SaveSlot = (byte)Advance(SaveSlot, direction, 255);
                        }
                        else if (selection == 1)
                        {
                            SaveFromExit = (SaveWhen)(((int)SaveFromExit + 1) % 3);
                        }
                        else if (selection == 2)
                        {
                            SaveFromShip = (SaveWhen)(((int)SaveFromShip + 1) % 3);
                        }
                        if (SaveSlot != 0)
                        {
                            continue;
                        }
                        if (selection == 3)
                        {
                            planet = Advance(planet, direction, 4);
                        }
                        else if (selection == 4)
                        {
                            _ca1State = Advance(_ca1State, direction, 2);
                        }
                        else if (selection == 5)
                        {
                            _ca2State = Advance(_ca2State, direction, 2);
                        }
                        else if (selection == 6)
                        {
                            _alinos1State = Advance(_alinos1State, direction, 2);
                        }
                        else if (selection == 7)
                        {
                            _alinos2State = Advance(_alinos2State, direction, 2);
                        }
                        else if (selection == 8)
                        {
                            _vdo1State = Advance(_vdo1State, direction, 2);
                        }
                        else if (selection == 9)
                        {
                            _vdo2State = Advance(_vdo2State, direction, 2);
                        }
                        else if (selection == 10)
                        {
                            _arcterra1State = Advance(_arcterra1State, direction, 2);
                        }
                        else if (selection == 11)
                        {
                            _arcterra2State = Advance(_arcterra2State, direction, 2);
                        }
                        else if (selection == 12)
                        {
                            if (_checkpointId >= 0 || direction == 1)
                            {
                                _checkpointId += direction;
                            }
                        }
                        else if (selection == 13)
                        {
                            if (direction == -1 && _healthMax > 99 || direction == 1 && _healthMax < 799)
                            {
                                _healthMax += direction * 100;
                            }
                        }
                        else if (selection == 14)
                        {
                            if (direction == -1 && _missileMax > 50 || direction == 1 && _missileMax < 950)
                            {
                                _missileMax += direction * 100;
                            }
                        }
                        else if (selection == 15)
                        {
                            if (direction == -1 && _uaMax > 400 || direction == 1 && _uaMax < 4000)
                            {
                                _uaMax += direction * 300;
                            }
                        }
                        else if (selection == 16)
                        {
                            weapon = Advance(weapon, direction, 8);
                        }
                        else if (selection == 17)
                        {
                            octolith = Advance(octolith, direction, 7);
                        }
                    }
                    if (SaveSlot != 0)
                    {
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Spacebar)
                    {
                        if (selection == s - 1)
                        {
                            Array.Fill(_planets, 0);
                            _planets[0] = 1;
                            _alinos1State = 0;
                            _alinos2State = 0;
                            _ca1State = 0;
                            _ca2State = 0;
                            _vdo1State = 0;
                            _vdo2State = 0;
                            _arcterra1State = 0;
                            _arcterra2State = 0;
                            _checkpointId = -1;
                            _healthMax = 99;
                            _missileMax = 50;
                            _uaMax = 400;
                            Array.Fill(_weapons, 0);
                            _weapons[(int)BeamType.PowerBeam] = 1;
                            _weapons[(int)BeamType.Missile] = 1;
                            Array.Fill(_octoliths, 0);
                            UpdateSettings();
                        }
                        else if (selection == 3)
                        {
                            _planets[planet] = (_planets[planet] + 1) % 2;
                        }
                        else if (selection == 16)
                        {
                            int highlight = weapon;
                            if (highlight == 1)
                            {
                                highlight = 2;
                            }
                            else if (highlight == 2)
                            {
                                highlight = 1;
                            }
                            _weapons[highlight] = (_weapons[highlight] + 1) % 2;
                        }
                        else if (selection == 17)
                        {
                            _octoliths[octolith] = (_octoliths[octolith] + 1) % 2;
                        }
                        else
                        {
                            prompt = selection + 1;
                        }
                    }
                    else if (keyInfo.Key == ConsoleKey.A)
                    {
                        selection = 3;
                    }
                    else if (keyInfo.Key == ConsoleKey.D1 || keyInfo.Key == ConsoleKey.NumPad1)
                    {
                        selection = 4;
                    }
                    else if (keyInfo.Key == ConsoleKey.D2 || keyInfo.Key == ConsoleKey.NumPad2)
                    {
                        selection = 5;
                    }
                    else if (keyInfo.Key == ConsoleKey.D3 || keyInfo.Key == ConsoleKey.NumPad3)
                    {
                        selection = 6;
                    }
                    else if (keyInfo.Key == ConsoleKey.D4 || keyInfo.Key == ConsoleKey.NumPad4)
                    {
                        selection = 7;
                    }
                    else if (keyInfo.Key == ConsoleKey.D5 || keyInfo.Key == ConsoleKey.NumPad5)
                    {
                        selection = 8;
                    }
                    else if (keyInfo.Key == ConsoleKey.D6 || keyInfo.Key == ConsoleKey.NumPad6)
                    {
                        selection = 9;
                    }
                    else if (keyInfo.Key == ConsoleKey.D7 || keyInfo.Key == ConsoleKey.NumPad7)
                    {
                        selection = 10;
                    }
                    else if (keyInfo.Key == ConsoleKey.D8 || keyInfo.Key == ConsoleKey.NumPad8)
                    {
                        selection = 11;
                    }
                    else if (keyInfo.Key == ConsoleKey.C)
                    {
                        selection = 12;
                    }
                    else if (keyInfo.Key == ConsoleKey.H)
                    {
                        selection = 13;
                    }
                    else if (keyInfo.Key == ConsoleKey.M)
                    {
                        selection = 14;
                    }
                    else if (keyInfo.Key == ConsoleKey.U)
                    {
                        selection = 15;
                    }
                    else if (keyInfo.Key == ConsoleKey.W)
                    {
                        selection = 16;
                    }
                    else if (keyInfo.Key == ConsoleKey.O)
                    {
                        selection = 17;
                    }
                    else if (keyInfo.Key == ConsoleKey.X)
                    {
                        selection = 18;
                    }
                }
                else
                {
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

        public static void ApplyMultiplayerSettings()
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

        public static void ApplyAdventureSettings()
        {
            if (_applySettings && SaveSlot == 0)
            {
                int areas = (_planets[0] == 0 ? 0 : 0xC)
                    | (_planets[1] == 0 ? 0 : 0x3)
                    | (_planets[2] == 0 ? 0 : 0x30)
                    | (_planets[3] == 0 ? 0 : 0xC0)
                    | (_planets[4] == 0 ? 0 : 0x100);
                GameState.StorySave.Areas = (ushort)areas;
                BossFlags[] flags;
                BossFlags bossFlags = BossFlags.None;
                flags = [BossFlags.None, BossFlags.Unit2B1Kill, BossFlags.Unit2B1Done];
                bossFlags |= flags[_ca1State];
                flags = [BossFlags.None, BossFlags.Unit2B2Kill, BossFlags.Unit2B2Done];
                bossFlags |= flags[_ca2State];
                flags = [BossFlags.None, BossFlags.Unit1B1Kill, BossFlags.Unit1B1Done];
                bossFlags |= flags[_alinos1State];
                flags = [BossFlags.None, BossFlags.Unit1B2Kill, BossFlags.Unit1B2Done];
                bossFlags |= flags[_alinos2State];
                flags = [BossFlags.None, BossFlags.Unit3B1Kill, BossFlags.Unit3B1Done];
                bossFlags |= flags[_vdo1State];
                flags = [BossFlags.None, BossFlags.Unit3B2Kill, BossFlags.Unit3B2Done];
                bossFlags |= flags[_vdo2State];
                flags = [BossFlags.None, BossFlags.Unit4B1Kill, BossFlags.Unit4B1Done];
                bossFlags |= flags[_arcterra1State];
                flags = [BossFlags.None, BossFlags.Unit4B2Kill, BossFlags.Unit4B2Done];
                bossFlags |= flags[_arcterra2State];
                GameState.StorySave.BossFlags = bossFlags;
                GameState.StorySave.CheckpointEntityId = _checkpointId;
                GameState.StorySave.HealthMax = _healthMax;
                GameState.StorySave.Health = _healthMax;
                GameState.StorySave.AmmoMax[1] = _missileMax;
                GameState.StorySave.Ammo[1] = _missileMax;
                GameState.StorySave.AmmoMax[0] = _uaMax;
                GameState.StorySave.Ammo[0] = _uaMax;
                int weapons = 0;
                for (int i = 0; i < _weapons.Length; i++)
                {
                    if (_weapons[i] != 0)
                    {
                        weapons |= 1 << i;
                    }
                }
                GameState.StorySave.Weapons = (ushort)weapons;
                int octoliths = 0;
                int[] bits = [2, 3, 0, 1, 4, 5, 6, 7];
                for (int i = 0; i < _octoliths.Length; i++)
                {
                    if (_octoliths[i] != 0)
                    {
                        octoliths |= 1 << bits[i];
                    }
                }
                GameState.StorySave.CurrentOctoliths = GameState.StorySave.FoundOctoliths = (ushort)octoliths;
            }
        }
    }
}
