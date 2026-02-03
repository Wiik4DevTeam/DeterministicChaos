using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.VFX
{
    public class Shockwave : ModProjectile
    {
        private const int FrameCount = 8;
        private const int FrameWidth = 314;
        private const int FrameHeight = 227;
        private const int TicksPerFrame = 2;
        private const int TotalLife = FrameCount * TicksPerFrame;
        
        private float initialScale;
        private bool initialized = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = FrameCount;
        }

        public override void SetDefaults()
        {
            Projectile.width = FrameWidth;
            Projectile.height = FrameHeight;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.scale = 2f;
            Projectile.alpha = 0;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(initialScale);
            writer.Write(initialized);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            initialScale = reader.ReadSingle();
            initialized = reader.ReadBoolean();
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                initialScale = Projectile.scale;
                Projectile.netUpdate = true;
            }
            
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.ai[0]; // Use ai[0] for rotation
            
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                
                if (Projectile.frame >= FrameCount)
                {
                    Projectile.Kill();
                    return;
                }
            }
            
            float progress = 1f - (Projectile.timeLeft / (float)TotalLife);
            Projectile.scale = initialScale * (1f + progress);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            if (texture == null)
                return false;

            Rectangle sourceRect = new Rectangle(0, Projectile.frame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth * 0.5f, FrameHeight * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Use additive blending for proper translucency
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
