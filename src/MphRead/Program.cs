using System;
using System.Diagnostics.CodeAnalysis;

namespace MphRead
{
    internal static class Program
    {
        public static Version Version { get; } = new Version(0, 6, 0, 0);

        private static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                if (args[0] == "-export" || args[0] == "-e")
                {
                    Read.ReadAndExport(args[1]);
                    return;
                }
                if (args[0] == "-extract" || args[0] == "-x")
                {
                    Read.ExtractArchive(args[1]);
                    return;
                }
            }
            using var renderer = new Renderer();
            if (args.Length == 0)
            {
                Test.TestAllModels();
                renderer.AddRoom("UNIT2_LAND");
                //renderer.AddModel("lightningLob", firstHunt: true);
                //renderer.AddModel("balljump_ray", firstHunt: true);
                //renderer.AddModel("balljump_ray");
                Nop();
            }
            else if (args.Length > 1)
            {
                bool foundRoom = false;
                bool foundModel = false;
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg == "-room" || arg == "-r")
                    {
                        if (foundRoom)
                        {
                            Exit();
                        }
                        foundRoom = true;
                        string? modelName = GetString(args, i + 1);
                        if (modelName == null)
                        {
                            Exit();
                        }
                        int mask = GetInt(args, i + 2);
                        renderer.AddRoom(modelName, mask);
                    }
                    else if (arg == "-model" || arg == "-m")
                    {
                        foundModel = true;
                        string? name = GetString(args, i + 1);
                        if (name == null)
                        {
                            Exit();
                        }
                        int recolor = GetInt(args, i + 2);
                        renderer.AddModel(name, recolor);
                    }
                }
                if (!foundRoom && !foundModel)
                {
                    Exit();
                }
            }
            renderer.Run();
        }

        private static string? GetString(string[] args, int index)
        {
            if (index > args.Length - 1)
            {
                return null;
            }
            return args[index];
        }

        private static int GetInt(string[] args, int index)
        {
            if (index > args.Length - 1 || !Int32.TryParse(args[index], out int result))
            {
                return 0;
            }
            return result;
        }

        [DoesNotReturn]
        private static void Exit()
        {
            Console.WriteLine("MphRead usage:");
            Console.WriteLine("    -room <room_name> [layer_mask]");
            Console.WriteLine("    -model <model_name> [recolor_index]");
            Console.WriteLine("At most one room may be specified. Any number of models may be specified.");
            Console.WriteLine("- or -");
            Console.WriteLine("    -extract <archive_path>");
            Console.WriteLine("If the target archive is LZ10-compressed, it will be decompressed.");
            Console.WriteLine("- or -");
            Console.WriteLine("    -export <target_name>");
            Console.WriteLine("The export target may be a model or room name.");
            Environment.Exit(1);
        }

        private static void Nop() { }
    }

    public class ProgramException : Exception
    {
        public ProgramException(string message) : base(message) { }
    }
}
