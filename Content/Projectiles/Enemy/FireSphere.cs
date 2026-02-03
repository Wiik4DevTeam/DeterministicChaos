using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class FireSphere : ModProjectile
    {
        private const int TotalLifetime = 300;
        private const int FrameWidth = 32;
        private const int FrameHeight = 32;
        private const int FrameCount = 2;
        private const int AnimTicksPerFrame = 6;
        
        private int animTick;
        private int animFrame;

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.light = 0.5f;
            Projectile.scale = 1.1f;
            Projectile.damage = 30;
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
            
            // Move linearly in initial direction (no tracking)
            // Velocity is set on spawn, just maintain it
            
            // Rotate based on velocity
            Projectile.rotation = Projectile.velocity.ToRotation();
            
            // Dust trail
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                dust.noGravity = true;
                dust.scale = 1.2f;
            }
            
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.2f);
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
            
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}
