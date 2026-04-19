using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items.Sparks
{
    public class SparkOfKindness : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(silver: 25);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.material = true;
        }
    }
}
