using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    [AutoloadBossHead]
    public class Jevil : ModNPC
    {
        // ── Spritesheet ──
        private const int FrameWidth = 48;
        private const int FrameHeight = 48;
        private const int IdleFrameCount = 8;
        private const int AnimTicksPerFrame = 6;

        private int animTick;
        private int animFrame;

        // ── Attack state machine ──
        private enum AttackState
        {
            Idle = 0,
            Bombs = 1,
            StandaloneSpade = 2,
            StandaloneDiamond = 3,
            StandaloneHeart = 4,
            StandaloneClub = 5,
            Carousel = 6,
            Scythe = 7,
            FallingScythe = 8
        }

        // ── AI slots (synced automatically) ──
        private ref float AIState => ref NPC.ai[0];
        private ref float AITimer => ref NPC.ai[1];
        private ref float AICounter => ref NPC.ai[2]; // sub-counter per attack
        private ref float AIExtra => ref NPC.ai[3];   // extra param per attack

        // ── Attack timing (easily editable) ──
        public static int DelayAboveHalf = 600;       // 10 seconds
        public static int DelayBelowHalf = 360;       // 6 seconds
        public static int DelayRevAboveHalf = 420;     // 7 seconds
        public static int DelayRevBelowHalf = 220;     // 2 seconds

        // ── Bomb attack tuning ──
        public static int BombCountNormal = 5;
        public static int BombCountRev = 7;
        public static float BombSpawnOffsetX = 500f;
        public static float BombSpawnOffsetY = 150f;
        public static float BombSpawnHeight = 800f;

        // ── Standalone Spade tuning ──
        public static int SpadeCircleCount = 24;
        public static float SpadeCircleRadius = 400f;
        public static int SpadeFireInterval = 8; // ~0.13 seconds
        public static int SpadeInitialDelay = 60; // 1 second delay before firing starts
        public static int SpadeRepeatCount = 3;

        // ── Standalone Diamond tuning ──
        public static int DiamondTotalCount = 50;
        public static int DiamondSpawnDuration = 300; // 5 seconds
        public static float DiamondSpawnRangeX = 600f;
        public static float DiamondSpawnBelowY = 400f;

        // ── Standalone Heart tuning ──
        public static float HeartSquareRadius = 160f;
        public static int HeartRepeatCount = 5;
        public static int HeartAttackDuration = 600; // 10 seconds

        // ── Standalone Club tuning ──
        public static float ClubTeleportMinDist = 320f;
        public static float ClubTeleportMaxDist = 500f;
        public static int ClubTeleportInterval = 30; // 0.5 seconds
        public static int ClubAttackDuration = 300;  // 5 seconds
        public static float ClubProjectileSpeed = 9f;
        public static float ClubSpreadAngle = MathHelper.ToRadians(15f);
        public static int ClubTweenFrames = 8;

        // ── Carousel tuning ──
        public static float CarouselOffsetX = 900f;
        public static float CarouselOffsetY = 350f;
        public static int CarouselHorseCount = 15;
        public static int CarouselDuration = 900; // 15 seconds

        // ── Scythe tuning ──
        public static int ScytheCount = 2;
        public static int ScytheCountEnraged = 4;
        public static int ScytheAttackDuration = 420; // 7 seconds
        public static float ScytheOrbitSpeed = 0.013f; // radians per tick
        public static int ScytheArenaEaseTicks = 240; // 4 seconds to shrink arena

        // ── Falling scythe pattern speed ──
        // 1.0 = normal (20s), 0.5 = half speed (40s), 2.0 = double speed (10s)
        public static float PatternSpeedMultiplier = 0.7f;

        // ── Extra synced state ──
        private int attackTargetPlayer = -1; // locked target for current attack
        private int usedBombMask;
        private int usedStandaloneMask;
        private bool nextIsBomb = true; // alternates: bomb → standalone → bomb → ...
        private bool belowHalf;

        // ── Club teleport tween ──
        private float clubScaleX = 1f;
        private bool clubTweeningShrink;
        private bool clubTweeningGrow;
        private int clubTweenTick;
        private int clubFireDelay; // frames to wait after reappearing before firing clubs
        private Player clubFireTarget; // locked target for delayed club fire

        // ── Carousel state ──
        private int carouselDirection; // -1 left, 1 right
        private float carouselAnchorX; // player X when attack starts
        private float carouselAnchorY; // player Y when attack starts

        // ── Spade state ──
        private int spadeCurrentIndex;
        private int spadeRepeatsDone;
        private bool spadeClockwise;
        private int spadeSubTimer;
        private int spadeStartOffset; // index of the spade closest to arena center

        // ── Scythe arena override ──
        private bool scytheArenaOverride;
        private float scytheArenaOverrideTimer;

        // ── Erratic movement ──
        private float orbitAngle;
        private float orbitTargetRadius = 250f;
        private int joltCooldown;

        // ── Clone afterimage system ──
        private struct JevilClone
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public int Timer;
            public int MaxLife;
            public float Alpha;
            public int AnimOffset; // random offset so clones desync from each other
            public SpriteEffects Effects;
        }
        private readonly List<JevilClone> activeClones = new List<JevilClone>();
        private int cloneSpawnCooldown;

        // ── Multiplayer state-change sound detection ──
        private float _prevAIState = -1f;

        // ── Final phase (1 HP) ──
        private bool finalPhaseTriggered;
        private bool finalPhaseActive;
        private int finalPhaseIntroTimer;
        private const int FinalPhaseIntroDuration = 90; // 1.5 seconds for move + shrink
        private int fallingScythePatternIndex;
        private int fallingScythePatternTimer;
        private bool giantScytheSpawned;
        internal static float WhiteFadeAlpha;
        internal bool allowFinalDeath;

        // Pre-rendered scythe pattern: (tick, xFraction 0..1)
        // Designed so players must keep moving, no static safe zones
        private static readonly (int tick, float xFrac)[] FallingScythePattern = new[]
        {
            // ── Phase 1: Gentle intro, single scythes, wide spacing (0-300) ──
            (0,    0.50f),
            (50,   0.20f),
            (100,  0.80f),
            (150,  0.40f),
            (200,  0.65f),
            (250,  0.15f),
            (300,  0.85f),

            // ── Phase 2: Pairs begin, comfortable gaps (330-560) ──
            (330,  0.25f),
            (330,  0.75f),
            (380,  0.50f),
            (380,  0.10f),
            (420,  0.40f),
            (420,  0.90f),
            (460,  0.60f),
            (460,  0.20f),
            (500,  0.80f),
            (500,  0.35f),
            (540,  0.15f),
            (540,  0.70f),

            // ── Phase 3: Tighter timing, sweep patterns (570-780) ──
            (570,  0.10f),
            (570,  0.50f),
            (600,  0.30f),
            (600,  0.70f),
            (625,  0.50f),
            (625,  0.90f),
            (650,  0.15f),
            (650,  0.60f),
            (675,  0.40f),
            (675,  0.85f),
            (700,  0.25f),
            (700,  0.75f),
            (730,  0.55f),
            (730,  0.05f),
            (755,  0.45f),
            (755,  0.95f),

            // ── Phase 4: Closing walls + center pressure (790-960) ──
            (790,  0.05f),
            (790,  0.95f),
            (810,  0.15f),
            (810,  0.85f),
            (830,  0.25f),
            (830,  0.75f),
            (850,  0.35f),
            (850,  0.65f),
            (870,  0.45f),
            (870,  0.55f),
            (895,  0.50f),
            (895,  0.20f),
            (920,  0.80f),
            (920,  0.40f),
            (940,  0.60f),
            (940,  0.10f),

            // ── Phase 5: Rapid barrage, fast pairs, minimal gaps (970-1200) ──
            (970,  0.30f),
            (970,  0.70f),
            (985,  0.50f),
            (985,  0.15f),
            (1000, 0.85f),
            (1000, 0.45f),
            (1015, 0.65f),
            (1015, 0.25f),
            (1030, 0.10f),
            (1030, 0.90f),
            (1045, 0.40f),
            (1045, 0.75f),
            (1060, 0.55f),
            (1060, 0.20f),
            (1075, 0.80f),
            (1075, 0.35f),
            (1090, 0.60f),
            (1090, 0.05f),
            (1105, 0.50f),
            (1105, 0.95f),
            (1120, 0.30f),
            (1120, 0.70f),
            (1140, 0.15f),
            (1140, 0.85f),
            (1160, 0.45f),
            (1160, 0.55f),
            (1180, 0.25f),
            (1180, 0.75f),
            (1200, 0.50f),
            (1200, 0.50f),
        };

        // ── Arena ──
        private BossArenaSystem.ArenaBox arenaBox;

        // ── Calamity difficulty ──
        private bool calDiffCached;
        private bool isRevengeance;
        private bool isDeath;

        // ── Damage value ──
        public static int ProjectileDamage = 30;
        private const int ProjectileDamageReduction = 16;
        private static int AdjustedProjectileDamage => Math.Max(1, ProjectileDamage - ProjectileDamageReduction);
        private static int AdjustProjectileDamage(int baseDamage) => Math.Max(1, baseDamage - ProjectileDamageReduction);

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Insert(0, Type);
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 48;
            NPC.height = 48;
            NPC.damage = 40;
            NPC.defense = 12;
            NPC.lifeMax = 8000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = Item.buyPrice(0, 8, 0, 0);
            NPC.SpawnWithHigherTime(30);
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.aiStyle = -1;
            NPC.scale = 2f;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("A chaotic jester who can do anything.")
            });
        }

        // ── Network sync ──
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(attackTargetPlayer);
            writer.Write(usedBombMask);
            writer.Write(usedStandaloneMask);
            writer.Write(nextIsBomb);
            writer.Write(spadeCurrentIndex);
            writer.Write(spadeRepeatsDone);
            writer.Write(spadeClockwise);
            writer.Write(spadeSubTimer);
            writer.Write(carouselDirection);
            writer.Write(carouselAnchorX);
            writer.Write(carouselAnchorY);
            writer.Write(clubScaleX);
            writer.Write(clubTweeningShrink);
            writer.Write(clubTweeningGrow);
            writer.Write(clubFireDelay);
            writer.Write(finalPhaseTriggered);
            writer.Write(finalPhaseActive);
            writer.Write(scytheArenaOverride);
            writer.Write(spadeStartOffset);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            attackTargetPlayer = reader.ReadInt32();
            usedBombMask = reader.ReadInt32();
            usedStandaloneMask = reader.ReadInt32();
            nextIsBomb = reader.ReadBoolean();
            spadeCurrentIndex = reader.ReadInt32();
            spadeRepeatsDone = reader.ReadInt32();
            spadeClockwise = reader.ReadBoolean();
            spadeSubTimer = reader.ReadInt32();
            carouselDirection = reader.ReadInt32();
            carouselAnchorX = reader.ReadSingle();
            carouselAnchorY = reader.ReadSingle();
            clubScaleX = reader.ReadSingle();
            clubTweeningShrink = reader.ReadBoolean();
            clubTweeningGrow = reader.ReadBoolean();
            clubFireDelay = reader.ReadInt32();
            finalPhaseTriggered = reader.ReadBoolean();
            finalPhaseActive = reader.ReadBoolean();
            scytheArenaOverride = reader.ReadBoolean();
            spadeStartOffset = reader.ReadInt32();
        }

        // ── Calamity detection ──
        private void CacheCalamityDifficulty()
        {
            if (calDiffCached) return;
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

        private bool IsRevOrDeath => isRevengeance || isDeath;

        private int GetAttackDelay()
        {
            bool low = NPC.life < NPC.lifeMax * 0.5f;
            if (IsRevOrDeath)
                return low ? DelayRevBelowHalf : DelayRevAboveHalf;
            return low ? DelayBelowHalf : DelayAboveHalf;
        }

        // ── Main AI ──
        public override void AI()
        {
            CacheCalamityDifficulty();
            belowHalf = NPC.life < NPC.lifeMax * 0.5f;

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            // Create arena once on first tick
            if (arenaBox == null)
            {
                Vector2 bottomCenter = target.Center + new Vector2(0f, 5f * 16f);
                arenaBox = BossArenaSystem.CreateArena(bottomCenter, 2000f, 2000f, () => !NPC.active);
                arenaBox.BackgroundTexturePath = "DeterministicChaos/Content/NPCs/Bosses/JevilBG";
                arenaBox.BackgroundTint = new Color(80, 120, 255); // blue filter
            }

            // Shrink arena: 2000 at 50% HP → 1000 at 10% HP
            // During scythe attack, override to 1000×1000 with fast ease (~2s)
            // During final phase, expand back to base size
            if (finalPhaseActive)
            {
                arenaBox.TargetHalfWidth = 1000f;
                arenaBox.TargetHalfHeight = 1000f;
                arenaBox.LerpSpeed = 0.04f;
            }
            else if (scytheArenaOverride)
            {
                scytheArenaOverrideTimer++;
                arenaBox.TargetHalfWidth = 500f;
                arenaBox.TargetHalfHeight = 500f;
                arenaBox.LerpSpeed = 0.04f; // faster ease for scythe shrink
            }
            else
            {
                arenaBox.LerpSpeed = 0.02f; // normal speed
                float hpRatio = NPC.life / (float)NPC.lifeMax;
                float shrinkProgress = MathHelper.Clamp((0.5f - hpRatio) / 0.4f, 0f, 1f);
                float currentHalf = MathHelper.Lerp(1000f, 500f, shrinkProgress);
                arenaBox.TargetHalfWidth = currentHalf;
                arenaBox.TargetHalfHeight = currentHalf;

                // Scale scroll speed with HP: 60 at full → 200 at 10% HP
                arenaBox.BackgroundScrollSpeed = MathHelper.Lerp(200f, 600f, shrinkProgress);
            }

            // Check if all locked-in players are dead
            bool allArenaDead = true;
            foreach (var box in BossArenaSystem.ActiveBoxes)
            {
                foreach (int pIdx in box.LockedPlayers)
                {
                    if (pIdx >= 0 && pIdx < Main.maxPlayers)
                    {
                        Player p = Main.player[pIdx];
                        if (p.active && !p.dead)
                        {
                            allArenaDead = false;
                            break;
                        }
                    }
                }
                if (!allArenaDead) break;
            }

            if (allArenaDead)
            {
                // Clean up white fade if dying during final phase
                if (finalPhaseActive)
                    Jevil.WhiteFadeAlpha = 0f;

                NPC.velocity.Y -= 0.5f;
                NPC.EncourageDespawn(30);
                return;
            }

            // Prevent vanilla despawn, only refresh when NOT trying to despawn
            NPC.timeLeft = NPC.activeTime * 2;

            AITimer++;

            // Detect AI state changes for client-side sound playback
            // (NPC.ai[0] syncs to clients, so they see transitions)
            float currentAIState = AIState;
            if (_prevAIState >= 0f && currentAIState != _prevAIState)
            {
                if (Main.netMode != NetmodeID.Server)
                {
                    if (currentAIState == (float)AttackState.Idle && _prevAIState != (float)AttackState.Idle)
                    {
                        int line = Main.rand.Next(1, 6);
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilLine" + line), NPC.Center);
                    }
                    if (currentAIState == (float)AttackState.FallingScythe)
                    {
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilFinal"), NPC.Center);
                    }
                }
            }
            _prevAIState = currentAIState;

            switch ((AttackState)(int)AIState)
            {
                case AttackState.Idle:
                    AI_Idle(target);
                    break;
                case AttackState.Bombs:
                    AI_Bombs(target);
                    break;
                case AttackState.StandaloneSpade:
                    AI_StandaloneSpade(target);
                    break;
                case AttackState.StandaloneDiamond:
                    AI_StandaloneDiamond(target);
                    break;
                case AttackState.StandaloneHeart:
                    AI_StandaloneHeart(target);
                    break;
                case AttackState.StandaloneClub:
                    AI_StandaloneClub(target);
                    break;
                case AttackState.Carousel:
                    AI_Carousel(target);
                    break;
                case AttackState.Scythe:
                    AI_Scythe(target);
                    break;
                case AttackState.FallingScythe:
                    AI_FallingScythe(target);
                    break;
            }

            // ── Clone afterimage spawning & updating ──
            if (belowHalf)
            {
                float hpRatio = NPC.life / (float)NPC.lifeMax;
                float cloneProgress = MathHelper.Clamp((0.5f - hpRatio) / 0.4f, 0f, 1f);

                int spawnRate = (int)MathHelper.Lerp(40f, 6f, cloneProgress);
                if (spawnRate < 1) spawnRate = 1;
                cloneSpawnCooldown--;
                if (cloneSpawnCooldown <= 0)
                {
                    cloneSpawnCooldown = spawnRate;

                    float alpha = MathHelper.Lerp(0.1f, 0.8f, cloneProgress);

                    float baseSpeed = MathHelper.Lerp(2f, 5f, cloneProgress);
                    float speedVariance = MathHelper.Lerp(1f, 4f, cloneProgress);
                    float speed = baseSpeed + Main.rand.NextFloat(-speedVariance, speedVariance);
                    if (speed < 0.5f) speed = 0.5f;
                    float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                    Vector2 vel = new Vector2(speed, 0f).RotatedBy(angle);

                    if (cloneProgress > 0.3f)
                        vel += new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));

                    activeClones.Add(new JevilClone
                    {
                        Position = NPC.Center,
                        Velocity = vel,
                        Timer = 0,
                        MaxLife = 300,
                        Alpha = alpha,
                        AnimOffset = Main.rand.Next(IdleFrameCount * AnimTicksPerFrame),
                        Effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None
                    });
                }
            }

            for (int i = activeClones.Count - 1; i >= 0; i--)
            {
                var clone = activeClones[i];
                clone.Timer++;
                clone.Position += clone.Velocity;

                if (belowHalf)
                {
                    float cp = MathHelper.Clamp((0.5f - NPC.life / (float)NPC.lifeMax) / 0.4f, 0f, 1f);
                    if (cp > 0.3f && clone.Timer % 10 == 0)
                    {
                        clone.Velocity += new Vector2(
                            Main.rand.NextFloat(-1.5f, 1.5f),
                            Main.rand.NextFloat(-1.5f, 1.5f));
                    }
                }

                if (clone.Timer >= clone.MaxLife)
                    activeClones.RemoveAt(i);
                else
                    activeClones[i] = clone;
            }

            NPC.spriteDirection = NPC.Center.X < target.Center.X ? 1 : -1;
        }

        // ── Movement: flee from players + sporadic jolts (during attacks) ──
        private void MoveErraticallyAway(Player target)
        {
            const float fleeRange = 400f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                float dist = Vector2.Distance(NPC.Center, p.Center);
                if (dist < fleeRange && dist > 1f)
                {
                    float strength = (fleeRange - dist) / fleeRange;
                    NPC.velocity += (NPC.Center - p.Center) / dist * strength * 2.5f;
                }
            }

            joltCooldown--;
            if (joltCooldown <= 0)
            {
                joltCooldown = Main.rand.Next(3, 10);
                NPC.velocity += new Vector2(
                    Main.rand.NextFloat(-5f, 5f),
                    Main.rand.NextFloat(-5f, 5f));
            }

            if (arenaBox != null)
            {
                Vector2 offset = NPC.Center - arenaBox.Center;
                float margin = 150f;
                if (Math.Abs(offset.X) > arenaBox.HalfWidth - margin)
                    NPC.velocity.X -= Math.Sign(offset.X) * 2f;
                if (Math.Abs(offset.Y) > arenaBox.HalfHeight - margin)
                    NPC.velocity.Y -= Math.Sign(offset.Y) * 2f;
            }

            float maxSpd = 14f;
            if (NPC.velocity.LengthSquared() > maxSpd * maxSpd)
                NPC.velocity = Vector2.Normalize(NPC.velocity) * maxSpd;
            NPC.velocity *= 0.94f;
        }

        // ── Movement: erratic orbit near player (during idle) ──
        private void MoveErraticallyOrbit(Player target)
        {
            orbitAngle += 0.035f + Main.rand.NextFloat(-0.025f, 0.04f);
            orbitTargetRadius = MathHelper.Lerp(orbitTargetRadius, 200f + Main.rand.NextFloat(-100f, 100f), 0.04f);

            Vector2 idealPos = target.Center + new Vector2(orbitTargetRadius, 0f).RotatedBy(orbitAngle);
            Vector2 toIdeal = idealPos - NPC.Center;
            float dist2 = toIdeal.Length();

            if (dist2 > 5f)
            {
                float steerSpeed = MathHelper.Clamp(dist2 / 30f, 2f, 10f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, toIdeal.SafeNormalize(Vector2.Zero) * steerSpeed, 0.1f);
            }

            joltCooldown--;
            if (joltCooldown <= 0)
            {
                joltCooldown = Main.rand.Next(6, 18);
                NPC.velocity += new Vector2(
                    Main.rand.NextFloat(-7f, 7f),
                    Main.rand.NextFloat(-7f, 7f));
            }

            float maxSpd = 12f;
            if (NPC.velocity.LengthSquared() > maxSpd * maxSpd)
                NPC.velocity = Vector2.Normalize(NPC.velocity) * maxSpd;
        }

        // ── Pick attack (no repeats until all used) ──
        private Player LockAttackTarget()
        {
            // Pick a random alive player and lock them for the entire attack
            int[] candidates = new int[Main.maxPlayers];
            int count = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                    candidates[count++] = i;
            }
            if (count <= 0)
            {
                attackTargetPlayer = NPC.target;
            }
            else
            {
                attackTargetPlayer = candidates[Main.rand.Next(count)];
            }
            return Main.player[attackTargetPlayer];
        }

        private Player GetLockedTarget()
        {
            if (attackTargetPlayer >= 0 && attackTargetPlayer < Main.maxPlayers)
            {
                Player p = Main.player[attackTargetPlayer];
                if (p.active && !p.dead)
                    return p;
            }
            return Main.player[NPC.target];
        }

        // Bomb pool (suit-themed bomb variants, all use AttackState.Bombs but with forced suit)
        private static readonly int[] BombSuits = { 0, 1, 2, 3 }; // Club, Heart, Spade, Diamond

        // Standalone / misc pool
        private static readonly AttackState[] StandalonePool = new[]
        {
            AttackState.StandaloneSpade,
            AttackState.StandaloneDiamond,
            AttackState.StandaloneHeart,
            AttackState.StandaloneClub,
            AttackState.Carousel,
            AttackState.Scythe
        };

        private AttackState PickAttack()
        {
            if (nextIsBomb)
            {
                // Pick a bomb suit that hasn't been used yet
                int allBombMask = 0xF; // bits 0-3
                if ((usedBombMask & allBombMask) == allBombMask)
                    usedBombMask = 0;

                int chosenSuit = 0;
                int tries = 0;
                while (tries++ < 20)
                {
                    int candidate = BombSuits[Main.rand.Next(BombSuits.Length)];
                    if ((usedBombMask & (1 << candidate)) == 0)
                    {
                        chosenSuit = candidate;
                        break;
                    }
                }
                usedBombMask |= 1 << chosenSuit;
                AIExtra = chosenSuit; // force this suit for the bomb attack
                nextIsBomb = false;
                return AttackState.Bombs;
            }
            else
            {
                // Pick a standalone/misc attack that hasn't been used yet
                int allMask = 0;
                for (int i = 0; i < StandalonePool.Length; i++)
                    allMask |= (1 << (int)StandalonePool[i]);

                if ((usedStandaloneMask & allMask) == allMask)
                    usedStandaloneMask = 0;

                AttackState chosen = StandalonePool[0];
                int tries = 0;
                while (tries++ < 40)
                {
                    AttackState candidate = StandalonePool[Main.rand.Next(StandalonePool.Length)];
                    int bit = 1 << (int)candidate;
                    if ((usedStandaloneMask & bit) == 0)
                    {
                        chosen = candidate;
                        break;
                    }
                }

                usedStandaloneMask |= 1 << (int)chosen;
                nextIsBomb = true;
                return chosen;
            }
        }

        private void TransitionToAttack(AttackState state)
        {
            AIState = (float)state;
            AITimer = 0f;
            AICounter = 0f;
            AIExtra = 0f;
            spadeCurrentIndex = 0;
            spadeRepeatsDone = 0;
            spadeSubTimer = 0;
            clubScaleX = 1f;
            clubTweeningShrink = false;
            clubTweeningGrow = false;
            clubTweenTick = 0;
            clubFireDelay = 0;
            clubFireTarget = null;
            NPC.netUpdate = true;
        }

        private void ReturnToIdle()
        {
            // If at 1 HP and final phase triggered, start the falling scythe finale
            if (finalPhaseTriggered && !finalPhaseActive)
            {
                finalPhaseActive = true;
                TransitionToAttack(AttackState.FallingScythe);
                fallingScythePatternIndex = 0;
                fallingScythePatternTimer = 0;
                giantScytheSpawned = false;
                finalPhaseIntroTimer = 0;
                NPC.dontTakeDamage = true;

                NPC.netUpdate = true;
                return;
            }

            // Sounds are handled by state-change detection in AI()
            TransitionToAttack(AttackState.Idle);
        }

        public override bool CheckDead()
        {
            if (!finalPhaseTriggered)
            {
                // Don't die, hold at 1 HP and flag for final phase
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                finalPhaseTriggered = true;
                NPC.netUpdate = true;
                return false;
            }
            // Only allow death once the giant scythe signals it
            if (!allowFinalDeath)
            {
                NPC.life = 1;
                return false;
            }
            return true;
        }

        // ═══════════════════════════════════════════
        // IDLE, hover, then pick next attack
        // ═══════════════════════════════════════════
        private void AI_Idle(Player target)
        {
            MoveErraticallyOrbit(target);

            if (AITimer >= GetAttackDelay() && Main.netMode != NetmodeID.MultiplayerClient)
            {
                LockAttackTarget();
                AttackState next = PickAttack();
                // PickAttack may set AIExtra (bomb suit), so save it
                float savedExtra = AIExtra;
                TransitionToAttack(next);
                AIExtra = savedExtra;
                NPC.netUpdate = true;
            }
        }

        // ═══════════════════════════════════════════
        // BOMBS, drop suit bombs from sky
        // ═══════════════════════════════════════════
        private void AI_Bombs(Player target)
        {
            MoveErraticallyAway(target);
            Player locked = GetLockedTarget();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int totalBombs = IsRevOrDeath ? BombCountRev : BombCountNormal;
            int spawnInterval = 25; // one bomb every ~0.33s

            if ((int)AITimer % spawnInterval == 0 && (int)AICounter < totalBombs)
            {
                // Suit is forced by PickAttack via AIExtra above 50% HP
                // Below 50%: use multiple suits
                int[] suits;
                if (belowHalf)
                {
                    if (IsRevOrDeath)
                        suits = new[] { 0, 1, 2, 3 }; // all 4
                    else
                        suits = PickRandomSuits(3);
                }
                else
                {
                    suits = new[] { (int)AIExtra };
                }

                int suit = suits[Main.rand.Next(suits.Length)];
                SpawnBomb(locked, suit);

                AICounter++;
                NPC.netUpdate = true;
            }

            if ((int)AICounter >= totalBombs && AITimer > totalBombs * spawnInterval + 120)
            {
                ReturnToIdle();
            }
        }

        private int[] PickRandomSuits(int count)
        {
            int[] all = { 0, 1, 2, 3 };
            // Fisher-Yates shuffle
            for (int i = all.Length - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            int[] result = new int[count];
            Array.Copy(all, result, count);
            return result;
        }

        private void SpawnBomb(Player target, int suit)
        {
            // Bomb fall sound moved to bomb projectile AI for multiplayer compatibility

            float xOffset = BombSpawnOffsetX * (Main.rand.NextBool() ? 1f : -1f);
            float spawnX = target.Center.X + xOffset + Main.rand.NextFloat(-50f, 50f);
            float spawnY = target.Center.Y - BombSpawnHeight;
            float targetY = target.Center.Y + Main.rand.NextFloat(-BombSpawnOffsetY - 300f, BombSpawnOffsetY);

            int type;
            switch (suit)
            {
                case 0: type = ModContent.ProjectileType<ClubBomb>(); break;
                case 1: type = ModContent.ProjectileType<HeartBomb>(); break;
                case 2: type = ModContent.ProjectileType<SpadeBomb>(); break;
                default: type = ModContent.ProjectileType<DiamondBomb>(); break;
            }

            int id = Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                new Vector2(spawnX, spawnY),
                Vector2.Zero,
                type,
                AdjustedProjectileDamage,
                0f,
                Main.myPlayer,
                target.whoAmI,
                targetY
            );
            if (id >= 0 && id < Main.maxProjectiles)
                Main.projectile[id].netUpdate = true;
        }

        // ═══════════════════════════════════════════
        // STANDALONE SPADE, circle pull-and-fire
        // ═══════════════════════════════════════════
        private void AI_StandaloneSpade(Player target)
        {
            MoveErraticallyAway(target);
            Player locked = GetLockedTarget();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // On first tick of each repeat, spawn the circle (all frozen)
            if (spadeSubTimer == 0 && spadeCurrentIndex == 0)
            {
                spadeClockwise = Main.rand.NextBool();
                Vector2 center = locked.Center;

                // Find the spade index closest to arena center
                spadeStartOffset = 0;
                if (arenaBox != null)
                {
                    float bestDist = float.MaxValue;
                    for (int i = 0; i < SpadeCircleCount; i++)
                    {
                        float angle = MathHelper.TwoPi * i / SpadeCircleCount;
                        Vector2 pos = center + new Vector2(SpadeCircleRadius, 0f).RotatedBy(angle);
                        float dist = Vector2.DistanceSquared(pos, arenaBox.Center);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            spadeStartOffset = i;
                        }
                    }
                }

                for (int i = 0; i < SpadeCircleCount; i++)
                {
                    float angle = MathHelper.TwoPi * i / SpadeCircleCount;
                    Vector2 pos = center + new Vector2(SpadeCircleRadius, 0f).RotatedBy(angle);
                    float launchAngle = (center - pos).ToRotation(); // toward center

                    int id = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        pos,
                        Vector2.Zero,
                        ModContent.ProjectileType<SpadeProjectile>(),
                        AdjustedProjectileDamage,
                        0f,
                        Main.myPlayer,
                        2f, // mode = standalone pull-then-fire
                        -999f, // frozen sentinel: Timer set to -999 means "waiting"
                        launchAngle
                    );
                    if (id >= 0 && id < Main.maxProjectiles)
                    {
                        Main.projectile[id].localAI[0] = i;
                        Main.projectile[id].localAI[1] = 0f; // not yet activated
                        Main.projectile[id].velocity = Vector2.Zero;
                        Main.projectile[id].netUpdate = true;
                    }
                }
                NPC.netUpdate = true;
            }

            spadeSubTimer++;

            // Wait for initial delay before starting sequential activation
            int fireStart = SpadeInitialDelay;
            int elapsed = spadeSubTimer - fireStart;

            if (elapsed >= 0 && spadeCurrentIndex < SpadeCircleCount)
            {
                if (elapsed % SpadeFireInterval == 0)
                {
                    int rawIndex = spadeClockwise ? spadeCurrentIndex : (SpadeCircleCount - 1 - spadeCurrentIndex);
                    int targetIndex = (rawIndex + spadeStartOffset) % SpadeCircleCount;

                    int spadeType = ModContent.ProjectileType<SpadeProjectile>();
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (!p.active || p.type != spadeType)
                            continue;
                        if ((int)p.localAI[0] == targetIndex && p.localAI[1] == 0f && (int)p.ai[0] == 2)
                        {
                            // Activate: reset timer to 0 so pull-back begins
                            p.ai[1] = 0f;
                            p.localAI[1] = 1f;
                            p.netUpdate = true;
                            break;
                        }
                    }

                    spadeCurrentIndex++;
                    NPC.netUpdate = true;
                }
            }

            // All spades fired, wait a bit then repeat or finish
            if (spadeCurrentIndex >= SpadeCircleCount)
            {
                if (spadeSubTimer > fireStart + SpadeCircleCount * SpadeFireInterval + 60)
                {
                    spadeRepeatsDone++;
                    if (spadeRepeatsDone >= SpadeRepeatCount)
                    {
                        ReturnToIdle();
                    }
                    else
                    {
                        spadeCurrentIndex = 0;
                        spadeSubTimer = 0;
                        NPC.netUpdate = true;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════
        // STANDALONE DIAMOND, rise from below
        // ═══════════════════════════════════════════
        private void AI_StandaloneDiamond(Player target)
        {
            MoveErraticallyAway(target);
            Player locked = GetLockedTarget();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int interval = DiamondSpawnDuration / DiamondTotalCount;
            if (interval < 1) interval = 1;

            if ((int)AITimer % interval == 0 && (int)AICounter < DiamondTotalCount)
            {
                float xOffset = Main.rand.NextFloat(-DiamondSpawnRangeX, DiamondSpawnRangeX);
                Vector2 spawnPos = locked.Center + new Vector2(xOffset, DiamondSpawnBelowY);

                int id = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    new Vector2(0f, DiamondProjectile.DipSpeed),
                    ModContent.ProjectileType<DiamondProjectile>(),
                    AdjustedProjectileDamage,
                    0f,
                    Main.myPlayer,
                    2f // mode = rise from below
                );
                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;

                AICounter++;
                NPC.netUpdate = true;
            }

            if ((int)AICounter >= DiamondTotalCount && AITimer > DiamondSpawnDuration + 120)
            {
                ReturnToIdle();
            }
        }

        // ═══════════════════════════════════════════
        // STANDALONE HEART, rotating squares
        // ═══════════════════════════════════════════
        private void AI_StandaloneHeart(Player target)
        {
            MoveErraticallyAway(target);
            Player locked = GetLockedTarget();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int repeatInterval = HeartAttackDuration / HeartRepeatCount;

            if ((int)AITimer % repeatInterval == 0 && (int)AICounter < HeartRepeatCount)
            {
                Vector2 center = locked.Center;
                int type = ModContent.ProjectileType<HeartProjectile>();
                float sharedDriftAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi) + 0.01f;

                for (int i = 0; i < 4; i++)
                {
                    float angleOffset = MathHelper.PiOver2 * i;
                    Vector2 pos = center + new Vector2(HeartSquareRadius, 0f).RotatedBy(angleOffset);

                    int id = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        pos,
                        Vector2.Zero,
                        type,
                        AdjustedProjectileDamage,
                        0f,
                        Main.myPlayer,
                        2f, // mode = standalone spin then drift
                        center.X
                    );
                    if (id >= 0 && id < Main.maxProjectiles)
                    {
                        Main.projectile[id].ai[2] = center.Y;
                        Main.projectile[id].localAI[0] = angleOffset;
                        Main.projectile[id].localAI[1] = HeartSquareRadius;
                        if (Main.projectile[id].ModProjectile is HeartProjectile heart)
                            heart.ExtraData = sharedDriftAngle;
                        Main.projectile[id].netUpdate = true;
                    }
                }

                AICounter++;
                NPC.netUpdate = true;
            }

            if (AITimer >= HeartAttackDuration + 60)
            {
                ReturnToIdle();
            }
        }

        // ═══════════════════════════════════════════
        // STANDALONE CLUB, teleport + spread shot
        // ═══════════════════════════════════════════
        private void AI_StandaloneClub(Player target)
        {
            Player locked = GetLockedTarget();

            // Tween logic
            if (clubTweeningShrink)
            {
                clubTweenTick++;
                if (clubTweenTick == 1)
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilTeleport"), NPC.Center);
                clubScaleX = MathHelper.Lerp(1f, 0.05f, (float)clubTweenTick / ClubTweenFrames);
                if (clubTweenTick >= ClubTweenFrames)
                {
                    clubTweeningShrink = false;
                    clubTweeningGrow = true;
                    clubTweenTick = 0;

                    // Actually teleport now (at thinnest point)
                    Vector2 newPos = FindTeleportPosition(locked);
                    NPC.Center = newPos;
                    NPC.velocity = Vector2.Zero;

                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilOh"), NPC.Center);

                    // Delay club fire by 10 frames after reappearing
                    clubFireDelay = 10;
                    clubFireTarget = locked;

                    NPC.netUpdate = true;
                }
                return;
            }

            // Fire clubs after delay
            if (clubFireDelay > 0)
            {
                clubFireDelay--;
                if (clubFireDelay == 0 && Main.netMode != NetmodeID.MultiplayerClient && clubFireTarget != null)
                {
                    Vector2 toPlayer = (clubFireTarget.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    int type = ModContent.ProjectileType<ClubProjectile>();
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 dir = toPlayer.RotatedBy(ClubSpreadAngle * i);
                        int id = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            dir * ClubProjectileSpeed,
                            type,
                            AdjustedProjectileDamage,
                            0f,
                            Main.myPlayer
                        );
                        if (id >= 0 && id < Main.maxProjectiles)
                            Main.projectile[id].netUpdate = true;
                    }
                    clubFireTarget = null;
                }
                return;
            }

            if (clubTweeningGrow)
            {
                clubTweenTick++;
                clubScaleX = MathHelper.Lerp(0.05f, 1f, (float)clubTweenTick / ClubTweenFrames);
                if (clubTweenTick >= ClubTweenFrames)
                {
                    clubTweeningGrow = false;
                    clubScaleX = 1f;
                }
                return;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Trigger teleport every interval
            if ((int)AITimer % ClubTeleportInterval == 0 && AITimer > 0 && AITimer < ClubAttackDuration)
            {
                clubTweeningShrink = true;
                clubTweenTick = 0;
                NPC.netUpdate = true;
            }

            if (AITimer >= ClubAttackDuration + 30)
            {
                ReturnToIdle();
            }
        }

        private Vector2 FindTeleportPosition(Player target)
        {
            for (int i = 0; i < 50; i++)
            {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(ClubTeleportMinDist, ClubTeleportMaxDist);
                Vector2 pos = target.Center + new Vector2(dist, 0f).RotatedBy(angle);

                float d = Vector2.Distance(pos, target.Center);
                if (d >= ClubTeleportMinDist && d <= ClubTeleportMaxDist)
                    return pos;
            }
            return target.Center + new Vector2(ClubTeleportMaxDist, 0f);
        }

        // ═══════════════════════════════════════════
        // CAROUSEL, horse columns
        // ═══════════════════════════════════════════
        private void AI_Carousel(Player target)
        {
            MoveErraticallyAway(target);
            Player locked = GetLockedTarget();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (AITimer == 1f)
            {
                carouselDirection = Main.rand.NextBool() ? -1 : 1;
                // Anchor to the arena center Y; X is the arena edge
                if (arenaBox != null)
                {
                    carouselAnchorX = arenaBox.Center.X;
                    carouselAnchorY = arenaBox.Center.Y;
                }
                else
                {
                    carouselAnchorX = locked.Center.X;
                    carouselAnchorY = locked.Center.Y;
                }
                NPC.netUpdate = true;
            }

            int spawnInterval = CarouselDuration / CarouselHorseCount;
            if (spawnInterval < 1) spawnInterval = 1;

            if ((int)AITimer % spawnInterval == 0 && (int)AICounter < CarouselHorseCount)
            {
                // Spawn slightly behind the arena wall
                float wallEdge = arenaBox != null
                    ? carouselAnchorX + arenaBox.HalfWidth * carouselDirection
                    : carouselAnchorX + CarouselOffsetX * carouselDirection;
                float spawnX = wallEdge + 60f * carouselDirection; // 60px behind wall
                float moveDir = -carouselDirection;

                float baseY = carouselAnchorY + Main.rand.NextFloat(-CarouselOffsetY, CarouselOffsetY);

                // Horse spawn sound moved to HorseProjectile AI for multiplayer compatibility
                int type = ModContent.ProjectileType<HorseProjectile>();
                for (int h = 0; h < HorseProjectile.ColumnCount; h++)
                {
                    float yPos = baseY + (h - HorseProjectile.ColumnCount / 2) * HorseProjectile.VerticalSpacing;

                    int id = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        new Vector2(spawnX, yPos),
                        Vector2.Zero,
                        type,
                        AdjustedProjectileDamage,
                        0f,
                        Main.myPlayer,
                        moveDir,
                        0f,
                        HorseProjectile.HorizontalSpeed
                    );
                    if (id >= 0 && id < Main.maxProjectiles)
                        Main.projectile[id].netUpdate = true;
                }

                AICounter++;
                NPC.netUpdate = true;
            }

            if (AITimer >= CarouselDuration + 120)
            {
                ReturnToIdle();
            }
        }

        // ═══════════════════════════════════════════
        // SCYTHE, orbit around the arena perimeter
        // ═══════════════════════════════════════════
        private void AI_Scythe(Player target)
        {
            MoveErraticallyAway(target);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Enable arena override to shrink to 1000×1000
            if (AITimer == 1f)
            {
                scytheArenaOverride = true;
                scytheArenaOverrideTimer = 0f;

                int count = belowHalf ? ScytheCountEnraged : ScytheCount;
                int trackingType = ModContent.ProjectileType<ScytheProjectileTracking>();
                Vector2 spawnPos = arenaBox != null ? arenaBox.Center : NPC.Center;

                for (int s = 0; s < count; s++)
                {
                    float baseAngle = MathHelper.TwoPi * s / count;

                    int id = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        Vector2.Zero,
                        trackingType,
                        AdjustedProjectileDamage,
                        0f,
                        Main.myPlayer,
                        baseAngle,         // ai[0] = base angle offset
                        ScytheOrbitSpeed,  // ai[1] = line rotation speed (CCW)
                        0f                 // ai[2] = unused
                    );
                    if (id >= 0 && id < Main.maxProjectiles)
                    {
                        // Stagger oscillation phase so scythes start at different positions
                        Main.projectile[id].localAI[0] = (float)ScytheProjectileTracking.OscillationPeriod * s / count;
                        Main.projectile[id].timeLeft = ScytheAttackDuration + 60;
                        Main.projectile[id].netUpdate = true;
                    }
                }

                NPC.netUpdate = true;
            }

            if (AITimer >= ScytheAttackDuration)
            {
                scytheArenaOverride = false;
                scytheArenaOverrideTimer = 0f;
                ReturnToIdle();
            }
        }



        // ═══════════════════════════════════════════
        // FALLING SCYTHE, final phase at 1 HP
        // ═══════════════════════════════════════════
        private void AI_FallingScythe(Player target)
        {
            // Intro phase: move to arena center with scale-up + X squeeze
            if (finalPhaseIntroTimer < FinalPhaseIntroDuration)
            {
                finalPhaseIntroTimer++;

                // Move toward arena center
                if (arenaBox != null)
                {
                    Vector2 arenaCenter = arenaBox.Center;
                    Vector2 toCenter = arenaCenter - NPC.Center;
                    float dist = toCenter.Length();
                    if (dist > 4f)
                    {
                        float moveSpeed = MathHelper.Clamp(dist / 20f, 2f, 16f);
                        NPC.velocity = toCenter.SafeNormalize(Vector2.Zero) * moveSpeed;
                    }
                    else
                    {
                        NPC.Center = arenaCenter;
                        NPC.velocity = Vector2.Zero;
                    }
                }
                else
                {
                    NPC.velocity = Vector2.Zero;
                }

                // Scale effects are handled in PreDraw
                return;
            }

            // After intro, Jevil stays still and invisible
            NPC.velocity = Vector2.Zero;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Spawn scythes according to pattern
            if (fallingScythePatternIndex < FallingScythePattern.Length)
            {
                while (fallingScythePatternIndex < FallingScythePattern.Length
                    && FallingScythePattern[fallingScythePatternIndex].tick <= (int)(fallingScythePatternTimer * PatternSpeedMultiplier))
                {
                    var entry = FallingScythePattern[fallingScythePatternIndex];

                    if (arenaBox != null)
                    {
                        float arenaLeft = arenaBox.Center.X - arenaBox.HalfWidth;
                        float arenaWidth = arenaBox.HalfWidth * 2f;
                        float spawnX = arenaLeft + arenaWidth * entry.xFrac;
                        float spawnY = arenaBox.Center.Y - arenaBox.HalfHeight - 60f;

                        int id = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            new Vector2(spawnX, spawnY),
                            new Vector2(0f, ScytheProjectileFalling.FallSpeed),
                            ModContent.ProjectileType<ScytheProjectileFalling>(),
                            AdjustProjectileDamage(ScytheProjectileFalling.DefaultDamage + 10),
                            0f,
                            Main.myPlayer,
                            arenaBox.Center.Y + arenaBox.HalfHeight
                        );
                        if (id >= 0 && id < Main.maxProjectiles)
                            Main.projectile[id].netUpdate = true;
                    }

                    fallingScythePatternIndex++;
                }

                fallingScythePatternTimer++;
            }
            else
            {
                // Pattern finished, spawn giant scythe for the finale
                if (!giantScytheSpawned && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    giantScytheSpawned = true;
                    if (arenaBox != null)
                    {
                        float spawnX = arenaBox.Center.X;
                        float spawnY = arenaBox.Center.Y - arenaBox.HalfHeight;
                        int id = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            new Vector2(spawnX, spawnY),
                            Vector2.Zero,
                            ModContent.ProjectileType<GiantScytheProjectile>(),
                            0,
                            0f,
                            Main.myPlayer,
                            ai0: NPC.whoAmI
                        );
                        if (id >= 0 && id < Main.maxProjectiles)
                            Main.projectile[id].netUpdate = true;
                    }
                }
            }
        }

        // ── Animation ──
        public override void FindFrame(int frameHeight)
        {
            animTick++;
            if (animTick >= AnimTicksPerFrame)
            {
                animTick = 0;
                animFrame++;
                if (animFrame >= IdleFrameCount)
                    animFrame = 0;
            }

            NPC.frame = new Rectangle(animFrame * FrameWidth, 1 * FrameHeight, FrameWidth, FrameHeight);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);

            // During final phase intro, show Jevil with grow + squeeze effect
            if (finalPhaseActive && finalPhaseIntroTimer < FinalPhaseIntroDuration)
            {
                float t = finalPhaseIntroTimer / (float)FinalPhaseIntroDuration;
                // Smooth ease-in-out
                float eased = t * t * (3f - 2f * t);

                // Scale grows from 2x to 3x
                float growScale = MathHelper.Lerp(NPC.scale, NPC.scale * 1.5f, eased);
                // X width shrinks to near zero (like teleport squeeze)
                float xScale = MathHelper.Lerp(1f, 0.05f, eased);
                // Fade out alpha
                float alpha = MathHelper.Lerp(1f, 0f, eased);

                Vector2 drawPos = NPC.Center - screenPos;
                SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Vector2 scale = new Vector2(xScale * growScale, growScale);

                spriteBatch.Draw(texture, drawPos, NPC.frame, Color.White * alpha, NPC.rotation, origin, scale, effects, 0f);
                return false;
            }

            // Hide Jevil after intro is done
            if (finalPhaseActive)
                return false;

            // Draw clone afterimages behind the NPC
            foreach (var clone in activeClones)
            {
                float lifeProgress = clone.Timer / (float)clone.MaxLife;
                float fadeMult = lifeProgress > 0.7f ? (1f - lifeProgress) / 0.3f : 1f;
                float drawAlpha = clone.Alpha * fadeMult;
                Color cloneColor = Color.White * drawAlpha;
                Vector2 clonePos = clone.Position - screenPos;
                int cloneFrame = ((clone.Timer + clone.AnimOffset) / AnimTicksPerFrame) % IdleFrameCount;
                Rectangle cloneRect = new Rectangle(cloneFrame * FrameWidth, 1 * FrameHeight, FrameWidth, FrameHeight);
                spriteBatch.Draw(texture, clonePos, cloneRect, cloneColor, 0f, origin, NPC.scale, clone.Effects, 0f);
            }

            Vector2 npcDrawPos = NPC.Center - screenPos;
            SpriteEffects npcEffects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Apply club teleport tween (X scale squeeze)
            Vector2 npcScale = new Vector2(clubScaleX * NPC.scale, NPC.scale);

            spriteBatch.Draw(texture, npcDrawPos, NPC.frame, drawColor, NPC.rotation, origin, npcScale, npcEffects, 0f);
            return false;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            if (finalPhaseActive) return false;
            return true;
        }

        public override bool? CanBeHitByItem(Player player, Item item)
        {
            if (finalPhaseActive) return false;
            if (!BossArenaSystem.IsPlayerLockedIn(player.whoAmI))
                return false;
            return null;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (finalPhaseActive) return false;
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers
                && !BossArenaSystem.IsPlayerLockedIn(projectile.owner))
                return false;
            return null;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Boss bag (Expert/Master/Revengeance/Death)
            var bagRule = new LeadingConditionRule(new Items.BagDropCondition());
            bagRule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Items.JevilBossBag>()));
            npcLoot.Add(bagRule);

            // Trophy (10% chance on any difficulty)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Trophies.JevilTrophy>(), 10));

            // Relic (Master mode only)
            npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Trophies.JevilRelic>()));

            // Normal mode direct drops (bag handles Expert+/Rev+)
            var directDrop = new LeadingConditionRule(new Items.DirectDropCondition());

            directDrop.OnSuccess(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<Items.DeckOfCards>(),
                ModContent.ItemType<Items.OopsAllCrits>()));

            // 1 guaranteed weapon + 50% chance of a second
            int[] weaponPool = new int[]
            {
                ModContent.ItemType<Items.Devilsknife>(),
                ModContent.ItemType<Items.AceOfSpades>(),
                ModContent.ItemType<Items.QueenOfDiamonds>(),
                ModContent.ItemType<Items.KingOfHearts>(),
                ModContent.ItemType<Items.JackOfClubs>()
            };
            directDrop.OnSuccess(ItemDropRule.OneFromOptions(1, weaponPool));
            directDrop.OnSuccess(ItemDropRule.OneFromOptions(2, weaponPool));

            directDrop.OnSuccess(ItemDropRule.ByCondition(new Items.HasSoulTraitCondition(), ModContent.ItemType<Items.Soulflicker>(), 1));

            npcLoot.Add(directDrop);
        }

        public override void OnKill()
        {
            WhiteFadeAlpha = 0f;
            NPC.SetEventFlagCleared(ref NPC.downedQueenBee, -1);
            Systems.ERAMProgressSystem.JevilDefeated = true;
        }

        public override void BossLoot(ref string name, ref int potionType)
        {
            potionType = ItemID.HealingPotion;
        }
    }
}
