using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class TitanStarPlayer : ModPlayer
    {
        public bool TitanStarActive;

        public override void ResetEffects()
        {
            TitanStarActive = false;
        }

        public override void PostUpdate()
        {
            if (!Player.HasBuff(ModContent.BuffType<TitanStarBuff>()))
                return;

            TitanStarActive = true;

            if (Player.whoAmI != Main.myPlayer)
                return;

            int starType = ModContent.ProjectileType<TitanStarProjectile>();
            if (Player.ownedProjectileCounts[starType] <= 0)
            {
                Projectile.NewProjectile(
                    Player.GetSource_Misc("TitanStar"),
                    Player.Center,
                    Vector2.Zero,
                    starType,
                    0,
                    0f,
                    Player.whoAmI);
            }
        }
    }
}
