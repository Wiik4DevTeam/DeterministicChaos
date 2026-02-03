using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringTomeStarProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.aiStyle = -1;
            Projectile.light = 0.4f;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.scale = 0.6f;
        }

        public override void AI()
        {
            Projectile.rotation = 0f;

            // Pulsing scale effect
            Projectile.ai[0]++;
            float pulse = 0.6f + 0.1f * (float)System.Math.Sin(Projectile.ai[0] * 0.15f);
            Projectile.scale = pulse;

            float pullRadius = 1200f;
            float absorbRadius = 50f;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == Projectile.owner && other.type == ModContent.ProjectileType<RoaringTomeBigProjectile>())
                {
                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    // Get absorbed if close enough
                    if (distance < absorbRadius)
                    {
                        // Notify the big projectile to absorb this star
                        if (other.ModProjectile is RoaringTomeBigProjectile bigProj)
                        {
                            bigProj.AbsorbStar(Projectile.damage);
                        }

                        Projectile.Kill();
                        return;
                    }
                    // Get pulled toward big projectile
                    else if (distance < pullRadius)
                    {
                        Vector2 direction = (other.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        float pullStrength = (1f - distance / pullRadius) * 1.5f;
                        Projectile.velocity += direction * pullStrength;

                        // Cap velocity
                        if (Projectile.velocity.Length() > 20f)
                        {
                            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                        }
                    }
                }
            }

            // Slow down slightly over time
            Projectile.velocity *= 0.995f;

            // Light
            Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Brief invincibility frames
            Projectile.damage = (int)(Projectile.damage * 0.85f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Draw clean afterimages, use Projectile.Center for correct positioning
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                // oldPos stores top-left, so add half the hitbox size to get center
                Vector2 drawPos = Projectile.oldPos[i] + new Vector2(Projectile.width / 2f, Projectile.height / 2f) - Main.screenPosition;
                float progress = i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.White * (1f - progress) * 0.5f;
                float trailScale = Projectile.scale * (1f - progress * 0.3f);
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, 0f, origin, trailScale, SpriteEffects.None, 0);
            }

            // Draw main sprite centered on hitbox
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, Color.White, 0f, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void PostDraw(Color lightColor)
        {
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.White;
        }
    }
}
