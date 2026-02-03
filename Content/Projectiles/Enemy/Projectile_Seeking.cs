using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class Projectile_Seeking : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;

            Projectile.scale = 1.0f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            float seekRange = 520f;
            int warmup = 18;
            int seekTime = 60;

            float maxSpeed = 9.5f;
            float turnAuthority = 40f;

            Projectile.ai[0]++;

            Player target = null;

            if (Projectile.ai[0] >= warmup && Projectile.ai[0] <= warmup + seekTime)
                target = FindNearestAlivePlayer(Projectile.Center, seekRange);

            if (target != null)
            {
                Vector2 toT = target.Center - Projectile.Center;
                Vector2 desired = toT.SafeNormalize(Vector2.UnitY) * maxSpeed;

                float sway = (float)System.Math.Sin((Projectile.ai[0] + Projectile.whoAmI * 7) * 0.12f) * 0.9f;
                desired = desired.RotatedBy(sway * 0.04f);

                Projectile.velocity = (Projectile.velocity * (turnAuthority - 1f) + desired) / turnAuthority;
            }

            float timeRemaining = Projectile.timeLeft;
            if (timeRemaining < 20f)
            {
                Projectile.scale = MathHelper.Lerp(0f, 1.0f, timeRemaining / 20f);
            }
        }

        private static Player FindNearestAlivePlayer(Vector2 from, float maxDist)
        {
            Player best = null;
            float bestD = maxDist * maxDist;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                float d = Vector2.DistanceSquared(from, p.Center);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }

            return best;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                float a = (1f - t) * 0.45f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.spriteBatch.Draw(tex, pos, null, Color.White * a, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
