using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class ArtifactEntity : EntityBase
    {
        private readonly ArtifactEntityData _data;
        private readonly float _heightOffset;

        private bool _invSetUp = false;
        private EntityBase? _parent = null;
        private Vector3 _invPos;
        private EntityBase? _msgTarget1 = null;
        private EntityBase? _msgTarget2 = null;
        private EntityBase? _msgTarget3 = null;

        public new bool Active { get; set; }

        public ArtifactEntity(ArtifactEntityData data, string nodeName, Scene scene)
            : base(EntityType.Artifact, nodeName, scene)
        {
            // todo: load resources for simple octolith/dropped octolith when needed
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            string name = data.ModelId >= 8 ? "Octolith" : $"Artifact0{data.ModelId + 1}";
            ModelInstance inst = SetUpModel(name);
            _heightOffset = data.ModelId >= 8 ? 1.75f : inst.Model.Nodes[0].BoundingRadius;
            if (data.HasBase != 0)
            {
                SetUpModel("ArtifactBase");
            }
            Debug.Assert(scene.GameMode == GameMode.SinglePlayer);
            Active = GameState.StorySave.InitRoomState(_scene.RoomId, Id, active: _data.Active != 0, activeState: 2) != 0;
        }

        public override void Initialize()
        {
            base.Initialize();
            if (_data.LinkedEntityId != -1)
            {
                if (_scene.TryGetEntity(_data.LinkedEntityId, out EntityBase? parent))
                {
                    _parent = parent;
                }
            }
            if (_scene.TryGetEntity(_data.Message1Target, out EntityBase? target))
            {
                _msgTarget1 = target;
            }
            if (_scene.TryGetEntity(_data.Message2Target, out target))
            {
                _msgTarget2 = target;
            }
            if (_scene.TryGetEntity(_data.Message3Target, out target))
            {
                _msgTarget3 = target;
            }
        }

        private readonly IReadOnlyList<int> _scanIds = new int[32]
        {
            48, 48, 48, 48, 48, 48, 48, 48, 40, 41, 42, 40, 41, 42, 40, 41,
            42, 40, 41, 42, 40, 41, 42, 40, 41, 42, 40, 41, 42, 40, 41, 42
        };

        private readonly IReadOnlyList<int> _octolithMessageIds = new int[8]
        {
            14, 31, 32, 33, 34, 35, 36, 51
        };

        public override bool Process()
        {
            if (_parent != null)
            {
                if (!_invSetUp)
                {
                    _parent.GetDrawInfo(); // force update transforms
                    _invPos = Matrix.Vec3MultMtx4(Position, _parent.CollisionTransform.Inverted());
                    _invSetUp = true;
                }
                Position = Matrix.Vec3MultMtx4(_invPos, _parent.CollisionTransform);
            }
            if (Active)
            {
                _soundSource.Update(Position, rangeIndex: 7);
                // sfxtodo: if node ref is not active, set sound volume override to 0
                _soundSource.PlaySfx(SfxId.ARTIFACT_LOOP, loop: true);
                if (_data.ModelId >= 8)
                {
                    _scanId = _scanIds[_data.ArtifactId];
                }
                else
                {
                    _scanId = _scanIds[_data.ModelId * 3 + 8 + _data.ArtifactId];
                }
                if (CameraSequence.Current?.BlockInput == true)
                {
                    return base.Process();
                }
                // todo: move dropped octolith toward player
                PlayerEntity player = PlayerEntity.Main;
                if (player.Health == 0)
                {
                    return base.Process();
                }
                // todo: visualize pickup volumes
                Vector3 position = Position.AddY(_heightOffset);
                bool pickedUp = false;
                float radii = player.Volume.SphereRadius + (_data.ModelId >= 8 ? 1 : 0.1f);
                if (player.IsAltForm)
                {
                    Vector3 between = position - player.Volume.SpherePosition;
                    if (Vector3.Dot(between, between) < radii * radii)
                    {
                        pickedUp = true;
                    }
                }
                else
                {
                    Vector3 between = position - player.Position;
                    if (between.X * between.X + between.Z * between.Z < radii * radii)
                    {
                        float diffY = position.Y - player.Position.Y;
                        float maxY = Fixed.ToFloat(player.Values.MaxPickupHeight);
                        if (diffY <= maxY + 0.5f)
                        {
                            float minY = Fixed.ToFloat(player.Values.MinPickupHeight);
                            if (diffY >= minY - 0.5f)
                            {
                                pickedUp = true;
                            }
                        }
                    }
                }
                if (!pickedUp)
                {
                    return base.Process();
                }
                if (_data.Message1 != Message.None)
                {
                    _scene.SendMessage(_data.Message1, this, _msgTarget1, 0, 0);
                }
                if (_data.Message2 != Message.None)
                {
                    _scene.SendMessage(_data.Message2, this, _msgTarget2, 0, 0);
                }
                if (_data.Message3 != Message.None)
                {
                    _scene.SendMessage(_data.Message3, this, _msgTarget3, 0, 0);
                }
                if (_data.ModelId >= 8)
                {
                    // todo: update story save
                    if (Id == -1)
                    {
                        // OCTOLITH RECLAIMED you recovered a stolen OCTOLITH!
                        PlayerEntity.Main.ShowDialog(DialogType.Event, messageId: 54, param1: (int)EventType.Octolith);
                    }
                    else
                    {
                        // todo: start movie, update global state and story save, defer the dialog
                        // OCTOLITH ACQUIRED you obtained an OCTOLITH!
                        PlayerEntity.Main.ShowDialog(DialogType.Event, messageId: 7, param1: (int)EventType.Octolith);
                        int messageId = _octolithMessageIds[0]; // todo: get index from story save
                        _scene.SendMessage(Message.ShowPrompt, this, null, param1: messageId, param2: 0, delay: 1);
                    }
                }
                else
                {
                    int collected = 1;
                    // todo: update story save
                    _soundSource.PlayFreeSfx(SfxId.ARTIFACT1); // sfxtodo: play correct SFX
                    // ARTIFACT DISCOVERED you retrieved an ALIMBIC ARTIFACT!
                    PlayerEntity.Main.ShowDialog(DialogType.Event, messageId: 6, param1: (int)EventType.Artifact);
                    if (collected > 2)
                    {
                        // PORTAL ACTIVATED long-range thermomagnetic-resonance scanners indicate remote
                        // and inaccessible chambers. use the PORTAL to access the inaccessible.
                        _scene.SendMessage(Message.ShowPrompt, this, null, param1: 13, param2: 0, delay: 1);
                    }
                }
                Active = false;
                // todo: room state
                _soundSource.StopAllSfx(force: true);
            }
            else
            {
                _scanId = 0;
            }
            return base.Process();
        }

        public override void GetDrawInfo()
        {
            if (Active && IsVisible(NodeRef))
            {
                base.GetDrawInfo();
            }
        }

        public override void HandleMessage(MessageInfo info)
        {
            if (info.Message != Message.Activate)
            {
                Active = true;
                // todo: room state
            }
            else if (info.Message != Message.SetActive)
            {
                if ((int)info.Param1 != 0)
                {
                    Active = true;
                    // todo: room state
                }
                else
                {
                    Active = false;
                    // todo: room state
                }
            }
            else if (info.Message != Message.MoveItemSpawner)
            {
                if (info.Sender.Type == EntityType.EnemySpawn)
                {
                    var spawner = (EnemySpawnEntity)info.Sender;
                    if (spawner.Data.EnemyType == EnemyType.Hunter)
                    {
                        // todo: check bot spawner slots and find player to move to
                    }
                    else
                    {
                        info.Sender.GetPosition(out Vector3 position);
                        Position = position;
                    }
                }
                else
                {
                    info.Sender.GetPosition(out Vector3 position);
                    Position = position;
                }
            }
        }

        public override void Destroy()
        {
            _soundSource.StopAllSfx(force: true);
            base.Destroy();
        }

        protected override LightInfo GetLightInfo()
        {
            if (_data.ModelId >= 8)
            {
                Vector3 player = _scene.CameraMode == CameraMode.Player
                    ? PlayerEntity.Main.CameraInfo.Position
                    : _scene.CameraPosition; // skdebug
                var vector1 = new Vector3(0, 1, 0);
                Vector3 vector2 = new Vector3(player.X - Position.X, 0, player.Z - Position.Z).Normalized();
                Matrix3 lightTransform = Matrix.GetTransform3(vector2, vector1);
                return new LightInfo(
                    (Metadata.OctolithLight1Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor,
                    (Metadata.OctolithLight2Vector * lightTransform).Normalized(),
                    Metadata.OctolithLightColor
                );
            }
            return base.GetLightInfo();
        }

        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 0)
            {
                transform.Row3.Y += _heightOffset;
            }
            else if (index == 1)
            {
                transform.Row3.Y -= 0.2f;
            }
            return transform;
        }
    }
}
