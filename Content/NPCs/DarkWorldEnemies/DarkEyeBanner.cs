using Terraria.Enums;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class DarkEyeBanner : ModItem
    {
        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<DarkWorldEnemyBanners>(), (int)DarkWorldEnemyBanners.StyleID.DarkEye);
            Item.width = 10;
            Item.height = 24;
            Item.SetShopValues(ItemRarityColor.Blue1, Terraria.Item.buyPrice(silver: 10));
        }
    }
}