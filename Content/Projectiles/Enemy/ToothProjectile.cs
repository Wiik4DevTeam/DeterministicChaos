using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    public class ToothProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.hostile = true;
            Projectile.friendly = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;

            Projectile.hide = false;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // emissive
            Lighting.AddLight(Projectile.Center, 0.9f, 0.9f, 0.9f);

            // small damping so it doesn't go forever
            Projectile.velocity *= 0.992f;

            // Shrink to zero when about to despawn
            if (Projectile.timeLeft <= 20)
            {
                float shrinkProgress = Projectile.timeLeft / 20f;
                Projectile.scale = shrinkProgress;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null) return false;

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Rectangle src = tex.Bounds;
            Vector2 origin = src.Size() * 0.5f;

            Color c = Color.White * 0.95f;
            Main.EntitySpriteDraw(tex, pos, src, c, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
