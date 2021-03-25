using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace MphRead
{
    internal static class ConsoleSetup
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        public static void Run()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr iStdOut = GetStdHandle(-11);
                GetConsoleMode(iStdOut, out uint outConsoleMode);
                outConsoleMode |= 4;
                SetConsoleMode(iStdOut, outConsoleMode);
            }
        }
    }
}
