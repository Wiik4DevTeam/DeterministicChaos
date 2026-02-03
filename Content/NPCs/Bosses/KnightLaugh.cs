using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses
{
    public class KnightLaugh : ModProjectile
    {
        private const int FrameW = 16;
        private const int FrameH = 16;
        private const int TotalFrames = 3;
        private const int MaxLifetime = 90;
        
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.scale = 2.4f;
        }

        public override void AI()
        {
            if (Projectile.ai[0] == 0f)
            {
                Projectile.ai[0] = 1f;
                Projectile.velocity = Main.rand.NextVector2Circular(2f, 2f);
            }
            
            Projectile.velocity *= 0.98f;
            
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 8)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= TotalFrames)
                    Projectile.frame = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            
            float opacity = (float)Projectile.timeLeft / MaxLifetime;
            
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * FrameH, FrameW, FrameH);
            
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            
            Main.spriteBatch.Draw(
                texture,
                drawPos,
                sourceRect,
                Color.White * opacity,
                Projectile.rotation,
                new Vector2(FrameW * 0.5f, FrameH * 0.5f),
                Projectile.scale,
                SpriteEffects.None,
                0f
            );
            
            return false;
        }
    }
}
