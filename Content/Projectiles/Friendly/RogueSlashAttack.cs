using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Friendly rogue slash attack spawned by RogueSeekingKnife
    public class RogueSlashAttack : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/SlashAttack";
        
        private const int Frames = 4;
        private const int TotalLife = 15;
        private const int TicksPerFrame = 4;
        
        // Slash dimensions
        private const float WidthScale = 800f / 300f; // About 800px wide
        private const float HeightScale = 1.5f;
        
        private bool hasPlayedSound = false;
        
        // ai[0] = rotation
        // ai[1] = flip flags
        
        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = Frames;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }
        
        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
            
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            
            // Rogue/throwing damage type
            Projectile.DamageType = DamageClass.Throwing;
            
            // Can hit same target from multiple slashes
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            
            Projectile.hide = false;
        }
        
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.rotation);
        }
        
        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
            Projectile.rotation = reader.ReadSingle();
        }
        
        private bool initialized = false;
        
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // Lock spawn position
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;
        }
        
        public override void AI()
        {
            // Initialize rotation from synced ai values
            if (!initialized)
            {
                initialized = true;
                float passedAngle = Projectile.ai[0];
                Projectile.rotation = passedAngle - MathHelper.PiOver2;
                
                // Use ai[1] bit 1 for horizontal flip as rotation offset
                if (((int)Projectile.ai[1] & 2) != 0)
                    Projectile.rotation += MathHelper.Pi;
                
                // Ensure position is locked
                if (Projectile.localAI[0] == 0 && Projectile.localAI[1] == 0)
                {
                    Projectile.localAI[0] = Projectile.Center.X;
                    Projectile.localAI[1] = Projectile.Center.Y;
                }
            }
            
            // Play sound on first tick
            if (!hasPlayedSound && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch")
                {
                    Volume = 0.4f,
                    Pitch = 0.3f
                }, Projectile.Center);
                hasPlayedSound = true;
            }
            
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            
            // Animate frames
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Frames)
                    Projectile.frame = Frames - 1;
            }
            
            // Emissive lighting
            Lighting.AddLight(Projectile.Center, 0.8f, 0.8f, 0.8f);
            
            if (Projectile.timeLeft <= 2)
                Projectile.Kill();
        }
        
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Calculate current height scale based on lifetime
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            
            if (curHeightScale < 0.1f)
                return false;
            
            // Slash dimensions
            float actualWidth = 300f * WidthScale;
            float actualHeight = 10f * curHeightScale;
            
            Vector2 center = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
            
            // Get direction from rotation
            Vector2 direction = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            
            // Target center and half-extents
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 targetHalfSize = new Vector2(targetHitbox.Width * 0.5f, targetHitbox.Height * 0.5f);
            
            // Vector from slash center to target
            Vector2 toTarget = targetCenter - center;
            
            // Project onto slash local axes
            float projDir = Math.Abs(Vector2.Dot(toTarget, direction));
            float projPerp = Math.Abs(Vector2.Dot(toTarget, perpendicular));
            
            // Also project target half-extents onto axes for broadening
            float targetWidthOnDir = Math.Abs(targetHalfSize.X * direction.X) + Math.Abs(targetHalfSize.Y * direction.Y);
            float targetWidthOnPerp = Math.Abs(targetHalfSize.X * perpendicular.X) + Math.Abs(targetHalfSize.Y * perpendicular.Y);
            
            // Check if target is inside the slash box
            float slashHalfWidth = actualWidth * 0.5f;
            float slashHalfHeight = actualHeight * 0.5f;
            
            bool withinWidth = projDir <= (slashHalfWidth + targetWidthOnDir);
            bool withinHeight = projPerp <= (slashHalfHeight + targetWidthOnPerp);
            
            return withinWidth && withinHeight;
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;
            
            int frameCount = Main.projFrames[Type];
            int frameHeight = tex.Height / frameCount;
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, tex.Width, frameHeight);
            
            Vector2 origin = new Vector2(tex.Width * 0.5f, frameHeight * 0.5f);
            Vector2 drawPos = new Vector2(Projectile.localAI[0], Projectile.localAI[1]) - Main.screenPosition;
            
            // Calculate scales
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            
            Vector2 scale = new Vector2(WidthScale, curHeightScale);
            
            // Draw with white glow
            Color drawColor = Color.White * 0.9f;
            
            Main.EntitySpriteDraw(
                tex,
                drawPos,
                sourceRect,
                drawColor,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0
            );
            
            return false;
        }
    }
}
