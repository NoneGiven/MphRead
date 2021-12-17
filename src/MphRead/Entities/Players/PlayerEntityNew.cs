using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Effects;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
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

    public enum PlayerAnimation : byte
    {
        Morph = 0,
        Flourish = 1,
        WalkForward = 2,
        Unmorph = 3,
        DamageFront = 4,
        DamageBack = 5,
        DamageRight = 6,
        DamageLeft = 7,
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

    public enum KandenAltAnim : byte
    {
        Idle = 0,
        TailOut = 1,
        TailIn = 2
    }

    public partial class PlayerEntityNew : EntityBase
    {
        private Scene _scene = null!;
        private readonly ModelInstance[] _bipedModels1 = new ModelInstance[2];
        private readonly ModelInstance[] _bipedModels2 = new ModelInstance[2];
        private ModelInstance _bipedModel1 = null!;
        private ModelInstance _bipedModel2 = null!;
        private ModelInstance _altModel = null!;
        private ModelInstance _gunModel = null!;
        private ModelInstance _gunSmokeModel = null!;
        private ModelInstance _doubleDmgModel = null!;
        private ModelInstance _altIceModel = null!;
        private ModelInstance _bipedIceModel = null!;
        private ModelInstance _trailModel = null!;
        private readonly Node?[] _spineNodes = new Node?[2];
        private readonly Node?[] _shootNodes = new Node?[2];

        // todo?: could save space with a union
        private readonly Node?[] _spireAltNodes = new Node?[4];
        private Vector3 _spireField0; // pos?
        private Vector3 _spireFieldC; // prev pos?
        private Vector3 _spireField720; // up vec?
        private Vector3 _spireField72C; // facing/right vec?
        private readonly Vector3[] _spireAltVecs = new Vector3[16];
        private readonly Vector3[] _kandenSegPos = new Vector3[5];
        private byte _syluxBombCount = 0;
        private readonly BombEntity?[] _syluxBombs = new BombEntity?[3];

        // todo: these settings can change
        public static int MainPlayerIndex { get; set; } = 0;
        public static int MaxPlayers { get; set; } = 4;
        public static int PlayersCreated { get; set; } = 0;
        public static PlayerEntityNew MainPlayer => Players[MainPlayerIndex];
        public static IReadOnlyList<PlayerEntityNew> Players { get; } = new PlayerEntityNew[4]
        {
            new PlayerEntityNew(0), new PlayerEntityNew(1), new PlayerEntityNew(2), new PlayerEntityNew(3)
        };
        private bool IsMainPlayer => this == MainPlayer;

        private const int UA = 0;
        private const int Missiles = 1;

        private int _healthMax = 0;
        private int _health = 0;
        private int _healthRecovery = 0;
        private bool _tickedHealthRecovery = false; // used to update health every other frame
        private int[] _ammoMax = new int[2];
        private int[] _ammo = new int[2];
        private readonly int[] _ammoRecovery = new int[2];
        private readonly bool[] _tickedAmmoRecovery = new bool[2];
        private readonly BeamType[] _weaponSlots = new BeamType[3];
        private readonly AvailableArray _availableWeapons = new AvailableArray();
        private readonly AvailableArray _availableCharges = new AvailableArray();
        private AbilityFlags _abilities;
        private readonly BeamProjectileEntity[] _beams = SceneSetup.CreateBeamList(16); // in-game: 5
        public EquipInfo EquipInfo { get; } = new EquipInfo();
        public BeamType CurrentWeapon { get; private set; }
        public BeamType PreviousWeapon { get; private set; }
        public BeamType WeaponSelection { get; private set; }
        public readonly Effectiveness[] BeamEffectiveness = new Effectiveness[9];
        public GunAnimation GunAnimation { get; private set; }
        private byte _bombCooldown = 0;
        private byte _bombRefillTimer = 0;
        private byte _bombAmount = 0;
        private byte _bombOveruse = 0;
        private ushort _boostCharge = 0;
        private ushort _altAttackCooldown = 0;

        public int TeamIndex { get; private set; }
        public int SlotIndex { get; private set; }
        public bool IsBot { get; set; }
        public LoadFlags LoadFlags { get; set; }
        public Hunter Hunter { get; private set; }
        public PlayerValues Values { get; private set; }
        public bool IsPrimeHunter { get; set; } // todo: use game state

        private const int _mbTrailSegments = 9;
        private static readonly Matrix4[,] _mbTrailMatrices = new Matrix4[MaxPlayers, _mbTrailSegments];
        private static readonly int[,] _mbTrailAlphas = new int[MaxPlayers, _mbTrailSegments];
        private static readonly int[] _mbTrailIndices = new int[MaxPlayers];
        private Vector3 _light1Vector;
        private Vector3 _light1Color;
        private Vector3 _light2Vector;
        private Vector3 _light2Color;

        // todo: visualize
        private CollisionVolume _volumeUnxf; // todo: names
        private CollisionVolume _volume;

        private Vector3 _gunVec1;
        private Vector3 _gunVec2;
        private Vector3 _aimPosition;
        private float _gunViewBob = 0;
        private float _walkViewBob = 0;
        private Vector3 _gunDrawPos;
        private Vector3 _aimVec;

        private float _field88 = 0; // some angle?
        private short _fieldE4 = 0;
        private short _fieldE6 = 0;
        private float _fieldE8 = 0;
        private short _field2BC = 0;
        private byte _field360 = 0;
        private short _field40C = 0;
        private byte _field447 = 0;
        private ushort _field43A = 0;
        private int _field450 = 0;
        private byte _field449 = 0;
        private byte _field4AC = 0;
        private Vector3 _field4E8;
        private byte _field53E = 0;
        private byte _field551 = 0;
        private byte _field552 = 0;
        private byte _field553 = 0;

        public EnemySpawnEntity? EnemySpawner => _enemySpawner;
        public EnemyInstanceEntity? AttachedEnemy { get; set; } = null;
        private EntityBase? _field35C = null;
        private MorphCameraEntity? _camPos = null;
        private OctolithFlagEntity? _octolithFlag = null;
        private JumpPadEntity? _lastJumpPad = null;
        private EnemySpawnEntity? _enemySpawner = null;
        private EntityBase? _burnedBy = null;
        private EntityBase? _lastTarget = null;
        private PlayerEntityNew? _shockCoilTarget = null;

        public bool IsAltForm => Flags1.TestFlag(PlayerFlags1.AltForm);
        public bool IsMorphing => Flags1.TestFlag(PlayerFlags1.Morphing);
        public bool IsUnmorphing => Flags1.TestFlag(PlayerFlags1.Unmorphing);
        public PlayerFlags1 Flags1 { get; private set; }
        public PlayerFlags2 Flags2 { get; private set; }
        public Vector3 Speed { get; set; }
        public Vector3 Acceleration { get; set; }
        public Vector3 PrevSpeed { get; set; }
        public Vector3 PrevPosition { get; set; }
        public Vector3 PrevCamPos { get; set; }
        public Vector3 IdlePosition { get; private set; }
        private short _someSpeedCounter = 0;
        private float _hSpeedCap = 0;
        private float _hspeedMag = 0;

        private short _jumpPadControlLock = 0;
        private short _jumpPadControlLockMin = 0;
        private ushort _timeSinceJumpPad = 0;

        private ulong _timeSinceInput = 0;
        private ushort _timeSinceShot = 0;
        private ushort _timeSinceDamage = 0;
        private ushort _timeSincePickup = 0;
        private ushort _timeSinceHeal = 0;
        private ushort _respawnTimer = 0;
        private ushort _damageInvulnTimer = 0;
        private ushort _spawnInvulnTimer = 0;
        private ushort _viewSwayTimer = 0;
        private ushort _doubleDmgTimer = 0;
        private ushort _cloakTimer = 0;
        private ushort _deathaltTimer = 0;
        private ushort _frozenTimer = 0;
        private ushort _disruptedTimer = 0;
        private ushort _burnTimer = 0;
        private ushort _timeSinceFrozen = 0;
        private ushort _timeSinceDead = 0;
        private ushort _hidingTimer = 0;
        private ushort _timeSinceButtonTouch = 0;
        private ushort _timeStanding = 0;
        private ushort _timeSinceHitTarget = 0;
        private ushort _shockCoilTimer = 0;
        private ushort _timeSinceMorphCamera = 0;

        private EffectEntry? _deathaltEffect = null;

        private bool _frozen = false;
        private float _curAlpha = 1;
        private float _targetAlpha = 1;
        private float _smokeAlpha = 0;

        // debug/viewer
        public bool IgnoreItemPickups { get; set; }

        private PlayerEntityNew(int slotIndex) : base(EntityType.Player)
        {
            SlotIndex = slotIndex;
        }

        public static PlayerEntityNew? Create(Hunter hunter, int recolor)
        {
            if (PlayersCreated >= MaxPlayers)
            {
                return null;
            }
            PlayerEntityNew player = Players[PlayersCreated++];
            player.Hunter = hunter;
            player.Recolor = recolor;
            if (player.IsBot)
            {
                // todo: update controls
            }
            player.LoadFlags |= LoadFlags.SlotActive;
            player.LoadFlags &= ~LoadFlags.Spawned;
            return player;
        }

        public override void Initialize(Scene scene)
        {
            _scene = scene;
            _models.Clear();
            _bipedModels1[0] = Read.GetModelInstance(Metadata.HunterModels[Hunter][0]);
            _bipedModels1[1] = Read.GetModelInstance(Metadata.HunterModels[Hunter][1]);
            _bipedModels2[0] = Read.GetModelInstance(Metadata.HunterModels[Hunter][0]);
            _bipedModels2[1] = Read.GetModelInstance(Metadata.HunterModels[Hunter][1]);
            _bipedModel1 = _bipedModels1[0];
            _bipedModel2 = _bipedModels2[0];
            _altModel = Read.GetModelInstance(Metadata.HunterModels[Hunter][2]);
            _gunModel = Read.GetModelInstance(Metadata.HunterModels[Hunter][3]);
            _gunSmokeModel = Read.GetModelInstance("gunSmoke");
            _bipedIceModel = Read.GetModelInstance(Hunter == Hunter.Noxus || Hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            _altIceModel = Read.GetModelInstance("alt_ice");
            _doubleDmgModel = Read.GetModelInstance("doubleDamage_img");
            _models.Add(_bipedModels1[0]);
            _models.Add(_bipedModels1[1]);
            _models.Add(_altModel);
            _models.Add(_gunModel);
            _models.Add(_gunSmokeModel);
            _models.Add(_bipedIceModel);
            _models.Add(_altIceModel);
            _models.Add(_doubleDmgModel);
            if (Hunter == Hunter.Samus)
            {
                _trailModel = Read.GetModelInstance("trail");
            }
            base.Initialize(scene);
            // todo: respawn node ref
            // todo: update controls
            EquipInfo.Beams = _beams;
            Values = Metadata.PlayerValues[(int)Hunter];
            if (scene.Multiplayer)
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
            SetTransform(-Vector3.UnitZ, Vector3.UnitY, Vector3.Zero);
            Speed = Vector3.Zero;
            _gunVec1 = -Vector3.UnitZ;
            _gunVec2 = -Vector3.UnitX;
            _volumeUnxf = PlayerVolumes[(int)Hunter, 0];
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            _aimPosition = (Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance)).AddY(Fixed.ToFloat(Values.AimYOffset));
            _field88 = 0;
            _gunViewBob = 0;
            _walkViewBob = 0;
            _health = 0;
            Flags2 |= PlayerFlags2.HideModel;
            AttachedEnemy = null;
            _field35C = null;
            _field360 = 0;
            EquipInfo.ChargeLevel = 0;
            _timeSinceShot = 255;
            _timeSinceDamage = 255;
            _timeSincePickup = 255;
            _timeSinceHeal = 255;
            _field447 = 0;
            _field449 = 0;
            _respawnTimer = 0;
            _timeSinceDead = 0;
            _field551 = 255;
            _field552 = 0;
            _field553 = 0;
            _bombCooldown = 0;
            _bombRefillTimer = 0;
            _bombAmount = 3;
            _field53E = 1;
            _damageInvulnTimer = 0;
            _spawnInvulnTimer = 0;
            _abilities = AbilityFlags.None;
            _field43A = 0;
            _field450 = 0;
            TeamIndex = SlotIndex; // todo: use game state
            _field4E8 = Vector3.Zero;
            _viewSwayTimer = (ushort)(Values.ViewSwayTime * 2); // todo: FPS stuff
            ResetCameraInfo();
            // todo: update camera info
            _field2BC = 0;
            _timeSinceInput = 0;
            _field40C = 0;
            _doubleDmgTimer = 0;
            // todo: room and camera node refs
            _octolithFlag = null;
            ResetMorphBallTrail();
            // todo?: point module
            string shootNodeName = Hunter == Hunter.Guardian ? "Head_1" : "R_elbow";
            for (int i = 0; i < 2; i++)
            {
                _spineNodes[i] = _bipedModels1[i].Model.GetNodeByName("Spine_1");
                _shootNodes[i] = _bipedModels1[i].Model.GetNodeByName(shootNodeName);
            }
            if (Hunter == Hunter.Spire)
            {
                _spireAltNodes[0] = _altModel.Model.GetNodeByName("L_Rock01");
                _spireAltNodes[1] = _altModel.Model.GetNodeByName("R_Rock01");
                _spireAltNodes[2] = _altModel.Model.GetNodeByName("R_POS_ROT");
                _spireAltNodes[3] = _altModel.Model.GetNodeByName("R_POS_ROT_1");
            }
            else
            {
                _spireAltNodes[0] = null;
                _spireAltNodes[1] = null;
                _spireAltNodes[2] = null;
                _spireAltNodes[3] = null;
            }
            // todo: respawn and/or checkpoint or something
        }

        public void Spawn(Vector3 pos, Vector3 facing, Vector3 up, bool respawn) // todo: node ref
        {
            LoadFlags |= LoadFlags.Spawned;
            if (IsMainPlayer)
            {
                // todo: update SFX
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
                _syluxBombs[0] = null;
                _syluxBombs[1] = null;
                _syluxBombs[2] = null;
            }
            else if (Hunter == Hunter.Noxus)
            {
                _abilities |= AbilityFlags.NoxusAltAttack;
            }
            else if (Hunter == Hunter.Spire)
            {
                _abilities |= AbilityFlags.SpireAltAttack;
                _spireField0 = pos;
                _spireFieldC = pos;
                _spireField720 = Vector3.UnitY;
                _spireField72C = Vector3.UnitX;
                for (int i = 0; i < _spireAltVecs.Length; i++)
                {
                    _kandenSegPos[i] = Vector3.Zero;
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
            _frozen = false;
            _hidingTimer = 0;
            _curAlpha = 1;
            _targetAlpha = 1;
            _disruptedTimer = 0;
            _burnedBy = null;
            _burnTimer = 0;
            _timeSinceButtonTouch = 255;
            _hSpeedCap = Fixed.ToFloat(Values.WalkSpeedCap);
            Speed = Vector3.Zero;
            if (respawn)
            {
                pos = pos.AddY(1);
            }
            SetTransform(facing, up, pos);
            PrevPosition = Position;
            IdlePosition = Position;
            _gunVec2 = Vector3.Cross(up, facing).Normalized();
            _gunVec1 = facing;
            // sktodo: field_70 stuff
            _aimPosition = (Position + _gunVec1 * Fixed.ToFloat(Values.AimDistance)).AddY(Fixed.ToFloat(Values.AimYOffset));
            Acceleration = Vector3.Zero;
            _someSpeedCounter = 0;
            // todo: room node ref
            _field88 = 0;
            _fieldE4 = 0;
            _fieldE6 = 0;
            _fieldE8 = 0;
            _gunViewBob = 0;
            _walkViewBob = 0;
            // todo: update camera info and camseq stuff
            _gunDrawPos = Fixed.ToFloat(Values.FieldB8) * facing
                + _scene.CameraPosition
                + Fixed.ToFloat(Values.FieldB0) * _gunVec2
                + Fixed.ToFloat(Values.FieldB4) * up; // todo: use camera info position
            _aimVec = _aimPosition - _gunDrawPos;
            _timeSinceInput = 0;
            Flags1 = PlayerFlags1.Standing | PlayerFlags1.StandingPrevious | PlayerFlags1.CanTouchBoost;
            Flags2 = PlayerFlags2.NoShotsFired;
            _volumeUnxf = PlayerVolumes[(int)Hunter, 0];
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
            AttachedEnemy = null;
            _field35C = null;
            _field360 = 0;
            _timeSinceShot = 255;
            _timeSinceDamage = 255;
            _timeSincePickup = 255;
            _timeSinceHeal = 255;
            _field447 = 0;
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
            _bombAmount = 3;
            _field53E = 1;
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
            _timeSinceMorphCamera = UInt16.MaxValue;
            _bipedModel1 = _bipedModels1[0];
            _bipedModel2 = _bipedModels2[0];
            SetBipedAnimation(PlayerAnimation.Spawn, AnimFlags.NoLoop);
            _altModel.SetAnimation(0, AnimFlags.Paused);
            SetGunAnimation(GunAnimation.Idle, AnimFlags.NoLoop);
            _gunSmokeModel.SetAnimation(0);
            _smokeAlpha = 0;
            _camPos = null;
            _octolithFlag = null;
            ResetMorphBallTrail();
            // todo: stop SFX, play SFX, update SFX handle
            _lastJumpPad = null;
            _jumpPadControlLock = 0;
            _jumpPadControlLockMin = 0;
            // todo: update HUD effects
            // todo: update camera info vecs
            _light1Vector = _scene.Light1Vector;
            _light1Color = _scene.Light1Color;
            _light2Vector = _scene.Light2Vector;
            _light2Color = _scene.Light2Color;
            // todo: clear input
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
                int effectId = _scene.Multiplayer && _scene.PlayerCount > 2 ? 33 : 31;
                Matrix4 transform = GetTransformMatrix(Vector3.UnitX, Vector3.UnitY, Position);
                _scene.SpawnEffect(effectId, transform);
            }
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

        private void ResetCameraInfo()
        {
            // todo: this
        }

        private void ResetMorphBallTrail()
        {
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                _mbTrailAlphas[SlotIndex, i] = 0;
            }
            _mbTrailIndices[SlotIndex] = 0;
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

        private bool TryEquipWeapon(BeamType beam, bool silent = false)
        {
            int index = (int)beam;
            if (index < 0 || index >= 9)
            {
                return false;
            }
            WeaponInfo info = Weapons.Current[(int)BeamType.Missile];
            byte ammoType = info.AmmoType;
            bool hasAmmo = beam == BeamType.PowerBeam || _ammo[ammoType] >= info.AmmoCost;
            if (!silent && (!hasAmmo || !_availableWeapons[beam] || GunAnimation == GunAnimation.UpDown))
            {
                if (IsMainPlayer)
                {
                    // todo: play SFX, update HUD
                }
                return false;
            }
            BeamProjectileEntity.StopChargeSfx(CurrentWeapon, Hunter);
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
                if (IsMainPlayer && !IsAltForm)
                {
                    // todo: play SFX
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
            int slot = 0;
            if (beam == BeamType.Missile)
            {
                slot = 1;
            }
            else if (beam != BeamType.PowerBeam)
            {
                UpdateAffinityWeaponSlot(beam);
                slot = 2;
            }
            if (IsMainPlayer)
            {
                // todo: update HUD
            }
            return true;
        }

        public void UpdateZoom(bool zoom)
        {
            if (IsMainPlayer && EquipInfo.Zoomed != zoom)
            {
                // todo: play SFX, update HUD
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
            if (IsMainPlayer)
            {
                // todo: update HUD
            }
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
                    // todo: play SFX
                }
                else if (anim == GunAnimation.MissileOpen && EquipInfo.ChargeLevel == 0)
                {
                    // todo: play SFX
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
                if (_gunModel.AnimInfo.Flags[0].TestFlag(AnimFlags.Reverse))
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
                    SetGunAnimation(GunAnimation.ChargeShot, AnimFlags.NoLoop);
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
    }

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
        Bit29 = 0x20000000,
        Bit30 = 0x40000000,
        DrawGunSmoke = 0x80000000
    }

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
        Bit10 = 0x400,
        Lod1 = 0x800,
        DrawnThirdPerson = 0x1000,
        RadarReveal = 0x2000,
        RadarRevealPrevious = 0x4000,
        SpireClimbing = 0x8000,
        NoShotsFired = 0x10000,
        UnequipOmegaCannon = 0x20000
    }

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
        public readonly int Field3C;
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
        public readonly ushort ViewSwayTime;
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
        public readonly short DmgInvuln;
        public readonly ushort DmgFlashDuration;
        public readonly int FieldB0;
        public readonly int FieldB4;
        public readonly int FieldB8;
        public readonly int SmokeZOffset;
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
        public readonly ushort FieldE4;
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
        public readonly byte AltGroundedNoGrav;
        public readonly byte Padding109;
        public readonly ushort Padding10A;
        public readonly int FallDamageSpeed;
        public readonly int FallDamageMax;
        public readonly int Field114;
        public readonly int Field118;
        public readonly int JumppadSlideFactor;
        public readonly int Field120;
        public readonly int Field124;
        public readonly int Field128;
        public readonly int AltSpinSpeed;
        public readonly int Field130;
        public readonly int Field134;
        public readonly int Field138;
        public readonly int Field13C;
        public readonly int Field140;
        public readonly int Field144;
        public readonly int Field148;
        public readonly int Field14C;
        public readonly short Field150;
        public readonly ushort AltAttackStartup;
        public readonly int Field154;
        public readonly int Field158;
        public readonly int LungeHSpeed;
        public readonly int LungeVSpeed;
        public readonly ushort AltAttackDamage;
        public readonly short AltAttackCooldown;

        public PlayerValues(Hunter hunter, int walkBipedTraction, int strafeBipedTraction, int walkSpeedCap, int strafeSpeedCap,
            int altMinHSpeed, int boostSpeedCap, int bipedGravity, int altAirGravity, int altGroundGravity, int jumpSpeed, int walkSpeedFactor,
            int altGroundSpeedFactor, int strafeSpeedFactor, int airSpeedFactor, int standSpeedFactor, int field3C, int altColRadius,
            int altColYPos, ushort boostChargeMin, ushort boostChargeMax, int boostSpeedMin, int boostSpeedMax, int altHSpeedCapIncrement,
            int field58, int field5C, int walkBobMax, int aimDistance, ushort viewSwayTime, ushort padding6A, int normalFov, int field70,
            int aimYOffset, int field78, int field7C, int field80, int field84, int field88, int field8C, int field90, int minPickupHeight,
            int maxPickupHeight, int bipedColRadius, int fieldA0, int fieldA4, int fieldA8, short dmgInvuln, ushort dmgFlashDuration,
            int fieldB0, int fieldB4, int fieldB8, int smokeZOffset, int bombCooldown, int bombSelfRadius, int bombSelfRadiusSquared,
            int bombRadius, int bombRadiusSquared, int bombJumpSpeed, int bombRefillTime, short bombDamage, short bombEnemyDamage,
            short fieldE0, short spawnInvulnerability, ushort fieldE4, ushort paddingE6, int fieldE8, int fieldEC, int swayStartTime,
            int swayIncrement, int swayLimit, int gunIdleTime, short mpAmmoCap, byte ammoRecharge, byte padding103, ushort energyTank,
            short field106, byte altGroundedNoGrav, byte padding109, ushort padding10A, int fallDamageSpeed, int fallDamageMax, int field114,
            int field118, int jumppadSlideFactor, int field120, int field124, int field128, int altSpinSpeed, int field130, int field134,
            int field138, int field13C, int field140, int field144, int field148, int field14C, short field150, ushort altAttackStartup,
            int field154, int field158, int lungeHSpeed, int lungeVSpeed, ushort altAttackDamage, short altAttackCooldown)
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
            Field3C = field3C;
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
            ViewSwayTime = viewSwayTime;
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
            DmgInvuln = dmgInvuln;
            DmgFlashDuration = dmgFlashDuration;
            FieldB0 = fieldB0;
            FieldB4 = fieldB4;
            FieldB8 = fieldB8;
            SmokeZOffset = smokeZOffset;
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
            FieldE4 = fieldE4;
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
            AltGroundedNoGrav = altGroundedNoGrav;
            Padding109 = padding109;
            Padding10A = padding10A;
            FallDamageSpeed = fallDamageSpeed;
            FallDamageMax = fallDamageMax;
            Field114 = field114;
            Field118 = field118;
            JumppadSlideFactor = jumppadSlideFactor;
            Field120 = field120;
            Field124 = field124;
            Field128 = field128;
            AltSpinSpeed = altSpinSpeed;
            Field130 = field130;
            Field134 = field134;
            Field138 = field138;
            Field13C = field13C;
            Field140 = field140;
            Field144 = field144;
            Field148 = field148;
            Field14C = field14C;
            Field150 = field150;
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
