using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Small magnetic chunk, passes through tiles, pierces with a 1-second immunity window.
    // ai[0] = desired scale (set by LodestoneFork.Shoot for size variance).
    public class LodestoneForkSpam : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Throwing;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60; // 1-second pierce cooldown
            Projectile.timeLeft = 300;            // 5 seconds
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // First frame: read scale from ai[0]
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.scale = Projectile.ai[0] > 0f ? Projectile.ai[0] : 1f;
            }

            // Slight gravity
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.07f, 16f);
            Projectile.velocity.X *= 0.9985f;

            // Tumble rotation
            Projectile.rotation += (Projectile.velocity.X * 0.045f) + (Projectile.velocity.Y * 0.01f);

            // Fade in quickly
            Projectile.alpha = Math.Max(0, Projectile.alpha - 40);

            // Faint magnetic glow, scales with projectile size
            float glow = 0.06f * Projectile.scale;
            Lighting.AddLight(Projectile.Center, glow * 0.35f, glow * 0.6f, glow);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            SpriteBatch sb = Main.spriteBatch;
            float fadeIn = 1f - Projectile.alpha / 255f;

            // Short tinted motion trail
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float frac = (1f - (float)i / Projectile.oldPos.Length) * fadeIn;
                Vector2 pos = Projectile.oldPos[i] + new Vector2(Projectile.width, Projectile.height) * 0.5f - Main.screenPosition;
                sb.Draw(tex, pos, null,
                    new Color(80, 155, 255) * frac * 0.28f,
                    Projectile.oldRot[i], origin, Projectile.scale * 0.85f, SpriteEffects.None, 0f);
            }

            // Main sprite
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor * fadeIn,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
