using System;

namespace MphRead
{
    internal static class Program
    {
        public static Version Version { get; } = new Version(0, 1, 0, 0);

        private static void Main(string[] args)
        {
            using var renderer = new Renderer();
            if (args.Length > 0)
            {
                int recolor = 0;
                if (args.Length > 1)
                {
                    Int32.TryParse(args[1], out recolor);
                }
                renderer.AddModel(Read.GetModelByName(args[0], recolor));
            }
            else
            {
                //Model model = Read.GetRoomByName("CRYSTALROOM");
                //model.ExportImages();
                renderer.AddRoom("MP3 PROVING GROUND");
                renderer.AddModel(Read.GetModelByName("Trace_lod1"));
                Nop();
            }
            renderer.Run();
        }

        private static void Nop() { }
    }

    public class ProgramException : Exception
    {
        public ProgramException(string message) : base(message) { }
    }
}
