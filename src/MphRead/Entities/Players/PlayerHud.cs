using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private HudObjectInstance _cloakInst = null!;
        private HudObjectInstance _doubleDamageInst = null!;
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

        private HudObjectInstance _octolithInst = null!;
        private HudObjectInstance _primeHunterInst = null!;
        private HudObjectInstance _nodesInst = null!;
        private HudMeter _nodeProgressMeter = null!;

        private ModelInstance _damageIndicator = null!;
        private readonly ushort[] _damageIndicatorTimers = new ushort[8];
        private readonly Node[] _damageIndicatorNodes = new Node[8];
        private ModelInstance _playerLocator = null!;
        private ModelInstance _arrowLocator = null!;
        private ModelInstance _nodeLocator = null!;
        private ModelInstance _octolithLocator = null!;

        private HudObjectInstance _starsInst = null!;
        private readonly HudObjectInstance[] _hunterInsts = new HudObjectInstance[8];

        private ModelInstance _filterModel = null!;
        private bool _showScoreboard = false;
        private int _iceLayerBindingId = -1;
        private int _helmetBindingId = -1;
        private int _helmetDropBindingId = -1;
        private int _visorBindingId = -1; // todo: support other visor views

        public void SetUpHud()
        {
            _iceLayerBindingId = HudInfo.CharMapToTexture(HudElements.IceLayer,
                startX: 16, startY: 0, tilesX: 32, tilesY: 32, _scene);
            _helmetBindingId = HudInfo.CharMapToTexture(_hudObjects.Helmet, _scene);
            _helmetDropBindingId = HudInfo.CharMapToTexture(_hudObjects.HelmetDrop, _scene);
            _visorBindingId = HudInfo.CharMapToTexture(_hudObjects.Visor, startX: 0, startY: 0, tilesX: 0, tilesY: 32, _scene);
            // todo: only load what needs to be loaded for the mode
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
            _playerLocator = Read.GetModelInstance("hud_icon_player", dir: MetaDir.Hud);
            _scene.LoadModel(_playerLocator.Model);
            _arrowLocator = Read.GetModelInstance("hud_icon_arrow", dir: MetaDir.Hud);
            _scene.LoadModel(_arrowLocator.Model);
            _nodeLocator = Read.GetModelInstance("hud_icon_nodes", dir: MetaDir.Hud);
            _scene.LoadModel(_nodeLocator.Model);
            _octolithLocator = Read.GetModelInstance("hud_icon_octolith", dir: MetaDir.Hud);
            _scene.LoadModel(_octolithLocator.Model);
            _targetCircleObj = HudInfo.GetHudObject(_hudObjects.Reticle);
            _sniperCircleObj = HudInfo.GetHudObject(_hudObjects.SniperReticle);
            Debug.Assert(_sniperCircleObj.Width >= _targetCircleObj.Width);
            Debug.Assert(_sniperCircleObj.Height >= _targetCircleObj.Height);
            _targetCircleInst = new HudObjectInstance(_targetCircleObj.Width, _targetCircleObj.Height,
                _sniperCircleObj.Width, _sniperCircleObj.Height);
            _targetCircleInst.SetCharacterData(_targetCircleObj.CharacterData, _scene);
            _targetCircleInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
            _targetCircleInst.Center = true;
            HudObject cloak = HudInfo.GetHudObject(_hudObjects.Cloaking);
            _cloakInst = new HudObjectInstance(cloak.Width, cloak.Height);
            _cloakInst.SetCharacterData(cloak.CharacterData, _scene);
            _cloakInst.SetPaletteData(cloak.PaletteData, _scene);
            _cloakInst.Enabled = true;
            HudObject doubleDamage = HudInfo.GetHudObject(_hudObjects.DoubleDamage);
            _doubleDamageInst = new HudObjectInstance(doubleDamage.Width, doubleDamage.Height);
            _doubleDamageInst.SetCharacterData(doubleDamage.CharacterData, _scene);
            _doubleDamageInst.SetPaletteData(doubleDamage.PaletteData, _scene);
            _doubleDamageInst.Enabled = true;
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
            if (_scene.Multiplayer)
            {
                HudObject damageBar = HudInfo.GetHudObject(_hudObjects.DamageBar);
                _enemyHealthMeter = new HudMeter() { Horizontal = true, };
                _enemyHealthMeter.BarInst = new HudObjectInstance(damageBar.Width, damageBar.Height);
                _enemyHealthMeter.BarInst.SetCharacterData(damageBar.CharacterData, _scene);
                _enemyHealthMeter.BarInst.SetPaletteData(damageBar.PaletteData, _scene);
                _enemyHealthMeter.BarInst.Enabled = true;
            }
            else
            {
                _enemyHealthMeter = HudElements.EnemyHealthbar;
                _enemyHealthMeter.BarInst = new HudObjectInstance(healthbarSub.Width, healthbarSub.Height);
                _enemyHealthMeter.BarInst.SetCharacterData(healthbarSub.CharacterData, _scene);
                _enemyHealthMeter.BarInst.SetPaletteData(healthbarSub.PaletteData, _scene);
                _enemyHealthMeter.BarInst.Enabled = true;
            }
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
            HudObject stars = HudInfo.GetHudObject(HudElements.Stars);
            _starsInst = new HudObjectInstance(stars.Width, stars.Height);
            _starsInst.SetCharacterData(stars.CharacterData, _scene);
            _starsInst.SetPaletteData(stars.PaletteData, _scene);
            _starsInst.Enabled = true;
            for (int i = 0; i < 8; i++)
            {
                HudObject hunter = HudInfo.GetHudObject(HudElements.Hunters[i]);
                var hunterInst = new HudObjectInstance(hunter.Width, hunter.Height);
                hunterInst.SetCharacterData(hunter.CharacterData, _scene);
                if (i == 7)
                {
                    // skdebug: black out the Guardian portrait (using Samus's) except the frame
                    var palette = hunter.PaletteData.ToList();
                    for (int j = 0; j < palette.Count; j++)
                    {
                        if (j != 1)
                        {
                            palette[j] = new ColorRgba(0, 0, 0, 255);
                        }
                    }
                    hunterInst.SetPaletteData(palette, _scene);
                }
                else
                {
                    hunterInst.SetPaletteData(hunter.PaletteData, _scene);
                }
                hunterInst.Enabled = true;
                _hunterInsts[i] = hunterInst;
            }
            HudObject octolith = HudInfo.GetHudObject(HudElements.Octolith);
            _octolithInst = new HudObjectInstance(octolith.Width, octolith.Height);
            _octolithInst.SetCharacterData(octolith.CharacterData, _scene);
            _octolithInst.SetPaletteData(octolith.PaletteData, _scene);
            _octolithInst.Enabled = true;
            HudObject primeHunter = HudInfo.GetHudObject(_hudObjects.PrimeHunter);
            _primeHunterInst = new HudObjectInstance(primeHunter.Width, primeHunter.Height);
            _primeHunterInst.SetCharacterData(primeHunter.CharacterData, _scene);
            _primeHunterInst.SetPaletteData(primeHunter.PaletteData, _scene);
            _primeHunterInst.Enabled = true;
            HudObject nodes = HudInfo.GetHudObject(GameState.Teams ? HudElements.NodesOG : HudElements.NodesRB);
            _nodesInst = new HudObjectInstance(nodes.Width, nodes.Height);
            _nodesInst.SetCharacterData(nodes.CharacterData, _scene);
            _nodesInst.SetPaletteData(nodes.PaletteData, _scene);
            _nodesInst.Enabled = true;
            HudObject systemLoad = HudInfo.GetHudObject(HudElements.SystemLoad);
            _nodeProgressMeter = HudElements.NodeProgressBar;
            _nodeProgressMeter.BarInst = new HudObjectInstance(systemLoad.Width, systemLoad.Height);
            _nodeProgressMeter.BarInst.SetCharacterData(systemLoad.CharacterData, _scene);
            _nodeProgressMeter.BarInst.SetPaletteData(systemLoad.PaletteData, _scene);
            _nodeProgressMeter.BarInst.Enabled = true;
            _textInst = new HudObjectInstance(width: 8, height: 8); // todo: max is 16x16
            _textInst.SetCharacterData(Font.CharacterData, _scene);
            _textInst.SetPaletteData(ammoBar.PaletteData, _scene);
            _textInst.Enabled = true;
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                _hudMessageQueue[i].Lifetime = 0;
            }
            if (_scene.Multiplayer)
            {
                LoadModeRules();
            }
        }

        private RulesInfo _rulesInfo = null!;
        private readonly string?[] _rulesLines = new string[8];
        private readonly (int Length, int Newlines)[] _rulesLengths = new (int, int)[8];

        private void LoadModeRules()
        {
            for (int i = 0; i < 8; i++)
            {
                _rulesLines[i] = null;
                _rulesLengths[i] = (0, 0);
            }
            GameMode mode = _scene.GameMode;
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
            {
                _rulesInfo = HudElements.RulesInfo[0];
            }
            else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                _rulesInfo = HudElements.RulesInfo[1];
            }
            else if (mode == GameMode.PrimeHunter)
            {
                _rulesInfo = HudElements.RulesInfo[2];
            }
            else if (mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                _rulesInfo = HudElements.RulesInfo[3];
            }
            else if (mode == GameMode.Capture)
            {
                _rulesInfo = HudElements.RulesInfo[4];
            }
            else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
            {
                _rulesInfo = HudElements.RulesInfo[5];
            }
            else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                _rulesInfo = HudElements.RulesInfo[6];
            }
            char[] buf = new char[256];
            for (int i = 0; i < _rulesInfo.Count; i++)
            {
                string line = Strings.GetMessage('S', _rulesInfo.MessageIds[i], StringTables.HudMessagesMP);
                if (i == 0)
                {
                    _rulesLines[i] = line;
                    _rulesLengths[0] = (30, 0);
                }
                else
                {
                    // todo: text wrapping should be based on current window width
                    // --> and so it also needs to be deferred until the text is being rendered
                    int offset = _rulesInfo.Offsets[i];
                    int lineCount = WrapText(line, 244 - (offset + 12), buf);
                    int length = 0;
                    for (int j = 0; j < buf.Length; j++)
                    {
                        if (buf[j] == '\0')
                        {
                            break;
                        }
                        length++;
                    }
                    line = new string(buf, 0, length);
                    _rulesLines[i] = line;
                    _rulesLengths[i] = (_rulesLengths[i - 1].Length + line.Length, lineCount - 1);
                }
            }
        }

        public void UpdateHud()
        {
            ProcessDoubleDamageHud();
            ProcessCloakHud();
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
            _scene.Layer1Info.BindingId = -1;
            _scene.Layer2Info.BindingId = -1;
            _scene.Layer3Info.BindingId = -1;
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            if (_health > 0)
            {
                if (!IsAltForm && !IsMorphing && !IsUnmorphing)
                {
                    if (!Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen) && !_showScoreboard)
                    {
                        // sktodo: HUD shift
                        if (_drawIceLayer)
                        {
                            _scene.Layer3Info.BindingId = _iceLayerBindingId;
                            _scene.Layer3Info.Alpha = 9 / 16f;
                            _scene.Layer3Info.ScaleX = -1;
                            _scene.Layer3Info.ScaleY = -1;
                        }
                        else
                        {
                            _scene.Layer3Info.BindingId = _helmetDropBindingId;
                            _scene.Layer3Info.Alpha = Features.HelmetOpacity;
                            _scene.Layer3Info.ScaleX = 2;
                            _scene.Layer3Info.ScaleY = 256 / 192f;
                        }
                        _scene.Layer1Info.BindingId = _visorBindingId;
                        _scene.Layer1Info.Alpha = Features.VisorOpacity;
                        _scene.Layer1Info.ScaleX = 1;
                        _scene.Layer1Info.ScaleY = 256 / 192f;
                        _scene.Layer2Info.BindingId = _helmetBindingId;
                        _scene.Layer2Info.Alpha = Features.HelmetOpacity;
                        _scene.Layer2Info.ScaleX = 2;
                        _scene.Layer2Info.ScaleY = 256 / 192f;
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
            if (IsAltForm || IsMorphing)
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
                DrawText2D(128, 40, Align.Center, 0, text, new ColorRgba(0x3FEF), fontSpacing: 8);
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
                DrawModeRules();
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
                if (Health > 0)
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
                    DrawDoubleDamageHud();
                    DrawCloakHud();
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
            else
            {
                if (_health > 0)
                {
                    DrawLocatorIcons();
                }
                if (_damageIndicator.Active)
                {
                    _scene.DrawHudDamageModel(_damageIndicator);
                }
            }
        }

        private struct LocatorInfo
        {
            public Vector3 Position;
            public ModelInstance Model;
            public ColorRgb Color;
            public float Alpha;

            public LocatorInfo(Vector3 position, ModelInstance model, ColorRgb color, float alpha)
            {
                Position = position;
                Model = model;
                Color = color;
                Alpha = alpha;
            }
        }

        private readonly List<LocatorInfo> _locatorInfo = new List<LocatorInfo>(15);

        private void AddLocatorInfo(Vector3 position, ModelInstance inst, ColorRgb color, float alpha = 1)
        {
            _locatorInfo.Add(new LocatorInfo(position, inst, color, alpha));
        }

        private void DrawLocatorIcons()
        {
            for (int i = 0; i < _locatorInfo.Count; i++)
            {
                LocatorInfo info = _locatorInfo[i];
                DrawLocatorIcon(info.Position, info.Model, info.Color, info.Alpha);
            }
        }

        private void DrawLocatorIcon(Vector3 position, ModelInstance inst, ColorRgb color, float alpha)
        {
            float W(float value)
            {
                return value / 256f * _scene.Size.X;
            }

            float H(float value)
            {
                return value / 192f * _scene.Size.Y;
            }

            float x;
            float y;
            bool behind = false;
            Vector2 proj = Vector2.Zero;
            Vector3 mult = Matrix.Vec3MultMtx4(position, _scene.ViewMatrix);
            if (mult.Z < -1)
            {
                Matrix.ProjectPosition(position, _scene.ViewMatrix, _scene.PerspectiveMatrix, out proj);
                x = proj.X * _scene.Size.X - W(128);
                y = proj.Y * _scene.Size.Y - H(106);
            }
            else
            {
                x = W(mult.X);
                y = -H(mult.Y);
                behind = true;
            }
            float absX = MathF.Abs(x);
            float absY = MathF.Abs(y);
            if (behind || absX > W(100) || absY > H(60))
            {
                if (absY >= 1 / 4096f)
                {
                    float v15 = absX + MathF.Truncate((H(60) - absY) * absX / absY);
                    if (v15 > W(100))
                    {
                        float v17 = absY + MathF.Truncate((W(100) - absX) * absY / absX);
                        if (x <= 0)
                        {
                            proj.X = W(28);
                        }
                        else
                        {
                            proj.X = W(228);
                        }
                        if (y <= 0)
                        {
                            proj.Y = H(106) - v17;
                        }
                        else
                        {
                            proj.Y = v17 + H(106);
                        }
                    }
                    else
                    {
                        if (x <= 0)
                        {
                            proj.X = W(128) - v15;
                        }
                        else
                        {
                            proj.X = v15 + W(128);
                        }
                        if (y <= 0)
                        {
                            proj.Y = H(46);
                        }
                        else
                        {
                            proj.Y = H(166);
                        }
                    }
                }
                else if (x <= 0)
                {
                    proj.X = W(28);
                }
                else
                {
                    proj.X = W(228);
                }
                proj.X /= _scene.Size.X;
                proj.Y /= _scene.Size.Y;
                float angle = MathHelper.RadiansToDegrees(MathF.Atan2(-y, x));
                _scene.DrawIconModel(proj, angle, _arrowLocator, color, alpha);
            }
            else
            {
                _scene.DrawIconModel(proj, angle: 0, inst, color, alpha);
            }
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{time.Hours * 60 + time.Minutes}:{time.Seconds:00}";
        }

        private void DrawMatchTime()
        {
            if (GameState.MatchTime < 0)
            {
                return;
            }
            var time = TimeSpan.FromSeconds(GameState.MatchTime);
            int palette = time.TotalSeconds < 10 ? 2 : 0;
            float posY = 10;
            string text = Strings.GetHudMessage(5); // TIME
            DrawText2D(128, posY, Align.Center, palette, text);
            text = FormatTime(time);
            DrawText2D(128, posY + 10, Align.Center, palette, text);
        }

        private const float _scoreStartSpace = 13;
        private const float _scoreTeamHeaderSpace = 4;
        private const float _scoreTeamLineSpace = 18;
        private const float _scorePlayerSpace = 28;

        private float GetScoreboardHeight()
        {
            float height = _scoreStartSpace;
            if (GameState.MatchState == MatchState.Ending)
            {
                height *= 2;
            }
            int curTeam = 4;
            for (int i = 0; i < GameState.ActivePlayers; i++)
            {
                PlayerEntity player = Players[GameState.ResultSlots[i]];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                if (GameState.Teams && player.TeamIndex != curTeam)
                {
                    if (curTeam != 4)
                    {
                        height -= _scoreTeamHeaderSpace;
                    }
                    height += _scoreTeamLineSpace;
                    curTeam = player.TeamIndex;
                }
                height += _scorePlayerSpace;
            }
            return height;
        }

        private static float Lerp(float first, float second, float by)
        {
            return first * (1 - by) + second * by;
        }

        private void DrawScoreboard()
        {
            GameMode mode = _scene.GameMode;
            float posY = 104 - GetScoreboardHeight() / 2;
            if (GameState.MatchState == MatchState.Ending)
            {
                string text = Strings.GetHudMessage(219); // GAME OVER
                DrawText2D(128, posY, Align.Center, 0, text, new ColorRgba(0x53F4), fontSpacing: 8);
                posY += _scoreStartSpace;
            }
            string header1 = "";
            string header2 = "";
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams
                || mode == GameMode.Nodes || mode == GameMode.NodesTeams)
            {
                header1 = Strings.GetHudMessage(225); // points
            }
            else if (mode == GameMode.Capture || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                header1 = Strings.GetHudMessage(227); // octoliths
            }
            else // survival/teams, defender/teams, prime hunter
            {
                header1 = Strings.GetHudMessage(224); // time
            }
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams
                || mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                header2 = Strings.GetHudMessage(223); // deaths
            }
            else // capture, bounty/teams, defender/teams, nodes/teams, prime hunter
            {
                header2 = Strings.GetHudMessage(220); // kills
            }
            DrawText2D(160, posY, Align.Center, 0, header1, new ColorRgba(0x3FEF), fontSpacing: 8);
            DrawText2D(215, posY, Align.Center, 0, header2, new ColorRgba(0x3FEF), fontSpacing: 8);
            posY += _scoreStartSpace;
            string maxText = Strings.GetHudMessage(256); // MAX

            string ChooseValue1(float time, int points)
            {
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams || mode == GameMode.Defender
                    || mode == GameMode.DefenderTeams || mode == GameMode.PrimeHunter)
                {
                    // time should only be -1 to indicate max in survival/teams
                    return time < 0 ? maxText : FormatTime(TimeSpan.FromSeconds(time));
                }
                // battle/teams, capture, bounty/teams, nodes/teams
                return points.ToString();
            }

            string ChooseValue2(int deaths, int kills)
            {
                if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams
                    || mode == GameMode.Battle || mode == GameMode.BattleTeams)
                {
                    return deaths.ToString();
                }
                // capture, bounty/teams, defender/teams, nodes/teams, prime hunter
                return kills.ToString();
            }

            string teamText = Strings.GetHudMessage(222); // team
            int curTeam = 4;
            for (int i = 0; i < GameState.ActivePlayers; i++)
            {
                int slot = GameState.ResultSlots[i];
                PlayerEntity player = Players[slot];
                if (!player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    continue;
                }
                if (GameState.Teams && player.TeamIndex != curTeam)
                {
                    if (curTeam != 4)
                    {
                        posY -= _scoreTeamHeaderSpace;
                    }
                    curTeam = player.TeamIndex;
                    string teamValue1 = ChooseValue1(GameState.TeamTime[curTeam], GameState.TeamPoints[curTeam]);
                    string teamValue2 = ChooseValue2(GameState.TeamDeaths[curTeam], GameState.TeamKills[curTeam]);
                    var teamColor = new ColorRgba(player.Team == Team.Orange ? 0x23Fu : 0x2BEAu);
                    string teamName = $"{teamText} {player.TeamIndex + 1}";
                    DrawText2D(42, posY, Align.Center, 0, teamName, teamColor, fontSpacing: 8);
                    DrawText2D(160, posY, Align.Center, 0, teamValue1, teamColor, fontSpacing: 8);
                    DrawText2D(215, posY, Align.Center, 0, teamValue2, teamColor, fontSpacing: 8);
                    posY += _scoreTeamLineSpace;
                }
                string value1 = ChooseValue1(GameState.Time[slot], GameState.Points[slot]);
                string value2 = ChooseValue2(GameState.Deaths[slot], GameState.Kills[slot]);
                var color = new ColorRgba(0x7DEF);
                if (player.IsMainPlayer)
                {
                    float rg;
                    float pct = _scene.ElapsedTime / (32 / 30f) % 1;
                    if (pct <= 0.5f)
                    {
                        rg = Lerp(0, 1, pct * 2);
                    }
                    else
                    {
                        rg = Lerp(1, 0, (pct - 0.5f) * 2);
                    }
                    color = new ColorRgba((byte)(rg * 255), (byte)(rg * 255), 255, 255);
                }
                DrawScoreboardPlayer(60, posY, color, _hunterInsts[(int)player.Hunter], slot);
                DrawText2D(160, posY, Align.Center, 0, value1, color, fontSpacing: 8);
                DrawText2D(215, posY, Align.Center, 0, value2, color, fontSpacing: 8);
                posY += _scorePlayerSpace;
            }
        }

        private void DrawScoreboardPlayer(float posX, float posY, ColorRgba color, HudObjectInstance hunter, int slot)
        {
            hunter.PositionX = (posX - 40) / 256f;
            hunter.PositionY = (posY - 13) / 192f;
            _scene.DrawHudObject(hunter, mode: 2);
            int stars = GameState.Stars[slot];
            _starsInst.PositionX = posX / 256f;
            _starsInst.PositionY = posY / 192f;
            _starsInst.SetIndex(stars * 2, _scene);
            _scene.DrawHudObject(_starsInst, mode: 2);
            _starsInst.PositionX = (posX + 32) / 256f;
            _starsInst.SetIndex(stars * 2 + 1, _scene);
            _scene.DrawHudObject(_starsInst, mode: 2);
            string nickname = GameState.Nicknames[slot];
            DrawText2D(posX + 32, posY - 9, Align.Center, 0, nickname, color, fontSpacing: 8);
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
                _ammoBarMeter.Align, _ammoBarPalette, $"{amount:00}");
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
                DrawText2D(230, posY + 18, Align.Center, palette: 0, message);
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
                DrawText2D(29, posY + 18, Align.Center, palette: 0, message);
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
                DrawText2D(x + meter.BarOffsetX, y + meter.BarOffsetY, meter.Align, _healthbarPalette, $"{amount:00}");
                if (meter.MessageId > 0)
                {
                    string message = Strings.GetHudMessage(meter.MessageId);
                    DrawText2D(x + meter.TextOffsetX, y + meter.TextOffsetY, Align.Left, _healthbarPalette, message);
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

        public void ProcessModeHud()
        {
            _locatorInfo.Clear();
            ProcessOpponent();
            if (_scene.GameMode == GameMode.Survival || _scene.GameMode == GameMode.SurvivalTeams)
            {
                ProcessHudSurvival();
            }
            else if (_scene.GameMode == GameMode.Bounty || _scene.GameMode == GameMode.BountyTeams)
            {
                ProcessHudBounty();
            }
            else if (_scene.GameMode == GameMode.Capture)
            {
                ProcessHudCapture();
            }
            else if (_scene.GameMode == GameMode.Defender || _scene.GameMode == GameMode.DefenderTeams)
            {
                ProcessHudDefender();
            }
            else if (_scene.GameMode == GameMode.Nodes || _scene.GameMode == GameMode.NodesTeams)
            {
                ProcessHudNodes();
            }
            else if (_scene.GameMode == GameMode.PrimeHunter)
            {
                ProcessHudPrimeHunter();
            }
        }

        private void ProcessHudSurvival()
        {
            int reveal = 0;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health == 0 || player.TeamIndex == TeamIndex)
                {
                    continue;
                }
                float alpha = 1;
                if (GameState.RadarPlayers)
                {
                    float past = _scene.ElapsedTime % (120 / 30f);
                    if (past > 32 / 30f)
                    {
                        alpha = 0;
                    }
                    else
                    {
                        float pct = past / (32 / 30f) % 1;
                        if (pct <= 0.5f)
                        {
                            alpha = Lerp(0, 1, pct * 2);
                        }
                        else
                        {
                            alpha = Lerp(1, 0, (pct - 0.5f) * 2);
                        }
                    }
                }
                else
                {
                    if (!player.Flags2.TestFlag(PlayerFlags2.RadarReveal))
                    {
                        continue;
                    }
                    if (player.Flags2.TestFlag(PlayerFlags2.RadarRevealPrevious))
                    {
                        reveal = 2;
                    }
                    else if (reveal == 0)
                    {
                        reveal = 1;
                    }
                }
                Vector3 pos = player.Position;
                if (!player.IsAltForm)
                {
                    pos.Y += 0.75f;
                }
                AddLocatorInfo(pos, _playerLocator, new ColorRgb(31, 31, 31), alpha);
            }
            if (reveal == 1)
            {
                // todo: play voice
                QueueHudMessage(128, 150, 60 / 30f, 0, 234); // COWARD DETECTED!
            }
        }

        private void ProcessHudBounty()
        {
            var goodColor = new ColorRgb(15, 15, 31);
            if (OctolithFlag != null)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.FlagBase)
                    {
                        continue;
                    }
                    AddLocatorInfo(entity.Position, _nodeLocator, goodColor);
                }
            }
            else
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.OctolithFlag)
                    {
                        continue;
                    }
                    var flag = (OctolithFlagEntity)entity;
                    var color = new ColorRgb(31, 31, 31);
                    if (flag.Carrier != null && (_scene.FrameCount & (4 * 2)) != 0) // todo: FPS stuff
                    {
                        color = flag.Carrier.TeamIndex == TeamIndex ? goodColor : new ColorRgb(31, 0, 0);
                    }
                    AddLocatorInfo(flag.Position, _octolithLocator, color);
                }
            }
        }

        private void ProcessHudCapture()
        {
            var goodColor = new ColorRgb(15, 15, 31); // todo: share common colors
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.OctolithFlag)
                {
                    continue;
                }
                var flag = (OctolithFlagEntity)entity;
                if (flag.Carrier != this)
                {
                    ColorRgb color = Metadata.TeamColors[flag.Data.TeamId];
                    if (flag.Carrier != null && (_scene.FrameCount & (4 * 2)) != 0) // todo: FPS stuff
                    {
                        color = flag.Carrier.TeamIndex == TeamIndex ? goodColor : new ColorRgb(31, 0, 0);
                    }
                    AddLocatorInfo(flag.Position, _octolithLocator, color);
                    if (OctolithFlag != null && flag.Data.TeamId == TeamIndex)
                    {
                        AddLocatorInfo(flag.BasePosition, _nodeLocator, goodColor);
                    }
                }
            }
        }

        private void ProcessHudDefender()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.NodeDefense)
                {
                    continue;
                }
                var defense = (NodeDefenseEntity)entity;
                ColorRgb color;
                if (defense.CurrentTeam == 4)
                {
                    color = new ColorRgb(31, 31, 31);
                }
                else if (GameState.Teams)
                {
                    Debug.Assert(defense.CurrentTeam == 0 || defense.CurrentTeam == 1);
                    color = Metadata.TeamColors[defense.CurrentTeam];
                }
                else if (defense.CurrentTeam == TeamIndex)
                {
                    color = new ColorRgb(15, 15, 31);
                }
                else
                {
                    color = new ColorRgb(31, 0, 0);
                }
                AddLocatorInfo(defense.Position, _nodeLocator, color);
            }
        }

        private int _nodeBonusOpponent = -1;
        private bool _mainNodeBonus = false;
        private readonly int[] _teamNodeCounts = new int[4];
        public int _nodesHudState = 0;
        public int _nodesProgressAmount = 0;

        private void ProcessHudNodes()
        {
            _nodeBonusOpponent = -1;
            _mainNodeBonus = false;
            for (int i = 0; i < 4; i++)
            {
                _teamNodeCounts[i] = 0;
            }
            bool showBar = false;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.NodeDefense)
                {
                    continue;
                }
                var defense = (NodeDefenseEntity)entity;
                ColorRgb color;
                if (defense.CurrentTeam == 4)
                {
                    if (defense.Blinking)
                    {
                        if (GameState.Teams)
                        {
                            Debug.Assert(defense.OccupyingTeam == 0 || defense.OccupyingTeam == 1);
                            color = Metadata.TeamColors[defense.OccupyingTeam];
                        }
                        else if (defense.OccupyingTeam == TeamIndex)
                        {
                            color = new ColorRgb(15, 15, 31);
                        }
                        else
                        {
                            color = new ColorRgb(31, 0, 0);
                        }
                    }
                    else
                    {
                        color = new ColorRgb(31, 31, 31);
                    }
                }
                else if (GameState.Teams)
                {
                    color = Metadata.TeamColors[defense.Blinking ? defense.OccupyingTeam : defense.CurrentTeam];
                }
                else
                {
                    if (defense.CurrentTeam == TeamIndex)
                    {
                        if (!defense.Blinking || defense.OccupyingTeam == TeamIndex)
                        {
                            color = new ColorRgb(15, 15, 31);
                        }
                        else
                        {
                            color = new ColorRgb(31, 0, 0);
                        }
                    }
                    else if (defense.Blinking && defense.OccupyingTeam == TeamIndex)
                    {
                        color = new ColorRgb(15, 15, 31);
                    }
                    else
                    {
                        color = new ColorRgb(31, 0, 0);
                    }
                }
                AddLocatorInfo(defense.Position, _nodeLocator, color);
                if (defense.CurrentTeam != 4 && defense.OccupyingTeam == 4)
                {
                    int count = _teamNodeCounts[defense.CurrentTeam] + 1;
                    _teamNodeCounts[defense.CurrentTeam] = count;
                    if (count > 1)
                    {
                        if (defense.CurrentTeam == TeamIndex)
                        {
                            _mainNodeBonus = true;
                        }
                        else if (_nodeBonusOpponent == -1 || count > _teamNodeCounts[_nodeBonusOpponent])
                        {
                            _nodeBonusOpponent = defense.CurrentTeam;
                        }
                    }
                }
                if (defense.OccupiedBy[SlotIndex])
                {
                    showBar = true;
                    if (_nodesHudState == 0)
                    {
                        QueueHudMessage(128, 133, 45 / 30f, 17, 205); // acquiring node
                        _nodesProgressAmount = 0;
                        _nodesHudState = 1;
                    }
                    else if (_nodesHudState == 1)
                    {
                        _nodesProgressAmount = (int)MathF.Round(Lerp(0, 40, defense.Progress / (300 / 30f)));
                    }
                }
            }
            if (!showBar && _nodesHudState != 0)
            {
                ClearHudMessage(mask: 16);
                _nodesHudState = 0;
            }
        }

        private bool _hudIsPrimeHunter = false;
        private float _primeHunterTextTimer = 0;

        private void ProcessHudPrimeHunter()
        {
            if (GameState.PrimeHunter == SlotIndex)
            {
                if (!_hudIsPrimeHunter)
                {
                    _primeHunterInst.SetAnimation(start: 0, target: 1, frames: 20, loop: true);
                    _primeHunterTextTimer = 90 / 30f;
                    _hudIsPrimeHunter = true;
                }
                if (_primeHunterTextTimer > 0)
                {
                    _primeHunterTextTimer -= _scene.FrameTime;
                }
            }
            else
            {
                if (_hudIsPrimeHunter)
                {
                    _hudIsPrimeHunter = false;
                }
                if (GameState.PrimeHunter != -1)
                {
                    PlayerEntity primeHunter = Players[GameState.PrimeHunter];
                    Vector3 pos = primeHunter.Position;
                    if (!primeHunter.IsAltForm)
                    {
                        pos.Y += 0.75f;
                    }
                    AddLocatorInfo(pos, _playerLocator, new ColorRgb(31, 0, 0));
                }
            }
            _primeHunterInst.ProcessAnimation(_scene);
        }

        private void DrawModeHud()
        {
            GameMode mode = _scene.GameMode;
            if (mode == GameMode.SinglePlayer)
            {
                DrawHudAdventure();
            }
            else
            {
                if (mode == GameMode.Battle || mode == GameMode.BattleTeams)
                {
                    DrawHudBattle();
                }
                else if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
                {
                    DrawHudSurvival();
                }
                else if (mode == GameMode.Bounty || mode == GameMode.BountyTeams)
                {
                    DrawHudBounty();
                }
                else if (mode == GameMode.Capture)
                {
                    DrawHudCapture();
                }
                else if (mode == GameMode.Defender || mode == GameMode.DefenderTeams)
                {
                    DrawHudDefender();
                }
                else if (mode == GameMode.Nodes || mode == GameMode.NodesTeams)
                {
                    DrawHudNodes();
                }
                else if (mode == GameMode.PrimeHunter)
                {
                    DrawHudPrimeHunter();
                }
                DrawOpponent();
            }
        }

        private void DrawHudAdventure()
        {
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
                if (!DrawTargetHealthbar(_lastTarget))
                {
                    _lastTarget = null;
                }
            }
            // todo: draw visor name
        }

        private string FormatModeScore(int slot)
        {
            GameMode mode = _scene.GameMode;
            if (mode == GameMode.Battle || mode == GameMode.BattleTeams || mode == GameMode.Capture || mode == GameMode.Nodes
                || mode == GameMode.NodesTeams || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                if (GameState.Teams)
                {
                    return $"{GameState.TeamPoints[Players[slot].TeamIndex]} / {GameState.PointGoal}";
                }
                return $"{GameState.Points[slot]} / {GameState.PointGoal}";
            }
            if (mode == GameMode.Survival || mode == GameMode.SurvivalTeams)
            {
                int lives = Math.Max(GameState.PointGoal - GameState.TeamDeaths[Players[slot].TeamIndex], 0);
                return lives.ToString();
            }
            if (mode == GameMode.Defender || mode == GameMode.DefenderTeams || mode == GameMode.PrimeHunter)
            {
                return $"{FormatTime(TimeSpan.FromSeconds(GameState.Time[slot]))}/" +
                    $"{FormatTime(TimeSpan.FromSeconds(GameState.TimeGoal))}";
            }
            return " ";
        }

        private void DrawModeScore(int messageId, string text)
        {
            float posX = _hudObjects.ScorePosX;
            float posY = _hudObjects.ScorePosY;
            _textSpacingY = 8;
            string message = Strings.GetHudMessage(messageId);
            // the game wraps text here, but the text used will never wrap (and doesn't have newlines)
            DrawText2D(posX, posY, _hudObjects.ScoreAlign, 0, message);
            posY += 9;
            DrawText2D(posX, posY, _hudObjects.ScoreAlign, 0, text);
            _textSpacingY = 0;
        }

        private void DrawHudBattle()
        {
            DrawModeScore(212, FormatModeScore(MainPlayerIndex)); // points
        }

        private void DrawHudSurvival()
        {
            DrawModeScore(213, FormatModeScore(MainPlayerIndex)); // lives left
        }

        private void DrawOctolithInst(int frame)
        {
            bool drawIcon = false;
            if (OctolithFlag != null)
            {
                drawIcon = (_scene.FrameCount & (16 * 2)) != 0; // todo: FPS stuff
                _octolithInst.Alpha = 1;
            }
            else if (GameState.Teams)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.OctolithFlag)
                    {
                        continue;
                    }
                    var flag = (OctolithFlagEntity)entity;
                    if (flag.Carrier != null && flag.Carrier.TeamIndex == TeamIndex)
                    {
                        drawIcon = true;
                        _octolithInst.Alpha = 0.5f;
                        break;
                    }
                }
            }
            if (drawIcon)
            {
                _octolithInst.PositionX = _hudObjects.OctolithPosX / 256f;
                _octolithInst.PositionY = _hudObjects.OctolithPosY / 192f;
                _octolithInst.SetIndex(frame, _scene);
                _scene.DrawHudObject(_octolithInst);
            }
        }

        private void DrawHudBounty()
        {
            DrawModeScore(215, FormatModeScore(MainPlayerIndex)); // octoliths
            DrawOctolithInst(frame: 0);
        }

        private void DrawHudCapture()
        {
            DrawModeScore(216, FormatModeScore(MainPlayerIndex)); // octoliths
            DrawOctolithInst(frame: TeamIndex == 0 ? 4 : 3);
        }

        private void DrawHudDefender()
        {
            DrawModeScore(217, FormatModeScore(MainPlayerIndex)); // ring time
        }

        private void DrawHudNodes()
        {
            DrawModeScore(218, FormatModeScore(MainPlayerIndex)); // points
            DrawNodesBonuses();
            DrawNodesIcons();
            if (_nodesHudState == 1 && !IsHudMessageQueued(mask: 16))
            {
                _nodeProgressMeter.TankAmount = 40;
                _nodeProgressMeter.TankCount = 0;
                DrawMeter(108, 143, _nodesProgressAmount, _nodesProgressAmount, palette: 0,
                    _nodeProgressMeter, drawText: false, drawTanks: false);
                string message = Strings.GetHudMessage(204); // progress
                DrawText2D(128, 133, Align.Center, palette: 0, message);
            }
        }

        private void DrawNodesBonuses()
        {
            string message = Strings.GetHudMessage(210); // bonus
            if (_mainNodeBonus)
            {
                _nodesInst.PositionX = _hudObjects.NodeBonusPosX / 256f;
                _nodesInst.PositionY = _hudObjects.NodeBonusPosY / 192f;
                _nodesInst.SetIndex(GameState.Teams && TeamIndex == 0 ? 2 : 4, _scene);
                _scene.DrawHudObject(_nodesInst);
                string text = $"x {_teamNodeCounts[TeamIndex]}";
                DrawText2D(_hudObjects.NodeBonusPosX + 12, _hudObjects.NodeBonusPosY + 2, Align.Left, 0, text);
                DrawText2D(_hudObjects.NodeBonusPosX, _hudObjects.NodeBonusPosY + 10, Align.Left, 0, message);
            }
            float past = _scene.ElapsedTime % (16 / 30f);
            if (_nodeBonusOpponent != -1 && past < 12 / 30f)
            {
                _nodesInst.PositionX = _hudObjects.EnemyBonusPosX / 256f;
                _nodesInst.PositionY = _hudObjects.EnemyBonusPosY / 192f;
                _nodesInst.SetIndex(GameState.Teams && _nodeBonusOpponent == 1 ? 4 : 2, _scene);
                _scene.DrawHudObject(_nodesInst);
                string text = $"x {_teamNodeCounts[_nodeBonusOpponent]}";
                DrawText2D(_hudObjects.EnemyBonusPosX + 12, _hudObjects.EnemyBonusPosY + 2, Align.Left, 2, text);
                DrawText2D(_hudObjects.EnemyBonusPosX, _hudObjects.EnemyBonusPosY + 10, Align.Left, 2, message);
            }
        }

        private void DrawNodesIcons()
        {
            float startX = 12;
            int nodeCount = 0;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type == EntityType.NodeDefense)
                {
                    nodeCount++;
                }
            }
            if (nodeCount < 4)
            {
                startX = (16 * nodeCount / 2) - 12;
            }
            float posX = 0;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.NodeDefense)
                {
                    continue;
                }
                var defense = (NodeDefenseEntity)entity;
                int frame;
                if (defense.CurrentTeam == 4)
                {
                    if (defense.Blinking)
                    {
                        if (GameState.Teams)
                        {
                            frame = defense.OccupyingTeam == 0 ? 2 : 4;
                        }
                        else
                        {
                            frame = defense.OccupyingTeam == TeamIndex ? 4 : 2;
                        }
                    }
                    else
                    {
                        frame = 0;
                    }
                }
                else if (GameState.Teams)
                {
                    if (defense.Blinking)
                    {
                        frame = defense.OccupyingTeam == 0 ? 2 : 4;
                    }
                    else
                    {
                        frame = defense.CurrentTeam == 0 ? 2 : 4;
                    }
                }
                else
                {
                    if (defense.CurrentTeam == TeamIndex)
                    {
                        if (!defense.Blinking || defense.OccupyingTeam == TeamIndex)
                        {
                            frame = 4;
                        }
                        else
                        {
                            frame = 2;
                        }
                    }
                    else if (defense.Blinking && defense.OccupyingTeam == TeamIndex)
                    {
                        frame = 4;
                    }
                    else
                    {
                        frame = 2;
                    }
                }
                _nodesInst.PositionX = (_hudObjects.NodeIconPosX + startX - posX) / 256f;
                _nodesInst.PositionY = (_hudObjects.NodeIconPosY - 8) / 192f;
                _nodesInst.SetIndex(frame, _scene);
                _scene.DrawHudObject(_nodesInst);
                posX += 16;
            }
            string text = Strings.GetHudMessage(8); // NODES
            DrawText2D(_hudObjects.NodeTextPosX, _hudObjects.NodeTextPosY, Align.Center, 0, text);
        }

        private void DrawHudPrimeHunter()
        {
            if (_hudIsPrimeHunter)
            {
                float posX = _hudObjects.PrimePosX;
                float posY = _hudObjects.PrimePosY;
                _primeHunterInst.PositionX = (posX - 16) / 256f;
                _primeHunterInst.PositionY = (posY - 16) / 192f;
                _scene.DrawHudObject(_primeHunterInst);
                if (_primeHunterTextTimer > 0)
                {
                    float elapsed = (90 / 30f) - _primeHunterTextTimer;
                    int length = (int)MathF.Ceiling(elapsed / (1 / 30f));
                    string message = Strings.GetHudMessage(11); // prime hunter
                    _textSpacingY = 8;
                    DrawText2D(posX + _hudObjects.PrimeTextPosX, posY + _hudObjects.PrimeTextPosY,
                        _hudObjects.PrimeAlign, 0, message, maxLength: length);
                    _textSpacingY = 0;
                }
            }
            DrawModeScore(214, FormatModeScore(MainPlayerIndex)); // prime time
        }

        private int _doubleDamageSpeed = 0;
        private float _doubleDamageTextTimer = 0;
        private float _doubleDamageIconTimer = 0;

        private void UpdateDoubleDamageSpeed(int speed)
        {
            _doubleDamageSpeed = speed;
            _doubleDamageIconTimer = 0;
            if (speed == 1)
            {
                _doubleDamageTextTimer = 60 / 30f;
            }
        }

        private void ProcessDoubleDamageHud()
        {
            if (_doubleDmgTimer > 0)
            {
                if (_doubleDamageTextTimer > 0)
                {
                    _doubleDamageTextTimer -= _scene.FrameTime;
                }
                _doubleDamageIconTimer += _scene.FrameTime;
            }
        }

        private void DrawDoubleDamageHud()
        {
            if (_doubleDmgTimer > 0)
            {
                float posX = _hudObjects.DblDmgPosX;
                float posY = _hudObjects.DblDmgPosY;
                _doubleDamageInst.PositionX = (posX - 16) / 256f;
                _doubleDamageInst.PositionY = (posY - 16) / 192f;
                int frame = 0;
                if (_doubleDamageSpeed == 1)
                {
                    float past = _doubleDamageIconTimer % (35 / 30f);
                    if (past >= 30 / 30f)
                    {
                        frame = 1;
                    }
                }
                else if (_doubleDamageSpeed == 2)
                {
                    float past = _doubleDamageIconTimer % (25 / 30f);
                    if (past >= 20 / 30f)
                    {
                        frame = 1;
                    }
                }
                else if (_doubleDamageSpeed == 3)
                {
                    float past = _doubleDamageIconTimer % (10 / 30f);
                    if (past >= 5 / 30f)
                    {
                        frame = 1;
                    }
                }
                _doubleDamageInst.SetIndex(frame, _scene);
                _doubleDamageInst.Alpha = 0.5f;
                _scene.DrawHudObject(_doubleDamageInst);
                if (_doubleDamageTextTimer > 0)
                {
                    float elapsed = (60 / 30f) - _doubleDamageTextTimer;
                    int length = (int)MathF.Ceiling(elapsed / (1 / 30f));
                    string message = Strings.GetHudMessage(3); // double damage
                    _textSpacingY = 10;
                    DrawText2D(posX + _hudObjects.DblDmgTextPosX, posY + _hudObjects.DblDmgTextPosY,
                        _hudObjects.DblDmgAlign, 0, message, maxLength: length);
                    _textSpacingY = 0;
                }
            }
        }

        private bool _hudCloaking = false;
        private float _cloakTextTimer = 0;

        private void ProcessCloakHud()
        {
            if (_cloakTimer > 0 && Flags2.TestFlag(PlayerFlags2.Cloaking))
            {
                if (!_hudCloaking)
                {
                    _hudCloaking = true;
                    _cloakTextTimer = 45 / 30f;
                }
                if (_cloakTextTimer > 0)
                {
                    _cloakTextTimer -= _scene.FrameTime;
                }
            }
            else
            {
                _hudCloaking = false;
            }
        }

        private void DrawCloakHud()
        {
            if (_cloakTimer > 0 && Flags2.TestFlag(PlayerFlags2.Cloaking))
            {
                float posX = _hudObjects.CloakPosX;
                float posY = _hudObjects.CloakPosY;
                _cloakInst.PositionX = (posX - 16) / 256f;
                _cloakInst.PositionY = (posY - 16) / 192f;
                _cloakInst.Alpha = 0.5f;
                _scene.DrawHudObject(_cloakInst);
                if (_cloakTextTimer > 0)
                {
                    float elapsed = (45 / 30f) - _cloakTextTimer;
                    int length = (int)MathF.Ceiling(elapsed / (1 / 30f));
                    string message = Strings.GetHudMessage(4); // cloak
                    DrawText2D(posX + _hudObjects.CloakTextPosX, posY + _hudObjects.CloakTextPosY,
                        _hudObjects.CloakAlign, 0, message, maxLength: length);
                }
            }
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
                DrawText2D(_hudObjects.EnemyHealthTextPosX, _hudObjects.EnemyHealthTextPosY, Align.Center, palette, text);
            }
            return current > 0;
        }

        private float _opponentHealthbarTimer = 0;
        private int _opponentIndex = -1;

        private void UpdateOpponent(int slot)
        {
            if (_scene.Multiplayer && slot != SlotIndex)
            {
                _opponentHealthbarTimer = 60 / 30f;
                _opponentIndex = slot;
            }
        }

        private void ProcessOpponent()
        {
            if (_opponentIndex != -1 && _opponentHealthbarTimer > 0)
            {
                _opponentHealthbarTimer -= _scene.FrameTime;
                if (_opponentHealthbarTimer <= 0)
                {
                    _opponentHealthbarTimer = 0;
                    _opponentIndex = -1;
                }
            }
        }

        private void DrawOpponent()
        {
            if (_opponentIndex == -1 || _opponentHealthbarTimer == 0 || !Features.TopScreenTargetInfo)
            {
                return;
            }
            PlayerEntity opponent = Players[_opponentIndex];
            float posX = 93;
            float posY = 182;
            string nickname = GameState.Nicknames[_opponentIndex];
            DrawText2D(posX, posY, Align.Center, 0, nickname);
            HudObjectInstance portrait = _hunterInsts[(int)opponent.Hunter];
            portrait.PositionX = (posX - 16) / 256f;
            portrait.PositionY = (posY - 33) / 192f;
            _scene.DrawHudObject(portrait);
            posX += 18;
            posY -= 26;
            int remainingAmount = opponent.Health >= Values.EnergyTank ? opponent.Health - Values.EnergyTank : 0;
            _enemyHealthMeter.TankAmount = Values.EnergyTank;
            _enemyHealthMeter.TankCount = opponent.HealthMax / Values.EnergyTank;
            _enemyHealthMeter.Length = 72;
            DrawMeter(posX, posY, Values.EnergyTank - 1, opponent.Health, 0, _enemyHealthMeter,
                drawText: false, drawTanks: false);
            DrawMeter(posX, posY + 5, Values.EnergyTank - 1, remainingAmount, 0, _enemyHealthMeter,
                drawText: false, drawTanks: false);
            string score = FormatModeScore(opponent.SlotIndex);
            DrawText2D(posX + 5, posY + 14, Align.Left, 0, score);
        }

        private void DrawModeRules()
        {
            string? header = _rulesLines[0];
            Debug.Assert(header != null);
            DrawText2D(128, 10, Align.Center, 0, header, new ColorRgba(0x7FDE));
            int totalCharacters = (int)(_scene.ElapsedTime / (1 / 30f));
            float posY = 28;
            _textSpacingY = 8;
            for (int i = 1; i < _rulesInfo.Count; i++)
            {
                (int prevLength, _) = _rulesLengths[i - 1];
                int characters = totalCharacters - prevLength;
                if (characters <= 0)
                {
                    break;
                }
                string? line = _rulesLines[i];
                Debug.Assert(line != null);
                float posX = _rulesInfo.Offsets[i] + 12;
                DrawText2D(posX, posY, Align.Left, 0, line, new ColorRgba(0x7F5A), maxLength: characters);
                posY += 13 + _rulesLengths[i].Newlines * 8;
                // todo: play SFX, somewhere
            }
            _textSpacingY = 0;
        }

        private float _textSpacingY = 0;

        // todo: size/shape (seemingly only used by the bottom screen rank, which is 16x16/square instead of 8x8/square)
        private Vector2 DrawText2D(float x, float y, Align type, int palette, ReadOnlySpan<char> text,
            ColorRgba? color = null, float alpha = 1, float fontSpacing = -1, int maxLength = -1)
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
            if (maxLength != -1)
            {
                length = Math.Min(length, maxLength);
            }
            if (length == 0)
            {
                return new Vector2(x, y);
            }
            _textInst.Alpha = alpha;
            float spacingY = _textSpacingY == 0 ? (fontSpacing == -1 ? 12 : fontSpacing) : _textSpacingY;
            if (type == Align.Left)
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
            else if (type == Align.Right)
            {
                float startX = x;
                int start = 0;
                int end = 0;
                do
                {
                    end = text[start..].IndexOf('\n');
                    if (end == -1 || length < end)
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
            else if (type == Align.Center)
            {
                float startX = x;
                int start = 0;
                int end = 0;
                do
                {
                    end = text[start..].IndexOf('\n');
                    if (end == -1 || length < end)
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
            else if (type == Align.Type3)
            {
                // todo: this
                Debug.Assert(false);
            }
            return new Vector2(x, y);
        }

        public void QueueHudMessage(float x, float y, float duration, byte category, int messageId)
        {
            string text = Strings.GetHudMessage(messageId);
            QueueHudMessage(x, y, Align.Center, 256, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, int maxWidth, float duration, byte category, int messageId)
        {
            string text = Strings.GetHudMessage(messageId);
            QueueHudMessage(x, y, Align.Center, maxWidth, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, float duration, byte category, string text)
        {
            QueueHudMessage(x, y, Align.Center, 256, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, int maxWidth, float duration, byte category, string text)
        {
            QueueHudMessage(x, y, Align.Center, maxWidth, 8, new ColorRgba(0x3FEF), 1, duration, category, text);
        }

        public void QueueHudMessage(float x, float y, Align align, int maxWidth, float fontSize,
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
            message.Align = align;
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
            dest[c] = '\0';
            return lines;
        }

        private void ClearHudMessage(int mask)
        {
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                HudMessage message = _hudMessageQueue[i];
                if ((mask & message.Category) != 0)
                {
                    message.Lifetime = 0;
                }
            }
        }

        private bool IsHudMessageQueued(int mask)
        {
            for (int i = 0; i < _hudMessageQueue.Count; i++)
            {
                HudMessage message = _hudMessageQueue[i];
                if ((mask & message.Category) != 0 && message.Lifetime > 0)
                {
                    return true;
                }
            }
            return false;
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
                    DrawText2D(message.Position.X, message.Position.Y, message.Align, palette: 0,
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
            public Align Align { get; set; }
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
