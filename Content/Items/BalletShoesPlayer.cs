using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class BalletShoesPlayer : ModPlayer
    {
        // Grace stacks (0-3) - gained on parry or hit, consumed on taking damage
        public int ParryStacks = 0;
        private const int MAX_PARRY_STACKS = 3;

        // Parry window - active period after kick where any damage is parried
        private int parryActiveTimer = 0;
        private const int PARRY_WINDOW = 12; // 0.25 seconds at 60fps

        // Public property to check if currently in parry window (for projectile visuals)
        public bool IsCurrentlyInParryWindow => parryActiveTimer > 0;

        // Cooldown system
        private int cooldownTimer = 0;
        public bool IsOnCooldown => cooldownTimer > 0;
        public float CooldownProgress => cooldownTimer / 180f; // Max 3 seconds

        // Cooldown durations
        private const int COOLDOWN_PARRY = 40;   // 1 second
        private const int COOLDOWN_HIT = 20;     // 0.5 seconds
        private const int COOLDOWN_MISS = 110;   // 3 seconds

        // Kick state
        private bool isKicking = false;
        public bool IsKicking => isKicking;
        private bool hasHitEnemy = false;
        private bool hasParried = false;
        public bool HasParriedThisKick => hasParried;
        private int kickTimer = 0;
        private const int KICK_DURATION = 20; // How long the kick lasts
        private Vector2 dashVelocity = Vector2.Zero; // Stored velocity at dash start for knockback

        // Immunity after hit
        private const int IMMUNITY_FRAMES_ON_HIT = 30; // 0.5 seconds of immunity after hitting

        public override void ResetEffects()
        {
            // Decrement parry window timer
            if (parryActiveTimer > 0)
                parryActiveTimer--;
        }

        public override void PostUpdate()
        {
            // Decrement cooldown
            if (cooldownTimer > 0)
                cooldownTimer--;

            // Handle kick state
            if (isKicking)
            {
                kickTimer++;
                if (kickTimer >= KICK_DURATION)
                {
                    EndKick();
                }
            }
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            // Check if we're in the parry window - completely negate damage
            if (parryActiveTimer > 0)
            {
                // Parry successful!
                hasParried = true;
                
                // Gain a stack for the parry
                GainStack();
                
                // Parry sound
                SoundEngine.PlaySound(SoundID.DD2_CrystalCartImpact with { Pitch = 0.5f }, Player.Center);
                
                // Golden parry burst effect
                for (int i = 0; i < 20; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                    Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y);
                    dust.noGravity = true;
                    dust.scale = 1.5f;
                }
                
                // Knockback player in opposite direction of the dash
                Player.velocity = -dashVelocity * 0.6f; // Reverse the dash direction
                
                // End the parry window since we successfully parried
                parryActiveTimer = 0;
                
                // Grant 0.15 seconds of immunity after parry
                Player.immune = true;
                Player.immuneTime = 9;
                
                return true; // Dodge the damage completely
            }
            
            return false; // Don't dodge
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            // If we have stacks (and not parrying), use one to halve damage
            if (ParryStacks > 0 && parryActiveTimer <= 0)
            {
                modifiers.FinalDamage *= 0.5f;
                
                // We'll consume the stack in OnHurt after damage is confirmed
            }
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            // Consume a stack if we had one (damage was halved in ModifyHurt)
            if (ParryStacks > 0)
            {
                ParryStacks--;
                
                // Visual feedback for consuming a stack
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

        public bool IsInParryWindow()
        {
            return parryActiveTimer > 0;
        }

        public void StartKick(Vector2 velocity)
        {
            isKicking = true;
            hasHitEnemy = false;
            hasParried = false;
            kickTimer = 0;
            dashVelocity = velocity; // Store the dash velocity for knockback calculations

            // Start the parry window - any damage during this window is completely negated
            parryActiveTimer = PARRY_WINDOW;
        }

        public void GainStack()
        {
            if (ParryStacks < MAX_PARRY_STACKS)
            {
                ParryStacks++;
            }
        }

        public void OnKickHitEnemy(NPC target)
        {
            if (!isKicking) return;
            
            hasHitEnemy = true;
            
            // Gain a stack on hit (up to 3)
            GainStack();
            
            // Stack gain sound
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Pitch = 0.3f, Volume = 0.7f }, Player.Center);

            // Grant immunity frames so player doesn't take damage after dashing into enemy
            Player.immuneTime = IMMUNITY_FRAMES_ON_HIT;
            Player.immune = true;

            // Knockback player backwards (like shield bash)
            Vector2 knockbackDir = (Player.Center - target.Center).SafeNormalize(Vector2.UnitX);
            float knockbackStrength = 8f;
            Player.velocity = knockbackDir * knockbackStrength;
        }

        private void EndKick()
        {
            isKicking = false;

            // Determine cooldown based on outcome
            if (hasParried)
            {
                // Parried - 1 second cooldown
                cooldownTimer = COOLDOWN_PARRY;
            }
            else if (hasHitEnemy)
            {
                // Hit enemy but no parry - 0.5 second cooldown
                cooldownTimer = COOLDOWN_HIT;
            }
            else
            {
                // Missed completely - 3 second cooldown
                cooldownTimer = COOLDOWN_MISS;
            }

            hasHitEnemy = false;
            hasParried = false;
            kickTimer = 0;
        }

        public void ForceEndKick()
        {
            if (isKicking)
            {
                EndKick();
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            ForceEndKick();
            ParryStacks = 0;
        }
    }
}
