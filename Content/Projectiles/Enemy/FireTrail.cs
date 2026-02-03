using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class FireTrail : ModProjectile
    {
        private const int TotalLifetime = 10;
        private const int FrameWidth = 32;
        private const int FrameHeight = 32;
        private const int FrameCount = 2;
        private const int AnimTicksPerFrame = 6;
        
        private int animTick;
        private int animFrame;

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.light = 0.4f;
            Projectile.scale = 1.5f;
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
            
            // Stationary fire trail, doesn't move
            Projectile.velocity = Vector2.Zero;
            
            // Fade out over time
            float progress = 1f - (Projectile.timeLeft / (float)TotalLifetime);
            Projectile.alpha = (int)(progress * 255);
            
            // Flicker
            if (Main.rand.NextBool(4))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                dust.velocity = new Vector2(0, -2f);
                dust.noGravity = true;
                dust.scale = 1f - progress;
            }
            
            Lighting.AddLight(Projectile.Center, (1f - progress) * 1f, (1f - progress) * 0.4f, 0f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Don't kill on hit, lingering hazard
        }

        public override Color? GetAlpha(Color lightColor)
        {
            float progress = 1f - (Projectile.timeLeft / (float)TotalLifetime);
            return Color.White * (1f - progress);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = new Rectangle(0, animFrame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            
            float progress = 1f - (Projectile.timeLeft / (float)TotalLifetime);
            Color drawColor = Color.White * (1f - progress);
            
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, drawColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
