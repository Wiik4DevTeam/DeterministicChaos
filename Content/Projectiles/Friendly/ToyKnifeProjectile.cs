using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class ToyKnifeProjectile : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            // Rotate to face direction of travel
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            // Slight gravity for arc
            Projectile.velocity.Y += 0.05f;

            // Cyan trail dust
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch);
                dust.velocity = Projectile.velocity * 0.2f;
                dust.noGravity = true;
                dust.scale = 0.8f;
            }

            // Lighting
            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Notify the player about the hit for resource gains
            Player player = Main.player[Projectile.owner];
            if (player != null && player.active)
            {
                var toyKnifePlayer = player.GetModPlayer<ToyKnifePlayer>();
                toyKnifePlayer.OnToyKnifeHit(target);
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Death dust burst
            for (int i = 0; i < 8; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Draw with slight cyan tint
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            Color drawColor = lightColor;
            drawColor.R = (byte)(drawColor.R * 0.8f);
            drawColor.G = (byte)System.Math.Min(255, drawColor.G * 1.1f);
            drawColor.B = (byte)System.Math.Min(255, drawColor.B * 1.2f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                drawColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
