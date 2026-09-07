using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MphRead.Mods.Update
{
    /// <summary>
    /// A dedicated server keeps itself on the newest release.
    ///
    /// Everywhere else in this program an update is a notice and a link,
    /// deliberately: installing means executing a file nobody signed, and the
    /// person at the keyboard is the one who should decide. A headless server
    /// is the case where every part of that reasoning inverts.
    ///
    /// There is nobody at the keyboard -- that is what "dedicated" means -- so
    /// "ask the operator" is not caution, it is a server that stays stale
    /// until somebody happens to read a journal. And staleness is not cosmetic
    /// here: <see cref="Mods.Network.NetConfig.ProtocolVersion"/> makes a
    /// server refuse every client on a different build at Hello, so the first
    /// protocol bump turns an un-updated server into a server nobody in the
    /// world can join, which looks from the outside exactly like a server that
    /// is switched off. The failure the manual policy was protecting against
    /// -- a bad binary -- is one an operator can undo. The failure it caused
    /// is one nobody can even see.
    ///
    /// So: check at startup before the port is bound, check again on a timer,
    /// and swap when the swap costs nothing. "Costs nothing" is the whole of
    /// the politeness here -- a server with players on it waits until they
    /// have gone rather than dropping a match to be a version newer.
    ///
    /// <para>
    /// The swap is not the desktop's. <see cref="DesktopUpdate"/> starts a
    /// second process which waits for this one to exit and then copies over
    /// the installation, and that is exactly wrong under systemd: the copier
    /// is a child, so it lives in the unit's control group, and the moment the
    /// main process exits systemd kills the group and restarts the unit --
    /// killing the copier mid-copy and bringing the old build back up. The
    /// update would appear to do nothing, for ever, with no error anywhere.
    /// </para>
    /// <para>
    /// What works instead needs no second process at all. A running program on
    /// Linux holds its files by inode, not by name: the file can be unlinked
    /// and a new one put at the same path, and the process carries on with
    /// what it has open until it exits. So the new build is written over the
    /// installation *by the server itself*, one atomic rename at a time, and
    /// then the process exits and comes back as the new build.
    /// </para>
    /// <para>
    /// Windows refuses to delete a running image, and for a while that was
    /// where this stopped: the copy failed halfway, the operator was told to
    /// finish it by hand, and until they did, the server sat on the old build.
    /// Harmless while a stale server merely lagged; not harmless at all once a
    /// protocol bump made one refuse every client in the world. Windows does
    /// allow a running image to be *renamed*, though -- the mapping follows
    /// the file rather than the name -- so there the old build is moved aside
    /// and swept away by the next start. See <see cref="ReplaceInPlace"/>.
    /// </para>
    /// </summary>
    public static class ServerUpdate
    {
        /// <summary>
        /// Off with <c>-noautoupdate</c>, and with <c>-noupdate</c>, which
        /// turns off the checking as well and so turns off everything here.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// How often a running server looks again.
        ///
        /// Ten minutes, on every platform. It used to be six hours, on the
        /// grounds that releases are rare and GitHub's API is rate limited --
        /// both true, and both beside the point. What decides this number is
        /// not how often a release happens, it is how long a server is
        /// unjoinable after one: <see cref="Mods.Network.NetConfig.ProtocolVersion"/>
        /// makes a server on the old build refuse every client on the new one
        /// at Hello, so the window between a release going out and a server
        /// picking it up is a window in which that server looks, to everybody
        /// trying to join it, switched off. Six hours of that is a server
        /// nobody can play on for the rest of the evening; ten minutes is a
        /// pause.
        ///
        /// It is affordable at that rate. GitHub allows sixty unauthenticated
        /// requests an hour per address and this is six -- twelve on a machine
        /// running a server and a directory, which is the busiest case here --
        /// and a check that finds nothing new downloads nothing at all. A
        /// server with players on it still waits for them to leave before it
        /// swaps, so looking more often costs no interrupted matches either.
        /// </summary>
        public static TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>What was staged and is waiting for an empty server.</summary>
        public static UpdateInfo? Pending { get; private set; }

        /// <summary>Whether a complete new build is on disk, ready to swap in.</summary>
        public static bool Staged { get; private set; }

        private static string[] _relaunch = Array.Empty<string>();
        private static DateTime _nextCheck = DateTime.MinValue;
        private static bool _working;
        private static readonly object _lock = new object();

        /// <summary>
        /// Whether something is watching this process and will start it again
        /// when it exits.
        ///
        /// systemd sets <c>INVOCATION_ID</c> for every service it runs, and it
        /// is the difference between "exit and be restarted" and "exit and be
        /// gone". Under a supervisor, exiting *is* the restart and starting a
        /// replacement here would race the supervisor's own -- two servers,
        /// one port, and whichever loses prints a bind error and dies. With no
        /// supervisor there is nothing to come back, so this process has to
        /// start its successor itself.
        /// </summary>
        private static bool Supervised =>
            !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("INVOCATION_ID"))
            || !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("LISTEN_PID"));

        /// <summary>
        /// Look before the port is bound, and swap if there is anything to
        /// swap. Blocking, and only ever called when no one is connected --
        /// nothing is bound yet.
        /// </summary>
        /// <param name="commandLine">
        /// What this server was started with, so it comes back as the same
        /// server. Restarting a dedicated server bare would open a launcher.
        /// </param>
        /// <returns>Whether the caller should exit now, updated.</returns>
        public static bool AtStartup(IReadOnlyList<string> commandLine)
        {
            _relaunch = new List<string>(commandLine).ToArray();
            _nextCheck = DateTime.UtcNow + Interval;
            // Before anything else, and whether or not updating is on: what it
            // sweeps was left by an update that has already happened, and a
            // server switched to -noautoupdate afterwards should not keep the
            // debris for ever.
            SweepOld(AppContext.BaseDirectory);
            if (!Enabled || Updater.Disabled)
            {
                return false;
            }
            UpdateInfo? update;
            try
            {
                update = Updater.Check();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[update] could not check: {ex.Message}");
                return false;
            }
            if (update == null)
            {
                // Either up to date or unable to say. UpdateCheck.LastReason
                // separates the two, and only the second is worth a line: a
                // server that is current should be silent.
                if (!String.IsNullOrEmpty(UpdateCheck.LastReason))
                {
                    Console.WriteLine($"[update] {UpdateCheck.LastReason}");
                }
                return false;
            }
            Console.WriteLine($"[update] {Updater.Describe(update.Value)}");
            if (!Stage(update.Value))
            {
                Console.WriteLine("[update] carrying on with "
                    + $"{BuildVersion.Display}");
                return false;
            }
            return Swap("before binding");
        }

        /// <summary>
        /// Called from the server's own loop, every iteration. Cheap: it does
        /// nothing at all until the timer is up, and starts its work on
        /// another thread when it is.
        /// </summary>
        /// <param name="playerCount">
        /// How many people are connected. A staged update waits for this to be
        /// zero -- being one release behind for another twenty minutes is
        /// nothing next to ending somebody's match to fix it.
        /// </param>
        /// <returns>Whether the caller should shut down, updated.</returns>
        public static bool ShouldRestart(int playerCount)
        {
            if (!Enabled || Updater.Disabled)
            {
                return false;
            }
            if (Staged)
            {
                if (playerCount > 0)
                {
                    return false;
                }
                return Swap("the server is empty");
            }
            lock (_lock)
            {
                if (_working || DateTime.UtcNow < _nextCheck)
                {
                    return false;
                }
                _working = true;
                _nextCheck = DateTime.UtcNow + Interval;
            }
            // Both halves off the loop's thread: the check is a request across
            // the internet and the staging is a download, and a server that
            // stops relaying packets while GitHub thinks about it is a server
            // that has dropped everybody to look for a new version of itself.
            Task.Run(() =>
            {
                try
                {
                    UpdateInfo? update = Updater.Check();
                    if (update != null)
                    {
                        Console.WriteLine($"[update] {Updater.Describe(update.Value)}");
                        if (Stage(update.Value))
                        {
                            Console.WriteLine("[update] staged; it will be applied "
                                + "as soon as the server is empty");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[update] check failed: {ex.Message}");
                }
                finally
                {
                    lock (_lock)
                    {
                        _working = false;
                    }
                }
            });
            return false;
        }

        /// <summary>Fetch and unpack, without touching the installation.</summary>
        private static bool Stage(UpdateInfo update)
        {
            if (!DesktopUpdate.Supported)
            {
                Console.WriteLine("[update] this installation cannot update itself "
                    + $"({DesktopUpdate.LastError ?? "the directory is not writable"}); "
                    + $"fetch it from {update.PageUrl}");
                return false;
            }
            Console.WriteLine($"[update] fetching {update.AssetName}");
            if (!DesktopUpdate.Stage(update))
            {
                Console.WriteLine($"[update] could not stage: {DesktopUpdate.LastError}");
                return false;
            }
            Pending = update;
            Staged = true;
            return true;
        }

        /// <summary>
        /// Put the staged build over this one and say whether to exit.
        ///
        /// Every file is written to a neighbouring name and then renamed into
        /// place, which on both platforms is the one operation that either
        /// happened or did not. A machine that loses power in the middle of
        /// this comes back with a mix of two builds either way -- there is no
        /// transaction across a whole directory -- but no single file is ever
        /// a half-written one, which is the difference between a server that
        /// refuses to start and one that starts and then behaves strangely.
        /// </summary>
        private static bool Swap(string why)
        {
            string staged = DesktopUpdate.StagedBuildPath;
            string target = AppContext.BaseDirectory;
            if (!Directory.Exists(staged))
            {
                Staged = false;
                return false;
            }
            string version = Pending.HasValue
                ? Pending.Value.Version.ToString() : "the new build";
            Console.WriteLine($"[update] applying {version} ({why})");
            try
            {
                ReplaceInPlace(staged, target);
            }
            catch (Exception ex)
            {
                // Loud, because the installation may now be a mix of two
                // builds and the staged one is still on disk to finish by
                // hand. The server keeps running on what it has loaded, which
                // is the old build in memory -- correct until it restarts.
                Console.WriteLine($"[update] the copy failed: {ex.Message}");
                Console.WriteLine($"[update] the new build is in {staged} -- "
                    + $"copy it over {target} by hand");
                Staged = false;
                return false;
            }
            Staged = false;
            if (Supervised)
            {
                Console.WriteLine("[update] applied; exiting for the supervisor "
                    + "to start the new build");
                return true;
            }
            if (!Restart(target))
            {
                // Nothing will bring it back, so staying up on the old build in
                // memory beats exiting into silence. The files are already the
                // new ones, so the next restart by hand gets it.
                Console.WriteLine("[update] applied, but could not restart -- "
                    + "this server is still running the old build until it is "
                    + "restarted by hand");
                return false;
            }
            Console.WriteLine("[update] applied; restarting");
            return true;
        }

        /// <summary>
        /// Copy <paramref name="source"/> over <paramref name="target"/> while
        /// the target is in use.
        ///
        /// Getting the running program out of the way is the whole problem,
        /// and the two platforms solve it differently.
        ///
        /// **Unix**: the delete is what makes this legal. Writing into a file
        /// this process has mapped would corrupt the running program;
        /// unlinking it and creating a new one at the same path does not touch
        /// what is mapped at all -- the old inode stays alive, unnamed, until
        /// the process exits. A POSIX guarantee.
        ///
        /// **Windows**: deleting a running image is refused, and this used to
        /// stop there -- the copy failed, the operator was told to finish it
        /// by hand, and the installation was left as a mix of two builds,
        /// since every file enumerated before the executable had already been
        /// replaced. A Windows server therefore never updated itself, which
        /// stopped being cosmetic the moment a protocol bump made a stale
        /// server one that refuses every client in the world.
        ///
        /// But Windows *does* allow a running image to be **renamed**: the
        /// mapping follows the file, not the name, so moving it aside and
        /// putting the new build at the old name is legal and atomic. The
        /// leftover is deleted by the next start (<see cref="SweepOld"/>) --
        /// it cannot be deleted by this one, which is still running out of it.
        ///
        /// So: delete where deleting works, rename aside where it does not,
        /// and rename the new file into place either way.
        /// </summary>
        private static void ReplaceInPlace(string source, string target)
        {
            foreach (string path in Directory.EnumerateFiles(source, "*",
                SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, path);
                string destination = Path.Combine(target, relative);
                string? directory = Path.GetDirectoryName(destination);
                if (!String.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string incoming = destination + IncomingSuffix;
                File.Copy(path, incoming, overwrite: true);
                if (File.Exists(destination))
                {
                    Displace(destination);
                }
                File.Move(incoming, destination);
                MakeExecutable(destination);
            }
        }

        /// <summary>
        /// Get one file out of the way: deleted if this machine will delete
        /// it, renamed aside if it will not.
        ///
        /// The rename is tried second rather than first because a delete
        /// leaves nothing behind and a rename leaves something for the next
        /// start to tidy. On Unix the delete always works; on Windows it works
        /// for every file except the ones this process is running out of,
        /// which is exactly the set that has to be renamed.
        /// </summary>
        private static void Displace(string destination)
        {
            try
            {
                File.Delete(destination);
                return;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // In use. Fall through to the rename, and let *that* throw if
                // this is a file nothing can do anything with -- a genuine
                // permission problem should still reach the operator.
            }
            string aside = destination + OldSuffix;
            File.Delete(aside); // no-op when it is not there; a stale one otherwise
            File.Move(destination, aside);
        }

        /// <summary>
        /// The two names this leaves in an installation, and neither is a name
        /// anything else uses: a half-written incoming file if the machine
        /// lost power mid-copy, and a displaced build that was still running
        /// when it was replaced.
        /// </summary>
        private const string IncomingSuffix = ".incoming";
        private const string OldSuffix = ".fp-old";

        /// <summary>
        /// Delete what a previous update had to leave behind.
        ///
        /// Called at startup, which is the first moment the old build is not
        /// running any more -- that is the whole reason it is a separate pass
        /// rather than the last line of the swap. Failure is nothing: a file
        /// that will not delete is a few megabytes beside an installation, and
        /// the next start will try again.
        /// </summary>
        private static void SweepOld(string target)
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(target, "*" + OldSuffix,
                    SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception)
                    {
                    }
                }
                foreach (string path in Directory.EnumerateFiles(target, "*" + IncomingSuffix,
                    SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
                // A directory that cannot be enumerated is one this had no
                // business tidying.
            }
        }

        private static bool Restart(string target)
        {
            try
            {
                string binary = Path.Combine(target, BinaryName());
                var start = new ProcessStartInfo(binary)
                {
                    WorkingDirectory = target,
                    UseShellExecute = false
                };
                for (int i = 0; i < _relaunch.Length; i++)
                {
                    start.ArgumentList.Add(_relaunch[i]);
                }
                return Process.Start(start) != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[update] could not restart: {ex.Message}");
                return false;
            }
        }

        private static string BinaryName()
        {
            string name = UpdateCheck.IsServerBuild
                ? Branding.FileName + "Server"
                : Branding.FileName;
            return OperatingSystem.IsWindows() ? name + ".exe" : name;
        }

        private static void MakeExecutable(string path)
        {
            if (OperatingSystem.IsWindows() || Path.GetExtension(path).Length > 0)
            {
                return;
            }
            try
            {
                File.SetUnixFileMode(path, File.GetUnixFileMode(path)
                    | UnixFileMode.UserExecute | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherExecute);
            }
            catch (Exception)
            {
                // Not fatal here: the file is in place, and a mode that did not
                // take is something the next start reports far more clearly
                // than this line would.
            }
        }
    }
}
