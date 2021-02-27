using System;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class CameraSequenceEntity : EntityBase
    {
        public CameraSequenceEntityData Data { get; }
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x69, 0xB4).AsVector4();

        public CameraSequence Sequence { get; }
        public string Name => Sequence.Name;

        private static readonly CameraSequence?[] _sequenceData = new CameraSequence[199];

        private int _keyframeIndex = 0;
        private float _keyframeElapsed = 0;

        //private byte _entityFlags = 0;
        private byte _sequenceFlags = 0;

        private EntityBase? _messageTarget = null;

        public CameraSequenceEntity(CameraSequenceEntityData data) : base(EntityType.CameraSequence)
        {
            Data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            byte seqId = data.SequenceId;
            CameraSequence? sequence = _sequenceData[seqId];
            if (sequence == null)
            {
                sequence = CameraSequence.Load(seqId);
                _sequenceData[seqId] = sequence;
            }
            Sequence = sequence;
            Active = false;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            // these would override the keyframe refs if they were used, but they aren't
            Debug.Assert(Data.PlayerId1 == 0);
            Debug.Assert(Data.PlayerId2 == 0);
            Debug.Assert(Data.Entity1 == UInt16.MaxValue);
            Debug.Assert(Data.Entity2 == UInt16.MaxValue);
            if (Data.MessageTargetId != UInt16.MaxValue)
            {
                scene.TryGetEntity(Data.MessageTargetId, out _messageTarget);
            }
            foreach (CameraSequenceKeyframe keyframe in Sequence.Keyframes)
            {
                keyframe.Entity1 = GetKeyframeRef(keyframe.Entity1Type, keyframe.Entity1Id, scene);
                keyframe.Entity2 = GetKeyframeRef(keyframe.Entity2Type, keyframe.Entity2Id, scene);
                keyframe.MessageTarget = GetKeyframeRef(keyframe.MessageTargetType, keyframe.MessageTargetId, scene);
            }
        }

        private EntityBase? GetKeyframeRef(ushort type, ushort id, Scene scene)
        {
            if (type == 25)
            {
                Debug.Assert(id < PlayerEntity.MaxPlayers);
                return PlayerEntity.Players[id];
            }
            if (type != UInt16.MaxValue && scene.TryGetEntity(id, out EntityBase? entity))
            {
                return entity;
            }
            return null;
        }

        public override bool Process(Scene scene)
        {
            // todo: delay timer and other stuff at the entity level
            if (Active && (_sequenceFlags & 1) == 0 && Sequence.Keyframes.Count != 0)
            {
                scene.StartCutscene();
                CameraSequenceKeyframe curFrame = Sequence.Keyframes[_keyframeIndex];
                float frameLength = curFrame.HoldTime + curFrame.MoveTime;
                float fadeOutStart = frameLength - curFrame.FadeOutTime;
                CalculateFrameValues(scene);
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
                    scene.SetFade(fadeType, fadeTime, overwrite: false);
                }
                // todo: messaging, field2/field3 stuff
                if (curFrame.HoldTime == 0 && curFrame.MoveTime == 0)
                {
                    // make zero-length frames last for 2 frames (equivalent to 1 frame in game)
                    _keyframeElapsed += scene.FrameTime / 2;
                }
                else
                {
                    _keyframeElapsed += scene.FrameTime;
                }
                if (_keyframeElapsed >= frameLength)
                {
                    _keyframeElapsed -= frameLength;
                    _keyframeIndex++;
                    // todo: looping or ending
                    if (_keyframeIndex >= Sequence.Keyframes.Count)
                    {
                        _keyframeIndex = 0;
                        _keyframeElapsed = 0;
                        Active = false;
                        scene.EndCutscene();
                    }
                }
            }
            // todo: player input restrictions, etc.
            return base.Process(scene);
        }

        private void CalculateFrameValues(Scene scene)
        {
            // todo: set camera shake to 0 once that exists
            Vector3 finalPosition;
            Vector3 finalToTarget;
            float finalRoll;
            float finalFov;
            CameraSequenceKeyframe curFrame = Sequence.Keyframes[_keyframeIndex];
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
            if (_keyframeIndex + 1 < Sequence.Keyframes.Count)
            {
                nextFrame = Sequence.Keyframes[_keyframeIndex + 1];
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
                        prevFrame = Sequence.Keyframes[_keyframeIndex - 1];
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
                    if (_keyframeIndex + 2 < Sequence.Keyframes.Count)
                    {
                        afterFrame = Sequence.Keyframes[_keyframeIndex + 2];
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
            // todo: pass and use roll
            finalFov = MathHelper.DegreesToRadians(finalFov * 2);
            finalRoll = MathHelper.DegreesToRadians(finalRoll);
            scene.SetCamera(finalPosition, finalToTarget, finalFov, finalRoll);
        }

        private void AddEntityPosition(CameraSequenceKeyframe keyframe, ref Vector3 vec1, ref Vector3 vec2)
        {
            // ctodo: entity position/tracking
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
    }
}
