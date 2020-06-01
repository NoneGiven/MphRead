using System;
using System.Diagnostics.CodeAnalysis;

namespace MphRead
{
    internal static class Program
    {
        public static Version Version { get; } = new Version(0, 2, 0, 0);

        private static void Main(string[] args)
        {
            using var renderer = new Renderer();
            if (args.Length > 0)
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
            else
            {
                //renderer.AddRoom("MP3 PROVING GROUND");
                renderer.AddModel("Guardian_lod1");
                Nop();
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
            Environment.Exit(1);
        }
        
        private static void Nop() { }
    }

    public class ProgramException : Exception
    {
        public ProgramException(string message) : base(message) { }
    }
}
