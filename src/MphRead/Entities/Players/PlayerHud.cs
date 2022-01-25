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
            _targetCircleInst.SetCharacterData(targetCircle.CharacterData, index: 0, _scene);
            _targetCircleInst.SetPaletteData(targetCircle.PaletteData, index: 0, _scene);
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

        private void UpdateReticle()
        {
            // sktodo: test input time or whatever, expand/contract animation
            Matrix.ProjectPosition(_aimPosition, _scene.ViewMatrix, _scene.PerspectiveMatrix, out Vector2 pos);
            // sktodo: center
            _targetCircleInst.PositionX = pos.X;
            _targetCircleInst.PositionY = pos.Y;
            _targetCircleInst.Enabled = true;
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
