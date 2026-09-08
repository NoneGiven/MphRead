using System;
using System.Globalization;
using System.IO;
using MphRead.Mods;

namespace MphRead.Mods.Launcher
{
    /// <summary>
    /// Launcher-only preferences, kept in their own file beside the
    /// executable.
    ///
    /// Deliberately not folded into upstream's MenuSettings: that type is
    /// serialized by GameState and gains fields as upstream develops, so
    /// adding mod-specific keys to it would guarantee a merge conflict. A
    /// separate file costs nothing and keeps the fetch clean.
    /// </summary>
    public static class LauncherPrefs
    {
        /// <summary>
        /// Where launcher.txt lives. Beside the executable, which is where the
        /// rest of a portable install keeps its files -- except where the
        /// program does not own that folder. An Android package's own
        /// directory is read-only, so the head there points this at the app's
        /// data directory before anything reads.
        /// </summary>
        public static string Directory { get; set; } = AppContext.BaseDirectory;

        private static string Path => System.IO.Path.Combine(Directory, "launcher.txt");

        /// <summary>
        /// The project's own server, so that a fresh install can press "play
        /// online" and be in a match without being asked for an address it has
        /// no way to know. Typing another one over it is one field on the front
        /// screen, and whatever was typed is what gets saved here.
        ///
        /// The address, not the hostname: the hostname is for the people
        /// working on this, and a name that resolves somewhere else later
        /// would send every copy of the launcher with it.
        /// </summary>
        public const string DefaultServer = "89.160.162.50";

        public static string ServerAddress { get; set; } = DefaultServer;
        public static int ServerPort { get; set; } = Network.NetConfig.DefaultPort;

        /// <summary>
        /// The directory the server browser asks. A hostname on purpose --
        /// unlike the default server address, this one is a service that has
        /// to be able to move without a new build reaching every player.
        /// </summary>
        public static string MasterHost { get; set; } = Network.NetMasterConfig.DefaultHost;
        public static int MasterPort { get; set; } = Network.NetMasterConfig.DefaultPort;
        public static int LastRole { get; set; }
        public static string PlayerName { get; set; } = "Player";
        /// <summary>Hunter last chosen, possibly <see cref="Hunter.Random"/>.</summary>
        public static Hunter LastHunter { get; set; } = Hunter.Samus;

        /// <summary>
        /// Which of the hunter's four suits this player wears, 0-3.
        ///
        /// A preference rather than a per-launch question, which is why it
        /// sits here beside the name and not on the Host and Join cards: it
        /// is what you look like, and nobody wants to answer it twice a
        /// session. Team modes ignore it -- the team's own two palettes are
        /// the point there -- and a match where somebody in a lower slot has
        /// already taken this suit on this hunter moves it along by one. See
        /// <see cref="Network.PlayerColors"/>.
        /// </summary>
        public static int LastColor { get; set; }
        /// <summary>Bots in an offline match.</summary>
        public static int Bots { get; set; } = 3;
        /// <summary>0 easy, 1 normal, 2 hard -- PlayerEntity.BotLevel.</summary>
        public static int BotLevel { get; set; } = 1;
        public static int HostPort { get; set; } = Network.NetConfig.DefaultPort;

        /// <summary>
        /// Whether a hosted game announces itself to the directory.
        ///
        /// On by default -- a game nobody can find is a game nobody joins --
        /// but it does publish this machine's address on a public list, which
        /// is why it is a switch on the card rather than a decision made for
        /// the person hosting.
        /// </summary>
        public static bool ListHostedGame { get; set; } = true;

        /// <summary>
        /// Whether "Host a game" asks the directory to run the match instead
        /// of running it on this machine.
        ///
        /// The default, because it is the one that works: a server on a home
        /// PC is unreachable from outside unless UDP is forwarded to it, and
        /// asking somebody to configure their router is asking most people not
        /// to play.
        /// </summary>
        public static bool HostOnMaster { get; set; } = true;
        /// <summary>Last LaunchKind, so the front screen can offer it again.</summary>
        public static int LastKind { get; set; }

        /// <summary>
        /// Whether the front screen looks for a new release.
        ///
        /// Looks only. Finding one puts "Update now" on the screen, and that
        /// opens the release page in a browser; the download and the unpacking
        /// are the player's. On by default because a server refuses a client on
        /// a different protocol version outright, so an out-of-date copy is not
        /// a slightly worse copy, it is one that cannot join anything -- and
        /// nobody should have to work that out from a failed connection.
        /// </summary>
        public static bool AutoUpdate { get; set; } = true;

        /// <summary>
        /// How the game window opens. Kept here rather than in MenuSettings
        /// for the same reason as everything else in this file, and read by
        /// Mods.WindowMode, which is where the window itself lives.
        /// </summary>
        public static WindowStartMode WindowMode { get; set; } = WindowStartMode.Windowed;

        /// <summary>
        /// Whether the program writes a file of everything it can say about
        /// itself. See <see cref="Mods.DebugLog"/>.
        ///
        /// On, everywhere, and switchable off in the corner of the front
        /// screen.
        ///
        /// It used to be off and asked for, which reads as the careful choice
        /// and is the wrong one: the reports it exists to answer -- a crash
        /// while a map loads, on a machine nobody here can plug in -- are
        /// exactly the ones where nobody thought to turn it on beforehand, and
        /// asking a player to reproduce a crash with logging enabled is asking
        /// them to hit it twice. It costs a directory that grows and a lock on
        /// every line printed; that is a cheaper price than a bug report
        /// nobody can act on.
        /// </summary>
        public static bool DebugLogs { get; set; } = true;


        public static void Load()
        {
            if (!File.Exists(Path))
            {
                return;
            }
            try
            {
                foreach (string raw in File.ReadAllLines(Path))
                {
                    string line = raw.Trim();
                    int split = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || split <= 0)
                    {
                        continue;
                    }
                    string key = line[..split].Trim();
                    string value = line[(split + 1)..].Trim();
                    switch (key)
                    {
                        case "server_address":
                            ServerAddress = value;
                            break;
                        case "master_host":
                            if (value.Length > 0)
                            {
                                MasterHost = value;
                            }
                            break;
                        case "master_port":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int masterPort)
                                && masterPort > 0 && masterPort <= 65535)
                            {
                                MasterPort = masterPort;
                            }
                            break;
                        case "server_port":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int port))
                            {
                                ServerPort = port;
                            }
                            break;
                        case "player_name":
                            if (value.Length > 0)
                            {
                                PlayerName = value;
                            }
                            break;
                        case "last_role":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int role))
                            {
                                LastRole = role;
                            }
                            break;
                        case "hunter":
                            if (Enum.TryParse(value, ignoreCase: true, out Hunter hunter))
                            {
                                LastHunter = hunter;
                            }
                            break;
                        case "color":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int color))
                            {
                                LastColor = Network.PlayerColors.Clamp(color);
                            }
                            break;
                        case "bots":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int bots))
                            {
                                Bots = bots;
                            }
                            break;
                        case "bot_level":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int level))
                            {
                                BotLevel = level;
                            }
                            break;
                        case "host_on_master":
                            if (Boolean.TryParse(value, out bool hostOnMaster))
                            {
                                HostOnMaster = hostOnMaster;
                            }
                            break;
                        case "list_hosted":
                            if (Boolean.TryParse(value, out bool listHosted))
                            {
                                ListHostedGame = listHosted;
                            }
                            break;
                        case "host_port":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int hostPort))
                            {
                                HostPort = hostPort;
                            }
                            break;
                        case "window_mode":
                            WindowMode = Mods.WindowMode.Parse(value, WindowMode);
                            break;
                        case "auto_update":
                            if (Boolean.TryParse(value, out bool autoUpdate))
                            {
                                AutoUpdate = autoUpdate;
                            }
                            break;
                        case "debug_logs":
                            if (Boolean.TryParse(value, out bool debugLogs))
                            {
                                DebugLogs = debugLogs;
                            }
                            break;
                        case "last_kind":
                            if (Int32.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int kind))
                            {
                                LastKind = kind;
                            }
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // Preferences are a convenience; a unreadable file must not
                // stop the launcher from opening.
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllLines(Path, new[]
                {
                    $"# {Branding.Name} launcher preferences.",
                    $"server_address={ServerAddress}",
                    $"server_port={ServerPort.ToString(CultureInfo.InvariantCulture)}",
                    $"master_host={MasterHost}",
                    $"master_port={MasterPort.ToString(CultureInfo.InvariantCulture)}",
                    $"last_role={LastRole.ToString(CultureInfo.InvariantCulture)}",
                    $"player_name={PlayerName}",
                    $"hunter={LastHunter}",
                    $"color={LastColor.ToString(CultureInfo.InvariantCulture)}",
                    $"bots={Bots.ToString(CultureInfo.InvariantCulture)}",
                    $"bot_level={BotLevel.ToString(CultureInfo.InvariantCulture)}",
                    $"host_port={HostPort.ToString(CultureInfo.InvariantCulture)}",
                    $"list_hosted={ListHostedGame.ToString().ToLowerInvariant()}",
                    $"host_on_master={HostOnMaster.ToString().ToLowerInvariant()}",
                    $"last_kind={LastKind.ToString(CultureInfo.InvariantCulture)}",
                    $"auto_update={AutoUpdate.ToString().ToLowerInvariant()}",
                    $"debug_logs={DebugLogs.ToString().ToLowerInvariant()}",
                    $"window_mode={(WindowMode == WindowStartMode.BorderlessFullscreen ? "borderless" : "windowed")}"
                });
            }
            catch (Exception)
            {
                // Same rationale as Load: never block launching over this.
            }
        }
    }
}
