using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class TitanForgeItem : ModItem
    {
        // Use the TitanForge.png sprite in the Items folder
        public override string Texture => "DeterministicChaos/Content/Items/TitanForge";
        
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.TitanForge>());
            Item.width = 28;
            Item.height = 14;
            Item.value = Item.sellPrice(gold: 2);
            Item.rare = ItemRarityID.LightPurple;
        }

        // No recipe, only found in Dark Worlds naturally
    }
}
