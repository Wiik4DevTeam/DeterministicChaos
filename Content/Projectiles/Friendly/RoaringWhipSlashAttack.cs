using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Miniature friendly version of the Roaring Knight slash attack
    public class RoaringWhipSlashAttack : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/SlashAttack";
        
        private const int Frames = 4;
        private const int TotalLife = 15; // Shorter than boss version
        private const int TicksPerFrame = 4;
        
        // Smaller scale than boss version
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
            
            Projectile.DamageType = DamageClass.SummonMeleeSpeed; // Counts for both whip and minion bonuses
            
            // No immunity so multiple slashes can hit the same target
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // Only hit once per projectile, but different projectiles can hit
            
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
        
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            // Lock spawn position
            Projectile.localAI[0] = Projectile.Center.X;
            Projectile.localAI[1] = Projectile.Center.Y;
            
            // Set rotation from ai[0]
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float passedAngle = Projectile.ai[0];
                Projectile.rotation = passedAngle - MathHelper.PiOver2;
                
                if (Main.rand.NextBool())
                    Projectile.rotation += MathHelper.Pi;
                
                Projectile.netUpdate = true;
            }
        }
        
        public override void AI()
        {
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
            
            // Project onto slash axes
            float alongSlash = Vector2.Dot(toTarget, direction);
            float perpToSlash = Math.Abs(Vector2.Dot(toTarget, perpendicular));
            
            // Check overlap
            float halfWidth = actualWidth * 0.5f;
            float halfHeight = actualHeight * 0.5f;
            
            float targetRadius = Math.Max(targetHalfSize.X, targetHalfSize.Y);
            
            bool withinWidth = Math.Abs(alongSlash) <= (halfWidth + targetRadius);
            bool withinHeight = perpToSlash <= (halfHeight + targetRadius);
            
            return withinWidth && withinHeight;
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<Enemy.SlashAttack>()].Value;
            if (tex == null)
                return false;
            
            int frameWidth = tex.Width;
            int frameHeight = tex.Height / Frames;
            
            Rectangle src = new Rectangle(0, Projectile.frame * frameHeight, frameWidth, frameHeight);
            Vector2 origin = new Vector2(frameWidth * 0.5f, frameHeight * 0.5f);
            
            Vector2 pos = new Vector2(Projectile.localAI[0], Projectile.localAI[1]) - Main.screenPosition;
            
            float lifeT = (TotalLife - Projectile.timeLeft) / (float)TotalLife;
            float curHeightScale = MathHelper.Lerp(HeightScale, 0f, lifeT);
            Vector2 drawScale = new Vector2(WidthScale * Projectile.scale, Math.Max(0.001f, curHeightScale * Projectile.scale));
            
            // Decode flip flags
            int flipFlags = (int)Projectile.ai[1];
            SpriteEffects fx = SpriteEffects.None;
            if ((flipFlags & 1) != 0) fx |= SpriteEffects.FlipHorizontally;
            if ((flipFlags & 2) != 0) fx |= SpriteEffects.FlipVertically;
            
            // White color for player version
            Color drawColor = Color.White * 0.95f;
            
            Main.EntitySpriteDraw(tex, pos, src, drawColor, Projectile.rotation, origin, drawScale, fx, 0);
            return false;
        }
    }
}
