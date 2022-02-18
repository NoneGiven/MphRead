using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
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
        }
    }
}
