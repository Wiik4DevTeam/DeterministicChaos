using Microsoft.Xna.Framework;
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
    public class AerodynamicBootsPlayer : ModPlayer
    {
        // Grace stacks (0-5) - gained on parry or hit, consumed on taking damage
        public int GraceStacks = 0;
        public const int MAX_GRACE_STACKS = 5;

        // Parry window - active period after dash where any damage is parried
        private int parryActiveTimer = 0;
        private const int PARRY_WINDOW = 10;

        public bool IsCurrentlyInParryWindow => parryActiveTimer > 0;

        // Cooldown system
        private int cooldownTimer = 0;
        public bool IsOnCooldown => cooldownTimer > 0;
        public float CooldownProgress => (float)cooldownTimer / currentMaxCooldown;
        private int currentMaxCooldown = COOLDOWN_BASE; // Tracks the active cooldown ceiling for UI
        public float CurrentMaxCooldownSeconds => currentMaxCooldown / 60f;

        // Cooldown duration
        public const int COOLDOWN_BASE = 42;      // ~0.5s base
        private const int COOLDOWN_PER_STACK = 3;  // -3 ticks per grace stack

        // Spam prevention, consecutive misses (no hit/parry) escalate cooldown
        private int consecutiveWhiffs = 0;
        private const int WHIFF_THRESHOLD = 3;           // After 3 misses, exhaustion kicks in
        private const int EXHAUSTION_COOLDOWN = 120;     // ~2s exhaustion cooldown

        // Dash state - precise line movement
        private bool isDashing = false;
        public bool IsDashing => isDashing;
        private bool hasHitEnemy = false;
        private bool hasParried = false;
        public bool HasParriedThisDash => hasParried;
        private int dashTimer = 0;
        private const int DASH_DURATION = 6; // Very short, precise dash (~0.1s)
        private Vector2 dashVelocity = Vector2.Zero;
        private const float DASH_SPEED = 32f; // Very fast, precise line

        // Immunity after hit
        private const int IMMUNITY_FRAMES_ON_HIT = 20;

        // Air maneuverability bonus per stack
        private const float AIR_ACCEL_PER_STACK = 0.12f;
        private const float AIR_MAX_SPEED_PER_STACK = 0.6f;

        public override void ResetEffects()
        {
            if (parryActiveTimer > 0)
                parryActiveTimer--;
        }

        public override void PostUpdateRunSpeeds()
        {
            // Remove terminal velocity cap while holding AerodynamicBoots with Integrity trait
            if (Player.HeldItem.ModItem is AerodynamicBoots && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Integrity)
            {
                Player.maxFallSpeed = 100f;
            }
        }

        public override void PostUpdate()
        {
            // Decrement cooldown
            if (cooldownTimer > 0)
                cooldownTimer--;

            // Handle dash state - lock velocity to dash direction
            if (isDashing)
            {
                // Force the player along the dash line at constant high speed
                Player.velocity = dashVelocity;
                // Disable gravity during dash
                Player.gravity = 0f;
                Player.fallStart = (int)(Player.position.Y / 16f); // Prevent fall damage

                dashTimer++;
                if (dashTimer >= DASH_DURATION)
                {
                    EndDash();
                }
            }

            // Apply air maneuverability bonus (only with Integrity trait)
            if (!IsPlayerOnGround() && GraceStacks > 0 && !isDashing && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Integrity)
            {
                ApplyAirManeuverability();
            }
        }

        private bool IsPlayerOnGround()
        {
            return Player.velocity.Y == 0f;
        }

        private void ApplyAirManeuverability()
        {
            float bonusAccel = AIR_ACCEL_PER_STACK * GraceStacks;
            float bonusMaxSpeed = AIR_MAX_SPEED_PER_STACK * GraceStacks;

            if (Player.controlLeft)
            {
                Player.velocity.X -= bonusAccel;
                float maxLeft = -(Player.maxRunSpeed + bonusMaxSpeed);
                if (Player.velocity.X < maxLeft)
                    Player.velocity.X = maxLeft;
            }
            else if (Player.controlRight)
            {
                Player.velocity.X += bonusAccel;
                float maxRight = Player.maxRunSpeed + bonusMaxSpeed;
                if (Player.velocity.X > maxRight)
                    Player.velocity.X = maxRight;
            }
        }

        // --- PARRY SYSTEM ---

        public override bool FreeDodge(Player.HurtInfo info)
        {
            // Only works with Integrity trait
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Integrity)
                return false;

            if (parryActiveTimer > 0)
            {
                hasParried = true;
                GainStack();
                consecutiveWhiffs = 0; // Successful parry resets whiff counter

                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Parry") with { PitchVariance = 0.2f }, Player.Center);

                for (int i = 0; i < 20; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                    Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = 1.5f;
                }

                // Push player away from damage source using dash reversal
                Player.velocity = -dashVelocity.SafeNormalize(Vector2.UnitX) * 12f;
                parryActiveTimer = 0;

                // End the dash so velocity isn't overwritten
                if (isDashing)
                {
                    isDashing = false;
                    dashTimer = 0;
                    int baseCd = COOLDOWN_BASE - (GraceStacks * COOLDOWN_PER_STACK);
                    cooldownTimer = baseCd;
                    currentMaxCooldown = baseCd;
                    hasHitEnemy = false;
                    hasParried = false;
                }

                Player.immune = true;
                Player.immuneTime = 9;

                return true;
            }

            return false;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            // Only works with Integrity trait
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Integrity)
                return;

            if (GraceStacks > 0 && parryActiveTimer <= 0)
            {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            // Only works with Integrity trait
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Integrity)
                return;

            if (GraceStacks > 0)
            {
                GraceStacks--;

                SoundEngine.PlaySound(SoundID.DD2_CrystalCartImpact with { Pitch = -0.2f }, Player.Center);

                for (int i = 0; i < 10; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.PurpleTorch, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = 1.2f;
                }
            }
        }

        // --- DASH SYSTEM ---

        public void StartDash(Vector2 direction)
        {
            isDashing = true;
            hasHitEnemy = false;
            hasParried = false;
            dashTimer = 0;

            // Normalize and set precise dash velocity
            float speedMult = 1f + (GraceStacks * 0.08f);
            dashVelocity = direction.SafeNormalize(Vector2.UnitX) * DASH_SPEED * speedMult;
            parryActiveTimer = PARRY_WINDOW;

        }

        public void GainStack()
        {
            if (GraceStacks < MAX_GRACE_STACKS)
            {
                GraceStacks++;
            }
        }

        public void OnDashHitEnemy(NPC target)
        {
            if (!isDashing) return;

            hasHitEnemy = true;
            GainStack();
            consecutiveWhiffs = 0; // Successful hit resets whiff counter

            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Parry") with { PitchVariance = 0.2f }, Player.Center);

            Player.immuneTime = IMMUNITY_FRAMES_ON_HIT;
            Player.immune = true;

            // End dash first (won't override velocity since hasHitEnemy is true)
            EndDash();

            // Knockback player backwards AFTER EndDash so it doesn't get overwritten
            Vector2 knockbackDir = (Player.Center - target.Center).SafeNormalize(Vector2.UnitX);
            Player.velocity = knockbackDir * 8f;
        }

        private void EndDash()
        {
            isDashing = false;

            // Reduce velocity significantly but don't zero it, keep momentum
            Player.velocity = dashVelocity * 0.25f;

            // Track whiff dashes for spam prevention
            if (!hasHitEnemy && !hasParried)
            {
                consecutiveWhiffs++;
            }

            // Apply escalating cooldown if spamming without engaging
            if (consecutiveWhiffs >= WHIFF_THRESHOLD)
            {
                cooldownTimer = EXHAUSTION_COOLDOWN;
                currentMaxCooldown = EXHAUSTION_COOLDOWN;
                consecutiveWhiffs = 0; // Reset after exhaustion penalty
            }
            else
            {
                int baseCd = COOLDOWN_BASE - (GraceStacks * COOLDOWN_PER_STACK);
                cooldownTimer = baseCd;
                currentMaxCooldown = baseCd;
            }

            hasHitEnemy = false;
            hasParried = false;
            dashTimer = 0;
        }

        public void ForceEndDash()
        {
            if (isDashing)
            {
                EndDash();
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            ForceEndDash();
            GraceStacks = 0;
            consecutiveWhiffs = 0;
        }
    }
}
