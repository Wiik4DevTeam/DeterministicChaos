using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    
    // Club symbol that erupts from the ground after JackOfClubs ground impact.
    // ai[0] = spawn delay (ticks before becoming visible/active).
    // Flies straight up, points upward, no gravity.
    
    public class ClubEarthquakeProjectile : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/FriendlyClubProjectile";

        private const int LifeTime = 80;
        private const int FadeInTicks = 6;
        private const int FadeOutTicks = 12;

        private ref float SpawnDelay => ref Projectile.ai[0];
        private int age;
        private bool delayDone;
        private Vector2 savedVelocity;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI()
        {
            // On first tick, save the velocity before zeroing it for the delay
            if (age == 0 && !delayDone && savedVelocity == Vector2.Zero && Projectile.velocity != Vector2.Zero)
            {
                savedVelocity = Projectile.velocity;
            }

            // Wait for staggered spawn delay
            if (!delayDone)
            {
                Projectile.velocity = Vector2.Zero;
                if (SpawnDelay > 0f)
                {
                    SpawnDelay--;
                    return;
                }

                delayDone = true;
                Projectile.timeLeft = LifeTime;

                // Restore the saved upward velocity
                Projectile.velocity = savedVelocity;

                // Burst of ground dust when erupting
                SoundEngine.PlaySound(SoundID.Item69 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);

                for (int i = 0; i < 6; i++)
                {
                    Vector2 dv = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1f));
                    Dust d = Dust.NewDustDirect(Projectile.Bottom + new Vector2(-8f, 0f), 16, 0, DustID.Dirt, dv.X, dv.Y, 0, default, 1.3f);
                    d.noGravity = false;
                }
            }

            age++;

            // Fly straight up, no gravity, no deceleration, ignore tiles
            // (tileCollide is already false in SetDefaults)

            // Point upward
            Projectile.rotation = -MathHelper.PiOver2;

            // Green glow dust trailing behind
            if (age % 3 == 0)
            {
                Dust glow = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), 0, 0, DustID.GreenTorch, 0f, 1f, 120, default, 0.9f);
                glow.noGravity = true;
                glow.fadeIn = 0.6f;
            }
        }

        public override bool? CanDamage()
        {
            return delayDone && age > 0 ? null : false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Clubs deal half damage
            modifiers.FinalDamage *= 0.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.6f, Pitch = 0.1f }, target.Center);

            // Launch non-boss enemies upward on hit
            if (!target.boss && target.knockBackResist > 0f)
            {
                // Nudge position up to detach from ground, prevents ground collision
                // from zeroing velocity.Y on the very next frame
                target.position.Y -= 12f;
                target.velocity.Y = -16f;
                target.velocity.X *= 0.3f;
                target.netUpdate = true;
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GreenTorch, dustVel.X, dustVel.Y, 100, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!delayDone) return false;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            float fadeIn = MathHelper.Clamp(age / (float)FadeInTicks, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutTicks, 0f, 1f);
            float alpha = Math.Min(fadeIn, fadeOut);

            float scale = Projectile.scale * alpha;

            Color tint = Color.Lerp(Color.White, new Color(60, 200, 60), 0.6f) * alpha;
            Color glowColor = new Color(40, 180, 40) * (0.45f * alpha);

            // Glow layer
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, scale * 1.35f, SpriteEffects.None, 0);
            // Main sprite
            Main.EntitySpriteDraw(tex, pos, null, tint, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
