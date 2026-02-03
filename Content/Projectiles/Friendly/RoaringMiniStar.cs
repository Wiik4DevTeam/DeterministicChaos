using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringMiniStar : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/Projectile_Star";
        
        private float initialScale = 0.5f;
        private float shrinkRate = 0.008f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;

            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;

            Projectile.scale = initialScale;
            
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            // Shrink over time
            Projectile.scale -= shrinkRate;
            
            // Kill when too small
            if (Projectile.scale <= 0.1f)
            {
                Projectile.Kill();
                return;
            }
            
            // Slight deceleration
            Projectile.velocity *= 0.98f;
            
            // Add some light
            Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Golden/orange color tint
            Color tint = new Color(255, 200, 100);

            // Draw trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float a = (1f - t) * 0.4f;
                float trailScale = Projectile.scale * (1f - t * 0.5f);

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.spriteBatch.Draw(
                    tex,
                    pos,
                    null,
                    tint * a,
                    Projectile.rotation,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // Draw main star
            Main.spriteBatch.Draw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                tint,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
