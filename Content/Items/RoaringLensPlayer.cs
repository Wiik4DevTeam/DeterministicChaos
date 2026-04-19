using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class RoaringLensPlayer : ModPlayer
    {
        // Set by RoaringLensBuff while the light pet is active.
        public bool RoaringLensActive;

        public override void ResetEffects()
        {
            RoaringLensActive = false;
        }

        public override void PostUpdate()
        {
            if (!RoaringLensActive || Player.whoAmI != Main.myPlayer)
                return;

            SpawnIfMissing();
        }

        private void SpawnIfMissing()
        {
            int sphereType = ModContent.ProjectileType<Projectiles.Friendly.KnightSphere>();
            int eyeType = ModContent.ProjectileType<Projectiles.Friendly.KnightEye>();

            if (Player.ownedProjectileCounts[sphereType] <= 0)
            {
                Projectile.NewProjectile(
                    Player.GetSource_Misc("RoaringLens"),
                    Player.Center,
                    Vector2.Zero,
                    sphereType,
                    0,
                    0f,
                    Player.whoAmI);
            }

            if (Player.ownedProjectileCounts[eyeType] <= 0)
            {
                Projectile.NewProjectile(
                    Player.GetSource_Misc("RoaringLens"),
                    Player.Center,
                    Vector2.Zero,
                    eyeType,
                    0,
                    0f,
                    Player.whoAmI);
            }
        }
    }
}
