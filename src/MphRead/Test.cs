using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MphRead
{
    public static class Test
    {
#pragma warning disable IDE0051 // Remove unused private members

        public static void TestCollision()
        {
            ushort headerSize = 0;
            var sizes = new List<(int, ushort, string)>();
            string modelPath = Paths.FileSystem;
            foreach (string path in Directory.EnumerateFiles(modelPath, "", SearchOption.AllDirectories))
            {
                if (path.ToLower().Contains("_collision.bin"))
                {
                    var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
                    ushort eight = BitConverter.ToUInt16(bytes[0x04..0x06]);
                    sizes.Add((bytes.Length - headerSize, eight, path.Replace(Paths.FileSystem + "\\", "")));
                }
            }
            foreach ((int size, int eight, string name) in sizes.OrderBy(s => s.Item1))
            {
                Console.WriteLine($"{size,6} {eight,4} - {name.Replace("_archive", "archive")}");
            }
            Nop();
        }

        public static void WriteAllModels()
        {
            string modelPath = Path.Combine(Paths.FileSystem, "models");
            var modelFiles = new List<string>();
            var textureFiles = new List<string>();
            var animationFiles = new List<string>();
            var collisionFiles = new List<string>();
            var unknownFiles = new List<string>();
            foreach (string path in Directory.EnumerateFiles(modelPath, "", SearchOption.AllDirectories))
            {
                string pathLower = path.ToLower();
                if (pathLower.Contains("_model.bin"))
                {
                    modelFiles.Add(path);
                }
                else if (pathLower.Contains("_tex.bin"))
                {
                    textureFiles.Add(path);
                }
                else if (pathLower.Contains("_anim.bin"))
                {
                    animationFiles.Add(path);
                }
                else if (pathLower.Contains("_collision.bin"))
                {
                    collisionFiles.Add(path);
                }
                else
                {
                    unknownFiles.Add(path);
                }
            }
            var lines = new List<string>();
            lines.Add($"model ({modelFiles.Count}):");
            lines.AddRange(modelFiles.OrderBy(m => m));
            lines.Add("");
            lines.Add($"texture ({textureFiles.Count}):");
            lines.AddRange(textureFiles.OrderBy(m => m));
            lines.Add("");
            lines.Add($"animation ({animationFiles.Count}):");
            lines.AddRange(animationFiles.OrderBy(m => m));
            lines.Add("");
            lines.Add($"collision ({collisionFiles.Count}):");
            lines.AddRange(collisionFiles.OrderBy(m => m));
            lines.Add("");
            lines.Add($"unknown ({unknownFiles.Count}):");
            lines.AddRange(unknownFiles.OrderBy(m => m));
            File.WriteAllLines("models.txt", lines);
            lines.Clear();

            void AddMatch(string model, string suffix, List<string> list)
            {
                model = model.ToLower().Replace("_lod0", "").Replace("lod1", "");
                string match1 = model.Replace("_model.bin", $"_{suffix}.bin");
                string match2 = match1.Replace("_mdl", "");
                int index = list.IndexOf(f => f.ToLower() == match1 || f.ToLower() == match2);
                if (index != -1)
                {
                    lines!.Add(list[index]);
                    list.RemoveAt(index);
                }
            }

            foreach (string file in modelFiles)
            {
                lines.Add(file);
                AddMatch(file, "tex", textureFiles);
                AddMatch(file, "anim", animationFiles);
                AddMatch(file, "collision", collisionFiles);
                lines.Add("");
            }
            lines.Add("unmatched texture:");
            foreach (string file in textureFiles)
            {
                lines.Add(file);
            }
            lines.Add("");
            lines.Add("unmatched animation:");
            foreach (string file in animationFiles)
            {
                lines.Add(file);
            }
            lines.Add("");
            lines.Add("unmatched collision:");
            foreach (string file in collisionFiles)
            {
                lines.Add(file);
            }
            File.WriteAllLines("matches.txt", lines);
        }

        public static void ParseAllModels()
        {
            GetAllModels();
        }

        public static void TestAllModels()
        {
            foreach (Model model in GetAllModels())
            {
                foreach (Recolor recolor in model.Recolors)
                {
                    foreach (Material material in model.Materials)
                    {
                        if (material.TextureId != UInt16.MaxValue)
                        {
                            recolor.GetPixels(material.TextureId, material.PaletteId);
                        }
                    }
                }
            }
        }

        public static void TestDifAmb()
        {
            foreach (Model model in GetAllModels())
            {
                bool f = false;
                foreach (IReadOnlyList<RenderInstruction> list in model.RenderInstructionLists)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        RenderInstruction instruction = list[i];
                        if (instruction.Code == InstructionCode.DIF_AMB)
                        {
                            Console.WriteLine(i);
                            f = true;
                            //System.Diagnostics.Debugger.Break();
                        }
                    }
                    if (f)
                    {
                        f = false;
                        Console.WriteLine();
                    }
                }
            }
        }

        public static void TestAllEntities()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, i);
                        foreach (Entity entity in entities)
                        {
                            if (entity.Type == EntityType.Object)
                            {
                                ObjectEntityData data = ((Entity<ObjectEntityData>)entity).Data;
                            }
                        }
                    }
                }
            }
            Nop();
        }

#pragma warning restore IDE0051 // Remove unused private members

        private static IEnumerable<Model> GetAllModels()
        {
            foreach (KeyValuePair<string, EntityMetadata> meta in Metadata.EntityMetadata)
            {
                yield return Read.GetModelByName(meta.Key);
            }
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                yield return Read.GetRoomByName(meta.Key);
            }
        }

        private static IEnumerable<Model> GetAllRooms()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                yield return Read.GetRoomByName(meta.Key);
            }
        }

        private static void Nop() { }
    }
}
