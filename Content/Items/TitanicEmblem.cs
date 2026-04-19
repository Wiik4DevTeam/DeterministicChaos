using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class TitanicEmblem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 18);
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TitanicEmblemPlayer>().hasTitanicEmblem = true;
        }
    }
}
