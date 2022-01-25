using MphRead.Formats;
using MphRead.Hud;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private HudObjectInstance _targetCircleInst = null!;

        // sktodo: sniper circle
        public void SetUpHud()
        {
            HudObject targetCircle = HudInfo.GetHudObject("_archives/localSamus/hud_targetcircle.bin");
            HudObject sniperCircle = HudInfo.GetHudObject("_archives/localSamus/hud_snipercircle.bin");
            _targetCircleInst = new HudObjectInstance(targetCircle.Width, targetCircle.Height);
            _targetCircleInst.SetCharacterData(targetCircle.CharacterData, _scene);
            _targetCircleInst.SetPaletteData(targetCircle.PaletteData, _scene);
            _targetCircleInst.Center = true;
        }

        public void UpdateHud()
        {
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                _scene.Layer1BindingId = -1;
                _scene.Layer2BindingId = -1;
                _scene.Layer3BindingId = -1;
                return;
            }
            // todo: lots more stuff
            _targetCircleInst.Enabled = false;
            if (_health > 0)
            {
                if (IsAltForm || IsMorphing || IsUnmorphing)
                {
                    _scene.Layer1BindingId = -1;
                    _scene.Layer2BindingId = -1;
                    _scene.Layer3BindingId = -1;
                }
                else
                {
                    _scene.Layer3BindingId = _drawIceLayer ? _scene.IceLayerBindingId : -1;
                    UpdateReticle();
                }
            }
        }

        private bool _smallReticle = false;
        private ushort _smallReticleTimer = 0;

        private void HudOnFiredShot()
        {
            // todo: check scan visor
            // sktodo: check snipercircle
            if (!_smallReticle)
            {
                _smallReticle = true;
                _targetCircleInst.SetAnimation(start: 0, target: 3, frames: 4);
            }
            _smallReticleTimer = 60 * 2; // todo: FPS stuff
        }

        private void ResetReticle()
        {
            _targetCircleInst.SetIndex(0, _scene);
            _smallReticle = false;
            _smallReticleTimer = 0;
        }

        private void UpdateReticle()
        {
            // sktodo: reset this when spawning, morphing, etc.
            if (_smallReticleTimer > 0)
            {
                // sktodo: check snipercircle
                _smallReticleTimer--;
                if (_smallReticleTimer == 0 && _smallReticle)
                {
                    // the game's animation for this gets stuck at full contraction for 4 frames,
                    // then has one frame of starting to expand, and then jumps to fully expanded
                    _targetCircleInst.SetAnimation(start: 3, target: 0, frames: 4);
                    _smallReticle = false;
                }
            }
            // sktodo: test input time or whatever
            Matrix.ProjectPosition(_aimPosition, _scene.ViewMatrix, _scene.PerspectiveMatrix, out Vector2 pos);
            _targetCircleInst.PositionX = pos.X;
            _targetCircleInst.PositionY = pos.Y;
            _targetCircleInst.Enabled = true;
            _targetCircleInst.ProcessAnimation(_scene);
        }

        private void HudOnMorphStart(bool teleported)
        {
            _targetCircleInst.SetIndex(0, _scene);
            // todo: turn off scan visor, possibly other stuff (if it's not just touch screen updates)
        }

        private void HudOnWeaponSwitch(BeamType beam)
        {
            if (beam != BeamType.Imperialist) // sktodo: or snipercircle is on
            {
                // sktodo: turn snipercircle off
                ResetReticle(); // todo: only do this if scan visor is off
            }
            else
            {
                // sktodo: turn snipercircle on, etc.
            }
            // todo?: set HUD object anim for top screen weapon icon
        }

        public void DrawObjects()
        {
            if (_targetCircleInst.Enabled)
            {
                DrawHudObject(_targetCircleInst);
            }
        }

        private void DrawHudObject(HudObjectInstance inst)
        {
            _scene.DrawHudObject(inst.PositionX, inst.PositionY, inst.Width, inst.Height, inst.BindingId, inst.Center);
        }
    }
}
