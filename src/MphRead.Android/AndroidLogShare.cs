using System;
using System.IO;
using Android.Content;
using AndroidX.Core.Content;
using MphRead.Mods;

namespace MphRead.Droid
{
    /// <summary>
    /// Hands the zipped logs to whatever the player picks -- mail, a chat app,
    /// their own files.
    ///
    /// This is the half of "send me your logs" that a phone cannot otherwise
    /// do. The logs are in the app's external files directory, which since
    /// Android 11 no file manager will browse, so without this there is a file
    /// the player has been asked for and no way for them to reach it.
    ///
    /// <c>FileProvider</c> is not optional here, unlike in
    /// <see cref="ApkInstaller"/> which writes into an install session and
    /// needs none: handing another app a <c>file://</c> URI has thrown
    /// <c>FileUriExposedException</c> since Android 7. The provider is
    /// declared in the manifest against <c>@xml/file_paths</c>, and the only
    /// directory it exposes is the one scratch folder below -- not the logs
    /// directory itself, so nothing is shared except the file just built.
    /// </summary>
    internal sealed class AndroidLogShare : ILogShare
    {
        private readonly Context _context;

        public AndroidLogShare(Context context)
        {
            _context = context;
        }

        /// <summary>
        /// Must match the provider's path in <c>Resources/xml/file_paths.xml</c>.
        /// </summary>
        private const string _folder = "logs-share";

        public string StagingPath(string fileName)
        {
            string directory = Path.Combine(
                _context.CacheDir?.AbsolutePath ?? Path.GetTempPath(), _folder);
            Directory.CreateDirectory(directory);
            // One at a time: the previous zip has been sent or abandoned, and
            // leaving them to pile up in the cache is how an app grows a
            // hundred megabytes nobody can explain.
            try
            {
                foreach (string old in Directory.GetFiles(directory, "*.zip"))
                {
                    File.Delete(old);
                }
            }
            catch (Exception)
            {
                // A stale file is not worth failing a share for.
            }
            return Path.Combine(directory, fileName);
        }

        public bool Share(string path, string subject, out string error)
        {
            error = "";
            try
            {
                var file = new Java.IO.File(path);
                Android.Net.Uri? uri = FileProvider.GetUriForFile(
                    _context, _context.PackageName + ".logs", file);
                if (uri == null)
                {
                    error = "the file could not be offered";
                    return false;
                }
                var intent = new Intent(Intent.ActionSend);
                intent.SetType("application/zip");
                intent.PutExtra(Intent.ExtraStream, uri);
                intent.PutExtra(Intent.ExtraSubject, subject);
                // The grant travels with the intent and lasts as long as the
                // receiving app's task: the chooser cannot know in advance
                // which app is about to be given the file.
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                Intent chooser = Intent.CreateChooser(intent, "Share logs")!;
                // The context here is the activity, but a chooser started from
                // a non-activity context needs this and it is harmless when it
                // is one.
                chooser.AddFlags(ActivityFlags.NewTask);
                _context.StartActivity(chooser);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[logs] could not share: {ex}");
                error = ex.Message;
                return false;
            }
        }
    }
}
