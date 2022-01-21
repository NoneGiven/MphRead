using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class CamSeqEntity : EntityBase
    {
        public CameraSequenceEntityData Data { get; }
        protected override Vector4? OverrideColor { get; } = new ColorRgb(0xFF, 0x69, 0xB4).AsVector4();
        public CameraSequence Sequence { get; }
        public string Name => Sequence.Name;

        private bool _active = false;
        private bool _handoff = false;
        private byte _handoffTimer = 0;
        private byte _delayTimer = 0;
        private EntityBase? _endMessageTarget = null;

        public static CamSeqEntity? Current { get; set; }
        // todo: clear when changing rooms
        private static readonly CameraSequence?[] _sequenceData = new CameraSequence[199];

        public CamSeqEntity(CameraSequenceEntityData data, Scene scene) : base(EntityType.CameraSequence, scene)
        {
            Data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            AddPlaceholderModel();
            byte seqId = data.SequenceId;
            CameraSequence? sequence = _sequenceData[seqId];
            if (sequence == null)
            {
                sequence = CameraSequence.Load(seqId, scene);
                _sequenceData[seqId] = sequence;
            }
            Sequence = sequence;
        }

        public override void Initialize()
        {
            base.Initialize();
            // these would override the keyframe refs if they were used, but they aren't
            Debug.Assert(Data.PlayerId1 == 0);
            Debug.Assert(Data.PlayerId2 == 0);
            Debug.Assert(Data.Entity1 == -1);
            Debug.Assert(Data.Entity2 == -1);
            if (Data.EndMessageTargetId != -1)
            {
                _scene.TryGetEntity(Data.EndMessageTargetId, out _endMessageTarget);
            }
            Sequence.Initialize();
        }

        public override bool Process()
        {
            if (!_active)
            {
                return base.Process();
            }
            if (_handoffTimer > 0)
            {
                _handoffTimer--;
                if (_handoffTimer == 0)
                {
                    Cancel();
                    return base.Process();
                }
            }
            if (_delayTimer <= Data.DelayFrames * 2) // todo: FPS stuff
            {
                TryStart();
            }
            if (_delayTimer > Data.DelayFrames * 2) // todo: FPS stuff
            {
                Sequence.Process();
                if (Sequence.Flags.TestFlag(CamSeqFlags.CanEnd))
                {
                    if (Data.Loop != 0)
                    {
                        ushort transitionTimer = Sequence.TransitionTimer;
                        Sequence.Restart();
                        Sequence.TransitionTimer = transitionTimer;
                    }
                    else
                    {
                        // todo: stop SFX scripts, stop/restart long running/env SFX, play paused music
                        _active = false;
                        Sequence.End();
                        Current = null;
                        PlayerEntity.Main.RefreshExternalCamera();
                        SendEndEvent();
                    }
                }
            }
            return base.Process();
        }

        private void TryStart()
        {
            PlayerEntity player = PlayerEntity.Main;
            if (player.Health == 0 && player.DeathCountdown > 0 && Data.BlockInput != 0) // todo: or if some pause global
            {
                return;
            }
            // todo: test music mask
            if (_delayTimer == 0)
            {
                if (Data.Loop == 0)
                {
                    // todo: stop SFX
                }
                // todo: stop SFX
            }
            _delayTimer++;
            if (_delayTimer > Data.DelayFrames * 2) // todo: FPS stuff
            {
                // todo: update SFX scripts and music
                Start();
            }
        }

        private void Start()
        {
            // the game overrides keyframe values with ent1/ent2/player1/player2, but none of those are ever set
            Sequence.Flags &= ~CamSeqFlags.BlockInput;
            Sequence.Flags &= ~CamSeqFlags.ForceAlt;
            Sequence.Flags &= ~CamSeqFlags.ForceBiped;
            if (Data.BlockInput != 0)
            {
                Sequence.Flags |= CamSeqFlags.BlockInput;
            }
            if (Data.ForceAltForm != 0)
            {
                Sequence.Flags |= CamSeqFlags.ForceAlt;
            }
            else if (Data.ForceBipedForm != 0 && Data.BlockInput != 0)
            {
                Sequence.Flags |= CamSeqFlags.ForceBiped;
            }
            ushort transitionTime = (ushort)(_handoff ? 60 * 2 : 0); // todo: FPS stuff
            Sequence.SetUp(PlayerEntity.Main.CameraInfo, transitionTime);
            PlayerEntity.Main.RefreshExternalCamera();
        }

        private void Cancel()
        {
            // todo: something with SFX
            SendEndEvent();
            Sequence.End();
            _active = false;
            if (CameraSequence.Current == Sequence)
            {
                PlayerEntity player = PlayerEntity.Main;
                player.RefreshExternalCamera();
                if (Sequence.CamInfoRef == player.CameraInfo
                    && (player.IsAltForm || player.IsMorphing || player.IsUnmorphing))
                {
                    player.ResumeOwnCamera();
                }
            }
            if (Current == this)
            {
                Current = null;
            }
        }

        private void SendEndEvent()
        {
            if (Data.EndMessage != Message.None)
            {
                _scene.SendMessage(Data.EndMessage, this, _endMessageTarget, Data.EndMessageParam, 0);
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message == Message.Activate || (info.Message == Message.SetActive && (int)info.Param1 != 0))
            {
                PlayerEntity player = PlayerEntity.Main;
                if (player.Health == 0 && player.DeathCountdown > 0 && Data.BlockInput != 0)
                {
                    return;
                }
                bool activate = true;
                bool handoff = false;
                if (Current != null)
                {
                    if (Current.Data.BlockInput != 0)
                    {
                        activate = false;
                    }
                    if (Current.Data.Handoff != 0 && Data.Handoff != 0)
                    {
                        handoff = true;
                        if (Current._handoffTimer == 0)
                        {
                            activate = false;
                        }
                    }
                }
                if (CameraSequence.Current != null && CameraSequence.Current.Flags.TestFlag(CamSeqFlags.BlockInput))
                {
                    activate = false;
                }
                if (activate)
                {
                    if (Current != null && Current != this)
                    {
                        if (handoff)
                        {
                            Sequence.CamInfoRef = null;
                        }
                        Current.Cancel();
                    }
                    if (CameraSequence.Current != null && CameraSequence.Current != Sequence)
                    {
                        CameraSequence.Current.End();
                    }
                    if (!_active)
                    {
                        _active = true;
                        _delayTimer = 0;
                        _handoffTimer = 0;
                        _handoff = handoff;
                        Current = this;
                        if (Data.DelayFrames == 0)
                        {
                            TryStart();
                        }
                    }
                }
                else
                {
                    IReadOnlyList<CameraSequenceKeyframe> keyframes = Sequence.Keyframes;
                    for (int i = 0; i < keyframes.Count; i++)
                    {
                        CameraSequenceKeyframe keyframe = keyframes[i];
                        var message = (Message)keyframe.MessageId;
                        if (message != Message.None)
                        {
                            // game sets the keyframe as the sender
                            _scene.SendMessage(message, null!, keyframe.MessageTarget, (int)keyframe.MessageParam, 0);
                        }
                    }
                }
            }
            else if (info.Message == Message.SetActive && (int)info.Param1 == 0)
            {
                if (_handoffTimer == 0)
                {
                    _handoffTimer = 2 * 2; // todo: FPS stuff
                }
            }
        }

        public static void CancelCurrent()
        {
            Current?.Cancel();
        }
    }
}
