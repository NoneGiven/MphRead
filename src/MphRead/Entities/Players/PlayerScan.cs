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
        private float _visorMessageTime = 30 / 30f;
        private float _visorMessageTimer = 30 / 30f;
        private int _visorMessageId = 0;
        private bool _visorMessageScrollOut = false;

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
            _visorMessageTimer = _visorMessageTime = 30 / 30f;
            UpdateScanSfx(index: -1, enable: false);
            if (ScanVisor)
            {
                if (!reset && !_silentVisorSwitch)
                {
                    _soundSource.PlayFreeSfx(SfxId.SCAN_VISOR_OFF);
                }
                ScanVisor = false;
                _visorMessageId = 108; // COMBAT VISOR
                _scanning = false;
                _smallReticle = false;
                _smallReticleTimer = 0;
                ResetScanValues();
            }
            else if (!reset)
            {
                UpdateScanSfx(index: 2, enable: true);
                if (!_silentVisorSwitch)
                {
                    _soundSource.PlayFreeSfx(SfxId.SCAN_VISOR_ON2);
                }
                ScanVisor = true;
                _visorMessageId = 107; // SCAN VISOR
                _scanning = false;
                _smallReticle = false;
                _smallReticleTimer = 0;
            }
        }

        private void ResetScanValues()
        {
            _scanComplete = false;
            _scanningTime = 0;
            _scanningTimer = 0;
            _scanningEntity = null;
            _curScanTarget.Entity = null;
            _curScanTarget.CenterDist = Single.MaxValue;
            UpdateScanSfx(index: 1, enable: false);
        }

        private void UpdateScanHud()
        {
            EntityBase? curEnt = _curScanTarget.Entity;
            Vector3 curTargetPos = Vector3.Zero;
            float curScreenX = 0;
            float curScreenY = 0;
            float minCenter = Single.MaxValue;
            _scanTargetCount = 0;
            bool update = false;
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                int scanId = entity.GetScanId();
                if (scanId == 0 || !entity.ScanVisible())
                {
                    continue;
                }
                int category = Strings.GetScanEntryCategory(scanId);
                if (category == 5)
                {
                    // the game does this check later, and wastes the target slot if it's 5
                    continue;
                }
                entity.GetPosition(out Vector3 entPos);
                Matrix.GetProjectedValues(entPos, CameraInfo.Position, _scene.ViewMatrix, _scene.PerspectiveMatrix,
                    out float dist, out float depth, out float scaleInv, out Vector3 targetPos, out Vector2 distPos);
                var screenPos = new Vector2((distPos.X + 1) / 2, (1 - distPos.Y) / 2);
                if (depth < 0)
                {
                    continue;
                }
                bool noMaxDist = false;
                if (entity.Type == EntityType.EnemyInstance)
                {
                    var enemy = (EnemyInstanceEntity)entity;
                    noMaxDist = enemy.Flags.TestFlag(EnemyFlags.NoMaxDistance);
                }
                if (dist >= 24 && !noMaxDist)
                {
                    continue;
                }
                // the game sets a flag if the distance is 12 or greater (and not no max dist)
                // the game also checks the scan visibility here, with an unused bypass flag, which we do above
                int pixelX = (int)MathF.Floor(screenPos.X * 256);
                int pixelY = (int)MathF.Floor(screenPos.Y * 192);
                if (pixelX <= -16 || pixelX >= 272 || pixelY <= -16 || pixelY >= 208)
                {
                    continue;
                }
                ScanTarget target = _scanTargets[_scanTargetCount];
                target.Category = category;
                target.Entity = entity;
                target.Scale = scaleInv;
                target.Position = targetPos;
                target.ScreenX = screenPos.X;
                target.ScreenY = screenPos.Y;
                target.Dim = GameState.StorySave.CheckLogbook(scanId);
                target.Distance = dist;
                if (pixelX > 58 && pixelX < 198 && pixelY > 64 && pixelY < 128)
                {
                    float center = 4 * (distPos.X * distPos.X + distPos.Y * distPos.Y) + dist / 32;
                    if (center < minCenter && !_scanning)
                    {
                        curTargetPos = targetPos;
                        curEnt = entity;
                        minCenter = center;
                    }
                    if (_curScanTarget.Entity == entity)
                    {
                        _curScanTarget.Distance = dist;
                        _curScanTarget.Scale = scaleInv;
                        _curScanTarget.Position = targetPos;
                        _curScanTarget.CenterDist = center;
                        _curScanTarget.Dim = target.Dim;
                        curScreenX = screenPos.X;
                        curScreenY = screenPos.Y;
                        update = true;
                    }
                }
                _scanTargetCount++;
                if (_scanTargetCount == _scanTargets.Count)
                {
                    break;
                }
            }
            EntityBase? prevEnt = _curScanTarget.Entity;
            if (_curScanTarget.Entity != curEnt && (_curScanTarget.Entity == null || minCenter <= _curScanTarget.CenterDist))
            {
                _curScanTarget.Entity = curEnt;
                _curScanTarget.Position = curTargetPos;
                _curScanTarget.CenterDist = minCenter;
                update = true;
            }
            if (_curScanTarget.Entity != prevEnt)
            {
                if (prevEnt == null)
                {
                    _boxSizeFac = 3;
                    _boxCornerX = 0.5f;
                    _boxCornerY = 0.5f;
                }
                _boxCornerFac = 0.125f;
            }
            if (_curScanTarget.Entity != null)
            {
                CollisionResult discard = default;
                if (!update || CollisionDetection.CheckBetweenPoints(CameraInfo.Position, _curScanTarget.Position,
                    TestFlags.Scan, _scene, ref discard))
                {
                    _curScanTarget.Entity = null;
                }
            }
            float scale = _scene.FrameTime / (1 / 30f);
            if (_curScanTarget.Entity != null)
            {
                if (_boxCornerFac < 1)
                {
                    _boxCornerFac += 0.0625f * scale;
                    if (_boxCornerFac > 1)
                    {
                        _boxCornerFac = 1;
                    }
                }
                if (_boxSizeFac > 1)
                {
                    _boxSizeFac -= 0.25f * scale;
                    if (_boxSizeFac < 1)
                    {
                        _boxSizeFac = 1;
                    }
                }
                // todo: FPS stuff
                _boxCornerX += (curScreenX - _boxCornerX) * _boxCornerFac;
                _boxCornerY += (curScreenY - _boxCornerY) * _boxCornerFac;
            }
            else
            {
                if (_boxCornerFac > 0.125f)
                {
                    _boxCornerFac -= 0.0625f * scale;
                    if (_boxCornerFac < 0.125f)
                    {
                        _boxCornerFac = 0.125f;
                    }
                }
                if (_boxSizeFac < 4)
                {
                    _boxSizeFac += 1 * scale;
                    if (_boxSizeFac > 4)
                    {
                        _boxSizeFac = 4;
                    }
                }
            }
        }

        private bool _scanning = false;
        private bool _scanComplete = false;
        private EntityBase? _scanningEntity = null;
        private float _scanningTime = 0;
        private float _scanningTimer = 0;
        private int _scanCategoryIndex = 0;

        private void UpdateScanning(bool scanning)
        {
            if (!scanning)
            {
                UpdateScanSfx(index: 1, enable: false);
                _scanning = false;
            }
            else if (_curScanTarget.Entity != null)
            {
                if (_curScanTarget.Distance < 12)
                {
                    _scanning = true;
                }
                else if (_visorMessageTimer == 0)
                {
                    _soundSource.PlayFreeSfx(SfxId.SCAN_OUT_OF_RANGE);
                    _visorMessageId = 118; // OBJECT OUT OF SCAN RANGE
                    _visorMessageTimer = _visorMessageTime = 60 / 30f;
                }
            }
        }

        private static readonly IReadOnlyList<int> _scanCategoryLayers = new int[6]
        {
            0, 1, 3, 2, 4, 4
        };

        private void UpdateScanState()
        {
            if (GameState.DialogPause)
            {
                return;
            }
            if (_curScanTarget.Entity == null)
            {
                UpdateScanSfx(index: 1, enable: false);
                _scanning = false;
            }
            EntityBase? curEnt = _curScanTarget.Entity;
            if (curEnt != null && curEnt != _scanningEntity)
            {
                ResetScanValues();
                _scanningEntity = curEnt;
                _curScanTarget.Entity = curEnt;
                UpdateScanSfx(index: 1, enable: false);
                _scanning = false;
                int scanId = _scanningEntity.GetScanId();
                _scanningTime = Strings.GetScanEntryTime(scanId);
            }
            if (_scanning && _scanningTimer < _scanningTime)
            {
                Debug.Assert(_curScanTarget.Entity != null);
                if (GameState.StorySave.CheckLogbook(_curScanTarget.Entity.GetScanId()))
                {
                    UpdateScanSfx(index: 1, enable: false);
                    _scanningTimer = _scanningTime;
                    _showDialogConfirm = true;
                }
                else
                {
                    UpdateScanSfx(index: 1, enable: true);
                    _scanningTimer += _scene.FrameTime;
                }
            }
            else if (_scanning && _scanningTimer >= _scanningTime && !_scanComplete && _curScanTarget.Entity != null)
            {
                UpdateScanSfx(index: 1, enable: false);
                _soundSource.PlayFreeSfx(SfxId.SCAN_COMPLETE);
                StopLongSfx();
                _scanComplete = true;
            }
            if (_scanComplete)
            {
                Debug.Assert(_curScanTarget.Entity != null);
                _scanning = false;
                int scanId = _curScanTarget.Entity.GetScanId();
                StringTableEntry? entry = Strings.GetScanEntry(scanId);
                if (entry == null)
                {
                    entry = Strings.EmptyScanEntry;
                }
                _scanCategoryIndex = Strings.GetScanEntryCategory(scanId);
                _overlayMessage1 = entry.Value1;
                _overlayMessage2 = entry.Value2;
                Array.Fill(_overlayBuffer1, '\0');
                Array.Fill(_overlayBuffer2, '\0');
                int lineCount = WrapText(_overlayMessage1, 256, _overlayBuffer1);
                BufferDialogPages();
                ShowDialog(DialogType.Scan, messageId: 0);
            }
        }

        private void AfterScan()
        {
            Debug.Assert(_scanningEntity != null);
            _scanningEntity.OnScanned();
            int scanId = _scanningEntity.GetScanId();
            int altScanId = _scanningEntity.GetScanId(alternate: true);
            GameState.StorySave.UpdateLogbook(scanId);
            if (altScanId != scanId)
            {
                GameState.StorySave.UpdateLogbook(altScanId);
            }
            RestartLongSfx();
            ResetScanValues();
        }

        private float _boxCornerFac = 0.125f;
        private float _boxSizeFac = 4;
        private float _boxCornerX = 0.5f;
        private float _boxCornerY = 0.5f;

        private void DrawScanModels()
        {
            for (int i = 0; i < _scanTargetCount; i++)
            {
                ScanTarget target = _scanTargets[i];
                float iconScale = target.Scale * 90;
                if (target.Entity != _curScanTarget.Entity || iconScale <= 14)
                {
                    float particleScale = 0.625f;
                    float value = Fixed.ToFloat(820);
                    if (target.Scale > value)
                    {
                        float inv = value / target.Scale;
                        particleScale *= inv * inv;
                    }
                    SingleType particle = _scanParticles[2 * target.Category + (target.Dim ? 1 : 0)];
                    float alpha = target.Dim ? 24 / 31f : 1;
                    _scene.AddSingleParticle(particle, target.Position, Vector3.One, alpha, particleScale);
                }
            }
        }

        private void DrawScanObjects()
        {
            for (int i = 0; i < _scanTargetCount; i++)
            {
                ScanTarget target = _scanTargets[i];
                float iconScale = target.Scale * 90;
                if (target.Entity == _curScanTarget.Entity && iconScale > 14)
                {
                    HudObjectInstance iconInst = _scanIconInsts[2 * target.Category + (target.Dim ? 1 : 0)];
                    iconInst.PositionX = target.ScreenX;
                    iconInst.PositionY = target.ScreenY;
                    iconInst.Center = true;
                    iconInst.Alpha = 9 / 16f;
                    iconInst.UseMask = true;
                    _scene.DrawHudObject(iconInst);
                }
            }
            if (_curScanTarget.Entity != null && _boxSizeFac < 4)
            {
                float pixelSize = _curScanTarget.Scale * 90;
                pixelSize = Math.Clamp(pixelSize, 4, 14);
                pixelSize *= _boxSizeFac;
                bool small = pixelSize < 8;
                if (small)
                {
                    _scanCornerInst.SetCharacterData(_scanCornerSmallObj.CharacterData,
                        _scanCornerSmallObj.Width, _scanCornerSmallObj.Height, _scene);
                }
                else
                {
                    _scanCornerInst.SetCharacterData(_scanCornerObj.CharacterData,
                        _scanCornerObj.Width, _scanCornerObj.Height, _scene);
                }
                int index = _curScanTarget.Distance < 12 ? 0 : 1;
                _scanCornerInst.SetIndex(index, _scene);
                _scanLineHorizInst.SetIndex(index, _scene);
                _scanLineVertInst.SetIndex(index, _scene);
                _scanCornerInst.UseMask = true;
                _scanLineHorizInst.UseMask = true;
                _scanLineVertInst.UseMask = true;
                // todo: the box and icon both have notable issues with aspect ratio
                float offsetX = (pixelSize - 16) / 256f;
                float offsetY = (pixelSize - 16) / 192f;
                float leftPos = _boxCornerX - pixelSize / 256f;
                float rightPos = _boxCornerX + offsetX;
                float topPos = _boxCornerY - pixelSize / 192f;
                float bottomPos = _boxCornerY + offsetY;
                _scanCornerInst.PositionX = leftPos;
                _scanCornerInst.PositionY = topPos;
                _scanCornerInst.FlipHorizontal = false;
                _scanCornerInst.FlipVertical = false;
                _scene.DrawHudObject(_scanCornerInst, mode: 1);
                _scanCornerInst.PositionX = rightPos;
                _scanCornerInst.PositionY = topPos;
                _scanCornerInst.FlipHorizontal = true;
                _scanCornerInst.FlipVertical = false;
                _scene.DrawHudObject(_scanCornerInst, mode: 1);
                _scanCornerInst.PositionX = rightPos;
                _scanCornerInst.PositionY = bottomPos;
                _scanCornerInst.FlipHorizontal = true;
                _scanCornerInst.FlipVertical = true;
                _scene.DrawHudObject(_scanCornerInst, mode: 1);
                _scanCornerInst.PositionX = leftPos;
                _scanCornerInst.PositionY = bottomPos;
                _scanCornerInst.FlipHorizontal = false;
                _scanCornerInst.FlipVertical = true;
                _scene.DrawHudObject(_scanCornerInst, mode: 1);
                float curX = 16 / 256f;
                _scanLineHorizInst.PositionY = _boxCornerY;
                for (int i = 0; i < 10; i++)
                {
                    _scanLineHorizInst.PositionX = _boxCornerX + offsetX + curX;
                    _scene.DrawHudObject(_scanLineHorizInst);
                    curX += 16 / 256f;
                }
                curX = 32 / 256f;
                _scanLineHorizInst.PositionY = _boxCornerY;
                for (int i = 0; i < 10; i++)
                {
                    _scanLineHorizInst.PositionX = _boxCornerX - offsetX - curX;
                    _scene.DrawHudObject(_scanLineHorizInst);
                    curX += 16 / 256f;
                }
                float curY = 16 / 192f;
                _scanLineVertInst.PositionX = _boxCornerX;
                for (int i = 0; i < 10; i++)
                {
                    _scanLineVertInst.PositionY = _boxCornerY + offsetY + curY;
                    _scene.DrawHudObject(_scanLineVertInst);
                    curY += 16 / 192f;
                }
                curY = 32 / 192f;
                _scanLineVertInst.PositionY = _boxCornerY;
                for (int i = 0; i < 10; i++)
                {
                    _scanLineVertInst.PositionY = _boxCornerY - offsetY - curY;
                    _scene.DrawHudObject(_scanLineVertInst);
                    curY += 16 / 192f;
                }
            }
        }

        private void DrawScanProgress()
        {
            // the game doesn't apply the X shift, only Y
            float posY = 128 + _objShiftY;
            string text = Strings.GetHudMessage(103); // scanning...
            DrawText2D(128 + _objShiftX, posY - 8, Align.Center, 0, text);
            int length = _scanProgressMeter.Length;
            _scanProgressMeter.TankAmount = (int)(_scanningTime * 120);
            _scanProgressMeter.Horizontal = true;
            _scanProgressMeter.TankCount = 0;
            _scanProgressMeter.Length = 40;
            DrawMeter(108 + _objShiftX, posY, _scanProgressMeter.TankAmount, (int)(_scanningTimer * 120),
                palette: 0, _scanProgressMeter, drawText: false, drawTanks: false);
            _scanProgressMeter.Length = length;
        }

        private void UpdateVisorMessage()
        {
            if (_visorMessageTimer > 0 && _visorMessageId != 0)
            {
                _visorMessageTimer -= _scene.FrameTime;
                if (_visorMessageTimer <= 0)
                {
                    if (_visorMessageScrollOut)
                    {
                        _visorMessageScrollOut = false;
                        _visorMessageTimer = 0;
                    }
                    else
                    {
                        _visorMessageScrollOut = true;
                        _visorMessageTimer = _visorMessageTime;
                    }
                }
            }
        }

        private void DrawVisorMessage()
        {
            if (_visorMessageTimer > 0 && _visorMessageId != 0)
            {
                string text = Strings.GetHudMessage(_visorMessageId);
                float time = _visorMessageTimer;
                if (!_visorMessageScrollOut)
                {
                    time = _visorMessageTime - _visorMessageTimer;
                }
                int characters = (int)(time / (1 / 30f));
                float posX = 128 + _objShiftX;
                float posY = 157 + _objShiftY;
                DrawText2D(posX, posY, Align.PadCenter, 0, text, maxLength: characters);
            }
        }

        private static readonly IReadOnlyList<SingleType> _scanParticles = new SingleType[10]
        {
            SingleType.Lore,
            SingleType.LoreDim,
            SingleType.Enemy,
            SingleType.EnemyDim,
            SingleType.Object,
            SingleType.ObjectDim,
            SingleType.Equipment,
            SingleType.EquipmentDim,
            SingleType.Red,
            SingleType.RedDim
        };

        private readonly HudObjectInstance[] _scanIconInsts = new HudObjectInstance[10];
        private HudObject _scanCornerObj = null!;
        private HudObject _scanCornerSmallObj = null!;
        private HudObjectInstance _scanCornerInst = null!;
        private HudObjectInstance _scanLineHorizInst = null!;
        private HudObjectInstance _scanLineVertInst = null!;

        private class ScanTarget
        {
            public EntityBase? Entity { get; set; }
            public float Distance { get; set; }
            public float CenterDist { get; set; } = Single.MaxValue;
            public int Category { get; set; }
            public Vector3 Position { get; set; }
            public float ScreenX { get; set; }
            public float ScreenY { get; set; }
            public float Scale { get; set; }
            public bool Dim { get; set; }
        }

        private int _scanTargetCount = 0;
        private readonly ScanTarget _curScanTarget = new ScanTarget();
        private readonly IReadOnlyList<ScanTarget> _scanTargets = new ScanTarget[32]
        {
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget(),
            new ScanTarget()
        };
    }
}
