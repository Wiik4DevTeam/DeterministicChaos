using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Imbued;
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
    public class RoaringYoyoPlayer : ModPlayer
    {
        public ImbuedStaticVariant imbuedStaticVariant = ImbuedStaticVariant.None;
        public bool isHoldingStatic = false;
        public int perseverancePullCooldown = 0;

        public override void ResetEffects()
        {
            isHoldingStatic = false;
        }

        public override void PostUpdate()
        {
            if (!isHoldingStatic)
                imbuedStaticVariant = ImbuedStaticVariant.None;
            if (perseverancePullCooldown > 0)
                perseverancePullCooldown--;
        }

        // Perseverance right-click: pull all owned mini-stars toward the yoyo (or player if no yoyo)
        public void PerseverancePullStars()
        {
            int yoyoType = ModContent.ProjectileType<RoaringYoyoProjectile>();
            int starType = ModContent.ProjectileType<RoaringMiniStar>();

            Vector2 anchor = Player.Center;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == yoyoType)
                {
                    anchor = p.Center;
                    break;
                }
            }

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Player.whoAmI || p.type != starType)
                    continue;

                if (Vector2.Distance(p.Center, anchor) > 1000f)
                    continue;

                Vector2 dir = (anchor - p.Center).SafeNormalize(Vector2.Zero);
                p.velocity = dir * 14f;
                p.netUpdate = true;
            }
        }
    }
}
