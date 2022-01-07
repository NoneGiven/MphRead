using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class TeleporterEntity : EntityBase
    {
        private readonly TeleporterEntityData _data;
        private readonly Vector3 _targetPos = Vector3.Zero;
        private readonly Matrix4 _artifact1Transform;
        private readonly Matrix4 _artifact2Transform;
        private readonly Matrix4 _artifact3Transform;

        private bool _big = false;
        public new bool Active { get; set; }
        private bool _bool3 = false; // todo: names
        private bool _bool4 = false;
        private readonly bool[] _triggeredSlots = new bool[4] { true, true, true, true };
        private int _targetRoomId = -1; // todo: this

        // used for invisible teleporters
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0xFF, 0xFF).AsVector4();
        // used for multiplayer teleporter destination
        private readonly Vector4 _overrideColor2 = new ColorRgb(0xAA, 0xAA, 0xAA).AsVector4();

        public TeleporterEntity(TeleporterEntityData data, Scene scene) : base(EntityType.Teleporter, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            bool multiplayer = scene.Multiplayer;
            // todo: scan ID
            if (data.Invisible != 0)
            {
                AddPlaceholderModel();
            }
            else
            {
                Recolor = multiplayer ? 0 : scene.AreaId;
                string modelName;
                if (data.ArtifactId >= 8)
                {
                    modelName = multiplayer ? "TeleporterMP" : "TeleporterSmall";
                }
                else
                {
                    modelName = "Teleporter";
                    _big = true;
                }
                ModelInstance inst = SetUpModel(modelName);
                inst.SetAnimation(2, AnimFlags.NoLoop | AnimFlags.Reverse);

            }
            // todo: use room state/artifact bits/etc. to determine active state
            Active = data.Active != 0;
            // 0-7 = big teleporter using the corresponding artifact model
            // 8, 10, 11, 255 = small teleporter (no apparent meaning to each value beyond that)
            if (data.ArtifactId < 8)
            {
                // todo: set artifacts active based on state
                string name = $"Artifact0{data.ArtifactId + 1}";
                ModelInstance inst = SetUpModel(name);
                _models.Add(inst);
                _models.Add(inst);
                float angleY = MathHelper.DegreesToRadians(337 * (360 / 4096f));
                float angleZ = MathHelper.DegreesToRadians(360 * (360 / 4096f));
                Matrix4 transform = Matrix4.CreateRotationY(angleY) * Matrix4.CreateRotationZ(angleZ);
                transform.Row3.Xyz = new Vector3(Fixed.ToFloat(7208), Fixed.ToFloat(2375), 0);
                _artifact1Transform = transform;
                angleY = MathHelper.DegreesToRadians(1365 * (360 / 4096f));
                _artifact2Transform = _artifact1Transform * Matrix4.CreateRotationY(angleY);
                angleY = MathHelper.DegreesToRadians(2730 * (360 / 4096f));
                _artifact3Transform = _artifact1Transform * Matrix4.CreateRotationY(angleY);
            }
            if (multiplayer)
            {
                AddPlaceholderModel();
                _targetPos = _data.TargetPosition.ToFloatVector();
            }
        }

        public override bool Process()
        {
            if (_data.Invisible == 0)
            {
                base.Process();
                AnimationInfo animInfo = _models[0].AnimInfo;
                if (animInfo.Index[0] == 2
                    && !animInfo.Flags[0].TestFlag(AnimFlags.Reverse) && animInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    _models[0].SetAnimation(0);
                }
                else if (_big && !Active)
                {
                    // todo: activate based on story state
                    Activate();
                }
                if (_bool4 && (animInfo.Index[0] != 0 || animInfo.Frame[0] == animInfo.FrameCount[0] - 1))
                {
                    InitiateAnimaton();
                }
            }
            if (!Active)
            {
                return true;
            }
            // todo: positional audio, node ref
            bool activated = false;
            Vector3 testPos = Position.AddY(1);
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.Health == 0 || player.IsBot && !_scene.Multiplayer)
                {
                    continue;
                }
                Vector3 between = player.Volume.SpherePosition - Position;
                if (between.Y < 1.5f && between.Y > -1.5f && between.X * between.X + between.Z * between.Z < 49)
                {
                    activated = true;
                    ActivateAnimaton();
                    CollisionResult discard = default;
                    if (CollisionDetection.CheckCylinderOverlapSphere(player.PrevPosition, player.Volume.SpherePosition,
                        testPos, 1.75f, ref discard))
                    {
                        if (!_triggeredSlots[player.SlotIndex])
                        {
                            float radius = _big ? 1.5f : 1;
                            if (CollisionDetection.CheckCylinderOverlapSphere(player.PrevPosition, player.Volume.SpherePosition,
                                testPos, radius, ref discard))
                            {
                                if (_targetRoomId == -1)
                                {
                                    // todo: play SFX
                                    player.Teleport(_targetPos.AddY(0.5f), FacingVector);
                                    // todo: node ref
                                    // todo: update camera
                                }
                                else
                                {
                                    // todo: teleport to new room
                                }
                                player.Speed = new Vector3(0, player.Speed.Y, 0);
                                // todo: update bot AI flag
                                _triggeredSlots[player.SlotIndex] = true;
                            }
                        }
                    }
                    else
                    {
                        _triggeredSlots[player.SlotIndex] = false;
                    }
                }
                else
                {
                    _triggeredSlots[player.SlotIndex] = false;
                }
            }
            if (!activated && _bool3)
            {
                _bool4 = true;
            }
            return true;
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (_big)
            {
                return;
            }
            if (info.Message == Message.Activate)
            {
                Activate();
            }
            else if (info.Message == Message.SetActive)
            {
                if ((int)info.Param1 != 0)
                {
                    Activate();
                }
                else
                {
                    Active = false;
                    // todo: scan ID, room state
                    _bool4 = true;
                }
            }
        }

        private void Activate()
        {
            if (!Active)
            {
                Active = true;
                // todo: update scan ID
                ActivateAnimaton();
            }
        }

        // todo: names
        private void ActivateAnimaton()
        {
            if (!_bool3 && _data.Invisible == 0)
            {
                _bool3 = true;
                _bool4 = false;
                // todo: positional audio
                AnimationInfo animInfo = _models[0].AnimInfo;
                if (animInfo.Index[0] == 2)
                {
                    // todo: play SFX
                    animInfo.Flags[0] |= AnimFlags.NoLoop;
                    animInfo.Flags[0] &= ~AnimFlags.Ended;
                    animInfo.Flags[0] &= ~AnimFlags.Reverse;
                }
            }
        }

        private void InitiateAnimaton()
        {
            if (_bool3 && _data.Invisible == 0)
            {
                _bool3 = false;
                _bool4 = false;
                AnimationInfo animInfo = _models[0].AnimInfo;
                if (animInfo.Index[0] == 2)
                {
                    // todo: play SFX
                    animInfo.Flags[0] |= AnimFlags.NoLoop;
                    animInfo.Flags[0] |= AnimFlags.Reverse;
                    animInfo.Flags[0] &= ~AnimFlags.Ended;
                }
                else if (animInfo.Index[0] == 0)
                {
                    _models[0].SetAnimation(2, AnimFlags.NoLoop | AnimFlags.Reverse);
                }
            }
            else
            {
                _bool4 = false;
            }
        }

        public override void Destroy()
        {
            // todo: stop SFX
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index != 0 && inst.IsPlaceholder)
            {
                transform.Row3.Xyz = _targetPos;
            }
            else if (index == 1)
            {
                return _artifact1Transform * _transform;
            }
            else if (index == 2)
            {
                return _artifact2Transform * _transform;
            }
            else if (index == 3)
            {
                return _artifact3Transform * _transform;
            }
            return transform;
        }

        protected override Vector4? GetOverrideColor(ModelInstance inst, int index)
        {
            if (index != 0 && inst.IsPlaceholder)
            {
                return _overrideColor2;
            }
            return base.GetOverrideColor(inst, index);
        }

        protected override int GetModelRecolor(ModelInstance inst, int index)
        {
            if (index != 0)
            {
                return 0;
            }
            return base.GetModelRecolor(inst, index);
        }

        public override void GetDisplayVolumes()
        {
            if (_scene.ShowVolumes == VolumeDisplay.Teleporter)
            {
                CollisionVolume volume;
                if (_data.Invisible != 0 || _data.ArtifactId < 8)
                {
                    volume = new CollisionVolume(Position.AddY(1.0f), 1.0f);
                }
                else
                {
                    volume = new CollisionVolume(Position.AddY(1.5f), 1.0f);
                }
                AddVolumeItem(volume, Vector3.UnitX);
            }
        }
    }
}
