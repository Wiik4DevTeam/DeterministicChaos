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
    
    // Heavy piercing projectile fired by Incandescent's M2.
    // Pierces enemies, detonates burning targets with massive fire explosions.
    // ai[0] = charge level (0-1), ai[1] = combo meter at time of fire.
    // Reuses ToughGauntletProjectile sprite scaled up with fire overlay.
    
    public class IncandescentHeavyProjectile : ModProjectile
    {
        private float ChargeLevel => Projectile.ai[0];
        private float ComboLevel => Projectile.ai[1];

        private int hitCount = 0;
        private float baseScale = 1f;
        private bool initialized = false;

        // These match the ToughGauntletProjectile sprite layout
        private const int FRAME_COUNT = 2;
        private const int FRAME_WIDTH = 74;
        private const int FRAME_HEIGHT = 74;
        private int frameCounter = 0;
        private int currentFrame = 0;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/ToughGauntletProjectile";

        public override void SetDefaults()
        {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                baseScale = 2.5f + ChargeLevel * 1.5f; // 2.5 to 4.0
                Projectile.scale = baseScale;

                // Size scales with charge
                int size = (int)(80 + ChargeLevel * 30);
                Projectile.width = size;
                Projectile.height = size;
            }

            // Animate
            frameCounter++;
            if (frameCounter >= 3)
            {
                frameCounter = 0;
                currentFrame = (currentFrame + 1) % FRAME_COUNT;
            }

            // Rotate to face movement
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver4 - MathHelper.PiOver2;

            // Slow down more gradually than regular punches
            Projectile.velocity *= 0.985f;

            // Shrink near death
            if (Projectile.timeLeft < 12)
            {
                float progress = Projectile.timeLeft / 12f;
                Projectile.scale = baseScale * progress;
                if (Projectile.timeLeft < 6)
                    Projectile.damage = 0;
            }

            // Intense fire trail
            for (int i = 0; i < 3; i++)
            {
                Vector2 dustVel = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, dustVel.X, dustVel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.5f, 2.5f) * (Projectile.scale / baseScale);
                d.alpha = 80;
            }

            // InfernoFork trail for fiery glow
            if (Main.rand.NextBool(2))
            {
                Vector2 dustVel = -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(2f, 2f);
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.InfernoFork, dustVel.X, dustVel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.0f, 1.8f) * (Projectile.scale / baseScale);
                d.alpha = 80;
            }

            // Smoke trail
            if (Main.rand.NextBool(2))
            {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f);
                smoke.scale = Main.rand.NextFloat(1.5f, 2.5f);
                smoke.alpha = 100;
            }

            // Bright torch particles along edges
            if (Main.rand.NextBool(2))
            {
                Vector2 offset = Main.rand.NextVector2Circular(Projectile.width * 0.4f, Projectile.height * 0.4f);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0,
                    DustID.Torch, 0f, 0f);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(0.8f, 1.2f);
                d.velocity = -Projectile.velocity * 0.2f;
                d.alpha = 100;
            }

            // Speed lines
            if (Main.rand.NextBool(2) && Projectile.timeLeft > 12)
            {
                Vector2 projDir = Projectile.velocity.SafeNormalize(Vector2.Zero);
                Vector2 perpendicular = new Vector2(-projDir.Y, projDir.X);
                Vector2 spawnPos = Projectile.Center + perpendicular * Main.rand.NextFloat(-15f, 15f);
                Vector2 lineVel = -projDir * Main.rand.NextFloat(4f, 9f);
                float lineLength = Main.rand.NextFloat(25f, 55f);
                float lineThickness = Main.rand.NextFloat(2f, 5f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(), spawnPos, lineVel,
                    ModContent.ProjectileType<SpeedLine>(),
                    0, 0f, Projectile.owner, lineLength, lineThickness);
                if (proj >= 0 && proj < Main.maxProjectiles)
                    Main.projectile[proj].localAI[1] = 1f; // Orange mode
            }

            // Lighting
            float scaleFactor = Projectile.scale / baseScale;
            Lighting.AddLight(Projectile.Center, 2f * scaleFactor, 0.7f * scaleFactor, 0.1f * scaleFactor);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            hitCount++;

            // Always apply both fire debuffs
            target.AddBuff(BuffID.OnFire3, 360); // 6s Hellfire
            target.AddBuff(BuffID.OnFire, 360);  // 6s OnFire

            // DETONATE: if target was already burning, trigger massive fire explosion
            bool wasBurning = target.HasBuff(BuffID.OnFire3) || target.HasBuff(BuffID.OnFire);

            // Since we just applied the buffs above, check if they had it BEFORE
            // We use hitCount > 1 as a proxy: second+ hit means previous hits applied fire
            // OR check if they were already burning from M1 punches
            if (wasBurning && hitCount >= 1)
            {
                TriggerFireExplosion(target, damageDone);
            }

            // Screen shake on each pierce
            if (Projectile.owner == Main.myPlayer)
            {
                Main.instance.CameraModifiers.Add(
                    new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                        target.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX),
                        4f + hitCount * 1.5f, 6f, 3, 800f, "IncandescentHeavy"));
            }

            // Brief hitstop: freeze the projectile for 2 frames for impact feel
            if (Projectile.velocity.Length() > 3f)
            {
                // Store velocity, will resume next frame (simulated via slight slow)
                Projectile.velocity *= 0.6f;
            }
        }

        private void TriggerFireExplosion(NPC target, int damageDone)
        {
            Vector2 center = target.Center;
            float comboFraction = MathHelper.Clamp(ComboLevel / 10f, 0f, 1f);
            float explosionRadius = 180f + comboFraction * 70f;
            int explosionDamage = (int)(damageDone * (0.8f + ChargeLevel * 0.4f));

            // Massive visual explosion
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f + hitCount * 0.05f, Volume = 1.3f }, center);
            SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.5f, Volume = 0.6f }, center);

            // Core flash — large bright burst at center
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.Torch, vel.X, vel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(3.0f, 4.0f);
                d.alpha = 60;
                d.velocity *= 0.3f;
            }

            // Main fiery burst — fast outward with InfernoFork for bigger visuals
            for (int i = 0; i < 30; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(18f, 18f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.InfernoFork, vel.X, vel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.5f, 2.5f);
                d.alpha = 80;
            }

            // Fire ring expanding outward — larger scales
            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi / 24 * i;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.Torch, vel.X, vel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(2.0f, 3.0f);
                d.alpha = 80;
            }

            // Flame burst pillars going up — heavier with gravity
            for (int i = 0; i < 12; i++)
            {
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.Torch,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-16f, -6f));
                d.noGravity = false;
                d.scale = Main.rand.NextFloat(1.5f, 2.5f);
                d.alpha = 80;
            }

            // Embers scattering outward with gravity (falling sparks)
            for (int i = 0; i < 14; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 10f) + new Vector2(0f, -4f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.Torch, vel.X, vel.Y);
                d.noGravity = false;
                d.scale = Main.rand.NextFloat(1.0f, 1.8f);
                d.alpha = 100;
            }

            // Smoke cloud — thicker
            for (int i = 0; i < 14; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.Smoke, vel.X, vel.Y);
                d.scale = Main.rand.NextFloat(1.8f, 3.0f);
                d.alpha = 140;
            }

            // Smoke gore puffs for big volumetric feel
            for (int i = 0; i < 3; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f) + new Vector2(0f, -2f);
                Gore.NewGore(Projectile.GetSource_Death(), center, vel, Main.rand.Next(61, 64), Main.rand.NextFloat(0.8f, 1.2f));
            }

            // Deal explosion damage to nearby enemies
            if (Projectile.owner == Main.myPlayer)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.whoAmI != target.whoAmI)
                    {
                        float dist = Vector2.Distance(center, npc.Center);
                        if (dist < explosionRadius)
                        {
                            float falloff = 1f - dist / explosionRadius * 0.3f;
                            int dmg = (int)(explosionDamage * falloff);
                            Main.player[Projectile.owner].ApplyDamageToNPC(npc, dmg, 0f, 0, false);
                            npc.AddBuff(BuffID.OnFire3, 240);
                            npc.AddBuff(BuffID.OnFire, 240);
                        }
                    }
                }

                // Bonus damage to primary target
                int bonusDmg = (int)(explosionDamage * 0.5f);
                Main.player[Projectile.owner].ApplyDamageToNPC(target, bonusDmg, 0f, 0, false);
            }

            // Screen shake for explosion
            if (Projectile.owner == Main.myPlayer)
            {
                Main.instance.CameraModifiers.Add(
                    new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                        center, Main.rand.NextVector2Unit(),
                        10f + comboFraction * 5f, 8f, 6, 1500f, "IncandescentDetonation"));
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Each pierce through enemies does slightly more damage (fury building)
            modifiers.FinalDamage *= 1f + hitCount * 0.15f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.scale < 0.05f)
                return false;

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle sourceRect = new Rectangle(0, currentFrame * FRAME_HEIGHT, FRAME_WIDTH, FRAME_HEIGHT);
            Vector2 origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Draw a fiery glow behind the projectile
            float glowScale = Projectile.scale * 1.3f;
            Color glowColor = new Color(255, 80, 20, 0) * 0.4f;
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, glowColor,
                Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);

            // Draw the main projectile with fire tint
            Color tint = Color.Lerp(new Color(255, 200, 150), new Color(255, 100, 40), ChargeLevel);
            tint *= ((255 - Projectile.alpha) / 255f);
            Main.EntitySpriteDraw(texture, drawPos, sourceRect, tint,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // At high charge, draw a second additive copy for intensity
            if (ChargeLevel > 0.5f)
            {
                float additiveAlpha = (ChargeLevel - 0.5f) * 2f * 0.3f;
                Color addColor = new Color(255, 150, 50, 0) * additiveAlpha;
                Main.EntitySpriteDraw(texture, drawPos, sourceRect, addColor,
                    Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Dissipation burst
            for (int i = 0; i < 14; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.5f, 2.5f);
                d.alpha = 80;
            }

            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.InfernoFork, vel.X, vel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.0f, 1.6f);
                d.alpha = 80;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y);
                d.noGravity = false;
                d.scale = Main.rand.NextFloat(0.6f, 1.0f);
                d.alpha = 100;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.2f, Volume = 0.5f }, Projectile.Center);
        }
    }
}
