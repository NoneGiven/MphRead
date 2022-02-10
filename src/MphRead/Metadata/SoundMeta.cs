using System;
using System.Diagnostics;

namespace MphRead
{
    public static partial class Metadata
    {
        public static int[,] HunterSfx { get; private set; } = null!;
        public static int[,] BeamSfx { get; private set; } = null!;
        public static int[] EnemyDamageSfx { get; private set; } = null!;
        public static int[] EnemyDeathSfx { get; private set; } = null!;

        public static void SetHunterSfxData(byte[] data)
        {
            HunterSfx = ParseSfxData2(data, rows: 8, columns: 17);
        }

        public static void SetBeamSfxData(byte[] data)
        {
            BeamSfx = ParseSfxData2(data, rows: 9, columns: 10);
        }

        public static void SetEnemyDamageSfxData(byte[] data)
        {
            EnemyDamageSfx = ParseSfxData4(data, count: 52);
        }

        public static void SetEnemyDeathSfxData(byte[] data)
        {
            EnemyDeathSfx = ParseSfxData4(data, count: 52);
        }

        private static int[,] ParseSfxData2(byte[] data, int rows, int columns)
        {
            Debug.Assert(data.Length > 0 && data.Length % 2 == 0);
            Debug.Assert(data.Length / 2 == rows * columns);
            int[,] dest = new int[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int start = r * columns * 2 + c * 2;
                    int end = start + 2;
                    ushort value = BitConverter.ToUInt16(data[start..end]);
                    dest[r, c] = value == 0xFFFF ? -1 : value;
                }
            }
            return dest;
        }

        private static int[] ParseSfxData4(byte[] data, int count)
        {
            Debug.Assert(data.Length > 0 && data.Length % 4 == 0);
            Debug.Assert(data.Length / 4 == count);
            int[] dest = new int[count];
            for (int i = 0; i < count; i++)
            {
                int start = i * 4;
                int end = start + 4;
                uint value = BitConverter.ToUInt32(data[start..end]);
                dest[i] = value == 0xFFFFFFFF ? -1 : (int)value;
            }
            return dest;
        }
    }

    public enum HunterSfx
    {
        Damage = 0,
        Death = 1,
        DamageEnemy = 2,
        DeathEnemy = 3,
        Spawn = 4,
        Jump = 5,
        Unmorph = 6,
        Morph = 7,
        Roll = 8,
        BoostCharge = 9,
        Boost = 10,
        Bounce = 11,
        BeamSwitch = 12,
        MissileSwitch = 13,
        MissileOpen = 14,
        MissileClose = 15,
        MissileCharge = 16
    }

    public enum BeamSfx
    {
        Charge = 0,
        Shot = 1,
        Riccochet = 2,
        ChargeShot = 3,
        Hit = 4,
        ChargeHit = 5,
        Empty = 6,
        Switch = 7,
        AffinityCharge = 8,
        AffinityChargeShot = 9
    }

    public enum SfxId
    {
        NONE = -1,
        // normal SFX
        LID_CLOSE = 0,
        LID_OPEN = 1,
        TOUCH_FORWARD = 2,
        TOUCH_BACK = 3,
        TOUCH_NEUTRAL = 4,
        MAIN_MENU_IN = 5,
        MULTI_MENU_IN = 6,
        OPTIONS_MENU_IN = 7,
        TRAINING_MENU_IN = 8,
        OPTIONS_MENU_OUT = 9,
        TOUCH_MENU_IN = 10,
        TOUCH_MENU_OUT = 11,
        CONTROL_WIPE = 12,
        RESULTS_FIRST_PLACE = 13,
        TOUCH_NEGATIVE = 14,
        CREATE_NEW_GAME = 15,
        TTS01 = 16,
        BEGIN_GAME02 = 17,
        BEGIN_GAME03 = 18,
        BEGIN_GAME04 = 19,
        ADV_MODE01 = 20,
        ADV_MODE02 = 21,
        RIVAL_FOUND = 22,
        VOIP_OFF = 23,
        MENU_CANCEL = 24,
        MENU_CONFIRM = 25,
        MENU_CURSOR = 26,
        QUIT_GAME = 27,
        ITEM_SPAWN1 = 28,
        POWER_UP1 = 29,
        POWER_UP2 = 30,
        AMMO_POWER_UP1 = 31,
        AMMO_POWER_UP2 = 32,
        POWER_UP_LOOP1 = 33,
        POWER_UP_LOOP2 = 34,
        WEAPON_POWER_UP = 35,
        ALARM = 36,
        ENERGY_ALARM = 37,
        DOUBLE_DAMAGE_POWER_UP = 38,
        DBL_DAMAGE_A = 39,
        DBL_DAMAGE_B = 40,
        DBL_DAMAGE_C = 41,
        CLOAK_POWER_UP = 42,
        CLOAK_A = 43,
        CLOAK_B = 44,
        CLOAK_C = 45,
        CLOAK_OFF = 46,
        GET_ITEM = 47,
        GET_ITEM2 = 48,
        ELEVATOR_START = 49,
        ELEVATOR_STOP = 50,
        DOOR_UNLOCK = 51,
        UNLOCK_ANIM = 52,
        LOCK_ANIM = 53,
        DOOR_CLOSE = 54,
        DOOR_OPEN = 55,
        DOOR2_CLOSE = 56,
        DOOR2_CLOSE2 = 57,
        DOOR2_LOOP = 58,
        DOOR2_PRE_OPEN = 59,
        DOOR2_OPEN = 60,
        JUMP_PAD = 61,
        TELEPORT_ACTIVATE = 62,
        TELEPORTER_LOOP = 63,
        STONE_PLATFORM = 64,
        STONE_PLATFORM2 = 65,
        STONE_PLATFORM3 = 66,
        STONE_PLATFORM4 = 67,
        ARMORED_DOOR_OPEN1 = 68,
        ARMORED_DOOR_OPEN2 = 69,
        LAVA_PLUME1 = 70,
        PLAYER_SPAWN = 71,
        SUCCESS = 72,
        WEAPON_ALARM = 73,
        PUZZLE_TIMER1 = 74,
        PUZZLE_TIMER2 = 75,
        PUZZLE_TIMER3 = 76, // empty
        DATA_SLOW = 77, // empty
        DATA_FAST = 78, // empty
        CAPTURE_RING_1 = 79,
        CAPTURE_RING_3 = 80,
        CAPTURE_RING_4 = 81,
        CAPTURE_RING_5 = 82,
        CAPTURE_RING_FAIL = 83,
        ARTIFACT1 = 84, // empty
        ARTIFACT2 = 85, // empty
        ARTIFACT3 = 86, // empty
        ARTIFACT_LOOP = 87,
        TELEPORT_OUT = 88,
        GUNSHIP_TRANSMISSION = 89,
        GAME_HINT = 90,
        INTRO_PAGE_TURN = 91,
        GEN_OFF = 92,
        DOOR3A = 93,
        DOOR3B = 94,
        DOOR3C = 95,
        ELECTRIC_BARRIER = 96,
        KEY_PICKUP = 97,
        KEY_APPEAR = 98,
        KEY_DISAPPEAR = 99,
        FORCEFIELD_APPEAR = 100,
        HUD_WEAPON_SWITCH1 = 101,
        HUD_WEAPON_SWITCH2 = 102,
        EXPOSE_ARTIFACT = 103,
        BOX_BREAK1 = 104,
        CHIME1 = 105,
        ELECTRICITY = 106,
        ENERGY_BALL = 107,
        BLUE_FLAME = 108,
        ELEVATOR2_START = 109,
        ELEVATOR2_STOP = 110, // empty
        BUBBLES = 111,
        F2_SWITCH = 112,
        TELEPATHIC_MESSAGE1 = 113,
        TELEPATHIC_MESSAGE2 = 114,
        TELEPATHIC_MESSAGE3 = 115,
        PISTON1 = 116,
        UNIT4_SM_EXPLOSION = 117,
        REGAIN_OCTOLITH1 = 118,
        GEN_ON = 119, // empty
        LOCK_ON = 120, // empty
        BOOST_BALL1 = 121, // empty
        FREELOOK_OFF = 122, // empty
        FREELOOK_ON = 123, // empty
        MODULE_YES = 124, // empty
        MODULE_NO = 125, // empty
        FLAG_ACQUIRED = 126,
        FLAG_CARRIED = 127,
        FLAG_DROPPED = 128,
        FLAG_RESET1 = 129,
        FLAG_RESET2 = 130,
        SCORE = 131,
        SCORED_ON = 132,
        OPPONENT_DAMAGE = 133,
        OPPONENT_DIE = 134,
        BEAM = 135,
        BEAM_HIT = 136,
        BEAM_CHARGE1 = 137,
        BEAM_CHARGE2 = 138,
        BEAM_SWITCH = 139,
        MISSILE = 140,
        MISSILE_HIT = 141,
        MISSILE_OPEN = 142,
        MISSILE_CLOSE = 143,
        BEAM_SWITCH_FAIL = 144,
        MISSILE_SWITCH = 145,
        LOB_GUN = 146,
        LOB_GUN_HIT = 147,
        LOB_GUN_HIT2 = 148,
        LOB_GUN_SWITCH = 149,
        LOB_DISRUPT = 150,
        LOB_GUN_CHARGE1 = 151,
        LOB_GUN_CHARGE2 = 152,
        LOB_GUN_CHARGE2_KANDEN = 153,
        MISSILE_CHARGE1 = 154,
        MISSILE_CHARGE2 = 155,
        MISSILE_CHARGE3 = 156,
        MISSILE_CHARGE1A = 157,
        JACKHAMMER = 158,
        JACKHAMMER2 = 159,
        JACKHAMMER_HIT = 160,
        JACKHAMMER_SWITCH = 161,
        SNIPER = 162,
        SNIPER_RELOAD = 163,
        SNIPER_HIT = 164,
        SNIPER_SWITCH = 165,
        SNIPER_ZOOM_IN = 166,
        SNIPER_ZOOM_OUT = 167,
        MORTAR = 168,
        MORTAR_CHARGE1 = 169,
        MORTAR_CHARGE2 = 170,
        MORTAR_CHARGE2_SPIRE = 171,
        MORTAR_CHARGE_HIT = 172,
        MORTAR_HIT = 173,
        MORTAR_SWITCH = 174,
        MORTAR_BOUNCE = 175,
        MORTAR_BOUNCE2 = 176,
        SHOTGUN_RICCO = 177,
        SHOTGUN_RICCO2 = 178,
        SHOTGUN_RICCO3 = 179,
        SHOTGUN_CHARGE1 = 180,
        SHOTGUN_CHARGE1_NOX = 181,
        SHOTGUN_CHARGE2 = 182,
        SHOTGUN_CHARGE2_NOX = 183,
        SHOTGUN = 184,
        SHOTGUN_HIT = 185,
        SHOTGUN_SWITCH = 186,
        SHOTGUN_BREAK_FREEZE = 187,
        SHOTGUN_FREEZE = 188,
        GENERIC_HIT = 189,
        BEAM_SWITCH_KANDEN = 190,
        MISSILE_SWITCH_KANDEN = 191,
        MISSILE_OPEN_KANDEN = 192,
        MISSILE_CHARGE1_KANDEN = 193,
        BEAM_SWITCH_NOX = 194,
        MISSILE_SWITCH_NOX = 195,
        MISSILE_OPEN_NOX = 196,
        MISSILE_CHARGE1_NOX = 197,
        BEAM_SWITCH_SPIRE = 198,
        MISSILE_SWITCH_SPIRE = 199,
        MISSILE_OPEN_SPIRE = 200,
        MISSILE_CHARGE1_SPIRE = 201,
        ELECTRO_WAVE = 202,
        ELECTRO_WAVE2 = 203,
        ELECTRO_WAVE3 = 204,
        BEAM_SWITCH_WEAVEL = 205,
        MISSILE_SWITCH_WEAVEL = 206,
        MISSILE_OPEN_WEAVEL = 207,
        BEAM_SWITCH_TRACE = 208,
        MISSILE_SWITCH_TRACE = 209,
        MISSILE_OPEN_TRACE = 210,
        BEAM_SWITCH_SYLUX = 211,
        MISSILE_SWITCH_SYLUX = 212,
        MISSILE_OPEN_SYLUX = 213,
        NOX_ALT_ATTACK_HIT = 214,
        SPIRE_ALT_ATTACK_HIT = 215,
        TRACE_ALT_ATTACK_HIT = 216,
        WEAVEL_ALT_ATTACK_HIT = 217,
        MISSILE_DRY = 218, // empty
        LOB_GUN_DRY = 219, // empty
        MORTAR_LOOP = 220, // empty
        ZOOMER_DIE = 221,
        ZOOMER_DAMAGE = 222,
        ZOOMER_IDLE_LOOP = 223,
        GEEMER_EXTEND = 224,
        GEEMER_RETRACT = 225,
        SHRIEKBAT_ATTACK = 226,
        SHRIEKBAT_PRE_ATTACK = 227,
        SHRIEKBAT_DIE = 228,
        BLASTCAP_DAMAGE = 229,
        BLASTCAP_DIE = 230,
        BLASTCAP_AGITATE = 231,
        PSYCHOBIT_CHARGE = 232,
        PSYCHOBIT_BEAM = 233,
        PSYCHOBIT_FLY = 234,
        PSYCHOBIT_DAMAGE = 235,
        PSYCHOBIT_DIE = 236,
        TURRET_ATTACK = 237,
        TURRET_DAMAGE = 238,
        TURRET_DIE = 239,
        TURRET_LOCK_ON = 240,
        HANGING_TERROR_DAMAGE = 241,
        HANGING_TERROR_DIE = 242,
        HANGING_TERROR_DROP = 243,
        HANGING_TERROR_UP = 244,
        HANGING_TERROR_WALK = 245,
        HANGING_TERROR_ATTACK1 = 246,
        HANGING_TERROR_WARN = 247,
        HANGING_TERROR_SCREAM = 248,
        GUARD_BOT_ROLL = 249,
        GUARD_BOT_DAMAGE = 250,
        GUARD_BOT_DIE = 251,
        GUARD_BOT_ATTACK1 = 252,
        GUARD_BOT_ATTACK2 = 253,
        GUARD_BOT_JUMP = 254,
        GUARDIAN_DAMAGE = 255,
        GUARDIAN_DIE = 256,
        GUARDIAN_JUMP = 257,
        GUARDIAN_LAND_METAL = 258,
        WASP_ATTACK = 259,
        WASP_DAMAGE = 260,
        WASP_DIE = 261,
        WASP_IDLE = 262,
        MOCHTROID_DAMAGE = 263,
        MOCHTROID_DIE = 264,
        MOCHTROID_FLY = 265,
        MOCHTROID_TELEPORT_IN = 266,
        MOCHTROID_TELEPORT_OUT = 267,
        METROID_CHARGE = 268,
        METROID_DAMAGE = 269,
        METROID_DIE = 270,
        METROID_FLY = 271,
        STATUE_DAMAGE = 272,
        STATUE_DIE = 273,
        STATUE_1 = 274,
        STATUE_2 = 275,
        STATUE_3 = 276,
        STATUE_ACTIVATE1 = 277,
        ENEMY_SPAWNER_DAMAGE = 278,
        ENEMY_SPAWNER_DIE = 279,
        ENEMY_SPAWNER_SPAWN = 280,
        DRIPSTANK_ATTACK1 = 281,
        DRIPSTANK_ATTACK2 = 282,
        DRIPSTANK_DAMAGE = 283,
        DRIPSTANK_DIE = 284,
        DRIPSTANK_IDLE = 285,
        DRIPSTANK_PRE_ATTACK1 = 286,
        LAVA_DEMON_ATTACK1 = 287,
        LAVA_DEMON_APPEAR1 = 288,
        LAVA_DEMON_APPEAR2 = 289,
        LAVA_DEMON_APPEAR3 = 290,
        LAVA_DEMON_APPEAR4 = 291,
        LAVA_DEMON_DAMAGE = 292,
        LAVA_DEMON_DIE1 = 293,
        PLANT_DIE = 294,
        CYLINDER_BOSS_ATTACK = 295,
        CYLINDER_BOSS_CRYSTAL_DAMAGE = 296,
        CYLINDER_BOSS_CRYSTAL_DOWN = 297,
        CYLINDER_BOSS_CRYSTAL_UP = 298,
        CYLINDER_BOSS_DAMAGE = 299,
        CYLINDER_BOSS_SPIN = 300,
        CYLINDER_BOSS_DIE = 301, // empty
        CYLINDER_BOSS_ATTACK2 = 302,
        CYL_BOSS_CRYSTAL_SCR1_BOOM = 303,
        CYL_BOSS_CRYSTAL_SCR1_WHSH = 304,
        GOREA_SHOULDER_DAMAGE1 = 305,
        GOREA_SHOULDER_DAMAGE2 = 306,
        GOREA_SHOULDER_DIE = 307,
        GOREA_ROAR = 308,
        GOREA_REGEN_ARM = 309,
        GOREA_ARM_SWING_ATTACK = 310,
        GOREA_ATTACK1 = 311,
        GOREA_WEAPON_SWITCH = 312,
        GOREA_SWITCH1A = 313,
        GOREA_SWITCH1B = 314,
        GOREA_SWITCH1C = 315,
        GOREA_SWITCH_DEACTIVATE1 = 316,
        GOREA_ATTACK2 = 317,
        GOREA_ATTACK2A = 318,
        GOREA_ATTACK2B = 319,
        GOREA_1B_DAMAGE = 320,
        GOREA_1B_DIE = 321,
        GOREA_1B_DIE2 = 322,
        GOREA_ATTACK3A = 323,
        GOREA_ATTACK3B = 324,
        GOREA_ATTACK3_LOOP = 325,
        GOREA_TRANSFORM1_1 = 326,
        GOREA_TRANSFORM2_1 = 327,
        GOREA_TRANSFORM2_2 = 328,
        GOREA_TRANSFORM2_3 = 329,
        BIGEYE_OPEN = 330,
        BIGEYE_CLOSE = 331,
        BIGEYE_ATTACH = 332,
        BIGEYE_DETACH = 333,
        BIGEYE_SYNAPSE_DAMAGE = 334,
        BIGEYE_SYNAPSE_DIE_SCR1 = 335,
        BIGEYE_SYNAPSE_REGEN_SCR1 = 336,
        BIGEYE_ATTACK1A_SCR1 = 337,
        BIGEYE_ATTACK1C = 338,
        BIGEYE_ATTACK2 = 339,
        BIGEYE_DAMAGE = 340,
        BIGEYE_DEFLECT = 341,
        BIGEYE_DIE1 = 342,
        BIGEYE_INTRO_SCR1 = 343,
        BIGEYE_INTRO_SCR2 = 344,
        BIGEYE_INTRO_SCR3 = 345,
        GOREA2_TELEPORT1 = 346,
        GOREA2_TELEPORT_OUT = 347,
        GOREA2_DAMAGE1 = 348,
        GOREA2_DAMAGE2A = 349,
        GOREA2_REGEN = 350,
        GOREA2_ATTACK2_LOOP1 = 351,
        GOREA2_ATTACK2_LOOP2 = 352,
        GOREA2_ATTACK2_HIT = 353,
        GOREA2_ATTACK1A = 354,
        GOREA2_ATTACK1B = 355,
        GOREA2_DEATH1 = 356,
        NGLE = 357,
        MORPH_BALL = 358,
        MORPH_BALL_BOMB = 359,
        MORPH_BALL_BOMB_PLACE = 360,
        ROLL = 361,
        JUMP = 362,
        LAND_METAL = 363,
        MORPH_BALL_BOUNCE_METAL = 364,
        WALK_METAL1 = 365,
        WALK_METAL2 = 366,
        BOOST_BALL2 = 367,
        DAMAGE1 = 368,
        DAMAGE2 = 369,
        DAMAGE3 = 370,
        DAMAGE4 = 371,
        DIE = 372,
        WALK_WET1 = 373,
        WALK_WET2 = 374,
        LAND_WET = 375,
        DIE_LOSE_CRYSTAL = 376,
        DIE_MULTI1 = 377,
        LAVA_DAMAGE = 378,
        SAMUS_DEATH1 = 379,
        SAMUS_DEATH2 = 380,
        SAMUS_DEATH3 = 381,
        LAND_ROCK = 382,
        WALK_ROCK1 = 383,
        WALK_ROCK2 = 384,
        LAND_SNOW = 385,
        WALK_SNOW1 = 386,
        WALK_SNOW2 = 387,
        LAND_ICE = 388,
        WALK_ICE1 = 389,
        WALK_ICE2 = 390,
        LAND_SAND = 391,
        WALK_SAND1 = 392,
        WALK_SAND2 = 393,
        ROLL_SNOW1 = 394,
        ROLL_SNOW2 = 395,
        ROLL_ICE1 = 396,
        ROLL_ROCK = 397,
        ROLL_SAND = 398,
        NOX_SPIN1 = 399,
        NOX_TRANSFORM = 400,
        NOX_TRANSFORM2 = 401,
        NOX_LAND_METAL = 402,
        NOX_TOP_BOUNCE_METAL = 403,
        NOX_WALK_METAL1 = 404,
        NOX_WALK_METAL2 = 405,
        NOX_DAMAGE1 = 406,
        NOX_DIE = 407,
        NOX_JUMP = 408,
        NOX_TOP_ATTACK1 = 409,
        NOX_TOP_ATTACK2 = 410,
        NOX_TOP_ATTACK3 = 411,
        NOX_TOP_ENERGY_DRAIN1 = 412,
        NOX_TOP_ENERGY_DRAIN2 = 413,
        NOX_TOP_ENERGY_DRAIN3 = 414,
        NOX_DIE_MULTI1 = 415,
        KANDEN_WALK_METAL1 = 416,
        KANDEN_WALK_METAL2 = 417,
        KANDEN_TRANSFORM = 418,
        KANDEN_TRANSFORM2 = 419,
        KANDEN_LAND_METAL = 420,
        KANDEN_DAMAGE = 421,
        KANDEN_DIE = 422,
        KANDEN_DIE2 = 423,
        KANDEN_JUMP = 424,
        KANDEN_SLITHER = 425,
        KANDEN_ALT_LAND = 426,
        KANDEN_ALT_ATTACK = 427,
        KANDEN_DIE_MULTI1 = 428,
        SPIRE_ROLL = 429,
        SPIRE_BALL_BOUNCE = 430,
        SPIRE_WALK_METAL1 = 431,
        SPIRE_WALK_METAL2 = 432,
        SPIRE_LAND_METAL = 433,
        SPIRE_JUMP = 434,
        SPIRE_DIE = 435,
        SPIRE_DAMAGE = 436,
        SPIRE_ALT_ATTACK = 437,
        SPIRE_TRANSFORM = 438,
        SPIRE_TRANSFORM2 = 439,
        SPIRE_DIE_MULTI1 = 440,
        SYLUX_DAMAGE = 441,
        SYLUX_DIE_MULTI1 = 442,
        SYLUX_TRANSFORM = 443,
        SYLUX_TRANSFORM2 = 444,
        SYLUX_JUMP = 445,
        SYLUX_ALT_ATTACK = 446,
        SYLUX_ALT_HOVER = 447,
        SYLUX_ALT_BOUNCE = 448,
        TRACE_DAMAGE = 449,
        TRACE_DIE_MULTI1 = 450,
        TRACE_ALT_ATTACK = 451,
        TRACE_TRANSFORM = 452,
        TRACE_TRANSFORM2 = 453,
        TRACE_JUMP = 454,
        TRACE_ALT_WALK = 455,
        TRACE_ALT_LAND = 456,
        WEAVEL_DAMAGE = 457,
        WEAVEL_DIE_MULTI1 = 458,
        WEAVEL_TRANSFORM = 459,
        WEAVEL_TRANSFORM2 = 460,
        WEAVEL_JUMP = 461,
        WEAVEL_ALT_ATTACK = 462,
        SCAN_VISOR_ON = 463,
        SCAN_VISOR_ON2 = 464,
        SCAN_VISOR_OFF = 465,
        SCAN_STATUS_BAR = 466,
        SCAN_COMPLETE = 467,
        SCAN_OK = 468,
        SCAN_SCROLL_BUTTONS = 469,
        SCAN_VISOR_LOOP = 470,
        SCAN_OUT_OF_RANGE = 471,
        RETURN_TO_SHIP_SCR01 = 472,
        LOGBOOK01 = 473,
        LOGBOOK02 = 474,
        CHOOSE_ITEM_SCR01 = 475,
        SHIP_TAKEOFF1 = 476,
        FAST_SCROLL_DOWN_LOOP = 477,
        FAST_SCROLL_UP_LOOP = 478,
        ITEM_ARROW = 479,
        LETTER_BLIP = 480,
        NEGATIVE_CLICK = 481,
        OPTIONS_CONTROL_TYPE = 482,
        OPTIONS_LOOK_INVERT = 483,
        OPTIONS_SENSITIVITY_LOOP = 484,
        OPTIONS_SENSITIVITY_OFF = 485,
        OPTIONS_SENSITIVITY_ON = 486,
        RETURN_TO_SHIP_NO = 487,
        RETURN_TO_SHIP_YES = 488,
        SLOW_SCROLL_DOWN_LOOP = 489,
        SLOW_SCROLL_UP_LOOP = 490,
        SMALL_POP_UP_NO = 491,
        SMALL_POP_UP_YES = 492,
        WEAPON_DOWN = 493,
        WEAPON_EQUIPPED = 494,
        WEAPON_UP = 495,
        PRE_SELECT = 496,
        SLIDER_DOWN = 497,
        SLIDER_LOOP = 498,
        SLIDER_UP = 499,
        LAND_SHIP = 500,
        SHIP_THRUST_LOOP = 501,
        SHIP_THRUST_RELEASE = 502,
        CELESTIAL_ARCHIVES_SCR01A = 503,
        CAG01A = 504,
        CAG01B = 505,
        CAG02 = 506,
        CAG03 = 507,
        SAMUS_SHIP1 = 508,
        SAMUS_SHIP2 = 509,
        SAMUS_SHIP3 = 510,
        POWER_OFF1 = 511,
        COMP_ON1 = 512,
        COMP_OFF1 = 513,
        GLASS_BREAK1 = 514,
        GLASS_BREAK2 = 515,
        U3F2RM2_ENERGY_START1 = 516,
        U3F2RM2_ENERGY_START2 = 517,
        SAMUS_SHIP4 = 518,
        UNIT4_RM1_SCR03_BIGPOWER1 = 519,
        UNIT4_RM1_SCR03_BIGPOWER2 = 520,
        UNIT4_RM1_SCR03_BIGPOWER3 = 521,
        SYLUX_SHIP_FLY_BY1 = 522,
        SYLUX_SHIP_FLY_BY2 = 523,
        SYLUX_SHIP_FLY_BY3 = 524,
        SYLUX_SHIP_FLY_BY4 = 525,
        SYLUX_SHIP_FLY_BY5 = 526,
        SYLUX_SHIP_FLY_BY6 = 527,
        // SFX scripts
        CONTROLSELECT = 0 | 0x4000,
        CAPTURE_RING_SCRIPT1 = 1 | 0x4000,
        CAPTURE_RING_SCRIPT2 = 2 | 0x4000,
        CAPTURE_RING_SCRIPT3 = 3 | 0x4000,
        SAMUS_DEATH = 4 | 0x4000,
        BIG_SLIDE_BACK_SCR = 5 | 0x4000,
        BIG_SLIDE_SCR = 6 | 0x4000,
        CHOOSE_ITEM_SCR = 7 | 0x4000,
        ITEM_BACK_SCR = 8 | 0x4000,
        LOGBOOK_1ST_CATEGORY_SCR = 9 | 0x4000,
        LOGBOOK_NEXT_CATEGORY_SCR = 10 | 0x4000,
        RETURN_TO_SHIP_SCR = 11 | 0x4000,
        SAVE_GAME_YES_SCR = 12 | 0x4000,
        SMALL_POP_UP_SCR = 13 | 0x4000,
        ENTER_SHIP_SCR = 14 | 0x4000,
        LAND_SHIP_SCR = 15 | 0x4000,
        PLANET_FOUND_SCR = 16 | 0x4000,
        CELESTIAL_ARCHIVES_SCR01 = 17 | 0x4000,
        CELESTIAL_ARCHIVES_GUARDIAN_SCR = 18 | 0x4000,
        CELESTIAL_ARCHIVES_UNLOCK_SCR01 = 19 | 0x4000,
        CELESTIAL_ARCHIVES_KANDEN_DIE = 20 | 0x4000,
        SPIRE_DIE_SCR = 21 | 0x4000,
        WEAVEL_DIE_SCR = 22 | 0x4000,
        ADVENTURE_MODE_SCR = 23 | 0x4000,
        BEGIN_GAME_SCR = 24 | 0x4000,
        CREATE_CANCEL_SCR = 25 | 0x4000,
        CREATE_CONFIRM_SCR = 26 | 0x4000,
        TOUCH_TO_START_SCR = 27 | 0x4000,
        TOUCH_BACK_SCR = 28 | 0x4000,
        SMALL_ARROW_SCR = 29 | 0x4000,
        STATUE_ATTACK_SCR = 30 | 0x4000,
        STATUE_HOP_SCR = 31 | 0x4000,
        STATUE_ACTIVATE_SCR = 32 | 0x4000,
        STATUE_TURN_SCR = 33 | 0x4000,
        HANGING_TERROR_ATTACK1_SCR = 34 | 0x4000,
        HANGING_TERROR_SCREAM_SCR = 35 | 0x4000,
        SHRIEKBAT_PRE_ATTACK_SCR = 36 | 0x4000,
        LAVA_DEMON_APPEAR_SCR = 37 | 0x4000,
        LAVA_DEMON_DISAPPEAR_SCR = 38 | 0x4000,
        LAVA_DEMON_ATTACK_SCR = 39 | 0x4000,
        LAVA_DEMON_DIE_SCR = 40 | 0x4000,
        GOREA_ARM_SWING_ATTACK_SCR = 41 | 0x4000,
        GOREA_REGEN_ARM_SCR = 42 | 0x4000,
        GOREA_ROAR_SCR = 43 | 0x4000,
        GOREA_1B_DIE_SCR = 44 | 0x4000,
        GOREA_1B_DIE2_SCR = 45 | 0x4000,
        GOREA_TRANSFORM1_SCR = 46 | 0x4000,
        GOREA_TRANSFORM2_SCR = 47 | 0x4000,
        GOREA_WEAPON_SWITCH_SCR = 48 | 0x4000,
        BIGEYE_SYNAPSE_DIE_SCR = 49 | 0x4000,
        BIGEYE_SYNAPSE_REGEN_SCR = 50 | 0x4000,
        BIGEYE_ATTACK1A_SCR = 51 | 0x4000,
        BIGEYE_DIE_SCR = 52 | 0x4000,
        CYLINDER_BOSS_CRYSTAL_SCR = 53 | 0x4000,
        TELEPORT_ACTIVATE_SCR = 54 | 0x4000,
        DOOR_LOCK_SCR = 55 | 0x4000,
        ARMORED_DOOR_SCR = 56 | 0x4000,
        LAVA_PLUME_SCR = 57 | 0x4000,
        PUZZLE_TIMER1_SCR = 58 | 0x4000,
        DOOR3_OPEN_SCR = 59 | 0x4000,
        DOOR3_CLOSE_SCR = 60 | 0x4000,
        DOOR2_CLOSE_SCR = 61 | 0x4000,
        TRACE_WARNING_SCR = 62 | 0x4000,
        FORCEFIELD01_SCR = 63 | 0x4000,
        BOX_BREAK1_SCR = 64 | 0x4000,
        UNIT4_RM1_INTRO_SCR01 = 65 | 0x4000,
        UNIT4_RM1_INTRO_SCR02 = 66 | 0x4000,
        LAUNCH_SHIP_YES_SCR = 67 | 0x4000,
        BIGEYE_ATTACK3_SCR = 68 | 0x4000,
        U4F2RM5_CRASHING_COLUMNS = 69 | 0x4000,
        U4F2RM5_ELECTRO_OFF = 70 | 0x4000,
        U3F2RM3_COMPUTER_OFF = 71 | 0x4000,
        U3F2RM3_COMPUTER_ON = 72 | 0x4000,
        U3F2RM3_GUARDIANS = 73 | 0x4000,
        GOREA_SWITCH1 = 74 | 0x4000,
        GOREA_SWITCH4 = 75 | 0x4000,
        GOREA_SWITCH5 = 76 | 0x4000,
        GOREA_SWITCH6 = 77 | 0x4000,
        GOREA_SWITCH_DEACTIVATE = 78 | 0x4000,
        U3F2RM3_KEY_DISAPPEAR = 79 | 0x4000,
        U3F2RM2_ENERGY_START = 80 | 0x4000,
        GOREA_ATTACK2C_SCR = 81 | 0x4000,
        BIGEYE_INTRO_SCR = 82 | 0x4000,
        BIGEYE_ATTACH_SCR = 83 | 0x4000,
        UNIT4_RM1_SCR03 = 84 | 0x4000,
        GOREA2_TELEPORT_IN_SCR = 85 | 0x4000,
        GOREA2_DAMAGE2_SCR = 86 | 0x4000,
        GOREA2_ATTACK2_SCR = 87 | 0x4000,
        GOREA2_ATTACK2_DIE_SCR = 88 | 0x4000,
        GOREA_GUN_SCR = 89 | 0x4000,
        U3F2RM2_ENERGY_FAIL = 90 | 0x4000,
        GOREA2_DEATH_SCR = 91 | 0x4000,
        ALINOS_INTRO_SCR = 92 | 0x4000,
        ARCTERRA_INTRO_SCR = 93 | 0x4000,
        VESPER_INTRO_SCR = 94 | 0x4000,
        SYLUX_SHIP_FLY_BY = 95 | 0x4000,
        TELEPATHIC_MESSAGE = 96 | 0x4000,
        U3F1RM1_SYLUX_SHIP1 = 97 | 0x4000,
        U3F1RM1_SYLUX_SHIP2 = 98 | 0x4000,
        GOREA1_LAND_INTRO_SCR = 99 | 0x4000,
        RESULTS_FIRST_PLACE_SCR = 100 | 0x4000,
        WASP_ATTACK_SCR = 101 | 0x4000,
        UNIT4_RM1_EXPLOSION_SCR = 102 | 0x4000,
        GOREA_ATTACK2B_SCR = 103 | 0x4000,
        GOREA_TENTACLE_DIE_SCR = 104 | 0x4000,
        // DGN SFX
        DGN_ROLL = 0 | 0x8000,
        DGN_LAND_METAL = 1 | 0x8000,
        DGN_MORPH_BALL_BOUNCE_METAL = 2 | 0x8000,
        DGN_WALK_METAL1 = 3 | 0x8000,
        DGN_WALK_METAL2 = 4 | 0x8000,
        DGN_WALK_WET1 = 5 | 0x8000,
        DGN_WALK_WET2 = 6 | 0x8000,
        DGN_LAND_WET = 7 | 0x8000,
        DGN_LAND_ROCK = 8 | 0x8000,
        DGN_WALK_ROCK1 = 9 | 0x8000,
        DGN_WALK_ROCK2 = 10 | 0x8000,
        DGN_LAND_SNOW = 11 | 0x8000,
        DGN_WALK_SNOW1 = 12 | 0x8000,
        DGN_WALK_SNOW2 = 13 | 0x8000,
        DGN_LAND_ICE = 14 | 0x8000,
        DGN_WALK_ICE1 = 15 | 0x8000,
        DGN_WALK_ICE2 = 16 | 0x8000,
        DGN_LAND_SAND = 17 | 0x8000,
        DGN_WALK_SAND1 = 18 | 0x8000,
        DGN_WALK_SAND2 = 19 | 0x8000,
        DGN_ROLL_SNOW = 20 | 0x8000,
        DGN_ROLL_ICE = 21 | 0x8000,
        DGN_ROLL_ROCK = 22 | 0x8000,
        DGN_ROLL_SAND = 23 | 0x8000,
        DGN_ICE_SKATE = 24 | 0x8000,
        DGN_LAVA_DAMAGE = 25 | 0x8000,
        DGN_NOX_SPIN = 26 | 0x8000,
        DGN_NOX_TOP_BOUNCE_METAL = 27 | 0x8000,
        DGN_KANDEN_SLITHER = 28 | 0x8000,
        DGN_KANDEN_ALT_LAND = 29 | 0x8000,
        DGN_SPIRE_BALL_BOUNCE = 30 | 0x8000,
        DGN_SPIRE_ROLL = 31 | 0x8000,
        DGN_SPIRE_ROLL_SNOW = 32 | 0x8000,
        DGN_SPIRE_ROLL_METAL = 33 | 0x8000,
        DGN_SYLUX_ALT_HOVER = 34 | 0x8000,
        DGN_SYLUX_ALT_BOUNCE = 35 | 0x8000,
        DGN_TRACE_ALT_WALK = 36 | 0x8000,
        DGN_TRACE_ALT_LAND = 37 | 0x8000,
        DGN_TRACE_ALT_WALK_ICE = 38 | 0x8000,
        DGN_TRACE_ALT_WALK_ROCK = 39 | 0x8000,
        DGN_TRACE_ALT_WALK_SAND = 40 | 0x8000,
        DGN_TRACE_ALT_WALK_SNOW = 41 | 0x8000,
        DGN_TRACE_ALT_WALK_WET = 42 | 0x8000,
        DGN_MORTAR_BOUNCE = 43 | 0x8000,
        DGN_SHOTGUN_RICCO = 44 | 0x8000,
        DGN_GUARD_BOT_ROLL = 45 | 0x8000,
        DGN_GUARDIAN_LAND_METAL = 46 | 0x8000,
        DGN_ELECTRO_WAVE = 47 | 0x8000,
        DGN_PISTON1 = 48 | 0x8000
    }
}
