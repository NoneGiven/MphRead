using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Hud;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private HudObjects _hudObjects = null!;
        private HudObject _targetCircleObj = null!;
        private HudObject _sniperCircleObj = null!;
        private HudObjectInstance _targetCircleInst = null!;

        private ModelInstance _damageIndicator = null!;
        private readonly ushort[] _damageIndicatorTimers = new ushort[8];
        private readonly Node[] _damageIndicatorNodes = new Node[8];

        public void SetUpHud()
        {
            _damageIndicator = Read.GetModelInstance("damage", dir: MetaDir.Hud);
            _scene.LoadModel(_damageIndicator.Model);
            for (int i = 0; i < 8; i++)
            {
                _damageIndicatorTimers[i] = 0;
            }
            _damageIndicatorNodes[0] = _damageIndicator.Model.GetNodeByName("north")!;
            _damageIndicatorNodes[1] = _damageIndicator.Model.GetNodeByName("ne")!;
            _damageIndicatorNodes[2] = _damageIndicator.Model.GetNodeByName("east")!;
            _damageIndicatorNodes[3] = _damageIndicator.Model.GetNodeByName("se")!;
            _damageIndicatorNodes[4] = _damageIndicator.Model.GetNodeByName("south")!;
            _damageIndicatorNodes[5] = _damageIndicator.Model.GetNodeByName("sw")!;
            _damageIndicatorNodes[6] = _damageIndicator.Model.GetNodeByName("west")!;
            _damageIndicatorNodes[7] = _damageIndicator.Model.GetNodeByName("nw")!;
            _targetCircleObj = HudInfo.GetHudObject(_hudObjects.Reticle);
            _sniperCircleObj = HudInfo.GetHudObject(_hudObjects.SniperReticle);
            Debug.Assert(_sniperCircleObj.Width > _targetCircleObj.Width);
            Debug.Assert(_sniperCircleObj.Height > _targetCircleObj.Height);
            _targetCircleInst = new HudObjectInstance(_targetCircleObj.Width, _targetCircleObj.Height,
                _sniperCircleObj.Width, _sniperCircleObj.Height);
            _targetCircleInst.SetCharacterData(_targetCircleObj.CharacterData, _scene);
            _targetCircleInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
            _targetCircleInst.Center = true;
        }

        public void UpdateHud()
        {
            UpdateDamageIndicators();
            _targetCircleInst.Enabled = false;
            _damageIndicator.Active = false;
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
                    if (_timeSinceInput < (ulong)Values.GunIdleTime * 2) // todo: FPS stuff
                    {
                        UpdateReticle();
                    }
                }
                _damageIndicator.Active = true;
            }
        }

        private void UpdateDamageIndicators()
        {
            for (int i = 0; i < 8; i++)
            {
                ushort time = _damageIndicatorTimers[i];
                if (time > 0)
                {
                    time--;
                    _damageIndicatorTimers[i] = time;
                }
                _damageIndicatorNodes[i].Enabled = (time & (4 * 2)) != 0; // todo: FPS stuff
            }
        }

        private bool _smallReticle = false;
        private ushort _smallReticleTimer = 0;
        private bool _sniperReticle = false;
        private bool _hudZoom = false;

        private void HudOnFiredShot()
        {
            // todo: check scan visor
            if (!_smallReticle && !_sniperReticle)
            {
                _smallReticle = true;
                _targetCircleInst.SetAnimation(start: 0, target: 3, frames: 4);
            }
            _smallReticleTimer = 60 * 2; // todo: FPS stuff
        }

        private void ResetReticle()
        {
            _targetCircleInst.SetCharacterData(_targetCircleObj.CharacterData, _targetCircleObj.Width,
                _targetCircleObj.Height, _scene);
            _smallReticle = false;
            _smallReticleTimer = 0;
        }

        private void UpdateReticle()
        {
            if (_smallReticleTimer > 0 && !_sniperReticle)
            {
                _smallReticleTimer--;
                if (_smallReticleTimer == 0 && _smallReticle)
                {
                    // the game's animation for this gets stuck at full contraction for 4 frames,
                    // then has one frame of starting to expand, and then jumps to fully expanded
                    _targetCircleInst.SetAnimation(start: 3, target: 0, frames: 4);
                    _smallReticle = false;
                }
            }
            Matrix.ProjectPosition(_aimPosition, _scene.ViewMatrix, _scene.PerspectiveMatrix, out Vector2 pos);
            _targetCircleInst.PositionX = MathF.Round(pos.X, 5);
            _targetCircleInst.PositionY = MathF.Round(pos.Y, 5);
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
            if (beam != BeamType.Imperialist || _sniperReticle)
            {
                _sniperReticle = false;
                ResetReticle(); // todo: only do this if scan visor is off
            }
            else
            {
                _sniperReticle = true;
                _targetCircleInst.SetCharacterData(_sniperCircleObj.CharacterData, _sniperCircleObj.Width,
                    _sniperCircleObj.Height, _scene);
            }
            // todo?: set HUD object anim for top screen weapon icon
        }

        private void HudOnZoom(bool zoom)
        {
            if (_hudZoom != zoom)
            {
                _hudZoom = zoom;
                // todo: only do the rest if scan visor is off
                if (_hudZoom)
                {
                    _targetCircleInst.SetAnimation(start: 0, target: 2, frames: 2);
                }
                else
                {
                    _targetCircleInst.SetAnimation(start: 2, target: 0, frames: 2);
                }
            }
        }

        public void DrawHudObjects()
        {
            if (_targetCircleInst.Enabled)
            {
                DrawHudObject(_targetCircleInst);
            }
        }

        public void DrawHudModels()
        {
            if (_damageIndicator.Active)
            {
                _scene.DrawHudDamageModel(_damageIndicator);
            }
        }

        private void DrawHudObject(HudObjectInstance inst)
        {
            _scene.DrawHudObject(inst.PositionX, inst.PositionY, inst.Width, inst.Height, inst.BindingId, inst.Center);
        }
    }
}
