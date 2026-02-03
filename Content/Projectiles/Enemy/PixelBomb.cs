using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class PixelBomb : ModProjectile
    {
        private const int FrameWidth = 16;
        private const int FrameHeight = 16;
        private const int FrameCount = 4;
        private const int AnimTicksPerFrame = 6;
        private const int WaitTimeBeforeExplosion = 60;
        private const float TravelSpeed = 0.02f;
        
        private int animTick;
        private int animFrame;
        
        // Use Projectile.ai[] for synced state:
        // ai[0] = target X (passed from boss)
        // ai[1] = target Y (passed from boss)
        // Use Projectile.localAI[] for client-side derived values:
        // localAI[0] = start X (captured on spawn)
        // localAI[1] = start Y (captured on spawn)
        
        // Travel progress tracked via time since spawn
        private ref float TravelTimer => ref Projectile.ai[2];
        
        private Vector2 StartPos => new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
        private Vector2 TargetPos
        {
            get
            {
                // Calculate target: center of a 4x4 tile grid cell
                int tileX = (int)(Projectile.ai[0] / 16f);
                int tileY = (int)(Projectile.ai[1] / 16f);
                
                // Align to 4x4 grid center
                tileX = (tileX / 4) * 4 + 2;
                tileY = (tileY / 4) * 4 + 2;
                
                return new Vector2(tileX * 16f, tileY * 16f);
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.scale = 3f;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            // Sync localAI (start position) and ai[2] (travel timer)
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
        }

        public override void AI()
        {
            // Animation
            animTick++;
            if (animTick >= AnimTicksPerFrame)
            {
                animTick = 0;
                animFrame++;
                if (animFrame >= FrameCount)
                    animFrame = 0;
            }
            
            // Initialize start position on first tick (all clients do this from spawn position)
            if (Projectile.localAI[0] == 0 && Projectile.localAI[1] == 0)
            {
                Projectile.localAI[0] = Projectile.Center.X;
                Projectile.localAI[1] = Projectile.Center.Y;
            }
            
            // Calculate travel progress from timer
            float travelProgress = TravelTimer * TravelSpeed;
            
            if (travelProgress < 1f)
            {
                TravelTimer++;
                travelProgress = TravelTimer * TravelSpeed;
                
                if (travelProgress >= 1f)
                {
                    travelProgress = 1f;
                }
                
                // Interpolate position with arc
                Vector2 linearPos = Vector2.Lerp(StartPos, TargetPos, travelProgress);
                
                // Add arc height based on sine curve
                float arcHeight = (float)Math.Sin(travelProgress * MathHelper.Pi) * 100f;
                linearPos.Y -= arcHeight;
                
                Projectile.Center = linearPos;
                Projectile.velocity = Vector2.Zero;
                
                // Rotate while moving
                Projectile.rotation += 0.1f;
            }
            else
            {
                // At target, wait then explode
                TravelTimer++;
                
                // Snap to target position
                Projectile.Center = TargetPos;
                Projectile.velocity = Vector2.Zero;
                
                // Pulse effect while waiting
                Projectile.rotation += 0.05f;
                
                // Calculate wait time (travel takes 1/TravelSpeed ticks = 50 ticks)
                float waitTime = TravelTimer - (1f / TravelSpeed);
                
                if (waitTime >= WaitTimeBeforeExplosion)
                {
                    Explode();
                }
            }
        }

        private void Explode()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 explosionCenter = Projectile.Center;
                
                int waveType = ModContent.ProjectileType<PixelWave>();
                int damage = 7;
                
                // Spawn 4 waves in cardinal directions
                // Up
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), explosionCenter, new Vector2(0, -8f), waveType, damage, 0f, Main.myPlayer, 0f);
                // Down
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), explosionCenter, new Vector2(0, 8f), waveType, damage, 0f, Main.myPlayer, 1f);
                // Left
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), explosionCenter, new Vector2(-8f, 0), waveType, damage, 0f, Main.myPlayer, 2f);
                // Right
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), explosionCenter, new Vector2(8f, 0), waveType, damage, 0f, Main.myPlayer, 3f);
            }
            
            // Visual effect
            for (int i = 0; i < 20; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Electric);
                dust.velocity = Main.rand.NextVector2Circular(5f, 5f);
                dust.noGravity = true;
            }
            
            // Play custom PixelBomb explosion sound
            SoundStyle pixelBombSound = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelBomb");
            SoundEngine.PlaySound(pixelBombSound, Projectile.Center);
            
            Projectile.Kill();
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.White;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = new Rectangle(0, animFrame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
