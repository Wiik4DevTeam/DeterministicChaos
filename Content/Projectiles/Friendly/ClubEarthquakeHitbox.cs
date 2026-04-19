using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    /// <summary>
    /// Invisible shockwave hitbox spawned alongside ClubEarthquakeProjectile.
    /// Deals full damage and launches enemies upward on contact.
    /// ai[0] = spawn delay (matches the visual club's delay).
    /// </summary>
    public class ClubEarthquakeHitbox : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/FriendlyClubProjectile";

        private const int ActiveTicks = 14;
        private const float LaunchSpeedY = -12f;

        private ref float SpawnDelay => ref Projectile.ai[0];
        private bool delayDone;
        private int activeAge;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // Hit each NPC only once
            Projectile.alpha = 255; // Invisible
        }

        public override void AI()
        {
            if (!delayDone)
            {
                Projectile.velocity = Vector2.Zero;
                if (SpawnDelay > 0f)
                {
                    SpawnDelay--;
                    return;
                }
                delayDone = true;
            }

            activeAge++;
            if (activeAge > ActiveTicks)
            {
                Projectile.Kill();
            }
        }

        public override bool? CanDamage()
        {
            return delayDone && activeAge > 0 && activeAge <= ActiveTicks ? null : false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Launch the enemy upward, nudge position to detach from ground
            if (!target.boss && target.knockBackResist > 0f)
            {
                target.position.Y -= 12f;
                target.velocity.Y = -16f;
                target.velocity.X *= 0.3f;
                target.netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            return false; // Never draw
        }
    }
}
