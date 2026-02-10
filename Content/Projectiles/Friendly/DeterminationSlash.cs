using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using DeterministicChaos.Content.Items;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class DeterminationSlash : ModProjectile
    {
        private ref float DamageMultiplier => ref Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];
        
        private bool initialized = false;
        private float baseScale = 1f;
        private int aliveTimer = 0;

        // With extraUpdates = 1, AI runs 2x per tick
        // 1 second of normal flight = 120 AI calls, then 1 second of shrinking = 120 AI calls
        private const int NormalFlightTicks = 20;
        private const int ShrinkTicks = 120;
        private const int TotalLifetime = NormalFlightTicks + ShrinkTicks;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300; // generous buffer; despawn handled manually
            Projectile.tileCollide = false; // Goes through walls
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1; // Smoother movement
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                // Set velocity from the aim angle
                float speed = 14f;
                Projectile.velocity = AimAngle.ToRotationVector2() * speed;
                
                // Scale up based on damage multiplier: 1x at multiplier 1, up to 1.6x at multiplier 10
                float multNormalized = MathHelper.Clamp((DamageMultiplier - 1f) / 9f, 0f, 1f);
                baseScale = MathHelper.Lerp(1f, 1.6f, multNormalized);
                Projectile.scale = baseScale;
                
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.9f }, Projectile.Center);
            }

            aliveTimer++;

            // Rotate sprite to face movement direction
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (aliveTimer <= NormalFlightTicks)
            {
                // Normal flight phase — full size
                Projectile.scale = baseScale;
            }
            else if (aliveTimer <= TotalLifetime)
            {
                // Shrink phase — scale down to 0
                float shrinkProgress = (float)(aliveTimer - NormalFlightTicks) / ShrinkTicks;
                Projectile.scale = MathHelper.Lerp(baseScale, 0f, shrinkProgress);
            }
            else
            {
                // Done
                Projectile.Kill();
                return;
            }

            // Red trailing dust
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.RedTorch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, default, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            // Slow down slightly over time for weight feel
            Projectile.velocity *= 0.995f;

            // Emit red light
            Lighting.AddLight(Projectile.Center, 0.5f, 0.1f, 0.1f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Apply the damage multiplier from the timed attack
            modifiers.FinalDamage *= DamageMultiplier;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() * 0.5f;

            // Draw afterimage trail
            for (int i = 0; i < Projectile.oldPos.Length && i < 6; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                    
                float alpha = 1f - (i / 6f);
                float scale = Projectile.scale * (1f - (i * 0.08f));
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                Main.EntitySpriteDraw(
                    tex, drawPos, null,
                    Color.Red * alpha * 0.5f,
                    Projectile.rotation, origin, scale, SpriteEffects.None, 0
                );
            }

            // Draw main projectile
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void OnKill(int timeLeft)
        {
            // Burst of red dust on death
            for (int i = 0; i < 10; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, vel, 0, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}
