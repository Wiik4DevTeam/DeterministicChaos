using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Projectile for the Dark Shard throwable knife
    public class DarkShardProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // Enable afterimages
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
        }

        public override void AI()
        {
            // Rotate to face movement direction
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            // Apply gravity
            Projectile.velocity.Y += 0.15f;
            if (Projectile.velocity.Y > 16f)
            {
                Projectile.velocity.Y = 16f;
            }

            // Emit light so the projectile is always visible
            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.5f);

            // Spawn dark dust trail
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Draw afterimages
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                float trailOpacity = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color trailColor = Color.White * trailOpacity * 0.5f;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            // Draw main projectile fully lit
            Vector2 mainDrawPos = Projectile.position - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
            Main.EntitySpriteDraw(texture, mainDrawPos, null, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Spawn impact dust
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Spawn seeking knife on the owner's client (they handle projectile spawning)
            if (Main.myPlayer == Projectile.owner)
            {
                // Spawn 1 seeking knife at the target on every hit
                int knifeDamage = (int)(damageDone * 0.5f);
                
                // Pass a random orbit angle in ai[1] so it's consistent
                float orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                
                int p = Projectile.NewProjectile(
                    Projectile.GetSource_OnHit(target),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<RogueSeekingKnife>(),
                    knifeDamage,
                    2f,
                    Projectile.owner,
                    target.whoAmI,
                    orbitAngle
                );
                
                if (p >= 0 && p < Main.maxProjectiles)
                    Main.projectile[p].netUpdate = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Play hit sound on tile collision
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            return true;
        }
    }
}
