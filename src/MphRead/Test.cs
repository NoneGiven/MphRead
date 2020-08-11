using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenToolkit.Mathematics;

namespace MphRead
{
    public static class Test
    {
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

        public static bool TestBytes(string one, string two)
        {
            byte[] bone = File.ReadAllBytes(one);
            byte[] btwo = File.ReadAllBytes(two);
            return Enumerable.SequenceEqual(bone, btwo);
        }

        private static void WriteAllModels()
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
                foreach (Material material in model.Materials)
                {
                    GetPolygonAttrs(model, material, 1);
                }
            }
        }

        public static Matrix4x3 Concat43(Matrix4x3 first, Matrix4x3 second)
        {
            var output = Matrix4x3.Zero;
            output.M11 = first.M13 * second.M31 + first.M11 * second.M11 + first.M12 * second.M21;
            output.M12 = first.M13 * second.M32 + first.M11 * second.M12 + first.M12 * second.M22;
            output.M13 = first.M13 * second.M33 + first.M11 * second.M13 + first.M12 * second.M23;
            output.M21 = first.M23 * second.M31 + first.M21 * second.M11 + first.M22 * second.M21;
            output.M22 = first.M23 * second.M32 + first.M21 * second.M12 + first.M22 * second.M22;
            output.M23 = first.M23 * second.M33 + first.M21 * second.M13 + first.M22 * second.M23;
            output.M31 = first.M33 * second.M31 + first.M31 * second.M11 + first.M32 * second.M21;
            output.M32 = first.M33 * second.M32 + first.M31 * second.M12 + first.M32 * second.M22;
            output.M33 = first.M33 * second.M33 + first.M31 * second.M13 + first.M32 * second.M23;
            output.M41 = second.M41 + first.M43 * second.M31 + first.M41 * second.M11 + first.M42 * second.M21;
            output.M42 = second.M42 + first.M43 * second.M32 + first.M41 * second.M12 + first.M42 * second.M22;
            output.M43 = second.M43 + first.M43 * second.M33 + first.M41 * second.M13 + first.M42 * second.M23;
            return output;
        }

        public static void TestMatrix()
        {
            // 0x020DB528 (passed to draw_animated_model from CModel_draw from draw_player)
            // updated in sub_201DCE4 -- I guess it's just the model transform?
            Matrix4x3 mtx1 = Test.ParseMatrix48("03 F0 FF FF 00 00 00 00 9C 00 00 00 F9 FF FF FF FB 0F 00 00 3E FF FF FF 64 FF FF FF 3E FF FF FF 08 F0 FF FF 22 00 00 00 86 40 00 00 F1 AD FD FF");
            // 0x220DA430 (constant?)
            Matrix4x3 mtx2 = Test.ParseMatrix48("FD 0F 00 00 D3 FF FF FF 97 00 00 00 00 00 00 00 53 0F 00 00 9B 04 00 00 62 FF FF FF 66 FB FF FF 50 0F 00 00 F4 E8 FF FF DA 0B FF FF BF F8 01 00");
            // concatenation result
            Matrix4x3 currentTextureMatrix = Test.ParseMatrix48("FF EF FF FF 00 00 00 00 FE FF FF FF 00 00 00 00 86 0F 00 00 DF 03 00 00 01 00 00 00 DF 03 00 00 7A F0 FF FF 00 00 00 00 7F F4 FF FF CA D2 FF FF");
            Matrix4x3 mult = Concat43(mtx1, mtx2);

            var trans = new Matrix4(
                new Vector4(mtx1.Row0, 0.0f),
                new Vector4(mtx1.Row1, 0.0f),
                new Vector4(mtx1.Row2, 0.0f),
                new Vector4(mtx1.Row3, 1.0f)
            );
            Vector3 pos = trans.ExtractTranslation();
            Vector3 rot = trans.ExtractRotation().ToEulerAngles();
            rot = new Vector3(
                MathHelper.RadiansToDegrees(rot.X),
                MathHelper.RadiansToDegrees(rot.Y),
                MathHelper.RadiansToDegrees(rot.Z)
            );
            Vector3 scale = trans.ExtractScale();
        }

        public enum Hunter : byte
        {
            Samus = 0,
            Kanden = 1,
            Trace = 2,
            Sylux = 3,
            Noxus = 4,
            Spire = 5,
            Weavel = 6,
            Guardian = 7
        }

        public static void TestLogic()
        {
            Hunter hunter = 0;
            int flags = 0;
            int v45 = 0;

            if (hunter == Hunter.Noxus)
            {

            }
            else if (hunter > Hunter.Samus && hunter != Hunter.Spire)
            {
                if (hunter == Hunter.Kanden)
                {
                    /* call sub_202657C */
                }
                else // Trace, Sylux, Weavel, Guardian
                {

                }
            }
            else // Samus, Spire
            {
                // the "404 + 64" used in the vector setup seems to point to fx32 0.5
                if (hunter > Hunter.Samus || (flags & 0x80) > 0) // Spire OR colliding with platform
                {
                    /* v45 vector setup 1 */
                    v45 = 1;
                }
                else // Samus AND !(colliding with platform)
                {
                    /* v45 vector setup 2 */
                    v45 = 2;
                }
                if (v45 > 0)
                {
                    /* matrix setup */
                    if (hunter == Hunter.Samus)
                    {
                        /* 4F4 matrix concat */
                    }
                    /* 4F4 cross product and normalize */
                    if (hunter == Hunter.Spire)
                    {
                        /* 4F4 matrix multiplication */
                    }
                }
            }
        }

        public static Matrix4x3 ParseMatrix12(params string[] values)
        {
            if (values.Length != 12 || values.Any(v => v.Length != 8))
            {
                throw new ArgumentException(nameof(values));
            }
            return new Matrix4x3(
                Int32.Parse(values[0], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[1], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[2], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[3], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[4], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[5], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[6], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[7], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[8], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[9], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[10], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[11], System.Globalization.NumberStyles.HexNumber) / 4096f
            );
        }

        public static Matrix4 ParseMatrix16(params string[] values)
        {
            if (values.Length != 16 || values.Any(v => v.Length != 8))
            {
                throw new ArgumentException(nameof(values));
            }
            return new Matrix4(
                Int32.Parse(values[ 0], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 1], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 2], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 3], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 4], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 5], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 6], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 7], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 8], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[ 9], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[10], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[11], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[12], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[13], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[14], System.Globalization.NumberStyles.HexNumber) / 4096f,
                Int32.Parse(values[15], System.Globalization.NumberStyles.HexNumber) / 4096f
            );
        }

        public static Matrix4 ParseMatrix16(string value)
        {
            return ParseMatrix16(value.Split(' '));
        }

        public static Matrix4x3 ParseMatrix48(string value)
        {
            string[] values = value.Split(' ');
            if (values.Length != 48 || values.Any(v => v.Length != 2))
            {
                throw new ArgumentException(nameof(values));
            }
            return ParseMatrix12(
                values[3] + values[2] + values[1] + values[0],
                values[7] + values[6] + values[5] + values[4],
                values[11] + values[10] + values[9] + values[8],
                values[15] + values[14] + values[13] + values[12],
                values[19] + values[18] + values[17] + values[16],
                values[23] + values[22] + values[21] + values[20],
                values[27] + values[26] + values[25] + values[24],
                values[31] + values[30] + values[29] + values[28],
                values[35] + values[34] + values[33] + values[32],
                values[39] + values[38] + values[37] + values[36],
                values[43] + values[42] + values[41] + values[40],
                values[47] + values[46] + values[45] + values[44]
            );
        }

        public static Matrix4 ParseMatrix64(string value)
        {
            string[] values = value.Split(' ');
            if (values.Length != 64 || values.Any(v => v.Length != 2))
            {
                throw new ArgumentException(nameof(values));
            }
            return ParseMatrix16(
                values[ 3] + values[ 2] + values[ 1] + values[ 0],
                values[ 7] + values[ 6] + values[ 5] + values[ 4],
                values[11] + values[10] + values[ 9] + values[ 8],
                values[15] + values[14] + values[13] + values[12],
                values[19] + values[18] + values[17] + values[16],
                values[23] + values[22] + values[21] + values[20],
                values[27] + values[26] + values[25] + values[24],
                values[31] + values[30] + values[29] + values[28],
                values[35] + values[34] + values[33] + values[32],
                values[39] + values[38] + values[37] + values[36],
                values[43] + values[42] + values[41] + values[40],
                values[47] + values[46] + values[45] + values[44],
                values[51] + values[50] + values[49] + values[48],
                values[55] + values[54] + values[53] + values[52],
                values[59] + values[58] + values[57] + values[56],
                values[63] + values[62] + values[61] + values[60]
            );
        }

        public static void GetPolygonAttrs(Model model, int polygonId)
        {
            foreach (Material material in model.Materials)
            {
                GetPolygonAttrs(model, material, polygonId);
            }
        }

        public static void GetPolygonAttrs(Model model, Material material, int polygonId)
        {
            int v19 = polygonId == 1 ? 0x4000 : 0;
            int v20 = v19 | 0x8000;
            int attr = v20 | material.Lighting | 16 * (int)material.PolygonMode
                | ((int)material.Culling << 6) | (polygonId << 24) | (material.Alpha << 16);
            Console.WriteLine($"{model.Name} - {material.Name}");
            Console.WriteLine($"light = {material.Lighting}, mode = {(int)material.PolygonMode} ({material.PolygonMode}), " +
                $"cull = {(int)material.Culling} ({material.Culling}), alpha = {material.Alpha}, id = {polygonId}");
            DumpPolygonAttr((uint)attr);
        }

        public static void DumpPolygonAttr(uint attr)
        {
            Console.WriteLine($"0x{attr:X2}");
            string bits = Convert.ToString(attr, 2);
            Console.WriteLine(bits);
            Console.WriteLine($"light1: {attr & 0x1}");
            Console.WriteLine($"light2: {(attr >> 1) & 0x1}");
            Console.WriteLine($"light3: {(attr >> 2) & 0x1}");
            Console.WriteLine($"light4: {(attr >> 3) & 0x1}");
            Console.WriteLine($"mode: {(attr >> 4) & 0x2}");
            Console.WriteLine($"back: {(attr >> 6) & 0x1}");
            Console.WriteLine($"front: {(attr >> 7) & 0x1}");
            Console.WriteLine($"clear: {(attr >> 11) & 0x1}");
            Console.WriteLine($"far: {(attr >> 12) & 0x1}");
            Console.WriteLine($"1dot: {(attr >> 13) & 0x1}");
            Console.WriteLine($"depth: {(attr >> 14) & 0x1}");
            Console.WriteLine($"fog: {(attr >> 15) & 0x1}");
            Console.WriteLine($"alpha: {(attr >> 16) & 0x1F}");
            Console.WriteLine($"id: {(attr >> 24) & 0x3F}");
            Console.WriteLine();
        }

        public static void TestAllLayers()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                SceneSetup.LoadRoom(meta.Key);
            }
        }

        public static void TestAllNodes()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                Model room = Read.GetRoomByName(meta.Key);
                Console.WriteLine(meta.Key);
                for (int i = 0; i < room.Nodes.Count; i++)
                {
                    Node node = room.Nodes[i];
                }
                Console.WriteLine();
            }
            Nop();
        }

        public static void TestAllEntities()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.LightSource)
                        {
                            LightSourceEntityData data = ((Entity<LightSourceEntityData>)entity).Data;
                            if (data.VolumeType == VolumeType.Cylinder)
                            {
                                Console.WriteLine(data.Volume.CylinderVector.ToFloatVector());
                            }
                        }
                    }
                }
            }
            Nop();
        }

        public static void LightColor(uint arg)
        {
            uint r = arg & 0x1F;
            uint g = (arg >> 5) & 0x1F;
            uint b = (arg >> 10) & 0x1F;
            int light = (arg & 0x40000000) == 0 ? 0 : 1;
            Console.WriteLine($"light: {light} R {r}, G {g}, B {b}");
            //Console.WriteLine($"light: {light} R 0x{r:X2}, G 0x{g:X2}, B 0x{b:X2}");
            Console.WriteLine();
        }

        private static IEnumerable<Model> GetAllModels()
        {
            foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.ModelMetadata)
            {
                yield return Read.GetModelByName(meta.Key);
            }
            foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.FirstHuntModels)
            {
                yield return Read.GetModelByName(meta.Key, firstHunt: true);
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
