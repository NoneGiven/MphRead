using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace MphRead
{
    public static class Test
    {
        public static void ParseAllModels()
        {
            GetAllModels().ToList();
        }

        public static void TestAllModels()
        {
            foreach (Model model in GetAllModels())
            {
            }
            Nop();
        }

        public static void TestModelFiles()
        {
            var paths = new List<string>();
            paths.AddRange(Directory.EnumerateFiles(Path.Combine(Paths.FileSystem, "models")).Where(f => f.EndsWith("odel.bin")));
            paths.AddRange(Directory.EnumerateFiles(Path.Combine(Paths.FileSystem, "_archives"), "", SearchOption.AllDirectories)
                .Where(f => f.EndsWith("odel.bin")));
            var headers = new Dictionary<string, Header>();
            foreach (string path in paths)
            {
                var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
                headers.Add(Path.GetFileName(path), Read.ReadStruct<Header>(bytes));
            }
            foreach (KeyValuePair<string, Header> kvp in headers)
            {
                string name = kvp.Key;
                Header h = kvp.Value;
            }
            Nop();
        }

        public static void TestAllRooms()
        {
            var ignore = new HashSet<int>() { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 23, 26 };
            for (int i = 0; i < 122; i++)
            {
                if (!ignore.Contains(i))
                {
                    RoomMetadata meta = Metadata.GetRoomById(i)!;
                }
            }
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
                Model room = Read.GetRoomModelInstance(meta.Key).Model;
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
                if (meta.Value.EntityPath != null && !meta.Value.FirstHunt)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.Platform)
                        {
                            PlatformEntityData data = ((Entity<PlatformEntityData>)entity).Data;
                        }
                    }
                }
            }
            Nop();
        }

        public static void TestAllEntityMessages()
        {
            var used = new HashSet<Message>();
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null && !meta.Value.FirstHunt)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.Platform)
                        {
                            PlatformEntityData data = ((Entity<PlatformEntityData>)entity).Data;
                            used.Add(data.ScanMessage);
                            used.Add(data.LifetimeMessage1);
                            used.Add(data.LifetimeMessage2);
                            used.Add(data.LifetimeMessage3);
                            used.Add(data.LifetimeMessage4);
                            used.Add(data.BeamHitMessage);
                            used.Add(data.DeadMessage);
                            used.Add(data.PlayerColMessage);
                        }
                        else if (entity.Type == EntityType.Object)
                        {
                            ObjectEntityData data = ((Entity<ObjectEntityData>)entity).Data;
                            used.Add(data.ScanMessage);
                        }
                        else if (entity.Type == EntityType.Artifact)
                        {
                            ArtifactEntityData data = ((Entity<ArtifactEntityData>)entity).Data;
                            used.Add(data.Message1);
                            used.Add(data.Message2);
                            used.Add(data.Message3);
                        }
                        else if (entity.Type == EntityType.EnemySpawn)
                        {
                            EnemySpawnEntityData data = ((Entity<EnemySpawnEntityData>)entity).Data;
                            used.Add(data.Message1);
                            used.Add(data.Message2);
                            used.Add(data.Message3);
                            if (data.EnemyType == EnemyType.Hunter && data.Fields.S09.EncounterType != 0)
                            {
                                Console.WriteLine($"EH {meta.Value.InGameName} {(Hunter)data.Fields.S09.HunterId}" +
                                    $" type {data.Fields.S09.EncounterType}");
                            }
                        }
                        else if (entity.Type == EntityType.ItemSpawn)
                        {
                            ItemSpawnEntityData data = ((Entity<ItemSpawnEntityData>)entity).Data;
                            used.Add(data.CollectedMessage);
                        }
                        else if (entity.Type == EntityType.CameraSequence)
                        {
                            CameraSequenceEntityData data = ((Entity<CameraSequenceEntityData>)entity).Data;
                            used.Add(data.Message);
                        }
                        else if (entity.Type == EntityType.AreaVolume)
                        {
                            AreaVolumeEntityData data = ((Entity<AreaVolumeEntityData>)entity).Data;
                            used.Add(data.InsideMessage);
                            used.Add(data.ExitMessage);
                        }
                        else if (entity.Type == EntityType.TriggerVolume)
                        {
                            TriggerVolumeEntityData data = ((Entity<TriggerVolumeEntityData>)entity).Data;
                            used.Add(data.ParentMessage);
                            used.Add(data.ChildMessage);
                        }
                    }
                }
            }
            for (int i = 0; i < Formats.CameraSequence.Filenames.Count; i++)
            {
                if (i != 8 && i != 66 && i != 69 && i != 84 && i != 106 && i != 122 && i != 123)
                {
                    var seq = Formats.CameraSequence.Load(i);
                    foreach (CameraSequenceKeyframe frame in seq.Keyframes)
                    {
                        used.Add((Message)frame.MessageId);
                    }
                }
            }
            //Console.WriteLine("Used:");
            //foreach (Message message in used.OrderBy(m => m))
            //{
            //    Console.WriteLine(message);
            //}
            //Console.WriteLine();
            //Console.WriteLine("Unused:");
            //for (int i = 0; i <= 61; i++)
            //{
            //    var message = (Message)i;
            //    if (!used.Contains(message))
            //    {
            //        Console.WriteLine(message);
            //    }
            //}
            Nop();
        }

        public static void TestTriggerVolumes()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.TriggerVolume)
                        {
                            TriggerVolumeEntityData data = ((Entity<TriggerVolumeEntityData>)entity).Data;
                        }
                    }
                }
            }
            Nop();
        }

        public static void TestAreaVolumes()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.AreaVolume)
                        {
                            AreaVolumeEntityData data = ((Entity<AreaVolumeEntityData>)entity).Data;
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
                yield return Read.GetModelInstance(meta.Key).Model;
            }
            foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.FirstHuntModels)
            {
                yield return Read.GetModelInstance(meta.Key, firstHunt: true).Model;
            }
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                yield return Read.GetRoomModelInstance(meta.Key).Model;
            }
        }

        private static IEnumerable<Model> GetAllRooms()
        {
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                yield return Read.GetRoomModelInstance(meta.Key).Model;
            }
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

        public static void TestDlistBounds()
        {
            foreach (Model model in GetAllModels())
            {
                if (model.NodeMatrixIds.Count > 0)
                {
                    continue;
                }
                for (int i = 0; i < model.DisplayLists.Count; i++)
                {
                    var verts = new List<Vector4i>();
                    DisplayList dlist = model.DisplayLists[i];
                    IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[i];
                    int stackIndex = 0;
                    int vtxX = 0;
                    int vtxY = 0;
                    int vtxZ = 0;
                    void Update()
                    {
                        verts.Add(new Vector4i(vtxX, vtxY, vtxZ, stackIndex));
                    }
                    foreach (RenderInstruction instruction in list)
                    {
                        switch (instruction.Code)
                        {
                        case InstructionCode.MTX_RESTORE:
                            {
                                stackIndex = (int)instruction.Arguments[0];
                            }
                            break;
                        case InstructionCode.VTX_16:
                            {
                                uint xy = instruction.Arguments[0];
                                int x = (int)((xy >> 0) & 0xFFFF);
                                if ((x & 0x8000) > 0)
                                {
                                    x = (int)(x | 0xFFFF0000);
                                }
                                int y = (int)((xy >> 16) & 0xFFFF);
                                if ((y & 0x8000) > 0)
                                {
                                    y = (int)(y | 0xFFFF0000);
                                }
                                int z = (int)(instruction.Arguments[1] & 0xFFFF);
                                if ((z & 0x8000) > 0)
                                {
                                    z = (int)(z | 0xFFFF0000);
                                }
                                vtxX = x;
                                vtxY = y;
                                vtxZ = z;
                                Update();
                            }
                            break;
                        case InstructionCode.VTX_10:
                            {
                                uint xyz = instruction.Arguments[0];
                                int x = (int)((xyz >> 0) & 0x3FF);
                                if ((x & 0x200) > 0)
                                {
                                    x = (int)(x | 0xFFFFFC00);
                                }
                                int y = (int)((xyz >> 10) & 0x3FF);
                                if ((y & 0x200) > 0)
                                {
                                    y = (int)(y | 0xFFFFFC00);
                                }
                                int z = (int)((xyz >> 20) & 0x3FF);
                                if ((z & 0x200) > 0)
                                {
                                    z = (int)(z | 0xFFFFFC00);
                                }
                                vtxX = x << 6;
                                vtxY = y << 6;
                                vtxZ = z << 6;
                                Update();
                            }
                            break;
                        case InstructionCode.VTX_XY:
                            {
                                uint xy = instruction.Arguments[0];
                                int x = (int)((xy >> 0) & 0xFFFF);
                                if ((x & 0x8000) > 0)
                                {
                                    x = (int)(x | 0xFFFF0000);
                                }
                                int y = (int)((xy >> 16) & 0xFFFF);
                                if ((y & 0x8000) > 0)
                                {
                                    y = (int)(y | 0xFFFF0000);
                                }
                                vtxX = x;
                                vtxY = y;
                                Update();
                            }
                            break;
                        case InstructionCode.VTX_XZ:
                            {
                                uint xz = instruction.Arguments[0];
                                int x = (int)((xz >> 0) & 0xFFFF);
                                if ((x & 0x8000) > 0)
                                {
                                    x = (int)(x | 0xFFFF0000);
                                }
                                int z = (int)((xz >> 16) & 0xFFFF);
                                if ((z & 0x8000) > 0)
                                {
                                    z = (int)(z | 0xFFFF0000);
                                }
                                vtxX = x;
                                vtxZ = z;
                                Update();
                            }
                            break;
                        case InstructionCode.VTX_YZ:
                            {
                                uint yz = instruction.Arguments[0];
                                int y = (int)((yz >> 0) & 0xFFFF);
                                if ((y & 0x8000) > 0)
                                {
                                    y = (int)(y | 0xFFFF0000);
                                }
                                int z = (int)((yz >> 16) & 0xFFFF);
                                if ((z & 0x8000) > 0)
                                {
                                    z = (int)(z | 0xFFFF0000);
                                }
                                vtxY = y;
                                vtxZ = z;
                                Update();
                            }
                            break;
                        case InstructionCode.VTX_DIFF:
                            {
                                uint xyz = instruction.Arguments[0];
                                int x = (int)((xyz >> 0) & 0x3FF);
                                if ((x & 0x200) > 0)
                                {
                                    x = (int)(x | 0xFFFFFC00);
                                }
                                int y = (int)((xyz >> 10) & 0x3FF);
                                if ((y & 0x200) > 0)
                                {
                                    y = (int)(y | 0xFFFFFC00);
                                }
                                int z = (int)((xyz >> 20) & 0x3FF);
                                if ((z & 0x200) > 0)
                                {
                                    z = (int)(z | 0xFFFFFC00);
                                }
                                vtxX += x;
                                vtxY += y;
                                vtxZ += z;
                                Update();
                            }
                            break;
                        }
                    }
                    var dlistMin = new Vector3i(
                        dlist.MinBounds.X.Value,
                        dlist.MinBounds.Y.Value,
                        dlist.MinBounds.Z.Value
                    );
                    var dlistMax = new Vector3i(
                        dlist.MaxBounds.X.Value,
                        dlist.MaxBounds.Y.Value,
                        dlist.MaxBounds.Z.Value
                    );
                    int minX = Int32.MaxValue;
                    int maxX = Int32.MinValue;
                    int minY = Int32.MaxValue;
                    int maxY = Int32.MinValue;
                    int minZ = Int32.MaxValue;
                    int maxZ = Int32.MinValue;
                    foreach (Vector4i vert in verts)
                    {
                        minX = Math.Min(minX, vert.X);
                        maxX = Math.Max(maxX, vert.X);
                        minY = Math.Min(minY, vert.Y);
                        maxY = Math.Max(maxY, vert.Y);
                        minZ = Math.Min(minZ, vert.Z);
                        maxZ = Math.Max(maxZ, vert.Z);
                    }
                    int scale = (int)model.Scale.X;
                    if (model.NodeMatrixIds.Count == 0 && model.Name != "Level MP5")
                    {
                        minX *= scale;
                        maxX *= scale;
                        minY *= scale;
                        maxY *= scale;
                        minZ *= scale;
                        maxZ *= scale;
                    }
                    Debug.Assert(model.Scale.X - scale == 0);
                    // off-by-one gets magnified by scale
                    if (minX != dlistMin.X && Math.Abs(minX - dlistMin.X) != scale)
                    {
                        Debugger.Break();
                    }
                    if (maxX != dlistMax.X && Math.Abs(maxX - dlistMax.X) != scale)
                    {
                        Debugger.Break();
                    }
                    if (minY != dlistMin.Y && Math.Abs(minY - dlistMin.Y) != scale)
                    {
                        Debugger.Break();
                    }
                    if (maxY != dlistMax.Y && Math.Abs(maxY - dlistMax.Y) != scale)
                    {
                        Debugger.Break();
                    }
                    if (minZ != dlistMin.Z && Math.Abs(minZ - dlistMin.Z) != scale)
                    {
                        Debugger.Break();
                    }
                    if (maxZ != dlistMax.Z && Math.Abs(maxZ - dlistMax.Z) != scale)
                    {
                        Debugger.Break();
                    }
                }
            }
            Nop();
        }

        public static void TestNodeBounds()
        {
            foreach (Model model in GetAllModels())
            {
                if (model.Name == "Level MP5")
                {
                    // scale factor of 4 is applied to the node bounds but not the dlists
                    // --> and then on top of that, there are some precision issues +/- 1 fx32
                    continue;
                }
                bool uncapped = model.Name == "filter" || model.Name == "trail";
                for (int i = 0; i < model.Nodes.Count; i++)
                {
                    Node node = model.Nodes[i];
                    RawNode rawNode = model.RawNodes[i];
                    int minX = Int32.MaxValue;
                    int minY = Int32.MaxValue;
                    int minZ = Int32.MaxValue;
                    int maxX = Int32.MinValue;
                    int maxY = Int32.MinValue;
                    int maxZ = Int32.MinValue;
                    bool anyMesh = false;
                    List<int> ids;
                    if (node.MeshCount == 0)
                    {
                        ids = node.GetAllMeshIds(model.Nodes, root: true).ToList();
                    }
                    else
                    {
                        ids = node.GetMeshIds().ToList();
                    }
                    var dlists = new List<int>();
                    foreach (int meshId in ids)
                    {
                        anyMesh = true;
                        dlists.Add(model.Meshes[meshId].DlistId);
                        DisplayList dlist = model.DisplayLists[model.Meshes[meshId].DlistId];
                        minX = Math.Min(minX, dlist.MinBounds.X.Value);
                        minY = Math.Min(minY, dlist.MinBounds.Y.Value);
                        minZ = Math.Min(minZ, dlist.MinBounds.Z.Value);
                        maxX = Math.Max(maxX, dlist.MaxBounds.X.Value);
                        maxY = Math.Max(maxY, dlist.MaxBounds.Y.Value);
                        maxZ = Math.Max(maxZ, dlist.MaxBounds.Z.Value);
                    }
                    //Console.WriteLine($"[{i}] {node.Name} x{node.MeshCount}: {String.Join(", ", dlists.OrderBy(d => d))}");
                    var nodeMin = new Vector3i(
                        rawNode.MinBounds.X.Value,
                        rawNode.MinBounds.Y.Value,
                        rawNode.MinBounds.Z.Value
                    );
                    var nodeMax = new Vector3i(
                        rawNode.MaxBounds.X.Value,
                        rawNode.MaxBounds.Y.Value,
                        rawNode.MaxBounds.Z.Value
                    );
                    if (anyMesh)
                    {
                        if (minX > Int16.MaxValue * model.Scale.X || minX < Int16.MinValue * model.Scale.X)
                        {
                            Debugger.Break();
                        }
                        if (minY > Int16.MaxValue * model.Scale.Y || minY < Int16.MinValue * model.Scale.Y)
                        {
                            Debugger.Break();
                        }
                        if (minZ > Int16.MaxValue * model.Scale.Z || minZ < Int16.MinValue * model.Scale.Z)
                        {
                            Debugger.Break();
                        }
                        if (maxX > Int16.MaxValue * model.Scale.X || maxX < Int16.MinValue * model.Scale.X)
                        {
                            Debugger.Break();
                        }
                        if (maxY > Int16.MaxValue * model.Scale.Y || maxY < Int16.MinValue * model.Scale.Y)
                        {
                            Debugger.Break();
                        }
                        if (maxZ > Int16.MaxValue * model.Scale.Z || maxZ < Int16.MinValue * model.Scale.Z)
                        {
                            Debugger.Break();
                        }
                        if (minX != nodeMin.X)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(minX == nodeMin.X / model.Scale.X);
                            }
                            else if (nodeMin.X > Int16.MaxValue * model.Scale.X)
                            {
                                Debug.Assert(minX == Int16.MaxValue * model.Scale.X);
                            }
                            else if (nodeMin.X < Int16.MinValue * model.Scale.X)
                            {
                                Debug.Assert(minX == Int16.MinValue * model.Scale.X);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                        if (minY != nodeMin.Y)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(minY == nodeMin.Y / model.Scale.Y);
                            }
                            else if (nodeMin.Y > Int16.MaxValue * model.Scale.Y)
                            {
                                Debug.Assert(minY == Int16.MaxValue * model.Scale.Y);
                            }
                            else if (nodeMin.Y < Int16.MinValue * model.Scale.Y)
                            {
                                Debug.Assert(minY == Int16.MinValue * model.Scale.Y);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                        if (minZ != nodeMin.Z)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(minZ == nodeMin.Z / model.Scale.Z);
                            }
                            else if (nodeMin.Z > Int16.MaxValue * model.Scale.Z)
                            {
                                Debug.Assert(minZ == Int16.MaxValue * model.Scale.Z);
                            }
                            else if (nodeMin.Z < Int16.MinValue * model.Scale.Z)
                            {
                                Debug.Assert(minZ == Int16.MinValue * model.Scale.Z);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                        if (maxX != nodeMax.X)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(maxX == nodeMax.X / model.Scale.X);
                            }
                            else if (nodeMax.X > Int16.MaxValue * model.Scale.X)
                            {
                                Debug.Assert(maxX == Int16.MaxValue * model.Scale.X);
                            }
                            else if (nodeMax.X < Int16.MinValue * model.Scale.X)
                            {
                                Debug.Assert(maxX == Int16.MinValue * model.Scale.X);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                        if (maxY != nodeMax.Y)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(maxY == nodeMax.Y / model.Scale.Y);
                            }
                            else if (nodeMax.Y > Int16.MaxValue * model.Scale.Y)
                            {
                                Debug.Assert(maxY == Int16.MaxValue * model.Scale.Y);
                            }
                            else if (nodeMax.Y < Int16.MinValue * model.Scale.Y)
                            {
                                Debug.Assert(maxY == Int16.MinValue * model.Scale.Y);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                        if (maxZ != nodeMax.Z)
                        {
                            if (uncapped)
                            {
                                Debug.Assert(maxZ == nodeMax.Z / model.Scale.Z);
                            }
                            else if (nodeMax.Z > Int16.MaxValue * model.Scale.Z)
                            {
                                Debug.Assert(maxZ == Int16.MaxValue * model.Scale.Z);
                            }
                            else if (nodeMax.Z < Int16.MinValue * model.Scale.Z)
                            {
                                Debug.Assert(maxZ == Int16.MinValue * model.Scale.Z);
                            }
                            else
                            {
                                Debugger.Break();
                            }
                        }
                    }
                    else
                    {
                        Debug.Assert(rawNode.MinBounds.X.Value == 0 && rawNode.MinBounds.Y.Value == 0 && rawNode.MinBounds.Z.Value == 0);
                        Debug.Assert(rawNode.MaxBounds.X.Value == 0 && rawNode.MaxBounds.Y.Value == 0 && rawNode.MaxBounds.Z.Value == 0);
                    }
                }
            }
            Nop();
        }

        private static void Nop()
        {
        }
    }
}
