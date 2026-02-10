using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class EggProjectile : ModProjectile
    {
        private float rotation = 0f;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<Items.RangedMeleeDamageClass>();
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180; // 3 seconds
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.aiStyle = -1; // Custom AI
        }

        public override void AI()
        {
            // Apply gravity
            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            // Tumble rotation
            rotation += Projectile.velocity.X * 0.05f;
            Projectile.rotation = rotation;

            // Egg dust trail
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Cloud, 0f, 0f, 150, Color.White, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            SpawnKindnessPickup(target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Crack and disappear on tile hit
            CrackEffect();
            return true; // Kill projectile
        }

        public override void OnKill(int timeLeft)
        {
            CrackEffect();
        }

        private void CrackEffect()
        {
            // Egg cracking dust
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud, vel, 100, Color.White, 1f);
                dust.noGravity = false;
            }
            
            // Yellow yolk dust
            for (int i = 0; i < 4; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, vel, 50, default, 0.8f);
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.8f, Volume = 0.5f }, Projectile.Center);
        }

        private void SpawnKindnessPickup(Vector2 position)
        {
            // Only spawn on owning client for multiplayer safety
            if (Main.myPlayer != Projectile.owner)
                return;

            int pickup = Projectile.NewProjectile(
                Projectile.GetSource_OnHit(null),
                position,
                new Vector2(Main.rand.NextFloat(-2f, 2f), -3f), // Small upward pop
                ModContent.ProjectileType<KindnessPickup>(),
                0, 0f,
                Projectile.owner
            );

            if (pickup >= 0 && pickup < Main.maxProjectiles)
            {
                Main.projectile[pickup].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() * 0.5f;
            
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
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
