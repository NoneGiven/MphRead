using System;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        public static int[,] ScanIds { get; } = new int[8, 4]
        {
            { 0, 0, 0, 0 },
            { 232, 233, 532, 233 },
            { 236, 237, 534, 237 },
            { 230, 231, 531, 231 },
            { 228, 229, 530, 229 },
            { 234, 235, 533, 235 },
            { 238, 239, 535, 239 },
            { 225, 225, 224, 225 }
        };

        private bool _silentVisorSwitch = false; // set by dialogs
        private float _visorNameTime = 30 / 30f;
        private float _visorNameTimer = 30 / 30f;
        private int _visorNameId = 0;
        private bool _scanning = false;

        public void SetCombatVisor()
        {
            if (!_scene.Multiplayer && ScanVisor)
            {
                SwitchVisors(reset: false);
            }
        }

        public void ResetCombatVisor()
        {
            if (!_scene.Multiplayer && ScanVisor)
            {
                SwitchVisors(reset: true);
            }
        }

        private void SwitchVisors(bool reset)
        {
            _visorNameTime = 30 / 30f;
            _visorNameTimer = 30 / 30f;
            // scantodo: SFX
            if (ScanVisor)
            {
                // todo?: flash radar lights if not reset
                if (!reset && !_silentVisorSwitch)
                {
                    _soundSource.PlayFreeSfx(SfxId.SCAN_VISOR_OFF);
                }
                ScanVisor = false;
                _visorNameId = 108; // COMBAT VISOR
                _scanning = false;
                _smallReticle = false;
                _smallReticleTimer = 0;
                ResetScanValues();
            }
            else if (!reset)
            {
                // scantodo: SFX
                if (!_silentVisorSwitch)
                {
                    // todo?: flash radar lights
                    _soundSource.PlayFreeSfx(SfxId.SCAN_VISOR_ON2);
                    ScanVisor = true;
                    _visorNameId = 107; // SCAN VISOR
                    _scanning = false;
                    _smallReticle = false;
                    _smallReticleTimer = 0;
                }
            }
        }

        private void ResetScanValues()
        {
            // sktodo: init values and SFX
            _curScanTarget.Entity = null;
            _curScanTarget.CenterDist = Single.MaxValue;
        }

        // sktodo: call this and call the draw method (will need to draw the objects we update here, not the particles)
        private void UpdateScanHud()
        {
            EntityBase? curEnt = _curScanTarget.Entity;
            float minCenter = Single.MaxValue;
            int targetCount = 0;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {

            }
        }

        private readonly ScanTarget _curScanTarget = new ScanTarget();

        private class ScanTarget
        {
            public EntityBase? Entity { get; set; }
            public float CenterDist { get; set; } = Single.MaxValue;
            public int Category { get; set; }
            public Vector3 Position { get; set; }
            public float ScreenX { get; set; }
            public float ScreenY { get; set; }
            public float Scale { get; set; }
            public bool Dim { get; set; }
        }
    }
}
