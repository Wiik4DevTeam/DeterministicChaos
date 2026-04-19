using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.NPCs.DarkWorldEnemies
{
    public class ArmoredZombieBanner : ModItem
    {

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            ItemID.Sets.KillsToBanner[Type] = 100;
        }
        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<DarkWorldEnemyBanners>(), (int)DarkWorldEnemyBanners.StyleID.ArmoredZombie);
            Item.width = 10;
            Item.height = 24;
            Item.SetShopValues(ItemRarityColor.Blue1, Terraria.Item.buyPrice(silver: 10));
        }
    }
}