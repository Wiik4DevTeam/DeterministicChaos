using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class QueenOfDiamonds : ModItem
    {
        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<QueenOfDiamondsProjectile>(), 30, 4f, 4f);
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
        }

        public override bool MeleePrefix() => true;
    }
}
