using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Projectiles.Friendly;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class IncandescentPlayer : ModPlayer
    {
        // ── Combo Meter ──────────────────────────────────────────────
        // Builds from consecutive M1 hits, decays over time, boosts damage
        public int ComboMeter = 0;
        public int ComboDecayTimer = 0;
        private const int COMBO_DECAY_DELAY = 90;  // 1.5s before combo starts decaying
        private const int MAX_COMBO = 10;

        // ── Attack state ─────────────────────────────────────────────
        public int AttackCooldown = 0;
        private const int PUNCH_COOLDOWN = 6;       // Very fast punches
        private const int HEAVY_COOLDOWN = 20;       // Short recovery after heavy attack

        // ── Punch alternation ────────────────────────────────────────
        private bool lastPunchWasLeft = false;

        // ── Punch lunge (M1 movement) ────────────────────────────────
        // Each punch nudges the player slightly forward
        private const float LUNGE_STRENGTH = 1.2f;
        private const float LUNGE_STRENGTH_AIR = 0.8f;
        private const float LUNGE_FRICTION = 0.85f;
        private int lungeTicks = 0;
        private Vector2 lungeDirection = Vector2.Zero;
        private const int LUNGE_DURATION = 3;

        // ── Heavy Attack (M2) ────────────────────────────────────────
        // Wind-up → release piercing projectile
        public bool IsWindingUp = false;
        public bool IsHeavySwinging = false;
        private int heavySwingTimer = 0;
        private const int HEAVY_SWING_DURATION = 15;  // Max frames of lunge drag
        private int windUpTimer = 0;
        private const int WIND_UP_TICKS = 24;        // 0.4s wind-up
        private const int MAX_WIND_UP_TICKS = 60;     // Can hold up to 1s for bonus
        private Vector2 windUpDirection = Vector2.Zero;
        private float windUpCharge = 0f;              // 0-1 charge level

        // ── Screen shake ─────────────────────────────────────────────
        private float shakeIntensity = 0f;
        private int shakeTimer = 0;

        // ── Heat shimmer visual ──────────────────────────────────────
        private float heatLevel = 0f; // 0-1, increases with combo

        public override void ResetEffects()
        {
            if (AttackCooldown > 0) AttackCooldown--;

            // Combo decay
            if (ComboDecayTimer > 0)
            {
                ComboDecayTimer--;
            }
            else if (ComboMeter > 0)
            {
                // Decay 1 per 10 ticks once delay expires
                if (Player.miscCounter % 10 == 0)
                    ComboMeter--;
            }

            // Lunge physics
            if (lungeTicks > 0)
            {
                lungeTicks--;
                float power = (float)lungeTicks / LUNGE_DURATION;
                bool onGround = Player.velocity.Y == 0f;
                float strength = onGround ? LUNGE_STRENGTH : LUNGE_STRENGTH_AIR;
                Player.velocity += lungeDirection * strength * power * LUNGE_FRICTION;

                // Cap lunge speed so it feels like a nudge
                float maxLungeSpeed = 8f;
                if (Player.velocity.Length() > maxLungeSpeed)
                    Player.velocity = Player.velocity.SafeNormalize(Vector2.Zero) * maxLungeSpeed;
            }

            // Screen shake decay
            if (shakeTimer > 0)
            {
                shakeTimer--;
                if (Player.whoAmI == Main.myPlayer)
                {
                    float shake = shakeIntensity * (shakeTimer / 10f);
                    Main.instance.CameraModifiers.Add(
                        new Terraria.Graphics.CameraModifiers.PunchCameraModifier(
                            Player.Center,
                            Main.rand.NextVector2Unit(),
                            shake, 6f, 2, 1000f, "IncandescentPunch"));
                }
            }

            // Heat level follows combo
            float targetHeat = MathHelper.Clamp(ComboMeter / 10f, 0f, 1f);
            heatLevel = MathHelper.Lerp(heatLevel, targetHeat, 0.05f);
        }

        public override void PostUpdate()
        {
            // Handle wind-up for heavy attack
            if (IsWindingUp)
            {
                UpdateWindUp();
            }

            // Handle heavy swing travel
            if (IsHeavySwinging)
            {
                UpdateHeavySwing();
            }

            // Visual effects when holding Incandescent with Bravery trait
            if (Player.HeldItem.ModItem is Incandescent && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                Player.armorEffectDrawShadow = true;

                // Intense fiery aura that scales with combo
                float comboFraction = MathHelper.Clamp(ComboMeter / 10f, 0f, 1f);
                float lightR = 1.4f + comboFraction * 0.6f;
                float lightG = 0.3f + comboFraction * 0.2f;
                float lightB = 0.05f + comboFraction * 0.15f;
                Lighting.AddLight(Player.Center, lightR, lightG, lightB);

                // Persistent fire particles, more frequent at high combo
                int dustChance = Math.Max(1, 3 - (int)(comboFraction * 2));
                if (Main.rand.NextBool(dustChance))
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                        DustID.Torch, Player.velocity.X * 0.2f, Player.velocity.Y * 0.2f);
                    dust.noGravity = true;
                    dust.scale = 1.4f + comboFraction * 0.8f;
                    dust.alpha = 60;
                }

                // Solar flare sparks at higher combo
                if (ComboMeter >= 5 && Main.rand.NextBool(Math.Max(1, 5 - (int)(comboFraction * 3))))
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                        DustID.Torch, 0f, -1f);
                    dust.noGravity = true;
                    dust.scale = 0.6f + comboFraction * 0.4f;
                    dust.alpha = 120;
                }

                // At high combo, emit rising ember particles
                if (ComboMeter >= 7 && Main.rand.NextBool(3))
                {
                    Dust ember = Dust.NewDustDirect(
                        Player.position + new Vector2(Main.rand.NextFloat(-10f, Player.width + 10f), Player.height),
                        0, 0, DustID.Torch,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f));
                    ember.noGravity = false;
                    ember.scale = Main.rand.NextFloat(0.6f, 1.0f);
                    ember.alpha = 100;
                }

                // Display combo meter above head
                if (ComboMeter > 0 && Player.whoAmI == Main.myPlayer && Player.miscCounter % 60 == 0)
                {
                    Color comboColor = Color.Lerp(new Color(255, 200, 80), new Color(255, 50, 20), comboFraction);
                    Rectangle textRect = new Rectangle((int)Player.Top.X - 20, (int)Player.Top.Y - 40, 40, 20);
                    CombatText.NewText(textRect, comboColor, $"{ComboMeter}x", false, false);
                }
            }
        }

        public override void PostUpdateRunSpeeds()
        {
            if (Player.HeldItem.ModItem is Incandescent && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                // Base movement bonuses (stronger than gauntlet)
                Player.maxRunSpeed *= 1.45f;
                Player.runAcceleration *= 1.7f;
                Player.runSlowdown *= 0.5f;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Input Processing
        // ─────────────────────────────────────────────────────────────

        public void ProcessInput(bool isRightClick)
        {
            if (isRightClick)
            {
                StartHeavyAttack();
            }
            else
            {
                PerformPunch();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  M1: Rapid Fire Punches
        // ─────────────────────────────────────────────────────────────

        private void PerformPunch()
        {
            AttackCooldown = PUNCH_COOLDOWN;

            Vector2 direction = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            int baseDamage = Player.GetWeaponDamage(Player.HeldItem);
            float knockback = Player.GetWeaponKnockback(Player.HeldItem, Player.HeldItem.knockBack);
            float shootSpeed = Player.HeldItem.shootSpeed;
            int damage = baseDamage;

            // Alternate left/right punch for visual variety
            lastPunchWasLeft = !lastPunchWasLeft;

            // Offset spawn position slightly to the side for alternating fists
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
            float sideOffset = lastPunchWasLeft ? -8f : 8f;
            Vector2 spawnPos = Player.Center + perpendicular * sideOffset + direction * 10f;

            // Calculate velocity (inherits player momentum)
            float playerSpeed = Player.velocity.Length();
            float totalSpeed = shootSpeed + playerSpeed * 0.5f;
            Vector2 velocity = direction * totalSpeed;

            // Add slight random angle for organic feel (±5°)
            float randomAngle = MathHelper.ToRadians(Main.rand.NextFloat(-5f, 5f));
            velocity = velocity.RotatedBy(randomAngle);

            if (Player.whoAmI == Main.myPlayer)
            {
                int proj = Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<ToughGauntletProjectile>(),
                    damage,
                    knockback,
                    Player.whoAmI,
                    0, // small punch
                    1  // ai[1] = 1 signals Incandescent origin for enhanced on-hit
                );

                // Make incandescent punches apply both Hellfire AND OnFire
                // (handled in IncandescentOnHit global)
            }

            // Lunge forward with each punch
            lungeDirection = direction;
            lungeTicks = LUNGE_DURATION;

            // Sound: rising pitch with combo
            float pitch = -0.2f + MathHelper.Clamp(ComboMeter * 0.03f, 0f, 0.6f);
            float volume = 0.6f + MathHelper.Clamp(ComboMeter * 0.01f, 0f, 0.2f);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch, Volume = volume }, Player.Center);

            // At high combo, play extra whoosh
            if (ComboMeter >= 10)
            {
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.5f + ComboMeter * 0.02f, Volume = 0.3f }, Player.Center);
            }

            // Punch VFX burst at spawn
            for (int i = 0; i < 5; i++)
            {
                Vector2 dustVel = direction * Main.rand.NextFloat(2f, 6f) + Main.rand.NextVector2Circular(2f, 2f);
                Dust d = Dust.NewDustDirect(spawnPos, 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1f, 1.8f);
            }

            // At 10+ combo, punches emit shockwave ring
            if (ComboMeter >= 10 && Player.whoAmI == Main.myPlayer)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = MathHelper.TwoPi / 12 * i;
                    Vector2 ringVel = angle.ToRotationVector2() * 4f;
                    Dust d = Dust.NewDustDirect(spawnPos, 0, 0, DustID.Torch, ringVel.X, ringVel.Y);
                    d.noGravity = true;
                    d.scale = 0.8f;
                    d.alpha = 120;
                }
            }

            // Mini screen shake on each punch
            shakeIntensity = 2f + MathHelper.Clamp(ComboMeter * 0.15f, 0f, 3f);
            shakeTimer = 3;
        }

        // ─────────────────────────────────────────────────────────────
        //  M2: Heavy Piercing Attack
        // ─────────────────────────────────────────────────────────────

        private void StartHeavyAttack()
        {
            IsWindingUp = true;
            windUpTimer = 0;
            windUpDirection = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);

            // Slow the player during wind-up
            Player.velocity *= 0.3f;

            SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 0.6f }, Player.Center);
        }

        private void UpdateWindUp()
        {
            windUpTimer++;

            // Track mouse direction during wind-up
            windUpDirection = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);

            windUpCharge = MathHelper.Clamp((float)windUpTimer / MAX_WIND_UP_TICKS, 0f, 1f);

            // Player pulls back during wind-up (recoil before strike)
            if (windUpTimer < WIND_UP_TICKS)
            {
                float pullBack = 1.5f * (1f - (float)windUpTimer / WIND_UP_TICKS);
                Player.velocity -= windUpDirection * pullBack * 0.3f;
                // Prevent excessive backward speed
                if (Player.velocity.Length() > 6f)
                    Player.velocity = Player.velocity.SafeNormalize(Vector2.Zero) * 6f;
            }

            // Freeze movement during wind-up
            Player.maxRunSpeed *= 0.2f;
            Player.runAcceleration *= 0.1f;

            // Charging VFX: swirling fire around the player
            float chargeAlpha = MathHelper.Clamp(windUpTimer / (float)WIND_UP_TICKS, 0f, 1f);
            if (chargeAlpha > 0.2f)
            {
                for (int i = 0; i < (int)(chargeAlpha * 4); i++)
                {
                    float angle = Main.GameUpdateCount * 0.15f + i * MathHelper.PiOver2;
                    float radius = 30f - chargeAlpha * 15f; // Particles spiral inward
                    Vector2 dustPos = Player.Center + new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Torch,
                        -windUpDirection.X * 0.5f, -windUpDirection.Y * 0.5f);
                    d.noGravity = true;
                    d.scale = 0.6f + chargeAlpha * 0.6f;
                    d.alpha = 120;
                }

                Lighting.AddLight(Player.Center, 1.5f * chargeAlpha, 0.4f * chargeAlpha, 0.05f);
            }

            // Auto-release at min charge, or if player releases right-click
            bool canRelease = windUpTimer >= WIND_UP_TICKS;
            bool forceRelease = windUpTimer >= MAX_WIND_UP_TICKS;
            bool mouseReleased = canRelease && !Main.mouseRight && Player.whoAmI == Main.myPlayer;

            if (forceRelease || mouseReleased)
            {
                ReleaseHeavyAttack();
            }
            // Safety timeout
            else if (windUpTimer > MAX_WIND_UP_TICKS + 30)
            {
                IsWindingUp = false;
                windUpTimer = 0;
            }
        }

        private void ReleaseHeavyAttack()
        {
            IsWindingUp = false;
            IsHeavySwinging = true;
            heavySwingTimer = 0;

            int baseDamage = Player.GetWeaponDamage(Player.HeldItem);
            float knockback = Player.GetWeaponKnockback(Player.HeldItem, Player.HeldItem.knockBack);

            // Heavy attack damage: 4x base + charge bonus + combo bonus
            // Combo scales heavy damage massively: each combo point adds 20% (up to 3x at max 10)
            float chargeMultiplier = 4f + windUpCharge * 2f; // 4x to 6x
            float comboMultiplier = 1f + ComboMeter * 0.2f; // 1x at 0 combo, 3x at 10 combo
            int heavyDamage = (int)(baseDamage * chargeMultiplier * comboMultiplier);

            float heavySpeed = Player.HeldItem.shootSpeed * (1.5f + windUpCharge * 0.5f);
            Vector2 velocity = windUpDirection * heavySpeed;

            if (Player.whoAmI == Main.myPlayer)
            {
                // Fire the heavy piercing projectile
                int proj = Projectile.NewProjectile(
                    Player.GetSource_ItemUse(Player.HeldItem),
                    Player.Center + windUpDirection * 20f,
                    velocity,
                    ModContent.ProjectileType<IncandescentHeavyProjectile>(),
                    heavyDamage,
                    knockback * 2f,
                    Player.whoAmI,
                    windUpCharge, // ai[0] = charge level
                    ComboMeter    // ai[1] = combo meter for VFX scaling
                );
            }

            // Massive lunge forward on release — heavy punch propels the player
            float lungeForce = 20f + windUpCharge * 10f;
            Player.velocity = windUpDirection * lungeForce;

            // consumption: spend half the combo on heavy attack for huge payoff
            if (ComboMeter > 0)
            {
                ComboMeter = ComboMeter / 2;
                ComboDecayTimer = COMBO_DECAY_DELAY;
            }

            // Heavy attack cooldown
            AttackCooldown = HEAVY_COOLDOWN;

            // Impact VFX
            shakeIntensity = 8f + windUpCharge * 6f;
            shakeTimer = 8;

            // Release burst of fire in a cone
            for (int i = 0; i < 15; i++)
            {
                float angle = windUpDirection.ToRotation() + MathHelper.ToRadians(Main.rand.NextFloat(-25f, 25f));
                float speed = Main.rand.NextFloat(6f, 14f);
                Vector2 dustVel = angle.ToRotationVector2() * speed;
                Dust d = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Torch, dustVel.X, dustVel.Y);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(0.8f, 1.4f);
                d.alpha = 120;
            }

            // Sound: massive impact
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 1.2f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.4f, Volume = 0.8f }, Player.Center);

            windUpTimer = 0;
            windUpCharge = 0f;
        }

        private void UpdateHeavySwing()
        {
            heavySwingTimer++;

            // Blend lunge drag with normal movement over time
            float dragProgress = (float)heavySwingTimer / HEAVY_SWING_DURATION;
            float dragStrength = MathHelper.Lerp(0.90f, 0.99f, dragProgress);
            Player.velocity *= dragStrength;

            // Trail fire behind the player during the lunge
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                    DustID.Torch, -Player.velocity.X * 0.3f, -Player.velocity.Y * 0.3f);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.5f, 2.5f);
            }

            if (heavySwingTimer >= HEAVY_SWING_DURATION || Player.velocity.Length() < 2f)
            {
                IsHeavySwinging = false;
                heavySwingTimer = 0;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Combo Meter Increment (called from global OnHitNPC)
        // ─────────────────────────────────────────────────────────────

        public void OnPunchHitEnemy()
        {
            if (ComboMeter < MAX_COMBO)
                ComboMeter++;
            ComboDecayTimer = COMBO_DECAY_DELAY;
        }
    }
}
