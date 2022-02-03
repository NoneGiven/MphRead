using System;
using System.Collections.Generic;
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
        private readonly HudObjectInstance[] _weaponSelectInsts = new HudObjectInstance[6];
        private readonly HudObjectInstance[] _selectBoxInsts = new HudObjectInstance[6];
        private HudObjectInstance _textInst = null!;

        private HudMeter _healthbarMainMeter = null!;
        private HudMeter _healthbarSubMeter = null!;
        private HudMeter _ammoBarMeter = null!;
        private HudObjectInstance _weaponIconInst = null!;
        private HudObjectInstance _boostInst = null!;
        private HudObjectInstance _bombInst = null!;
        private HudMeter _enemyHealthMeter = null!;

        private ModelInstance _damageIndicator = null!;
        private readonly ushort[] _damageIndicatorTimers = new ushort[8];
        private readonly Node[] _damageIndicatorNodes = new Node[8];

        private ModelInstance _filterModel = null!;
        private bool _showScoreboard = false;

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
            HudObject weaponSelectObj = HudInfo.GetHudObject(_hudObjects.WeaponSelect);
            HudObject selectBoxObj = HudInfo.GetHudObject(_hudObjects.SelectBox);
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
                var weaponInst = new HudObjectInstance(weaponSelectObj.Width, weaponSelectObj.Height);
                int frame = i == 0 ? 1 : i + 2;
                weaponInst.SetCharacterData(weaponSelectObj.CharacterData, frame, _scene);
                weaponInst.SetPaletteData(weaponSelectObj.PaletteData, _scene);
                weaponInst.Alpha = 0.722f;
                var boxInst = new HudObjectInstance(selectBoxObj.Width, selectBoxObj.Height);
                boxInst.SetCharacterData(selectBoxObj.CharacterData, _scene);
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
            HudObject healthbarMain = HudInfo.GetHudObject(_hudObjects.HealthBarA);
            HudObject healthbarSub = HudInfo.GetHudObject(_hudObjects.HealthBarB);
            _healthbarMainMeter = HudElements.MainHealthbars[(int)Hunter];
            _healthbarSubMeter = HudElements.SubHealthbars[(int)Hunter];
            _healthbarMainMeter.BarInst = new HudObjectInstance(healthbarMain.Width, healthbarMain.Height);
            _healthbarMainMeter.BarInst.SetCharacterData(healthbarMain.CharacterData, _scene);
            _healthbarMainMeter.BarInst.SetPaletteData(healthbarMain.PaletteData, _scene);
            _healthbarMainMeter.BarInst.Enabled = true;
            _healthbarSubMeter.BarInst = new HudObjectInstance(healthbarSub.Width, healthbarSub.Height);
            _healthbarSubMeter.BarInst.SetCharacterData(healthbarSub.CharacterData, _scene);
            _healthbarSubMeter.BarInst.SetPaletteData(healthbarSub.PaletteData, _scene);
            _healthbarSubMeter.BarInst.Enabled = true;
            _enemyHealthMeter = HudElements.EnemyHealthbar;
            _enemyHealthMeter.BarInst = new HudObjectInstance(healthbarSub.Width, healthbarSub.Height);
            _enemyHealthMeter.BarInst.SetCharacterData(healthbarSub.CharacterData, _scene);
            _enemyHealthMeter.BarInst.SetPaletteData(healthbarSub.PaletteData, _scene);
            _enemyHealthMeter.BarInst.Enabled = true;
            if (!_scene.Multiplayer && _hudObjects.EnergyTanks != null)
            {
                HudObject healthbarTank = HudInfo.GetHudObject(_hudObjects.EnergyTanks);
                _healthbarMainMeter.TankInst = new HudObjectInstance(healthbarTank.Width, healthbarTank.Height);
                _healthbarMainMeter.TankInst.SetCharacterData(healthbarTank.CharacterData, _scene);
                if (Hunter == Hunter.Samus || Hunter == Hunter.Guardian)
                {
                    _healthbarMainMeter.TankInst.SetPaletteData(healthbarTank.PaletteData, _scene);
                }
                else
                {
                    _healthbarMainMeter.TankInst.SetPaletteData(healthbarMain.PaletteData, _scene);
                }
                _healthbarMainMeter.TankInst.Enabled = true;
            }
            _healthbarYOffset = _hudObjects.HealthOffsetY;
            HudObject ammoBar = HudInfo.GetHudObject(_hudObjects.AmmoBar);
            _ammoBarMeter = HudElements.AmmoBars[(int)Hunter];
            _ammoBarMeter.BarInst = new HudObjectInstance(ammoBar.Width, ammoBar.Height);
            _ammoBarMeter.BarInst.SetCharacterData(ammoBar.CharacterData, _scene);
            _ammoBarMeter.BarInst.SetPaletteData(ammoBar.PaletteData, _scene);
            HudObject weaponIcon = HudInfo.GetHudObject(_hudObjects.WeaponIcon);
            _weaponIconInst = new HudObjectInstance(weaponIcon.Width, weaponIcon.Height);
            _weaponIconInst.SetCharacterData(weaponIcon.CharacterData, _scene);
            _weaponIconInst.SetPaletteData(weaponIcon.PaletteData, _scene);
            _weaponIconInst.PositionX = _hudObjects.WeaponIconPosX / 256f;
            _weaponIconInst.PositionY = _hudObjects.WeaponIconPosY / 192f;
            _weaponIconInst.SetAnimationFrames(weaponIcon.AnimParams);
            HudObject boost = HudInfo.GetHudObject(HudElements.Boost);
            _boostInst = new HudObjectInstance(boost.Width, boost.Height);
            _boostInst.SetCharacterData(boost.CharacterData, _scene);
            _boostInst.SetPaletteData(boost.PaletteData, _scene);
            _boostInst.SetAnimationFrames(boost.AnimParams);
            _boostInst.Enabled = true;
            HudObject bombs = HudInfo.GetHudObject(HudElements.Bombs);
            _bombInst = new HudObjectInstance(bombs.Width, bombs.Height);
            _bombInst.SetCharacterData(bombs.CharacterData, _scene);
            _bombInst.SetPaletteData(bombs.PaletteData, _scene);
            _bombInst.Enabled = true;
            _boostBombsYOffset = 208;
            _textInst = new HudObjectInstance(width: 8, height: 8); // todo: max is 16x16
            _textInst.SetCharacterData(Font.CharacterData, _scene);
            _textInst.SetPaletteData(ammoBar.PaletteData, _scene);
            _textInst.Enabled = true;
        }

        public void UpdateHud()
        {
            UpdateHealthbars();
            UpdateAmmoBar();
            _weaponIconInst.ProcessAnimation(_scene);
            _boostInst.ProcessAnimation(_scene);
            UpdateBoostBombs();
            UpdateDamageIndicators();
            UpdateDisruptedState();
            WeaponSelection = CurrentWeapon;
            if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                UpdateWeaponSelect();
            }
            _targetCircleInst.Enabled = false;
            _ammoBarMeter.BarInst.Enabled = false;
            _weaponIconInst.Enabled = false;
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
                    if (_drawIceLayer && !Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen) && !_showScoreboard)
                    {
                        _scene.Layer3BindingId = _scene.IceLayerBindingId;
                    }
                    if (_timeSinceInput < (ulong)Values.GunIdleTime * 2) // todo: FPS stuff
                    {
                        UpdateReticle();
                    }
                    _ammoBarMeter.BarInst.Enabled = true;
                    _weaponIconInst.Enabled = true;
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
            float targetOffsetY = _hudObjects.HealthOffsetY;
            if (IsAltForm || IsMorphing) // todo: or match state is 2
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

        private int _ammoBarPalette = 0;
        private bool _ammoBarChangedColor = false;

        private void UpdateAmmoBar()
        {
            // todo?:
            // - use the other palettes for low ammo warning and danger?
            // - the bar flashes when picking up UA w/ missiles equipped and vice versa
            // - the bar doesn't flash when ammo is restored by the affinity weapon pickup
            if (_timeSincePickup < 10 * 2) // todo: FPS stuff
            {
                if (!_ammoBarChangedColor)
                {
                    _ammoBarPalette = 1;
                    _ammoBarChangedColor = true;
                }
                // todo?: update radar lights
            }
            else if (_ammoBarChangedColor)
            {
                _ammoBarPalette = 0;
                _ammoBarChangedColor = false;
            }
        }

        private float _boostBombsYOffset = 0;

        private void UpdateBoostBombs()
        {
            float targetOffsetY = 208;
            if (IsAltForm || IsMorphing) // todo: or match state is 2
            {
                targetOffsetY = 160;
            }
            if (_boostBombsYOffset > targetOffsetY)
            {
                _boostBombsYOffset -= 2 / 2f; // todo: FPS stuff
            }
            else if (_boostBombsYOffset < targetOffsetY)
            {
                _boostBombsYOffset += 2 / 2f; // todo: FPS stuff
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
            _weaponIconInst.SetAnimation(start: 9, target: 27, frames: 19, afterAnim: (int)beam);
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
            if (GameState.MatchState == MatchState.GameOver)
            {
                string text = Strings.GetHudMessage(219); // GAME OVER
                DrawText2D(128, 40, TextType.Centered, 0, text, new ColorRgba(0x3FEF), fontSpacing: 8);
            }
            else if (GameState.MatchState == MatchState.Ending)
            {
                DrawScoreboard();
            }
            else if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            else if (CameraSequence.Current?.IsIntro == true)
            {
                // sktodo: draw laws of battle
                DrawQueuedHudMessages();
                return;
            }
            else if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                for (int i = 0; i < 6; i++)
                {
                    _scene.DrawHudObject(_selectBoxInsts[i], mode: 1);
                    _scene.DrawHudObject(_weaponSelectInsts[i], mode: 1);
                }
            }
            else if (_showScoreboard)
            {
                DrawMatchTime();
                DrawScoreboard();
            }
            else
            {
                if (IsAltForm || IsMorphing || IsUnmorphing)
                {
                    DrawBoostBombs();
                }
                else
                {
                    DrawAmmoBar();
                    _scene.DrawHudObject(_weaponIconInst);
                    _scene.DrawHudObject(_targetCircleInst);
                }
                DrawModeHud();
                if (_health > 0)
                {
                    DrawHealthbars();
                }
                DrawQueuedHudMessages();
            }
        }

        public void DrawHudModels()
        {
            if (CameraSequence.Current?.IsIntro == true)
            {
                _scene.DrawHudFilterModel(_filterModel, alpha: 15 / 31f);
            }
            else if (GameState.MatchState == MatchState.GameOver)
            {
                _scene.DrawHudFilterModel(_filterModel, alpha: 12 / 31f);
            }
            else if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen) || _showScoreboard || GameState.MatchState == MatchState.Ending)
            {
                _scene.DrawHudFilterModel(_filterModel);
            }
            else if (_damageIndicator.Active)
            {
                _scene.DrawHudDamageModel(_damageIndicator);
            }
        }

        private void DrawMatchTime()
        {
            if (GameState.MatchTime < 0)
            {
                return;
            }
            var time = TimeSpan.FromSeconds(GameState.MatchTime);
            int palette = time.TotalSeconds < 10 ? 2 : 0;
            float posY = 25;
            string text = Strings.GetHudMessage(5); // TIME
            DrawText2D(128, posY, TextType.Centered, palette, text);
            text = $"{time.Minutes}:{time.Seconds:00}";
            DrawText2D(128, posY + 10, TextType.Centered, palette, text);
        }

        private void DrawScoreboard()
        {
            // sktodo
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

        private void DrawAmmoBar()
        {
            WeaponInfo info = EquipInfo.Weapon;
            if (info.AmmoCost == 0 || !_ammoBarMeter.BarInst.Enabled)
            {
                return;
            }
            _ammoBarMeter.TankAmount = _ammoMax[info.AmmoType] + 1;
            _ammoBarMeter.TankCount = 0;
            int amount = _ammo[info.AmmoType];
            DrawMeter(_hudObjects.AmmoBarPosX, _hudObjects.AmmoBarPosY, amount, amount,
                _ammoBarPalette, _ammoBarMeter, drawText: false, drawTanks: false);
            amount /= info.AmmoCost;
            DrawText2D(_hudObjects.AmmoBarPosX + _ammoBarMeter.BarOffsetX, _hudObjects.AmmoBarPosY + _ammoBarMeter.BarOffsetY,
                _ammoBarMeter.TextType, _ammoBarPalette, $"{amount:00}");
        }

        private void DrawBoostBombs()
        {
            float posY = _boostBombsYOffset;
            if (_abilities.TestFlag(AbilityFlags.Bombs) && Hunter != Hunter.Kanden)
            {
                float posX = 244;
                for (int i = 3; i > 0; i--)
                {
                    _bombInst.SetIndex(_bombAmmo < i ? 1 : 0, _scene);
                    _bombInst.PositionX = (posX - _bombInst.Width / 2) / 256f;
                    _bombInst.PositionY = posY / 192f;
                    _scene.DrawHudObject(_bombInst, mode: 2);
                    posX -= 14;
                }
                string message = Strings.GetHudMessage(1); // bombs
                DrawText2D(230, posY + 18, TextType.Centered, palette: 0, message);
            }
            if (_abilities.TestFlag(AbilityFlags.Boost))
            {
                if (_altAttackCooldown == 0)
                {
                    _boostInst.SetIndex(0, _scene);
                }
                else if (_boostInst.Timer <= 1 / 30f)
                {
                    _boostInst.SetIndex(1, _scene);
                }
                _boostInst.PositionX = (29 - _boostInst.Width / 2) / 256f;
                _boostInst.PositionY = (posY - 16) / 192f;
                _scene.DrawHudObject(_boostInst, mode: 2);
                string message = Strings.GetHudMessage(2); // boost
                DrawText2D(29, posY + 18, TextType.Centered, palette: 0, message);
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
                DrawText2D(x + meter.BarOffsetX, y + meter.BarOffsetY, meter.TextType, _healthbarPalette, $"{amount:00}");
                if (meter.MessageId > 0)
                {
                    string message = Strings.GetHudMessage(meter.MessageId);
                    DrawText2D(x + meter.TextOffsetX, y + meter.TextOffsetY, TextType.LeftAlign, _healthbarPalette, message);
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
                        meter.TankInst.SetData(charFrame: i < filledTanks ? 0 : 1, palette, _scene);
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
                meter.BarInst.SetData(charFrame, palette, _scene);
                _scene.DrawHudObject(meter.BarInst, mode: 2);
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

        private void DrawModeHud()
        {
            // todo: the rest
            if (_scene.GameMode == GameMode.SinglePlayer)
            {
                DrawHudAdventure();
            }
        }

        private void DrawHudAdventure()
        {
            //_enemyHealthMeter
            // todo: draw scan visor if enabled
            // else...
            if (_scene.RoomId == 92) // Gorea_b2
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type == EntityType.EnemyInstance)
                    {
                        var enemy = (EnemyInstanceEntity)entity;
                        if (enemy.EnemyType == EnemyType.GoreaSealSphere2)
                        {
                            // todo: draw healthbar if damaged and some flag
                            break;
                        }
                    }
                }
            }
            else if (_lastTarget != null)
            {
                // todo: we'll need to use this for MP too, while making sure it doesn't conflict with other HUD elements
                if (!DrawTargetHealthbar(_lastTarget))
                {
                    _lastTarget = null;
                }
            }
            // todo: draw visor name
        }

        private bool DrawTargetHealthbar(EntityBase target)
        {
            int max = 0;
            int current = 0;
            string? text = null;
            int lowHealth = 0;
            if (target.Type == EntityType.EnemyInstance)
            {
                var enemy = (EnemyInstanceEntity)target;
                if (enemy.EnemyType != EnemyType.FireSpawn && enemy.EnemyType != EnemyType.CretaphidCrystal
                    && enemy.EnemyType != EnemyType.Slench && enemy.EnemyType != EnemyType.SlenchShield
                    && enemy.EnemyType != EnemyType.GoreaArm && enemy.EnemyType != EnemyType.GoreaSealSphere1
                    && enemy.EnemyType != EnemyType.GoreaSealSphere2)
                {
                    return true;
                }
                text = Strings.GetMessage('E', enemy.HealthbarMessageId, StringTables.HudMessagesSP);
                max = enemy.HealthMax;
                current = enemy.Health;
                if (enemy.EnemyType == EnemyType.SlenchShield)
                {
                    // todo: get current and max from owner
                }
                else if (enemy.EnemyType == EnemyType.GoreaArm)
                {
                    // todo: get current by subtracting damage from max
                }
                else if (enemy.EnemyType == EnemyType.GoreaSealSphere1)
                {
                    // todo: get current by subtracting damage from max
                }
                else if (enemy.EnemyType == EnemyType.GoreaSealSphere2)
                {
                    // todo: get current by subtracting damage from max
                }
                lowHealth = max / 4;
            }
            else if (target.Type == EntityType.Player)
            {
                var player = (PlayerEntity)target;
                max = player.HealthMax;
                current = player.Health;
                text = _hunterNames[(int)player.Hunter];
                lowHealth = 25;
            }
            else if (target.Type == EntityType.Halfturret)
            {
                var turret = (HalfturretEntity)target;
                max = turret.Owner.HealthMax / 2;
                current = turret.Health;
                text = _altAttackNames[(int)Hunter.Weavel];
                lowHealth = 25;
            }
            int palette = current > lowHealth ? 0 : 2;
            _enemyHealthMeter.TankAmount = max;
            _enemyHealthMeter.TankCount = 0;
            _enemyHealthMeter.Length = _healthbarSubMeter.Length;
            DrawMeter(_hudObjects.EnemyHealthPosX, _hudObjects.EnemyHealthPosY, max, current, palette,
                _enemyHealthMeter, drawText: false, drawTanks: false);
            // todo: only draw text if we have the scan data
            // else, draw "enemy" instead
            if (text != null)
            {
                DrawText2D(_hudObjects.EnemyHealthTextPosX, _hudObjects.EnemyHealthTextPosY, TextType.Centered, palette, text);
            }
            return current > 0;
        }

        private float _textSpacingY = 0;

        // todo: size/shape (seemingly only used by the bottom screen rank, which is 16x16/square instead of 8x8/square)
        private Vector2 DrawText2D(float x, float y, TextType type, int palette, ReadOnlySpan<char> text,
            ColorRgba? color = null, float alpha = 1, float fontSpacing = -1)
        {
            int length = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\0')
                {
                    break;
                }
                length++;
            }
            if (length == 0)
            {
                return new Vector2(x, y);
            }
            _textInst.Alpha = alpha;
            float spacingY = _textSpacingY == 0 ? (fontSpacing == -1 ? 12 : fontSpacing) : _textSpacingY;
            if (type == TextType.LeftAlign)
            {
                float startX = x;
                for (int i = 0; i < length; i++)
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
                        int index = ch - 32; // todo: starting character
                        float offset = Font.Offsets[index] + y;
                        if (ch != ' ')
                        {
                            _textInst.PositionX = x / 256f;
                            _textInst.PositionY = offset / 192f;
                            if (color.HasValue)
                            {
                                _textInst.SetData(index, color.Value, _scene);
                            }
                            else
                            {
                                _textInst.SetData(index, palette, _scene);
                            }
                            _scene.DrawHudObject(_textInst, mode: 2);
                        }
                        x += Font.Widths[index];
                    }
                }
            }
            else if (type == TextType.RightAlign)
            {
                float startX = x;
                int start = 0;
                int end = 0;
                do
                {
                    end = text[start..].IndexOf('\n');
                    if (end == -1)
                    {
                        end = length;
                    }
                    x = startX;
                    for (int i = end - 1; i >= start; i--)
                    {
                        char ch = text[i];
                        Debug.Assert(ch < 128);
                        int index = ch - 32; // todo: starting character
                        x -= Font.Widths[index];
                        float offset = Font.Offsets[index] + y;
                        if (ch != ' ')
                        {
                            _textInst.PositionX = x / 256f;
                            _textInst.PositionY = offset / 192f;
                            if (color.HasValue)
                            {
                                _textInst.SetData(index, color.Value, _scene);
                            }
                            else
                            {
                                _textInst.SetData(index, palette, _scene);
                            }
                            _scene.DrawHudObject(_textInst);
                        }
                    }
                    if (end != length)
                    {
                        do
                        {
                            end++;
                            start = end;
                            y += spacingY;
                        }
                        while (text[start] == '\n');
                    }
                }
                while (end < length);
            }
            else if (type == TextType.Centered)
            {
                float startX = x;
                int start = 0;
                int end = 0;
                do
                {
                    end = text[start..].IndexOf('\n');
                    if (end == -1)
                    {
                        end = length;
                    }
                    x = startX;
                    float width = 0;
                    for (int i = start; i < end; i++)
                    {
                        char ch = text[i];
                        Debug.Assert(ch < 128);
                        int index = ch - 32; // todo: starting character
                        width += Font.Widths[index];
                    }
                    x = startX - width / 2;
                    for (int i = start; i < end; i++)
                    {
                        char ch = text[i];
                        Debug.Assert(ch < 128);
                        int index = ch - 32; // todo: starting character
                        float offset = Font.Offsets[index] + y;
                        if (ch != ' ')
                        {
                            _textInst.PositionX = x / 256f;
                            _textInst.PositionY = offset / 192f;
                            if (color.HasValue)
                            {
                                _textInst.SetData(index, color.Value, _scene);
                            }
                            else
                            {
                                _textInst.SetData(index, palette, _scene);
                            }
                            _scene.DrawHudObject(_textInst);
                        }
                        x += Font.Widths[index];
                    }
                    if (end != length)
                    {
                        do
                        {
                            end++;
                            start = end;
                            y += spacingY;
                        }
                        while (text[start] == '\n');
                    }
                }
                while (end < length);
            }
            else if (type == TextType.Type3)
            {
                // todo: this
                Debug.Assert(false);
            }
            return new Vector2(x, y);
        }

        public void QueueHudMessage(float x, float y, float duration, byte category, int messageId)
        {
            string text = Strings.GetHudMessage(messageId);
            QueueHudMessage(x, y, TextType.Centered, 256, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, int maxWidth, float duration, byte category, int messageId)
        {
            string text = Strings.GetHudMessage(messageId);
            QueueHudMessage(x, y, TextType.Centered, maxWidth, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, float duration, byte category, string text)
        {
            QueueHudMessage(x, y, TextType.Centered, 256, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, int maxWidth, float duration, byte category, string text)
        {
            QueueHudMessage(x, y, TextType.Centered, maxWidth, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, TextType textType, int maxWidth, float fontSize,
            ColorRgba color, float alpha, float duration, byte category, string text)
        {
            Debug.Assert(text.Length < 256);
            char[] buffer = new char[512];
            int lineCount = WrapText(text, maxWidth, buffer);
            float minDuration = Single.MaxValue;
            HudMessage? message = null;
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                HudMessage existing = _hudMessageQueue[i];
                if (existing.Lifetime > 0)
                {
                    if ((category & existing.Category & 14) != 0)
                    {
                        existing.Position = existing.Position.AddY(-lineCount * existing.FontSize);
                    }
                    else if (existing.Position.Y == y)
                    {
                        existing.Lifetime = 0;
                    }
                }
                if (existing.Lifetime < minDuration)
                {
                    minDuration = existing.Lifetime;
                    message = existing;
                }
            }
            Debug.Assert(message != null);
            Array.Fill(message.Text, '\0');
            Array.Copy(buffer, message.Text, message.Text.Length);
            if ((category & 14) != 0)
            {
                y -= (lineCount - 1) * fontSize;
            }
            message.Position = new Vector2(x, y);
            message.MaxWidth = maxWidth;
            message.FontSize = fontSize;
            message.Color = color;
            message.Alpha = alpha;
            message.TextType = textType;
            message.Category = category;
            message.Lifetime = duration;
        }

        private int WrapText(string text, int maxWidth, char[] dest)
        {
            int lines = 1;
            if (maxWidth <= 0)
            {
                return lines;
            }
            int lineWidth = 0;
            int breakPos = 0;
            int c = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                // todo: upper bit check/alt font stuff will be important for symbols (i.e. nicknames)
                Debug.Assert(ch < 128);
                dest[c] = ch;
                if (ch == '\n')
                {
                    lineWidth = 0;
                    breakPos = 0;
                    lines++;
                }
                else
                {
                    if (ch == ' ')
                    {
                        breakPos = c;
                    }
                    if (ch >= ' ')
                    {
                        int index = ch - 32; // todo: starting character
                        lineWidth += Font.Widths[index];
                    }
                    if (i < text.Length - 1 && lineWidth > maxWidth)
                    {
                        if (breakPos == 0 && maxWidth >= 8)
                        {
                            dest[c + 1] = ch;
                            breakPos = c;
                            c++;
                        }
                        if (breakPos > 0)
                        {
                            dest[breakPos] = '\n';
                            breakPos = 0;
                            lineWidth = 0;
                            lines++;
                        }
                    }
                }
                c++;
            }
            return lines;
        }

        public void ProcessHudMessageQueue()
        {
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                HudMessage message = _hudMessageQueue[i];
                if (message.Lifetime > 0)
                {
                    message.Lifetime -= _scene.FrameTime;
                    if (message.Lifetime < 0)
                    {
                        message.Lifetime = 0;
                    }
                }
            }
        }

        private void DrawQueuedHudMessages()
        {
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                HudMessage message = _hudMessageQueue[i];
                if (message.Lifetime > 0
                    && ((message.Category & 1) == 0 || (_scene.FrameCount & (7 * 2)) <= 3 * 2)) // todo: FPS stuff
                {
                    // todo: support font size
                    DrawText2D(message.Position.X, message.Position.Y, message.TextType, palette: 0,
                        message.Text, message.Color, message.Alpha, fontSpacing: message.FontSize);
                }
            }
        }

        private class HudMessage
        {
            public Vector2 Position { get; set; }
            public float FontSize { get; set; }
            public ColorRgba Color { get; set; }
            public float Lifetime { get; set; }
            public float Alpha { get; set; }
            public byte Category { get; set; }
            public int MaxWidth { get; set; }
            public TextType TextType { get; set; }
            public char[] Text { get; } = new char[256];
        }

        private static readonly IReadOnlyList<HudMessage> _hudMessageQueue = new HudMessage[20]
        {
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage(),
            new HudMessage()
        };
    }
}
