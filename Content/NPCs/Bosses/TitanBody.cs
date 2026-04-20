using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.VFX;
using DeterministicChaos.Content.UI;
using DeterministicChaos.Content.Projectiles.Enemy;
using System;
using System.IO;
using System.Collections.Generic;
using ReLogic.Content;
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

namespace DeterministicChaos.Content.NPCs.Bosses
{
    [AutoloadBossHead]
    public class TitanBody : ModNPC
    {
        // ======== Fight Phase System ========
        public enum FightPhase
        {
            Parkour = 0,
            EnemyClear = 1,
            Cutscene = 2,
            Damage = 3
        }

        // Phase durations in seconds
        private const float PARKOUR_DURATION = 39.5f;
        private const float ENEMY_CLEAR_DURATION = 39.5f; // 1:19 - 0:39.5
        private const float CUTSCENE_DURATION = 3.5f;     // 1:22.5 - 1:19
        public const float DAMAGE_DURATION = 26.5f;      // 1:49 - 1:22.5

        // AI slots
        private ref float PhaseIndex => ref NPC.ai[0];
        public ref float PhaseTimer => ref NPC.ai[1];

        // The position players get teleported back to at loop reset
        private Vector2 playerSpawnPosition = Vector2.Zero;

        // ======== Dark Sea (Parkour rising floor) ========
        private const int SEA_EXTRA_TILES = 50;           // Extra tiles on each side beyond tower width
        private const int SEA_BELOW_TOWER = 20;           // Start 20 tiles below tower bottom
        private const int SEA_END_BELOW_TOP = 20;         // End 20 tiles below tower top
        private const int SEA_SPHERE_COUNT = 25;          // Number of spheres across the surface row
        private const float SEA_SPHERE_SIZE = 80f;        // Base size of each surface sphere
        private const float SEA_SPHERE_PULSE = 15f;       // Pulse amplitude on surface spheres
        private const int SEA_DAMAGE_PER_SECOND = 90;    // Damage per second when submerged
        private const int SEA_DAMAGE_TICK_INTERVAL = 15;  // Ticks between damage applications (4x/sec)
        private int seaDamageTick = 0;                     // Tick counter for damage pacing
        private int clientSeaDamageTick = 0;               // Client-side tick counter for local player damage

        private float seaStartWorldY;      // World Y where sea starts (bottom)
        private float seaEndWorldY;         // World Y where sea ends (top, after full rise)
        private float seaCurrentWorldY;     // Current sea surface Y (interpolated)
        private float seaCenterWorldX;      // Center X of the sea
        private float seaTotalWidth;        // Total pixel width of the sea
        private List<int> seaSphereIDs = new List<int>();
        private bool seaActive = false;
        private float seaDamageAccumulator = 0f;  // Fractional damage accumulator

        // ======== Animation ========
        private const int BODY_FRAME_COUNT = 3;
        private const int BODY_FRAME_WIDTH = 484;
        private const int BODY_FRAME_HEIGHT = 624;
        private const int BODY_FRAME_SPEED = 8; // Ticks per frame
        private int bodyFrameCounter = 0;
        public int BodyAnimFrame { get; private set; } = 0;

        // ======== Damage Multiplier ========
        public float DamageMultiplier { get; set; } = 1f;

        // ======== X-axis Following ========
        private const float MAX_DRIFT_TILES = 10f;        // Max horizontal drift in tiles
        private const float FOLLOW_SPEED = 0.03f;         // Lerp speed for easing (0-1, lower = smoother)
        private float startingWorldX;                      // Initial X position (set on spawn)
        private bool hasStartingX = false;

        // ======== Enemy Clear Spawning ========
        private const float WORM_SPAWN_INTERVAL = 10f;    // Seconds between worm spawns
        private const float BLOB_SPAWN_INTERVAL = 1.5f;     // Seconds between blob spawns
        private float enemySpawnTimer = WORM_SPAWN_INTERVAL; // Start ready to spawn first immediately
        private float blobSpawnTimer = BLOB_SPAWN_INTERVAL;  // Start ready to spawn first immediately

        // ======== TitanSpawn Proximity Spawning ========
        private const float TITAN_SPAWN_INTERVAL = 3f;        // Seconds between TitanSpawn spawns
        private const float TITAN_SPAWN_RADIUS = 15f * 16f;   // 15 tiles in pixels
        private float titanSpawnTimer = 0f;

        // ======== Star Component ========
        private int starNpcIndex = -1;
        private bool starSpawned = false;

        // ======== Hand Components ========
        private bool handsSpawned = false;

        // ======== Hand Laser Coordination ========
        private const float HAND_LASER_INTERVAL = 5f;    // Seconds between hand laser attacks
        private float handLaserTimer = 0f;
        private int nextLaserHandSlot = 0;               // 0=left, 1=right, alternates

        // ======== Arena Attack Coordination ========
        private const float ARENA_ATTACK_INTERVAL = 5f;   // Seconds between arena attacks per hand
        private float arenaAttackTimerLeft = 0f;
        private float arenaAttackTimerRight = 0f;

        // ======== Damage Phase Attack Sequence ========
        // Step 0: Left hand big laser
        // Step 1: Right hand big laser (1s after step 0)
        // Step 2: Both hands 2x small lasers (1s after step 1)
        // Step 3: FloorIsLava telegraph (1s after step 2)
        // Step 4: FloorIsLava kill (1s after step 3)
        // Step 5: Cooldown (2s), then loop back to 0
        private int damageAttackStep = 0;
        private float damageAttackTimer = 0f;
        private bool damageFloorKillTriggered = false; // Prevents repeated kills in one danger window
        private bool damageEndSoundPlayed = false;      // One-shot sound before phase ends

        // ======== Damage Phase Ball Attack (independent timer) ========
        private const float BALL_CHARGE_DURATION = 1.0f;    // Charge phase (white orb growing)
        private const float BALL_FIRE_DURATION = 0.9f;      // Firing window (3 shots at 0.3s each)
        private const float BALL_COOLDOWN_DURATION = 0.6f;   // Cooldown before next cycle
        private const float BALL_FIRE_INTERVAL = 0.3f;       // Time between each shot during fire
        private float ballAttackTimer = 0f;
        private int ballAttackPhase = 0;  // 0=charge, 1=fire, 2=cooldown
        private int ballShotsFired = 0;
        private float ballFireSubTimer = 0f;
        private int ballWhiteProjectileIndex = -1;

        // ======== Final Stand ========
        public const float FINAL_STAND_DURATION = 30f;
        private const float FINAL_LASER_INTERVAL = 0.14f;     // ~7 lasers/second
        private const float FINAL_SHOCKWAVE_INTERVAL = 0.55f; // Shockwave bursts
        private const int FINAL_SEA_ABOVE_TOWER = 12;         // Tiles above tower top at start
        private const int FINAL_SEA_BELOW_TOWER = 0;          // Tiles below tower bottom at end
        private const int FINAL_SEA_EXTRA_TILES = 30;         // Extra tiles on each side
        private const int FINAL_SEA_DAMAGE_TICK_INTERVAL = 15;

        public bool IsInFinalStand { get; private set; } = false;
        private bool finalStandCompleted = false;
        public float finalStandTimer = 0f;
        private float finalLaserTimer = 0f;
        private float finalShockwaveTimer = 0f;
        private float finalSyncTimer = 0f;
        private int finalSeaDamageTick = 0;
        private int clientFinalSeaDamageTick = 0;

        // Final sea sync fields
        private bool finalSeaActive = false;
        private float finalSeaStartWorldY;
        private float finalSeaEndWorldY;
        private float finalSeaCenterWorldX;
        private float finalSeaTotalWidth;

        public FightPhase CurrentPhase => (FightPhase)(int)PhaseIndex;

        private float CurrentPhaseDuration => CurrentPhase switch
        {
            FightPhase.Parkour => PARKOUR_DURATION,
            FightPhase.EnemyClear => ENEMY_CLEAR_DURATION,
            FightPhase.Cutscene => CUTSCENE_DURATION,
            FightPhase.Damage => DAMAGE_DURATION,
            _ => PARKOUR_DURATION
        };

        // ── Multiplayer sync ──────────────────────────────────────────
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(DamageMultiplier);
            writer.Write(seaActive);
            writer.Write(seaStartWorldY);
            writer.Write(seaEndWorldY);
            writer.Write(seaCenterWorldX);
            writer.Write(seaTotalWidth);
            writer.Write(IsInFinalStand);
            writer.Write(finalStandTimer);
            writer.Write(finalSeaActive);
            writer.Write(finalSeaStartWorldY);
            writer.Write(finalSeaEndWorldY);
            writer.Write(finalSeaCenterWorldX);
            writer.Write(finalSeaTotalWidth);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            DamageMultiplier = reader.ReadSingle();
            seaActive = reader.ReadBoolean();
            seaStartWorldY = reader.ReadSingle();
            seaEndWorldY = reader.ReadSingle();
            seaCenterWorldX = reader.ReadSingle();
            seaTotalWidth = reader.ReadSingle();
            IsInFinalStand = reader.ReadBoolean();
            finalStandTimer = reader.ReadSingle();
            finalSeaActive = reader.ReadBoolean();
            finalSeaStartWorldY = reader.ReadSingle();
            finalSeaEndWorldY = reader.ReadSingle();
            finalSeaCenterWorldX = reader.ReadSingle();
            finalSeaTotalWidth = reader.ReadSingle();
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1; // Horizontal sheet, animation handled manually

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.NoMultiplayerSmoothingByType[Type] = true;

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 0;
            NPC.defense = 30;
            NPC.lifeMax = 40000;

            // Apply difficulty scaling directly (NPC.NewNPC doesn't call ApplyDifficultyAndPlayerScaling)
            if (Main.masterMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 3f);
            else if (Main.expertMode)
                NPC.lifeMax = (int)(NPC.lifeMax * 2f);

            // Calamity difficulty mode scaling (Revengeance / Death)
            float calMultiplier = GetCalamityDifficultyMultiplier();
            if (calMultiplier > 1f)
                NPC.lifeMax = (int)(NPC.lifeMax * calMultiplier);

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = Item.buyPrice(0, 15, 0, 0);
            NPC.SpawnWithHigherTime(30);
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.aiStyle = -1;
            NPC.scale = 1f;
            NPC.dontTakeDamage = true; // Invulnerable until Damage phase
            NPC.BossBar = ModContent.GetInstance<TitanBossBar>();
        }

        // Returns an HP multiplier based on Calamity's active difficulty mode.
        // Death Mode: 1.35x, Revengeance Mode: 1.15x, otherwise 1.0x.
        private float GetCalamityDifficultyMultiplier()
        {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod cal))
                return 1f;

            try
            {
                Type calWorldType = cal.Code?.GetType("CalamityMod.World.CalamityWorld");
                if (calWorldType == null)
                    return 1f;

                // Check Death Mode first (it's the harder mode)
                var deathField = calWorldType.GetField("death",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (deathField != null && (bool)deathField.GetValue(null))
                    return 1.35f;

                // Check Revengeance Mode
                var revengeField = calWorldType.GetField("revenge",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (revengeField != null && (bool)revengeField.GetValue(null))
                    return 1.15f;
            }
            catch
            {
                // If reflection fails, just use base scaling
            }

            return 1f;
        }

        // Returns a damage value that always feels like Expert mode.
        // In Journey/Classic, enemy damage is not auto-scaled, so we double it
        // to match the Expert-mode feel. Call this for all Titan projectile and
        // contact damage values.
        public static int ExpertDamage(int baseDamage)
        {
            if (Main.expertMode || Main.masterMode)
                return baseDamage;
            return baseDamage * 2;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,
                new FlavorTextBestiaryInfoElement("A colossal guardian born from the darkness.")
            });
        }

        public override void AI()
        {
            NPC.TargetClosest(true);

            // Cache starting X position
            if (!hasStartingX)
            {
                startingWorldX = NPC.Center.X;
                hasStartingX = true;
            }

            // Loosely follow the target player on X, clamped to ±10 tiles from start
            // Freeze position when a hand is firing its laser or during final stand
            if (!IsAnyHandFiringLaser() && !IsInFinalStand && NPC.target >= 0 && NPC.target < Main.maxPlayers && Main.player[NPC.target].active)
            {
                float targetX = Main.player[NPC.target].Center.X;
                float maxDriftPx = MAX_DRIFT_TILES * 16f;
                float clampedTargetX = MathHelper.Clamp(targetX, startingWorldX - maxDriftPx, startingWorldX + maxDriftPx);

                // Smooth ease toward the clamped target
                float newX = MathHelper.Lerp(NPC.Center.X, clampedTargetX, FOLLOW_SPEED);
                NPC.position.X = newX - NPC.width / 2f;
            }

            NPC.velocity = Vector2.Zero;

            // Spawn the TitanStar on first tick
            if (!starSpawned && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<TitanStar>());
                if (idx >= 0 && idx < Main.maxNPCs)
                {
                    Main.npc[idx].ai[0] = NPC.whoAmI;
                    starNpcIndex = idx;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                starSpawned = true;
            }

            // Spawn the TitanHands on first tick
            if (!handsSpawned && Main.netMode != NetmodeID.MultiplayerClient)
            {
                TitanHand.SpawnHands(this);
                handsSpawned = true;
            }

            // Cache player spawn position from the fountain
            if (playerSpawnPosition == Vector2.Zero && TitanSpawnCutscene.FountainWorldPosition != Vector2.Zero)
            {
                Vector2 fountain = TitanSpawnCutscene.FountainWorldPosition;
                int fountainTileX = (int)(fountain.X / 16f);
                int fountainTileY = (int)(fountain.Y / 16f);
                playerSpawnPosition = new Vector2(fountainTileX * 16 + 8, (fountainTileY - 3) * 16);
            }

            // Despawn if no players are alive (but never during the final stand)
            if (!IsInFinalStand)
            {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
                {
                    NPC.TargetClosest(false);
                    if (NPC.target < 0 || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
                    {
                        if (seaActive) StopDarkSea();
                        if (finalSeaActive)
                        {
                            finalSeaActive = false;
                            if (Main.netMode != NetmodeID.Server) VFX.TitanFinalSea.Deactivate();
                        }
                        TitanSpawnCutscene.StopDarkerFountain();
                        NPC.active = false;
                        return;
                    }
                }
            }

            // ── Client-visible logic (runs on ALL sides) ───────────────
            // Toggle vulnerability based on current phase (synced via ai[0])
            NPC.dontTakeDamage = CurrentPhase != FightPhase.Damage;
            if (IsInFinalStand)
                NPC.dontTakeDamage = true;

            // Clients advance PhaseTimer and finalStandTimer locally for smooth interpolation.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                PhaseTimer += 1f / 60f;
                if (IsInFinalStand)
                    finalStandTimer += 1f / 60f;
            }

            // Client-side dark sea visuals (phase + timer are synced via ai[0]/ai[1])
            // Sea persists through all phases, only reset on Damage→Parkour loop
            if (Main.netMode != NetmodeID.Server && seaActive)
            {
                UpdateClientDarkSea();
            }

            // Client-side sea damage, each client hurts its own player locally
            // (Server-side Player.Hurt doesn't reliably sync to clients)
            if (Main.netMode == NetmodeID.MultiplayerClient && seaActive)
            {
                clientSeaDamageTick++;
                if (clientSeaDamageTick >= SEA_DAMAGE_TICK_INTERVAL)
                {
                    clientSeaDamageTick = 0;
                    Player localPlayer = Main.LocalPlayer;
                    if (localPlayer.active && !localPlayer.dead && localPlayer.Center.Y > seaCurrentWorldY)
                    {
                        int damagePerTick = ExpertDamage(SEA_DAMAGE_PER_SECOND * SEA_DAMAGE_TICK_INTERVAL / 60);
                        localPlayer.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                            damagePerTick, 0, knockback: 0f, armorPenetration: 9999f);
                    }
                }
            }

            // Client-side final sea visuals
            if (Main.netMode != NetmodeID.Server && finalSeaActive)
                UpdateClientFinalSea();

            // Client-side final sea damage (MP clients hurt their own player)
            if (Main.netMode == NetmodeID.MultiplayerClient && finalSeaActive && IsInFinalStand)
            {
                clientFinalSeaDamageTick++;
                if (clientFinalSeaDamageTick >= FINAL_SEA_DAMAGE_TICK_INTERVAL)
                {
                    clientFinalSeaDamageTick = 0;
                    float seaCurrentY = MathHelper.Lerp(finalSeaStartWorldY, finalSeaEndWorldY,
                        MathHelper.Clamp(finalStandTimer / FINAL_STAND_DURATION, 0f, 1f));
                    Player localPlayer = Main.LocalPlayer;
                    if (localPlayer.active && !localPlayer.dead && localPlayer.Center.Y < seaCurrentY)
                    {
                        int dmg = ExpertDamage(SEA_DAMAGE_PER_SECOND * FINAL_SEA_DAMAGE_TICK_INTERVAL / 60);
                        localPlayer.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                            dmg, 0, knockback: 0f, armorPenetration: 9999f);
                    }
                }
            }

            // Client-side sound effects using synced phase state
            if (Main.netMode == NetmodeID.MultiplayerClient && CurrentPhase == FightPhase.Damage)
            {
                if (!damageEndSoundPlayed && PhaseTimer >= DAMAGE_DURATION - 1.6f)
                {
                    damageEndSoundPlayed = true;
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ReverseGlassShatter") { MaxInstances = 1 });
                }
            }

            // Only server/singleplayer advances phase logic and spawns enemies
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Final stand mode, replaces all phase logic
            if (IsInFinalStand)
            {
                UpdateFinalStand();
                return;
            }

            // Advance phase timer
            PhaseTimer += 1f / 60f;

            // Check for phase transition
            if (PhaseTimer >= CurrentPhaseDuration)
            {
                PhaseTimer = 0f;

                if (CurrentPhase == FightPhase.Damage)
                {
                    // Loop back to Parkour and teleport players
                    PhaseIndex = (float)FightPhase.Parkour;
                    TeleportPlayersToSpawn();
                    TitanSpawnCutscene.ReplaceTitanTower();
                    if (seaActive) StopDarkSea();
                    enemySpawnTimer = WORM_SPAWN_INTERVAL; // Ready for next EnemyClear
                    blobSpawnTimer = BLOB_SPAWN_INTERVAL;
                    handLaserTimer = 0f;
                    DamageMultiplier = 1f; // Reset multiplier each loop
                    damageAttackStep = 0;
                    damageAttackTimer = 0f;
                    damageFloorKillTriggered = false;
                    damageEndSoundPlayed = false;
                    titanSpawnTimer = 0f;
                    ballAttackTimer = 0f;
                    ballAttackPhase = 0;
                    ballShotsFired = 0;
                    ballFireSubTimer = 0f;
                    ballWhiteProjectileIndex = -1;
                    ResetHandBehaviors();
                }
                else
                {
                    // Despawn all enemies when leaving EnemyClear; blobs persist through all phases
                    if (CurrentPhase == FightPhase.Parkour)
                    {
                        arenaAttackTimerLeft = 0f;
                        arenaAttackTimerRight = ARENA_ATTACK_INTERVAL / 2f; // Stagger hands
                    }
                    else if (CurrentPhase == FightPhase.EnemyClear)
                        DespawnEnemyClearEnemies();

                    // Advance to next phase
                    PhaseIndex = (float)((int)PhaseIndex + 1);
                }

                NPC.netUpdate = true;
            }

            // Phase-specific logic

            // Animate body sprite sheet
            bodyFrameCounter++;
            if (bodyFrameCounter >= BODY_FRAME_SPEED)
            {
                bodyFrameCounter = 0;
                BodyAnimFrame = (BodyAnimFrame + 1) % BODY_FRAME_COUNT;
            }

            switch (CurrentPhase)
            {
                case FightPhase.Parkour:
                    UpdateParkourSea();
                    blobSpawnTimer += 1f / 60f;
                    if (blobSpawnTimer >= BLOB_SPAWN_INTERVAL)
                    {
                        blobSpawnTimer = 0f;
                        TitanBlob.SpawnBlob(NPC.GetSource_FromAI());
                    }
                    // Trigger alternating hand laser attacks
                    handLaserTimer += 1f / 60f;
                    if (handLaserTimer >= HAND_LASER_INTERVAL)
                    {
                        handLaserTimer = 0f;
                        TriggerHandLaser(nextLaserHandSlot);
                        nextLaserHandSlot = (nextLaserHandSlot + 1) % 2;
                    }
                    // Spawn TitanSpawn if any player is within 15 tiles
                    titanSpawnTimer += 1f / 60f;
                    if (titanSpawnTimer >= TITAN_SPAWN_INTERVAL)
                    {
                        bool playerNearby = false;
                        for (int p = 0; p < Main.maxPlayers; p++)
                        {
                            Player plr = Main.player[p];
                            if (plr.active && !plr.dead && Vector2.Distance(plr.Center, NPC.Center) <= TITAN_SPAWN_RADIUS)
                            {
                                playerNearby = true;
                                break;
                            }
                        }
                        if (playerNearby)
                        {
                            titanSpawnTimer = 0f;
                            TitanSpawn.SpawnAboveTitan(NPC.GetSource_FromAI(), NPC.Center);
                        }
                    }
                    break;
                case FightPhase.EnemyClear:
                    UpdateSeaDamage(); // Sea persists, keep dealing damage
                    blobSpawnTimer += 1f / 60f;
                    if (blobSpawnTimer >= BLOB_SPAWN_INTERVAL)
                    {
                        blobSpawnTimer = 0f;
                        TitanBlob.SpawnBlobNearPlayer(NPC.GetSource_FromAI());
                    }
                    enemySpawnTimer += 1f / 60f;
                    if (enemySpawnTimer >= WORM_SPAWN_INTERVAL)
                    {
                        enemySpawnTimer = 0f;
                        SpawnEnemyClearWorm();
                    }
                    // Arena hand attacks, each hand picks independently
                    arenaAttackTimerLeft += 1f / 60f;
                    arenaAttackTimerRight += 1f / 60f;
                    if (arenaAttackTimerLeft >= ARENA_ATTACK_INTERVAL)
                    {
                        arenaAttackTimerLeft = 0f;
                        TriggerArenaAttack(0);
                    }
                    if (arenaAttackTimerRight >= ARENA_ATTACK_INTERVAL)
                    {
                        arenaAttackTimerRight = 0f;
                        TriggerArenaAttack(1);
                    }
                    break;
                case FightPhase.Cutscene:
                    UpdateSeaDamage(); // Sea persists
                    blobSpawnTimer += 1f / 60f;
                    if (blobSpawnTimer >= BLOB_SPAWN_INTERVAL)
                    {
                        blobSpawnTimer = 0f;
                        TitanBlob.SpawnBlobNearPlayer(NPC.GetSource_FromAI());
                    }
                    // TODO: Short cutscene logic
                    break;
                case FightPhase.Damage:
                    UpdateSeaDamage(); // Sea persists
                    blobSpawnTimer += 1f / 60f;
                    if (blobSpawnTimer >= BLOB_SPAWN_INTERVAL)
                    {
                        blobSpawnTimer = 0f;
                        TitanBlob.SpawnBlobNearPlayer(NPC.GetSource_FromAI());
                    }

                    // Play ReverseGlassShatter + spawn inward shard particles 1.6s before phase ends (once)
                    if (!damageEndSoundPlayed && PhaseTimer >= DAMAGE_DURATION - 1.6f)
                    {
                        damageEndSoundPlayed = true;
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ReverseGlassShatter") { MaxInstances = 1 });
                        StarShardParticle.SpawnInwardConverge(NPC.GetSource_FromAI(), NPC.Center);
                    }

                    // In the last 1.6s, disable skip-return so hands lerp back to default position
                    bool nearDamageEnd = PhaseTimer >= DAMAGE_DURATION - 1.6f;

                    // Enable skip-return on hands so attacks chain without returning to default
                    for (int slot = 0; slot < 2; slot++)
                    {
                        TitanHand h = FindTitanHand(slot);
                        if (h != null)
                        {
                            if (nearDamageEnd)
                            {
                                h.SkipReturnToDefault = false;
                                // Force hands back to default if they're still busy
                                if (h.IsBusy)
                                {
                                    h.CurrentBehavior = TitanHand.HandBehavior.Default;
                                    h.NPC.netUpdate = true;
                                }
                            }
                            else
                            {
                                h.SkipReturnToDefault = true;
                            }
                        }
                    }
                    UpdateDamagePhaseAttacks();
                    UpdateDamagePhaseBallAttack();
                    break;
            }

            // Draw multiplier text is handled in PostDraw
        }

        public override bool CheckDead()
        {
            // On first death: enter final stand instead of dying.
            // On second call (after finalStandCompleted = true): let the NPC die normally.
            if (!finalStandCompleted)
            {
                if (!IsInFinalStand)
                    StartFinalStand();
                return false;
            }
            return true;
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (CurrentPhase == FightPhase.Damage)
            {
                modifiers.FinalDamage *= DamageMultiplier;
            }
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Multiplier text is drawn by TitanBossBar
        }

        // ======== Enemy Clear Spawning ========

        private void SpawnEnemyClearWorm()
        {
            if (!TitanSpawnCutscene.TowerPlaced || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var towerTop = TitanSpawnCutscene.TowerTopLeft;
            int towerCenterX = towerTop.X + TitanSpawnCutscene.TOWER_WIDTH / 2;
            int towerMidY = towerTop.Y + TitanSpawnCutscene.TOWER_HEIGHT / 2;

            // Spawn above the Titan so players have time to react
            bool spawnLeft = Main.rand.NextBool();
            float spawnX = (towerCenterX + (spawnLeft ? -TitanSpawnCutscene.TOWER_WIDTH : TitanSpawnCutscene.TOWER_WIDTH)) * 16f;
            float spawnY = (towerTop.Y - Main.rand.Next(10, 25)) * 16f; // 10–25 tiles above tower top

            TitanWormHead.SpawnWorm(NPC.GetSource_FromAI(), new Vector2(spawnX, spawnY));
        }

        // Despawns all blob NPCs (blob + eye + pupil).
        private void DespawnBlobs()
        {
            int blobType = ModContent.NPCType<TitanBlob>();
            int eyeType = ModContent.NPCType<TitanEye>();
            int pupilType = ModContent.NPCType<TitanPupil>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == blobType || n.type == eyeType || n.type == pupilType))
                {
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                }
            }
        }

        // Despawns all worm NPCs and multiplier orb projectiles without granting any multiplier.
        private void DespawnEnemyClearEnemies()
        {
            int headType = ModContent.NPCType<TitanWormHead>();
            int bodyType = ModContent.NPCType<TitanWormBody>();
            int tailType = ModContent.NPCType<TitanWormTail>();
            int blobType = ModContent.NPCType<TitanBlob>();
            int eyeType = ModContent.NPCType<TitanEye>();
            int pupilType = ModContent.NPCType<TitanPupil>();
            int spawnType = ModContent.NPCType<TitanSpawn>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == headType || n.type == bodyType || n.type == tailType
                    || n.type == blobType || n.type == eyeType || n.type == pupilType
                    || n.type == spawnType))
                {
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                }
            }

            int orbType = ModContent.ProjectileType<Projectiles.Friendly.TitanMultiplierOrb>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == orbType)
                {
                    p.Kill();
                }
            }
        }

        // ======== Hand Laser Coordination ========

        // Triggers a laser attack on the specified hand slot.
        private void TriggerHandLaser(int handSlot)
        {
            TitanHand hand = FindTitanHand(handSlot);
            hand?.StartLaserAttack();
        }

        // Triggers a random arena attack on the specified hand slot.
        // Avoids picking the same attack as the other hand.
        private void TriggerArenaAttack(int handSlot)
        {
            TitanHand hand = FindTitanHand(handSlot);
            if (hand == null || hand.IsBusy) return;

            // Get the other hand's current behavior to avoid duplicates
            TitanHand otherHand = FindTitanHand(1 - handSlot);
            TitanHand.HandBehavior otherBehavior = otherHand?.CurrentBehavior ?? TitanHand.HandBehavior.Default;

            // Available arena attacks
            var attacks = new List<TitanHand.HandBehavior>
            {
                TitanHand.HandBehavior.ArenaLaser,
                TitanHand.HandBehavior.FingerLaunch,
                TitanHand.HandBehavior.Slam
            };

            // Remove what the other hand is currently doing (can't duplicate)
            if (otherBehavior != TitanHand.HandBehavior.Default)
                attacks.Remove(otherBehavior);

            // Pick randomly
            TitanHand.HandBehavior chosen = attacks[Main.rand.Next(attacks.Count)];

            switch (chosen)
            {
                case TitanHand.HandBehavior.ArenaLaser: hand.StartArenaLaser(); break;
                case TitanHand.HandBehavior.FingerLaunch: hand.StartFingerLaunch(); break;
                case TitanHand.HandBehavior.Slam: hand.StartSlam(); break;
            }
        }

        // Finds a TitanHand child NPC by slot (0=left, 1=right).
        private TitanHand FindTitanHand(int slot)
        {
            int handType = ModContent.NPCType<TitanHand>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == handType && (int)n.ai[0] == NPC.whoAmI && (int)n.ai[1] == slot)
                    return n.ModNPC as TitanHand;
            }
            return null;
        }

        // Returns true if any child TitanHand is in its laser indicator/beam/hold phase.
        // Used to freeze body X-following during laser attacks.
        private bool IsAnyHandFiringLaser()
        {
            int handType = ModContent.NPCType<TitanHand>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == handType && (int)n.ai[0] == NPC.whoAmI
                    && n.ModNPC is TitanHand hand && hand.IsLaserFiring)
                    return true;
            }
            return false;
        }

        // ======== Damage Phase Attack Sequence ========

        // Runs the scripted attack sequence during the Damage phase.
        private void UpdateDamagePhaseAttacks()
        {
            damageAttackTimer += 1f / 60f;

            // Stop starting new attacks 5 seconds before the damage phase ends
            //, only let in-progress attacks (steps 4-5) finish naturally
            bool nearPhaseEnd = PhaseTimer >= DAMAGE_DURATION - 5f;
            if (nearPhaseEnd && damageAttackStep <= 3)
            {
                // Jump to cooldown so hands just finish whatever they're doing
                damageAttackStep = 5;
                damageAttackTimer = 0f;
                NPC.netUpdate = true;
                return;
            }

            switch (damageAttackStep)
            {
                case 0: // Left hand big laser
                {
                    TitanHand left = FindTitanHand(0);
                    if (left != null && !left.IsBusy)
                    {
                        left.StartArenaLaser();
                        damageAttackTimer = 0f;
                        damageAttackStep = 1;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: // Wait 1s, then right hand big laser
                {
                    if (damageAttackTimer >= 1f)
                    {
                        TitanHand right = FindTitanHand(1);
                        if (right != null && !right.IsBusy)
                        {
                            right.StartArenaLaser();
                            damageAttackTimer = 0f;
                            damageAttackStep = 2;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
                case 2: // Wait 1s, then both hands fire 2 spread lasers each (4 total)
                {
                    if (damageAttackTimer >= 1f)
                    {
                        TitanHand left = FindTitanHand(0);
                        TitanHand right = FindTitanHand(1);

                        bool leftReady = left != null && !left.IsBusy;
                        bool rightReady = right != null && !right.IsBusy;

                        if (leftReady && rightReady)
                        {
                            left.StartDualLaserSpread();
                            right.StartDualLaserSpread();

                            damageAttackTimer = 0f;
                            damageAttackStep = 3;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
                case 3: // Wait 1s + hands done, then FloorIsLava
                {
                    if (damageAttackTimer >= 1f)
                    {
                        TitanHand left = FindTitanHand(0);
                        TitanHand right = FindTitanHand(1);

                        bool leftReady = left != null && !left.IsBusy;
                        bool rightReady = right != null && !right.IsBusy;

                        if (leftReady && rightReady)
                        {
                            left.StartFloorIsLava();
                            right.StartFloorIsLava();
                            damageAttackTimer = 0f;
                            damageAttackStep = 4;
                            damageFloorKillTriggered = false;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }
                case 4: // FloorIsLava active, wait for danger sub-phase then kill grounded players
                {
                    // Check if either hand is in danger sub-phase (floorSubPhase == 2)
                    TitanHand left = FindTitanHand(0);
                    if (left != null && left.CurrentBehavior == TitanHand.HandBehavior.FloorIsLava)
                    {
                        // The hand's floorSubPhase is private, so we use timing:
                        // Rise=0.5s + Telegraph=1.0s = 1.5s before danger starts
                        // Danger lasts 0.5s
                        if (damageAttackTimer >= 1.5f && !damageFloorKillTriggered)
                        {
                            KillGroundedPlayers();
                            damageFloorKillTriggered = true;
                        }

                        // Wait for hands to finish FloorIsLava (back to Default)
                        if (!left.IsBusy)
                        {
                            damageAttackTimer = 0f;
                            damageAttackStep = 5; // Cooldown
                            NPC.netUpdate = true;
                        }
                    }
                    else
                    {
                        // Hands already done
                        damageAttackTimer = 0f;
                        damageAttackStep = 5;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 5: // 2 second cooldown, then loop
                {
                    if (damageAttackTimer >= 2f)
                    {
                        damageAttackTimer = 0f;
                        damageAttackStep = 0; // Loop back to start
                        NPC.netUpdate = true;
                    }
                    break;
                }
            }
        }

        // ======== Damage Phase Ball Attack (independent timer) ========

        // Runs the ball attack cycle independently during the Damage phase.
        // Charge (1s) → Fire 3 shots (0.9s) → Cooldown (0.6s) → repeat.
        // Total cycle: ~2.5 seconds.
        private void UpdateDamagePhaseBallAttack()
        {
            // Don't start new cycles near phase end
            if (PhaseTimer >= DAMAGE_DURATION - 2.5f && ballAttackPhase == 2)
            {
                // Let cooldown finish but don't start a new cycle
                ballAttackTimer += 1f / 60f;
                return;
            }

            ballAttackTimer += 1f / 60f;

            switch (ballAttackPhase)
            {
                case 0: // Charge, spawn and grow TitanBallWhite
                {
                    if (ballAttackTimer <= 1f / 60f)
                    {
                        // Spawn the white charge indicator on first tick
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 starPos = NPC.Center + new Vector2(0f, -20f);
                            ballWhiteProjectileIndex = Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), starPos, Vector2.Zero,
                                ModContent.ProjectileType<TitanBallWhite>(),
                                0, 0f, Main.myPlayer, ai0: NPC.whoAmI);
                        }
                    }

                    if (ballAttackTimer >= BALL_CHARGE_DURATION)
                    {
                        // Kill the charge indicator
                        if (ballWhiteProjectileIndex >= 0 && ballWhiteProjectileIndex < Main.maxProjectiles
                            && Main.projectile[ballWhiteProjectileIndex].active)
                        {
                            Main.projectile[ballWhiteProjectileIndex].Kill();
                        }
                        ballWhiteProjectileIndex = -1;

                        ballAttackTimer = 0f;
                        ballAttackPhase = 1;
                        ballShotsFired = 0;
                        ballFireSubTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 1: // Fire, launch 3 TitanBallAttacks at 0.3s intervals
                {
                    ballFireSubTimer += 1f / 60f;

                    if (ballShotsFired < 3 && ballFireSubTimer >= BALL_FIRE_INTERVAL)
                    {
                        ballFireSubTimer -= BALL_FIRE_INTERVAL;
                        ballShotsFired++;

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // Aim at closest player
                            Vector2 firePos = NPC.Center + new Vector2(0f, -20f);
                            Player target = Main.player[NPC.target];
                            if (!target.active || target.dead)
                            {
                                NPC.TargetClosest(true);
                                target = Main.player[NPC.target];
                            }

                            Vector2 dir = target.Center - firePos;
                            if (dir.LengthSquared() > 0)
                                dir.Normalize();
                            else
                                dir = -Vector2.UnitY;

                            // Slight spread per shot
                            float spread = (ballShotsFired - 2) * 0.12f; // -0.12, 0, 0.12
                            dir = dir.RotatedBy(spread);

                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), firePos, dir * 5.5f,
                                ModContent.ProjectileType<TitanBallAttack>(),
                                ExpertDamage(40), 2f, Main.myPlayer);

                            // Sound is now played by TitanBallAttack on its first AI tick (client-safe)

                            // Spawn a shockwave at the fire point
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), firePos, Vector2.Zero,
                                ModContent.ProjectileType<Shockwave>(),
                                0, 0f, Main.myPlayer,
                                Main.rand.NextFloat(0f, MathHelper.TwoPi));
                        }
                    }

                    if (ballAttackTimer >= BALL_FIRE_DURATION)
                    {
                        ballAttackTimer = 0f;
                        ballAttackPhase = 2;
                        NPC.netUpdate = true;
                    }
                    break;
                }
                case 2: // Cooldown
                {
                    if (ballAttackTimer >= BALL_COOLDOWN_DURATION)
                    {
                        ballAttackTimer = 0f;
                        ballAttackPhase = 0;
                        NPC.netUpdate = true;
                    }
                    break;
                }
            }
        }

        // Kills all players who are standing on the ground inside the arena.
        // Used by the FloorIsLava attack during the danger sub-phase.
        private void KillGroundedPlayers()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var towerTop = TitanSpawnCutscene.TowerTopLeft;
            float arenaLeft = towerTop.X * 16f;
            float arenaRight = (towerTop.X + TitanSpawnCutscene.TOWER_WIDTH) * 16f;
            float arenaGroundY = towerTop.Y * 16f + 11f * 16f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                // Check if player is within arena X bounds
                if (p.Center.X < arenaLeft || p.Center.X > arenaRight) continue;

                // Check if player is grounded (velocity Y ~0 and near ground level)
                // Player is "on the floor" if their feet are near the arena ground
                float playerBottom = p.position.Y + p.height;
                bool isGrounded = playerBottom >= arenaGroundY - 16f && p.velocity.Y >= -0.01f && p.velocity.Y <= 2f;

                if (isGrounded)
                {
                    p.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                        9999, 0, knockback: 0f, armorPenetration: 9999f);
                }
            }
        }

        // Forces both hands back to Default state. Used when leaving Damage phase.
        private void ResetHandBehaviors()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                TitanHand hand = FindTitanHand(slot);
                if (hand != null)
                {
                    hand.CurrentBehavior = TitanHand.HandBehavior.Default;
                    hand.PendingSecondLaser = false;
                    hand.SkipReturnToDefault = false;
                    hand.NPC.netUpdate = true;
                }
            }
        }

        // Client-side dark sea visual update. Computes sea Y from synced phase timer
        // and updates the visual system. No damage logic here.
        private void UpdateClientDarkSea()
        {
            if (!TitanDarkSea.IsActive)
            {
                // Initialize client-side sea visuals using synced sea params
                if (seaStartWorldY > 0f && seaTotalWidth > 0f)
                {
                    TitanDarkSea.Activate(seaCenterWorldX, seaStartWorldY, seaTotalWidth);
                }
                return;
            }

            // During Parkour, rise over 80% of the phase duration (must match server rate)
            // After Parkour, hold at the fully-risen position
            if (CurrentPhase == FightPhase.Parkour)
            {
                float riseProgress = MathHelper.Clamp(PhaseTimer / (PARKOUR_DURATION * 0.8f), 0f, 1f);
                seaCurrentWorldY = MathHelper.Lerp(seaStartWorldY, seaEndWorldY, riseProgress);
            }
            // else: seaCurrentWorldY stays at its last value (fully risen)

            TitanDarkSea.SetSurfaceY(seaCurrentWorldY);
        }

        // ======== Dark Sea Methods ========

        // Initializes and activates the rising dark sea for the Parkour phase.
        private void InitDarkSea()
        {
            if (!TitanSpawnCutscene.TowerPlaced)
                return;

            var towerTop = TitanSpawnCutscene.TowerTopLeft;
            int towerTopY = towerTop.Y;
            int towerBottomY = towerTopY + TitanSpawnCutscene.TOWER_HEIGHT - 5;
            int towerCenterX = towerTop.X + TitanSpawnCutscene.TOWER_WIDTH / 2;

            // Sea starts 20 tiles below tower bottom, ends 15 tiles below tower top
            seaStartWorldY = (towerBottomY + SEA_BELOW_TOWER) * 16f;
            seaEndWorldY = (towerTopY + SEA_END_BELOW_TOP) * 16f;
            seaCurrentWorldY = seaStartWorldY;
            seaCenterWorldX = towerCenterX * 16f + 8f;

            // Width: tower width + 30 tiles on each side (in pixels)
            seaTotalWidth = (TitanSpawnCutscene.TOWER_WIDTH + SEA_EXTRA_TILES * 2) * 16f;

            seaActive = true;
            NPC.netUpdate = true; // Sync sea parameters to clients

            // Activate the visual system (client-side only, for singleplayer)
            if (Main.netMode != NetmodeID.Server)
            {
                TitanDarkSea.Activate(seaCenterWorldX, seaCurrentWorldY, seaTotalWidth);
            }
        }

        // Updates the dark sea each tick during Parkour: rises the surface and damages submerged players.
        private void UpdateParkourSea()
        {
            if (!seaActive)
            {
                InitDarkSea();
                return;
            }

            // Lerp surface Y from start to end over 80% of parkour, then hold at top
            float riseProgress = MathHelper.Clamp(PhaseTimer / (PARKOUR_DURATION * 0.8f), 0f, 1f);
            seaCurrentWorldY = MathHelper.Lerp(seaStartWorldY, seaEndWorldY, riseProgress);

            // Update visual system (singleplayer only, MP clients update in UpdateClientDarkSea)
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                TitanDarkSea.SetSurfaceY(seaCurrentWorldY);
            }

            // Damage players below the sea surface (server/singleplayer only)
            seaDamageTick++;
            if (seaDamageTick >= SEA_DAMAGE_TICK_INTERVAL)
            {
                seaDamageTick = 0;
                int damagePerTick = ExpertDamage(SEA_DAMAGE_PER_SECOND * SEA_DAMAGE_TICK_INTERVAL / 60);
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead && p.Center.Y > seaCurrentWorldY)
                    {
                        p.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                            damagePerTick, 0, knockback: 0f, armorPenetration: 9999f);
                    }
                }
            }
        }

        // Deals damage to players below the sea surface (sea holds at final risen position).
        // Called during non-Parkour phases to keep the sea active.
        private void UpdateSeaDamage()
        {
            if (!seaActive)
                return;

            // Sea stays at its final risen position (seaCurrentWorldY unchanged)
            // Update visual system for singleplayer
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                TitanDarkSea.SetSurfaceY(seaCurrentWorldY);
            }

            // Damage players below the sea surface
            seaDamageTick++;
            if (seaDamageTick >= SEA_DAMAGE_TICK_INTERVAL)
            {
                seaDamageTick = 0;
                int damagePerTick = ExpertDamage(SEA_DAMAGE_PER_SECOND * SEA_DAMAGE_TICK_INTERVAL / 60);
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead && p.Center.Y > seaCurrentWorldY)
                    {
                        p.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                            damagePerTick, 0, knockback: 0f, armorPenetration: 9999f);
                    }
                }
            }
        }

        // Stops the dark sea and cleans up visuals.
        private void StopDarkSea()
        {
            seaActive = false;
            if (Main.netMode != NetmodeID.Server)
            {
                TitanDarkSea.Deactivate();
            }
        }

        // ======== Final Stand ========

        // Begins the final stand: the Titan survives at 1 HP for 30 seconds, shaking violently
        // while lasers rain from its face and the inverted dark sea descends from above.
        private void StartFinalStand()
        {
            IsInFinalStand = true;
            finalStandTimer = 0f;
            finalLaserTimer = 0f;
            finalShockwaveTimer = 0f;
            finalSyncTimer = 0f;
            finalSeaDamageTick = 0;
            clientFinalSeaDamageTick = 0;

            NPC.life = 1;
            NPC.dontTakeDamage = true;
            NPC.netUpdate = true;

            // Remove the bottom lava floor
            if (seaActive) StopDarkSea();

            // Despawn all attacking minions so only the Titan body (and wings) remain
            DespawnAllTitanMinions();

            // Start the inverted ceiling sea
            InitFinalSea();
        }

        // Server-side tick logic for the final stand.
        private void UpdateFinalStand()
        {
            finalStandTimer += 1f / 60f;

            // Update the inverted sea surface Y and damage players caught above it
            if (finalSeaActive)
            {
                float descentProgress = MathHelper.Clamp(finalStandTimer / FINAL_STAND_DURATION, 0f, 1f);
                float seaCurrentY = MathHelper.Lerp(finalSeaStartWorldY, finalSeaEndWorldY, descentProgress);

                if (Main.netMode == NetmodeID.SinglePlayer)
                    VFX.TitanFinalSea.SetSurfaceY(seaCurrentY);

                // Damage players above the descending ceiling
                finalSeaDamageTick++;
                if (finalSeaDamageTick >= FINAL_SEA_DAMAGE_TICK_INTERVAL)
                {
                    finalSeaDamageTick = 0;
                    int dmg = ExpertDamage(SEA_DAMAGE_PER_SECOND * FINAL_SEA_DAMAGE_TICK_INTERVAL / 60);
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        Player p = Main.player[i];
                        if (p.active && !p.dead && p.Center.Y < seaCurrentY)
                        {
                            p.Hurt(Terraria.DataStructures.PlayerDeathReason.LegacyDefault(),
                                dmg, 0, knockback: 0f, armorPenetration: 9999f);
                        }
                    }
                }
            }

            // Spawn shockwave bursts around the Titan body
            finalShockwaveTimer += 1f / 60f;
            if (finalShockwaveTimer >= FINAL_SHOCKWAVE_INTERVAL)
            {
                finalShockwaveTimer = 0f;
                SpawnFinalShockwaves();
            }

            // Periodically push sync to correct client timer drift
            finalSyncTimer += 1f / 60f;
            if (finalSyncTimer >= 1f)
            {
                finalSyncTimer = 0f;
                NPC.netUpdate = true;
            }

            // End final stand after 30 seconds
            if (finalStandTimer >= FINAL_STAND_DURATION)
                FinallyKillTitan();

            // Per-tick screen shake, runs on server/singleplayer but camera modifier
            // only affects the local client (no-op on dedicated server)
            if (Main.netMode != NetmodeID.Server)
            {
                float shakeProgress = MathHelper.Clamp(finalStandTimer / FINAL_STAND_DURATION, 0f, 1f);
                // Escalates from ~3 at the start to ~18 at the end
                float strength = MathHelper.Lerp(3f, 18f, shakeProgress);
                Vector2 shakeDir = new Vector2(
                    (float)Math.Sin(finalStandTimer * 11.3f),
                    (float)Math.Cos(finalStandTimer * 7.7f));
                Main.instance.CameraModifiers.Add(
                    new PunchCameraModifier(NPC.Center, shakeDir, strength, 6f, 4, 3000f, "TitanFinalStand"));
            }
        }

        // Client-side: activate/update the TitanFinalSea visual using synced parameters.
        private void UpdateClientFinalSea()
        {
            if (!VFX.TitanFinalSea.IsActive)
            {
                if (finalSeaActive && finalSeaStartWorldY > 0f && finalSeaTotalWidth > 0f)
                    VFX.TitanFinalSea.Activate(finalSeaCenterWorldX, finalSeaStartWorldY, finalSeaTotalWidth);
                return;
            }

            float descentProgress = MathHelper.Clamp(finalStandTimer / FINAL_STAND_DURATION, 0f, 1f);
            float seaCurrentY = MathHelper.Lerp(finalSeaStartWorldY, finalSeaEndWorldY, descentProgress);
            VFX.TitanFinalSea.SetSurfaceY(seaCurrentY);
        }

        // Initializes the inverted dark sea for the final stand.
        private void InitFinalSea()
        {
            if (!TitanSpawnCutscene.TowerPlaced)
                return;

            var towerTop = TitanSpawnCutscene.TowerTopLeft;
            int towerTopY = towerTop.Y;
            int towerBottomY = towerTopY + TitanSpawnCutscene.TOWER_HEIGHT;
            int towerCenterX = towerTop.X + TitanSpawnCutscene.TOWER_WIDTH / 2;

            // Ceiling starts above the tower and descends to past the bottom
            finalSeaStartWorldY = (towerTopY - FINAL_SEA_ABOVE_TOWER) * 16f;
            finalSeaEndWorldY = (towerBottomY + FINAL_SEA_BELOW_TOWER) * 16f;
            finalSeaCenterWorldX = towerCenterX * 16f + 8f;
            finalSeaTotalWidth = (TitanSpawnCutscene.TOWER_WIDTH + FINAL_SEA_EXTRA_TILES * 2) * 16f;

            finalSeaActive = true;
            NPC.netUpdate = true;

            if (Main.netMode != NetmodeID.Server)
                VFX.TitanFinalSea.Activate(finalSeaCenterWorldX, finalSeaStartWorldY, finalSeaTotalWidth);
        }

        // Despawns all Titan minion/enemy NPCs (hands, star, worms, blobs, spawns).
        // Wings are intentionally kept for visuals during the final stand.
        private void DespawnAllTitanMinions()
        {
            int handType = ModContent.NPCType<TitanHand>();
            int starType = ModContent.NPCType<TitanStar>();
            int wormHeadType = ModContent.NPCType<TitanWormHead>();
            int wormBodyType = ModContent.NPCType<TitanWormBody>();
            int wormTailType = ModContent.NPCType<TitanWormTail>();
            int blobType = ModContent.NPCType<TitanBlob>();
            int eyeType = ModContent.NPCType<TitanEye>();
            int pupilType = ModContent.NPCType<TitanPupil>();
            int spawnType = ModContent.NPCType<TitanSpawn>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active) continue;

                // Hands are intentionally kept alive so they shake during the death sequence.
                if (n.type == starType
                    || n.type == wormHeadType || n.type == wormBodyType || n.type == wormTailType
                    || n.type == blobType || n.type == eyeType || n.type == pupilType
                    || n.type == spawnType)
                {
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
                }
            }

            // Despawn active titan attack projectiles too
            int laserBeamType = ModContent.ProjectileType<Projectiles.Enemy.TitanLaserBeam>();
            int laserIndType = ModContent.ProjectileType<Projectiles.Enemy.TitanLaserIndicator>();
            int ballType = ModContent.ProjectileType<Projectiles.Enemy.TitanBallAttack>();
            int ballWhiteType = ModContent.ProjectileType<Projectiles.Enemy.TitanBallWhite>();
            int fingerType = ModContent.ProjectileType<Projectiles.Enemy.TitanFingerProjectile>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == laserBeamType || p.type == laserIndType
                    || p.type == ballType || p.type == ballWhiteType || p.type == fingerType))
                    p.Kill();
            }
        }

        // Spawns 2–3 shockwave visual effects scattered around the Titan body.
        private void SpawnFinalShockwaves()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 2–3 shockwaves close to the Titan body
            int bodyCount = 2 + Main.rand.Next(2);
            for (int i = 0; i < bodyCount; i++)
            {
                Vector2 pos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-210f, 210f),
                    Main.rand.NextFloat(-310f, 310f));
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<VFX.Shockwave>(),
                    0, 0f, Main.myPlayer,
                    Main.rand.NextFloat(MathHelper.TwoPi));
            }

            // 3–5 additional shockwaves scattered across the full tower interior
            if (TitanSpawnCutscene.TowerPlaced)
            {
                var towerTop = TitanSpawnCutscene.TowerTopLeft;
                float towerLeftPx  = towerTop.X * 16f;
                float towerTopPx   = towerTop.Y * 16f;
                float towerWidthPx = TitanSpawnCutscene.TOWER_WIDTH  * 16f;
                float towerHeightPx = TitanSpawnCutscene.TOWER_HEIGHT * 16f;

                int towerCount = 3 + Main.rand.Next(3);
                for (int i = 0; i < towerCount; i++)
                {
                    Vector2 pos = new Vector2(
                        towerLeftPx  + Main.rand.NextFloat(0.1f, 0.9f) * towerWidthPx,
                        towerTopPx   + Main.rand.NextFloat(0.05f, 0.95f) * towerHeightPx);
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<VFX.Shockwave>(),
                        0, 0f, Main.myPlayer,
                        Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }
        }

        // Ends the final stand and kills the Titan, triggering OnKill and item drops.
        private void FinallyKillTitan()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Deactivate final sea
            finalSeaActive = false;
            if (Main.netMode != NetmodeID.Server)
                VFX.TitanFinalSea.Deactivate();

            // Let CheckDead() pass through on the next call
            finalStandCompleted = true;
            NPC.life = 0;
            NPC.checkDead();
        }

        // Teleports all active players in the dark world back to the bottom-center of the tower.
        private void TeleportPlayersToSpawn()
        {
            if (playerSpawnPosition == Vector2.Zero)
                return;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    p.Teleport(playerSpawnPosition, -1);
                    p.velocity = Vector2.Zero;
                    p.fallStart = (int)(playerSpawnPosition.Y / 16f);

                    // Sync teleport to the client in multiplayer
                    if (Main.netMode == NetmodeID.Server)
                    {
                        RemoteClient.CheckSection(i, playerSpawnPosition);
                        NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null,
                            0, // 0 = player teleport
                            i, // player index
                            playerSpawnPosition.X,
                            playerSpawnPosition.Y,
                            -1); // teleport style
                    }
                }
            }
        }

        public override void OnKill()
        {
            // Stop both dark seas
            if (seaActive) StopDarkSea();
            if (finalSeaActive)
            {
                finalSeaActive = false;
                if (Main.netMode != NetmodeID.Server) VFX.TitanFinalSea.Deactivate();
            }

            // Stop the darker fountain and remove the tower
            TitanSpawnCutscene.StopDarkerFountain();

            // Mark as defeated for Boss Checklist (world-persistent)
            Systems.ERAMProgressSystem.TitanDefeated = true;
        }

        public override void BossLoot(ref string name, ref int potionType)
        {
            name = "Titan";
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Boss bag (Expert/Master/Revengeance/Death)
            var bagRule = new LeadingConditionRule(new BagDropCondition());
            bagRule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<TitanBossBag>()));
            npcLoot.Add(bagRule);

            // Trophy (10% chance on any difficulty)
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Trophies.TitanTrophy>(), 10));

            // Relic (Master mode only)
            npcLoot.Add(ItemDropRule.MasterModeCommonDrop(ModContent.ItemType<Trophies.TitanRelic>()));

            // Normal mode direct drops (bag handles Expert+/Rev+)
            var directDrop = new LeadingConditionRule(new DirectDropCondition());

            directDrop.OnSuccess(new PickTwoDropRule(
                ModContent.ItemType<ForthcomingWrath>(),
                ModContent.ItemType<LodestoneFork>(),
                ModContent.ItemType<Cascade>(),
                ModContent.ItemType<ShatteredGlass>(),
                ModContent.ItemType<Leyline>(),
                ModContent.ItemType<Appendage>()));

            directDrop.OnSuccess(ItemDropRule.OneFromOptions(1, ModContent.ItemType<TitanicEmblem>()));
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Items.Accessories.TitanStar>(), 4));

            // Titansblood (15-30)
            directDrop.OnSuccess(ItemDropRule.Common(ModContent.ItemType<Titansblood>(), 1, 15, 30));

            npcLoot.Add(directDrop);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Skip normal NPC layer draw, body is drawn behind tiles by TitanSpawnCutscene
            return false;
        }

        public override bool CheckActive()
        {
            // Don't despawn naturally
            return false;
        }
    }
}
