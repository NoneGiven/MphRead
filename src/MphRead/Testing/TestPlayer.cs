using System.Collections.Generic;
using MphRead.Utility;

namespace MphRead.Testing
{
    public static class TestPlayer
    {
        public readonly struct ButtonControl
        {
            public readonly ButtonFlags Button;
            public readonly PressFlags Flags;
        }

        public readonly struct PlayerControls
        {
            public readonly uint Flags;
            public readonly ButtonControl Field4;
            public readonly ButtonControl Field8;
            public readonly ButtonControl FieldC;
            public readonly ButtonControl Field10;
            public readonly ButtonControl Field14;
            public readonly ButtonControl Field18;
            public readonly ButtonControl Field1C;
            public readonly ButtonControl Field20;
            public readonly ButtonControl Field24;
            public readonly ButtonControl Field28;
            public readonly ButtonControl Field2C;
            public readonly ButtonControl Field30;
            public readonly ButtonControl Shoot;
            public readonly ButtonControl Jump; // repeated touch is checked separately
            public readonly ButtonControl Field3C;
            public readonly ButtonControl Field40;
            public readonly ButtonControl Field44;
            public readonly ButtonControl Field48;
            public readonly ButtonControl Field4C;
            public readonly ButtonControl Field50;
            public readonly ButtonControl Field54;
            public readonly ButtonControl Field58;
            public readonly ButtonControl Field5C;
            public readonly ButtonControl Field60;
            public readonly ButtonControl Field64;
            public readonly ButtonControl Field68;
            public readonly ButtonControl Field6C;
            public readonly ButtonControl Field70;
            public readonly ButtonControl Field74;
            public readonly ButtonControl Field78;
            public readonly ButtonControl Field7C;
            public readonly ButtonControl Field80;
            public readonly int Field84;
            public readonly int Field88;
            public readonly int Field8C;
            public readonly int Field90;
            public readonly int Field94;
            public readonly int Field98;
        }

        public readonly struct PlayerValues
        {
            public readonly uint Field0;
            public readonly uint Field4;
            public readonly uint Field8;
            public readonly uint FieldC;
            public readonly uint Field10;
            public readonly uint Field14;
            public readonly Fixed BipedGravity; // gravity to apply to biped form in the air or on slippery terrain
            public readonly Fixed AltGravityAir; // gravity to apply to alt form in the air
            public readonly Fixed AltGravityGround; // gravity to apply to alt form on the ground
            public readonly Fixed JumpSpeed; // 1433 (0.35) is used if the player is prime hunter
            public readonly uint Field28;
            public readonly uint Field2C;
            public readonly uint Field30;
            public readonly uint Field34;
            public readonly uint Field38;
            public readonly uint Field3C;
            public readonly Fixed AltCollisionRadius;
            public readonly Fixed AltCollisionY;
            public readonly ushort Field48;
            public readonly ushort Field4A;
            public readonly uint Field4C;
            public readonly uint Field50;
            public readonly uint Field54;
            public readonly uint Field58;
            public readonly uint Field5C;
            public readonly uint Field60;
            public readonly uint Field64;
            public readonly ushort Field68;
            public readonly ushort Field6A;
            public readonly uint Field6C;
            public readonly uint Field70;
            public readonly uint Field74;
            public readonly uint Field78;
            public readonly uint Field7C;
            public readonly uint Field80;
            public readonly uint Field84;
            public readonly uint Field88;
            public readonly uint Field8C;
            public readonly uint Field90;
            public readonly Fixed MinCollisionHeight; // cylinder bottom
            public readonly Fixed MaxCollisionHeight; // cylinder top
            public readonly Fixed BipedCollisionRadius;
            public readonly uint FieldA0;
            public readonly uint FieldA4;
            public readonly uint FieldA8;
            public readonly ushort FieldAC;
            public readonly ushort DamageFlashDurationMaybe;
            public readonly uint FieldB0;
            public readonly uint FieldB4;
            public readonly uint FieldB8;
            public readonly uint FieldBC;
            public readonly uint FieldC0;
            public readonly uint FieldC4;
            public readonly uint FieldC8; // C4 * C4 -- set in load_some_hunter_stuff
            public readonly uint FieldCC;
            public readonly uint FieldD0; // CC * CC -- set in load_some_hunter_stuff
            public readonly uint FieldD4;
            public readonly uint FieldD8;
            public readonly uint FieldDC;
            public readonly ushort FieldE0;
            public readonly ushort FieldE2;
            public readonly ushort FieldE4; // some touch duration threshold
            public readonly ushort FieldE6;
            public readonly uint FieldE8;
            public readonly uint FieldEC;
            public readonly uint ViewSwayStartTime;
            public readonly uint SwayTimeIncrement;
            public readonly uint SwayLimit;
            public readonly uint FieldFC;
            public readonly uint Field100;
            public readonly ushort EnergyStart;
            public readonly ushort Field106;
            public readonly byte AltGroundNoGravity; // don't apply gravity to alt form on ground unless terrain is slippery
            public readonly byte Field109;
            public readonly ushort Field10A;
            public readonly uint Field10C;
            public readonly uint Field110;
            public readonly uint Field114;
            public readonly uint Field118;
            public readonly Fixed JumpPadSlideFactor; // 
            public readonly uint Field120;
            public readonly uint Field124;
            public readonly uint Field128;
            public readonly uint Field12C;
            public readonly uint Field130;
            public readonly uint Field134;
            public readonly uint Field138;
            public readonly uint Field13C;
            public readonly uint Field140;
            public readonly uint Field144;
            public readonly uint Field148;
            public readonly uint Field14C;
            public readonly ushort Field150;
            public readonly ushort Field152;
            public readonly uint Field154;
            public readonly uint Field158;
            public readonly uint Field15C;
            public readonly uint Field160;
            public readonly ushort Field164;
            public readonly ushort Field166;
        }

        public static IReadOnlyList<PlayerControls> GetPlayerControls()
        {
            return Parser.ParseBytes<PlayerControls>(size: 0x9C, count: 5, new byte[]
            {
                0x76, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x03, 0x0C, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x03, 0x04, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x5D, 0xF7, 0xFF, 0xFF, 0x8E, 0xFB, 0xFF, 0xFF, 0x76, 0x02, 0x00, 0x00,
                0x00, 0x08, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x08, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x00, 0x00, 0xF0, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x03, 0x04, 0x00,
                0x00, 0x00, 0x09, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x5D, 0xF7, 0xFF, 0xFF, 0x8E, 0xFB, 0xFF, 0xFF, 0x7C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00,
                0x10, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x20, 0x08, 0x00, 0x00,
                0x11, 0x00, 0x00, 0x00, 0x40, 0x04, 0x00, 0x00, 0x82, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
                0x00, 0x01, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x02, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x00, 0x00, 0x09, 0x00,
                0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x5D, 0xF7, 0xFF, 0xFF,
                0x8E, 0xFB, 0xFF, 0xFF, 0x7C, 0x02, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x40, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x20, 0x08, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00,
                0x40, 0x04, 0x00, 0x00, 0x82, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
                0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x80, 0x00, 0x00,
                0x00, 0x80, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x5D, 0xF7, 0xFF, 0xFF, 0x8E, 0xFB, 0xFF, 0xFF,
                0x74, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00,
                0x40, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00,
                0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
                0x00, 0x03, 0x04, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x5D, 0xF7, 0xFF, 0xFF, 0x8E, 0xFB, 0xFF, 0xFF
            });
        }

        public static IReadOnlyList<PlayerValues> GetPlayerValues()
        {
            return Parser.ParseBytes<PlayerValues>(size: 0x168, count: 8, new byte[]
            {
                0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00,
                0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF,
                0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x70, 0x0F, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00, 0xE1, 0x0A, 0x00, 0x00, 0x8B, 0x00, 0x00, 0x00,
                0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x0F, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00,
                0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00,
                0x00, 0x0C, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00,
                0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00,
                0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF, 0xCC, 0x00, 0x00, 0x00, 0x0C, 0x06, 0x00, 0x00,
                0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0x2D, 0x00, 0x00, 0x00, 0x10, 0x00, 0x19, 0x00,
                0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00, 0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00,
                0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00,
                0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00,
                0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00,
                0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00, 0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00,
                0x01, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x1C, 0x00, 0x0F, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00,
                0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF, 0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00,
                0xE1, 0x0A, 0x00, 0x00, 0xCC, 0x00, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00,
                0x05, 0x00, 0x16, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00, 0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00,
                0x00, 0x28, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00,
                0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF,
                0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00, 0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF,
                0xCC, 0x00, 0x00, 0x00, 0x47, 0x05, 0x00, 0x00, 0x1E, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00,
                0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x14, 0x00, 0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00,
                0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00, 0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00,
                0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00,
                0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00, 0x01, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x14, 0x00, 0x3C, 0x00,
                0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00,
                0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF,
                0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00, 0xE1, 0x0A, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0x14, 0x0A, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x05, 0x00, 0x16, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00,
                0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00,
                0x00, 0x0C, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00,
                0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00,
                0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF, 0xCC, 0x00, 0x00, 0x00, 0x0C, 0x06, 0x00, 0x00,
                0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x08, 0x00,
                0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00, 0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00,
                0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00,
                0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00,
                0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00,
                0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00, 0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00,
                0x04, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0xAA, 0x02, 0x00, 0x00,
                0x33, 0x03, 0x00, 0x00, 0x32, 0x00, 0x1E, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00,
                0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0xB3, 0xFF, 0xFF, 0xFF, 0x5D, 0xFF, 0xFF, 0xFF, 0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x5C, 0x0F, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00,
                0xE1, 0x0A, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x14, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x05, 0x00, 0x16, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00, 0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00,
                0x33, 0x23, 0x00, 0x00, 0x33, 0x13, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF,
                0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00, 0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF,
                0xCC, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x03, 0x00, 0x23, 0x00, 0x00, 0x0C, 0x00, 0x00,
                0x00, 0x09, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x0A, 0x00, 0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00,
                0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00, 0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00,
                0x01, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00,
                0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00,
                0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0x05, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x0A, 0x00, 0x3C, 0x00,
                0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00,
                0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF,
                0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x5C, 0x0F, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00, 0xE1, 0x0A, 0x00, 0x00, 0x8F, 0x00, 0x00, 0x00,
                0x00, 0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x05, 0x00, 0x0F, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00,
                0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00, 0x33, 0x23, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00,
                0x00, 0x0C, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00,
                0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00,
                0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF, 0xCC, 0x00, 0x00, 0x00, 0x51, 0x06, 0x00, 0x00,
                0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x08, 0x00,
                0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00, 0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00,
                0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00,
                0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00,
                0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00,
                0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00, 0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x04, 0x00, 0x16, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x2A, 0x00, 0x3C, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00,
                0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF, 0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0xAE, 0x0F, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00,
                0xE1, 0x0A, 0x00, 0x00, 0xB8, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x05, 0x00, 0x16, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00, 0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00,
                0x00, 0x28, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00,
                0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF,
                0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00, 0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF,
                0xCC, 0x00, 0x00, 0x00, 0x51, 0x06, 0x00, 0x00, 0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00,
                0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x08, 0x00, 0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00,
                0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00, 0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00,
                0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00,
                0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00, 0x01, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x08, 0x00, 0x3C, 0x00,
                0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00,
                0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF,
                0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0xC2, 0x0D, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00, 0xE1, 0x0A, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0x66, 0x06, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0x05, 0x00, 0x16, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00,
                0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00,
                0x6A, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00, 0xCC, 0x24, 0x00, 0x00, 0xCC, 0x14, 0x00, 0x00,
                0x33, 0x07, 0x00, 0x00, 0x66, 0x06, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00,
                0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00,
                0x67, 0xFE, 0xFF, 0xFF, 0x9A, 0xFD, 0xFF, 0xFF, 0xCC, 0x00, 0x00, 0x00, 0x0C, 0x06, 0x00, 0x00,
                0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x08, 0x00,
                0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00, 0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00,
                0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00, 0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00,
                0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00,
                0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00,
                0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00, 0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00,
                0x01, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x99, 0x05, 0x00, 0x00,
                0xCC, 0x06, 0x00, 0x00, 0x24, 0x00, 0x0F, 0x00, 0xC2, 0x01, 0x00, 0x00, 0xC2, 0x01, 0x00, 0x00,
                0xD7, 0x03, 0x00, 0x00, 0xD7, 0x03, 0x00, 0x00, 0x1E, 0x05, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0xB3, 0xFF, 0xFF, 0xFF, 0x0B, 0xFF, 0xFF, 0xFF, 0x92, 0xFF, 0xFF, 0xFF, 0xCC, 0x04, 0x00, 0x00,
                0x14, 0x0E, 0x00, 0x00, 0xAE, 0x0F, 0x00, 0x00, 0x14, 0x0E, 0x00, 0x00, 0x99, 0x0D, 0x00, 0x00,
                0xE1, 0x0A, 0x00, 0x00, 0x7A, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x05, 0x00, 0x0F, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x00,
                0x14, 0x00, 0x00, 0x00, 0x00, 0x70, 0x02, 0x00, 0x62, 0x00, 0x00, 0x00, 0x66, 0x0E, 0x00, 0x00,
                0x00, 0x28, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00,
                0xC8, 0x02, 0x00, 0x00, 0xC8, 0x02, 0x00, 0x00, 0x33, 0x03, 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF,
                0x99, 0x11, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xF5, 0x0C, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x0F, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xCC, 0x00, 0x00, 0x00, 0x0C, 0x06, 0x00, 0x00, 0x00, 0x00, 0x23, 0x00, 0x00, 0x10, 0x00, 0x00,
                0x00, 0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x99, 0x09, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x08, 0x00, 0x05, 0x00, 0x1E, 0x00, 0x05, 0x00, 0x00, 0x00,
                0x3F, 0x02, 0x00, 0x00, 0x76, 0x07, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x5A, 0x00, 0x00, 0x00,
                0x66, 0x02, 0x00, 0x00, 0x08, 0x07, 0x00, 0x00, 0x57, 0x02, 0x0F, 0x00, 0x64, 0x00, 0x64, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00,
                0xF5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x01, 0x00, 0x00, 0x40, 0x00, 0x00,
                0x00, 0xE0, 0x01, 0x00, 0x00, 0x30, 0x02, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0xA0, 0x00, 0x00,
                0x00, 0xA0, 0x05, 0x00, 0x00, 0xC0, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x30, 0x00, 0x00,
                0x00, 0x40, 0x00, 0x00, 0xCC, 0x04, 0x00, 0x00, 0x01, 0x00, 0x1E, 0x00, 0x99, 0x01, 0x00, 0x00,
                0x00, 0x04, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x99, 0x01, 0x00, 0x00, 0x19, 0x00, 0x3C, 0x00
            });
        }
    }
}
