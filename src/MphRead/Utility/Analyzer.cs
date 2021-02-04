using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MphRead.Utility
{
    public static class Analyzer
    {
        public readonly struct ParticleDefinition
        {
            public readonly int Model; // Model*
            public readonly int Node; // Node*
            public readonly int MaterialId;
        }

        public readonly struct EffectParticle
        {
            public readonly Fixed CreationTime;
            public readonly Fixed ExpirationTime;
            public readonly Fixed Lifespan;
            public readonly Vector3Fx Position;
            public readonly Vector3Fx Speed;
            public readonly Fixed Scale;
            public readonly Fixed Rotation;
            public readonly Fixed Red;
            public readonly Fixed Green;
            public readonly Fixed Blue;
            public readonly Fixed Alpha;
            public readonly int ParticleId;
            public readonly Fixed PortionTotal;
            public readonly int RoField1; // 4 general-purpose fields set once upon particle creation
            public readonly int RoField2;
            public readonly int RoField3;
            public readonly int RoField4;
            public readonly int RwField1; // 4 general-purpose fields updated every frame
            public readonly int RwField2;
            public readonly int RwField3;
            public readonly int RwField4;
            public readonly int Func180; // function pointers assigned from the parent element entry
            public readonly int Func188;
            public readonly int Func18C;
            public readonly int Func190;
            public readonly int Func194;
            public readonly int Func198;
            public readonly int Func19C;
            public readonly int Func1B0;
            public readonly int Func1B4;
            public readonly int Func1B8;
            public readonly int Func1BC;
            public readonly int Prev; // EffectParticle*
            public readonly int Next; // EffectParticle*
        }

        public readonly struct EffectElementEntry
        {
            public readonly int EffectEntry; // EffectEntry*
            public readonly int EffectId;
            public readonly int MatrixPointer; // Matrix43Fx*
            public readonly Matrix43Fx Transform;
            public readonly int State;
            public readonly int Element; // EffectElement*
            public readonly int CreationTime;
            public readonly int ExpirationTime;
            public readonly int DrainTime;
            public readonly int BufferTime;
            public readonly int Func39Called;
            public readonly int Field58;
            public readonly int Field5C;
            public readonly int Field60;
            public readonly int Field64;
            public readonly int Flags;
            public readonly int ChildEffect;
            public readonly int ParticleAmount;
            public readonly int Lifespan;
            public readonly int DrawType;
            public readonly int ParticleCount;
            public readonly ParticleDefinition Particle00;
            public readonly ParticleDefinition Particle01;
            public readonly ParticleDefinition Particle02;
            public readonly ParticleDefinition Particle03;
            public readonly ParticleDefinition Particle04;
            public readonly ParticleDefinition Particle05;
            public readonly ParticleDefinition Particle06;
            public readonly ParticleDefinition Particle07;
            public readonly ParticleDefinition Particle08;
            public readonly ParticleDefinition Particle09;
            public readonly ParticleDefinition Particle10;
            public readonly ParticleDefinition Particle11;
            public readonly ParticleDefinition Particle12;
            public readonly ParticleDefinition Particle13;
            public readonly ParticleDefinition Particle14;
            public readonly ParticleDefinition Particle15;
            public readonly Vector3Fx Position;
            public readonly Vector3Fx Vector1;
            public readonly Vector3Fx Vector2;
            public readonly Vector3Fx Acceleration;
            public readonly int Func170;
            public readonly int Func174;
            public readonly int Func178;
            public readonly int Func17C;
            public readonly int Func180;
            public readonly int Func184;
            public readonly int Func188;
            public readonly int Func18C;
            public readonly int Func190;
            public readonly int Func194;
            public readonly int Func198;
            public readonly int Func19C;
            public readonly int Func1A0;
            public readonly int Func1A4;
            public readonly int Func1A8;
            public readonly int Func1AC;
            public readonly int Func1B0;
            public readonly int Func1B4;
            public readonly int Func1B8;
            public readonly int Func1BC;
            public readonly int SetVectors;
            public readonly int Draw;
            public readonly int ParticleFreePointer; // EffectParticle*
            public readonly EffectParticle ParticleHead;
            public readonly int Prev; //EffectElementEntry*
            public readonly int Next; // EffectElementEntry*
        }

        // start 0x2000000, size 0x400000
        private static readonly uint _fileOffset = 0x2000000;
        private static readonly uint _elemList = 0x1242AC;
        private static readonly uint _elapsedGlobal = 0x123E00;
        private static readonly uint _viewMatrix = 0xDA430;
        private static readonly uint _effVec1 = 0x123EEC;
        private static readonly uint _effVec2 = 0x123E8C;
        private static readonly uint _effVec3 = 0x123E98;

        public static Dictionary<EffectElementEntry, List<EffectParticle>> ReadThing()
        {
            var entries = new List<EffectElementEntry>();
            var effElems = new Dictionary<EffectElementEntry, RawEffectElement>();
            var effParts = new Dictionary<EffectElementEntry, List<EffectParticle>>();
            var matrixBytes = new ReadOnlySpan<byte>(File.ReadAllBytes(@"D:\Cdrv\MPH\Disassembly\mtx.bin"));
            Matrix43Fx viewMatrix = Read.DoOffset<Matrix43Fx>(matrixBytes, _viewMatrix);
            Vector3Fx effVec1 = Read.DoOffset<Vector3Fx>(matrixBytes, _effVec1);
            Vector3Fx effVec2 = Read.DoOffset<Vector3Fx>(matrixBytes, _effVec2);
            Vector3Fx effVec3 = Read.DoOffset<Vector3Fx>(matrixBytes, _effVec3);
            Debug.Assert(effVec1.X.Value == viewMatrix.One.X.Value);
            Debug.Assert(effVec1.Y.Value == viewMatrix.Two.X.Value);
            Debug.Assert(effVec1.Z.Value == viewMatrix.Three.X.Value);
            Debug.Assert(effVec2.X.Value == -viewMatrix.One.Y.Value);
            Debug.Assert(effVec2.Y.Value == -viewMatrix.Two.Y.Value);
            Debug.Assert(effVec2.Z.Value == -viewMatrix.Three.Y.Value);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(@"D:\Cdrv\MPH\Disassembly\dump.bin"));
            uint elapsed = Read.SpanReadUint(bytes, _elapsedGlobal);
            float time = elapsed / 4096f;
            Console.WriteLine(MathF.Round(time, 3));
            EffectElementEntry head = Read.DoOffset<EffectElementEntry>(bytes, _elemList);
            uint offset = (uint)head.Prev - _fileOffset;
            while (offset != _elemList)
            {
                EffectElementEntry entry = Read.DoOffset<EffectElementEntry>(bytes, offset);
                uint elemOffset = (uint)entry.Element - _fileOffset;
                RawEffectElement elem = Read.DoOffset<RawEffectElement>(bytes, elemOffset);
                uint partOffset = (uint)entry.ParticleHead.Prev - _fileOffset;
                entries.Add(entry);
                effElems.Add(entry, elem);
                effParts.Add(entry, new List<EffectParticle>());
                var particles = new List<EffectParticle>();
                while (partOffset != offset + 0x1CC)
                {
                    EffectParticle part = Read.DoOffset<EffectParticle>(bytes, partOffset);
                    particles.Add(part);
                    effParts[entry].Add(part);
                    partOffset = (uint)part.Prev - _fileOffset;
                    Nop();
                }
                float creation = MathF.Round(entry.CreationTime / 4096, 3);
                float expiration = MathF.Round(entry.ExpirationTime / 4096, 3);
                Console.WriteLine($"0x{offset:X2} {creation} - {expiration} x{particles.Count} ({elem.Name.MarshalString()})");
                Console.WriteLine(" -- " + String.Join(", ", particles.Select(p => MathF.Round(p.Rotation.FloatValue, 3))));
                offset = (uint)entry.Prev - _fileOffset;
            }
            Console.WriteLine();
            foreach (EffectElementEntry entry in entries)
            {
                Console.WriteLine($"{entry.Position.X.FloatValue}, {entry.Position.Y.FloatValue}, {entry.Position.Z.FloatValue}");
                Console.WriteLine();
                foreach (EffectParticle particle in effParts[entry])
                {
                    float creation = particle.CreationTime.FloatValue;
                    float expiration = particle.ExpirationTime.FloatValue;
                    float age = time - creation;
                    float lifespan = expiration - creation;
                    float percent = age / lifespan;
                    Console.WriteLine($"{MathF.Round(percent * 100, 3)}%");
                    Console.WriteLine($"{particle.Position.X.FloatValue}, {particle.Position.Y.FloatValue}, {particle.Position.Z.FloatValue}");
                    Console.WriteLine(particle.Scale.FloatValue);
                    Console.WriteLine(particle.Rotation.FloatValue);
                    Console.WriteLine();
                }
                Console.WriteLine("-----------------------------------");
                Console.WriteLine();
            }
            Nop();
            return effParts;
        }

        private static void Nop() { }
    }
}
