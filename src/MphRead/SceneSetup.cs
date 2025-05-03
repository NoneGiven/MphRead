using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Formats.Collision;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead
{
    public static class SceneSetup
    {
        // todo: artifact flags
        public static (RoomEntity, RoomMetadata, CollisionInstance, IReadOnlyList<EntityBase>)
            LoadGame(string name, Scene scene, int playerCount = 0, BossFlags bossFlags = BossFlags.Unspecified,
            int nodeLayerMask = 0, int entityLayerId = -1)
        {
            (RoomMetadata? metadata, int roomId) = Metadata.GetRoomByName(name);
            scene.AreaId = Metadata.GetAreaInfo(roomId);
            if (metadata == null)
            {
                throw new ProgramException("No room with this name is known.");
            }
            GameMode mode = scene.GameMode;
            if (mode == GameMode.None)
            {
                mode = metadata.Multiplayer ? GameMode.Battle : GameMode.SinglePlayer;
                if (mode == GameMode.Battle && metadata.Name == "AD1 TRANSFER LOCK BT")
                {
                    mode = GameMode.Bounty;
                }
                Weapons.Current = metadata.Multiplayer ? Weapons.WeaponsMP : Weapons.Weapons1P;
            }
            else
            {
                Weapons.Current = scene.Multiplayer ? Weapons.WeaponsMP : Weapons.Weapons1P;
            }
            scene.GameMode = mode;
            if (mode == GameMode.SinglePlayer)
            {
                Menu.ApplyAdventureSettings();
            }
            Extract.LoadRuntimeData();
            LoadResources(scene);
            // currently no differentiation between loading a file and choosing a planet,
            // so reset the RNG when loading a different save slot from the previous
            if (Menu.SaveSlot != Menu.PreviousSaveSlot)
            {
                Rng.SetRng1(Rng.Rng1StartValue);
                Rng.SetRng2(Rng.Rng2StartValue);
                Menu.PreviousSaveSlot = Menu.SaveSlot;
            }
            CamSeqEntity.ClearData();
            CamSeqEntity.Current = null;
            CameraSequence.Current = null;
            CameraSequence.Intro = null;
            if (scene.Multiplayer && PlayerEntity.PlayerCount > 0)
            {
                int seqId = roomId - 93 + 172;
                if (seqId >= 172 && seqId < 199)
                {
                    CameraSequence.Intro = CameraSequence.Load(seqId, scene);
                }
            }
            Sound.Sfx.Load(scene);
            var room = new RoomEntity(scene);
            (CollisionInstance collision, IReadOnlyList<EntityBase> entities) = SetUpRoom(mode, playerCount,
                bossFlags, nodeLayerMask, entityLayerId, metadata, room, scene, isRoomTransition: false);
            UpdateAreaHunters();
            InitHunterSpawns(scene, entities, initialize: false); // see: "probably revisit this"
            GameState.StorySave.CheckpointRoomId = room.RoomId;
            return (room, metadata, collision, entities);
        }

        // this is only for the no repeat encounters feature
        private static readonly bool[] _completedRandomEncounterRooms = new bool[66];

        public static void CompleteEncounter(int roomId)
        {
            if (roomId >= 27 && roomId <= 92)
            {
                _completedRandomEncounterRooms[roomId - 27] = true;
            }
        }

        public static void UpdateAreaHunters(StorySave? save = null)
        {
            // temporary parameter(?) so the menu can call this in advance on a not-yet-loaded save
            if (save == null)
            {
                save = GameState.StorySave;
                Array.Fill(_completedRandomEncounterRooms, false);
            }
            // todo?: the game does this in the cockpit
            Array.Fill(save.AreaHunters, (byte)0);
            byte chance = 0;
            byte[] chances = new byte[4];
            byte[] counts = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                int area1 = i * 2;
                if (GameState.GetAreaState(area1, save) == AreaState.Clear)
                {
                    uint lostOctoliths = save.LostOctoliths;
                    if (((lostOctoliths >> (8 * i)) & 15) == 15 || ((lostOctoliths >> (4 * (2 * i + 1))) & 15) == 15)
                    {
                        // increased chance if you haven't lost either of the planet's octoliths
                        chance += 2;
                    }
                    else
                    {
                        chance++;
                    }
                    chances[i] = chance;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                if ((save.DefeatedHunters & (1 << i)) == 0)
                {
                    continue;
                }
                uint rand = Rng.GetRandomInt2(chance);
                for (int j = 0; j < 4; j++)
                {
                    if (rand < chances[j])
                    {
                        save.AreaHunters[j] |= (byte)(1 << i);
                        if (++counts[j] >= 3)
                        {
                            for (int k = 3; k > j; k--)
                            {
                                chances[k] = chances[k - 1];
                            }
                            chances[j] = 0;
                            chance = chances[3];
                        }
                        break;
                    }
                }
            }
        }

        public static void InitHunterSpawns(Scene scene, IReadOnlyList<EntityBase> entities, bool initialize)
        {
            for (int i = 1; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                player.LoadFlags &= ~LoadFlags.Active;
                player.LoadFlags &= ~LoadFlags.SlotActive;
                player.IsBot = false;
            }
            PlayerEntity.PlayerCount = 1;
            PlayerEntity.PlayersCreated = 1;
            if (scene.AreaId >= 8) // handled differently in-game
            {
                return;
            }
            if (GameState.GetAreaState(scene.AreaId) != AreaState.Clear
                || scene.RoomId != 50 // Data Shrine 02 (UNIT2_RM2)
                || PlayerEntity.Main.AvailableWeapons[BeamType.Battlehammer])
            {
                int randomHunters = GameState.StorySave.AreaHunters[scene.AreaId / 2] & 0x7E; // ignore Samus and Guardian
                int randomHunterCount = System.Numerics.BitOperations.PopCount((uint)randomHunters);
                int extraCount = 0; // extra index to roll Guardian if at least one hunter has already been rolled
                for (int i = 0; i < entities.Count; i++)
                {
                    if (PlayerEntity.PlayerCount >= PlayerEntity.MaxPlayers)
                    {
                        break;
                    }
                    EntityBase entity = entities[i];
                    if (entity.Type != EntityType.EnemySpawn)
                    {
                        continue;
                    }
                    var spawner = (EnemySpawnEntity)entity;
                    if (spawner.Data.EnemyType != EnemyType.Hunter)
                    {
                        continue;
                    }
                    if (spawner.Data.Fields.S09.HunterId == 8 && (Cheats.NoRandomEncounters || Features.NoRepeatEncounters
                        && scene.RoomId >= 27 && scene.RoomId <= 92 && _completedRandomEncounterRooms[scene.RoomId - 27]))
                    {
                        return;
                    }
                    if (Rng.GetRandomInt2(100) >= spawner.Data.Fields.S09.HunterChance)
                    {
                        continue;
                    }
                    PlayerEntity player = PlayerEntity.Players[PlayerEntity.PlayerCount];
                    player.IsBot = true;
                    player.EnemySpawner = spawner;
                    Hunter hunter;
                    if (spawner.Data.Fields.S09.HunterId == 8) // random
                    {
                        uint rand = Rng.GetRandomInt2(randomHunterCount + extraCount);
                        if (rand < randomHunterCount)
                        {
                            // todo?: determine bot level based on octoliths (unused)
                            int index = 0;
                            int j;
                            for (j = 0; j < 8; j++)
                            {
                                if ((randomHunters & (1 << j)) != 0)
                                {
                                    if (index++ == rand)
                                    {
                                        break;
                                    }
                                }
                            }
                            if (j > 0 && j < 7)
                            {
                                // mustodo: play hunter music
                            }
                            hunter = (Hunter)j;
                        }
                        else
                        {
                            hunter = Hunter.Guardian;
                        }
                    }
                    else
                    {
                        hunter = (Hunter)spawner.Data.Fields.S09.HunterId;
                    }
                    if (hunter != Hunter.Guardian)
                    {
                        extraCount = 1;
                    }
                    if ((randomHunters & (1 << (int)hunter)) != 0)
                    {
                        randomHunters &= ~(1 << (int)hunter);
                        randomHunterCount--;
                    }
                    int suitColor = spawner.Data.Fields.S09.HunterColor;
                    if (hunter == PlayerEntity.Main.Hunter && suitColor == PlayerEntity.Main.Recolor
                        && Features.AlternateHunters1P)
                    {
                        suitColor = PlayerEntity.Main.Recolor == 0 ? 1 : 0;
                    }
                    PlayerEntity.Create(hunter, suitColor);
                    if (initialize)
                    {
                        player.LoadFlags |= LoadFlags.SlotActive;
                        player.Initialized = false;
                        scene.AddEntity(player);
                    }
                    // todo: encounter state and bot level
                    PlayerEntity.PlayerCount++;
                }
            }
            for (int i = 1; i < PlayerEntity.MaxPlayers; i++)
            {
                PlayerEntity player = PlayerEntity.Players[i];
                if (player.IsBot)
                {
                    int dropId = GameState.StorySave.GetEnemyOctolithDrop((int)player.Hunter);
                    if (dropId < 8)
                    {
                        var header = new EntityDataHeader((ushort)EntityType.Artifact, entityId: -1,
                            position: Vector3.Zero, upVector: Vector3.UnitY, facingVector: Vector3.UnitX);
                        var data = new ArtifactEntityData(header, modelId: 8, artifactId: (byte)dropId, active: 0,
                            hasBase: 0, message1Target: 0, message1: Message.None, message2Target: 0, message2: Message.None,
                            message3Target: 0, message3: Message.None, linkedEntityId: -1);
                        var artifact = new ArtifactEntity(data, nodeName: "", scene);
                        scene.AddEntity(artifact);
                    }
                }
            }
        }

        public static (CollisionInstance, IReadOnlyList<EntityBase>) SetUpRoom(GameMode mode,
            int playerCount, BossFlags bossFlags, int nodeLayerMask, int entityLayerId,
            RoomMetadata metadata, RoomEntity room, Scene scene, bool isRoomTransition)
        {
            if (playerCount == 0)
            {
                playerCount = PlayerEntity.PlayerCount;
            }
            if (entityLayerId < 0 || entityLayerId > 15)
            {
                if (mode == GameMode.SinglePlayer)
                {
                    if (bossFlags == BossFlags.Unspecified)
                    {
                        bossFlags = GameState.StorySave.BossFlags;
                    }
                    entityLayerId = ((int)bossFlags >> (2 * scene.AreaId)) & 3;
                }
                else
                {
                    entityLayerId = Metadata.GetMultiplayerEntityLayer(mode, playerCount);
                }
            }
            if (nodeLayerMask == 0)
            {
                int nodePlayerCount = Features.MaxRoomDetail ? 2 : playerCount;
                nodeLayerMask = GetNodeLayer(mode, metadata.NodeLayer, nodePlayerCount);
            }
            CollisionInstance collision = Collision.GetCollision(metadata, nodeLayerMask);
            NodeData? nodeData = null;
            if (metadata.NodePath != null)
            {
                nodeData = ReadNodeData.ReadData(Paths.Combine(@"", metadata.NodePath));
            }
            if (isRoomTransition)
            {
                collision.Active = false;
            }
            room.Setup(metadata.Name, metadata, collision, nodeData, nodeLayerMask, metadata.Id);
            IReadOnlyList<EntityBase> entities = LoadEntities(metadata, entityLayerId, scene);
            entities = GetExtraEntities(room.RoomId, entities, scene);
            return (collision, entities);
        }

        public static int GetNodeLayer(GameMode mode, int roomLayer, int playerCount)
        {
            int nodeLayerMask = 0;
            if (mode == GameMode.SinglePlayer)
            {
                if (roomLayer > 0)
                {
                    nodeLayerMask = nodeLayerMask & 0xC03F | (((1 << roomLayer) & 0xFF) << 6);
                }
            }
            else
            {
                nodeLayerMask |= (int)NodeLayer.MultiplayerU;
                if (playerCount <= 2)
                {
                    nodeLayerMask |= (int)NodeLayer.MultiplayerLod0;
                }
                else
                {
                    nodeLayerMask |= (int)NodeLayer.MultiplayerLod1;
                }
                if (mode == GameMode.Capture)
                {
                    nodeLayerMask |= (int)NodeLayer.CaptureTheFlag;
                }
            }
            return nodeLayerMask;
        }

        private static IReadOnlyList<EntityBase> LoadEntities(RoomMetadata metadata, int layerId, Scene scene)
        {
            var results = new List<EntityBase>();
            if (metadata.EntityPath == null)
            {
                return results;
            }
            // only FirstHunt is passed here, not Hybrid -- model/anim/col should be loaded from FH, and ent/node from MPH
            IReadOnlyList<Entity> entities = Read.GetEntities(metadata.EntityPath, layerId, metadata.FirstHunt);
            foreach (Entity entity in entities)
            {
                string nodeName = entity.NodeName;
                if (entity.Type == EntityType.Platform)
                {
                    results.Add(new PlatformEntity(((Entity<PlatformEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhPlatform)
                {
                    results.Add(new FhPlatformEntity(((Entity<FhPlatformEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.Object)
                {
                    results.Add(new ObjectEntity(((Entity<ObjectEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.PlayerSpawn || entity.Type == EntityType.FhPlayerSpawn)
                {
                    results.Add(new PlayerSpawnEntity(((Entity<PlayerSpawnEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.Door)
                {
                    results.Add(new DoorEntity(((Entity<DoorEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhDoor)
                {
                    results.Add(new FhDoorEntity(((Entity<FhDoorEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.ItemSpawn)
                {
                    results.Add(new ItemSpawnEntity(((Entity<ItemSpawnEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhItemSpawn)
                {
                    results.Add(new FhItemSpawnEntity(((Entity<FhItemSpawnEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.EnemySpawn)
                {
                    results.Add(new EnemySpawnEntity(((Entity<EnemySpawnEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhEnemySpawn)
                {
                    results.Add(new FhEnemySpawnEntity(((Entity<FhEnemySpawnEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.TriggerVolume)
                {
                    results.Add(new TriggerVolumeEntity(((Entity<TriggerVolumeEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.FhTriggerVolume)
                {
                    results.Add(new FhTriggerVolumeEntity(((Entity<FhTriggerVolumeEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.AreaVolume)
                {
                    results.Add(new AreaVolumeEntity(((Entity<AreaVolumeEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhAreaVolume)
                {
                    results.Add(new FhAreaVolumeEntity(((Entity<FhAreaVolumeEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.JumpPad)
                {
                    results.Add(new JumpPadEntity(((Entity<JumpPadEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhJumpPad)
                {
                    results.Add(new FhJumpPadEntity(((Entity<FhJumpPadEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.PointModule || entity.Type == EntityType.FhPointModule)
                {
                    results.Add(new PointModuleEntity(((Entity<PointModuleEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.MorphCamera)
                {
                    results.Add(new MorphCameraEntity(((Entity<MorphCameraEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.FhMorphCamera)
                {
                    results.Add(new FhMorphCameraEntity(((Entity<FhMorphCameraEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.OctolithFlag)
                {
                    results.Add(new OctolithFlagEntity(((Entity<OctolithFlagEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.FlagBase)
                {
                    results.Add(new FlagBaseEntity(((Entity<FlagBaseEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.Teleporter)
                {
                    results.Add(new TeleporterEntity(((Entity<TeleporterEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.NodeDefense)
                {
                    results.Add(new NodeDefenseEntity(((Entity<NodeDefenseEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.LightSource)
                {
                    results.Add(new LightSourceEntity(((Entity<LightSourceEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.Artifact)
                {
                    results.Add(new ArtifactEntity(((Entity<ArtifactEntityData>)entity).Data, nodeName, scene));
                }
                else if (entity.Type == EntityType.CameraSequence)
                {
                    results.Add(new CamSeqEntity(((Entity<CameraSequenceEntityData>)entity).Data, scene));
                }
                else if (entity.Type == EntityType.ForceField)
                {
                    results.Add(new ForceFieldEntity(((Entity<ForceFieldEntityData>)entity).Data, nodeName, scene));
                }
                else
                {
                    throw new ProgramException($"Invalid entity type {entity.Type}");
                }
            }
            return results;
        }

        private static void LoadResources(Scene? scene)
        {
            // todo: this could also allocate effect lists and stuff, since we don't need those if there's no room
            // todo: sort this all out by game mode/etc. for what's actually needed
            // todo: add an assert if any loading occurs after room init (besides manual model/entity loading)
            if (scene != null)
            {
                if (Paths.IsMphJapan || Paths.IsMphKorea)
                {
                    (int count, byte[] charData) = Read.ReadKanjiFont(scene.GameMode == GameMode.SinglePlayer);
                    byte[] widths = new byte[count];
                    if (Paths.IsMphJapan)
                    {
                        Array.Fill(widths, (byte)10);
                    }
                    else
                    {
                        Array.Fill(widths, (byte)11);
                        widths[1] = 2; // KR period
                        widths[32] = 6; // ASCII space
                    }
                    byte[] offsets = new byte[count];
                    Font.Kanji.SetData(widths, offsets, charData, minChar: 0);
                }
                LoadBombResources(scene);
                LoadBeamEffectResources(scene);
                LoadBeamProjectileResources(scene);
                LoadRoomResources(scene);
                LoadHunterResources(Hunter.Samus, scene);
                LoadHunterResources(Hunter.Kanden, scene);
                LoadHunterResources(Hunter.Trace, scene);
                LoadHunterResources(Hunter.Sylux, scene);
                LoadHunterResources(Hunter.Noxus, scene);
                LoadHunterResources(Hunter.Spire, scene);
                LoadHunterResources(Hunter.Weavel, scene);
                LoadHunterResources(Hunter.Guardian, scene);
                LoadCommonHunterResources(scene);
            }
        }

        public static void LoadCommonHunterResources(Scene scene)
        {
            scene.LoadModel("doubleDamage_img");
            scene.LoadModel("alt_ice");
            scene.LoadModel("gunSmoke");
            scene.LoadModel("trail");
            scene.LoadModel("octolith_simple");
            scene.LoadModel("Octolith");
            // todo?: same as above
            scene.LoadEffect(10); // ballDeath
            scene.LoadEffect(216); // deathAlt
            scene.LoadEffect(187); // flamingAltForm
            scene.LoadEffect(188); // flamingGun
            scene.LoadEffect(189); // flamingHunter
            for (int i = 0; i < 9; i++)
            {
                scene.LoadEffect(Metadata.MuzzleEffectIds[i]);
                scene.LoadEffect(Metadata.ChargeEffectIds[i]);
                scene.LoadEffect(Metadata.ChargeLoopEffectIds[i]);
            }
            PlayerEntity.LoadWeaponNames();
            PlayerEntity.GeneratePlayerVolumes();
            Strings.ReadStringTable(StringTables.HudMsgsCommon);
            Strings.ReadStringTable(StringTables.HudMessagesSP);
            Strings.ReadStringTable(StringTables.HudMessagesMP);
            if (!scene.Multiplayer)
            {
                Strings.ReadStringTable(StringTables.ScanLog);
            }
        }

        public static void LoadHunterResources(Hunter hunter, Scene scene)
        {
            scene.LoadModel(hunter == Hunter.Noxus || hunter == Hunter.Trace ? "nox_ice" : "samus_ice");
            foreach (string modelName in Metadata.HunterModels[hunter])
            {
                scene.LoadModel(modelName);
            }
            if (hunter == Hunter.Samus)
            {
                scene.LoadEffect(30); // samusFurl
                scene.LoadEffect(136); // samusDash
            }
            else if (hunter == Hunter.Kanden)
            {
                PlayerEntity.GenerateKandenAltNodeDistances();
            }
            else if (hunter == Hunter.Spire)
            {
                scene.LoadEffect(37); // spireAltSlam
            }
            else if (hunter == Hunter.Noxus)
            {
                scene.LoadEffect(235); // noxHit
            }
        }

        private static void LoadBombResources(Scene scene)
        {
            // todo?: not all of these need to be loaded depending on the hunters/mode
            scene.LoadModel("KandenAlt_TailBomb");
            scene.LoadModel("arcWelder");
            scene.LoadModel("arcWelder1");
            scene.LoadEffect(9); // bombStart
            scene.LoadEffect(113); // bombStartSylux
            scene.LoadEffect(119); // bombStartMP
            scene.LoadEffect(128); // bombKanden
            scene.LoadEffect(129); // collapsingStreaks
            scene.LoadEffect(145); // bombBlue
            scene.LoadEffect(146); // bombSylux
            scene.LoadEffect(149); // bombStartSyluxG
            scene.LoadEffect(150); // bombStartSyluxO
            scene.LoadEffect(151); // bombStartSyluxP
            scene.LoadEffect(152); // bombStartSyluxR
            scene.LoadEffect(153); // bombStartSyluxW
        }

        private static void LoadBeamEffectResources(Scene scene)
        {
            scene.LoadModel("iceWave");
            scene.LoadModel("sniperBeam");
            scene.LoadModel("cylBossLaserBurn");
        }

        private static void LoadBeamProjectileResources(Scene scene)
        {
            scene.LoadModel("iceShard");
            scene.LoadModel("energyBeam");
            scene.LoadModel("trail");
            scene.LoadModel("electroTrail");
            scene.LoadModel("arcWelder");
            scene.LoadEffect(57); // muzzleElc
            scene.LoadEffect(58); // muzzleGst
            scene.LoadEffect(59); // muzzleIce
            scene.LoadEffect(60); // muzzleJak
            scene.LoadEffect(61); // muzzleMrt
            scene.LoadEffect(62); // muzzlePB
            scene.LoadEffect(63); // muzzleSnp
            scene.LoadEffect(78); // iceWave
            scene.LoadEffect(85); // electroCharge
            scene.LoadEffect(86); // electroHit
            scene.LoadEffect(92); // powerBeamCharge
            scene.LoadEffect(98); // powerBeamChargeNoSplat
            scene.LoadEffect(99); // powerBeamHolo
            scene.LoadEffect(100); // powerBeamLava
            scene.LoadEffect(121); // powerBeamHoloBG
            scene.LoadEffect(122); // powerBeamHoloB
            scene.LoadEffect(123); // powerBeamIce
            scene.LoadEffect(124); // powerBeamRock
            scene.LoadEffect(125); // powerBeamSand
            scene.LoadEffect(126); // powerBeamSnow
            scene.LoadEffect(130); // fireProjectile
            scene.LoadEffect(134); // hammerProjectile
            scene.LoadEffect(137); // electroProjectile
            scene.LoadEffect(140); // energyRippleB
            scene.LoadEffect(141); // energyRippleBG
            scene.LoadEffect(142); // energyRippleO
            scene.LoadEffect(171); // electroChargeNA
            scene.LoadEffect(211); // mortarProjectile
            scene.LoadEffect(237); // electroProjectileUncharged
            scene.LoadEffect(238); // enemyProjectile1
            scene.LoadEffect(246); // enemyMortarProjectile
        }

        private static void LoadRoomResources(Scene scene)
        {
            scene.LoadEffect(1); // powerBeam
            scene.LoadEffect(2); // powerBeamNoSplat
            scene.LoadEffect(5); // missile1
            scene.LoadEffect(6); // mortar1
            scene.LoadEffect(7); // shotGunCol
            scene.LoadEffect(8); // shotGunShrapnel
            scene.LoadEffect(11); // jackHammerCol
            scene.LoadEffect(12); // effectiveHitPB
            scene.LoadEffect(13); // effectiveHitElectric
            scene.LoadEffect(14); // effectiveHitMsl
            scene.LoadEffect(15); // effectiveHitJack
            scene.LoadEffect(16); // effectiveHitSniper
            scene.LoadEffect(17); // effectiveHitIce
            scene.LoadEffect(18); // effectiveHitMortar
            scene.LoadEffect(19); // effectiveHitGhost
            scene.LoadEffect(20); // sprEffectivePB
            scene.LoadEffect(21); // sprEffectiveElectric
            scene.LoadEffect(22); // sprEffectiveMsl
            scene.LoadEffect(23); // sprEffectiveJack
            scene.LoadEffect(24); // sprEffectiveSniper
            scene.LoadEffect(25); // sprEffectiveIce
            scene.LoadEffect(26); // sprEffectiveMortar
            scene.LoadEffect(27); // sprEffectiveGhost
            scene.LoadEffect(28); // sniperCol
            scene.LoadEffect(31); // spawnEffect
            scene.LoadEffect(33); // spawnEffectMP
            scene.LoadEffect(99); // powerBeamHolo
            scene.LoadEffect(115); // ineffectivePsycho
            scene.LoadEffect(154); // mpEffectivePB
            scene.LoadEffect(155); // mpEffectiveElectric
            scene.LoadEffect(156); // mpEffectiveMsl
            scene.LoadEffect(157); // mpEffectiveJack
            scene.LoadEffect(158); // mpEffectiveSniper
            scene.LoadEffect(159); // mpEffectiveIce
            scene.LoadEffect(160); // mpEffectiveMortar
            scene.LoadEffect(161); // mpEffectiveGhost
            scene.LoadEffect(173); // jackHammerColNA
            scene.LoadEffect(190); // missileCharged
            scene.LoadEffect(191); // mortarCharged
            scene.LoadEffect(192); // mortarChargedAffinity
            scene.LoadEffect(231); // iceShatter
            scene.LoadEffect(239); // enemyCol1
            scene.LoadModel(Read.GetSingleParticle(SingleType.Death).Model);
            scene.LoadModel(Read.GetSingleParticle(SingleType.Fuzzball).Model);
            if (!scene.Multiplayer)
            {
                scene.LoadModel(Read.GetModelInstance("icons", dir: MetaDir.Hud).Model);
            }
            // skdebug - the game only loads these if the Omega Cannon item is in the room
            scene.LoadEffect(209); // ultimateProjectile
            scene.LoadEffect(245); // ultimateCol
        }

        public static void LoadEntityResources(EntityBase entity, Scene scene)
        {
            if (entity is ObjectEntity obj)
            {
                LoadObjectResources(obj, scene);
            }
            else if (entity is PlatformEntity plat)
            {
                LoadPlatformResources(plat, scene);
            }
            else if (entity is EnemySpawnEntity spawner)
            {
                LoadEnemyResources(spawner, scene);
            }
            else if (entity is ItemSpawnEntity itemSpawner)
            {
                LoadItemResources(itemSpawner, scene);
            }
        }

        public static void LoadObjectResources(Scene scene)
        {
            foreach (EntityBase entity in scene.Entities)
            {
                if (entity is ObjectEntity obj)
                {
                    LoadObjectResources(obj, scene);
                }
            }
        }

        public static void LoadObjectResources(ObjectEntity obj, Scene scene)
        {
            if (obj.Data.EffectId != 0)
            {
                scene.LoadEffect(obj.Data.EffectId);
            }
        }

        public static void LoadPlatformResources(Scene scene)
        {
            foreach (EntityBase entity in scene.Entities)
            {
                if (entity is PlatformEntity platform)
                {
                    LoadPlatformResources(platform, scene);
                }
            }
        }

        public static void LoadPlatformResources(PlatformEntity platform, Scene scene)
        {
            if (platform.Data.ResistEffectId != 0)
            {
                scene.LoadEffect(platform.Data.ResistEffectId);
            }
            if (platform.Data.DamageEffectId != 0)
            {
                scene.LoadEffect(platform.Data.DamageEffectId);
            }
            if (platform.Data.DeadEffectId != 0)
            {
                scene.LoadEffect(platform.Data.DeadEffectId);
            }
            if (platform.Data.Flags.TestFlag(PlatformFlags.SamusShip))
            {
                scene.LoadEffect(182); // nozzleJet
            }
            if (platform.Data.Flags.TestFlag(PlatformFlags.BeamSpawner) && platform.Data.BeamId == 0)
            {
                scene.LoadEffect(183); // syluxMissile
                scene.LoadEffect(184); // syluxMissileCol
                scene.LoadEffect(185); // syluxMissileFlash
            }
            if (platform.Data.ItemChance > 0)
            {
                LoadItem(platform.Data.ItemType, scene);
            }
        }

        public static void LoadEnemyResources(Scene scene)
        {
            // todo?: pre-allocation(?)
            foreach (EntityBase entity in scene.Entities)
            {
                if (entity is EnemySpawnEntity spawner)
                {
                    LoadEnemyResources(spawner, scene);
                }
            }
        }

        public static void LoadEnemyResources(EnemySpawnEntity spawner, Scene scene)
        {
            LoadEnemy(spawner.Data.EnemyType, scene);
            if (spawner.Data.SpawnerHealth > 0)
            {
                if (spawner.Data.EnemyType == EnemyType.WarWasp || spawner.Data.EnemyType == EnemyType.BarbedWarWasp)
                {
                    scene.LoadModel("PlantCarnivarous_Pod");
                }
                else
                {
                    scene.LoadModel("EnemySpawner");
                }
            }
            if (spawner.Data.ItemChance > 0)
            {
                LoadItem(spawner.Data.ItemType, scene);
            }
            switch (spawner.Data.EnemyType)
            {
            case EnemyType.Cretaphid:
                LoadEnemy(EnemyType.CretaphidEye, scene);
                scene.LoadEffect(65); // cylCrystalCharge
                scene.LoadEffect(66); // cylCrystalKill
                scene.LoadEffect(67); // cylCrystalShot
                scene.LoadEffect(73); // cylCrystalKill2
                scene.LoadEffect(74); // cylCrystalKill3
                scene.LoadEffect(116); // cylCrystalProjectile
                scene.LoadEffect(117); // cylWeakSpotShot
                scene.LoadEffect(138); // cylHomingProjectile
                scene.LoadEffect(139); // cylHomingKill
                LoadItem(ItemType.HealthMedium, scene);
                LoadItem(ItemType.UASmall, scene);
                LoadItem(ItemType.MissileSmall, scene);
                break;
            case EnemyType.Gorea1A:
                LoadEnemy(EnemyType.Gorea1B, scene);
                scene.LoadEffect(48); // goreaChargeJak
                scene.LoadEffect(46); // goreaChargeElc
                scene.LoadEffect(49); // goreaChargeMrt
                scene.LoadEffect(47); // goreaChargeIce
                scene.LoadEffect(50); // goreaChargeSnp
                scene.LoadEffect(41); // goreaArmChargeUp
                scene.LoadEffect(42); // goreaBallExplode
                scene.LoadEffect(43); // goreaShoulderDamageLoop
                scene.LoadEffect(44); // goreaShoulderHits
                scene.LoadEffect(45); // goreaShoulderKill
                scene.LoadEffect(54); // goreaFireJak
                scene.LoadEffect(51); // goreaFireElc
                scene.LoadEffect(55); // goreaFireMrt
                scene.LoadEffect(53); // goreaFireIce
                scene.LoadEffect(56); // goreaFireSnp
                scene.LoadEffect(52); // goreaFireGst
                scene.LoadEffect(71); // goreaSlam
                scene.LoadEffect(72); // goreaBallExplode2
                scene.LoadEffect(104); // goreaEyeFlash
                scene.LoadEffect(148); // grappleEnd
                scene.LoadEffect(175); // goreaReveal
                scene.LoadEffect(179); // goreaGrappleDamage
                scene.LoadEffect(180); // goreaGrappleDie
                LoadItem(ItemType.HealthBig, scene);
                LoadItem(ItemType.UABig, scene);
                LoadItem(ItemType.MissileBig, scene);
                break;
            case EnemyType.Trocra:
                scene.LoadEffect(164); // goreaCrystalHit
                scene.LoadEffect(75); // goreaCrystalExplode
                LoadItem(ItemType.HealthSmall, scene);
                LoadItem(ItemType.UASmall, scene);
                LoadItem(ItemType.MissileSmall, scene);
                break;
            case EnemyType.Gorea2:
                LoadEnemy(EnemyType.GoreaMeteor, scene);
                scene.LoadEffect(104); // goreaEyeFlash
                scene.LoadEffect(224); // goreaLaserCol
                scene.LoadEffect(79); // goreaMeteor
                scene.LoadEffect(176); // goreaMeteorDamage
                scene.LoadEffect(177); // goreaMeteorDestroy
                scene.LoadEffect(178); // goreaMeteorHit
                scene.LoadEffect(80); // goreaTeleport
                scene.LoadEffect(225); // goreaHurt
                scene.LoadEffect(44); // goreaShoulderHits
                scene.LoadEffect(72); // goreaBallExplode2
                scene.LoadEffect(174); // goreaMeteorLaunch
                scene.LoadEffect(210); // goreaLaserCharge
                LoadItem(ItemType.HealthSmall, scene);
                LoadItem(ItemType.UASmall, scene);
                LoadItem(ItemType.MissileSmall, scene);
                break;
            case EnemyType.Slench:
                LoadEnemy(EnemyType.SlenchNest, scene);
                LoadEnemy(EnemyType.SlenchSynapse, scene);
                scene.LoadEffect(64); // tear
                scene.LoadEffect(81); // tearChargeUp
                scene.LoadEffect(68); // tearSplat
                scene.LoadEffect(82); // eyeShield
                scene.LoadEffect(70); // eyeShieldHit
                scene.LoadEffect(69); // eyeShieldCharge
                scene.LoadEffect(83); // eyeShieldDefeat
                scene.LoadEffect(109); // eyeTurretCharge
                scene.LoadEffect(135); // synapseKill
                scene.LoadEffect(201); // eyeDamageLoop
                scene.LoadEffect(202); // eyeHit
                scene.LoadEffect(203); // eyelKill
                scene.LoadEffect(204); // eyeKill2
                scene.LoadEffect(205); // eyeKill3
                scene.LoadEffect(206); // eyeFinalKill
                LoadItem(ItemType.HealthMedium, scene);
                LoadItem(ItemType.UASmall, scene);
                LoadItem(ItemType.MissileSmall, scene);
                break;
            case EnemyType.Blastcap:
                scene.LoadEffect(3); // blastCapHit
                scene.LoadEffect(4); // blastCapBlow
                break;
            case EnemyType.PsychoBit1:
                scene.LoadEffect(240); // psychoCharge
                break;
            case EnemyType.AlimbicTurret:
                scene.LoadEffect(207); // chargeTurret
                scene.LoadEffect(208); // flashTurret
                break;
            case EnemyType.FireSpawn:
                if (spawner.Data.Fields.S06.EnemySubtype == 1)
                {
                    scene.LoadEffect(96); // iceDemonHurl
                    scene.LoadEffect(132); // iceDemonRise
                    scene.LoadEffect(133); // iceDemonDive
                    scene.LoadEffect(131); // iceDemonSplat
                    scene.LoadEffect(217); // iceDemonDeath
                }
                else
                {
                    scene.LoadEffect(94); // lavaDemonHurl
                    scene.LoadEffect(95); // lavaDemonRise
                    scene.LoadEffect(93); // lavaDemonDive
                    scene.LoadEffect(110); // lavaDemonSplat
                    scene.LoadEffect(218); // lavaDemonDeath
                }
                break;
            case EnemyType.GreaterIthrak:
                scene.LoadEffect(102); // hangingSpit
                scene.LoadEffect(101); // hangingDrip
                scene.LoadEffect(103); // hangingSplash
                break;
            case EnemyType.Shriekbat:
                scene.LoadEffect(29); // shriekBatTrail
                scene.LoadEffect(108); // shriekBatCol
                break;
            case EnemyType.CarnivorousPlant:
                ObjectMetadata meta = Metadata.GetObjectById(spawner.Data.Fields.S07.EnemySubtype);
                scene.LoadModel(meta.Name);
                break;
            }
        }

        private static void LoadEnemy(EnemyType enemy, Scene scene)
        {
            if (enemy == EnemyType.SlenchSynapse)
            {
                if (scene.RoomId == 76) // UNIT3_B2
                {
                    scene.LoadModel("BigEyeSynapse_04");
                }
                else if (scene.RoomId == 64) // UNIT2_B2
                {
                    scene.LoadModel("BigEyeSynapse_03");
                }
                else if (scene.RoomId == 82) // UNIT4_B1
                {
                    scene.LoadModel("BigEyeSynapse_02");
                }
                else // if(scene.RoomId == 35) // UNIT1_B1
                {
                    scene.LoadModel("BigEyeSynapse_01");
                }
            }
            else
            {
                string? model = Metadata.GetEnemyModelName(enemy);
                if (model != null)
                {
                    scene.LoadModel(model);
                }
                if (enemy == EnemyType.Gorea1A)
                {
                    scene.LoadModel("Gorea1B_lod0");
                    scene.LoadModel("goreaArmRegen");
                    scene.LoadModel("goreaMindTrick");
                    scene.LoadModel("goreaMindTrick");
                }
                else if (enemy == EnemyType.Gorea2)
                {
                    scene.LoadModel("goreaMeteor");
                    scene.LoadModel("goreaLaser");
                    scene.LoadModel("goreaLaserColl");
                }
            }
            int effectId = Metadata.GetEnemyDeathEffect(enemy);
            if (effectId > 0)
            {
                scene.LoadEffect(effectId);
            }
        }

        public static void LoadItemResources(Scene scene)
        {
            // todo: pre-allocate
            if (scene.Multiplayer)
            {
                LoadItem(ItemType.UASmall, scene);
                LoadItem(ItemType.UABig, scene);
                LoadItem(ItemType.MissileSmall, scene);
                LoadItem(ItemType.MissileBig, scene);
            }
            foreach (EntityBase entity in scene.Entities)
            {
                if (entity is ItemSpawnEntity itemSpawner)
                {
                    LoadItemResources(itemSpawner, scene);
                }
            }
        }

        public static void LoadItemResources(ItemSpawnEntity itemSpawner, Scene scene)
        {
            LoadItem(itemSpawner.Data.ItemType, scene);
            if (itemSpawner.Data.HasBase != 0)
            {
                scene.LoadModel("items_base");
            }
        }

        private static void LoadItem(ItemType item, Scene scene)
        {
            if (item == ItemType.None)
            {
                return;
            }
            // todo: affinity weapon replacement
            int index = (int)item;
            Debug.Assert(index < Metadata.Items.Count);
            scene.LoadModel(Metadata.Items[index]);
            if (item == ItemType.ArtifactKey)
            {
                scene.LoadEffect(144); // artifactKeyEffect
            }
            else if (item == ItemType.Deathalt)
            {
                scene.LoadEffect(181); // deathBall
            }
            else if (item == ItemType.OmegaCannon)
            {
                scene.LoadEffect(209); // ultimateProjectile
                scene.LoadEffect(245); // ultimateCol
            }
            else if (item == ItemType.DoubleDamage)
            {
                scene.LoadEffect(244); //  doubleDamageGun
            }
        }

        public static BeamProjectileEntity[] CreateBeamList(int size, Scene scene)
        {
            Debug.Assert(size > 0);
            var beams = new BeamProjectileEntity[size];
            for (int i = 0; i < size; i++)
            {
                beams[i] = new BeamProjectileEntity(scene);
            }
            return beams;
        }

        private static IReadOnlyList<EntityBase> GetExtraEntities(int roomId, IReadOnlyList<EntityBase> entities, Scene scene)
        {
            // todo: load entities into unused rooms to make them playable
            Hunter hunter = PlayerEntity.Main.Hunter;
            if (scene.GameMode != GameMode.SinglePlayer || hunter == Hunter.Samus || !Features.AlternateHunters1P)
            {
                return entities;
            }

            EntityBase GetEntity(EntityType type, int id)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    EntityBase entity = entities[i];
                    if (entity.Type == type && entity.Id == id)
                    {
                        return entity;
                    }
                }
                throw new ProgramException("Could not find entity to update.");
            }

            short nextId = 30000;
            JumpPadEntity CreateJumpPad(string nodeName, Vector3 position, float speed, float radius = 1,
                float height = 1, float offset = 0, ushort frames = 20, TriggerFlags flags = TriggerFlags.PlayerAlt)
            {
                var radiusFx = new Fixed(Fixed.ToInt(radius));
                var heightFx = new Fixed(Fixed.ToInt(height));
                var header = new EntityDataHeader((ushort)EntityType.JumpPad, nextId++, position, Vector3.UnitY, Vector3.UnitZ);
                var cylPos = new Vector3Fx(0, Fixed.ToInt(offset), 0);
                var volume = new RawCollisionVolume(Vector3.UnitY.ToVector3Fx(), cylPos, radiusFx, heightFx);
                var data = new JumpPadEntityData(header, parentId: -1, volume, beamVector: Vector3.UnitY.ToVector3Fx(),
                    speed: new Fixed(Fixed.ToInt(speed)), controlLockTime: 0, cooldownTime: frames, active: 1,
                    modelId: 0, beamType: 0, triggerFlags: flags);
                return new JumpPadEntity(data, nodeName, scene);
            }

            TeleporterEntity CreateTeleporter(Vector3 position, Vector3 targetPos, Vector3 facing, string nodeName, string targetNode)
            {
                var header = new EntityDataHeader((ushort)EntityType.Teleporter, nextId++, position, Vector3.UnitY, facing);
                var data = new TeleporterEntityData(header, 0, 0, 8, 1, 1, null, targetPos.ToVector3Fx(), targetNode);
                return new TeleporterEntity(data, nodeName, scene, forceMultiplayer: true);
            }

            var list = new List<EntityBase>(entities);
            if (roomId == 27 && (hunter == Hunter.Noxus || hunter == Hunter.Trace)) // Alinos Gateway
            {
                EntityBase missileSpawn = GetEntity(EntityType.ItemSpawn, 13);
                missileSpawn.Position = new Vector3(-60, 6.5f, 33.6f);
            }
            else if (roomId == 28 && (hunter == Hunter.Noxus || hunter == Hunter.Trace)) // Echo Hall
            {
                list.Add(CreateJumpPad("rmC0B", new Vector3(-14.3f, 0, -8.9f), 0.25f, 0.75f));
            }
            else if (roomId == 67 && (hunter == Hunter.Noxus || hunter == Hunter.Spire
                || hunter == Hunter.Trace || hunter == Hunter.Weavel)) // Cortex CPU
            {
                if (hunter != Hunter.Weavel)
                {
                    list.Add(CreateJumpPad("rmMain", new Vector3(8.7f, -2, 12.4f), 0.3f, 0.5f));
                }
                if (hunter == Hunter.Noxus || hunter == Hunter.Trace || hunter == Hunter.Weavel)
                {
                    list.Add(CreateJumpPad("rmMain", new Vector3(8.7f, 3.4f, 0), 0.5f, 0.5f));
                }
            }
            else if (roomId == 79 && (hunter == Hunter.Noxus || hunter == Hunter.Trace)) // Sic Transit
            {
                EntityBase energyTankSpawn = GetEntity(EntityType.ItemSpawn, 29);
                energyTankSpawn.Position = energyTankSpawn.Position.AddY(-1.5f);
            }
            else if (roomId == 78 && hunter == Hunter.Noxus) // Ice Hive
            {
                list.Add(CreateJumpPad("rmChamberE", new Vector3(18.5f, 33.5f, -38.6f), 0.3f, 0.4f, frames: 35)); // same v
                list.Add(CreateJumpPad("rmChamberE", new Vector3(16.4f, 35.5f, -38.6f), 0.3f, 0.4f, frames: 35));
                list.Add(CreateJumpPad("rmChamberE", new Vector3(19.1f, 40.5f, -38.6f), 0.3f, 0.4f, frames: 35));
            }
            else if (roomId == 78 && hunter == Hunter.Weavel) // Ice Hive
            {
                list.Add(CreateJumpPad("rmChamberE", new Vector3(18.5f, 33.5f, -38.6f), 0.3f, 0.4f, frames: 35)); // same ^
            }
            else if (roomId == 78 && (hunter == Hunter.Trace || hunter == Hunter.Sylux)) // Ice Hive
            {
                var position = new Vector3(18.5f, 33, -38.6f);
                var targetPos = new Vector3(16.8f, 35.5f, -38.6f);
                list.Add(CreateTeleporter(position, targetPos, -Vector3.UnitZ, "rmChamberE", "rmChamberE"));
                if (hunter == Hunter.Trace)
                {
                    position = new Vector3(19.1f, 40, -38.6f);
                    targetPos = new Vector3(17.4f, 43.3f, -22.5f);
                    list.Add(CreateTeleporter(position, targetPos, -Vector3.UnitZ, "rmChamberE", "rmChamberE"));
                }
            }
            else if (roomId == 80) // Frost Labyrinth
            {
                if (hunter == Hunter.Noxus || hunter == Hunter.Spire)
                {
                    list.Add(CreateJumpPad("rmC0b", new Vector3(8, 0, 12.1f), 0.5f, 0.3f, frames: 60));
                }
                else if (hunter == Hunter.Kanden)
                {
                    EntityBase keySpawn = GetEntity(EntityType.ItemSpawn, 31);
                    keySpawn.Position = keySpawn.Position.AddY(-0.7f);
                }
                else if (hunter == Hunter.Weavel)
                {
                    EntityBase keySpawn = GetEntity(EntityType.ItemSpawn, 31);
                    keySpawn.Position = keySpawn.Position.AddY(-2);
                }
                else if (hunter == Hunter.Trace)
                {
                    EntityBase keySpawn = GetEntity(EntityType.ItemSpawn, 31);
                    keySpawn.Position = keySpawn.Position.WithY(0);
                }
            }
            else if (roomId == 30) // Magma Drop
            {
                if (hunter == Hunter.Noxus || hunter == Hunter.Spire || hunter == Hunter.Trace || hunter == Hunter.Weavel)
                {
                    list.Add(CreateJumpPad("rmLava", new Vector3(0.6f, -26.5f, 4.8f), 0.15f, 0.5f, offset: 2.2f));
                    list.Add(CreateJumpPad("rmLava", new Vector3(0.6f, 3.2f, -14.6f), 0.6f, 0.5f));
                    if (hunter != Hunter.Spire)
                    {
                        list.Add(CreateJumpPad("rmLava", new Vector3(0.6f, -21.6f, -6.8f), 0.5f, 0.5f));
                        list.Add(CreateJumpPad("rmLava", new Vector3(0.6f, 73.5f, -9.4f), 0.5f, 0.5f));
                        list.Add(CreateJumpPad("rmLava", new Vector3(0.6f, 94.2f, -15.5f), 0.5f, 0.5f));
                    }
                    else
                    {
                        ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 4)).Active = false;
                    }
                }
                if (hunter == Hunter.Trace || hunter == Hunter.Sylux)
                {
                    var position = new Vector3(0.59f, -27.63f, -11.91f);
                    var targetPos = new Vector3(0.59f, -20.63f, -6.81f);
                    list.Add(CreateTeleporter(position, -Vector3.UnitX, targetPos, "rmLava", "rmLava"));
                    position = new Vector3(0.59f, -23.03f, -10.61f);
                    targetPos = new Vector3(0.59f, -28.43f, -10.61f);
                    list.Add(CreateTeleporter(position, -Vector3.UnitX, targetPos, "rmLava", "rmLava"));
                }
            }
            else if (roomId == 41 && hunter == Hunter.Spire) // Processor Core
            {
                ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 3)).Active = false;
                ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 11)).Active = false;
                ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 12)).Active = false;
                ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 13)).Active = false;
                ((AreaVolumeEntity)GetEntity(EntityType.AreaVolume, 16)).Active = false;
            }
            else if (roomId == 38 && (hunter == Hunter.Noxus || hunter == Hunter.Trace)) // Piston Cave
            {
                list.Add(CreateJumpPad("rmMain", new Vector3(-1.4f, 0.3f, 29.1f), 0.3f, 0.4f));
                list.Add(CreateJumpPad("rmMain", new Vector3(29.1f, 0.3f, 1.4f), 0.3f, 0.4f));
                list.Add(CreateJumpPad("rmend", new Vector3(-10.8f, 9.6f, -28.3f), 0.3f, 0.4f)); // same v
                list.Add(CreateJumpPad("rmend", new Vector3(-19.6f, 12, -28.3f), 0.3f, 0.4f)); // same v
                list.Add(CreateJumpPad("rmend", new Vector3(-39.9f, 17, -24.5f), 0.5f, 0.4f));
            }
            else if (roomId == 38 && hunter == Hunter.Weavel) // Piston Cave
            {
                list.Add(CreateJumpPad("rmend", new Vector3(-10.8f, 9.6f, -28.3f), 0.3f, 0.4f)); // same ^
                list.Add(CreateJumpPad("rmend", new Vector3(-19.6f, 12, -28.3f), 0.3f, 0.4f)); // same ^
            }
            return list;
        }
    }
}
