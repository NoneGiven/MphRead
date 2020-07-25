using System;
using System.Runtime.InteropServices;

namespace MphRead
{
    internal static class ConsoleColor
    {
        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        public static void Setup()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr iStdIn = GetStdHandle(-10);
                IntPtr iStdOut = GetStdHandle(-11);
                GetConsoleMode(iStdIn, out uint inConsoleMode);
                GetConsoleMode(iStdOut, out uint outConsoleMode);
                inConsoleMode |= 0x0200;
                outConsoleMode |= 0x0004 | 0x0008;
                SetConsoleMode(iStdIn, inConsoleMode);
                SetConsoleMode(iStdOut, outConsoleMode);
            }
        }
    }
}
