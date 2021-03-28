using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MphRead.Testing
{
    public static class TestOverlay
    {
        public static void CompareGames(string game1, string game2)
        {
            string root1 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game1);
            string root2 = Path.Combine(Path.GetDirectoryName(Paths.FileSystem) ?? "", game2);
            var dirs1 = Directory.EnumerateDirectories(root1, "", SearchOption.AllDirectories)
                .Select(d => d.Replace(root1, "")).ToList();
            var dirs2 = Directory.EnumerateDirectories(root2, "", SearchOption.AllDirectories)
                .Select(d => d.Replace(root2, "")).ToList();
            var files1 = new Dictionary<string, List<string>>();
            var files2 = new Dictionary<string, List<string>>();
            foreach (string dir in dirs1)
            {
                files1.Add(dir, new List<string>());
                foreach (string file in Directory.EnumerateFiles(Path.Combine(root1, dir)))
                {
                    files1[dir].Add(Path.GetFileName(file));
                }
            }
            foreach (string dir in dirs2)
            {
                files2.Add(dir, new List<string>());
                foreach (string file in Directory.EnumerateFiles(Path.Combine(root2, dir)))
                {
                    files2[dir].Add(Path.GetFileName(file));
                }
            }
            var dir1not2 = dirs1.Where(d => !dirs2.Contains(d)).ToList();
            if (dir1not2.Count > 0)
            {
                Console.WriteLine($"Directories in {game1} not in {game2}:");
                foreach (string dir in dir1not2)
                {
                    Console.WriteLine($"-- {dir}");
                }
                Console.WriteLine();
            }
            var dir2not1 = dirs2.Where(d => !dirs1.Contains(d)).ToList();
            if (dir2not1.Count > 0)
            {
                Console.WriteLine($"Directories in {game2} not in {game1}:");
                foreach (string dir in dir2not1)
                {
                    Console.WriteLine($"-- {dir}");
                }
                Console.WriteLine();
            }
            foreach (string dir in dirs1.Where(d => dirs2.Contains(d)))
            {
                var file1not2 = files1[dir].Where(d => !files2[dir].Contains(d)).ToList();
                var file2not1 = files2[dir].Where(d => !files1[dir].Contains(d)).ToList();
                if (file1not2.Count > 0 || file2not1.Count > 0)
                {
                    Console.WriteLine(dir);
                }
                if (file1not2.Count > 0)
                {
                    Console.WriteLine($"Files in {game1} not in {game2}:");
                    foreach (string file in file1not2)
                    {
                        Console.WriteLine($"-- {file}");
                    }
                }
                if (file2not1.Count > 0)
                {
                    Console.WriteLine($"Files in {game2} not in {game1}:");
                    foreach (string file in file2not1)
                    {
                        Console.WriteLine($"-- {file}");
                    }
                }
                if (file1not2.Count > 0 || file2not1.Count > 0)
                {
                    Console.WriteLine();
                }
            }
            foreach (string dir in dirs1.Where(d => dirs2.Contains(d)))
            {
                var changes = new List<string>();
                foreach (string file in files1[dir].Where(d => files2[dir].Contains(d)))
                {
                    byte[] bytes1 = File.ReadAllBytes(Path.Combine(root1, dir, file));
                    byte[] bytes2 = File.ReadAllBytes(Path.Combine(root2, dir, file));
                    if (!Enumerable.SequenceEqual(bytes1, bytes2))
                    {
                        changes.Add(file);
                    }
                }
                if (changes.Count > 0)
                {
                    Console.WriteLine(dir);
                    Console.WriteLine("Changed files:");
                    foreach (string file in changes)
                    {
                        Console.WriteLine(file);
                    }
                    Console.WriteLine();
                }
            }
            Nop();
        }

        public static void Translate(int mask)
        {
            mask = 0x21;
            var active = new List<int>();
            for (int i = 0; i < 18; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    active.Add(OverlayMap[i]);
                }
            }
            Console.WriteLine(String.Join(", ", active.OrderBy(a => a)));
            Nop();
        }

        private static void Nop()
        {
        }

        public static readonly IReadOnlyList<int> OverlayMap = new List<int>()
        {
            /*  0 */ 4,
            /*  1 */ 6,
            /*  2 */ 17,
            /*  3 */ 5,
            /*  4 */ 16,
            /*  5 */ 0,
            /*  6 */ 7,
            /*  7 */ 1,
            /*  8 */ 2,
            /*  9 */ 3,
            /* 10 */ 8,
            /* 11 */ 15,
            /* 12 */ 10,
            /* 13 */ 9,
            /* 14 */ 11,
            /* 15 */ 12,
            /* 16 */ 13,
            /* 17 */ 14
        };
    }

    [Flags]
    public enum MphOverlay
    {
        None = 0x0,
        WiFiPlay = 0x1,
        DownloadPlay = 0x2,
        Bit02 = 0x4, // 20 bytes
        VoiceChat = 0x8,
        Bit04 = 0x10, // 20 bytes
        Frontend = 0x20,
        Bit06 = 0x40, // ?
        Movies = 0x80,
        Gameplay = 0x100,
        MpEntities = 0x200,
        SpEntities1 = 0x400,
        SpEntities2 = 0x800,
        Enemies = 0x1000,
        BotAi = 0x2000,
        Cretaphid = 0x4000,
        Gorea = 0x8000,
        Slench = 0x10000,
        Bit17 = 0x20000 // 20 bytes
    }
}
