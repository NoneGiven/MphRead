using System;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Hud;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private HudObjects _hudObjects = null!;
        private HudObject _targetCircleObj = null!;
        private HudObject _sniperCircleObj = null!;
        private HudObjectInstance _targetCircleInst = null!;
        private HudObject _weaponSelectObj = null!;
        private HudObject _selectBoxObj = null!;
        private readonly HudObjectInstance[] _weaponSelectInsts = new HudObjectInstance[6];
        private readonly HudObjectInstance[] _selectBoxInsts = new HudObjectInstance[6];
        private HudObjectInstance _textInst = null!;

        private HudObject _healthbarMain = null!;
        private HudObject _healthbarSub = null!;
        private HudObject? _healthbarTank = null;
        private HudMeter _healthbarMainMeter = null!;
        private HudMeter _healthbarSubMeter = null!;

        private ModelInstance _damageIndicator = null!;
        private readonly ushort[] _damageIndicatorTimers = new ushort[8];
        private readonly Node[] _damageIndicatorNodes = new Node[8];

        private ModelInstance _filterModel = null!;

        public void SetUpHud()
        {
            _filterModel = Read.GetModelInstance("filter");
            _scene.LoadModel(_filterModel.Model);
            _damageIndicator = Read.GetModelInstance("damage", dir: MetaDir.Hud);
            _scene.LoadModel(_damageIndicator.Model);
            _damageIndicator.Active = false;
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
            Debug.Assert(_sniperCircleObj.Width >= _targetCircleObj.Width);
            Debug.Assert(_sniperCircleObj.Height >= _targetCircleObj.Height);
            _targetCircleInst = new HudObjectInstance(_targetCircleObj.Width, _targetCircleObj.Height,
                _sniperCircleObj.Width, _sniperCircleObj.Height);
            _targetCircleInst.SetCharacterData(_targetCircleObj.CharacterData, _scene);
            _targetCircleInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
            _targetCircleInst.Center = true;
            _weaponSelectObj = HudInfo.GetHudObject(_hudObjects.WeaponSelect);
            _selectBoxObj = HudInfo.GetHudObject(_hudObjects.SelectBox);
            // todo: left-handed mode
            var positions = new Vector2[6]
            {
                new Vector2(201 / 256f, 156 / 192f),
                new Vector2(161 / 256f, 152 / 192f),
                new Vector2(122 / 256f, 142 / 192f),
                new Vector2(90 / 256f, 109 / 192f),
                new Vector2(81 / 256f, 70 / 192f),
                new Vector2(77 / 256f, 32 / 192f)
            };
            HudObject iconInst = HudInfo.GetHudObject(_hudObjects.SelectIcon);
            for (int i = 0; i < 6; i++)
            {
                var weaponInst = new HudObjectInstance(_weaponSelectObj.Width, _weaponSelectObj.Height);
                int frame = i == 0 ? 1 : i + 2;
                weaponInst.SetCharacterData(_weaponSelectObj.CharacterData, frame, _scene);
                weaponInst.SetPaletteData(_weaponSelectObj.PaletteData, _scene);
                weaponInst.Alpha = 0.722f;
                var boxInst = new HudObjectInstance(_selectBoxObj.Width, _selectBoxObj.Height);
                boxInst.SetCharacterData(_selectBoxObj.CharacterData, _scene);
                boxInst.SetPaletteData(iconInst.PaletteData, _scene);
                boxInst.Enabled = true;
                Vector2 position = positions[i];
                weaponInst.PositionX = position.X;
                weaponInst.PositionY = position.Y;
                boxInst.PositionX = position.X;
                boxInst.PositionY = position.Y;
                _weaponSelectInsts[i] = weaponInst;
                _selectBoxInsts[i] = boxInst;
            }
            _healthbarMain = HudInfo.GetHudObject(_hudObjects.HealthBarA);
            _healthbarSub = HudInfo.GetHudObject(_hudObjects.HealthBarB);
            _healthbarMainMeter = HudElements.MainHealthbars[(int)Hunter];
            _healthbarSubMeter = HudElements.SubHealthbars[(int)Hunter];
            _healthbarMainMeter.BarInst = new HudObjectInstance(_healthbarMain.Width, _healthbarMain.Height);
            _healthbarMainMeter.BarInst.SetCharacterData(_healthbarMain.CharacterData, _scene);
            _healthbarMainMeter.BarInst.SetPaletteData(_healthbarMain.PaletteData, _scene);
            _healthbarMainMeter.BarInst.Enabled = true;
            _healthbarSubMeter.BarInst = new HudObjectInstance(_healthbarSub.Width, _healthbarSub.Height);
            _healthbarSubMeter.BarInst.SetCharacterData(_healthbarSub.CharacterData, _scene);
            _healthbarSubMeter.BarInst.SetPaletteData(_healthbarSub.PaletteData, _scene);
            _healthbarSubMeter.BarInst.Enabled = true;
            // todo: MP1P/other hunters
            if (!_scene.Multiplayer && _hudObjects.EnergyTanks != null)
            {
                _healthbarTank = HudInfo.GetHudObject(_hudObjects.EnergyTanks);
                _healthbarMainMeter.TankInst = new HudObjectInstance(_healthbarTank.Width, _healthbarTank.Height);
                _healthbarMainMeter.TankInst.SetCharacterData(_healthbarTank.CharacterData, _scene);
                if (Hunter == Hunter.Samus || Hunter == Hunter.Guardian)
                {
                    _healthbarMainMeter.TankInst.SetPaletteData(_healthbarTank.PaletteData, _scene);
                }
                else
                {
                    _healthbarMainMeter.TankInst.SetPaletteData(_healthbarMain.PaletteData, _scene);
                }
                _healthbarMainMeter.TankInst.Enabled = true;
            }
            _healthbarYOffset = _hudObjects.HealthOffsetY;
            _textInst = new HudObjectInstance(width: 8, height: 8); // todo: max is 16x16
            _textInst.SetCharacterData(Font.CharacterData, _scene);
            _textInst.SetPaletteData(_healthbarMain.PaletteData, _scene); // sktodo
            _textInst.Enabled = true;
        }

        public void UpdateHud()
        {
            UpdateHealthbars();
            UpdateDamageIndicators();
            UpdateDisruptedState();
            WeaponSelection = CurrentWeapon;
            if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                UpdateWeaponSelect();
            }
            _targetCircleInst.Enabled = false;
            _damageIndicator.Active = false;
            _scene.Layer1BindingId = -1;
            _scene.Layer2BindingId = -1;
            _scene.Layer3BindingId = -1;
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            // todo: lots more stuff
            if (_health > 0)
            {
                if (!IsAltForm && !IsMorphing && !IsUnmorphing)
                {
                    if (_drawIceLayer && !Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
                    {
                        _scene.Layer3BindingId = _scene.IceLayerBindingId;
                    }
                    if (_timeSinceInput < (ulong)Values.GunIdleTime * 2) // todo: FPS stuff
                    {
                        UpdateReticle();
                    }
                }
                _damageIndicator.Active = true;
            }
        }

        private int _healthbarPalette = 0;
        private bool _healthbarChangedColor = false;
        private float _healthbarYOffset = 0;

        private void UpdateHealthbars()
        {
            if (_health < 25)
            {
                if (!_healthbarChangedColor)
                {
                    _healthbarPalette = 2;
                    _healthbarChangedColor = true;
                }
            }
            else if (_timeSinceHeal < 10 * 2) // todo: FPS stuff
            {
                if (!_healthbarChangedColor)
                {
                    _healthbarPalette = 1;
                    _healthbarChangedColor = true;
                }
                // todo?: update radar lights
            }
            else if (_timeSinceDamage < 6 * 2) // todo: FPS stuff
            {
                if (!_healthbarChangedColor)
                {
                    _healthbarPalette = 2;
                    _healthbarChangedColor = true;
                }
            }
            else if (_healthbarChangedColor)
            {
                _healthbarPalette = 0;
                _healthbarChangedColor = false;
            }
            // todo: bomb UI
            float targetOffsetY = _hudObjects.HealthOffsetY; // todo: or match state is 2
            if (IsAltForm || IsMorphing)
            {
                targetOffsetY += _hudObjects.HealthOffsetYAlt;
            }
            if (_healthbarYOffset > targetOffsetY)
            {
                _healthbarYOffset -= 1 / 2f; // todo: FPS stuff
            }
            else if (_healthbarYOffset < targetOffsetY)
            {
                _healthbarYOffset += 1 / 2f; // todo: FPS stuff
            }
        }

        private void UpdateWeaponSelect()
        {
            int selection = -1;
            float x = Input.MouseState?.X ?? 0;
            float y = Input.MouseState?.Y ?? 0;
            float ratioX = _scene.Size.X / 256f;
            float ratioY = _scene.Size.Y / 192f;
            float distX = 224 * ratioX - x; // todo: invert for left-handed mode
            float distY = y - 38 * ratioY;
            if (distX > 0 && distY > 0 && distX * distX + distY * distY > 20 * ratioY * 20 * ratioY)
            {
                float div = distX / distY;
                if (div >= Fixed.ToFloat(1060) * ratioX / (Fixed.ToFloat(3956) * ratioY))
                {
                    if (div >= Fixed.ToFloat(2048) * ratioX / (Fixed.ToFloat(3547) * ratioY))
                    {
                        if (div >= Fixed.ToFloat(2896) * ratioX / (Fixed.ToFloat(2896) * ratioY))
                        {
                            if (div >= Fixed.ToFloat(3547) * ratioX / (Fixed.ToFloat(2048) * ratioY))
                            {
                                if (div >= Fixed.ToFloat(3956) * ratioX / (Fixed.ToFloat(1060) * ratioY))
                                {
                                    if (_availableWeapons[BeamType.ShockCoil])
                                    {
                                        selection = 5;
                                        WeaponSelection = BeamType.ShockCoil;
                                    }
                                }
                                else if (_availableWeapons[BeamType.Magmaul])
                                {
                                    selection = 4;
                                    WeaponSelection = BeamType.Magmaul;
                                }
                            }
                            else if (_availableWeapons[BeamType.Judicator])
                            {
                                selection = 3;
                                WeaponSelection = BeamType.Judicator;
                            }
                        }
                        else if (_availableWeapons[BeamType.Imperialist])
                        {
                            selection = 2;
                            WeaponSelection = BeamType.Imperialist;
                        }
                    }
                    else if (_availableWeapons[BeamType.Battlehammer])
                    {
                        selection = 1;
                        WeaponSelection = BeamType.Battlehammer;
                    }
                }
                else if (_availableWeapons[BeamType.VoltDriver])
                {
                    selection = 0;
                    WeaponSelection = BeamType.VoltDriver;
                }
            }
            for (int i = 0; i < 6; i++)
            {
                HudObjectInstance weaponInst = _weaponSelectInsts[i];
                bool available = _availableWeapons[weaponInst.CurrentFrame];
                weaponInst.Enabled = available;
                HudObjectInstance boxInst = _selectBoxInsts[i];
                boxInst.SetIndex(available ? (i == selection ? 2 : 1) : 0, _scene);
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

        public byte HudDisruptedState { get; private set; } = 0;
        public float HudDisruptionFactor { get; private set; } = 0;
        private ushort _hudDisruptedTimer = 0;

        private void HudOnDisrupted()
        {
            if (CameraSequence.Current == null)
            {
                HudDisruptedState = 1;
                _hudDisruptedTimer = _disruptedTimer;
            }
        }

        public void HudEndDisrupted()
        {
            if (HudDisruptedState != 0)
            {
                HudDisruptedState = 0;
                _hudDisruptedTimer = 0;
                HudDisruptionFactor = 0;
            }
        }

        private void UpdateDisruptedState()
        {
            if (HudDisruptedState == 1)
            {
                HudDisruptionFactor += 0.25f / 2; // todo: FPS stuff
                if (HudDisruptionFactor >= 1)
                {
                    HudDisruptionFactor = 1;
                    HudDisruptedState = 2;
                }
            }
            else if (HudDisruptedState == 2)
            {
                if (--_hudDisruptedTimer == 0)
                {
                    HudDisruptedState = 3;
                }
            }
            else if (HudDisruptedState == 3)
            {
                HudDisruptionFactor -= 0.125f / 2; // todo: FPS stuff
                if (HudDisruptionFactor <= 0)
                {
                    HudDisruptionFactor = 0;
                    HudDisruptedState = 0;
                    _hudDisruptedTimer = 32 * 2; // todo: FPS stuff
                }
            }
            else if (HudDisruptedState != 0)
            {
                if (--_hudDisruptedTimer == 0)
                {
                    HudDisruptedState = 1;
                }
            }
        }

        public void DrawHudObjects()
        {
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                for (int i = 0; i < 6; i++)
                {
                    _scene.DrawHudObject(_selectBoxInsts[i], byHeight: true);
                    _scene.DrawHudObject(_weaponSelectInsts[i], byHeight: true);
                }
            }
            else
            {
                _scene.DrawHudObject(_targetCircleInst);
                if (_health > 0)
                {
                    DrawHealthbars();
                }
            }
        }

        public void DrawHudModels()
        {
            if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                _scene.DrawHudFilterModel(_filterModel);
            }
            else if (_damageIndicator.Active)
            {
                _scene.DrawHudDamageModel(_damageIndicator);
            }
        }

        private void DrawHealthbars()
        {
            _healthbarMainMeter.TankAmount = Values.EnergyTank;
            _healthbarMainMeter.TankCount = _healthMax / Values.EnergyTank;
            DrawMeter(_hudObjects.HealthMainPosX, _hudObjects.HealthMainPosY + _healthbarYOffset, Values.EnergyTank - 1,
                _health, _healthbarPalette, _healthbarMainMeter, drawText: true, drawTanks: !_scene.Multiplayer);
            if (_scene.Multiplayer)
            {
                int amount = 0;
                if (_health >= Values.EnergyTank)
                {
                    amount = _health - Values.EnergyTank;
                }
                _healthbarSubMeter.TankAmount = Values.EnergyTank;
                _healthbarSubMeter.TankCount = _healthMax / Values.EnergyTank;
                DrawMeter(_hudObjects.HealthSubPosX, _hudObjects.HealthSubPosY + _healthbarYOffset, Values.EnergyTank - 1,
                    amount, _healthbarPalette, _healthbarSubMeter, drawText: false, drawTanks: false);
            }
        }

        private void DrawMeter(float x, float y, int baseAmount, int curAmount, int palette,
            HudMeter meter, bool drawText, bool drawTanks)
        {
            int filledTanks = 0;
            int remaining = curAmount;
            if (drawTanks && meter.TankCount > 0)
            {
                for (int i = 0; i < meter.TankCount; i++)
                {
                    if (remaining < meter.TankAmount)
                    {
                        break;
                    }
                    filledTanks++;
                    remaining -= meter.TankAmount;
                }
            }
            int barAmount;
            if (!_scene.Multiplayer)
            {
                barAmount = curAmount - filledTanks * meter.TankAmount;
            }
            else
            {
                barAmount = Math.Min(baseAmount, curAmount);
            }
            int tiles = (meter.Length + 7) / 8;
            int filledTiles = 100000 * barAmount / (99000 * meter.TankAmount / meter.Length);
            if (filledTiles == 0 && barAmount > 0)
            {
                filledTiles = 1;
            }
            if (drawText)
            {
                int amount = _scene.Multiplayer ? curAmount : barAmount;
                DrawText2D(x + meter.BarOffsetX, y + meter.BarOffsetY, type: 0, $"{amount:00}");
                if (meter.MessageId > 0)
                {
                    string message = Strings.GetHudMessage(meter.MessageId);
                    DrawText2D(x + meter.TextOffsetX, y + meter.TextOffsetY, type: 0, message);
                }
                if (drawTanks && meter.TankCount > 0)
                {
                    Debug.Assert(meter.TankInst != null);
                    float tankX = x + meter.TankOffsetX;
                    float tankY = y + meter.TankOffsetY;
                    for (int i = 0; i < meter.TankCount; i++)
                    {
                        meter.TankInst.PositionX = tankX / 256f;
                        meter.TankInst.PositionY = tankY / 192f;
                        meter.TankInst.SetData(i < filledTanks ? 0 : 1, palIndex: palette, _scene);
                        _scene.DrawHudObject(meter.TankInst);
                        if (meter.Horizontal)
                        {
                            tankX += meter.TankSpacing;
                        }
                        else
                        {
                            tankY -= meter.TankSpacing;
                        }
                    }
                }
            }

            void DrawTile(int charFrame)
            {
                meter.BarInst.PositionX = x / 256f;
                meter.BarInst.PositionY = y / 192f;
                meter.BarInst.SetData(charFrame, palIndex: palette, _scene);
                _scene.DrawHudObject(meter.BarInst);
                if (meter.Horizontal)
                {
                    x += 8;
                }
                else
                {
                    y -= 8;
                }
            }

            for (int i = 0; i < filledTiles / 8; i++)
            {
                DrawTile(charFrame: 0);
                tiles--;
            }
            if (tiles > 0)
            {
                DrawTile(charFrame: 8 - (filledTiles & 7));
                tiles--;
                if (tiles > 0)
                {
                    for (int i = 0; i < tiles; i++)
                    {
                        DrawTile(charFrame: 8);
                    }
                }
            }
        }

        private int _textSpacingY = 0;

        // todo: size/shape (seemingly only used by the bottom screen rank, which is 16x16/square instead of 8x8/square)
        private Vector2 DrawText2D(float x, float y, int type, string text)
        {
            int spacingY = _textSpacingY == 0 ? 12 : _textSpacingY;
            if (type == 0)
            {
                float startX = x;
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    Debug.Assert(ch < 128);
                    if (ch == '\n')
                    {
                        x = startX;
                        y += spacingY;
                    }
                    else
                    {
                        int index = ch - 32; // sktodo
                        float offset = Font.Offsets[index] + y;
                        if (ch != ' ')
                        {
                            _textInst.PositionX = x / 256f;
                            _textInst.PositionY = offset / 192f;
                            _textInst.SetData(index, _healthbarPalette, _scene);
                            _scene.DrawHudObject(_textInst);
                        }
                        x += Font.Widths[index];
                    }
                }
            }
            else if (type == 1)
            {

            }
            else if (type == 2)
            {

            }
            else if (type == 3)
            {

            }
            return new Vector2(x, y);
        }
    }
}
