using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Friendly seeking projectile spawned by DarkShardProjectile on enemy hit
    public class DarkShardSeeker : ModProjectile
    {
        private const int ActivationDelay = 30;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.hostile = false;
            Projectile.friendly = false;

            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;

            Projectile.scale = 1.0f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Projectile.ai[0]++;

            // Emit purple light
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.6f);

            // After activation delay, become friendly and start homing
            if (Projectile.ai[0] >= ActivationDelay)
            {
                Projectile.friendly = true;

                float seekRange = 520f;
                float maxSpeed = 12f;
                float turnAuthority = 25f;

                NPC target = FindNearestEnemy(Projectile.Center, seekRange);

                if (target != null)
                {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Vector2 desired = toTarget.SafeNormalize(Vector2.UnitY) * maxSpeed;

                    float sway = (float)System.Math.Sin((Projectile.ai[0] + Projectile.whoAmI * 7) * 0.12f) * 0.9f;
                    desired = desired.RotatedBy(sway * 0.04f);

                    Projectile.velocity = (Projectile.velocity * (turnAuthority - 1f) + desired) / turnAuthority;
                }
            }
            else
            {
                // Before activation, drift slowly and spawn particles
                Projectile.velocity *= 0.97f;

                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.8f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }
            }

            // Fade out near end of life
            float timeRemaining = Projectile.timeLeft;
            if (timeRemaining < 20f)
            {
                Projectile.scale = MathHelper.Lerp(0f, 1.0f, timeRemaining / 20f);
            }
        }

        private static NPC FindNearestEnemy(Vector2 from, float maxDist)
        {
            NPC best = null;
            float bestD = maxDist * maxDist;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;

                float d = Vector2.DistanceSquared(from, npc.Center);
                if (d < bestD)
                {
                    bestD = d;
                    best = npc;
                }
            }

            return best;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Draw trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color trailColor = Color.Purple * (1f - t) * 0.6f * Projectile.scale;
                float trailScale = Projectile.scale * (1f - t * 0.5f);

                Main.EntitySpriteDraw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // Draw main projectile
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Color mainColor = Projectile.ai[0] >= ActivationDelay ? Color.White : Color.White * 0.7f;
            Main.EntitySpriteDraw(tex, mainPos, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Spawn impact dust
            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.0f);
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                dust.noGravity = true;
            }
        }
    }
}
