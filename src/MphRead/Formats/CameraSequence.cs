using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MphRead.Formats
{
    public class CameraSequence
    {
        public string Name { get; }
        public byte Flags { get; }
        public IReadOnlyList<CameraSequenceKeyframe> Keyframes { get; }

        public CameraSequence(string name, CameraSequenceHeader header, IReadOnlyList<CameraSequenceKeyframe> keyframes)
        {
            Name = name.Replace(".bin", "");
            Flags = header.Flags;
            Keyframes = keyframes;
        }

        public static CameraSequence Load(int id)
        {
            Debug.Assert(id > 0 && id < 172);
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
            "unit1_land_intro.bin",
            "unit2_land_intro.bin",
            "unit3_land_intro.bin",
            "unit4_land_intro.bin",
            "unit4_c1_platform_intro.bin",
            "unit2_co_scan_intro.bin",
            "unit2_co_scan_outro.bin",
            "unit2_co_bit_intro.bin",
            "unit2_c4_teleporter_intro.bin",
            "unit2_co_bit_outro.bin",
            "unit2_co_helm_flyby.bin",
            "unit2_rm1_artifact_intro.bin",
            "unit2_rm1_artifact_outro.bin",
            "unit2_c4_artifact_intro.bin",
            "unit2_c4_artifact_outro.bin",
            "unit2_rm2_kanden_intro.bin",
            "unit2_rm3_artifact_intro.bin",
            "unit2_rm3_artifact_outro.bin",
            "unit2_rm3_kanden_intro.bin",
            "unit4_co_morphballmaze.bin",
            "unit2_b1_octolith_intro.bin",
            "unit2_co_guardian_intro.bin",
            "unit2_rm3_kanden_outro.bin",
            "unit4_rm1_morphballjumps1.bin",
            "unit4_land_guardian_intro.bin",
            "unit4_rm3_scandoor_unlock.bin",
            "unit1_c4_dropmaze_left.bin",
            "unit4_co_morphballmaze_enter.bin",
            "unit4_rm5_arcticspawn_intro.bin",
            "unit4_rm5_arcticspawn_outro.bin",
            "unit1_c4_dropmaze_right.bin",
            "unit4_rm3_hunters_intro.bin",
            "unit4_rm3_hunters_outro.bin",
            "unit4_rm2_switch_intro.bin",
            "unit4_rm2_guardian_intro.bin",
            "unit1_c5_pistonmaze_1.bin",
            "unit1_c5_pistonmaze_2.bin",
            "unit1_c5_pistonmaze_3.bin",
            "unit1_c5_pistonmaze_4.bin",
            "unit4_rm2_guardian_outro.bin",
            "unit3_c2_morphballmaze.bin",
            "unit4_rm5_powerdown.bin",
            "unit4_rm1_morphballjumps2.bin",
            "unit4_rm2_elevator_intro.bin",
            "unit4_rm1_morphballjumps3.bin",
            "unit4_rm5_pillarcrash.bin",
            "unit4_rm1_wasp_intro.bin",
            "unit1_RM1_spire_intro_layer0.bin",
            "unit1_RM1_spire_intro_layer3.bin",
            "unit1_RM1_spire_outro.bin",
            "unit1_RM6_spire_intro_layer3.bin",
            "unit1_c1_shipflyby.bin",
            "unit4_rm3_trace_intro.bin",
            "unit3_rm1_forcefield_unlock.bin",
            "unit3_rm1_ship_battle_end.bin",
            "unit3_rm2_evac_intro.bin",
            "unit3_rm2_evac_fail.bin",
            "unit4_rm1_puzzle_activate.bin",
            "unit4_rm1_artifact_intro.bin",
            "unit1_c0_weavel_intro.bin",
            "bigeye_octolith_intro.bin",
            "unit2_rm4_panel_open_1.bin",
            "unit2_rm4_panel_open_2.bin",
            "unit2_rm4_panel_open_3.bin",
            "unit2_rm4_cntlroom_open.bin",
            "unit2_rm4_teleporter_active.bin",
            "unit2_rm6_teleporter_active.bin",
            "unit1_rm2_rm3door_open.bin",
            "unit1_rm2_c3door_open.bin",
            "unit1_rm3_lavademon_intro.bin",
            "unit1_rm3_magmaul_intro.bin",
            "unit3_rm3_race1.bin",
            "unit3_rm3_race1_fail.bin",
            "unit3_rm3_race2.bin",
            "unit3_rm3_race2_fail.bin",
            "unit3_rm3_incubator_malfunction_intro.bin",
            "unit3_rm3_incubator_malfunction_outro.bin",
            "unit3_rm3_door_unlock.bin",
            "unit1_rm3_forcefield_unlock.bin",
            "unit4_rm4_sniperspot_intro.bin",
            "unit4_rm5_artifact_key_intro.bin",
            "unit4_rm5_artifact_intro.bin",
            "unit3_rm2_door_unlock.bin",
            "unit3_rm2_evac_end.bin",
            "unit1_rm3_forcefield_unlock.bin",
            "unit1_c0_weavel_outro.bin",
            "unit3_rm1_sylux_preship.bin",
            "unit1_rm1_mover_activate_layer3.bin",
            "unit3_rm1_sylux_intro.bin",
            "unit4_rm3_trace_outro.bin",
            "unit4_rm5_sniper_intro.bin",
            "unit3_rm1_artifact_intro.bin",
            "unit1_rm6_spire_escape.bin",
            "unit1_crystalroom_octolith.bin",
            "unit4_co_morphballmaze_exit.bin",
            "unit4_rm3_key_intro.bin",
            "unit2_rm1_door_lock.bin",
            "unit2_rm1_key_intro.bin",
            "unit3_rm4_morphball.bin",
            "unit1_c0_morphball_door_unlock.bin",
            "unit1_rm6_forcefield_lock.bin",
            "unit1_rm6_forcefield_unlock.bin",
            "unit1_land_cockpit.bin",
            "unit2_land_cockpit.bin",
            "unit3_land_cockpit.bin",
            "unit4_land_cockpit.bin",
            "unit1_land_cockpit.bin",
            "unit1_rm1_artifact_intro.bin",
            "unit2_rm5_artifact_intro.bin",
            "unit2_c7_forcefield_lock.bin",
            "unit2_c7_forcefield_unlock.bin",
            "unit2_c7_artifact_intro.bin",
            "unit2_rm8_artifact_intro.bin",
            "unit4_co_door_unlock.bin",
            "unit1_land_cockpit_land.bin",
            "unit1_land_cockpit_takeoff.bin",
            "unit2_land_cockpit_land.bin",
            "unit2_land_cockpit_takeoff.bin",
            "unit3_land_cockpit_land.bin",
            "unit3_land_cockpit_takeoff.bin",
            "unit4_land_cockpit_land.bin",
            "unit4_land_cockpit_takeoff.bin",
            "unit1_land_cockpit_land.bin",
            "unit1_land_cockpit_takeoff.bin",
            "unit1_rm2_mover1_activate.bin",
            "unit1_rm2_mover2_activate.bin",
            "unit1_rm2_mover3_activate.bin",
            "unit3_rm3_race_artifact_intro.bin",
            "unit1_c5_artifact_intro.bin",
            "unit1_rm3_key_intro.bin",
            "unit1_rm3_artifact_intro.bin",
            "unit1_c3_artifact_intro.bin",
            "unit4_rm1_forcefield_unlock.bin",
            "unit4_rm1_wasp_outro.bin",
            "unit4_rm4_artifact_intro.bin",
            "unit4_rm4_artifact_outro.bin",
            "unit3_rm2_artifact_intro.bin",
            "unit3_rm1_ship_intro.bin",
            "bigeye1_intro.bin",
            "unit4_rm2_key_intro.bin",
            "unit2_rm1_bit_intro.bin",
            "unit1_rm1_spire_escape.bin",
            "bigeye_morphball.bin",
            "unit3_rm3_key_outro.bin",
            "unit3_rm4_key_intro.bin",
            "unit3_rm4_key_outro.bin",
            "unit1_c0_key_intro.bin",
            "unit1_rm6_key_intro.bin",
            "unit1_rm6_morphball.bin",
            "unit3_rm1_bottomfloorkey_intro.bin",
            "unit4_rm4_key_intro.bin",
            "unit4_rm4_guardian_outro.bin",
            "unit4_rm5_forcefield_outro.bin",
            "unit4_rm5_quadtroid_outro.bin",
            "unit4_rm2_key_outro.bin",
            "unit3_rm4_guardian_intro.bin",
            "unit3_rm4_morphballdoor_unlock.bin",
            "unit2_rm5_key_intro.bin",
            "unit3_rm4_item_intro.bin",
            "unit2_c7_key_intro.bin",
            "unit2_rm4_forcefield_unlock_1.bin",
            "unit2_rm4_forcefield_unlock_2.bin",
            "unit2_rm4_forcefield_unlock_3.bin",
            "unit3_c2_battlehammer_intro.bin",
            "unit4_rm3_morphball_cam.bin",
            "unit4_rm1_door_open.bin",
            "gorea_b2_gun_intro.bin",
            "gorea_land_intro.bin",
            "gorea_land_cockpit.bin",
            "gorea_land_cockpit_land.bin",
            "gorea_land_cockpit_takeoff.bin",
            "unit4_rm1_puzzle_intro.bin"
        };
    }
}
