using System;
using System.Collections.Generic;
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
            _curScanTarget.Entity = null;
            _curScanTarget.CenterDist = Single.MaxValue;
        }

        private void UpdateScanHud()
        {
            EntityBase? curEnt = _curScanTarget.Entity;
            Vector3 curTargetPos = Vector3.Zero;
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
                target.Dim = false; // scantodo: use dim icon if scan is in logbook
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
                        _curScanTarget.Dim = false; // scantodo: use dim icon if scan is in logbook
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
                    // scantodo: set values
                }
                // scantodo: set value
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
            // sktodo: draw lines etc.
        }

        private void DrawScanModels()
        {
            for (int i = 0; i < _scanTargetCount; i++)
            {
                ScanTarget target = _scanTargets[i];
                float iconScale = target.Scale * Fixed.ToFloat(90);
                if (target.Entity != _curScanTarget.Entity || iconScale <= Fixed.ToFloat(14))
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
                float iconScale = target.Scale * Fixed.ToFloat(90);
                if (target.Entity == _curScanTarget.Entity && iconScale > Fixed.ToFloat(14))
                {
                    HudObjectInstance iconInst = _scanIconInsts[2 * target.Category + (target.Dim ? 1 : 0)];
                    iconInst.PositionX = target.ScreenX;
                    iconInst.PositionY = target.ScreenY;
                    iconInst.Alpha = 9 / 16f;
                    _scene.DrawHudObject(iconInst);
                }
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
