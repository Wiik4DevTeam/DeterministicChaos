using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class KnightSphere : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<Items.RoaringLensPlayer>().RoaringLensActive)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;

            Vector2 anchor = player.Center + new Vector2(0f, -player.height / 2f - 10f);
            Projectile.Center = anchor;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;
            Projectile.scale = 1f;

            // Broad aura around player + sphere glow.
            Lighting.AddLight(player.Center, 0.30f, 0.36f, 0.55f);
            Lighting.AddLight(Projectile.Center, 0.36f, 0.45f, 0.68f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Aura glow (larger, semi-transparent)
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                new Color(255, 255, 255, 0) * 0.35f,
                Projectile.rotation,
                origin,
                Projectile.scale * 1.6f,
                SpriteEffects.None,
                0f);

            // Main sphere
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0f);

            return false; // Skip default drawing
        }
    }
}
