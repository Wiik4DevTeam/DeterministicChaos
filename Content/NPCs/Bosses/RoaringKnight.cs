using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
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
        private int sphereCyclesDone;

        private AttackKind currentAttack;
        private int usedAttackMask;
        private bool majorUsed;
        private Vector2 majorAnchor;

        private Vector2 slashAnchor;
        private float slashBaseAngle;
        private float slashOmega;
        private int slashWaveIndex;
        private int nextSlashWaveTick;
        private int slashWaveTimer;
        private int slashCycleTicks;
        private bool slashAnimLooping;

        private bool splitSpawned;

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
        private int coneVisualId = -1;

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
            NPC.defense = 120;
            NPC.lifeMax = 88000;

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
            // Don't completely nullify damage, just apply high defense
            // The 100 defense will reduce damage significantly
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            // Don't completely nullify damage, just apply high defense
            // The 100 defense will reduce damage significantly
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
            
            // Animation state sync
            writer.Write((int)animKind);
            writer.Write(animDir);
            writer.Write(animRow);
            writer.Write(hideKnightSprite);
            writer.Write(transformToBall);
            writer.Write(transformFromBall);
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
            
            // Animation state sync
            animKind = (AnimKind)reader.ReadInt32();
            animDir = reader.ReadInt32();
            animRow = reader.ReadInt32();
            hideKnightSprite = reader.ReadBoolean();
            transformToBall = reader.ReadBoolean();
            transformFromBall = reader.ReadBoolean();
        }

        public override void AI()
        {
            // Enable background
            RoaringKnightBackgroundSystem.ShowBackground = true;
            
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

            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead)
                NPC.TargetClosest(faceTarget: true);

            Player player = Main.player[NPC.target];

            if (player.dead)
            {
                NPC.velocity.Y -= 0.2f;
                if (NPC.timeLeft > 30) NPC.timeLeft = 30;
                RoaringKnightBackgroundSystem.ShowBackground = false;
                CacheTrail();
                return;
            }

            EnsureSingleSphereExists();
            TryEnterMajorPhase();
            UpdateSphereVisibilityAndDamage();

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

            majorAnchor = targetPos + new Vector2(0f, -500f);
            
            State = MainState.MajorCentered;
            Timer = 0f;
            NPC.velocity = Vector2.Zero;
            
            // Initialize major attack state
            majorStarSpawnTick = 0;
            majorStarAngle = 0f;
            majorKnifeSpawnTick = 0;
            majorWindupComplete = false;
            majorInFinalPhase = false;
            
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
            RoaringKnightBackgroundSystem.ShowBackground = false;
            
            // Mark as defeated for Boss Checklist (world-persistent)
            Systems.ERAMProgressSystem.RoaringKnightDefeated = true;
        }
        
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Greater Healing Potions (5-10)
            npcLoot.Add(ItemDropRule.Common(ItemID.GreaterHealingPotion, 1, 5, 10));
            
            // Dark Fragments (30-40)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DarkFragment>(), 1, 30, 40));
            
            // Dark Shard weapon (guaranteed)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DarkShard>(), 1));
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
            if (kind == AttackKind.SeekingExplosives) return 240;
            if (kind == AttackKind.SpinningSlash) return 160 + 5 * 120;
            if (kind == AttackKind.SplitScreen) return 400;
            if (kind == AttackKind.SeekingKnives) return 540;
            if (kind == AttackKind.KnifeLattice) return 600;
            if (kind == AttackKind.AttackCentered) return 1200;
            return 240;
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

            Timer++;

            if (Timer == 1f)
            {
                currentAttack = PickAttack();
                if (currentAttack == AttackKind.SpinningSlash || currentAttack == AttackKind.SplitScreen)
                {
                    slashWaveTimer = 0;
                    slashWaveIndex = 0;
                    nextSlashWaveTick = (int)(AttackStartDelay + 1f);
                    splitSpawned = false;
                }


                else if (currentAttack == AttackKind.SeekingKnives)
                {
                    knifeWaveIndex = 0;
                    nextKnifeWaveTick = (int)(AttackStartDelay + 1f);
                }
                else if (currentAttack == AttackKind.KnifeLattice)
                {
                    latticeWaveIndex = 0;
                    nextLatticeWaveTick = (int)(AttackStartDelay + 1f);
                }
            }

            if (Timer > AttackStartDelay)
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
                    if (Timer == AttackStartDelay + 1f)
                        InitSpinningSlash();

                    animKind = AnimKind.Attack2Slash;
                    slashAnimLooping = true;
                    DoSpinningSlash();
                }
                else if (currentAttack == AttackKind.SplitScreen)
                {
                    if (Timer == AttackStartDelay + 1f)
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
            if (Timer > AttackStartDelay)
            {
                if (currentAttack == AttackKind.SpinningSlash || currentAttack == AttackKind.SplitScreen)
                    distToUse = 260f;
                else if (currentAttack == AttackKind.SeekingKnives || currentAttack == AttackKind.KnifeLattice)
                    distToUse = AttackDesiredDist;
                else
                    distToUse = AttackDesiredDist;
            }

            MoveKeepDistance(p, distToUse);

            if (Timer >= AttackStartDelay + GetAttackDuration(currentAttack))
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
                NPC.netUpdate = true;
            }
        }

        private void DoPostAttackFollow()
        {
            Player p = GetClosestPlayer();

            Timer++;

            MoveKeepDistance(p, NormalDesiredDist);

            if (Timer >= PostAttackFollowTime)
            {
                State = MainState.SpherePhase;
                Timer = 0f;

                transformToBall = true;
                transformFromBall = false;
                hideKnightSprite = false;
                
                // Play transform sound
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform")
                {
                    Volume = 0.8f
                }, NPC.Center);

                animKind = AnimKind.Transform;
                animDir = +1;
                animRow = TransformStartRow;
                animTick = 0;

                Player dashTarget = GetRandomAlivePlayer();

                ballPivot = NPC.Center;
                ballTarget = dashTarget.Center;

                NPC.velocity *= 0.1f;

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
                    NPC.velocity *= 0.9f;
                    return;
                }
            }

            Timer++;

            if (Timer <= OrbitDuration)
            {
                float t = Timer / OrbitDuration;
                float ease = t * t;
                float radius = MathHelper.Lerp(OrbitRadiusStart, OrbitRadiusEnd, ease);

                float loops = 1.25f;
                float angular = (MathHelper.TwoPi * loops) / OrbitDuration;
                float phase = NPC.whoAmI * 0.7f;

                float angle = phase + Timer * angular;
                Vector2 orbitPos = ballPivot + new Vector2(radius, 0f).RotatedBy(angle);

                Vector2 step = orbitPos - NPC.Center;
                NPC.velocity = step * 0.35f;
                NPC.Center += NPC.velocity;
                return;
            }

            if (Timer < OrbitDuration + LingerAfterOrbit)
            {
                NPC.velocity *= 0.85f;
                return;
            }

            int dashTick = OrbitDuration + LingerAfterOrbit;

            if (Timer == dashTick)
            {
                // Play dash sound
                Player dashTarget = GetRandomAlivePlayer();
                ballTarget = dashTarget.Center;
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightDash")
                {
                    Volume = 0.8f
                }, NPC.Center);
                
                Vector2 dashDir = (ballTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity = dashDir * BallDashSpeed;
                
                // Spawn 5 shockwave lines at dash start
                if (Main.netMode != NetmodeID.MultiplayerClient)
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
                
                NPC.netUpdate = true;
            }

            if (Timer >= dashTick + DashDuration)
            {
                hideKnightSprite = false;
                transformFromBall = true;
                
                // Play transform sound
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightTransform")
                {
                    Volume = 0.8f
                }, NPC.Center);

                animKind = AnimKind.Transform;
                animDir = -1;
                animRow = TransformStartRow + TransformFrameCount - 1;
                animTick = 0;

                Timer = 0f;
                State = MainState.NormalAttack;

                sphereCyclesDone++;

                NPC.netUpdate = true;
            }
        }

        private void DoMajorCentered()
        {
            Timer++;
            
            // FORCE these to stay false during entire major phase
            hideKnightSprite = false;
            transformToBall = false;
            transformFromBall = false;

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
                if (!majorInFinalPhase && Timer == MajorPhase1Duration + MajorTransitionDuration + 1)
                {
                    
                    // Start fire animation
                    animKind = AnimKind.MajorFire;
                    animDir = +1;
                    animRow = MajorFireStartRow;
                    animTick = 0;

                    if(Timer == MajorPhase1Duration + MajorTransitionDuration - 10)
                        AnimateStrip(ColMajorWindup, MajorWindupStartRow, 7);
                    
                    // Play roar sound at start of expel phase
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightRoar")
                    {
                        Volume = 1.0f
                    }, NPC.Center);
                }
                
                DoMajorExpelPhase(sphere);
                
                if (!majorInFinalPhase && hp01 <= MajorHealthThreshold)
                {
                    majorInFinalPhase = true;
                    NPC.netUpdate = true;
                }
                
                if (majorInFinalPhase)
                {
                    DoMajorFinalPhaseKnives();
                }
            }
            // Phase 3: Cleanup (only if above health threshold)
            else if (!majorInFinalPhase)
            {
                DoMajorCleanup(sphere);
            }
        }

        private void DoMajorAbsorbPhase(NPC sphere)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || sphere == null)
                return;

            // Spawn 2 stars every 10 ticks and decrease as time goes on (much slower)
            int spawnInterval = 10;
            
            if ((int)Timer >= majorStarSpawnTick)
            {
                // Calculate angle rotation amount (decreases over time)
                float progress = Timer / MajorPhase1Duration;
                float angleStep = MathHelper.Lerp(MathHelper.Pi * 0.3f, MathHelper.Pi * 0.02f, progress);

                // Update spawn interval for next time (decreases over time)
                spawnInterval = (int)MathHelper.Lerp(10f, 4f, progress);
                majorStarSpawnTick = (int)Timer + spawnInterval;
                
                // Spawn 2 stars from opposite sides
                for (int i = 0; i < 2; i++)
                {
                    float angle = majorStarAngle + (i * MathHelper.Pi);
                    
                    // Spawn much farther away from sphere
                    float spawnDist = 2000f;
                    Vector2 spawnPos = sphere.Center + new Vector2(spawnDist, 0f).RotatedBy(angle);
                    
                    // Direction towards sphere
                    Vector2 dir = (sphere.Center - spawnPos).SafeNormalize(Vector2.UnitX);
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
                        Main.projectile[proj].ai[0] = sphere.whoAmI;
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
            if (Main.netMode == NetmodeID.MultiplayerClient || sphere == null)
                return;
            
            // Add constant screen shake during expulsion
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

            // Spawn 2 stars every 8 ticks (much slower)
            int spawnInterval = 7;
            
            if ((int)Timer >= majorStarSpawnTick)
            {
                majorStarSpawnTick = (int)Timer + spawnInterval;
                
                // Calculate angle rotation amount (same pattern as absorb)
                int starPhaseTimer = (int)Timer - (MajorPhase1Duration + MajorTransitionDuration);
                float progress = starPhaseTimer / (float)MajorPhase2Duration;

                spawnInterval = (int)MathHelper.Lerp(7f, 2f, progress);

                if (majorInFinalPhase)
                    progress = 1f; // Keep at minimum rotation in final phase
                    
                float angleStep = MathHelper.Pi * 0.3f;
                
                // Spawn 2 stars from sphere center
                for (int i = 0; i < 2; i++)
                {
                    float angle = majorStarAngle + (i * MathHelper.Pi);
                    
                    // Direction away from sphere
                    Vector2 dir = new Vector2(1f, 0f).RotatedBy(angle);
                    float speed = 18f; // Increased from 12f
                    
                    int proj = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        sphere.Center,
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

            nextSlashWaveTick = (int)(AttackStartDelay + 1f);

            NPC.netUpdate = true;
        }

        private void InitSplitScreen()
        {
            slashWaveIndex = 0;
            slashWaveTimer = 0;
            
            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);
            
            int hold = 30;
            slashCycleTicks = 60 + 8 + hold;
            
            nextSlashWaveTick = (int)(AttackStartDelay + 1f);
            
            NPC.netUpdate = true;
        }

        private void DoSpinningSlash()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int startTick = AttackStartDelay + 1;
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

            // Update anchor to current player position for each wave
            Player target = GetRandomAlivePlayer();
            Vector2 anchor = target.Center;

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

            // Check if we should spawn next wave
            if ((int)Timer < nextSlashWaveTick)
                return;

            // Limit to 5 waves total
            if (slashWaveIndex >= 5)
                return;

            // Reset wave timer when spawning new wave
            slashWaveTimer = 0;

            slashWaveIndex++;

            // Update target position for each wave
            Player target = GetRandomAlivePlayer();
            Vector2 anchor = target.Center;

            // Random new base angle for each wave
            float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            int type = ModContent.ProjectileType<SliceIndicator>();
            int dmg = 85;

            float hp01 = NPC.life / (float)NPC.lifeMax;
            bool belowHalf = hp01 < 0.5f;
            
            // Below half health: spawn 2 sets of indicators instead of 1
            int sets = belowHalf ? 2 : 1;
            
            for (int s = 0; s < sets; s++)
            {
                float angleOffset = s * (MathHelper.PiOver2); // 90 degree offset for second set
                
                float a0 = baseAngle + angleOffset;
                float a1 = baseAngle + MathHelper.Pi + angleOffset;

                // Random rotation direction for both slices (same direction)
                float rotationDirection = Main.rand.NextBool() ? 1f : -1f;

                int id0 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor.X, anchor.Y, 0f, 0f, type, 0, 0f, Main.myPlayer, dmg, a0, rotationDirection);
                if (id0 >= 0 && id0 < Main.maxProjectiles) Main.projectile[id0].netUpdate = true;

                int id1 = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor.X, anchor.Y, 0f, 0f, type, 0, 0f, Main.myPlayer, dmg, a1, rotationDirection);
                if (id1 >= 0 && id1 < Main.maxProjectiles) Main.projectile[id1].netUpdate = true;
            }

            // Schedule next wave
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

            int totalKnives = (int)MathHelper.Lerp(10f, 20f, ramp);

            // Stop spawning after reaching total
            if (knifeWaveIndex >= totalKnives)
                return;

            // Below half health: spawn 2-3 knives instead of 1
            int knivesPerWave = hp01 < 0.5f ? (hp01 < 0.25f ? 1 : 2) : 1;
            
            // Play knife spawn sound once per wave
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightKnifeSpawn")
            {
                Volume = 0.7f
            }, NPC.Center);

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
            // Spawn interval: 60 ticks (1s) -> 20 ticks (0.33s) as health decreases
            float minInterval = 40f;
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

            // Calculate health-based parameters
            float hp01 = NPC.life / (float)NPC.lifeMax;
            float ramp = MathHelper.Clamp(1f - hp01, 0f, 1f);

            int totalWaves = (int)MathHelper.Lerp(5f, 12f, ramp);

            // Stop spawning after reaching total
            if (latticeWaveIndex >= totalWaves)
                return;

            // Spawn ~25 knives in a lattice pattern aimed at random player
            Player target = GetRandomAlivePlayer();
            int targetIndex = target.whoAmI;

            int type = ModContent.ProjectileType<LatticeKnife>();
            
            // Spawn approximately 25 knives per wave
            int knivesPerWave = 25 + (int)(ramp * 20f);
            
            for (int i = 0; i < knivesPerWave; i++)
            {
                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center, // Will be repositioned in OnSpawn
                    Vector2.Zero,
                    type,
                    131, // Damage (30% reduction)
                    0f,
                    Main.myPlayer,
                    targetIndex
                );

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
            

            latticeWaveIndex++;

            // Calculate next spawn time with speed ramp
            // Interval: 120 ticks (2s) -> 60 ticks (1s) as attack progresses
            int minInterval = 80; // 1 second
            int maxInterval = 150; // 2 seconds
            
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
            if (Timer == AttackStartDelay + 1)
            {
                // Play energy charge sound
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightEnergyCharge")
                {
                    Volume = 0.8f
                }, NPC.Center);
                
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

                spriteBatch.Draw(tex, basePos, NPC.frame, Color.White, NPC.rotation, origin, NPC.scale, mainFx, 0f);

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

            for (int i = TrailLength - 1; i >= 1; i--)
            {
                float t = i / (float)TrailLength;
                float alpha = (1f - t) * 0.45f;

                Vector2 pos = trailPos[i] - screenPos;
                pos.Y += NPC.gfxOffY;
                pos -= SpritePivotOffset * NPC.scale;

                float drift = i * 0.45f;
                pos.X += trailSpriteDir[i] * drift;

                SpriteEffects fx = (trailSpriteDir[i] == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(tex2, pos, NPC.frame, Color.White * alpha, trailRot[i], origin2, NPC.scale, fx, 0f);
            }

            SpriteEffects mainFx2 = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            spriteBatch.Draw(tex2, basePos2, NPC.frame, Color.White, NPC.rotation, origin2, NPC.scale, mainFx2, 0f);

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

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            // Can't hit player during major attack (sphere does damage instead)
            if (State == MainState.MajorCentered)
                return false;
                
            return !hideKnightSprite;
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            // Always allow hits, damage is transferred from sphere during ball/major phases
            return null;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            // Always allow hits, damage is transferred from sphere during ball/major phases
            return null;
        }
    }
}