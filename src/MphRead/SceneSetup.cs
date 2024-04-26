using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Formats.Collision;
using MphRead.Text;

namespace MphRead
{
    public static class SceneSetup
    {
        // todo: artifact flags
        public static (RoomEntity, RoomMetadata, CollisionInstance, IReadOnlyList<EntityBase>)
            LoadRoom(string name, Scene scene, int playerCount = 0, BossFlags bossFlags = BossFlags.Unspecified,
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
            Extract.LoadRuntimeData();
            LoadResources(scene);
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
                bossFlags, nodeLayerMask, entityLayerId, metadata, room, scene);
            GameState.StorySave.CheckpointRoomId = room.RoomId;
            return (room, metadata, collision, entities);
        }

        public static (CollisionInstance, IReadOnlyList<EntityBase>) SetUpRoom(GameMode mode,
            int playerCount, BossFlags bossFlags, int nodeLayerMask, int entityLayerId,
            RoomMetadata metadata, RoomEntity room, Scene scene)
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
                    entityLayerId = ((int)bossFlags >> 2 * scene.AreaId) & 3;
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
            room.Setup(metadata.Name, metadata, collision, nodeData, nodeLayerMask, metadata.Id);
            IReadOnlyList<EntityBase> entities = LoadEntities(metadata, entityLayerId, scene);
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
                    Array.Fill(widths, (byte)10);
                    byte[] offsets = new byte[count];
                    Font.Kanji.SetData(widths, offsets, charData, minChar: 0);
                }
                LoadBombResources(scene);
                LoadBeamEffectResources(scene);
                LoadBeamProjectileResources(scene);
                LoadRoomResources(scene);
                bool anyActive = false;
                for (int i = 0; i < 4; i++)
                {
                    PlayerEntity player = PlayerEntity.Players[i];
                    if (player.LoadFlags.TestFlag(LoadFlags.SlotActive))
                    {
                        anyActive = true;
                        LoadHunterResources(player.Hunter, scene);
                    }
                }
                if (anyActive)
                {
                    LoadCommonHunterResources(scene);
                }
            }
        }

        public static void LoadCommonHunterResources(Scene scene)
        {
            scene.LoadModel("doubleDamage_img");
            scene.LoadModel("alt_ice");
            scene.LoadModel("gunSmoke");
            scene.LoadModel("trail");
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
            // todo: other things done in load_enemy_data: enemy beams, boss stuff, pre-allocation(?)
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
    }
}
