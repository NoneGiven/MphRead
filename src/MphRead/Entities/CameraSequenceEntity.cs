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
        }

        public override void Initialize(Scene scene)
        {
            base.Initialize(scene);
            // todo: entity refs
        }
    }
}
