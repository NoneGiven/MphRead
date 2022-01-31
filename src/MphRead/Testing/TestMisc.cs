using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Formats.Collision;
using MphRead.Utility;

namespace MphRead.Testing
{
    public static class TestMisc
    {
        //public static int GetSfxIndex(string query)
        //{
        //    IReadOnlyList<SoundSample> samples = SoundRead.ReadSoundSamples();
        //    string[] split = query.Split(", ");
        //    var num = split.Select(s => s.StartsWith("0x") ? UInt32.Parse(s.Replace("0x", ""), NumberStyles.HexNumber) : UInt32.Parse(s)).ToList();
        //    var results = samples.Where(s => s.Header.Field0 == num[0] && s.Header.Field4 == num[1]
        //        && s.Header.Field6 == num[2] && s.Header.Field8 == num[3] && s.Header.FieldA == num[4]).ToList();
        //    if (results.Count != 1)
        //    {
        //        Debugger.Break();
        //    }
        //    return samples.IndexOf(s => s == results[0]);
        //}

        public static void TestCameraSequences()
        {
            var ids = new HashSet<int>();
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.EntityPath != null)
                {
                    IReadOnlyList<Entity> entities = Read.GetEntities(meta.Value.EntityPath, -1, meta.Value.FirstHunt);
                    foreach (Entity entity in entities)
                    {
                        if (entity.Type == EntityType.CameraSequence)
                        {
                            CameraSequenceEntityData data = ((Entity<CameraSequenceEntityData>)entity).Data;
                            var entityClass = new CamSeqEntity(data, scene: null!);
                            if (ids.Contains(data.SequenceId))
                            {
                                continue;
                            }
                            ids.Add(data.SequenceId);
                            foreach (CameraSequenceKeyframe frame in entityClass.Sequence.Keyframes)
                            {
                            }
                            Nop();
                        }
                    }
                }
            }
            Nop();
        }

        public static void TestCameraSequenceFiles()
        {
            foreach (string filePath in Directory.EnumerateFiles(Path.Combine(Paths.FileSystem, "cameraEditor")))
            {
                string name = Path.GetFileName(filePath);
                if (name != "cameraEditBG.bin")
                {
                    var seq = CameraSequence.Load(name, scene: null!);
                    Nop();
                }
            }
            Nop();
        }

        public static void TestAllCollision()
        {
            var allCollision = new List<(bool, CollisionInstance)>();
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (!meta.Value.Hybrid)
                {
                    allCollision.Add((true, Collision.GetCollision(meta.Value)));
                }
            }
            foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.ModelMetadata)
            {
                if (meta.Value.CollisionPath != null)
                {
                    allCollision.Add((false, Collision.GetCollision(meta.Value)));
                    if (meta.Value.ExtraCollisionPath != null)
                    {
                        allCollision.Add((false, Collision.GetCollision(meta.Value, extra: true)));
                    }
                }
            }
            foreach ((bool room, CollisionInstance instance) in allCollision)
            {
                if (instance.Info is MphCollisionInfo collision)
                {
                    foreach (CollisionData data in collision.Data)
                    {
                    }
                }
                else if (instance.Info is FhCollisionInfo fhCollision)
                {
                }
            }
            Nop();
        }

        public static void TestAllFhCollision()
        {
            var allCollision = new List<(bool, CollisionInstance)>();
            foreach (KeyValuePair<string, RoomMetadata> meta in Metadata.RoomMetadata)
            {
                if (meta.Value.FirstHunt || meta.Value.Hybrid)
                {
                    allCollision.Add((true, Collision.GetCollision(meta.Value)));
                }
            }
            foreach (KeyValuePair<string, ModelMetadata> meta in Metadata.FirstHuntModels)
            {
                if (meta.Value.CollisionPath != null)
                {
                    allCollision.Add((false, Collision.GetCollision(meta.Value)));
                }
            }
            foreach ((bool room, CollisionInstance instance) in allCollision)
            {
                var collision = (FhCollisionInfo)instance.Info;
            }
            Nop();
        }

        public static void ConvertRoomToMph(string room, string? over = null)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            RoomMetadata? overMeta = null;
            if (over != null)
            {
                overMeta = Metadata.RoomMetadata[over];
            }
            Debug.Assert(meta.EntityPath != null && meta.NodePath != null);
            string folder = Path.Combine(Paths.Export, "_pack");
            string fileSystem = meta.FirstHunt ? Paths.FhFileSystem : Paths.FileSystem;
            Console.WriteLine("Converting model...");
            // model, texure
            (byte[] model, byte[] texture) = Repack.RepackRoomModel(room, separateTextures: true);
            string modelPath = Path.GetFileName(overMeta?.ModelPath ?? meta.ModelPath);
            string modelDest = Path.Combine(folder, modelPath);
            string texDest = Path.Combine(folder, modelPath.Replace("_Model.bin", "_Tex.bin").Replace("_model.bin", "_tex.bin"));
            File.WriteAllBytes(modelDest, model);
            File.WriteAllBytes(texDest, texture);
            Console.WriteLine("Converting collision...");
            // collision
            byte[] collision = RepackCollision.RepackMphRoom(room);
            string colDest = Path.Combine(folder, Path.GetFileName(overMeta?.CollisionPath ?? meta.CollisionPath));
            File.WriteAllBytes(colDest, collision);
            Console.WriteLine("Converting animation...");
            // animation
            string animSrc = Path.Combine(fileSystem, meta.AnimationPath);
            string animDest = Path.Combine(folder, Path.GetFileName(overMeta?.AnimationPath ?? meta.AnimationPath));
            File.Delete(animDest);
            File.Copy(animSrc, animDest);
            //entity, nodedata
            if (meta.Hybrid)
            {
                Console.WriteLine("Copying entities...");
                Console.WriteLine("Copying nodedata...");
                string entSrc = Path.Combine(fileSystem, meta.EntityPath);
                string nodeSrc = Path.Combine(fileSystem, meta.NodePath);
                string entDest = Path.Combine(folder, meta.EntityPath);
                string nodeDest = Path.Combine(folder, meta.NodePath);
                if (overMeta != null)
                {
                    Debug.Assert(overMeta.EntityPath != null && overMeta.NodePath != null);
                    entDest = Path.Combine(folder, overMeta.EntityPath);
                    nodeDest = Path.Combine(folder, overMeta.NodePath);
                }
                File.Delete(entDest);
                File.Delete(nodeDest);
                File.Copy(entSrc, entDest);
                File.Copy(nodeSrc, nodeDest);
            }
            else
            {
                Console.WriteLine("Converting entities...");
                byte[] entity = Repack.RepackMphEntities(room);
                string entDest = Path.Combine(folder, Path.GetFileName(overMeta?.EntityPath ?? meta.EntityPath));
                File.WriteAllBytes(entDest, entity);
                // todo: nodedata
            }
            Console.Write("Creating archive...");
            // archive
            var files = new List<string>()
            {
                animDest,
                colDest,
                modelDest
            };
            string outPath = Path.Combine(folder, "out.arc");
            Archive.Archiver.Archive(outPath, files);
            string archiveName = overMeta?.Archive ?? meta.Archive;
            Console.WriteLine(" Compressing...");
            LZ10.Compress(outPath, outPath.Replace("out.arc", $"{archiveName}.arc"));
            File.Delete(outPath);
            Console.WriteLine("Done.");
            Nop();
        }

        public static void ConvertRoomToFh(string room, string? over = null)
        {
            RoomMetadata meta = Metadata.RoomMetadata[room];
            RoomMetadata? overMeta = null;
            if (over != null)
            {
                overMeta = Metadata.RoomMetadata[over];
            }
            Debug.Assert(meta.EntityPath != null && meta.NodePath != null);
            string folder = Path.Combine(Paths.Export, "_pack");
            string fileSystem = meta.FirstHunt ? Paths.FhFileSystem : Paths.FileSystem;
            RepackFilter filter = RepackFilter.All;
            if (!meta.FirstHunt && !meta.Hybrid)
            {
                filter = meta.Multiplayer ? RepackFilter.Multiplayer : RepackFilter.SinglePlayer;
            }
            Console.WriteLine("Converting model...");
            // model, texure
            (byte[] model, _) = Repack.RepackRoomModel(room, separateTextures: false, filter);
            string modelPath = Path.GetFileName(overMeta?.ModelPath ?? meta.ModelPath);
            string modelDest = Path.Combine(folder, modelPath);
            File.WriteAllBytes(modelDest, model);
            Console.WriteLine("Converting collision...");
            // collision
            byte[] collision = RepackCollision.RepackFhRoom(room, filter);
            string colDest = Path.Combine(folder, Path.GetFileName(overMeta?.CollisionPath ?? meta.CollisionPath));
            File.WriteAllBytes(colDest, collision);
            Console.WriteLine("Converting animation...");
            // animation
            string animSrc = Path.Combine(fileSystem, meta.AnimationPath);
            string animDest = Path.Combine(folder, Path.GetFileName(overMeta?.AnimationPath ?? meta.AnimationPath));
            File.Delete(animDest);
            File.Copy(animSrc, animDest);
            //entity, nodedata
            if (meta.Hybrid)
            {
                Console.WriteLine("Copying entities...");
                Console.WriteLine("Copying nodedata...");
                string entSrc = Path.Combine(fileSystem, meta.EntityPath);
                string nodeSrc = Path.Combine(fileSystem, meta.NodePath);
                string entDest = Path.Combine(folder, meta.EntityPath);
                string nodeDest = Path.Combine(folder, meta.NodePath);
                if (overMeta != null)
                {
                    Debug.Assert(overMeta.EntityPath != null && overMeta.NodePath != null);
                    entDest = Path.Combine(folder, overMeta.EntityPath);
                    nodeDest = Path.Combine(folder, overMeta.NodePath);
                }
                File.Delete(entDest);
                File.Delete(nodeDest);
                File.Copy(entSrc, entDest);
                File.Copy(nodeSrc, nodeDest);
            }
            else
            {
                Console.WriteLine("Converting entities...");
                byte[] entity = Repack.RepackFhEntities(room, filter);
                string entDest = Path.Combine(folder, Path.GetFileName(overMeta?.EntityPath ?? meta.EntityPath));
                File.WriteAllBytes(entDest, entity);
                // todo: nodedata
            }
            Console.WriteLine("Done.");
            Nop();
        }

        public static void TestCameraShake()
        {
            for (int i = 0; i < 1; i++)
            {
                Rng.DoCameraShake(204);
            }
            Console.WriteLine();
            var chances = new List<int>() { 50, 50, 50 };
            foreach (int chance in chances)
            {
                bool spawn = Rng.GetRandomInt2(100) < chance;
                Console.WriteLine(spawn);
                if (spawn)
                {
                    Rng.GetRandomInt2(1);
                }
            }
            Console.ReadLine();
        }

        private static void Nop()
        {
        }
    }
}
