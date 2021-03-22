using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Mathematics;

namespace MphRead.Testing
{
    public class TestPrint
    {
        public static void PrintStruct(string name, int size)
        {
            Debug.Assert(size > 0 && size % 4 == 0);
            Console.WriteLine($"struct {name}");
            Console.WriteLine("{");
            int offset = 0;
            while (offset < size)
            {
                Console.WriteLine($"  int field_{offset:X1};");
                offset += 4;
            }
            Console.WriteLine("}");
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
            Debug.Assert(polygonId >= 0);
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

        public static Vector3 LightCalc(Vector3 light_vec, Vector3 light_col, Vector3 normal_vec,
            Vector3 dif_col, Vector3 amb_col, Vector3 spe_col)
        {
            var sight_vec = new Vector3(0.0f, 0.0f, -1.0f);
            float dif_factor = Math.Max(0.0f, -Vector3.Dot(light_vec, normal_vec));
            Vector3 half_vec = (light_vec + sight_vec) / 2.0f;
            float spe_factor = Math.Max(0.0f, Vector3.Dot(-half_vec, normal_vec));
            spe_factor *= spe_factor;
            Vector3 spe_out = spe_col * light_col * spe_factor;
            Vector3 dif_out = dif_col * light_col * dif_factor;
            Vector3 amb_out = amb_col * light_col;
            return spe_out + dif_out + amb_out;
        }

        public static void PrintEntityEditor(Type type)
        {
            string name = type.Name;
            string rawName = type.Name.Replace("Editor", "Data");
            Console.WriteLine($"public {name}(Entity header, {rawName} raw) : base(header)");
            Console.WriteLine("        {");
            foreach (System.Reflection.PropertyInfo prop in type.GetProperties())
            {
                string propName = prop.Name;
                string srcName = prop.Name;
                if (prop.PropertyType == typeof(bool))
                {
                    Console.WriteLine($"            {propName} = raw.{srcName} != 0;");
                }
                else if (prop.PropertyType == typeof(CollisionVolume))
                {
                    Console.WriteLine($"            {propName} = new CollisionVolume(raw.{srcName});");
                }
                else
                {
                    string prefix = "";
                    string suffix = "";
                    if (prop.PropertyType == typeof(Vector3))
                    {
                        suffix = ".ToFloatVector()";
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        suffix = ".MarshalString()";
                    }
                    if (prop.Name == "Id" || prop.Name == "Type" || prop.Name == "LayerMask" || prop.Name == "NodeName"
                         || prop.Name == "Position" || prop.Name == "Up" || prop.Name == "Facing")
                    {
                        continue;
                    }
                    Console.WriteLine($"            {propName} = {prefix}raw.{srcName}{suffix};");
                }
            }
            Console.WriteLine("        }");
            Nop();
        }

        public static void ParseStruct(string className, bool entity, string data)
        {
            if (String.IsNullOrWhiteSpace(data))
            {
                return;
            }
            int index = 0;
            int offset = entity ? 0x18 : 0;
            var byteEnums = new Dictionary<string, string>()
            {
                { "ENEMY_TYPE", "EnemyType" },
                { "HUNTER", "Hunter" }
            };
            var ushortEnums = new Dictionary<string, string>()
            {
                { "ITEM_TYPE", "ItemType" }
            };
            var uintEnums = new Dictionary<string, string>()
            {
                { "EVENT_TYPE", "Message" },
                { "DOOR_TYPE", "DoorType" },
                { "COLLISION_VOLUME_TYPE", "VolumeType" }
            };
            if (entity)
            {
                Console.WriteLine($"public class {className} : CEntity");
            }
            else
            {
                Console.WriteLine($"public class {className} : MemoryClass");
            }
            Console.WriteLine("    {");
            var news = new List<string>();
            foreach (string line in data.Split(Environment.NewLine))
            {
                string[] split = line.Trim().Replace("signed ", "signed").Replace(" *", "* ").Replace(";", "").Split(' ');
                Debug.Assert(split.Length == 2);
                string name = "";
                foreach (string part in split[1].Split('_'))
                {
                    name += part[0].ToString().ToUpperInvariant() + part[1..];
                }
                string comment = "";
                string type;
                string getter;
                string setter;
                int size;
                bool enums = false;
                bool embed = false;
                string cast = "";
                if (split[0].Contains("*"))
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // {split[0]}";
                }
                else if (split[0] == "EntityPtrUnion")
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // CEntity*";
                }
                else if (split[0] == "EntityIdOrRef")
                {
                    type = "IntPtr";
                    getter = "ReadPointer";
                    setter = "WritePointer";
                    size = 4;
                    comment = $" // EntityIdOrRef";
                }
                else if (split[0] == "int")
                {
                    type = "int";
                    getter = "ReadInt32";
                    setter = "WriteInt32";
                    size = 4;
                }
                else if (split[0] == "unsignedint")
                {
                    type = "uint";
                    getter = "ReadUInt32";
                    setter = "WriteUInt32";
                    size = 4;
                }
                else if (split[0] == "signed__int16")
                {
                    type = "short";
                    getter = "ReadInt16";
                    setter = "WriteInt16";
                    size = 2;
                }
                else if (split[0] == "__int16" || split[0] == "unsigned__int16")
                {
                    type = "ushort";
                    getter = "ReadUInt16";
                    setter = "WriteUInt16";
                    size = 2;
                }
                else if (split[0] == "char" || split[0] == "__int8" || split[0] == "unsigned__int8")
                {
                    type = "byte";
                    getter = "ReadByte";
                    setter = "WriteByte";
                    size = 1;
                }
                else if (split[0] == "signed__int8")
                {
                    type = "sbyte";
                    getter = "ReadSByte";
                    setter = "WriteSByte";
                    size = 1;
                }
                else if (split[0] == "Color3")
                {
                    type = "ColorRgb";
                    getter = "ReadColor3";
                    setter = "WriteColor3";
                    size = 3;
                }
                else if (split[0] == "VecFx32")
                {
                    type = "Vector3";
                    getter = "ReadVec3";
                    setter = "WriteVec3";
                    size = 12;
                }
                else if (split[0] == "Vec4")
                {
                    type = "Vector4";
                    getter = "ReadVec4";
                    setter = "WriteVec4";
                    size = 16;
                }
                else if (split[0] == "MtxFx43")
                {
                    type = "Matrix4x3";
                    getter = "ReadMtx43";
                    setter = "WriteMtx43";
                    size = 48;
                }
                else if (split[0] == "RoomState")
                {
                    type = "RoomState";
                    getter = "";
                    setter = "";
                    size = 60;
                    embed = true;
                }
                else if (split[0] == "CModel")
                {
                    type = "CModel";
                    getter = "";
                    setter = "";
                    size = 0x48;
                    embed = true;
                }
                else if (split[0] == "BeamInfo")
                {
                    type = "BeamInfo";
                    getter = "";
                    setter = "";
                    size = 0x14;
                    embed = true;
                }
                else if (split[0] == "EntityCollision")
                {
                    type = "EntityCollision";
                    getter = "";
                    setter = "";
                    size = 0xB4;
                    embed = true;
                }
                else if (split[0] == "SmallSfxStruct")
                {
                    type = "SmallSfxStruct";
                    getter = "";
                    setter = "";
                    size = 4;
                    embed = true;
                }
                else if (split[0] == "CollisionVolume")
                {
                    type = "CollisionVolume";
                    getter = "";
                    setter = "";
                    size = 0x40;
                    embed = true;
                }
                else if (split[0] == "Light")
                {
                    type = "Light";
                    getter = "";
                    setter = "";
                    size = 0xF;
                    embed = true;
                }
                else if (split[0] == "LightInfo")
                {
                    type = "LightInfo";
                    getter = "";
                    setter = "";
                    size = 0x1F;
                    embed = true;
                }
                else if (split[0] == "CameraInfo")
                {
                    type = "CameraInfo";
                    getter = "";
                    setter = "";
                    size = 0x11C;
                    embed = true;
                }
                else if (split[0] == "PlayerControlsMaybe")
                {
                    type = "PlayerControls";
                    getter = "";
                    setter = "";
                    size = 0x9C;
                    embed = true;
                }
                else if (split[0] == "PlayerInputProbably")
                {
                    type = "PlayerInput";
                    getter = "";
                    setter = "";
                    size = 0x48;
                    embed = true;
                }
                else if (split[0] == "CBeamProjectile")
                {
                    type = "CBeamProjectile";
                    getter = "";
                    setter = "";
                    size = 0x158;
                    embed = true;
                }
                else if (byteEnums.TryGetValue(split[0], out string? value))
                {
                    type = value;
                    getter = "ReadByte";
                    setter = "WriteByte";
                    size = 1;
                    enums = true;
                    cast = "byte";
                }
                else if (ushortEnums.TryGetValue(split[0], out value))
                {
                    type = value;
                    getter = "ReadUInt16";
                    setter = "WriteUInt16";
                    size = 2;
                    enums = true;
                    cast = "ushort";
                }
                else if (uintEnums.TryGetValue(split[0], out value))
                {
                    type = value;
                    getter = "ReadUInt32";
                    setter = "WriteUInt32";
                    size = 4;
                    enums = true;
                    cast = "uint";
                }
                else
                {
                    type = split[0];
                    getter = "Read";
                    setter = "Write";
                    size = 4;
                    embed = true;
                    Debugger.Break();
                }
                int number = 0;
                bool array = name.Contains('[');
                string param = "";
                if (array)
                {
                    split = name.Split('[');
                    number = Int32.Parse(split[1].Split(']')[0]);
                    Debug.Assert(number > 1);
                    name = split[0];
                    if (comment == "")
                    {
                        comment = $" // {type}";
                    }
                    comment += $"[{number}]";
                    if (embed)
                    {
                        param = $",{Environment.NewLine}                {size}, (Memory m, int a) => new {type}(m, a)";
                        type = $"StructArray<{type}>";
                    }
                    else if (enums && size == 1)
                    {
                        type = $"U8EnumArray<{type}>";
                    }
                    else if (enums && size == 2)
                    {
                        type = $"U16EnumArray<{type}>";
                    }
                    else if (enums && size == 4)
                    {
                        type = $"U32EnumArray<{type}>";
                    }
                    else
                    {
                        type = getter.Replace("Read", "").Replace("Pointer", "IntPtr") + "Array";
                    }
                    size *= number;
                }
                Console.WriteLine($"        private const int _off{index} = 0x{offset:X1};{comment}");
                if (array)
                {
                    Console.WriteLine($"        public {type} {name} {{ get; }}");
                    news.Add($"            {name} = new {type}(memory, address + _off{index}, {number}{param});");
                }
                else if (embed)
                {
                    Console.WriteLine($"        public {type} {name} {{ get; }}");
                    news.Add($"            {name} = new {type}(memory, address + _off{index});");
                }
                else if (enums)
                {
                    Console.WriteLine($"        public {type} {name} {{ get => ({type}){getter}(_off{index}); " +
                        $"set => {setter}(_off{index}, ({cast})value); }}");
                }
                else
                {
                    Console.WriteLine($"        public {type} {name} {{ get => {getter}(_off{index}); " +
                        $"set => {setter}(_off{index}, value); }}");
                }
                Console.WriteLine();
                index++;
                offset += size;
            }
            Console.WriteLine($"        public {className}(Memory memory, int address) : base(memory, address)");
            Console.WriteLine("        {");
            foreach (string line in news)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine("        }");
            Console.WriteLine();
            Console.WriteLine($"        public {className}(Memory memory, IntPtr address) : base(memory, address)");
            Console.WriteLine("        {");
            foreach (string line in news)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine("        }");
            Console.WriteLine("    }");
            Debugger.Break();
        }

        private static void Nop()
        {
        }
    }
}
