using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;

namespace MphRead
{
    public static partial class Metadata
    {
        public static string? GetEnemyModelName(EnemyType type)
        {
            int index = (int)type;
            if (index > EnemyModelNames.Count)
            {
                throw new IndexOutOfRangeException();
            }
            return EnemyModelNames[index];
        }

        public static readonly IReadOnlyList<string?> EnemyModelNames = new string?[52]
        {
            /*  0 */ "warwasp_lod0",
            /*  1 */ "zoomer",
            /*  2 */ "Temroid_lod0",
            /*  3 */ "Chomtroid",
            /*  4 */ "Chomtroid",
            /*  5 */ "Chomtroid",
            /*  6 */ "Chomtroid",
            /*  7 */ null,
            /*  8 */ null,
            /*  9 */ null,
            /* 10 */ "BarbedWarWasp",
            /* 11 */ "shriekbat",
            /* 12 */ "geemer",
            /* 13 */ null,
            /* 14 */ null,
            /* 15 */ null,
            /* 16 */ "blastcap",
            /* 17 */ null,
            /* 18 */ "Alimbic_Turret",
            /* 19 */ "CylinderBoss",
            /* 20 */ "CylinderBossEye",
            /* 21 */ null,
            /* 22 */ null,
            /* 23 */ "PsychoBit",
            /* 24 */ "Gorea1A_lod0",
            /* 25 */ null,
            /* 26 */ null,
            /* 27 */ null,
            /* 28 */ "Gorea1B_lod0",
            /* 29 */ null,
            /* 30 */ "PowerBomb",
            /* 31 */ "Gorea2_lod0",
            /* 32 */ null,
            /* 33 */ "goreaMeteor",
            /* 34 */ "PsychoBit",
            /* 35 */ "GuardBot2_lod0",
            /* 36 */ "GuardBot1",
            /* 37 */ "DripStank_lod0",
            /* 38 */ "AlimbicStatue_lod0",
            /* 39 */ "LavaDemon",
            /* 40 */ null,
            /* 41 */ "BigEyeBall",
            /* 42 */ null,
            /* 43 */ "BigEyeNest",
            /* 44 */ null,
            /* 45 */ "BigEyeTurret",
            /* 46 */ "SphinkTick_lod0",
            /* 47 */ "SphinkTick_lod0",
            /* 48 */ null,
            /* 49 */ null,
            /* 50 */ null,
            /* 51 */ null
        };

        public static int GetEnemyDeathEffect(EnemyType type)
        {
            int index = (int)type;
            if (index > EnemyDeathEffects.Count)
            {
                throw new IndexOutOfRangeException();
            }
            return EnemyDeathEffects[index];
        }

        public static readonly IReadOnlyList<int> EnemyDeathEffects = new int[52]
        {
            193, 221, 219, 219, 219, 219, 219, 76, 76, 76, 193,
            108, 221, 76, 76, 76, 76, 6, 6, 76, 77,
            76, 76, 77, 76, 76, 76, 76, 76, 76, 77,
            76, 76, 76, 77, 77, 77, 220, 222, 0, 6,
            76, 76, 76, 76, 76, 223, 223, 76, 77, 0,
            220
        };

        public static float GetDamageMultiplier(Effectiveness effectiveness)
        {
            int index = (int)effectiveness;
            if (index > DamageMultipliers.Count)
            {
                throw new IndexOutOfRangeException();
            }
            return DamageMultipliers[index];
        }

        public static readonly IReadOnlyList<float> DamageMultipliers = new float[4] { 0, 0.5f, 1, 2 };

        public static void LoadEffectiveness(EnemyType type, Effectiveness[] dest)
        {
            int index = (int)type;
            if (index > EnemyEffectiveness.Count)
            {
                throw new IndexOutOfRangeException();
            }
            LoadEffectiveness(EnemyEffectiveness[index], dest);
        }

        public static void LoadEffectiveness(int value, Effectiveness[] dest)
        {
            Debug.Assert(dest.Length == 9);
            dest[0] = (Effectiveness)(value & 3);
            dest[1] = (Effectiveness)((value >> 2) & 3);
            dest[2] = (Effectiveness)((value >> 4) & 3);
            dest[3] = (Effectiveness)((value >> 6) & 3);
            dest[4] = (Effectiveness)((value >> 8) & 3);
            dest[5] = (Effectiveness)((value >> 10) & 3);
            dest[6] = (Effectiveness)((value >> 12) & 3);
            dest[7] = (Effectiveness)((value >> 14) & 3);
            dest[8] = (Effectiveness)((value >> 16) & 3);
        }

        // todo: document other replacements
        // defualt = 0x2AAAA = 101010101010101010 (2/normal effectiveness for everything)
        // replacements:
        // AlimbicTurretV10 - 0xEAAA - zero Omega Cannon, double Shock Coil
        // AlimbicTurretV14 - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // AlimbicTurretV27 - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // PsychoBitV10 - 0xEAAA - zero Omega Cannon, double Shock Coil
        // PsychoBitV20 - 0xEAF2 - zero Volt Driver/Omega Cannon, double Missile/Battlehammer/Shock Coil
        // PsychoBitV30 - 0xCEF9 - zero Magmaul/Omega Cannon, double Missile/Battlehammer/Judicator/Shock Coil, half Power Beam
        // PsychoBitV40 - 0xF2F9 - zero Judicator/Omega Cannon, double Missile/Battlehammer/Magmaul/Shock Coil, half Power Beam
        // FireSpawn   - 0x8955 - zero Magmaul/Omega Cannon, normal Judicator/Shock Coil, half all else
        // ArcticSpawn - 0xB155 - zero Judicator/Omega Cannon, normal Shock Coil, double Magmaul, half all else
        // ForceFieldLock - types 0-7 are normal from the corresponding beam and zero from all else; type 8 is zero from all (vulerable to bombs)
        public static readonly IReadOnlyList<int> EnemyEffectiveness = new int[52]
        {
            /*  0 */ 0x2AAAA, // WarWasp
            /*  1 */ 0x2AAAA, // Zoomer
            /*  2 */ 0x2AAAA, // Temroid
            /*  3 */ 0x2AAAA, // Petrasyl1
            /*  4 */ 0x2AAAA, // Petrasyl2
            /*  5 */ 0x2AAAA, // Petrasyl3
            /*  6 */ 0x2AAAA, // Petrasyl4
            /*  7 */ 0x2AAAA, // Unknown7
            /*  8 */ 0x2AAAA, // Unknown8
            /*  9 */ 0x2AAAA, // Unknown9
            /* 10 */ 0x2AAAA, // BarbedWarWasp
            /* 11 */ 0x2AAAA, // Shriekbat
            /* 12 */ 0x2AAA8, // Geemer -- zero from Power Beam
            /* 13 */ 0x00000, // Unknown13 -- zero from all
            /* 14 */ 0x2AAAA, // Unknown14
            /* 15 */ 0x2AAAA, // Unknown15
            /* 16 */ 0x2AAAA, // Blastcap
            /* 17 */ 0x2AAAA, // Unknown17
            /* 18 */ 0x2AAAA, // AlimbicTurret (not used)
            /* 19 */ 0x2AAAA, // Cretaphid
            /* 20 */ 0x2AAAA, // CretaphidEye
            /* 21 */ 0x2AAAA, // CretaphidCrystal
            /* 22 */ 0x2AAAA, // Unknown22
            /* 23 */ 0x2AAAA, // PsychoBit1
            /* 24 */ 0x2AAAA, // Gorea1A
            /* 25 */ 0x2AAAA, // GoreaHead
            /* 26 */ 0x2AAAA, // GoreaArm
            /* 27 */ 0x2AAAA, // GoreaLeg
            /* 28 */ 0x2AAAA, // Gorea1B
            /* 29 */ 0x2AAAA, // GoreaSealSphere1
            /* 30 */ 0x2AAAA, // Trocra
            /* 31 */ 0x2AAAA, // Gorea2
            /* 32 */ 0x20000, // GoreaSealSphere2 -- zero from all except Omega Cannon
            /* 33 */ 0x2AAAA, // GoreaMeteor
            /* 34 */ 0x2AAAA, // PsychoBit2
            /* 35 */ 0x2AABA, // Voldrum2 -- double from Missile
            /* 36 */ 0x2AABA, // Voldrum1 -- double from Missile
            /* 37 */ 0x2AAAA, // Quadtroid
            /* 38 */ 0x2EAFA, // CrashPillar -- double from Missile/Battlehammer/Shock Coil
            /* 39 */ 0x24D55, // FireSpawn -- zero from Magmual, double from Judicator, half from all else except Omega Cannon (not used)
            /* 40 */ 0x2AAAA, // Spawner
            /* 41 */ 0x2AA99, // Slench -- half from Power Beam/Missile
            /* 42 */ 0x2AA99, // SlenchShield -- half from Power Beam/Missile
            /* 43 */ 0x2AA99, // SlenchNest -- half from Power Beam/Missile
            /* 44 */ 0x2AA99, // SlenchSynapse -- half from Power Beam/Missile
            /* 45 */ 0x2AA99, // SlenchTurret -- half from Power Beam/Missile
            /* 46 */ 0x2AAAA, // LesserIthrak
            /* 47 */ 0x2AAAA, // GreaterIthrak
            /* 48 */ 0x2AAAA, // Hunter
            /* 49 */ 0x2AAAA, // ForceFieldLock (not used)
            /* 50 */ 0x2AAAA, // WeakSpot
            /* 51 */ 0x2AAAA  // CarnivorousPlant
        };
    }
}