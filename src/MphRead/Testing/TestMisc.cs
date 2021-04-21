using System;
using System.Collections.Generic;
using System.IO;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Formats.Collision;

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
                            var entityClass = new CameraSequenceEntity(data);
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
                    var seq = CameraSequence.Load(name);
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

        public static void ConvertFhRoomToMph()
        {
            //Utility.RepackCollision.TestCollision("Level FhTestLevel");
            //Utility.Repack.TestRepack();
            //var files = new List<string>()
            //{
            //    @"D:\Cdrv\MPH\Data\_Export\_pack\mp12_anim.bin",
            //    @"D:\Cdrv\MPH\Data\_Export\_pack\mp12_collision.bin",
            //    @"D:\Cdrv\MPH\Data\_Export\_pack\mp12_model.bin"
            //};
            //string outPath = @"D:\Cdrv\MPH\Data\_Export\_pack\out.arc";
            //Archive.Archiver.Archive(outPath, files);
            //Lz.Compress(outPath, outPath.Replace("out.arc", "mp12.arc"));
            //File.Delete(outPath);
            //Testing.TestMisc.TestAllCollision();
            //Utility.Repack.TestEntityEdit();
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
