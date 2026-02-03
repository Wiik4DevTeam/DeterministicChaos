using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class ShockwaveLine : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";
        
        private bool initialized = false;

        public override void SetStaticDefaults()
        {
            // Visual only
        }

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30; // Half a second
            Projectile.alpha = 0;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(initialized);
            writer.WriteVector2(Projectile.velocity);
            writer.Write(Projectile.ai[1]);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            initialized = reader.ReadBoolean();
            Projectile.velocity = reader.ReadVector2();
            Projectile.ai[1] = reader.ReadSingle();
        }

        public override void AI()
        {
            // Initialize once on server only, then sync to clients
            if (!initialized && Main.netMode != NetmodeID.MultiplayerClient)
            {
                initialized = true;
                
                // Apply randomization to velocity
                float speedMult = Main.rand.NextFloat(0.85f, 1.15f);
                Projectile.velocity *= speedMult;
                
                // Apply randomization to length
                if (Projectile.ai[1] <= 0f)
                    Projectile.ai[1] = 20f;
                Projectile.ai[1] *= Main.rand.NextFloat(0.75f, 1.25f);
                
                Projectile.netUpdate = true;
            }
            
            // Fade out over lifetime
            Projectile.alpha = (int)MathHelper.Lerp(0f, 255f, 1f - (Projectile.timeLeft / 30f));
            
            // Slow down over time
            Projectile.velocity *= 0.96f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            // Calculate length based on lifetime (shrinks over time)
            float lifetimeProgress = 1f - (Projectile.timeLeft / 30f);
            float currentLength = Projectile.ai[1] * MathHelper.Lerp(3f, 0.1f, lifetimeProgress);
            
            // Calculate thickness (also shrinks)
            float thickness = MathHelper.Lerp(5.1f, 0.1f, lifetimeProgress);
            
            // Emissive white color
            float alpha = 1f - (Projectile.alpha / 255f);
            Color color = Color.White * alpha;
            
            Vector2 start = Projectile.Center - Main.screenPosition;
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.Zero);
            float lineAngle = direction.ToRotation();
            
            // Draw the line using MagicPixel (1x1 texture)
            spriteBatch.Draw(texture, start, new Rectangle(0, 0, 1, 1), color, lineAngle,
                new Vector2(0f, 0.5f), new Vector2(currentLength, thickness),
                SpriteEffects.None, 0f);
            
            return false;
        }
    }
}
