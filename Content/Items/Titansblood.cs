using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class Titansblood : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 1);
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, Color.White, 0.4f);
        }

        public override void PostUpdate()
        {
            Lighting.AddLight(Item.Center, 0.28f, 0.92f, 0.97f);
        }
    }
}
