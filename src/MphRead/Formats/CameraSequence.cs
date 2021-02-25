using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MphRead.Formats
{
    public class CameraSequence
    {
        public string Name { get; }
        public byte Version { get; }
        public IReadOnlyList<CameraSequenceKeyframe> Keyframes { get; }

        public CameraSequence(string name, CameraSequenceHeader header, IReadOnlyList<CameraSequenceKeyframe> keyframes)
        {
            Name = name.Replace(".bin", "");
            Version = header.Version;
            Keyframes = keyframes;
        }

        public static CameraSequence Load(int id)
        {
            Debug.Assert(id >= 0 && id < 172);
            return Load(_filenames[id]);
        }

        public static CameraSequence Load(string name)
        {
            string path = Path.Combine(Paths.FileSystem, "cameraEditor", name);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            CameraSequenceHeader header = Read.ReadStruct<CameraSequenceHeader>(bytes);
            Debug.Assert(header.Padding3 == 0);
            Debug.Assert(header.Padding4 == 0);
            return new CameraSequence(name, header, Read.DoOffsets<CameraSequenceKeyframe>(bytes, Sizes.CameraSequenceHeader, header.Count));
        }

        private static readonly IReadOnlyList<string> _filenames = new List<string>()
        {
            /*   0 */ "unit1_land_intro.bin",
            /*   1 */ "unit2_land_intro.bin",
            /*   2 */ "unit3_land_intro.bin",
            /*   3 */ "unit4_land_intro.bin",
            /*   4 */ "unit4_c1_platform_intro.bin",
            /*   5 */ "unit2_co_scan_intro.bin",
            /*   6 */ "unit2_co_scan_outro.bin",
            /*   7 */ "unit2_co_bit_intro.bin",
            /*   8 */ "unit2_c4_teleporter_intro.bin",
            /*   9 */ "unit2_co_bit_outro.bin",
            /*  10 */ "unit2_co_helm_flyby.bin",
            /*  11 */ "unit2_rm1_artifact_intro.bin",
            /*  12 */ "unit2_rm1_artifact_outro.bin",
            /*  13 */ "unit2_c4_artifact_intro.bin",
            /*  14 */ "unit2_c4_artifact_outro.bin",
            /*  15 */ "unit2_rm2_kanden_intro.bin",
            /*  16 */ "unit2_rm3_artifact_intro.bin",
            /*  17 */ "unit2_rm3_artifact_outro.bin",
            /*  18 */ "unit2_rm3_kanden_intro.bin",
            /*  19 */ "unit4_co_morphballmaze.bin",
            /*  20 */ "unit2_b1_octolith_intro.bin",
            /*  21 */ "unit2_co_guardian_intro.bin",
            /*  22 */ "unit2_rm3_kanden_outro.bin",
            /*  23 */ "unit4_rm1_morphballjumps1.bin",
            /*  24 */ "unit4_land_guardian_intro.bin",
            /*  25 */ "unit4_rm3_scandoor_unlock.bin",
            /*  26 */ "unit1_c4_dropmaze_left.bin",
            /*  27 */ "unit4_co_morphballmaze_enter.bin",
            /*  28 */ "unit4_rm5_arcticspawn_intro.bin",
            /*  29 */ "unit4_rm5_arcticspawn_outro.bin",
            /*  30 */ "unit1_c4_dropmaze_right.bin",
            /*  31 */ "unit4_rm3_hunters_intro.bin",
            /*  32 */ "unit4_rm3_hunters_outro.bin",
            /*  33 */ "unit4_rm2_switch_intro.bin",
            /*  34 */ "unit4_rm2_guardian_intro.bin",
            /*  35 */ "unit1_c5_pistonmaze_1.bin",
            /*  36 */ "unit1_c5_pistonmaze_2.bin",
            /*  37 */ "unit1_c5_pistonmaze_3.bin",
            /*  38 */ "unit1_c5_pistonmaze_4.bin",
            /*  39 */ "unit4_rm2_guardian_outro.bin",
            /*  40 */ "unit3_c2_morphballmaze.bin",
            /*  41 */ "unit4_rm5_powerdown.bin",
            /*  42 */ "unit4_rm1_morphballjumps2.bin",
            /*  43 */ "unit4_rm2_elevator_intro.bin",
            /*  44 */ "unit4_rm1_morphballjumps3.bin",
            /*  45 */ "unit4_rm5_pillarcrash.bin",
            /*  46 */ "unit4_rm1_wasp_intro.bin",
            /*  47 */ "unit1_RM1_spire_intro_layer0.bin",
            /*  48 */ "unit1_RM1_spire_intro_layer3.bin",
            /*  49 */ "unit1_RM1_spire_outro.bin",
            /*  50 */ "unit1_RM6_spire_intro_layer3.bin",
            /*  51 */ "unit1_c1_shipflyby.bin",
            /*  52 */ "unit4_rm3_trace_intro.bin",
            /*  53 */ "unit3_rm1_forcefield_unlock.bin",
            /*  54 */ "unit3_rm1_ship_battle_end.bin",
            /*  55 */ "unit3_rm2_evac_intro.bin",
            /*  56 */ "unit3_rm2_evac_fail.bin",
            /*  57 */ "unit4_rm1_puzzle_activate.bin",
            /*  58 */ "unit4_rm1_artifact_intro.bin",
            /*  59 */ "unit1_c0_weavel_intro.bin",
            /*  60 */ "bigeye_octolith_intro.bin",
            /*  61 */ "unit2_rm4_panel_open_1.bin",
            /*  62 */ "unit2_rm4_panel_open_2.bin",
            /*  63 */ "unit2_rm4_panel_open_3.bin",
            /*  64 */ "unit2_rm4_cntlroom_open.bin",
            /*  65 */ "unit2_rm4_teleporter_active.bin",
            /*  66 */ "unit2_rm6_teleporter_active.bin",
            /*  67 */ "unit1_rm2_rm3door_open.bin",
            /*  68 */ "unit1_rm2_c3door_open.bin",
            /*  69 */ "unit1_rm3_lavademon_intro.bin",
            /*  70 */ "unit1_rm3_magmaul_intro.bin",
            /*  71 */ "unit3_rm3_race1.bin",
            /*  72 */ "unit3_rm3_race1_fail.bin",
            /*  73 */ "unit3_rm3_race2.bin",
            /*  74 */ "unit3_rm3_race2_fail.bin",
            /*  75 */ "unit3_rm3_incubator_malfunction_intro.bin",
            /*  76 */ "unit3_rm3_incubator_malfunction_outro.bin",
            /*  77 */ "unit3_rm3_door_unlock.bin",
            /*  78 */ "unit1_rm3_forcefield_unlock.bin",
            /*  79 */ "unit4_rm4_sniperspot_intro.bin",
            /*  80 */ "unit4_rm5_artifact_key_intro.bin",
            /*  81 */ "unit4_rm5_artifact_intro.bin",
            /*  82 */ "unit3_rm2_door_unlock.bin",
            /*  83 */ "unit3_rm2_evac_end.bin",
            /*  84 */ "unit1_rm3_forcefield_unlock.bin",
            /*  85 */ "unit1_c0_weavel_outro.bin",
            /*  86 */ "unit3_rm1_sylux_preship.bin",
            /*  87 */ "unit1_rm1_mover_activate_layer3.bin",
            /*  88 */ "unit3_rm1_sylux_intro.bin",
            /*  89 */ "unit4_rm3_trace_outro.bin",
            /*  90 */ "unit4_rm5_sniper_intro.bin",
            /*  91 */ "unit3_rm1_artifact_intro.bin",
            /*  92 */ "unit1_rm6_spire_escape.bin",
            /*  93 */ "unit1_crystalroom_octolith.bin",
            /*  94 */ "unit4_co_morphballmaze_exit.bin",
            /*  95 */ "unit4_rm3_key_intro.bin",
            /*  96 */ "unit2_rm1_door_lock.bin",
            /*  97 */ "unit2_rm1_key_intro.bin",
            /*  98 */ "unit3_rm4_morphball.bin",
            /*  99 */ "unit1_c0_morphball_door_unlock.bin",
            /* 100 */ "unit1_rm6_forcefield_lock.bin",
            /* 101 */ "unit1_rm6_forcefield_unlock.bin",
            /* 102 */ "unit1_land_cockpit.bin",
            /* 103 */ "unit2_land_cockpit.bin",
            /* 104 */ "unit3_land_cockpit.bin",
            /* 105 */ "unit4_land_cockpit.bin",
            /* 106 */ "unit1_land_cockpit.bin",
            /* 107 */ "unit1_rm1_artifact_intro.bin",
            /* 108 */ "unit2_rm5_artifact_intro.bin",
            /* 109 */ "unit2_c7_forcefield_lock.bin",
            /* 110 */ "unit2_c7_forcefield_unlock.bin",
            /* 111 */ "unit2_c7_artifact_intro.bin",
            /* 112 */ "unit2_rm8_artifact_intro.bin",
            /* 113 */ "unit4_co_door_unlock.bin",
            /* 114 */ "unit1_land_cockpit_land.bin",
            /* 115 */ "unit1_land_cockpit_takeoff.bin",
            /* 116 */ "unit2_land_cockpit_land.bin",
            /* 117 */ "unit2_land_cockpit_takeoff.bin",
            /* 118 */ "unit3_land_cockpit_land.bin",
            /* 119 */ "unit3_land_cockpit_takeoff.bin",
            /* 120 */ "unit4_land_cockpit_land.bin",
            /* 121 */ "unit4_land_cockpit_takeoff.bin",
            /* 122 */ "unit1_land_cockpit_land.bin",
            /* 123 */ "unit1_land_cockpit_takeoff.bin",
            /* 124 */ "unit1_rm2_mover1_activate.bin",
            /* 125 */ "unit1_rm2_mover2_activate.bin",
            /* 126 */ "unit1_rm2_mover3_activate.bin",
            /* 127 */ "unit3_rm3_race_artifact_intro.bin",
            /* 128 */ "unit1_c5_artifact_intro.bin",
            /* 129 */ "unit1_rm3_key_intro.bin",
            /* 130 */ "unit1_rm3_artifact_intro.bin",
            /* 131 */ "unit1_c3_artifact_intro.bin",
            /* 132 */ "unit4_rm1_forcefield_unlock.bin",
            /* 133 */ "unit4_rm1_wasp_outro.bin",
            /* 134 */ "unit4_rm4_artifact_intro.bin",
            /* 135 */ "unit4_rm4_artifact_outro.bin",
            /* 136 */ "unit3_rm2_artifact_intro.bin",
            /* 137 */ "unit3_rm1_ship_intro.bin",
            /* 138 */ "bigeye1_intro.bin",
            /* 139 */ "unit4_rm2_key_intro.bin",
            /* 140 */ "unit2_rm1_bit_intro.bin",
            /* 141 */ "unit1_rm1_spire_escape.bin",
            /* 142 */ "bigeye_morphball.bin",
            /* 143 */ "unit3_rm3_key_outro.bin",
            /* 144 */ "unit3_rm4_key_intro.bin",
            /* 145 */ "unit3_rm4_key_outro.bin",
            /* 146 */ "unit1_c0_key_intro.bin",
            /* 147 */ "unit1_rm6_key_intro.bin",
            /* 148 */ "unit1_rm6_morphball.bin",
            /* 149 */ "unit3_rm1_bottomfloorkey_intro.bin",
            /* 150 */ "unit4_rm4_key_intro.bin",
            /* 151 */ "unit4_rm4_guardian_outro.bin",
            /* 152 */ "unit4_rm5_forcefield_outro.bin",
            /* 153 */ "unit4_rm5_quadtroid_outro.bin",
            /* 154 */ "unit4_rm2_key_outro.bin",
            /* 155 */ "unit3_rm4_guardian_intro.bin",
            /* 156 */ "unit3_rm4_morphballdoor_unlock.bin",
            /* 157 */ "unit2_rm5_key_intro.bin",
            /* 158 */ "unit3_rm4_item_intro.bin",
            /* 159 */ "unit2_c7_key_intro.bin",
            /* 160 */ "unit2_rm4_forcefield_unlock_1.bin",
            /* 161 */ "unit2_rm4_forcefield_unlock_2.bin",
            /* 162 */ "unit2_rm4_forcefield_unlock_3.bin",
            /* 163 */ "unit3_c2_battlehammer_intro.bin",
            /* 164 */ "unit4_rm3_morphball_cam.bin",
            /* 165 */ "unit4_rm1_door_open.bin",
            /* 166 */ "gorea_b2_gun_intro.bin",
            /* 167 */ "gorea_land_intro.bin",
            /* 168 */ "gorea_land_cockpit.bin",
            /* 169 */ "gorea_land_cockpit_land.bin",
            /* 170 */ "gorea_land_cockpit_takeoff.bin",
            /* 171 */ "unit4_rm1_puzzle_intro.bin"
        };
    }
}
