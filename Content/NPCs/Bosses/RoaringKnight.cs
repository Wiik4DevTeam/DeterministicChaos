using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Projectiles;
using DeterministicChaos.Content.Projectiles;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.Systems;
using DeterministicChaos.Content.VFX;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class RoaringKnight : ModNPC
    {
        private enum MainState
        {
            NormalAttack = 0,
            PostAttackFollow = 1,
            SpherePhase = 2,
            MajorCentered = 3
        }

        private enum AttackKind
        {
            SeekingExplosives = 0,
            SpinningSlash = 1,
            SplitScreen = 2,
            SeekingKnives = 3,
            KnifeLattice = 4,
            AttackCentered = 6
        }

        private const int TrailLength = 12;

        private const int OrbitDuration = 50;
        private const int LingerAfterOrbit = 12;
        private const int DashDuration = 40;

        private const float OrbitRadiusStart = 18f;
        private const float OrbitRadiusEnd = 90f;

        private const float BallDashSpeed = 22f;

        private const int PostAttackFollowTime = 180;
        private const int CyclesBeforeMajor = 4;

        private const float NormalDesiredDist = 300f;
        private const float AttackDesiredDist = 550f;

        private const int AttackStartDelay = 60;

        // Returns the current attack start delay, halved when below 50% health.
        private int CurrentAttackDelay => NPC.life < NPC.lifeMax * 0.5f ? AttackStartDelay / 2 : AttackStartDelay;

        private const int FrameW = 100;
        private const int FrameH = 100;

        private const int ColIdleAndTransform = 0;
        private const int ColAttack1 = 1;
        private const int ColAttack2_Slash = 2;
        private const int ColMajorWindup = 3;
        private const int ColMajorFire = 4;

        private const int IdleRow = 0;

        private const int TransformStartRow = 1;
        private const int TransformFrameCount = 7;

        private const int Attack1StartRow = 0;
        private const int Attack1FrameCount = 5;

        private const int SlashAnimStartRow = 0;

        private const int SlashTelegraphFrames = 2;
        private const int SlashFireFrames = 2;
        
        private const int MajorWindupStartRow = 1;
        private const int MajorWindupFrameCount = 7;
        
        private const int MajorFireStartRow = 0;
        private const int MajorFireFrameCount = 2;

        // Major attack constants
        private const float MajorHealthThreshold = 0.15f; // 15% health
        private const int MajorPhase1Duration = 600; // Absorb phase (10 seconds)
        private const int MajorTransitionDuration = 120; // Delay between phases (2 seconds)
        private const int MajorPhase2Duration = 600;
        private const int MajorTotalDuration = 1320;
        private const float FinalStandDurationSeconds = 44.27f;
        private const float FinalStandDurationTicks = FinalStandDurationSeconds * 60f;

        private const float MajorSphereRadiusMin = 0f;
        private const float MajorSphereRadiusMax = 150f;
        
        // Shockwave VFX
        private const int ShockwaveInterval = 12; // 0.5 seconds

        private static readonly Vector2 SpritePivotOffset = new Vector2(0f, 0f);

        private bool hideKnightSprite;
        private bool transformToBall;
        private bool transformFromBall;

        private Vector2[] trailPos;
        private float[] trailRot;
        private int[] trailSpriteDir;
        private bool trailInit;

        private Vector2 ballPivot;
        private Vector2 ballTarget;
        private Vector2 dashTargetPos; // the position the boss dashes toward (opposite side of player)
        private Vector2 dashStartPos;  // position at the start of the current dash (for position lerp)
        private int sphereCyclesDone;
        private int sphereDashCount; // tracks multi-dash progress within one sphere phase
        private bool isDashing; // true during the dash portion of sphere phase (for contact damage)

        // Calamity difficulty cache
        private bool calDiffCached;
        private bool isRevengeance;
        private bool isDeath;

        private AttackKind currentAttack;
        private int usedAttackMask;
        private bool majorUsed;
        private Vector2 majorAnchor;

        // ── Arena ──
        private BossArenaSystem.ArenaBox arenaBox;
        private bool splitScreenArenaOverride;
        private bool dashArenaOverride;
        private bool finalStandArenaOverride;

        private Vector2 slashAnchor;
        private int slashTargetPlayer = -1;
        private float slashBaseAngle;
        private float slashOmega;
        private int slashWaveIndex;
        private int nextSlashWaveTick;
        private int slashWaveTimer;
        private int slashCycleTicks;
        private bool slashAnimLooping;

        private bool splitSpawned;
        private bool allPlayersDead;
        private bool hasEnteredEnrage;
        private bool hasPlayedDeathSound;

        private int knifeWaveIndex;
        private int nextKnifeWaveTick;

        private int latticeWaveIndex;
        private int nextLatticeWaveTick;

        // Major attack state
        private int majorStarSpawnTick;
        private float majorStarAngle;
        private int majorKnifeSpawnTick;
        private bool majorWindupComplete;
        private bool majorInFinalPhase;
        private bool majorRoarPlayed;
        private int coneVisualId = -1;
        private bool finalStandSequenceStarted;
        private int finalStandTimer;
        private int finalStandStartLife;

        public bool IsInFinalStand => finalStandSequenceStarted && State == MainState.MajorCentered;

        private enum AnimKind
        {
            Idle,
            Transform,
            Attack1,
            Attack2Slash,
            MajorWindup,
            MajorFire
        }

        private AnimKind animKind = AnimKind.Idle;
        private int animRow;
        private int animDir;
        private int animTick;
        private int animTicksPerFrame = 6;

        private MainState State
        {
            get => (MainState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float Timer => ref NPC.ai[1];
        private ref float SphereIndexAI => ref NPC.ai[2];

        private void SetFrame(int col, int row)
        {
            NPC.frame = new Rectangle(col * FrameW, row * FrameH, FrameW, FrameH);
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Frostburn] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.CursedInferno] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Ichor] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Bleeding] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Venom] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.ShadowFlame] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Daybreak] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.BetsysCurse] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Slow] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;

            NPC.damage = 54;
            NPC.defense = 80;
            NPC.lifeMax = 42000;

            NPC.boss = true;
            NPC.npcSlots = 15f;

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            NPC.noTileCollide = true;
            NPC.noGravity = true;

            NPC.knockBackResist = 0f;

            NPC.value = Item.buyPrice(gold: 5);

            NPC.aiStyle = -1;
            NPC.ShowNameOnHover = false;

            NPC.scale = 2.4f;

            NPC.BossBar = ModContent.GetInstance<global::DeterministicChaos.Content.UI.RoaringKnightBossBar>();
        }

        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (IsInFinalStand)
                modifiers.FinalDamage *= 0f;
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (IsInFinalStand)
                modifiers.FinalDamage *= 0f;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(ballPivot);
            writer.WriteVector2(ballTarget);
            writer.Write(sphereCyclesDone);
            writer.Write((int)currentAttack);
            writer.Write(usedAttackMask);
            writer.Write(majorUsed);
            writer.WriteVector2(majorAnchor);

            writer.WriteVector2(slashAnchor);
            writer.Write(slashTargetPlayer);
            writer.Write(slashBaseAngle);
            writer.Write(slashOmega);
            writer.Write(slashWaveIndex);
            writer.Write(nextSlashWaveTick);
            writer.Write(slashWaveTimer);
            writer.Write(slashCycleTicks);
            writer.Write(slashAnimLooping);

            writer.Write(splitSpawned);
            
            writer.Write(knifeWaveIndex);
            writer.Write(nextKnifeWaveTick);
            
            writer.Write(latticeWaveIndex);
            writer.Write(nextLatticeWaveTick);
            
            writer.Write(majorStarSpawnTick);
            writer.Write(majorStarAngle);
            writer.Write(majorKnifeSpawnTick);
            writer.Write(majorWindupComplete);
            writer.Write(majorInFinalPhase);
            writer.Write(finalStandSequenceStarted);
            writer.Write(finalStandTimer);
            writer.Write(finalStandStartLife);
            
            // Animation state sync
            writer.Write((int)animKind);
            writer.Write(animDir);
            writer.Write(animRow);
            writer.Write(hideKnightSprite);
            writer.Write(transformToBall);
            writer.Write(transformFromBall);
            writer.Write(sphereDashCount);
            writer.Write(isDashing);
            writer.WriteVector2(dashStartPos);
            writer.WriteVector2(dashTargetPos);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            ballPivot = reader.ReadVector2();
            ballTarget = reader.ReadVector2();
            sphereCyclesDone = reader.ReadInt32();
            currentAttack = (AttackKind)reader.ReadInt32();
            usedAttackMask = reader.ReadInt32();
            majorUsed = reader.ReadBoolean();
            majorAnchor = reader.ReadVector2();

            slashAnchor = reader.ReadVector2();
            slashTargetPlayer = reader.ReadInt32();
            slashBaseAngle = reader.ReadSingle();
            slashOmega = reader.ReadSingle();
            slashWaveIndex = reader.ReadInt32();
            nextSlashWaveTick = reader.ReadInt32();
            slashWaveTimer = reader.ReadInt32();
            slashCycleTicks = reader.ReadInt32();
            slashAnimLooping = reader.ReadBoolean();

            splitSpawned = reader.ReadBoolean();
            
            knifeWaveIndex = reader.ReadInt32();
            nextKnifeWaveTick = reader.ReadInt32();
            
            latticeWaveIndex = reader.ReadInt32();
            nextLatticeWaveTick = reader.ReadInt32();
            
            majorStarSpawnTick = reader.ReadInt32();
            majorStarAngle = reader.ReadSingle();
            majorKnifeSpawnTick = reader.ReadInt32();
            majorWindupComplete = reader.ReadBoolean();
            majorInFinalPhase = reader.ReadBoolean();
            finalStandSequenceStarted = reader.ReadBoolean();
            finalStandTimer = reader.ReadInt32();
            finalStandStartLife = reader.ReadInt32();
            
            // Animation state sync
            animKind = (AnimKind)reader.ReadInt32();
            animDir = reader.ReadInt32();
            animRow = reader.ReadInt32();
            hideKnightSprite = reader.ReadBoolean();
            transformToBall = reader.ReadBoolean();
            transformFromBall = reader.ReadBoolean();
            sphereDashCount = reader.ReadInt32();
            isDashing = reader.ReadBoolean();
            dashStartPos = reader.ReadVector2();
            dashTargetPos = reader.ReadVector2();
        }

        public override void AI()
        {
            // Enable background
            // (background is now part of the arena box)

            // Create arena once on first tick
            if (arenaBox == null)
            {
                NPC.TargetClosest(true);
                Player summoner = Main.player[NPC.target];
                Vector2 bottomCenter = summoner.Center + new Vector2(0f, 5f * 16f);
                arenaBox = BossArenaSystem.CreateArena(bottomCenter, 2000f, 2000f, () => !NPC.active);
                arenaBox.BackgroundTexturePath = "DeterministicChaos/Content/NPCs/Bosses/BossBG";
                arenaBox.BackgroundTint = Color.White;
                NPC.Center = arenaBox.Center;
            }

            // Shrink arena: 2000 at 50% HP → 1000 at 10% HP
            if (!splitScreenArenaOverride && !dashArenaOverride && !finalStandArenaOverride)
            {
                float hpRatio = NPC.life / (float)NPC.lifeMax;
                float shrinkProgress = MathHelper.Clamp((0.5f - hpRatio) / 0.4f, 0f, 1f);
                float currentHalf = MathHelper.Lerp(1000f, 650f, shrinkProgress);
                arenaBox.TargetHalfWidth = currentHalf;
                arenaBox.TargetHalfHeight = currentHalf;

                // Scale warp intensity with HP: 1.0 at full → 3.0 at 10% HP
                arenaBox.BackgroundWarpIntensity = MathHelper.Lerp(1f, 3f, shrinkProgress);
                arenaBox.BackgroundScrollSpeed = MathHelper.Lerp(200f, 500f, shrinkProgress);
            }
            
            if (!trailInit)
            {
                trailPos = new Vector2[TrailLength];
                trailRot = new float[TrailLength];
                trailSpriteDir = new int[TrailLength];
                for (int i = 0; i < TrailLength; i++)
                {
                    trailPos[i] = NPC.Center;
                    trailRot[i] = NPC.rotation;
                    trailSpriteDir[i] = 1;
                }
                trailInit = true;
            }

            if (!calDiffCached)
            {
                calDiffCached = true;
                if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
                {
                    Type calWorldType = cal.Code?.GetType("CalamityMod.World.CalamityWorld");
                    if (calWorldType != null)
                    {
                        var revengeField = calWorldType.GetField("revenge", BindingFlags.Public | BindingFlags.Static);
                        var deathField = calWorldType.GetField("death", BindingFlags.Public | BindingFlags.Static);
                        if (revengeField != null)
                            isRevengeance = (bool)revengeField.GetValue(null);
                        if (deathField != null)
                            isDeath = (bool)deathField.GetValue(null);
                    }
                }
            }

            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead)
                NPC.TargetClosest(faceTarget: true);

            Player player = Main.player[NPC.target];

            if (player.dead)
            {
                // Check if ALL players are dead, if so, despawn
                bool anyAlive = false;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead)
                    {
                        anyAlive = true;
                        break;
                    }
                }

                if (!anyAlive)
                {
                    allPlayersDead = true;
                    NPC.velocity.Y -= 0.4f;
                    NPC.EncourageDespawn(30);
                    CacheTrail();
                    return;
                }

                // Target player is dead but others are alive, retarget
                NPC.TargetClosest(faceTarget: true);
                player = Main.player[NPC.target];
            }

            EnsureSingleSphereExists();
            TryEnterMajorPhase();
            UpdateSphereVisibilityAndDamage();

            // Play hurt sound when dropping below 50% for the first time
            if (!hasEnteredEnrage && NPC.life < NPC.lifeMax * 0.5f)
            {
                hasEnteredEnrage = true;
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightHurt")
                {
                    Volume = 1.0f
                });
            }

            Lighting.AddLight(NPC.Center, 0.9f, 0.75f, 0.35f);

            switch (State)
            {
                case MainState.NormalAttack:
                    DoNormalAttack();
                    break;

                case MainState.PostAttackFollow:
                    DoPostAttackFollow();
                    break;

                case MainState.SpherePhase:
                    DoSpherePhase();
                    break;

                case MainState.MajorCentered:
                    DoMajorCentered();
                    break;
            }

            int faceDir = (player.Center.X >= NPC.Center.X) ? 1 : -1;
            NPC.direction = faceDir;
            NPC.spriteDirection = -faceDir;

            CacheTrail();
        }

        private void TryEnterMajorPhase()
        {
            // Don't enter if already in major phase
            if (State == MainState.MajorCentered)
                return;

            bool forceAtLowHp = NPC.life <= (int)(NPC.lifeMax * MajorHealthThreshold);
            bool reachedCycleCount = sphereCyclesDone >= CyclesBeforeMajor;

            // Only enter during sphere phase
            if (State != MainState.SpherePhase)
                return;

            // Check if we should transition (after 8th cycle or low HP during sphere phase)
            if (!forceAtLowHp && !reachedCycleCount)
                return;

            // We're in sphere phase, so transition to major instead of returning to normal
            Player targetPlayer = GetClosestPlayer();
            Vector2 targetPos = targetPlayer != null ? targetPlayer.Center : NPC.Center;

            // End dash arena override before locking majorAnchor.
            if (arenaBox != null && dashArenaOverride)
            {
                dashArenaOverride = false;
                arenaBox.LerpSpeed = 0.02f;
            }

            majorAnchor = arenaBox != null ? arenaBox.Center : (targetPos + new Vector2(0f, -500f));
            
            State = MainState.MajorCentered;
            Timer = 0f;
            NPC.velocity = Vector2.Zero;
            
            // Initialize major attack state
            majorStarSpawnTick = 0;
            majorStarAngle = 0f;
            majorKnifeSpawnTick = 0;
            majorWindupComplete = false;
            majorInFinalPhase = false;
            majorRoarPlayed = false;
            finalStandSequenceStarted = false;
            finalStandTimer = 0;
            finalStandStartLife = 0;
            // Only trigger final-stand music/drain when entering due to very low HP.
            // Entering via cycle count is a normal mid-fight major phase.
            if (forceAtLowHp)
                StartFinalStand();
            
            // CRITICAL: Force these flags to prevent sphere transformation
            hideKnightSprite = false; // Keep knight visible
            transformToBall = false; // Stop ball transformation completely
            transformFromBall = false; // Not transforming from ball
            
            // Start windup animation
            animKind = AnimKind.MajorWindup;
            animDir = +1;
            animRow = MajorWindupStartRow;
            animTick = 0;
            
            majorUsed = true;
            NPC.defense = (int)(NPC.defense/2);

            NPC.netUpdate = true;
        }

        private void EnsureSingleSphereExists()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int sphereType = ModContent.NPCType<RoaringKnightSphere>();

            int foundFirst = -1;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active)
                    continue;

                if (n.type == sphereType && (int)n.ai[0] == NPC.whoAmI)
                {
                    if (foundFirst == -1)
                        foundFirst = i;
                    else
                        n.active = false;
                }
            }

            if (foundFirst != -1)
            {
                SphereIndexAI = foundFirst;
                return;
            }

            int id = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, sphereType);
            Main.npc[id].ai[0] = NPC.whoAmI;
            Main.npc[id].netUpdate = true;
            SphereIndexAI = id;
            NPC.netUpdate = true;
        }

        private void UpdateSphereVisibilityAndDamage()
        {
            int idx = (int)SphereIndexAI;
            if (idx < 0 || idx >= Main.maxNPCs)
                return;

            NPC sphere = Main.npc[idx];
            if (!sphere.active)
                return;

            bool vulnerable = (State == MainState.SpherePhase && hideKnightSprite) || State == MainState.MajorCentered;

            sphere.dontTakeDamage = !vulnerable;
            sphere.chaseable = vulnerable;
            sphere.alpha = vulnerable ? 0 : 255;

            // High contact damage during dash, zero otherwise
            if (isDashing && vulnerable)
                sphere.damage = 120;
            else
                sphere.damage = 0;
        }

        private Player GetClosestPlayer()
        {
            Player best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                float d = Vector2.DistanceSquared(NPC.Center, p.Center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }

            return best ?? Main.player[NPC.target];
        }

        public override void OnKill()
        {
            // Mark as defeated for Boss Checklist (world-persistent)
            Systems.ERAMProgressSystem.RoaringKnightDefeated = true;
        }

        public override void BossLoot(ref string name, ref int potionType)
        {
            potionType = ItemID.GreaterHealingPotion;
        }
        
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Boss bag (Expert/Master/Revengeance/Death)
            var bagRule = new LeadingConditionRule(new BagDropCondition());
            bagRule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<KnightBossBag>()));
            npcLoot.Add(bagRule);

            // Trophy (10% chance on any difficulty)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Trophies.KnightTrophy>(), 10));

            // Relic (Master mode only)
            npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Trophies.KnightRelic>()));

            // Normal mode direct drops (bag handles Expert+/Rev+)
            var directDrop = new LeadingConditionRule(new DirectDropCondition());

            // Dark Fragments (30-40)
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<DarkFragment>(), 1, 30, 40));
            
            // Dark Shard weapon (guaranteed)
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<DarkShard>(), 1));

            // 2 random Roaring weapons (guaranteed)
            directDrop.OnSuccess(new FewFromOptionsNotScaledWithLuckDropRule(2, 1, 1,
                ModContent.ItemType<RoaringSword>(),
                ModContent.ItemType<RoaringBow>(),
                ModContent.ItemType<RoaringGun>(),
                ModContent.ItemType<RoaringTome>(),
                ModContent.ItemType<RoaringSummon>(),
                ModContent.ItemType<RoaringWhip>(),
                ModContent.ItemType<RoaringYoyo>()));

            // Always drop one of Rod of Stagnation or Roaring Shield.
            directDrop.OnSuccess(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<RodOfStagnation>(),
                ModContent.ItemType<RoaringShield>()));

            // Ring and Lens drop independently at random (25% each).
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<RoaringRing>(), 4));
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<RoaringLens>(), 4));

            npcLoot.Add(directDrop);
        }

        private Player GetRandomAlivePlayer()
        {
            int[] candidates = new int[Main.maxPlayers];
            int count = 0;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                candidates[count++] = i;
            }

            if (count <= 0)
                return Main.player[NPC.target];

            return Main.player[candidates[Main.rand.Next(count)]];
        }

        private AttackKind PickAttack()
        {
            AttackKind[] bag = new[] { 
                AttackKind.SeekingExplosives, 
                AttackKind.SpinningSlash, 
                AttackKind.SplitScreen,
                AttackKind.SeekingKnives,
                AttackKind.KnifeLattice
            };

            int allMask = 0;
            for (int i = 0; i < bag.Length; i++)
                allMask |= (1 << (int)bag[i]);

            if ((usedAttackMask & allMask) == allMask)
                usedAttackMask = 0;

            AttackKind chosen = bag[0];

            int tries = 0;
            while (tries++ < 40)
            {
                AttackKind candidate = bag[Main.rand.Next(bag.Length)];
                int bit = 1 << (int)candidate;

                if ((usedAttackMask & bit) == 0)
                {
                    chosen = candidate;
                    break;
                }
            }

            usedAttackMask |= 1 << (int)chosen;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;

            return chosen;
        }

        private int GetAttackDuration(AttackKind kind)
        {
            int dur;
            if (kind == AttackKind.SeekingExplosives) dur = 240;
            else if (kind == AttackKind.SpinningSlash) dur = 160 + 5 * 120;
            else if (kind == AttackKind.SplitScreen) dur = 500;
            else if (kind == AttackKind.SeekingKnives) dur = 540;
            else if (kind == AttackKind.KnifeLattice) dur = 600;
            else if (kind == AttackKind.AttackCentered) dur = 1200;
            else dur = 240;

            // Rev/Death enraged: 20% faster attacks
            if ((isRevengeance || isDeath) && NPC.life < NPC.lifeMax * 0.5f)
                dur = (int)(dur * 0.8f);

            return dur;
        }

        private void DoNormalAttack()
        {
            if (transformFromBall)
            {
                if (animRow <= TransformStartRow)
                {
                    transformFromBall = false;
                    animKind = AnimKind.Idle;
                }
                NPC.velocity *= 0.9f;
            }

            Player p = GetClosestPlayer();

            bool enragedAttack = NPC.life < NPC.lifeMax * 0.5f;
            int attackDelay = CurrentAttackDelay;

            Timer++;

            if (Timer == 1f)
            {
                currentAttack = PickAttack();
                if (currentAttack == AttackKind.SpinningSlash || currentAttack == AttackKind.SplitScreen)
                {
                    slashWaveTimer = 0;
                    slashWaveIndex = 0;
                    nextSlashWaveTick = (int)(attackDelay + 1f);
                    splitSpawned = false;
                }


                else if (currentAttack == AttackKind.SeekingKnives)
                {
                    knifeWaveIndex = 0;
                    nextKnifeWaveTick = (int)(attackDelay + 1f);
                }
                else if (currentAttack == AttackKind.KnifeLattice)
                {
                    latticeWaveIndex = 0;
                    nextLatticeWaveTick = (int)(attackDelay + 1f);
                }
            }

            if (Timer > attackDelay)
            {
                if (currentAttack == AttackKind.SeekingExplosives)
                {
                    if (animKind != AnimKind.Attack1)
                    {
                        animKind = AnimKind.Attack1;
                        animDir = +1;
                        animRow = Attack1StartRow;
                        animTick = 0;
                    }

                    DoSeekingExplosivesProjectiles();
                }
                else if (currentAttack == AttackKind.SpinningSlash)
                {
                    if (Timer == attackDelay + 1f)
                        InitSpinningSlash();

                    animKind = AnimKind.Attack2Slash;
                    slashAnimLooping = true;
                    DoSpinningSlash();
                }
                else if (currentAttack == AttackKind.SplitScreen)
                {
                    if (Timer == attackDelay + 1f)
                        InitSplitScreen();

                    animKind = AnimKind.Attack2Slash;
                    slashAnimLooping = false;
                    DoSplitScreen();
                }
                else if (currentAttack == AttackKind.SeekingKnives)
                {
                    if (animKind != AnimKind.Attack1)
                    {
                        animKind = AnimKind.Attack1;
                        animDir = +1;
                        animRow = Attack1StartRow;
                        animTick = 0;
                    }

                    DoSeekingKnives();
                }
                else if (currentAttack == AttackKind.KnifeLattice)
                {
                    if (animKind != AnimKind.Attack1)
                    {
                        animKind = AnimKind.Attack1;
                        animDir = +1;
                        animRow = Attack1StartRow;
                        animTick = 0;
                    }

                    DoKnifeLattice();
                }
            }

            float distToUse = NormalDesiredDist;
            if (Timer > attackDelay)
            {
                if (currentAttack == AttackKind.SpinningSlash || currentAttack == AttackKind.SplitScreen)
                    distToUse = 260f;
                else if (currentAttack == AttackKind.SeekingKnives || currentAttack == AttackKind.KnifeLattice)
                    distToUse = AttackDesiredDist;
                else
                    distToUse = AttackDesiredDist;
            }

            MoveKeepDistance(p, distToUse);

            if (Timer >= attackDelay + GetAttackDuration(currentAttack))
            {
                if (animKind == AnimKind.Attack2Slash)
                    animKind = AnimKind.Idle;

                if (animKind == AnimKind.Attack1)
                {
                    animDir = -1;
                    animRow = Attack1StartRow + Attack1FrameCount - 1;
                    animTick = 0;
                }

                State = MainState.PostAttackFollow;
                Timer = 0f;

                // Restore arena size after split screen
                if (currentAttack == AttackKind.SplitScreen)
                    splitScreenArenaOverride = false;

                NPC.netUpdate = true;
            }
        }

        private void DoPostAttackFollow()
        {
            Player p = GetClosestPlayer();

            Timer++;

            MoveKeepDistance(p, NormalDesiredDist);

            bool enraged = NPC.life < NPC.lifeMax * 0.5f;
            bool revOrDeath = isRevengeance || isDeath;
            int followTime;
            if (revOrDeath)
                followTime = enraged ? (isDeath ? 30 : 40) : (isDeath ? 45 : 60);
            else
                followTime = enraged ? PostAttackFollowTime / 2 : PostAttackFollowTime;

            // Extra grace after SpinningSlash and SeekingExplosives
            if (currentAttack == AttackKind.SpinningSlash || currentAttack == AttackKind.SeekingExplosives)
                followTime += 120;

            if (Timer >= followTime)
            {
                State = MainState.SpherePhase;
                Timer = 0f;
                sphereDashCount = 0;

                transformToBall = true;
                transformFromBall = false;
                hideKnightSprite = false;
                
                // Play transform sound (non-positional so all players hear it)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform")
                {
                    Volume = 0.8f
                });

                animKind = AnimKind.Transform;
                animDir = +1;
                animRow = TransformStartRow;
                animTick = 0;

                Player dashTarget = GetRandomAlivePlayer();

                ballPivot = NPC.Center;
                ballTarget = dashTarget.Center;

                NPC.velocity *= 0.1f;

                // Shrink arena to 500x500 for dash phase, player dodges left/right
                if (arenaBox != null)
                {
                    dashArenaOverride = true;
                    arenaBox.TargetHalfWidth = 500f;
                    arenaBox.TargetHalfHeight = 500f;
                    arenaBox.LerpSpeed = 0.06f; // fast transition
                }

                NPC.netUpdate = true;
            }
        }

        private void DoSpherePhase()
        {
            // Check for major phase transition before other logic
            TryEnterMajorPhase();
            
            if (State == MainState.MajorCentered)
                return;

            if (transformToBall && State == MainState.SpherePhase) // Added state check
            {
                int last = TransformStartRow + TransformFrameCount - 1;
                if (animRow >= last)
                {
                    transformToBall = false;
                    hideKnightSprite = true;
                    NPC.netUpdate = true;
                }
                else
                {
                    // Smoothly converge toward arena center during transform animation
                    if (dashArenaOverride && arenaBox != null)
                    {
                        NPC.Center = Vector2.Lerp(NPC.Center, arenaBox.Center, 0.08f);
                        NPC.velocity *= 0.85f;
                    }
                    else
                    {
                        NPC.velocity *= 0.9f;
                    }
                    return;
                }
            }

            Timer++;

            bool enraged = NPC.life < NPC.lifeMax * 0.5f;
            bool revOrDeath = isRevengeance || isDeath;

            // Dash parameters scale with difficulty and health
            int orbitDur, lingerDur, dashDur, maxDashes;
            float dashSpeed;

            if (revOrDeath)
            {
                // Rev/Death: no orbit windup, brief pause, faster when enraged
                orbitDur = 0;
                lingerDur = enraged ? (isDeath ? 10 : 15) : (isDeath ? 25 : 30);
                dashDur = enraged ? (isDeath ? 22 : 28) : (isDeath ? 32 : 48);
                dashSpeed = 0f; // unused, velocity computed from distance/dashDur
                maxDashes = enraged ? 6 : 4;
            }
            else if (enraged)
            {
                orbitDur = OrbitDuration * 2 / 3;
                lingerDur = 30;
                dashDur = 38;
                dashSpeed = 0f;
                maxDashes = 3;
            }
            else
            {
                orbitDur = OrbitDuration;
                lingerDur = 45;
                dashDur = 50;
                dashSpeed = 0f;
                maxDashes = 2;
            }

            // Orbit phase: spiral outward from pivot (skipped in Rev/Death)
            if (orbitDur > 0 && Timer <= orbitDur)
            {
                isDashing = false;
                NPC.localAI[0] = 0f;
                float t = Timer / orbitDur;
                float ease = t * t;
                float radius = MathHelper.Lerp(OrbitRadiusStart, OrbitRadiusEnd, ease);

                float loops = 1.25f;
                float angular = (MathHelper.TwoPi * loops) / orbitDur;
                float phase = NPC.whoAmI * 0.7f;

                float angle = phase + Timer * angular;
                Vector2 orbitPos = ballPivot + new Vector2(radius, 0f).RotatedBy(angle);

                Vector2 step = orbitPos - NPC.Center;
                NPC.velocity = step * 0.35f;
                NPC.Center += NPC.velocity;
                return;
            }

            // Linger phase: hold at current position (which is the dash start)
            if (Timer < orbitDur + lingerDur)
            {
                isDashing = false;
                NPC.localAI[0] = 0f;

                // On first linger tick of the very first dash, choose a starting position
                if (sphereDashCount == 0 && (Timer == orbitDur + 1 || (orbitDur == 0 && Timer == 1)))
                {
                    Player lingerTarget = GetRandomAlivePlayer();
                    float angleBias = Main.rand.NextBool()
                        ? Main.rand.NextFloat(-MathHelper.PiOver4 * 3f, -MathHelper.PiOver4)
                        : Main.rand.NextFloat(MathHelper.PiOver4, MathHelper.PiOver4 * 3f);
                    float dist = Main.rand.NextFloat(350f, 550f);
                    ballTarget = lingerTarget.Center + new Vector2((float)Math.Cos(angleBias), (float)Math.Sin(angleBias)) * dist;
                }

                // Smoothly decelerate and hold at ballTarget
                // If too far from player, drift ballTarget toward player to maintain 700px max distance
                Player trackTarget = GetRandomAlivePlayer();
                if (trackTarget != null)
                {
                    float distToPlayer = Vector2.Distance(ballTarget, trackTarget.Center);
                    if (distToPlayer > 400f)
                    {
                        Vector2 dir = (trackTarget.Center - ballTarget).SafeNormalize(Vector2.Zero);
                        ballTarget += dir * (distToPlayer - 400f) * 0.1f;
                    }
                }

                Vector2 toTarget = ballTarget - NPC.Center;
                float lingerT = (Timer - orbitDur) / (float)lingerDur;
                if (toTarget.Length() > 4f)
                {
                    float moveStrength = MathHelper.Lerp(0.08f, 0.2f, lingerT);
                    NPC.velocity = toTarget * moveStrength;
                }
                else
                {
                    NPC.velocity *= 0.85f;
                }

                return;
            }

            int dashTick = orbitDur + lingerDur;

            // Dash start: calculate opposite position through player and set constant velocity
            if (Timer == dashTick)
            {
                isDashing = true;
                NPC.localAI[0] = 1f;

                Player dashTarget = GetRandomAlivePlayer();

                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightDash")
                {
                    Volume = 0.8f
                });

                // Reflect position through player, capped at 700px from player
                Vector2 offset = NPC.Center - dashTarget.Center;
                Vector2 reflected = dashTarget.Center - offset;
                float reflectedDist = Vector2.Distance(reflected, dashTarget.Center);
                if (reflectedDist > 400f)
                    reflected = dashTarget.Center + (reflected - dashTarget.Center).SafeNormalize(Vector2.Zero) * 400f;
                dashTargetPos = reflected;
                dashStartPos = NPC.Center;

                // Velocity hint for initial frame (position lerp handles actual movement)
                Vector2 travel = dashTargetPos - dashStartPos;
                NPC.velocity = travel / dashDur;

                // Spawn shockwave lines at dash start
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int shockwaveType = ModContent.ProjectileType<ShockwaveLine>();
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                        float speed = Main.rand.NextFloat(12f, 20f);

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            dir * speed,
                            shockwaveType,
                            0,
                            0f,
                            Main.myPlayer,
                            speed,
                            Main.rand.NextFloat(15f, 30f)
                        );
                    }
                }

                NPC.netUpdate = true;
            }

            // During dash: compute velocity so Terraria's built-in position += velocity
            // lands the NPC at the correct smoothstepped position each tick.
            if (Timer > dashTick && Timer < dashTick + dashDur)
            {
                float dashProgress = (Timer - dashTick) / (float)dashDur;
                float easeFactor = dashProgress * dashProgress * (3f - 2f * dashProgress);
                Vector2 desiredPos = Vector2.Lerp(dashStartPos, dashTargetPos, easeFactor);
                // Let Terraria apply velocity to reach the desired position (self-correcting)
                NPC.velocity = desiredPos - NPC.Center;
            }

            // End of dash, boss is now at the reflected position
            if (Timer >= dashTick + dashDur)
            {
                isDashing = false;
                NPC.localAI[0] = 0f;
                NPC.velocity *= 0.1f;
                sphereDashCount++;

                // The boss's current position becomes the start of the next dash
                ballTarget = NPC.Center;

                // Loop back for more dashes, skip orbit, go to linger at current position
                if (sphereDashCount < maxDashes)
                {
                    Timer = orbitDur; // jump to linger phase start
                    NPC.netUpdate = true;
                    return;
                }

                hideKnightSprite = false;
                transformFromBall = true;
                NPC.localAI[0] = 0f;
                
                // Play transform sound (non-positional so all players hear it)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform")
                {
                    Volume = 0.8f
                });

                animKind = AnimKind.Transform;
                animDir = -1;
                animRow = TransformStartRow + TransformFrameCount - 1;
                animTick = 0;

                Timer = 0f;
                State = MainState.NormalAttack;

                sphereCyclesDone++;

                // Restore arena after dash phase
                if (arenaBox != null && dashArenaOverride)
                {
                    dashArenaOverride = false;
                    arenaBox.LerpSpeed = 0.02f; // normal speed
                }

                NPC.netUpdate = true;
            }
        }

        private void DoMajorCentered()
        {
            // If HP drops below final stand threshold during a normal center phase,
            // restart the phase as final stand immediately.
            if (!finalStandSequenceStarted && NPC.life <= (int)(NPC.lifeMax * MajorHealthThreshold))
            {
                Timer = 0f;
                majorStarSpawnTick = 0;
                majorStarAngle = 0f;
                majorKnifeSpawnTick = 0;
                majorWindupComplete = false;
                majorInFinalPhase = false;
                majorRoarPlayed = false;
                StartFinalStand();

                animKind = AnimKind.MajorWindup;
                animDir = +1;
                animRow = MajorWindupStartRow;
                animTick = 0;

                NPC.netUpdate = true;
            }

            Timer++;

            // Keep major anchor pinned to arena center in case another system shifts it.
            if (arenaBox != null)
                majorAnchor = arenaBox.Center;
            
            // FORCE these to stay false during entire major phase
            hideKnightSprite = false;
            transformToBall = false;
            transformFromBall = false;

            if (finalStandSequenceStarted)
                UpdateFinalStandCountdown();

            // Move to anchor position
            Vector2 toAnchor = majorAnchor - NPC.Center;
            float speed = 18f;
            float inertia = 10f;

            if (toAnchor.Length() > 8f)
            {
                Vector2 desiredVel = toAnchor.SafeNormalize(Vector2.Zero) * speed;
                NPC.velocity = (NPC.velocity * (inertia - 1f) + desiredVel) / inertia;
            }
            else
            {
                NPC.Center = majorAnchor;
                NPC.velocity *= 0.75f;
                if (NPC.velocity.Length() < 0.2f)
                    NPC.velocity = Vector2.Zero;
            }

            // Get sphere reference and reset its position to knight center
            int sphereIdx = (int)SphereIndexAI;
            NPC sphere = null;
            if (sphereIdx >= 0 && sphereIdx < Main.maxNPCs && Main.npc[sphereIdx].active)
            {
                sphere = Main.npc[sphereIdx];
                
                // Reset sphere to knight's position at start of major phase
                if (Timer == 1f)
                {
                    sphere.Center = NPC.Center;
                    sphere.velocity = Vector2.Zero;
                }
            }

            // Calculate sphere orbit radius based on health (expands exponentially as health lowers)
            float hp01 = NPC.life / (float)NPC.lifeMax;
            float healthProgress = 1f - hp01;
            float exponentialProgress = healthProgress * healthProgress * healthProgress;
            float sphereRadius = MathHelper.Lerp(MajorSphereRadiusMin, MajorSphereRadiusMax, exponentialProgress);

            // Move sphere with chaotic organic movement pattern
            if (sphere != null)
            {
                float baseSpeed = MathHelper.Lerp(0.015f, 0.03f, healthProgress);
                float t = (float)Timer * baseSpeed;
                
                // Orbiting component with direction changes
                float orbitSpeed = MathHelper.Lerp(0.8f, 1.8f, healthProgress);
                float directionSwitch = (float)System.Math.Sin(t * 0.15f);
                float orbitAngle = t * orbitSpeed * (directionSwitch > 0 ? 1f : -1f);
                
                // Left-right oscillation, smoothed to reduce jarring
                float horizontalSpeed = MathHelper.Lerp(0.6f, 1.2f, healthProgress);
                float horizontalAngle = t * horizontalSpeed + (float)System.Math.Sin(t * 0.5f) * MathHelper.PiOver2;
                float horizontalIntensity = MathHelper.Lerp(0.25f, 0.15f, healthProgress);
                float horizontalOffset = (float)System.Math.Sin(horizontalAngle) * sphereRadius * horizontalIntensity;
                
                // Up-down oscillation, smoothed to reduce jarring
                float verticalSpeed = MathHelper.Lerp(0.5f, 1.0f, healthProgress);
                float verticalAngle = t * verticalSpeed + (float)System.Math.Cos(t * 0.8f) * MathHelper.PiOver2;
                float verticalIntensity = MathHelper.Lerp(0.2f, 0.12f, healthProgress);
                float verticalOffset = (float)System.Math.Cos(verticalAngle) * sphereRadius * verticalIntensity;
                
                // Combine all components
                float finalAngle = orbitAngle + (float)System.Math.Sin(t * 1.2f) * 0.4f;
                
                // Dynamic radius variation
                float radiusWave1 = (float)System.Math.Sin(t * 1.8f) * sphereRadius * 0.12f;
                float radiusWave2 = (float)System.Math.Cos(t * 1.2f) * sphereRadius * 0.08f;
                float currentRadius = sphereRadius + radiusWave1 + radiusWave2;
                
                // Calculate base orbital position
                Vector2 baseOrbitPos = new Vector2(currentRadius, 0f).RotatedBy(finalAngle);
                
                // Apply horizontal and vertical offsets
                Vector2 perpendicular = new Vector2(-baseOrbitPos.Y, baseOrbitPos.X).SafeNormalize(Vector2.Zero);
                Vector2 targetPos = NPC.Center + baseOrbitPos + perpendicular * horizontalOffset + new Vector2(0, verticalOffset);
                
                // Smooth movement with reduced speed
                Vector2 toTarget = targetPos - sphere.Center;
                float moveSpeed = MathHelper.Lerp(0.12f, 0.25f, healthProgress);
                sphere.velocity = toTarget * moveSpeed;
            }

            // Phase 1: Absorb stars
            if (Timer <= MajorPhase1Duration)
            {
                DoMajorAbsorbPhase(sphere);
                
                // Play windup animation once (don't stop it, let AnimateStrip handle staying on last frame)
                if (!majorWindupComplete && animKind == AnimKind.MajorWindup)
                {
                    // Check if animation finished (reached last frame)
                    if (animRow >= MajorWindupStartRow + MajorWindupFrameCount - 1)
                    {
                        majorWindupComplete = true;
                    }
                }
            }
            // Transition: Wait between phases
            else if (Timer <= MajorPhase1Duration + MajorTransitionDuration)
            {
                // Hold on last frame of windup, don't spawn anything
            }
            // Phase 2: Expel stars
            else if (Timer <= MajorPhase1Duration + MajorTransitionDuration + MajorPhase2Duration || majorInFinalPhase)
            {
                if (!majorRoarPlayed && Timer >= MajorPhase1Duration + MajorTransitionDuration + 1)
                {
                    majorRoarPlayed = true;
                    
                    // Start fire animation
                    animKind = AnimKind.MajorFire;
                    animDir = +1;
                    animRow = MajorFireStartRow;
                    animTick = 0;
                    
                    // Play roar sound at start of expel phase (non-positional)
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightRoar")
                    {
                        Volume = 1.0f
                    });
                }
                
                DoMajorExpelPhase(sphere);
                
                if (!majorInFinalPhase && hp01 <= MajorHealthThreshold)
                {
                    majorInFinalPhase = true;
                    NPC.netUpdate = true;
                }
                
                if (majorInFinalPhase && isDeath)
                {
                    DoMajorFinalPhaseKnives();
                }
            }
            // Phase 3: Cleanup (only if above health threshold)
            else if (!majorInFinalPhase && !finalStandSequenceStarted)
            {
                DoMajorCleanup(sphere);
            }
        }

        private void StartFinalStand()
        {
            finalStandSequenceStarted = true;
            finalStandTimer = 0;
            finalStandStartLife = Math.Max(NPC.life, 1);

            // Play hurt sound when entering final phase
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightHurt")
            {
                Volume = 1.0f
            });

            // Override arena sizing: expand back to max and shrink to min by split-screen time
            finalStandArenaOverride = true;
            if (arenaBox != null)
            {
                arenaBox.TargetHalfWidth  = 1000f;
                arenaBox.TargetHalfHeight = 1000f;
                arenaBox.LerpSpeed = 0.02f;
            }
        }

        private void UpdateFinalStandCountdown()
        {
            if (!finalStandSequenceStarted)
                StartFinalStand();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            finalStandTimer++;
            float progress = MathHelper.Clamp(finalStandTimer / FinalStandDurationTicks, 0f, 1f);
            int targetLife = (int)Math.Ceiling(MathHelper.Lerp(finalStandStartLife, 0f, progress));

            if (NPC.life > targetLife)
                NPC.life = targetLife;

            // Arena: expand at start, slowly shrink to minimum by the time split slashes begin (T-10s)
            if (finalStandArenaOverride && arenaBox != null)
            {
                float shrinkWindow = FinalStandDurationTicks - 10f * 60f; // ticks before slashes
                float shrinkT = MathHelper.Clamp(finalStandTimer / shrinkWindow, 0f, 1f);
                float half = MathHelper.Lerp(1000f, 550f, shrinkT);
                arenaBox.TargetHalfWidth  = half;
                arenaBox.TargetHalfHeight = half;
                arenaBox.BackgroundWarpIntensity = MathHelper.Lerp(3f, 5f, shrinkT);
                arenaBox.BackgroundScrollSpeed   = MathHelper.Lerp(500f, 800f, shrinkT);
            }

            DoFinalStandSplitScreen();

            if (progress >= 1f)
            {
                NPC.life = 0;
                NPC.checkDead();
                NPC.netUpdate = true;
            }

            // Play death sound 1 second before actual death
            float timeRemaining = FinalStandDurationTicks - finalStandTimer;
            if (!hasPlayedDeathSound && timeRemaining <= 60f)
            {
                hasPlayedDeathSound = true;
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightDeath")
                {
                    Volume = 1.0f
                });
            }
        }

        private void DoMajorAbsorbPhase(NPC sphere)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || sphere == null)
                return;

            // Spawn 2 stars periodically, interval increases toward end of phase
            int spawnInterval = 12;
            
            if ((int)Timer >= majorStarSpawnTick)
            {
                // Calculate angle rotation amount (decreases over time)
                float progress = Timer / MajorPhase1Duration;
                float angleStep = MathHelper.Lerp(MathHelper.Pi * 0.3f, MathHelper.Pi * 0.02f, progress);

                // Interval widens toward end: 12 ticks early → 20 ticks late (bigger gaps)
                spawnInterval = (int)MathHelper.Lerp(12f, 20f, progress);
                majorStarSpawnTick = (int)Timer + spawnInterval;
                
                // Spawn 2 stars from opposite sides
                for (int i = 0; i < 2; i++)
                {
                    float angle = majorStarAngle + (i * MathHelper.Pi);
                    
                    // Spawn much farther away from sphere
                    float spawnDist = 2000f;
                    Vector2 spawnPos = majorAnchor + new Vector2(spawnDist, 0f).RotatedBy(angle);
                    
                    // Direction towards arena center
                    Vector2 dir = (majorAnchor - spawnPos).SafeNormalize(Vector2.UnitX);
                    float speed = 22f;
                    
                    int proj = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        dir * speed,
                        ModContent.ProjectileType<Projectile_Star>(),
                        54,
                        0f,
                        Main.myPlayer
                    );
                    
                    if (proj >= 0 && proj < Main.maxProjectiles)
                    {
                        Main.projectile[proj].ai[0] = NPC.whoAmI;
                        Main.projectile[proj].ai[1] = -1f;
                        Main.projectile[proj].ai[2] = 0f;
                        Main.projectile[proj].timeLeft = 300;
                        Main.projectile[proj].netUpdate = true;
                    }
                }
                
                majorStarAngle += angleStep;
            }
        }

        private void DoMajorExpelPhase(NPC sphere)
        {
            // Screen shake during expulsion, must run on clients, not server
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0)
            {
                Main.instance.CameraModifiers.Add(new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                    NPC.Center,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f)).SafeNormalize(Vector2.UnitX),
                    6f, // moderate strength
                    6f, // vibration speed
                    10, // short duration for continuous effect
                    3000f // max distance
                ));
            }

            if (Main.netMode == NetmodeID.MultiplayerClient || sphere == null)
                return;
            
            // Spawn shockwave VFX every 0.5 seconds during expel phase
            int expelPhaseTimer = (int)Timer - (MajorPhase1Duration + MajorTransitionDuration);
            if (expelPhaseTimer % ShockwaveInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<Shockwave>(),
                    0,
                    0f,
                    Main.myPlayer,
                    Main.rand.NextFloat(0f, MathHelper.TwoPi)
                );
            }

            // Spawn 2 stars periodically
            int spawnInterval = 10;
            
            if ((int)Timer >= majorStarSpawnTick && !(finalStandSequenceStarted && finalStandTimer >= FinalStandDurationTicks - 10f * 60f))
            {
                majorStarSpawnTick = (int)Timer + spawnInterval;
                
                // Calculate angle rotation amount (same pattern as absorb)
                int starPhaseTimer = (int)Timer - (MajorPhase1Duration + MajorTransitionDuration);
                float progress = starPhaseTimer / (float)MajorPhase2Duration;

                spawnInterval = (int)MathHelper.Lerp(10f, 4f, progress);

                if (majorInFinalPhase)
                    progress = 1f; // Keep at minimum rotation in final phase
                    
                float angleStep = MathHelper.Pi * 0.3f;
                
                // Spawn 2 stars from arena center
                for (int i = 0; i < 2; i++)
                {
                    float angle = majorStarAngle + (i * MathHelper.Pi);
                    
                    // Direction away from center
                    Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                    float speed = 13f;
                    
                    Vector2 spawnPos = majorAnchor;
                    
                    int proj = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        dir * speed,
                        ModContent.ProjectileType<Projectile_Star>(),
                        54,
                        0f,
                        Main.myPlayer
                    );
                    
                    // Spawn 2 fast shockwaves from knight each star spawn
                    int shockwaveType = ModContent.ProjectileType<ShockwaveLine>();
                    for (int j = 0; j < 2; j++)
                    {
                        float shockAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        Vector2 shockDir = new Vector2(1f, 0f).RotatedBy(shockAngle);
                        float shockSpeed = Main.rand.NextFloat(20f, 30f); // Fast speed
                        
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            shockDir * shockSpeed,
                            shockwaveType,
                            0,
                            0f,
                            Main.myPlayer,
                            shockSpeed,
                            Main.rand.NextFloat(10f, 20f) // Short length
                        );
                    }
                    
                    if (proj >= 0 && proj < Main.maxProjectiles)
                    {
                        // Calculate time until end of phase for detonation timer
                        int fuseTime = (MajorPhase1Duration + MajorTransitionDuration + MajorPhase2Duration) - (int)Timer;
                        if (majorInFinalPhase)
                            fuseTime = 300; // Long timer in final phase
                        
                        Main.projectile[proj].ai[0] = 0f; // Reset explosion counter
                        Main.projectile[proj].ai[1] = 2f; // Explode mode
                        Main.projectile[proj].ai[2] = fuseTime; // Fuse = time until end
                        Main.projectile[proj].timeLeft = fuseTime + 60;
                        Main.projectile[proj].netUpdate = true;
                    }
                }
                
                majorStarAngle += angleStep;
            }
        }

        private void DoMajorFinalPhaseKnives()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int knifeInterval = 120; // 1 second
            
            if ((int)Timer >= majorKnifeSpawnTick)
            {
                majorKnifeSpawnTick = (int)Timer + knifeInterval;
                
                Player target = GetRandomAlivePlayer();
                int targetIndex = target.whoAmI;
                
                int type = ModContent.ProjectileType<SeekingKnife>();
                
                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center,
                    Vector2.Zero,
                    type,
                    0,
                    0f,
                    Main.myPlayer,
                    targetIndex
                );
                
                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
        }

        private void DoFinalStandSplitScreen()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // First 8 seconds: rotating split-screen; last 2 seconds: nothing
            float windowStart = FinalStandDurationTicks - 10f * 60f;
            float elapsed = finalStandTimer - windowStart;
            float activeWindow = 8f * 60f; // 480 ticks

            if (elapsed < 0f || elapsed >= activeWindow)
                return;

            // Spawn a pair every 8 ticks (~60 pairs over 8 seconds)
            const int spawnInterval = 8;
            if ((int)elapsed % spawnInterval != 0)
                return;

            Vector2 anchor = arenaBox != null ? arenaBox.Center : NPC.Center;

            // Sweep CCW one full rotation over the 8-second window, starting vertically (PiOver2)
            float t = elapsed / activeWindow;
            float baseAngle = MathHelper.PiOver2 - MathHelper.TwoPi * t;

            // 5-degree CCW rotation per indicator (as fraction of 90 degrees, negative = CCW on screen)
            const float microRotation = -(5f / 90f);

            int type = ModContent.ProjectileType<SliceIndicator>();
            int dmg = 85;

            int id0 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor, Vector2.Zero, type, 0, 0f, Main.myPlayer, dmg, baseAngle, microRotation);
            if (id0 >= 0 && id0 < Main.maxProjectiles) Main.projectile[id0].netUpdate = true;

            int id1 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor, Vector2.Zero, type, 0, 0f, Main.myPlayer, dmg, baseAngle + MathHelper.Pi, microRotation);
            if (id1 >= 0 && id1 < Main.maxProjectiles) Main.projectile[id1].netUpdate = true;
        }

        private void DoMajorCleanup(NPC sphere)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int cleanupStart = MajorPhase1Duration + MajorTransitionDuration + MajorPhase2Duration + 60;

            // Explode all remaining Projectile_Stars
            if (Timer == cleanupStart + 1)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != ModContent.ProjectileType<Projectile_Star>())
                        continue;
                    
                    // Trigger explosion by setting ai[1] to explode mode
                    p.ai[1] = 2f; // Explode mode
                    p.ai[2] = 1f; // Trigger explosion timer
                    p.netUpdate = true;
                }
                
                // Make sphere immune and invisible
                if (sphere != null)
                {
                    sphere.dontTakeDamage = true;
                    sphere.alpha = 255;
                }
            }
            
            // Spawn final screen split attack
            if (Timer == cleanupStart + 120)
            {
                Player target = GetRandomAlivePlayer();
                Vector2 anchor = target.Center;
                float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                
                int type = ModContent.ProjectileType<SliceIndicator>();
                int dmg = 85;
                
                float rotationDirection = Main.rand.NextBool() ? 1f : -1f;
                
                int id0 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor, Vector2.Zero, type, 0, 0f, Main.myPlayer, dmg, baseAngle, rotationDirection);
                if (id0 >= 0) Main.projectile[id0].netUpdate = true;
                
                int id1 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor, Vector2.Zero, type, 0, 0f, Main.myPlayer, dmg, baseAngle + MathHelper.Pi, rotationDirection);
                if (id1 >= 0) Main.projectile[id1].netUpdate = true;
            }
            
            // Return to normal state
            if (Timer >= cleanupStart + 180)
            {
                Timer = 0f;
                State = MainState.NormalAttack;
                sphereCyclesDone = 0;
                hideKnightSprite = false;
                
                // Transform back from ball
                transformFromBall = true;
                animKind = AnimKind.Transform;
                animDir = -1;
                animRow = TransformStartRow + TransformFrameCount - 1;
                animTick = 0;
                
                // Reset sphere
                if (sphere != null)
                {
                    sphere.dontTakeDamage = false;
                    sphere.alpha = 255;
                }
                
                NPC.netUpdate = true;
            }
        }

        private void InitSpinningSlash()
        {
            Player target = GetRandomAlivePlayer();
            slashTargetPlayer = target.whoAmI;
            slashAnchor = target.Center;

            slashWaveIndex = 0;
            slashWaveTimer = 0;
            nextSlashWaveTick = (int)Timer;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            int hold = (int)MathHelper.Lerp(30f, 5f, ramp * ramp);
            slashCycleTicks = 60 + 8 + hold;

            slashOmega = MathHelper.Lerp(0.22f, 0.42f, ramp);
            if (Main.rand.NextBool())
                slashOmega *= -1f;

            slashBaseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            nextSlashWaveTick = (int)(CurrentAttackDelay + 1f);

            NPC.netUpdate = true;
        }

        private void InitSplitScreen()
        {
            Player target = GetRandomAlivePlayer();
            slashTargetPlayer = target.whoAmI;
            slashAnchor = target.Center;

            slashWaveIndex = 0;
            slashWaveTimer = 0;
            
            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);
            
            int hold = 30;
            slashCycleTicks = 60 + 8 + hold;
            
            nextSlashWaveTick = (int)(CurrentAttackDelay + 1f);
            
            // Shrink arena to 1000x1000 for split screen
            splitScreenArenaOverride = true;
            if (arenaBox != null)
            {
                arenaBox.TargetHalfWidth = 500f;
                arenaBox.TargetHalfHeight = 500f;
            }

            NPC.netUpdate = true;
        }

        private void DoSpinningSlash()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int startTick = CurrentAttackDelay + 1;
            if ((int)Timer < startTick)
                return;

            slashWaveTimer++;

            if ((int)Timer < nextSlashWaveTick)
                return;

            // Reset wave timer when spawning new wave
            slashWaveTimer = 0;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            int hold = (int)MathHelper.Lerp(30f, 5f, ramp * ramp);
            slashCycleTicks = 60 + 8 + hold;

            int interval = slashCycleTicks;

            nextSlashWaveTick = (int)Timer + interval;

            int totalAdd = (int)MathHelper.Lerp(2f, 6f, ramp) * 2;

            int total = totalAdd + slashWaveIndex * 2;

            if(total >= 12)
                total = 12;

            slashWaveIndex++;

            // Track the target player's current position
            if (slashTargetPlayer >= 0 && slashTargetPlayer < Main.maxPlayers && Main.player[slashTargetPlayer].active && !Main.player[slashTargetPlayer].dead)
                slashAnchor = Main.player[slashTargetPlayer].Center;

            // Use tracked anchor centered on the target player
            Vector2 anchor = slashAnchor;

            int dmg = 85;
            int type = ModContent.ProjectileType<SlashIndicator>();

            // Random rotation direction: +1 (counterclockwise) or -1 (clockwise)
            float rotationDirection = Main.rand.NextBool() ? 1f : -1f;

            for (int i = 0; i < total; i++)
            {
                float ang = slashBaseAngle + (MathHelper.TwoPi * i / total);

                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    anchor,
                    new Vector2(rotationDirection, 0f),
                    type,
                    0,
                    0f,
                    Main.myPlayer,
                    dmg,
                    ang
                );

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
            
        }

        private void DoSplitScreen()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            slashWaveTimer++;

            if ((int)Timer < nextSlashWaveTick)
                return;

            if (slashWaveIndex >= 5)
                return;

            slashWaveTimer = 0;
            slashWaveIndex++;

            // Center on arena
            Vector2 anchor = arenaBox != null ? arenaBox.Center : NPC.Center;

            // Pick from straight and diagonal angles
            float[] angleOptions = new float[]
            {
                0f,                          // horizontal
                MathHelper.PiOver2,          // vertical
                MathHelper.PiOver4,          // 45 degrees
                MathHelper.PiOver4 * 3f      // 135 degrees
            };
            float baseAngle = angleOptions[Main.rand.Next(angleOptions.Length)];
            // Add small random variance
            baseAngle += Main.rand.NextFloat(-0.15f, 0.15f);

            int type = ModContent.ProjectileType<SliceIndicator>();
            int dmg = 85;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            bool belowHalf = hp01 < 0.5f;

            int sets = belowHalf ? 2 : 1;

            for (int s = 0; s < sets; s++)
            {
                float angleOffset = s * MathHelper.PiOver2;

                float a0 = baseAngle + angleOffset;
                float a1 = baseAngle + MathHelper.Pi + angleOffset;

                float rotationDirection = Main.rand.NextBool() ? 1f : -1f;

                int id0 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor.X, anchor.Y, 0f, 0f, type, 0, 0f, Main.myPlayer, dmg, a0, rotationDirection);
                if (id0 >= 0 && id0 < Main.maxProjectiles) Main.projectile[id0].netUpdate = true;

                int id1 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor.X, anchor.Y, 0f, 0f, type, 0, 0f, Main.myPlayer, dmg, a1, rotationDirection);
                if (id1 >= 0 && id1 < Main.maxProjectiles) Main.projectile[id1].netUpdate = true;
            }

            nextSlashWaveTick = (int)Timer + slashCycleTicks;
        }

        private void DoSeekingKnives()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if ((int)Timer < nextKnifeWaveTick)
                return;

            // Calculate health-based parameters
            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            int totalKnives = (int)MathHelper.Lerp(10f, 30f, ramp);

            // Stop spawning after reaching total
            if (knifeWaveIndex >= totalKnives)
                return;

            // Always spawn 1 knife per wave (enrage uses faster intervals instead)
            int knivesPerWave = 1;

            // Sound is now played by SeekingKnife on its first AI tick (client-safe)

            for (int i = 0; i < knivesPerWave; i++)
            {
                // Spawn knife on random player
                Player target = GetRandomAlivePlayer();
                int targetIndex = target.whoAmI;

                int type = ModContent.ProjectileType<SeekingKnife>();
                
                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center,
                    Vector2.Zero,
                    type,
                    0,
                    0f,
                    Main.myPlayer,
                    targetIndex
                );

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }

            knifeWaveIndex++;

            // Calculate next spawn time with speed ramp
            // Interval shrinks as health decreases: 80 -> 40 ticks normally, down to 20 when enraged
            float minInterval = hp01 < 0.5f ? 28f : 40f;
            float maxInterval = 80f;
            
            float progress = knifeWaveIndex / (float)totalKnives;
            float intervalT = 1f - (1f - progress) * (1f - progress);
            int interval = (int)MathHelper.Lerp(maxInterval, minInterval, intervalT);

            nextKnifeWaveTick = (int)Timer + interval;
        }

        private void DoKnifeLattice()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if ((int)Timer < nextLatticeWaveTick)
                return;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            int totalWaves = (int)MathHelper.Lerp(5f, 12f, ramp);

            if (latticeWaveIndex >= totalWaves)
                return;

            int type = ModContent.ProjectileType<LatticeKnife>();

            int knivesPerWave = 19 + (int)(ramp * 3f);

            // Arena bounds for spawning outside
            float arenaLeft = arenaBox != null ? arenaBox.Center.X - arenaBox.HalfWidth : NPC.Center.X - 1000f;
            float arenaRight = arenaBox != null ? arenaBox.Center.X + arenaBox.HalfWidth : NPC.Center.X + 1000f;
            float arenaTop = arenaBox != null ? arenaBox.Center.Y - arenaBox.HalfHeight : NPC.Center.Y - 1000f;
            float arenaBottom = arenaBox != null ? arenaBox.Center.Y + arenaBox.HalfHeight : NPC.Center.Y + 1000f;
            Vector2 arenaCenter = arenaBox != null ? arenaBox.Center : NPC.Center;
            float arenaW = arenaRight - arenaLeft;
            float arenaH = arenaBottom - arenaTop;
            float margin = 20f; // spawn right at the arena edge (slightly inward)

            for (int i = 0; i < knivesPerWave; i++)
            {
                // Pick a random edge (0=top, 1=bottom, 2=left, 3=right)
                int edge = Main.rand.Next(4);
                Vector2 spawnPos;

                switch (edge)
                {
                    case 0: // top
                        spawnPos = new Vector2(Main.rand.NextFloat(arenaLeft, arenaRight), arenaTop - margin);
                        break;
                    case 1: // bottom
                        spawnPos = new Vector2(Main.rand.NextFloat(arenaLeft, arenaRight), arenaBottom + margin);
                        break;
                    case 2: // left
                        spawnPos = new Vector2(arenaLeft - margin, Main.rand.NextFloat(arenaTop, arenaBottom));
                        break;
                    default: // right
                        spawnPos = new Vector2(arenaRight + margin, Main.rand.NextFloat(arenaTop, arenaBottom));
                        break;
                }

                // Target a random position inside the arena
                Vector2 targetPos = arenaCenter + new Vector2(
                    Main.rand.NextFloat(-arenaW * 0.4f, arenaW * 0.4f),
                    Main.rand.NextFloat(-arenaH * 0.4f, arenaH * 0.4f)
                );

                Vector2 dashDir = (targetPos - spawnPos).SafeNormalize(Vector2.UnitX);

                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    type,
                    80,
                    0f,
                    Main.myPlayer,
                    -1f,              // ai[0] = -1 signals arena-based spawn
                    dashDir.X,        // ai[1] = dash direction X
                    dashDir.Y         // ai[2] = dash direction Y
                );

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }

            latticeWaveIndex++;

            int minInterval = 80;
            int maxInterval = 150;

            float progress = latticeWaveIndex / (float)totalWaves;
            float intervalT = 1f - (1f - progress) * (1f - progress);
            int interval = (int)MathHelper.Lerp(maxInterval, minInterval, intervalT);

            nextLatticeWaveTick = (int)Timer + interval;
        }

        private void DoSeekingExplosivesProjectiles()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float coneDegrees = 36f;

            // Spawn cone visual effect on first tick
            if (Timer == CurrentAttackDelay + 1)
            {
                // Sound is now played by ConeVisualEffect on its first AI tick (client-safe)
                
                coneVisualId = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<VFX.ConeVisualEffect>(),
                    0,
                    0f,
                    Main.myPlayer,
                    NPC.whoAmI, // ai[0] = NPC index
                    coneDegrees // ai[1] = cone angle
                );
                
                if (coneVisualId >= 0 && coneVisualId < Main.maxProjectiles)
                {
                    Main.projectile[coneVisualId].timeLeft = GetAttackDuration(AttackKind.SeekingExplosives);
                    Main.projectile[coneVisualId].netUpdate = true;
                }
            }

            int fireRate = 7;
            if (((int)Timer % fireRate) != 0)
                return;

            Player p = GetRandomAlivePlayer();

            Vector2 from = NPC.Center;
            Vector2 baseDir = (p.Center - from).SafeNormalize(Vector2.UnitX);

            int stars = 1;

            float cone = MathHelper.ToRadians(coneDegrees);

            float speed = 20f;

            for (int i = 0; i < stars; i++)
            {
                float ang = Main.rand.NextFloat(-cone * 0.5f, cone * 0.5f);

                Vector2 dir = baseDir.RotatedBy(ang);

                float sideJitter = Main.rand.NextFloat(-0.12f, 0.12f);
                dir = (dir + baseDir.RotatedBy(MathHelper.PiOver2) * sideJitter).SafeNormalize(Vector2.UnitX);

                Vector2 v = dir * speed;

                int proj = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    from,
                    v,
                    ModContent.ProjectileType<Projectile_Star>(),
                    28,
                    0f,
                    Main.myPlayer
                );

                if (proj >= 0 && proj < Main.maxProjectiles)
                {
                    Main.projectile[proj].ai[1] = 2f;
                    int fuseTime = 120 + (GetAttackDuration(AttackKind.SeekingExplosives) - (int)Timer);
                    Main.projectile[proj].ai[2] = fuseTime;
                    Main.projectile[proj].timeLeft = fuseTime + 60;
                    Main.projectile[proj].netUpdate = true;
                    
                    // Below half health: spawn lattice knives when stars are about to explode
                    float hp01 = NPC.life / (float)NPC.lifeMax;
                    if (hp01 < 0.5f && fuseTime <= 15)
                    {
                        SpawnLatticeKnivesForStar(Main.projectile[proj]);
                    }
                }
            }
        }

        private void SpawnLatticeKnivesForStar(Projectile star)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Player target = GetRandomAlivePlayer();
            int targetIndex = target.whoAmI;
            int type = ModContent.ProjectileType<LatticeKnife>();
            
            // Spawn 6 lattice knives around the star's explosion point
            for (int i = 0; i < 6; i++)
            {
                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    star.Center,
                    Vector2.Zero,
                    type,
                    40,
                    0f,
                    Main.myPlayer,
                    targetIndex
                );

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
        }

        private void SpawnSeekingProjectilesAtPoint(Vector2 position, int count)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int seekerType = ModContent.ProjectileType<Projectile_Seeking>();
            
            for (int i = 0; i < count; i++)
            {
                float angle = (MathHelper.TwoPi * i) / count;
                Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                float speed = 4f; // Start slower
                
                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    position,
                    dir * speed,
                    seekerType,
                    54,
                    0f,
                    Main.myPlayer
                );
                
                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
        }

        private void MoveKeepDistance(Player p, float desiredDist)
        {
            Vector2 toPlayer = p.Center - NPC.Center;

            Vector2 desiredPos;
            if (toPlayer.Length() < 0.001f)
            {
                desiredPos = p.Center + new Vector2(-desiredDist, 0f);
            }
            else
            {
                Vector2 away = -Vector2.Normalize(toPlayer);
                desiredPos = p.Center + away * desiredDist;
            }

            MoveToPoint(desiredPos, 16.0f, 12f);
        }

        private void MoveToPoint(Vector2 desiredPos, float speed, float inertia)
        {
            Vector2 toDesired = desiredPos - NPC.Center;
            Vector2 desiredVel = toDesired.SafeNormalize(Vector2.Zero) * speed;

            if (toDesired.Length() < 80f)
                desiredVel *= 0.25f;

            NPC.velocity = (NPC.velocity * (inertia - 1f) + desiredVel) / inertia;
        }

        private void CacheTrail()
        {
            for (int i = TrailLength - 1; i > 0; i--)
            {
                trailPos[i] = trailPos[i - 1];
                trailRot[i] = trailRot[i - 1];
                trailSpriteDir[i] = trailSpriteDir[i - 1];
            }

            trailPos[0] = NPC.Center;
            trailRot[0] = NPC.rotation;
            trailSpriteDir[0] = NPC.spriteDirection == 0 ? 1 : NPC.spriteDirection;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Red tint when Revengeance/Death and below 50% HP
            bool revDeathEnraged = (isRevengeance || isDeath) && NPC.life < NPC.lifeMax * 0.5f;
            Color spriteColor = revDeathEnraged ? new Color(255, 200, 200) : Color.White;

            // During major attack, always show the knight sprite using proper animations
            // hideKnightSprite should NEVER be true during MajorCentered state
            if (State == MainState.MajorCentered)
            {
                // Force knight to be visible during major attack
                Texture2D tex = TextureAssets.Npc[Type].Value;

                Vector2 origin = new Vector2(FrameW * 0.5f, FrameH * 0.5f);
                Vector2 basePos = NPC.Center - screenPos;
                basePos.Y += NPC.gfxOffY;
                basePos -= SpritePivotOffset * NPC.scale;

                SpriteEffects mainFx = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(tex, basePos, NPC.frame, spriteColor, NPC.rotation, origin, NPC.scale, mainFx, 0f);

                return false;
            }
            
            if (hideKnightSprite)
                return false;

            // Ensure trail arrays are initialized before drawing
            if (trailPos == null || trailRot == null || trailSpriteDir == null)
                return true;

            Texture2D tex2 = TextureAssets.Npc[Type].Value;

            Vector2 origin2 = new Vector2(FrameW * 0.5f, FrameH * 0.5f);
            Vector2 basePos2 = NPC.Center - screenPos;
            basePos2.Y += NPC.gfxOffY;
            basePos2 -= SpritePivotOffset * NPC.scale;

            bool enragedTrail = NPC.life < NPC.lifeMax * 0.5f;

            for (int i = TrailLength - 1; i >= 1; i--)
            {
                float t = i / (float)TrailLength;
                float alpha = (1f - t) * 0.45f;

                Vector2 pos = trailPos[i] - screenPos;
                pos.Y += NPC.gfxOffY;
                pos -= SpritePivotOffset * NPC.scale;

                float trailRoti = trailRot[i];
                float trailScale = NPC.scale;

                if (enragedTrail)
                {
                    // Animalistic afterimage: jitter increases with trail age
                    float jitterStrength = t * 14f;
                    float seed = i * 73.7f + (float)Main.GameUpdateCount * 0.4f;
                    pos.X += (float)System.Math.Sin(seed) * jitterStrength;
                    pos.Y += (float)System.Math.Cos(seed * 1.3f) * jitterStrength;
                    trailRoti += (float)System.Math.Sin(seed * 0.9f) * t * 0.3f;
                    trailScale *= MathHelper.Lerp(1f, 0.85f + (float)System.Math.Sin(seed * 1.7f) * 0.15f, t);
                    alpha *= 1.3f; // Slightly more visible
                }
                else
                {
                    float drift = i * 0.45f;
                    pos.X += trailSpriteDir[i] * drift;
                }

                SpriteEffects fx = (trailSpriteDir[i] == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(tex2, pos, NPC.frame, spriteColor * alpha, trailRoti, origin2, trailScale, fx, 0f);
            }

            // Draw main sprite at NPC.Center (interpolated by engine for smooth rendering).
            SpriteEffects mainFx2 = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.Draw(tex2, basePos2, NPC.frame, spriteColor, NPC.rotation, origin2, NPC.scale, mainFx2, 0f);

            return false;
        }

        public override void FindFrame(int frameHeight)
        {
            switch (animKind)
            {
                case AnimKind.Idle:
                    SetFrame(ColIdleAndTransform, IdleRow);
                    break;

                case AnimKind.Transform:
                    if(NPC.ai[0] == 3)
                        AnimateStrip(ColMajorWindup, MajorWindupStartRow, 1);
                    else
                        AnimateStrip(ColIdleAndTransform, TransformStartRow, TransformFrameCount);
                    break;

                case AnimKind.Attack1:
                    AnimateStrip(ColAttack1, Attack1StartRow, Attack1FrameCount);
                    if (animDir == -1 && animRow <= Attack1StartRow)
                        animKind = AnimKind.Idle;
                    break;

                case AnimKind.Attack2Slash:
                    AnimateSlashColumn();
                    break;
                    
                case AnimKind.MajorWindup:
                    AnimateStrip(ColMajorWindup, MajorWindupStartRow, 1);
                    break;
                    
                case AnimKind.MajorFire:
                    AnimateStripLooping(ColMajorFire, MajorFireStartRow, MajorFireFrameCount);
                    break;
            }
        }

        private void AnimateStripLooping(int col, int startRow, int count)
        {
            animTick++;
            if (animTick < animTicksPerFrame)
            {
                SetFrame(col, animRow);
                return;
            }

            animTick = 0;
            animRow += animDir;

            int minRow = startRow;
            int maxRow = startRow + count - 1;

            if (animRow > maxRow)
                animRow = minRow;
            if (animRow < minRow)
                animRow = maxRow;

            SetFrame(col, animRow);
        }

        private void AnimateStrip(int col, int startRow, int count)
        {
            animTick++;
            if (animTick < animTicksPerFrame)
            {
                SetFrame(col, animRow);
                return;
            }

            animTick = 0;
            animRow += animDir;

            int minRow = startRow;
            int maxRow = startRow + count - 1;

            if (animRow < minRow) animRow = minRow;
            if (animRow > maxRow) animRow = maxRow;

            SetFrame(col, animRow);
        }

        private void AnimateSlashColumn()
        {
            int telegraphTicks = 60;
            int fireTicks = 8;
            int holdTicks = 60;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            if (currentAttack == AttackKind.SpinningSlash)
                holdTicks = (int)MathHelper.Lerp(60f, 30f, ramp * ramp);
            if (currentAttack == AttackKind.SplitScreen)
                holdTicks = 30;

            int cycle = telegraphTicks + fireTicks + holdTicks;
            if (cycle <= 0) cycle = 1;

            int t = slashWaveTimer;
            if (slashAnimLooping)
                t = t % cycle;
            else
                t = System.Math.Min(t, cycle - 1);

            int frame = 0;

            if (t < telegraphTicks)
            {
                int per = 15;
                frame = (t / per) % 2;
            }
            else if (t < telegraphTicks + fireTicks)
            {
                if( t == telegraphTicks )
                {
                    int shockwaveType = ModContent.ProjectileType<ShockwaveLine>();
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                        float speed = Main.rand.NextFloat(12f, 20f); // Medium speed
                        
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            dir * speed,
                            shockwaveType,
                            0,
                            0f,
                            Main.myPlayer,
                            speed,
                            Main.rand.NextFloat(15f, 30f) // Short to medium length
                        );
                    }
                }
                int tt = t - telegraphTicks;
                frame = SlashTelegraphFrames + ((tt / 4) % 2);
            }
            else
            {
                frame = SlashTelegraphFrames + SlashFireFrames - 1;
            }

            SetFrame(ColAttack2_Slash, SlashAnimStartRow + frame);
        }

        public override bool CheckActive()
        {
            // Allow despawn only when all players are dead
            return allPlayersDead;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            // Can't hit player during major attack (sphere does damage instead)
            if (State == MainState.MajorCentered)
                return false;
                
            return !hideKnightSprite;
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (!BossArenaSystem.IsPlayerLockedIn(player.whoAmI))
                return false;
            return null;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers
                && !BossArenaSystem.IsPlayerLockedIn(projectile.owner))
                return false;
            return null;
        }
    }
}
