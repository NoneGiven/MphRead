using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MphRead.Entities.Enemies;
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
        private HudMeter _scanProgressMeter = null!;

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

        private HudObjectInstance _messageBoxInst = null!;
        private HudObjectInstance _messageSpacerInst = null!;
        private HudObjectInstance _dialogButtonInst = null!;
        private HudObjectInstance _dialogArrowInst = null!;
        private HudObjectInstance _dialogCrystalInst = null!;
        private HudObjectInstance _dialogPickupInst = null!;
        private HudObjectInstance _dialogFrameInst = null!;

        private ModelInstance _filterModel = null!;
        private bool _showScoreboard = false;
        private int _iceLayerBindingId = -1;
        private int _helmetBindingId = -1;
        private int _helmetDropBindingId = -1;
        private int _visorBindingId = -1;
        private int _scanBindingId = -1;
        private readonly int[] _dialogBindingIds = new int[5] { -1, -1, -1, -1, -1 };

        private IReadOnlyList<ColorRgba> _textPaletteData = null!;
        private IReadOnlyList<ColorRgba> _dialogPaletteData = null!;

        public void SetUpHud()
        {
            (_iceLayerBindingId, _) = HudInfo.CharMapToTexture(HudElements.IceLayer,
                startX: 16, startY: 0, tilesX: 32, tilesY: 32, _scene);
            (_helmetBindingId, _) = HudInfo.CharMapToTexture(_hudObjects.Helmet, _scene);
            (_helmetDropBindingId, _) = HudInfo.CharMapToTexture(_hudObjects.HelmetDrop, _scene);
            (_visorBindingId, IReadOnlyList<ushort>? visorPal) = HudInfo.CharMapToTexture(_hudObjects.Visor,
                startX: 0, startY: 0, tilesX: 0, tilesY: 32, _scene);
            if (Hunter == Hunter.Samus)
            {
                visorPal = null;
            }
            (_scanBindingId, _) = HudInfo.CharMapToTexture(_hudObjects.ScanVisor, startX: 0, startY: 96,
                tilesX: 0, tilesY: 32, _scene, visorPal);
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
            HudObject samusSubBar = healthbarSub;
            if (Hunter != Hunter.Samus)
            {
                samusSubBar = HudInfo.GetHudObject(HudElements.HunterObjects[(int)Hunter.Samus].HealthBarB);
            }
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
                _enemyHealthMeter.BarInst = new HudObjectInstance(samusSubBar.Width, samusSubBar.Height);
                _enemyHealthMeter.BarInst.SetCharacterData(samusSubBar.CharacterData, _scene);
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
            _textPaletteData = ammoBar.PaletteData;
            HudObject weaponIcon = HudInfo.GetHudObject(_hudObjects.WeaponIcon);
            _weaponIconInst = new HudObjectInstance(weaponIcon.Width, weaponIcon.Height);
            _weaponIconInst.SetCharacterData(weaponIcon.CharacterData, _scene);
            _weaponIconInst.SetPaletteData(weaponIcon.PaletteData, _scene);
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
            _textInst = new HudObjectInstance(width: 8, height: 8, maxWidth: 16, maxHeight: 16);
            _textInst.SetCharacterData(Font.Normal.CharacterData, _scene);
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
            else
            {
                _scanCornerObj = HudInfo.GetHudObject(HudElements.ScanCorner);
                _scanCornerSmallObj = HudInfo.GetHudObject(HudElements.ScanCornerSmall);
                _scanCornerInst = new HudObjectInstance(_scanCornerObj.Width, _scanCornerObj.Height);
                _scanCornerInst.SetCharacterData(_scanCornerObj.CharacterData, _scene);
                _scanCornerInst.Enabled = true;
                HudObject lineHoriz = HudInfo.GetHudObject(HudElements.ScanLineHoriz);
                _scanLineHorizInst = new HudObjectInstance(lineHoriz.Width, lineHoriz.Height);
                _scanLineHorizInst.SetCharacterData(lineHoriz.CharacterData, _scene);
                _scanLineHorizInst.Enabled = true;
                HudObject lineVert = HudInfo.GetHudObject(HudElements.ScanLineVert);
                _scanLineVertInst = new HudObjectInstance(lineVert.Width, lineVert.Height);
                _scanLineVertInst.SetCharacterData(lineVert.CharacterData, _scene);
                _scanLineVertInst.Enabled = true;
                if (Hunter == Hunter.Samus)
                {
                    _scanCornerInst.SetPaletteData(_scanCornerObj.PaletteData, _scene);
                    _scanLineHorizInst.SetPaletteData(lineHoriz.PaletteData, _scene);
                    _scanLineVertInst.SetPaletteData(lineVert.PaletteData, _scene);
                }
                else
                {
                    _scanCornerInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
                    _scanLineHorizInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
                    _scanLineVertInst.SetPaletteData(_targetCircleObj.PaletteData, _scene);
                }
                for (int i = 0; i < _scanIconInsts.Length; i++)
                {
                    HudObject scanIcon = HudInfo.GetHudObject(HudElements.ScanIcons[i]);
                    var scanIconInst = new HudObjectInstance(scanIcon.Width, scanIcon.Height);
                    scanIconInst.SetCharacterData(scanIcon.CharacterData, _scene);
                    scanIconInst.SetPaletteData(scanIcon.PaletteData, _scene);
                    scanIconInst.Enabled = true;
                    _scanIconInsts[i] = scanIconInst;
                }
                _scanProgressMeter = HudElements.SubHealthbars[(int)Hunter.Samus];
                _scanProgressMeter.BarInst = new HudObjectInstance(samusSubBar.Width, samusSubBar.Height);
                _scanProgressMeter.BarInst.SetCharacterData(samusSubBar.CharacterData, _scene);
                _scanProgressMeter.BarInst.SetPaletteData(healthbarSub.PaletteData, _scene);
                _scanProgressMeter.Horizontal = true;
                _scanProgressMeter.BarInst.Enabled = true;
                HudObject samusAmmo = ammoBar;
                if (Hunter != Hunter.Samus)
                {
                    samusAmmo = HudInfo.GetHudObject(HudElements.HunterObjects[(int)Hunter.Samus].AmmoBar);
                }
                _dialogPaletteData = samusAmmo.PaletteData;
                HudObject messageBox = HudInfo.GetHudObject(HudElements.MessageBox);
                _messageBoxInst = new HudObjectInstance(messageBox.Width, messageBox.Height);
                _messageBoxInst.SetCharacterData(messageBox.CharacterData, _scene);
                _messageBoxInst.SetPaletteData(samusAmmo.PaletteData, _scene);
                _messageBoxInst.SetAnimationFrames(messageBox.AnimParams);
                _messageBoxInst.Enabled = true;
                HudObject messageSpacer = HudInfo.GetHudObject(HudElements.MessageSpacer);
                _messageSpacerInst = new HudObjectInstance(messageSpacer.Width, messageSpacer.Height);
                _messageSpacerInst.SetCharacterData(messageSpacer.CharacterData, _scene);
                _messageSpacerInst.SetPaletteData(samusAmmo.PaletteData, _scene);
                _messageSpacerInst.Enabled = true;
                for (int i = 0; i < 5; i++)
                {
                    (_dialogBindingIds[i], _) = HudInfo.CharMapToTexture(HudElements.MapScan,
                        startX: 0, startY: 0, tilesX: 32, tilesY: 24, _scene, paletteId: i);
                }
                HudObject dialogButton = HudInfo.GetHudObject(HudElements.DialogButton);
                _dialogButtonInst = new HudObjectInstance(dialogButton.Width, dialogButton.Height);
                _dialogButtonInst.SetCharacterData(dialogButton.CharacterData, _scene);
                _dialogButtonInst.SetPaletteData(dialogButton.PaletteData, _scene);
                _dialogButtonInst.SetAnimationFrames(dialogButton.AnimParams);
                _dialogButtonInst.Enabled = true;
                HudObject dialogArrow = HudInfo.GetHudObject(HudElements.DialogArrow);
                _dialogArrowInst = new HudObjectInstance(dialogArrow.Width, dialogArrow.Height);
                _dialogArrowInst.SetCharacterData(dialogArrow.CharacterData, _scene);
                _dialogArrowInst.SetPaletteData(dialogArrow.PaletteData, _scene);
                _dialogArrowInst.SetAnimationFrames(dialogArrow.AnimParams);
                _dialogArrowInst.Enabled = true;
                HudObject dialogCrystal = HudInfo.GetHudObject(HudElements.DialogCrystal);
                _dialogCrystalInst = new HudObjectInstance(dialogCrystal.Width, dialogCrystal.Height);
                _dialogCrystalInst.SetCharacterData(dialogCrystal.CharacterData, _scene);
                _dialogCrystalInst.SetPaletteData(dialogCrystal.PaletteData, _scene);
                _dialogCrystalInst.SetAnimationFrames(dialogCrystal.AnimParams);
                _dialogCrystalInst.Enabled = true;
                HudObject dialogPickup = HudInfo.GetHudObject(HudElements.DialogPickup);
                _dialogPickupInst = new HudObjectInstance(dialogPickup.Width, dialogPickup.Height);
                _dialogPickupInst.SetCharacterData(dialogPickup.CharacterData, _scene);
                _dialogPickupInst.SetPaletteData(dialogPickup.PaletteData, _scene);
                _dialogPickupInst.Enabled = true;
                HudObject dialogFrame = HudInfo.GetHudObject(HudElements.DialogFrame);
                _dialogFrameInst = new HudObjectInstance(dialogFrame.Width, dialogFrame.Height);
                _dialogFrameInst.SetCharacterData(dialogFrame.CharacterData, _scene);
                _dialogFrameInst.SetPaletteData(dialogFrame.PaletteData, _scene);
                _dialogFrameInst.Enabled = true;
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

        private float _hudShiftX = 0;
        private float _hudShiftY = 0;
        private float _objShiftX = 0;
        private float _objShiftY = 0;
        private bool _hudWeaponMenuOpen = false;
        public bool ScanVisor { get; private set; }

        public void UpdateHud()
        {
            UpdateScanState();
            if (!_scene.Multiplayer)
            {
                UpdateDialogs();
            }
            ProcessDoubleDamageHud();
            ProcessCloakHud();
            UpdateHealthbars();
            UpdateAmmoBar();
            UpdateVisorMessage();
            _weaponIconInst.ProcessAnimation(_scene);
            _boostInst.ProcessAnimation(_scene);
            UpdateBoostBombs();
            UpdateDamageIndicators();
            UpdateDisruptedState();
            UpdateWhiteoutState();
            WeaponSelection = CurrentWeapon;
            if (Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen))
            {
                if (!_hudWeaponMenuOpen)
                {
                    if (ScanVisor)
                    {
                        SwitchVisors(reset: false);
                    }
                    _soundSource.PlayFreeSfx(SfxId.HUD_WEAPON_SWITCH1);
                }
                _hudWeaponMenuOpen = true;
                UpdateWeaponSelect();
            }
            else
            {
                _hudWeaponMenuOpen = false;
            }
            if (ScanVisor)
            {
                UpdateScanHud();
            }
            _targetCircleInst.Enabled = false;
            _ammoBarMeter.BarInst.Enabled = false;
            _weaponIconInst.Enabled = false;
            _damageIndicator.Active = false;
            _scene.Layer1Info.BindingId = -1;
            _scene.Layer2Info.BindingId = -1;
            _scene.Layer3Info.BindingId = -1;
            _scene.Layer4Info.BindingId = -1;
            _scene.Layer5Info.BindingId = -1;
            _scene.Layer1Info.ShiftX = 0;
            _scene.Layer1Info.ShiftY = 0;
            _scene.Layer2Info.ShiftX = 0;
            _scene.Layer2Info.ShiftY = 0;
            _scene.Layer3Info.ShiftX = 0;
            _scene.Layer3Info.ShiftY = 0;
            _scene.Layer4Info.ShiftX = 0;
            _scene.Layer4Info.ShiftY = 0;
            _scene.Layer5Info.ShiftX = 0;
            _scene.Layer5Info.ShiftY = 0;
            if (CameraSequence.Current?.Flags.TestFlag(CamSeqFlags.BlockInput) == true)
            {
                return;
            }
            if (_health > 0 || _deathCountdown > 0)
            {
                if (!IsAltForm && !IsMorphing && !IsUnmorphing)
                {
                    if (!Flags1.TestFlag(PlayerFlags1.WeaponMenuOpen) && !_showScoreboard
                        && GameState.MatchState == MatchState.InProgress)
                    {
                        if (_drawIceLayer)
                        {
                            // ice layer
                            _scene.Layer4Info.BindingId = _iceLayerBindingId;
                            _scene.Layer4Info.Alpha = 9 / 16f;
                            _scene.Layer4Info.ScaleX = -1;
                            _scene.Layer4Info.ScaleY = -1;
                        }
                        // helmet back
                        _scene.Layer3Info.BindingId = _helmetDropBindingId;
                        _scene.Layer3Info.Alpha = Features.HelmetOpacity;
                        _scene.Layer3Info.ScaleX = 2;
                        _scene.Layer3Info.ScaleY = 256 / 192f;
                        // visor
                        if (ScanVisor)
                        {
                            _scene.Layer1Info.BindingId = _scanBindingId;
                            _scene.Layer1Info.MaskId = _scanBindingId;
                        }
                        else
                        {
                            _scene.Layer1Info.BindingId = _visorBindingId;
                            _scene.Layer1Info.MaskId = -1;
                        }
                        _scene.Layer1Info.Alpha = Features.VisorOpacity;
                        _scene.Layer1Info.ScaleX = 1;
                        _scene.Layer1Info.ScaleY = 256 / 192f;
                        // helmet front
                        _scene.Layer2Info.BindingId = _helmetBindingId;
                        _scene.Layer2Info.Alpha = Features.HelmetOpacity;
                        _scene.Layer2Info.ScaleX = 2;
                        _scene.Layer2Info.ScaleY = 256 / 192f;
                        _scene.Layer1Info.ShiftX = _hudShiftX / 256f;
                        _scene.Layer1Info.ShiftY = _hudShiftY / 192f;
                        _scene.Layer2Info.ShiftX = _hudShiftX / 256f;
                        _scene.Layer2Info.ShiftY = _hudShiftY / 192f;
                        _scene.Layer3Info.ShiftX = -_hudShiftX / 4 / 256f;
                        _scene.Layer3Info.ShiftY = -_hudShiftY / 4 / 192f;
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

        private int _hudPreviousWeaponSelection = -1;

        private void UpdateWeaponSelect()
        {
            BeamType previousWeapon = WeaponSelection;
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
            if (selection != _hudPreviousWeaponSelection)
            {
                _soundSource.PlayFreeSfx(SfxId.HUD_WEAPON_SWITCH2);
                _hudPreviousWeaponSelection = selection;
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
            if (ScanVisor)
            {
                return;
            }
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

        private void HudOnMorphStart()
        {
            _targetCircleInst.SetIndex(0, _scene);
            SetCombatVisor();
        }

        private void HudOnWeaponSwitch(BeamType beam)
        {
            SetCombatVisor();
            if (beam != BeamType.Imperialist || _sniperReticle)
            {
                _sniperReticle = false;
                ResetReticle();
            }
            else
            {
                _sniperReticle = true;
                if (!ScanVisor)
                {
                    _targetCircleInst.SetCharacterData(_sniperCircleObj.CharacterData, _sniperCircleObj.Width,
                        _sniperCircleObj.Height, _scene);
                }
            }
            _weaponIconInst.SetAnimation(start: 9, target: 27, frames: 19, afterAnim: (int)beam);
        }

        private void HudOnZoom(bool zoom)
        {
            if (_hudZoom != zoom)
            {
                _hudZoom = zoom;
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

        public int HudWhiteoutState { get; private set; } = -1;
        public float HudWhiteoutFactor { get; private set; } = 0;
        private float _whiteoutAmount = 0;
        private float _whiteoutTime = 0;

        public void BeginWhiteout()
        {
            HudWhiteoutState = 0;
            HudWhiteoutFactor = 0;
            _whiteoutTime = _scene.GlobalElapsedTime;
            UpdateWhiteoutTable(value: 0);
        }

        private void EndWhiteout()
        {
            HudWhiteoutState = -1;
            HudWhiteoutFactor = 0;
        }

        private void UpdateWhiteoutState()
        {
            float GetPosition()
            {
                float time = (_scene.GlobalElapsedTime - _whiteoutTime) * 60;
                float timeSquared = time * time;
                float timeCubed = timeSquared * time;
                float jerk = 0.004f;
                float initAcceleration = 0;
                float initVelocity = 1;
                float initPosition = 0;
                // the game has limits on the acceleration and velocity, but the limits on
                // first the factor and then the position are reached before those
                Debug.Assert(initAcceleration + jerk * time < 120); // acceleration
                Debug.Assert(initVelocity + initAcceleration * time + 0.5f * jerk * timeSquared < 480); // velocity
                return initPosition + initVelocity * time + 0.5f * initAcceleration * timeSquared + 1 / 6f * jerk * timeCubed;
            }

            if (HudWhiteoutState == 0)
            {
                HudWhiteoutFactor += 2.8125f * _scene.FrameTime;
                if (HudWhiteoutFactor >= 1)
                {
                    HudWhiteoutFactor = 1;
                    HudWhiteoutState = 1;
                }
                _whiteoutAmount = GetPosition();
                Debug.Assert(_whiteoutAmount < 96);
            }
            else if (HudWhiteoutState == 1)
            {
                _whiteoutAmount = GetPosition();
                if (_whiteoutAmount >= 96)
                {
                    _whiteoutAmount = 96;
                    HudWhiteoutState = 2;
                    _whiteoutTime = _scene.GlobalElapsedTime;
                }
                UpdateWhiteoutTable(_whiteoutAmount);
            }
            else if (HudWhiteoutState == 2)
            {
                HudWhiteoutFactor = -1; // use the table value directly instead of as a factor
                float time = _scene.GlobalElapsedTime - _whiteoutTime;
                float value = 1 - Math.Min(time / (16 / 30f), 1);
                Array.Fill(HudWhiteoutTable, value);
            }
        }

        public static readonly float[] HudWhiteoutTable = new float[192];

        private void UpdateWhiteoutTable(float value)
        {
            int trunc = (int)value;
            float amount = 0.9975f;
            for (int i = 95; i >= 0; i--)
            {
                int index = i - trunc;
                if (index >= 0)
                {
                    Debug.Assert(index <= 95);
                    float factor = (MathF.Pow(amount, 8) * 32 - 16) / 16f;
                    Debug.Assert(factor >= -1 && factor <= 1);
                    HudWhiteoutTable[index] = factor;
                    HudWhiteoutTable[191 - index] = factor;
                }
                amount -= 0.0105f;
            }
            for (int j = 95; j >= 95 - trunc && j >= 0; j--)
            {
                HudWhiteoutTable[j] = 16;
                HudWhiteoutTable[191 - j] = 16;
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
                DrawDialogs();
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
                if (!_scene.Multiplayer && !GameState.DialogPause)
                {
                    DrawEscapeTime();
                }
                if (Health > 0)
                {
                    if (!GameState.DialogPause)
                    {
                        if (IsAltForm || IsMorphing || IsUnmorphing)
                        {
                            DrawBoostBombs();
                        }
                        else if (!ScanVisor)
                        {
                            DrawAmmoBar();
                            _weaponIconInst.PositionX = (_hudObjects.WeaponIconPosX + _objShiftX) / 256f;
                            _weaponIconInst.PositionY = (_hudObjects.WeaponIconPosY + _objShiftY) / 192f;
                            _weaponIconInst.Alpha = Features.HudOpacity;
                            _scene.DrawHudObject(_weaponIconInst);
                            _targetCircleInst.Alpha = Features.ReticleOpacity;
                            _scene.DrawHudObject(_targetCircleInst);
                        }
                        DrawModeHud();
                        DrawDoubleDamageHud();
                        DrawCloakHud();
                    }
                    // todo: once we have masking that can account for various things (in this case, not drawing the scan lines
                    // on top of the layer for the scan log title box), call DrawModeHud when dialog pause is active
                    if (!GameState.DialogPause || DialogType != DialogType.Event
                        && (Hunter == Hunter.Samus || Hunter == Hunter.Guardian))
                    {
                        DrawHealthbars();
                    }
                }
                DrawQueuedHudMessages();
                DrawDialogs();
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

        private void DrawEscapeTime()
        {
            if (GameState.EscapeTimer < 0)
            {
                return;
            }
            var time = TimeSpan.FromSeconds(GameState.EscapeTimer);
            int palette = time.TotalSeconds < 10 ? 2 : 0;
            string text = $"{time.Hours * 60 + time.Minutes}:{time.Seconds:00}:{time.Milliseconds / 10:00}";
            DrawText2D(128 + _objShiftX, 180 + _objShiftY, Align.Center, palette, text);
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
            DrawMeter(_hudObjects.HealthMainPosX + _objShiftX, _hudObjects.HealthMainPosY + _healthbarYOffset + _objShiftY,
                Values.EnergyTank - 1, _health, _healthbarPalette, _healthbarMainMeter,
                drawText: true, drawTanks: !_scene.Multiplayer, Features.HudOpacity);
            if (_scene.Multiplayer)
            {
                int amount = 0;
                if (_health >= Values.EnergyTank)
                {
                    amount = _health - Values.EnergyTank;
                }
                _healthbarSubMeter.TankAmount = Values.EnergyTank;
                _healthbarSubMeter.TankCount = _healthMax / Values.EnergyTank;
                DrawMeter(_hudObjects.HealthSubPosX + _objShiftX, _hudObjects.HealthSubPosY + _healthbarYOffset + _objShiftY,
                    Values.EnergyTank - 1, amount, _healthbarPalette, _healthbarSubMeter,
                    drawText: false, drawTanks: false, Features.HudOpacity);
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
            DrawMeter(_hudObjects.AmmoBarPosX + _objShiftX, _hudObjects.AmmoBarPosY + _objShiftY, amount, amount,
                _ammoBarPalette, _ammoBarMeter, drawText: false, drawTanks: false, Features.HudOpacity);
            amount /= info.AmmoCost;
            DrawText2D(_hudObjects.AmmoBarPosX + _ammoBarMeter.BarOffsetX + _objShiftX,
                _hudObjects.AmmoBarPosY + _ammoBarMeter.BarOffsetY + _objShiftY,
                _ammoBarMeter.Align, _ammoBarPalette, $"{amount:00}", alpha: Features.HudOpacity);
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

        // skhere
        private void DrawMeter(float x, float y, int baseAmount, int curAmount, int palette,
            HudMeter meter, bool drawText, bool drawTanks, float alpha = 1)
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
                DrawText2D(x + meter.BarOffsetX, y + meter.BarOffsetY, meter.Align, _healthbarPalette, $"{amount:00}", alpha: alpha);
                if (meter.MessageId > 0)
                {
                    string message = Strings.GetHudMessage(meter.MessageId);
                    DrawText2D(x + meter.TextOffsetX, y + meter.TextOffsetY, Align.Left, _healthbarPalette, message, alpha: alpha);
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
                        meter.TankInst.Alpha = alpha;
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
                meter.BarInst.Alpha = alpha;
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
                _soundSource.QueueStream(VoiceId.VOICE_CAMPING, delay: 1);
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
            if (ScanVisor)
            {
                if (_scanning)
                {
                    DrawScanProgress();
                }
                DrawScanObjects();
                if (!GameState.DialogPause)
                {
                    DrawVisorMessage();
                }
                return;
            }
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
                            // todo-gorea: draw healthbar if damaged and some flag
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
            DrawVisorMessage();
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
            float posX = _hudObjects.ScorePosX + _objShiftX;
            float posY = _hudObjects.ScorePosY + _objShiftY;
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
                _octolithInst.PositionX = (_hudObjects.OctolithPosX + _objShiftX) / 256f;
                _octolithInst.PositionY = (_hudObjects.OctolithPosY + _objShiftY) / 192f;
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
                _nodesInst.PositionX = (_hudObjects.NodeBonusPosX + _objShiftX) / 256f;
                _nodesInst.PositionY = (_hudObjects.NodeBonusPosY + _objShiftY) / 192f;
                _nodesInst.SetIndex(GameState.Teams && TeamIndex == 0 ? 2 : 4, _scene);
                _scene.DrawHudObject(_nodesInst);
                string text = $"x {_teamNodeCounts[TeamIndex]}";
                DrawText2D(_hudObjects.NodeBonusPosX + 12 + _objShiftX, _hudObjects.NodeBonusPosY + 2 + _objShiftY,
                    Align.Left, 0, text);
                DrawText2D(_hudObjects.NodeBonusPosX + _objShiftX, _hudObjects.NodeBonusPosY + 10 + _objShiftY,
                    Align.Left, 0, message);
            }
            float past = _scene.ElapsedTime % (16 / 30f);
            if (_nodeBonusOpponent != -1 && past < 12 / 30f)
            {
                _nodesInst.PositionX = (_hudObjects.EnemyBonusPosX + _objShiftX) / 256f;
                _nodesInst.PositionY = (_hudObjects.EnemyBonusPosY + _objShiftY) / 192f;
                _nodesInst.SetIndex(GameState.Teams && _nodeBonusOpponent == 1 ? 4 : 2, _scene);
                _scene.DrawHudObject(_nodesInst);
                string text = $"x {_teamNodeCounts[_nodeBonusOpponent]}";
                DrawText2D(_hudObjects.EnemyBonusPosX + 12 + _objShiftX, _hudObjects.EnemyBonusPosY + 2 + _objShiftY,
                    Align.Left, 2, text);
                DrawText2D(_hudObjects.EnemyBonusPosX + _objShiftX, _hudObjects.EnemyBonusPosY + 10 + _objShiftY,
                    Align.Left, 2, message);
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
                _nodesInst.PositionX = (_hudObjects.NodeIconPosX + startX - posX + _objShiftX) / 256f;
                _nodesInst.PositionY = (_hudObjects.NodeIconPosY - 8 + _objShiftY) / 192f;
                _nodesInst.SetIndex(frame, _scene);
                _scene.DrawHudObject(_nodesInst);
                posX += 16;
            }
            string text = Strings.GetHudMessage(8); // NODES
            DrawText2D(_hudObjects.NodeTextPosX + _objShiftX, _hudObjects.NodeTextPosY + _objShiftY, Align.Center, 0, text);
        }

        private void DrawHudPrimeHunter()
        {
            if (_hudIsPrimeHunter)
            {
                float posX = _hudObjects.PrimePosX + _objShiftX;
                float posY = _hudObjects.PrimePosY + _objShiftY;
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
                float posX = _hudObjects.DblDmgPosX + _objShiftX;
                float posY = _hudObjects.DblDmgPosY + _objShiftY;
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
                float posX = _hudObjects.CloakPosX + _objShiftX;
                float posY = _hudObjects.CloakPosY + _objShiftY;
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
                    var shield = (Enemy42Entity)enemy;
                    max = shield.Slench.HealthMax;
                    current = shield.Slench.Health;
                }
                else if (enemy.EnemyType == EnemyType.GoreaArm)
                {
                    // todo-gorea: get current by subtracting damage from max
                }
                else if (enemy.EnemyType == EnemyType.GoreaSealSphere1)
                {
                    // todo-gorea: get current by subtracting damage from max
                }
                else if (enemy.EnemyType == EnemyType.GoreaSealSphere2)
                {
                    // todo-gorea: get current by subtracting damage from max
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
            DrawMeter(_hudObjects.EnemyHealthPosX + _objShiftX, _hudObjects.EnemyHealthPosY + _objShiftY, max, current,
                palette, _enemyHealthMeter, drawText: false, drawTanks: false);
            int scanId = target.GetScanId();
            if (scanId != 0 && !_scene.Multiplayer && !GameState.StorySave.CheckLogbook(scanId))
            {
                text = Strings.GetMessage('E', 6, StringTables.HudMessagesSP); // enemy
            }
            if (text != null)
            {
                DrawText2D(_hudObjects.EnemyHealthTextPosX + _objShiftX, _hudObjects.EnemyHealthTextPosY + _objShiftY,
                    Align.Center, palette, text);
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
            if (Features.TargetInfoSway)
            {
                posX += _objShiftX;
                posY += _objShiftY;
            }
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

        private int _prevIntroChars = 0;

        private void DrawModeRules()
        {
            string? header = _rulesLines[0];
            Debug.Assert(header != null);
            ColorRgba? color = Paths.IsMphJapan || Paths.IsMphKorea ? null : new ColorRgba(0x7FDE);
            DrawText2D(128, 10, Align.Center, 0, header, color);
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
                color = Paths.IsMphJapan || Paths.IsMphKorea ? null : new ColorRgba(0x7F5A);
                DrawText2D(posX, posY, Align.Left, 0, line, color, maxLength: characters);
                posY += 13 + _rulesLengths[i].Newlines * 8;
            }
            // todo?: ideally this should be in a process method, not draw
            if (totalCharacters > _prevIntroChars && totalCharacters > _rulesLengths[0].Length
                && totalCharacters <= _rulesLengths[_rulesInfo.Count - 1].Length)
            {
                _soundSource.PlayFreeSfx(SfxId.LETTER_BLIP);
                _prevIntroChars = totalCharacters;
            }
            _textSpacingY = 0;
        }

        private bool _usingKanjiFont = false;

        private Font SetUpFont(char firstChar, bool set)
        {
            Font font = Font.Normal;
            if (Scene.Language == Language.Japanese && (Paths.IsMphJapan || Paths.IsMphKorea) && (firstChar & 0xA0) == 0xA0)
            {
                font = Font.Kanji;
                if (set && !_usingKanjiFont)
                {
                    _textInst.SetCharacterData(Font.Kanji.CharacterData, width: 16, height: 16, _scene);
                    _usingKanjiFont = true;
                }
            }
            else if (set && _usingKanjiFont)
            {
                _textInst.SetCharacterData(Font.Normal.CharacterData, width: 8, height: 8, _scene);
                _usingKanjiFont = false;
            }
            return font;
        }

        private float _textSpacingY = 0;

        // todo: size/shape (seemingly only used by the bottom screen rank, which is 16x16/square instead of 8x8/square)
        private Vector2 DrawText2D(float x, float y, Align type, int palette, ReadOnlySpan<char> text,
            ColorRgba? color = null, float alpha = 1, float fontSpacing = -1, int maxLength = -1)
        {
            int padAfter = maxLength;
            if (type == Align.PadCenter)
            {
                maxLength = -1;
            }
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
            Font font = SetUpFont(text[0], set: true);
            _textInst.Alpha = alpha;
            float spacingY = _textSpacingY == 0 ? (fontSpacing == -1 ? 12 : fontSpacing) : _textSpacingY;
            if (type == Align.Left)
            {
                float startX = x;
                for (int i = 0; i < length; i++)
                {
                    int ch = text[i];
                    int orig = ch;
                    if ((ch & 0x80) != 0)
                    {
                        ch = text[++i] & 0x3F | ((ch & 0x1F) << 6);
                    }
                    if (orig == '\n')
                    {
                        x = startX;
                        y += spacingY;
                    }
                    else
                    {
                        int index = ch - font.MinCharacter;
                        float offset = font.Offsets[index] + y;
                        if (orig != ' ')
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
                        x += font.Widths[index];
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
                    if (end != -1)
                    {
                        end += start;
                    }
                    if (end == -1 || length < end)
                    {
                        end = length;
                    }
                    x = startX;
                    for (int i = end - 1; i >= start; i--)
                    {
                        int ch = text[i];
                        int orig = ch;
                        if ((ch & 0x80) != 0)
                        {
                            ch = text[++i] & 0x3F | ((ch & 0x1F) << 6);
                        }
                        int index = ch - font.MinCharacter;
                        x -= font.Widths[index];
                        float offset = font.Offsets[index] + y;
                        if (orig != ' ')
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
            else if (type == Align.Center || type == Align.PadCenter)
            {
                float startX = x;
                int start = 0;
                int end = 0;
                do
                {
                    end = text[start..].IndexOf('\n');
                    if (end != -1)
                    {
                        end += start;
                    }
                    if (end == -1 || length < end)
                    {
                        end = length;
                    }
                    x = startX;
                    float width = 0;
                    for (int i = start; i < end; i++)
                    {
                        int ch = text[i];
                        if ((ch & 0x80) != 0)
                        {
                            ch = text[++i] & 0x3F | ((ch & 0x1F) << 6);
                        }
                        int index = ch - font.MinCharacter;
                        width += font.Widths[index];
                    }
                    x = startX - width / 2;
                    for (int i = start; i < end; i++)
                    {
                        int ch = text[i];
                        int orig = ch;
                        if ((ch & 0x80) != 0)
                        {
                            ch = text[++i] & 0x3F | ((ch & 0x1F) << 6);
                        }
                        int index = ch - font.MinCharacter;
                        float offset = font.Offsets[index] + y;
                        if (orig != ' ')
                        {
                            _textInst.PositionX = x / 256f;
                            _textInst.PositionY = offset / 192f;
                            if (type != Align.PadCenter || i < padAfter)
                            {
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
                        x += font.Widths[index];
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

        private int WrapText(string text, int maxWidth, char[] dest, int maxTiles = 0)
        {
            int lines = 1;
            if (maxWidth <= 0)
            {
                return lines;
            }
            int lineWidth = 0;
            // if the line is broken at a previous space, how much width the now-next line already
            // has written to it. zeroed out if we break without a previous space to break at, since in
            // that case we break after the most recent character and the new line will start empty.
            int widthAfterBreak = 0;
            int breakPos = 0;
            int c = 0;
            if (text.Length == 0)
            {
                return 1;
            }
            Font font = SetUpFont(text[0], set: false);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                dest[c] = ch;
                if (ch == '\n')
                {
                    lineWidth = 0;
                    breakPos = 0;
                    widthAfterBreak = 0;
                    lines++;
                }
                else
                {
                    if (ch == ' ')
                    {
                        breakPos = c;
                        widthAfterBreak = 0;
                    }
                    if (ch >= ' ')
                    {
                        int index = ch;
                        if ((ch & 0x80) != 0)
                        {
                            char next = text[++i];
                            dest[++c] = next;
                            index = next & 0x3F | ((ch & 0x1F) << 6);
                        }
                        index -= font.MinCharacter;
                        int width = font.Widths[index];
                        lineWidth += width;
                        if (ch != ' ')
                        {
                            widthAfterBreak += width;
                        }
                    }
                    // todo?:
                    // the game has a stupid, possibly bugged behavior with scan and other bottom screen messages. the gist is that they're
                    // limited to 90 characters, presumably because of OAM limitations. because spaces are skipped instead of being actually drawn,
                    // they don't count toward the OAM limit, but they do get counted by the game anyway, sort of. basically the game counts
                    // 90 non-space characters, and if it finds that many on one page, it then counts 90 of *any* character, and breaks the
                    // dialog page there. this results in unnecessary breaks with text unexpectedly being pushed to the next page (such as the first
                    // lore scan having "divine" pushed to the second page) even when it could have fit. the initial count also counts newlines
                    // as non-space characters, which is dumb, since they also aren't drawn. to implement this we would have done some/all of:
                    // - keep count of non-space characters/tiles, reset every 3 lines
                    // - (counting newline characters, since the game does, even though that's stupid)
                    // - if we hit 90, count 90 chars (incl. spaces) from the start of the page and break there, starting a new page
                    // - (this uses the break rule of breaking at the previous space, or after the current character if no spaces)
                    // - this potentially pushes some already written text onto a new page, so we may need to undo/reinitialize wrapping on it?
                    // - (main thing is determining if this is necessary; if it is, we may need to track "real" newline characters vs. added breaks?)
                    // - (since we need to undo the breaks we inserted and start fresh, but don't want to erase newlines from the original text)
                    // - (easiest way to do this might be to just set our position back to the start of the new page in both source and dest?)
                    // - also need to start counting 90 non-space characters again from the the start of that page
                    if (i + 1 < text.Length && lineWidth > maxWidth)
                    {
                        if (breakPos == 0 && maxWidth >= 8)
                        {
                            breakPos = c++ + 1;
                            widthAfterBreak = 0;
                        }
                        if (breakPos > 0)
                        {
                            dest[breakPos] = '\n';
                            lineWidth = widthAfterBreak;
                            widthAfterBreak = 0;
                            breakPos = 0;
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
