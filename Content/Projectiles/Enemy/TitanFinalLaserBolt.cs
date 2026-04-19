using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Fast laser bolt fired from the Titan's face during the final stand sequence.
    // Travels in a random direction within a 90-degree downward cone.
    // Drawn as an elongated glowing cyan streak using the MagicPixel texture.
    // Reuses TitanBallAttack texture for the atlas reference; actual draw is fully custom.
    public class TitanFinalLaserBolt : ModProjectile
    {
        private const int LIFETIME = 300;          // 5 seconds max
        private const float TRAIL_LENGTH = 64f;    // Pixel length of visual trail
        private const float BOLT_RADIUS = 7f;       // Head glow radius
        private const int TRAIL_SEGMENTS = 14;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/TitanBallAttack";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LIFETIME;
            Projectile.alpha = 255; // Custom draw only
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Dust trail
            if (Main.rand.NextBool(2) && Main.netMode != NetmodeID.Server)
            {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f,
                    1, 1, DustID.WhiteTorch, Scale: 0.6f);
                d.noGravity = true;
                d.velocity = Vector2.Zero;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            float speed = Projectile.velocity.Length();
            if (speed < 0.01f)
                return false;

            Vector2 dir = Vector2.Normalize(Projectile.velocity);
            Vector2 headScreen = Projectile.Center - Main.screenPosition;
            Vector2 tailScreen = headScreen - dir * TRAIL_LENGTH;

            // Lifetime-based alpha fade (blip out as it dies)
            float lifeAlpha = Math.Min(1f, Projectile.timeLeft / 20f);

            for (int i = 0; i < TRAIL_SEGMENTS; i++)
            {
                float t = (float)i / TRAIL_SEGMENTS;
                Vector2 pos = Vector2.Lerp(headScreen, tailScreen, t);
                float alpha = (1f - t) * lifeAlpha;
                float radius = BOLT_RADIUS * (1f - t * 0.65f);
                int sz = Math.Max(1, (int)(radius * 2));

                // Outer cyan glow
                Color c1 = new Color(60, 190, 255) * alpha * 0.7f;
                Rectangle r1 = new Rectangle((int)(pos.X - radius), (int)(pos.Y - radius), sz, sz);
                sb.Draw(pixel, r1, c1);

                // Bright white core near head
                if (t < 0.35f)
                {
                    float coreAlpha = (1f - t / 0.35f) * lifeAlpha;
                    int csz = Math.Max(1, (int)(radius * 0.55f) * 2);
                    Color cw = Color.White * coreAlpha * 0.95f;
                    Rectangle rc = new Rectangle((int)(pos.X - csz / 2), (int)(pos.Y - csz / 2), csz, csz);
                    sb.Draw(pixel, rc, cw);
                }
            }

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Spawn a small impact burst of dust
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 8; i++)
                {
                    Dust d = Dust.NewDustDirect(
                        Projectile.Center - new Vector2(4, 4), 8, 8,
                        DustID.WhiteTorch, Scale: 0.9f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(2f, 2f);
                }
            }
        }

        public override bool ShouldUpdatePosition() => true;
    }
}
