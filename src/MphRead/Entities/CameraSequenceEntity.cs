using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class CameraSequenceEntity : EntityBase
    {
        private readonly CameraSequenceEntityData _data;
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x69, 0xB4).AsVector4();

        private byte _flags;
        private readonly IReadOnlyList<CameraSequenceKeyframe> _keyframes;

        private static readonly CameraSequence?[] _sequenceData = new CameraSequence[172]; // game uses 175, but the last three are tmp.bin

        public CameraSequenceEntity(CameraSequenceEntityData data) : base(EntityType.CameraSequence)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.RightVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            byte id = _data.Id;
            CameraSequence? sequence = _sequenceData[id];
            if (sequence == null)
            {
                sequence = CameraSequence.Load(id);
                _sequenceData[id] = sequence;
            }
            _flags = sequence.Flags;
            _keyframes = sequence.Keyframes;
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            // todo: entity refs
        }
    }
}
