using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.VFX;
using DeterministicChaos.Content.Projectiles;
using DeterministicChaos.Content.Projectiles.Enemy;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class RoaringKnightCutscene : ModNPC
    {
        // TIMING CONFIGURATION (all values in ticks, 60 ticks = 1 second)
        private const int InitialDelay = 120;                    // 2 seconds invisible before appearing
        private const int SpawnOffsetY = -200;                   // Spawn 200px above player
        
        // Phase 0: Column 0 Animation
        private const int Phase0_AnimationFrames = 15;           // Frames 0-14
        private const int Phase0_FrameSpeed = 6;                 // Ticks per frame
        private const int Phase0_LingerTime = 120;               // 2 seconds on last frame
        
        // Phase 1: Column 1 Shake
        private const int Phase1_Duration = 240;                 // 4 seconds total
        private const int Phase1_LaughInterval = 30;             // Spawn laugh effect every 0.5 seconds
        private const float Phase1_ShakeAmount = 3f;             // Screen shake intensity
        private const float Phase1_LaughOffsetX = 40f;           // X offset for laugh projectiles (positive = right)
        private const float Phase1_LaughOffsetY = -65f;          // Y offset for laugh projectiles (negative = up)
        
        // Phase 2: Column 2 Animation Loop
        private const int Phase2_InitialPause = 120;             // 2 seconds on frame 0 before roar
        private const int Phase2_LoopDuration = 300;             // 6 seconds looping frames 1-2
        private const int Phase2_LoopFrameSpeed = 8;             // Ticks per frame
        
        // Phase 3: Column 3 Final Animation
        private const int Phase3_AnimationFrames = 6;            // Frames 0-5
        private const int Phase3_FrameSpeed = 6;                 // Ticks per frame
        
        // Shockwave VFX
        private const int ShockwaveInterval = 12;                // 0.5 seconds
        
        // Texture settings
        private const int FrameW = 100;
        private const int FrameH = 100;
        
        // Afterimage trail
        private const int TrailLength = 12;
        private Vector2[] trailPos;
        private float[] trailRot;
        private int[] trailSpriteDir;
        private bool trailInit;
        
        // State tracking
        private int cutscenePhase = 0;
        private int cutsceneTimer = 0;
        private int fakeEyeIndex = -1;
        private bool hasAppeared = false;
        private bool hasSpawnedBoss = false;
        
        private int animColumn = 0;
        private int animRow = 0;
        private int animTick = 0;
        
        private const int Phase_Column0 = 0;
        private const int Phase_Column1 = 1;
        private const int Phase_Column2 = 2;
        private const int Phase_Column3 = 3;
        
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(cutscenePhase);
            writer.Write(cutsceneTimer);
            writer.Write(hasAppeared);
            writer.Write(hasSpawnedBoss);
        }
        
        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            cutscenePhase = reader.ReadInt32();
            cutsceneTimer = reader.ReadInt32();
            hasAppeared = reader.ReadBoolean();
            hasSpawnedBoss = reader.ReadBoolean();
        }
        
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults()
        {
            NPC.width = 100;
            NPC.height = 100;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 1;
            NPC.HitSound = null;
            NPC.DeathSound = null;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.scale = 2.4f;
            NPC.ShowNameOnHover = false;
            Music = MusicLoader.GetMusicSlot(Mod, "Assets/Music/Breath");
        }

        public override void AI()
        {
            // Initialize trail arrays
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
            
            // Cache trail position every frame
            CacheTrail();
            
            cutsceneTimer++;
            
            // Initial delay: stay invisible and position once
            if (!hasAppeared)
            {
                if (cutsceneTimer >= InitialDelay)
                {
                    Player nearest = Main.player[Player.FindClosest(NPC.Center, NPC.width, NPC.height)];
                    if (nearest != null && nearest.active)
                    {
                        NPC.Center = new Vector2(nearest.Center.X, nearest.Center.Y + SpawnOffsetY);
                    }
                    hasAppeared = true;
                    cutsceneTimer = 0;
                }
                return;
            }
            
            animTick++;
            
            fakeEyeIndex = (int)NPC.ai[0];
            NPC fakeEye = null;
            
            if (fakeEyeIndex >= 0 && fakeEyeIndex < Main.maxNPCs && Main.npc[fakeEyeIndex].active)
            {
                fakeEye = Main.npc[fakeEyeIndex];
            }

            switch (cutscenePhase)
            {
                case Phase_Column0:
                    animColumn = 0;
                    
                    // Animate through frames 0-14
                    if (animRow < Phase0_AnimationFrames - 1)
                    {
                        if (animTick >= Phase0_FrameSpeed)
                        {
                            animTick = 0;
                            animRow++;
                        }
                    }
                    // Hold on last frame
                    else if (animRow == Phase0_AnimationFrames - 1)
                    {
                        int totalPhaseTime = (Phase0_AnimationFrames * Phase0_FrameSpeed) + Phase0_LingerTime;
                        
                        if (cutsceneTimer >= totalPhaseTime)
                        {
                            // Spawn blood particles at fake eye's last position if it existed
                            if (fakeEye != null)
                            {
                                for (int i = 0; i < 50; i++)
                                {
                                    Vector2 velocity = Main.rand.NextVector2Circular(12f, 12f);
                                    Dust.NewDust(fakeEye.position, fakeEye.width, fakeEye.height, 
                                        DustID.Blood, velocity.X, velocity.Y, 0, default, 2f);
                                }
                            }
                            
                            // Transition to Phase 1
                            cutscenePhase = Phase_Column1;
                            cutsceneTimer = 0;
                            animRow = 0;
                            animTick = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case Phase_Column1:
                    animColumn = 1;
                    animRow = 0;
                    
                    // Play laugh sound at start
                    if (cutsceneTimer == 1)
                    {
                        if (Main.netMode != NetmodeID.Server)
                        {
                            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Laugh")
                            {
                                Volume = 1.0f
                            }, NPC.Center);
                        }
                    }
                    
                    // Spawn laugh projectiles periodically
                    if (cutsceneTimer % Phase1_LaughInterval == 0)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center.X + Phase1_LaughOffsetX,
                                NPC.Center.Y + Phase1_LaughOffsetY,
                                0f,
                                0f,
                                ModContent.ProjectileType<KnightLaugh>(),
                                0,
                                0f
                            );
                        }
                    }
                    
                    // Transition to Phase 2
                    if (cutsceneTimer >= Phase1_Duration)
                    {
                        cutscenePhase = Phase_Column2;
                        cutsceneTimer = 0;
                        animRow = 0;
                        animTick = 0;
                        NPC.netUpdate = true;
                        
                        if (Main.netMode != NetmodeID.Server)
                        {
                            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightEnergyCharge")
                            {
                                Volume = 1.0f
                            }, NPC.Center);
                        }
                    }
                    break;

                case Phase_Column2:
                    animColumn = 2;
                    
                    RoaringKnightBackgroundSystem.ShowBackground = true;
                    
                    if (cutsceneTimer < Phase2_InitialPause)
                    {
                        animRow = 0;
                    }
                    else
                    {
                        if (cutsceneTimer == Phase2_InitialPause)
                        {
                            if (Main.netMode != NetmodeID.Server)
                            {
                                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnightRoar")
                                {
                                    Volume = 1.0f
                                }, NPC.Center);
                            }
                        }
                        
                        if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0)
                        {
                            Main.instance.CameraModifiers.Add(new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                                NPC.Center,
                                new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f)).SafeNormalize(Vector2.UnitX),
                                6f,
                                6f,
                                10,
                                3000f
                            ));
                        }
                        
                        // Spawn shockwave VFX every 0.5 seconds during roar
                        int roarTimer = cutsceneTimer - Phase2_InitialPause;
                        if (roarTimer % ShockwaveInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
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
                        
                        // Spawn shockwaves periodically during roar
                        if (cutsceneTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int shockwaveType = ModContent.ProjectileType<ShockwaveLine>();
                            for (int j = 0; j < 2; j++)
                            {
                                float shockAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                                Vector2 shockDir = new Vector2(1f, 0f).RotatedBy(shockAngle);
                                float shockSpeed = Main.rand.NextFloat(20f, 30f);
                                
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center,
                                    shockDir * shockSpeed,
                                    shockwaveType,
                                    0,
                                    0f,
                                    Main.myPlayer
                                );
                            }
                        }
                        
                        // Animate frames 1-2 in loop
                        if (animTick >= Phase2_LoopFrameSpeed)
                        {
                            animTick = 0;
                            animRow++;
                            if (animRow > 2)
                                animRow = 1;
                        }
                        
                        if (cutsceneTimer >= Phase2_InitialPause + Phase2_LoopDuration)
                        {
                            cutscenePhase = Phase_Column3;
                            cutsceneTimer = 0;
                            animRow = 0;
                            animTick = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case Phase_Column3:
                    animColumn = 3;
                    
                    // Animate through frames 0-5
                    if (animRow < Phase3_AnimationFrames - 1)
                    {
                        if (animTick >= Phase3_FrameSpeed)
                        {
                            animTick = 0;
                            animRow++;
                        }
                    }
                    // Spawn real boss on last frame
                    else if (animRow == Phase3_AnimationFrames - 1)
                    {
                        if (!hasSpawnedBoss)
                        {
                            hasSpawnedBoss = true;
                            
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                NPC.NewNPC(
                                    NPC.GetSource_FromAI(),
                                    (int)NPC.Center.X,
                                    (int)NPC.Center.Y,
                                    ModContent.NPCType<RoaringKnight>()
                                );
                            }
                            
                            // Background will continue showing from the boss itself
                            NPC.life = 0;
                            NPC.active = false;
                        }
                    }
                    break;
            }
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
        
        public override void FindFrame(int frameHeight)
        {
            NPC.frame = new Rectangle(animColumn * FrameW, animRow * FrameH, FrameW, FrameH);
        }

        public override bool CheckActive()
        {
            return false;
        }

        public override bool CheckDead()
        {
            return true;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Stay invisible until appearing
            if (!hasAppeared)
                return false;
            
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = new Vector2(FrameW * 0.5f, FrameH * 0.5f);
            
            // Draw afterimages
            for (int i = TrailLength - 1; i >= 1; i--)
            {
                float t = i / (float)TrailLength;
                float alpha = (1f - t) * 0.45f;

                Vector2 pos = trailPos[i] - screenPos;
                
                float drift = i * 0.45f;
                pos.X += trailSpriteDir[i] * drift;

                SpriteEffects fx = (trailSpriteDir[i] == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(texture, pos, NPC.frame, Color.White * alpha, trailRot[i], origin, NPC.scale, fx, 0f);
            }
            
            // Calculate main draw position
            Vector2 drawPos = NPC.Center - screenPos;
            
            // Shake effect during Phase 1
            if (cutscenePhase == Phase_Column1)
            {
                drawPos.X += Main.rand.NextFloat(-Phase1_ShakeAmount, Phase1_ShakeAmount);
                drawPos.Y += Main.rand.NextFloat(-Phase1_ShakeAmount, Phase1_ShakeAmount);
            }
            
            SpriteEffects mainFx = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            
            // Draw main sprite with fullbright (Color.White ignores lighting)
            spriteBatch.Draw(
                texture,
                drawPos,
                NPC.frame,
                Color.White,
                NPC.rotation,
                origin,
                NPC.scale,
                mainFx,
                0f
            );
            
            return false;
        }
    }
}
