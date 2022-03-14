using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MphRead.Entities;
using MphRead.Formats.Culling;
using OpenTK.Mathematics;

namespace MphRead.Formats
{
    [Flags]
    public enum CamSeqFlags
    {
        None = 0,
        Complete = 1,
        CanEnd = 2,
        BlockInput = 4,
        ForceAlt = 8,
        ForceBiped = 0x10,
        Loop = 0x20
    }

    public class CameraSequence
    {
        public int SequenceId { get; }
        public string Name { get; }
        public byte Version { get; }
        public IReadOnlyList<CameraSequenceKeyframe> Keyframes { get; }

        public CamSeqFlags Flags { get; set; }
        public bool BlockInput => Flags.TestFlag(CamSeqFlags.BlockInput);
        public bool ForceAlt => Flags.TestFlag(CamSeqFlags.ForceAlt);
        public bool ForceBiped => Flags.TestFlag(CamSeqFlags.ForceBiped);

        public ushort TransitionTimer { get; set; }
        public ushort TransitionTime { get; private set; }
        private int _keyframeIndex = 0;
        private float _keyframeElapsed = 0;

        public CameraInfo InitialCamInfo { get; } = new CameraInfo();
        public CameraInfo? CamInfoRef { get; set; }

        private readonly Scene _scene;

        public static CameraSequence? Current { get; set; }
        public static CameraSequence? Intro { get; set; }
        public bool IsIntro => SequenceId >= 172 && SequenceId <= 198;
        private static readonly HashSet<int> _cockpitLoops = new HashSet<int>() { 102, 103, 104, 105, 106, 168 };

        private CameraSequence(int id, string name, Scene scene,
            CameraSequenceHeader header, IReadOnlyList<RawCameraSequenceKeyframe> keyframes)
        {
            SequenceId = id;
            Name = name.Replace(".bin", "");
            Version = header.Version;
            Keyframes = keyframes.Select(k => new CameraSequenceKeyframe(k)).ToList();
            if (id > 171 || _cockpitLoops.Contains(id))
            {
                // the game does this for MP intros when switching to them (scene_setup, then match states 1/2 in process_frame)
                Flags |= CamSeqFlags.Loop;
            }
            _scene = scene;
        }

        public void Initialize()
        {
            foreach (CameraSequenceKeyframe keyframe in Keyframes)
            {
                keyframe.PositionEntity = GetKeyframeRef(keyframe.PosEntityType, keyframe.PosEntityId);
                keyframe.TargetEntity = GetKeyframeRef(keyframe.TargetEntityType, keyframe.TargetEntityId);
                keyframe.MessageTarget = GetKeyframeRef(keyframe.MessageTargetType, keyframe.MessageTargetId);
                keyframe.NodeRef = _scene.GetNodeRefByName(keyframe.NodeName);
                if (keyframe.NodeRef == NodeRef.None)
                {
                    keyframe.NodeRef = _scene.GetNodeRefByName("rmMain");
                }
                Debug.Assert(_scene.Room == null || keyframe.NodeRef != NodeRef.None);
            }
        }

        public void Process()
        {
            if (Flags.TestFlag(CamSeqFlags.Complete) || Keyframes.Count == 0)
            {
                return;
            }
            Debug.Assert(CamInfoRef != null);
            CamInfoRef.Shake = 0;
            CamInfoRef.PrevPosition = CamInfoRef.Position;
            CameraSequenceKeyframe curFrame = Keyframes[_keyframeIndex];
            float frameLength = curFrame.HoldTime + curFrame.MoveTime;
            float fadeOutStart = frameLength - curFrame.FadeOutTime;
            CalculateFrameValues();
            if (_keyframeElapsed < 1 / 60f) // todo: FPS stuff
            {
                CamInfoRef.PrevPosition = CamInfoRef.Position;
                if (curFrame.PositionEntity != null)
                {
                    NodeRef nodeRef = curFrame.PositionEntity.NodeRef;
                    if (nodeRef != NodeRef.None)
                    {
                        curFrame.PositionEntity.GetPosition(out Vector3 prevPos);
                        CamInfoRef.NodeRef = _scene.UpdateNodeRef(CamInfoRef.NodeRef, prevPos, CamInfoRef.Position);
                    }
                    else
                    {
                        CamInfoRef.NodeRef = curFrame.NodeRef;
                    }
                }
                else
                {
                    CamInfoRef.NodeRef = curFrame.NodeRef;
                }
                var message = (Message)curFrame.MessageId;
                if (message != Message.None)
                {
                    // game sets the keyframe as the sender
                    _scene.SendMessage(message, null!, curFrame.MessageTarget, (int)curFrame.MessageParam, 0);
                }
            }
            FadeType fadeType = FadeType.None;
            float fadeTime = 0;
            if (curFrame.FadeInType != FadeType.None && _keyframeElapsed <= 2 / 30f)
            {
                fadeType = curFrame.FadeInType;
                fadeTime = curFrame.FadeInTime;
            }
            else if (curFrame.FadeOutType != FadeType.None
                && _keyframeElapsed >= fadeOutStart && _keyframeElapsed <= fadeOutStart + 2 / 30f)
            {
                fadeType = curFrame.FadeOutType;
                fadeTime = curFrame.FadeOutTime;
            }
            if (fadeType != FadeType.None)
            {
                _scene.SetFade(fadeType, fadeTime, overwrite: false);
            }
            _keyframeElapsed += _scene.FrameTime;
            if (_keyframeElapsed >= frameLength)
            {
                _keyframeElapsed -= frameLength;
                if (_keyframeElapsed >= 1 / 60f)
                {
                    _keyframeElapsed = 1 / 60f - 1 / 4096f;
                }
                _keyframeIndex++;
                if (_keyframeIndex >= Keyframes.Count)
                {
                    if (Flags.TestFlag(CamSeqFlags.Loop))
                    {
                        Restart();
                    }
                    else
                    {
                        Flags |= CamSeqFlags.CanEnd;
                        Flags |= CamSeqFlags.Complete;
                        _keyframeElapsed = frameLength;
                    }
                }
            }
            if (TransitionTimer < TransitionTime)
            {
                TransitionTimer++;
            }
            CamInfoRef.Update();
            // todo?: the game only does this when ptr_tbl_idx is 14
            CamInfoRef.NodeRef = _scene.UpdateNodeRef(CamInfoRef.NodeRef, CamInfoRef.PrevPosition, CamInfoRef.Position);
            PlayerEntity player = PlayerEntity.Main;
            if (Flags.TestFlag(CamSeqFlags.ForceAlt) && player.IsAltForm
                || Flags.TestFlag(CamSeqFlags.ForceBiped) && !player.IsAltForm)
            {
                player.BlockFormSwitch();
            }
        }

        public void SetUp(CameraInfo camInfo, ushort transitionTime)
        {
            CamInfoRef = camInfo;
            InitialCamInfo.Position = camInfo.Position;
            InitialCamInfo.PrevPosition = camInfo.PrevPosition;
            InitialCamInfo.Target = camInfo.Target;
            InitialCamInfo.UpVector = camInfo.UpVector;
            InitialCamInfo.TrueUp = camInfo.TrueUp;
            InitialCamInfo.Facing = camInfo.Facing;
            InitialCamInfo.Fov = camInfo.Fov;
            InitialCamInfo.Shake = camInfo.Shake;
            InitialCamInfo.ViewMatrix = camInfo.ViewMatrix;
            InitialCamInfo.Field48 = camInfo.Field48;
            InitialCamInfo.Field4C = camInfo.Field4C;
            InitialCamInfo.Field50 = camInfo.Field50;
            InitialCamInfo.Field54 = camInfo.Field54;
            InitialCamInfo.NodeRef = camInfo.NodeRef;
            Flags &= ~CamSeqFlags.Complete;
            Flags &= ~CamSeqFlags.CanEnd;
            Flags &= ~CamSeqFlags.Loop;
            _keyframeElapsed = 0;
            TransitionTimer = 0;
            TransitionTime = transitionTime;
            _keyframeIndex = 0;
            if (Keyframes.Count > 0)
            {
                CameraSequenceKeyframe firstFrame = Keyframes[0];
                CamInfoRef.NodeRef = firstFrame.NodeRef;
                CalculateFrameValues();
                if (firstFrame.PositionEntity != null)
                {
                    NodeRef nodeRef = firstFrame.PositionEntity.NodeRef;
                    if (nodeRef != NodeRef.None)
                    {
                        firstFrame.PositionEntity.GetPosition(out Vector3 prevPos);
                        CamInfoRef.NodeRef = _scene.UpdateNodeRef(nodeRef, prevPos, CamInfoRef.Position);
                    }
                }
            }
            Current = this;
            // todo?: the game only does the rest when ptr_tbl_idx is 14
            PlayerEntity.Main.CloseDialogs();
            PlayerEntity.Main.HudEndDisrupted();
            PlayerEntity.Main.ResetCombatVisor();
        }

        public void Restart(ushort transitionTimer = 0, ushort transitionTime = 0)
        {
            Flags &= ~CamSeqFlags.Complete;
            Flags &= ~CamSeqFlags.CanEnd;
            _keyframeElapsed = 0;
            TransitionTimer = transitionTimer;
            TransitionTime = transitionTime;
            _keyframeIndex = 0;
            Debug.Assert(Keyframes.Count > 0);
            Debug.Assert(CamInfoRef != null);
            CameraSequenceKeyframe firstFrame = Keyframes[0];
            CamInfoRef.NodeRef = firstFrame.NodeRef;
            CalculateFrameValues();
            // this has the potential for "tearing" of node refs e.g. in Cortex CPU, and the update is never needed
            if (!Bugfixes.BetterCamSeqNodeRef && firstFrame.PositionEntity != null)
            {
                NodeRef nodeRef = firstFrame.PositionEntity.NodeRef;
                if (nodeRef != NodeRef.None)
                {
                    firstFrame.PositionEntity.GetPosition(out Vector3 prevPos);
                    CamInfoRef.NodeRef = _scene.UpdateNodeRef(nodeRef, prevPos, CamInfoRef.Position);
                }
            }
        }

        public void End()
        {
            Flags |= CamSeqFlags.CanEnd;
            Flags |= CamSeqFlags.Complete;
            _keyframeElapsed = 0;
            TransitionTimer = 0;
            TransitionTime = 0;
            _keyframeIndex = 0;
            if (Current == this)
            {
                if (CamInfoRef != null)
                {
                    CamInfoRef.Position = InitialCamInfo.Position;
                    CamInfoRef.PrevPosition = InitialCamInfo.PrevPosition;
                    CamInfoRef.Target = InitialCamInfo.Target;
                    CamInfoRef.UpVector = InitialCamInfo.UpVector;
                    CamInfoRef.TrueUp = InitialCamInfo.TrueUp;
                    CamInfoRef.Facing = InitialCamInfo.Facing;
                    CamInfoRef.Fov = InitialCamInfo.Fov;
                    CamInfoRef.Shake = InitialCamInfo.Shake;
                    CamInfoRef.ViewMatrix = InitialCamInfo.ViewMatrix;
                    CamInfoRef.Field48 = InitialCamInfo.Field48;
                    CamInfoRef.Field4C = InitialCamInfo.Field4C;
                    CamInfoRef.Field50 = InitialCamInfo.Field50;
                    CamInfoRef.Field54 = InitialCamInfo.Field54;
                    CamInfoRef.NodeRef = InitialCamInfo.NodeRef;
                    CamInfoRef = null;
                }
                Current = null;
            }
        }

        private void CalculateFrameValues()
        {
            Vector3 finalPosition;
            Vector3 finalToTarget;
            float finalRoll;
            float finalFov;
            CameraSequenceKeyframe curFrame = Keyframes[_keyframeIndex];
            float movePercent = 0;
            float moveElapsed = _keyframeElapsed - curFrame.HoldTime;
            if (moveElapsed >= 0 && curFrame.MoveTime > 0)
            {
                movePercent = moveElapsed / curFrame.MoveTime;
            }
            Vector4 moveVec = GetVec4(movePercent);
            // current/prev/after/next
            var factorVec = new Vector4(
                0,
                (curFrame.PrevFrameInfluence & 1) == 0 ? 1 / 3f : 0,
                (curFrame.AfterFrameInfluence & 1) == 0 ? 2 / 3f : 1,
                1
            );
            float factorDot = Vector4.Dot(factorVec, moveVec);
            CameraSequenceKeyframe? nextFrame = null;
            if (_keyframeIndex + 1 < Keyframes.Count)
            {
                nextFrame = Keyframes[_keyframeIndex + 1];
            }
            if (nextFrame != null)
            {
                if (((curFrame.PrevFrameInfluence | curFrame.AfterFrameInfluence) & 2) != 0 && curFrame.MoveTime > 0)
                {
                    Vector3 curPos = curFrame.Position;
                    Vector3 curTarget = curFrame.ToTarget;
                    Vector3 nextPos = nextFrame.Position;
                    Vector3 nextTarget = nextFrame.ToTarget;
                    AddEntityPosition(curFrame, ref curPos, ref curTarget);
                    AddEntityPosition(nextFrame, ref nextPos, ref nextTarget);
                    Vector3 curToNextPos = (nextPos - curPos) / curFrame.MoveTime;
                    Vector3 curToNextTarget = (nextTarget - curTarget) / curFrame.MoveTime;
                    Vector3 prevPos;
                    Vector3 prevTarget;
                    CameraSequenceKeyframe? prevFrame = null;
                    if (_keyframeIndex - 1 >= 0)
                    {
                        prevFrame = Keyframes[_keyframeIndex - 1];
                    }
                    if (_keyframeIndex > 0 && prevFrame != null && (curFrame.PrevFrameInfluence & 2) != 0 && prevFrame.MoveTime > 0)
                    {
                        float easing = 1 / 6f * curFrame.MoveTime * curFrame.Easing;
                        prevPos = prevFrame.Position;
                        prevTarget = prevFrame.ToTarget;
                        AddEntityPosition(prevFrame, ref prevPos, ref prevTarget);
                        Vector3 prevToCurPos = (curPos - prevPos) / prevFrame.MoveTime;
                        Vector3 prevToNextPos = curToNextPos + prevToCurPos;
                        prevPos = prevToNextPos * easing + curPos;
                        Vector3 prevToCurTarget = (curTarget - prevTarget) / prevFrame.MoveTime;
                        Vector3 prevToNextTarget = curToNextTarget + prevToCurTarget;
                        prevTarget = prevToNextTarget * easing + curTarget;
                    }
                    else
                    {
                        float easing = 1 / 3f * curFrame.MoveTime * curFrame.Easing;
                        prevPos = curToNextPos * easing + curPos;
                        prevTarget = curToNextTarget * easing + curTarget;
                    }
                    Vector3 afterPos;
                    Vector3 afterTarget;
                    CameraSequenceKeyframe? afterFrame = null;
                    if (_keyframeIndex + 2 < Keyframes.Count)
                    {
                        afterFrame = Keyframes[_keyframeIndex + 2];
                    }
                    if (afterFrame != null && (curFrame.AfterFrameInfluence & 2) != 0 && nextFrame.MoveTime > 0)
                    {
                        float easing = -1 / 6f * curFrame.MoveTime * nextFrame.Easing;
                        afterPos = afterFrame.Position;
                        afterTarget = afterFrame.ToTarget;
                        AddEntityPosition(afterFrame, ref afterPos, ref afterTarget);
                        Vector3 nextToAfterPos = (afterPos - nextPos) / nextFrame.MoveTime;
                        Vector3 currentToAfterPos = curToNextPos + nextToAfterPos;
                        afterPos = currentToAfterPos * easing + nextPos;
                        Vector3 nextToAfterTarget = (afterTarget - nextTarget) / nextFrame.MoveTime;
                        Vector3 currentToAfterTarget = curToNextTarget + nextToAfterTarget;
                        afterTarget = currentToAfterTarget * easing + nextTarget;
                    }
                    else
                    {
                        float easing = -1 / 3f * curFrame.MoveTime * curFrame.Easing;
                        afterPos = curToNextPos * easing + nextPos;
                        afterTarget = curToNextTarget * easing + nextTarget;
                    }
                    Vector4 dotVec = GetVec4(factorDot);
                    var posMtx = new Matrix4x3(
                        curPos,
                        prevPos,
                        afterPos,
                        nextPos
                    );
                    var targetMtx = new Matrix4x3(
                        curTarget,
                        prevTarget,
                        afterTarget,
                        nextTarget
                    );
                    finalPosition = Matrix.Vec4MultMtx4x3(dotVec, posMtx);
                    finalToTarget = Matrix.Vec4MultMtx4x3(dotVec, targetMtx);
                    finalRoll = curFrame.Roll * (1 - factorDot) + nextFrame.Roll * factorDot;
                    finalFov = curFrame.Fov * (1 - factorDot) + nextFrame.Fov * factorDot;
                }
                else
                {
                    Vector3 curPos = curFrame.Position;
                    Vector3 curTarget = curFrame.ToTarget;
                    Vector3 nextPos = nextFrame.Position;
                    Vector3 nextTarget = nextFrame.ToTarget;
                    AddEntityPosition(curFrame, ref curPos, ref curTarget);
                    AddEntityPosition(nextFrame, ref nextPos, ref nextTarget);
                    finalPosition = new Vector3(
                        (curPos.X * (1 - factorDot)) + (nextPos.X * factorDot),
                        (curPos.Y * (1 - factorDot)) + (nextPos.Y * factorDot),
                        (curPos.Z * (1 - factorDot)) + (nextPos.Z * factorDot)
                    );
                    finalToTarget = new Vector3(
                        (curTarget.X * (1 - factorDot)) + (nextTarget.X * factorDot),
                        (curTarget.Y * (1 - factorDot)) + (nextTarget.Y * factorDot),
                        (curTarget.Z * (1 - factorDot)) + (nextTarget.Z * factorDot)
                    );
                    finalRoll = (curFrame.Roll * (1 - factorDot)) + (nextFrame.Roll * factorDot);
                    finalFov = (curFrame.Fov * (1 - factorDot)) + (nextFrame.Fov * factorDot);
                }
            }
            else
            {
                Vector3 curPos = curFrame.Position;
                Vector3 curTarget = curFrame.ToTarget;
                AddEntityPosition(curFrame, ref curPos, ref curTarget);
                finalPosition = curPos;
                finalToTarget = curTarget;
                finalRoll = curFrame.Roll;
                finalFov = curFrame.Fov;
            }
            Debug.Assert(CamInfoRef != null);
            finalFov *= 2;
            if (TransitionTimer >= TransitionTime)
            {
                CamInfoRef.Fov = finalFov;
                CamInfoRef.Position = finalPosition;
                CamInfoRef.Target = CamInfoRef.Position + finalToTarget;
            }
            else
            {
                Debug.Assert(TransitionTime != 0);
                float pct = TransitionTimer / (float)TransitionTime;
                CamInfoRef.Fov += (finalFov - CamInfoRef.Fov) * pct;
                CamInfoRef.Position += (finalPosition - CamInfoRef.Position) * pct;
                finalToTarget = CamInfoRef.Facing + (finalToTarget - CamInfoRef.Facing) * pct;
                CamInfoRef.Target = CamInfoRef.Position + finalToTarget;
            }
            Vector3 upVector = Vector3.UnitY;
            if (MathF.Abs(finalRoll) >= 1 / 4096f)
            {
                finalRoll = MathHelper.DegreesToRadians(finalRoll);
                CamInfoRef.Facing = CamInfoRef.Target - CamInfoRef.Position;
                Vector3 cross = Vector3.Cross(upVector, CamInfoRef.Facing).Normalized();
                upVector = Vector3.Cross(CamInfoRef.Facing, cross).Normalized();
                float cos = MathF.Cos(finalRoll);
                float sin = MathF.Sin(finalRoll);
                upVector = new Vector3(cross.X * cos, upVector.Y * sin, cross.Z * cos);
            }
            if (TransitionTimer >= TransitionTime)
            {
                CamInfoRef.UpVector = upVector;
            }
            else
            {
                Debug.Assert(TransitionTime != 0);
                float pct = TransitionTimer / (float)TransitionTime;
                CamInfoRef.UpVector += (upVector - CamInfoRef.UpVector) * pct;
                CamInfoRef.UpVector = CamInfoRef.UpVector.Normalized();
            }
        }

        private void AddEntityPosition(CameraSequenceKeyframe keyframe, ref Vector3 position, ref Vector3 toTarget)
        {
            if (keyframe.PositionEntity != null)
            {
                keyframe.PositionEntity.GetVectors(out Vector3 entPos, out Vector3 entUp, out Vector3 entFacing);
                if (keyframe.UseEntityTransform)
                {
                    Vector3 entRight = Vector3.Cross(entUp, entFacing).Normalized();
                    entUp = Vector3.Cross(entFacing, entRight).Normalized();
                    position = entPos + new Vector3(
                        entRight.X * position.X + entUp.X * position.Y + entFacing.X * position.Z,
                        entRight.Y * position.X + entUp.Y * position.Y + entFacing.Y * position.Z,
                        entRight.Z * position.X + entUp.Z * position.Y + entFacing.Z * position.Z
                    );
                }
                else
                {
                    position += entPos;
                }
            }
            if (keyframe.TargetEntity != null)
            {
                keyframe.TargetEntity.GetPosition(out Vector3 entPos);
                Vector3 between = (entPos - position).Normalized();
                Vector3 cross1 = Vector3.Cross(Vector3.UnitY, between).Normalized();
                var cross2 = Vector3.Cross(between, cross1);
                toTarget = new Vector3(
                    toTarget.Z * between.X + toTarget.X * cross1.X + toTarget.Y * cross2.X,
                    toTarget.Z * between.Y + toTarget.X * cross1.Y + toTarget.Y * cross2.Y,
                    toTarget.Z * between.Z + toTarget.X * cross1.Z + toTarget.Y * cross2.Z
                );
            }
        }

        private Vector4 GetVec4(float percent)
        {
            float pctSqr = percent * percent;
            float inverse = 1 - percent;
            float invSqr = inverse * inverse;
            return new Vector4(
                invSqr * inverse,
                3 * percent * invSqr,
                3 * pctSqr * inverse,
                pctSqr * percent
            );
        }

        public static CameraSequence Load(int id, Scene scene)
        {
            // indices only go up to 171 in game, but we've added the 27 multiplayer intros
            Debug.Assert(id >= 0 && id < 199);
            return Load(Filenames[id], scene, id);
        }

        public static CameraSequence Load(string name, Scene scene, int id = -1)
        {
            string path = Path.Combine(Paths.FileSystem, "cameraEditor", name);
            var bytes = new ReadOnlySpan<byte>(File.ReadAllBytes(path));
            CameraSequenceHeader header = Read.ReadStruct<CameraSequenceHeader>(bytes);
            Debug.Assert(header.Padding3 == 0);
            Debug.Assert(header.Padding4 == 0);
            IReadOnlyList<RawCameraSequenceKeyframe> keyframes
                = Read.DoOffsets<RawCameraSequenceKeyframe>(bytes, Sizes.CameraSequenceHeader, header.Count);
            return new CameraSequence(id, name, scene, header, keyframes);
        }

        private EntityBase? GetKeyframeRef(short type, short id)
        {
            if (type == (short)EntityType.Player)
            {
                Debug.Assert(id < PlayerEntity.MaxPlayers);
                return PlayerEntity.Players[id];
            }
            if (type != -1 && id != -1 && _scene.TryGetEntity(id, out EntityBase? entity))
            {
                return entity;
            }
            return null;
        }

        public static IReadOnlyList<int> SfxData { get; } = new int[199]
        {
            92 | 0x8000, 17 | 0x8000, 94 | 0x8000, 93 | 0x8000, 0, 0, 19, 0, 0, 19, 0, 0,
            19, 0, 19, 0, 19, 19, 0, 0, 0, 18 | 0x8000, 20 | 0x8000, 0, 0, 19, 0, 0, 0, 19,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 70, 0, 19 | 0x2000, 0, 69, 66 | 0x4000, 0, 0,
            21, 0, 95 | 0x8000, 0, 0, 98, 80, 90, 0, 0, 0, 0, 0, 0, 0, 0, 19, 0, 19, 19, 0,
            19, 72, 71, 0, 79, 73, 0, 19, 0, 0, 0, 0, 0, 19, 0, 22, 0, 0, 0, 19, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 19, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 65 | 0x4000, 84, 0, 0, 0, 97, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19, 0, 0,
            0, 99 | 0x8000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        public static IReadOnlyList<string> Filenames { get; } = new List<string>()
        {
            /*   0 */ "unit1_land_intro.bin",
            /*   1 */ "unit2_land_intro.bin",
            /*   2 */ "unit3_land_intro.bin",
            /*   3 */ "unit4_land_intro.bin",
            /*   4 */ "unit4_c1_platform_intro.bin",
            /*   5 */ "unit2_co_scan_intro.bin",
            /*   6 */ "unit2_co_scan_outro.bin",
            /*   7 */ "unit2_co_bit_intro.bin",
            /*   8 */ "unit2_c4_teleporter_intro.bin",  // no file
            /*   9 */ "unit2_co_bit_outro.bin",
            /*  10 */ "unit2_co_helm_flyby.bin",
            /*  11 */ "unit2_rm1_artifact_intro.bin",
            /*  12 */ "unit2_rm1_artifact_outro.bin",
            /*  13 */ "unit2_c4_artifact_intro.bin",
            /*  14 */ "unit2_c4_artifact_outro.bin",
            /*  15 */ "unit2_rm2_kanden_intro.bin",
            /*  16 */ "unit2_rm3_artifact_intro.bin",
            /*  17 */ "unit2_rm3_artifact_outro.bin",
            /*  18 */ "unit2_rm3_kanden_intro.bin",
            /*  19 */ "unit4_co_morphballmaze.bin",
            /*  20 */ "unit2_b1_octolith_intro.bin",
            /*  21 */ "unit2_co_guardian_intro.bin",
            /*  22 */ "unit2_rm3_kanden_outro.bin",
            /*  23 */ "unit4_rm1_morphballjumps1.bin",
            /*  24 */ "unit4_land_guardian_intro.bin",
            /*  25 */ "unit4_rm3_scandoor_unlock.bin",
            /*  26 */ "unit1_c4_dropmaze_left.bin",
            /*  27 */ "unit4_co_morphballmaze_enter.bin",
            /*  28 */ "unit4_rm5_arcticspawn_intro.bin",
            /*  29 */ "unit4_rm5_arcticspawn_outro.bin",
            /*  30 */ "unit1_c4_dropmaze_right.bin",
            /*  31 */ "unit4_rm3_hunters_intro.bin",
            /*  32 */ "unit4_rm3_hunters_outro.bin",
            /*  33 */ "unit4_rm2_switch_intro.bin",
            /*  34 */ "unit4_rm2_guardian_intro.bin",
            /*  35 */ "unit1_c5_pistonmaze_1.bin",
            /*  36 */ "unit1_c5_pistonmaze_2.bin",
            /*  37 */ "unit1_c5_pistonmaze_3.bin",
            /*  38 */ "unit1_c5_pistonmaze_4.bin",
            /*  39 */ "unit4_rm2_guardian_outro.bin",
            /*  40 */ "unit3_c2_morphballmaze.bin",
            /*  41 */ "unit4_rm5_powerdown.bin",
            /*  42 */ "unit4_rm1_morphballjumps2.bin",
            /*  43 */ "unit4_rm2_elevator_intro.bin",
            /*  44 */ "unit4_rm1_morphballjumps3.bin",
            /*  45 */ "unit4_rm5_pillarcrash.bin",
            /*  46 */ "unit4_rm1_wasp_intro.bin",
            /*  47 */ "unit1_RM1_spire_intro_layer0.bin",
            /*  48 */ "unit1_RM1_spire_intro_layer3.bin",
            /*  49 */ "unit1_RM1_spire_outro.bin",
            /*  50 */ "unit1_RM6_spire_intro_layer3.bin",
            /*  51 */ "unit1_c1_shipflyby.bin",
            /*  52 */ "unit4_rm3_trace_intro.bin",
            /*  53 */ "unit3_rm1_forcefield_unlock.bin",
            /*  54 */ "unit3_rm1_ship_battle_end.bin",
            /*  55 */ "unit3_rm2_evac_intro.bin",
            /*  56 */ "unit3_rm2_evac_fail.bin",
            /*  57 */ "unit4_rm1_puzzle_activate.bin",
            /*  58 */ "unit4_rm1_artifact_intro.bin",
            /*  59 */ "unit1_c0_weavel_intro.bin",
            /*  60 */ "bigeye_octolith_intro.bin",
            /*  61 */ "unit2_rm4_panel_open_1.bin",
            /*  62 */ "unit2_rm4_panel_open_2.bin",
            /*  63 */ "unit2_rm4_panel_open_3.bin",
            /*  64 */ "unit2_rm4_cntlroom_open.bin",
            /*  65 */ "unit2_rm4_teleporter_active.bin",
            /*  66 */ "unit2_rm6_teleporter_active.bin", // no file
            /*  67 */ "unit1_rm2_rm3door_open.bin",
            /*  68 */ "unit1_rm2_c3door_open.bin",
            /*  69 */ "unit1_rm3_lavademon_intro.bin", // no file
            /*  70 */ "unit1_rm3_magmaul_intro.bin",
            /*  71 */ "unit3_rm3_race1.bin",
            /*  72 */ "unit3_rm3_race1_fail.bin",
            /*  73 */ "unit3_rm3_race2.bin",
            /*  74 */ "unit3_rm3_race2_fail.bin",
            /*  75 */ "unit3_rm3_incubator_malfunction_intro.bin",
            /*  76 */ "unit3_rm3_incubator_malfunction_outro.bin",
            /*  77 */ "unit3_rm3_door_unlock.bin",
            /*  78 */ "unit1_rm3_forcefield_unlock.bin",
            /*  79 */ "unit4_rm4_sniperspot_intro.bin",
            /*  80 */ "unit4_rm5_artifact_key_intro.bin",
            /*  81 */ "unit4_rm5_artifact_intro.bin",
            /*  82 */ "unit3_rm2_door_unlock.bin",
            /*  83 */ "unit3_rm2_evac_end.bin",
            /*  84 */ "unit1_rm3_forcefield_unlock.bin", // duplicate of 78
            /*  85 */ "unit1_c0_weavel_outro.bin",
            /*  86 */ "unit3_rm1_sylux_preship.bin",
            /*  87 */ "unit1_rm1_mover_activate_layer3.bin",
            /*  88 */ "unit3_rm1_sylux_intro.bin",
            /*  89 */ "unit4_rm3_trace_outro.bin",
            /*  90 */ "unit4_rm5_sniper_intro.bin",
            /*  91 */ "unit3_rm1_artifact_intro.bin",
            /*  92 */ "unit1_rm6_spire_escape.bin",
            /*  93 */ "unit1_crystalroom_octolith.bin",
            /*  94 */ "unit4_co_morphballmaze_exit.bin",
            /*  95 */ "unit4_rm3_key_intro.bin",
            /*  96 */ "unit2_rm1_door_lock.bin",
            /*  97 */ "unit2_rm1_key_intro.bin",
            /*  98 */ "unit3_rm4_morphball.bin",
            /*  99 */ "unit1_c0_morphball_door_unlock.bin",
            /* 100 */ "unit1_rm6_forcefield_lock.bin",
            /* 101 */ "unit1_rm6_forcefield_unlock.bin",
            /* 102 */ "unit1_land_cockpit.bin",
            /* 103 */ "unit2_land_cockpit.bin",
            /* 104 */ "unit3_land_cockpit.bin",
            /* 105 */ "unit4_land_cockpit.bin",
            /* 106 */ "unit1_land_cockpit.bin", // duplicate of 102
            /* 107 */ "unit1_rm1_artifact_intro.bin",
            /* 108 */ "unit2_rm5_artifact_intro.bin",
            /* 109 */ "unit2_c7_forcefield_lock.bin",
            /* 110 */ "unit2_c7_forcefield_unlock.bin",
            /* 111 */ "unit2_c7_artifact_intro.bin",
            /* 112 */ "unit2_rm8_artifact_intro.bin",
            /* 113 */ "unit4_co_door_unlock.bin",
            /* 114 */ "unit1_land_cockpit_land.bin",
            /* 115 */ "unit1_land_cockpit_takeoff.bin",
            /* 116 */ "unit2_land_cockpit_land.bin",
            /* 117 */ "unit2_land_cockpit_takeoff.bin",
            /* 118 */ "unit3_land_cockpit_land.bin",
            /* 119 */ "unit3_land_cockpit_takeoff.bin",
            /* 120 */ "unit4_land_cockpit_land.bin",
            /* 121 */ "unit4_land_cockpit_takeoff.bin",
            /* 122 */ "unit1_land_cockpit_land.bin", // duplicate of 114
            /* 123 */ "unit1_land_cockpit_takeoff.bin", // duplicate of 115
            /* 124 */ "unit1_rm2_mover1_activate.bin",
            /* 125 */ "unit1_rm2_mover2_activate.bin",
            /* 126 */ "unit1_rm2_mover3_activate.bin",
            /* 127 */ "unit3_rm3_race_artifact_intro.bin",
            /* 128 */ "unit1_c5_artifact_intro.bin",
            /* 129 */ "unit1_rm3_key_intro.bin",
            /* 130 */ "unit1_rm3_artifact_intro.bin",
            /* 131 */ "unit1_c3_artifact_intro.bin",
            /* 132 */ "unit4_rm1_forcefield_unlock.bin",
            /* 133 */ "unit4_rm1_wasp_outro.bin",
            /* 134 */ "unit4_rm4_artifact_intro.bin",
            /* 135 */ "unit4_rm4_artifact_outro.bin",
            /* 136 */ "unit3_rm2_artifact_intro.bin",
            /* 137 */ "unit3_rm1_ship_intro.bin",
            /* 138 */ "bigeye1_intro.bin",
            /* 139 */ "unit4_rm2_key_intro.bin",
            /* 140 */ "unit2_rm1_bit_intro.bin",
            /* 141 */ "unit1_rm1_spire_escape.bin",
            /* 142 */ "bigeye_morphball.bin",
            /* 143 */ "unit3_rm3_key_outro.bin",
            /* 144 */ "unit3_rm4_key_intro.bin",
            /* 145 */ "unit3_rm4_key_outro.bin",
            /* 146 */ "unit1_c0_key_intro.bin",
            /* 147 */ "unit1_rm6_key_intro.bin",
            /* 148 */ "unit1_rm6_morphball.bin",
            /* 149 */ "unit3_rm1_bottomfloorkey_intro.bin",
            /* 150 */ "unit4_rm4_key_intro.bin",
            /* 151 */ "unit4_rm4_guardian_outro.bin",
            /* 152 */ "unit4_rm5_forcefield_outro.bin",
            /* 153 */ "unit4_rm5_quadtroid_outro.bin",
            /* 154 */ "unit4_rm2_key_outro.bin",
            /* 155 */ "unit3_rm4_guardian_intro.bin",
            /* 156 */ "unit3_rm4_morphballdoor_unlock.bin",
            /* 157 */ "unit2_rm5_key_intro.bin",
            /* 158 */ "unit3_rm4_item_intro.bin",
            /* 159 */ "unit2_c7_key_intro.bin",
            /* 160 */ "unit2_rm4_forcefield_unlock_1.bin",
            /* 161 */ "unit2_rm4_forcefield_unlock_2.bin",
            /* 162 */ "unit2_rm4_forcefield_unlock_3.bin",
            /* 163 */ "unit3_c2_battlehammer_intro.bin",
            /* 164 */ "unit4_rm3_morphball_cam.bin",
            /* 165 */ "unit4_rm1_door_open.bin",
            /* 166 */ "gorea_b2_gun_intro.bin",
            /* 167 */ "gorea_land_intro.bin",
            /* 168 */ "gorea_land_cockpit.bin",
            /* 169 */ "gorea_land_cockpit_land.bin",
            /* 170 */ "gorea_land_cockpit_takeoff.bin",
            /* 171 */ "unit4_rm1_puzzle_intro.bin",
            /* 172 */ "mp00_intro.bin",
            /* 173 */ "mp01_intro.bin",
            /* 174 */ "mp02_intro.bin",
            /* 175 */ "mp03_intro.bin",
            /* 176 */ "mp04_intro.bin",
            /* 177 */ "mp05_intro.bin",
            /* 178 */ "mp06_intro.bin",
            /* 179 */ "mp07_intro.bin",
            /* 180 */ "mp08_intro.bin",
            /* 181 */ "mp09_intro.bin",
            /* 182 */ "mp10_intro.bin",
            /* 183 */ "mp11_intro.bin",
            /* 184 */ "mp12_intro.bin",
            /* 185 */ "mp13_intro.bin",
            /* 186 */ "mp14_intro.bin",
            /* 187 */ "mp15_intro.bin",
            /* 188 */ "mp16_intro.bin",
            /* 189 */ "mp17_intro.bin",
            /* 190 */ "mp18_intro.bin",
            /* 191 */ "mp19_intro.bin",
            /* 192 */ "mp20_intro.bin",
            /* 193 */ "mp21_intro.bin",
            /* 194 */ "mp22_intro.bin",
            /* 195 */ "mp23_intro.bin",
            /* 196 */ "mp24_intro.bin",
            /* 197 */ "mp25_intro.bin",
            /* 198 */ "mp26_intro.bin"
        };
    }
}
