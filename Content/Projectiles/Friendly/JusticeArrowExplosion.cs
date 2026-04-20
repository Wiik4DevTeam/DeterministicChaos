using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class JusticeArrowExplosion : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/RoaringArrowProjectile";

        public override void SetDefaults()
        {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            float progress = (10 - Projectile.timeLeft) / 10f;
            Projectile.scale = 1f + progress * 1f;

            int dustCount = progress < 0.5f ? 8 : 4;
            for (int i = 0; i < dustCount; i++)
            {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(60f * Projectile.scale, 60f * Projectile.scale);
                Dust dust = Dust.NewDustPerfect(pos, DustID.GoldFlame, (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 4f), 0, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
            }

            // Ring of outward-flying sparks
            if (Projectile.timeLeft >= 7)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) * (0.8f + progress);
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, vel, 0, default, 1.8f);
                    spark.noGravity = true;
                    spark.fadeIn = 1.2f;
                }
            }

            Lighting.AddLight(Projectile.Center, 1f, 0.9f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float progress = (10 - Projectile.timeLeft) / 10f;
            float alpha = 0.5f * (1f - progress);
            float radius = 60f * Projectile.scale;
            Vector2 center = Projectile.Center - Main.screenPosition;

            // Draw layered rotated squares to approximate a circle/starburst
            int layers = 6;
            for (int i = 0; i < layers; i++)
            {
                float rotation = MathHelper.TwoPi / layers * i + progress * MathHelper.PiOver4;
                float layerScale = radius * (1f - i * 0.08f);
                float layerAlpha = alpha * (1f - i * 0.1f);

                Vector2 origin = new Vector2(pixel.Width / 2f, pixel.Height / 2f);
                Vector2 scale = new Vector2(layerScale * 2f / pixel.Width, layerScale * 2f / pixel.Height);

                // Outer glow layer (golden)
                Main.spriteBatch.Draw(pixel, center, null, new Color(255, 220, 50) * layerAlpha * 0.5f, rotation, origin, scale * 1.15f, SpriteEffects.None, 0f);
                // Inner bright layer (yellow-white)
                Main.spriteBatch.Draw(pixel, center, null, new Color(255, 255, 150) * layerAlpha, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            // Core bright flash
            float coreAlpha = 0.6f * (1f - progress * progress);
            float coreSize = radius * 0.5f;
            Vector2 coreScale = new Vector2(coreSize * 2f / pixel.Width, coreSize * 2f / pixel.Height);
            Vector2 coreOrigin = new Vector2(pixel.Width / 2f, pixel.Height / 2f);
            Main.spriteBatch.Draw(pixel, center, null, Color.White * coreAlpha, 0f, coreOrigin, coreScale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 18; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, vel, 0, default, Main.rand.NextFloat(1.2f, 1.8f));
                dust.noGravity = true;
            }
        }
    }
}
