using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Invisible short-lived hitbox projectile used by CascadeProjectile and TitansArrowProjectile
    // to deliver their explosion damage through the normal player damage pipeline (DPS meter,
    // crit chance, player hooks, etc.).
    //
    // ai[0] = explosion radius in pixels (e.g. 240 for Cascade, 110 for TitansArrow).
    // Spawn it centered on the explosion point; it lives for 3 ticks then vanishes.
    public class BoltExplosion : ModProjectile
    {
        // Reuse any existing texture, this projectile is never drawn.
        public override string Texture =>
            "DeterministicChaos/Content/Projectiles/Friendly/CascadeProjectile";

        private float Radius => Projectile.ai[0] > 0f ? Projectile.ai[0] : 240f;

        public override void SetDefaults()
        {
            Projectile.width              = 480;
            Projectile.height             = 480;
            Projectile.friendly           = true;
            Projectile.hostile            = false;
            Projectile.tileCollide        = false;
            Projectile.ignoreWater        = true;
            Projectile.penetrate          = -1;
            Projectile.timeLeft           = 3;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = -1;   // hit each NPC exactly once
            Projectile.DamageType         = DamageClass.Ranged;
        }

        // Restrict hits to a true circle matching the requested radius.
        public override bool? CanHitNPC(NPC target) =>
            Vector2.Distance(target.Center, Projectile.Center) <= Radius ? null : false;

        // Never draw anything.
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
