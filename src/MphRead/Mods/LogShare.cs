using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MphRead.Mods
{
    /// <summary>
    /// The log files, gathered into one file somebody can send.
    ///
    /// "Turn on debugging logs and send me the file" has a second half that
    /// the switch alone does not answer, and on a phone it is the hard half:
    /// the logs live in the app's own directory, no file manager since
    /// Android 11 can browse it, and there is nothing to attach to a message
    /// even when the player knows exactly which file is wanted. So the program
    /// hands the file over itself.
    ///
    /// A zip rather than the newest log on its own, because the run that
    /// crashed is often not the run being reported from -- <c>DebugLog</c>
    /// keeps several -- and because a file with an extension every mail
    /// client and chat app already knows travels better than a .log.
    /// </summary>
    public static class LogArchive
    {
        /// <summary>Where <c>DebugLog</c> writes. Not created by this.</summary>
        public static string Directory =>
            Path.Combine(Launcher.LauncherPrefs.Directory, "logs");

        /// <summary>
        /// The logs, newest first, or an empty list. Never throws: a directory
        /// that cannot be listed is the same answer as one with nothing in it.
        /// </summary>
        public static List<FileInfo> Files()
        {
            var files = new List<FileInfo>();
            try
            {
                var directory = new DirectoryInfo(Directory);
                if (!directory.Exists)
                {
                    return files;
                }
                files.AddRange(directory.GetFiles("*.log"));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            }
            catch (Exception)
            {
                files.Clear();
            }
            return files;
        }

        /// <summary>Whether there is anything to send at all.</summary>
        public static bool Any() => Files().Count > 0;

        /// <summary>
        /// Write every log into a zip at <paramref name="zipPath"/>.
        ///
        /// The log being written right now is included, and that is the point:
        /// the interesting one is usually this session's. <c>DebugLog</c>
        /// opens it <c>FileShare.ReadWrite</c> and flushes every line, so it
        /// can be read while it is still being written -- but only by a reader
        /// that also says ReadWrite, which is why the bytes are copied by hand
        /// rather than with <c>CreateEntryFromFile</c>: that one asks for
        /// FileShare.Read and would be refused the open.
        /// </summary>
        public static bool Create(string zipPath, out string error)
        {
            error = "";
            List<FileInfo> files = Files();
            if (files.Count == 0)
            {
                error = "there are no logs yet";
                return false;
            }
            try
            {
                string? directory = Path.GetDirectoryName(zipPath);
                if (!String.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                using var stream = new FileStream(zipPath, FileMode.Create,
                    FileAccess.Write, FileShare.None);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
                int written = 0;
                foreach (FileInfo file in files)
                {
                    try
                    {
                        using FileStream source = file.Open(FileMode.Open,
                            FileAccess.Read, FileShare.ReadWrite);
                        ZipArchiveEntry entry = zip.CreateEntry(
                            file.Name, CompressionLevel.Optimal);
                        entry.LastWriteTime = file.LastWriteTime;
                        using Stream target = entry.Open();
                        source.CopyTo(target);
                        written++;
                    }
                    catch (Exception)
                    {
                        // One unreadable file is not a reason to send none of
                        // the others.
                    }
                }
                if (written == 0)
                {
                    error = "none of the logs could be read";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            return true;
        }

        /// <summary>What the zip should be called. Dated, since it is sent.</summary>
        public static string FileName() =>
            $"{Branding.Name.Replace(" ", "")}-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
    }

    /// <summary>
    /// A platform that can hand a file to another application.
    ///
    /// Only Android has one today, and the seam exists for the same reason
    /// <see cref="Update.IUpdateInstaller"/>'s does: the shared screen must not
    /// know what a content URI is. Left null -- which is every desktop -- the
    /// front screen simply has no Share button, rather than one that opens
    /// something nobody asked for.
    /// </summary>
    public interface ILogShare
    {
        /// <summary>
        /// Where to build the zip. The platform's own scratch space, since the
        /// file has to outlive this call: the chooser is another app, and it
        /// reads the file after this has returned.
        /// </summary>
        string StagingPath(string fileName);

        /// <summary>
        /// Offer the file to whatever the player picks. True when the chooser
        /// was put up -- what they do with it afterwards never comes back.
        /// </summary>
        bool Share(string path, string subject, out string error);
    }

    /// <summary>The sharer this build has, or null. Set by the platform head.</summary>
    public static class LogShare
    {
        public static ILogShare? Current { get; set; }

        /// <summary>Whether the front screen should offer the button at all.</summary>
        public static bool Available => Current != null && LogArchive.Any();
    }
}
