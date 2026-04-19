using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses.Trophies
{
    public class JevilTrophy : ModItem
    {
        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<JevilTrophyTile>());
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 1);
        }
    }
}
