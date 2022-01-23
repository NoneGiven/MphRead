using MphRead.Formats;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
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
                }
            }
        }
    }
}
