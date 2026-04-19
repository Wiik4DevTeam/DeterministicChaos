using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.Bosses.Trophies
{
    public class TitanRelic : ModItem
    {
        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<TitanRelicTile>());
            Item.width = 30;
            Item.height = 40;
            Item.rare = ItemRarityID.Master;
            Item.master = true;
            Item.value = Item.buyPrice(gold: 5);
        }
    }
}
