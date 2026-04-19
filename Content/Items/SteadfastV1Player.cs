using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Items
{
    public class SteadfastV1Player : ModPlayer
    {
        // Air dash charges
        public const int MAX_AIR_DASHES = 5;
        public int AirDashesRemaining = MAX_AIR_DASHES;

        // Cooldown
        private int cooldownTimer = 0;
        private const int COOLDOWN_TICKS = 30; // 0.5s between dashes
        public bool IsOnCooldown => cooldownTimer > 0;
        public float CooldownProgress => (float)cooldownTimer / COOLDOWN_TICKS;

        // Dash state
        private bool isDashing = false;
        public bool IsDashing => isDashing;
        private int dashTimer = 0;
        private const int DASH_BURST = 4;       // ~0.07s forced full-speed burst
        private const int DASH_DECEL = 6;       // 0.1s deceleration to half momentum
        private const int DASH_TOTAL = 10;      // DASH_BURST + DASH_DECEL
        private Vector2 dashDirection = Vector2.Zero;
        private const float DASH_SPEED = 32f;

        // Parry window during dash
        private int parryActiveTimer = 0;
        private const int PARRY_WINDOW = 10; // Parry window covering entire dash
        public bool IsInParryWindow => parryActiveTimer > 0;

        // Invulnerability after enemy parry
        private const int PARRY_IMMUNITY_FRAMES = 30;

        // Track if grounded last frame for landing detection
        private bool wasGrounded = false;

        // Track if was touching arena wall last frame
        private bool wasTouchingArena = false;

        public override void ResetEffects()
        {
            if (parryActiveTimer > 0)
                parryActiveTimer--;
        }

        public override void PostUpdateRunSpeeds()
        {
            if (Player.HeldItem.ModItem is SteadfastV1 && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Integrity)
            {
                Player.maxFallSpeed = 100f;
            }
        }

        public override void PostUpdate()
        {
            if (cooldownTimer > 0)
                cooldownTimer--;

            bool isGrounded = Player.velocity.Y == 0f;

            // Reset air dashes on landing
            if (isGrounded && !wasGrounded)
            {
                AirDashesRemaining = MAX_AIR_DASHES;
            }
            wasGrounded = isGrounded;

            // Reset air dashes when touching any boss arena boundary
            bool touchingArena = IsTouchingArenaBoundary();
            if (touchingArena && !wasTouchingArena)
            {
                AirDashesRemaining = MAX_AIR_DASHES;
            }
            wasTouchingArena = touchingArena;

            // Handle dash movement
            if (isDashing)
            {
                dashTimer++;

                if (dashTimer <= DASH_BURST)
                {
                    // Burst phase: full speed, locked direction
                    Player.velocity = dashDirection * DASH_SPEED;
                }
                else if (dashTimer <= DASH_TOTAL)
                {
                    // Deceleration phase: lerp from full speed to half speed
                    float decelProgress = (float)(dashTimer - DASH_BURST) / DASH_DECEL;
                    float currentSpeed = MathHelper.Lerp(DASH_SPEED, DASH_SPEED * 0.5f, decelProgress);
                    Player.velocity = dashDirection * currentSpeed;
                }
                else
                {
                    EndDash();
                }

                if (isDashing)
                {
                    Player.gravity = 0f;
                    Player.fallStart = (int)(Player.position.Y / 16f);

                    // Dash VFX: trailing dust every frame
                    SpawnDashTrailDust();
                }
            }

            // Parry hostile projectiles during dash
            if (parryActiveTimer > 0 && isDashing)
                CheckProjectileParry();
        }

        private bool IsTouchingArenaBoundary()
        {
            if (!BossArenaSystem.IsPlayerLockedIn(Player.whoAmI))
                return false;

            const float EDGE_THRESHOLD = 4f;

            foreach (var box in BossArenaSystem.ActiveBoxes)
            {
                if (!box.LockedPlayers.Contains(Player.whoAmI))
                    continue;

                float left = box.Center.X - box.HalfWidth;
                float right = box.Center.X + box.HalfWidth;
                float top = box.Center.Y - box.HalfHeight;
                float bottom = box.Center.Y + box.HalfHeight;

                if (Player.position.X <= left + EDGE_THRESHOLD ||
                    Player.position.X + Player.width >= right - EDGE_THRESHOLD ||
                    Player.position.Y <= top + EDGE_THRESHOLD ||
                    Player.position.Y + Player.height >= bottom - EDGE_THRESHOLD)
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnDashTrailDust()
        {
            // Cyan wind streaks
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = Main.rand.NextVector2Circular(Player.width * 0.5f, Player.height * 0.5f);
                Dust streak = Dust.NewDustDirect(
                    Player.Center + offset, 0, 0, DustID.IceTorch,
                    -dashDirection.X * Main.rand.NextFloat(3f, 7f),
                    -dashDirection.Y * Main.rand.NextFloat(3f, 7f));
                streak.noGravity = true;
                streak.scale = 0.9f + Main.rand.NextFloat(0.5f);
                streak.fadeIn = 1.3f;
            }

            // Dark blue integrity sparks
            if (Main.rand.NextBool(2))
            {
                Dust spark = Dust.NewDustDirect(
                    Player.position, Player.width, Player.height,
                    DustID.BlueTorch,
                    -dashDirection.X * 2f, -dashDirection.Y * 2f);
                spark.noGravity = true;
                spark.scale = 1.5f;
            }
        }

        // --- DASH ---

        public void StartDash(Vector2 direction)
        {
            isDashing = true;
            dashTimer = 0;
            dashDirection = direction.SafeNormalize(Vector2.UnitX);
            parryActiveTimer = PARRY_WINDOW;

            // Consume an air dash charge if airborne
            if (Player.velocity.Y != 0f)
                AirDashesRemaining--;

            // Burst VFX at start position
            SpawnDashStartBurst();
        }

        private void SpawnDashStartBurst()
        {
            // Radial dust explosion at start
            for (int i = 0; i < 20; i++)
            {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 dustVel = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * Main.rand.NextFloat(3f, 6f);
                Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.4f + Main.rand.NextFloat(0.4f);
                dust.fadeIn = 1.5f;
            }

            // Concentrated burst in opposite direction from dash
            for (int i = 0; i < 10; i++)
            {
                Vector2 backVel = -dashDirection * Main.rand.NextFloat(4f, 10f) + Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustDirect(Player.Center, 0, 0, DustID.BlueTorch, backVel.X, backVel.Y);
                dust.noGravity = true;
                dust.scale = 1.6f;
            }
        }

        private void EndDash()
        {
            if (!isDashing)
                return;

            isDashing = false;
            dashTimer = 0;
            // Exit with half momentum in dash direction
            Player.velocity = dashDirection * DASH_SPEED * 0.5f;
            cooldownTimer = COOLDOWN_TICKS;
        }

        // --- PARRY (hostile projectiles) ---

        private void CheckProjectileParry()
        {
            Rectangle playerRect = Player.Hitbox;
            playerRect.Inflate(20, 20); // Generous parry hitbox

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile || proj.friendly)
                    continue;

                if (!playerRect.Intersects(proj.Hitbox))
                    continue;

                // Parry! Convert to friendly projectile
                proj.hostile = false;
                proj.friendly = true;
                proj.owner = Player.whoAmI;
                proj.velocity = -proj.velocity; // Reflect velocity
                proj.damage = (int)(proj.damage * 1.5f); // Boost reflected damage

                // Parry VFX - big flash burst
                SpawnParryBurst(proj.Center);

                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Parry") with { PitchVariance = 0.2f }, Player.Center);

                // Only parry one projectile per dash
                break;
            }
        }

        private void SpawnParryBurst(Vector2 position)
        {
            // Golden flash burst
            for (int i = 0; i < 25; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(position, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.6f + Main.rand.NextFloat(0.4f);
            }

            // White spark ring
            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 ringVel = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * 5f;
                Dust dust = Dust.NewDustDirect(position, 0, 0, DustID.WhiteTorch, ringVel.X, ringVel.Y);
                dust.noGravity = true;
                dust.scale = 1.3f;
                dust.fadeIn = 1.4f;
            }

            // Cyan integrity sparks
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(position, 0, 0, DustID.IceTorch, dustVel.X, dustVel.Y);
                dust.noGravity = true;
                dust.scale = 1.4f;
            }
        }

        // --- PARRY (enemies, called from dash projectile OnHitNPC) ---

        public void OnDashHitEnemy(NPC target)
        {
            if (!isDashing)
                return;

            // Reset air dashes on enemy hit
            AirDashesRemaining = MAX_AIR_DASHES;

            // Grant parry immunity
            Player.immune = true;
            Player.immuneTime = PARRY_IMMUNITY_FRAMES;

            // Parry VFX burst on hit
            SpawnParryBurst(target.Center);

            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Parry") with { PitchVariance = 0.2f }, Player.Center);

            // End dash
            EndDash();

            // Knockback player away from enemy
            Vector2 knockDir = (Player.Center - target.Center).SafeNormalize(Vector2.UnitX);
            Player.velocity = knockDir * 8f;
        }

        // --- FREE DODGE (enemy contact during parry window) ---

        public override bool FreeDodge(Player.HurtInfo info)
        {
            if (Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Integrity)
                return false;

            if (!(Player.HeldItem.ModItem is SteadfastV1))
                return false;

            if (parryActiveTimer > 0 && isDashing)
            {
                // Parried enemy contact during dash
                AirDashesRemaining = MAX_AIR_DASHES;

                SpawnParryBurst(Player.Center);

                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Parry") with { PitchVariance = 0.2f }, Player.Center);

                if (isDashing)
                {
                    Vector2 kickback = -dashDirection * 10f;
                    EndDash();
                    Player.velocity = kickback;
                }

                Player.immune = true;
                Player.immuneTime = PARRY_IMMUNITY_FRAMES;
                return true;
            }

            return false;
        }

        public override void Kill(double damage, int hitDirection, bool pvp, Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            isDashing = false;
            dashTimer = 0;
            cooldownTimer = 0;
            AirDashesRemaining = MAX_AIR_DASHES;
        }
    }
}
