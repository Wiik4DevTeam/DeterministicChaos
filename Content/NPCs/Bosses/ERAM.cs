using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class ERAM : ModNPC
    {
        private const int FrameWidth = 32;
        private const int FrameHeight = 32;
        private const int FramesPerColumn = 6;
        
        private int animTick;
        private int animFrame;
        private const int AnimTicksPerFrame = 8;
        
        private bool isLaughing = false;
        
        private static int bossHeadSlot = -1;
        
        // Attack state
        private enum AttackState
        {
            Normal = 0,
            FireCircle = 1,
            BombAttack = 2,
            MeteorAttack = 3,
            SpawnGoons = 4,
            Defeated = 5
        }
        
        // Track if post-defeat dialogue has started
        private bool defeatedDialogueStarted = false;
        
        // AI fields
        private ref float AIState => ref NPC.ai[0];
        private ref float AITimer => ref NPC.ai[1];
        private ref float AICounter => ref NPC.ai[2];
        private ref float AIExtra => ref NPC.ai[3];
        
        // Attack timing
        private const int NormalDuration = 120;
        private const int FireCircleDuration = 800;
        private const int BombAttackDuration = 360;
        private const int MeteorAttackDuration = 400;
        private const int SpawnGoonsDuration = 180;
        
        // Returns difficulty phase 0-2 based on health remaining
        // Phase 0 = full health (100%-76%)
        // Phase 1 = 75% health remaining (75%-51%)
        // Phase 2 = 50% health remaining and below
        private int GetDifficultyPhase()
        {
            float healthPercent = NPC.life / (float)NPC.lifeMax;
            if (healthPercent <= 0.50f) return 2; // 50% or less remaining
            if (healthPercent <= 0.75f) return 1; // 75% or less remaining
            return 0; // Full health (above 75%)
        }
        
        // Get number of PixelFriends to spawn based on phase (3, 4, 5)
        private int GetPixelFriendCount()
        {
            int phase = GetDifficultyPhase();
            return phase switch
            {
                0 => 3, // Full health
                1 => 4, // 75% health
                2 => 5, // 50% health and below
                _ => 3
            };
        }
        
        // Get number of bombs for bomb attack (4, 6, 8)
        private int GetBombCount()
        {
            int phase = GetDifficultyPhase();
            return phase switch
            {
                0 => 4, // Full health
                1 => 6, // 75% health
                2 => 8, // 50% health and below
                _ => 4
            };
        }
        
        // Get number of fire streams (2, 3, 4)
        private int GetFireStreamCount()
        {
            int phase = GetDifficultyPhase();
            return phase switch
            {
                0 => 2, // Full health
                1 => 3, // 75% health
                2 => 4, // 50% health and below
                _ => 2
            };
        }
        
        // Get meteor teleport delay in ticks (0.8s, 0.4s, 0s = 48, 24, 0 ticks)
        private int GetMeteorTeleportDelay()
        {
            int phase = GetDifficultyPhase();
            return phase switch
            {
                0 => 48, // 0.8 seconds at full health
                1 => 24, // 0.4 seconds at 75% health
                2 => 0,  // 0 seconds at 50% health and below
                _ => 48
            };
        }
        
        // Arena bounds
        private Vector2 ArenaCenter => new Vector2(100f * 16f, 100f * 16f);
        private const float ArenaHalfWidth = 20f * 16f;
        private const float ArenaHalfHeight = 12f * 16f;
        
        // Sound effects
        private static SoundStyle PixelLaugh;
        private static SoundStyle PixelBomb;
        private static SoundStyle PixelFire;
        private static SoundStyle PixelFlutter;
        private static SoundStyle PixelStartFire;
        private static SoundStyle PixelUp;
        private static SoundStyle PixelDash;
        
        // Timer for periodic laugh sound
        private int laughSoundTimer = 0;
        private const int LaughSoundInterval = 60; // Play laugh every 1 second
        
        // Attack cycling, tracks which attacks have been used
        private System.Collections.Generic.List<int> availableAttacks = new System.Collections.Generic.List<int>();

        public override void Load()
        {
            // Manually load the boss head icon using ERAMIcon
            string texture = "DeterministicChaos/Content/NPCs/Bosses/ERAMIcon";
            bossHeadSlot = Mod.AddBossHeadTexture(texture, -1);
            
            // Initialize sound effects
            PixelLaugh = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelLaugh");
            PixelBomb = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelBomb");
            PixelFire = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelFire");
            PixelFlutter = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelFlutter");
            PixelStartFire = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelStartFire");
            PixelUp = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelUp");
            PixelDash = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelDash");
        }

        public override void BossHeadSlot(ref int index)
        {
            index = bossHeadSlot;
        }
        
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.OnFire] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 0;
            NPC.defense = 15;
            NPC.lifeMax = 1600;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            NPC.SpawnWithHigherTime(30);
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.aiStyle = -1;
            NPC.scale = 2.2f;
            
            // Music is handled by ERAMSceneEffect for dynamic cutscene/fight switching
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,
                new FlavorTextBestiaryInfoElement("A mysterious entity lurking in isolated dimensions.")
            });
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            // Sync localAI values for multiplayer
            writer.Write(NPC.localAI[0]);
            writer.Write(NPC.localAI[1]);
            writer.Write(NPC.localAI[2]);
            writer.Write(NPC.localAI[3]);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            // Receive synced localAI values
            NPC.localAI[0] = reader.ReadSingle();
            NPC.localAI[1] = reader.ReadSingle();
            NPC.localAI[2] = reader.ReadSingle();
            NPC.localAI[3] = reader.ReadSingle();
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];
            
            if (!target.active || target.dead)
            {
                NPC.velocity.Y -= 0.4f;
                if (NPC.timeLeft > 10)
                    NPC.timeLeft = 10;
                return;
            }
            
            // Face player
            NPC.spriteDirection = target.Center.X < NPC.Center.X ? 1 : -1;
            
            // At 10% health or below, immediately switch to meteor attack if not already doing it
            // But don't override Defeated state
            float enrageHealthPercent = NPC.life / (float)NPC.lifeMax;
            if (enrageHealthPercent <= 0.10f && AIState != (float)AttackState.MeteorAttack && AIState != (float)AttackState.Defeated)
            {
                AIState = (float)AttackState.MeteorAttack;
                AITimer = 0;
                AICounter = 0;
                AIExtra = 0;
                NPC.localAI[0] = 0;
                NPC.localAI[1] = 0;
                isLaughing = false;
                NPC.netUpdate = true;
            }
            
            AITimer++;
            
            // Play laugh sound periodically while laughing
            if (isLaughing)
            {
                laughSoundTimer++;
                if (laughSoundTimer >= LaughSoundInterval)
                {
                    SoundEngine.PlaySound(PixelLaugh, NPC.Center);
                    laughSoundTimer = 0;
                }
            }
            else
            {
                laughSoundTimer = 0;
            }
            
            switch ((AttackState)(int)AIState)
            {
                case AttackState.Normal:
                    DoNormalState();
                    break;
                case AttackState.FireCircle:
                    DoFireCircleState(target);
                    break;
                case AttackState.BombAttack:
                    DoBombAttackState(target);
                    break;
                case AttackState.MeteorAttack:
                    DoMeteorAttackState(target);
                    break;
                case AttackState.SpawnGoons:
                    DoSpawnGoonsState(target);
                    break;
                case AttackState.Defeated:
                    DoDefeatedState();
                    break;
            }
        }
        
        private void MoveToPosition(Vector2 targetPos, float speed)
        {
            Vector2 toTarget = targetPos - NPC.Center;
            float dist = toTarget.Length();
            
            if (dist > 20f)
            {
                NPC.velocity = toTarget.SafeNormalize(Vector2.Zero) * speed;
            }
            else
            {
                NPC.velocity *= 0.9f;
            }
        }
        
        private void TransitionToNextAttack()
        {
            // Only server picks next attack to ensure sync
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
                
            AITimer = 0;
            AICounter = 0;
            AIExtra = 0;
            
            // Refill attack pool if empty
            if (availableAttacks.Count == 0)
            {
                availableAttacks.Add((int)AttackState.FireCircle);
                availableAttacks.Add((int)AttackState.BombAttack);
                availableAttacks.Add((int)AttackState.MeteorAttack);
                availableAttacks.Add((int)AttackState.SpawnGoons);
            }
            
            // Pick random attack from available pool
            int randomIndex = Main.rand.Next(availableAttacks.Count);
            int nextAttack = availableAttacks[randomIndex];
            availableAttacks.RemoveAt(randomIndex);
            
            AIState = nextAttack;
            NPC.netUpdate = true;
            
            // Stop laughing when starting an attack
            isLaughing = false;
        }
        
        private void DoNormalState()
        {
            isLaughing = true;
            MoveToPosition(ArenaCenter, 4f);
            
            if (AITimer >= NormalDuration)
            {
                // At 10% health or below, always do meteor attack
                float healthPercent = NPC.life / (float)NPC.lifeMax;
                if (healthPercent <= 0.10f)
                {
                    AITimer = 0;
                    AICounter = 0;
                    AIExtra = 0;
                    AIState = (float)AttackState.MeteorAttack;
                    isLaughing = false;
                }
                else
                {
                    TransitionToNextAttack();
                }
            }
        }
        
        private void DoFireCircleState(Player target)
        {
            isLaughing = false;
            MoveToPosition(ArenaCenter, 4f);
            
            // Play PixelStartFire at start of attack
            if (AITimer == 1)
            {
                SoundEngine.PlaySound(PixelStartFire, NPC.Center);
            }
            
            // Orbit fire spheres in triangle pattern, calculate from AITimer for sync
            float fireOrbitAngle = AITimer * 0.009f;
            
            if (AITimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int projType = ModContent.ProjectileType<FireSphere>();
                int streamCount = GetFireStreamCount();
                float streamSpacing = MathHelper.TwoPi / streamCount;
                
                for (int i = 0; i < streamCount; i++)
                {
                    float angle = fireOrbitAngle + (streamSpacing * i);
                    Vector2 spawnOffset = new Vector2(30f, 0f).RotatedBy(angle);
                    Vector2 spawnPos = NPC.Center + spawnOffset;
                    
                    // Fire away from boss (opposite direction of spawn offset)
                    Vector2 velocity = (-spawnOffset).SafeNormalize(Vector2.Zero) * -6f;
                    
                    int p = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        velocity,
                        projType,
                        25,
                        0f,
                        Main.myPlayer
                    );
                    
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].netUpdate = true;
                }
            }
            
            if (AITimer >= FireCircleDuration)
            {
                AIState = (float)AttackState.Normal;
                AITimer = -60; // 1 second downtime
            }
        }
        
        private void DoBombAttackState(Player target)
        {
            isLaughing = false;
            
            // Move side to side
            float sideOffset = AIExtra == 0 ? -ArenaHalfWidth * 0.6f : ArenaHalfWidth * 0.6f;
            Vector2 sidePos = ArenaCenter + new Vector2(sideOffset, 0);
            
            MoveToPosition(sidePos, 5f);
            
            // Switch sides periodically
            if (AITimer % 90 == 0)
            {
                AIExtra = AIExtra == 0 ? 1 : 0;
                // Play PixelFlutter when switching sides
                SoundEngine.PlaySound(PixelFlutter, NPC.Center);
            }
            
            // Spawn bombs, spawn interval adjusted to fit total bombs in attack duration
            int totalBombs = GetBombCount();
            int bombInterval = BombAttackDuration / (totalBombs + 1);
            if (bombInterval < 20) bombInterval = 20;
            
            if (AITimer % bombInterval == 0 && AITimer > 0 && AITimer <= bombInterval * totalBombs)
            {
                // Play PixelFire when spawning bomb (all clients)
                SoundEngine.PlaySound(PixelFire, NPC.Center);
                
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int projType = ModContent.ProjectileType<PixelBomb>();
                    
                    // Random position in arena for bomb target
                    float randX = Main.rand.NextFloat(-ArenaHalfWidth + 32, ArenaHalfWidth - 32);
                    float randY = Main.rand.NextFloat(-ArenaHalfHeight + 32, ArenaHalfHeight - 32);
                    Vector2 targetPos = ArenaCenter + new Vector2(randX, randY);
                    
                    // Spawn bomb from boss, pass target position in ai[0] and ai[1]
                    int p = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        Vector2.Zero,
                        projType,
                        0,
                        0f,
                        Main.myPlayer,
                        targetPos.X,
                        targetPos.Y
                    );
                    
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].netUpdate = true;
                }
            }
            
            if (AITimer >= BombAttackDuration)
            {
                AIState = (float)AttackState.Normal;
                AITimer = -180; // 3 second downtime
            }
        }
        
        private void DoMeteorAttackState(Player target)
        {
            isLaughing = false;
            
            float arenaTop = ArenaCenter.Y - ArenaHalfHeight - 264f;
            float arenaBottom = ArenaCenter.Y + ArenaHalfHeight + 264f;
            
            // AICounter tracks phase: 0 = moving to top, 1 = falling, 2+ = continue falling
            // AIExtra stores horizontal offset for starting position and side drift
            // NPC.localAI[0] stores drift direction: -1 or 1
            if (AICounter == 0)
            {
                // Move to random position above arena, server picks position
                if (NPC.localAI[0] == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    AIExtra = Main.rand.NextFloat(-ArenaHalfWidth * 0.7f, ArenaHalfWidth * 0.7f);
                    NPC.netUpdate = true;
                }
                
                // Play PixelUp when starting ascent (first frame only)
                if (AITimer == 1)
                {
                    SoundEngine.PlaySound(PixelUp, NPC.Center);
                }
                
                Vector2 topPos = new Vector2(ArenaCenter.X + AIExtra, arenaTop);
                MoveToPosition(topPos, 8f);
                
                if (Vector2.Distance(NPC.Center, topPos) < 30f)
                {
                    AICounter = 1;
                    // Choose slight horizontal drift direction, server only
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        NPC.localAI[0] = Main.rand.NextBool() ? 1f : -1f;
                        NPC.netUpdate = true;
                    }
                    // Play PixelDash when starting descent
                    SoundEngine.PlaySound(PixelDash, NPC.Center);
                }
            }
            else
            {
                // Fall down at a slight angle and leave fire trail
                float sideSpeed = NPC.localAI[0] * 1.5f;
                NPC.velocity = new Vector2(sideSpeed, 18f);
                
                // Spawn fire trail
                if (AITimer % 2 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int projType = ModContent.ProjectileType<FireTrail>();
                    
                    int p = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        Vector2.Zero,
                        projType,
                        21,
                        0f,
                        Main.myPlayer
                    );
                    
                    if (p >= 0 && p < Main.maxProjectiles)
                        Main.projectile[p].netUpdate = true;
                }
                
                // Check if reached past bottom
                if (NPC.Center.Y >= arenaBottom)
                {
                    // Check health for enrage phase
                    float healthPercent = NPC.life / (float)NPC.lifeMax;
                    bool isEnraged = healthPercent <= 0.10f;
                    
                    // Check if this is the last pass (but not during enrage, always teleport during enrage)
                    if (!isEnraged && AITimer >= MeteorAttackDuration - 60)
                    {
                        // Final pass, stop at center
                        MoveToPosition(ArenaCenter, 10f);
                        
                        if (Vector2.Distance(NPC.Center, ArenaCenter) < 30f)
                        {
                            AIState = (float)AttackState.Normal;
                            AITimer = 0;
                            AICounter = 0;
                            AIExtra = 0;
                        }
                    }
                    else
                    {
                        // Add delay before teleporting based on difficulty phase
                        int teleportDelay = GetMeteorTeleportDelay();
                        NPC.localAI[1] += 1f;
                        
                        if (NPC.localAI[1] >= teleportDelay && Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // Teleport back to top with new random X position, server only
                            float newX = Main.rand.NextFloat(-ArenaHalfWidth * 0.7f, ArenaHalfWidth * 0.7f);
                            NPC.position = new Vector2(ArenaCenter.X + newX - NPC.width / 2f, arenaTop - NPC.height / 2f);
                            NPC.localAI[0] = Main.rand.NextBool() ? 1f : -1f;
                            NPC.localAI[1] = 0f;
                            NPC.netUpdate = true;
                        }
                        
                        // Play PixelDash when starting new descent (after teleport delay passed)
                        if (NPC.localAI[1] == 0 && NPC.Center.Y <= arenaTop + 50f)
                        {
                            SoundEngine.PlaySound(PixelDash, NPC.Center);
                        }
                    }
                }
            }
            
            if (AITimer >= MeteorAttackDuration)
            {
                // At 10% health, restart meteor attack immediately
                float healthPercent = NPC.life / (float)NPC.lifeMax;
                if (healthPercent <= 0.10f)
                {
                    AITimer = 0;
                    AICounter = 0;
                    AIExtra = 0;
                    NPC.localAI[0] = 0;
                }
                else
                {
                    AIState = (float)AttackState.Normal;
                    AITimer = -60; // 1 second downtime
                    AICounter = 0;
                    AIExtra = 0;
                }
            }
        }
        
        private void DoSpawnGoonsState(Player target)
        {
            isLaughing = false;
            
            int totalToSpawn = GetPixelFriendCount();
            int spawnInterval = SpawnGoonsDuration / (totalToSpawn + 1);
            if (spawnInterval < 30) spawnInterval = 30;
            
            // Move to random empty area, server picks position
            if (NPC.localAI[2] == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Pick random position
                AIExtra = Main.rand.NextFloat(-ArenaHalfWidth * 0.5f, ArenaHalfWidth * 0.5f);
                AICounter = Main.rand.NextFloat(-ArenaHalfHeight * 0.5f, ArenaHalfHeight * 0.5f);
                NPC.localAI[2] = 1;
                NPC.netUpdate = true;
            }
            
            Vector2 targetPos = ArenaCenter + new Vector2(AIExtra, AICounter);
            MoveToPosition(targetPos, 6f);
            
            // Spawn PixelFriends at intervals
            if (Vector2.Distance(NPC.Center, targetPos) < 40f && AITimer % spawnInterval == spawnInterval / 2 && NPC.localAI[3] < totalToSpawn)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int npcType = ModContent.NPCType<PixelFriend>();
                    
                    int n = NPC.NewNPC(
                        NPC.GetSource_FromAI(),
                        (int)NPC.Center.X,
                        (int)NPC.Center.Y + 40,
                        npcType
                    );
                    
                    if (n >= 0 && n < Main.maxNPCs)
                        Main.npc[n].netUpdate = true;
                    
                    NPC.localAI[3] += 1f;
                    
                    // Pick new position, server only
                    AIExtra = Main.rand.NextFloat(-ArenaHalfWidth * 0.5f, ArenaHalfWidth * 0.5f);
                    AICounter = Main.rand.NextFloat(-ArenaHalfHeight * 0.5f, ArenaHalfHeight * 0.5f);
                    NPC.netUpdate = true;
                }
            }
            
            if (AITimer >= SpawnGoonsDuration)
            {
                AIState = (float)AttackState.Normal;
                AITimer = -360; // 6 second downtime
                AICounter = 0;
                AIExtra = 0;
                NPC.localAI[2] = 0;
                NPC.localAI[3] = 0;
            }
        }
        
        private void DoDefeatedState()
        {
            isLaughing = false;
            
            // Move to center and stop once there
            float distToCenter = Vector2.Distance(NPC.Center, ArenaCenter);
            if (distToCenter > 30f)
            {
                MoveToPosition(ArenaCenter, 3f);
            }
            else
            {
                // At center, stop moving
                NPC.velocity = Vector2.Zero;
            }
            
            // Start post-defeat dialogue once
            if (!defeatedDialogueStarted)
            {
                defeatedDialogueStarted = true;
                
                // Mark that ERAM was defeated for all players in the arena
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    if (player.active && !player.dead)
                    {
                        player.GetModPlayer<ERAMArenaPlayer>().defeatedERAM = true;
                    }
                }
                
                // Mark as defeated for Boss Checklist (world-persistent)
                ERAMProgressSystem.ERAMDefeated = true;
                
                // Queue the post-defeat dialogue
                if (DialogueSystem.Instance != null)
                {
                    DialogueSystem.Instance.QueueDialogues(
                        new DialogueEntry("Haha... there it is! That's what I wanted to see!", 4f),
                        new DialogueEntry("Your violence is a glorious sight to behold.", 4f),
                        new DialogueEntry("Here's your reward.", 4f)
                    );
                }
                
                // Don't check for dialogue completion in the same frame
                return;
            }
            
            // Wait for dialogue to complete, then exit subworld
            // Only exit if DialogueSystem exists and dialogue has finished
            if (DialogueSystem.Instance != null)
            {
                if (!DialogueSystem.Instance.IsDialogueActive)
                {
                    // Dialogue finished, now exit subworld
                    if (SubworldSystem.IsActive<ERAMArena>())
                    {
                        SubworldSystem.Exit();
                    }
                    
                    // Despawn this NPC
                    NPC.active = false;
                    NPC.netUpdate = true;
                }
            }
            else
            {
                // No dialogue system, wait a bit then exit
                AITimer++;
                if (AITimer >= 300) // 5 seconds
                {
                    if (SubworldSystem.IsActive<ERAMArena>())
                    {
                        SubworldSystem.Exit();
                    }
                    NPC.active = false;
                    NPC.netUpdate = true;
                }
            }
        }

        public override void FindFrame(int frameHeight)
        {
            // Column 0 = idle, Column 1 = laughing/active attacks
            int column = isLaughing ? 1 : 0;
            
            animTick++;
            if (animTick >= AnimTicksPerFrame)
            {
                animTick = 0;
                animFrame++;
                if (animFrame >= FramesPerColumn)
                    animFrame = 0;
            }
            
            NPC.frame = new Rectangle(column * FrameWidth, animFrame * FrameHeight, FrameWidth, FrameHeight);
        }

        public override Color? GetAlpha(Color drawColor)
        {
            return Color.White;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("DeterministicChaos/Content/NPCs/Bosses/ERAM").Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Rectangle sourceRect = NPC.frame;
            Vector2 origin = sourceRect.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            spriteBatch.Draw(texture, drawPos, sourceRect, Color.White, NPC.rotation, origin, NPC.scale, effects, 0f);
            
            return false;
        }

        public override void OnKill()
        {
            // OnKill shouldn't be called anymore since we prevent death in CheckDead
            // But just in case, ensure rewards are given
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    if (player.active && !player.dead)
                    {
                        player.GetModPlayer<ERAMArenaPlayer>().defeatedERAM = true;
                    }
                }
            }
        }
        
        public override bool CheckDead()
        {
            // Don't actually die, instead transition to Defeated state
            if (SubworldSystem.IsActive<ERAMArena>())
            {
                // If not already in Defeated state, transition to it
                if (AIState != (float)AttackState.Defeated)
                {
                    NPC.life = 1; // Keep alive with 1 HP
                    NPC.dontTakeDamage = true; // Prevent further damage
                    AIState = (float)AttackState.Defeated;
                    AITimer = 0;
                    AICounter = 0;
                    AIExtra = 0;
                    isLaughing = false;
                    NPC.netUpdate = true;
                    
                    // Don't die
                    return false;
                }
                
                return false; // Never actually die in the arena
            }
            
            return true; // Die normally outside arena
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // TODO: Add actual loot drops
            // Example: npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.YourItem>()));
        }

        public override void BossLoot(ref string name, ref int potionType)
        {
            potionType = ItemID.GreaterHealingPotion;
        }
    }
}
