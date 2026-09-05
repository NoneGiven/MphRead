using System;
using System.Collections.Generic;
using System.Linq;
using MphRead.Entities;
using MphRead.Mods.Network;

namespace MphRead.Mods
{
    /// <summary>
    /// Single dispatch point for everything under Mods/.
    ///
    /// Upstream is touched in exactly one place (a call to TryHandle in
    /// Program.Main) so that pulling from NoneGiven/MphRead stays a fast
    /// forward instead of a conflict hunt. Every new mod command is added
    /// here, not in Program.cs.
    /// </summary>
    public static class ModEntry
    {
        /// <summary>
        /// Returns true if a mod command handled this invocation and the
        /// program should exit without running the normal paths.
        ///
        /// Takes the raw argv rather than Program's parsed Argument type,
        /// which is private: matching on the raw strings keeps the upstream
        /// hook to a single line and adds no coupling to internals that may
        /// be refactored later.
        /// </summary>
        /// <summary>
        /// Commands that must run before the game-file setup check, because
        /// they need neither paths.txt nor extracted assets. Kept separate
        /// from TryHandle so the dedicated server can run on a machine that
        /// has no game data at all.
        /// </summary>
        public static bool TryHandleHeadless(string[] args)
        {
            // Keys and mouse feel, before anything creates a player. Called
            // here because this runs for every invocation, launcher or not.
            InputSettings.Load();
            // And the file of everything the program can say about itself, if
            // the player has asked for one. Read the preferences here rather
            // than waiting for the launcher to: a crash while a map loads
            // happens on paths that never open one, and the point of the log
            // is to be already running when that happens. -debuglog turns it
            // on for a single run without the setting, for the case where the
            // launcher itself is what will not start.
            Launcher.LauncherPrefs.Load();
            if (HasFlag(args, "debuglog"))
            {
                DebugLog.Force();
            }
            DebugLog.Attach();
            Update.Updater.Disabled = HasFlag(args, "noupdate");
            ApplyRenderOverrides(args);

            // The copying half of a desktop update, which is this build
            // started by the *previous* one. First, and before anything reads
            // a file or draws a window: it is not the game, it waits for the
            // old process to exit and copies itself over the installation.
            // See Mods/Update/DesktopUpdate.cs.
            int applyAt = IndexOfFlag(args, Update.DesktopUpdate.ApplyFlag);
            if (applyAt >= 0 && applyAt + 2 < args.Length)
            {
                // Two values, read by position rather than by name: the first
                // is a directory, and a directory is exactly the kind of
                // argument that can begin with a dash.
                Environment.ExitCode = Update.DesktopUpdate.Apply(args[applyAt + 1],
                    Int32.TryParse(args[applyAt + 2], out int parsed) ? parsed : -1);
                return true;
            }
            // Whatever the last update left behind. Here rather than in the
            // copying process, which cannot delete the directory it is running
            // from, and cheap when there is nothing there.
            Update.DesktopUpdate.Clean();
            // And the desktop's own installer, unless a platform head has
            // already put its own in place.
            Update.UpdateInstall.UseDesktopIfPossible();

            // A bad line, asked for. Before anything opens a socket, and for
            // every path that has one -- the game, the harness client and the
            // dedicated server alike -- so a fault that only shows up at 200
            // ms can be reproduced against the real server rather than only
            // behind a proxy in front of a local one. See Mods/Network/NetLag.
            string? netLag = ValueAfter(args, "netlag");
            if (netLag != null && !Network.NetLag.Configure(netLag))
            {
                Console.WriteLine($"[net] -netlag {netLag} is not a number of "
                    + "milliseconds (try -netlag 200 or -netlag 200:40)");
                return true;
            }
            string? netLoss = ValueAfter(args, "netloss");
            if (netLoss != null && !Network.NetLag.ConfigureLoss(netLoss))
            {
                Console.WriteLine($"[net] -netloss {netLoss} is not a percentage");
                return true;
            }
            if (Network.NetLag.Active)
            {
                Console.WriteLine($"[net] simulating a bad line: {Network.NetLag.Describe()}");
            }

            if (HasFlag(args, "credits"))
            {
                Credits.Print();
                return true;
            }

            // Where the maps are. Read for every invocation and before
            // anything reads the map list, which is loaded once -- and against
            // the directory the command was typed in rather than the one the
            // process moved itself to (see ConsoleSetup.LaunchDirectory), so
            // `-mapdir maps` from a checkout means that checkout's maps.
            string? mapDir = ValueAfter(args, "mapdir");
            if (mapDir != null)
            {
                MapGen.CustomRooms.MapDirectory = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(ConsoleSetup.LaunchDirectory, mapDir));
            }

            // Cooking a bundle is here, before the game-file check, for the
            // reason the dedicated server is: it reads a recipe, the level
            // beside it and the textures baked from it, and touches no
            // extracted game data at all. The workflow runs it on a runner
            // that has none, where the check exits with "press any key" on a
            // console nobody is looking at -- and then throws, because there
            // is no console to read a key from either.
            if (HasFlag(args, "mapbundle"))
            {
                string? which = ValueAfter(args, "mapbundle");
                string? outPath = ValueAfter(args, "out");
                int cooked = 0;
                int failed = 0;
                foreach (MapGen.MapDefinition def in MapGen.CustomRooms.Definitions)
                {
                    if (which != null && !which.Equals(def.Name, StringComparison.OrdinalIgnoreCase)
                        && !which.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (def.SourcePath == null || def.BundlePath != null || def.Import == null)
                    {
                        // Already a bundle, or a map that builds from its own
                        // description and has no level to carry.
                        continue;
                    }
                    try
                    {
                        MapGen.MapBundle.Cook(def, def.SourcePath, outPath);
                        cooked++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{def.Name}: {ex.Message}");
                        failed++;
                    }
                }
                if (cooked == 0 && failed == 0)
                {
                    Console.WriteLine("No map to bundle. A bundle is cooked from a recipe and the "
                        + $"level it converts; put both in {MapGen.CustomRooms.MapDirectory}.");
                }
                Environment.ExitCode = failed == 0 ? 0 : 1;
                return true;
            }


            // The explicit check, so there is always one command that answers
            // "am I on the latest build". Nothing is downloaded here either:
            // it prints the release page and opens it if there is a desktop to
            // open it on.
            if (HasFlag(args, "update"))
            {
                Update.Updater.Disabled = false;
                Update.UpdateInfo? update = Update.Updater.Check();
                if (update == null)
                {
                    Console.WriteLine($"[update] {Update.UpdateCheck.LastReason}");
                    return true;
                }
                Console.WriteLine($"[update] {Update.Updater.Describe(update.Value)}");
                Console.WriteLine($"[update] {update.Value.PageUrl}");
                Update.Updater.OpenPage(update.Value);
                return true;
            }
            // Before the game-file check, not after: a fresh install has no
            // paths.txt, and the check exits with "press any key" on a console
            // nobody is looking at. The launcher is the screen that fixes
            // that, so it has to be reachable first.
            // The launcher. The window first, the text screen when there is no
            // display to put it on -- an SSH login, a container, a machine with
            // no X or Wayland session. -text asks for the text one on a machine
            // that has both.
            //
            // No arguments means somebody double-clicked the binary, and on the
            // platforms where that is how a program is normally started that
            // has to be the launcher: the console menu behind it is for people
            // who typed something, and -menu is how they still get it. On Linux
            // a bare invocation has always opened upstream's console menu and
            // still does -- that is a screen people are already using, not an
            // empty spot to fill.
            bool doubleClicked = args.Length == 0
                && (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS());
#if MPHREAD_SERVER
            // Except in the server package, which has no launcher of either
            // kind and ships without game files: a bare invocation there is
            // answered further down by ServerUsage, which says what the binary
            // is for. Double-clicking FruityPrimeServer.exe must not open a
            // text launcher offering matches it cannot play.
            doubleClicked = false;
#endif
            if ((HasFlag(args, "launcher") || doubleClicked) && !HasFlag(args, "menu"))
            {
#if MPHREAD_AVALONIA
                if (!HasFlag(args, "text") && Launcher.Gui.GuiLauncher.TryRun())
                {
                    return true;
                }
                // The window could not be opened. On Windows that means the
                // process has no console either -- it is a GUI binary -- so the
                // text launcher would print into nothing.
                if (OperatingSystem.IsWindows())
                {
                    Mods.ConsoleWindow.Show();
                }
#endif
                Launcher.TextLauncher.Run();
                return true;
            }
#if MPHREAD_SERVER
            // The server package, run with nothing to do. Falling through to
            // upstream's setup check would answer with "could not find
            // paths.txt, drag a ROM onto the executable" -- true of this
            // binary, and useless: it ships without game files because it
            // needs none, and it cannot play a match even with them.
            if (args.Length == 0)
            {
                ServerUsage();
                return true;
            }
#endif
            // Both servers say so at startup if they are behind, and then get
            // on with it.
            //
            // A protocol change makes a server refuse every client on an older
            // build at Hello, so a stale server is a server nobody can join,
            // and that is worth one line in the journal where an operator will
            // find it. It is a line and not an install: nothing here has a
            // person at the keyboard to decide, and a server that replaced its
            // own binary and restarted would drop whoever was playing.
            if (HasFlag(args, "masterserver") || HasFlag(args, "server")
                || HasFlag(args, "dedicated"))
            {
                Update.UpdateInfo? update = Update.Updater.Check();
                if (update != null)
                {
                    Console.WriteLine($"[update] {Update.Updater.Describe(update.Value)}");
                    Console.WriteLine($"[update] {update.Value.PageUrl}");
                    Console.WriteLine("[update] this server keeps running on "
                        + $"{Update.BuildVersion.Display}; clients on the new build "
                        + "will be refused until it is updated by hand");
                }
            }

            // The server directory: -masterserver. Same binary as the game
            // server on purpose -- the machine that runs one usually runs the
            // other, and a second thing to install is a second thing to forget
            // to restart.
            if (HasFlag(args, "masterserver"))
            {
                int masterPort = NetMasterConfig.DefaultPort;
                string? masterPortValue = ValueAfter(args, "port")
                    ?? ValueAfter(args, "masterport");
                if (masterPortValue != null && Int32.TryParse(masterPortValue, out int parsedMasterPort))
                {
                    masterPort = parsedMasterPort;
                }
                var master = new MasterServer(masterPort);
                using var masterSignals = new ShutdownSignals();
                // The ports it may start games on, for players whose routers
                // will not forward one. A range by default, because the whole
                // point of the feature is that it works without anybody being
                // asked to configure it; -hostports none turns it off.
                string hostPorts = ValueAfter(args, "hostports") ?? "27900-27919";
                if (!hostPorts.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = hostPorts.Split('-', 2);
                    if (parts.Length == 2 && Int32.TryParse(parts[0], out int first)
                        && Int32.TryParse(parts[1], out int last) && first > 0 && last >= first)
                    {
                        master.SetHostPorts(first, last);
                    }
                    else
                    {
                        Console.WriteLine($"[master] ignoring -hostports {hostPorts} "
                            + "(expected e.g. 27900-27919, or none)");
                    }
                }
                // The address to hand out for servers running on this same
                // machine, which is the usual arrangement: the directory and
                // one game server on one small box. Their heartbeats arrive
                // over the loopback, and a list of loopback addresses is a
                // list of servers nobody can reach.
                string? publicHost = ValueAfter(args, "public")
                    ?? ValueAfter(args, "publicaddress");
                if (publicHost != null)
                {
                    master.SetPublicAddress(publicHost);
                }
                using var masterCancel = new System.Threading.CancellationTokenSource();
                masterSignals.OnShutdown(() =>
                {
                    masterCancel.Cancel();
                    master.Stop();
                });
                master.Run(masterCancel.Token);
                return true;
            }
            // The server list, printed. Same two calls the launcher's browser
            // makes -- ask the directory, then ask each server it named -- so
            // this is how that data path gets checked on a machine with no
            // WinForms, which is every machine that is not Windows.
            if (HasFlag(args, "servers"))
            {
                ListServers(ValueAfter(args, "master") ?? NetMasterConfig.DefaultHost,
                    ValueAfter(args, "masterport"));
                return true;
            }
            if (!HasFlag(args, "server") && !HasFlag(args, "dedicated"))
            {
                return false;
            }
            int port = NetConfig.DefaultPort;
            string? portValue = ValueAfter(args, "port");
            if (portValue != null && Int32.TryParse(portValue, out int parsedPort))
            {
                port = parsedPort;
            }
            int maxPlayers = 4;
            string? playersValue = ValueAfter(args, "players");
            if (playersValue != null && Int32.TryParse(playersValue, out int parsedPlayers))
            {
                maxPlayers = parsedPlayers;
            }

            // Rotation file lives beside the executable, the way a Quake 3
            // server keeps its config next to the binary.
            string rotationPath = ValueAfter(args, "rotation")
                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "maprotation.txt");
            MapRotation rotation = MapRotation.LoadOrCreate(rotationPath);

            var server = new Network.DedicatedServer(port, maxPlayers, rotation)
            {
                ServerName = ValueAfter(args, "servername") ?? ValueAfter(args, "name")
                    ?? Environment.MachineName,
                FriendlyFire = HasFlag(args, "friendlyfire")
            };
            // Listed by default. A dedicated server exists to be found, and a
            // server that has to be told to advertise itself is a server
            // nobody finds -- so the flag is the one that opts out.
            if (!HasFlag(args, "nomaster") && !HasFlag(args, "unlisted"))
            {
                string masterHost = ValueAfter(args, "master") ?? NetMasterConfig.DefaultHost;
                int reportPort = NetMasterConfig.DefaultPort;
                string? reportPortValue = ValueAfter(args, "masterport");
                if (reportPortValue != null && Int32.TryParse(reportPortValue, out int parsedReport))
                {
                    reportPort = parsedReport;
                }
                server.Reporter = new MasterReporter(masterHost, reportPort);
                Console.WriteLine($"[server] listing on {masterHost}:{reportPort} "
                    + $"as \"{server.ServerName}\" (-nomaster to stay private)");
            }
            using var cancel = new System.Threading.CancellationTokenSource();
            using var signals = new ShutdownSignals();
            signals.OnShutdown(() =>
            {
                cancel.Cancel();
                server.Stop();
            });
            server.Run(cancel.Token);
            return true;
        }

#if MPHREAD_SERVER
        /// <summary>
        /// What this binary is for, for somebody who started it with no
        /// arguments -- which on Windows is anybody who double-clicked it.
        /// </summary>
        private static void ServerUsage()
        {
            string exe = System.IO.Path.GetFileNameWithoutExtension(
                Environment.ProcessPath) ?? "MphReadServer";
            Console.WriteLine();
            Console.WriteLine($"{Branding.Name} dedicated server. It needs no game files.");
            Console.WriteLine();
            Console.WriteLine($"  {exe} -server -port {NetConfig.DefaultPort} -players 8 "
                + "-servername \"My server\"");
            Console.WriteLine("      run a server. Maps come from maprotation.txt, written");
            Console.WriteLine("      beside this program on first run.");
            Console.WriteLine();
            Console.WriteLine($"  {exe} -masterserver -port {NetMasterConfig.DefaultPort}");
            Console.WriteLine("      run a server directory of your own.");
            Console.WriteLine();
            Console.WriteLine($"  {exe} -servers");
            Console.WriteLine("      list the servers that are up right now.");
            Console.WriteLine();
            Console.WriteLine("A server lists itself on " + NetMasterConfig.DefaultHost
                + " so players can find it;");
            Console.WriteLine("-nomaster keeps it off every list. See SERVER.txt.");
            Console.WriteLine();
            // Double-clicked, so this window is about to close with everything
            // above it still unread.
            if (OperatingSystem.IsWindows() && ConsoleWindow.OwnsItsConsole())
            {
                Console.WriteLine("Press any key to close this window...");
                Console.ReadKey();
            }
        }
#endif

        private static void ListServers(string masterHost, string? portValue)
        {
            int port = NetMasterConfig.DefaultPort;
            if (portValue != null && Int32.TryParse(portValue, out int parsed))
            {
                port = parsed;
            }
            Console.WriteLine($"[servers] asking {masterHost}:{port}");
            MasterListResult result = NetMasterClient.Query(masterHost, port);
            if (!result.Answered)
            {
                Console.WriteLine($"[servers] no answer from {masterHost}:{port} -- "
                    + "it may be down, or UDP may not reach it");
                return;
            }
            if (result.Servers.Count == 0)
            {
                Console.WriteLine("[servers] the directory is up and has nobody listed");
                return;
            }
            Console.WriteLine($"[servers] {result.Servers.Count} listed; asking each one");
            foreach (MasterListing listing in result.Servers)
            {
                // Directly, not through the directory: the round trip that
                // matters is this machine's, and the answer also proves the
                // server is reachable from here rather than only from there.
                ServerStatus status = NetStatus.Query(listing.Address, listing.Port,
                    allowJoinProbe: false);
                string name = status.ServerName.Length > 0
                    ? status.ServerName
                    : listing.ServerName.Length > 0 ? listing.ServerName : listing.Endpoint;
                if (!status.Online)
                {
                    Console.WriteLine($"  {name,-24} {listing.Endpoint,-26} did not answer");
                    continue;
                }
                string players = status.MaxPlayers > 0
                    ? $"{status.Players}/{status.MaxPlayers}"
                    : status.Players.ToString();
                string ping = status.Latency >= 0 ? $"{status.Latency} ms" : "-- ms";
                Console.WriteLine($"  {name,-24} {listing.Endpoint,-26} "
                    + $"{status.RoomKey,-20} {NetStatus.ModeName(status.Mode),-14} "
                    + $"{players,-6} {ping}");
            }
        }

        public static bool TryHandle(string[] args)
        {
            (int width, int height) = ParseSize(args);

            // Custom maps are registered as rooms from their JSON at startup,
            // but a room whose binaries are not on disk crashes the moment
            // something tries to load it. Generating what is missing here --
            // the one place every entry point passes through, launcher
            // included, and after the game-file check -- means a map file is
            // enough to have a working room.
            if (!HasFlag(args, "mapgen"))
            {
                MapGen.CustomRooms.GenerateMissing();
            }

            // Opt-in per-second report of what this process believes about a
            // networked session -- slot occupancy, scoreboard count, which
            // remote slots have state. The failure worth catching is not
            // visible on the wire: two correctly connected clients can each
            // hold a scene containing only themselves.
            // Display flags, for the paths that never open a launcher.
            if (HasFlag(args, "fullscreen") || HasFlag(args, "borderless"))
            {
                WindowMode.Startup = WindowStartMode.BorderlessFullscreen;
            }
            else if (HasFlag(args, "windowed"))
            {
                WindowMode.Startup = WindowStartMode.Windowed;
            }
            if (HasFlag(args, "nohelmet"))
            {
                // Both of them: the helmet is drawn as three layers and the
                // visor is one of them, so zeroing only HelmetOpacity leaves a
                // tinted pane over the view that reads as a bug rather than as
                // a setting. The settings window ties the two together for the
                // same reason.
                Features.HelmetOpacity = 0;
                Features.VisorOpacity = 0;
            }
            if (HasFlag(args, "netdebug"))
            {
                Network.NetDiagnostics.Enabled = true;
                Network.MapAudit.Diagnostic = true;
            }

            // Print the game's own tables as markdown, so the mechanics
            // documentation is generated from the data rather than kept by
            // hand and quietly going stale.
            if (HasFlag(args, "mechanics"))
            {
                Network.MechanicsDump.Run();
                return true;
            }

            // What a connected pad is doing, with no match in the way. The
            // only way to tell "not connected" from "connected but not
            // mapped" from "the dead zone is eating it" apart.
            if (HasFlag(args, "gamepad"))
            {
                double seconds = 15;
                string? given = ValueAfter(args, "seconds");
                if (given != null && Double.TryParse(given, out double parsed) && parsed > 0)
                {
                    seconds = parsed;
                }
                Environment.ExitCode = Input.GamepadProbe.Run(seconds);
                return true;
            }

            // The multiplayer room list, one per line, so a shell loop can
            // walk every map without hard-coding the names.
            if (HasFlag(args, "rooms"))
            {
                foreach (string room in ThumbnailGenerator.MultiplayerRooms())
                {
                    Console.WriteLine(room);
                }
                return true;
            }

            // Load one room with a full house of players and report what it
            // contains and whether it survived.
            string? dpsTest = ValueAfter(args, "dpstest");
            if (dpsTest != null)
            {
                Hunter dpsHunter = Hunter.Sylux;
                string? dpsHunterValue = ValueAfter(args, "hunter");
                if (dpsHunterValue != null && Enum.TryParse(dpsHunterValue, ignoreCase: true, out Hunter parsedDpsHunter))
                {
                    dpsHunter = parsedDpsHunter;
                }
                BeamType dpsBeam = BeamType.ShockCoil;
                string? dpsBeamValue = ValueAfter(args, "weapon");
                if (dpsBeamValue != null && Enum.TryParse(dpsBeamValue, ignoreCase: true, out BeamType parsedDpsBeam))
                {
                    dpsBeam = parsedDpsBeam;
                }
                double dpsSeconds = 10;
                string? dpsSecondsValue = ValueAfter(args, "seconds");
                if (dpsSecondsValue != null && Double.TryParse(dpsSecondsValue,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedDpsSeconds))
                {
                    dpsSeconds = parsedDpsSeconds;
                }
                float dpsDistance = 2.2f;
                string? dpsDistanceValue = ValueAfter(args, "distance");
                if (dpsDistanceValue != null && Single.TryParse(dpsDistanceValue,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsedDpsDistance))
                {
                    dpsDistance = parsedDpsDistance;
                }
                Environment.ExitCode = Network.WeaponDps.Run(dpsTest, dpsHunter, dpsBeam, dpsSeconds, dpsDistance);
                return true;
            }
            // Generate the binaries for the custom maps in `maps/`. The
            // textures come out of the player's own extracted files, so this
            // has to run here rather than at build time, and what ships in the
            // repository is the JSON, never the .bin.
            if (HasFlag(args, "mapgen"))
            {
                string? only = ValueAfter(args, "mapgen");
                bool force = HasFlag(args, "force");
                int count = 0;
                int failed = 0;
                foreach (MapGen.MapDefinition def in MapGen.CustomRooms.Definitions)
                {
                    if (only != null && !only.Equals(def.Name, StringComparison.OrdinalIgnoreCase)
                        && !only.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    // one map at a time: a map that cannot be built -- most
                    // often one whose source level is not where it says --
                    // must not stop the others from being generated
                    try
                    {
                        MapGen.MapPacker.Generate(def, MapGen.CustomRooms.ArchiveDirectory(def),
                            MapGen.CustomRooms.EntityDirectory(), MapGen.CustomRooms.NodeDirectory(),
                            verbose: true);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{def.Name}: {ex.Message}");
                        failed++;
                    }
                }
                if (count == 0 && failed == 0)
                {
                    Console.WriteLine($"No maps to generate. Put a map JSON in {MapGen.CustomRooms.MapDirectory}.");
                }
                Environment.ExitCode = failed == 0 ? 0 : 1;
                return true;
            }

            // What levels are in a .pk3, so a conversion knows what to ask
            // for. Reads the archive's index only -- nothing is extracted.
            string? q3Maps = ValueAfter(args, "q3maps");
            if (q3Maps != null)
            {
                try
                {
                    foreach (string name in MapGen.Q3Bsp.ListMaps(q3Maps))
                    {
                        Console.WriteLine(name);
                    }
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not read {q3Maps}: {ex.Message}");
                    Environment.ExitCode = 1;
                }
                return true;
            }

            // A .pk3 to a room, in one command: the textures baked from the
            // level's own art, the scale and the extents picked from its
            // geometry, the spawns from its entities, and a map file written
            // out. Weapons and powerups are left for a person to place.
            string? q3Convert = ValueAfter(args, "q3convert");
            if (q3Convert != null)
            {
                float? scale = null;
                if (Single.TryParse(ValueAfter(args, "scale"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsedScale) && parsedScale > 0)
                {
                    scale = parsedScale;
                }
                int textureSize = Int32.TryParse(ValueAfter(args, "texsize"), out int parsedSize)
                    && parsedSize >= 8 && parsedSize <= 256
                        ? parsedSize
                        : MapGen.MapTextureBake.DefaultSize;
                try
                {
                    Environment.ExitCode = MapGen.Q3Convert.Run(q3Convert, ValueAfter(args, "map"),
                        ValueAfter(args, "name"), ValueAfter(args, "out"), HasFlag(args, "noclip"),
                        scale, textureSize);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not convert {q3Convert}: {ex.Message}");
                    Environment.ExitCode = 1;
                }
                return true;
            }

            // What a level draws with, commonest first, so a conversion knows
            // which shaders are worth mapping to a borrowed texture.
            string? q3Shaders = ValueAfter(args, "q3shaders");
            if (q3Shaders != null)
            {
                Environment.ExitCode = MapGen.MapReport.ListShaders(q3Shaders, ValueAfter(args, "map"));
                return true;
            }

            // List a room's materials, with the texture each one uses, so a
            // map can say which of them it wants to borrow.
            string? mapMaterials = ValueAfter(args, "mapmaterials");
            if (mapMaterials != null)
            {
                Environment.ExitCode = MapGen.MapReport.ListMaterials(mapMaterials);
                return true;
            }

            // Pictures of the launcher's own screens, rendered without a
            // window. The one part of this program that could not be looked at
            // from a headless box.
            string? uiShot = ValueAfter(args, "uishot");
            if (uiShot != null)
            {
                Environment.ExitCode = RunUiCapture(uiShot);
                return true;
            }

            if (HasFlag(args, "frametimingcheck"))
            {
                Environment.ExitCode = Render.FrameTimingCheck.Run();
                return true;
            }

            string? mapTest = ValueAfter(args, "maptest");
            if (mapTest != null)
            {
                int players = 8;
                string? playerValue = ValueAfter(args, "players");
                if (playerValue != null && Int32.TryParse(playerValue, out int parsed))
                {
                    players = parsed;
                }
                double seconds = 10;
                string? secondsValue = ValueAfter(args, "seconds");
                if (secondsValue != null && Double.TryParse(secondsValue,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedSeconds))
                {
                    seconds = parsedSeconds;
                }
                GameMode mapMode = GameMode.Battle;
                string? modeValue = ValueAfter(args, "mode");
                if (modeValue != null && Enum.TryParse(modeValue, ignoreCase: true, out GameMode parsedMode))
                {
                    mapMode = parsedMode;
                }
                // The HUD is drawn to the window, not to the offscreen
                // target every other capture reads, so seeing it needs a real
                // window and a read from its buffer.
                Network.MapAudit.ShowWindow = HasFlag(args, "hudshots");
                // -drawrate N draws each simulation step N times, which is
                // what a 144 Hz screen does to a 60 Hz game. It is how the
                // decoupled loop is checked from a box with no display.
                string? drawRate = ValueAfter(args, "drawrate");
                if (drawRate != null && Int32.TryParse(drawRate, out int parsedDrawRate)
                    && parsedDrawRate > 0)
                {
                    Network.MapAudit.DrawRate = parsedDrawRate;
                }
                // -size WxH, so a HUD capture can be taken at a window shape
                // other than the one this happens to default to.
                string? sizeValue = ValueAfter(args, "size");
                if (sizeValue != null)
                {
                    string[] parts = sizeValue.ToLowerInvariant().Split('x');
                    if (parts.Length == 2 && Int32.TryParse(parts[0], out int sizeWidth)
                        && Int32.TryParse(parts[1], out int sizeHeight)
                        && sizeWidth > 0 && sizeHeight > 0)
                    {
                        Network.MapAudit.WindowSize = new OpenTK.Mathematics.Vector2i(sizeWidth, sizeHeight);
                    }
                }
                Environment.ExitCode = Network.MapAudit.Run(mapTest, players, seconds, mapMode,
                    bots: HasFlag(args, "bots"), shotDirectory: ValueAfter(args, "shots"),
                    renderProbe: HasFlag(args, "renderprobe"),
                    allNodes: HasFlag(args, "allnodes"));
                return true;
            }

            // Ask the directory to run a match and join it. The launcher's
            // "Online, no setup" in one command -- and the only way to host
            // from a machine with no launcher, which is every machine that is
            // not Windows.
            string? hostGame = ValueAfter(args, "hostgame");
            if (hostGame != null)
            {
                string masterHost = ValueAfter(args, "master") ?? NetMasterConfig.DefaultHost;
                int masterPort = NetMasterConfig.DefaultPort;
                string? masterPortValue = ValueAfter(args, "masterport");
                if (masterPortValue != null && Int32.TryParse(masterPortValue, out int parsedMaster))
                {
                    masterPort = parsedMaster;
                }
                GameMode hostMode = GameMode.Battle;
                string? hostModeValue = ValueAfter(args, "mode");
                if (hostModeValue != null
                    && Enum.TryParse(hostModeValue, ignoreCase: true, out GameMode parsedHostMode))
                {
                    hostMode = parsedHostMode;
                }
                string hostName = ParseName(args);
                Console.WriteLine($"[net] asking {masterHost}:{masterPort} to run {hostGame}");
                HostedGame game = NetMasterClient.RequestGame(masterHost, masterPort,
                    hostGame, hostMode, timeLimit: 7 * 60, pointGoal: 7,
                    maxPlayers: PlayerEntity.SlotCapacity, serverName: $"{hostName}'s game");
                if (!game.Started)
                {
                    Console.WriteLine($"[net] it would not: {game.Reason}");
                    Environment.ExitCode = 1;
                    return true;
                }
                Console.WriteLine($"[net] running on {game.Host}:{game.Port}; joining it");
                Network.NetConnectCommand.Run(game.Host, game.Port, hostName,
                    ParseHunter(args), ParseRecolor(args));
                return true;
            }

            // Join a server from the command line, with no launcher dialog.
            // The only way to start a client on a platform without WinForms,
            // and the only practical way to start two of them side by side --
            // which is the arrangement every bug in this feature has needed.
            string? connect = ValueAfter(args, "connect");
            if (connect != null)
            {
                Network.NetConnectCommand.Run(connect, ParsePort(args), ParseName(args),
                    ParseHunter(args), ParseRecolor(args));
                return true;
            }

            // The same client, running to a script and reporting what it saw.
            string? check = ValueAfter(args, "netcheck");
            if (check != null)
            {
                string? shots = ValueAfter(args, "shots");
                double seconds = 30;
                string? secondsValue = ValueAfter(args, "seconds");
                if (secondsValue != null && Double.TryParse(secondsValue,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedSeconds))
                {
                    seconds = parsedSeconds;
                }
                // Watching instead of playing. -spectate on its own means
                // "from the moment the map is up"; with a number it is the
                // second to stop playing at, and -rejoin the second to come
                // back. Spectating is the one player state the scripted tour
                // cannot reach on its own -- the tour exists to drive a
                // hunter, and this is a player who has stopped driving one.
                double spectateAt = -1;
                double rejoinAt = -1;
                if (HasFlag(args, "spectate"))
                {
                    spectateAt = 0;
                    string? spectateValue = ValueAfter(args, "spectate");
                    if (spectateValue != null && Double.TryParse(spectateValue,
                        System.Globalization.CultureInfo.InvariantCulture, out double parsedSpectate))
                    {
                        spectateAt = parsedSpectate;
                    }
                }
                string? rejoinValue = ValueAfter(args, "rejoin");
                if (rejoinValue != null && Double.TryParse(rejoinValue,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedRejoin))
                {
                    rejoinAt = parsedRejoin;
                }
                Environment.ExitCode = Network.NetCheckClient.Run(check, ParsePort(args),
                    ParseName(args), ParseHunter(args), seconds, shots, width, height,
                    recordDemo: HasFlag(args, "recorddemo"),
                    spectateAt: spectateAt, rejoinAt: rejoinAt);
                return true;
            }

            // What a recorded match actually contains. Reads the file and
            // nothing else -- no room, no window, no game files.
            string? demoInfo = ValueAfter(args, "demoinfo");
            if (demoInfo != null)
            {
                Environment.ExitCode = Network.DemoInfo.Print(demoInfo,
                    replay: HasFlag(args, "replay"));
                return true;
            }

            // Worker invocation: capture the rooms this process was given and
            // exit. This is what ThumbnailBatch spawns -- a share of the
            // batch rather than one room, so the runtime that starts and the
            // code that JITs are paid for once across several pictures -- and
            // a single -thumbnail still works by hand to re-shoot one map.
            List<string> share = ValuesAfter(args, "thumbnail");
            if (share.Count > 0)
            {
                int captured = ThumbnailCapture.CaptureRooms(share, width, height);
                Console.WriteLine($"[thumbnails] captured {captured}/{share.Count}");
                return true;
            }

            if (HasFlag(args, "thumbnails"))
            {
                GenerateThumbnails(args, width, height);
                return true;
            }
            return false;
        }

        private static void GenerateThumbnails(string[] args, int width, int height)
        {
            bool force = HasFlag(args, "force");
            IReadOnlyList<string> rooms = force
                ? ThumbnailGenerator.MultiplayerRooms()
                : ThumbnailGenerator.MissingThumbnails();
            if (rooms.Count == 0)
            {
                Console.WriteLine("[thumbnails] all previews already present in "
                    + ThumbnailGenerator.CacheDirectory);
                Console.WriteLine("[thumbnails] pass -force to re-render them");
                return;
            }
            int jobs = ThumbnailBatch.DefaultParallelism;
            string? jobsValue = ValueAfter(args, "jobs");
            if (jobsValue != null && Int32.TryParse(jobsValue, out int parsedJobs))
            {
                jobs = parsedJobs;
            }
            Console.WriteLine($"[thumbnails] rendering {rooms.Count} preview(s) at "
                + $"{width}x{height}, {jobs} at a time");
            Console.WriteLine($"[thumbnails] output: {ThumbnailGenerator.CacheDirectory}");
            int written = ThumbnailBatch.Run(rooms, jobs, width, height);
            Console.WriteLine($"[thumbnails] done -- {written}/{rooms.Count} written");
        }

        private static (int Width, int Height) ParseSize(string[] args)
        {
            string? value = ValueAfter(args, "size");
            if (value != null)
            {
                string[] parts = value.Split('x', 'X');
                if (parts.Length == 2
                    && Int32.TryParse(parts[0], out int w)
                    && Int32.TryParse(parts[1], out int h)
                    && w > 0 && h > 0)
                {
                    return (w, h);
                }
                Console.WriteLine($"[thumbnails] ignoring -size {value} (expected e.g. 1920x1440)");
            }
            return (ThumbnailGenerator.ThumbnailWidth, ThumbnailGenerator.ThumbnailHeight);
        }

        private static int ParsePort(string[] args)
        {
            string? value = ValueAfter(args, "port");
            return value != null && Int32.TryParse(value, out int port) ? port : NetConfig.DefaultPort;
        }

        private static string ParseName(string[] args)
        {
            return ValueAfter(args, "name") ?? Environment.MachineName;
        }

        private static Hunter ParseHunter(string[] args)
        {
            string? value = ValueAfter(args, "hunter");
            return value != null && Enum.TryParse(value, ignoreCase: true, out Hunter hunter)
                ? hunter
                : Hunter.Samus;
        }

        private static int ParseRecolor(string[] args)
        {
            string? value = ValueAfter(args, "recolor");
            return value != null && Int32.TryParse(value, out int recolor) ? recolor : 0;
        }

        /// <summary>
        /// Render-option overrides from the command line, applied for every
        /// invocation before anything draws.
        ///
        /// The settings file is the launcher's, and the paths that never open
        /// one -- <c>-thumbnail</c>, <c>-maptest</c>, <c>-connect</c> -- had no
        /// way to ask for cel shading at all. That made the one mode whose
        /// whole point is what the picture looks like the one mode no
        /// screenshot command could turn on.
        /// </summary>
        private static void ApplyRenderOverrides(string[] args)
        {
            string? cel = ValueAfter(args, "cel");
            if (cel != null && !cel.StartsWith('-'))
            {
                RenderOptions.CelShading = RenderOptions.ParseOnOff(cel, RenderOptions.CelShading);
            }
            else if (HasFlag(args, "cel"))
            {
                // a bare -cel, with the next word belonging to another option
                RenderOptions.CelShading = true;
            }
            string? fog = ValueAfter(args, "fog");
            if (fog != null && !fog.StartsWith('-'))
            {
                RenderOptions.Fog = RenderOptions.ParseOnOff(fog, RenderOptions.Fog);
            }
            string? fps = ValueAfter(args, "fps");
            if (fps != null && !fps.StartsWith('-'))
            {
                RenderOptions.ShowFps = RenderOptions.ParseOnOff(fps, RenderOptions.ShowFps);
            }
            else if (HasFlag(args, "fps"))
            {
                RenderOptions.ShowFps = true;
            }
            // The frame rate, for the paths that never open a launcher --
            // which is every screenshot command and every scripted run. The
            // simulation is not affected by either of these: it is pinned at
            // 60 Hz in Mods/Render/FrameTiming.cs and these only decide how
            // often, and how smoothly, it is drawn.
            string? fpsCap = ValueAfter(args, "fpscap");
            if (fpsCap != null && !fpsCap.StartsWith('-'))
            {
                Render.FrameTiming.FrameRateCap = Render.FrameTiming.ParseCap(fpsCap,
                    Render.FrameTiming.FrameRateCap);
            }
            string? interp = ValueAfter(args, "interpolation");
            if (interp != null && !interp.StartsWith('-'))
            {
                Render.FrameTiming.Interpolate = RenderOptions.ParseOnOff(interp,
                    Render.FrameTiming.Interpolate);
            }
            else if (HasFlag(args, "nointerpolation"))
            {
                Render.FrameTiming.Interpolate = false;
            }
            string? bands = ValueAfter(args, "celbands");
            if (bands != null && Int32.TryParse(bands, out int bandCount))
            {
                RenderOptions.CelBands = bandCount;
            }
            string? edge = ValueAfter(args, "celedge");
            if (edge != null && Int32.TryParse(edge.TrimEnd('%'), out int edgePercent))
            {
                RenderOptions.CelEdge = edgePercent / 100f;
            }
            // The whole competitive HUD, for the same paths and the same
            // reason: it is a mode whose point is what the picture looks like,
            // and every command that can photograph one opens no launcher.
            string? proHud = ValueAfter(args, "prohud");
            if (proHud != null && !proHud.StartsWith('-'))
            {
                Features.ProHud = RenderOptions.ParseOnOff(proHud, Features.ProHud);
            }
            else if (HasFlag(args, "prohud"))
            {
                Features.ProHud = true;
            }
        }

        private static bool HasFlag(string[] args, string name)
        {
            return args.Any(a => a.TrimStart('-').Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Kept out of <see cref="TryHandle"/> and told not to inline.
        ///
        /// The runtime loads the assemblies a method needs when it first
        /// *enters* that method, not when it reaches the call -- so naming
        /// UiCapture directly in TryHandle made every command load Avalonia,
        /// including `-server`. On a machine without it that is not a missing
        /// feature, it is the dedicated server aborting at startup with a
        /// FileNotFoundException, which is exactly what the netcheck clients
        /// did against a bin/ that had not been refreshed.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int RunUiCapture(string directory)
        {
#if MPHREAD_AVALONIA
            try
            {
                return Launcher.Gui.UiCapture.Run(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[uishot] no launcher toolkit here: {ex.Message}");
                return 1;
            }
#else
            Console.WriteLine("[uishot] this build has no Avalonia launcher");
            return 1;
#endif
        }

        /// <summary>Where an option appears, or -1. For the ones read by position.</summary>
        private static int IndexOfFlag(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].TrimStart('-').Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string? ValueAfter(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].TrimStart('-').Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        /// <summary>
        /// Every value given for a repeated option, in order. A thumbnail
        /// worker is handed a whole share of rooms this way rather than one.
        /// </summary>
        private static List<string> ValuesAfter(string[] args, string name)
        {
            var values = new List<string>();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].TrimStart('-').Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(args[i + 1]);
                }
            }
            return values;
        }
    }
}
