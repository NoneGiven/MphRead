using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public class OctolithFlagEntity : EntityBase
    {
        private readonly OctolithFlagEntityData _data;
        public OctolithFlagEntityData Data => _data;
        private readonly Vector3 _basePosition = Vector3.Zero;
        public Vector3 BasePosition => _basePosition;
        private readonly bool _bounty = false;

        private PlayerEntity? _carrier = null;
        public PlayerEntity? Carrier => _carrier;
        private PlayerEntity? _lastCarrier = null;
        private bool _atBase = false;
        public bool AtBase => _atBase;
        private bool _grounded = false;
        private float _resetTimer = 0;
        private float _gravity = 0;

        public OctolithFlagEntity(OctolithFlagEntityData data, Scene scene) : base(EntityType.OctolithFlag, scene)
        {
            _data = data;
            Id = data.Header.EntityId;
            SetTransform(data.Header.FacingVector, data.Header.UpVector, data.Header.Position);
            GameMode mode = scene.GameMode;
            Recolor = mode == GameMode.Capture ? data.TeamId : 2;
            _bounty = mode != GameMode.Capture;
            if (mode == GameMode.Capture || mode == GameMode.Bounty || mode == GameMode.BountyTeams)
            {
                SetUpModel("octolith_ctf");
                SetUpModel(mode == GameMode.Capture ? "flagbase_ctf" : "flagbase_bounty");
                _basePosition = Position;
                SetAtBase();
            }
        }

        private void SetAtBase()
        {
            Position = _basePosition.AddY(1.25f);
            _atBase = true;
            _grounded = true;
            _resetTimer = 0;
            _gravity = 0;
            if (_carrier != null)
            {
                _carrier.OctolithFlag = null;
                _carrier = null;
            }
            _lastCarrier = null;
            // todo: nodedata
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = _basePosition;
            up = UpVector;
            facing = FacingVector;
        }

        public override void GetPosition(out Vector3 position)
        {
            position = _basePosition;
        }

        public override bool Process()
        {
            base.Process();
            // todo?: lots of wifi stuff
            bool pickedUp = false;
            if (_carrier == null)
            {
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    EntityBase entity = _scene.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }
                    var player = (PlayerEntity)entity;
                    if (player.Health == 0 || player.IsAltForm || player.IsMorphing
                        || !_bounty && player.TeamIndex == _data.TeamId && _atBase)
                    {
                        continue;
                    }
                    float max = Fixed.ToFloat(player.Values.MaxPickupHeight);
                    float min = Fixed.ToFloat(player.Values.MinPickupHeight);
                    float radius = Fixed.ToFloat(player.Values.BipedColRadius);
                    var cylPos = new Vector3(Position.X, Position.Y - max - 1.25f, Position.Z);
                    float cylHeight = max - min + 0.5f;
                    float radii = radius + 0.5f;
                    CollisionResult discard = default;
                    if (CollisionDetection.CheckCylinderBetweenPoints(player.PrevPosition, player.Position,
                        cylPos, cylHeight, radii, ref discard))
                    {
                        pickedUp = OnTouched(player);
                        break;
                    }
                }
                if (!_atBase && _carrier == null)
                {
                    _resetTimer += _scene.FrameTime;
                    if (_resetTimer >= 20)
                    {
                        Reset();
                    }
                }
            }
            if (_carrier != null)
            {
                _atBase = false;
                _grounded = true;
                _resetTimer = 0;
                // todo: nodedata
                Position = new Vector3(
                    _carrier.Position.X + -0.35f * _carrier.Field70,
                    _carrier.Position.Y + 1.05f,
                    _carrier.Position.Z + -0.35f * _carrier.Field74
                );
                if (_carrier.Health <= 0 || _carrier.IsAltForm || _carrier.IsMorphing
                    || !_carrier.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    bool reset = _carrier.Health == 0 && GameState.OctolithReset;
                    OnDropped(reset);
                }
                else if (pickedUp)
                {
                    if (!_bounty)
                    {
                        if (PlayerEntity.Main.TeamIndex == _data.TeamId)
                        {
                            // todo: play voice, update music
                        }
                        else
                        {
                            // todo: play SFX
                        }
                    }
                    else
                    {
                        // todo: update music
                        if (_carrier == PlayerEntity.Main)
                        {
                            // todo: play SFX, play voice
                            PlayerEntity.Main.QueueHudMessage(128, 133, 90 / 30f, 1, 202); // return to base
                        }
                        else
                        {
                            // todo: play voice, update music
                        }
                    }
                }
            }
            if (_grounded)
            {
                _gravity = 0;
            }
            else
            {
                Vector3 prevPos = Position;
                (float gravity, float displacement) = ConstantAcceleration(-0.02f, _gravity);
                Position = Position.AddY(displacement);
                _gravity = gravity;
                var results = new CollisionResult[16];
                int count = CollisionDetection.CheckSphereBetweenPoints(prevPos, Position, radius: 1.25f,
                    limit: 16, includeOffset: false, TestFlags.None, _scene, results);
                for (int i = 0; i < count; i++)
                {
                    CollisionResult result = results[i];
                    if (result.Plane.Y > Fixed.ToFloat(1401))
                    {
                        Vector3 pos = Position.AddY(-1.25f);
                        float dist = result.Plane.W - Vector3.Dot(pos, result.Plane.Xyz);
                        Position += result.Plane.Xyz * dist;
                        _grounded = true;
                    }
                }
                Debug.Assert(_scene.Room != null);
                if (Position.Y < _scene.Room.Metadata.KillHeight)
                {
                    Reset();
                }
            }
            return true;
        }

        private bool OnTouched(PlayerEntity player)
        {
            if (_lastCarrier != null && player.TeamIndex != _lastCarrier.TeamIndex)
            {
                GameState.OctolithStops[player.SlotIndex]++;
            }
            if (!_bounty && player.TeamIndex == _data.TeamId)
            {
                if (!_atBase)
                {
                    // todo: play SFX
                    // your octolith reset! / enemy octolith reset!
                    int messageId = PlayerEntity.Main.TeamIndex == _data.TeamId ? 201 : 207;
                    PlayerEntity.Main.QueueHudMessage(128, 133, 60 / 30f, 1, messageId);
                }
                SetAtBase();
                return false;
            }
            if (_carrier != null)
            {
                _carrier.OctolithFlag = null;
            }
            player.OctolithFlag = this;
            _carrier = player;
            _lastCarrier = player;
            _atBase = false;
            _grounded = true;
            _resetTimer = 0;
            return true;
        }

        private void Reset()
        {
            SetAtBase();
            int messageId;
            if (!_bounty)
            {
                if (PlayerEntity.Main.TeamIndex == _data.TeamId)
                {
                    messageId = 201; // your octolith reset!
                    // todo: play SFX
                }
                else
                {
                    messageId = 207; // enemy octolith reset!
                    // todo: play SFX
                }
            }
            else
            {
                messageId = 257; // octolith reset!
                // todo: play SFX
            }
            PlayerEntity.Main.QueueHudMessage(128, 133, 60 / 30f, 1, messageId);
        }

        private void OnDropped(bool reset)
        {
            Debug.Assert(_carrier != null);
            GameState.OctolithDrops[_carrier.SlotIndex]++;
            int messageId;
            if (!_bounty)
            {
                if (PlayerEntity.Main.TeamIndex == _data.TeamId)
                {
                    if (reset)
                    {
                        messageId = 201; // your octolith reset!
                        // todo: play SFX
                    }
                    else
                    {
                        messageId = 230; // the enemy dropped your octolith!
                        // todo: play voice
                    }
                }
                else if (reset)
                {
                    messageId = 207; // enemy octolith reset!
                    // todo: play SFX
                }
                else
                {
                    messageId = 231; // your team dropped the octolith!
                    // todo: play SFX
                }
            }
            else if (reset)
            {
                messageId = 257; // octolith reset!
                // todo: play SFX
            }
            else
            {
                messageId = 229; // the octolith has been dropped!
                // todo: play voice, play SFX
            }
            PlayerEntity.Main.QueueHudMessage(128, 133, 60 / 30f, 1, messageId);
            // todo: stop SFX, update music
            if (reset)
            {
                SetAtBase();
            }
            else
            {
                _grounded = false;
                _atBase = false;
                if (_carrier != null)
                {
                    _carrier.OctolithFlag = null;
                    _carrier = null;
                }
                _resetTimer = 0;
            }
        }

        public void OnCaptured()
        {
            Debug.Assert(_carrier != null);
            if (!_bounty)
            {
                if (PlayerEntity.Main.TeamIndex == _data.TeamId)
                {
                    // todo: play voice, play SFX
                }
                else
                {
                    // todo: play SFX
                }
            }
            else
            {
                if (PlayerEntity.Main.TeamIndex == _data.TeamId)
                {
                    // todo: play voice, play SFX
                }
                else
                {
                    // todo: play SFX
                }
                PlayerEntity.Main.QueueHudMessage(128, 133, 90 / 30f, 1, 203); // bounty received
            }
            // todo: stop SFX, update music
            GameState.Points[_carrier.SlotIndex]++;
            GameState.OctolithScores[_carrier.SlotIndex]++;
            SetAtBase();
        }

        // todo: is_visible for base and flag
        protected override Matrix4 GetModelTransform(ModelInstance inst, int index)
        {
            Matrix4 transform = base.GetModelTransform(inst, index);
            if (index == 1)
            {
                transform.Row3.Xyz = _basePosition;
            }
            return transform;
        }

        protected override int GetModelRecolor(ModelInstance inst, int index)
        {
            if (index == 1 && _bounty)
            {
                return 0;
            }
            return base.GetModelRecolor(inst, index);
        }
    }
}
