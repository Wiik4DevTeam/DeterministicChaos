using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class SpeedLine : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        private const int LIFETIME = 20;

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
            Projectile.timeLeft = LIFETIME;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // ai[0] = line length
            // ai[1] = line thickness

            // Fade out over lifetime
            float lifetimeProgress = 1f - (Projectile.timeLeft / (float)LIFETIME);
            Projectile.alpha = (int)(lifetimeProgress * 255f);

            // Slow down
            Projectile.velocity *= 0.94f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            SpriteBatch spriteBatch = Main.spriteBatch;

            float lifetimeProgress = 1f - (Projectile.timeLeft / (float)LIFETIME);

            // Length shrinks over time
            float baseLength = Projectile.ai[0];
            if (baseLength <= 0f) baseLength = 30f;
            float currentLength = baseLength * MathHelper.Lerp(1f, 0.05f, lifetimeProgress);

            // Thickness shrinks over time
            float baseThickness = Projectile.ai[1];
            if (baseThickness <= 0f) baseThickness = 3f;
            float thickness = baseThickness * MathHelper.Lerp(1f, 0.1f, lifetimeProgress);

            float alpha = 1f - (Projectile.alpha / 255f);

            // Color based on localAI[1]: 0 = blue-cyan (integrity), 1 = orange (bravery)
            Color coreColor, glowColor;
            if (Projectile.localAI[1] == 1f)
            {
                // Orange-flame color
                coreColor = new Color(255, 200, 80) * alpha;
                glowColor = new Color(255, 120, 20) * alpha * 0.5f;
            }
            else
            {
                // Blue-cyan color with bright core
                coreColor = new Color(140, 210, 255) * alpha;
                glowColor = new Color(60, 130, 220) * alpha * 0.5f;
            }

            Vector2 start = Projectile.Center - Main.screenPosition;
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.Zero);
            if (direction == Vector2.Zero)
                direction = Projectile.rotation.ToRotationVector2();
            float lineAngle = direction.ToRotation();

            // Draw glow layer (wider, dimmer)
            spriteBatch.Draw(texture, start, new Rectangle(0, 0, 1, 1), glowColor, lineAngle,
                new Vector2(0f, 0.5f), new Vector2(currentLength, thickness * 2.5f),
                SpriteEffects.None, 0f);

            // Draw core line (thin, bright)
            spriteBatch.Draw(texture, start, new Rectangle(0, 0, 1, 1), coreColor, lineAngle,
                new Vector2(0f, 0.5f), new Vector2(currentLength, thickness),
                SpriteEffects.None, 0f);

            return false;
        }
    }
}
