using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Entities.Enemies;

namespace MphRead
{
    public static partial class Metadata
    {
        public static IReadOnlyList<EnemySubroutine<Enemy00Entity>> Enemy00Subroutines = new EnemySubroutine<Enemy00Entity>[7]
        {
            // state 0
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[2]
            {
                new EnemyBehavior<Enemy00Entity>(0, Enemy00Entity.Behavior02),
                new EnemyBehavior<Enemy00Entity>(1, Enemy00Entity.Behavior03)
            }),
            // state 1
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[4]
            {
                new EnemyBehavior<Enemy00Entity>(1, Enemy00Entity.Behavior02),
                new EnemyBehavior<Enemy00Entity>(2, Enemy00Entity.Behavior06),
                new EnemyBehavior<Enemy00Entity>(6, Enemy00Entity.Behavior07),
                new EnemyBehavior<Enemy00Entity>(6, Enemy00Entity.Behavior08)
            }),
            // state 2
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[4]
            {
                new EnemyBehavior<Enemy00Entity>(3, Enemy00Entity.Behavior09),
                new EnemyBehavior<Enemy00Entity>(6, Enemy00Entity.Behavior07),
                new EnemyBehavior<Enemy00Entity>(6, Enemy00Entity.Behavior10),
                new EnemyBehavior<Enemy00Entity>(6, Enemy00Entity.Behavior08)
            }),
            // state 3
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[1]
            {
                new EnemyBehavior<Enemy00Entity>(4, Enemy00Entity.Behavior00)
            }),
            // state 4
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[2]
            {
                new EnemyBehavior<Enemy00Entity>(5, Enemy00Entity.Behavior04),
                new EnemyBehavior<Enemy00Entity>(5, Enemy00Entity.Behavior05)
            }),
            // state 5
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[1]
            {
                new EnemyBehavior<Enemy00Entity>(1, Enemy00Entity.Behavior01)
            }),
            // state 6
            new EnemySubroutine<Enemy00Entity>(new EnemyBehavior<Enemy00Entity>[2]
            {
                new EnemyBehavior<Enemy00Entity>(0, Enemy00Entity.Behavior02),
                new EnemyBehavior<Enemy00Entity>(1, Enemy00Entity.Behavior03)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy02Entity>> Enemy02Subroutines = new EnemySubroutine<Enemy02Entity>[11]
        {
            // state 0
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[2]
            {
                new EnemyBehavior<Enemy02Entity>(2, Enemy02Entity.Behavior04),
                new EnemyBehavior<Enemy02Entity>(1, Enemy02Entity.Behavior05)
            }),
            // state 1
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[4]
            {
                new EnemyBehavior<Enemy02Entity>(7, Enemy02Entity.Behavior09),
                new EnemyBehavior<Enemy02Entity>(7, Enemy02Entity.Behavior10),
                new EnemyBehavior<Enemy02Entity>(10, Enemy02Entity.Behavior11),
                new EnemyBehavior<Enemy02Entity>(2, Enemy02Entity.Behavior12)
            }),
            // state 2
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(3, Enemy02Entity.Behavior00)
            }),
            // state 3
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(4, Enemy02Entity.Behavior00)
            }),
            // state 4
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(5, Enemy02Entity.Behavior00)
            }),
            // state 5
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[2]
            {
                new EnemyBehavior<Enemy02Entity>(1, Enemy02Entity.Behavior02),
                new EnemyBehavior<Enemy02Entity>(8, Enemy02Entity.Behavior03)
            }),
            // state 6
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(9, Enemy02Entity.Behavior00)
            }),
            // state 7
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[3]
            {
                new EnemyBehavior<Enemy02Entity>(1, Enemy02Entity.Behavior04),
                new EnemyBehavior<Enemy02Entity>(1, Enemy02Entity.Behavior05),
                new EnemyBehavior<Enemy02Entity>(0, Enemy02Entity.Behavior06)
            }),
            // state 8
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[3]
            {
                new EnemyBehavior<Enemy02Entity>(6, Enemy02Entity.Behavior07),
                new EnemyBehavior<Enemy02Entity>(6, Enemy02Entity.Behavior02),
                new EnemyBehavior<Enemy02Entity>(6, Enemy02Entity.Behavior08)
            }),
            // state 9
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(7, Enemy02Entity.Behavior00)
            }),
            // state 10
            new EnemySubroutine<Enemy02Entity>(new EnemyBehavior<Enemy02Entity>[1]
            {
                new EnemyBehavior<Enemy02Entity>(7, Enemy02Entity.Behavior01)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy03Entity>> Enemy03Subroutines = new EnemySubroutine<Enemy03Entity>[3]
        {
            // state 0
            new EnemySubroutine<Enemy03Entity>(new EnemyBehavior<Enemy03Entity>[1]
            {
                new EnemyBehavior<Enemy03Entity>(1, Enemy03Entity.Behavior02)
            }),
            // state 1
            new EnemySubroutine<Enemy03Entity>(new EnemyBehavior<Enemy03Entity>[1]
            {
                new EnemyBehavior<Enemy03Entity>(2, Enemy03Entity.Behavior01)
            }),
            // state 2
            new EnemySubroutine<Enemy03Entity>(new EnemyBehavior<Enemy03Entity>[1]
            {
                new EnemyBehavior<Enemy03Entity>(0, Enemy03Entity.Behavior00)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy04Entity>> Enemy04Subroutines = new EnemySubroutine<Enemy04Entity>[2]
        {
            // state 0
            new EnemySubroutine<Enemy04Entity>(new EnemyBehavior<Enemy04Entity>[1]
            {
                new EnemyBehavior<Enemy04Entity>(1, Enemy04Entity.Behavior01)
            }),
            // state 1
            new EnemySubroutine<Enemy04Entity>(new EnemyBehavior<Enemy04Entity>[1]
            {
                new EnemyBehavior<Enemy04Entity>(1, Enemy04Entity.Behavior00)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy05Entity>> Enemy05Subroutines = new EnemySubroutine<Enemy05Entity>[2]
        {
            // state 0
            new EnemySubroutine<Enemy05Entity>(new EnemyBehavior<Enemy05Entity>[1]
            {
                new EnemyBehavior<Enemy05Entity>(1, Enemy05Entity.Behavior01)
            }),
            // state 1
            new EnemySubroutine<Enemy05Entity>(new EnemyBehavior<Enemy05Entity>[1]
            {
                new EnemyBehavior<Enemy05Entity>(1, Enemy05Entity.Behavior00)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy06Entity>> Enemy06Subroutines = new EnemySubroutine<Enemy06Entity>[5]
        {
            // state 0
            new EnemySubroutine<Enemy06Entity>(new EnemyBehavior<Enemy06Entity>[2]
            {
                new EnemyBehavior<Enemy06Entity>(1, Enemy06Entity.Behavior02),
                new EnemyBehavior<Enemy06Entity>(2, Enemy06Entity.Behavior00)
            }),
            // state 1
            new EnemySubroutine<Enemy06Entity>(new EnemyBehavior<Enemy06Entity>[1]
            {
                new EnemyBehavior<Enemy06Entity>(2, Enemy06Entity.Behavior00)
            }),
            // state 2
            new EnemySubroutine<Enemy06Entity>(new EnemyBehavior<Enemy06Entity>[2]
            {
                new EnemyBehavior<Enemy06Entity>(3, Enemy06Entity.Behavior03),
                new EnemyBehavior<Enemy06Entity>(4, Enemy06Entity.Behavior01)
            }),
            // state 3
            new EnemySubroutine<Enemy06Entity>(new EnemyBehavior<Enemy06Entity>[1]
            {
                new EnemyBehavior<Enemy06Entity>(4, Enemy06Entity.Behavior01)
            }),
            // state 4
            new EnemySubroutine<Enemy06Entity>(new EnemyBehavior<Enemy06Entity>[2]
            {
                new EnemyBehavior<Enemy06Entity>(1, Enemy06Entity.Behavior02),
                new EnemyBehavior<Enemy06Entity>(2, Enemy06Entity.Behavior00)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy10Entity>> Enemy10Subroutines = new EnemySubroutine<Enemy10Entity>[6]
        {
            // state 0
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[2]
            {
                new EnemyBehavior<Enemy10Entity>(0, Enemy10Entity.Behavior01),
                new EnemyBehavior<Enemy10Entity>(1, Enemy10Entity.Behavior03)
            }),
            // state 1
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[5]
            {
                new EnemyBehavior<Enemy10Entity>(1, Enemy10Entity.Behavior01),
                new EnemyBehavior<Enemy10Entity>(2, Enemy10Entity.Behavior06),
                new EnemyBehavior<Enemy10Entity>(4, Enemy10Entity.Behavior07),
                new EnemyBehavior<Enemy10Entity>(4, Enemy10Entity.Behavior08),
                new EnemyBehavior<Enemy10Entity>(4, Enemy10Entity.Behavior09)
            }),
            // state 2
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[1]
            {
                new EnemyBehavior<Enemy10Entity>(3, Enemy10Entity.Behavior00)
            }),
            // state 3
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[2]
            {
                new EnemyBehavior<Enemy10Entity>(4, Enemy10Entity.Behavior04),
                new EnemyBehavior<Enemy10Entity>(2, Enemy10Entity.Behavior05)
            }),
            // state 4
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[1]
            {
                new EnemyBehavior<Enemy10Entity>(0, Enemy10Entity.Behavior01)
            }),
            // state 5
            new EnemySubroutine<Enemy10Entity>(new EnemyBehavior<Enemy10Entity>[1]
            {
                new EnemyBehavior<Enemy10Entity>(1, Enemy10Entity.Behavior02)
            })
        };

        public static IReadOnlyList<Enemy10Values> Enemy10Values = new Enemy10Values[3]
        {
            // Barbed War Wasp
            new Enemy10Values()
            {
                HealthMax = 50,
                BeamDamage = 3,
                SplashDamage = 0,
                ContactDamage = 15,
                StepDistance1 = 1024,
                StepDistance2 = 819,
                StepDistance3 = 2457,
                CircleIncrement = 6144,
                Unknown18 = 0x3C001E,
                MinShots = 1,
                MaxShots = 2,
                ScanId = 215,
                Effectiveness = 0xEABA
            },
            // Red Barbed War Wasp
            new Enemy10Values()
            {
                HealthMax = 120,
                BeamDamage = 10,
                SplashDamage = 2,
                ContactDamage = 10,
                StepDistance1 = 1433,
                StepDistance2 = 1638,
                StepDistance3 = 2457,
                CircleIncrement = 6144,
                Unknown18 = 0x3C001E,
                MinShots = 1,
                MaxShots = 3,
                ScanId = 191,
                Effectiveness = 0xCEAA
            },
            // Blue Barbed War Wasp
            new Enemy10Values()
            {
                HealthMax = 120,
                BeamDamage = 8,
                SplashDamage = 0,
                ContactDamage = 10,
                StepDistance1 = 614,
                StepDistance2 = 409,
                StepDistance3 = 1024,
                CircleIncrement = 6144,
                Unknown18 = 0x3C001E,
                MinShots = 1,
                MaxShots = 1,
                ScanId = 192,
                Effectiveness = 0xF2AA
            }
        };

        public static IReadOnlyList<EnemySubroutine<Enemy11Entity>> Enemy11Subroutines = new EnemySubroutine<Enemy11Entity>[5]
        {
            // state 0
            new EnemySubroutine<Enemy11Entity>(new EnemyBehavior<Enemy11Entity>[1]
            {
                new EnemyBehavior<Enemy11Entity>(1, Enemy11Entity.Behavior04)
            }),
            // state 1
            new EnemySubroutine<Enemy11Entity>(new EnemyBehavior<Enemy11Entity>[1]
            {
                new EnemyBehavior<Enemy11Entity>(2, Enemy11Entity.Behavior03)
            }),
            // state 2
            new EnemySubroutine<Enemy11Entity>(new EnemyBehavior<Enemy11Entity>[]
            {
                new EnemyBehavior<Enemy11Entity>(3, Enemy11Entity.Behavior02)
            }),
            // state 3
            new EnemySubroutine<Enemy11Entity>(new EnemyBehavior<Enemy11Entity>[1]
            {
                new EnemyBehavior<Enemy11Entity>(4, Enemy11Entity.Behavior01)
            }),
            // state 4
            new EnemySubroutine<Enemy11Entity>(new EnemyBehavior<Enemy11Entity>[1]
            {
                new EnemyBehavior<Enemy11Entity>(0, Enemy11Entity.Behavior00)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy16Entity>> Enemy16Subroutines = new EnemySubroutine<Enemy16Entity>[4]
        {
            // state 0
            new EnemySubroutine<Enemy16Entity>(new EnemyBehavior<Enemy16Entity>[3]
            {
                new EnemyBehavior<Enemy16Entity>(1, Enemy16Entity.Behavior06),
                new EnemyBehavior<Enemy16Entity>(2, Enemy16Entity.Behavior05),
                new EnemyBehavior<Enemy16Entity>(3, Enemy16Entity.Behavior03)
            }),
            // state 1
            new EnemySubroutine<Enemy16Entity>(new EnemyBehavior<Enemy16Entity>[3]
            {
                new EnemyBehavior<Enemy16Entity>(0, Enemy16Entity.Behavior04),
                new EnemyBehavior<Enemy16Entity>(2, Enemy16Entity.Behavior05),
                new EnemyBehavior<Enemy16Entity>(3, Enemy16Entity.Behavior03)
            }),
            // state 2
            new EnemySubroutine<Enemy16Entity>(new EnemyBehavior<Enemy16Entity>[2]
            {
                new EnemyBehavior<Enemy16Entity>(0, Enemy16Entity.Behavior02),
                new EnemyBehavior<Enemy16Entity>(3, Enemy16Entity.Behavior03)
            }),
            // state 3
            new EnemySubroutine<Enemy16Entity>(new EnemyBehavior<Enemy16Entity>[2]
            {
                new EnemyBehavior<Enemy16Entity>(3, Enemy16Entity.Behavior00),
                new EnemyBehavior<Enemy16Entity>(0, Enemy16Entity.Behavior01)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy18Entity>> Enemy18Subroutines = new EnemySubroutine<Enemy18Entity>[5]
        {
            // state 0
            new EnemySubroutine<Enemy18Entity>(new EnemyBehavior<Enemy18Entity>[1]
            {
                new EnemyBehavior<Enemy18Entity>(1, Enemy18Entity.Behavior00)
            }),
            // state 1
            new EnemySubroutine<Enemy18Entity>(new EnemyBehavior<Enemy18Entity>[2]
            {
                new EnemyBehavior<Enemy18Entity>(2, Enemy18Entity.Behavior04),
                new EnemyBehavior<Enemy18Entity>(4, Enemy18Entity.Behavior02)
            }),
            // state 2
            new EnemySubroutine<Enemy18Entity>(new EnemyBehavior<Enemy18Entity>[2]
            {
                new EnemyBehavior<Enemy18Entity>(3, Enemy18Entity.Behavior05),
                new EnemyBehavior<Enemy18Entity>(4, Enemy18Entity.Behavior02)
            }),
            // state 3
            new EnemySubroutine<Enemy18Entity>(new EnemyBehavior<Enemy18Entity>[2]
            {
                new EnemyBehavior<Enemy18Entity>(4, Enemy18Entity.Behavior02),
                new EnemyBehavior<Enemy18Entity>(2, Enemy18Entity.Behavior03)
            }),
            // state 4
            new EnemySubroutine<Enemy18Entity>(new EnemyBehavior<Enemy18Entity>[1]
            {
                new EnemyBehavior<Enemy18Entity>(0, Enemy18Entity.Behavior01)
            })
        };

        public static IReadOnlyList<Enemy18Values> Enemy18Values = new Enemy18Values[3]
        {
            // Alimbic Turret v1.0
            new Enemy18Values()
            {
                HealthMax = 24,
                BeamDamage = 2,
                SplashDamage = 0,
                ContactDamage = 5,
                MinAngleY = -184320,
                MaxAngleY = 184320,
                AngleIncY = 4096,
                MinAngleX = 0,
                MaxAngleX = 245760,
                AngleIncX = 4096,
                ShotCooldown = 5,
                DelayTime = 40,
                MinShots = 1,
                MaxShots = 2,
                Unused28 = 214,
                Unused2C = 4090,
                Unused2E = 0,
                ShotOffset = 4096,
                ScanId = 219,
                Effectiveness = 0xEAAA
            },
            // Alimbic Turret v1.4
            new Enemy18Values()
            {
                HealthMax = 80,
                BeamDamage = 4,
                SplashDamage = 2,
                ContactDamage = 5,
                MinAngleY = -184320,
                MaxAngleY = 184320,
                AngleIncY = 4096,
                MinAngleX = 0,
                MaxAngleX = 245760,
                AngleIncX = 4096,
                ShotCooldown = 3,
                DelayTime = 30,
                MinShots = 3,
                MaxShots = 5,
                Unused28 = 214,
                Unused2C = 4090,
                Unused2E = 0,
                ShotOffset = 4096,
                ScanId = 196,
                Effectiveness = 0xEABA
            },
            // Alimbic Turret v2.7
            new Enemy18Values()
            {
                HealthMax = 120,
                BeamDamage = 50,
                SplashDamage = 0,
                ContactDamage = 5,
                MinAngleY = -184320,
                MaxAngleY = 184320,
                AngleIncY = 4096,
                MinAngleX = 0,
                MaxAngleX = 245760,
                AngleIncX = 4096,
                ShotCooldown = 3,
                DelayTime = 90,
                MinShots = 1,
                MaxShots = 1,
                Unused28 = 214,
                Unused2C = 4090,
                Unused2E = 0,
                ShotOffset = 4096,
                ScanId = 197,
                Effectiveness = 0xEABA
            }
        };

        public static IReadOnlyList<EnemySubroutine<Enemy23Entity>> Enemy23Subroutines = new EnemySubroutine<Enemy23Entity>[11]
        {
            // state 0
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[2]
            {
                new EnemyBehavior<Enemy23Entity>(1, Enemy23Entity.Behavior01),
                new EnemyBehavior<Enemy23Entity>(2, Enemy23Entity.Behavior07)
            }),
            // state 1
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(0, Enemy23Entity.Behavior05)
            }),
            // state 2
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[3]
            {
                new EnemyBehavior<Enemy23Entity>(3, Enemy23Entity.Behavior08),
                new EnemyBehavior<Enemy23Entity>(1, Enemy23Entity.Behavior09),
                new EnemyBehavior<Enemy23Entity>(8, Enemy23Entity.Behavior10)
            }),
            // state 3
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[4]
            {
                new EnemyBehavior<Enemy23Entity>(5, Enemy23Entity.Behavior11),
                new EnemyBehavior<Enemy23Entity>(1, Enemy23Entity.Behavior09),
                new EnemyBehavior<Enemy23Entity>(8, Enemy23Entity.Behavior10),
                new EnemyBehavior<Enemy23Entity>(8, Enemy23Entity.Behavior12)
            }),
            // state 4
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(6, Enemy23Entity.Behavior06)
            }),
            // state 5
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(4, Enemy23Entity.Behavior00)
            }),
            // state 6
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(2, Enemy23Entity.Behavior03)
            }),
            // state 7
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(1, Enemy23Entity.Behavior01)
            }),
            // state 8
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(7, Enemy23Entity.Behavior04)
            }),
            // state 9
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(10, Enemy23Entity.Behavior04)
            }),
            // state 10
            new EnemySubroutine<Enemy23Entity>(new EnemyBehavior<Enemy23Entity>[1]
            {
                new EnemyBehavior<Enemy23Entity>(1, Enemy23Entity.Behavior02)
            })
        };

        public static IReadOnlyList<Enemy23Values> Enemy23Values = new Enemy23Values[5]
        {
            // Psycho Bit v1.0
            new Enemy23Values()
            {
                HealthMax = 11,
                BeamDamage = 1,
                SplashDamage = 0,
                ContactDamage = 10,
                MinSpeedFactor1 = 409,
                MaxSpeedFactor1 = 918,
                MinSpeedFactor2 = 1638,
                MaxSpeedFactor2 = 2252,
                RangeMaxCosine = -4096,
                Unknown1C = 2457,
                Unused20 = 40960,
                DelayTime = 40,
                ShotTime = 15,
                Unused28 = 1638400,
                MinShots = 1,
                MaxShots = 2,
                DoubleSpeedSteps = 50,
                AimSteps = 4,
                SpeedSteps = 26,
                ScanId = 216,
                Effectiveness = 0xEAAA
            },
            // Psycho Bit v1.4
            new Enemy23Values()
            {
                HealthMax = 24,
                BeamDamage = 2,
                SplashDamage = 1,
                ContactDamage = 10,
                MinSpeedFactor1 = 614,
                MaxSpeedFactor1 = 1228,
                MinSpeedFactor2 = 2867,
                MaxSpeedFactor2 = 3686,
                RangeMaxCosine = -4096,
                Unknown1C = 3276,
                Unused20 = 61440,
                DelayTime = 25,
                ShotTime = 8,
                Unused28 = 1638400,
                MinShots = 1,
                MaxShots = 3,
                DoubleSpeedSteps = 60,
                AimSteps = 4,
                SpeedSteps = 30,
                ScanId = 216,
                Effectiveness = 0xEABA
            },
            // Psycho Bit v2.0
            new Enemy23Values()
            {
                HealthMax = 120,
                BeamDamage = 10,
                SplashDamage = 2,
                ContactDamage = 10,
                MinSpeedFactor1 = 614,
                MaxSpeedFactor1 = 1228,
                MinSpeedFactor2 = 2457,
                MaxSpeedFactor2 = 3276,
                RangeMaxCosine = -4096,
                Unknown1C = 4096,
                Unused20 = 40960,
                DelayTime = 25,
                ShotTime = 40,
                Unused28 = 1638400,
                MinShots = 1,
                MaxShots = 1,
                DoubleSpeedSteps = 60,
                AimSteps = 4,
                SpeedSteps = 30,
                ScanId = 208,
                Effectiveness = 0xEAF2
            },
            // Psycho Bit v3.0
            new Enemy23Values()
            {
                HealthMax = 120,
                BeamDamage = 8,
                SplashDamage = 2,
                ContactDamage = 10,
                MinSpeedFactor1 = 614,
                MaxSpeedFactor1 = 1228,
                MinSpeedFactor2 = 1638,
                MaxSpeedFactor2 = 2048,
                RangeMaxCosine = -4096,
                Unknown1C = 2048,
                Unused20 = 40960,
                DelayTime = 25,
                ShotTime = 30,
                Unused28 = 1638400,
                MinShots = 1,
                MaxShots = 2,
                DoubleSpeedSteps = 20,
                AimSteps = 30,
                SpeedSteps = 10,
                ScanId = 209,
                Effectiveness = 0xCEF9
            },
            // Psycho Bit v4.0
            new Enemy23Values()
            {
                HealthMax = 120,
                BeamDamage = 8,
                SplashDamage = 0,
                ContactDamage = 10,
                MinSpeedFactor1 = 614,
                MaxSpeedFactor1 = 1228,
                MinSpeedFactor2 = 1638,
                MaxSpeedFactor2 = 2048,
                RangeMaxCosine = -4096,
                Unknown1C = 4096,
                Unused20 = 61440,
                DelayTime = 25,
                ShotTime = 40,
                Unused28 = 1638400,
                MinShots = 1,
                MaxShots = 1,
                DoubleSpeedSteps = 20,
                AimSteps = 30,
                SpeedSteps = 10,
                ScanId = 207,
                Effectiveness = 0xF2F9
            }
        };

        public static IReadOnlyList<EnemySubroutine<Enemy35Entity>> Enemy35Subroutines = new EnemySubroutine<Enemy35Entity>[7]
        {
            // state 0
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[2]
            {
                new EnemyBehavior<Enemy35Entity>(1, Enemy35Entity.Behavior02),
                new EnemyBehavior<Enemy35Entity>(2, Enemy35Entity.Behavior06)
            }),
            // state 1
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[1]
            {
                new EnemyBehavior<Enemy35Entity>(0, Enemy35Entity.Behavior00)
            }),
            // state 2
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[1]
            {
                new EnemyBehavior<Enemy35Entity>(3, Enemy35Entity.Behavior03)
            }),
            // state 3
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[2]
            {
                new EnemyBehavior<Enemy35Entity>(4, Enemy35Entity.Behavior04),
                new EnemyBehavior<Enemy35Entity>(1, Enemy35Entity.Behavior05)
            }),
            // state 4
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[1]
            {
                new EnemyBehavior<Enemy35Entity>(5, Enemy35Entity.Behavior01)
            }),
            // state 5
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[1]
            {
                new EnemyBehavior<Enemy35Entity>(6, Enemy35Entity.Behavior00)
            }),
            // state 6
            new EnemySubroutine<Enemy35Entity>(new EnemyBehavior<Enemy35Entity>[1]
            {
                new EnemyBehavior<Enemy35Entity>(1, Enemy35Entity.Behavior02)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy36Entity>> Enemy36Subroutines = new EnemySubroutine<Enemy36Entity>[6]
        {
            // state 0
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[3]
            {
                new EnemyBehavior<Enemy36Entity>(1, Enemy36Entity.Behavior03),
                new EnemyBehavior<Enemy36Entity>(2, Enemy36Entity.Behavior05),
                new EnemyBehavior<Enemy36Entity>(1, Enemy36Entity.Behavior04)
            }),
            // state 1
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[1]
            {
                new EnemyBehavior<Enemy36Entity>(0, Enemy36Entity.Behavior00)
            }),
            // state 2
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[1]
            {
                new EnemyBehavior<Enemy36Entity>(3, Enemy36Entity.Behavior01)
            }),
            // state 3
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[1]
            {
                new EnemyBehavior<Enemy36Entity>(4, Enemy36Entity.Behavior02)
            }),
            // state 4
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[1]
            {
                new EnemyBehavior<Enemy36Entity>(5, Enemy36Entity.Behavior00)
            }),
            // state 5
            new EnemySubroutine<Enemy36Entity>(new EnemyBehavior<Enemy36Entity>[2]
            {
                new EnemyBehavior<Enemy36Entity>(1, Enemy36Entity.Behavior03),
                new EnemyBehavior<Enemy36Entity>(1, Enemy36Entity.Behavior04)
            })
        };

        public static IReadOnlyList<Enemy36Values> Enemy36Values = new Enemy36Values[5]
        {
            // Voldrum
            new Enemy36Values()
            {
                HealthMax = 55,
                BeamDamage = 2,
                SplashDamage = 0,
                ContactDamage = 7,
                MinSpeedFactor = 819,
                MaxSpeedFactor = 1433,
                DoubleSpeedSteps = 15,
                AimSteps = 10,
                DelayTime = 40,
                ShotTime = 15,
                MinShots = 1,
                MaxShots = 2,
                JumpSpeed = 819,
                RangeMaxCosine = -4096,
                SpeedSteps = 7,
                ScanId = 217,
                Effectiveness = 0xEABA
            },
            // Heavy Voldrum
            new Enemy36Values()
            {
                HealthMax = 100,
                BeamDamage = 5,
                SplashDamage = 1,
                ContactDamage = 7,
                MinSpeedFactor = 819,
                MaxSpeedFactor = 1433,
                DoubleSpeedSteps = 15,
                AimSteps = 10,
                DelayTime = 25,
                ShotTime = 8,
                MinShots = 2,
                MaxShots = 3,
                JumpSpeed = 819,
                RangeMaxCosine = -4096,
                SpeedSteps = 7,
                ScanId = 217,
                Effectiveness = 0xEABA
            },
            // Electro Voldrum
            new Enemy36Values()
            {
                HealthMax = 150,
                BeamDamage = 10,
                SplashDamage = 2,
                ContactDamage = 7,
                MinSpeedFactor = 819,
                MaxSpeedFactor = 1433,
                DoubleSpeedSteps = 15,
                AimSteps = 10,
                DelayTime = 30,
                ShotTime = 25,
                MinShots = 1,
                MaxShots = 1,
                JumpSpeed = 819,
                RangeMaxCosine = -4096,
                SpeedSteps = 7,
                ScanId = 193,
                Effectiveness = 0xEAB2
            },
            // Magma Voldrum
            new Enemy36Values()
            {
                HealthMax = 150,
                BeamDamage = 8,
                SplashDamage = 2,
                ContactDamage = 7,
                MinSpeedFactor = 1228,
                MaxSpeedFactor = 2048,
                DoubleSpeedSteps = 15,
                AimSteps = 10,
                DelayTime = 25,
                ShotTime = 20,
                MinShots = 1,
                MaxShots = 2,
                JumpSpeed = 819,
                RangeMaxCosine = -4096,
                SpeedSteps = 7,
                ScanId = 194,
                Effectiveness = 0xCEBA
            },
            // Ice Voldrum
            new Enemy36Values()
            {
                HealthMax = 152,
                BeamDamage = 8,
                SplashDamage = 0,
                ContactDamage = 7,
                MinSpeedFactor = 409,
                MaxSpeedFactor = 1024,
                DoubleSpeedSteps = 15,
                AimSteps = 10,
                DelayTime = 25,
                ShotTime = 40,
                MinShots = 1,
                MaxShots = 2,
                JumpSpeed = 819,
                RangeMaxCosine = -4096,
                SpeedSteps = 7,
                ScanId = 195,
                Effectiveness = 0xF2BA
            }
        };

        public static IReadOnlyList<EnemySubroutine<Enemy38Entity>> Enemy38Subroutines = new EnemySubroutine<Enemy38Entity>[17]
        {
            // state 0
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(1, Enemy38Entity.Behavior05)
            }),
            // state 1
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(2, Enemy38Entity.Behavior01)
            }),
            // state 2
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(4, Enemy38Entity.Behavior13)
            }),
            // state 3
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[3]
            {
                new EnemyBehavior<Enemy38Entity>(5, Enemy38Entity.Behavior14),
                new EnemyBehavior<Enemy38Entity>(6, Enemy38Entity.Behavior15),
                new EnemyBehavior<Enemy38Entity>(4, Enemy38Entity.Behavior16)
            }),
            // state 4
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(3, Enemy38Entity.Behavior03)
            }),
            // state 5
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(12, Enemy38Entity.Behavior11)
            }),
            // state 6
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(7, Enemy38Entity.Behavior10)
            }),
            // state 7
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(8, Enemy38Entity.Behavior09)
            }),
            // state 8
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(9, Enemy38Entity.Behavior08)
            }),
            // state 9
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(10, Enemy38Entity.Behavior00)
            }),
            // state 10
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(11, Enemy38Entity.Behavior06)
            }),
            // state 11
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(12, Enemy38Entity.Behavior12)
            }),
            // state 12
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[3]
            {
                new EnemyBehavior<Enemy38Entity>(15, Enemy38Entity.Behavior17),
                new EnemyBehavior<Enemy38Entity>(13, Enemy38Entity.Behavior16),
                new EnemyBehavior<Enemy38Entity>(14, Enemy38Entity.Behavior18)
            }),
            // state 13
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(12, Enemy38Entity.Behavior03)
            }),
            // state 14
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(3, Enemy38Entity.Behavior07)
            }),
            // state 15
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(16, Enemy38Entity.Behavior04)
            }),
            // state 16
            new EnemySubroutine<Enemy38Entity>(new EnemyBehavior<Enemy38Entity>[1]
            {
                new EnemyBehavior<Enemy38Entity>(0, Enemy38Entity.Behavior02)
            })
        };

        public static IReadOnlyList<EnemySubroutine<Enemy39Entity>> Enemy39Subroutines = new EnemySubroutine<Enemy39Entity>[6]
        {
            // state 0
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[1]
            {
                new EnemyBehavior<Enemy39Entity>(1, Enemy39Entity.Behavior0)
            }),
            // state 1
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[1]
            {
                new EnemyBehavior<Enemy39Entity>(2, Enemy39Entity.Behavior4)
            }),
            // state 2
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[1]
            {
                new EnemyBehavior<Enemy39Entity>(3, Enemy39Entity.Behavior3)
            }),
            // state 3
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[1]
            {
                new EnemyBehavior<Enemy39Entity>(4, Enemy39Entity.Behavior1)
            }),
            // state 4
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[2]
            {
                new EnemyBehavior<Enemy39Entity>(5, Enemy39Entity.Behavior5),
                new EnemyBehavior<Enemy39Entity>(5, Enemy39Entity.Behavior6)
            }),
            // state 5
            new EnemySubroutine<Enemy39Entity>(new EnemyBehavior<Enemy39Entity>[1]
            {
                new EnemyBehavior<Enemy39Entity>(0, Enemy39Entity.Behavior2)
            })
        };

        public static IReadOnlyList<Enemy39Values> Enemy39Values = new Enemy39Values[2]
        {
            // Fire Spawn
            new Enemy39Values()
            {
                HealthMax = 600,
                BeamDamage = 30,
                SplashDamage = 15,
                ContactDamage = 12,
                Unused8 = 600,
                AttackDelay = 0,
                AttackCountMin = 3,
                AttackCountMax = 6,
                DiveTimerMin = 1,
                DiveTimerMax = 40,
                Unused14 = 0x100010,
                Unused18 = 50,
                ScanId = 222,
                Effectiveness = 0x8955
            },
            // Arctic Spawn
            new Enemy39Values()
            {
                HealthMax = 600,
                BeamDamage = 30,
                SplashDamage = 0,
                ContactDamage = 12,
                Unused8 = 600,
                AttackDelay = 0,
                AttackCountMin = 2,
                AttackCountMax = 5,
                DiveTimerMin = 1,
                DiveTimerMax = 50,
                Unused14 = 0x100010,
                Unused18 = 50,
                ScanId = 240,
                Effectiveness = 0xB155
            }
        };

        public static string? GetEnemyModelName(EnemyType type)
        {
            int index = (int)type;
            if (index > EnemyModelNames.Count)
            {
                throw new IndexOutOfRangeException();
            }
            string name = EnemyModelNames[index];
            if (name == "")
            {
                return null;
            }
            return name;
        }

        public static readonly IReadOnlyList<string> EnemyModelNames = new string[52]
        {
            /*  0 */ "warwasp_lod0",
            /*  1 */ "zoomer",
            /*  2 */ "Temroid_lod0",
            /*  3 */ "Chomtroid",
            /*  4 */ "Chomtroid",
            /*  5 */ "Chomtroid",
            /*  6 */ "Chomtroid",
            /*  7 */ "",
            /*  8 */ "",
            /*  9 */ "",
            /* 10 */ "BarbedWarWasp",
            /* 11 */ "shriekbat",
            /* 12 */ "geemer",
            /* 13 */ "",
            /* 14 */ "",
            /* 15 */ "",
            /* 16 */ "blastcap",
            /* 17 */ "",
            /* 18 */ "Alimbic_Turret",
            /* 19 */ "CylinderBoss",
            /* 20 */ "CylinderBossEye",
            /* 21 */ "",
            /* 22 */ "",
            /* 23 */ "PsychoBit",
            /* 24 */ "Gorea1A_lod0",
            /* 25 */ "",
            /* 26 */ "",
            /* 27 */ "",
            /* 28 */ "Gorea1B_lod0",
            /* 29 */ "",
            /* 30 */ "PowerBomb",
            /* 31 */ "Gorea2_lod0",
            /* 32 */ "",
            /* 33 */ "goreaMeteor",
            /* 34 */ "PsychoBit",
            /* 35 */ "GuardBot2_lod0",
            /* 36 */ "GuardBot1",
            /* 37 */ "DripStank_lod0",
            /* 38 */ "AlimbicStatue_lod0",
            /* 39 */ "LavaDemon",
            /* 40 */ "",
            /* 41 */ "BigEyeBall",
            /* 42 */ "",
            /* 43 */ "BigEyeNest",
            /* 44 */ "",
            /* 45 */ "BigEyeTurret",
            /* 46 */ "SphinkTick_lod0",
            /* 47 */ "SphinkTick_lod0",
            /* 48 */ "",
            /* 49 */ "",
            /* 50 */ "",
            /* 51 */ ""
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
            Debug.Assert(value >= 0);
            LoadEffectiveness((uint)value, dest);
        }

        public static void LoadEffectiveness(uint value, Effectiveness[] dest)
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
        // default = 0x2AAAA = 101010101010101010 (2/normal effectiveness for everything)
        // replacements:
        // AlimbicTurretV10 - 0xEAAA - zero Omega Cannon, double Shock Coil
        // AlimbicTurretV14 - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // AlimbicTurretV27 - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // PsychoBitV10 - 0xEAAA - zero Omega Cannon, double Shock Coil
        // PsychoBitV14 - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // PsychoBitV20 - 0xEAF2 - zero Volt Driver/Omega Cannon, double Missile/Battlehammer/Shock Coil
        // PsychoBitV30 - 0xCEF9 - zero Magmaul/Omega Cannon, double Missile/Battlehammer/Judicator/Shock Coil, half Power Beam
        // PsychoBitV40 - 0xF2F9 - zero Judicator/Omega Cannon, double Missile/Battlehammer/Magmaul/Shock Coil, half Power Beam
        // Voldrum1N - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // Voldrum1H - 0xEABA - zero Omega Cannon, double Missile/Shock Coil
        // Voldrum1E - 0xEAB2 - zero Volt Driver/Omega Cannon, double Missile/Shock Coil
        // Voldrum1M - 0xCEBA - zero Magmaul/Omega Cannon, double Missile/Judicator/Shock Coil
        // Voldrum1I - 0xF2BA - zero Judicator/Omega Cannon, double Missile/Magmaul/Shock Coil
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
            /* 36 */ 0x2AABA, // Voldrum1 -- double from Missile (not used)
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
            /* 50 */ 0x2AAAA, // HitZone
            /* 51 */ 0x2AAAA  // CarnivorousPlant
        };
    }
}
