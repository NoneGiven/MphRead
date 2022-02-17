using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using MphRead.Formats;
using MphRead.Formats.Culling;
using MphRead.Hud;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    [Flags]
    public enum LoadFlags : byte
    {
        None = 0,
        Connected = 0x1,
        WasConnected = 0x2,
        Disconnected = 0x4,
        Initial = 0x8,
        Unknown4 = 0x10,
        Active = 0x20,
        SlotActive = 0x40,
        Spawned = 0x80
    }

    public class AvailableArray
    {
        private readonly bool[] _array = new bool[9];

        public void ClearAll()
        {
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = false;
            }
        }

        public void SetAll()
        {
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = true;
            }
        }

        public void CopyFrom(AvailableArray source)
        {
            for (int i = 0; i < _array.Length; i++)
            {
                _array[i] = source[i];
            }
        }

        public bool this[int key]
        {
            get => _array[key];
            set => _array[key] = value;
        }

        public bool this[BeamType key]
        {
            get => _array[(int)key];
            set => _array[(int)key] = value;
        }
    }

    public enum PlayerAnimation : sbyte
    {
        None = -1,
        Morph = 0,
        Flourish = 1,
        WalkForward = 2,
        Unmorph = 3,
        DamageBack = 4,
        DamageFront = 5,
        DamageLeft = 6,
        DamageRight = 7,
        Idle = 8,
        LandNeutral = 9,
        LandLeft = 10,
        LandRight = 11,
        JumpNeutral = 12,
        JumpBack = 13,
        JumpForward = 14,
        JumpLeft = 15,
        JumpRight = 16,
        Unused17 = 17,
        WalkBackward = 18,
        Spawn = 19,
        WalkLeft = 20,
        WalkRight = 21,
        Turn = 22,
        Charge = 23,
        ChargeShoot = 24,
        Shoot = 25
    }

    public enum GunAnimation : byte
    {
        FullCharge = 0,
        ChargeShot = 1,
        Charging = 2,
        Idle = 3,
        Switch = 4,
        FullChargeMissile = 5,
        ChargingMissile = 6,
        MissileClose = 7,
        MissileOpen = 8,
        Unknown9 = 9,
        MissileShot = 10,
        UpDown = 11,
        Shot = 12
    }

    public enum TraceAltAnim : byte
    {
        Idle = 0,
        Attack = 1,
        MoveLeft = 2,
        MoveForward = 3,
        MoveRight = 4,
        MoveBackward = 5
    }

    public enum WeavelAltAnim : byte
    {
        Idle = 0,
        Attack = 1,
        MoveLeft = 2,
        MoveForward = 3,
        MoveRight = 4,
        Turn = 5,
        MoveBackward = 6
    }

    public enum KandenAltAnim : byte
    {
        Idle = 0,
        TailOut = 1,
        TailIn = 2
    }

    public enum NoxusAltAnim : byte
    {
        Extend = 0
    }

    public enum SyluxAltAnim : byte
    {
        Idle = 0
    }

    public enum SpireAltAnim : byte
    {
        Attack = 0
    }

    public partial class PlayerEntity : DynamicLightEntityBase
    {
        private readonly ModelInstance[] _bipedModelLods = new ModelInstance[2];
        private ModelInstance _bipedModel1 = null!; // legs
        private ModelInstance _bipedModel2 = null!; // torso
        public ModelInstance BipedModel2 => _bipedModel2;
        private ModelInstance _altModel = null!;
        private ModelInstance _gunModel = null!;
        private ModelInstance _gunSmokeModel = null!;
        private ModelInstance _doubleDmgModel = null!;
        public ModelInstance DoubleDamageModel => _doubleDmgModel;
        private ModelInstance _altIceModel = null!;
        private ModelInstance _bipedIceModel = null!;
        private ModelInstance _trailModel = null!;
        private readonly Node?[] _spineNodes = new Node?[2];
        private readonly Node?[] _shootNodes = new Node?[2];
        private int _trailBindingId1 = 0;
        private int _trailBindingId2 = 0;
        private int _doubleDmgBindingId = 0;
        public int DoubleDmgBindingId => _doubleDmgBindingId;
        private readonly Matrix4[] _bipedIceTransforms = new Matrix4[19];

        // todo?: could save space with a union
        private readonly Node?[] _spireAltNodes = new Node?[4];
        private Vector3 _spireRockPosL; // positions after animation
        private Vector3 _spireRockPosR;
        private Vector3 _spireAltFacing;
        private Vector3 _spireAltUp;
        private readonly Vector3[] _spireAltVecs = new Vector3[16];
        private readonly Vector3[] _kandenSegPos = new Vector3[5];
        private readonly Matrix4[] _kandenSegMtx = new Matrix4[5];
        public IReadOnlyList<Vector3> KandenSegPos => _kandenSegPos;
        public byte SyluxBombCount { get; set; } = 0;
        public BombEntity?[] SyluxBombs { get; } = new BombEntity?[3];

        // todo: these settings can change
        public static int MainPlayerIndex { get; set; } = 0;
        public static int PlayerCount { get; set; } = 0;
        public static int MaxPlayers { get; set; } = 4;
        public static int PlayersCreated { get; set; } = 0;
        public static PlayerEntity Main => Players[MainPlayerIndex];
        public static readonly PlayerEntity[] _players = new PlayerEntity[4];
        public static IReadOnlyList<PlayerEntity> Players => _players;
        public bool IsMainPlayer => this == Main && _scene.CameraMode == CameraMode.Player;

        private const int UA = 0;
        private const int Missiles = 1;

        private int _healthMax = 0;
        private int _health = 0;
        private int _healthRecovery = 0;
        private bool _tickedHealthRecovery = false; // used to update health every other frame
        private readonly int[] _ammoMax = new int[2];
        private readonly int[] _ammo = new int[2];
        private readonly int[] _ammoRecovery = new int[2];
        private readonly bool[] _tickedAmmoRecovery = new bool[2];
        public int Health { get => _health; set => _health = value; }
        public int HealthMax => _healthMax;
        private readonly BeamType[] _weaponSlots = new BeamType[3];
        private readonly AvailableArray _availableWeapons = new AvailableArray();
        private readonly AvailableArray _availableCharges = new AvailableArray();
        private AbilityFlags _abilities;
        private readonly BeamProjectileEntity[] _beams;
        public EquipInfo EquipInfo { get; } = new EquipInfo();
        private WeaponInfo EquipWeapon => EquipInfo.Weapon;
        public BeamType CurrentWeapon { get; private set; }
        public BeamType PreviousWeapon { get; private set; }
        public BeamType WeaponSelection { get; private set; }
        public readonly Effectiveness[] BeamEffectiveness = new Effectiveness[9];
        public GunAnimation GunAnimation { get; private set; }
        private ushort _bombCooldown = 0;
        private ushort _bombRefillTimer = 0;
        private byte _bombAmmo = 0;
        private ushort _bombOveruse = 0;
        private ushort _boostCharge = 0;
        private ushort _boostDamage = 0;
        private ushort _altAttackCooldown = 0;
        private ushort _altAttackTime = 0;
        private float _altSpinSpeed = 0;

        private int _missileSfxHandle = -1;
        private float _walkSfxTimer = 0;
        private int _walkSfxIndex = 0;
        private float _burnSfxAmount = 0;
        private float _moveSfxAmount = 0;

        public Team Team { get; set; } = Team.None;
        public int TeamIndex { get; set; } = -1;
        public int SlotIndex { get; private set; }
        public bool IsBot { get; set; }
        public LoadFlags LoadFlags { get; set; }
        public Hunter Hunter { get; private set; }
        public PlayerValues Values { get; private set; }
        public bool IsPrimeHunter => SlotIndex == GameState.PrimeHunter;

        private const int _mbTrailSegments = 9 * 2;
        private static readonly Matrix4[,] _mbTrailMatrices = new Matrix4[MaxPlayers, _mbTrailSegments];
        private static readonly float[,] _mbTrailAlphas = new float[MaxPlayers, _mbTrailSegments];
        private static readonly int[] _mbTrailIndices = new int[MaxPlayers];
        private Matrix4 _modelTransform = Matrix4.Identity;

        // todo: visualize
        private CollisionVolume _volumeUnxf; // todo: names
        private CollisionVolume _volume;
        public CollisionVolume Volume => _volume;

        private Vector3 _facingVector;
        private Vector3 _upVector;
        public override Vector3 FacingVector => _facingVector;
        public override Vector3 UpVector => _upVector;
        private Vector3 _gunVec1; // facing? (aim?)
        private Vector3 _gunVec2; // right? (turn?)
        private Vector3 _aimPosition;
        private float _gunViewBob = 0;
        private float _walkViewBob = 0;
        private Vector3 _muzzlePos;
        private Vector3 _gunDrawPos;
        private Vector3 _aimVec;

        // something alt form angle related
        private float _field70 = 0;
        private float _field74 = 0;
        public float Field70 => _field70;
        public float Field74 => _field74;
        private float _field78 = 0;
        private float _field7C = 0;
        private float _field80 = 0;
        private float _field84 = 0;

        private float _aimY = 0;
        private float _field40C = 0; // view sway percentage
        private Vector3 _field410;
        private Vector3 _field41C;
        private Vector3 _field428;

        private float _buttonAimX = 0;
        private float _buttonAimY = 0;

        private ushort _timeIdle = 0;
        private byte _crushBits = 0;
        private Vector3 _field4E8; // stores gun vec 2
        private float _altTiltX = 0;
        private float _altTiltZ = 0;
        private float _altSpinRot = 0;
        private float _altWobble = 0;
        private byte _field551 = 0;
        private byte _field552 = 0;
        private byte _field553 = 0;
        private float _field684 = 0;
        private float _field688 = 0;
        private bool _field6D0 = false;
        private float _altRollFbX = 0; // set from other fields when entering alt form
        private float _altRollFbZ = 0;
        private float _altRollLrX = 0;
        private float _altRollLrZ = 0;

        private HalfturretEntity _halfturret = null!;
        public HalfturretEntity Halfturret => _halfturret;
        public EnemySpawnEntity? EnemySpawner => _enemySpawner;
        public EnemyInstanceEntity? AttachedEnemy { get; set; } = null;
        private EntityBase? _field35C = null;
        public MorphCameraEntity? MorphCamera { get; set; }
        public OctolithFlagEntity? OctolithFlag { get; set; }
        private JumpPadEntity? _lastJumpPad = null;
        private EnemySpawnEntity? _enemySpawner = null;
        private EntityBase? _burnedBy = null;
        public EntityBase? BurnedBy => _burnedBy;
        private EntityBase? _lastTarget = null;
        private EntityBase? _shockCoilTarget = null;
        public EntityBase? ShockCoilTarget => _shockCoilTarget;

        public bool IsAltForm => Flags1.TestFlag(PlayerFlags1.AltForm);
        public bool IsMorphing => Flags1.TestFlag(PlayerFlags1.Morphing);
        public bool IsUnmorphing => Flags1.TestFlag(PlayerFlags1.Unmorphing);
        public PlayerFlags1 Flags1 { get; private set; }
        public PlayerFlags2 Flags2 { get; private set; }
        public Vector3 Speed { get; set; }
        public Vector3 Acceleration { get; set; }
        public Vector3 PrevSpeed { get; set; }
        public Vector3 PrevPosition { get; set; }
        public Vector3 IdlePosition { get; private set; }
        private ushort _accelerationTimer = 0;
        private float _hSpeedCap = 0;
        private float _hSpeedMag = 0; // todo: all FPS stuff with speed
        private float _gravity = 0;
        private int _slipperiness = 0; // from stand_ter_flags
        private Terrain _standTerrain; // from stand_ter_flags
        private bool _terrainDamage = false; // from touch_ter_flags

        private PlayerAnimation Biped1Anim => (PlayerAnimation)_bipedModel1.AnimInfo.Index[0];
        private PlayerAnimation Biped2Anim => (PlayerAnimation)_bipedModel2.AnimInfo.Index[0];
        private int Biped1Frame => _bipedModel1.AnimInfo.Frame[0];
        private int Biped2Frame => _bipedModel2.AnimInfo.Frame[0];
        private int Biped1FrameCount => _bipedModel1.AnimInfo.Frame[0];
        private int Biped2FrameCount => _bipedModel2.AnimInfo.Frame[0];
        private AnimFlags Biped1Flags
        {
            get => _bipedModel1.AnimInfo.Flags[0];
            set => _bipedModel1.AnimInfo.Flags[0] = value;
        }
        private AnimFlags Biped2Flags
        {
            get => _bipedModel2.AnimInfo.Flags[0];
            set => _bipedModel2.AnimInfo.Flags[0] = value;
        }

        private ushort _jumpPadControlLock = 0;
        private ushort _jumpPadControlLockMin = 0;
        private ushort _timeSinceJumpPad = 0;
        private Vector3 _jumpPadAccel;

        public const ushort RespawnTime = 90 * 2; // todo: FPS stuff

        private ushort _autofireCooldown = 0;
        private ushort _powerBeamAutofire = 0;
        private ushort _timeSinceInput = 0;
        private ushort _timeSinceShot = 0;
        public ushort TimeSinceShot { get => _timeSinceShot; set => _timeSinceShot = value; }
        private ushort _timeSinceDamage = 0;
        private ushort _timeSincePickup = 0;
        private ushort _timeSinceHeal = 0;
        private ushort _respawnTimer = 0;
        public ushort RespawnTimer => _respawnTimer;
        private ushort _deathCountdown = 0;
        public ushort DeathCountdown => _deathCountdown;
        private ushort _damageInvulnTimer = 0;
        private ushort _spawnInvulnTimer = 0;
        private ushort _camSwitchTimer = 0;
        private ushort _doubleDmgTimer = 0;
        public bool DoubleDamage => _doubleDmgTimer > 0;
        private ushort _cloakTimer = 0;
        private ushort _deathaltTimer = 0;
        private ushort _frozenTimer = 0;
        private ushort _frozenGfxTimer = 0;
        private bool _drawIceLayer = false;
        private ushort _disruptedTimer = 0;
        private ushort _burnTimer = 0;
        private ushort _timeSinceFrozen = 0;
        private ushort _timeSinceDead = 0;
        private ushort _hidingTimer = 0;
        private ushort _timeStanding = 0;
        private ushort _timeSinceStanding = 0;
        private ushort _timeSinceGrounded = 0;
        private ushort _timeBeforeLanding = 0;
        private Vector3 _fieldC0;
        private ushort _field449 = 0;
        private float _field44C = 0; // basically landing speed/force?
        private ushort _timeSinceHitTarget = 0;
        private ushort _shockCoilTimer = 0;
        public ushort ShockCoilTimer => _shockCoilTimer;
        private ushort _timeSinceMorphCamera = 0;
        private ushort _horizColTimer = 0;

        private EffectEntry? _deathaltEffect = null;
        private EffectEntry? _doubleDmgEffect = null;
        private EffectEntry? _burnEffect = null;
        private EffectEntry? _furlEffect = null;
        private EffectEntry? _boostEffect = null;
        private EffectEntry? _muzzleEffect = null;
        private EffectEntry? _chargeEffect = null;

        private float _curAlpha = 1;
        public float CurAlpha => _curAlpha;
        private float _targetAlpha = 1;
        private float _smokeAlpha = 0;

        // debug/viewer
        public bool IgnoreItemPickups { get; set; }
        public Vector3? ForcedSpawnPos { get; set; }

        private PlayerEntity(int slotIndex, Scene scene) : base(EntityType.Player, scene)
        {
            SlotIndex = slotIndex;
            _beams = SceneSetup.CreateBeamList(16, scene); // in-game: 5
        }

        public static void Construct(Scene scene)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null)
                {
                    _players[i] = new PlayerEntity(i, scene);
                }
            }
        }

        public static void Reset()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i] = null!;
            }
            PlayerCount = 0;
            PlayersCreated = 0;
        }

        public static PlayerEntity? Create(Hunter hunter, int recolor)
        {
            if (PlayersCreated >= MaxPlayers)
            {
                return null;
            }
            PlayerEntity player = Players[PlayersCreated++];
            player.Hunter = hunter;
            player.Recolor = recolor;
            if (player.IsBot)
            {
                // todo: update controls
            }
            player.LoadFlags |= LoadFlags.SlotActive;
            player.LoadFlags &= ~LoadFlags.Spawned;
            player.CreateHalfturret();
            return player;
        }

        public void CreateHalfturret()
        {
            Debug.Assert(_halfturret == null);
            _halfturret = new HalfturretEntity(this, _scene);
            _halfturret.Create();
            _scene.InitEntity(_halfturret);
        }

        public override void Initialize()
        {
            _models.Clear();
            _bipedModelLods[0] = Read.GetModelInstance(Metadata.HunterModels[Hunter][0]);
            _bipedModelLods[1] = Read.GetModelInstance(Metadata.HunterModels[Hunter][1]);
            _bipedModel1 = Read.GetModelInstance(Metadata.HunterModels[Hunter][0]);
            _bipedModel2 = Read.GetModelInstance(Metadata.HunterModels[Hunter][0]);
            _altModel = Read.GetModelInstance(Metadata.HunterModels[Hunter][2]);
            _gunModel = Read.GetModelInstance(Metadata.HunterModels[Hunter][3]);
            _gunSmokeModel = Read.GetModelInstance("gunSmoke");
            _bipedIceModel = Read.GetModelInstance(Hunter == Hunter.Noxus || Hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            _altIceModel = Read.GetModelInstance("alt_ice");
            _doubleDmgModel = Read.GetModelInstance("doubleDamage_img");
            _models.Add(_bipedModel1);
            _models.Add(_bipedModel2);
            _models.Add(_altModel);
            _models.Add(_gunModel);
            _models.Add(_gunSmokeModel);
            _models.Add(_bipedIceModel);
            _models.Add(_altIceModel);
            _models.Add(_doubleDmgModel);
            _trailModel = Read.GetModelInstance("trail");
            Material material = _trailModel.Model.Materials[0];
            _trailBindingId1 = _scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            material = _trailModel.Model.Materials[1];
            _trailBindingId2 = _scene.BindGetTexture(_trailModel.Model, material.TextureId, material.PaletteId, 0);
            _doubleDmgBindingId = _scene.BindGetTexture(_doubleDmgModel.Model, 0, 0, 0);
            base.Initialize();
            EquipInfo.Beams = _beams;
            Values = Metadata.PlayerValues[(int)Hunter];
            _hudObjects = HudElements.HunterObjects[(int)Hunter];
            if (IsMainPlayer)
            {
                SetUpHud();
            }
            if (_scene.Multiplayer)
            {
                _healthMax = (ushort)(2 * Values.EnergyTank - 1);
                _ammoMax[UA] = _ammoMax[Missiles] = Values.MpAmmoCap;
            }
            else
            {
                // todo: get from story save
                _healthMax = 799;
                _ammoMax[UA] = 4000;
                _ammoMax[Missiles] = 950;
            }
            InitializeWeapon();
            _availableWeapons[BeamType.PowerBeam] = true;
            TryEquipWeapon(BeamType.PowerBeam, silent: true);
            _facingVector = -Vector3.UnitZ;
            _upVector = Vector3.UnitY;
            SetTransform(_facingVector, _upVector, Vector3.Zero);
            Speed = Vector3.Zero;
            _gunVec1 = -Vector3.UnitZ;
            _gunVec2 = -Vector3.UnitX;
            _volumeUnxf = PlayerVolumes[(int)Hunter, 0];
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            _aimPosition = (Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance)).AddY(Fixed.ToFloat(Values.AimYOffset));
            _aimY = 0;
            _gunViewBob = 0;
            _walkViewBob = 0;
            _health = 0;
            Flags2 |= PlayerFlags2.HideModel;
            AttachedEnemy = null;
            _field35C = null;
            EquipInfo.ChargeLevel = 0;
            _timeSinceShot = 255;
            _timeSinceDamage = 255;
            _timeSincePickup = 255;
            _timeSinceHeal = 255;
            _timeSinceStanding = 0;
            _field449 = 0;
            _respawnTimer = 0;
            _timeSinceDead = 0;
            _field551 = 255;
            _field552 = 0;
            _field553 = 0;
            _bombCooldown = 0;
            _bombRefillTimer = 0;
            _bombAmmo = 3;
            _damageInvulnTimer = 0;
            _spawnInvulnTimer = 0;
            _abilities = AbilityFlags.None;
            _walkSfxTimer = 0;
            _walkSfxIndex = 0;
            _burnSfxAmount = 0;
            if (TeamIndex == -1)
            {
                TeamIndex = SlotIndex;
            }
            _field4E8 = Vector3.Zero;
            _modelTransform = Matrix4.Identity;
            _camSwitchTimer = (ushort)(Values.CamSwitchTime * 2); // todo: FPS stuff (use floats)
            CameraInfo.Reset();
            CameraInfo.Position = Position;
            CameraInfo.UpVector = Vector3.UnitY;
            CameraInfo.Target = Position + _facingVector;
            CameraInfo.NodeRef = NodeRef.None;
            NodeRef = NodeRef.None;
            _timeIdle = 0;
            _timeSinceInput = 0;
            _field40C = 0;
            _doubleDmgTimer = 0;
            OctolithFlag = null;
            ResetMorphBallTrail();
            // todo?: point module
            string shootNodeName = Hunter == Hunter.Guardian ? "Head_1" : "R_elbow";
            for (int i = 0; i < 2; i++)
            {
                _spineNodes[i] = _bipedModelLods[i].Model.GetNodeByName("Spine_1");
                _shootNodes[i] = _bipedModelLods[i].Model.GetNodeByName(shootNodeName);
            }
            if (Hunter == Hunter.Spire)
            {
                _spireAltNodes[0] = _altModel.Model.GetNodeByName("L_Rock01");
                _spireAltNodes[1] = _altModel.Model.GetNodeByName("R_Rock01");
                _spireAltNodes[2] = _altModel.Model.GetNodeByName("R_POS_ROT"); // parent of 0
                _spireAltNodes[3] = _altModel.Model.GetNodeByName("R_POS_ROT_1"); // parent of 1
            }
            else
            {
                _spireAltNodes[0] = null;
                _spireAltNodes[1] = null;
                _spireAltNodes[2] = null;
                _spireAltNodes[3] = null;
            }
            // todo: respawn and/or checkpoint or something
            for (int i = 0; i < _bipedIceTransforms.Length; i++)
            {
                _bipedIceTransforms[i] = Matrix4.Identity;
            }
        }

        public void Spawn(Vector3 pos, Vector3 facing, Vector3 up, NodeRef nodeRef, bool respawn)
        {
            LoadFlags |= LoadFlags.Spawned;
            if (IsMainPlayer)
            {
                UpdateDoubleDamageSfx(index: 0, play: false);
                UpdateCloakSfx(index: 0, play: false);
            }
            _abilities = AbilityFlags.AltForm;
            if (Hunter == Hunter.Samus)
            {
                _abilities |= AbilityFlags.Bombs;
                _abilities |= AbilityFlags.Boost;
            }
            else if (Hunter == Hunter.Kanden)
            {
                _abilities |= AbilityFlags.Bombs;
                for (int i = 0; i < _kandenSegPos.Length; i++)
                {
                    _kandenSegPos[i] = Vector3.Zero;
                }
            }
            else if (Hunter == Hunter.Trace)
            {
                _abilities |= AbilityFlags.TraceAltAttack;
            }
            else if (Hunter == Hunter.Sylux)
            {
                _abilities |= AbilityFlags.Bombs;
                SyluxBombs[0] = null;
                SyluxBombs[1] = null;
                SyluxBombs[2] = null;
            }
            else if (Hunter == Hunter.Noxus)
            {
                _abilities |= AbilityFlags.NoxusAltAttack;
            }
            else if (Hunter == Hunter.Spire)
            {
                _abilities |= AbilityFlags.SpireAltAttack;
                _spireRockPosL = pos;
                _spireRockPosR = pos;
                _spireAltFacing = Vector3.UnitY;
                _spireAltUp = Vector3.UnitX;
                for (int i = 0; i < _spireAltVecs.Length; i++)
                {
                    _spireAltVecs[i] = Vector3.Zero;
                }
            }
            else if (Hunter == Hunter.Weavel)
            {
                _abilities |= AbilityFlags.WeavelAltAttack;
            }
            if (_scene.Multiplayer)
            {
                _health = Values.EnergyTank - 1;
            }
            else if (IsMainPlayer) // todo: MP1P
            {
                // todo: get from story save
                _health = _healthMax;
            }
            else
            {
                _health = _healthMax;
            }
            // todo?: a lot of this doesn't need to be set at all in create/init when it gets set every time you spawn anyway
            _availableWeapons.ClearAll();
            _availableCharges.ClearAll();
            InitializeWeapon();
            // todo: much of this is the same as what's done in init, so we could use a common method
            EquipInfo.ChargeLevel = 0;
            EquipInfo.SmokeLevel = 0;
            _doubleDmgTimer = 0;
            _cloakTimer = 0;
            _deathaltTimer = 0;
            PreviousWeapon = BeamType.PowerBeam;
            TryEquipWeapon(BeamType.PowerBeam, silent: true);
            Metadata.LoadEffectiveness(0x2AAAA, BeamEffectiveness);
            _frozenTimer = 0;
            _timeSinceFrozen = 255;
            _frozenGfxTimer = 0;
            _drawIceLayer = false;
            _hidingTimer = 0;
            _curAlpha = 1;
            _targetAlpha = 1;
            _disruptedTimer = 0;
            _burnedBy = null;
            _burnTimer = 0;
            _hSpeedCap = Fixed.ToFloat(Values.WalkSpeedCap); // todo: FPS stuff?
            Speed = Vector3.Zero;
            if (respawn)
            {
                pos = pos.AddY(1);
            }
            _upVector = up;
            _facingVector = facing;
            SetTransform(_facingVector, _upVector, pos);
            PrevPosition = Position;
            IdlePosition = Position;
            _gunVec2 = Vector3.Cross(up, facing).Normalized();
            _gunVec1 = facing;
            float hMag = MathF.Sqrt(facing.X * facing.X + facing.Z * facing.Z);
            _field70 = facing.X / hMag;
            _field74 = facing.Z / hMag;
            _field78 = _field74;
            _field7C = -_field70;
            _field80 = _field70;
            _field84 = _field74;
            _aimPosition = (Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance)).AddY(Fixed.ToFloat(Values.AimYOffset));
            Acceleration = Vector3.Zero;
            _accelerationTimer = 0;
            _aimY = 0;
            NodeRef = nodeRef;
            _buttonAimX = 0;
            _buttonAimY = 0;
            _gunViewBob = 0;
            _walkViewBob = 0;
            if (!_scene.Multiplayer && CameraSequence.Current != null)
            {
                _camSwitchTimer = (ushort)(Values.CamSwitchTime * 2); // todo: FPS stuff
                _field684 = 0;
                _field688 = 0;
            }
            else
            {
                if (IsMainPlayer && _scene.Multiplayer && CameraSequence.Current?.IsIntro == true)
                {
                    CameraSequence.Current.End();
                }
                CameraInfo.Reset();
                CameraInfo.Position = Position;
                CameraInfo.UpVector = Vector3.UnitY;
                CameraInfo.Target = Position + facing;
                CameraInfo.Fov = Fixed.ToFloat(Values.NormalFov) * 2;
                CameraInfo.NodeRef = NodeRef;
                SwitchCamera(CameraType.First, facing);
                _camSwitchTimer = (ushort)(Values.CamSwitchTime * 2); // todo: FPS stuff
                _field684 = 0;
                _field688 = 0;
                UpdateCameraFirst();
                CameraInfo.Update();
            }
            _gunDrawPos = Fixed.ToFloat(Values.FieldB8) * facing
                + CameraInfo.Position
                + Fixed.ToFloat(Values.FieldB0) * _gunVec2
                + Fixed.ToFloat(Values.FieldB4) * up;
            _aimVec = _aimPosition - _gunDrawPos;
            _timeSinceInput = 0;
            Flags1 = PlayerFlags1.Standing | PlayerFlags1.StandingPrevious | PlayerFlags1.CanTouchBoost;
            Flags2 = PlayerFlags2.NoShotsFired;
            _volumeUnxf = PlayerVolumes[(int)Hunter, 0];
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            AttachedEnemy = null;
            _field35C = null;
            _timeSinceShot = 255;
            _timeSinceDamage = 255;
            _timeSincePickup = 255;
            _timeSinceHeal = 255;
            _timeSinceStanding = 0;
            _timeStanding = 0;
            _field449 = 0;
            _respawnTimer = 0;
            _timeSinceDead = 0;
            _field551 = 255;
            _field552 = 0;
            _field553 = 0;
            _bombCooldown = 0;
            _bombOveruse = 0;
            _bombRefillTimer = 0;
            _bombAmmo = 3;
            _damageInvulnTimer = 0;
            if (IsBot && !_scene.Multiplayer)
            {
                _spawnInvulnTimer = 0;
            }
            else
            {
                _spawnInvulnTimer = (ushort)(Values.SpawnInvulnerability * 2); // todo: FPS stuff
            }
            _boostCharge = 0;
            _altAttackCooldown = 0;
            _field4E8 = Vector3.Zero;
            _modelTransform = Matrix4.Identity;
            _timeSinceMorphCamera = UInt16.MaxValue;
            SetBipedAnimation(PlayerAnimation.Spawn, AnimFlags.None);
            _altModel.SetAnimation(0, AnimFlags.Paused);
            SetGunAnimation(GunAnimation.Idle, AnimFlags.NoLoop);
            _gunSmokeModel.SetAnimation(0);
            _smokeAlpha = 0;
            MorphCamera = null;
            OctolithFlag = null;
            ResetMorphBallTrail();
            _soundSource.StopAllSfx();
            if (IsMainPlayer)
            {
                _soundSource.Update(Position, rangeIndex: -1);
            }
            else
            {
                int rangeIndex = 1;
                if (!_scene.Multiplayer && Hunter == Hunter.Guardian) // todo: MP1P
                {
                    rangeIndex = 21;
                }
                _soundSource.Update(Position, rangeIndex);
                // sfxtodo: if node ref is not active, set sound volume override to 0
            }
            if (respawn)
            {
                PlayHunterSfx(HunterSfx.Spawn);
            }
            _missileSfxHandle = -1;
            _lastJumpPad = null;
            _jumpPadControlLock = 0;
            _jumpPadControlLockMin = 0;
            _timeSinceJumpPad = UInt16.MaxValue; // the game doesn't do this
            if (IsMainPlayer)
            {
                // the game only does this in multiplayer, but it can't hurt either way
                ResetReticle();
                _weaponIconInst.SetIndex(0, _scene);
            }
            // todo: reset HUD effects
            _altRollFbX = CameraInfo.Field48;
            _altRollFbZ = CameraInfo.Field4C;
            _altRollLrX = CameraInfo.Field50;
            _altRollLrZ = CameraInfo.Field54;
            _light1Vector = _scene.Light1Vector;
            _light1Color = _scene.Light1Color;
            _light2Vector = _scene.Light2Vector;
            _light2Color = _scene.Light2Color;
            Controls.ClearPressed();
            if (IsBot)
            {
                // todo: bot stuff
            }
            _enemySpawner = null;
            _lastTarget = null;
            // todo: scan IDs
            if (respawn && (IsMainPlayer || _scene.Multiplayer))
            {
                // spawnEffectMP or spawnEffect
                int effectId = _scene.Multiplayer && PlayerCount > 2 ? 33 : 31;
                _scene.SpawnEffect(effectId, Vector3.UnitX, Vector3.UnitY, Position);
            }
        }

        public override void GetPosition(out Vector3 position)
        {
            position = Position.AddY(IsAltForm ? 0 : 0.5f);
        }

        public override void GetVectors(out Vector3 position, out Vector3 up, out Vector3 facing)
        {
            position = Position.AddY(IsAltForm ? 0 : 0.5f);
            up = _upVector;
            facing = _facingVector;
        }

        public override bool GetTargetable()
        {
            return _health != 0;
        }

        private void SetBiped1Animation(PlayerAnimation anim, AnimFlags animFlags)
        {
            SetBipedAnimation(anim, animFlags, setBiped1: true, setBiped2: false, setIfMorphing: false);
        }

        private void SetBiped2Animation(PlayerAnimation anim, AnimFlags animFlags)
        {
            SetBipedAnimation(anim, animFlags, setBiped1: false, setBiped2: true, setIfMorphing: false);
        }

        private void SetBipedAnimation(PlayerAnimation anim, AnimFlags animFlags, bool setBiped1 = true,
            bool setBiped2 = true, bool setIfMorphing = true)
        {
            if (setIfMorphing || !IsMorphing)
            {
                if (setBiped2 && (setIfMorphing || !IsUnmorphing))
                {
                    _bipedModel2.SetAnimation((int)anim, animFlags);
                }
                if (setBiped1)
                {
                    _bipedModel1.SetAnimation((int)anim, animFlags);
                }
            }
        }

        public void Teleport(Vector3 position, Vector3 facing, NodeRef nodeRef)
        {
            _soundSource.PlaySfx(SfxId.TELEPORT_OUT, noUpdate: true);
            _gunVec1 = facing;
            _facingVector = facing;
            SetTransform(facing, _upVector, position);
            if (nodeRef != NodeRef.None)
            {
                NodeRef = nodeRef;
                CameraInfo.NodeRef = nodeRef;
            }
            if (IsAltForm || IsMorphing || IsUnmorphing)
            {
                ResumeOwnCamera();
                CameraInfo.Update();
            }
        }

        public void BlockFormSwitch()
        {
            Flags2 |= PlayerFlags2.NoFormSwitch;
        }

        // todo: visualize
        public bool CheckHitByBomb(BombEntity bomb, bool halfturret)
        {
            if (bomb.Owner == this
                && (!bomb.Flags.TestFlag(BombFlags.Exploding) && !bomb.Flags.TestFlag(BombFlags.Exploded) || halfturret))
            {
                return false;
            }
            bool hit = false;
            Vector3 between;
            if (halfturret)
            {
                between = Halfturret.Position - bomb.Position;
            }
            else
            {
                between = Volume.SpherePosition - bomb.Position;
            }
            float distSqr = between.LengthSquared;
            float hitRadiusSqr = Fixed.ToFloat(Values.BombSelfRadiusSquared);
            if (bomb.Owner == this)
            {
                if (distSqr <= hitRadiusSqr && between.Y > -Volume.SphereRadius)
                {
                    hit = true;
                    float ySpeed = Fixed.ToFloat(Values.BombJumpSpeed);
                    if (Speed.Y < ySpeed)
                    {
                        Speed = Speed.WithY(ySpeed);
                    }
                }
            }
            else if (distSqr <= bomb.Radius * bomb.Radius)
            {
                hit = true;
                DamageFlags flags = DamageFlags.NoDmgInvuln;
                if (halfturret)
                {
                    flags |= DamageFlags.Halfturret;
                }
                TakeDamage(bomb.Damage, flags, null, bomb);
                _scene.SendMessage(Message.Impact, bomb, bomb.Owner, this, 0); // the game doesn't set anything as sender
            }
            if (hit)
            {
                float shake = (hitRadiusSqr - distSqr) / hitRadiusSqr * 0.1f;
                CameraInfo.SetShake(shake);
            }
            return hit;
        }

        public void OnHalfturretDied()
        {
            Flags2 &= ~PlayerFlags2.Halfturret;
        }

        private void ResetMorphBallTrail()
        {
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                _mbTrailAlphas[SlotIndex, i] = 0;
            }
            _mbTrailIndices[SlotIndex] = 0;
        }

        private void UpdateMorphBallTrail()
        {
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                float alpha = _mbTrailAlphas[SlotIndex, i] - 3 / 31f / 2; // todo: FPS stuff
                if (alpha < 0)
                {
                    alpha = 0;
                }
                _mbTrailAlphas[SlotIndex, i] = alpha;
            }
            if (IsAltForm)
            {
                Vector3 row0 = _modelTransform.Row0.Xyz;
                if (Vector3.Dot(Vector3.UnitY, row0) < 0.5f && _hSpeedMag >= Fixed.ToFloat(1269))
                {
                    Vector3 cross = Vector3.Cross(row0, Vector3.UnitY).Normalized();
                    var cross2 = Vector3.Cross(cross, row0);
                    int index = _mbTrailIndices[SlotIndex];
                    _mbTrailAlphas[SlotIndex, index] = 25 / 31f;
                    _mbTrailMatrices[SlotIndex, index] = new Matrix4(
                        row0.X, row0.Y, row0.Z, 0,
                        cross2.X, cross2.Y, cross2.Z, 0,
                        cross.X, cross.Y, cross.Z, 0,
                        Position.X, Position.Y, Position.Z, 1
                    );
                    _mbTrailIndices[SlotIndex] = (index + 1) % _mbTrailSegments;
                }
            }
        }

        private void InitializeWeapon()
        {
            _availableWeapons.ClearAll();
            _availableCharges.ClearAll();
            if (!_scene.Multiplayer && IsMainPlayer) // todo: MP1P
            {
                // todo: load available arrays, weapon slots, and ammo amounts from story save
                _availableWeapons.SetAll();
                _availableCharges.SetAll();
                _weaponSlots[0] = BeamType.PowerBeam;
                _weaponSlots[1] = BeamType.Missile;
                _weaponSlots[2] = BeamType.VoltDriver;
                _ammo[UA] = _ammoMax[UA];
                _ammo[Missiles] = _ammoMax[Missiles];
            }
            else if (!_scene.Multiplayer && IsBot)
            {
                BeamType affinityBeam = Weapons.GetAffinityBeam(Hunter);
                WeaponInfo affinityInfo = Weapons.Current[(int)affinityBeam];
                _availableWeapons[affinityBeam] = true;
                _availableCharges[affinityBeam] = true;
                _weaponSlots[0] = _weaponSlots[1] = BeamType.None;
                _weaponSlots[2] = affinityBeam;
                _ammo[UA] = _ammo[Missiles] = 0;
                _ammo[affinityInfo.AmmoType] = -1;
                PreviousWeapon = affinityBeam;
                TryEquipWeapon(affinityBeam, silent: true);
            }
            else
            {
                WeaponInfo missileInfo = Weapons.Current[(int)BeamType.Missile];
                _availableWeapons[BeamType.PowerBeam] = true;
                _availableWeapons[BeamType.Missile] = true;
                _availableCharges[BeamType.PowerBeam] = true;
                _availableCharges[BeamType.Missile] = true;
                _weaponSlots[0] = BeamType.PowerBeam;
                _weaponSlots[1] = BeamType.Missile;
                _weaponSlots[2] = BeamType.None;
                _ammo[UA] = _ammo[Missiles] = 0;
                _ammo[missileInfo.AmmoType] = 10 * missileInfo.AmmoCost;
            }
            if (IsMainPlayer)
            {
                // todo: set values for HUD graphics, probably
            }
        }

        private bool TryEquipWeapon(BeamType beam, bool silent = false, bool debug = false)
        {
            int index = (int)beam;
            if (index < 0 || index >= 9)
            {
                return false;
            }
            WeaponInfo info = Weapons.Current[(int)beam];
            byte ammoType = info.AmmoType;
            if (debug && Cheats.FreeWeaponSelect)
            {
                _availableWeapons[beam] = true;
                _availableCharges[beam] = true;
                _ammo[info.AmmoType] = _ammoMax[info.AmmoType];
            }
            bool hasAmmo = beam == BeamType.PowerBeam || _ammo[ammoType] >= info.AmmoCost;
            if (!silent && (!hasAmmo || !_availableWeapons[beam] || GunAnimation == GunAnimation.UpDown))
            {
                if (IsMainPlayer)
                {
                    _soundSource.PlayFreeSfx(SfxId.BEAM_SWITCH_FAIL);
                    if (!hasAmmo)
                    {
                        ShowNoAmmoMessage();
                    }
                }
                return false;
            }
            StopBeamChargeSfx(CurrentWeapon);
            UpdateZoom(false);
            PreviousWeapon = CurrentWeapon;
            CurrentWeapon = WeaponSelection = beam;
            if (beam == Weapons.GetAffinityBeam(Hunter)
                || !_scene.Multiplayer && (Hunter == Hunter.Samus && (beam == BeamType.PowerBeam || beam == BeamType.OmegaCannon)
                || Hunter == Hunter.Guardian && beam == BeamType.VoltDriver))
            {
                EquipInfo.Weapon = Weapons.Current[(int)beam + 9];
            }
            else
            {
                EquipInfo.Weapon = Weapons.Current[(int)beam];
            }
            EquipInfo.ChargeLevel = 0;
            EquipInfo.SmokeLevel = 0;
            EquipInfo.GetAmmo = () => _ammo[ammoType];
            EquipInfo.SetAmmo = (newAmmo) => _ammo[ammoType] = newAmmo;
            _timeSinceInput = 0;
            if (!silent)
            {
                if (IsMainPlayer && !IsAltForm && beam != BeamType.Missile)
                {
                    int sfx = Metadata.HunterSfx[(int)Hunter, (int)HunterSfx.BeamSwitch];
                    if (sfx != -1)
                    {
                        _soundSource.PlayFreeSfx(sfx);
                    }
                }
                if (beam == BeamType.Missile)
                {
                    SetGunAnimation(GunAnimation.MissileOpen, AnimFlags.NoLoop);
                }
                else if (PreviousWeapon == BeamType.Missile)
                {
                    SetGunAnimation(GunAnimation.MissileClose, AnimFlags.NoLoop);
                }
                else
                {
                    CurrentWeapon = WeaponSelection = PreviousWeapon;
                    SetGunAnimation(GunAnimation.Switch, AnimFlags.NoLoop);
                    CurrentWeapon = WeaponSelection = beam;
                }
            }
            if (beam != BeamType.PowerBeam && beam != BeamType.Missile)
            {
                UpdateAffinityWeaponSlot(beam);
            }
            if (IsMainPlayer)
            {
                HudOnWeaponSwitch(beam);
                // the game update the bottom screen weapon HUD objects here
            }
            return true;
        }

        private void ShowNoAmmoMessage()
        {
            string message = Strings.GetHudMessage(9); // AMMO DEPLETED!
            QueueHudMessage(128, 120, Align.Center, 256, 8, new ColorRgba(0x295F), 1, 45 / 30f, 1, message);
        }

        public void UpdateZoom(bool zoom)
        {
            if (IsMainPlayer && EquipInfo.Zoomed != zoom)
            {
                _soundSource.PlayFreeSfx(zoom ? SfxId.SNIPER_ZOOM_IN : SfxId.SNIPER_ZOOM_OUT);
                if (CurrentWeapon == BeamType.Imperialist)
                {
                    HudOnZoom(zoom);
                }
            }
            EquipInfo.Zoomed = zoom;
        }

        private void UpdateAffinityWeaponSlot(BeamType beam, int slot = 2)
        {
            Debug.Assert(slot == 2);
            if (_weaponSlots[slot] == BeamType.OmegaCannon && beam != BeamType.OmegaCannon)
            {
                _availableCharges[BeamType.OmegaCannon] = false;
                _availableWeapons[BeamType.OmegaCannon] = false;
            }
            _weaponSlots[slot] = beam;
            // the game updates the bottom screen weapon icon here
        }

        private void UnequipOmegaCannon()
        {
            if (CurrentWeapon == BeamType.OmegaCannon && _scene.Multiplayer)
            {
                _availableCharges[BeamType.OmegaCannon] = false;
                _availableWeapons[BeamType.OmegaCannon] = false;
                int priority = 0;
                BeamType nextBeam = BeamType.None;
                // find weapon to switch to (skip PB and Missiles)
                for (int i = 1; i < 9; i++)
                {
                    if (i != 2 && _availableWeapons[i])
                    {
                        WeaponInfo info = Weapons.Current[i];
                        if (info.Priority > priority && _ammo[info.AmmoType] >= info.AmmoCost)
                        {
                            priority = info.Priority;
                            nextBeam = (BeamType)i;
                        }
                    }
                }
                UpdateAffinityWeaponSlot(nextBeam);
                if (nextBeam == BeamType.None)
                {
                    TryEquipWeapon(BeamType.PowerBeam);
                }
                else
                {
                    TryEquipWeapon(nextBeam);
                }
            }
        }

        private void SetGunAnimation(GunAnimation anim, AnimFlags animFlags = AnimFlags.None)
        {
            GunAnimation = anim;
            int animId = Metadata.GunAnimationIds[(int)Hunter, (int)anim, 0];
            SetFlags setFlags = SetFlags.Texture | SetFlags.Texcoord | SetFlags.Material | SetFlags.Unused | SetFlags.Node;
            _gunModel.SetAnimation(animId, slot: 0, setFlags, animFlags);
            animId = Metadata.GunAnimationIds[(int)Hunter, (int)anim, (int)CurrentWeapon + 1];
            if (animId >= 0)
            {
                setFlags &= ~SetFlags.Node;
                if (Hunter == Hunter.Sylux)
                {
                    setFlags &= ~SetFlags.Texcoord;
                }
                _gunModel.SetAnimation(animId, slot: 1, setFlags, animFlags);
            }
            if (IsMainPlayer)
            {
                if (anim == GunAnimation.MissileClose)
                {
                    _soundSource.StopSfxByHandle(_missileSfxHandle);
                    _missileSfxHandle = -1;
                    if (!IsAltForm)
                    {
                        PlayMissileSfx(HunterSfx.MissileClose);
                    }
                }
                else if (anim == GunAnimation.MissileOpen && EquipInfo.ChargeLevel == 0)
                {
                    _soundSource.StopSfxByHandle(_missileSfxHandle);
                    if (!IsAltForm && _health > 0)
                    {
                        _missileSfxHandle = PlayMissileSfx(HunterSfx.MissileSwitch);
                    }
                }
            }
            if (anim == GunAnimation.FullChargeMissile || anim == GunAnimation.ChargingMissile || anim == GunAnimation.MissileClose
                || anim == GunAnimation.MissileOpen || anim == GunAnimation.Unknown9 || anim == GunAnimation.MissileShot)
            {
                Flags1 |= PlayerFlags1.GunOpenAnimation;
            }
            else
            {
                Flags1 &= ~PlayerFlags1.GunOpenAnimation;
            }
        }

        private void UpdateGunAnimation()
        {
            if (_timeSinceInput == 0)
            {
                if (GunAnimation == GunAnimation.UpDown && _gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
                {
                    _gunModel.AnimInfo.Flags[0] &= ~AnimFlags.Reverse;
                    _gunModel.AnimInfo.Flags[0] &= ~AnimFlags.Ended;
                }
            }
            else if (_timeSinceInput >= (ulong)Values.GunIdleTime * 2) // todo: FPS stuff
            {
                if (GunAnimation != GunAnimation.UpDown)
                {
                    if (CurrentWeapon != BeamType.Missile || GunAnimation == GunAnimation.MissileClose)
                    {
                        SetGunAnimation(GunAnimation.UpDown, AnimFlags.NoLoop | AnimFlags.Reverse);
                    }
                    else
                    {
                        SetGunAnimation(GunAnimation.MissileClose, AnimFlags.NoLoop);
                    }
                }
                return;
            }
            if (GunAnimation == GunAnimation.UpDown)
            {
                if (!_gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    return;
                }
                if (CurrentWeapon == BeamType.Missile)
                {
                    SetGunAnimation(GunAnimation.MissileOpen, AnimFlags.NoLoop);
                }
            }
            if (Flags1.TestFlag(PlayerFlags1.ShotUncharged))
            {
                if (!Flags1.TestFlag(PlayerFlags1.ShotMissile))
                {
                    SetGunAnimation(GunAnimation.Shot, AnimFlags.NoLoop);
                    return;
                }
                if (!Flags1.TestFlag(PlayerFlags1.GunOpenAnimation))
                {
                    SetGunAnimation(GunAnimation.Unknown9, AnimFlags.NoLoop);
                    return;
                }
                SetGunAnimation(GunAnimation.MissileShot, AnimFlags.NoLoop);
                return;
            }
            if (Flags1.TestFlag(PlayerFlags1.ShotCharged))
            {
                if (Flags1.TestFlag(PlayerFlags1.ShotMissile))
                {
                    SetGunAnimation(GunAnimation.MissileShot, AnimFlags.NoLoop);
                    return;
                }
                SetGunAnimation(GunAnimation.ChargeShot, AnimFlags.NoLoop);
                return;
            }
            if (EquipInfo.ChargeLevel >= EquipInfo.Weapon.MinCharge * 2) // todo: FPS stuff
            {
                if (GunAnimation == GunAnimation.Charging || GunAnimation == GunAnimation.ChargingMissile)
                {
                    int frame = (_gunModel.AnimInfo.FrameCount[0] - 1) * (EquipInfo.ChargeLevel / 2 - EquipInfo.Weapon.MinCharge)
                        / (EquipInfo.Weapon.FullCharge - EquipInfo.Weapon.MinCharge); // todo: FPS stuff
                    _gunModel.AnimInfo.Frame[0] = frame;
                }
                if (CurrentWeapon == BeamType.Missile)
                {
                    if (GunAnimation != GunAnimation.ChargingMissile && GunAnimation != GunAnimation.FullChargeMissile)
                    {
                        SetGunAnimation(GunAnimation.ChargingMissile, AnimFlags.NoLoop);
                    }
                    else if (GunAnimation != GunAnimation.FullChargeMissile && _gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                    {
                        SetGunAnimation(GunAnimation.FullChargeMissile);
                    }
                }
                else if (GunAnimation != GunAnimation.Charging && GunAnimation != GunAnimation.FullCharge)
                {
                    SetGunAnimation(GunAnimation.Charging, AnimFlags.NoLoop);
                }
                else if (GunAnimation != GunAnimation.FullCharge && _gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
                {
                    SetGunAnimation(GunAnimation.FullCharge);
                }
                return;
            }
            if ((GunAnimation == GunAnimation.Charging || GunAnimation == GunAnimation.ChargingMissile)
                && !_gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
            {
                _gunModel.AnimInfo.Flags[0] |= AnimFlags.Reverse;
                return;
            }
            if (GunAnimation == GunAnimation.FullCharge)
            {
                SetGunAnimation(GunAnimation.Idle, AnimFlags.NoLoop);
                return;
            }
            if (GunAnimation == GunAnimation.FullChargeMissile)
            {
                SetGunAnimation(GunAnimation.MissileOpen, AnimFlags.NoLoop);
                _gunModel.AnimInfo.Frame[0] = _gunModel.AnimInfo.FrameCount[0] - 1;
                return;
            }
            if (_gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Ended))
            {
                if (!Flags1.TestFlag(PlayerFlags1.GunOpenAnimation) || GunAnimation == GunAnimation.MissileClose)
                {
                    SetGunAnimation(GunAnimation.Idle, AnimFlags.NoLoop);
                }
                else if (CurrentWeapon == BeamType.Missile && WeaponSelection != BeamType.Missile)
                {
                    SetGunAnimation(GunAnimation.MissileClose, AnimFlags.NoLoop);
                }
            }
        }

        public void TakeDamage(int damage, DamageFlags flags, Vector3? direction, EntityBase? source)
        {
            TakeDamage((uint)damage, flags, direction, source);
        }

        public void TakeDamage(uint damage, DamageFlags flags, Vector3? direction, EntityBase? source)
        {
            if (_health == 0)
            {
                return;
            }
            if (IsMainPlayer && CameraSequence.Current?.BlockInput == true)
            {
                if (!flags.TestFlag(DamageFlags.Death))
                {
                    return;
                }
                CamSeqEntity.CancelCurrent();
            }
            if (_spawnInvulnTimer > 0 && !flags.TestFlag(DamageFlags.Death) && !flags.TestFlag(DamageFlags.IgnoreInvuln))
            {
                return;
            }
            // todo: if IsBot and some flag, return
            if (!flags.TestAny(DamageFlags.Death | DamageFlags.IgnoreInvuln | DamageFlags.NoDmgInvuln))
            {
                if (_damageInvulnTimer > 0)
                {
                    return;
                }
                _damageInvulnTimer = (ushort)(Values.DamageInvuln * 2); // todo: FPS stuff
            }
            PlayerEntity? attacker = null;
            bool fromHalfturret = false;
            BombEntity? bomb = null;
            BeamProjectileEntity? beam = null;
            if (source != null)
            {
                if (source.Type == EntityType.BeamProjectile)
                {
                    beam = (BeamProjectileEntity)source;
                    Effectiveness effectiveness = BeamEffectiveness[(int)beam.Beam];
                    if (effectiveness == Effectiveness.Zero)
                    {
                        return;
                    }
                    if (beam.Owner?.Type == EntityType.Player)
                    {
                        attacker = (PlayerEntity)beam.Owner;
                    }
                    else if (beam.Owner?.Type == EntityType.Halfturret)
                    {
                        attacker = ((HalfturretEntity)beam.Owner).Owner;
                        fromHalfturret = true;
                    }
                    if (damage > 0)
                    {
                        damage = (uint)(damage * Metadata.DamageMultipliers[(int)effectiveness]);
                        if (damage == 0)
                        {
                            damage = 1;
                        }
                    }
                    if (!IsMainPlayer)
                    {
                        beam.SpawnDamageEffect(effectiveness);
                    }
                }
                else if (source.Type == EntityType.Player)
                {
                    attacker = (PlayerEntity)source;
                    if (attacker._doubleDmgTimer > 0)
                    {
                        damage *= 2;
                    }
                }
                else if (source.Type == EntityType.Bomb)
                {
                    bomb = (BombEntity)source;
                    attacker = bomb.Owner;
                }
            }
            bool ignoreDamage = false;
            if (!_scene.Multiplayer && IsBot && attacker == this || GameState.Teams && !GameState.FriendlyFire
                && attacker != null && attacker != this && attacker.TeamIndex == TeamIndex)
            {
                ignoreDamage = true;
                damage = 0;
            }
            if (!ignoreDamage && flags.TestFlag(DamageFlags.Headshot) && attacker == Main) // todo: and not on wifi
            {
                int messageId = _scene.Multiplayer ? 228 : 121; // HEADSHOT!
                QueueHudMessage(128, 40, 20 / 30f, 0, messageId);
            }
            if (attacker != null && attacker != this && beam != null)
            {
                // bugfix?: since double damage was already applied above,
                // it's possible to increase efficiency with double damage hits
                GameState.BeamDamageDealt[attacker.SlotIndex] = Math.Min(
                    GameState.BeamDamageDealt[attacker.SlotIndex] + (int)damage,
                    GameState.BeamDamageMax[attacker.SlotIndex]
                );
            }
            if (damage > 0)
            {
                damage = (uint)(damage * Metadata.DamageLevels[GameState.DamageLevel]);
                if (damage == 0)
                {
                    damage = 1;
                }
            }
            if (Flags2.TestFlag(PlayerFlags2.Halfturret) && attacker != null && !ignoreDamage)
            {
                _halfturret.OnTakeDamage(attacker, damage);
            }
            if (flags.TestFlag(DamageFlags.Halfturret) && !ignoreDamage) // todo?: and either main player or not wifi
            {
                uint turretDamage;
                if (_health > Halfturret.Health)
                {
                    turretDamage = damage - damage / 2;
                }
                else
                {
                    turretDamage = damage / 2;
                }
                if (_halfturret.Health <= turretDamage)
                {
                    _halfturret.Die();
                }
                else
                {
                    _halfturret.Health -= (int)turretDamage;
                }
                damage -= turretDamage;
                // todo: if 1P bot with some AI flag, update damage value
                // else...
                if (_health <= damage)
                {
                    damage = (uint)(_health - 1);
                }
                _halfturret.TimeSinceDamage = 0;
                // todo: update bot AI field
            }
            if (IsBot)
            {
                // todo: bot stuff
            }
            bool dead = false;
            // todo?: something for wifi
            // else...
            // todo: if bot and some AI flags, set dead
            // else...
            if (_health <= damage || flags.TestFlag(DamageFlags.Death))
            {
                dead = true;
            }
            // todo?: something for wifi
            if (attacker != null)
            {
                if (attacker == Main)
                {
                    Main.UpdateOpponent(SlotIndex);
                }
                if (attacker != this)
                {
                    GameState.DamageCount[attacker.SlotIndex]++;
                    attacker._hidingTimer = 0;
                    _hidingTimer = 0;
                }
            }
            if (dead)
            {
                // todo?: the game encodes the beam in the damage flags for wifi stuff
                BeamType beamType = BeamType.Platform;
                if (beam != null)
                {
                    beamType = beam.Beam;
                }
                if (source == attacker || fromHalfturret || bomb != null)
                {
                    // halfturret, bomb, or direct hit by player (not beam)
                    // --> also if there's no source and no attacker
                    flags |= DamageFlags.FromAlt;
                }
                _scene.SendMessage(Message.Destroyed, this, null, 0, 0, delay: 1);
                if (EnemySpawner != null)
                {
                    _scene.SendMessage(Message.Destroyed, this, EnemySpawner, 0, 0);
                    Debug.Assert(EnemySpawner.Type == EntityType.EnemySpawn);
                    ItemSpawnEntity.SpawnItemDrop(EnemySpawner.Data.ItemType, Position, EnemySpawner.Data.ItemChance, _scene);
                }
                // todo: update HUD to cancel scan visor
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    _halfturret.Die();
                }
                _healthRecovery = 0;
                _ammoRecovery[0] = 0;
                _ammoRecovery[1] = 0;
                EquipInfo.ChargeLevel = 0;
                CameraInfo.Shake = 0;
                _doubleDmgTimer = 0;
                _deathaltTimer = 0;
                _cloakTimer = 0;
                Flags2 &= ~PlayerFlags2.Cloaking;
                GameState.KillStreak[SlotIndex] = 0;
                if (IsMainPlayer)
                {
                    // todo: license info
                    HudEndDisrupted();
                    if (_frozenGfxTimer > 0)
                    {
                        _drawIceLayer = false;
                    }
                }
                _frozenTimer = 0;
                _frozenGfxTimer = 0;
                _disruptedTimer = 0;
                _burnTimer = 0;
                if (_furlEffect != null)
                {
                    _scene.UnlinkEffectEntry(_furlEffect);
                    _furlEffect = null;
                }
                if (_boostEffect != null)
                {
                    _scene.UnlinkEffectEntry(_boostEffect);
                    _boostEffect = null;
                }
                if (_burnEffect != null)
                {
                    if (_burnEffect.EffectId == 188) // flamingGun
                    {
                        _scene.UnlinkEffectEntry(_burnEffect);
                    }
                    else
                    {
                        _scene.DetachEffectEntry(_burnEffect, setExpired: false);
                    }
                    _burnEffect = null;
                }
                if (_chargeEffect != null)
                {
                    _scene.UnlinkEffectEntry(_chargeEffect);
                    _chargeEffect = null;
                }
                if (_muzzleEffect != null)
                {
                    _scene.UnlinkEffectEntry(_muzzleEffect);
                    _muzzleEffect = null;
                }
                if (_doubleDmgEffect != null)
                {
                    _scene.UnlinkEffectEntry(_doubleDmgEffect);
                    _doubleDmgEffect = null;
                }
                if (_deathaltEffect != null)
                {
                    _scene.UnlinkEffectEntry(_deathaltEffect);
                    _deathaltEffect = null;
                }
                if (_health > 0)
                {
                    _soundSource.StopAllSfx(force: true);
                    if (IsMainPlayer)
                    {
                        // the game stops the unused weapon alarm SFX here
                        UpdateDoubleDamageSfx(index: 0, play: false);
                        UpdateCloakSfx(index: 0, play: false);
                        _soundSource.StopFreeSfxScripts();
                        if (_scene.Multiplayer)
                        {
                            PlayHunterSfx(HunterSfx.Death);
                        }
                        else
                        {
                            // skdebug - this should be set once the room is reloading
                            //_sfxStopTimer = 10 / 30f;
                            // mustodo?: update music or something?
                            _soundSource.PlayFreeSfx(SfxId.SAMUS_DEATH);
                        }
                    }
                    else
                    {
                        // mustodo: update hunter music
                        PlayHunterSfx(HunterSfx.Death);
                    }
                    StopBeamChargeSfx(CurrentWeapon);
                }
                // todo: if bot and some AI flags, set health
                // else...
                _health = 0;
                UpdateZoom(false);
                // the game stops the boost charge SFX here, but that SFX is empty
                _boostCharge = 0;
                GameState.Deaths[SlotIndex]++;
                if (this == Main && beamType == BeamType.OmegaCannon)
                {
                    _scene.SetFade(FadeType.FadeInWhite, 90 * (1 / 30f), overwrite: true);
                }
                Speed = Vector3.Zero;
                _respawnTimer = RespawnTime;
                _timeSinceDead = 0;
                if (!_scene.Multiplayer)
                {
                    if (IsAltForm)
                    {
                        int effectId = IsMainPlayer ? 10 : 216; // ballDeath or deathAlt
                        _scene.SpawnEffect(effectId, Vector3.UnitX, Vector3.UnitY, Position);
                    }
                    if (IsBot) // todo: and the planet's story save boss state is 2
                    {
                        // todo: determine whether to unlock doors
                    }
                    if (IsMainPlayer)
                    {
                        // todo: update story save, death countdown, lost octolith, etc.
                        CameraInfo.SetShake(0.25f);
                    }
                }
                else // multiplayer
                {
                    if (attacker != null)
                    {
                        if (IsMainPlayer)
                        {
                            // todo: update HUD to close dialogs
                            if (attacker == this)
                            {
                                QueueHudMessage(128, 70, 140, 90 / 30f, 2, 235); // YOU SELF-DESTRUCTED!
                            }
                            else
                            {
                                // todo: update license
                                string nickname = GameState.Nicknames[attacker.SlotIndex];
                                // %s's HEADSHOT KILLED YOU! / %s KILLED YOU!
                                string message = Strings.GetHudMessage(flags.TestFlag(DamageFlags.Headshot) ? 236 : 237);
                                QueueHudMessage(128, 70, 140, 90 / 30f, 2, message.Replace("%s", nickname));
                            }
                            string? killedBy = null;
                            if (flags.TestFlag(DamageFlags.Deathalt))
                            {
                                killedBy = Strings.GetHudMessage(250); // DEATHALT
                            }
                            else if (flags.TestFlag(DamageFlags.Burn))
                            {
                                killedBy = Strings.GetHudMessage(251); // MAGMAUL BURN
                            }
                            else if (fromHalfturret)
                            {
                                killedBy = _altAttackNames[(int)attacker.Hunter];
                            }
                            else if (beamType <= BeamType.OmegaCannon)
                            {
                                killedBy = _weaponNames[(int)beamType];
                            }
                            else if (source == attacker)
                            {
                                if (attacker.Hunter == Hunter.Weavel)
                                {
                                    killedBy = Strings.GetHudMessage(253); // HALFTURRET SLICE
                                }
                                else
                                {
                                    killedBy = _altAttackNames[(int)attacker.Hunter];
                                }
                            }
                            else if (bomb != null)
                            {
                                if (bomb.BombType == BombType.MorphBall)
                                {
                                    killedBy = Strings.GetHudMessage(252); // MORPH BALL BOMB
                                }
                                else if (bomb.BombType == BombType.Stinglarva)
                                {
                                    killedBy = _altAttackNames[(int)Hunter.Kanden];
                                }
                                else if (bomb.BombType == BombType.Lockjaw)
                                {
                                    killedBy = _altAttackNames[(int)Hunter.Sylux];
                                }
                            }
                            if (killedBy != null)
                            {
                                QueueHudMessage(128, 70, 140, 90 / 30f, 2, $"({killedBy})");
                            }
                        }
                        if (attacker == this)
                        {
                            GameState.Suicides[SlotIndex]++;
                            if (_scene.GameMode == GameMode.Battle || _scene.GameMode == GameMode.BattleTeams)
                            {
                                GameState.Points[SlotIndex]--;
                            }
                        }
                        else
                        {
                            if (attacker.TeamIndex == TeamIndex)
                            {
                                GameState.FriendlyKills[attacker.SlotIndex]++;
                                GameState.KillStreak[attacker.SlotIndex] = 0;
                                // todo: update license info
                                if (attacker == Main)
                                {
                                    string nickname = GameState.Nicknames[SlotIndex];
                                    string message = Strings.GetHudMessage(240); // YOU KILLED A TEAMMATE, (%s)!
                                    QueueHudMessage(128, 70, 140, 60 / 30f, 2, message.Replace("%s", nickname));
                                }
                            }
                            else
                            {
                                if (attacker == Main)
                                {
                                    // todo: update license info
                                    string nickname = GameState.Nicknames[SlotIndex];
                                    // YOUR HEADSHOT KILLED %s! / YOU KILLED %s!
                                    string message = Strings.GetHudMessage(flags.TestFlag(DamageFlags.Headshot) ? 239 : 238);
                                    QueueHudMessage(128, 70, 140, 60 / 30f, 2, message.Replace("%s", nickname));
                                }
                                if (flags.TestFlag(DamageFlags.Headshot))
                                {
                                    GameState.HeadshotKills[attacker.SlotIndex]++;
                                }
                                GameState.Kills[attacker.SlotIndex]++;
                                // todo?: the game also updates another kills stat(?) here
                                if (attacker.IsPrimeHunter)
                                {
                                    GameState.KillsAsPrime[attacker.SlotIndex]++;
                                }
                                if (beamType <= BeamType.OmegaCannon)
                                {
                                    GameState.BeamKills[attacker.SlotIndex, (int)beamType]++;
                                    // todo: update license info
                                }
                                if (GameState.KillStreak[attacker.SlotIndex] < 255)
                                {
                                    GameState.KillStreak[attacker.SlotIndex]++;
                                }
                                if (GameState.KillStreak[attacker.SlotIndex] == 5)
                                {
                                    _soundSource.QueueStream(VoiceId.VOICE_CONSECUTIVE_KILLS, delay: 1);
                                    string message;
                                    if (attacker.IsMainPlayer)
                                    {
                                        message = Strings.GetHudMessage(254); // YOU KILLED 5 IN A ROW!
                                    }
                                    else
                                    {
                                        string nickname = GameState.Nicknames[attacker.SlotIndex];
                                        message = Strings.GetHudMessage(255); // %s KILLED 5 IN A ROW!
                                        message = message.Replace("%s", nickname);
                                    }
                                    QueueHudMessage(128, 70, 140, 90 / 30f, 2, message);
                                }
                                if (_scene.GameMode == GameMode.PrimeHunter)
                                {
                                    if (attacker.IsPrimeHunter)
                                    {
                                        attacker.GainHealth(70);
                                    }
                                    else if (attacker.Health > 0 && (GameState.PrimeHunter == -1 || IsPrimeHunter))
                                    {
                                        GameState.PrimeHunter = attacker.SlotIndex;
                                        GameState.PrimesKilled[attacker.SlotIndex]++;
                                        if (Main.IsPrimeHunter)
                                        {
                                            _soundSource.QueueStream(VoiceId.VOICE_PRIME, delay: 1);
                                        }
                                        string nickname = GameState.Nicknames[attacker.SlotIndex];
                                        string message = Strings.GetHudMessage(241); // %s is the new prime hunter!
                                        QueueHudMessage(128, 70, 140, 90 / 30f, 2, message.Replace("%s", nickname));
                                    }
                                }
                                else if (_scene.GameMode == GameMode.Battle || _scene.GameMode == GameMode.BattleTeams)
                                {
                                    if (GameState.Points[attacker.SlotIndex] < 99999)
                                    {
                                        GameState.Points[attacker.SlotIndex]++;
                                    }
                                }
                                else if ((_scene.GameMode == GameMode.Capture || _scene.GameMode == GameMode.Bounty
                                    || _scene.GameMode == GameMode.BountyTeams) && OctolithFlag != null)
                                {
                                    GameState.OctolithStops[attacker.SlotIndex]++;
                                }
                                // bugfix?: this flag is also set for suicides/environmental damage/etc.
                                if (flags.TestFlag(DamageFlags.FromAlt))
                                {
                                    GameState.AltDamageCount[attacker.SlotIndex]++;
                                }
                            }
                        }
                    }
                    else // no attacker
                    {
                        GameState.Suicides[SlotIndex]++;
                        if (_scene.GameMode == GameMode.Battle || _scene.GameMode == GameMode.BattleTeams)
                        {
                            GameState.Points[SlotIndex]--;
                        }
                    }
                    if (IsAltForm || IsMorphing)
                    {
                        _scene.SpawnEffect(216, Vector3.UnitX, Vector3.UnitY, Position); // deathAlt
                    }
                    if (attacker == null || attacker == this)
                    {
                        Vector3 camFacing = CameraInfo.Position + CameraInfo.Facing;
                        SwitchCamera(CameraType.Free, camFacing);
                    }
                    else
                    {
                        SwitchCamera(CameraType.Free, attacker.Position);
                    }
                    if (IsPrimeHunter)
                    {
                        GameState.PrimeHunter = -1;
                        QueueHudMessage(128, 70, 140, 90 / 30f, 2, 242); // the prime hunter is dead!
                    }
                }
                if (_scene.Multiplayer && attacker != null && attacker != this)
                {
                    ItemType itemType = ItemType.UASmall;
                    if (attacker.EquipInfo.Weapon.AmmoType == 1)
                    {
                        itemType = ItemType.MissileSmall;
                    }
                    Vector3 position = _volume.SpherePosition.AddY(0.35f);
                    ItemSpawnEntity.SpawnItem(itemType, position, 300 * 2, _scene); // todo: FPS stuff
                }
                WeaponSelection = CurrentWeapon;
                Flags1 &= ~PlayerFlags1.WeaponMenuOpen;
            }
            else // not dead
            {
                bool skipSfx = false;
                _health -= (int)damage; // todo?: if wifi, only do this if main player
                if (beam != null && !ignoreDamage)
                {
                    if (beam.Afflictions.TestFlag(Affliction.Freeze))
                    {
                        if (flags.TestFlag(DamageFlags.Halfturret))
                        {
                            _soundSource.PlaySfx(SfxId.SHOTGUN_FREEZE);
                            _halfturret.OnFrozen();
                        }
                        else // todo?: if wifi, only do this if main player
                        {
                            _soundSource.PlaySfx(SfxId.SHOTGUN_FREEZE);
                            if (IsMainPlayer)
                            {
                                _drawIceLayer = true;
                            }
                            if (_frozenTimer == 0)
                            {
                                if (_timeSinceFrozen > 60 * 2) // todo: FPS stuff
                                {
                                    int time = (_scene.Multiplayer || attacker != null ? 75 : 30) * 2; // todo: FPS stuff
                                    _frozenTimer = (ushort)time;
                                }
                                else if (_frozenTimer < 15 * 2) // todo: FPS stuff
                                {
                                    _frozenTimer = 15 * 2; // todo: FPS stuff
                                }
                                _frozenGfxTimer = (ushort)(_frozenTimer + 5 * 2); // todo: FPS stuff
                            }
                            EndAltAttack();
                        }
                    }
                    if (beam.Afflictions.TestFlag(Affliction.Disrupt) && !flags.TestFlag(DamageFlags.Halfturret))
                    {
                        _disruptedTimer = 60 * 2; // todo: FPS stuff
                        if (IsMainPlayer)
                        {
                            skipSfx = true;
                            HudOnDisrupted();
                            _soundSource.PlaySfx(SfxId.LOB_DISRUPT);
                        }
                    }
                    if (beam.Afflictions.TestFlag(Affliction.Burn))
                    {
                        if (flags.TestFlag(DamageFlags.Halfturret))
                        {
                            _halfturret.OnSetOnFire();
                        }
                        else // todo?: if wifi, only do this if main player
                        {
                            // todo: if the attacker is a bot with some encounter state, use 75 * 2 frames
                            ushort time = 150 * 2; // todo: FPS stuff
                            _burnedBy = beam.Owner;
                            _burnTimer = time;
                            CreateBurnEffect();
                        }
                    }
                }
                if (!skipSfx && !flags.TestFlag(DamageFlags.NoSfx))
                {
                    PlayHunterSfx(HunterSfx.Damage);
                }
                if (IsMainPlayer && !IsAltForm)
                {
                    PlayRandomDamageSfx();
                }
            }
            _timeSinceDamage = 0;
            if (_health > 0 && _frozenTimer == 0)
            {
                Vector3? hitDirection = null;
                if (direction.HasValue)
                {
                    if (!IsAltForm)
                    {
                        if (!flags.TestFlag(DamageFlags.Halfturret))
                        {
                            // todo: FPS stuff?
                            Vector3 speed = Speed + direction.Value.WithY(0);
                            if (direction.Value.Y <= 0)
                            {
                                speed.Y += direction.Value.Y;
                            }
                            else if (speed.Y < 0.25f)
                            {
                                speed.Y += direction.Value.Y;
                                if (speed.Y > 0.25f)
                                {
                                    speed.Y = 0.25f;
                                }
                            }
                            Speed = speed;
                        }
                        if (direction.Value != Vector3.Zero)
                        {
                            hitDirection = direction.Value;
                        }
                        else if (beam != null)
                        {
                            hitDirection = beam.Velocity;
                        }
                    }
                    else if (!flags.TestFlag(DamageFlags.Halfturret))
                    {
                        Speed += (direction.Value * 0.4f).WithY(0); // todo: FPS stuff?
                    }
                }
                else if (attacker != null)
                {
                    hitDirection = Position - attacker.Position;
                }
                if (hitDirection.HasValue && !IsAltForm)
                {
                    // todo: clean this up
                    float hitZ = hitDirection.Value.Z;
                    float hitX = -hitDirection.Value.X;
                    float v126 = -hitZ * _field74;
                    float dirHorizontal = -hitZ * _gunVec2.Z;
                    float dirLeftRight = hitX * _gunVec2.X + dirHorizontal;
                    if (dirLeftRight < 0)
                    {
                        dirHorizontal = -dirLeftRight;
                    }
                    float v125 = hitX * _field70;
                    float dirUpDown = v125 + v126;
                    if (dirLeftRight >= 0)
                    {
                        dirHorizontal = dirLeftRight;
                    }
                    float dirVertical;
                    if (dirUpDown >= 0)
                    {
                        dirVertical = v125 + v126;
                    }
                    else
                    {
                        dirVertical = -dirUpDown;
                    }
                    // sktodo: diagonals (behind feature switch)
                    PlayerAnimation anim = PlayerAnimation.None;
                    if (dirVertical <= dirHorizontal)
                    {
                        if (dirLeftRight <= 0)
                        {
                            anim = PlayerAnimation.DamageRight;
                            if (IsMainPlayer)
                            {
                                _damageIndicatorTimers[2] = 63 * 2; // todo: FPS stuff
                            }
                        }
                        else
                        {
                            anim = PlayerAnimation.DamageLeft;
                            if (IsMainPlayer)
                            {
                                _damageIndicatorTimers[6] = 63 * 2; // todo: FPS stuff
                            }
                        }
                    }
                    else if (dirUpDown <= 0)
                    {
                        anim = PlayerAnimation.DamageBack;
                        if (IsMainPlayer)
                        {
                            _damageIndicatorTimers[4] = 63 * 2; // todo: FPS stuff
                        }
                    }
                    else
                    {
                        anim = PlayerAnimation.DamageFront;
                        if (IsMainPlayer)
                        {
                            _damageIndicatorTimers[0] = 63 * 2; // todo: FPS stuff
                        }
                    }
                    if (anim != PlayerAnimation.None)
                    {
                        SetBipedAnimation(anim, AnimFlags.NoLoop, setBiped1: false, setBiped2: true, setIfMorphing: false);
                    }
                }
            }
            if (_health > 0 && !IsAltForm)
            {
                float shake = 0.03f;
                if (!flags.TestFlag(DamageFlags.Burn))
                {
                    shake = Math.Max(damage * 0.01f, 0.05f);
                }
                CameraInfo.SetShake(shake);
            }
            if (IsMainPlayer)
            {
                // todo: do something (HUD or camera-related?)
            }
        }

        private static readonly string[] _altAttackNames = new string[8];
        private static readonly string[] _hunterNames = new string[8];
        private static readonly string[] _weaponNames = new string[9];

        public static void LoadWeaponNames()
        {
            // todo: use language
            for (int i = 0; i < 9; ++i)
            {
                _weaponNames[i] = Strings.GetMessage('W', i + 1, StringTables.WeaponNames);
            }
            for (int i = 0; i < 8; ++i)
            {
                _hunterNames[i] = Strings.GetMessage('H', i + 1, StringTables.WeaponNames);
            }
            // todo: Guardian alt form
            for (int i = 0; i < 7; ++i)
            {
                _altAttackNames[i] = Strings.GetMessage('A', i + 1, StringTables.WeaponNames);
            }
        }

        public static readonly CollisionVolume[,] PlayerVolumes = new CollisionVolume[8, 3];

        public static void GeneratePlayerVolumes()
        {
            for (int i = 0; i < 8; i++)
            {
                PlayerValues values = Metadata.PlayerValues[i];
                // placed so the bottom of the sphere coincides with the min pickup height (0.5f below y pos)
                float radius = Fixed.ToFloat(values.BipedColRadius);
                var center = new Vector3(0, Fixed.ToFloat(values.MinPickupHeight) + radius, 0);
                PlayerVolumes[i, 0] = new CollisionVolume(center, radius);
                // placed so the top of the sphere coincides with the max pickup height (1.1f above y pos)
                center = new Vector3(0, Fixed.ToFloat(values.MaxPickupHeight) - radius, 0);
                PlayerVolumes[i, 1] = new CollisionVolume(center, radius);
                // placed so the bottom of the sphere coincides with the ground level in alt form
                radius = Fixed.ToFloat(values.AltColRadius);
                center = new Vector3(0, Fixed.ToFloat(values.AltColYPos), 0);
                PlayerVolumes[i, 2] = new CollisionVolume(center, radius);
            }
        }

        public static readonly float[] KandenAltNodeDistances = new float[4];

        public static void GenerateKandenAltNodeDistances()
        {
            if (KandenAltNodeDistances[0] == 0 && KandenAltNodeDistances[1] == 0
                && KandenAltNodeDistances[2] == 0 && KandenAltNodeDistances[3] == 0)
            {
                Model model = Read.GetModelInstance("KandenAlt_lod0").Model;
                model.ComputeNodeMatrices(0);
                IReadOnlyList<Node> nodes = model.Nodes;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 pos1 = nodes[i].Transform.Row3.Xyz;
                    Vector3 pos2 = nodes[i + 1].Transform.Row3.Xyz;
                    KandenAltNodeDistances[i] = Vector3.Distance(pos1, pos2);
                }
            }
        }
    }

    [Flags]
    public enum DamageFlags : int
    {
        None = 0,
        NoDmgInvuln = 1,
        IgnoreInvuln = 2,
        Death = 4,
        Halfturret = 8,
        Headshot = 0x10,
        Deathalt = 0x20,
        Burn = 0x40,
        NoSfx = 0x80,
        FromAlt = 0x100
    }

    [Flags]
    public enum PlayerFlags1 : uint
    {
        None = 0,
        Standing = 1,
        StandingPrevious = 2,
        NoUnmorph = 4,
        NoUnmorphPrevious = 8,
        CollidingLateral = 0x10,
        OnAcid = 0x20,
        OnLava = 0x40,
        CollidingEntity = 0x80,
        UsedJump = 0x100,
        AltForm = 0x200,
        AltFormPrevious = 0x400,
        Morphing = 0x800,
        Unmorphing = 0x1000,
        Strafing = 0x2000,
        FreeLook = 0x4000,
        FreeLookPrevious = 0x8000,
        ShotUncharged = 0x10000,
        ShotMissile = 0x20000,
        ShotCharged = 0x40000,
        GunOpenAnimation = 0x80000,
        Grounded = 0x100000,
        GroundedPrevious = 0x200000,
        Walking = 0x400000,
        MovingBiped = 0x800000,
        NoAimInput = 0x1000000,
        WeaponMenuOpen = 0x2000000,
        Boosting = 0x4000000,
        CanTouchBoost = 0x8000000,
        UsedJumpPad = 0x10000000,
        AltDirOverride = 0x20000000,
        Bit30 = 0x40000000,
        DrawGunSmoke = 0x80000000
    }

    [Flags]
    public enum PlayerFlags2 : uint
    {
        None = 0,
        ChargeEffect = 1,
        HideModel = 2,
        Shooting = 4,
        AltAttack = 8,
        BipedStuck = 0x10,
        Halfturret = 0x20,
        Cloaking = 0x40,
        GravityOverride = 0x80,
        NoFormSwitch = 0x100,
        BipedLock = 0x200,
        AltFormGravity = 0x400,
        Lod1 = 0x800,
        DrawnThirdPerson = 0x1000,
        RadarReveal = 0x2000,
        RadarRevealPrevious = 0x4000,
        SpireClimbing = 0x8000,
        NoShotsFired = 0x10000,
        UnequipOmegaCannon = 0x20000
    }

    [Flags]
    public enum AbilityFlags : short
    {
        None = 0,
        AltForm = 1,
        SpaceJump = 2,
        Bombs = 4,
        Boost = 0x40,
        NoxusAltAttack = 0x100,
        SpireAltAttack = 0x200,
        TraceAltAttack = 0x400,
        WeavelAltAttack = 0x1000
    }

    [Flags]
    public enum CrushFlags : byte
    {
        None = 0,
        Bit0 = 1,
        Bit1 = 2,
        Bit2 = 4,
        Bit3 = 8,
        Bit4 = 0x10,
        Bit5 = 0x20
    }

    public readonly struct PlayerValues
    {
        public readonly Hunter Hunter; // the game doesn't have this
        public readonly int WalkBipedTraction;
        public readonly int StrafeBipedTraction;
        public readonly int WalkSpeedCap;
        public readonly int StrafeSpeedCap;
        public readonly int AltMinHSpeed;
        public readonly int BoostSpeedCap;
        public readonly int BipedGravity;
        public readonly int AltAirGravity;
        public readonly int AltGroundGravity;
        public readonly int JumpSpeed;
        public readonly int WalkSpeedFactor;
        public readonly int AltGroundSpeedFactor;
        public readonly int StrafeSpeedFactor;
        public readonly int AirSpeedFactor;
        public readonly int StandSpeedFactor;
        public readonly int RollAltTraction;
        public readonly int AltColRadius;
        public readonly int AltColYPos;
        public readonly ushort BoostChargeMin;
        public readonly ushort BoostChargeMax;
        public readonly int BoostSpeedMin;
        public readonly int BoostSpeedMax;
        public readonly int AltHSpeedCapIncrement;
        public readonly int Field58;
        public readonly int Field5C;
        public readonly int WalkBobMax;
        public readonly int AimDistance;
        public readonly ushort CamSwitchTime;
        public readonly ushort Padding6A;
        public readonly int NormalFov;
        public readonly int Field70;
        public readonly int AimYOffset;
        public readonly int Field78;
        public readonly int Field7C;
        public readonly int Field80;
        public readonly int Field84;
        public readonly int Field88;
        public readonly int Field8C;
        public readonly int Field90;
        public readonly int MinPickupHeight;
        public readonly int MaxPickupHeight;
        public readonly int BipedColRadius;
        public readonly int FieldA0;
        public readonly int FieldA4;
        public readonly int FieldA8;
        public readonly short DamageInvuln;
        public readonly ushort DamageFlashTime;
        public readonly int FieldB0;
        public readonly int FieldB4;
        public readonly int FieldB8;
        public readonly int MuzzleOffset;
        public readonly int BombCooldown;
        public readonly int BombSelfRadius;
        public readonly int BombSelfRadiusSquared;
        public readonly int BombRadius;
        public readonly int BombRadiusSquared;
        public readonly int BombJumpSpeed;
        public readonly int BombRefillTime;
        public readonly short BombDamage;
        public readonly short BombEnemyDamage;
        public readonly short FieldE0;
        public readonly short SpawnInvulnerability;
        public readonly ushort AimMinTouchTime;
        public readonly ushort PaddingE6;
        public readonly int FieldE8;
        public readonly int FieldEC;
        public readonly int SwayStartTime;
        public readonly int SwayIncrement;
        public readonly int SwayLimit;
        public readonly int GunIdleTime;
        public readonly short MpAmmoCap;
        public readonly byte AmmoRecharge;
        public readonly byte Padding103;
        public readonly ushort EnergyTank;
        public readonly short Field106;
        public readonly byte AltFormStrafe;
        public readonly byte Padding109;
        public readonly ushort Padding10A;
        public readonly int FallDamageSpeed;
        public readonly int FallDamageMax;
        public readonly int Field114;
        public readonly int Field118;
        public readonly int JumpPadSlideFactor;
        public readonly int AltTiltAngleCap;
        public readonly int AltMinWobble;
        public readonly int AltMaxWobble;
        public readonly int AltMinSpinAccel;
        public readonly int AltMaxSpinAccel;
        public readonly int AltMinSpinSpeed;
        public readonly int AltMaxSpinSpeed;
        public readonly int AltTiltAngleMax;
        public readonly int AltBounceWobble;
        public readonly int AltBounceTilt;
        public readonly int AltBounceSpin;
        public readonly int AltAttackKnockbackAccel;
        public readonly short AltAttackKnockbackTime;
        public readonly ushort AltAttackStartup;
        public readonly int Field154;
        public readonly int Field158;
        public readonly int LungeHSpeed;
        public readonly int LungeVSpeed;
        public readonly ushort AltAttackDamage;
        public readonly short AltAttackCooldown;

        public PlayerValues(Hunter hunter, int walkBipedTraction, int strafeBipedTraction, int walkSpeedCap, int strafeSpeedCap,
            int altMinHSpeed, int boostSpeedCap, int bipedGravity, int altAirGravity, int altGroundGravity, int jumpSpeed, int walkSpeedFactor,
            int altGroundSpeedFactor, int strafeSpeedFactor, int airSpeedFactor, int standSpeedFactor, int rollAltTraction, int altColRadius,
            int altColYPos, ushort boostChargeMin, ushort boostChargeMax, int boostSpeedMin, int boostSpeedMax, int altHSpeedCapIncrement,
            int field58, int field5C, int walkBobMax, int aimDistance, ushort camSwitchTime, ushort padding6A, int normalFov, int field70,
            int aimYOffset, int field78, int field7C, int field80, int field84, int field88, int field8C, int field90, int minPickupHeight,
            int maxPickupHeight, int bipedColRadius, int fieldA0, int fieldA4, int fieldA8, short damageInvuln, ushort damageFlashTime,
            int fieldB0, int fieldB4, int fieldB8, int muzzleOffset, int bombCooldown, int bombSelfRadius, int bombSelfRadiusSquared,
            int bombRadius, int bombRadiusSquared, int bombJumpSpeed, int bombRefillTime, short bombDamage, short bombEnemyDamage,
            short fieldE0, short spawnInvulnerability, ushort aimMinTouchTime, ushort paddingE6, int fieldE8, int fieldEC, int swayStartTime,
            int swayIncrement, int swayLimit, int gunIdleTime, short mpAmmoCap, byte ammoRecharge, byte padding103, ushort energyTank,
            short field106, byte altFormStrafe, byte padding109, ushort padding10A, int fallDamageSpeed, int fallDamageMax, int field114,
            int field118, int jumpPadSlideFactor, int altTiltAngleCap, int altMinWobble, int altMaxWobble, int altMinSpinAccel, int altMaxSpinAccel,
            int altMinSpinSpeed, int altMaxSpinSpeed, int altTiltAngleMax, int altBounceWobble, int altBounceTilt, int altBounceSpin,
            int altAttackKnockbackAccel, short altAttackKnockbackTime, ushort altAttackStartup, int field154, int field158, int lungeHSpeed,
            int lungeVSpeed, ushort altAttackDamage, short altAttackCooldown)
        {
            Hunter = hunter;
            WalkBipedTraction = walkBipedTraction;
            StrafeBipedTraction = strafeBipedTraction;
            WalkSpeedCap = walkSpeedCap;
            StrafeSpeedCap = strafeSpeedCap;
            AltMinHSpeed = altMinHSpeed;
            BoostSpeedCap = boostSpeedCap;
            BipedGravity = bipedGravity;
            AltAirGravity = altAirGravity;
            AltGroundGravity = altGroundGravity;
            JumpSpeed = jumpSpeed;
            WalkSpeedFactor = walkSpeedFactor;
            AltGroundSpeedFactor = altGroundSpeedFactor;
            StrafeSpeedFactor = strafeSpeedFactor;
            AirSpeedFactor = airSpeedFactor;
            StandSpeedFactor = standSpeedFactor;
            RollAltTraction = rollAltTraction;
            AltColRadius = altColRadius;
            AltColYPos = altColYPos;
            BoostChargeMin = boostChargeMin;
            BoostChargeMax = boostChargeMax;
            BoostSpeedMin = boostSpeedMin;
            BoostSpeedMax = boostSpeedMax;
            AltHSpeedCapIncrement = altHSpeedCapIncrement;
            Field58 = field58;
            Field5C = field5C;
            WalkBobMax = walkBobMax;
            AimDistance = aimDistance;
            CamSwitchTime = camSwitchTime;
            Padding6A = padding6A;
            NormalFov = normalFov;
            Field70 = field70;
            AimYOffset = aimYOffset;
            Field78 = field78;
            Field7C = field7C;
            Field80 = field80;
            Field84 = field84;
            Field88 = field88;
            Field8C = field8C;
            Field90 = field90;
            MinPickupHeight = minPickupHeight;
            MaxPickupHeight = maxPickupHeight;
            BipedColRadius = bipedColRadius;
            FieldA0 = fieldA0;
            FieldA4 = fieldA4;
            FieldA8 = fieldA8;
            DamageInvuln = damageInvuln;
            DamageFlashTime = damageFlashTime;
            FieldB0 = fieldB0;
            FieldB4 = fieldB4;
            FieldB8 = fieldB8;
            MuzzleOffset = muzzleOffset;
            BombCooldown = bombCooldown;
            BombSelfRadius = bombSelfRadius;
            BombSelfRadiusSquared = bombSelfRadiusSquared;
            BombRadius = bombRadius;
            BombRadiusSquared = bombRadiusSquared;
            BombJumpSpeed = bombJumpSpeed;
            BombRefillTime = bombRefillTime;
            BombDamage = bombDamage;
            BombEnemyDamage = bombEnemyDamage;
            FieldE0 = fieldE0;
            SpawnInvulnerability = spawnInvulnerability;
            AimMinTouchTime = aimMinTouchTime;
            PaddingE6 = paddingE6;
            FieldE8 = fieldE8;
            FieldEC = fieldEC;
            SwayStartTime = swayStartTime;
            SwayIncrement = swayIncrement;
            SwayLimit = swayLimit;
            GunIdleTime = gunIdleTime;
            MpAmmoCap = mpAmmoCap;
            AmmoRecharge = ammoRecharge;
            Padding103 = padding103;
            EnergyTank = energyTank;
            Field106 = field106;
            AltFormStrafe = altFormStrafe;
            Padding109 = padding109;
            Padding10A = padding10A;
            FallDamageSpeed = fallDamageSpeed;
            FallDamageMax = fallDamageMax;
            Field114 = field114;
            Field118 = field118;
            JumpPadSlideFactor = jumpPadSlideFactor;
            AltTiltAngleCap = altTiltAngleCap;
            AltMinWobble = altMinWobble;
            AltMaxWobble = altMaxWobble;
            AltMinSpinAccel = altMinSpinAccel;
            AltMaxSpinAccel = altMaxSpinAccel;
            AltMinSpinSpeed = altMinSpinSpeed;
            AltMaxSpinSpeed = altMaxSpinSpeed;
            AltTiltAngleMax = altTiltAngleMax;
            AltBounceWobble = altBounceWobble;
            AltBounceTilt = altBounceTilt;
            AltBounceSpin = altBounceSpin;
            AltAttackKnockbackAccel = altAttackKnockbackAccel;
            AltAttackKnockbackTime = altAttackKnockbackTime;
            AltAttackStartup = altAttackStartup;
            Field154 = field154;
            Field158 = field158;
            LungeHSpeed = lungeHSpeed;
            LungeVSpeed = lungeVSpeed;
            AltAttackDamage = altAttackDamage;
            AltAttackCooldown = altAttackCooldown;
        }
    }
}
