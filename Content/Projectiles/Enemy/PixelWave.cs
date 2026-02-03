using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class PixelWave : ModProjectile
    {
        private const int TotalLifetime = 180;
        private const int FrameWidth = 32;
        private const int FrameHeight = 32;
        private const int FrameCount = 2;
        private const int AnimTicksPerFrame = 6;
        
        private int animTick;
        private int animFrame;

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.light = 0.3f;
            Projectile.scale = 1.9f;
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
            
            // ai[0] = direction (0=up, 1=down, 2=left, 3=right)
            
            // Rotate based on direction
            Projectile.rotation = Projectile.velocity.ToRotation();
            
            // Trail dust
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Electric);
                dust.velocity *= 0.3f;
                dust.noGravity = true;
                dust.scale = 0.8f;
            }
            
            Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 1f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
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
            
            // Sprite faces left by default, so add Pi to rotation to correct direction
            float rotation = Projectile.rotation + MathHelper.Pi;
            
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, Color.White, rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
