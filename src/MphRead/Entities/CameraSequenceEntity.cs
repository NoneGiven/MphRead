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

        private static readonly CameraSequence?[] _sequenceData = new CameraSequence[172]; // game uses 175, but the last three are tmp.bin

        private int _keyframeIndex = 0;
        private float _keyframeElapsed = 0;

        private byte _entityFlags = 0;
        private byte _sequenceFlags = 0;

        public CameraSequenceEntity(CameraSequenceEntityData data) : base(EntityType.CameraSequence)
        {
            Data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
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
            // todo: entity refs
        }

        public override bool Process(Scene scene)
        {
            // todo: delay timer and other stuff at the entity level
            if (Active && (_sequenceFlags & 1) == 0 && Sequence.Keyframes.Count != 0)
            {
                CameraSequenceKeyframe curFrame = Sequence.Keyframes[_keyframeIndex];
                float frameLength = curFrame.HoldTime.FloatValue + curFrame.MoveTime.FloatValue;
                CalculateFrameValues(scene);
                // todo: messaging, fades, field2/field3 stuff
                _keyframeElapsed += scene.FrameTime;
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
            float moveElapsed = _keyframeElapsed - curFrame.HoldTime.FloatValue;
            if (moveElapsed >= 0)
            {
                movePercent = moveElapsed / curFrame.MoveTime.FloatValue;
            }
            Vector4 moveVec = GetVec4(movePercent);
            // current/prev/after/next
            var factorVec = new Vector4(
                0,
                (curFrame.Field32 & 1) == 0 ? 1 / 3f : 0,
                (curFrame.Field33 & 1) == 0 ? 2 / 3f : 1,
                1
            );
            float factorDot = Vector4.Dot(factorVec, moveVec);
            bool hasNextFrame = false;
            CameraSequenceKeyframe nextFrame = default;
            if (_keyframeIndex + 1 < Sequence.Keyframes.Count)
            {
                hasNextFrame = true;
                nextFrame = Sequence.Keyframes[_keyframeIndex + 1];
            }
            if (hasNextFrame)
            {
                if (((curFrame.Field32 | curFrame.Field33) & 2) != 0 && curFrame.MoveTime.FloatValue > 0)
                {
                    Vector3 curPos = curFrame.Position.ToFloatVector();
                    Vector3 curTarget = curFrame.ToTarget.ToFloatVector();
                    Vector3 nextPos = nextFrame.Position.ToFloatVector();
                    Vector3 nextTarget = nextFrame.ToTarget.ToFloatVector();
                    AddEntityPosition(curFrame, ref curPos, ref curTarget);
                    AddEntityPosition(nextFrame, ref nextPos, ref nextTarget);
                    Vector3 curToNextPos = (nextPos - curPos) / curFrame.MoveTime.FloatValue;
                    Vector3 curToNextTarget = (nextTarget - curTarget) / curFrame.MoveTime.FloatValue;
                    bool hasPrevFrame = false;
                    CameraSequenceKeyframe prevFrame = default;
                    if (_keyframeIndex - 1 >= 0)
                    {
                        hasPrevFrame = true;
                        prevFrame = Sequence.Keyframes[_keyframeIndex - 1];
                    }
                    Vector3 prevPos;
                    Vector3 prevTarget;
                    if (_keyframeIndex > 0 && hasPrevFrame && (curFrame.Field32 & 2) != 0 && prevFrame.MoveTime.FloatValue > 0)
                    {
                        float easing = 1 / 6f * curFrame.MoveTime.FloatValue * curFrame.Easing.FloatValue;
                        prevPos = prevFrame.Position.ToFloatVector();
                        prevTarget = prevFrame.ToTarget.ToFloatVector();
                        AddEntityPosition(prevFrame, ref prevPos, ref prevTarget);
                        Vector3 prevToCurPos = (curPos - prevPos) / prevFrame.MoveTime.FloatValue;
                        Vector3 prevToNextPos = curToNextPos + prevToCurPos;
                        prevPos = prevToNextPos * easing + curPos;
                        Vector3 prevToCurTarget = (curTarget - prevTarget) / prevFrame.MoveTime.FloatValue;
                        Vector3 prevToNextTarget = curToNextTarget + prevToCurTarget;
                        prevTarget = prevToNextTarget * easing + curTarget;
                    }
                    else
                    {
                        float easing = 1 / 3f * curFrame.MoveTime.FloatValue * curFrame.Easing.FloatValue;
                        prevPos = curToNextPos * easing + curPos;
                        prevTarget = curToNextTarget * easing + curTarget;
                    }
                    bool hasAfterFrame = false;
                    CameraSequenceKeyframe afterFrame = default;
                    if (_keyframeIndex + 2 < Sequence.Keyframes.Count)
                    {
                        hasAfterFrame = true;
                        afterFrame = Sequence.Keyframes[_keyframeIndex + 2];
                    }
                    Vector3 afterPos;
                    Vector3 afterTarget;
                    if (hasAfterFrame && (curFrame.Field33 & 2) != 0 && nextFrame.MoveTime.FloatValue > 0)
                    {
                        float easing = -1 / 6f * curFrame.MoveTime.FloatValue * nextFrame.Easing.FloatValue;
                        afterPos = afterFrame.Position.ToFloatVector();
                        afterTarget = afterFrame.ToTarget.ToFloatVector();
                        AddEntityPosition(afterFrame, ref afterPos, ref afterTarget);
                        Vector3 nextToAfterPos = (afterPos - nextPos) / nextFrame.MoveTime.FloatValue;
                        Vector3 currentToAfterPos = curToNextPos + nextToAfterPos;
                        afterPos = currentToAfterPos * easing + nextPos;
                        Vector3 nextToAfterTarget = (afterTarget - nextTarget) / nextFrame.MoveTime.FloatValue;
                        Vector3 currentToAfterTarget = curToNextTarget + nextToAfterTarget;
                        afterTarget = currentToAfterTarget * easing + nextTarget;
                    }
                    else
                    {
                        float easing = -1 / 3f * curFrame.MoveTime.FloatValue * curFrame.Easing.FloatValue;
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
                    finalRoll = curFrame.Roll.FloatValue * (1 - factorDot) + nextFrame.Roll.FloatValue * factorDot;
                    finalFov = curFrame.Fov.FloatValue * (1 - factorDot) + nextFrame.Fov.FloatValue * factorDot;
                }
                else
                {
                    Vector3 curPos = curFrame.Position.ToFloatVector();
                    Vector3 curTarget = curFrame.ToTarget.ToFloatVector();
                    Vector3 nextPos = nextFrame.Position.ToFloatVector();
                    Vector3 nextTarget = nextFrame.ToTarget.ToFloatVector();
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
                    finalRoll = (curFrame.Roll.FloatValue * (1 - factorDot)) + (nextFrame.Roll.FloatValue * factorDot);
                    finalFov = (curFrame.Fov.FloatValue * (1 - factorDot)) + (nextFrame.Fov.FloatValue * factorDot);
                }
            }
            else
            {
                Vector3 curPos = curFrame.Position.ToFloatVector();
                Vector3 curTarget = curFrame.ToTarget.ToFloatVector();
                AddEntityPosition(curFrame, ref curPos, ref curTarget);
                finalPosition = curPos;
                finalToTarget = curTarget;
                finalRoll = curFrame.Roll.FloatValue;
                finalFov = curFrame.Fov.FloatValue;
            }
            // todo: pass and use roll and FOV
            scene.SetCamera(finalPosition, finalToTarget + finalPosition, Vector3.UnitY);
        }

        private void AddEntityPosition(CameraSequenceKeyframe keyframe, ref Vector3 vec1, ref Vector3 vec2)
        {
            // sktodo
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

        private Vector3 VecMultAdd(Vector3 vec1, Vector3 vec2, float factor)
        {
            return vec1 * factor + vec2;
        }
    }
}
